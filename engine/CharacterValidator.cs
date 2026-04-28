namespace ProwlersAndParagonsAutomation.Engine;

public enum ValidationSeverity { Error, Warning }

public record ValidationIssue(ValidationSeverity Severity, string Code, string Message);

public record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Errors =>
        Issues.Where(i => i.Severity == ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Warnings =>
        Issues.Where(i => i.Severity == ValidationSeverity.Warning);
}

/// <summary>
/// Validates a CharacterSheet against all rules constraints.
/// Returns a ValidationResult with any errors and warnings found.
/// </summary>
public sealed class CharacterValidator
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;

    public CharacterValidator(
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived)
    {
        _rules   = rules;
        _costs   = costs;
        _derived = derived;
    }

    public ValidationResult Validate(CharacterSheet sheet)
    {
        var issues = new List<ValidationIssue>();

        CheckTierSelected(sheet, issues);

        var tier = sheet.SelectedTierId is not null
            ? _rules.GetTier(sheet.SelectedTierId)
            : null;

        if (tier is not null)
        {
            CheckHpBudget(sheet, tier, issues);
            CheckTraitCap(sheet, tier, issues);
            CheckIconicTier(tier, issues);
        }

        CheckFlawCount(sheet, issues);
        CheckFlawIds(sheet, issues);
        CheckPowerCosts(sheet, issues);
        CheckNeedsReviewTraits(sheet, issues);
        CheckLightningReflexes(sheet, issues);

        return new ValidationResult(issues);
    }

    // ── Checks ────────────────────────────────────────────────────────────

    private static void CheckTierSelected(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        if (sheet.SelectedTierId is null)
            issues.Add(new(ValidationSeverity.Error, "NO_TIER_SELECTED",
                "No tier has been selected. Choose a tier before validating."));
    }

    private void CheckHpBudget(CharacterSheet sheet, Engine.Models.TierModel tier, List<ValidationIssue> issues)
    {
        var total = _costs.TotalCost(sheet);
        if (total > tier.HeroPoints)
            issues.Add(new(ValidationSeverity.Error, "HP_BUDGET_EXCEEDED",
                $"Character costs {total} HP but the {tier.Name} tier budget is {tier.HeroPoints} HP " +
                $"({total - tier.HeroPoints} HP over)."));
    }

    private void CheckTraitCap(CharacterSheet sheet, Engine.Models.TierModel tier, List<ValidationIssue> issues)
    {
        var cap = tier.TraitCapRank;

        foreach (var (id, rank) in sheet.AbilityRanks)
            if (rank > cap)
                issues.Add(new(ValidationSeverity.Error, "TRAIT_ABOVE_CAP",
                    $"Ability '{id}' has rank {rank}d but the trait cap is {cap}d."));

        foreach (var (id, rank) in sheet.TalentRanks)
            if (rank > cap)
                issues.Add(new(ValidationSeverity.Error, "TRAIT_ABOVE_CAP",
                    $"Talent '{id}' has rank {rank}d but the trait cap is {cap}d."));

        foreach (var sp in sheet.SelectedPowers)
        {
            var effective = _derived.GetEffectiveRank(sp, sheet);
            if (effective > cap)
                issues.Add(new(ValidationSeverity.Error, "TRAIT_ABOVE_CAP",
                    $"Power '{sp.PowerId}' has effective rank {effective}d but the trait cap is {cap}d."));
        }
    }

    private static void CheckIconicTier(Engine.Models.TierModel tier, List<ValidationIssue> issues)
    {
        if (tier.NeedsReview || tier.Id == "iconic")
            issues.Add(new(ValidationSeverity.Warning, "ICONIC_TIER_OPEN_BUDGET",
                $"Tier '{tier.Name}' is marked needs_review. The Hero Point budget and trait cap " +
                "are subject to GM discretion at this power level."));
    }

    private void CheckFlawCount(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var flawRules = _rules.CreationRules.FlawRules;
        var count = sheet.Flaws.Count;

        if (count < flawRules.MinAtCreation)
            issues.Add(new(ValidationSeverity.Error, "FLAW_MIN_NOT_MET",
                $"Characters must have at least {flawRules.MinAtCreation} flaw(s) at creation " +
                $"(currently {count})."));

        if (count > flawRules.MaxAtCreation)
            issues.Add(new(ValidationSeverity.Error, "FLAW_MAX_EXCEEDED",
                $"Characters may have at most {flawRules.MaxAtCreation} flaws at creation " +
                $"(currently {count}). Additional flaws each cost {flawRules.ExtraFlawCostHp} HP."));
    }

    private void CheckFlawIds(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sf in sheet.Flaws)
        {
            if (_rules.GetFlaw(sf.FlawId) is null)
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_FLAW",
                    $"Flaw '{sf.FlawId}' is not defined in flaws.json."));
        }
    }

    private void CheckPowerCosts(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            var cost = _costs.PowerCost(sp);
            // CostCalculator already clamps to 1, but we surface a warning
            // if the raw calculation would have gone below 1 before clamping.
            if (cost == 1)
            {
                // Re-examine: if the power has any cons, it may have been clamped.
                if (sp.Cons.Any())
                    issues.Add(new(ValidationSeverity.Warning, "POWER_COST_AT_MINIMUM",
                        $"Power '{sp.PowerId}' cost was clamped to the 1 HP minimum after applying cons. " +
                        "Verify con selection is intentional."));
            }
        }
    }

    private void CheckNeedsReviewTraits(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            var power = _rules.GetPower(sp.PowerId);
            if (power?.NeedsReview == true)
                issues.Add(new(ValidationSeverity.Warning, "NEEDS_REVIEW_POWER",
                    $"Power '{power.Name}' is flagged needs_review — its rules may not be fully " +
                    "verified against the source PDF. Confirm with GM before play."));
        }
    }

    private static void CheckLightningReflexes(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        if (sheet.HasPower("lightning_reflexes"))
            issues.Add(new(ValidationSeverity.Warning, "LIGHTNING_REFLEXES_UNVERIFIED",
                "Lightning Reflexes Edge bonus is calculated as +2 per purchased rank. " +
                "The rulebook may specify a flat +6 total — verify against PDF before finalising."));
    }
}
