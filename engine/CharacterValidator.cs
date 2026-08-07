using ProwlersAndParagonsAutomation.Engine.Models;

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

        // Selections must be checked before anything that prices a power: a power
        // missing its cost variant or nominated Trait cannot be costed at all, and
        // asking for its cost would throw instead of reporting the gap.
        CheckPowerRanks(sheet, issues);
        var selectionsResolvable = CheckPowerSelections(sheet, issues);

        // Same reason, for gear: an unknown feature or a graded one with no grade cannot
        // be priced, so the gap has to be reported before anything asks for a total.
        var gearResolvable = CheckGear(sheet, issues);

        if (tier is not null)
        {
            if (selectionsResolvable && gearResolvable) CheckHpBudget(sheet, tier, issues);
            CheckTraitCap(sheet, tier, issues);
            CheckIconicTier(tier, issues);
        }

        CheckFlawCount(sheet, issues);
        CheckFlawIds(sheet, issues);
        if (selectionsResolvable) CheckPowerCosts(sheet, issues);
        CheckUnverifiedPowers(sheet, issues);
        CheckSources(sheet, issues);

        return new ValidationResult(issues);
    }

    // ── Checks ────────────────────────────────────────────────────────────

    private static void CheckTierSelected(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        if (sheet.SelectedTierId is null)
            issues.Add(new(ValidationSeverity.Error, "NO_TIER_SELECTED",
                "No tier has been selected. Choose a tier before validating."));
    }

    private void CheckHpBudget(CharacterSheet sheet, TierModel tier, List<ValidationIssue> issues)
    {
        var total = _costs.TotalCost(sheet);
        if (total > tier.HeroPoints)
            issues.Add(new(ValidationSeverity.Error, "HP_BUDGET_EXCEEDED",
                $"Character costs {total} HP but the {tier.Name} tier budget is {tier.HeroPoints} HP " +
                $"({total - tier.HeroPoints} HP over)."));
    }

    private void CheckTraitCap(CharacterSheet sheet, TierModel tier, List<ValidationIssue> issues)
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

    private static void CheckIconicTier(TierModel tier, List<ValidationIssue> issues)
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

    /// <summary>
    /// Custom gear (Ch.6, p.92). Mundane gear is free and untracked, so an uncustomised
    /// item is never an issue; these only bite once Hero Points are involved.
    ///
    /// <para>Returns false if any item cannot be priced at all, which stops the caller
    /// asking for a total that would throw.</para>
    /// </summary>
    private bool CheckGear(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var resolvable = true;

        foreach (var gear in sheet.Gear)
        {
            var itemResolvable = true;

            foreach (var f in gear.Features)
            {
                var feature = _rules.GetGearFeature(f.FeatureId);
                if (feature is null)
                {
                    issues.Add(new(ValidationSeverity.Error, "UNKNOWN_GEAR_FEATURE",
                        $"Gear '{gear.Name}' has feature '{f.FeatureId}', " +
                        "which is not defined in gear_features.json."));
                    itemResolvable = false;
                    continue;
                }

                if (feature.CostType != "flat" && f.GradeKey is null)
                {
                    issues.Add(new(ValidationSeverity.Error, "GEAR_FEATURE_NEEDS_GRADE",
                        $"'{feature.Name}' on '{gear.Name}' is priced by grade and none was chosen. " +
                        $"Valid grades: {string.Join(", ", feature.CostRange?.Keys ?? [])}."));
                    itemResolvable = false;
                }
            }

            resolvable &= itemResolvable;

            // "Regardless of Cons, no piece of gear can cost less than 0 Hero Points."
            // Cons past that point buy the character nothing, so say so rather than
            // letting a player think they are still saving.
            if (itemResolvable && gear.Cons.Count > 0 && _costs.GearCost(gear) == 0)
                issues.Add(new(ValidationSeverity.Warning, "GEAR_COST_AT_MINIMUM",
                    $"Gear '{gear.Name}' is already free after its cons. " +
                    "No piece of gear can cost less than 0 HP, so further cons will not help."));

            // Two-Fisted is what allows a matched pair to be customised for one price.
            if (gear.PairedUnderTwoFisted && !sheet.HasPower("two_fisted"))
                issues.Add(new(ValidationSeverity.Error, "TWO_FISTED_PAIR_WITHOUT_POWER",
                    $"Gear '{gear.Name}' is recorded as a Two-Fisted pair, but the character " +
                    "does not have the Two-Fisted Power that allows paying once for both."));
        }

        return resolvable;
    }

    /// <summary>
    /// Sources (Ch.2, p.15). A Source costs nothing and changes no rank, so a missing one
    /// is never an error — but a Power the rulebook gives no rank needs its Source to know
    /// which Ability stands in when another Power acts on it, so that gap is worth saying.
    /// </summary>
    private void CheckSources(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            if (sp.SourceId is not null && _rules.GetSource(sp.SourceId) is null)
            {
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_SOURCE",
                    $"Power '{sp.PowerId}' names Source '{sp.SourceId}', " +
                    "which is not defined in sources.json."));
                continue;
            }

            var power = _rules.GetPower(sp.PowerId);
            if (power is null || sp.SourceId is not null) continue;

            if (power.RankType is "default" or "special")
                issues.Add(new(ValidationSeverity.Warning, "RANKLESS_POWER_WITHOUT_SOURCE",
                    $"Power '{power.Name}' has no rank of its own, so it needs a Source to " +
                    "supply the default rank used when another Power acts on it " +
                    "(Drain, Nullify, Dispel, Power Absorption, Power Mimicry)."));
            else
                issues.Add(new(ValidationSeverity.Warning, "POWER_WITHOUT_SOURCE",
                    $"Power '{power.Name}' has no Source recorded. A published sheet groups " +
                    "Powers under Source headings, so the sheet will list it as unsourced."));
        }
    }

    private void CheckPowerCosts(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            if (!sp.Cons.Any()) continue;

            var power = _rules.GetPower(sp.PowerId);
            if (power is null) continue;

            // Report when Cons have driven the cost down to the rulebook floor, since
            // any further Cons on this Power buy the character nothing.
            var cost    = _costs.PowerCost(sp);
            var atFloor = power.CostType is "flat" or "flat_variable" or "per_unit"
                ? cost == 1
                : cost <= Math.Max(1, (int)Math.Ceiling(sp.PurchasedRanks / 2.0));

            if (atFloor)
                issues.Add(new(ValidationSeverity.Warning, "POWER_COST_AT_MINIMUM",
                    $"Power '{power.Name}' has reached the minimum cost the rulebook allows " +
                    $"({cost} HP) after its cons. Further cons will not reduce it."));
        }
    }

    /// <summary>
    /// Powers the rulebook gives no rank cannot have ranks bought for them.
    /// </summary>
    private void CheckPowerRanks(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            var power = _rules.GetPower(sp.PowerId);
            if (power is null) continue;

            if (power.MaxRank == 0 && sp.PurchasedRanks > 0)
                issues.Add(new(ValidationSeverity.Error, "POWER_HAS_NO_RANK",
                    $"Power '{power.Name}' has no purchasable rank ({power.RankType} rank, " +
                    $"{power.CostType} cost) but {sp.PurchasedRanks} rank(s) were bought."));
        }
    }

    /// <summary>
    /// Powers whose cost or baseline depends on a player choice are unresolvable until
    /// that choice is recorded on the selection.
    /// </summary>
    /// <returns>
    /// False when at least one power cannot be priced yet, so cost-dependent checks
    /// must be skipped this pass.
    /// </returns>
    private bool CheckPowerSelections(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var resolvable = true;

        foreach (var sp in sheet.SelectedPowers)
        {
            var power = _rules.GetPower(sp.PowerId);
            if (power is null)
            {
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_POWER",
                    $"Power '{sp.PowerId}' is not defined in powers.json."));
                resolvable = false;
                continue;
            }

            if (power.CostType is "per_rank_variable" or "flat_variable" && sp.CostVariantKey is null)
            {
                issues.Add(new(ValidationSeverity.Error, "POWER_VARIANT_NOT_CHOSEN",
                    $"Power '{power.Name}' has a variable cost and needs a variant chosen " +
                    $"({string.Join(", ", power.CostVariants?.Keys ?? [])})."));
                resolvable = false;
            }

            if (power.Prerequisite?.Relationship == "baseline_selected_trait" && sp.BaselineTraitId is null)
            {
                issues.Add(new(ValidationSeverity.Error, "POWER_BASELINE_TRAIT_NOT_CHOSEN",
                    $"Power '{power.Name}' derives its baseline rank from a Trait the player " +
                    "nominates, but none has been recorded."));

                // Boost also takes its cost per rank from that Trait.
                if (power.CostType == "special") resolvable = false;
            }
        }

        return resolvable;
    }

    /// <summary>
    /// Reports powers whose mechanics have not been checked against the rulebook, and
    /// separately notes that descriptions are project paraphrase rather than rules text.
    /// </summary>
    private void CheckUnverifiedPowers(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            var power = _rules.GetPower(sp.PowerId);
            if (power is null) continue;

            if (power.NeedsReview)
                issues.Add(new(ValidationSeverity.Warning, "POWER_MECHANICS_UNVERIFIED",
                    $"Power '{power.Name}' has unverified mechanics (verified: " +
                    $"{(power.VerifiedFields.Count > 0 ? string.Join(", ", power.VerifiedFields) : "nothing")}). " +
                    "Confirm with GM before play."));
        }

        var unverifiedText = sheet.SelectedPowers
            .Select(sp => _rules.GetPower(sp.PowerId))
            .OfType<PowerModel>()
            .Where(p => !p.DescriptionVerified)
            .Select(p => p.Name)
            .Distinct()
            .ToList();

        if (unverifiedText.Count > 0)
            issues.Add(new(ValidationSeverity.Warning, "POWER_DESCRIPTION_UNVERIFIED",
                $"{unverifiedText.Count} power description(s) have not been checked against the " +
                "rulebook entry. Costs and ranks are unaffected, but read the cited page before " +
                $"relying on the wording: {string.Join(", ", unverifiedText)}."));
    }
}
