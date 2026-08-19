using System.Net;
using System.Text;
using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Keeps the character on the server, so it is there in another browser.
///
/// <para>That is the whole feature: one character, one account, and it follows you. It is the
/// same envelope the browser's own store writes — see <see cref="StoredCharacter"/> — because
/// a character saved on a laptop has to restore on a phone, and two envelopes would drift.</para>
///
/// <para><b>Nothing here may throw, and a network is a new way for that to be hard.</b> The
/// interface's promise was written when the only failure was a browser refusing local storage;
/// now it also covers a site whose API is not deployed, a laptop with no network, and a session
/// that expired while the tab was open. All of them mean the same thing to a caller: there is
/// no character, start empty.</para>
///
/// <para><b>A failed save is silent, and that is a real cost stated plainly.</b> Local storage
/// effectively cannot fail; a network can, and the character then exists only in this tab. The
/// honest fix is telling somebody, which is the "Saved" feedback Phase 5 of the front-end plan
/// owes — not throwing from a method the app calls before its first render.</para>
/// </summary>
public sealed class ApiCharacterStore : ICharacterStore
{
    private const string Address = "api/character";

    private readonly HttpClient _http;
    private readonly StoredCharacter _payload;

    public ApiCharacterStore(HttpClient http, CostCalculator costs, CharacterValidator validator)
    {
        _http = http;
        _payload = new StoredCharacter(costs, validator);
    }

    /// <summary>Writes the character to the account it belongs to. Failure is not reported.</summary>
    public async Task SaveAsync(CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            // The payload goes up as the text `StoredCharacter` produced, not as an object for
            // the client to serialise again. The server stores those bytes and hands them back,
            // so what round-trips is exactly what was written down.
            using var body = new StringContent(
                StoredCharacter.Write(sheet, mode), Encoding.UTF8, "application/json");

            await _http.PutAsync(Address, body);
        }
        catch (Exception e) when (IsUnreachable(e)) { }
    }

    /// <summary>The account's character, or null if there is none this build can trust.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync()
    {
        try
        {
            var response = await _http.GetAsync(Address);

            // A 404 is the ordinary answer for an account that has not saved one yet, and a 401
            // for a session that ended while the tab was open. Neither is worth a word.
            if (!response.IsSuccessStatusCode) return null;

            return _payload.Read(await response.Content.ReadAsStringAsync());
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>Throws the account's character away. What "Start a new character" does.</summary>
    public async Task ClearAsync()
    {
        try { await _http.DeleteAsync(Address); }
        catch (Exception e) when (IsUnreachable(e)) { }
    }

    /// <summary>
    /// Whether the account has a character at all, without restoring it.
    ///
    /// <para>Asked once, on the sign-in page, so somebody who had an anonymous character can be
    /// offered the chance to keep it rather than have it silently replaced by an empty account.
    /// A failure answers "yes", which is the safe direction: it declines to offer a copy rather
    /// than offering to overwrite something that may be there.</para>
    /// </summary>
    public async Task<bool> HasCharacterAsync()
    {
        try
        {
            using var response = await _http.GetAsync(Address);

            return response.StatusCode != HttpStatusCode.NotFound;
        }
        catch (Exception e) when (IsUnreachable(e)) { return true; }
    }

    /// <summary>
    /// Every way the server can fail to answer. All of them mean the same thing here.
    ///
    /// <para><see cref="JsonException"/> is in the list because a site deployed without its API
    /// answers this address with the app's own <c>index.html</c> and a 200 — <c>_redirects</c>
    /// serves every unmatched path that way — so "success" is not proof of an answer.</para>
    /// </summary>
    private static bool IsUnreachable(Exception e) =>
        e is HttpRequestException           // no network, DNS, TLS, a refused connection
          or TaskCanceledException          // a timeout, or the host going away
          or OperationCanceledException
          or ObjectDisposedException
          or JsonException                  // an answer that is not the answer
          or NotSupportedException          // a content type this cannot read
          or InvalidOperationException;     // no base address
}
