using AngleSharp.Dom;
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

    /// <summary>
    /// The shared-objects panel itself. <b>Found rather than searched for over the whole page</b>,
    /// because the roster above it has a Remove button of its own and a search that crossed the
    /// two would press the wrong one.
    /// </summary>
    private static IElement SharedPanel(IRenderedComponent<CampaignApproval> page) =>
        page.FindAll("section.panel")
            .Single(s => s.TextContent.Contains("Shared vehicles and bases", StringComparison.Ordinal));

    private static string Shared(IRenderedComponent<CampaignApproval> page) =>
        SharedPanel(page).TextContent;

    /// <summary>One of the shared panel's own controls, by the word on it.</summary>
    private static IElement Control(IRenderedComponent<CampaignApproval> page, string word) =>
        SharedPanel(page).QuerySelectorAll("button")
            .Single(b => b.TextContent.Trim() == word);

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
    /// <b>What Chapter 6 says about the object itself is on the screen, and it is not a sentence
    /// about the budget.</b> p.96 floors a negative Control at −3 and caps it at half the Speed,
    /// and the shared panel prints <c>VehicleRanksNote</c> saying so — while nothing on the page
    /// ever checked either. A GM could write down a machine with Control −20, which pays forty
    /// Vehicle Points back, and read a figure comfortably inside the budget with no other word
    /// said.
    ///
    /// <para><b>Both places, because both are where a GM looks.</b> The row is what they scan; the
    /// editor is where they are typing, and a fault said only after the save is a fault said after
    /// the mistake was made.</para>
    ///
    /// <para>The positive control is the ordinary object above: a page that printed the sentence
    /// against every machine would satisfy this test and mean nothing.</para>
    /// </summary>
    [Fact]
    public async Task WhatTheBookSaysAboutTheObjectItselfIsOnTheScreen()
    {
        await using var ordinary = await AGameWithAFundedObject();

        Assert.DoesNotContain("Control cannot go below",
            Shared(ordinary.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId))),
            StringComparison.Ordinal);

        await using var ctx = await AGameWithAFundedObject(asset: Wing with { Control = -20 });

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        var shared = Shared(page);

        Assert.Contains("Control cannot go below", shared, StringComparison.Ordinal);
        Assert.Contains("The Wing", shared, StringComparison.Ordinal);

        // Reported, never repaired: the books still print what the campaign says, forty points
        // paid back and all.
        Assert.Contains($"{8 + 10 - 40}/", shared, StringComparison.Ordinal);

        // And in the editor, where the typing happens rather than where the reading does.
        await Control(page, "Edit").ClickAsync(new MouseEventArgs());

        var editor = page.FindAll("section.panel")
            .Single(s => s.TextContent.Contains("bought from zero", StringComparison.Ordinal))
            .TextContent;

        Assert.Contains("Control cannot go below", editor, StringComparison.Ordinal);

        // It is a report and not a bar: the save is still offered.
        Assert.NotNull(page.FindAll("button").Single(b => b.TextContent.Trim() == "Save"));
    }

    /// <summary>
    /// <b>A figure too large for the arithmetic is the same answer as a feature these rules do
    /// not have</b>, and it is reachable by typing rather than by meeting a payload from another
    /// build: the editor prices the draft on every change, so a GM who types a Body and a Speed
    /// that overflow together took the whole page down between two keystrokes.
    ///
    /// <para><b><c>CostCalculator</c> multiplies inside <c>checked</c> deliberately</b> — a
    /// wrapped total is a price that is wrong and says nothing — so what was missing was the
    /// catch, which named <see cref="InvalidOperationException"/> alone. Both are the engine
    /// refusing to answer, and a screen that survives one and not the other survives the one that
    /// needs a second build to reach.</para>
    ///
    /// <para>The positive control is the editor still being on screen with its name in it: a page
    /// that had thrown would satisfy "the sentence about being over budget is absent" by having no
    /// sentences at all.</para>
    /// </summary>
    [Fact]
    public async Task AFigureTooLargeToPriceIsSaidRatherThanThrown()
    {
        await using var ctx = AGameOwningNothing();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        await page.Find("input[aria-label='Name a shared vehicle or base']")
            .InputAsync(new() { Value = "The Wing" });

        await page.Find("button[aria-label='Add a shared vehicle or base']")
            .ClickAsync(new MouseEventArgs());

        await page.Find("#shared-body").ChangeAsync(new() { Value = "2000000000" });
        await page.Find("#shared-speed").ChangeAsync(new() { Value = "2000000000" });

        var editor = page.FindAll("section.panel")
            .Single(s => s.TextContent.Contains("bought from zero", StringComparison.Ordinal))
            .TextContent;

        // The no-price line, which is the one that carries what was put in and stops — and not
        // the pair, which would be a figure these rules cannot work out printed as though they
        // had.
        Assert.Contains("The Wing — 0 Vehicle Points put in", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("/0 Vehicle Points", editor, StringComparison.Ordinal);
    }

    // ── The GM writes one down ────────────────────────────────────────────────

    /// <summary>A GM signed in with one campaign that owns nothing shared.</summary>
    private static RenderContext AGameOwningNothing()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(GameId, "Nightfall", StoredCampaign.Write(
            new Campaign(GameId, "Nightfall", "standard", 8, false)));

        return ctx;
    }

    /// <summary>
    /// <b>A GM names a shared vehicle and it is written into the game's own payload</b>, where
    /// every member can then reach it through their own membership row.
    ///
    /// <para><b>The characteristics are asserted as well as the name</b>, because a save that
    /// wrote an empty record under the right name would satisfy every other assertion here and
    /// leave the table pooling into nothing.</para>
    /// </summary>
    [Fact]
    public async Task NamingASharedVehicleWritesItIntoTheGame()
    {
        await using var ctx = AGameOwningNothing();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        await page.Find("input[aria-label='Name a shared vehicle or base']")
            .InputAsync(new() { Value = "The Wing" });

        await page.Find("button[aria-label='Add a shared vehicle or base']")
            .ClickAsync(new MouseEventArgs());

        await page.Find("#shared-body").ChangeAsync(new() { Value = "8" });
        await page.Find("#shared-speed").ChangeAsync(new() { Value = "10" });

        // **The books are open while it is being typed**, against what the table has put in — which
        // for a machine nobody has funded yet is nothing, and that is the ordinary way round: a GM
        // writes the object down and the table pays for it afterwards.
        var editor = page.FindAll("section.panel")
            .Single(s => s.TextContent.Contains("bought from zero", StringComparison.Ordinal))
            .TextContent;

        Assert.Contains("The Wing — 18/0 Vehicle Points", editor, StringComparison.Ordinal);

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Save")
            .ClickAsync(new MouseEventArgs());

        var game = await ctx.Services.GetRequiredService<AccountCampaignStore>().LoadAsync(GameId);

        var written = Assert.Single(CampaignAsset.On(game));

        Assert.Equal("The Wing", written.Name);
        Assert.Equal(CampaignAssetContribution.Vehicle, written.Kind);
        Assert.Equal(8, written.Body);
        Assert.Equal(10, written.Speed);

        // Minted, and its own letter — it is a key inside a payload rather than a key the server
        // holds, and it is what every contribution will name.
        Assert.StartsWith("a_", written.Id, StringComparison.Ordinal);

        // And the screen redrew with it, which is what a GM checks rather than a payload.
        Assert.Contains("The Wing", Shared(page), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A base is the other table and the other currency, and it has no ranks at all.</b>
    /// pp.100–103 give a headquarters no Body, Speed, Control or Weapons — so the editor offers
    /// none, and the features it offers are the base list rather than the vehicle one.
    ///
    /// <para>The two lists are what discriminates here: an editor that ignored the kind would
    /// still be drawing a panel with a name in it.</para>
    /// </summary>
    [Fact]
    public async Task ABaseOffersTheOtherTableAndNoCharacteristics()
    {
        await using var ctx = AGameOwningNothing();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        await page.Find("select[aria-label='A vehicle or a headquarters']")
            .ChangeAsync(new() { Value = CampaignAssetContribution.Headquarters });

        await page.Find("input[aria-label='Name a shared vehicle or base']")
            .InputAsync(new() { Value = "The Roost" });

        await page.Find("button[aria-label='Add a shared vehicle or base']")
            .ClickAsync(new MouseEventArgs());

        Assert.Empty(page.FindAll("#shared-body"));

        var editor = page.FindAll("section.panel")
            .Single(s => s.TextContent.Contains("A base has no ranks", StringComparison.Ordinal))
            .TextContent;

        // Off pp.100–103, and not off pp.96–100.
        Assert.Contains("Holding Cells", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Gunnery Station", editor, StringComparison.Ordinal);

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Save")
            .ClickAsync(new MouseEventArgs());

        var game = await ctx.Services.GetRequiredService<AccountCampaignStore>().LoadAsync(GameId);

        Assert.True(Assert.Single(CampaignAsset.On(game)).IsHeadquarters);
    }

    /// <summary>
    /// <b>Renaming a shared object keeps its id, so nobody's contribution is orphaned by it.</b>
    ///
    /// <para>This is the whole reason the id is minted rather than derived from the name: every
    /// member's sheet is holding it as the record of what they paid for, and a key that moved on a
    /// rename would strand five contributions at once. The member's own screen is what would say
    /// so, and it is asserted here rather than assumed.</para>
    /// </summary>
    [Fact]
    public async Task RenamingKeepsTheIdSoNobodysContributionIsOrphaned()
    {
        await using var ctx = await AGameWithAFundedObject();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        await Control(page, "Edit").ClickAsync(new MouseEventArgs());

        await page.Find("#shared-name").InputAsync(new() { Value = "The Wing II" });

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Save")
            .ClickAsync(new MouseEventArgs());

        var game = await ctx.Services.GetRequiredService<AccountCampaignStore>().LoadAsync(GameId);

        var renamed = Assert.Single(CampaignAsset.On(game));

        Assert.Equal("The Wing II", renamed.Name);
        Assert.Equal(TheWing, renamed.Id);

        // The member's own answer, which is the one that matters: nothing is orphaned.
        Assert.Empty(CampaignAssets.Orphaned(Funder(2), game));

        // And the books still add up, which a lost id would have emptied.
        Assert.Contains("Ninefold 2 HP", Shared(page), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Removing a shared object takes nobody's Hero Points with it.</b>
    ///
    /// <para>The same answer deleting a campaign gets: the contribution stays on the member's
    /// sheet, still costs them, and their own screen reports that it names an object this game
    /// does not have. Reaching into five characters to tidy up after a decision about the game
    /// would be this application editing work that is not its own.</para>
    /// </summary>
    [Fact]
    public async Task RemovingASharedObjectLeavesEveryContributionWhereItIs()
    {
        await using var ctx = await AGameWithAFundedObject();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        await Control(page, "Remove").ClickAsync(new MouseEventArgs());

        // Two presses, because there is no undo behind it.
        await Control(page, "Remove for good").ClickAsync(new MouseEventArgs());

        var store = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var game = await store.LoadAsync(GameId);

        Assert.Empty(CampaignAsset.On(game));

        // The clone the campaign holds is untouched: the Hero Points are still spent.
        var clone = (await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .ReadAsync((await ctx.Services.GetRequiredService<ApiMembershipStore>()
                .InboxAsync())!.Single().Id))!.Approved;

        Assert.NotNull(clone);
        Assert.Equal(2, Assert.Single(clone!.CampaignAssets).HeroPoints);

        // And their own screen is what says the object is gone.
        Assert.Equal(CampaignAssets.UnknownAsset,
                     Assert.Single(CampaignAssets.Orphaned(clone, game)).Code);
    }

    /// <summary>
    /// <b>A save that went nowhere says so.</b> A form that closes and does nothing is
    /// indistinguishable from a control that was never wired up, which is the rule every refusal
    /// on the campaigns page follows.
    /// </summary>
    [Fact]
    public async Task ASaveThatWentNowhereSaysSo()
    {
        await using var ctx = AGameOwningNothing();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, GameId));

        await page.Find("input[aria-label='Name a shared vehicle or base']")
            .InputAsync(new() { Value = "The Wing" });

        await page.Find("button[aria-label='Add a shared vehicle or base']")
            .ClickAsync(new MouseEventArgs());

        ctx.Api.Unreachable = true;

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Save")
            .ClickAsync(new MouseEventArgs());

        Assert.Contains("could not be saved", page.Markup, StringComparison.Ordinal);

        // The draft is still open, so the GM's typing is not thrown away with the request.
        Assert.NotEmpty(page.FindAll("#shared-name"));
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
