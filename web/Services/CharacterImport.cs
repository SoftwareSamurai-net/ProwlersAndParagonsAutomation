using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Why a file a person picked could not become a character — distinguished only as far as
/// the message they are shown needs it to be.
/// </summary>
public enum ImportProblem
{
    /// <summary>Not JSON at all — the wrong kind of file, or nothing this program wrote.</summary>
    NotJson,

    /// <summary>
    /// Well-formed JSON, but not this program's character shape — the printed report this
    /// app already writes, someone else's data entirely, or a field this build no longer
    /// recognises.
    /// </summary>
    NotACharacter,

    /// <summary>
    /// A character shape the strict reader accepts, but one the rules cannot answer
    /// questions about — a selection that overflows the arithmetic, most likely.
    /// </summary>
    Unpriceable
}

/// <summary>
/// One attempt to import a character from a file's text.
///
/// <para><b>Never thrown</b> — see <see cref="CharacterImport"/>. <see cref="Imported"/> is
/// the shape a page wants, in exactly the form <c>ICharacterStore.LoadAsync</c> already
/// returns, so a caller that only wants the character needs nothing else here.
/// <see cref="Problem"/> and <see cref="Message"/> exist for whoever has to explain a
/// refusal to the person who picked the file.</para>
/// </summary>
public sealed class ImportResult
{
    private ImportResult(CharacterSheet? sheet, SheetMode mode, ImportProblem? problem, string? message)
    {
        Imported = sheet is null ? null : (sheet, mode);
        Problem = problem;
        Message = message;
    }

    public (CharacterSheet Sheet, SheetMode Mode)? Imported { get; }

    public ImportProblem? Problem { get; }

    /// <summary>A sentence a person can act on. Null on success.</summary>
    public string? Message { get; }

    /// <summary>
    /// The mode follows <see cref="CharacterSheet.IsVillain"/> rather than being asked for —
    /// the field now travels with the character (see <c>CharacterSheet.IsVillain</c>'s own
    /// remarks), so a Villain read back from a file stays a Villain without this class having
    /// an opinion of its own.
    /// </summary>
    internal static ImportResult Ok(CharacterSheet sheet) =>
        new(sheet, sheet.IsVillain ? SheetMode.Villain : SheetMode.Hero, null, null);

    internal static ImportResult Failed(ImportProblem problem, string message) =>
        new(null, SheetMode.Hero, problem, message);
}

/// <summary>
/// Reads a character back out of a file somebody picked — the other half of "Download as
/// data" on the review step, which this program already writes and nothing yet reads back.
///
/// <para><b>Accepts only this program's own character shape</b>, read with
/// <see cref="CharacterSheetJson.Read"/> in its <b>strict</b> mode. Strict is the point: a
/// misspelled field in a file somebody submits is not a small error — the lenient reader
/// would drop it silently and hand back a cheaper, legal character nobody notices is wrong.
/// Lenient reading is for this program's own storage restoring itself after a later build
/// removed a field; a file picked off somebody's own disk gets told when it does not
/// match, which is exactly the split <c>CharacterSheetJson</c> documents.</para>
///
/// <para><b>This is not the printed export.</b> <c>CharacterSheetRenderer.RenderText</c> and
/// <c>RenderJson</c> are reports — derived stats, costs and validation findings, all of
/// them answers rather than inputs — and reading one back would rebuild a character out of
/// its own conclusions. Both are refused here the same way anything else that is not this
/// program's character shape is refused: as <see cref="ImportProblem.NotACharacter"/>.</para>
///
/// <para><b>Nothing here throws.</b> A bad file is an ordinary outcome — see
/// <see cref="ImportResult"/> — because the budget bar renders on every route, so a
/// character this build cannot answer questions about must never reach a page. The check
/// below is the one <see cref="StoredCharacter"/> already makes for local storage, read
/// and reused rather than shared: the two want opposite answers about a misspelled field
/// (this one refuses; storage repairs and keeps going), so the strict/lenient choice has
/// to live at the call site, but the "can the rules actually answer for this" question is
/// the same question both times.</para>
/// </summary>
public sealed class CharacterImport
{
    private readonly CostCalculator _costs;
    private readonly CharacterValidator _validator;

    public CharacterImport(CostCalculator costs, CharacterValidator validator)
    {
        _costs = costs;
        _validator = validator;
    }

    /// <summary>Reads a character from a file's text, or explains why it could not.</summary>
    public ImportResult Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ImportResult.Failed(ImportProblem.NotJson, "That file is empty.");

        if (!TryParse(json, out var root))
            return ImportResult.Failed(ImportProblem.NotJson,
                "That doesn't look like a character's data file — pick the file this app "
                + "exported, not something else.");

        if (root.ValueKind != JsonValueKind.Object)
            return ImportResult.Failed(ImportProblem.NotACharacter,
                "That file's data doesn't describe a character this app recognises.");

        CharacterSheet? sheet;
        try
        {
            sheet = CharacterSheetJson.Read(json, strict: true);
        }
        catch (Exception e) when (IsNotACharacter(e))
        {
            return ImportResult.Failed(ImportProblem.NotACharacter,
                "That file's data doesn't describe a character this app recognises. If it "
                + "came from here, use the character's own exported data rather than the "
                + "printed sheet or a report about it.");
        }

        if (sheet is null)
            return ImportResult.Failed(ImportProblem.NotACharacter,
                "That file's data doesn't describe a character this app recognises.");

        if (!CanBeUsed(sheet))
            return ImportResult.Failed(ImportProblem.Unpriceable,
                "This character has a selection the rules can't make sense of, so it can't "
                + "be imported as it stands. Something in it is likely out of range for a "
                + "build this one can price.");

        return ImportResult.Ok(sheet);
    }

    /// <summary>
    /// Whether the rules can actually answer questions about this sheet — asked by asking,
    /// once, before anything is handed to a caller. Mirrors
    /// <c>StoredCharacter.CanBeUsed</c>: a selection that is merely incomplete (a
    /// variable-cost Power with no variant yet) throws <see cref="InvalidOperationException"/>,
    /// which <see cref="CharacterSession.TryCost"/> already swallows, and stays importable —
    /// that is a half-finished character, not a corrupt one. What is caught here is the
    /// engine genuinely failing to answer, which the arithmetic overflow guarded by
    /// <c>CostCalculator.TotalCost</c>'s own <c>checked</c> block is a real example of: a
    /// selection can name a quantity large enough that pricing it overflows an
    /// <see cref="int"/> before the validator ever sees it.
    /// </summary>
    private bool CanBeUsed(CharacterSheet sheet)
    {
        try
        {
            CharacterSession.TryCost(() => _costs.TotalCost(sheet));
            _validator.Validate(sheet);
            return true;
        }
        catch (Exception e) when (e is NullReferenceException or ArgumentException
                                    or KeyNotFoundException or FormatException or OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// True for the failures that mean "well-formed JSON, but not this program's character" —
    /// an unmapped field under the strict reader, or a value of the wrong shape for the
    /// field it is in. <see cref="Read"/> has already ruled out "not JSON at all" by the
    /// time this is asked, and a genuinely unpriceable character is caught separately once
    /// a <see cref="CharacterSheet"/> exists to ask the rules about.
    /// </summary>
    private static bool IsNotACharacter(Exception e) =>
        e is JsonException or NotSupportedException or ArgumentException or FormatException or OverflowException;

    private static bool TryParse(string json, out JsonElement root)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            root = default;
            return false;
        }
    }
}
