using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Services;
using Assets = ProwlersAndParagonsAutomation.Web.Pages.Assets;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The "Shared with the campaign" panel on the Vehicles &amp; bases step</b>: the game's own
/// objects offered to a member, and a contribution naming one the game has not.
///
/// <para><b>It shipped as a list with no way into it.</b> The panel drew whatever contributions a
/// payload already carried and offered nothing, so the only way to put Hero Points into a shared
/// machine in this application was to write the payload by hand — and a contribution keys on the
/// object's id, which is exactly the field nobody can type correctly. Offering the game's own
/// objects is what makes an id name something.</para>
///
/// <para><b>Every drive here is awaited.</b> The step now reads the campaign in
/// <c>OnInitializedAsync</c>, so the renderer is genuinely busy after the first render — which is
/// the one condition under which bUnit's synchronous drives read a render early, and the trap
/// <c>docs/guide/testing.md</c> records at length.</para>
/// </summary>
public sealed class CampaignSharedAssetTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string CharacterId = "c_1111111111111111111111";
    private const string TheWing = "a_2222222222222222222222";
    private const string TheRoost = "a_3333333333333333333333";

    private static CampaignAsset Wing =>
        new(TheWing, CampaignAssetContribution.Vehicle, "The Wing") { Body = 8, Speed = 10 };

    private static CampaignAsset Roost =>
        new(TheRoost, CampaignAssetContribution.Headquarters, "The Roost");

    private static string Payload(params CampaignAsset[] assets) =>
        StoredCampaign.Write(new Campaign(
            CampaignId, "Pinnacle City", "standard", null, false, Assets: assets));

    /// <summary>
    /// A game somebody else runs, joined by this browser's character — the reader who cannot read
    /// a campaign at its own address and reaches it through their own membership row instead.
    /// </summary>
    private static async Task<(RenderContext Ctx, string Code)> AGameRunByAnotherAccount(
        params CampaignAsset[] assets)
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = ctx.Api.Campaign(CampaignId, "Pinnacle City", Payload(assets));

        ctx.Api.SignedIn = ("u_player", "The Player");
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);

        return (ctx, code);
    }

    /// <summary>Join through the campaigns page, exactly as a player does.</summary>
    private static async Task Join(RenderContext ctx, string code)
    {
        var page = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Join").ClickAsync(new());
    }

    // ── The offer ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A member is offered the game's own objects, and choosing one records a contribution that
    /// names it.</b>
    ///
    /// <para>The id, the name and the kind all come off the object, so a sheet read with no
    /// campaign in front of it still prints something a reader recognises and a contribution's
    /// kind can never disagree with the object's.</para>
    /// </summary>
    [Fact]
    public async Task AMemberIsOfferedTheGamesOwnObjectsAndChoosingOneNamesIt()
    {
        var (ctx, code) = await AGameRunByAnotherAccount(Wing, Roost);
        await using var _ = ctx;

        await Join(ctx, code);

        // The control on everything below: the join really landed, so this is a member of a live
        // game rather than a page satisfying an absence.
        Assert.Equal(CampaignId, ctx.Session.Sheet.CampaignId);

        var page = ctx.Render<Assets>();

        await page.WaitForAssertionAsync(() =>
            Assert.Contains("The Wing", page.Markup, StringComparison.Ordinal));

        Assert.Contains("The Roost", page.Markup, StringComparison.Ordinal);

        await page.FindAll(".options button")
            .First(b => b.TextContent.Contains("The Wing", StringComparison.Ordinal))
            .ClickAsync(new());

        var put = Assert.Single(ctx.Session.Sheet.CampaignAssets);

        Assert.Equal(TheWing, put.AssetId);
        Assert.Equal("The Wing", put.Name);
        Assert.Equal(CampaignAssetContribution.Vehicle, put.Kind);

        // At nothing, because a contribution of nothing is an ordinary state — somebody has said
        // which object they are backing and not yet how much. The opposite of the Gadget's floor,
        // and for the opposite reason.
        Assert.Equal(0, put.HeroPoints);
    }

    /// <summary>
    /// <b>Choosing the same object twice records it once.</b> Two contributions to one object from
    /// one character are one contribution written down twice, and the campaign's ledger would sum
    /// them into a figure neither box on this page shows.
    /// </summary>
    [Fact]
    public async Task ChoosingOneObjectTwiceRecordsItOnce()
    {
        var (ctx, code) = await AGameRunByAnotherAccount(Wing);
        await using var _ = ctx;

        await Join(ctx, code);

        var page = ctx.Render<Assets>();

        await page.WaitForAssertionAsync(() => Assert.Contains(
            page.FindAll(".options button"),
            b => b.TextContent.Contains("The Wing", StringComparison.Ordinal)));

        await Offered(page).ClickAsync(new());
        Assert.Single(ctx.Session.Sheet.CampaignAssets);

        // **The second press is a real press.** The row is still on the list after the first —
        // nothing is taken off the offer — so a test that found no second row would be asserting
        // that a button nobody could reach adds nothing.
        await Offered(page).ClickAsync(new());
        Assert.Single(ctx.Session.Sheet.CampaignAssets);

        static AngleSharp.Dom.IElement Offered(IRenderedComponent<Assets> page) =>
            page.FindAll(".options button")
                .First(b => b.TextContent.Contains("The Wing", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The GM of the game is offered the same objects, off their own read.</b> They resolve the
    /// campaign at its own address and never come down the membership route — so this is the other
    /// half of <c>CampaignReaches</c>, and without it the one screen that can edit these objects
    /// could not put Hero Points into one.
    /// </summary>
    [Fact]
    public async Task TheGmIsOfferedTheSameObjectsFromTheirOwnCampaign()
    {
        await using var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City", Payload(Wing));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);
        ctx.Session.Sheet.CampaignId = CampaignId;

        var page = ctx.Render<Assets>();

        await page.WaitForAssertionAsync(() =>
            Assert.Contains("The Wing", page.Markup, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A character in no game is told so, and nothing is asked of the server.</b> Most
    /// characters are in no game and every one belonging to somebody signed out is; a step that
    /// made two round trips regardless would charge every reader of this page for a panel that
    /// then says there is no game.
    /// </summary>
    [Fact]
    public void ACharacterInNoGameIsToldSoAndCostsNoRequest()
    {
        using var ctx = new RenderContext(storesForReal: true);

        var before = ctx.Api.Asked.Count;
        var page = ctx.Render<Assets>();

        Assert.Contains("in no game", page.Markup, StringComparison.Ordinal);
        Assert.Equal(before, ctx.Api.Asked.Count);
    }

    // ── A contribution to something that is not there ─────────────────────────

    /// <summary>
    /// <b>A contribution the game has no object for is reported on the step, and kept.</b> The GM
    /// deleted the machine; the Hero Points are still spent and still counted, and dropping the row
    /// would be editing somebody's character to quieten a sentence.
    /// </summary>
    [Fact]
    public async Task AContributionToAnObjectTheGameHasNotIsReportedOnTheStep()
    {
        var (ctx, code) = await AGameRunByAnotherAccount(Roost);
        await using var _ = ctx;

        await Join(ctx, code);

        ctx.Session.Sheet.CampaignAssets.Add(new CampaignAssetContribution(TheWing)
        {
            Name = "The Wing", Kind = CampaignAssetContribution.Vehicle, HeroPoints = 2
        });

        var page = ctx.Render<Assets>();

        await page.WaitForAssertionAsync(() =>
            Assert.Contains("is not a shared vehicle or base this game has", page.Markup,
                StringComparison.Ordinal));

        Assert.Contains("The Wing", page.Markup, StringComparison.Ordinal);

        // Kept, with its Hero Points, and still charged.
        Assert.Equal(2, Assert.Single(ctx.Session.Sheet.CampaignAssets).HeroPoints);
        Assert.Equal(2, ctx.Session.Costs.TotalAssetPerkCost(ctx.Session.Sheet));
    }

    /// <summary>
    /// <b>A contribution to an object the game does have is not reported.</b> The positive control
    /// on the test above, without which a step that reported every contribution would pass it.
    /// </summary>
    [Fact]
    public async Task AContributionToARealObjectIsNotReportedOnTheStep()
    {
        var (ctx, code) = await AGameRunByAnotherAccount(Wing);
        await using var _ = ctx;

        await Join(ctx, code);

        ctx.Session.Sheet.CampaignAssets.Add(new CampaignAssetContribution(TheWing)
        {
            Name = "The Wing", Kind = CampaignAssetContribution.Vehicle, HeroPoints = 2
        });

        var page = ctx.Render<Assets>();

        await page.WaitForAssertionAsync(() =>
            Assert.Contains("The Wing", page.Markup, StringComparison.Ordinal));

        Assert.DoesNotContain("is not a shared vehicle or base this game has", page.Markup,
            StringComparison.Ordinal);
    }
}
