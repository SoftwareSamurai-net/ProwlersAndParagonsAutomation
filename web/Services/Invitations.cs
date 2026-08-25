using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>One address that may have an account here.</summary>
/// <param name="Id">Null for the address configured on the deployment, which has no row.</param>
/// <param name="Email">The address, normalised by the server.</param>
/// <param name="GrantsAdmin">Whether signing in on it can manage this list.</param>
/// <param name="HasSignedIn">Whether it has become an account yet.</param>
/// <param name="CreatedAt">When it was added, in milliseconds. Null for the configured one.</param>
/// <param name="Removable">False for the configured address: the lever for it is a deploy.</param>
public sealed record Invitation(
    string? Id,
    string Email,
    bool GrantsAdmin,
    bool HasSignedIn,
    long? CreatedAt,
    bool Removable);

/// <summary>What asking the server about the list did.</summary>
public enum ListRequest
{
    /// <summary>Here it is.</summary>
    Loaded,

    /// <summary>Signed in, but this is not a page for this account. Also what an absent server says.</summary>
    NotForYou,

    /// <summary>Nobody is signed in at all.</summary>
    NotSignedIn,

    /// <summary>Reached and broken, or not reached. Either way, not the reader's doing.</summary>
    Unavailable,
}

/// <summary>The list, and what happened when it was asked for.</summary>
public readonly record struct InvitationList(
    ListRequest Result,
    IReadOnlyList<Invitation> Invitations,
    string? You = null)
{
    public static InvitationList Refused(ListRequest why) => new(why, []);
}

/// <summary>
/// Who may have an account here, from the browser's side.
///
/// <para><b>This asks and never decides.</b> Whether the person at the keyboard may see or
/// change this list is settled by the server on every request — the client holds no claim, no
/// role and no flag, which is why <see cref="Identity"/> still carries a key and a name and
/// nothing else. A page that hid the button would be a nicety; a page that showed it would
/// still be refused.</para>
///
/// <para><b>Nothing here throws.</b> Same reason as <see cref="Accounts"/>: a site deployed
/// without its server answers every address with the app's own <c>index.html</c> and a 200, so
/// the body is read rather than the status believed, and an answer that is not the answer is a
/// refusal rather than an exception through a render.</para>
/// </summary>
public sealed class Invitations
{
    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;

    public Invitations(HttpClient http) => _http = http;

    /// <summary>
    /// Whether this account looks after the site, in the same four answers as the list itself.
    ///
    /// <para><b>It asks the same question of the same endpoint rather than a cheaper one of its
    /// own.</b> A second endpoint answering "are you an administrator" would be a second place
    /// that decides it, and the two would eventually disagree — which is the shape of bug where
    /// a page shows something the server would refuse, or hides something it would allow. The
    /// cost is one list nobody reads, on a page only an administrator reaches.</para>
    ///
    /// <para><b>And it deliberately returns the whole enumeration, not a bool.</b> "Not you" and
    /// "not signed in" and "could not reach the site" are three different things to tell a
    /// reader, and collapsing them to false makes every one of them read as the first.</para>
    /// </summary>
    public async Task<ListRequest> AmIAdministratorAsync() => (await ListAsync()).Result;

    /// <summary>Everyone who may have an account, or why not.</summary>
    public async Task<InvitationList> ListAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/admin/invitations");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return InvitationList.Refused(ListRequest.NotSignedIn);
            }

            // 404 is what an account that may not manage the list is told, deliberately — the
            // same answer an address this server does not route gives, so an ordinary account
            // cannot learn that this page exists. It is also what a site with no server at all
            // eventually produces, and the two are the same thing to a reader.
            if (!response.IsSuccessStatusCode) return InvitationList.Refused(ListRequest.NotForYou);

            var body = await response.Content.ReadFromJsonAsync<Wired>(Wire);

            return body?.Invitations is null
                ? InvitationList.Refused(ListRequest.NotForYou)
                : new InvitationList(ListRequest.Loaded, body.Invitations, body.You);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return InvitationList.Refused(ListRequest.Unavailable);
        }
    }

    /// <summary>
    /// Let one more address have an account. True when it may, including when it already could.
    /// </summary>
    public async Task<bool> AddAsync(string email, bool grantsAdmin)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/admin/invitations",
                new { email, grantsAdmin });

            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (IsUnreachable(e)) { return false; }
    }

    /// <summary>
    /// Withdraw one invitation. False when the server refused — which it does for your own.
    /// </summary>
    public async Task<bool> RemoveAsync(string id)
    {
        try
        {
            return (await _http.DeleteAsync($"api/admin/invitations/{id}")).IsSuccessStatusCode;
        }
        catch (Exception e) when (IsUnreachable(e)) { return false; }
    }

    /// <summary>Every way the server can fail to answer. All of them mean the same here.</summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException
          or TaskCanceledException
          or OperationCanceledException
          or JsonException
          or NotSupportedException
          or InvalidOperationException;

    /// <summary>What the server sends: who is asking, and the list.</summary>
    private sealed record Wired(string? You, IReadOnlyList<Invitation>? Invitations);
}
