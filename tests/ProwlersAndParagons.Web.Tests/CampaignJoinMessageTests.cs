using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>What a join tells the player it did, driven by pressing the button.</b>
///
/// <para>The sentence used to be chosen from the outcome alone, which cannot carry it: joining
/// copies the tier only into a character that has none and the cap only into a character that has
/// none, so one outcome covers four different things having happened. It printed "Its tier and its
/// Trait Cap are now yours" over a join that took the tier and left a cap the character already
/// had — the one thing joining most carefully does not do, claimed out loud. A message claiming a
/// change nobody made is worse than no message: it teaches a reader to distrust the ones that are
/// true.</para>
///
/// <para>Every test here types a code and presses Join, rather than calling
/// <c>CampaignJoin.Apply</c>, because the fault was in the sentence and not in the join.</para>
/// </summary>
public sealed class CampaignJoinMessageTests
{
    private const string CampaignId = "g_0000000000000000000000";

    /// <summary>
    /// A game somebody else is running, and a player signed in to join it. The code is minted by
    /// the fake server the way the real one mints it.
    /// </summary>
    private static async Task<(RenderContext Ctx, string Code)> AGameToJoin(
        string? tierId = "standard", int? traitCap = 6)
    {
        // **A storage that really stores, because the join needs a real character id.** The
        // current-character pointer defaults to this browser's legacy slot, whose name the server
        // refuses as ill-formed — so with bUnit's recorder, which answers null to every read, the
        // join is refused before any of this is reached and every assertion below would be about
        // an error message.
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(CampaignId, "Pinnacle City", tierId, traitCap, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        await ctx.Services.GetRequiredService<SavedCharacters>()
            .SetCurrentAsync("c_1111111111111111111111");

        return (ctx, code);
    }

    /// <summary>Types the code, presses Join, and hands back what the page then says.</summary>
    private static string JoinWith(RenderContext ctx, string code)
    {
        var page = ctx.Render<Campaigns>();

        page.Find("#join-code").Input(code);
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Join").Click();

        return page.Markup;
    }

    /// <summary>
    /// A character with no tier takes both, and both are named — the cap with its rank, because
    /// the rank is the thing that moved.
    /// </summary>
    [Fact]
    public async Task AJoinThatTakesBothSaysBoth()
    {
        var (ctx, code) = await AGameToJoin();
        await using var _ = ctx;

        var markup = JoinWith(ctx, code);

        Assert.Contains("Its tier and its 6d Trait Cap are now yours", markup, StringComparison.Ordinal);
        Assert.Equal(6, ctx.Session.Sheet.TraitCapRank);
    }

    /// <summary>
    /// A character already at the game's tier takes the cap alone, and the sentence says what that
    /// means: Resolve is measured from the cap, so a cap arriving moved a figure the player may
    /// have spent Hero Points on.
    /// </summary>
    [Fact]
    public async Task AJoinThatTakesOnlyTheCapSaysSo()
    {
        var (ctx, code) = await AGameToJoin();
        await using var _ = ctx;

        ctx.Session.Sheet.SelectedTierId = "standard";

        var markup = JoinWith(ctx, code);

        Assert.Contains("Its 6d Trait Cap is now yours", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Its tier and", markup, StringComparison.Ordinal);
        Assert.Equal(6, ctx.Session.Sheet.TraitCapRank);
    }

    /// <summary>
    /// A game that sets no cap takes the tier alone, and says only that.
    /// </summary>
    [Fact]
    public async Task AJoinThatTakesOnlyTheTierSaysSo()
    {
        var (ctx, code) = await AGameToJoin(traitCap: null);
        await using var _ = ctx;

        var markup = JoinWith(ctx, code);

        Assert.Contains("Its tier is now yours", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Trait Cap is now yours", markup, StringComparison.Ordinal);
        Assert.Null(ctx.Session.Sheet.TraitCapRank);
    }

    /// <summary>
    /// <b>The one that was wrong.</b> A character built to its own 8d cap joins a 6d game, keeps
    /// its cap, and is told the join happened and nothing else — because nothing else happened.
    /// </summary>
    [Fact]
    public async Task AJoinThatTakesNothingClaimsNothing()
    {
        var (ctx, code) = await AGameToJoin();
        await using var _ = ctx;

        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.TraitCapRank = 8;

        var markup = JoinWith(ctx, code);

        Assert.Contains("Joined Pinnacle City.", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("now yours", markup, StringComparison.Ordinal);

        // The cap it arrived with, untouched — which is what the sentence was lying about.
        Assert.Equal(8, ctx.Session.Sheet.TraitCapRank);
    }
}
