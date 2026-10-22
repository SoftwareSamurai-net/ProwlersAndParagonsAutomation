using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>A Villain approved into a campaign becomes the GM's, and comes back to its maker as a
/// nemesis</b> — the owner's rulings of 2026-10-01, driven through the screens a person presses.
///
/// <para>The server's half — that the move happens, inside the cap, all or nothing — is held by
/// <c>tests/worker/nemesis.test.mjs</c> against real SQLite. What is here is the half only a
/// browser decides: that sending a Villain warns first and needs a second press, that the word
/// the server acts on is the one this build sends, that the GM is told what approving does and is
/// told when their account is full, and that the player sees a nemesis rather than a gap.</para>
/// </summary>
public sealed class NemesisHandoverTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string VillainId = "c_1111111111111111111111";

    private static CharacterSheet TheHollowRegent() => new()
    {
        SelectedTierId = "standard",
        TraitCapRank = 8,
        Name = "The Hollow Regent",
        IsVillain = true,
        AbilityRanks = { ["might"] = 6, ["agility"] = 4 },
        TalentRanks = { ["covert"] = 3 },
    };

    /// <summary>
    /// A game the GM runs, and a player whose character is saved on their account, on screen, and
    /// joined to it — through the real store and the real join route.
    /// </summary>
    /// <summary>The Hero each fixture joined beside its Villain, by context — the key a send names.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RenderContext, string> HeroOf = new();

    private const string JetstreamId = "c_2222222222222222222222";

    private static async Task<(RenderContext Ctx, string Membership)> AtTheTable(SheetMode mode, bool withHero = true)
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = ctx.Api.Campaign(CampaignId, "Nightfall",
            StoredCampaign.Write(new Campaign(CampaignId, "Nightfall", "standard", 8, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var sheet = TheHollowRegent();
        sheet.IsVillain = mode == SheetMode.Villain;

        Assert.Equal(SaveOutcome.Saved, await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(VillainId, "The Hollow Regent", sheet, mode));
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(VillainId);

        var joined = await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .JoinAsync(code, VillainId, "The Hollow Regent");
        Assert.NotNull(joined);

        // The same player's Hero in the same game, which a Villain is sent as the nemesis of.
        if (withHero)
        {
            Assert.Equal(SaveOutcome.Saved, await ctx.Services.GetRequiredService<ApiCharacterStore>()
                .SaveAsync(JetstreamId, "Jetstream", new CharacterSheet { Name = "Jetstream", SelectedTierId = "standard" }, SheetMode.Hero));
            var hero = await ctx.Services.GetRequiredService<ApiMembershipStore>()
                .JoinAsync(code, JetstreamId, "Jetstream");
            Assert.NotNull(hero);
            HeroOf.AddOrUpdate(ctx, hero!.Value.Id);
        }

        var onScreen = TheHollowRegent();
        onScreen.IsVillain = mode == SheetMode.Villain;
        ctx.Session.Open(onScreen, mode);

        return (ctx, joined!.Value.Id);
    }

    /// <summary>Text with every run of whitespace one space, as a reader sees it.</summary>
    private static string Squeezed(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

    private static async Task Press(IRenderedComponent<Campaigns> page, string label) =>
        await page.FindAll("button").Single(b => b.TextContent.Trim() == label)
            .ClickAsync(new MouseEventArgs());

    private static async Task<MembershipSummary> PlayersRow(RenderContext ctx, string membership) =>
        (await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync())!
            .Single(m => m.Id == membership);

    private static async Task<MembershipSummary> GmsRow(RenderContext ctx, string membership)
    {
        var was = ctx.Api.SignedIn;
        ctx.Api.SignedIn = ("u_gm", "The GM");
        var row = (await ctx.Services.GetRequiredService<ApiMembershipStore>().InboxAsync())!
            .Single(m => m.Id == membership);
        ctx.Api.SignedIn = was;

        return row;
    }

    // ── Sending ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendingAVillainWarnsFirstAndSendsNothingUntilTheSecondPress()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        var page = ctx.Render<Campaigns>();

        await Press(page, "Send for approval");

        Assert.Contains("If the GM approves this Villain, it becomes theirs for good.",
            page.Markup, StringComparison.Ordinal);
        Assert.False((await PlayersRow(ctx, membership)).HasPending, "the first press sent it");
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Send for approval");

        // **A Hero has to be picked first**, and only the sender's own Heroes are offered.
        Assert.True(page.FindAll("button").Single(b => b.TextContent.Trim() == "Send it anyway").HasAttribute("disabled"));
        var offered = page.FindAll(".hero-pick label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["Jetstream"], offered);
        await page.Find(".hero-pick input").ChangeAsync(new ChangeEventArgs { Value = "on" });
        Assert.False(page.FindAll("button").Single(b => b.TextContent.Trim() == "Send it anyway").HasAttribute("disabled"));

        await Press(page, "Send it anyway");
        Assert.Equal(HeroOf.TryGetValue(ctx, out var key) ? key : null, (await GmsRow(ctx, membership)).NemesisOf);

        Assert.True((await PlayersRow(ctx, membership)).HasPending, "the second press did not send it");
        Assert.Equal("villain", (await GmsRow(ctx, membership)).PendingKind);
        Assert.DoesNotContain("becomes theirs for good", page.Markup, StringComparison.Ordinal);
        Assert.Contains("It is still yours until the GM approves it.", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepingItSendsNothingAndPutsTheButtonBack()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        var page = ctx.Render<Campaigns>();

        await Press(page, "Send for approval");
        await Press(page, "Keep it");

        Assert.False((await PlayersRow(ctx, membership)).HasPending);
        Assert.DoesNotContain("becomes theirs for good", page.Markup, StringComparison.Ordinal);
        Assert.Single(page.FindAll("button"), b => b.TextContent.Trim() == "Send for approval");
    }

    [Fact]
    public async Task AHeroGoesOnTheFirstPressAndIsSentAsAHero()
    {
        // The control for the warning: the same row, the same press, a Hero.
        var (ctx, membership) = await AtTheTable(SheetMode.Hero);
        await using var _ = ctx;
        var page = ctx.Render<Campaigns>();

        await Press(page, "Send for approval");

        Assert.DoesNotContain("becomes theirs for good", page.Markup, StringComparison.Ordinal);
        Assert.True((await PlayersRow(ctx, membership)).HasPending);
        Assert.Equal("hero", (await GmsRow(ctx, membership)).PendingKind);
        Assert.False((await GmsRow(ctx, membership)).ApprovingHandsOver);
    }

    // ── Approving ─────────────────────────────────────────────────────────────────────────

    private static async Task<IRenderedComponent<CampaignApproval>> TheGmOpensTheRequest(
        RenderContext ctx, string membership)
    {
        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .SubmitAsync(membership, TheHollowRegent(), SheetMode.Villain, HeroOf.TryGetValue(ctx, out var hero) ? hero : null));

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));
        await page.FindAll(".campaign-row .btn")
            .First(b => b.TextContent.Trim() == "Read the changes").ClickAsync(new MouseEventArgs());

        return page;
    }

    [Fact]
    public async Task TheGmIsToldApprovingTakesTheVillainAndApprovingMovesIt()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        var page = await TheGmOpensTheRequest(ctx, membership);

        Assert.Contains("This is a Villain. Approving moves it to your characters for good",
            Squeezed(page.Find(".campaign-diff").TextContent), StringComparison.Ordinal);

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Approve and take it")
            .ClickAsync(new MouseEventArgs());

        Assert.Contains("Approved. The Villain is yours now", page.Markup, StringComparison.Ordinal);

        var gms = await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync();
        Assert.Contains(gms.Characters, c => c.Label == "The Hollow Regent" && c.Kind == "villain");

        ctx.Api.SignedIn = ("u_player", "The Player");
        var players = await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync();
        Assert.DoesNotContain(players.Characters, c => c.Id == VillainId);
        Assert.True((await PlayersRow(ctx, membership)).HandedOver);
    }

    [Fact]
    public async Task AFullAccountIsToldTheLimitAndTheVillainStaysWaiting()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var gmStore = ctx.Services.GetRequiredService<ApiCharacterStore>();
        for (var i = 0; i < ctx.Api.Limit; i++)
        {
            Assert.Equal(SaveOutcome.Saved, await gmStore.SaveAsync(
                $"c_npc{i:D19}", $"NPC {i}", new CharacterSheet { Name = $"NPC {i}" }, SheetMode.Villain));
        }

        var page = await TheGmOpensTheRequest(ctx, membership);
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Approve and take it")
            .ClickAsync(new MouseEventArgs());

        Assert.Contains($"Not approved: your account already holds {ctx.Api.Limit} characters",
            page.Markup, StringComparison.Ordinal);
        Assert.Contains("It is still waiting.", page.Markup, StringComparison.Ordinal);

        var row = await GmsRow(ctx, membership);
        Assert.True(row.HasPending);
        Assert.False(row.HandedOver);

        // And the player still holds it: a refused approval moves nothing on either account.
        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.Contains((await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync()).Characters,
            c => c.Id == VillainId);
    }

    [Fact]
    public async Task LeavingANemesisSaysTheGmStillHoldsIt()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        await HandedOver(ctx, membership);

        var page = ctx.Render<Campaigns>();
        await page.Find(".nemesis-standing button").ClickAsync(new MouseEventArgs());
        await Press(page, "Leave for good");

        Assert.Contains("Its GM still holds the Villain you gave them.", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("no longer holds a copy", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".nemesis"));
    }

    // ── What the player sees afterwards ───────────────────────────────────────────────────

    private static async Task HandedOver(RenderContext ctx, string membership)
    {
        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .SubmitAsync(membership, TheHollowRegent(), SheetMode.Villain, HeroOf.TryGetValue(ctx, out var hero) ? hero : null));

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var version = (await store.InboxAsync())!.Single(m => m.Id == membership).PendingVersion;
        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, version)).Outcome);

        ctx.Api.SignedIn = ("u_player", "The Player");
    }

    [Fact]
    public async Task ThePlayerSeesTheirNemesisAndNothingToSend()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        await HandedOver(ctx, membership);

        var page = ctx.Render<Campaigns>();

        var nemesis = page.Find(".nemesis");
        Assert.Contains("Your nemesis", nemesis.TextContent, StringComparison.Ordinal);
        Assert.Equal("The Hollow Regent", nemesis.QuerySelector(".nemesis-name")!.TextContent.Trim());
        Assert.Contains("The GM of Nightfall holds it now.", nemesis.TextContent, StringComparison.Ordinal);
        Assert.Contains("Your nemesis · hunts Jetstream", nemesis.TextContent, StringComparison.Ordinal);
        Assert.Equal(2, nemesis.QuerySelectorAll(".nemesis-veil").Length);
        Assert.Equal(2, nemesis.QuerySelectorAll(".nemesis-eye").Length);

        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Send for approval");
        Assert.Contains("Given to Nightfall as a nemesis", page.Markup, StringComparison.Ordinal);
        Assert.Single(page.Find(".nemesis-standing").QuerySelectorAll("button"), b => b.TextContent.Trim() == "Leave");
    }

    [Fact]
    public async Task AHeroApprovalDrawsNoNemesis()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Hero);
        await using var _ = ctx;

        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .SubmitAsync(membership, TheHollowRegent(), SheetMode.Hero));
        ctx.Api.SignedIn = ("u_gm", "The GM");
        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();
        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, 1)).Outcome);
        ctx.Api.SignedIn = ("u_player", "The Player");

        var page = ctx.Render<Campaigns>();

        Assert.Empty(page.FindAll(".nemesis"));
        Assert.Contains("Approved", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRosterSaysTheVillainWasGivenAwayAndOffersNothingToOpen()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        await HandedOver(ctx, membership);

        var roster = ctx.Render<CharacterManager>();

        var given = roster.Find("li.given-away");
        Assert.Contains("The Hollow Regent", given.TextContent, StringComparison.Ordinal);
        Assert.Contains("Given to Nightfall as a nemesis", given.TextContent, StringComparison.Ordinal);
        Assert.Empty(given.QuerySelectorAll("button, a"));
        Assert.DoesNotContain(roster.FindAll(".open-target"),
            b => b.TextContent.Contains("The Hollow Regent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATabStillHoldingTheVillainIsToldItsEditsGoNowhere()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        await HandedOver(ctx, membership);

        var store = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var said = new List<string>();
        store.WriteRefused += said.Add;

        await store.SaveAsync(TheHollowRegent(), SheetMode.Villain);

        Assert.Equal(
            ["The Hollow Regent was not saved: it was given to a campaign as a nemesis, and it is the GM's now."],
            said);
        Assert.DoesNotContain((await store.ListAsync()).Characters, c => c.Id == VillainId);
    }

    [Fact]
    public async Task AVillainWithNoHeroInTheGameIsToldToJoinOneFirst()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain, withHero: false);
        await using var _ = ctx;
        var page = ctx.Render<Campaigns>();

        await Press(page, "Send for approval");

        Assert.Contains("you have no Hero in this game yet", Squeezed(page.Markup), StringComparison.Ordinal);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Send it anyway");
        Assert.False((await PlayersRow(ctx, membership)).HasPending);
    }

    [Fact]
    public async Task TheGmSeesEachNemesisUnderItsHeroAndCanShowEitherKindAlone()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        await HandedOver(ctx, membership);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));

        string[] Names() => [.. page.FindAll(".campaign-list > li .nm").Select(n => n.TextContent.Trim())];

        // Everyone: the Hero, then its nemesis set in beneath it on the night ground.
        Assert.Equal(["Jetstream", "The Hollow Regent"], Names());
        var pair = page.Find(".campaign-list > li.nemesis-pair");
        Assert.Contains("The Hollow Regent", pair.TextContent, StringComparison.Ordinal);
        Assert.Contains("Nemesis of Jetstream", pair.TextContent, StringComparison.Ordinal);
        Assert.NotNull(pair.QuerySelector(".nemesis.nemesis-row"));

        await page.FindAll(".roster-order button").Single(b => b.TextContent.Trim() == "Heroes").ClickAsync(new MouseEventArgs());
        Assert.Equal(["Jetstream"], Names());

        await page.FindAll(".roster-order button").Single(b => b.TextContent.Trim() == "Villains").ClickAsync(new MouseEventArgs());
        Assert.Equal(["The Hollow Regent"], Names());
        Assert.Empty(page.FindAll(".campaign-list > li.nemesis-pair"));
    }

    [Fact]
    public async Task TheGmReKeysANemesisOfTheirsToAnotherHero()
    {
        var (ctx, membership) = await AtTheTable(SheetMode.Villain);
        await using var _ = ctx;
        await HandedOver(ctx, membership);

        // Another player's Hero in the same game: the GM may key to any Hero at the table.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = (await ctx.Services.GetRequiredService<ApiCampaignStore>().ListAsync())!
            .Single(c => c.Id == CampaignId).JoinCode!;
        ctx.Api.SignedIn = ("u_other", "Another Player");
        var brian = await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .JoinAsync(code, "c_3333333333333333333333", "Brian Talison");
        Assert.NotNull(brian);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));
        await page.Find(".nemesis-row select").ChangeAsync(new ChangeEventArgs { Value = brian!.Value.Id });

        Assert.Equal(brian.Value.Id, (await GmsRow(ctx, membership)).NemesisOf);
        Assert.Contains("The Hollow Regent is Brian Talison's nemesis now.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Nemesis of Brian Talison", page.Find(".campaign-list > li.nemesis-pair").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNemesisNamesNoGameWhereTheCampaignIsGone()
    {
        using var ctx = new RenderContext();

        var block = ctx.Render<Nemesis>(p => p.Add(n => n.Name, "The Hollow Regent"));

        Assert.Contains("Yours once. The GM holds it now.", block.Markup, StringComparison.Ordinal);
    }
}
