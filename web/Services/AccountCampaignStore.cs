using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// A campaign lives on an account's server, or it does not exist.
///
/// <para><b>This used to fall back to local storage and no longer does, and that is a decision
/// rather than a simplification.</b> A campaign is the thing two accounts hand a snapshot between:
/// a GM's clone of a character, and a player's request to change it. A campaign kept in one browser
/// can never receive a submission, never hold a clone, and never be joined by the code it would
/// advertise — so an anonymous campaign is a promise to somebody who can never be told. It was a
/// reasonable shape while a campaign was five fields nobody could reach; it stopped being one the
/// moment the approval slot existed. <c>SavedCampaigns</c> is deleted; its two static helpers are
/// on <see cref="StoredCampaign"/>, where the envelope is.</para>
///
/// <para><b>So every method here answers "nothing" for somebody not signed in</b>, and
/// <see cref="SaveAsync"/> answers false rather than quietly writing somewhere. A control that did
/// nothing and said so would be a control that looks broken — which is why the screen refuses
/// before the store is ever asked, and this is the belt to that brace.</para>
///
/// <para><b>Nothing here may throw</b>, which it inherits from the HTTP store and from asking who
/// is here, which is itself a network call.</para>
/// </summary>
public sealed class AccountCampaignStore
{
    private readonly IIdentitySource _who;
    private readonly ApiCampaignStore _inTheAccount;

    public AccountCampaignStore(IIdentitySource who, ApiCampaignStore inTheAccount)
    {
        _who = who;
        _inTheAccount = inTheAccount;
    }

    /// <summary>Every campaign on this account, most recently touched first. Empty for nobody.</summary>
    public async Task<IReadOnlyList<SavedCampaignSummary>> ListAsync() =>
        await IsSignedInAsync() ? await _inTheAccount.ListAsync() : [];

    /// <summary>One campaign, or null. This is the resolution rules code may never do.</summary>
    public async Task<Campaign?> LoadAsync(string id) =>
        await IsSignedInAsync() ? await _inTheAccount.LoadAsync(id) : null;

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
    ///
    /// <para><b>A signed-out visitor whose character names a campaign gets null, and that is
    /// right.</b> It is reported as naming a campaign that is not here — the same shape a deleted
    /// campaign takes — rather than being repaired, because signing in puts it back and nothing
    /// about the character has changed.</para>
    /// </summary>
    public async Task<Campaign?> ForAsync(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.CampaignId is null ? null : await LoadAsync(sheet.CampaignId);
    }

    /// <summary>Create or replace one, and say whether it actually landed.</summary>
    public async Task<CampaignSaveOutcome> SaveAsync(Campaign campaign) =>
        await IsSignedInAsync() ? await _inTheAccount.SaveAsync(campaign) : new CampaignSaveOutcome(false, null);

    /// <summary>Throw one away. Characters that named it keep saying so — see the design note.</summary>
    public async Task DeleteAsync(string id)
    {
        if (await IsSignedInAsync()) await _inTheAccount.DeleteAsync(id);
    }

    /// <summary>Whether a campaign can be made or read at all right now.</summary>
    public Task<bool> IsAvailableAsync() => IsSignedInAsync();

    private async Task<bool> IsSignedInAsync() => (await _who.CurrentAsync()).IsSignedIn;
}
