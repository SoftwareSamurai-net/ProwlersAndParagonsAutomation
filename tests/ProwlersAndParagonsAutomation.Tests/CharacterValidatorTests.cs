using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

[Collection(SharedRules.Name)]
public sealed class CharacterValidatorTests
{
    private readonly RulesFixture _f;

    public CharacterValidatorTests(RulesFixture fixture) => _f = fixture;

    private static CharacterSheet LegalSheet(RulesFixture f)
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = 4;
        sheet.Flaws.Add(new SelectedFlaw(f.Rules.Flaws[0].Id));
        return sheet;
    }

    private static bool Has(ValidationResult r, string code) =>
        r.Issues.Any(i => i.Code == code);

    [Fact]
    public void ALegalSheetHasNoErrors()
    {
        var result = _f.Validator.Validate(LegalSheet(_f));
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void MissingTierIsAnError() =>
        Assert.True(Has(_f.Validator.Validate(new CharacterSheet()), "NO_TIER_SELECTED"));

    [Fact]
    public void OverspendingTheBudgetIsAnError()
    {
        var sheet = LegalSheet(_f);
        // Standard is 125 HP; Elemental Control is 3 HP per rank.
        sheet.SelectedPowers.Add(new SelectedPower("elemental_control", 12));   // 36
        sheet.SelectedPowers.Add(new SelectedPower("time_stop", 12));           // 36
        sheet.SelectedPowers.Add(new SelectedPower("constructs", 12));          // 36
        sheet.SelectedPowers.Add(new SelectedPower("super_speed", 12));         // 36

        Assert.True(Has(_f.Validator.Validate(sheet), "HP_BUDGET_EXCEEDED"));
    }

    [Fact]
    public void ATraitAboveTheCapIsAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.AbilityRanks["agility"] = 13;   // Standard cap is 12d

        Assert.True(Has(_f.Validator.Validate(sheet), "TRAIT_ABOVE_CAP"));
    }

    [Fact]
    public void APowerWhoseBaselinePlusPurchasedExceedsTheCapIsAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.AbilityRanks["agility"] = 10;
        sheet.SelectedPowers.Add(new SelectedPower("evasion", 5));   // 10 + 5 = 15 > 12

        Assert.True(Has(_f.Validator.Validate(sheet), "TRAIT_ABOVE_CAP"));
    }

    [Fact]
    public void BuyingRanksForARanklessPowerIsAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.SelectedPowers.Add(new SelectedPower("adaptation", 3));   // Default Rank

        Assert.True(Has(_f.Validator.Validate(sheet), "POWER_HAS_NO_RANK"));
    }

    [Fact]
    public void ARanklessPowerBoughtWithoutRanksIsFine()
    {
        var sheet = LegalSheet(_f);
        sheet.SelectedPowers.Add(new SelectedPower("adaptation", 0));

        Assert.False(Has(_f.Validator.Validate(sheet), "POWER_HAS_NO_RANK"));
    }

    [Fact]
    public void AVariableCostPowerWithNoVariantChosenIsAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.SelectedPowers.Add(new SelectedPower("stretching", 0));

        Assert.True(Has(_f.Validator.Validate(sheet), "POWER_VARIANT_NOT_CHOSEN"));
    }

    [Fact]
    public void AVariableCostPowerWithAVariantChosenIsFine()
    {
        var sheet = LegalSheet(_f);
        sheet.SelectedPowers.Add(new SelectedPower("stretching", 0) { CostVariantKey = "distant_range" });

        Assert.False(Has(_f.Validator.Validate(sheet), "POWER_VARIANT_NOT_CHOSEN"));
    }

    [Fact]
    public void ANominatedBaselineTraitIsRequired()
    {
        var sheet = LegalSheet(_f);
        sheet.SelectedPowers.Add(new SelectedPower("expertise", 4));

        Assert.True(Has(_f.Validator.Validate(sheet), "POWER_BASELINE_TRAIT_NOT_CHOSEN"));

        var chosen = RulesFixture.StandardSheet();
        chosen.AbilityRanks["might"] = 4;
        chosen.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));
        chosen.SelectedPowers.Add(new SelectedPower("expertise", 4) { BaselineTraitId = "might" });

        Assert.False(Has(_f.Validator.Validate(chosen), "POWER_BASELINE_TRAIT_NOT_CHOSEN"));
    }

    [Fact]
    public void TooFewFlawsIsAnError()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = 4;

        Assert.True(Has(_f.Validator.Validate(sheet), "FLAW_MIN_NOT_MET"));
    }

    [Fact]
    public void AnUnknownFlawIsAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.Flaws.Add(new SelectedFlaw("not_a_real_flaw"));

        Assert.True(Has(_f.Validator.Validate(sheet), "UNKNOWN_FLAW"));
    }

    [Fact]
    public void ConsThatBottomOutTheCostRaiseAWarningNotAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.SelectedPowers.Add(new SelectedPower("blast", 6,
            [], [new SelectedProCon("overkill"), new SelectedProCon("limited", "severely_limited")]));

        var result = _f.Validator.Validate(sheet);

        Assert.True(Has(result, "POWER_COST_AT_MINIMUM"));
        Assert.DoesNotContain(result.Errors, e => e.Code == "POWER_COST_AT_MINIMUM");
    }

    [Fact]
    public void NoPowerRaisesAnUnverifiedMechanicsWarningAnyMore()
    {
        // Every entry is now verified against Ch.2, so this warning should never fire.
        var sheet = LegalSheet(_f);
        foreach (var p in _f.Rules.Powers.Where(p => p.CostType == "per_rank").Take(20))
            sheet.SelectedPowers.Add(new SelectedPower(p.Id, 1));

        Assert.False(Has(_f.Validator.Validate(sheet), "POWER_MECHANICS_UNVERIFIED"));
    }

    [Fact]
    public void TheIconicTierIsFlaggedAsGmDiscretion()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedTierId = "iconic";
        sheet.AbilityRanks["might"] = 4;
        sheet.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));

        var result = _f.Validator.Validate(sheet);

        Assert.True(Has(result, "ICONIC_TIER_OPEN_BUDGET"));
        Assert.True(result.IsValid);   // a warning, not an error
    }
}
