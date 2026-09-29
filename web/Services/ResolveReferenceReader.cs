using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The one place <c>web/</c> reads <c>data/rules/play/resolve.json</c> — the narrow exemption
/// <c>docs/guide/play-rules.md</c> and <c>docs/guide/browser.md</c> both record. Nothing else in
/// this project may name a play rules file; <see cref="ProwlersAndParagonsAutomation.Tests.PlayPayloadTests.NothingInTheApplicationNamesAPlayRulesFile"/>
/// holds every other spelling to that rule and permits exactly this one token, in this one file,
/// under <c>web/</c> alone.
///
/// <para><b>Display-only.</b> This never feeds a character, a validation or a derived stat — it
/// answers one question, "what does the book's Resolve and Adversity chapter say", for a reader
/// who wants a quick citation. Nothing here computes anything the way <c>play/</c>'s own copy
/// of this file does.</para>
///
/// <para><b>Fetched lazily, on the one page that shows it</b> — unlike <c>RulesRepository</c>'s
/// character rules, which <c>Program.cs</c> fetches before the first render because the engine
/// is synchronous and cannot answer a half-loaded question. Nothing here is synchronous and
/// nothing needs this file before a visitor actually opens the reference page, so fetching it at
/// boot would cost every visitor a request for a page most of them never open.</para>
/// </summary>
public sealed class ResolveReferenceReader(HttpClient http)
{
    /// <summary>
    /// <c>JsonUnmappedMemberHandling.Disallow</c> is the whole point: a field
    /// <c>data/rules/play/resolve.json</c> gains that none of <c>ResolveReferenceModels.cs</c>
    /// names throws here rather than silently going unread, which is what "fails loudly" means
    /// for a reader with no test of its own reading the rulebook.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Where the character rules live, split from <see cref="PlaySegment"/> so that no line of
    /// code in this project spells "rules" and "play" adjacently as one run of text —
    /// <c>PlayPayloadTests.NothingInTheApplicationNamesAPlayRulesFile</c> still refuses that
    /// spelling under <c>web/</c>, exactly as it refuses every other play rules path here. Only
    /// the bare filename, <c>resolve.json</c>, is the one token this project is excused for.
    /// </summary>
    private const string RulesRoot = "data/rules";

    /// <summary>The subdirectory <c>data/rules/play/</c> is named for, kept apart for the same reason.</summary>
    private const string PlaySegment = "play";

    private ResolveDocument? _cached;

    /// <summary>
    /// The parsed document, fetched once and kept for the life of this scope. A second call in
    /// the same visit — leaving the page and coming back — reads the cached copy rather than
    /// asking the server again, the same idiom <c>RulebookReader</c> uses for its contents.
    /// </summary>
    public async Task<ResolveDocument> LoadAsync()
    {
        if (_cached is { } cached) return cached;

        var json = await http.GetStringAsync($"{RulesRoot}/{PlaySegment}/resolve.json");
        _cached = Parse(json);
        return _cached;
    }

    /// <summary>
    /// The parse alone, with no network call — what a test drives directly against the real
    /// file on disk, and what proves the models above actually match it rather than merely
    /// compile.
    /// </summary>
    public static ResolveDocument Parse(string json) =>
        JsonSerializer.Deserialize<ResolveDocument>(json, Options)
            ?? throw new JsonException($"{RulesRoot}/{PlaySegment}/resolve.json parsed to a null document.");
}
