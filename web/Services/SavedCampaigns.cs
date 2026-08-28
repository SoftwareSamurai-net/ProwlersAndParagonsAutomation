using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// One row a campaign list can draw. The campaign itself is fetched separately, exactly as
/// <see cref="SavedCharacterSummary"/> keeps a character's payload out of its list.
/// </summary>
/// <param name="Id"><c>g_</c> followed by 22 URL-safe characters.</param>
/// <param name="Label">What the GM called the game. Opaque here and on the server alike.</param>
/// <param name="UpdatedAt">Unix milliseconds, for "most recently touched first" and nothing else.</param>
public sealed record SavedCampaignSummary(string Id, string Label, long UpdatedAt);

/// <summary>
/// Many campaigns, kept in this browser's local storage.
///
/// <para><b>A new top-level prefix, never a suffix on the character one.</b>
/// <c>pp.campaign.v1</c> sits beside <c>pp.character.v1</c> rather than under it. That is not a
/// stylistic choice: <see cref="SavedCharacters"/>' own remarks record that the character key has
/// to keep meaning exactly what it always did, because moving it would silently empty every
/// returning visitor's browser and look precisely like storage having been cleared. Hanging a
/// campaign off that key would put a second meaning on a string this project has promised not to
/// touch. The per-identity split is the same, for the same reason it is there for characters:
/// an account's campaigns and an anonymous visitor's must not be one list.</para>
///
/// <para><b>An index, not enumeration</b>, and for the same reason again — real
/// <c>localStorage</c> can be enumerated with a new JS call and the fake in the tests cannot be
/// enumerated at all, so discovering campaigns through <c>{prefix}.index</c> needs no new interop
/// surface. And as with characters, the two can disagree: an index entry naming a campaign that
/// is not stored is dropped from the list, because storage is the source of truth for what
/// <em>exists</em>.</para>
///
/// <para><b>There is no "current campaign" pointer here, and that is deliberate.</b> A character
/// says which campaign it belongs to; nothing else needs to. Adding a pointer would be a second
/// answer to the same question, able to disagree with the first.</para>
///
/// <para><b>Nothing here may throw.</b> A browser that refuses storage, a hand-edited index and an
/// index naming a campaign that is not there are all the same case: there is no campaign (or no
/// list) here — carry on.</para>
/// </summary>
public sealed class SavedCampaigns
{
    /// <summary>
    /// The top-level key for campaigns. Beside the character key, never inside it — see the
    /// class remarks.
    /// </summary>
    private const string StorageKey = "pp.campaign.v1";

    private readonly IJSRuntime _js;
    private readonly IIdentitySource _who;

    public SavedCampaigns(IJSRuntime js, IIdentitySource who)
    {
        _js = js;
        _who = who;
    }

    /// <summary>
    /// Which identity's namespace a key lives in. Anonymous keeps the bare key and an account
    /// gets its own beside it — the same scheme <see cref="SavedCharacters"/> uses, so that
    /// signing in switches both lists at once.
    /// </summary>
    private static string PrefixFor(Identity who) =>
        who.Key == Identity.Anonymous.Key ? StorageKey : $"{StorageKey}.{who.Key}";

    private static string IndexKeyFor(string prefix) => $"{prefix}.index";

    private static string PayloadKeyFor(string prefix, string id) => $"{prefix}.{id}";

    /// <summary>
    /// <c>g_</c> plus 22 URL-safe characters — 16 random bytes, base64url without padding.
    ///
    /// <para><b>Mirrors <see cref="SavedCharacters.NewId"/> exactly except for the letter</b>, so
    /// the server can validate a campaign id with the pattern it already validates a character id
    /// with, and neither can be passed where the other is meant. There is no legacy slot to
    /// reserve a name for: campaigns did not exist before this list did.</para>
    /// </summary>
    internal static string NewId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var text = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"g_{text}";
    }

    /// <summary>
    /// The name to list a campaign under. Trimmed, and never empty — an unnamed game is an
    /// ordinary state and a blank row reads as broken rather than as unnamed. The same rule
    /// <see cref="SavedCharacters.LabelFor"/> applies to a character, and the server's own
    /// default matches it.
    /// </summary>
    internal static string LabelFor(Campaign campaign) =>
        string.IsNullOrWhiteSpace(campaign.Name) ? "Unnamed campaign" : campaign.Name.Trim();

    // ── Reading and writing the index ───────────────────────────────────────────────

    private async Task<List<SavedCampaignSummary>> ReadIndexAsync(string prefix)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("ppStore.load", IndexKeyFor(prefix));
            if (string.IsNullOrWhiteSpace(raw)) return [];

            return JsonSerializer.Deserialize<List<SavedCampaignSummary>>(raw) ?? [];
        }
        // A hand-edited or corrupted index is an index that names nothing, same as one that was
        // never written. The campaigns it forgot are not lost, only unreachable.
        catch (Exception e) when (IsStorageFailure(e)) { return []; }
    }

    private async Task WriteIndexAsync(string prefix, List<SavedCampaignSummary> index)
    {
        try { await _js.InvokeVoidAsync("ppStore.save", IndexKeyFor(prefix), JsonSerializer.Serialize(index)); }
        catch (Exception e) when (IsStorageFailure(e)) { /* the index is a cache; nothing lost but the list */ }
    }

    // ── The surface a page will use ──────────────────────────────────────────────────

    /// <summary>
    /// Every campaign this browser holds for whoever is here now, most recently touched first.
    /// Reconciled against storage rather than trusted: an entry naming nothing is dropped.
    /// </summary>
    public async Task<IReadOnlyList<SavedCampaignSummary>> ListAsync()
    {
        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());
            var index = await ReadIndexAsync(prefix);

            var result = new List<SavedCampaignSummary>();
            foreach (var entry in index)
            {
                var raw = await _js.InvokeAsync<string?>("ppStore.load", PayloadKeyFor(prefix, entry.Id));
                if (!string.IsNullOrWhiteSpace(raw)) result.Add(entry);
            }

            result.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));
            return result;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return Array.Empty<SavedCampaignSummary>(); }
    }

    /// <summary>One campaign, or null if there is none this build can trust at that id.</summary>
    public async Task<Campaign?> LoadAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());
            return StoredCampaign.Read(await _js.InvokeAsync<string?>("ppStore.load", PayloadKeyFor(prefix, id)));
        }
        catch (Exception e) when (IsStorageFailure(e)) { return null; }
    }

    /// <summary>
    /// Create or replace a campaign, and say whether anything was actually written.
    ///
    /// <para><b>The second half of that answer is not decoration</b> — see
    /// <see cref="SavedCharacters.SaveAsync"/>, which returned an id alone and reported success
    /// over a browser that had refused storage. The payload is written first and the index
    /// second, the same order and for the same reason: a payload write that throws has then
    /// touched nothing at all.</para>
    /// </summary>
    public async Task<bool> SaveAsync(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());

            await _js.InvokeVoidAsync(
                "ppStore.save", PayloadKeyFor(prefix, campaign.Id), StoredCampaign.Write(campaign));

            var index = await ReadIndexAsync(prefix);
            index.RemoveAll(e => e.Id == campaign.Id);
            index.Add(new SavedCampaignSummary(
                campaign.Id, LabelFor(campaign), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

            await WriteIndexAsync(prefix, index);
        }
        catch (Exception e) when (IsStorageFailure(e)) { return false; }

        return true;
    }

    /// <summary>
    /// Forgets one campaign.
    ///
    /// <para><b>Characters that named it keep saying so.</b> Nothing here walks the character
    /// list, and that is the decision rather than an omission: a delete that reached into every
    /// character would be one control quietly editing things a person did not have open, and a
    /// character whose campaign has gone is still a legal character — it is reported as naming a
    /// campaign that is not here, the same shape an unknown tier is reported in.</para>
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());

            await _js.InvokeVoidAsync("ppStore.clear", PayloadKeyFor(prefix, id));

            var index = await ReadIndexAsync(prefix);
            if (index.RemoveAll(e => e.Id == id) > 0) await WriteIndexAsync(prefix, index);
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* nothing left to remove */ }
    }

    /// <summary>
    /// Everything that can go wrong between here and the browser's storage. The same list
    /// <see cref="SavedCharacters"/> keeps, for the same reason, and every one means: there is no
    /// campaign here — carry on.
    /// </summary>
    private static bool IsStorageFailure(Exception e) =>
        e is JsonException                  // a campaign or index this build cannot even write down
          or JSException                    // the browser refused, or ppStore is missing
          or InvalidOperationException      // interop unavailable
          or ObjectDisposedException        // the host is going away
          or TaskCanceledException          // ditto, mid-call
          or ArgumentException              // an id or key the payload invented
          or OverflowException              // a number no build can hold
          or NotSupportedException;         // a type the serializer cannot handle
}
