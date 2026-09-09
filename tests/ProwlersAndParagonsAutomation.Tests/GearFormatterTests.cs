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

    // ── Chapter 6's catalogue ────────────────────────────────────────────────

    /// <summary>
    /// <b>A catalogue row prints the columns the book prints beside its name, and no price.</b>
    /// p.91 says mundane gear is not bought, so a "— 0 HP" beside a battle axe would be a charge
    /// the book does not make.
    /// </summary>
    [Fact]
    public void ACatalogueWeaponPrintsItsBonusAndFeaturesAndNoPrice()
    {
        var axe = new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe"
        };

        Assert.Equal("Battle Axe +3 (Two-Handed)", Describe(axe));
    }

    /// <summary>
    /// <b>The printed "(s)" travels with the bonus</b>, because the two are one column: it says
    /// the weapon knocks down rather than wounds, which is a fact about the three dice.
    /// </summary>
    [Fact]
    public void ASubdualWeaponPrintsTheMarkBesideItsBonus()
    {
        var baton = new SelectedGear("Baton") { CatalogueId = GearCatalogue.WeaponPrefix + "baton" };

        Assert.Equal("Baton +1(s) (Thrown)", Describe(baton));
    }

    /// <summary>
    /// <b>A shield's line says what the defensive die is for.</b> The weapons table prints only
    /// the half you get by swinging it; p.88's rule is the half it is mostly carried for, and the
    /// figure is read off the shield rule rather than written into this formatter.
    /// </summary>
    [Fact]
    public void AShieldPrintsTheDieItAddsToEveryDefence()
    {
        var shield = new SelectedGear("Shield") { CatalogueId = GearCatalogue.WeaponPrefix + "shield" };

        Assert.Equal("Shield +1(s) (Shield +1d defence)", Describe(shield));
    }

    /// <summary>
    /// <b>A zero bonus is printed and a missing one is not</b>, because the two say different
    /// things: p.88's Armor table prints 0 for Leather, and one weapons row has no bonus column at
    /// all. Suppressing the zero would make the two look alike on a sheet.
    /// </summary>
    [Fact]
    public void AZeroBonusIsPrintedWhereTheBookPrintsOne()
    {
        var leather = new SelectedGear("Leather")
        {
            CatalogueId = GearCatalogue.ArmorPrefix + "ancient_leather"
        };

        Assert.Equal("Leather +0", Describe(leather));

        // And the row above it, which does carry a figure and a feature.
        var plate = new SelectedGear("Plate") { CatalogueId = GearCatalogue.ArmorPrefix + "ancient_plate" };

        Assert.Equal("Plate +2 (Rigid)", Describe(plate));
    }

    /// <summary>
    /// <b>An item's bonus prints what the page says it is for.</b>
    ///
    /// <para>Two of p.91's thirty-six carry a figure that applies to one kind of roll and nothing
    /// else — the Crowbar's four dice are for forcing things open, the Climbing Claws' two are for
    /// a rock face — and the page prints the qualifier in the same breath as the number. A sheet
    /// reading <c>Crowbar +4</c> beside <c>Battle Axe +3</c> states a general bonus the book does
    /// not grant, and the row has carried <c>BonusAppliesTo</c> for exactly this since the
    /// catalogue was built. The other thirty-four have no figure at all and print none.</para>
    /// </summary>
    [Fact]
    public void AnItemsBonusPrintsWhatThePageSaysItIsFor()
    {
        var crowbar = new SelectedGear("Crowbar") { CatalogueId = GearCatalogue.ItemPrefix + "crowbar" };

        Assert.Equal("Crowbar +4 to Might rolls made to force things open or apart", Describe(crowbar));

        var claws = new SelectedGear("Climbing Claws")
        {
            CatalogueId = GearCatalogue.ItemPrefix + "climbing_claws"
        };

        Assert.Equal(
            "Climbing Claws +2 to challenge rolls made to climb natural surfaces", Describe(claws));

        // The control, and it is the half that says the clause is the row's and not a suffix this
        // formatter adds to everything: a weapon's bonus needs no qualifying and gets none.
        var axe = new SelectedGear("Battle Axe") { CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe" };

        Assert.Equal("Battle Axe +3 (Two-Handed)", Describe(axe));

        // And an item with no figure prints neither.
        var dice = new SelectedGear("Polyhedral Dice")
        {
            CatalogueId = GearCatalogue.ItemPrefix + "polyhedral_dice"
        };

        Assert.Equal("Polyhedral Dice", Describe(dice));
    }

    /// <summary>
    /// <b>A catalogue row that is also customised prints both, and then the price.</b> The row's
    /// own features come first because the book prints them beside the name; what the character
    /// bought follows.
    /// </summary>
    [Fact]
    public void ACustomisedCatalogueRowPrintsThePrintedFeaturesThenTheBoughtOnes()
    {
        var axe = new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe",
            Features = [new("upgraded")]
        };

        Assert.Equal("Battle Axe +3 (Two-Handed, Upgraded) — 2 HP", Describe(axe));
    }

    /// <summary>
    /// <b>An id that resolves to nothing prints the bare name.</b> Inventing a bonus for an unknown
    /// row would be the repair this engine does not make; <c>CharacterValidator</c> reports it as
    /// <c>UNKNOWN_GEAR_CATALOGUE_ROW</c> instead.
    /// </summary>
    [Fact]
    public void AnUnknownRowPrintsTheNameAndNoFigures()
    {
        var axe = new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battel_axe"
        };

        Assert.Equal("Battle Axe", Describe(axe));
    }

    // ── And the same line, in the sheet the exports print ────────────────────

    /// <summary>
    /// <b>The <c>.txt</c> sheet prints the catalogue line, and the worn Armor rank in the derived
    /// block.</b>
    ///
    /// <para><b>Two blocks and not one, deliberately.</b> The bonus and the features are facts about
    /// the object and belong beside its name; the Armor rank is a fact about the <em>wearer</em> —
    /// their Toughness under the Gear Limit, plus the suit's bonus — and belongs where the other
    /// figures computed from the whole character are. This is the guard on that split: the two
    /// halves are asserted in the two places they are supposed to be.</para>
    /// </summary>
    [Fact]
    public void TheTextSheetPrintsTheRowsColumnsAndTheWornArmorRank()
    {
        var sheet = _f.LegalSheet();
        sheet.AbilityRanks["toughness"] = 10;
        sheet.Gear.Add(new SelectedGear("Plate") { CatalogueId = GearCatalogue.ArmorPrefix + "ancient_plate" });
        sheet.Gear.Add(new SelectedGear("Battle Axe") { CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe" });

        var text = CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), new DateTime(2026, 9, 9));

        var gear = Section(text, "GEAR");
        var derived = Section(text, "DERIVED STATS");

        Assert.Contains("Plate +2 (Rigid)", gear, StringComparison.Ordinal);
        Assert.Contains("Battle Axe +3 (Two-Handed)", gear, StringComparison.Ordinal);

        // Neither block borrows the other's business.
        Assert.DoesNotContain("Armor", gear, StringComparison.Ordinal);
        Assert.Contains("Armor:   8d (worn, under the Gear Limit)", derived, StringComparison.Ordinal);

        // And a character in no armour has no such line, rather than one reading zero.
        var bare = _f.LegalSheet();

        Assert.DoesNotContain("Armor:", Section(
            CharacterSheetRenderer.RenderText(
                bare, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(bare), new DateTime(2026, 9, 9)),
            "DERIVED STATS"),
            StringComparison.Ordinal);
    }

    /// <summary>One block of the text sheet, from its heading to the next blank-line break.</summary>
    private static string Section(string text, string heading)
    {
        var lines = text.Split('\n');
        var start = Array.FindIndex(lines, l => l.Contains(heading, StringComparison.Ordinal));

        Assert.True(start >= 0, $"the sheet has no {heading} block at all, so this test reads nothing");

        var length = Array.FindIndex(lines, start + 1, string.IsNullOrWhiteSpace) - start;

        return string.Join('\n', lines.Skip(start).Take(length < 0 ? lines.Length - start : length));
    }
}
