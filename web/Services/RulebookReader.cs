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

/// <summary>
/// The book's own words about a Power, for somebody who is signed in.
///
/// <para><b>The text is not part of the site's payload and cannot be.</b> It is the publisher's
/// prose, held in this repository by permission granted to its owner; it is bundled into the
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

    /// <summary>
    /// What has already been asked, including the misses.
    ///
    /// <para>Caching the misses is the point: without it, every render of a Power with no entry
    /// of its own — about a fifth of them — is another round trip that will fail again. The
    /// cache is per visit and per scope, so signing out and back in re-asks.</para>
    /// </summary>
    private readonly Dictionary<string, PowerEntry?> _seen = new(StringComparer.OrdinalIgnoreCase);

    public RulebookReader(HttpClient http) => _http = http;

    /// <summary>The entry for a Power by name, or null when there is not one to show.</summary>
    public async Task<PowerEntry?> ForPowerAsync(string powerName)
    {
        if (string.IsNullOrWhiteSpace(powerName)) return null;

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
