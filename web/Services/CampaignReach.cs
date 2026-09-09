using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The two ways a browser can reach the campaign a character is in, and what each one means.
/// </summary>
/// <param name="Owned">
/// The campaign as <c>AccountCampaignStore</c> resolved it, which is a campaign this account owns
/// — so a GM, and never a player. Null both for a character in no campaign and for one naming a
/// campaign this account cannot see, which <see cref="CampaignJoin.Inspect"/> is what tells apart.
/// </param>
/// <param name="Live">
/// The same game read through the reader's <em>own membership row</em>, and asked for only where
/// <see cref="Owned"/> answered nothing.
///
/// <para><b>Null covers four things and they are one thing to a screen</b>: no membership of that
/// campaign, a game the GM has deleted, a payload this build cannot read, and a server that could
/// not be reached. Each means there is nothing live to draw.</para>
/// </param>
public readonly record struct CampaignReach(Campaign? Owned, Campaign? Live)
{
    /// <summary>
    /// The campaign this reader can actually see, whichever route answered — <see cref="Owned"/>
    /// first, because a GM's own read is the one that has always worked and costs nothing extra.
    ///
    /// <para>Null when neither answered, which is what every caller has to be able to draw: a
    /// character in no game at all, and a member whose table could not be read, look the same from
    /// here and are told apart by the membership list one screen up.</para>
    /// </summary>
    public Campaign? Either => Owned ?? Live;
}

/// <summary>
/// Resolving the campaign a character names, from whichever side of it the reader is on.
///
/// <para><b>It exists because two screens now need the same two-step and one of them shipped
/// first.</b> Everything under <c>/api/campaigns</c> belongs to the GM, so a player's browser
/// resolves no campaign at all for a game that is alive — which is why
/// <c>GET /api/memberships/{id}/table</c> exists, authorised by the reader's own
/// <c>campaign_members</c> row. The campaigns page has done this since item 30; the Vehicles and
/// bases step needs the identical answer to offer the game's shared objects, and a second copy of
/// the ordering is a second chance to ask the wrong address first.</para>
///
/// <para><b>The order is the whole of it, and it is not an optimisation.</b> The live read is
/// asked <em>only</em> where the account's own answered nothing. A GM reading their own game
/// would be answered 404 by that address anyway — it is scoped to a membership row and a GM has
/// none for their own campaign — so asking would be a request whose only possible outcomes are a
/// wasted round trip and a wrong answer.</para>
/// </summary>
public static class CampaignReaches
{
    /// <summary>
    /// The campaign the character names, read whichever way this account can read it.
    /// </summary>
    /// <param name="store">The account's own campaigns — the GM's half.</param>
    /// <param name="memberships">The membership store, for the player's half.</param>
    /// <param name="sheet">The character, whose <c>CampaignId</c> is what is being resolved.</param>
    /// <param name="playing">
    /// The memberships this account holds, or null when that list could not be read.
    ///
    /// <para><b>The row is what makes the live request possible at all</b>, since the address is
    /// keyed on a membership rather than on a campaign — so a missing list is a null answer rather
    /// than a guess. That is the same evidence the campaigns page suppresses
    /// <c>UNKNOWN_CAMPAIGN</c> on, from the same list.</para>
    /// </param>
    public static async Task<CampaignReach> ForAsync(
        AccountCampaignStore store,
        ApiMembershipStore memberships,
        CharacterSheet sheet,
        IReadOnlyList<MembershipSummary>? playing)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(sheet);

        var owned = await store.ForAsync(sheet);

        return new CampaignReach(owned, owned is null
            ? await LiveAsync(memberships, sheet, playing)
            : null);
    }

    /// <summary>
    /// The live half on its own — the game as its table has it now, through the reader's own
    /// membership row.
    ///
    /// <para><b>Public because a recheck is one request and must stay one.</b> A reader pressing
    /// "Check again" is asking whether the table has moved; routing that through
    /// <see cref="ForAsync"/> would put the account's own campaign read in front of it, which for
    /// the only reader who ever sees that button — a member — can never answer anything.</para>
    /// </summary>
    /// <param name="memberships">The membership store.</param>
    /// <param name="sheet">The character, whose <c>CampaignId</c> names the game.</param>
    /// <param name="playing">The memberships this account holds, or null for a list that failed.</param>
    public static async Task<Campaign?> LiveAsync(
        ApiMembershipStore memberships,
        CharacterSheet sheet,
        IReadOnlyList<MembershipSummary>? playing)
    {
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet.CampaignId is not { } id) return null;

        // **A membership of *this* campaign, which the first version of this did not check.** It
        // took whichever row came back first, so a reader in two games was answered the wrong
        // table — the campaigns page shipped that and its review caught it. What this still does
        // not do, deliberately, is pick out the row for the character on screen: an account with
        // two characters in one game holds two rows, and either answers the same campaign, so the
        // extra read that would tell them apart buys a screen nothing.
        var row = playing?.FirstOrDefault(
            m => string.Equals(m.CampaignId, id, StringComparison.Ordinal));

        return row is null ? null : await memberships.TableAsync(row.Id);
    }
}
