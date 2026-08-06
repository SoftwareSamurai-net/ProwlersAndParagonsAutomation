using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// HP costs for every cost_type the rulebook uses. Expected values are read straight
/// off the Power entries in Ch.2 — if one of these fails, either powers.json or
/// CostCalculator has drifted from the book.
/// </summary>
[Collection(SharedRules.Name)]
public sealed class CostCalculatorTests
{
    private readonly RulesFixture _f;

    public CostCalculatorTests(RulesFixture fixture) => _f = fixture;

    // ── per_rank ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("blast", 8, 8)]                 // 1 HP per rank
    [InlineData("telekinesis", 6, 12)]          // 2 HP per rank
    [InlineData("elemental_control", 5, 15)]    // 3 HP per rank
    [InlineData("super_speed", 4, 12)]          // 3 HP per rank
    [InlineData("swimming", 7, 4)]              // 1 HP per 2 ranks, ⌈7/2⌉
    [InlineData("evasion", 5, 3)]               // 1 HP per 2 ranks, ⌈5/2⌉
    public void PerRankPowersCostRanksTimesRate(string powerId, int ranks, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(new SelectedPower(powerId, ranks)));

    // ── flat ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("animal_empathy", 1)]
    [InlineData("lightning_reflexes", 3)]
    [InlineData("darkness", 6)]
    [InlineData("invisibility", 9)]
    [InlineData("adaptation", 12)]
    [InlineData("buff", 12)]
    [InlineData("duplication", 25)]
    public void FlatPowersCostTheirPrintedPrice(string powerId, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(new SelectedPower(powerId, 0)));

    [Fact]
    public void SpecialtyIsFree() =>
        Assert.Equal(0, _f.Costs.PowerCost(new SelectedPower("specialty", 0)));

    [Fact]
    public void FlatPowersIgnorePurchasedRanks()
    {
        // The validator rejects buying ranks on a rankless Power; the cost must not
        // change even if a caller sets them anyway.
        Assert.Equal(12, _f.Costs.PowerCost(new SelectedPower("adaptation", 5)));
    }

    // ── per_unit ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("immunity", 3, 9)]          // 3 HP per immunity
    [InlineData("determination", 2, 10)]    // 5 HP per Resolve
    [InlineData("alternate_form", 3, 12)]   // 4 HP per power level; Standard is the 3rd
    public void PerUnitPowersCostRatePerUnit(string powerId, int units, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(new SelectedPower(powerId, 0) { Units = units }));

    // ── variable ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("close_range", 1)]
    [InlineData("distant_range", 3)]
    [InlineData("extreme_range", 6)]
    public void StretchingCostsByReach(string variant, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(
            new SelectedPower("stretching", 0) { CostVariantKey = variant }));

    [Theory]
    [InlineData("narrow", 30)]   // 3 HP per rank
    [InlineData("broad", 50)]    // 5 HP per rank
    public void OmniPowerCostsByBreadth(string variant, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(
            new SelectedPower("omni_power", 10) { CostVariantKey = variant }));

    [Theory]
    [InlineData("standard", 6)]  // 1 HP per rank
    [InlineData("kinetic", 18)]  // 3 HP per rank
    public void EnergyAbsorptionCostsMoreForKinetic(string variant, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(
            new SelectedPower("energy_absorption", 6) { CostVariantKey = variant }));

    [Fact]
    public void VariableCostPowerWithoutAVariantThrows() =>
        Assert.Throws<InvalidOperationException>(
            () => _f.Costs.PowerCost(new SelectedPower("omni_power", 4)));

    // ── special ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2, 8, 8)]    // Threat 2d → 1 HP per rank
    [InlineData(6, 8, 24)]   // Threat 6d → 3 HP per rank
    [InlineData(5, 8, 24)]   // Threat 5d → ⌈5/2⌉ = 3 HP per rank (halves round up)
    public void SummoningCostsPerRankPerTwoThreat(int threat, int ranks, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(
            new SelectedPower("summoning", ranks) { Units = threat }));

    [Theory]
    [InlineData("might", 4)]           // ability: 1 HP per rank
    [InlineData("technology", 4)]      // talent: 1 HP per rank
    [InlineData("telekinesis", 8)]     // power at 2 HP per rank
    [InlineData("elemental_control", 12)] // power at 3 HP per rank
    public void BoostMirrorsTheRateOfTheTraitItRaises(string traitId, int expected) =>
        Assert.Equal(expected, _f.Costs.PowerCost(
            new SelectedPower("boost", 4) { BaselineTraitId = traitId }));

    [Fact]
    public void BoostWithoutANominatedTraitThrows() =>
        Assert.Throws<InvalidOperationException>(
            () => _f.Costs.PowerCost(new SelectedPower("boost", 4)));

    // ── Overkill / Weak ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("blast", 8, 4)]                  // 1 → 0.5 per rank
    [InlineData("telekinesis", 6, 6)]            // 2 → 1 per rank
    [InlineData("elemental_control", 5, 10)]     // 3 → 2 per rank
    public void OverkillReducesTheRateByOnePerRank(string powerId, int ranks, int expected)
    {
        List<SelectedProCon> overkill = [new("overkill")];
        Assert.Equal(expected, _f.Costs.PowerCost(new SelectedPower(powerId, ranks, [], overkill)));
    }

    [Fact]
    public void OverkillIsNotAHalvingOfTheRate()
    {
        // Regression: a previous version multiplied the rate by 0.5 regardless of the
        // Power's own rate, pricing 3 HP/rank Elemental Control at 0.5 HP/rank.
        List<SelectedProCon> overkill = [new("overkill")];
        var halved = (int)Math.Ceiling(5 * 0.5);
        var actual = _f.Costs.PowerCost(new SelectedPower("elemental_control", 5, [], overkill));

        Assert.NotEqual(halved, actual);
        Assert.Equal(10, actual);
    }

    [Fact]
    public void OverkillAndWeakTogetherStopAtTheRulebookFloor()
    {
        // Two rate reductions on a 1 HP/rank Power cannot go below 1 HP per 2 ranks.
        List<SelectedProCon> both = [new("overkill"), new("weak")];
        Assert.Equal(4, _f.Costs.PowerCost(new SelectedPower("blast", 8, [], both)));
    }

    [Fact]
    public void ConsCannotTakeARankedPowerBelowOneHpPerTwoRanks()
    {
        // "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks)
        // regardless of its Cons." 8 ranks therefore floor at 4 HP, not 1 HP.
        List<SelectedProCon> heavyCons =
        [
            new("overkill"),
            new("limited", "severely_limited"),
            new("charges", "1_per_scene")
        ];
        var cost = _f.Costs.PowerCost(new SelectedPower("blast", 8, [], heavyCons));

        Assert.Equal(4, cost);
    }

    [Fact]
    public void ConsCannotTakeAFlatPowerBelowOneHp()
    {
        List<SelectedProCon> heavyCons =
        [
            new("limited", "severely_limited"),
            new("shutdown", "rarely_works")
        ];
        var cost = _f.Costs.PowerCost(new SelectedPower("animal_empathy", 0, [], heavyCons));

        Assert.Equal(1, cost);
    }

    // ── Abilities, talents, totals ───────────────────────────────────────────

    [Fact]
    public void AbilitiesAndTalentsCostOneHpPerRank()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"]     = 5;
        sheet.AbilityRanks["agility"]   = 3;
        sheet.TalentRanks["technology"] = 4;

        Assert.Equal(8, _f.Costs.AbilityCost(sheet));
        Assert.Equal(4, _f.Costs.TalentCost(sheet));
    }

    [Fact]
    public void TotalCostSumsEveryCategory()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = 5;             // 5
        sheet.TalentRanks["charm"]  = 2;             // 2
        sheet.SelectedPowers.Add(new SelectedPower("blast", 6));       // 6
        sheet.SelectedPowers.Add(new SelectedPower("adaptation", 0));  // 12

        Assert.Equal(25, _f.Costs.TotalCost(sheet));
    }
}
