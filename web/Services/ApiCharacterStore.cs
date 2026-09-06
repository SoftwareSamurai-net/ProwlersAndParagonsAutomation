using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>What a save was refused for, when it was.</summary>
public enum SaveOutcome
{
    /// <summary>Stored.</summary>
    Saved,

    /// <summary>The account is at its cap and this would have been another character.</summary>
    AccountIsFull,

    /// <summary>The server could not be reached, or refused for a reason worth nobody's time.</summary>
    NotSaved,
}

/// <summary>
/// Why a read of one character by id came back with nothing.
///
/// <para><b>Two answers rather than one null, because they are two different sentences to a
/// reader and the wrong one of them is a lie about their character.</b> A screen that says "open
/// it in the character manager" about a character sitting safely on a server behind a dropped
/// connection has sent somebody looking for a fault that is not theirs; a screen that says "try
/// again in a moment" about a character this build cannot open will be believed, and the reader
/// will keep trying. It is the same split <c>CampaignApproval</c> already makes between its
/// unreachable and unreadable arms.</para>
///
/// <para><b>And it is load-bearing rather than cosmetic.</b> The campaigns page reads the
/// pointer's character before it acts on the sheet on screen, and "there is no character at that
/// id" is what tells a fresh, empty slot — where there is nothing to lose — apart from a real
/// character that could not be reached, where acting would risk writing over it.</para>
/// </summary>
public enum ReadRefusal
{
    /// <summary>Nothing was refused: the character was read.</summary>
    None,

    /// <summary>
    /// The server could not be reached, answered with something that is not an answer, or no
    /// longer knows who is asking. <b>Nothing at all is known about whether the character is
    /// there</b>, which is what makes this the dangerous one.
    /// </summary>
    Unreachable,

    /// <summary>
    /// The server answered, and there is no character at that id this build can open — it is not
    /// on the account, or its payload is one this version cannot read. Either way, reading it
    /// again in a moment will not change the answer.
    /// </summary>
    NotThere,
}

/// <summary>
/// Keeps an account's characters on the server, so they are there in another browser.
///
/// <para>The addresses and their meanings are <c>docs/CHARACTERS-API.md</c>, which is also what
/// the server was built against. <c>AccountsContractTests</c> compares the two — and it is worth
/// knowing that it did its job during this change: the server dropped the old single-character
/// address while this file still asked for it, both language suites stayed green, and that one
/// test is what said so.</para>
///
/// <para><b>The list lives on the server; which one is open lives in this browser.</b> That split
/// is deliberate. Your characters belong to your account and should follow you; which of them you
/// happen to have on screen is a fact about this tab, and syncing it would mean opening a laptop
/// and having a phone decide what you are looking at. So the current-id pointer stays in
/// <see cref="SavedCharacters"/>, under the same per-identity prefix, for both stores.</para>
///
/// <para><b>Nothing here may throw.</b> Restoring happens before the first render, so an exception
/// is not a lost character but an app that does not start. A site whose API is not deployed, a
/// laptop with no network, and a session that expired while the tab was open are all the same
/// answer: there is no character, start empty.</para>
///
/// <para><b>A failed save is silent, and that is a real cost stated plainly</b> — except for the
/// one refusal a person can act on, which is the account being full. That comes back as
/// <see cref="SaveOutcome.AccountIsFull"/> so a caller can say so; everything else is
/// <see cref="SaveOutcome.NotSaved"/>, because "the network went away" is not a sentence worth
/// interrupting somebody with on every keystroke.</para>
/// </summary>
public sealed class ApiCharacterStore : ICharacterStore
{
    private const string List = "api/characters";

    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;
    private readonly SavedCharacters _local;
    private readonly StoredCharacter _payload;

    /// <summary>Kept as well as handed to <see cref="_payload"/>, for the reason
    /// <see cref="SavedCharacters"/> keeps one: a PUT carries the character's spend now, and the
    /// two stores have to describe a character the same way. See
    /// <see cref="SavedCharacters.IndexFieldsFor"/>.</summary>
    private readonly CostCalculator _costs;

    /// <summary>The two the emptiness question needs — see <see cref="CharacterSession.HasNothingOnIt(CharacterSheet, CharacterValidator, RulesRepository)"/>.</summary>
    private readonly CharacterValidator _validator;

    private readonly RulesRepository _rules;

    public ApiCharacterStore(
        HttpClient http, SavedCharacters local, CostCalculator costs, CharacterValidator validator,
        RulesRepository rules)
    {
        _http = http;
        _local = local;
        _costs = costs;
        _validator = validator;
        _rules = rules;
        _payload = new StoredCharacter(costs, validator);
    }

    /// <summary>
    /// Raised when a write-through was refused rather than attempted, carrying the sentence to
    /// put in front of a reader. See <see cref="SaveAsync(CharacterSheet, SheetMode)"/>.
    ///
    /// <para><b>An event because a refusal has to be said out loud and this class has nowhere to
    /// say it.</b> The autosave's own overload implements <c>ICharacterStore</c>, which returns
    /// nothing and may not throw; a refusal that only returned early would be exactly the silent
    /// control every refusal in this application is written not to be.
    /// <c>AccountCharacterStore</c> passes it on and <c>MainLayout</c> prints it in the same
    /// live region as "Saved".</para>
    /// </summary>
    internal event Action<string>? WriteRefused;

    /// <summary>The account's characters, and the cap it is held to.</summary>
    public async Task<AccountCharacters> ListAsync()
    {
        try
        {
            using var response = await _http.GetAsync(List);
            if (!response.IsSuccessStatusCode) return AccountCharacters.Unknown;

            var listed = await response.Content.ReadFromJsonAsync<Wired>(Wire);

            // A site deployed without its API answers this address with the app's own index.html
            // and a 200, so an answer is not proof of an answer.
            return listed?.Characters is null
                ? AccountCharacters.Unknown
                : new AccountCharacters(
                    listed.Limit,
                    [.. listed.Characters
                        .Where(c => c.Id is { Length: > 0 })
                        .Select(c => new SavedCharacterSummary(
                            c.Id!, c.Label ?? "Unnamed character", c.UpdatedAt, c.CampaignId,
                            c.Kind, c.TierId, c.Spent))]);
        }
        catch (Exception e) when (IsUnreachable(e)) { return AccountCharacters.Unknown; }
    }

    /// <summary>
    /// Why the last <see cref="LoadAsync(string)"/> answered null, or <see cref="ReadRefusal.None"/>
    /// when it did not.
    ///
    /// <para><b>Beside the read rather than in its return type</b>, which is the shape
    /// <see cref="ApiMembershipStore.LastJoinRefusal"/> already uses for the same reason: this
    /// method is called from half a dozen places that want the character and nothing else, and
    /// widening it for the one caller that has to tell the two failures apart would make every
    /// other call site carry a value it has no use for.</para>
    /// </summary>
    public ReadRefusal LastReadRefusal { get; private set; } = ReadRefusal.None;

    /// <summary>One character by id, or null if there is none this build can trust.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            LastReadRefusal = ReadRefusal.NotThere;
            return null;
        }

        try
        {
            using var response = await _http.GetAsync($"{List}/{Uri.EscapeDataString(id)}");

            // A 404 is the ordinary answer for a character this account does not have, and a 401
            // for a session that ended while the tab was open. Neither is worth a word to a
            // reader who is only restoring a character — but they are not the same answer to one
            // deciding whether it is safe to write, so which it was is recorded above.
            if (!response.IsSuccessStatusCode)
            {
                LastReadRefusal = response.StatusCode == HttpStatusCode.NotFound
                    ? ReadRefusal.NotThere
                    : ReadRefusal.Unreachable;

                return null;
            }

            var read = _payload.Read(await response.Content.ReadAsStringAsync());

            // A payload this build cannot open is `NotThere` and not `Unreachable`: the server
            // answered, and asking it again will produce the same bytes.
            LastReadRefusal = read is null ? ReadRefusal.NotThere : ReadRefusal.None;

            return read;
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            LastReadRefusal = ReadRefusal.Unreachable;
            return null;
        }
    }

    /// <summary>
    /// Write one character, creating it if the id is new.
    ///
    /// <para>The label travels because <b>the server never parses a character</b> — it cannot read
    /// a name out of a payload it refuses to look inside, so the name is sent alongside.</para>
    /// </summary>
    public async Task<SaveOutcome> SaveAsync(
        string id, string label, CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            var (kind, tierId, spent) = SavedCharacters.IndexFieldsFor(sheet, mode, _costs);

            using var body = new StringContent(
                JsonSerializer.Serialize(
                    new Sending(
                        label, StoredCharacter.Write(sheet, mode), sheet.CampaignId,
                        kind, tierId, spent),
                    Wire),
                Encoding.UTF8,
                "application/json");

            using var response = await _http.PutAsync($"{List}/{Uri.EscapeDataString(id)}", body);

            if (response.IsSuccessStatusCode) return SaveOutcome.Saved;

            return response.StatusCode == HttpStatusCode.Conflict
                ? SaveOutcome.AccountIsFull
                : SaveOutcome.NotSaved;
        }
        catch (Exception e) when (IsUnreachable(e)) { return SaveOutcome.NotSaved; }
    }

    /// <summary>Throw one character away. Absent is not an error — the end state is the same.</summary>
    public async Task DeleteAsync(string id)
    {
        try { await _http.DeleteAsync($"{List}/{Uri.EscapeDataString(id)}"); }
        catch (Exception e) when (IsUnreachable(e)) { }
    }

    // ── ICharacterStore: the character that is open, which is all most callers want ──────────

    /// <summary>
    /// Writes whichever character is open.
    ///
    /// <para>The label is taken from the sheet's own name here rather than asked for, because this
    /// is the autosave path — it fires on every change and there is nobody to ask. A manager
    /// renaming a character calls <see cref="SaveAsync(string, string, CharacterSheet, SheetMode)"/>
    /// with the label it was given.</para>
    ///
    /// <para><b>A sheet with nothing worth keeping is never written through, and that is the
    /// whole fix for characters nobody built.</b> <c>PUT</c> below creates the account's row if
    /// the id is new — there is no separate "create" step — so before this guard existed,
    /// switching the palette or the sandbox toggle before choosing a tier fired
    /// <see cref="CharacterSession.NotifyChanged"/>, which fired this, which created a real,
    /// listed, empty character on the account the moment either happened. Skipping the write
    /// loses nothing: the edit is still on the sheet in memory, and the next substantive change
    /// — a tier, a Power, a name — saves it along with everything already there.
    /// <see cref="CharacterSession.IsWorthKeeping"/> is the same question
    /// <see cref="CharacterSession.HasSomethingToLose"/> answers for the manager's own
    /// confirmations, asked here because this path only ever has the sheet.</para>
    ///
    /// <para><b>The outcome is discarded on purpose, and the discard is written out rather than
    /// implied.</b> This overload implements <c>ICharacterStore</c>, which may not throw and has
    /// nowhere to report to — it runs before the first render, so an exception here is a blank
    /// page rather than a lost character. A save that failed over the network therefore goes
    /// unmentioned, which is a real gap: the character exists only in that tab and nobody is
    /// told. Closing it needs somewhere on screen to say so, which is Phase 5's "Saved"
    /// feedback — see <c>PROGRESS.md</c>. Until then <c>_ =</c> is the honest spelling, because
    /// it distinguishes a result nobody wanted from one somebody forgot.</para>
    /// </summary>
    public async Task SaveAsync(CharacterSheet sheet, SheetMode mode)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (!CharacterSession.IsWorthKeeping(sheet)) return;

        var id = await CurrentIdAsync();

        if (await WouldEmptyACharacter(id, sheet) is { } whose)
        {
            WriteRefused?.Invoke(
                $"{whose} was not overwritten: the sheet on screen has nothing on it. "
                + "Open that character again from your characters.");

            return;
        }

        _ = await SaveAsync(id, SavedCharacters.LabelFor(sheet), sheet, mode);
    }

    /// <summary>
    /// Whether writing <paramref name="sheet"/> at <paramref name="id"/> would replace a real
    /// character with an empty one — and if so, what that character is called.
    ///
    /// <para><b>The belt beside the campaigns page's own guard, and it is here because this is
    /// the last thing that touches the character before the server does.</b> The pointer and the
    /// sheet on screen are supposed to name the same character and nothing makes them: the
    /// sign-in page empties the session when the account's character cannot be read and leaves
    /// the pointer where it was, and the boot restore does the same for a read that throws. Any
    /// edit at all from that state — a palette switch, a tier, a join — fires this path, and
    /// before this it wrote the empty sheet over whatever the pointer named.
    /// <see cref="CharacterSession.IsWorthKeeping"/> cannot stop it, deliberately: a tier alone
    /// counts there, and a join writes one.</para>
    ///
    /// <para><b>It asks the account's own list rather than reading the character</b>, because
    /// the list is what says a row exists and what it is called without a payload being fetched
    /// and opened — and the label is the whole of what the sentence needs. A row is a character
    /// worth protecting when it has been priced or has a name of its own; the empty envelope
    /// this exists to refuse has neither.</para>
    ///
    /// <para><b>A list that could not be read refuses too</b>, which is the direction
    /// <see cref="AccountCharacters.IsFull"/> already takes and for the same reason: not knowing
    /// whether there is a character behind the pointer is not the same as knowing there is not,
    /// and the cost of being wrong is asymmetric — a sentence and a retry against somebody's
    /// character.</para>
    ///
    /// <para><b>Nothing is read on the ordinary path.</b> The list is asked for only once the
    /// sheet has already answered "nothing on it", which for a character anybody is building is
    /// false from the first Ability, Power or letter of a name.</para>
    /// </summary>
    private async Task<string?> WouldEmptyACharacter(string id, CharacterSheet sheet)
    {
        if (!CharacterSession.HasNothingOnIt(sheet, _validator, _rules)) return null;

        var listed = await ListAsync();

        if (listed.Limit is null) return "Your character";

        return listed.Characters.FirstOrDefault(c => c.Id == id) is { } row && WorthProtecting(row)
            ? row.Label
            : null;
    }

    /// <summary>
    /// Whether an index row describes a character rather than the empty envelope this refuses to
    /// write. Priced, or named: either is somebody having done something.
    /// </summary>
    private static bool WorthProtecting(SavedCharacterSummary row) =>
        row.Spent is > 0
        || !(string.IsNullOrWhiteSpace(row.Label)
             || string.Equals(row.Label, "Unnamed character", StringComparison.Ordinal));

    /// <summary>The open character, or null.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync() =>
        await LoadAsync(await CurrentIdAsync());

    /// <summary>Throws the open character away.</summary>
    public async Task ClearAsync() => await DeleteAsync(await CurrentIdAsync());

    /// <summary>
    /// Whether the account has any character at all, without restoring one.
    ///
    /// <para>Asked on the sign-in page, to decide whether to offer to keep what this browser was
    /// holding. <b>A failure answers "yes"</b>, which is the safe direction: it declines to offer a
    /// copy rather than offering to write over something that may well be there.</para>
    /// </summary>
    public async Task<bool> HasCharacterAsync()
    {
        var listed = await ListAsync();

        return listed.Limit is null || listed.Characters.Count > 0;
    }

    /// <summary>
    /// The id the account's autosave writes to.
    ///
    /// <para>Read from this browser, for the reason in the class remarks. The legacy id is the
    /// pointer's own default and means "the one slot this browser has always had" — on the server
    /// it is not a legal id, so it is mapped to a real one the first time an account saves.</para>
    ///
    /// <para><b>Internal rather than private because keeping a character before opening a fresh
    /// slot has to name the same id this would.</b> <c>AccountCharacterStore.StartAnotherAsync</c>
    /// writes the character on screen down explicitly rather than trusting the autosave that fired
    /// on the last edit, and the browser's raw pointer is not the id to write it at: it may still
    /// say <see cref="SavedCharacters.LegacyId"/>, which the server refuses as ill-formed. Asking
    /// here rather than assuming an autosave has already adopted one removes an ordering
    /// assumption, which is exactly the kind of thing that has been wrong here before.</para>
    /// </summary>
    internal async Task<string> CurrentIdAsync()
    {
        var current = await _local.CurrentIdAsync();

        return current == SavedCharacters.LegacyId ? await AdoptAnIdAsync() : current;
    }

    /// <summary>
    /// Decides which of the account's characters this browser has open, when it has no opinion yet.
    ///
    /// <para><b>It asks the server before minting anything, and that is the whole of "your
    /// character follows you to another browser".</b> The pointer is local — see the class remarks
    /// — so a browser signing in for the first time has none. Minting a fresh id there would ask
    /// the server for a character that cannot exist, get a 404, and start the visitor on an empty
    /// sheet while their character sat on the server under an id this browser had never heard of.
    /// The feature would have looked broken in precisely the case it was built for, and a test
    /// caught it.</para>
    ///
    /// <para>The most recently touched one is the one adopted, because that is what "carry on where
    /// I left off" means and it is the order the server already returns.</para>
    ///
    /// <para>Minting is the fallback rather than the rule: an account with nothing stored needs an
    /// id for its first save, and <see cref="SavedCharacters.LegacyId"/> is not one — it is this
    /// browser's private name for a slot, and the server refuses it as ill-formed, correctly.</para>
    /// </summary>
    private async Task<string> AdoptAnIdAsync()
    {
        var listed = await ListAsync();

        var adopted = listed.Characters.Count > 0
            ? listed.Characters[0].Id
            : SavedCharacters.NewId();

        await _local.SetCurrentAsync(adopted);

        return adopted;
    }

    /// <summary>
    /// Every way the server can fail to answer. All of them mean the same thing here.
    ///
    /// <para><see cref="JsonException"/> is in the list because a site deployed without its API
    /// answers with the app's own <c>index.html</c> and a 200 — <c>_redirects</c> serves every
    /// unmatched path that way — so "success" is not proof of an answer.</para>
    /// </summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException           // no network, DNS, TLS, a refused connection
          or TaskCanceledException          // a timeout, or the host going away
          or OperationCanceledException
          or ObjectDisposedException
          or JsonException                  // an answer that is not the answer
          or NotSupportedException          // a content type this cannot read
          or InvalidOperationException;     // no base address

    /// <summary>What the server sends for a list. Bound by name; see the contract.</summary>
    private sealed record Wired(
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("characters")] Listed[]? Characters);

    private sealed record Listed(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("updatedAt")] long UpdatedAt,
        [property: JsonPropertyName("campaignId")] string? CampaignId,
        [property: JsonPropertyName("kind")] string? Kind = null,
        [property: JsonPropertyName("tierId")] string? TierId = null,
        [property: JsonPropertyName("spent")] int? Spent = null);

    /// <summary>
    /// What the browser sends to store one. `payload` is opaque to the server.
    ///
    /// <para><b><c>campaignId</c> travels beside the payload for exactly the reason
    /// <c>label</c> does</b>, and it is the same bargain: the server will not look inside a
    /// payload, so anything a list has to show has to be handed to it. It stores the string
    /// against the row and never derives it, never validates it and never joins it to a rule —
    /// see <c>docs/CHARACTERS-API.md</c>.</para>
    /// </summary>
    private sealed record Sending(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("payload")] string Payload,
        [property: JsonPropertyName("campaignId")] string? CampaignId,
        [property: JsonPropertyName("kind")] string? Kind = null,
        [property: JsonPropertyName("tierId")] string? TierId = null,
        [property: JsonPropertyName("spent")] int? Spent = null);
}

/// <summary>
/// An account's characters and the cap it is held to.
/// </summary>
/// <param name="Limit">
/// The cap, or <c>null</c> when the server could not be asked.
///
/// <para>Null is not zero and the difference matters: zero would mean "you may store none", and
/// what is meant is "this is unknown right now". Every caller that acts on the number has to
/// decide what to do about not knowing, which is the point of making it nullable.</para>
/// </param>
/// <param name="Characters">Most recently touched first, as the server orders them.</param>
public sealed record AccountCharacters(int? Limit, IReadOnlyList<SavedCharacterSummary> Characters)
{
    /// <summary>The server could not be asked. Not the same as an account with nothing in it.</summary>
    public static AccountCharacters Unknown { get; } = new(null, []);

    /// <summary>Whether another character would be refused. Unknown counts as full — see below.</summary>
    /// <remarks>
    /// A cap that cannot be read is treated as reached, so nothing offers to create a character the
    /// server would then refuse. The alternative fails in the direction of losing work somebody has
    /// already typed, which is the worse half of a choice that has to be made either way.
    /// </remarks>
    public bool IsFull => Limit is null || Characters.Count >= Limit;
}
