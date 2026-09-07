using System.Globalization;
using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>A figure a finding carries reaches the screen, or it was carried for nobody.</b>
///
/// <para><c>CampaignFinding</c>'s own remarks are a promise: the ids, the ranks and the prices are
/// fields rather than words in the sentence <em>because</em> "a screen renders this looks both up
/// and says them". The screen is <c>Campaigns.razor</c>'s <c>Ranks</c>, a switch whose last arm is
/// <c>_ =&gt; ""</c> — so a pair with no arm of its own is not a shorter message, it is that promise
/// silently broken, and the switch says nothing about which pairs it has forgotten.</para>
///
/// <para><b>Which is exactly what shipped.</b> <c>CAMPAIGN_IMMORTALITY_COST_MISMATCH</c> and
/// <c>IMMORTALITY_COST_WITHOUT_CAMPAIGN</c> both carry a price, both fell through to the empty arm,
/// and a reader was told their character "is charged a different price for Immortality" with
/// neither number anywhere on the page. Nothing was red: no test in either project had ever
/// asserted that <c>Ranks</c> prints at all, so the two arms that <em>were</em> written — the tier
/// and the cap — were as unguarded as the two that were not.</para>
///
/// <para><b>So this drives the page and reads the record, rather than listing arms.</b> Each
/// scenario below provokes one finding through <c>CampaignJoin.Inspect</c>, renders
/// <c>/campaign</c>, and then asks the finding itself — by reflection over its properties — which
/// figures it is carrying, requiring every one of them on screen. A seventh finding with a new
/// field costs nothing to add and fails here until somebody says it out loud.</para>
///
/// <para><b>It reads the finding's own paragraph and not the page.</b> The first version of this
/// asserted the figures were somewhere in the markup and went green against the unfixed page,
/// because the read-only house-rules panel below prints "Immortality costs 11 HP" for its own
/// reasons and 11 was the number being looked for. A guard satisfied by a different element saying
/// something else is the denylist failure one shape over, so the paragraph is located by the
/// finding's own sentence and the figures are required inside it.</para>
///
/// <para><b>What it cannot do</b>, stated because <c>CLAUDE.md</c> requires it: it has no opinion
/// about how a figure is worded once it is there, so an arm printing the two prices the wrong way
/// round satisfies it. That is what the per-finding assertions beside each scenario are for.</para>
/// </summary>
public sealed class CampaignFindingFiguresTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string CharacterId = "c_1111111111111111111111";

    /// <summary>
    /// One finding, provoked and drawn.
    ///
    /// <para><b>The GM's own account holds the character on purpose.</b> A campaign's payload is
    /// scoped to the account that owns it, so a member's page resolves no campaign at all and
    /// every mismatch finding is preceded by <c>UNKNOWN_CAMPAIGN</c> — see
    /// <c>CampaignHouseRuleTests</c> for that state and what the member is shown instead. A GM
    /// looking at their own character is the one reader who reaches these, and is therefore the
    /// one this has to be true for.</para>
    /// </summary>
    private static async Task<(CampaignFinding Finding, string Said)> Drawn(
        Campaign campaign, Action<CharacterSheet> build)
    {
        await using var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(campaign.Id, campaign.Name, StoredCampaign.Write(campaign));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(CharacterId);

        build(ctx.Session.Sheet);

        var store = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var resolved = await store.ForAsync(ctx.Session.Sheet);
        var finding = CampaignJoin.Inspect(ctx.Session.Sheet, resolved);

        Assert.True(finding is not null,
            "This scenario provoked no finding at all, so whatever it asserts below is about "
            + "nothing. Check the sheet it builds against CampaignJoin.Inspect's order.");

        var page = ctx.Render<Campaigns>();

        // **The finding's own paragraph, not the page.** Everything below is about what this
        // sentence says, and the panel underneath it prints figures of its own.
        var said = page.FindAll("p")
            .Select(e => e.TextContent)
            .SingleOrDefault(t => t.Contains(finding!.Message, StringComparison.Ordinal));

        Assert.True(said is not null,
            $"'{finding!.Code}' was computed and no element on /campaign carries its sentence, so "
            + "it reaches no reader at all — the fault CampaignJoin.Inspect already shipped once. "
            + "Look for the `Finding` block at the head of \"Games you are in\".");

        return (finding, said!);
    }

    /// <summary>
    /// The figures this finding is carrying, by the record's own properties — every one that is a
    /// value rather than the code or the sentence, and is set.
    /// </summary>
    private static IReadOnlyList<(string Property, string Value)> Figures(CampaignFinding finding) =>
        [.. typeof(CampaignFinding)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not (nameof(CampaignFinding.Code) or nameof(CampaignFinding.Message)))
            .Select(p => (p.Name, Value: p.GetValue(finding)))
            .Where(e => e.Value is not null)
            .Select(e => (e.Name, Convert.ToString(e.Value, CultureInfo.InvariantCulture)!))];

    private static void AssertEveryFigureIsOnScreen(
        string expectedCode, CampaignFinding finding, string said)
    {
        Assert.Equal(expectedCode, finding.Code);

        var carried = Figures(finding);

        // The control on this test's own instrument. A finding carrying nothing satisfies the
        // loop below completely, which is the shape three of this repository's four historical
        // guard faults had — so a scenario that has stopped setting its fields fails here.
        Assert.True(carried.Count > 0,
            $"{expectedCode} came back carrying no figure at all, so the loop below asserts "
            + "nothing. Either CampaignJoin.Inspect stopped setting the fields, or this scenario "
            + "no longer provokes the finding it names.");

        var missing = carried
            .Where(f => !said.Contains(f.Value, StringComparison.Ordinal))
            .Select(f => $"{f.Property} = {f.Value}")
            .ToList();

        Assert.True(missing.Count == 0,
            $"{expectedCode} carries these figures and the page prints none of them, so a reader "
            + "is told two things disagree and not what either one is. Add an arm to "
            + "Campaigns.razor's `Ranks` — its last arm is `_ => \"\"`, which is why a forgotten "
            + $"pair is silent rather than red:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", missing));
    }

    /// <summary>The tier pair, which had an arm and no test.</summary>
    [Fact]
    public async Task ATierMismatchNamesBothTiers()
    {
        var (finding, said) = await Drawn(
            new Campaign(CampaignId, "Pinnacle City", "street_level", null, false),
            sheet =>
            {
                sheet.CampaignId = CampaignId;
                sheet.SelectedTierId = "legendary";
            });

        Assert.Equal("CAMPAIGN_TIER_MISMATCH", finding.Code);

        // The ids are carried and the *names* are printed — the finding's own rule, and the reason
        // the figures loop cannot be used for this one.
        Assert.Contains("Built to Legendary", said, StringComparison.Ordinal);
        Assert.Contains("played at Street Level", said, StringComparison.Ordinal);
        Assert.DoesNotContain("street_level", said, StringComparison.Ordinal);
    }

    /// <summary>The Trait Cap pair, which had an arm and no test.</summary>
    [Fact]
    public async Task ATraitCapMismatchNamesBothCaps()
    {
        var (finding, said) = await Drawn(
            new Campaign(CampaignId, "Pinnacle City", "standard", 7, false),
            sheet =>
            {
                sheet.CampaignId = CampaignId;
                sheet.SelectedTierId = "standard";
                sheet.TraitCapRank = 5;
            });

        AssertEveryFigureIsOnScreen("CAMPAIGN_TRAIT_CAP_MISMATCH", finding, said);

        Assert.Contains("Built to a Trait Cap of 5d; the game caps at 7d.", said,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The price pair, which had no arm at all.</b> A table charging 7 against a character built
    /// at 11 is two numbers a GM can act on; "a different price" is not.
    /// </summary>
    [Fact]
    public async Task AnImmortalityPriceMismatchNamesBothPrices()
    {
        var (finding, said) = await Drawn(
            new Campaign(CampaignId, "Pinnacle City", "standard", null, false, null, 7),
            sheet =>
            {
                sheet.CampaignId = CampaignId;
                sheet.SelectedTierId = "standard";
                sheet.ImmortalityCost = 11;
            });

        AssertEveryFigureIsOnScreen("CAMPAIGN_IMMORTALITY_COST_MISMATCH", finding, said);

        // The character's price first and the game's second, because the sentence above it says
        // the character's is the one its Hero Points were counted against — the two numbers the
        // wrong way round would satisfy the loop and tell a GM the opposite of what is true.
        Assert.Contains("Charged 11 HP for Immortality; the game charges 7 HP.", said,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The orphaned price, which had no arm either.</b> One figure rather than a pair: there is
    /// no game to name a second one, and the reader still has to be told which number to put back.
    /// </summary>
    [Fact]
    public async Task AHousePriceOnACharacterInNoGameNamesThePrice()
    {
        var (finding, said) = await Drawn(
            new Campaign(CampaignId, "Pinnacle City", "standard", null, false),
            sheet =>
            {
                sheet.CampaignId = null;
                sheet.SelectedTierId = "standard";
                sheet.ImmortalityCost = 11;
            });

        AssertEveryFigureIsOnScreen("IMMORTALITY_COST_WITHOUT_CAMPAIGN", finding, said);

        Assert.Contains("Charged 11 HP for Immortality.", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The empty copy, which arrived carrying a figure and with no scenario here.</b>
    ///
    /// <para>This class's own remarks say a new finding with a new field "costs nothing to add and
    /// fails here until somebody says it out loud", and
    /// <c>CAMPAIGN_HOUSE_RULES_NOT_COPIED</c> was added carrying a price without one — the guard
    /// built for exactly this was not extended, and the price reached the screen only because a
    /// different test happened to assert the sentence.</para>
    ///
    /// <para><b>Two figures and no character figure beside either</b>, which is the shape unique to
    /// this finding: the game has decided a cap and a price and the character carries neither, so
    /// both numbers are the table's. A game that sets both must print both — an arm matching on the
    /// price alone would answer with half of it, and a reader told they are "measured by the book"
    /// with no ceiling named cannot act on it.</para>
    /// </summary>
    [Fact]
    public async Task AnEmptyCopyNamesTheCapAndThePriceTheGameHasSet()
    {
        var (finding, said) = await Drawn(
            new Campaign(CampaignId, "Pinnacle City", "standard", 6, false, null, 12),
            sheet =>
            {
                sheet.CampaignId = CampaignId;
                sheet.SelectedTierId = "standard";
            });

        AssertEveryFigureIsOnScreen("CAMPAIGN_HOUSE_RULES_NOT_COPIED", finding, said);

        Assert.Contains("The game caps at 6d and charges 12 HP for Immortality.", said,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Only the setting that is actually missing is named.</b>
    ///
    /// <para>The character took the cap at the join and the game set a price afterwards. Printing
    /// "the game caps at 6d" here would be a true figure given for a false reason — the cap is not
    /// what was missed — and the reader would go looking for a disagreement that is not there.</para>
    /// </summary>
    [Fact]
    public async Task AnEmptyCopyNamesNoFigureTheCharacterAlreadyTook()
    {
        var (finding, said) = await Drawn(
            new Campaign(CampaignId, "Pinnacle City", "standard", 6, false, null, 12),
            sheet =>
            {
                sheet.CampaignId = CampaignId;
                sheet.SelectedTierId = "standard";
                sheet.TraitCapRank = 6;
            });

        AssertEveryFigureIsOnScreen("CAMPAIGN_HOUSE_RULES_NOT_COPIED", finding, said);

        Assert.Null(finding.CampaignTraitCapRank);
        Assert.Contains("The game charges 12 HP for Immortality.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("caps at", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The one arm where a figure would be an invention, and the mirror image of everything
    /// above.</b>
    ///
    /// <para>A game that has switched an optional rule on and set neither a cap nor a price
    /// produces <c>CAMPAIGN_HOUSE_RULES_NOT_COPIED</c> carrying nothing at all: thirteen switches
    /// are not a figure, which is why the join sentence says the rules came with it and lets the
    /// panel say which. So <c>Ranks</c> falls through to <c>_ =&gt; ""</c> here <em>correctly</em>,
    /// and this is the only scenario in this class that says so — every other one is about an arm
    /// that was missing.</para>
    ///
    /// <para><b>It cannot use the figures loop, and the reason is that loop's own control.</b> A
    /// finding carrying nothing satisfies <c>AssertEveryFigureIsOnScreen</c> completely, which is
    /// exactly what its <c>carried.Count &gt; 0</c> guard exists to catch — so the assertion here
    /// is the other side of it: nothing carried, and the sentence alone on screen. A cap or a
    /// price appearing beside it would be a true number given for a false reason.</para>
    /// </summary>
    [Fact]
    public async Task AnEmptyCopyOfSwitchesAloneCarriesNoFigureAndPrintsNone()
    {
        var (finding, said) = await Drawn(
            new Campaign(CampaignId, "Pinnacle City", "standard", null, false,
                new CampaignTable { FatalDamage = true }),
            sheet =>
            {
                sheet.CampaignId = CampaignId;
                sheet.SelectedTierId = "standard";
            });

        Assert.Equal("CAMPAIGN_HOUSE_RULES_NOT_COPIED", finding.Code);
        Assert.Empty(Figures(finding));

        // The sentence and nothing but the sentence. Asserted as equality rather than as three
        // absences, because `Ranks` borrowing an arm would append something this cannot predict.
        Assert.Equal(finding.Message, said.Trim());
    }
}
