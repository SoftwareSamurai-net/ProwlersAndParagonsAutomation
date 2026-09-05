using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// One account that plays in a campaign this caller runs, and the cap it is held to.
/// </summary>
/// <param name="Email">
/// The address, normalised by the server — and <b>the key of the row</b>.
///
/// <para>The account id is deliberately not on the wire, exactly as it is not on a membership:
/// a GM is never told whose account is on the other side of an <c>m_…</c>. Nothing is given away
/// by keying on the address here, because whoever is reading this panel is reading every address
/// on the invitation list beside it — and a membership id would be the wrong key rather than a
/// safer one, since a player in two of this GM's games has two of those and one cap.</para>
/// </param>
/// <param name="DisplayName">What they call themselves, or null for an account with no name set.</param>
/// <param name="CharacterCount">How many characters they are holding right now.</param>
/// <param name="CharacterLimit">How many they may hold — the number this panel sets.</param>
public sealed record ManagedAccount(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("characterCount")] int CharacterCount,
    [property: JsonPropertyName("characterLimit")] int CharacterLimit);

/// <summary>
/// One of that account's own characters, as far as this panel is ever told.
///
/// <para><b>There is no payload here and there must never be one.</b> The server does not parse a
/// character and this list is drawn from the index columns beside it — the same ones a roster row
/// is drawn from. A GM reading a member's actual sheet reads the campaign's clone, which is a
/// sheet that member deliberately sent.</para>
/// </summary>
public sealed record ManagedCharacter(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("updatedAt")] long UpdatedAt,
    [property: JsonPropertyName("kind")] string? Kind = null,
    [property: JsonPropertyName("tierId")] string? TierId = null,
    [property: JsonPropertyName("spent")] int? Spent = null);

/// <summary>
/// What asking the server for the accounts did.
///
/// <para>The same four answers <see cref="ListRequest"/> and <see cref="ErrorLogRequest"/> give,
/// because it is the identical gate on the server. A third enum rather than reusing either, for
/// the reason the second one records: the three questions happen to share an answer today and a
/// page that conflated them could not stop doing so if they ever did not.</para>
/// </summary>
public enum AdminAccountsRequest
{
    /// <summary>Here they are.</summary>
    Loaded,

    /// <summary>Signed in, but this account may not see them. Also what an absent server says.</summary>
    NotForYou,

    /// <summary>Nobody is signed in at all.</summary>
    NotSignedIn,

    /// <summary>Reached and broken, or not reached. Either way, not the reader's doing.</summary>
    Unavailable,
}

/// <summary>The accounts, and what happened when they were asked for.</summary>
public readonly record struct AdminAccountsView(
    AdminAccountsRequest Result, IReadOnlyList<ManagedAccount> Accounts)
{
    public static AdminAccountsView Refused(AdminAccountsRequest why) => new(why, []);
}

/// <summary>What one player holds, and what happened when it was asked for.</summary>
public readonly record struct HeldCharactersView(
    AdminAccountsRequest Result, IReadOnlyList<ManagedCharacter> Characters)
{
    public static HeldCharactersView Refused(AdminAccountsRequest why) => new(why, []);
}

/// <summary>
/// What setting one cap did.
///
/// <para><b>Three failures rather than one bool</b>, for the reason <see cref="Invitations"/>
/// gives about its own enumeration: "the number is not one this server will store", "that account
/// is no longer in one of your campaigns" and "nothing was reached" are three different things to
/// tell somebody, and collapsing them makes every one of them read as the first.</para>
/// </summary>
public enum CapChange
{
    /// <summary>Set, and the row that came back says so.</summary>
    Set,

    /// <summary>The server would not store that number.</summary>
    Refused,

    /// <summary>No such account in one of your campaigns — the same 404 an unrouted address gets.</summary>
    NotThere,

    /// <summary>Reached and broken, or not reached at all.</summary>
    Unavailable,
}

/// <summary>The outcome, and the row as it now stands when there is one.</summary>
public readonly record struct CapChanged(CapChange Result, ManagedAccount? Account);

/// <summary>
/// The players in this account's own campaigns, from the browser's side.
///
/// <para><b>This asks and never decides.</b> Whether the person at the keyboard may see or change
/// any of this is settled by the server on every request — the client holds no claim, no role and
/// no flag, which is why <see cref="Identity"/> still carries a key and a name and nothing else.
/// Same sentence as <see cref="Invitations"/>, and the same gate underneath it:
/// <c>invitations.isAdministrator</c>.</para>
///
/// <para><b>Nothing here throws.</b> A site deployed without its server answers every address with
/// the app's own <c>index.html</c> and a 200, so the body is read rather than the status believed,
/// and an answer that is not the answer is a refusal rather than an exception through a
/// render.</para>
///
/// <para><b>The address is the key and is escaped into the path.</b> An address may hold a
/// <c>+</c>, and a raw one in a path is a different string from the one the server normalises —
/// <c>Uri.EscapeDataString</c> is what makes the two agree.</para>
/// </summary>
public sealed class AdminAccounts
{
    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;

    public AdminAccounts(HttpClient http) => _http = http;

    /// <summary>Every player in this caller's campaigns, or why there are none to show.</summary>
    public async Task<AdminAccountsView> ListAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/admin/accounts");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return AdminAccountsView.Refused(AdminAccountsRequest.NotSignedIn);
            }

            // 404 is what an account this is not for is told, deliberately — the same answer an
            // address this server does not route gives, so an ordinary account cannot learn the
            // endpoint exists.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return AdminAccountsView.Refused(AdminAccountsRequest.NotForYou);
            }

            // **Everything else that is not a 2xx is the server being broken, not a decision about
            // the reader** — and folding the two together is the defect this branch shipped. A 500
            // from storage, a 502 from in front of it and a 405 from a route that moved all arrived
            // as "this is not for you", which the panel drew as *"Nobody is in one of your
            // campaigns yet."* A GM was told their games are empty on the strength of a request
            // that failed — which is exactly the rule the panel already keeps one level down, where
            // a player who holds nothing and a list that could not be read are two sentences.
            if (!response.IsSuccessStatusCode)
            {
                return AdminAccountsView.Refused(AdminAccountsRequest.Unavailable);
            }

            var body = await response.Content.ReadFromJsonAsync<WiredAccounts>(Wire);

            // A 200 carrying something that is not this answer is the site deployed without its
            // server: its own `index.html` under every address. Not a decision about the reader
            // either, so it lands on the same side as the statuses above.
            return body?.Accounts is null
                ? AdminAccountsView.Refused(AdminAccountsRequest.Unavailable)
                : new AdminAccountsView(AdminAccountsRequest.Loaded, body.Accounts);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return AdminAccountsView.Refused(AdminAccountsRequest.Unavailable);
        }
    }

    /// <summary>One player's own characters — names and times, never a payload.</summary>
    public async Task<HeldCharactersView> HeldByAsync(string email)
    {
        try
        {
            var response = await _http.GetAsync($"api/admin/accounts/{Key(email)}/characters");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return HeldCharactersView.Refused(AdminAccountsRequest.NotSignedIn);
            }

            if (!response.IsSuccessStatusCode)
            {
                return HeldCharactersView.Refused(AdminAccountsRequest.NotForYou);
            }

            var body = await response.Content.ReadFromJsonAsync<WiredCharacters>(Wire);

            return body?.Characters is null
                ? HeldCharactersView.Refused(AdminAccountsRequest.NotForYou)
                : new HeldCharactersView(AdminAccountsRequest.Loaded, body.Characters);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return HeldCharactersView.Refused(AdminAccountsRequest.Unavailable);
        }
    }

    /// <summary>
    /// Set how many characters one player may hold.
    ///
    /// <para><b>The bounds are the server's and are not restated here.</b> A second range in this
    /// file would be a second thing to keep in step with a column, and the refusal it produced
    /// would be a sentence about a rule this side made up. What this does is send the number and
    /// turn each of the server's three answers into one the panel can say.</para>
    /// </summary>
    public async Task<CapChanged> SetCharacterLimitAsync(string email, int characterLimit)
    {
        try
        {
            var response = await _http.PutAsJsonAsync(
                $"api/admin/accounts/{Key(email)}/character-limit", new { characterLimit });

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new CapChanged(CapChange.Refused, null);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new CapChanged(CapChange.NotThere, null);
            }

            if (!response.IsSuccessStatusCode) return new CapChanged(CapChange.Unavailable, null);

            var body = await response.Content.ReadFromJsonAsync<WiredAccount>(Wire);

            // The body is read rather than the status believed, for the reason above: a site with
            // no server answers this PUT with its own page and a 200.
            return body?.Account is null
                ? new CapChanged(CapChange.Unavailable, null)
                : new CapChanged(CapChange.Set, body.Account);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return new CapChanged(CapChange.Unavailable, null);
        }
    }

    /// <summary>The address as one path segment. See the note on the class.</summary>
    private static string Key(string email) =>
        Uri.EscapeDataString(email.Trim().ToLower(CultureInfo.InvariantCulture));

    /// <summary>Every way the server can fail to answer. All of them mean the same here.</summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException
          or TaskCanceledException
          or OperationCanceledException
          or JsonException
          or NotSupportedException
          or InvalidOperationException;

    /// <summary>What the server sends: the players, in the shape the panel reads.</summary>
    private sealed record WiredAccounts(IReadOnlyList<ManagedAccount>? Accounts);

    /// <summary>What the server sends back from setting one cap: the row as it now stands.</summary>
    private sealed record WiredAccount(ManagedAccount? Account);

    /// <summary>What the server sends for one player's sheets.</summary>
    private sealed record WiredCharacters(IReadOnlyList<ManagedCharacter>? Characters);
}
