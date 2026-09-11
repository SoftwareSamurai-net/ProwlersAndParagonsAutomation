using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>Teamwork from a campaign's shared headquarters, on the Derived stats step.</b>
///
/// <para>PROGRESS.md item 33.4: the owner's 2026-09-10 ruling is that a shared base's Training
/// Facilities grants the point to every <em>approved</em> member of the campaign, the way an
/// owner gets it from their own base — and a pending or rejected membership grants nothing. The
/// engine half is <c>DerivedStatsCalculator.CalculateTeamwork</c>'s two-argument overload,
/// covered in <c>AssetCostTests</c>; this is the browser half, which decides <em>which</em> bases
/// a reader may hand it — see <c>CampaignAssets.TeamworkBases</c>.</para>
/// </summary>
public sealed class DerivedTeamworkTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string CharacterId = "c_0000000000000000000000";
    private const string AssetId = "a_0000000000000000000000";

    private static Campaign ACampaignWithATrainedBase() => new(
        CampaignId, "Nightfall", "standard", null, false,
        Assets: [new CampaignAsset(AssetId, CampaignAssetContribution.Headquarters, "The Sanctum")
        {
            Features = [new SelectedAssetFeature("training_facilities")]
        }]);

    /// <summary>
    /// Joins the campaign through the real store, exactly as <c>CampaignApprovalTests</c>'s own
    /// <c>AJoinedMember</c> does — a test about what a member sees must not pass against a join
    /// that has stopped working. <see cref="CampaignJoin.Apply"/> is what a player's browser calls
    /// after a real join, and it is what puts <see cref="CharacterSheet.CampaignId"/> on the sheet
    /// this step reads.
    /// </summary>
    private static async Task<(RenderContext Ctx, string Membership)> AJoinedMember()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = ctx.Api.Campaign(
            CampaignId, "Nightfall", StoredCampaign.Write(ACampaignWithATrainedBase()));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var joined = await memberships.JoinAsync(code, CharacterId, "Ninefold");
        Assert.NotNull(joined);

        CampaignJoin.Apply(ctx.Session.Sheet, joined!.Value.Campaign);
        Assert.Equal(CampaignId, ctx.Session.Sheet.CampaignId);

        return (ctx, joined.Value.Id);
    }

    /// <summary>
    /// <b>An approved member sees Teamwork from the campaign's shared base</b>, having put none
    /// of their own Hero Points into it and owning no base of their own — the point is to every
    /// character who shares the headquarters, not to whoever paid for it.
    /// </summary>
    [Fact]
    public async Task AnApprovedMemberSeesTeamworkFromTheCampaignsSharedBase()
    {
        var (ctx, membership) = await AJoinedMember();
        await using var _ = ctx;

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        Assert.NotNull(await memberships.SubmitAsync(membership, ctx.Session.Sheet, SheetMode.Hero));

        ctx.Api.SignedIn = ("u_gm", "The GM");
        Assert.Equal(DecisionOutcome.Done, (await memberships.ApproveAsync(membership, 1)).Outcome);

        ctx.Api.SignedIn = ("u_player", "The Player");

        var page = ctx.Render<Derived>();

        Assert.Contains("Teamwork", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Training Facilities", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A pending member — joined, nothing yet approved — sees nothing.</b> The control on the
    /// test above: without an approval on the wire, the same campaign and the same shared base
    /// grant nothing, which is what says the figure above came from the approval rather than from
    /// the campaign alone.
    /// </summary>
    [Fact]
    public async Task APendingMemberSeesNoTeamworkFromTheCampaignsSharedBase()
    {
        var (ctx, _) = await AJoinedMember();
        await using var _2 = ctx;

        var page = ctx.Render<Derived>();

        Assert.DoesNotContain("Teamwork", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The GM's own character in their own campaign counts the shared base unconditionally</b>
    /// — there is no membership row for a GM to be pending or rejected on, so
    /// <c>CampaignAssets.TeamworkBases</c> takes the account's own read
    /// (<see cref="CampaignReach.Owned"/>) rather than gating on one.
    /// </summary>
    [Fact]
    public void TheGmsOwnCharacterCountsTheSharedBaseWithNoMembershipAtAll()
    {
        using var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Nightfall", StoredCampaign.Write(ACampaignWithATrainedBase()));
        ctx.Session.Sheet.CampaignId = CampaignId;

        var page = ctx.Render<Derived>();

        Assert.Contains("Teamwork", page.Markup, StringComparison.Ordinal);
    }
}
