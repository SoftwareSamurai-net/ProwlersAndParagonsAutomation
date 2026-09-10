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
/// <param name="PlayerKey">
/// The same hint <see cref="MembershipSummary.PlayerKey"/> carries, threaded through so two
/// contributions can be folded under one player — see <see cref="CampaignAssetLine.ContributorGroups"/>.
/// Still never an account: it says two rows share one without saying which.
/// </param>
public sealed record AssetContributor(string MembershipId, string Who, int HeroPoints, string? PlayerKey = null);

/// <summary>
/// One of the campaign's shared objects with its books opened: what its members bought it, what
/// it cost, and who paid.
/// </summary>
/// <param name="Asset">The object itself.</param>
/// <param name="Budget">
/// The second currency its members' Hero Points bought — Vehicle Points or Base Points, depending
/// on the kind. <see cref="CostCalculator.CampaignAssetBudget"/>'s answer, never worked out here —
/// and <b>null when a contribution is too large to trust that arithmetic with</b>. See
/// <see cref="Findings"/>: this is the same "asked rather than caught" shape <see cref="Spent"/>
/// already makes for a feature id these rules cannot price, one currency over.
/// </param>
/// <param name="Spent">
/// What has been built with it, in the same currency, or <b>null for an object these rules cannot
/// price at all</b> — a feature id no table in <c>vehicles.json</c> or <c>headquarters.json</c>
/// has, a graded feature carrying no grade, or a figure too large for the arithmetic to hold.
///
/// <para><b>Asked rather than caught by the caller, and the null is the whole point.</b>
/// <see cref="CostCalculator"/> throws on a feature it cannot price, deliberately — and a campaign
/// payload can carry one, since it is written by whatever build the GM was running and read by
/// whatever build is open now. An exception here takes down the GM's whole roster over one
/// mistyped id, which is the same trade <c>CharacterSheetRenderer.Priceable</c> already refuses
/// for a machine one character owns.</para>
/// </param>
/// <param name="Contributors">Who put in, most first. Empty for an object nobody has funded.</param>
/// <param name="Findings">
/// What <see cref="CharacterValidator.CheckSharedAsset"/> said about this object and the
/// contributions naming it — <c>CAMPAIGN_ASSET_KIND_MISMATCH</c> and
/// <c>CAMPAIGN_ASSET_CONTRIBUTION_TOO_LARGE</c> among them. Asked once here, on the same
/// contributions <see cref="Budget"/> is summed from, rather than a second time by whatever draws
/// the row — the row's own <c>Findings="line.Findings"</c> is this list, verbatim.
/// </param>
public sealed record CampaignAssetLine(
    CampaignAsset Asset, int? Budget, int? Spent, IReadOnlyList<AssetContributor> Contributors,
    IReadOnlyList<ValidationIssue> Findings)
{
    /// <summary>
    /// Whether the object is built past what its members paid for.
    ///
    /// <para><b>Reported and never repaired</b>, the same answer the engine gives an over-budget
    /// character: the remedy is somebody putting more in or the object losing a feature, and both
    /// are decisions about a table's game rather than arithmetic a program may do on their
    /// behalf.</para>
    ///
    /// <para><b>False for an object with no price, and false for a budget nothing can be trusted
    /// to sum.</b> "Over its budget" is a claim about two figures and one of them is missing
    /// either way — the same direction <see cref="EmptySubmissions"/> takes with a row it could
    /// not read, where an accusation nobody can check is worse than silence. The comparison below
    /// is a lifted <c>&gt;</c>, so a null <see cref="Budget"/> already answers false without a
    /// second clause — kept explicit in this comment because that is easy to miss re-reading the
    /// code.</para>
    /// </summary>
    public bool IsOverBudget => Spent is { } spent && spent > Budget;

    /// <summary>
    /// What is left, which is negative exactly when <see cref="IsOverBudget"/> is true, and null
    /// exactly when <see cref="Spent"/> is — or when <see cref="Budget"/> is, for the same reason.
    /// </summary>
    public int? Remaining => Spent is { } spent ? Budget - spent : null;

    /// <summary>
    /// <see cref="Contributors"/>, folded so that two characters funded by one player draw as one
    /// row rather than two with nothing saying they are the same person.
    ///
    /// <para><b>Computed here rather than by the screen</b>, for the same reason every other
    /// figure on this record is: a page that grouped its own copy could disagree with one that
    /// read <see cref="Contributors"/> directly, and this way there is one answer to "how many
    /// funders does this object have". <see cref="Contributors"/> itself is unchanged — flat,
    /// largest first — because it is what a caller with no interest in the grouping already
    /// reads.</para>
    /// </summary>
    public IReadOnlyList<PlayerGroup<AssetContributor>> ContributorGroups =>
        PlayerGrouping.Group(Contributors, c => c.PlayerKey, c => c.HeroPoints, c => c.Who);
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
    /// The campaign's shared objects a character may count towards Teamwork — PROGRESS.md item
    /// 33.4, the owner's 2026-09-10 ruling that a shared base's Training Facilities grants the
    /// point to every <em>approved</em> member of the campaign, the way an owner gets it from
    /// their own base.
    ///
    /// <para><b>Live for a member, unconditional for the GM.</b> <see cref="CampaignReach.Owned"/>
    /// is a GM reading their own game directly — there is no membership row to be pending or
    /// rejected on, so the whole list counts, the same way the GM's own characters are simply
    /// "at the table". <see cref="CampaignReach.Live"/> is the opposite: a player's browser, read
    /// through <c>GET /api/memberships/{id}/table</c>, and that row can be pending or rejected —
    /// which is exactly the membership this checks before trusting it, off the same
    /// <paramref name="playing"/> list <see cref="CampaignReaches.LiveAsync"/> used to find the
    /// row in the first place.</para>
    ///
    /// <para><b>Recommended over reading the copy taken at join, and this is the argument for
    /// it.</b> A base the GM adds to the campaign after a member joined should grant that member
    /// the point without asking them to leave and rejoin — the same reasoning
    /// <see cref="CampaignReaches"/>'s own doc comment gives for reading the table live rather
    /// than the character's stale settings. <b>Offline, this answers no grant and no error</b>:
    /// <see cref="CampaignReach.Live"/> is null for a laptop with no network exactly as it is for
    /// a deleted game or an unreadable payload, and <see cref="CalculateTeamwork"/> below treats
    /// an empty list as "count only what the character owns", which is the same figure this
    /// character's sheet has always been able to answer on its own.</para>
    ///
    /// <para><b>Approval is <see cref="MembershipSummary.HasApproved"/> alone, not
    /// <see cref="MembershipSummary.Standing"/>.</b> A member who has been approved once and has a
    /// new snapshot pending is still an approved member sitting at the table today — the pending
    /// edit is a question about their <em>next</em> sheet, not about whether the party's shared
    /// base currently covers them. Only a membership that has never been approved — pending its
    /// first decision, or turned down outright — grants nothing, which is
    /// <c>!HasApproved</c> exactly.</para>
    /// </summary>
    /// <param name="reach">
    /// The campaign, read whichever way this account could — see <see cref="CampaignReaches.ForAsync"/>.
    /// </param>
    /// <param name="playing">
    /// The memberships this account holds, or null for a list that failed to read — the same list
    /// <see cref="CampaignReaches.ForAsync"/> takes, so a caller that already fetched it for the
    /// reach does not fetch it twice.
    /// </param>
    /// <param name="campaignId">The character's own <see cref="CharacterSheet.CampaignId"/>.</param>
    public static IReadOnlyList<CampaignAsset> TeamworkBases(
        CampaignReach reach, IReadOnlyList<MembershipSummary>? playing, string? campaignId)
    {
        if (reach.Owned is not null) return CampaignAsset.On(reach.Owned);
        if (reach.Live is null || campaignId is null) return [];

        var approved = playing?.Any(m =>
            m.HasApproved && string.Equals(m.CampaignId, campaignId, StringComparison.Ordinal)) ?? false;

        return approved ? CampaignAsset.On(reach.Live) : [];
    }

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
    /// <param name="validator">
    /// The same engine, asked what it thinks of the object and the contributions naming it —
    /// see <see cref="CampaignAssetLine.Findings"/> and the note on <see cref="CampaignAssetLine.Budget"/>
    /// about why a finding can stop this method trusting its own arithmetic.
    /// </param>
    /// <param name="members">
    /// One entry per character the caller managed to read: its membership id, the label the roster
    /// draws it under, the <see cref="MembershipSummary.PlayerKey"/> hint (or null, which groups
    /// with nobody — see <see cref="PlayerGrouping.Group{T}"/>), and the sheet.
    /// </param>
    public static IReadOnlyList<CampaignAssetLine> Ledger(
        Campaign? campaign,
        CostCalculator costs,
        CharacterValidator validator,
        IEnumerable<(string MembershipId, string Who, string? PlayerKey, CharacterSheet Sheet)> members)
    {
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(members);

        var assets = CampaignAsset.On(campaign);
        if (assets.Count == 0) return [];

        // Read once: the caller's sequence may be a query, and every object below asks it again.
        var everyone = members.ToList();
        var everyContribution = everyone.SelectMany(m => m.Sheet.CampaignAssets).ToList();

        return
        [
            .. assets.Select(asset =>
            {
                var findings = validator.CheckSharedAsset(asset, everyContribution);

                // **The owner's 2026-09-10 ruling, cashed in here rather than only reported.**
                // `CostCalculator.CampaignAssetBudget` multiplies a contribution's Hero Points by
                // the object's own rate inside a `checked` block, and a contribution the validator
                // has already called too large is exactly the input that overflows it — a hundred
                // million Hero Points times twenty-five is past `int.MaxValue`. Asking first means
                // this method never hands that arithmetic something it does not trust, rather than
                // catching the `OverflowException` after the fact.
                var tooLarge = findings.Any(i => i.Code == "CAMPAIGN_ASSET_CONTRIBUTION_TOO_LARGE");

                return new CampaignAssetLine(
                    asset,
                    tooLarge ? null : costs.CampaignAssetBudget(asset, everyContribution),
                    Spend(costs, asset),
                    [
                        .. everyone
                            .Select(m => new AssetContributor(
                                m.MembershipId, m.Who,
                                m.Sheet.CampaignAssets
                                    .Where(c => string.Equals(c.AssetId, asset.Id, StringComparison.Ordinal))
                                    .Sum(c => c.HeroPoints),
                                m.PlayerKey))
                            .Where(c => c.HeroPoints != 0)
                            .OrderByDescending(c => c.HeroPoints)
                            .ThenBy(c => c.Who, StringComparer.CurrentCulture)
                    ],
                    findings);
            })
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

        var assets = CampaignAsset.On(campaign)
            .ToDictionary(a => a.Id, StringComparer.Ordinal);

        var orphans = sheet.CampaignAssets
            .Where(c => !string.IsNullOrWhiteSpace(c.AssetId) && !assets.ContainsKey(c.AssetId))
            .Select(c => new CampaignFinding(
                UnknownAsset,
                $"{Named(c)} is not a shared vehicle or base this game has. The Hero Points "
                + "are still spent and still counted — put them somewhere else, or ask the "
                + "game to put the object back. Nothing has been changed either way."));

        // **Ruling 8, read from the same two things this method already has in hand.** A
        // contribution naming an object the game *does* have can still disagree with it about
        // what it is — a hand-written payload, or an asset whose kind changed after the
        // contribution was saved — and `CharacterValidator.CheckContributionAgainstAsset` is the
        // engine's own answer, called here rather than re-derived: the finding's wording belongs
        // to the rule, not to this screen.
        var mismatches = sheet.CampaignAssets
            .Where(c => !string.IsNullOrWhiteSpace(c.AssetId) && assets.ContainsKey(c.AssetId))
            .SelectMany(c => CharacterValidator.CheckContributionAgainstAsset(c, assets[c.AssetId]))
            .Select(i => new CampaignFinding(i.Code, i.Message));

        return [.. orphans, .. mismatches];
    }

    /// <summary>
    /// What to call an object in a sentence: the name the character recorded, or the id when it
    /// recorded none. The same fallback the printed sheet makes, so a reader meets one answer.
    /// </summary>
    /// <summary>
    /// What the object cost in its own currency, or null where these rules cannot say.
    ///
    /// <para><b>Caught here rather than guarded by the caller</b> for the reason
    /// <c>CharacterSheetRenderer</c> gives about the same throw: the alternative is every screen
    /// that draws a campaign's objects re-deciding whether a payload it did not write is
    /// priceable, and the one that forgets takes a GM's whole roster down over a feature id from
    /// another build.</para>
    ///
    /// <para><b>Public because the editor needs the same answer about an object that is not in
    /// the ledger yet</b> — a shared object being written down is priced as it is typed, and a
    /// screen that reached past this to the engine would be the caller that forgets.</para>
    /// </summary>
    public static int? Spend(CostCalculator costs, CampaignAsset asset)
    {
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(asset);

        try
        {
            return costs.CampaignAssetPointsSpent(asset);
        }
        catch (Exception e) when (e is InvalidOperationException or OverflowException)
        {
            return null;
        }
    }

    private static string Named(CampaignAssetContribution contribution) =>
        string.IsNullOrWhiteSpace(contribution.Name) ? contribution.AssetId : contribution.Name.Trim();
}
