namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Calculates Hero Point costs for all character traits.
/// All methods are pure — they read from sheet and rules but never mutate.
/// </summary>
public sealed class CostCalculator
{
    private readonly RulesRepository _rules;

    public CostCalculator(RulesRepository rules) => _rules = rules;

    // ── Abilities ────────────────────────────────────────────────────────

    /// <summary>Total HP spent on abilities (1 HP per rank each).</summary>
    public int AbilityCost(CharacterSheet sheet)
    {
        return sheet.AbilityRanks.Values.Sum();
    }

    // ── Talents ──────────────────────────────────────────────────────────

    /// <summary>Total HP spent on talents (1 HP per rank each).</summary>
    public int TalentCost(CharacterSheet sheet)
    {
        return sheet.TalentRanks.Values.Sum();
    }

    // ── Package ──────────────────────────────────────────────────────────

    /// <summary>
    /// Flat HP cost of the selected optional package (Civilian/Hero/Superhero), or 0 if none.
    /// Package costs are independent of any ability/talent ranks bought on top.
    /// </summary>
    public int PackageCost(CharacterSheet sheet)
    {
        if (sheet.SelectedPackageId is null) return 0;
        var pkg = _rules.CreationRules.OptionalPackages
                        .FirstOrDefault(p => p.Id == sheet.SelectedPackageId);
        return pkg?.Cost ?? 0;
    }

    // ── Powers ───────────────────────────────────────────────────────────

    /// <summary>
    /// HP cost for a single selected power including its pros and cons.
    ///
    /// Formula:
    ///   1. Determine per-rank multiplier (normally 1.0; halved to 0.5 if Overkill or Weak con applied)
    ///   2. base = ⌈purchasedRanks × multiplier⌉
    ///   3. Add flat pro costs (or variant-key pro costs)
    ///   4. Add flat con discounts (negative values reduce cost)
    ///   5. Clamp to minimum 1
    ///
    /// Throws if the power has cost_type "special" with a null CostPerRank and no
    /// special-case handling is defined — this signals a data gap to address.
    /// </summary>
    public int PowerCost(SelectedPower selection)
    {
        var power = _rules.GetPower(selection.PowerId)
                    ?? throw new InvalidOperationException($"Unknown power id '{selection.PowerId}'.");

        // Detect special cons that halve the per-rank cost (Overkill / Weak)
        var hasHalfCostCon = selection.Cons.Any(c => c.Id is "overkill" or "weak");
        var perRankMultiplier = hasHalfCostCon ? 0.5 : (power.CostPerRank ?? throw new InvalidOperationException(
            $"Power '{power.Id}' has null cost_per_rank with unhandled cost_type '{power.CostType}'. " +
            "Extend CostCalculator to handle this case."));

        var baseCost = (int)Math.Ceiling(selection.PurchasedRanks * perRankMultiplier);

        var prosTotal = selection.Pros.Sum(p => ResolveProCost(p));
        var consTotal = selection.Cons.Sum(c => ResolveConCost(c));

        // consTotal values are negative; summing them reduces the cost
        return Math.Max(1, baseCost + prosTotal + consTotal);
    }

    /// <summary>Total HP spent on all selected powers.</summary>
    public int TotalPowersCost(CharacterSheet sheet)
    {
        return sheet.SelectedPowers.Sum(PowerCost);
    }

    // ── Perks ─────────────────────────────────────────────────────────────

    /// <summary>
    /// HP cost for a single selected perk.
    /// flat perks cost their fixed Cost value.
    /// per_unit perks cost CostPerUnit × Units.
    /// </summary>
    public int PerkCost(SelectedPerk selection)
    {
        var perk = _rules.GetPerk(selection.PerkId)
                   ?? throw new InvalidOperationException($"Unknown perk id '{selection.PerkId}'.");

        return perk.CostType switch
        {
            "flat"     => perk.Cost ?? 0,
            "per_unit" => (perk.CostPerUnit ?? 1) * selection.Units,
            _          => throw new InvalidOperationException(
                              $"Unknown cost_type '{perk.CostType}' on perk '{perk.Id}'.")
        };
    }

    /// <summary>Total HP spent on all selected perks.</summary>
    public int TotalPerksCost(CharacterSheet sheet) =>
        sheet.Perks.Sum(PerkCost);

    // ── Total ────────────────────────────────────────────────────────────

    /// <summary>
    /// Grand total HP spend: package + abilities + talents + powers.
    /// This is compared against the tier's HeroPoints budget by CharacterValidator.
    /// </summary>
    public int TotalCost(CharacterSheet sheet)
    {
        return PackageCost(sheet)
             + AbilityCost(sheet)
             + TalentCost(sheet)
             + TotalPowersCost(sheet)
             + TotalPerksCost(sheet);
    }

    // ── Private ──────────────────────────────────────────────────────────

    private int ResolveProCost(SelectedProCon selectedPro)
    {
        var pro = _rules.GetPro(selectedPro.Id)
                  ?? throw new InvalidOperationException($"Unknown pro id '{selectedPro.Id}'.");

        if (pro.CostModifier.HasValue)
            return pro.CostModifier.Value;

        if (pro.CostModifierRange is not null && selectedPro.VariantKey is not null)
        {
            if (pro.CostModifierRange.TryGetValue(selectedPro.VariantKey, out var variantCost))
                return variantCost;

            throw new InvalidOperationException(
                $"Pro '{pro.Id}' has no variant key '{selectedPro.VariantKey}'. " +
                $"Valid keys: {string.Join(", ", pro.CostModifierRange.Keys)}");
        }

        throw new InvalidOperationException(
            $"Pro '{pro.Id}' has no resolvable cost. " +
            "Either CostModifier must be set or CostModifierRange + VariantKey must both be provided.");
    }

    private int ResolveConCost(SelectedProCon selectedCon)
    {
        // Overkill and Weak are handled at the per-rank level above; their flat value is 0
        if (selectedCon.Id is "overkill" or "weak") return 0;

        var con = _rules.GetCon(selectedCon.Id)
                  ?? throw new InvalidOperationException($"Unknown con id '{selectedCon.Id}'.");

        if (con.CostModifier.HasValue)
            return con.CostModifier.Value; // already negative in data

        if (con.CostModifierRange is not null && selectedCon.VariantKey is not null)
        {
            if (con.CostModifierRange.TryGetValue(selectedCon.VariantKey, out var variantCost))
                return variantCost;

            throw new InvalidOperationException(
                $"Con '{con.Id}' has no variant key '{selectedCon.VariantKey}'. " +
                $"Valid keys: {string.Join(", ", con.CostModifierRange.Keys)}");
        }

        throw new InvalidOperationException(
            $"Con '{con.Id}' has no resolvable cost. " +
            "Either CostModifier must be set or CostModifierRange + VariantKey must both be provided.");
    }
}
