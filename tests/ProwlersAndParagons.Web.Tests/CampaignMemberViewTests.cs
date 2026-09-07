using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>What a player sees on <c>/campaign</c> about a game they are in, as opposed to what a GM
/// sees about a game they run.</b>
///
/// <para>These are two different screens drawn by one page, and the difference is not a design
/// choice — it is the server. A campaign's payload is scoped to the account that owns it, so a
/// player's browser resolves <em>no campaign at all</em> for a game that is perfectly alive. Every
/// finding <c>CampaignJoin.Inspect</c> produces from a resolved campaign is therefore unreachable
/// for a member, and the one it produces from an unresolved campaign was being shown to all of
/// them.</para>
///
/// <para><b>Both halves below were true of the shipped page and both were wrong.</b> A player who
/// had just successfully joined was told "This character names a campaign that is not here … the
/// campaign may be on another browser, or may have been deleted", printed directly above a panel
/// listing that same campaign's house rules and directly above the campaign's own name in "Games
/// you are in". And the panel underneath said the rules on it are the game's and that only the GM
/// can change them — which reads as a live view of a payload this browser cannot read, and stops
/// being true the moment the GM edits the campaign.</para>
/// </summary>
public sealed class CampaignMemberViewTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string CharacterId = "c_1111111111111111111111";

    /// <summary>A game run by somebody else, joined by this browser's character.</summary>
    private static async Task<(RenderContext Ctx, IRenderedComponent<Campaigns> Page)> AMemberOf(
        CampaignTable? table = null, int? immortality = null)
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false, table, immortality)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Join").ClickAsync(new());

        return (ctx, page);
    }

    /// <summary>
    /// <b>A member is not told their game has been deleted.</b>
    ///
    /// <para>The membership row is the evidence the finding cannot have: the player-scoped read on
    /// the server answers which games this reader is in, and the page holds that list already for
    /// the panel below. So a campaign the reader is a member of is there, and the sentence is
    /// about a store rather than about the world.</para>
    /// </summary>
    [Fact]
    public async Task AMemberIsNotToldTheirGameMayHaveBeenDeleted()
    {
        var (ctx, page) = await AMemberOf(new CampaignTable { FatalDamage = true }, immortality: 9);
        await using var _ = ctx;

        // The control on everything below: the join really happened, so this is a member looking
        // at a live game rather than an empty page satisfying an absence.
        Assert.Equal(CampaignId, ctx.Session.Sheet.CampaignId);
        Assert.Contains("Pinnacle City", page.Markup, StringComparison.Ordinal);

        // And the finding really is the one being suppressed rather than none being computed —
        // otherwise this would go green over a page that had stopped resolving anything at all.
        var store = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var resolved = await store.ForAsync(ctx.Session.Sheet);

        Assert.Null(resolved);
        Assert.Equal(CampaignJoin.UnknownCampaign,
            CampaignJoin.Inspect(ctx.Session.Sheet, resolved)?.Code);

        Assert.DoesNotContain("names a campaign that is not here", page.Markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain("may have been deleted", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A character naming a campaign nobody here is a member of is still told.</b>
    ///
    /// <para>The other half, and the one that keeps the suppression above from being a way of never
    /// reporting anything: a deleted campaign, or one on another browser, leaves its members naming
    /// it on purpose — <c>docs/guide/accounts-server.md</c> records that as owner-approved, so that
    /// restoring the campaign puts everything back. That state has no membership row, and the
    /// sentence is exactly right about it.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterNamingAGameThisReaderIsNotInIsStillTold()
    {
        await using var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_player", "The Player");
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);

        ctx.Session.Sheet.CampaignId = "g_9999999999999999999999";

        var page = ctx.Render<Campaigns>();

        Assert.Contains("names a campaign that is not here", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The house-rules panel says whose copy it is showing, because it cannot show the
    /// game's.</b>
    ///
    /// <para>Driven all the way through the state it is about: the player joins, the GM then edits
    /// the campaign, and the player's panel keeps printing what was copied in at the join. Nothing
    /// refreshes it — a join writes into empty fields only, and there is no other writer — and no
    /// finding says so, because the finding that would is computed from a campaign a member cannot
    /// resolve. So the panel is the only thing on this screen in a position to be honest about what
    /// it is, and "only the GM can change them" was the sentence that made it dishonest: true of
    /// the game and false of the list underneath it.</para>
    /// </summary>
    [Fact]
    public async Task TheHouseRulesPanelSaysItIsTheCharactersCopyAndNotTheGamesLiveOne()
    {
        var (ctx, page) = await AMemberOf(new CampaignTable { FatalDamage = true }, immortality: 9);
        await using var _ = ctx;

        Assert.Contains("Fatal Damage", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Immortality costs 9 HP", page.Markup, StringComparison.Ordinal);

        // The GM changes the game out from under the character that has already joined.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false,
                new CampaignTable { ToughMinions = true }, 12)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var after = ctx.Render<Campaigns>();

        // The copy is stale, silently — this is the state, asserted rather than wished away.
        Assert.Contains("Fatal Damage", after.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Tough Minions", after.Markup, StringComparison.Ordinal);
        Assert.Contains("Immortality costs 9 HP", after.Markup, StringComparison.Ordinal);

        // So the panel has to say that is what it is. It is the character's copy, taken at the
        // join, and a change the GM makes does not reach it.
        Assert.Contains("when it joined", after.Markup, StringComparison.Ordinal);
        Assert.Contains("joins again", after.Markup, StringComparison.Ordinal);

        // And it must not claim to be the game's live answer. This is the sentence that shipped.
        Assert.DoesNotContain("Only the GM can change them.", after.Markup, StringComparison.Ordinal);
    }
}
