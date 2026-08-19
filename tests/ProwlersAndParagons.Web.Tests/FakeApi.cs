using System.Net;
using System.Text;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The accounts server, as far as the browser can tell.
///
/// <para><b>It is not a second implementation of the server, and the split is deliberate.</b>
/// What the server <em>does</em> — single-use tokens, hashed secrets, one character per account,
/// who may read the book — is tested against the real code in <c>tests/worker</c>, running the
/// real migration in real SQLite. What is tested here is the other half: how the browser behaves
/// when the server answers 401, or 404, or nothing at all. This only has to produce those
/// answers.</para>
///
/// <para>The one thing that could drift is the <em>contract</em> — an address or a field name
/// changing on one side only. That is pinned by <c>AccountsContractTests</c> in the engine
/// suite, which reads both sides and compares them.</para>
///
/// <para><b>Signed out is the default</b>, so every test written before accounts existed still
/// renders the app it was written against: an anonymous visitor whose character is in this
/// browser.</para>
/// </summary>
public sealed class FakeApi : HttpMessageHandler
{
    /// <summary>Who the server says is signed in, or null for nobody.</summary>
    public (string Key, string DisplayName)? SignedIn { get; set; }

    /// <summary>The account's stored character, exactly as the browser sent it.</summary>
    public string? StoredCharacter { get; set; }

    /// <summary>A Power's entry, for whoever is allowed to read one.</summary>
    public Dictionary<string, string> Book { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set to have every call fail the way a missing network does.</summary>
    public bool Unreachable { get; set; }

    /// <summary>
    /// Set to answer everything with the app's own page and a 200.
    ///
    /// <para>That is what a deploy without its server actually does — <c>_redirects</c> serves
    /// every unmatched path as <c>index.html</c> — so "success" is not proof of an answer, and
    /// a client that believed the status would take the app down on the first frame.</para>
    /// </summary>
    public bool ServerNotDeployed { get; set; }

    /// <summary>Every address asked for, in order, so a test can assert nothing was called.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// What a request for a sign-in link answers. 204 by default.
    ///
    /// <para><b>A knob rather than a copy of the server's validation.</b> Whether something is an
    /// address is the server's decision and is tested against the server; what is tested here is
    /// the page's reaction to each answer, and a second validator in the tests would be a second
    /// validator to keep in step.</para>
    /// </summary>
    public HttpStatusCode LinkRequestAnswer { get; set; } = HttpStatusCode.NoContent;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = request.RequestUri!.AbsolutePath;
        Asked.Add(request.Method + " " + path + request.RequestUri.Query);

        if (Unreachable) throw new HttpRequestException("no network");
        if (ServerNotDeployed) return Text(HttpStatusCode.OK, "<!DOCTYPE html><html><body></body></html>");

        return path switch
        {
            "/api/me" => SignedIn is { } who
                ? Json($$"""{"key":"{{who.Key}}","displayName":"{{who.DisplayName}}"}""")
                : Status(HttpStatusCode.Unauthorized),

            "/api/auth/request" => Status(LinkRequestAnswer),

            "/api/auth/verify" => Json("""{"key":"acct-7","displayName":"player"}"""),

            "/api/auth/signout" => Status(HttpStatusCode.NoContent),

            "/api/character" => Character(request),

            "/api/rulebook/power" => Entry(request),

            _ => Status(HttpStatusCode.NotFound),
        };
    }

    private Task<HttpResponseMessage> Character(HttpRequestMessage request)
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);

        if (request.Method == HttpMethod.Put)
        {
            // Read as text and kept as text, because that is what the real server does: it
            // stores bytes it never parses, and what round-trips is exactly what was written.
            StoredCharacter = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Status(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Delete)
        {
            StoredCharacter = null;
            return Status(HttpStatusCode.NoContent);
        }

        return StoredCharacter is { } stored ? Json(stored) : Status(HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> Entry(HttpRequestMessage request)
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);

        var name = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["name"] ?? "";

        return Book.TryGetValue(name, out var text)
            ? Json($$"""
                {"heading":"{{name.ToUpperInvariant()}}","printedPage":21,
                 "text":{{System.Text.Json.JsonSerializer.Serialize(text)}},
                 "sourceRef":"Ultimate Edition, Ch.2 Characters, pp.13-65"}
                """)
            : Status(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> Json(string body) =>
        Text(HttpStatusCode.OK, body, "application/json");

    private static Task<HttpResponseMessage> Text(
        HttpStatusCode status, string body, string type = "text/html") =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, type),
        });

    private static Task<HttpResponseMessage> Status(HttpStatusCode status) =>
        Task.FromResult(new HttpResponseMessage(status));
}
