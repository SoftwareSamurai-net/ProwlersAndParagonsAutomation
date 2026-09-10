using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Where one character stands in one campaign — the answer to "which sheet do I print at the
/// table".
/// </summary>
public enum CampaignStanding
{
    /// <summary>
    /// This character is in no campaign, so the question does not arise. Distinct from
    /// <see cref="NotSubmitted"/>, which is a character that <em>is</em> in one and has never sent
    /// anything: telling a reader "not submitted" about a character nobody has put in a game would
    /// be an answer to a question they have not asked.
    /// </summary>
    NotInACampaign,

    /// <summary>In a campaign, and nothing has ever been sent for approval.</summary>
    NotSubmitted,

    /// <summary>A snapshot is waiting for the GM's decision.</summary>
    ChangesPending,

    /// <summary>
    /// The GM turned the last snapshot down.
    ///
    /// <para><b>This value is the whole of a defect, not a refinement.</b> Rejecting clears the
    /// pending slot and leaves the clone exactly as it was — which is what rejecting *means* — so
    /// the two booleans this standing was derived from came back to precisely the state they were
    /// in before the player sent anything. A rejection and an approval of an earlier snapshot
    /// produced the identical sentence, and a rejection of a first submission produced <see
    /// cref="NotSubmitted"/>. There was no way for a player to learn that a decision had been
    /// made at all, let alone which way it went.</para>
    ///
    /// <para><b>It persists until they resubmit</b>, and needs no acknowledgement to clear it: a
    /// new snapshot fills the pending slot, which shadows this, and the next decision overwrites
    /// the fact itself.</para>
    /// </summary>
    ChangesTurnedDown,

    /// <summary>
    /// The campaign is holding a clone of this character and nothing is waiting. This is the sheet
    /// that counts at the table.
    /// </summary>
    Approved,

    /// <summary>
    /// The campaign cannot be asked. A signed-out visitor, a laptop with no network, a session
    /// that ended while the tab was open.
    ///
    /// <para><b>Not folded into <see cref="NotInACampaign"/>, and the reason is the whole of why
    /// this value exists.</b> "This character is in no campaign" and "I could not find out" are
    /// different sentences, and collapsing them tells somebody their character is out of a game it
    /// is still in — the same fault <c>SheetPage</c> already records for a character that could
    /// not be read.</para>
    /// </summary>
    Unknown,
}

/// <summary>
/// What the GM last decided about a membership, or <see cref="None"/> before any decision.
///
/// <para><b>Read off the wire's own word rather than off two booleans</b>, because the booleans
/// cannot carry it: approving and rejecting leave them in states that are, respectively, what an
/// approval of any earlier snapshot looks like and what having sent nothing looks like.</para>
/// </summary>
public enum MembershipDecision
{
    /// <summary>Nothing has been decided. An ordinary state, not a missing value.</summary>
    None,

    /// <summary>The last snapshot was accepted, and is the campaign's clone.</summary>
    Approved,

    /// <summary>The last snapshot was turned down. The clone is whatever it was before.</summary>
    Rejected,
}

/// <summary>
/// One row of the approval list, on either side of it.
/// </summary>
/// <param name="Id"><c>m_</c> plus 22 URL-safe characters, minted by the server.</param>
/// <param name="CampaignId">Which campaign. <c>g_</c> plus 22.</param>
/// <param name="CharacterId">
/// Which of the caller's own characters, or null when the caller is the GM — a GM approves a
/// membership and is never told the id of a row in somebody else's account.
/// </param>
/// <param name="Label">What the character goes by. Opaque on the wire, like a character's.</param>
/// <param name="HasApproved">Whether the campaign is holding a clone.</param>
/// <param name="ApprovedAt">When the clone was accepted, in Unix milliseconds, or null.</param>
/// <param name="HasPending">Whether a snapshot is waiting for a decision.</param>
/// <param name="PendingAt">When it was sent, or null.</param>
/// <param name="PendingVersion">
/// The compare-and-swap token. A decision sends this back, and a mismatch is refused — see
/// <see cref="ApiMembershipStore.ApproveAsync"/>.
/// </param>
/// <param name="Decision">
/// What the GM last decided, or <see cref="MembershipDecision.None"/>. <b>Not derivable from the
/// two booleans</b>, which is the whole reason it is on the wire: a rejection leaves them exactly
/// as an approval of an earlier snapshot does.
/// </param>
/// <param name="PlayerKey">
/// A hint that this row shares an account with another one in the same game — never which
/// account. <b>Only ever set on a GM's own inbox row</b>, which is what
/// <see cref="ApiMembershipStore.InboxAsync"/> reads it off; <see cref="ApiMembershipStore.MineAsync"/>
/// never binds it at all, so a player's own list carries null here even if a future server ever
/// sent one by mistake. <b>Null on an older server</b>, deliberately — a server that has not
/// learned to send this must not be read as "every row is its own player", which would be a claim
/// this build invented rather than one the server made; see <see cref="CampaignAssets.Ledger"/>,
/// which groups by this key and treats null as "groups with nobody, ever" for exactly that
/// reason.
/// </param>
public sealed record MembershipSummary(
    string Id,
    string CampaignId,
    string? CharacterId,
    string Label,
    bool HasApproved,
    long? ApprovedAt,
    bool HasPending,
    long? PendingAt,
    int PendingVersion,
    MembershipDecision Decision = MembershipDecision.None,
    string? PlayerKey = null)
{
    /// <summary>
    /// Where this character stands. Read off the two slots rather than stored, because a third
    /// field saying the same thing is a third field that can disagree with them.
    /// </summary>
    /// <summary>
    /// Where this character stands, in the order the sentences displace one another.
    ///
    /// <para><b>A waiting snapshot comes first</b>, because it is the live fact and it is what
    /// shadows a previous decision without anything having to clear one. <b>A rejection comes
    /// before the clone</b>, because a clone is what a rejection deliberately leaves untouched —
    /// putting <see cref="CampaignStanding.Approved"/> ahead of it is the bug this value exists to
    /// fix, not a reordering of equals.</para>
    /// </summary>
    public CampaignStanding Standing =>
        HasPending ? CampaignStanding.ChangesPending
        : Decision == MembershipDecision.Rejected ? CampaignStanding.ChangesTurnedDown
        : HasApproved ? CampaignStanding.Approved
        : CampaignStanding.NotSubmitted;
}

/// <summary>
/// One membership in full: the campaign's clone, and the snapshot waiting for a decision.
/// </summary>
/// <param name="Id">The membership.</param>
/// <param name="CampaignId">Which campaign.</param>
/// <param name="CharacterId">The caller's own character, or null for the GM.</param>
/// <param name="Label">What the character goes by.</param>
/// <param name="IsGm">Which side of the membership the caller is on.</param>
/// <param name="Approved">The clone, or null before the first approval.</param>
/// <param name="Pending">The snapshot waiting, or null when nothing is.</param>
/// <param name="PendingVersion">What a decision has to name.</param>
public sealed record MembershipDetail(
    string Id,
    string CampaignId,
    string? CharacterId,
    string Label,
    bool IsGm,
    CharacterSheet? Approved,
    CharacterSheet? Pending,
    int PendingVersion);

/// <summary>What became of a decision.</summary>
public enum DecisionOutcome
{
    /// <summary>Applied. The clone is the snapshot, or the snapshot is gone.</summary>
    Done,

    /// <summary>
    /// Refused, because the snapshot moved while the GM was looking at it. The newer one is
    /// attached — see <see cref="Decision.Newer"/>.
    ///
    /// <para><b>This is the whole reason a decision carries a version.</b> Without it: the GM
    /// reads snapshot A, the player resubmits B, the GM clicks Approve, and B is approved
    /// unseen.</para>
    /// </summary>
    Stale,

    /// <summary>Refused, because there is nothing waiting for a decision at all.</summary>
    NothingWaiting,

    /// <summary>Nothing could be reached, so nothing is known to have happened.</summary>
    Unreachable,
}

/// <summary>
/// What became of an attempt to end a membership — a player leaving, or a GM removing somebody.
///
/// <para><b>Three values rather than a bool</b>, because the one refusal is a state the reader can
/// do something about and "it did not work" is not the same sentence as "that game is not there
/// any more".</para>
/// </summary>
public enum LeftOutcome
{
    /// <summary>
    /// The membership is not there. <b>Also the answer to ending one twice</b>, and to ending one
    /// belonging to somebody else: the server answers on the end state rather than on whether this
    /// particular request was the one that changed it.
    /// </summary>
    Done,

    /// <summary>
    /// Refused: this account is the GM and has deleted the game. The player's row deliberately
    /// outlives a deleted campaign so that writing it back is a complete undo, so a removal from
    /// one is refused rather than reported as done.
    /// </summary>
    GameIsGone,

    /// <summary>Nothing could be reached, so nothing is known to have happened.</summary>
    Unreachable,
}

/// <summary>
/// What a decision answered.
/// </summary>
/// <param name="Outcome">Which of the four.</param>
/// <param name="Newer">
/// The snapshot the GM had not seen, on a <see cref="DecisionOutcome.Stale"/> refusal — so the
/// screen can redraw the diff rather than telling somebody to go and look again. Null otherwise.
/// </param>
/// <param name="NewerVersion">Its version, which a second decision has to name.</param>
public sealed record Decision(
    DecisionOutcome Outcome, CharacterSheet? Newer = null, int NewerVersion = 0);

/// <summary>
/// A campaign's clones and the snapshots waiting for a decision, kept on the server.
///
/// <para><b>There is no local half and there must not be one.</b> Every other store in this
/// directory has a browser copy beside its HTTP one, because a character is worth keeping for
/// somebody who has not signed in. A membership is not: it exists so that two accounts can hand a
/// snapshot between them, and a membership in one browser is a promise to somebody who can never
/// be told. The whole feature is account-only — see <see cref="AccountCampaignStore"/>, which was
/// changed to match.</para>
///
/// <para><b>Nothing here may throw.</b> A site deployed without its API, a laptop with no
/// network, and a session that ended while the tab was open are all the same answer: nothing is
/// known — carry on. <see cref="JsonException"/> is in the unreachable list for the reason
/// <see cref="ApiCharacterStore"/> records: an unmatched path is served as the app's own page with
/// a 200, so a success is not proof of an answer.</para>
///
/// <para><b>A payload is read through <see cref="StoredCharacter"/>, exactly as a character's
/// is.</b> That reader costs and validates the sheet before answering, so a snapshot this build
/// cannot make sense of comes back null rather than reaching a screen half-formed — and the
/// engine, not this class, is what decides whether it is a character at all.</para>
/// </summary>
public sealed class ApiMembershipStore
{
    private const string List = "api/memberships";

    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;

    /// <summary>
    /// The one reader for a character payload, built the way <see cref="ApiCharacterStore"/>
    /// builds its own: a snapshot is a character, and a second reader is a second chance to
    /// disagree about what one is.
    /// </summary>
    private readonly StoredCharacter _payload;

    public ApiMembershipStore(HttpClient http, CostCalculator costs, CharacterValidator validator)
    {
        _http = http;
        _payload = new StoredCharacter(costs, validator);
    }

    /// <summary>
    /// Every campaign the caller's own characters are in, with where each stands.
    ///
    /// <para>Null — not an empty list — when nothing could be asked. A screen drawing a standing
    /// has to be able to tell "in no campaign" from "I could not find out"; an empty list says the
    /// first, and saying it wrongly tells somebody their character is out of a game it is still
    /// in.</para>
    /// </summary>
    public async Task<IReadOnlyList<MembershipSummary>?> MineAsync() => await ListAsync(List);

    /// <summary>
    /// Every membership of every campaign the caller runs — what is waiting, and what is settled.
    ///
    /// <para>Across every campaign in one call, because both screens want it: the campaign list
    /// needs a waiting count per game and the approval screen needs the rows of one. Two calls
    /// could disagree about the count.</para>
    ///
    /// <para><b>Its own wire shape rather than <see cref="ListAsync"/>'s</b>, because this is the
    /// one list that carries <c>playerKey</c> — a player's own list never does, and
    /// <c>AccountsContractTests.TheMembershipKeysOnTheWireAreSpelledTheSameAtBothEnds</c> holds
    /// <see cref="WiredRow"/> to exactly what <c>asPlayerRow</c> sends. Giving both routes one
    /// record would mean that guard either missing this field on the GM's side or failing on the
    /// player's for a field it correctly never sends.</para>
    /// </summary>
    public async Task<IReadOnlyList<MembershipSummary>?> InboxAsync()
    {
        try
        {
            using var response = await _http.GetAsync($"{List}/inbox");
            if (!response.IsSuccessStatusCode) return null;

            var listed = await response.Content.ReadFromJsonAsync<WiredGmList>(Wire);
            if (listed?.Memberships is null) return null;

            return [.. listed.Memberships
                .Where(m => m is { Id.Length: > 0, CampaignId.Length: > 0 })
                .Select(m => new MembershipSummary(
                    m.Id!, m.CampaignId!, m.CharacterId, m.Label ?? "Unnamed character",
                    m.HasApproved, m.ApprovedAt, m.HasPending, m.PendingAt, m.PendingVersion,
                    Decided(m.Decision), m.PlayerKey))];
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    private async Task<IReadOnlyList<MembershipSummary>?> ListAsync(string address)
    {
        try
        {
            using var response = await _http.GetAsync(address);
            if (!response.IsSuccessStatusCode) return null;

            var listed = await response.Content.ReadFromJsonAsync<WiredList>(Wire);
            if (listed?.Memberships is null) return null;

            return [.. listed.Memberships
                .Where(m => m is { Id.Length: > 0, CampaignId.Length: > 0 })
                .Select(m => new MembershipSummary(
                    m.Id!, m.CampaignId!, m.CharacterId, m.Label ?? "Unnamed character",
                    m.HasApproved, m.ApprovedAt, m.HasPending, m.PendingAt, m.PendingVersion,
                    Decided(m.Decision)))];
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>
    /// The wire's word for the last decision, or <see cref="MembershipDecision.None"/>.
    ///
    /// <para><b>A word this build does not know reads as no decision rather than as a
    /// rejection.</b> A later version of the server could spell a third outcome, and defaulting an
    /// unknown one to the value that changes what a player is told would be this build inventing a
    /// decision nobody made — the same rule <see cref="StoredCharacter"/> follows for an envelope
    /// it cannot open.</para>
    /// </summary>
    private static MembershipDecision Decided(string? wire) => wire switch
    {
        "approved" => MembershipDecision.Approved,
        "rejected" => MembershipDecision.Rejected,
        _ => MembershipDecision.None,
    };

    /// <summary>
    /// One membership in full, with both payloads read through the engine's own reader.
    /// </summary>
    public async Task<MembershipDetail?> ReadAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        try
        {
            using var response = await _http.GetAsync($"{List}/{Uri.EscapeDataString(id)}");
            if (!response.IsSuccessStatusCode) return null;

            var read = await response.Content.ReadFromJsonAsync<WiredDetail>(Wire);
            if (read?.Id is not { Length: > 0 } || read.CampaignId is not { Length: > 0 })
            {
                return null;
            }

            return new MembershipDetail(
                read.Id, read.CampaignId, read.CharacterId,
                read.Label ?? "Unnamed character",
                string.Equals(read.Role, "gm", StringComparison.Ordinal),
                _payload.Read(read.Approved)?.Sheet,
                _payload.Read(read.Pending)?.Sheet,
                read.PendingVersion);
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>
    /// Put one of the caller's characters into a campaign, by a code somebody read out to them.
    ///
    /// <para><b>What comes back is the campaign</b>, because the browser can decide nothing about
    /// a tier without it — and <see cref="CampaignJoin.Apply"/> is what decides: inherit into an
    /// empty field, report a disagreement, and never repair one.</para>
    ///
    /// <para>Null for a code no campaign is using, a code the GM has replaced, a code that is not
    /// a code at all, too many attempts, and a server that could not be reached. Those are five
    /// different sentences to a reader and one answer here — see <see cref="JoinRefusal"/>, which
    /// is what the screen tells them apart with.</para>
    /// </summary>
    public async Task<(string Id, Campaign Campaign)?> JoinAsync(
        string code, string characterId, string label)
    {
        try
        {
            using var body = Body(new Joining(code, characterId, label));
            using var response = await _http.PostAsync($"{List}/join", body);

            _lastJoinRefusal = response.StatusCode switch
            {
                HttpStatusCode.OK => JoinRefusal.None,
                HttpStatusCode.NotFound => JoinRefusal.NoSuchCampaign,
                HttpStatusCode.BadRequest => JoinRefusal.NotACode,
                HttpStatusCode.TooManyRequests => JoinRefusal.TooManyTries,
                _ => JoinRefusal.Unreachable,
            };

            if (!response.IsSuccessStatusCode) return null;

            var joined = await response.Content.ReadFromJsonAsync<WiredJoin>(Wire);
            if (joined?.Id is not { Length: > 0 }) return null;

            // The campaign's own payload, read by the one reader both stores share — so a campaign
            // made on a laptop and joined on a phone is spelled the same either way.
            var campaign = StoredCampaign.Read(joined.Payload);
            if (campaign is null) return null;

            return (joined.Id, campaign);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            _lastJoinRefusal = JoinRefusal.Unreachable;
            return null;
        }
    }

    /// <summary>Why the last join did not happen. <see cref="JoinRefusal.None"/> before any.</summary>
    public JoinRefusal LastJoinRefusal => _lastJoinRefusal;

    private JoinRefusal _lastJoinRefusal = JoinRefusal.None;

    /// <summary>
    /// Send a snapshot for approval. The version it was given, or null if it went nowhere.
    ///
    /// <para>The sheet is written down by <see cref="StoredCharacter.Write"/>, which is the same
    /// envelope an ordinary save uses — so the GM reads exactly the bytes the player's own store
    /// holds, and there is one writer rather than two that could drift.</para>
    /// </summary>
    public async Task<int?> SubmitAsync(string id, CharacterSheet sheet, SheetMode mode)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        try
        {
            using var body = Body(new Sending(
                SheetLabel(sheet), StoredCharacter.Write(sheet, mode)));

            using var response = await _http.PutAsync(
                $"{List}/{Uri.EscapeDataString(id)}/submission", body);

            if (!response.IsSuccessStatusCode) return null;

            return (await response.Content.ReadFromJsonAsync<WiredVersion>(Wire))?.Version;
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>Accept the snapshot at <paramref name="version"/>, and nothing else.</summary>
    public Task<Decision> ApproveAsync(string id, int version) => DecideAsync(id, "approve", version);

    /// <summary>Turn down the snapshot at <paramref name="version"/>, and nothing else.</summary>
    public Task<Decision> RejectAsync(string id, int version) => DecideAsync(id, "reject", version);

    private async Task<Decision> DecideAsync(string id, string what, int version)
    {
        try
        {
            using var body = Body(new Deciding(version));
            using var response = await _http.PostAsync(
                $"{List}/{Uri.EscapeDataString(id)}/{what}", body);

            if (response.IsSuccessStatusCode) return new Decision(DecisionOutcome.Done);

            if (response.StatusCode != HttpStatusCode.Conflict)
            {
                return new Decision(DecisionOutcome.Unreachable);
            }

            var refused = await response.Content.ReadFromJsonAsync<WiredRefusal>(Wire);

            // Nothing waiting and a snapshot that moved are both 409 and are different sentences.
            // The server distinguishes them by whether it attached one.
            if (refused?.Pending is not { Length: > 0 })
            {
                return new Decision(DecisionOutcome.NothingWaiting);
            }

            return new Decision(
                DecisionOutcome.Stale,
                _payload.Read(refused.Pending)?.Sheet,
                refused.PendingVersion);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return new Decision(DecisionOutcome.Unreachable);
        }
    }

    /// <summary>
    /// End a membership: the player walking out, or the GM removing somebody.
    ///
    /// <para><b>One address and two meanings, and the server decides which from the owner column
    /// that matches</b> — so nothing here sends a role, and nothing here could claim one it does
    /// not have. A third account's request ends nothing and is told nothing.</para>
    ///
    /// <para><b>The row goes and the campaign's clone with it.</b> Deleting a campaign keeps its
    /// memberships so that writing it back is a complete undo; this is the opposite act and has no
    /// undo behind it, which is why both controls that reach it ask twice.</para>
    ///
    /// <para><b>The refusal comes back in the result rather than out of band.</b>
    /// <see cref="LastJoinRefusal"/> is the older shape, and PROGRESS.md records returning it in
    /// the result as the smaller surface — so nothing new is written the other way.</para>
    /// </summary>
    public async Task<LeftOutcome> LeaveAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return LeftOutcome.Unreachable;

        try
        {
            using var response = await _http.DeleteAsync($"{List}/{Uri.EscapeDataString(id)}");

            if (response.IsSuccessStatusCode) return LeftOutcome.Done;

            return response.StatusCode == HttpStatusCode.Conflict
                ? LeftOutcome.GameIsGone
                : LeftOutcome.Unreachable;
        }
        catch (Exception e) when (IsUnreachable(e)) { return LeftOutcome.Unreachable; }
    }

    /// <summary>
    /// The campaign this membership names, as the table has it <em>now</em>.
    ///
    /// <para><b>The one campaign read a player can make, and the reason it exists.</b> Every other
    /// address under <c>/api/campaigns</c> is scoped to the account that owns the game, so a member
    /// resolves no campaign at all through <see cref="AccountCampaignStore"/> — which is why
    /// <see cref="CampaignJoin.Inspect"/> answers <c>UNKNOWN_CAMPAIGN</c> to one. The server
    /// authorises this by the caller's own membership row instead, and answers the campaign's
    /// payload and nothing else.</para>
    ///
    /// <para><b>It is a live view and never a write.</b> What is in force for the character is the
    /// copy taken when it joined, and nothing here changes that — <c>Campaigns.razor</c> draws the
    /// two beside each other and says which is which. Refreshing the character silently is the one
    /// thing <see cref="CampaignJoin"/> exists not to do.</para>
    ///
    /// <para>Null for a membership that is not the caller's, a campaign the GM has deleted, a
    /// payload this build cannot read, and a server that could not be reached. All four mean the
    /// same thing to the screen: there is no live table to put beside the copy, so draw the copy
    /// alone, which is exactly what it drew before this existed.</para>
    /// </summary>
    public async Task<Campaign?> TableAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        try
        {
            using var response =
                await _http.GetAsync($"{List}/{Uri.EscapeDataString(id)}/table");

            if (!response.IsSuccessStatusCode) return null;

            var read = await response.Content.ReadFromJsonAsync<WiredTable>(Wire);

            // The one reader both stores share, so a campaign made on a laptop and read live on a
            // phone is spelled the same either way — the same call `JoinAsync` makes on the same
            // bytes, and never a second reader that could disagree about what a campaign is.
            return read?.Payload is { Length: > 0 } payload ? StoredCampaign.Read(payload) : null;
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>
    /// Replace a campaign's join code, and hand the new one back.
    ///
    /// <para>On the campaign rather than on a membership, because a code is a property of the game
    /// — and it is a <c>POST</c> rather than a <c>PUT</c> because the caller does not choose the
    /// value. Nobody already in the campaign is evicted.</para>
    /// </summary>
    public async Task<string?> NewCodeAsync(string campaignId)
    {
        try
        {
            using var body = Body(new object());
            using var response = await _http.PostAsync(
                $"api/campaigns/{Uri.EscapeDataString(campaignId)}/code", body);

            if (!response.IsSuccessStatusCode) return null;

            var minted = await response.Content.ReadFromJsonAsync<WiredCode>(Wire);

            return minted?.JoinCode is { Length: > 0 } code ? code : null;
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>
    /// The name a snapshot is listed under. The sheet's own, never empty — the same rule
    /// <see cref="SavedCharacters.LabelFor"/> applies, spelled once there and followed here.
    /// </summary>
    private static string SheetLabel(CharacterSheet sheet) =>
        string.IsNullOrWhiteSpace(sheet.Name) ? "Unnamed character" : sheet.Name.Trim();

    private static StringContent Body<T>(T value) =>
        new(JsonSerializer.Serialize(value, Wire), Encoding.UTF8, "application/json");

    /// <summary>Every way the server can fail to answer. All of them mean the same thing here.</summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException           // no network, DNS, TLS, a refused connection
          or TaskCanceledException          // a timeout, or the host going away
          or OperationCanceledException
          or ObjectDisposedException
          or JsonException                  // an answer that is not the answer
          or NotSupportedException          // a content type this cannot read
          or InvalidOperationException;     // no base address

    private sealed record WiredList(
        [property: JsonPropertyName("memberships")] WiredRow[]? Memberships);

    private sealed record WiredRow(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("campaignId")] string? CampaignId,
        [property: JsonPropertyName("characterId")] string? CharacterId,
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("hasApproved")] bool HasApproved,
        [property: JsonPropertyName("approvedAt")] long? ApprovedAt,
        [property: JsonPropertyName("hasPending")] bool HasPending,
        [property: JsonPropertyName("pendingAt")] long? PendingAt,
        [property: JsonPropertyName("pendingVersion")] int PendingVersion,
        [property: JsonPropertyName("decision")] string? Decision);

    /// <summary>
    /// The GM's inbox list, which is <see cref="WiredList"/> plus <see cref="WiredGmRow.PlayerKey"/>
    /// — see <see cref="InboxAsync"/> for why this is not the shared shape.
    /// </summary>
    private sealed record WiredGmList(
        [property: JsonPropertyName("memberships")] WiredGmRow[]? Memberships);

    private sealed record WiredGmRow(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("campaignId")] string? CampaignId,
        [property: JsonPropertyName("characterId")] string? CharacterId,
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("hasApproved")] bool HasApproved,
        [property: JsonPropertyName("approvedAt")] long? ApprovedAt,
        [property: JsonPropertyName("hasPending")] bool HasPending,
        [property: JsonPropertyName("pendingAt")] long? PendingAt,
        [property: JsonPropertyName("pendingVersion")] int PendingVersion,
        [property: JsonPropertyName("decision")] string? Decision,
        [property: JsonPropertyName("playerKey")] string? PlayerKey);

    private sealed record WiredDetail(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("campaignId")] string? CampaignId,
        [property: JsonPropertyName("characterId")] string? CharacterId,
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("role")] string? Role,
        [property: JsonPropertyName("approved")] string? Approved,
        [property: JsonPropertyName("pending")] string? Pending,
        [property: JsonPropertyName("pendingVersion")] int PendingVersion);

    private sealed record WiredJoin(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("campaignId")] string? CampaignId,
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("payload")] string? Payload);

    /// <summary>
    /// What the live-table read answers. <b>Two keys, and the smallness is the design</b> — the
    /// server sends the campaign's id and its opaque payload and nothing else, so there is no
    /// account id, no label, no join code and nothing about another member to bind here.
    /// </summary>
    private sealed record WiredTable(
        [property: JsonPropertyName("campaignId")] string? CampaignId,
        [property: JsonPropertyName("payload")] string? Payload);

    private sealed record WiredVersion([property: JsonPropertyName("version")] int Version);

    private sealed record WiredRefusal(
        [property: JsonPropertyName("pendingVersion")] int PendingVersion,
        [property: JsonPropertyName("pending")] string? Pending);

    private sealed record WiredCode([property: JsonPropertyName("joinCode")] string? JoinCode);

    private sealed record Joining(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("characterId")] string CharacterId,
        [property: JsonPropertyName("label")] string Label);

    private sealed record Sending(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("payload")] string Payload);

    private sealed record Deciding([property: JsonPropertyName("version")] int Version);
}

/// <summary>
/// Why a join did not happen — five states a reader is owed different sentences for.
///
/// <para><b>A refusal that said nothing would be the worst outcome here.</b> A code box that
/// clears itself and does nothing is indistinguishable from a control that is not wired up, which
/// is the rule <c>DiscardedCharacter</c> and <c>CharacterManager</c> both already follow.</para>
/// </summary>
public enum JoinRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>That is not a code at all — the wrong length, or letters no code contains.</summary>
    NotACode,

    /// <summary>
    /// No campaign is using it. Deliberately one state rather than two: a code that never existed
    /// and one the GM has replaced answer identically, so that asking twice cannot tell somebody a
    /// code was once valid.
    /// </summary>
    NoSuchCampaign,

    /// <summary>Too many codes tried too quickly.</summary>
    TooManyTries,

    /// <summary>Nothing was reached, so nothing is known.</summary>
    Unreachable,
}

/// <summary>
/// What a standing is called on screen.
///
/// <para><b>Here rather than in <see cref="Labels"/>, and the line is worth stating.</b> That class
/// turns a rules key into readable text — a cost variant, a grade — for the handful of values the
/// data gives no printed name to. A standing is not in the data at all: it is a fact about a
/// decision somebody has or has not made, and naming it is presentation about this feature.</para>
///
/// <para><b>The campaign's name is in the sentence where there is one</b>, because the question
/// this answers is "which sheet do I print at the table" and a bare "Approved" beside a character
/// in three games answers it for none of them.</para>
/// </summary>
public static class Standings
{
    /// <summary>Where a character stands, in words.</summary>
    /// <param name="standing">The standing.</param>
    /// <param name="campaign">
    /// What the game is called, where the row is about one campaign. Omitted on a screen that has
    /// already said which campaign it is talking about.
    /// </param>
    public static string Say(CampaignStanding standing, string? campaign = null) => standing switch
    {
        CampaignStanding.Approved =>
            string.IsNullOrWhiteSpace(campaign) ? "Approved" : $"Approved for {campaign}",

        CampaignStanding.ChangesPending => "Changes pending",

        // **Says which way it went, and does not offer to explain.** There is no reason on the
        // wire and no place a GM types one, so a sentence promising more than the row holds would
        // send somebody looking for something that is not there.
        CampaignStanding.ChangesTurnedDown =>
            string.IsNullOrWhiteSpace(campaign)
                ? "Changes turned down"
                : $"Changes turned down for {campaign}",
        CampaignStanding.NotSubmitted => "Not submitted",

        // **Said rather than left blank.** A character whose standing could not be read is not a
        // character out of the game, and a blank where a standing goes reads as the second.
        CampaignStanding.Unknown => "Standing not known",

        // Nothing at all: a character in no campaign has not been asked this question, and
        // answering it would be the app replying to something nobody said.
        _ => "",
    };
}
