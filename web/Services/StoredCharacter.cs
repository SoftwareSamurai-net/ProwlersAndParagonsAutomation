using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// What a stored character actually is, on the wire and in local storage alike.
///
/// <para><b>Extracted so the browser's store and the account's store cannot disagree about
/// it.</b> They keep the character in two very different places, and the temptation is to let
/// each own its own envelope — at which point a version bump, or the null-repair below, lands
/// in one of them and not the other, and a character saved on a laptop restores wrongly on a
/// phone. There is one reader and one writer, here.</para>
///
/// <para><b>Nothing here throws.</b> A character from an older build, hand-edited storage, and
/// a payload that arrived down a wire are the same case to a caller: there is no character,
/// start empty. Reading happens before the first render, so an exception is not a lost
/// character but an app that does not start.</para>
/// </summary>
public sealed class StoredCharacter
{
    /// <summary>
    /// Bumped when a change to <see cref="CharacterSheet"/> would make an older saved character
    /// restore wrongly rather than merely incompletely. A mismatch is discarded in silence —
    /// the alternative is a character that looks right and is not.
    /// </summary>
    private const int CurrentVersion = 1;

    private readonly CostCalculator _costs;
    private readonly CharacterValidator _validator;

    public StoredCharacter(CostCalculator costs, CharacterValidator validator)
    {
        _costs = costs;
        _validator = validator;
    }

    /// <summary>
    /// The engine's own options for the sheet shape, shared with the headless <c>build</c>
    /// command. Populate rather than replace, because <see cref="CharacterSheet"/> exposes its
    /// collections as get-only properties with initialisers.
    /// </summary>
    private static JsonSerializerOptions Options => CharacterSheetJson.Options;

    private sealed record Saved(int Version, SheetMode Mode, CharacterSheet? Sheet);

    /// <summary>
    /// The character as it is written down — the inputs, never the export.
    ///
    /// <para>The export is a report: it carries derived stats, costs and validation findings,
    /// all of which are answers rather than inputs, and reading one back would rebuild a
    /// character from its own conclusions.</para>
    /// </summary>
    public static string Write(CharacterSheet sheet, SheetMode mode) =>
        JsonSerializer.Serialize(new Saved(CurrentVersion, mode, sheet), Options);

    /// <summary>A stored payload, or null if there is none this build can trust.</summary>
    public (CharacterSheet Sheet, SheetMode Mode)? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            if (Usable(JsonSerializer.Deserialize<Saved>(json, Options)) is not { } restored) return null;

            return CanBeUsed(restored.Sheet) ? restored : null;
        }
        catch (Exception e) when (IsUnreadable(e)) { return null; }
    }

    /// <summary>
    /// Whether the engine can actually answer questions about this sheet — asked by asking it,
    /// once, before the app is allowed to render against it.
    ///
    /// <para><b>This is the guard, and the null-repair below is only tidying.</b> The shape of
    /// a stored character is nested several levels deep — a Power holds Pros, a Pro holds a
    /// variant key, a piece of gear holds features — and the deserializer will put a null at
    /// any of those depths without the type system objecting. Checking a list of shapes goes
    /// stale the moment somebody adds a field, and they will not think about this file when
    /// they do.</para>
    ///
    /// <para>The consequence of missing one is not a bad sheet, it is a dead app: the budget bar
    /// renders on every route, so a payload that costs badly takes the whole page down on the
    /// first frame.</para>
    ///
    /// <para><see cref="InvalidOperationException"/> is deliberately <b>not</b> caught. That is
    /// what the engine throws for a selection it cannot price yet — a variable-cost Power with
    /// no variant — which is a legitimate half-finished character, not corruption, and
    /// discarding one would lose exactly the work this class exists to keep.</para>
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
    ///     <c>{"Version":1}</c>. Restoring that threw <b>before the first render</b>, so the app
    ///     did not start at all — the exact failure this class claims to prevent.</item>
    ///   <item><c>Mode</c> is an enum and accepts any number. <c>{"Mode":7}</c> gave a character
    ///     that was neither Hero nor Villain, with a palette that disagreed with every check
    ///     made against it.</item>
    ///   <item>A <c>SelectedPower</c>'s <c>Pros</c> and <c>Cons</c> are declared non-null and
    ///     come back null when the keys are absent, which is a NullReferenceException on the
    ///     next render rather than anything a JSON catch would ever see. That one, and the nulls
    ///     inside the four lists and the four free-text fields, are repaired by
    ///     <see cref="CharacterSheetJson.Repair"/> — shared with the headless command, because a
    ///     second copy of this would be a second copy that goes stale.</item>
    /// </list>
    /// </summary>
    private static (CharacterSheet Sheet, SheetMode Mode)? Usable(Saved? saved)
    {
        if (saved is not { Version: CurrentVersion, Sheet: { } sheet }) return null;

        // dropIdlessEntries: an entry naming nothing is junk to a browser restoring its own
        // storage, and one lost entry is worth less than the character. The headless command
        // asks for the opposite, because there the same entry has to be reported rather than
        // quietly removed from somebody's submitted file.
        return (CharacterSheetJson.Repair(sheet, dropIdlessEntries: true),
                Enum.IsDefined(saved.Mode) ? saved.Mode : SheetMode.Hero);
    }

    /// <summary>
    /// Everything that can go wrong reading a payload. Every one means the same thing: there is
    /// no saved character, carry on without one.
    ///
    /// <para>Named rather than a bare <c>catch</c> so the list is reviewable.</para>
    /// </summary>
    private static bool IsUnreadable(Exception e) =>
        e is JsonException                  // malformed, hand-edited, or an HTML error page
          or NotSupportedException          // a type the serializer cannot handle
          or ArgumentException              // an id or key the payload invented
          or OverflowException;             // a number no build can hold
}
