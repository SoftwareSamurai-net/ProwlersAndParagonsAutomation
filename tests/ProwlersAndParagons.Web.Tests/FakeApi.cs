using System.Globalization;
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

    /// <summary>
    /// One stored character, in the four fields the real server keeps beside the payload.
    ///
    /// <para><b>This held only the label and the payload, and that was a gap of exactly the kind
    /// this class exists not to have.</b> `campaign_id` has been a column since 0005 and the list
    /// endpoint has answered it since; this stub dropped it on the floor, so every browser-side
    /// test of "which game is this character in" was asserting against a fake that could never
    /// have said. `kind`, `tier_id` and `spent` arrived with 0008 and are stored here from the
    /// start for the same reason — a stub that answers less than the real server turns a broken
    /// round trip into a green suite.</para>
    /// </summary>
    private sealed record Stored(
        string Label,
        string Payload,
        long UpdatedAt,
        string? CampaignId = null,
        string? Kind = null,
        string? TierId = null,
        int? Spent = null);

    /// <summary>
    /// A counter, not a clock. The list is ordered by it and the browser adopts the first entry, so
    /// the order has to be deterministic — a real timestamp would let two saves in the same
    /// millisecond order themselves either way and make a passing test a coin toss.
    /// </summary>
    /// <summary>
    /// The stamp the next stored character gets, moving forward one step at a time.
    ///
    /// <para><b>It starts from now rather than from zero, and that is a correctness fix rather
    /// than a nicety.</b> A bare counter handed out 1, 2, 3 — timestamps a millisecond after the
    /// epoch — and the real server writes <c>Date.now()</c>. Nothing asserted on the value, so it
    /// went unnoticed until a panel started printing "edited …" beside a character and every proof
    /// page in the project read <em>over a year ago</em>. This class's own remarks name the rule it
    /// was breaking: a stub that answers something the real server never would is worse than no
    /// stub.</para>
    ///
    /// <para>Still a counter underneath, because ordering is the only property anything here reads
    /// — the list comes back most-recently-touched first — and a counter cannot hand out two equal
    /// stamps the way a fast clock can.</para>
    /// </summary>
    private long _clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

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

    /// <summary>
    /// The recorded conversations, as the server would bundle and answer them — a JSON object
    /// keyed by file name, matching what <c>worker/transcripts-corpus.js</c> holds. Populated
    /// with the real files by default; see <c>RenderContext</c>.
    /// </summary>
    public string? TranscriptsBundle { get; set; }

    /// <summary>
    /// Set to have <c>/api/transcripts</c> refuse even a signed-in caller — the shape of a
    /// server that has the route but not the data, or one that is simply down for a moment.
    /// </summary>
    public bool TranscriptsUnavailable { get; set; }

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
    /// Whether the signed-in account may manage who can sign in.
    ///
    /// <para><b>False by default, which is the interesting case.</b> Most accounts are not
    /// administrators, and what the page does for one of those — say so, and show nothing — is
    /// the behaviour worth being sure of. The real server answers such an account with the same
    /// 404 it gives an address it does not route, so that is what this answers too.</para>
    /// </summary>
    public bool ManagesInvitations { get; set; }

    /// <summary>Who may have an account, as the list endpoint reports them.</summary>
    /// <remarks>
    /// A list rather than a canned body, so a test can add a row and assert the page redraws.
    /// Each entry is exactly the shape the server sends; the contract between the two is pinned
    /// by <c>AccountsContractTests</c> and not by this.
    /// </remarks>
    public List<(string? Id, string Email, bool GrantsAdmin, bool HasSignedIn, bool Removable)>
        Invited { get; } = [];

    /// <summary>The address the server calls "you" in that list. The signed-in account's.</summary>
    public string You { get; set; } = "you@example.test";

    /// <summary>
    /// Every recorded failure, as the error-log endpoint reports it. Gated by
    /// <see cref="ManagesInvitations"/>, the same as <see cref="Invited"/> — the real server
    /// answers both from the identical check.
    /// </summary>
    public List<(string Category, string Route, string Kind, string? Detail, int Occurrences,
        long FirstAt, long LastAt, string Reference)> ErrorLogRows { get; } = [];

    /// <summary>Set to have an invitation refuse to be added or withdrawn.</summary>
    public bool RefuseInvitationChanges { get; set; }

    /// <summary>Set to have a display-name change refused, the way the real server refuses one
    /// that is not a usable string.</summary>
    public bool RefuseDisplayNameChanges { get; set; }

    /// <summary>Set false to have a newly-added address fail to be mailed. True by default.</summary>
    public bool InvitationMailSucceeds { get; set; } = true;

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
            "/api/me/display-name" => SetDisplayName(request),

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

            "/api/campaigns" => CampaignList(),
            var p when p.StartsWith("/api/campaigns/", StringComparison.Ordinal) =>
                Campaign(request, p["/api/campaigns/".Length..]),

            // The two lists are two addresses on purpose, so a missing parameter cannot default
            // to the wrong half of the feature — see the note in worker/index.js.
            "/api/memberships" => MembershipList(),
            "/api/memberships/inbox" => MembershipInbox(),
            "/api/memberships/join" => MembershipJoin(request),
            var p when p.StartsWith("/api/memberships/", StringComparison.Ordinal) =>
                Membership(request, p["/api/memberships/".Length..]),

            "/api/rulebook/power" => Entry(request),
            "/api/rulebook/contents" => Contents(),
            "/api/rulebook/search" => Found(request),
            "/api/rulebook/passage" => Passage(request),

            "/api/transcripts" => Transcripts(),

            "/api/admin/invitations" => InvitationList(request),
            var p when p.StartsWith("/api/admin/invitations/", StringComparison.Ordinal) =>
                Invitation(request, p["/api/admin/invitations/".Length..]),

            "/api/admin/error-log" => ErrorLogList(),

            _ => Status(HttpStatusCode.NotFound),
        };
    }

    /// <summary>Whoever <see cref="SignedIn"/> says, or 401. One answer, so two routes agree.</summary>
    private Task<HttpResponseMessage> Identity() =>
        SignedIn is { } who
            ? Json($$"""{"key":"{{who.Key}}","displayName":"{{who.DisplayName}}"}""")
            : Status(HttpStatusCode.Unauthorized);

    /// <summary>
    /// Change the signed-in account's own name, or 401.
    ///
    /// <para>The real rule — trimmed, capped, refused for a shape it cannot store, blank resets
    /// to the account's email — is tested against the real server in <c>tests/worker</c>; this
    /// only has to give the client something to react to, so a name that is not a usable string
    /// is the one shape refused here.</para>
    /// </summary>
    private Task<HttpResponseMessage> SetDisplayName(HttpRequestMessage request)
    {
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);
        if (RefuseDisplayNameChanges) return Status(HttpStatusCode.BadRequest);

        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var sent = JsonDocument.Parse(body);

        if (!sent.RootElement.TryGetProperty("displayName", out var named)
            || named.ValueKind != JsonValueKind.String)
        {
            return Status(HttpStatusCode.BadRequest);
        }

        // The same identity `/api/me` gives, not a hand-built one — see the note on
        // `/api/auth/verify` above; the same lesson applies here.
        SignedIn = (who.Key, named.GetString() ?? who.DisplayName);

        return Identity();
    }

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
            .Select(e => $$"""
                {"id":"{{e.Key.Id}}","label":{{Quote(e.Value.Label)}},
                 "updatedAt":{{e.Value.UpdatedAt}},
                 "campaignId":{{Quote(e.Value.CampaignId)}},
                 "kind":{{Quote(e.Value.Kind)}},
                 "tierId":{{Quote(e.Value.TierId)}},
                 "spent":{{e.Value.Spent?.ToString(CultureInfo.InvariantCulture) ?? "null"}}}
                """);

        return Json($$"""{"limit":{{Limit}},"characters":[{{string.Join(",", mine)}}]}""");
    }

    /// <summary>
    /// Held open before a character is answered, so a test can decide what order two reads finish
    /// in.
    ///
    /// <para><b>A slow response is a state the real server genuinely has</b>, which is the bar this
    /// class sets for itself — a stub that answers something the real server never would is worse
    /// than no stub. Every read here is a network round trip in the deployed app and any two of
    /// them can land out of order; without this, that is unreachable in a test, because the fake
    /// answers synchronously and closes the window the fault lives in.</para>
    ///
    /// <para>Null is the ordinary case and every other test is unaffected.</para>
    /// </summary>
    public Func<string, Task>? BeforeAnsweringCharacter { get; set; }

    private async Task<HttpResponseMessage> Character(HttpRequestMessage request, string id)
    {
        if (request.Method == HttpMethod.Get && BeforeAnsweringCharacter is { } gate)
            await gate(id);

        return await CharacterAnswer(request, id);
    }

    private Task<HttpResponseMessage> CharacterAnswer(HttpRequestMessage request, string id)
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

            // Everything beside the payload, kept exactly as it arrived and echoed back by the
            // list — the whole of what the real server does with these four.
            _characters[key] = new Stored(
                label, payload, ++_clock,
                Text(sent.RootElement, "campaignId"),
                Text(sent.RootElement, "kind"),
                Text(sent.RootElement, "tierId"),
                Number(sent.RootElement, "spent"));

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

    /// <summary>
    /// Who may have an account, or the refusal.
    ///
    /// <para>401 for nobody signed in and 404 for an account that may not manage the list — the
    /// two the real server gives, and they are different questions. The second is deliberately
    /// the same answer an unrouted address gets, so that an ordinary account cannot learn the
    /// page exists.</para>
    /// </summary>
    private Task<HttpResponseMessage> InvitationList(HttpRequestMessage request)
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);
        if (!ManagesInvitations) return Status(HttpStatusCode.NotFound);

        if (request.Method == HttpMethod.Post)
        {
            if (RefuseInvitationChanges) return Status(HttpStatusCode.BadRequest);

            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var sent = JsonDocument.Parse(body);

            var email = sent.RootElement.GetProperty("email").GetString() ?? "";
            var grants = sent.RootElement.TryGetProperty("grantsAdmin", out var g) && g.GetBoolean();

            var already = Invited.Any(i => i.Email == email);
            if (!already)
            {
                Invited.Add(($"i_{Invited.Count:D22}", email, grants, false, true));
            }

            var mailed = !already && InvitationMailSucceeds;

            return Json($$"""{"alreadyAllowed":{{Lower(already)}},"mailed":{{Lower(mailed)}}}""");
        }

        var rows = Invited.Select(i => $$"""
            {"id":{{(i.Id is null ? "null" : Quote(i.Id))}},"email":{{Quote(i.Email)}},
             "grantsAdmin":{{Lower(i.GrantsAdmin)}},"hasSignedIn":{{Lower(i.HasSignedIn)}},
             "createdAt":0,"removable":{{Lower(i.Removable)}}}
            """);

        return Json($$"""{"you":{{Quote(You)}},"invitations":[{{string.Join(",", rows)}}]}""");
    }

    /// <summary>Withdraw one, unless this stub has been told to refuse.</summary>
    private Task<HttpResponseMessage> Invitation(HttpRequestMessage request, string id)
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);
        if (!ManagesInvitations) return Status(HttpStatusCode.NotFound);
        if (request.Method != HttpMethod.Delete) return Status(HttpStatusCode.MethodNotAllowed);
        if (RefuseInvitationChanges) return Status(HttpStatusCode.Conflict);

        Invited.RemoveAll(i => i.Id == id);

        return Status(HttpStatusCode.NoContent);
    }

    /// <summary>The recorded failures, or the refusal — gated exactly as <see cref="Invitation"/>.</summary>
    private Task<HttpResponseMessage> ErrorLogList()
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);
        if (!ManagesInvitations) return Status(HttpStatusCode.NotFound);

        var rows = ErrorLogRows.Select(r => $$"""
            {"category":{{Quote(r.Category)}},"route":{{Quote(r.Route)}},"kind":{{Quote(r.Kind)}},
             "detail":{{(r.Detail is null ? "null" : Quote(r.Detail))}},
             "occurrences":{{r.Occurrences}},"firstAt":{{r.FirstAt}},"lastAt":{{r.LastAt}},
             "reference":{{Quote(r.Reference)}}}
            """);

        return Json($$"""{"rows":[{{string.Join(",", rows)}}]}""");
    }

    /// <summary>
    /// The bundle, or the two refusals the real server gives — 401 for nobody signed in, and
    /// whatever <see cref="TranscriptsUnavailable"/> asks for otherwise.
    /// </summary>
    private Task<HttpResponseMessage> Transcripts()
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);
        if (TranscriptsUnavailable) return Status(HttpStatusCode.NotFound);

        return Json(TranscriptsBundle ?? "{}");
    }

    private static string Lower(bool value) => value ? "true" : "false";

    /// <summary>A quoted string, or the literal <c>null</c> — what the real server answers for a
    /// column that has none.</summary>
    private static string Quote(string? text) => text is null ? "null" : JsonSerializer.Serialize(text);

    /// <summary>One optional string out of a sent body. Absent, null and not-a-string are all
    /// "nothing sent", which is what the server's own normalisers make of them.</summary>
    private static string? Text(JsonElement body, string name) =>
        body.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()
            : null;

    /// <summary>One optional whole number out of a sent body, on the same terms as <see cref="Text"/>.</summary>
    private static int? Number(JsonElement body, string name) =>
        body.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Number
            ? found.GetInt32()
            : null;

    // ── Campaigns, and the approval slot beside them ─────────────────────────────────────
    //
    // **The same bar the rest of this class is held to: a stub that answers something the real
    // server never would is worse than no stub.** So a campaign is keyed by account, a join code
    // is minted per campaign and looked up across accounts (which is what a join code *is*), and
    // a decision carries a version that is compared — because the compare-and-swap is a
    // behaviour the browser has to hold up against, not a status code to be canned.
    //
    // What the real server *does* — the SQL scoping, the unique index on the code, the rate limit
    // — is tested against the real code in tests/worker/memberships.test.mjs against real SQLite.
    // What is tested here is the other half: how the browser behaves when it is told 409, or 404,
    // or nothing at all.

    /// <summary>One stored campaign: the label, the opaque payload, and the code to join it.</summary>
    private sealed record StoredCampaignRow(string Label, string Payload, string Code, long UpdatedAt);

    private readonly Dictionary<(string Account, string Id), StoredCampaignRow> _campaigns = [];

    /// <summary>
    /// One membership: which campaign, whose character, and the two payload slots.
    ///
    /// <para><b>Both accounts are on the row</b>, exactly as they are in the real table, so a test
    /// about one account not reaching another's row is testing scoping rather than an absent
    /// feature.</para>
    /// </summary>
    private sealed record MembershipRow(
        string CampaignId, string GmAccount, string PlayerAccount, string CharacterId,
        string Label, string? Approved, long? ApprovedAt, string? Pending, long? PendingAt,
        int PendingVersion, string? Decision = null);

    private readonly Dictionary<string, MembershipRow> _memberships = [];

    /// <summary>
    /// Whether the campaign this membership belongs to is still there, which is the real
    /// server's <c>EXISTS (SELECT 1 FROM campaigns c WHERE c.user_id = gm_user_id AND c.id =
    /// campaign_id)</c> — and the <c>AND c.id</c> half matters: a GM who runs two games and
    /// deletes one must lose that one's half and keep the other's.
    /// </summary>
    private bool StillThere(MembershipRow row) =>
        _campaigns.ContainsKey((row.GmAccount, row.CampaignId));

    /// <summary>
    /// Set to have the join endpoint refuse the way a rate limit does, so the browser's own
    /// sentence for that refusal is reachable in a test.
    /// </summary>
    public bool JoinIsRateLimited { get; set; }

    /// <summary>
    /// A word to answer in every list row's <c>decision</c>, in place of the row's own.
    ///
    /// <para><b>A seam, because the state it reaches cannot be provoked through the endpoints.</b>
    /// The server writes only the two words it knows, so a third — one a later version could
    /// spell — is unreachable by approving or rejecting anything. The browser must read an unknown
    /// word as no decision rather than as a rejection, and without this that rule is a claim.</para>
    /// </summary>
    public string? DecisionOnTheWire { get; set; }

    // A `BeforeDeciding` seam lived here — a hook fired between a decision arriving and being
    // answered, so a test could let a resubmission land in between. **It is gone because it does
    // not discriminate**, which a mutation established: the fault it was meant to catch (a page
    // reading the *current* version rather than the one it drew) does its extra read before such a
    // seam could fire, so the test passed against the broken page. The real race is between the
    // diff being drawn and the button being pressed, and a test can drive that with no seam at
    // all — see `CampaignApprovalTests`. A seam nothing races reads as a guarantee and is not one.

    /// <summary>
    /// Put a campaign on the server directly, and hand back its join code.
    ///
    /// <para>Convenience for the many tests that need a campaign to exist without driving the
    /// screen that makes one. It goes through the same dictionary the routes use, so nothing can
    /// be set up here that a real request could not produce.</para>
    /// </summary>
    public string Campaign(string id, string label, string payload)
    {
        if (SignedIn is not { } who)
        {
            throw new InvalidOperationException(
                "Nobody is signed in, so there is no account to hold a campaign. Set SignedIn "
                + "first — the real server has no anonymous campaign either.");
        }

        var code = NextCode();
        _campaigns[(who.Key, id)] = new StoredCampaignRow(label, payload, code, ++_clock);

        return code;
    }

    /// <summary>
    /// A join code, in the form the real server puts on the wire — <b>ten symbols and no hyphen</b>.
    ///
    /// <para><b>This minted a hyphenated eleven characters, and the server never sends one.</b>
    /// `normaliseJoinCode` takes the punctuation out on the way in, so what is stored and what
    /// both `/api/campaigns` and `/api/campaigns/{id}/code` answer is the bare ten — the hyphen is
    /// presentation, put back by `SavedCampaignSummary.Spoken` where a reader is. A fake sending a
    /// shape the server does not is a suite that cannot see the drift it exists to catch.</para>
    ///
    /// <para>Sequential rather than random for the reason the clock is a counter: a test asserting
    /// on a code has to be able to predict it, and two codes minted in one test must differ.</para>
    /// </summary>
    private string NextCode()
    {
        var n = ++_codes;

        return $"AAAA{n % 10}BBBB{n % 10}";
    }

    private int _codes;

    /// <summary>What a campaign's own list row looks like — the shape the client binds.</summary>
    private Task<HttpResponseMessage> CampaignList()
    {
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);

        var mine = _campaigns
            .Where(e => e.Key.Account == who.Key)
            .OrderByDescending(e => e.Value.UpdatedAt)
            .Select(e => $$"""
                {"id":{{Quote(e.Key.Id)}},"label":{{Quote(e.Value.Label)}},
                 "updatedAt":{{e.Value.UpdatedAt}},"joinCode":{{Quote(e.Value.Code)}}}
                """);

        return Json($$"""{"campaigns":[{{string.Join(",", mine)}}]}""");
    }

    private Task<HttpResponseMessage> Campaign(HttpRequestMessage request, string rest)
    {
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);

        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        var id = slash < 0 ? rest : rest[..slash];
        var tail = slash < 0 ? "" : rest[(slash + 1)..];
        var key = (who.Key, id);

        if (tail == "code")
        {
            if (request.Method != HttpMethod.Post) return Status(HttpStatusCode.MethodNotAllowed);
            if (!_campaigns.TryGetValue(key, out var rotating)) return Status(HttpStatusCode.NotFound);

            var fresh = NextCode();
            _campaigns[key] = rotating with { Code = fresh };

            return Json($$"""{"joinCode":{{Quote(fresh)}}}""");
        }

        if (tail.Length > 0) return Status(HttpStatusCode.NotFound);

        if (request.Method == HttpMethod.Put)
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var sent = JsonDocument.Parse(body);

            var payload = sent.RootElement.GetProperty("payload").GetString() ?? "";
            var label = sent.RootElement.TryGetProperty("label", out var l)
                ? l.GetString() ?? "Unnamed campaign"
                : "Unnamed campaign";

            // The code is minted on the first write and kept afterwards, which is what the real
            // statement's COALESCE does — a stub that rotated it on every save would make a test
            // about not locking players out pass for the wrong reason.
            var code = _campaigns.TryGetValue(key, out var already) ? already.Code : NextCode();

            _campaigns[key] = new StoredCampaignRow(label, payload, code, ++_clock);

            return Status(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Delete)
        {
            _campaigns.Remove(key);
            return Status(HttpStatusCode.NoContent);
        }

        return _campaigns.TryGetValue(key, out var stored)
            ? Json(stored.Payload)
            : Status(HttpStatusCode.NotFound);
    }

    /// <summary>The caller's own memberships — the player's half. No payload, like the real one.</summary>
    private Task<HttpResponseMessage> MembershipList()
    {
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);

        return Json($$"""
            {"memberships":[{{string.Join(",", _memberships
                .Where(m => m.Value.PlayerAccount == who.Key)
                .Select(m => Row(m.Key, m.Value, forGm: false)))}}]}
            """);
    }

    /// <summary>Every membership of every campaign the caller runs — the GM's half.</summary>
    private Task<HttpResponseMessage> MembershipInbox()
    {
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);

        return Json($$"""
            {"memberships":[{{string.Join(",", _memberships
                .Where(m => m.Value.GmAccount == who.Key && StillThere(m.Value))
                .Select(m => Row(m.Key, m.Value, forGm: true)))}}]}
            """);
    }

    /// <summary>
    /// One list row. <c>characterId</c> is withheld from the GM exactly as the real server
    /// withholds it — a stub that sent it would make a test about that impossible to write.
    /// </summary>
    private string Row(string id, MembershipRow row, bool forGm) => $$"""
        {"id":{{Quote(id)}},"campaignId":{{Quote(row.CampaignId)}},
         "characterId":{{(forGm ? "null" : Quote(row.CharacterId))}},
         "label":{{Quote(row.Label)}},
         "hasApproved":{{Lower(row.Approved is not null)}},
         "approvedAt":{{row.ApprovedAt?.ToString(CultureInfo.InvariantCulture) ?? "null"}},
         "hasPending":{{Lower(row.Pending is not null)}},
         "pendingAt":{{row.PendingAt?.ToString(CultureInfo.InvariantCulture) ?? "null"}},
         "pendingVersion":{{row.PendingVersion}},
         "decision":{{((DecisionOnTheWire ?? row.Decision) is not { } word ? "null" : Quote(word))}}}
        """;

    /// <summary>Redeem a join code, or refuse the four ways the real server refuses.</summary>
    private Task<HttpResponseMessage> MembershipJoin(HttpRequestMessage request)
    {
        if (SignedIn is not { } who) return Status(HttpStatusCode.Unauthorized);
        if (JoinIsRateLimited) return Status(HttpStatusCode.TooManyRequests);

        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var sent = JsonDocument.Parse(body);

        var code = Normalised(sent.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null);
        if (code is null) return Status(HttpStatusCode.BadRequest);

        var characterId = sent.RootElement.TryGetProperty("characterId", out var ch)
            ? ch.GetString() ?? "" : "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(characterId, "^c_[A-Za-z0-9_-]{22}$"))
        {
            return Status(HttpStatusCode.BadRequest);
        }

        var label = sent.RootElement.TryGetProperty("label", out var l)
            ? l.GetString() ?? "Unnamed character" : "Unnamed character";

        // **Across accounts, which is what a join code is for**, and the one lookup in this class
        // that is not scoped to the caller. It answers the campaign and nothing about its owner.
        var found = _campaigns.FirstOrDefault(e => Normalised(e.Value.Code) == code);
        if (found.Value is null) return Status(HttpStatusCode.NotFound);

        var already = _memberships.FirstOrDefault(m =>
            m.Value.CampaignId == found.Key.Id
            && m.Value.PlayerAccount == who.Key
            && m.Value.CharacterId == characterId);

        var id = already.Key ?? $"m_{_memberships.Count:D22}";

        _memberships[id] = already.Value is { } existing
            ? existing with { Label = label }
            : new MembershipRow(found.Key.Id, found.Key.Account, who.Key, characterId,
                label, null, null, null, null, 0);

        return Json($$"""
            {"id":{{Quote(id)}},"campaignId":{{Quote(found.Key.Id)}},
             "label":{{Quote(found.Value.Label)}},"payload":{{Quote(found.Value.Payload)}},
             "pendingVersion":{{_memberships[id].PendingVersion}}}
            """);
    }

    /// <summary>A code as the real server stores and compares one: upper case, no punctuation.</summary>
    private static string? Normalised(string? code)
    {
        if (code is null) return null;

        var symbols = new string([.. code.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit)]);

        return symbols.Length == 10 ? symbols : null;
    }

    private async Task<HttpResponseMessage> Membership(HttpRequestMessage request, string rest)
    {
        if (SignedIn is not { } who) return await Status(HttpStatusCode.Unauthorized);

        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        var id = slash < 0 ? rest : rest[..slash];
        var tail = slash < 0 ? "" : rest[(slash + 1)..];

        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^m_[A-Za-z0-9_-]{22}$"))
        {
            return await Status(HttpStatusCode.BadRequest);
        }

        // ── Ending a membership, ahead of the two 404s below ────────────────────────────
        //
        // **Before them because the real server answers differently on both.** `DELETE` is two
        // statements, each scoped to its own owner column, and then a probe on the miss: a row
        // this account cannot name answers 204 like an already-deleted one, and a GM whose game
        // is gone answers 409 rather than the 404 a read of the same address gets. A fake that
        // fell into the read's gates here would be more restrictive than the server, which is the
        // same class of fault as being more permissive — it makes a reachable state untestable.
        if (tail.Length == 0 && request.Method == HttpMethod.Delete)
        {
            if (!_memberships.TryGetValue(id, out var ending))
            {
                return await Status(HttpStatusCode.NoContent);
            }

            var isPlayers = ending.PlayerAccount == who.Key;
            var isTheirGame = ending.GmAccount == who.Key;

            // The player's half is a bare column and the GM's carries the campaign check, exactly
            // as `leaveCampaign` and `removeMember` are written.
            if (isPlayers || (isTheirGame && StillThere(ending)))
            {
                _memberships.Remove(id);

                return await Status(HttpStatusCode.NoContent);
            }

            if (isTheirGame)
            {
                return await Json(
                    """{"error":"That campaign is no longer here."}""",
                    HttpStatusCode.Conflict);
            }

            // Somebody else's row. Nothing ended, and the answer says nothing about that.
            return await Status(HttpStatusCode.NoContent);
        }

        // Scoped to whoever is asking, both ways round. **Not `(gm_user_id = ? OR
        // player_user_id = ?)`**, which is what this said and what `docs/CHARACTERS-API.md`
        // said: the GM's half also requires the campaign to still exist, and the player's
        // deliberately does not. A third account matches neither.
        if (!_memberships.TryGetValue(id, out var row)
            || (row.GmAccount != who.Key && row.PlayerAccount != who.Key))
        {
            return await Status(HttpStatusCode.NotFound);
        }

        var isGm = row.GmAccount == who.Key;

        // A GM who deleted the game reaches nothing of it — no read, no decision — while the
        // player keeps their row and is told why. A fake that answered anyway made every screen's
        // handling of a deleted campaign untestable, which is how the wrong sentence below it
        // survived: the approval page called a stale snapshot "nothing waiting".
        if (isGm && !StillThere(row)) return await Status(HttpStatusCode.NotFound);

        if (tail == "submission")
        {
            if (request.Method != HttpMethod.Put) return await Status(HttpStatusCode.MethodNotAllowed);
            if (isGm) return await Status(HttpStatusCode.NotFound);

            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var sent = JsonDocument.Parse(body);

            var payload = sent.RootElement.TryGetProperty("payload", out var p) ? p.GetString() : null;
            if (payload is null) return await Status(HttpStatusCode.BadRequest);

            if (!StillThere(row))
            {
                return await Json(
                    """{"error":"That campaign is no longer here."}"""[..],
                    HttpStatusCode.Conflict);
            }

            var label = sent.RootElement.TryGetProperty("label", out var l)
                ? l.GetString() ?? row.Label : row.Label;

            _memberships[id] = row with
            {
                Label = label,
                Pending = payload,
                PendingAt = ++_clock,
                PendingVersion = row.PendingVersion + 1,
            };

            return await Json($$"""{"version":{{_memberships[id].PendingVersion}}}""");
        }

        if (tail is "approve" or "reject")
        {
            if (request.Method != HttpMethod.Post) return await Status(HttpStatusCode.MethodNotAllowed);
            if (!isGm) return await Status(HttpStatusCode.NotFound);

            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var sent = JsonDocument.Parse(body);

            if (!sent.RootElement.TryGetProperty("version", out var v)
                || v.ValueKind != JsonValueKind.Number)
            {
                return await Status(HttpStatusCode.BadRequest);
            }

            var version = v.GetInt32();

            row = _memberships[id];

            if (row.Pending is null)
            {
                return await Json($$"""
                    {"error":"There is nothing waiting for a decision here.",
                     "pendingVersion":{{row.PendingVersion}},"pending":null}
                    """, HttpStatusCode.Conflict);
            }

            if (row.PendingVersion != version)
            {
                return await Json($$"""
                    {"error":"This changed while you were looking at it.",
                     "pendingVersion":{{row.PendingVersion}},"pending":{{Quote(row.Pending)}}}
                    """, HttpStatusCode.Conflict);
            }

            // **Both arms write `decision`, which is the point of it.** A fake that recorded it
            // only on the approve path would leave a rejection looking exactly as it did before
            // `0007` — and the whole defect that migration exists for is a rejection being
            // indistinguishable from an approval of an earlier snapshot.
            _memberships[id] = tail == "approve"
                ? row with
                {
                    Approved = row.Pending, ApprovedAt = ++_clock,
                    Pending = null, PendingAt = null, Decision = "approved",
                }
                : row with { Pending = null, PendingAt = null, Decision = "rejected" };

            return await Status(HttpStatusCode.NoContent);
        }

        if (tail.Length > 0) return await Status(HttpStatusCode.NotFound);
        if (request.Method != HttpMethod.Get) return await Status(HttpStatusCode.MethodNotAllowed);

        return await Json($$"""
            {"id":{{Quote(id)}},"campaignId":{{Quote(row.CampaignId)}},
             "characterId":{{(isGm ? "null" : Quote(row.CharacterId))}},
             "label":{{Quote(row.Label)}},"role":{{Quote(isGm ? "gm" : "player")}},
             "approved":{{(row.Approved is null ? "null" : Quote(row.Approved))}},
             "pending":{{(row.Pending is null ? "null" : Quote(row.Pending))}},
             "pendingVersion":{{row.PendingVersion}}}
            """);
    }


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

    /// <summary>
    /// What there is to read, or 401.
    ///
    /// <para><b>Not a second search.</b> What the matching rule does — that "city" must not reach
    /// Plasticity, that a heading beats a mention — is tested against the real code in
    /// <c>tests/worker</c>, over the real corpus. What is tested on this side is how the page
    /// behaves when the server answers, refuses, or is not there, so this only has to produce
    /// those answers.</para>
    /// </summary>
    private Task<HttpResponseMessage> Contents()
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);

        var rows = Chapters.Select(c => $$"""
            {"chapter":{{c.Number}},"title":{{Quote(c.Title)}},"printedPages":[1,2],
             "sourceRef":{{Quote($"Ultimate Edition, Ch.{c.Number} {c.Title}, pp.1-2")}},
             "sections":{{c.Passages.Count}}}
            """);

        return Json($$"""
            {"chapters":[{{string.Join(",", rows)}}],
             "sections":{{Chapters.Sum(c => c.Passages.Count)}}}
            """);
    }

    /// <summary>
    /// What a search answers, or 401. Matching is a plain word test — see above.
    ///
    /// <para><b><c>chapter</c> narrows the corpus before anything is matched, and the count comes
    /// out of the narrowed set</b>, which is what the real server does and the only property the
    /// page reads. A stub that filtered the rows afterwards and left <c>found</c> alone would
    /// answer exactly what the server change exists to stop the browser doing for itself.</para>
    /// </summary>
    private Task<HttpResponseMessage> Found(HttpRequestMessage request)
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);

        var asked = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);

        var query = asked["q"];
        if (query is null) return Status(HttpStatusCode.BadRequest);

        var searching = Chapters.AsEnumerable();

        if (asked["chapter"] is { } wanted)
        {
            // The same two answers the real server gives: not a number is the caller's mistake,
            // a number naming no chapter is a miss. Neither is an empty result set.
            if (!int.TryParse(wanted, out var number)) return Status(HttpStatusCode.BadRequest);
            if (Chapters.All(c => c.Number != number)) return Status(HttpStatusCode.NotFound);

            searching = Chapters.Where(c => c.Number == number);
        }

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Select(w => w.ToLowerInvariant())
            .ToList();

        var hits = searching
            .SelectMany(c => c.Passages.Select((p, i) => (Chapter: c, Index: i, Passage: p)))
            .Where(h => words.Any(w =>
                h.Passage.Heading.Contains(w, StringComparison.OrdinalIgnoreCase)
                || h.Passage.Prose.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var rows = hits.Select(h => $$"""
            {"chapter":{{h.Chapter.Number}},"chapterTitle":{{Quote(h.Chapter.Title)}},
             "index":{{h.Index}},"heading":{{Quote(h.Passage.Heading)}},"printedPage":21,
             "sourceRef":{{Quote($"Ultimate Edition, Ch.{h.Chapter.Number} {h.Chapter.Title}, pp.1-2")}},
             "matchedTerms":[{{string.Join(",", words.Select(Quote))}}],
             "matchedHeading":{{Lower(words.Any(w =>
                 h.Passage.Heading.Contains(w, StringComparison.OrdinalIgnoreCase)))}},
             "snippet":{{Quote(Window(h.Passage.Prose))}}}
            """);

        return Json($$"""
            {"query":{{Quote(query)}},"terms":[{{string.Join(",", words.Select(Quote))}}],
             "found":{{hits.Count}},
             "nothingMatchedByHeading":{{Lower(!hits.Any(h => words.Any(w =>
                 h.Passage.Heading.Contains(w, StringComparison.OrdinalIgnoreCase))))}},
             "results":[{{string.Join(",", rows)}}]}
            """);
    }

    /// <summary>
    /// A window of the passage, the way the server sends one.
    ///
    /// <para><b>It used to be the whole passage, and that made a stub that lied about the
    /// shape.</b> A result row prints its snippet as one line of small print; handing it a Power's
    /// entire 200-word entry drew the whole thing there, above the panel that sets the same text
    /// out properly — which is what the rules proof captured, and it read as the structured
    /// version being pointless. The real server windows around the word that matched.</para>
    /// </summary>
    private static string Window(string prose) =>
        prose.Length <= 120 ? prose : prose[..120].TrimEnd() + "…";

    /// <summary>One passage in full, by where it is, or 401/404.</summary>
    private Task<HttpResponseMessage> Passage(HttpRequestMessage request)
    {
        if (SignedIn is null) return Status(HttpStatusCode.Unauthorized);

        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);

        if (!int.TryParse(query["chapter"], out var number)
            || !int.TryParse(query["index"], out var at)) return Status(HttpStatusCode.BadRequest);

        var chapter = Chapters.FirstOrDefault(c => c.Number == number);
        if (chapter is null || at < 0 || at >= chapter.Passages.Count) return Status(HttpStatusCode.NotFound);

        var passage = chapter.Passages[at];

        return Json($$"""
            {"chapter":{{number}},"chapterTitle":{{Quote(chapter.Title)}},"index":{{at}},
             "heading":{{Quote(passage.Heading)}},"printedPage":21,
             "text":{{Quote(passage.Prose)}},
             "sourceRef":{{Quote($"Ultimate Edition, Ch.{number} {chapter.Title}, pp.1-2")}}}
            """);
    }

    /// <summary>One chapter of the book, as far as the browser can tell.</summary>
    public sealed record FakeChapter(int Number, string Title, List<FakePassage> Passages);

    /// <summary>
    /// One passage in it.
    ///
    /// <para>The body is <c>Prose</c> rather than <c>Text</c> because <c>Text</c> is the name of
    /// this stub's own response helper, and a nested record whose property hides a method of the
    /// class around it is a name two readers will resolve differently.</para>
    /// </summary>
    public sealed record FakePassage(string Heading, string Prose);

    /// <summary>
    /// What the book holds, for a page that wants results rather than an empty box.
    ///
    /// <para>Two chapters and three passages: enough for a search to answer, for one result to
    /// have matched by heading and another only in its body, and for the contents to list more
    /// than one row. Not a copy of the book — the real corpus is searched by the real code in the
    /// accounts suite.</para>
    /// </summary>
    public List<FakeChapter> Chapters { get; } =
    [
        new(2, "Characters",
        [
            new("KNOCKBACK", "There are times you want to knock someone across the room."),
            new("TRAIT CAP", "The highest rank any single Trait may reach at this power level."),
        ]),
        new(4, "Combat",
        [
            new("SURPRISE", "Acting before somebody who has not noticed you, and what knockback does then."),
        ]),
    ];

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
