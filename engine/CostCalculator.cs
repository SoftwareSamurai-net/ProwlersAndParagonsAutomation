using ProwlersAndParagonsAutomation.Engine.Models;

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
    /// Ranked Powers (per_rank and its variable form):
    ///   1. Start from the Power's HP per rank. Overkill and Weak each reduce that
    ///      rate by 1 HP per rank, floored at 0.5 — the rulebook's "1 Hero Point per
    ///      rank becomes 1 Hero Point per 2 ranks".
    ///   2. base = ⌈purchasedRanks × rate⌉
    ///   3. Add pro costs and con discounts (cons are stored negative).
    ///   4. Floor the result at the rulebook minimum: no Power costs less than
    ///      1 HP per rank, or 1 HP per 2 ranks once a rate-reducing Con applies.
    ///
    /// Unranked Powers (flat, flat_variable, per_unit) ignore purchased ranks and are
    /// floored at 1 HP, except Specialty, which the rulebook makes free.
    /// </summary>
    public int PowerCost(SelectedPower selection)
    {
        var power = _rules.GetPower(selection.PowerId)
                    ?? throw new InvalidOperationException($"Unknown power id '{selection.PowerId}'.");

        var prosTotal = selection.Pros.Sum(ResolveProCost);
        var consTotal = selection.Cons.Sum(ResolveConCost);

        return power.CostType switch
        {
            "per_rank" or "per_rank_variable" or "special" =>
                RankedCost(power, selection, prosTotal + consTotal),

            "flat" =>
                FlatCost(power.CostFlat ?? throw MissingCost(power, "cost_flat"),
                         prosTotal + consTotal, power.Id),

            "flat_variable" =>
                FlatCost((int)ResolveVariant(power, selection), prosTotal + consTotal, power.Id),

            "per_unit" =>
                FlatCost((power.CostPerUnit ?? throw MissingCost(power, "cost_per_unit")) * selection.Units,
                         prosTotal + consTotal, power.Id),

            _ => throw new InvalidOperationException(
                     $"Unknown cost_type '{power.CostType}' on power '{power.Id}'.")
        };
    }

    /// <summary>
    /// Cost of a Power priced per rank. <paramref name="modifiers"/> is the summed
    /// pro/con total (cons negative).
    /// </summary>
    private int RankedCost(PowerModel power, SelectedPower selection, int modifiers)
    {
        var rate = PerRankRate(power, selection);

        // Overkill and Weak reduce the rate by 1 HP per rank each rather than halving
        // it (Ch.2: "reduces a Power's base cost by 1 Hero Point per rank"). For a
        // 1 HP/rank Power that lands on 1 HP per 2 ranks, which is the rulebook floor.
        var reductions = selection.Cons.Count(c => c.Id is "overkill" or "weak");
        var effectiveRate = Math.Max(0.5, rate - reductions);

        var baseCost = (int)Math.Ceiling(selection.PurchasedRanks * effectiveRate);
        var minimum  = MinimumRankedCost(selection.PurchasedRanks, effectiveRate);

        return Math.Max(minimum, baseCost + modifiers);
    }

    /// <summary>
    /// The rulebook floor for a ranked Power: "No Power can ever cost less than
    /// 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons." The
    /// per-2-ranks form applies once a rate-reducing Con has taken the rate to 0.5.
    /// </summary>
    private static int MinimumRankedCost(int purchasedRanks, double effectiveRate)
    {
        if (purchasedRanks <= 0) return 0;
        return effectiveRate <= 0.5
            ? Math.Max(1, (int)Math.Ceiling(purchasedRanks / 2.0))
            : Math.Max(1, purchasedRanks);
    }

    /// <summary>
    /// HP per rank before Cons. Resolves the variable cost types and the two Powers
    /// whose rate the rulebook defines as Special.
    /// </summary>
    private double PerRankRate(PowerModel power, SelectedPower selection)
    {
        if (power.CostType == "per_rank_variable")
            return ResolveVariant(power, selection);

        if (power.CostType == "special")
        {
            return power.Id switch
            {
                // Boost costs as many HP per rank as the Trait it raises.
                "boost" => BoostRate(selection),

                // Summoning costs 1 HP per rank for every 2d of Minion Threat,
                // rounded up. Units carries the chosen Threat rank.
                "summoning" => Math.Max(1, Math.Ceiling(selection.Units / 2.0)),

                _ => throw new InvalidOperationException(
                         $"Power '{power.Id}' has cost_type 'special' with no handler. " +
                         "Add one to CostCalculator.PerRankRate.")
            };
        }

        return power.CostPerRank ?? throw MissingCost(power, "cost_per_rank");
    }

    /// <summary>
    /// Boost's per-rank cost mirrors the nominated Trait: abilities and talents cost
    /// 1 HP per rank, a Power costs whatever that Power costs per rank.
    /// </summary>
    private double BoostRate(SelectedPower selection)
    {
        var traitId = selection.BaselineTraitId
            ?? throw new InvalidOperationException(
                   "Boost requires BaselineTraitId — its cost per rank matches the Trait it raises.");

        if (_rules.GetAbility(traitId) is not null || _rules.GetTalent(traitId) is not null)
            return 1.0;

        var target = _rules.GetPower(traitId)
            ?? throw new InvalidOperationException(
                   $"Boost names Trait '{traitId}', which is not a known ability, talent or power.");

        return target.CostPerRank
            ?? throw new InvalidOperationException(
                   $"Boost cannot mirror power '{traitId}': it is not priced per rank.");
    }

    private double ResolveVariant(PowerModel power, SelectedPower selection)
    {
        var variants = power.CostVariants
            ?? throw MissingCost(power, "cost_variants");

        var key = selection.CostVariantKey
            ?? throw new InvalidOperationException(
                   $"Power '{power.Id}' has a variable cost and needs a CostVariantKey. " +
                   $"Valid keys: {string.Join(", ", variants.Keys)}");

        if (variants.TryGetValue(key, out var value)) return value;

        throw new InvalidOperationException(
            $"Power '{power.Id}' has no cost variant '{key}'. " +
            $"Valid keys: {string.Join(", ", variants.Keys)}");
    }

    /// <summary>
    /// Cost of a Power bought for a fixed price. Specialty is the one Power the
    /// rulebook prices at 0 HP, so it is not floored at 1.
    /// </summary>
    private static int FlatCost(int cost, int modifiers, string powerId)
    {
        if (powerId == "specialty") return 0;
        return Math.Max(1, cost + modifiers);
    }

    private static InvalidOperationException MissingCost(PowerModel power, string field) =>
        new($"Power '{power.Id}' has cost_type '{power.CostType}' but no {field} value.");

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
