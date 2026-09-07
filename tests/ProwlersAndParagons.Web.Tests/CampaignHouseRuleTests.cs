using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>A table's optional rules and its price for Immortality, driven through the page.</b>
///
/// <para>The GM sets them when a campaign is made or edited, every member sees them, a character
/// joining takes them, and the approval diff shows a price the campaign changed. Each of those is
/// driven by pressing the control a person presses, rather than by calling the service underneath
/// it — which is the fault this repository keeps recording: <c>CampaignJoin.Inspect</c> shipped
/// computed by tests alone for a whole slice, reaching no reader at all.</para>
///
/// <para><b>Every test here takes <c>RenderContext(storesForReal: true)</c></b>, because a join
/// needs a real character id: the current-character pointer defaults to this browser's legacy
/// slot, whose name the server refuses as ill-formed, so with bUnit's recorder every assertion
/// below would be about an error message.</para>
/// </summary>
public sealed class CampaignHouseRuleTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string CharacterId = "c_1111111111111111111111";

    /// <summary>A game somebody else is running, with the rules this test wants on it.</summary>
    private static async Task<(RenderContext Ctx, string Code)> AGameToJoin(
        CampaignTable? table = null, int? immortality = null, int? traitCap = null)
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", traitCap, false, table, immortality)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);

        return (ctx, code);
    }

    private static async Task<IRenderedComponent<Campaigns>> JoinWith(RenderContext ctx, string code)
    {
        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Join").ClickAsync(new());

        return page;
    }

    /// <summary>
    /// <b>A character with no rules of its own takes the campaign's, and the sentence says so.</b>
    ///
    /// <para>The assertions are on the sheet as well as on the message, because a sentence about a
    /// copy that did not happen is the exact failure the join message was rewritten to stop.</para>
    /// </summary>
    [Fact]
    public async Task AJoinTakesTheTablesRulesAndSaysSo()
    {
        var (ctx, code) = await AGameToJoin(
            new CampaignTable { FatalDamage = true, WoundPenalties = true }, immortality: 9);

        await using var _ = ctx;

        var page = await JoinWith(ctx, code);

        Assert.Contains("Its house rules came with it", page.Markup, StringComparison.Ordinal);

        Assert.NotNull(ctx.Session.Sheet.CampaignTable);
        Assert.True(ctx.Session.Sheet.CampaignTable!.FatalDamage);
        Assert.True(ctx.Session.Sheet.CampaignTable.WoundPenalties);
        Assert.False(ctx.Session.Sheet.CampaignTable.TheDrop);
        Assert.Equal(9, ctx.Session.Sheet.ImmortalityCost);
    }

    /// <summary>
    /// <b>A game that has adopted nothing takes nothing, and says nothing.</b>
    ///
    /// <para>The control on the test above: a message printed over every join is a message that
    /// says nothing, and a copy that fired for a campaign with no rules would put a block on a
    /// character that had joined a table playing the book.</para>
    /// </summary>
    [Fact]
    public async Task AGameWithNoHouseRulesTakesNothingAndSaysNothing()
    {
        var (ctx, code) = await AGameToJoin();
        await using var _ = ctx;

        var page = await JoinWith(ctx, code);

        Assert.Contains("Joined Pinnacle City", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("house rules came with it", page.Markup, StringComparison.Ordinal);

        Assert.Null(ctx.Session.Sheet.CampaignTable);
        Assert.Null(ctx.Session.Sheet.ImmortalityCost);
    }

    /// <summary>
    /// <b>A table that has only re-priced Immortality still says the rules came with it.</b>
    ///
    /// <para><b>The one case where the sentence matters most, and the one no test covered.</b>
    /// Every other test here gives the campaign switches <em>and</em> a price, so
    /// <c>TakesHouseRules</c>' two clauses were only ever exercised together — dropping the price
    /// clause entirely left the whole suite green while the join went on copying a figure that
    /// moves the character's spend and saying nothing about it. A change made and not claimed is
    /// the same fault as a change claimed and not made, which is what
    /// <c>CampaignJoinResult</c>'s own remarks were written about; the switches move nothing until
    /// somebody fights with the sheet, and the price moves Hero Points now.</para>
    /// </summary>
    [Fact]
    public async Task ATableThatHasOnlyRepricedImmortalitySaysSo()
    {
        var (ctx, code) = await AGameToJoin(immortality: 9);
        await using var _ = ctx;

        var page = await JoinWith(ctx, code);

        Assert.Contains("Its house rules came with it", page.Markup, StringComparison.Ordinal);

        Assert.Equal(9, ctx.Session.Sheet.ImmortalityCost);
        Assert.Null(ctx.Session.Sheet.CampaignTable);

        // And the panel below draws the one thing the table decided.
        Assert.Contains("Immortality costs 9 HP", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The mirror: a table that has only adopted switches says so too.</b>
    ///
    /// <para>The other clause of the same condition, so neither can be dropped without something
    /// going red. Kept beside its twin rather than folded into it, because a theory over the two
    /// would let one pass on the other's evidence.</para>
    /// </summary>
    [Fact]
    public async Task ATableThatHasOnlyAdoptedSwitchesSaysSo()
    {
        var (ctx, code) = await AGameToJoin(new CampaignTable { TheDrop = true });
        await using var _ = ctx;

        var page = await JoinWith(ctx, code);

        Assert.Contains("Its house rules came with it", page.Markup, StringComparison.Ordinal);

        Assert.Null(ctx.Session.Sheet.ImmortalityCost);
        Assert.True(ctx.Session.Sheet.CampaignTable!.TheDrop);
    }

    /// <summary>
    /// <b>A character that already carries rules keeps them, and the join does not claim
    /// otherwise.</b>
    ///
    /// <para>The rule the cap follows, one field at a time. <c>??=</c> is silent by construction,
    /// so the flag it reports is read before the write — the same fault the cap's own message
    /// shipped with.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterThatAlreadyCarriesRulesKeepsThem()
    {
        var (ctx, code) = await AGameToJoin(
            new CampaignTable { FatalDamage = true }, immortality: 9);

        await using var _ = ctx;

        ctx.Session.Sheet.CampaignTable = new CampaignTable { TheDrop = true };
        ctx.Session.Sheet.ImmortalityCost = 6;

        var page = await JoinWith(ctx, code);

        Assert.DoesNotContain("house rules came with it", page.Markup, StringComparison.Ordinal);

        Assert.True(ctx.Session.Sheet.CampaignTable!.TheDrop);
        Assert.False(ctx.Session.Sheet.CampaignTable.FatalDamage);
        Assert.Equal(6, ctx.Session.Sheet.ImmortalityCost);
    }

    /// <summary>
    /// <b>Every member sees what the game has decided, read-only, on the page they arrive at.</b>
    ///
    /// <para>This is the half that was worth building the rest for. A GM can set thirteen switches
    /// and a price, and until they are drawn somewhere a player can read, they are a promise to
    /// somebody who can never be told — the fault this repository keeps hitting.</para>
    /// </summary>
    [Fact]
    public async Task EveryMemberSeesTheGamesRules()
    {
        var (ctx, code) = await AGameToJoin(
            new CampaignTable { FatalDamage = true, RaisedGearLimit = true, GearLimitRank = 12 },
            immortality: 9, traitCap: 6);

        await using var _ = ctx;

        var page = await JoinWith(ctx, code);

        Assert.Contains("House rules on this character", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Fatal Damage", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Gear Limit 12d", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Trait Cap 6d", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Immortality costs 9 HP", page.Markup, StringComparison.Ordinal);

        // Named the way the book names them, never by the property key — the rule every finding
        // and every diff row in this app already follows.
        Assert.DoesNotContain("FatalDamage", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("RaisedGearLimit", page.Markup, StringComparison.Ordinal);

        // And nothing the table did not adopt.
        Assert.DoesNotContain("Tough Minions", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A game playing the book prints no house-rules block at all.</b>
    ///
    /// <para>The control on the test above, and the same question the printed sheet asks: a
    /// heading over thirteen "no"s is true of every game and tells a reader less than its absence
    /// does.</para>
    /// </summary>
    [Fact]
    public async Task AGamePlayingTheBookPrintsNoHouseRules()
    {
        var (ctx, code) = await AGameToJoin();
        await using var _ = ctx;

        var page = await JoinWith(ctx, code);

        Assert.DoesNotContain("House rules on this character", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The GM's form writes the switches down, and a form nobody touched writes nothing.</b>
    ///
    /// <para>Driven by ticking the box a GM ticks. The second half is what keeps a campaign whose
    /// GM opened this form and turned nothing on byte-identical to one written before the form
    /// existed — they are the same game, and a stored block of thirteen falses would be a
    /// difference the diff and <c>Inspect</c> would then have to be careful not to report.</para>
    /// </summary>
    [Fact]
    public async Task TheGmsFormWritesWhatWasTickedAndNothingWhenNothingWas()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("u_gm", "The GM");

        var page = ctx.Render<Campaigns>();

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Name a campaign")
            .ClickAsync(new());

        await page.Find("#campaign-name").InputAsync(new() { Value = "Pinnacle City" });
        await page.Find("#campaign-immortality").ChangeAsync(new() { Value = "9" });

        var fatal = page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Fatal Damage", StringComparison.Ordinal))
            .QuerySelector("input")!;

        await fatal.ChangeAsync(new() { Value = true });

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Save").ClickAsync(new());

        var store = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var rows = await store.ListAsync();
        var saved = await store.LoadAsync(Assert.Single(rows!).Id);

        Assert.NotNull(saved);
        Assert.Equal(9, saved!.ImmortalityCost);
        Assert.NotNull(saved.Table);
        Assert.True(saved.Table!.FatalDamage);
        Assert.False(saved.Table.TheDrop);
    }

    /// <summary>
    /// <b>A price outside the range is said out loud on the form, and not refused.</b>
    ///
    /// <para>The choice the Trait Cap box already makes one field up: a GM may type a figure
    /// before reading the range, and a Save that silently does nothing is a control that looks
    /// broken. The engine reports the same mistake one level down, so the two answers agree.</para>
    /// </summary>
    [Fact]
    public async Task APriceOutsideTheRangeIsSaidOnTheFormAndStillSaved()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("u_gm", "The GM");

        var page = ctx.Render<Campaigns>();

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Name a campaign")
            .ClickAsync(new());

        // Inside the range first: the control that this hint is not simply always on screen.
        await page.Find("#campaign-immortality").ChangeAsync(new() { Value = "9" });
        Assert.DoesNotContain("puts a table's price between", page.Markup, StringComparison.Ordinal);

        await page.Find("#campaign-immortality").ChangeAsync(new() { Value = "40" });

        Assert.Contains("The rulebook puts a table's price between 6 and 12",
            page.Markup, StringComparison.Ordinal);

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Save").ClickAsync(new());

        var store = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var saved = await store.LoadAsync(Assert.Single((await store.ListAsync())!).Id);

        Assert.Equal(40, saved!.ImmortalityCost);
    }

    /// <summary>
    /// <b>The Gear Limit rank is asked for only once the switch that reads it is on, and turning
    /// the switch back off drops the rank with it.</b>
    ///
    /// <para>A figure kept behind an unadopted switch is a setting the next reader has to guess
    /// about: it does not print on the sheet and it does not apply in a fight, so keeping it would
    /// only mean a GM turning the switch back on and silently getting a number they had forgotten
    /// typing.</para>
    /// </summary>
    [Fact]
    public async Task TheGearLimitIsAskedForOnlyUnderItsOwnSwitch()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("u_gm", "The GM");

        var page = ctx.Render<Campaigns>();

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Name a campaign")
            .ClickAsync(new());

        Assert.Empty(page.FindAll("#campaign-gear-limit"));

        var raised = page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Raised Gear Limit", StringComparison.Ordinal))
            .QuerySelector("input")!;

        await raised.ChangeAsync(new() { Value = true });
        await page.Find("#campaign-gear-limit").ChangeAsync(new() { Value = "12" });

        Assert.Single(page.FindAll("#campaign-gear-limit"));

        await page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Raised Gear Limit", StringComparison.Ordinal))
            .QuerySelector("input")!
            .ChangeAsync(new() { Value = false });

        Assert.Empty(page.FindAll("#campaign-gear-limit"));

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Save").ClickAsync(new());

        var store = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var saved = await store.LoadAsync(Assert.Single((await store.ListAsync())!).Id);

        // The whole block is the book again, so nothing is stored at all.
        Assert.Null(saved!.Table);
    }
}
