using System.Text.Json;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Keeps the character in the browser's local storage, so a refresh, a bookmark or a
/// shared deep link does not throw it away.
///
/// <para>It used to. The sheet lived in a scoped <see cref="CharacterSession"/> and nowhere
/// else, so reloading the page — or opening a link someone sent you, which
/// <c>_redirects</c> deliberately serves with a 200 precisely so links <em>can</em> be
/// shared — silently dropped twenty minutes of work and landed on "Choose a tier first".
/// </para>
///
/// <para><b>The saved shape is <see cref="CharacterSheet"/> itself, not the JSON export.</b>
/// The export is a report: it carries derived stats, costs and validation findings, all of
/// which are answers rather than inputs, and reading it back would mean re-deriving a
/// character from its own conclusions. The sheet is the inputs, and it round-trips.</para>
///
/// <para><b>Nothing here may throw, and "nothing" is stricter than it looks.</b> A saved
/// character from an older build, hand-edited storage, and a browser that refuses local
/// storage are all the same case: start empty. The traps are that the deserializer hands
/// back <c>null</c> for a property whose declared type is non-nullable, and an out-of-range
/// number for an enum — neither of which the compiler can warn about, and both of which turn
/// "your character is gone" into "the app does not start", because restoring happens before
/// the first render. <see cref="Usable"/> is where that is caught.</para>
/// </summary>
public sealed class CharacterStore
{
    /// <summary>
    /// Bumped when a change to <see cref="CharacterSheet"/> would make an older saved
    /// character restore wrongly rather than merely incompletely. A mismatch is discarded in
    /// silence — the alternative is a character that looks right and is not.
    /// </summary>
    private const int CurrentVersion = 1;

    private const string StorageKey = "pp.character.v1";

    private readonly IJSRuntime _js;
    private readonly CostCalculator _costs;
    private readonly CharacterValidator _validator;

    public CharacterStore(IJSRuntime js, CostCalculator costs, CharacterValidator validator)
    {
        _js = js;
        _costs = costs;
        _validator = validator;
    }

    /// <summary>
    /// The engine's own options for the sheet shape, shared with the headless <c>build</c>
    /// command. Populate rather than replace, because <see cref="CharacterSheet"/> exposes
    /// its collections as get-only properties with initialisers — the shape the engine
    /// wants, and one that a deserializer has to be told to fill rather than assign.
    /// </summary>
    private static JsonSerializerOptions Options => CharacterSheetJson.Options;

    private sealed record Saved(int Version, SheetMode Mode, CharacterSheet? Sheet);

    /// <summary>Writes the character to local storage. Failure is not worth reporting.</summary>
    public async Task SaveAsync(CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            var json = JsonSerializer.Serialize(new Saved(CurrentVersion, mode, sheet), Options);
            await _js.InvokeVoidAsync("ppStore.save", StorageKey, json);
        }
        catch (Exception e) when (IsStorageFailure(e)) { }
    }

    /// <summary>The stored character, or null if there is none this build can trust.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("ppStore.load", StorageKey);
            if (string.IsNullOrWhiteSpace(json)) return null;

            if (Read(json) is { } restored) return restored;

            // Storage this build cannot use is removed rather than left. Left in place it is
            // re-read and re-rejected on every visit, and if it ever gets past a guard the
            // failure repeats forever with no way out from inside the app.
            await ClearAsync();
            return null;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return null; }
    }

    /// <summary>Forgets the stored character. What "Start a new character" actually does.</summary>
    public async Task ClearAsync()
    {
        try { await _js.InvokeVoidAsync("ppStore.clear", StorageKey); }
        catch (Exception e) when (IsStorageFailure(e)) { }
    }

    /// <summary>Reads a stored payload. Internal so the tests can feed it malformed storage.</summary>
    internal (CharacterSheet Sheet, SheetMode Mode)? Read(string json)
    {
        try
        {
            if (Usable(JsonSerializer.Deserialize<Saved>(json, Options)) is not { } restored) return null;

            return CanBeUsed(restored.Sheet) ? restored : null;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return null; }
    }

    /// <summary>
    /// Whether the engine can actually answer questions about this sheet — asked by asking
    /// it, once, before the app is allowed to render against it.
    ///
    /// <para><b>This is the guard, and the null-stripping below is only tidying.</b> The
    /// shape of a stored character is nested several levels deep — a Power holds Pros, a Pro
    /// holds a variant key, a piece of gear holds features — and the deserializer will put a
    /// null at any of those depths without the type system objecting. Stripping them level by
    /// level means a list that goes stale the moment somebody adds a field, and they will not
    /// think about this file when they do.</para>
    ///
    /// <para>The consequence of missing one is not a bad sheet, it is a dead app: the budget
    /// bar renders on every route, so a payload that costs badly takes the whole page down on
    /// the first frame — after <c>Program.cs</c>'s backstop has already been passed.</para>
    ///
    /// <para><see cref="InvalidOperationException"/> is deliberately <b>not</b> caught here.
    /// That is what the engine throws for a selection it cannot price yet — a variable-cost
    /// Power with no variant — which is a legitimate half-finished character, not corruption,
    /// and discarding one would lose exactly the work this class exists to keep.</para>
    /// </summary>
    private bool CanBeUsed(CharacterSheet sheet)
    {
        try
        {
            CharacterSession.TryCost(() => _costs.TotalCost(sheet));
            _validator.Validate(sheet);
            return true;
        }
        catch (Exception e) when (e is NullReferenceException or ArgumentException or KeyNotFoundException
                                    or FormatException or OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether a payload can be handed to the app, and the repair it needs first.
    ///
    /// <para>Three things the type system promises and the deserializer does not:</para>
    /// <list type="bullet">
    ///   <item><c>Sheet</c> is declared non-nullable and comes back null for
    ///     <c>{"Version":1}</c>. Restoring that threw <b>before the first render</b>, so the
    ///     app did not start at all — the exact failure this class claims to prevent.</item>
    ///   <item><c>Mode</c> is an enum and accepts any number. <c>{"Mode":7}</c> gave a
    ///     character that was neither Hero nor Villain, with a palette that disagreed with
    ///     every check made against it.</item>
    ///   <item>A <c>SelectedPower</c>'s <c>Pros</c> and <c>Cons</c> are declared non-null and
    ///     come back null when the keys are absent, which is a NullReferenceException on the
    ///     next render rather than anything a JSON catch would ever see. That one, and the
    ///     nulls inside the four lists and the four free-text fields, are repaired by
    ///     <see cref="CharacterSheetJson.Repair"/> — shared with the headless command,
    ///     because a second copy of this would be a second copy that goes stale.</item>
    /// </list>
    /// </summary>
    private static (CharacterSheet Sheet, SheetMode Mode)? Usable(Saved? saved)
    {
        if (saved is not { Version: CurrentVersion, Sheet: { } sheet }) return null;

        // dropIdlessEntries: an entry naming nothing is junk to a browser restoring its own
        // storage, and one lost entry is worth less than the character. The headless command asks
        // for the opposite, because there the same entry has to be reported rather than quietly
        // removed from somebody's submitted file.
        return (CharacterSheetJson.Repair(sheet, dropIdlessEntries: true),
                Enum.IsDefined(saved.Mode) ? saved.Mode : SheetMode.Hero);
    }

    /// <summary>
    /// Everything that can go wrong between here and the browser's storage. Every one means
    /// the same thing: there is no saved character, carry on without one.
    ///
    /// <para>Named rather than a bare <c>catch</c> so the list is reviewable, and wider than
    /// the obvious three because the alternative — an unobserved exception out of a
    /// fire-and-forget save — stops persistence silently and tells nobody.</para>
    /// </summary>
    private static bool IsStorageFailure(Exception e) =>
        e is JsonException                  // malformed or hand-edited storage
          or NotSupportedException          // a type the serializer cannot handle
          or JSException                    // the browser refused, or ppStore is missing
          or InvalidOperationException      // interop unavailable
          or ObjectDisposedException        // the host is going away
          or TaskCanceledException          // ditto, mid-call
          or ArgumentException              // an id or key the payload invented
          or OverflowException;             // a number no build can hold
}
