using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The campaign's own page opens the books on a shared vehicle or base</b>: what its members
/// bought it, what has been built with that, and who put in what.
///
/// <para><b>The rendered page rather than the service, because the two halves fail
/// differently.</b> <c>CampaignAssetLedgerTests</c> covers the sums; a figure that is right in a
/// record and never drawn is a screen that tells a GM nothing, which is the split the two test
/// projects exist for. What is only assertable here is that the budget was summed from the
/// campaign's <em>clones</em> at all — the read this screen makes and the service is handed.</para>
///
/// <para><b>Every drive is awaited</b>, since the page reads the campaign, the inbox and a payload
/// per member before it can draw a figure.</para>
/// </summary>
public sealed class CampaignSharedBooksTests
{
    private const string GameId = "g_0000000000000000000000";
    private const string PlayerCharacter = "c_0000000000000000000000";
    private const string TheWing = "a_1111111111111111111111";

    private static CampaignAsset Wing =>
        new(TheWing, CampaignAssetContribution.Vehicle, "The Wing") { Body = 8, Speed = 10 };

    /// <summary>
    /// A game with one shared object and one member whose approved clone has put into it.
    ///
    /// <para>Driven through the real join, submit and approve routes, so a test about the books
    /// cannot pass against a clone that has stopped arriving.</para>
    /// </summary>
    private static async Task<RenderContext> AGameWithAFundedObject(
        int heroPoints = 2, CampaignAsset? asset = null, bool approve = true)
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(GameId, "Nightfall", StoredCampaign.Write(
            new Campaign(GameId, "Nightfall", "standard", 8, false,
                         Assets: [asset ?? Wing])));

        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();

        ctx.Api.SignedIn = ("u_player", "The Player");

        var joined = await store.JoinAsync(code, PlayerCharacter, "Ninefold");
        Assert.NotNull(joined);

        Assert.NotNull(await store.SubmitAsync(joined!.Value.Id, Funder(heroPoints), SheetMode.Hero));

        ctx.Api.SignedIn = ("u_gm", "The GM");

        if (approve)
        {
            var waiting = (await store.InboxAsync())!.Single();

            Assert.Equal(DecisionOutcome.Done,
                (await store.ApproveAsync(joined.Value.Id, waiting.PendingVersion)).Outcome);
        }

        return ctx;
    }

    private static CharacterSheet Funder(int heroPoints)
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId = "standard",
            Name = "Ninefold",
            AbilityRanks = { ["might"] = 6 },
        };

        sheet.CampaignAssets.Add(new CampaignAssetContribution(TheWing)
        {
            Kind = CampaignAssetContribution.Vehicle,
            Name = "The Wing",
            HeroPoints = heroPoints,
        });

        return sheet;
    }

    private static string Shared(IRenderedComponent<CampaignApproval> page) =>
        page.FindAll("section.panel")
            .Single(s => s.TextContent.Contains("Shared vehicles and bases", StringComparison.Ordinal))
            .TextContent;

    /// <summary>
    /// <b>The budget is the members' Hero Points, converted at the rate the rules data carries,
    /// and the spend is what has been built.</b>
    ///
    /// <para>Two Hero Points is fifty Vehicle Points; Body 8 and Speed 10 is eighteen of them. Both
    /// figures are printed, because over budget is reported rather than repaired and a reader owed
    /// that report is owed both halves of it.</para>
    /// </summary>
    [Fact]
    public async Task TheBooksShowTheSpendAgainstWhatTheMembersPutIn()
    {
        await using var ctx = await AGameWithAFundedObject();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        var shared = Shared(page);

        Assert.Contains("The Wing", shared, StringComparison.Ordinal);
        Assert.Contains(
            $"18/{2 * ctx.Session.Rules.Assets.VehiclePointsPerHeroPoint} Vehicle Points",
            shared, StringComparison.Ordinal);

        // Who paid, which is the one question on this screen no single player's sheet answers.
        Assert.Contains("Ninefold 2 HP", shared, StringComparison.Ordinal);

        // Under budget, so neither sentence about a shortfall is drawn.
        Assert.DoesNotContain("More has been built", shared, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing counted until the GM approved it.</b> A waiting snapshot is a request nobody has
    /// decided about, so counting its contributions would spend a player's Hero Points on a shared
    /// object before anybody agreed they were spent — and the GM would be reading a budget for a
    /// character that is not in their game yet.
    ///
    /// <para>This is the assertion that fixes the read to the <em>clone</em>. Everything else in
    /// this file passes just as well against a page reading whichever slot is fuller.</para>
    /// </summary>
    [Fact]
    public async Task AWaitingSnapshotDoesNotFundAnything()
    {
        await using var ctx = await AGameWithAFundedObject(approve: false);

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        var shared = Shared(page);

        // The object is there, and nobody has funded it.
        Assert.Contains("The Wing", shared, StringComparison.Ordinal);
        Assert.Contains("Nobody has put anything into this yet", shared, StringComparison.Ordinal);
        Assert.Contains("18/0 Vehicle Points", shared, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An object built past what its members put in says so, and both figures survive.</b>
    /// Reported and never repaired: the remedy is somebody putting more in or the object losing a
    /// feature, and both are decisions about this table's game.
    /// </summary>
    [Fact]
    public async Task AnObjectBuiltPastItsBudgetSaysSoOnTheScreen()
    {
        await using var ctx = await AGameWithAFundedObject(heroPoints: 1);

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        var shared = Shared(page);

        // 18 built against one Hero Point's 25 is inside the budget; the fixture asserts which
        // side of the line it is on, so a rate that moved cannot leave this test asserting
        // nothing.
        Assert.True(18 < ctx.Session.Rules.Assets.VehiclePointsPerHeroPoint,
                    "the fixture stopped being under budget, so the negative control below is void");

        Assert.DoesNotContain("More has been built", shared, StringComparison.Ordinal);

        // Now the same object with more built into it than one Hero Point buys.
        await using var over = await AGameWithAFundedObject(
            heroPoints: 1, asset: Wing with { Body = 20 });

        var second = over.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        var said = Shared(second);

        Assert.Contains("More has been built", said, StringComparison.Ordinal);

        // Both figures, not just the complaint.
        Assert.Contains($"30/{over.Session.Rules.Assets.VehiclePointsPerHeroPoint} Vehicle Points",
                        said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An object these rules cannot price is drawn, said, and does not take the page with
    /// it.</b>
    ///
    /// <para>A campaign's objects live inside a payload written by whatever build the GM was
    /// running, so a feature id this build has never heard of is an ordinary thing to meet — and
    /// the engine throws on one rather than guessing a price. The screen it would have taken down
    /// is the GM's whole roster.</para>
    ///
    /// <para><b>Not drawn as over budget</b>, which is a different fact: an object with no price
    /// has not been measured against anything.</para>
    /// </summary>
    [Fact]
    public async Task AnObjectTheseRulesCannotPriceIsSaidRatherThanThrown()
    {
        await using var ctx = await AGameWithAFundedObject(
            asset: Wing with { Features = [new SelectedAssetFeature("warp_nacelles")] });

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        var shared = Shared(page);

        Assert.Contains("cannot be worked out here", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("More has been built", shared, StringComparison.Ordinal);

        // The half that still answers, and the positive control on the read: what was put in.
        Assert.Contains(
            $"{2 * ctx.Session.Rules.Assets.VehiclePointsPerHeroPoint} Vehicle Points put in",
            shared, StringComparison.Ordinal);
        Assert.Contains("Ninefold 2 HP", shared, StringComparison.Ordinal);

        // And the roster above it drew, which is what the exception used to prevent.
        Assert.Contains("Ninefold", page.Find(".campaign-list").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A game that owns nothing shared says what to do about it</b>, rather than restating the
    /// emptiness — the rule every empty state in this app is held to.
    /// </summary>
    [Fact]
    public async Task AGameWithNothingSharedNamesTheNextAction()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(GameId, "Nightfall", StoredCampaign.Write(
            new Campaign(GameId, "Nightfall", "standard", 8, false)));

        await using var _ = ctx;

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        Assert.Contains("Name a vehicle or a base", Shared(page), StringComparison.Ordinal);
    }
}
