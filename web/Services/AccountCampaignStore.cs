using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Puts a campaign wherever it belongs: on the server for somebody signed in, in this browser for
/// everybody else.
///
/// <para><b>A store rather than a branch in <c>Program.cs</c></b>, for the reason
/// <see cref="AccountCharacterStore"/> gives: the choice has to be made per call, because
/// identity changes when somebody signs in and the next save has to land in the new place without
/// anything being re-registered.</para>
///
/// <para><b>It is much smaller than the character equivalent, and the missing parts are missing
/// on purpose.</b> There is no copy-up on sign-in, no anonymous slot to protect and no undo,
/// because a campaign is five fields a GM can retype rather than twenty minutes of work — and
/// every one of those mechanisms on the character side exists to stop something being destroyed
/// silently. Adding them speculatively would be adding the most dangerous code in that class
/// before there is anything for it to protect.</para>
///
/// <para><b>Nothing here may throw</b>, which it inherits from both halves and from asking who is
/// here, which is itself a network call.</para>
/// </summary>
public sealed class AccountCampaignStore
{
    private readonly IIdentitySource _who;
    private readonly SavedCampaigns _inThisBrowser;
    private readonly ApiCampaignStore _inTheAccount;

    public AccountCampaignStore(
        IIdentitySource who, SavedCampaigns inThisBrowser, ApiCampaignStore inTheAccount)
    {
        _who = who;
        _inThisBrowser = inThisBrowser;
        _inTheAccount = inTheAccount;
    }

    /// <summary>Every campaign for whoever is here now, most recently touched first.</summary>
    public async Task<IReadOnlyList<SavedCampaignSummary>> ListAsync() =>
        await IsSignedInAsync() ? await _inTheAccount.ListAsync() : await _inThisBrowser.ListAsync();

    /// <summary>One campaign, or null. This is the resolution rules code may never do.</summary>
    public async Task<Campaign?> LoadAsync(string id) =>
        await IsSignedInAsync() ? await _inTheAccount.LoadAsync(id) : await _inThisBrowser.LoadAsync(id);

    /// <summary>
    /// The campaign a character belongs to, or null when it belongs to none.
    ///
    /// <para><b>This is the whole of "campaign resolution", and it is one line on purpose.</b> It
    /// is the only place a character's campaign id is turned into a campaign, it lives in
    /// <c>web/</c> because resolving an id means asking storage, and <b>a null id resolves to
    /// null</b> — never to a default. A default campaign for a character that is in no campaign
    /// would apply a tier, a cap and a budget to every character in this application that has
    /// never heard of one; there is a test that a character in no campaign renders and validates
    /// byte for byte as it does with none of this code present at all.</para>
    /// </summary>
    public async Task<Campaign?> ForAsync(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.CampaignId is null ? null : await LoadAsync(sheet.CampaignId);
    }

    /// <summary>Create or replace one, and say whether it actually landed.</summary>
    public async Task<bool> SaveAsync(Campaign campaign) =>
        await IsSignedInAsync() ? await _inTheAccount.SaveAsync(campaign) : await _inThisBrowser.SaveAsync(campaign);

    /// <summary>Throw one away. Characters that named it keep saying so — see the design note.</summary>
    public async Task DeleteAsync(string id)
    {
        if (await IsSignedInAsync()) await _inTheAccount.DeleteAsync(id);
        else await _inThisBrowser.DeleteAsync(id);
    }

    private async Task<bool> IsSignedInAsync() => (await _who.CurrentAsync()).IsSignedIn;
}
