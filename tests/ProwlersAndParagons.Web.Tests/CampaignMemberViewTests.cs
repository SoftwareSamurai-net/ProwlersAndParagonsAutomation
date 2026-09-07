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
    /// <b>The house-rules panel says whose copy it is showing, and now shows the game's beside
    /// it.</b>
    ///
    /// <para>Driven all the way through the state it is about: the player joins, the GM then edits
    /// the campaign, and the player's own list keeps printing what was copied in at the join.
    /// Nothing refreshes it — a join writes into empty fields only, and there is no other writer —
    /// so the copy going stale is the state, asserted rather than wished away.</para>
    ///
    /// <para><b>What changed with item 30 is the second list, not the first.</b> The copy is still
    /// what the character is costed by and what travels to a fight; <c>GET
    /// /api/memberships/{id}/table</c> is what lets the same panel say what the table has decided
    /// since. Both headings name whose list they are, because "only the GM can change them" was
    /// true of the game and false of the list underneath it, and a reader has no way to tell those
    /// apart from a screen that states the first.</para>
    /// </summary>
    [Fact]
    public async Task TheHouseRulesPanelSaysItIsTheCharactersCopyAndShowsTheTableBesideIt()
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

        // The control on everything below: the live read really happened, and it is the new
        // player-scoped address rather than the campaign one a member is answered 404 by.
        Assert.Contains(ctx.Api.Asked,
            a => a.StartsWith("GET /api/memberships/", StringComparison.Ordinal)
                 && a.EndsWith("/table", StringComparison.Ordinal));

        // The copy is stale, silently. Still true, and still the reason the panel has to say so.
        Assert.Contains("House rules on this character", after.Markup, StringComparison.Ordinal);
        Assert.Contains("Fatal Damage", after.Markup, StringComparison.Ordinal);
        Assert.Contains("Immortality costs 9 HP", after.Markup, StringComparison.Ordinal);
        Assert.Contains("when it joined", after.Markup, StringComparison.Ordinal);
        Assert.Contains("joins again", after.Markup, StringComparison.Ordinal);

        // And the table as it stands is beside it, under a heading that says which is which.
        Assert.Contains("House rules at the table now", after.Markup, StringComparison.Ordinal);
        Assert.Contains("Tough Minions", after.Markup, StringComparison.Ordinal);
        Assert.Contains("Immortality costs 12 HP", after.Markup, StringComparison.Ordinal);

        // Two lists, not one that has replaced the other — the whole point of the pair.
        Assert.Equal(2, after.FindAll("ul.house-rules-read").Count);

        // A sentence per differing setting, in the approval screen's own idiom. Read off the
        // rendered rows rather than out of the whole markup, because the two lists above contain
        // every one of these words already and a substring search would pass without a diff.
        var moved = after.FindAll("ul.diff-rows > li").Select(li => li.TextContent.Trim()).ToList();

        Assert.Contains(moved, t => t.Contains("Fatal Damage", StringComparison.Ordinal)
                                    && t.Contains("on → off", StringComparison.Ordinal));
        Assert.Contains(moved, t => t.Contains("Tough Minions", StringComparison.Ordinal)
                                    && t.Contains("off → on", StringComparison.Ordinal));
        Assert.Contains(moved, t => t.Contains("Immortality", StringComparison.Ordinal)
                                    && t.Contains("9 HP → 12 HP", StringComparison.Ordinal));

        // And nothing has been repaired: the sheet is exactly as the join left it.
        Assert.Equal(9, ctx.Session.Sheet.ImmortalityCost);
        Assert.True(ctx.Session.Sheet.CampaignTable?.FatalDamage);
        Assert.False(ctx.Session.Sheet.CampaignTable?.ToughMinions);

        // It must not claim to be the game's live answer. This is the sentence that shipped.
        Assert.DoesNotContain("Only the GM can change them.", after.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A character that joined before the GM decided anything is told it carries nothing.</b>
    ///
    /// <para>The direction that reported nothing at all, and the whole of item 30's defect. Both
    /// of <c>Inspect</c>'s house-rule checks need the campaign <em>and</em> the character to have
    /// set something — item 15's condition, unchanged — so a sheet with a null price at a table
    /// charging 12 produced no finding, no panel to print one under, and an engine going on
    /// costing Immortality at the book's 3.</para>
    ///
    /// <para><b>Reported and never repaired</b>, which is why the sentence points at joining again:
    /// that is the one act that writes into the still-empty field, and copying the price in from a
    /// panel would move somebody's spend while they were reading a list.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterThatJoinedBeforeTheTableDecidedIsToldItCarriesNoHouseRules()
    {
        // A game that had decided nothing at the moment of the join, so the `??=` copied nothing.
        var (ctx, page) = await AMemberOf();
        await using var _ = ctx;

        Assert.Null(ctx.Session.Sheet.ImmortalityCost);
        Assert.Null(ctx.Session.Sheet.CampaignTable);
        Assert.DoesNotContain("House rules on this character", page.Markup, StringComparison.Ordinal);

        // The GM decides afterwards. Nothing writes this onto the character — there is no writer
        // but the join — which is exactly the state that used to go unreported.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false,
                new CampaignTable { SlowHealing = true }, 12)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var after = ctx.Render<Campaigns>();

        Assert.Contains("joined before the game set its house rules", after.Markup,
            StringComparison.Ordinal);

        // The figure, because "costed by the book" is not something a reader can act on without
        // being told what the table charges instead — the promise `CampaignFinding` makes.
        Assert.Contains("The game charges 12 HP for Immortality.", after.Markup,
            StringComparison.Ordinal);

        // The remedy is joining again and nothing has been changed either way.
        Assert.Contains("Join again", after.Markup, StringComparison.Ordinal);
        Assert.Null(ctx.Session.Sheet.ImmortalityCost);
        Assert.Null(ctx.Session.Sheet.CampaignTable);
    }

    /// <summary>
    /// <b>A live read that answered nothing leaves the panel exactly as it was.</b>
    ///
    /// <para>The other half of the pair, and what keeps the new list from being a thing the screen
    /// depends on. A game the GM has deleted, a membership that is not the reader's, a payload this
    /// build cannot read and a server that is not there are one answer to this page: draw the copy
    /// alone, which is what it drew before any of this existed.</para>
    /// </summary>
    [Fact]
    public async Task WithNoLiveTableTheMemberStillSeesTheCopyTheirCharacterCarries()
    {
        var (ctx, page) = await AMemberOf(new CampaignTable { FatalDamage = true }, immortality: 9);
        await using var _ = ctx;

        // The control: with the game there, both lists are drawn.
        Assert.Contains("House rules at the table now", page.Markup, StringComparison.Ordinal);

        // Through the real route, as the GM, so the state under test is one a request produces.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        await ctx.Services.GetRequiredService<AccountCampaignStore>().DeleteAsync(CampaignId);
        ctx.Api.SignedIn = ("u_player", "The Player");

        var after = ctx.Render<Campaigns>();

        Assert.Contains("House rules on this character", after.Markup, StringComparison.Ordinal);
        Assert.Contains("Fatal Damage", after.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("House rules at the table now", after.Markup, StringComparison.Ordinal);
        Assert.Empty(after.FindAll("ul.diff-rows"));
    }

    /// <summary>
    /// <b>The GM's own screen is unchanged, and costs no request.</b>
    ///
    /// <para>A GM reads their campaign at its own address and always could; the table address is
    /// authorised by a membership row, which a GM has none of for their own game, so asking would
    /// be a round trip answered 404. The page therefore asks only where the account's own read
    /// answered nothing — and the second list, which is about a copy going stale, is meaningless
    /// on a screen whose reader is the one who changes the game.</para>
    /// </summary>
    [Fact]
    public async Task TheGmsOwnScreenNeitherAsksForTheLiveTableNorDrawsASecondList()
    {
        await using var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false,
                new CampaignTable { FatalDamage = true }, 9)));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);

        ctx.Session.Sheet.CampaignId = CampaignId;
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.ImmortalityCost = 9;
        ctx.Session.Sheet.CampaignTable = new CampaignTable { FatalDamage = true };

        var page = ctx.Render<Campaigns>();

        // The control: this reader really does resolve the campaign, so the absence below is a
        // request not made rather than a page that never got as far as resolving anything.
        var store = ctx.Services.GetRequiredService<AccountCampaignStore>();

        Assert.NotNull(await store.ForAsync(ctx.Session.Sheet));

        Assert.DoesNotContain(ctx.Api.Asked, a => a.EndsWith("/table", StringComparison.Ordinal));
        Assert.DoesNotContain("House rules at the table now", page.Markup, StringComparison.Ordinal);
    }
}
