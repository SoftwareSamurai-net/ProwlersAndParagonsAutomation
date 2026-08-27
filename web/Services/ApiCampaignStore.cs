using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Keeps an account's campaigns on the server, so a game is there in another browser.
///
/// <para><b>Exactly the shape <see cref="ApiCharacterStore"/> has, and the server treats a
/// campaign exactly as opaquely as it treats a character.</b> The payload travels as a string it
/// never parses, and the label travels beside it because a server that will not look inside a
/// payload cannot read a name out of one. Nothing on the far side knows what a tier is.</para>
///
/// <para><b>Nothing here may throw.</b> A site deployed without its API, a laptop with no network
/// and a session that ended while the tab was open are all the same answer: there is no campaign,
/// carry on. <see cref="JsonException"/> is in the unreachable list for the reason
/// <see cref="ApiCharacterStore"/> records — <c>_redirects</c> serves an unmatched path as the
/// app's own <c>index.html</c> with a 200, so a success is not proof of an answer.</para>
///
/// <para><b>There is no cap here and no 409.</b> The account's cap is a cap on characters; a
/// campaign is not one, and inventing a second limit would be inventing a rule the contract does
/// not have.</para>
/// </summary>
public sealed class ApiCampaignStore
{
    private const string List = "api/campaigns";

    private static readonly JsonSerializerOptions Wire =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;

    public ApiCampaignStore(HttpClient http) => _http = http;

    /// <summary>The account's campaigns, most recently touched first.</summary>
    public async Task<IReadOnlyList<SavedCampaignSummary>> ListAsync()
    {
        try
        {
            using var response = await _http.GetAsync(List);
            if (!response.IsSuccessStatusCode) return [];

            var listed = await response.Content.ReadFromJsonAsync<Wired>(Wire);

            return listed?.Campaigns is null
                ? []
                : [.. listed.Campaigns
                    .Where(c => c.Id is { Length: > 0 })
                    .Select(c => new SavedCampaignSummary(
                        c.Id!, c.Label ?? "Unnamed campaign", c.UpdatedAt))];
        }
        catch (Exception e) when (IsUnreachable(e)) { return []; }
    }

    /// <summary>One campaign by id, or null if there is none this build can trust.</summary>
    public async Task<Campaign?> LoadAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        try
        {
            using var response = await _http.GetAsync($"{List}/{Uri.EscapeDataString(id)}");

            // A 404 for a campaign this account does not have and a 401 for a session that ended
            // while the tab was open are both ordinary, and neither is worth a word.
            if (!response.IsSuccessStatusCode) return null;

            return StoredCampaign.Read(await response.Content.ReadAsStringAsync());
        }
        catch (Exception e) when (IsUnreachable(e)) { return null; }
    }

    /// <summary>Write one campaign, creating it if the id is new. False if it went nowhere.</summary>
    public async Task<bool> SaveAsync(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        try
        {
            using var body = new StringContent(
                JsonSerializer.Serialize(
                    new Sending(SavedCampaigns.LabelFor(campaign), StoredCampaign.Write(campaign)), Wire),
                Encoding.UTF8,
                "application/json");

            using var response =
                await _http.PutAsync($"{List}/{Uri.EscapeDataString(campaign.Id)}", body);

            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (IsUnreachable(e)) { return false; }
    }

    /// <summary>
    /// Throw one campaign away. Absent is not an error — the end state is the same, which is the
    /// rule <c>docs/CHARACTERS-API.md</c> states for a character and this route follows.
    ///
    /// <para>Characters that named it keep their id; see <see cref="SavedCampaigns.DeleteAsync"/>.
    /// The server could not do otherwise if it wanted to — it does not know what a character
    /// payload contains.</para>
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        try { await _http.DeleteAsync($"{List}/{Uri.EscapeDataString(id)}"); }
        catch (Exception e) when (IsUnreachable(e)) { }
    }

    /// <summary>Every way the server can fail to answer. All of them mean the same thing here.</summary>
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
        [property: JsonPropertyName("campaigns")] Listed[]? Campaigns);

    private sealed record Listed(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("updatedAt")] long UpdatedAt);

    /// <summary>What the browser sends to store one. `payload` is opaque to the server.</summary>
    private sealed record Sending(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("payload")] string Payload);
}
