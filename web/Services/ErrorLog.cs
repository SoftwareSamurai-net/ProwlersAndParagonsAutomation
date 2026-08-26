using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// One recorded failure, folded by (category, route) the way the server stores it.
/// </summary>
/// <param name="Category">Which subsystem — the same closed set <see cref="Accounts"/> renders.</param>
/// <param name="Route">A route pattern from the server's fixed list, or "other".</param>
/// <param name="Kind">The exception's type, such as "TypeError". Always safe to keep.</param>
/// <param name="Detail">As much of the message as the server's redaction leaves, or null.</param>
/// <param name="Occurrences">How many failures this row stands for, including dropped ones.</param>
/// <param name="FirstAt">When this run of failures started, in milliseconds since the epoch.</param>
/// <param name="LastAt">The most recent one, in milliseconds since the epoch.</param>
/// <param name="Reference">The six characters the most recent visitor was told to quote.</param>
public sealed record ErrorLogRow(
    FailureCategory Category,
    string Route,
    string Kind,
    string? Detail,
    int Occurrences,
    long FirstAt,
    long LastAt,
    string Reference);

/// <summary>
/// What asking the server for the log did.
///
/// <para>The same four answers <see cref="ListRequest"/> gives for the invitation list, because
/// it is the identical gate on the server — see <c>invitations.isAdministrator</c> in
/// <c>worker/index.js</c>. A second enum rather than reusing that one, because the two questions
/// happen to share an answer today and a page that conflated them could not stop doing so if
/// they ever did not.</para>
/// </summary>
public enum ErrorLogRequest
{
    /// <summary>Here it is.</summary>
    Loaded,

    /// <summary>Signed in, but this account may not see it. Also what an absent server says.</summary>
    NotForYou,

    /// <summary>Nobody is signed in at all.</summary>
    NotSignedIn,

    /// <summary>Reached and broken, or not reached. Either way, not the reader's doing.</summary>
    Unavailable,
}

/// <summary>The log, and what happened when it was asked for.</summary>
public readonly record struct ErrorLogView(ErrorLogRequest Result, IReadOnlyList<ErrorLogRow> Rows)
{
    public static ErrorLogView Refused(ErrorLogRequest why) => new(why, []);
}

/// <summary>
/// The durable half of a failure, from the browser's side.
///
/// <para><b>Gated by the same question as <see cref="Invitations"/>, not a second one.</b> The
/// server checks <c>invitations.isAdministrator</c> before either address is reached, so an
/// ordinary account gets the same refusal from both — this asks it again rather than trusting a
/// flag left over from asking it a moment ago, for the reason <see cref="Invitations"/> already
/// gives: two places deciding "am I an admin" is two places that could disagree.</para>
///
/// <para><b>Read-only.</b> There is no method here that deletes or clears a row, because the
/// server offers no such route — see the comment on <c>error_log</c> in
/// <c>d1/migrations/0004_error_log.sql</c>.</para>
///
/// <para><b>Nothing here throws</b>, for the same reason as <see cref="Invitations"/>: a site
/// deployed without its server answers every address with the app's own page and a 200, so the
/// body is read rather than the status believed.</para>
/// </summary>
public sealed class ErrorLog
{
    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;

    public ErrorLog(HttpClient http) => _http = http;

    /// <summary>Every recorded failure, or why there is none to show.</summary>
    public async Task<ErrorLogView> ListAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/admin/error-log");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ErrorLogView.Refused(ErrorLogRequest.NotSignedIn);
            }

            // 404 is what an account that may not see this is told, deliberately — the same
            // answer an address this server does not route gives, so an ordinary account cannot
            // learn that the endpoint exists. Also what a site with no server at all eventually
            // produces.
            if (!response.IsSuccessStatusCode) return ErrorLogView.Refused(ErrorLogRequest.NotForYou);

            var body = await response.Content.ReadFromJsonAsync<Wired>(Wire);

            return body?.Rows is null
                ? ErrorLogView.Refused(ErrorLogRequest.NotForYou)
                : new ErrorLogView(ErrorLogRequest.Loaded, body.Rows.Select(ToRow).ToList());
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return ErrorLogView.Refused(ErrorLogRequest.Unavailable);
        }
    }

    private static ErrorLogRow ToRow(WiredRow row) => new(
        Accounts.CategoryNamed(row.Category), row.Route, row.Kind, row.Detail, row.Occurrences,
        row.FirstAt, row.LastAt, row.Reference);

    /// <summary>Every way the server can fail to answer. All of them mean the same here.</summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException
          or TaskCanceledException
          or OperationCanceledException
          or JsonException
          or NotSupportedException
          or InvalidOperationException;

    /// <summary>What the server sends: the rows, in the shape the table stores them.</summary>
    private sealed record Wired(IReadOnlyList<WiredRow>? Rows);

    private sealed record WiredRow(
        string Category, string Route, string Kind, string? Detail, int Occurrences,
        long FirstAt, long LastAt, string Reference);
}
