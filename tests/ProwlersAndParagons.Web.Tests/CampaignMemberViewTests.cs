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
    /// <b>A cap the game set after the join is reported too, and it was not.</b>
    ///
    /// <para>The finding built for the empty copy asked about the price and the switches and left
    /// the Trait Cap out — which made it inconsistent with <c>CAMPAIGN_TRAIT_CAP_MISMATCH</c> above
    /// it in exactly the direction where the cap matters most. <c>Apply</c> copies all three
    /// settings with the same <c>??=</c>, so all three go missing the same way; and the cap is the
    /// one of them that moves the most, because <c>EffectiveTraitCap</c> feeds rank legality
    /// <em>and</em> Resolve where the price moves a spend and the switches move nothing until a
    /// fight.</para>
    ///
    /// <para><b>Reported and never repaired</b>, the same as the price: the remedy is joining
    /// again, which is the one act that writes into a still-empty field.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterThatJoinedBeforeTheTableCappedIsToldItIsMeasuredByTheBook()
    {
        var (ctx, page) = await AMemberOf();
        await using var _ = ctx;

        // The control: the join happened, and it copied no cap because there was none to copy.
        Assert.Equal(CampaignId, ctx.Session.Sheet.CampaignId);
        Assert.Null(ctx.Session.Sheet.TraitCapRank);

        // The GM caps the game afterwards, and nothing writes it onto the character.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", 6, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var after = ctx.Render<Campaigns>();

        Assert.Contains("joined before the game set its house rules", after.Markup,
            StringComparison.Ordinal);

        // The figure, for the reason every other one is carried: "measured by the book" is not
        // something a reader can act on without being told what the table caps at instead.
        Assert.Contains("The game caps at 6d.", after.Markup, StringComparison.Ordinal);

        Assert.Contains("Join again", after.Markup, StringComparison.Ordinal);
        Assert.Null(ctx.Session.Sheet.TraitCapRank);
    }

    /// <summary>
    /// <b>A rule the game switched on after the join is reported too, and nothing checked it.</b>
    ///
    /// <para>The third arm of <c>CAMPAIGN_HOUSE_RULES_NOT_COPIED</c>: a table that is not the book
    /// against a character carrying no table at all, with no cap and no price missing beside it.
    /// The cap arm and the price arm each had a fixture and this one had none — replacing
    /// <c>switchesMissing</c> with <c>false</c> left the whole browser suite green, so a game that
    /// switched Fatal Damage on after somebody joined said nothing to that player on any
    /// screen.</para>
    ///
    /// <para><b>And carrying no figure is the assertion rather than an omission.</b> The other two
    /// arms print the table's cap and the table's price because those are the numbers that went
    /// missing; a game that has set only switches has neither, and thirteen of them enumerated in a
    /// finding is not a sentence anybody finishes — the same reason the join message says the
    /// rules came with it and lets the panel say which. A cap or a price appearing in this
    /// paragraph would be a figure given for a reason that is not there, which is the fault
    /// <c>Ranks</c> exists to keep out.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterThatJoinedBeforeTheTableSwitchedARuleOnIsToldItPlaysTheBook()
    {
        var (ctx, page) = await AMemberOf();
        await using var _ = ctx;

        // The control: the join happened, and there was nothing at all to copy at the time — so
        // the finding below is about the switches and cannot be the cap's or the price's arm.
        Assert.Equal(CampaignId, ctx.Session.Sheet.CampaignId);
        Assert.Null(ctx.Session.Sheet.CampaignTable);
        Assert.Null(ctx.Session.Sheet.TraitCapRank);
        Assert.Null(ctx.Session.Sheet.ImmortalityCost);
        Assert.DoesNotContain("joined before the game set its house rules", page.Markup,
            StringComparison.Ordinal);

        // The GM switches one optional rule on, and sets neither a cap nor a price.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false,
                new CampaignTable { FatalDamage = true })));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var after = ctx.Render<Campaigns>();

        // The control on the finding: a member computes one only from the live read, so this says
        // the table really was fetched and really does differ from what the character carries.
        Assert.Contains("House rules at the table now", after.Markup, StringComparison.Ordinal);
        Assert.Contains("Fatal Damage", after.Markup, StringComparison.Ordinal);

        // The finding's own paragraph, not the page: the list above prints the switch's name for
        // its own reasons, and a substring search over the markup would pass without a finding.
        var said = after.FindAll("p")
            .Select(e => e.TextContent)
            .SingleOrDefault(t => t.Contains("joined before the game set its house rules",
                StringComparison.Ordinal));

        Assert.True(said is not null,
            "The game switched an optional rule on and the character carries no table, which is "
            + "CAMPAIGN_HOUSE_RULES_NOT_COPIED's third arm — and no element on /campaign carries "
            + "its sentence, so the state reaches no reader. See CampaignJoin.Inspect's "
            + "`switchesMissing`.");

        // No cap and no price, because neither is what went missing. `Ranks` falls through to its
        // empty arm here and that is correct — the numbers in the panel below are the table's own
        // list saying what it has decided, not figures this sentence is entitled to borrow.
        Assert.DoesNotContain("caps at", said!, StringComparison.Ordinal);
        Assert.DoesNotContain("charges", said!, StringComparison.Ordinal);
        Assert.DoesNotContain("HP", said!, StringComparison.Ordinal);

        // The remedy, and nothing repaired: the join is still the only writer.
        Assert.Contains("Join again", after.Markup, StringComparison.Ordinal);
        Assert.Null(ctx.Session.Sheet.CampaignTable);

        // **The positive control.** The same game, joined after it had switched Fatal Damage on,
        // so the character carries the table and nothing is missing. Without it, a page that had
        // stopped drawing findings at all would satisfy every absence above and this fixture would
        // be measuring a screen rather than a state.
        var (took, tookPage) = await AMemberOf(new CampaignTable { FatalDamage = true });
        await using var control = took;

        Assert.True(took.Session.Sheet.CampaignTable?.FatalDamage);
        Assert.Contains("House rules on this character", tookPage.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("joined before the game set its house rules", tookPage.Markup,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The other direction is drawn as rows and is deliberately not a finding.</b>
    ///
    /// <para>The GM clears the table's house rules and the character keeps the copy it took. That
    /// is not a disagreement anybody has to act on — a game that has set nothing is not overruling
    /// anybody, which is the sentence <c>Inspect</c> already applies to a cap — so no finding is
    /// produced for any of the three settings, in either the cap's, the price's or the switches'
    /// case.</para>
    ///
    /// <para><b>Which does not make it silent</b>, and that is what this pins: the member's own
    /// panel draws it, a row per setting, because <c>CampaignDiff.BetweenTables</c> compares the
    /// copy against the live table both ways round. An absence asserted with nothing beside it
    /// would be satisfied by a page that had stopped drawing anything at all.</para>
    /// </summary>
    [Fact]
    public async Task RulesTheGameHasDroppedAreDrawnAsRowsRatherThanReportedAsAFinding()
    {
        var (ctx, page) = await AMemberOf(new CampaignTable { FatalDamage = true }, immortality: 9);
        await using var _ = ctx;

        // The control: the copy was taken, so there is something to go stale.
        Assert.Equal(9, ctx.Session.Sheet.ImmortalityCost);
        Assert.True(ctx.Session.Sheet.CampaignTable?.FatalDamage);

        // The GM drops every house rule the game had.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var after = ctx.Render<Campaigns>();

        // Drawn: the live list says the book, and the rows say what the character still carries.
        Assert.Contains("House rules at the table now", after.Markup, StringComparison.Ordinal);
        Assert.Contains("The book as printed.", after.Markup, StringComparison.Ordinal);

        var moved = after.FindAll("ul.diff-rows > li").Select(li => li.TextContent.Trim()).ToList();

        Assert.Contains(moved, t => t.Contains("Fatal Damage", StringComparison.Ordinal)
                                    && t.Contains("on → off", StringComparison.Ordinal));
        Assert.Contains(moved, t => t.Contains("Immortality", StringComparison.Ordinal)
                                    && t.Contains("9 HP → 3 HP", StringComparison.Ordinal));

        // And no finding: the sentences of both house-rule findings are absent, with the rows
        // above as the control that this page really did compare the two tables.
        Assert.DoesNotContain("joined before the game set its house rules", after.Markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain("carries a different set of optional rules", after.Markup,
            StringComparison.Ordinal);

        // Nothing repaired, in this direction either.
        Assert.Equal(9, ctx.Session.Sheet.ImmortalityCost);
        Assert.True(ctx.Session.Sheet.CampaignTable?.FatalDamage);
    }

    /// <summary>
    /// <b>The live list says when it was read, and can be read again without a reload.</b>
    ///
    /// <para>The heading over it says <em>now</em>, and the page fetches it when the panel opens
    /// and after anything that reloads the lists — never again. So a player sitting on the screen
    /// while their GM changes a setting is reading a list that claims the present tense and means
    /// "when you arrived", which is the same dishonesty the copy's own sentence was rewritten to
    /// fix one heading further down.</para>
    ///
    /// <para><b>Driven against one open page rather than a re-render</b>, because a re-render is a
    /// fresh <c>OnInitializedAsync</c> and would refresh everything for free — the state under test
    /// is exactly the one a reader is in when they have not navigated.</para>
    /// </summary>
    [Fact]
    public async Task TheLiveTableSaysHowOldItIsAndARereadPicksUpAChangeWithoutAReload()
    {
        var (ctx, page) = await AMemberOf(new CampaignTable { FatalDamage = true }, immortality: 9);
        await using var _ = ctx;

        // The control: the list is there and it says how old it is.
        Assert.Contains("House rules at the table now", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Read just now", page.Markup, StringComparison.Ordinal);

        var asked = ctx.Api.Asked.Count(a => a.EndsWith("/table", StringComparison.Ordinal));

        Assert.True(asked > 0, "the panel never read the live table, so nothing below is about it");

        // The GM changes the game while this page is open. Nothing on it moves.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false,
                new CampaignTable { FatalDamage = true }, 12)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        // Stale, and that is the state this control exists for — asserted rather than assumed.
        Assert.Contains("Immortality costs 9 HP", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Immortality costs 12 HP", page.Markup, StringComparison.Ordinal);
        Assert.Equal(asked,
            ctx.Api.Asked.Count(a => a.EndsWith("/table", StringComparison.Ordinal)));

        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Check again").ClickAsync(new());

        // One request, and the new figure is on screen with the difference beside it.
        Assert.Equal(asked + 1,
            ctx.Api.Asked.Count(a => a.EndsWith("/table", StringComparison.Ordinal)));
        Assert.Contains("Immortality costs 12 HP", page.Markup, StringComparison.Ordinal);

        var moved = page.FindAll("ul.diff-rows > li").Select(li => li.TextContent.Trim()).ToList();

        Assert.Contains(moved, t => t.Contains("Immortality", StringComparison.Ordinal)
                                    && t.Contains("9 HP → 12 HP", StringComparison.Ordinal));

        // And the character's own copy is untouched by a read.
        Assert.Equal(9, ctx.Session.Sheet.ImmortalityCost);
    }

    /// <summary>
    /// <b>A recheck that answers nothing says so rather than taking the list away.</b>
    ///
    /// <para>The section vanishing is right on a first render — the panel then draws what it always
    /// drew, which is the copy alone. It is not right under a button somebody pressed: a reader who
    /// watched a list disappear has been told nothing about why, and the four things that produce
    /// this null include a game that has been deleted.</para>
    /// </summary>
    [Fact]
    public async Task ARecheckThatAnswersNothingSaysSoRatherThanRemovingTheListInSilence()
    {
        var (ctx, page) = await AMemberOf(new CampaignTable { FatalDamage = true }, immortality: 9);
        await using var _ = ctx;

        // The control: the list is on screen before the game goes away.
        Assert.Contains("House rules at the table now", page.Markup, StringComparison.Ordinal);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        await ctx.Services.GetRequiredService<AccountCampaignStore>().DeleteAsync(CampaignId);
        ctx.Api.SignedIn = ("u_player", "The Player");

        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Check again").ClickAsync(new());

        Assert.DoesNotContain("House rules at the table now", page.Markup, StringComparison.Ordinal);
        Assert.Contains("The table could not be read just now", page.Markup,
            StringComparison.Ordinal);

        // And the copy is still drawn, which is the whole of what the panel falls back to.
        Assert.Contains("House rules on this character", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Fatal Damage", page.Markup, StringComparison.Ordinal);
        Assert.Contains("when it joined", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A player in two games is shown the table of the one the character on screen is in.</b>
    ///
    /// <para><c>LiveTableAsync</c> has to pick a membership id to ask under, and <c>_playing</c>
    /// holds every membership of every character this account owns — so the row it picks has to be
    /// matched on the campaign the character on screen names. <b>Nothing checked that</b>:
    /// replacing the match with <c>FirstOrDefault()</c> left all 967 browser tests green, and a
    /// player in two games would have read the other game's table under a heading saying it was
    /// theirs, with a difference list computed against a campaign the character has never been
    /// in.</para>
    ///
    /// <para>The wrong game is joined first, so the row that must not be picked is the one an
    /// unmatched read reaches first.</para>
    /// </summary>
    [Fact]
    public async Task APlayerInTwoGamesReadsTheTableOfTheGameTheCharacterOnScreenIsIn()
    {
        const string OtherCampaignId = "g_2222222222222222222222";
        const string OtherCharacterId = "c_3333333333333333333333";

        await using var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        // The one the character on screen is *not* in, written first.
        var otherCode = ctx.Api.Campaign(OtherCampaignId, "Harbour Nights",
            StoredCampaign.Write(new Campaign(
                OtherCampaignId, "Harbour Nights", "standard", null, false,
                new CampaignTable { SlowHealing = true }, 5)));

        var code = ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(
                CampaignId, "Pinnacle City", "standard", null, false,
                new CampaignTable { FatalDamage = true }, 12)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();

        // The other character's membership, put in first and never on screen.
        Assert.NotNull(await memberships.JoinAsync(otherCode, OtherCharacterId, "Someone Else"));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Join").ClickAsync(new());

        // The control: two memberships, and the character on screen is in the second one.
        var mine = await memberships.MineAsync();

        Assert.NotNull(mine);
        Assert.Equal(2, mine.Count);
        Assert.Equal(CampaignId, ctx.Session.Sheet.CampaignId);

        // The live list is Pinnacle City's, not Harbour Nights'.
        Assert.Contains("House rules at the table now", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Immortality costs 12 HP", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Immortality costs 5 HP", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Slow Healing", page.Markup, StringComparison.Ordinal);
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

        // **And the copy still carries its own sentence**, which is what makes this the panel as
        // it was rather than an empty region. A list of switches under a heading, with nothing
        // saying it is a copy or what would take a new one, is the state the heading was rewritten
        // to fix — and it would satisfy every absence above.
        Assert.Contains("copied onto the character when it joined", after.Markup,
            StringComparison.Ordinal);
        Assert.Contains("joins again", after.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The GM of the very row is answered nothing at the member's own address.</b>
    ///
    /// <para>The server's most careful rule on this route — <c>player_user_id</c> and no GM arm, so
    /// the account that owns a campaign reads it at its own address and nowhere else — and
    /// <c>FakeApi</c> mirrors it. Nothing drove that arm: the page test asserts the GM's screen
    /// makes no request at all, which is true and says nothing about what the fake would answer if
    /// one were made. <b>A fake arm nothing exercises is a fake arm free to drift from the
    /// server</b>, and this one is the difference between a rule being tested and being
    /// described.</para>
    /// </summary>
    [Fact]
    public async Task TheGmOfTheRowIsAnsweredNothingAtTheMembersOwnAddress()
    {
        var (ctx, _page) = await AMemberOf(new CampaignTable { FatalDamage = true }, immortality: 9);
        await using var _ = ctx;

        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var mine = await store.MineAsync();

        Assert.NotNull(mine);

        var id = mine.Single().Id;

        // The control: the member is answered, so the null below is a refusal rather than a route
        // that answers nobody.
        Assert.NotNull(await store.TableAsync(id));

        ctx.Api.SignedIn = ("u_gm", "The GM");

        Assert.Null(await store.TableAsync(id));
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
