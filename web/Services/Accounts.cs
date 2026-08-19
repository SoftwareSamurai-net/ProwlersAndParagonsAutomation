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

    /// <summary>The site could not be reached, or answered with something unexpected.</summary>
    Unavailable,
}

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
    public async Task<LinkRequest> AskForLinkAsync(string email)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/request", new { email });

            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => LinkRequest.Accepted,
                HttpStatusCode.BadRequest => LinkRequest.NotAnAddress,
                _ => LinkRequest.Unavailable,
            };
        }
        catch (Exception e) when (IsUnreachable(e)) { return LinkRequest.Unavailable; }
    }

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
