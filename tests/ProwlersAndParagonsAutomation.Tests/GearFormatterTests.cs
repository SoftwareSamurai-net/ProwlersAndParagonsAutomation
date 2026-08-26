using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <see cref="GearFormatter"/> renders one gear line for every host — the .txt sheet, the
/// JSON export, the browser's sheet and the GM review — and had no test of its own.
///
/// <para><b>Assert on the exact rendered string, never on a fragment.</b> This repository has
/// already shipped a test-side helper that turned <c>&lt;b&gt;Armor&lt;/b&gt;&lt;span&gt;8d&lt;/span&gt;</c>
/// into "Armor 8d" by stripping tags — the exact string the assertions were looking for,
/// produced by the exact bug they existed to find. There are no tags here, but the same
/// failure has a plain-string spelling: a <c>Contains("Upgraded")</c> assertion would still
/// pass if a missing separator glued two feature names together, or if the cost suffix moved
/// to the wrong side of the parenthesis. Every assertion below is <c>Assert.Equal</c> against
/// the whole line.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class GearFormatterTests
{
    private readonly RulesFixture _f;

    public GearFormatterTests(RulesFixture fixture) => _f = fixture;

    private string Describe(SelectedGear gear) => GearFormatter.Describe(gear, _f.Rules, _f.Costs);

    // ── Plain mundane gear ──────────────────────────────────────────────────

    /// <summary>
    /// Ch.6: mundane gear is free and explicitly untracked, so nearly every item on a
    /// sheet is just its name. This is the negative case every other test in this file is
    /// a positive control for: something with nothing bought for it must print nothing
    /// beyond its name.
    /// </summary>
    [Fact]
    public void PlainMundaneGearPrintsJustItsName()
    {
        var gear = new SelectedGear("Padded costume");

        Assert.Equal("Padded costume", Describe(gear));
    }

    /// <summary>
    /// The early-return guard is <c>{ IsCustomised: false, PairedUnderTwoFisted: false }</c>
    /// — both have to be false to print the bare name. A pair that customises nothing still
    /// carries the label and a real (zero) cost, which is a different branch from the one
    /// above and worth its own test: a guard written as <c>!IsCustomised</c> alone would
    /// pass every other test in this file and only fail here.
    /// </summary>
    [Fact]
    public void APairedButUncustomisedItemStillPrintsTheLabelAndItsCost()
    {
        var gear = new SelectedGear("Twin batons") { PairedUnderTwoFisted = true };

        Assert.Equal("Twin batons (Two-Fisted pair) — 0 HP", Describe(gear));
    }

    // ── Features ─────────────────────────────────────────────────────────────

    [Fact]
    public void AFlatFeaturePrintsItsNameAndCost()
    {
        var gear = new SelectedGear("Silenced pistol")
        {
            Features = [new SelectedGearFeature("silenced")]
        };

        Assert.Equal("Silenced pistol (Silenced) — 1 HP", Describe(gear));
    }

    /// <summary>
    /// The two graded features print the grade the player bought, never the "X / Very X"
    /// option heading — <c>GradeName</c> exists for exactly this. Both grades of both
    /// graded features in the rulebook, so the underscore-splitting logic is exercised on
    /// every real key it will ever see, not just the one with a single word to capitalise.
    /// </summary>
    [Theory]
    [InlineData("accurate", "accurate", "Accurate", 1)]
    [InlineData("accurate", "very_accurate", "Very Accurate", 2)]
    [InlineData("powerful", "powerful", "Powerful", 1)]
    [InlineData("powerful", "very_powerful", "Very Powerful", 2)]
    public void AGradedFeaturePrintsTheGradeBoughtNotTheOptionHeading(
        string featureId, string gradeKey, string expectedLabel, int expectedCost)
    {
        var gear = new SelectedGear("Pistol")
        {
            Features = [new SelectedGearFeature(featureId, gradeKey)]
        };

        Assert.Equal($"Pistol ({expectedLabel}) — {expectedCost} HP", Describe(gear));
    }

    /// <summary>
    /// More than one feature on the same item joins with ", " in selection order — the
    /// separator that a dropped-comma or dropped-space mutation would remove.
    /// </summary>
    [Fact]
    public void MultipleFeaturesJoinWithCommasInSelectionOrder()
    {
        var gear = new SelectedGear("Jo Sticks")
        {
            Features = [new SelectedGearFeature("upgraded"), new SelectedGearFeature("accurate", "very_accurate")]
        };

        Assert.Equal("Jo Sticks (Upgraded, Very Accurate) — 4 HP", Describe(gear));
    }

    // ── Pros, Cons and ordering ─────────────────────────────────────────────

    /// <summary>
    /// Features print first, then Pros, then Cons — the order <see cref="GearFormatter.Describe"/>
    /// builds <c>parts</c> in. The expected string is built from the same rules lookups the
    /// formatter uses, the way <c>GearTests</c> derives its expected costs, so this does not
    /// duplicate the rulebook's Pro/Con names as a second hand-copied source of truth.
    /// </summary>
    [Fact]
    public void FeaturesProsAndConsPrintInThatOrder()
    {
        var proName = _f.Rules.GetPro("penetrating")!.Name;
        var conName = _f.Rules.GetCon("charges")!.Name;
        var expectedCost = 2
            + _f.Rules.GetPro("penetrating")!.CostModifier!.Value
            + _f.Rules.GetCon("charges")!.CostModifierRange!["3_per_scene"];

        var gear = new SelectedGear("Rifle")
        {
            Features = [new SelectedGearFeature("upgraded")],
            Pros     = [new SelectedProCon("penetrating")],
            Cons     = [new SelectedProCon("charges", "3_per_scene")]
        };

        Assert.Equal(
            $"Rifle (Upgraded, {proName}, {conName}) — {Math.Max(0, expectedCost)} HP",
            Describe(gear));
    }

    /// <summary>
    /// Features, Pros, Cons and the Two-Fisted label together — the exact shape the class's
    /// own doc comment claims the formatter produces.
    /// </summary>
    [Fact]
    public void TheDocCommentExampleRendersExactly()
    {
        var gear = new SelectedGear("Jo Sticks")
        {
            Features = [new SelectedGearFeature("upgraded")],
            PairedUnderTwoFisted = true
        };

        Assert.Equal("Jo Sticks (Upgraded, Two-Fisted pair) — 2 HP", Describe(gear));
    }

    /// <summary>
    /// Gear floors at 0 HP rather than a Power's 1 — Ch.6: "regardless of Cons, no piece of
    /// gear can cost less than 0 Hero Points". The line still names every Con that was
    /// bought even though none of them shows up in the price; a formatter that stopped
    /// printing Cons once the floor was reached would silently hide what was bought.
    /// </summary>
    [Fact]
    public void ConsCanFloorTheCostAtZeroAndTheLineStillNamesThem()
    {
        var limitedName = _f.Rules.GetCon("limited")!.Name;
        var burnoutName = _f.Rules.GetCon("burnout")!.Name;

        var gear = new SelectedGear("Cursed blade")
        {
            Features = [new SelectedGearFeature("bonded")],
            Cons     = [new SelectedProCon("limited", "severely_limited"), new SelectedProCon("burnout")]
        };

        Assert.Equal($"Cursed blade (Bonded, {limitedName}, {burnoutName}) — 0 HP", Describe(gear));
    }

    /// <summary>
    /// The Item Con is never credited against a piece of gear (Ch.6: it is a statement of
    /// what gear <em>is</em>, not a discount to claim) — so a feature that would otherwise
    /// cost 1 HP still costs 1 HP once the rendered cost is inspected, not just the number
    /// <c>CostCalculator</c> returns in isolation. This is the same rule <c>GearTests</c>
    /// checks against <c>GearCost</c> directly; here it is checked against what a player
    /// actually reads on the line.
    /// </summary>
    [Fact]
    public void TheRenderedCostDoesNotCreditTheImplicitItemCon()
    {
        var gear = new SelectedGear("Silenced pistol")
        {
            Features = [new SelectedGearFeature("silenced")]
        };

        Assert.Equal("Silenced pistol (Silenced) — 1 HP", Describe(gear));
    }
}
