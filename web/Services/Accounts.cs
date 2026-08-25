using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>What asking for a sign-in link did.</summary>
public enum LinkRequest
{
    /// <summary>The server took it. Whether an account existed is deliberately not said.</summary>
    Accepted,

    /// <summary>What was typed is not an address. The only refusal worth showing.</summary>
    NotAnAddress,

    /// <summary>The site could not be reached at all — no network, or no server there.</summary>
    Unavailable,

    /// <summary>
    /// The site was reached and answered with a failure.
    ///
    /// <para><b>Kept apart from <see cref="Unavailable"/> because they are somebody else's
    /// problem each.</b> Both used to be this enum's one failure, so a misconfigured server
    /// rendered as "could not reach the site" — which is a sentence about the reader's network,
    /// and sent the owner of a 500 looking at their wifi. Once cost real time: three sign-in
    /// attempts against a deployment whose environment variables predated it, all reported as
    /// unreachable while the server was answering perfectly promptly with a 500.</para>
    /// </summary>
    Failed,
}

/// <summary>
/// Which subsystem failed, as the server classified it.
///
/// <para><b>A closed set of four, and it is the server's set — see <c>worker/errors.js</c>.</b>
/// Two audiences want opposite things from one failure: a visitor needs to know whether to
/// retry, wait, or report, and nothing else, because an internal message is both meaningless to
/// them and a disclosure. So the server sends a category and this side renders a sentence for
/// it. A category per throw site would be a description of the server's internals by
/// enumeration, which is the disclosure the whole design avoids.</para>
///
/// <para><b><see cref="Unknown"/> is first so it is <c>default</c>.</b> A category this side does
/// not recognise — an older client against a newer server, a body that is not what was meant —
/// lands here rather than being guessed at, and the sentence for it is the honest one.</para>
///
/// <para><b>A category never depends on whether an account exists.</b> Asking for a link always
/// answers 204 precisely so the endpoint cannot be used to ask whether an address is registered;
/// the server pins that with a test requiring byte-identical bodies for a registered and an
/// unregistered address failing the same way. Nothing on this side may reintroduce the
/// difference either.</para>
/// </summary>
public enum FailureCategory
{
    /// <summary>Unclassified, and honestly so. The default in both directions.</summary>
    Unknown,

    /// <summary>The mail provider. The address was fine and nothing was sent.</summary>
    Mail,

    /// <summary>The database. What was asked for did not persist, rather than being refused.</summary>
    Storage,

    /// <summary>
    /// The deployment is wrong.
    ///
    /// <para><b>The one that must never advise retrying</b>, because retrying cannot fix it. This
    /// is the category the sign-in failure that prompted all of this would have landed in.</para>
    /// </summary>
    Configuration,
}

/// <summary>
/// What asking for a link did, the reference to quote if it failed, and which subsystem failed.
///
/// <para><b>The reference is the whole reason this is not just the enum.</b> The id lets somebody
/// paste six characters into a report and have it match a recorded row, which is the difference
/// between one grep and a guess. It is null for every outcome except a failure that carried one,
/// and it says nothing about what went wrong.</para>
///
/// <para>The category is <see cref="FailureCategory.Unknown"/> for everything that is not a
/// failure the server classified, which includes every outcome that never reached the server.</para>
/// </summary>
public readonly record struct LinkOutcome(
    LinkRequest Result,
    string? Reference = null,
    FailureCategory Category = FailureCategory.Unknown);

/// <summary>
/// Who is signed in, and the three things that change it.
///
/// <para><b>It is the <see cref="IIdentitySource"/> the app runs on, rather than a service
/// beside one</b>, because signing in and out has to change the answer. A separate source that
/// cached its own reply would keep saying "anonymous" for the rest of the visit, and the
/// character would go on being saved to the browser while somebody watched a sign-in succeed.</para>
///
/// <para><b>Nothing here throws.</b> Identity is asked for before the first render — a site
/// whose API is not deployed, or a laptop with no network, must produce an anonymous visitor
/// and a working character generator, not a blank page. Every failure is the same answer:
/// nobody is signed in.</para>
///
/// <para><b>No credential is ever held here.</b> The session is an HttpOnly cookie the browser
/// keeps and this code cannot read; what comes back is a key and a name. That is the whole
/// reason there is no token in local storage and no bearer header anywhere in this project.</para>
/// </summary>
public sealed class Accounts : IIdentitySource
{
    /// <summary>
    /// How long to wait for the server to say who somebody is.
    ///
    /// <para>Short because this call is on the path to the first render. The default
    /// <see cref="HttpClient"/> timeout is a hundred seconds, which as a startup path is a blank
    /// page for a minute and a half — worse than the anonymous answer this falls back to.</para>
    /// </summary>
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;
    private Identity? _known;

    public Accounts(HttpClient http) => _http = http;

    /// <summary>
    /// Raised when the answer to <see cref="CurrentAsync"/> has changed.
    ///
    /// <para>The shell listens so the name in the banner follows a sign-in, and the app reloads
    /// the character because it now belongs somewhere else.</para>
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Who is here — asked once, then remembered until something changes it.
    ///
    /// <para>Remembered because it is asked on every save and every load, and each one would
    /// otherwise be a round trip. Signing in and out clear it, which is why they are on this
    /// class rather than anywhere else.</para>
    /// </summary>
    public async ValueTask<Identity> CurrentAsync()
    {
        if (_known is { } known) return known;

        _known = await AskAsync();

        return _known;
    }

    /// <summary>Ask for a sign-in link. See <see cref="LinkRequest"/> for what it can say.</summary>
    public async Task<LinkOutcome> AskForLinkAsync(string email)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/request", new { email });

            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => new LinkOutcome(LinkRequest.Accepted),
                HttpStatusCode.BadRequest => new LinkOutcome(LinkRequest.NotAnAddress),

                // Reached and refused. The reference and the category are read out of the body
                // when the server sent them; a server that did not is still a failure and still
                // not a network problem, so the outcome does not depend on finding either.
                _ => await FailureIn(response),
            };
        }
        // Genuinely could not get there: no network, DNS, or nothing listening. The only case
        // where telling somebody to try again in a moment is honest advice.
        catch (Exception e) when (IsUnreachable(e)) { return new LinkOutcome(LinkRequest.Unavailable); }
    }

    /// <summary>
    /// The reference and the category the server put in the body, as a failure outcome.
    ///
    /// <para><b>Nothing here may throw.</b> This runs while reporting a failure, so a body that
    /// is not the JSON expected — an HTML error page from something in front of the app, an
    /// empty response — must produce a bare failure rather than a second exception on top of the
    /// first. That is also why it does not use the typed reader: the shape is whatever arrived,
    /// not whatever was meant to.</para>
    ///
    /// <para><b>The message in the body is deliberately never read.</b> The server does not put
    /// one there — an exception from D1 or from a mail provider can quote a query or an address —
    /// and reading it if it appeared would be this side undoing that decision.</para>
    /// </summary>
    private static async Task<LinkOutcome> FailureIn(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var body = document.RootElement;

            var reference = body.TryGetProperty("reference", out var found)
                            && found.ValueKind == JsonValueKind.String
                ? found.GetString()
                : null;

            var category = body.TryGetProperty("category", out var said)
                           && said.ValueKind == JsonValueKind.String
                ? CategoryNamed(said.GetString())
                : FailureCategory.Unknown;

            return new LinkOutcome(LinkRequest.Failed, reference, category);
        }
        catch (Exception e) when (e is JsonException or HttpRequestException or InvalidOperationException)
        {
            return new LinkOutcome(LinkRequest.Failed);
        }
    }

    /// <summary>
    /// One of the server's four names, or <see cref="FailureCategory.Unknown"/>.
    ///
    /// <para>Matched by name rather than parsed, so the wire spelling is decided here and not by
    /// how the enum happens to be capitalised. Anything unrecognised is unknown — a newer server
    /// with a fifth category must degrade to the honest sentence, not to a wrong one.</para>
    ///
    /// <para><b>Internal rather than private</b>: <see cref="ErrorLog"/> reads the same four
    /// categories off the same server, for the same rows this design already classifies. A
    /// second mapping there would be a second place the two lists could drift apart — the exact
    /// shape <c>AccountsContractTests</c> exists to catch, which reads this method's own source
    /// and would be blind to a copy of it.</para>
    /// </summary>
    internal static FailureCategory CategoryNamed(string? wire) => wire switch
    {
        "mail" => FailureCategory.Mail,
        "storage" => FailureCategory.Storage,
        "configuration" => FailureCategory.Configuration,
        _ => FailureCategory.Unknown,
    };

    /// <summary>
    /// Spend a link's token. True when somebody is now signed in.
    ///
    /// <para>Every refusal is one answer, for the same reason the server gives one: a link that
    /// was never issued, has expired, or has already been used are not different problems to
    /// whoever is holding it, and the difference is only useful to somebody guessing.</para>
    /// </summary>
    public async Task<bool> CompleteSignInAsync(string token)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/verify", new { token });
            if (!response.IsSuccessStatusCode) return false;

            var who = await response.Content.ReadFromJsonAsync<Wired>(Wire);
            if (who?.Key is null) return false;

            Became(new Identity(who.Key, who.DisplayName));

            return true;
        }
        catch (Exception e) when (IsUnreachable(e)) { return false; }
    }

    /// <summary>
    /// Stop being signed in.
    ///
    /// <para><b>The local answer changes whether or not the server was reachable.</b> Somebody
    /// who has asked to sign out on a shared machine must not be left signed in because a
    /// request failed — and the cookie's own expiry, plus the server deleting the session when
    /// it does hear, are what make that safe rather than merely polite.</para>
    /// </summary>
    public async Task SignOutAsync()
    {
        try { await _http.PostAsJsonAsync("api/auth/signout", new { }); }
        catch (Exception e) when (IsUnreachable(e)) { /* below happens anyway */ }

        Became(Identity.Anonymous);
    }

    private void Became(Identity who)
    {
        _known = who;
        Changed?.Invoke();
    }

    /// <summary>
    /// The server's answer, or the anonymous visitor.
    ///
    /// <para>A 401 is the ordinary answer for somebody with no account and is not a failure. So
    /// is everything else that can happen here — including this site being deployed without its
    /// server at all, which answers with the app's own <c>index.html</c> and a 200, because
    /// <c>_redirects</c> serves every unmatched path that way. That is why the body is parsed
    /// rather than the status believed.</para>
    /// </summary>
    private async Task<Identity> AskAsync()
    {
        using var deadline = new CancellationTokenSource(AskTimeout);

        try
        {
            var response = await _http.GetAsync("api/me", deadline.Token);
            if (!response.IsSuccessStatusCode) return Identity.Anonymous;

            var who = await response.Content.ReadFromJsonAsync<Wired>(Wire, deadline.Token);

            return who?.Key is { Length: > 0 } key
                ? new Identity(key, who.DisplayName)
                : Identity.Anonymous;
        }
        catch (Exception e) when (IsUnreachable(e)) { return Identity.Anonymous; }
    }

    /// <summary>
    /// Every way the server can fail to answer. All of them mean the same thing here.
    ///
    /// <para>Named rather than a bare <c>catch</c> so the list is reviewable — and it includes
    /// <see cref="JsonException"/> because a site served without its API answers this address
    /// with a page of HTML and a 200.</para>
    /// </summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException           // no network, DNS, TLS, a refused connection
          or TaskCanceledException          // the deadline above, or the host going away
          or OperationCanceledException
          or JsonException                  // an answer that is not the answer (an HTML page)
          or NotSupportedException          // a content type this cannot read
          or InvalidOperationException;     // no base address, or interop unavailable

    /// <summary>
    /// What the server says about somebody: a key and a name, and nothing else.
    ///
    /// <para>It matches <see cref="Identity"/> exactly, and the omissions are the design — no
    /// claims, no token, no expiry, no email address. Adding a field here means adding it to
    /// <see cref="Identity"/>, which is the conversation this shape exists to force.</para>
    /// </summary>
    private sealed record Wired(
        [property: JsonPropertyName("key")] string? Key,
        [property: JsonPropertyName("displayName")] string? DisplayName);
}
