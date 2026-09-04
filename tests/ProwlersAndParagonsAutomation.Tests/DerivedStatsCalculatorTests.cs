using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Edge, Health, Resolve and baseline ranks. Several of these encode rules that were
/// wrong in an earlier version, so they double as regression tests.
/// </summary>
[Collection(SharedRules.Name)]
public sealed class DerivedStatsCalculatorTests
{
    private readonly RulesFixture _f;

    public DerivedStatsCalculatorTests(RulesFixture fixture) => _f = fixture;

    private static CharacterSheet SheetWithAbilities()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["perception"] = 4;
        sheet.AbilityRanks["agility"]    = 6;
        sheet.AbilityRanks["intellect"]  = 3;
        sheet.AbilityRanks["toughness"]  = 5;
        sheet.AbilityRanks["might"]      = 8;
        sheet.AbilityRanks["willpower"]  = 3;
        return sheet;
    }

    // ── Edge ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EdgeIsPerceptionPlusTheGreaterOfAgilityOrIntellect() =>
        Assert.Equal(10, _f.Derived.CalculateEdge(SheetWithAbilities()));   // 4 + max(6, 3)

    [Fact]
    public void LightningReflexesAddsAFlatSix()
    {
        var sheet = SheetWithAbilities();
        sheet.SelectedPowers.Add(new SelectedPower("lightning_reflexes", 0));

        Assert.Equal(16, _f.Derived.CalculateEdge(sheet));
    }

    [Fact]
    public void DangerSenseReplacesPerceptionRatherThanAddingToIt()
    {
        // Regression: an earlier version added Danger Sense on top of Perception, which
        // double-counted it. Ch.2: "use this Power instead of Perception when
        // determining your Edge."
        var sheet = SheetWithAbilities();
        sheet.SelectedPowers.Add(new SelectedPower("danger_sense", 3));   // baseline 4 + 3 = 7

        Assert.Equal(13, _f.Derived.CalculateEdge(sheet));                // 7 + max(6, 3)
        Assert.NotEqual(20, _f.Derived.CalculateEdge(sheet));             // 4 + 6 + 7 if added
    }

    [Fact]
    public void SuperSpeedFloorsEdgeAtThreeTimesItsRank()
    {
        var sheet = SheetWithAbilities();
        sheet.SelectedPowers.Add(new SelectedPower("super_speed", 9));

        Assert.Equal(27, _f.Derived.CalculateEdge(sheet));
    }

    [Fact]
    public void SuperSpeedNeverLowersAnAlreadyHigherEdge()
    {
        var sheet = SheetWithAbilities();
        sheet.SelectedPowers.Add(new SelectedPower("lightning_reflexes", 0));   // Edge 16
        sheet.SelectedPowers.Add(new SelectedPower("super_speed", 2));          // ×3 = 6

        Assert.Equal(16, _f.Derived.CalculateEdge(sheet));
    }

    // ── Health ───────────────────────────────────────────────────────────────

    [Fact]
    public void HealthTakesTheBetterOfMightOrWillpowerPairedWithToughness() =>
        // max(⌈(5+8)/2⌉, ⌈(5+3)/2⌉) = max(7, 4)
        Assert.Equal(7, _f.Derived.CalculateHealth(SheetWithAbilities()));

    [Fact]
    public void HealthRoundsHalvesUp()
    {
        // The rulebook rounds half of an odd number up, globally (the Introduction's Glossary, p.7, "Half").
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["toughness"] = 3;
        sheet.AbilityRanks["might"]     = 2;   // (3+2)/2 = 2.5 → 3
        sheet.AbilityRanks["willpower"] = 1;

        Assert.Equal(3, _f.Derived.CalculateHealth(sheet));
    }

    // ── Resolve ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(12, 0)]   // at the Trait Cap
    [InlineData(11, 2)]
    [InlineData(10, 4)]
    [InlineData(9, 6)]
    public void ResolveFollowsTheRulebookTable(int highestRank, int expected)
    {
        var sheet = RulesFixture.StandardSheet();   // Trait Cap 12d
        sheet.AbilityRanks["might"] = highestRank;

        Assert.Equal(expected, _f.Derived.CalculateResolve(sheet));
    }

    [Fact]
    public void DeterminationGrantsOneResolvePerFiveHeroPoints()
    {
        // Regression: an earlier version granted 1 Resolve per purchased rank on a
        // 1 HP/rank power, five times what the rulebook allows.
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = 12;                                            // base 0
        sheet.SelectedPowers.Add(new SelectedPower("determination", 0) { Units = 3 });

        Assert.Equal(3, _f.Derived.CalculateResolve(sheet));
        Assert.Equal(15, _f.Costs.PowerCost(sheet.SelectedPowers[0]));
        Assert.Equal(5, DerivedStatsCalculator.DeterminationHpPerResolve);
    }

    [Fact]
    public void TalentsDoNotAffectResolve()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"]     = 4;
        sheet.TalentRanks["technology"] = 12;   // at the cap, but excluded

        Assert.Equal(16, _f.Derived.CalculateResolve(sheet));   // (12 − 4) × 2
    }

    [Theory]
    // Every Power the rulebook names as Resolve-exempt (Ch.5).
    [InlineData("danger_sense")]
    [InlineData("detection")]
    [InlineData("expertise")]
    [InlineData("flight")]
    [InlineData("leaping")]
    [InlineData("running")]
    [InlineData("super_senses_acute")]
    [InlineData("swimming")]
    [InlineData("swing_line")]
    [InlineData("teleportation")]
    [InlineData("tunneling")]
    public void PowersTheRulebookExemptsDoNotAffectResolve(string powerId)
    {
        var power = _f.Rules.GetPower(powerId);
        Assert.NotNull(power);
        Assert.False(DerivedStatsCalculator.ResolveAffectedByPower(power));
    }

    [Theory]
    [InlineData("blast")]
    [InlineData("armor")]
    [InlineData("mind_control")]
    [InlineData("super_speed")]   // explicitly true: it attacks and defends
    public void CombatCapablePowersDoAffectResolve(string powerId)
    {
        var power = _f.Rules.GetPower(powerId);
        Assert.NotNull(power);
        Assert.True(DerivedStatsCalculator.ResolveAffectedByPower(power));
    }

    [Fact]
    public void ConditionAndPlotHookFlawsEachGrantOneResolve()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = 12;   // base 0

        var flawIds = _f.Rules.Flaws
            .Where(fl => fl.FlawType is "condition" or "plot_hook" or "plot_hook_and_condition")
            .Take(2)
            .Select(fl => fl.Id)
            .ToList();

        Assert.Equal(2, flawIds.Count);
        foreach (var id in flawIds) sheet.Flaws.Add(new SelectedFlaw(id));

        Assert.Equal(2, _f.Derived.CalculateResolve(sheet));
    }

    // ── Baseline and effective ranks ─────────────────────────────────────────

    [Theory]
    [InlineData("armor", 3)]           // ⌈Toughness 5 / 2⌉
    [InlineData("leaping", 4)]         // ⌈Might 8 / 2⌉
    [InlineData("evasion", 6)]         // Agility 6
    [InlineData("danger_sense", 4)]    // Perception 4
    [InlineData("psi_screen", 3)]      // Willpower 3
    [InlineData("resistance", 5)]      // Toughness 5
    [InlineData("martial_arts", 8)]    // Might 8
    [InlineData("running", 3)]         // flat 3d
    public void BaselineRanksComeFromTheNamedTrait(string powerId, int expected)
    {
        var power = _f.Rules.GetPower(powerId);
        Assert.NotNull(power);
        Assert.Equal(expected, _f.Derived.GetBaselineRank(power, SheetWithAbilities()));
    }

    [Fact]
    public void StrikeTakesTheGreaterOfMightAndMartialArts()
    {
        var sheet = SheetWithAbilities();          // Might 8
        var strike = _f.Rules.GetPower("strike")!;

        Assert.Equal(8, _f.Derived.GetBaselineRank(strike, sheet));

        sheet.SelectedPowers.Add(new SelectedPower("martial_arts", 3));   // 8 + 3 = 11
        Assert.Equal(11, _f.Derived.GetBaselineRank(strike, sheet));
    }

    [Fact]
    public void NominatedBaselineTraitsResolveAgainstAbilitiesTalentsAndPowers()
    {
        var sheet = SheetWithAbilities();
        sheet.TalentRanks["technology"] = 7;
        sheet.SelectedPowers.Add(new SelectedPower("blast", 9));

        var expertise = _f.Rules.GetPower("expertise")!;
        var boost     = _f.Rules.GetPower("boost")!;

        Assert.Equal(7, _f.Derived.GetBaselineRank(expertise, sheet,
            new SelectedPower("expertise", 0) { BaselineTraitId = "technology" }));

        Assert.Equal(8, _f.Derived.GetBaselineRank(boost, sheet,
            new SelectedPower("boost", 0) { BaselineTraitId = "might" }));

        Assert.Equal(9, _f.Derived.GetBaselineRank(boost, sheet,
            new SelectedPower("boost", 0) { BaselineTraitId = "blast" }));
    }

    [Fact]
    public void ANominatedTraitThatIsNotSetGivesNoBaseline()
    {
        var boost = _f.Rules.GetPower("boost")!;
        Assert.Equal(0, _f.Derived.GetBaselineRank(boost, SheetWithAbilities()));
    }

    /// <summary>
    /// <b>Where a baseline comes from is a different question from how big it is</b>, and a
    /// roster report needs both: a 9d Power bought outright and a 9d Power sitting on a 9d
    /// Ability are the same number and different characters.
    ///
    /// <para>Each case is asserted with the rank beside it, so an answer that has stopped
    /// naming the Trait cannot pass by naming nothing on a Power whose baseline is 0 anyway.</para>
    /// </summary>
    [Theory]
    [InlineData("armor", "toughness")]        // baseline_half
    [InlineData("evasion", "agility")]        // baseline_equal
    [InlineData("danger_sense", "perception")]
    [InlineData("martial_arts", "might")]
    public void ABaselineNamesTheTraitItIsReadFrom(string powerId, string traitId)
    {
        var power = _f.Rules.GetPower(powerId)!;

        Assert.Equal([traitId], DerivedStatsCalculator.BaselineTraitIds(power));
        Assert.True(_f.Derived.GetBaselineRank(power, SheetWithAbilities()) > 0,
            "The positive control failed: this Power derives no baseline on the fixture sheet, "
            + "so naming its Trait would prove nothing.");
    }

    /// <summary>
    /// Strike reads from an Ability <em>and</em> a Power, and both are named — dropping either
    /// would leave a reader unable to tell which of the two put the rank there.
    /// </summary>
    [Fact]
    public void AGreaterOfBaselineNamesEveryTraitItChoosesBetween()
    {
        var strike = _f.Rules.GetPower("strike")!;

        Assert.Equal(["might", "martial_arts"], DerivedStatsCalculator.BaselineTraitIds(strike));
    }

    /// <summary>
    /// A nominated baseline names whatever was nominated, and names nothing when nothing was —
    /// which is the state the validator reports, not a Trait to invent.
    /// </summary>
    [Fact]
    public void ANominatedBaselineNamesTheNominationOrNothing()
    {
        var boost = _f.Rules.GetPower("boost")!;

        Assert.Equal(["might"], DerivedStatsCalculator.BaselineTraitIds(
            boost, new SelectedPower("boost", 0) { BaselineTraitId = "might" }));

        Assert.Empty(DerivedStatsCalculator.BaselineTraitIds(boost));
    }

    /// <summary>
    /// A baseline printed in the Power's own entry is read from no Trait at all, and Running
    /// is the one that has one — so an empty answer here is a fact rather than a gap.
    /// </summary>
    [Fact]
    public void AFixedBaselineNamesNoTrait()
    {
        var running = _f.Rules.GetPower("running")!;

        Assert.Equal(3, _f.Derived.GetBaselineRank(running, SheetWithAbilities()));
        Assert.Empty(DerivedStatsCalculator.BaselineTraitIds(running));
    }

    /// <summary>
    /// Every Power the rules ship, so a relationship added to the data without a case here
    /// fails at the line rather than through a roster report that lost a column.
    /// </summary>
    [Fact]
    public void EveryPowerWithAPrerequisiteCanSayWhereItsBaselineComesFrom()
    {
        var withPrerequisite = _f.Rules.Powers.Where(p => p.Prerequisite is not null).ToList();

        Assert.True(withPrerequisite.Count >= 27,
            $"Only {withPrerequisite.Count} Powers carry a prerequisite; the rules ship 27 or more. "
            + "Fix the loading rather than this number.");

        Assert.All(withPrerequisite, power =>
            DerivedStatsCalculator.BaselineTraitIds(
                power, new SelectedPower(power.Id, 0) { BaselineTraitId = "might" }));
    }

    [Fact]
    public void EffectiveRankIsBaselinePlusPurchased()
    {
        var sheet = SheetWithAbilities();   // Toughness 5 → Armor baseline 3
        Assert.Equal(7, _f.Derived.GetEffectiveRank(new SelectedPower("armor", 4), sheet));
    }

    [Theory]
    [InlineData("adaptation")]       // Default Rank
    [InlineData("lightning_reflexes")]
    [InlineData("determination")]    // Special
    [InlineData("inanimate")]
    public void RanklessPowersHaveNoEffectiveRank(string powerId) =>
        Assert.Equal(0, _f.Derived.GetEffectiveRank(new SelectedPower(powerId, 0), SheetWithAbilities()));
}
