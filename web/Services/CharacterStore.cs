using System.Text.Json;
using System.Text.Json.Serialization;
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
/// <para>Nothing here may throw. A saved character from an older build, hand-edited storage,
/// or a browser that refuses local storage entirely are all the same case: start empty. A
/// tool that will not open because of something it wrote itself is worse than one that
/// forgets.</para>
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

    public CharacterStore(IJSRuntime js) => _js = js;

    /// <summary>
    /// Populate rather than replace, because <see cref="CharacterSheet"/> exposes its
    /// collections as get-only properties with initialisers — the shape the engine wants,
    /// and one that a deserializer has to be told to fill rather than assign.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record Saved(int Version, SheetMode Mode, CharacterSheet Sheet);

    /// <summary>Writes the character to local storage. Failure is not worth reporting.</summary>
    public async Task SaveAsync(CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            var json = JsonSerializer.Serialize(new Saved(CurrentVersion, mode, sheet), Options);
            await _js.InvokeVoidAsync("ppStore.save", StorageKey, json);
        }
        catch (JsonException) { }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary>The stored character, or null if there is none this build can trust.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("ppStore.load", StorageKey);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var saved = JsonSerializer.Deserialize<Saved>(json, Options);

            return saved is { Version: CurrentVersion }
                ? (saved.Sheet, saved.Mode)
                : null;
        }
        catch (JsonException) { return null; }
        catch (JSException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public async Task ClearAsync()
    {
        try { await _js.InvokeVoidAsync("ppStore.clear", StorageKey); }
        catch (JSException) { }
    }

    /// <summary>
    /// Round-trips a sheet through the same serializer the browser uses, without a browser.
    /// The tests use it to prove that everything a player can put on a sheet survives —
    /// which is the only part of this that can go quietly wrong.
    /// </summary>
    public static CharacterSheet? RoundTrip(CharacterSheet sheet, SheetMode mode = SheetMode.Hero)
    {
        var json = JsonSerializer.Serialize(new Saved(CurrentVersion, mode, sheet), Options);
        return JsonSerializer.Deserialize<Saved>(json, Options)?.Sheet;
    }

    /// <summary>
    /// A sheet as the stored text, for tests that want to compare two of them without
    /// naming every field — a field list is the first thing to go stale.
    /// </summary>
    public static string Describe(CharacterSheet sheet) => JsonSerializer.Serialize(sheet, Options);
}
