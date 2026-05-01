using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Computes derived statistics (Edge, Health) and power baseline ranks
/// from a CharacterSheet and the rules data.
/// </summary>
public sealed class DerivedStatsCalculator
{
    private readonly RulesRepository _rules;

    public DerivedStatsCalculator(RulesRepository rules) => _rules = rules;

    // ── Edge ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Edge = Perception + max(Agility, Intellect) + Danger Sense bonus + Lightning Reflexes bonus.
    ///
    /// Danger Sense: if present, its effective rank (Perception + purchased ranks) is added.
    /// Lightning Reflexes: +2 per purchased rank. NOTE: the rulebook may specify a flat +6 total —
    /// this is flagged needs_review in powers.json. The validator will emit a warning.
    /// </summary>
    public int CalculateEdge(CharacterSheet sheet)
    {
        var perception = sheet.GetAbilityRank("perception");
        var agility    = sheet.GetAbilityRank("agility");
        var intellect  = sheet.GetAbilityRank("intellect");

        var edge = perception + Math.Max(agility, intellect);

        // Danger Sense: effective rank = Perception + purchased ranks
        var dangerSensePower = sheet.GetPower("danger_sense");
        if (dangerSensePower is not null)
        {
            var dangerSenseEffective = GetEffectiveRank(dangerSensePower, sheet);
            edge += dangerSenseEffective;
        }

        // Lightning Reflexes: +2 per purchased rank (see needs_review note)
        var lightningReflexesPower = sheet.GetPower("lightning_reflexes");
        if (lightningReflexesPower is not null)
        {
            edge += lightningReflexesPower.PurchasedRanks * 2;
        }

        return edge;
    }

    // ── Health ───────────────────────────────────────────────────────────

    /// <summary>
    /// Health = max(⌈(Toughness + Might) / 2⌉, ⌈(Toughness + Willpower) / 2⌉)
    /// </summary>
    public int CalculateHealth(CharacterSheet sheet)
    {
        var toughness  = sheet.GetAbilityRank("toughness");
        var might      = sheet.GetAbilityRank("might");
        var willpower  = sheet.GetAbilityRank("willpower");

        var mightHealth    = (int)Math.Ceiling((toughness + might)    / 2.0);
        var willpowerHealth = (int)Math.Ceiling((toughness + willpower) / 2.0);

        return Math.Max(mightHealth, willpowerHealth);
    }

    // ── Resolve ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Starting Resolve at the beginning of an issue.
    ///
    /// Formula (Chapter 5):
    ///   base = max(0, (TraitCap - highestRelevantRank) × 2)
    ///   + Determination purchased ranks (1 per rank, needs_review)
    ///   + 1 per Condition or Plot Hook flaw
    ///
    /// Relevant ranks: all Ability ranks; Power effective ranks where the power
    /// affects Resolve. Talents excluded. Movement and Sensory category powers
    /// excluded by default; explicit overrides via PowerModel.AffectsResolve.
    /// </summary>
    public int CalculateResolve(CharacterSheet sheet)
    {
        if (sheet.SelectedTierId is null) return 0;
        var tier = _rules.GetTier(sheet.SelectedTierId);
        if (tier is null) return 0;

        var traitCap = tier.TraitCapRank;

        var highestAbility = sheet.AbilityRanks.Values.DefaultIfEmpty(0).Max();

        var highestPower = sheet.SelectedPowers
            .Select(sp =>
            {
                var power = _rules.GetPower(sp.PowerId);
                if (power is null || !ResolveAffectedByPower(power)) return 0;
                return GetEffectiveRank(sp, sheet);
            })
            .DefaultIfEmpty(0)
            .Max();

        var highestRelevant = Math.Max(highestAbility, highestPower);
        var baseResolve     = Math.Max(0, (traitCap - highestRelevant) * 2);

        // Determination: +1 per purchased rank (needs_review — verify ratio)
        var determination      = sheet.GetPower("determination");
        var determinationBonus = determination?.PurchasedRanks ?? 0;

        // Condition / Plot Hook flaws: +1 each at start of every issue
        var flawBonus = sheet.Flaws.Count(sf =>
        {
            var flaw = _rules.GetFlaw(sf.FlawId);
            return flaw?.FlawType is "condition" or "plot_hook" or "plot_hook_and_condition";
        });

        return baseResolve + determinationBonus + flawBonus;
    }

    /// <summary>
    /// Returns whether a power's effective rank contributes to the Resolve calculation.
    /// Movement and Sensory powers are excluded by default (cannot be used for
    /// attack, defense, or to affect other characters/objects — Chapter 5).
    /// An explicit AffectsResolve value on the model overrides the category default.
    /// </summary>
    public static bool ResolveAffectedByPower(PowerModel power)
    {
        if (power.AffectsResolve.HasValue) return power.AffectsResolve.Value;

        return power.Category switch
        {
            "Movement" => false,
            "Sensory"  => false,
            _          => true
        };
    }

    // ── Baseline rank ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the free baseline rank contributed by a character's ability scores
    /// for the given power's prerequisite relationship.
    ///
    /// baseline_equal:  baseline = ability rank
    /// baseline_half:   baseline = ⌈ability rank / 2⌉
    /// baseline_fixed:  baseline = FixedValue (e.g. 3 for Running)
    /// null prerequisite: baseline = 0
    /// </summary>
    public int GetBaselineRank(PowerModel power, CharacterSheet sheet)
    {
        var prereq = power.Prerequisite;
        if (prereq is null) return 0;

        return prereq.Relationship switch
        {
            "baseline_equal" => sheet.GetAbilityRank(prereq.Ability!),
            "baseline_half"  => (int)Math.Ceiling(sheet.GetAbilityRank(prereq.Ability!) / 2.0),
            "baseline_fixed" => prereq.FixedValue ?? 0,
            _ => throw new InvalidOperationException(
                     $"Unknown prerequisite relationship '{prereq.Relationship}' on power '{power.Id}'.")
        };
    }

    /// <summary>
    /// Returns the total effective rank of a selected power:
    /// baseline rank (from ability) + purchased ranks.
    /// </summary>
    public int GetEffectiveRank(SelectedPower selected, CharacterSheet sheet)
    {
        var power = _rules.GetPower(selected.PowerId)
                    ?? throw new InvalidOperationException($"Unknown power id '{selected.PowerId}'.");
        return GetBaselineRank(power, sheet) + selected.PurchasedRanks;
    }
}
