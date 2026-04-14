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
