using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The GM's own characters go straight into the GM's own game</b> — the owner's ruling of
/// 2026-10-28: a villain of the week, a boss, or a Hero the GM plays, with no approval, no Hero to
/// hunt, and nothing handed over.
///
/// <para>The server's half is <c>tests/worker/nemesis.test.mjs</c> against real SQLite. What is
/// here is what the screens say and press: the row offers to put the character in rather than to
/// ask, a Villain is not asked whose nemesis it is, and the GM's roster sorts it as theirs.</para>
/// </summary>
public sealed class GmsOwnCharactersTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string BossId = "c_3333333333333333333333";
    private const string PlayerHeroId = "c_4444444444444444444444";

    private static CharacterSheet Boss() => new()
    {
        SelectedTierId = "standard",
        TraitCapRank = 8,
        Name = "Big Boss McGee",
        IsVillain = true,
        AbilityRanks = { ["might"] = 6 },
    };

    /// <summary>
    /// The GM's game, with the GM's own character joined to it by its own code and on screen, and
    /// optionally a player's Hero beside it. Through the real stores and the real join route.
    /// </summary>
    private static async Task<(RenderContext Ctx, string Membership)> TheGmSeatsTheirOwn(
        SheetMode mode, bool withPlayer = false)
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = ctx.Api.Campaign(CampaignId, "Nightfall",
            StoredCampaign.Write(new Campaign(CampaignId, "Nightfall", "standard", 8, false)));

        if (withPlayer)
        {
            ctx.Api.SignedIn = ("u_player", "The Player");
            Assert.Equal(SaveOutcome.Saved, await ctx.Services.GetRequiredService<ApiCharacterStore>()
                .SaveAsync(PlayerHeroId, "Jetstream", new CharacterSheet { Name = "Jetstream", SelectedTierId = "standard" }, SheetMode.Hero));
            var hero = await ctx.Services.GetRequiredService<ApiMembershipStore>().JoinAsync(code, PlayerHeroId, "Jetstream");
            Assert.NotNull(await ctx.Services.GetRequiredService<ApiMembershipStore>()
                .SubmitAsync(hero!.Value.Id, new CharacterSheet { Name = "Jetstream", SelectedTierId = "standard" }, SheetMode.Hero));
            ctx.Api.SignedIn = ("u_gm", "The GM");
        }

        var sheet = Boss();
        sheet.IsVillain = mode == SheetMode.Villain;

        Assert.Equal(SaveOutcome.Saved, await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(BossId, "Big Boss McGee", sheet, mode));
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(BossId);

        var joined = await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .JoinAsync(code, BossId, "Big Boss McGee");
        Assert.NotNull(joined);

        var onScreen = Boss();
        onScreen.IsVillain = mode == SheetMode.Villain;
        ctx.Session.Open(onScreen, mode);

        return (ctx, joined!.Value.Id);
    }

    private static string Squeezed(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

    private static async Task Press(IRenderedComponent<Campaigns> page, string label) =>
        await page.FindAll("button").Single(b => b.TextContent.Trim() == label)
            .ClickAsync(new MouseEventArgs());

    private static async Task<MembershipSummary> GmsRow(RenderContext ctx, string membership) =>
        (await ctx.Services.GetRequiredService<ApiMembershipStore>().InboxAsync())!
            .Single(m => m.Id == membership);

    /// <summary>
    /// One press puts the GM's own Villain in: no "whose nemesis is it?", no warning that it will
    /// be given away, and the campaign holding it at once with nothing waiting.
    /// </summary>
    [Fact]
    public async Task TheGmsOwnVillainGoesStraightInWithNoNemesisQuestion()
    {
        var (ctx, membership) = await TheGmSeatsTheirOwn(SheetMode.Villain);
        await using var _ = ctx;
        var page = ctx.Render<Campaigns>();

        // Positive control: the row is drawn, and it is the GM's own.
        Assert.Contains("Not put in yet", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Send for approval");

        await Press(page, "Put in the game");

        Assert.DoesNotContain("Whose nemesis is it?", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("becomes theirs for good", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("you have no Hero in this game", Squeezed(page.Markup), StringComparison.Ordinal);
        Assert.Contains("Big Boss McGee is in the game.", page.Markup, StringComparison.Ordinal);

        var row = await GmsRow(ctx, membership);
        Assert.True(row.GmsOwn);
        Assert.True(row.HasApproved);
        Assert.False(row.HasPending);
        Assert.False(row.HandedOver);
        Assert.Null(row.NemesisOf);
        Assert.Equal(SavedCharacters.VillainKind, row.OwnKind);

        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"),
            b => b.TextContent.Trim() == "Update the game's copy"));
    }

    /// <summary>
    /// A player's Villain in the same app is still the request it was: the shortcut is the GM's
    /// alone. The control for the test above, through the same page.
    /// </summary>
    [Fact]
    public async Task APlayersVillainStillAsksWhoseNemesisItIs()
    {
        var ctx = new RenderContext(storesForReal: true);
        await using var _ = ctx;

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = ctx.Api.Campaign(CampaignId, "Nightfall",
            StoredCampaign.Write(new Campaign(CampaignId, "Nightfall", "standard", 8, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.Equal(SaveOutcome.Saved, await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(BossId, "Big Boss McGee", Boss(), SheetMode.Villain));
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(BossId);
        Assert.NotNull(await ctx.Services.GetRequiredService<ApiMembershipStore>().JoinAsync(code, BossId, "Big Boss McGee"));
        ctx.Session.Open(Boss(), SheetMode.Villain);

        var page = ctx.Render<Campaigns>();
        await Press(page, "Send for approval");

        Assert.Contains("you have no Hero in this game yet", Squeezed(page.Markup), StringComparison.Ordinal);
    }

    /// <summary>
    /// The GM's roster sorts their own Villain as a Villain and under nobody, and says it is
    /// theirs rather than "Approved"; their own Hero sits among the Heroes.
    /// </summary>
    [Fact]
    public async Task TheGmsRosterSortsTheirOwnVillainAsTheirsAndUnderNobody()
    {
        var (ctx, membership) = await TheGmSeatsTheirOwn(SheetMode.Villain, withPlayer: true);
        await using var _ = ctx;
        Assert.NotNull(await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .SubmitAsync(membership, Boss(), SheetMode.Villain));

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));
        string[] Names() => [.. page.FindAll(".campaign-list > li .nm").Select(n => n.TextContent.Trim())];

        page.WaitForAssertion(() => Assert.Equal(["Jetstream", "Big Boss McGee"], Names()));
        Assert.Empty(page.FindAll(".campaign-list > li.nemesis-pair"));
        Assert.Contains("Your Villain, in the game", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Nemesis of", page.Markup, StringComparison.Ordinal);

        await page.FindAll(".roster-order button").Single(b => b.TextContent.Trim() == "Heroes").ClickAsync(new MouseEventArgs());
        Assert.Equal(["Jetstream"], Names());

        await page.FindAll(".roster-order button").Single(b => b.TextContent.Trim() == "Villains").ClickAsync(new MouseEventArgs());
        Assert.Equal(["Big Boss McGee"], Names());
    }

    /// <summary>The GM's own Hero sits among the Heroes, and is called theirs.</summary>
    [Fact]
    public async Task TheGmsOwnHeroSitsAmongTheHeroes()
    {
        var (ctx, membership) = await TheGmSeatsTheirOwn(SheetMode.Hero);
        await using var _ = ctx;
        var hero = Boss();
        hero.IsVillain = false;
        Assert.NotNull(await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .SubmitAsync(membership, hero, SheetMode.Hero));

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));

        page.WaitForAssertion(() => Assert.Contains("Yours, in the game", page.Markup, StringComparison.Ordinal));
        await page.FindAll(".roster-order button").Single(b => b.TextContent.Trim() == "Heroes").ClickAsync(new MouseEventArgs());
        Assert.Equal(["Big Boss McGee"], page.FindAll(".campaign-list > li .nm").Select(n => n.TextContent.Trim()));
    }
}
