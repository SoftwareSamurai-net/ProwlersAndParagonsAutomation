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

    /// <summary>
    /// A Trait with no Source recorded raises nothing at all, and that is the rule rather
    /// than a gap in the checks. Ch.2 p.15 gives Abilities and Talents a default — Innate
    /// and Trained — so silence means "on its default". A Power has no default, which is
    /// why <c>POWER_WITHOUT_SOURCE</c> exists and no Trait equivalent does.
    /// </summary>
    [Fact]
    public void ATraitWithNoSourceIsNotReported()
    {
        var sheet = LegalSheet(_f);
        sheet.AbilityRanks["might"]    = 8;
        sheet.TalentRanks["academics"] = 6;

        var result = _f.Validator.Validate(sheet);

        Assert.DoesNotContain(result.Issues, i =>
            i.Message.Contains("Might", StringComparison.Ordinal) &&
            i.Message.Contains("Source", StringComparison.Ordinal));
        Assert.False(Has(result, "UNKNOWN_TRAIT_SOURCE"));
    }

    /// <summary>
    /// A Source that is not one of the six is an error on a Trait exactly as it is on a
    /// Power. It cannot be rendered and it is not something a player could have chosen, so
    /// it means a hand-edited or stale saved character rather than an unfinished one.
    /// </summary>
    [Fact]
    public void AnUnknownSourceOnATraitIsAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.AbilitySources["might"]    = "cosmic";
        sheet.TalentSources["academics"] = "cosmic";

        var result = _f.Validator.Validate(sheet);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count(e => e.Code == "UNKNOWN_SOURCE"));
        Assert.Contains(result.Errors, e =>
            e.Message.Contains("Ability 'Might'", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e =>
            e.Message.Contains("Talent 'Academics'", StringComparison.Ordinal));
    }

    /// <summary>
    /// A Source recorded against a Trait that does not exist is reported rather than
    /// silently ignored — the grouping cannot print it, so it would otherwise be a
    /// selection the player made and the sheet never mentions again.
    /// </summary>
    [Fact]
    public void ASourceAgainstAnUnknownTraitIsAnError()
    {
        var sheet = LegalSheet(_f);
        sheet.AbilitySources["telepathy"] = "tech";

        var result = _f.Validator.Validate(sheet);

        Assert.True(Has(result, "UNKNOWN_TRAIT_SOURCE"));
        Assert.False(result.IsValid);
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
