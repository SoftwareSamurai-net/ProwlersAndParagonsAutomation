using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>A Power's entry as the book prints it.</summary>
/// <param name="Heading">The name in the book, which is not always cased as the rules data has it.</param>
/// <param name="PrintedPage">The printed page, so a reader can check it against their own copy.</param>
/// <param name="Text">The entry itself, verbatim.</param>
/// <param name="SourceRef">Which chapter and pages the text came from.</param>
public sealed record PowerEntry(string Heading, int PrintedPage, string Text, string SourceRef);

/// <summary>One chapter, and how much of it there is.</summary>
/// <param name="Chapter">Its number in the book.</param>
/// <param name="Title">Its printed title.</param>
/// <param name="PrintedPages">First and last printed page.</param>
/// <param name="SourceRef">The citation the chapter carries.</param>
/// <param name="Sections">How many passages it holds.</param>
public sealed record RulebookChapter(
    int Chapter,
    string Title,
    IReadOnlyList<int> PrintedPages,
    string SourceRef,
    int Sections);

/// <summary>What there is to read.</summary>
public sealed record RulebookContents(IReadOnlyList<RulebookChapter> Chapters, int Sections);

/// <summary>
/// One passage a search turned up, with enough to say why it is here.
/// </summary>
/// <param name="Chapter">Which chapter, for fetching it in full.</param>
/// <param name="ChapterTitle">That chapter's printed title.</param>
/// <param name="Index">Where in the chapter, for fetching it in full.</param>
/// <param name="Heading">The heading the book sets it under.</param>
/// <param name="PrintedPage">The printed page, so it can be checked against a real copy.</param>
/// <param name="SourceRef">The chapter's citation.</param>
/// <param name="MatchedTerms">Which of the reader's words this passage uses.</param>
/// <param name="MatchedHeading">Whether one of them is in the heading rather than the body.</param>
/// <param name="Snippet">A window of the passage around the word that matched.</param>
public sealed record RulebookResult(
    int Chapter,
    string ChapterTitle,
    int Index,
    string Heading,
    int PrintedPage,
    string SourceRef,
    IReadOnlyList<string> MatchedTerms,
    bool MatchedHeading,
    string Snippet);

/// <summary>
/// How this app cites a passage: the chapter, and the page a paper copy prints it on.
///
/// <para><b>One spelling, because two surfaces print it.</b> The rules reference puts it in a
/// row's cost slot and the command palette puts it in a row's detail line — and a citation
/// reading <c>Ch.4 p.62</c> on one screen and something else on the other would be the app
/// disagreeing with itself about where a rule is, on the one figure the whole page exists to let
/// somebody check against the book on the table.</para>
///
/// <para><b>A static beside the record rather than a property on it</b>, because
/// <see cref="RulebookResult"/> is the wire shape — what the server sent, field for field — and a
/// computed member on it is one more thing a reader has to decide came from the server or from
/// here.</para>
/// </summary>
public static class RulebookCitation
{
    /// <summary>The citation for one result, as both surfaces print it.</summary>
    public static string For(RulebookResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return FormattableString.Invariant($"Ch.{result.Chapter} p.{result.PrintedPage}");
    }
}

/// <summary>
/// What a search found, and how.
/// </summary>
/// <param name="Query">What was asked, as it was asked.</param>
/// <param name="Terms">The words it was actually searched on, filler dropped.</param>
/// <param name="Found">How many passages matched — <b>not</b> how many are listed.</param>
/// <param name="NothingMatchedByHeading">
/// Whether every match was in a body rather than a heading. It says how the results matched and
/// never what to conclude: only <see cref="Found"/> at zero means the book is silent.
/// </param>
/// <param name="Results">The best of them, capped by the server.</param>
public sealed record RulebookResults(
    string Query,
    IReadOnlyList<string> Terms,
    int Found,
    bool NothingMatchedByHeading,
    IReadOnlyList<RulebookResult> Results);

/// <summary>One passage, in full.</summary>
public sealed record RulebookPassage(
    int Chapter,
    string ChapterTitle,
    int Index,
    string Heading,
    int PrintedPage,
    string Text,
    string SourceRef);

/// <summary>
/// The book's own words about a Power, for somebody who is signed in.
///
/// <para><b>The text is not part of the site's payload and cannot be.</b> It is the publisher's
/// prose; it is bundled into the
/// server rather than copied into <c>wwwroot</c>, because a file under <c>wwwroot</c> is a
/// public URL and no amount of checking sessions in the browser would make it not be one. So
/// this asks the server, every entry, and the server asks who is calling.</para>
///
/// <para><b>Where the two stores disagree, <c>data/rules</c> wins.</b> Nothing on this path
/// feeds a cost, a rank or a validity — the engine has never heard of it. What arrives here is
/// shown beside the mechanics, never instead of them.</para>
///
/// <para><b>Nothing here throws, and a miss is ordinary.</b> Super Senses' sixteen options,
/// Form's and Transformation's, are separate entries in the rules data sharing one printed
/// entry between them, so a good fraction of the 141 Powers have no heading of their own. So
/// does everybody who is not signed in. All of it answers null, and the component shows
/// nothing.</para>
/// </summary>
public sealed class RulebookReader
{
    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;
    private readonly IIdentitySource _who;

    /// <summary>
    /// What has already been asked, including the misses.
    ///
    /// <para>Caching the misses is the point: without it, every render of a Power with no entry
    /// of its own — about a fifth of them — is another round trip that will fail again.</para>
    /// </summary>
    private readonly Dictionary<string, PowerEntry?> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whose answers <see cref="_seen"/> currently holds, or null before the first ask.
    ///
    /// <para><b>This is the whole of the fix for a real leak, and the comment it replaced claimed
    /// the opposite.</b> It said the cache was "per visit and per scope, so signing out and back
    /// in re-asks" — which is false: <b>Blazor WebAssembly has one DI scope for the life of the
    /// app</b>, so a scoped service is a singleton here, and signing out is pure SPA state with
    /// no reload. So a signed-in visitor who opened a Power's entry on a shared machine, then
    /// signed out, could open the same Power again and be shown the book's own text from this
    /// dictionary — never asking the server, which would have refused. Reproduced, then fixed.</para>
    ///
    /// <para><b>Comparing the key rather than listening for a change is deliberate.</b>
    /// <see cref="Accounts"/> does raise an event, but a guarantee that depends on an event being
    /// raised is a guarantee somebody can remove by editing another file. This cannot be wrong
    /// about who is asking, because it asks.</para>
    /// </summary>
    private string? _cachedFor;

    public RulebookReader(HttpClient http, IIdentitySource who)
    {
        _http = http;
        _who = who;
    }

    /// <summary>The entry for a Power by name, or null when there is not one to show.</summary>
    public async Task<PowerEntry?> ForPowerAsync(string powerName)
    {
        if (string.IsNullOrWhiteSpace(powerName)) return null;

        // Emptied rather than partitioned by key: nothing wants yesterday's account's answers
        // back, and leaving the book's text in memory after a sign-out is the thing to avoid.
        var who = await _who.CurrentAsync();
        if (_cachedFor != who.Key)
        {
            _seen.Clear();
            _cachedFor = who.Key;
        }

        if (_seen.TryGetValue(powerName, out var already)) return already;

        var entry = await AskAsync(powerName);
        _seen[powerName] = entry;

        return entry;
    }

    private async Task<PowerEntry?> AskAsync(string powerName)
    {
        try
        {
            using var response = await _http.GetAsync(
                "api/rulebook/power?name=" + Uri.EscapeDataString(powerName));

            // 401 for anybody not signed in, 404 for a Power the book files under another
            // heading. Neither is a fault and neither is worth a word on screen.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) return null;

            var entry = await response.Content.ReadFromJsonAsync<PowerEntry>(Wire);

            // A site deployed without its server answers this address with the app's own
            // index.html and a 200, so an answer is not proof of an answer.
            return string.IsNullOrWhiteSpace(entry?.Text) ? null : entry;
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>
    /// How many passages this account may search, or null if it may not search at all.
    ///
    /// <para>Asked of the server rather than counted here. What is readable is the server's
    /// answer — the book is bundled into it and never staged into the site's own files — so a
    /// figure kept on this side would be a second one, free to disagree with the first.</para>
    /// </summary>
    public async Task<int?> CountAsync()
    {
        var contents = await ContentsAsync();
        return contents?.Sections;
    }

    /// <summary>What there is to read, or null for anybody who may not.</summary>
    public async Task<RulebookContents?> ContentsAsync() =>
        await AskForAsync<RulebookContents>("api/rulebook/contents");

    /// <summary>
    /// What the book says about a query, best first.
    ///
    /// <para><b>The flags come back untouched and are not interpreted here.</b>
    /// <see cref="RulebookResults.Found"/> at zero is the only answer that means the book is
    /// silent; <see cref="RulebookResults.NothingMatchedByHeading"/> says every passage matched
    /// in its body, which is ordinary for a question phrased as a question. The Powers search
    /// shipped a version that turned the second into the first on screen, and a reader was told
    /// the rulebook had nothing while a dozen real passages sat under the sentence.</para>
    /// </summary>
    /// <param name="query">What to look for.</param>
    /// <param name="chapter">
    /// One chapter to look in, or null for the whole book.
    ///
    /// <para><b>The server does the narrowing, and it has to.</b> Keeping only the rows from one
    /// chapter out of the answer would be filtering what survived a cap the caller cannot raise,
    /// so a chapter with real matches outside the server's best thirty would come back empty —
    /// and <see cref="RulebookResults.Found"/> would be a count of the whole book presented as a
    /// count of the chapter.</para>
    /// </param>
    /// <param name="limit">
    /// How many rows to send back, or null for as many as the server is willing to.
    ///
    /// <para><b>Asking for fewer is safe and asking for more is not.</b> The server's own
    /// <c>MOST_RESULTS</c> caps this and a caller cannot raise it — see
    /// <c>docs/guide/accounts-server.md</c> — so this only ever narrows. It is worth asking for
    /// because <see cref="RulebookResults.Found"/> is computed over the whole ranking and only
    /// then is the list cut, so a short answer still says truthfully how many passages matched:
    /// the palette shows five rows, and thirty passages and their snippets is a large answer to
    /// send for one keystroke.</para>
    /// </param>
    public async Task<RulebookResults?> SearchAsync(string query, int? chapter = null, int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = chapter is { } number
            ? FormattableString.Invariant($"&chapter={number}")
            : "";

        var most = limit is { } rows
            ? FormattableString.Invariant($"&limit={rows}")
            : "";

        return await AskForAsync<RulebookResults>(
            "api/rulebook/search?q=" + Uri.EscapeDataString(query) + scope + most);
    }

    /// <summary>One passage in full, by the address a result carries.</summary>
    public async Task<RulebookPassage?> PassageAsync(int chapter, int index) =>
        await AskForAsync<RulebookPassage>(
            FormattableString.Invariant($"api/rulebook/passage?chapter={chapter}&index={index}"));

    /// <summary>
    /// One GET that answers with a body or with nothing, in the same four refusals the Power
    /// lookup already treats as ordinary.
    ///
    /// <para>Shared rather than written out three times, because the trap is the one the Power
    /// lookup already carries a comment about: a site deployed without its server answers every
    /// address with the app's own page and a 200, so a successful status is not proof of an
    /// answer and the body has to be read.</para>
    /// </summary>
    private async Task<T?> AskForAsync<T>(string address) where T : class
    {
        try
        {
            using var response = await _http.GetAsync(address);

            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<T>(Wire);
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>Every way the server can fail to answer. All of them mean "show nothing".</summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException
          or TaskCanceledException
          or OperationCanceledException
          or ObjectDisposedException
          or JsonException
          or NotSupportedException
          or InvalidOperationException;
}
