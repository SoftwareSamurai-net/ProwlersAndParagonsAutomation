using System.Net;
using System.Text;
using System.Text.Json;

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

    /// <summary>
    /// Every account's stored character, keyed by account, exactly as the browser sent it.
    ///
    /// <para><b>Keyed, because one shared slot was a second lie of the same kind the verify route
    /// used to tell.</b> A single field meant two signed-in accounts on one instance shared one
    /// character — so any test about two accounts and their characters would have been quietly a
    /// test about one, and would have passed against a server that had no notion of ownership at
    /// all. Found by a fix audit against the unmodified stub; the real server's own tests do cover
    /// this, which is why nothing was actually broken in production code.</para>
    /// </summary>
    /// <remarks>
    /// Private: every test reaches it through <see cref="StoredCharacter"/> or through the routes,
    /// and a public collection on a stub is an invitation to set up a state the real server could
    /// never be in. Qodana caught it as public within minutes of it being written.
    /// </remarks>
    private readonly Dictionary<(string Account, string Id), Stored> _characters = [];

    /// <summary>One stored character: the label, the opaque payload, and when it was touched.</summary>
    private sealed record Stored(string Label, string Payload, long UpdatedAt);

    /// <summary>
    /// A counter, not a clock. The list is ordered by it and the browser adopts the first entry, so
    /// the order has to be deterministic — a real timestamp would let two saves in the same
    /// millisecond order themselves either way and make a passing test a coin toss.
    /// </summary>
    private long _clock;

    /// <summary>This account's cap, as the list endpoint reports it. Five, like the server's default.</summary>
    public int Limit { get; set; } = 5;

    /// <summary>
    /// The id `StoredCharacter` writes under. A fixed, well-formed one, so a test that only cares
    /// that a character exists on the server does not have to mint an id — and so the shape the
    /// real server validates is still the shape the stub stores.
    /// </summary>
    private const string ConvenienceId = "c_0000000000000000000000";

    /// <summary>
    /// The one character of whoever is signed in. Convenience for the many tests with one account
    /// and one character, which is most of them.
    ///
    /// <para>Reading it with several stored answers the most recently touched, which is the one the
    /// browser would have open. Setting it replaces whatever single character is there, under a
    /// fixed id — so a test that only cares "there is a character on the server" does not have to
    /// invent one.</para>
    ///
    /// <para>Setting it with nobody signed in is a test mistake rather than a state the server can
    /// be in, so it throws rather than silently storing nothing.</para>
    /// </summary>
    public string? StoredCharacter
    {
        get => SignedIn is { } who
            ? _characters.Where(e => e.Key.Account == who.Key)
                         .OrderByDescending(e => e.Value.UpdatedAt)
                         .Select(e => e.Value.Payload)
                         .FirstOrDefault()
            : null;
        set
        {
            var who = SignedIn ?? throw new InvalidOperationException(
                "Nobody is signed in, so there is no account for this character to belong to. "
                + "Set SignedIn first.");

            foreach (var key in _characters.Keys.Where(k => k.Account == who.Key).ToList())
                _characters.Remove(key);

            if (value is not null) _characters[(who.Key, ConvenienceId)] = new Stored("Unnamed character", value, ++_clock);
        }
    }

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

    /// <summary>
    /// The failure reference the real server puts in a 500 body, or null for a server that
    /// answered without one.
    ///
    /// <para>Both shapes are real and the client has to hold up in each: the reference exists
    /// only on the path that catches an exception, and something in front of the app — a proxy,
    /// an edge error page — can produce a failure with no JSON in it at all.</para>
    /// </summary>
    public string? LinkRequestReference { get; set; }

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
            "/api/me" => Identity(),

            "/api/auth/request" => LinkRequestReference is null
                ? Status(LinkRequestAnswer)
                : Json($"{{\"error\":\"Something went wrong at this end.\","
                       + $"\"reference\":{Quote(LinkRequestReference)}}}", LinkRequestAnswer),

            // **The same identity `/api/me` gives, not a hardcoded one.** It used to answer with a
            // fixed key whatever `SignedIn` said, which made a test about two accounts on one
            // machine quietly a test about one: the second sign-in returned the first one's key.
            // A stub that answers something the real server never would is worse than no stub.
            "/api/auth/verify" => Identity(),

            "/api/auth/signout" => Status(HttpStatusCode.NoContent),

            "/api/characters" => CharacterList(),
            var p when p.StartsWith("/api/characters/", StringComparison.Ordinal) =>
                Character(request, p["/api/characters/".Length..]),

            "/api/rulebook/power" => Entry(request),

            _ => Status(HttpStatusCode.NotFound),
        };
    }

    /// <summary>Whoever <see cref="SignedIn"/> says, or 401. One answer, so two routes agree.</summary>
    private Task<HttpResponseMessage> Identity() =>
        SignedIn is { } who
            ? Json($$"""{"key":"{{who.Key}}","displayName":"{{who.DisplayName}}"}""")
            : Status(HttpStatusCode.Unauthorized);

    /// <summary>
    /// The list, and the cap. Ordered most recently touched first, as the real server orders it —
    /// the browser adopts the first entry when it has no opinion about which character is open, so
    /// a stub that returned them in another order would test a different feature.
    /// </summary>
    private Task<HttpResponseMessage> CharacterList()
    {
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);

        var mine = _characters
            .Where(e => e.Key.Account == who.Key)
            .OrderByDescending(e => e.Value.UpdatedAt)
            .Select(e => $$"""{"id":"{{e.Key.Id}}","label":{{Quote(e.Value.Label)}},"updatedAt":{{e.Value.UpdatedAt}}}""");

        return Json($$"""{"limit":{{Limit}},"characters":[{{string.Join(",", mine)}}]}""");
    }

    private Task<HttpResponseMessage> Character(HttpRequestMessage request, string id)
    {
        // Scoped to whoever is asking, like the real server's `WHERE user_id = ? AND id = ?`. A
        // stub that answered from one slot could not tell a working ownership check from a missing
        // one — which it could not, once, and a fix audit found it.
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);

        var key = (who.Key, id);

        if (request.Method == HttpMethod.Put)
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            // The cap, enforced the way the real server enforces it: replacing an existing
            // character is always allowed however full the account is.
            if (!_characters.ContainsKey(key) && _characters.Count(e => e.Key.Account == who.Key) >= Limit)
            {
                return Json($$"""{"error":"This account already holds {{Limit}} characters.","limit":{{Limit}}}""",
                    HttpStatusCode.Conflict);
            }

            // Read as text and kept as text, because that is what the real server does: it stores
            // bytes it never parses, and what round-trips is exactly what was written. The label
            // travels beside it for the same reason — the server cannot read a name out of a
            // payload it refuses to look inside.
            using var sent = JsonDocument.Parse(body);
            var payload = sent.RootElement.GetProperty("payload").GetString() ?? "";
            var label = sent.RootElement.TryGetProperty("label", out var l)
                ? l.GetString() ?? "Unnamed character"
                : "Unnamed character";

            _characters[key] = new Stored(label, payload, ++_clock);

            return Status(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Delete)
        {
            // 204 whether or not it was there — deleting something already gone is not a failure.
            _characters.Remove(key);
            return Status(HttpStatusCode.NoContent);
        }

        return _characters.TryGetValue(key, out var stored)
            ? Json(stored.Payload)
            : Status(HttpStatusCode.NotFound);
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text);

    private Task<HttpResponseMessage> Entry(HttpRequestMessage request)
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);

        var name = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["name"] ?? "";

        return Book.TryGetValue(name, out var text)
            ? Json($$"""
                {"heading":"{{name.ToUpperInvariant()}}","printedPage":21,
                 "text":{{JsonSerializer.Serialize(text)}},
                 "sourceRef":"Ultimate Edition, Ch.2 Characters, pp.13-65"}
                """)
            : Status(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> Json(
        string body, HttpStatusCode status = HttpStatusCode.OK) =>
        Text(status, body, "application/json");

    private static Task<HttpResponseMessage> Text(
        HttpStatusCode status, string body, string type = "text/html") =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, type),
        });

    private static Task<HttpResponseMessage> Status(HttpStatusCode status) =>
        Task.FromResult(new HttpResponseMessage(status));
}
