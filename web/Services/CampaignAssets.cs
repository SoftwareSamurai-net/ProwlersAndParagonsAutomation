using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// What one member put into one of the campaign's shared objects, with a name a GM can read.
/// </summary>
/// <param name="MembershipId">
/// Which membership. The key, because two players may call their characters the same thing and a
/// row keyed on the label would fold them into one line.
/// </param>
/// <param name="Who">
/// The character's label, which is what the GM's roster already calls this member. <b>Never an
/// account or an address</b> — a membership id deliberately never tells a GM whose account is on
/// the other side of it, and nothing here weakens that.
/// </param>
/// <param name="HeroPoints">What that character's sheet says it put in.</param>
public sealed record AssetContributor(string MembershipId, string Who, int HeroPoints);

/// <summary>
/// One of the campaign's shared objects with its books opened: what its members bought it, what
/// it cost, and who paid.
/// </summary>
/// <param name="Asset">The object itself.</param>
/// <param name="Budget">
/// The second currency its members' Hero Points bought — Vehicle Points or Base Points, depending
/// on the kind. <see cref="CostCalculator.CampaignAssetBudget"/>'s answer, never worked out here.
/// </param>
/// <param name="Spent">What has been built with it, in the same currency.</param>
/// <param name="Contributors">Who put in, most first. Empty for an object nobody has funded.</param>
public sealed record CampaignAssetLine(
    CampaignAsset Asset, int Budget, int Spent, IReadOnlyList<AssetContributor> Contributors)
{
    /// <summary>
    /// Whether the object is built past what its members paid for.
    ///
    /// <para><b>Reported and never repaired</b>, the same answer the engine gives an over-budget
    /// character: the remedy is somebody putting more in or the object losing a feature, and both
    /// are decisions about a table's game rather than arithmetic a program may do on their
    /// behalf.</para>
    /// </summary>
    public bool IsOverBudget => Spent > Budget;

    /// <summary>What is left, which is negative exactly when <see cref="IsOverBudget"/> is true.</summary>
    public int Remaining => Budget - Spent;
}

/// <summary>
/// The campaign side of Chapter 6's pooling rule: a shared object's budget summed from the
/// characters that funded it, and a contribution that names an object the game no longer has.
///
/// <para><b>This is in <c>web/</c> and not in <c>CharacterValidator</c>, and the line is the one
/// <see cref="CampaignJoin"/> already draws.</b> The validator tallies one character; this reads
/// several characters <em>and</em> a campaign, which means storage, which is what
/// <c>CharacterSheet.CampaignId</c>'s bar keeps out of the engine. What is not decided here is any
/// figure: every number below is <see cref="CostCalculator"/>'s, and this only collects the
/// contributions to hand it and sorts the answers into rows.</para>
///
/// <para><b>Nothing here repairs anything either.</b> An object built past its budget is reported
/// with both figures; a contribution naming an object that has been deleted is reported and kept.
/// Dropping the contribution would be editing somebody's character to make a report go away, and
/// it would take the Hero Points with it — the same reason deleting a campaign leaves its members
/// naming it.</para>
/// </summary>
public static class CampaignAssets
{
    /// <summary>
    /// A contribution naming an object this campaign does not have — deleted, or a payload from a
    /// build that spelled the id differently.
    ///
    /// <para><b>Deliberately the same <c>UNKNOWN_</c> shape as <c>UNKNOWN_CAMPAIGN</c> and the
    /// engine's own <c>UNKNOWN_GEAR_CATALOGUE_ROW</c></b>, and here for the reason
    /// <c>UNKNOWN_CAMPAIGN</c> is here rather than in the validator: answering it means resolving
    /// a campaign, and no rules code may. The engine cannot see a campaign at all, so the one
    /// finding it <em>can</em> make about a contribution is about the contribution's own fields —
    /// <c>CAMPAIGN_ASSET_WITHOUT_ID</c> and <c>UNKNOWN_CAMPAIGN_ASSET_KIND</c>, which
    /// <c>CharacterValidator.CheckCampaignAssets</c> already makes. This is the third question and
    /// the only one that needs the game in front of it.</para>
    /// </summary>
    public const string UnknownAsset = "UNKNOWN_CAMPAIGN_ASSET";

    /// <summary>
    /// Every shared object the campaign has, with what its members put in and what it cost.
    ///
    /// <para><b>The sheets are handed in rather than fetched, so this stays synchronous and
    /// testable</b> — reading a campaign's clones is a request per member, which is the caller's
    /// business and is argued where the caller does it. A member whose clone could not be read is
    /// simply not in the list handed here, and the honest consequence is a budget that reads low
    /// rather than a row invented for a sheet nobody fetched.</para>
    ///
    /// <para><b>Every object gets a row, funded or not.</b> An object nobody has put into is an
    /// ordinary state — a GM writes the machine down and the table pays for it afterwards — and a
    /// list that drew only the funded ones would answer "no shared vehicles" to a GM looking at
    /// the one they just made.</para>
    ///
    /// <para><b>Contributors are ordered by what they put in, largest first, then by name.</b> The
    /// question a GM has is who paid for this, and a tie broken by name rather than by whatever
    /// order the memberships came back in is what stops the list reshuffling between two reads
    /// that say the same thing.</para>
    /// </summary>
    /// <param name="campaign">The game, or null when none resolved — which answers nothing.</param>
    /// <param name="costs">The engine, which decides every figure here.</param>
    /// <param name="members">
    /// One entry per character the caller managed to read: its membership id, the label the roster
    /// draws it under, and the sheet.
    /// </param>
    public static IReadOnlyList<CampaignAssetLine> Ledger(
        Campaign? campaign,
        CostCalculator costs,
        IEnumerable<(string MembershipId, string Who, CharacterSheet Sheet)> members)
    {
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(members);

        var assets = CampaignAsset.On(campaign);
        if (assets.Count == 0) return [];

        // Read once: the caller's sequence may be a query, and every object below asks it again.
        var everyone = members.ToList();

        return
        [
            .. assets.Select(asset => new CampaignAssetLine(
                asset,
                costs.CampaignAssetBudget(asset, everyone.SelectMany(m => m.Sheet.CampaignAssets)),
                costs.CampaignAssetPointsSpent(asset),
                [
                    .. everyone
                        .Select(m => new AssetContributor(
                            m.MembershipId, m.Who,
                            m.Sheet.CampaignAssets
                                .Where(c => string.Equals(c.AssetId, asset.Id, StringComparison.Ordinal))
                                .Sum(c => c.HeroPoints)))
                        .Where(c => c.HeroPoints != 0)
                        .OrderByDescending(c => c.HeroPoints)
                        .ThenBy(c => c.Who, StringComparer.CurrentCulture)
                ]))
        ];
    }

    /// <summary>
    /// The contributions on one character that name an object its game does not have.
    ///
    /// <para><b>Nothing at all when no campaign resolved</b>, and that is the same judgement
    /// <c>Campaigns.razor</c>'s <c>WorthSaying</c> makes about <c>UNKNOWN_CAMPAIGN</c>. A member's
    /// browser resolves no campaign for a game that is perfectly alive — the payload is scoped to
    /// the account that owns it — so reporting an orphan against a null would accuse every member
    /// of every live game of naming an object that had been deleted. The reader who can see the
    /// campaign is the one who can be told; for anybody else there is no evidence either way.</para>
    ///
    /// <para><b>A blank id is not reported here</b>, because the engine already reports it as
    /// <c>CAMPAIGN_ASSET_WITHOUT_ID</c> on the character's own findings, and two sentences about
    /// one mistake is one sentence too many. What this adds is the question the engine cannot
    /// ask.</para>
    /// </summary>
    /// <param name="sheet">The character.</param>
    /// <param name="campaign">Its game, resolved, or null.</param>
    public static IReadOnlyList<CampaignFinding> Orphaned(CharacterSheet sheet, Campaign? campaign)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (campaign is null) return [];

        var known = CampaignAsset.On(campaign)
            .Select(a => a.Id)
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. sheet.CampaignAssets
                .Where(c => !string.IsNullOrWhiteSpace(c.AssetId) && !known.Contains(c.AssetId))
                .Select(c => new CampaignFinding(
                    UnknownAsset,
                    $"{Named(c)} is not a shared vehicle or base this game has. The Hero Points "
                    + "are still spent and still counted — put them somewhere else, or ask the "
                    + "game to put the object back. Nothing has been changed either way."))
        ];
    }

    /// <summary>
    /// What to call an object in a sentence: the name the character recorded, or the id when it
    /// recorded none. The same fallback the printed sheet makes, so a reader meets one answer.
    /// </summary>
    private static string Named(CampaignAssetContribution contribution) =>
        string.IsNullOrWhiteSpace(contribution.Name) ? contribution.AssetId : contribution.Name.Trim();
}
