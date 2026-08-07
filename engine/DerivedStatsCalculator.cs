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
    /// Edge = Perception + max(Agility, Intellect), then the three Powers the rulebook
    /// says can affect it (Ch.5):
    /// <list type="bullet">
    ///   <item>Danger Sense <b>replaces</b> Perception in the sum — "use this Power
    ///   instead of Perception when determining your Edge" — it is not added on top.</item>
    ///   <item>Lightning Reflexes adds a flat +6. It has no rank, so nothing scales.</item>
    ///   <item>Super Speed sets Edge to its rank × 3, taken if that beats the total.</item>
    /// </list>
    /// </summary>
    public int CalculateEdge(CharacterSheet sheet)
    {
        var perception = sheet.GetAbilityRank("perception");
        var agility    = sheet.GetAbilityRank("agility");
        var intellect  = sheet.GetAbilityRank("intellect");

        // Danger Sense stands in for Perception when the character has it.
        var dangerSense = sheet.GetPower("danger_sense");
        var perceptual  = dangerSense is not null
            ? GetEffectiveRank(dangerSense, sheet)
            : perception;

        var edge = perceptual + Math.Max(agility, intellect);

        // Lightning Reflexes: flat +6, verified against Ch.2 ("Increase your Edge by 6").
        if (sheet.HasPower("lightning_reflexes"))
            edge += LightningReflexesEdgeBonus;

        // Super Speed: "your Edge equals your Super Speed rank times 3". Treated as a
        // floor rather than an override so it never lowers an already-higher Edge.
        var superSpeed = sheet.GetPower("super_speed");
        if (superSpeed is not null)
            edge = Math.Max(edge, GetEffectiveRank(superSpeed, sheet) * 3);

        return edge;
    }

    /// <summary>Flat Edge bonus granted by Lightning Reflexes (Ch.2).</summary>
    public const int LightningReflexesEdgeBonus = 6;

    /// <summary>Hero Points that buy 1 extra starting Resolve via Determination (Ch.2).</summary>
    public const int DeterminationHpPerResolve = 5;

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
    /// Formula (Chapter 5): the Resolve table runs Trait Cap → 0, Cap-1d → 2, Cap-2d → 4,
    /// which is the arithmetic below.
    ///   base = max(0, (TraitCap - highestRelevantRank) × 2)
    ///   + 1 per <see cref="DeterminationHpPerResolve"/> HP spent on Determination
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

        // Determination has no rank: 5 HP buys 1 extra starting Resolve, and Units
        // holds how many Resolve were bought.
        var determination      = sheet.GetPower("determination");
        var determinationBonus = determination?.Units ?? 0;

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
    /// baseline_equal:          baseline = ability rank
    /// baseline_half:           baseline = ⌈ability rank / 2⌉
    /// baseline_fixed:          baseline = FixedValue (3 for Running)
    /// baseline_greater_of:     baseline = max(ability rank, effective ranks of the named Powers)
    /// baseline_selected_trait: baseline = rank of the Trait nominated on the selection
    /// null prerequisite:       baseline = 0
    /// </summary>
    public int GetBaselineRank(PowerModel power, CharacterSheet sheet, SelectedPower? selection = null)
    {
        var prereq = power.Prerequisite;
        if (prereq is null) return 0;

        return prereq.Relationship switch
        {
            "baseline_equal" => sheet.GetAbilityRank(prereq.Ability!),
            "baseline_half"  => (int)Math.Ceiling(sheet.GetAbilityRank(prereq.Ability!) / 2.0),
            "baseline_fixed" => prereq.FixedValue ?? 0,

            // Strike: the greater of Might and Martial Arts.
            "baseline_greater_of" => prereq.Powers
                .Select(id => sheet.GetPower(id) is { } sp ? GetEffectiveRank(sp, sheet) : 0)
                .Append(prereq.Ability is not null ? sheet.GetAbilityRank(prereq.Ability) : 0)
                .Max(),

            // Boost and Expertise: the player nominates the Trait at purchase. Until
            // they do, the baseline is 0 and the validator reports the gap.
            "baseline_selected_trait" => selection?.BaselineTraitId is { } traitId
                ? GetTraitRank(traitId, sheet)
                : 0,

            _ => throw new InvalidOperationException(
                     $"Unknown prerequisite relationship '{prereq.Relationship}' on power '{power.Id}'.")
        };
    }

    /// <summary>
    /// Rank of any Trait by id — ability, talent or power — for the Powers whose
    /// baseline is whatever Trait the player nominated.
    /// </summary>
    private int GetTraitRank(string traitId, CharacterSheet sheet)
    {
        if (sheet.AbilityRanks.TryGetValue(traitId, out var ability)) return ability;
        if (sheet.TalentRanks.TryGetValue(traitId, out var talent))   return talent;

        if (sheet.GetPower(traitId) is not { } sp) return 0;

        // A nominated Power whose own baseline is player-nominated could point back
        // here and recurse. Only these Powers create that indirection, so refusing to
        // follow one is enough to make the resolution terminate.
        var nominated = _rules.GetPower(traitId);
        if (nominated?.Prerequisite?.Relationship == "baseline_selected_trait") return 0;

        return GetEffectiveRank(sp, sheet);
    }

    /// <summary>
    /// Total effective rank of a selected power: baseline rank + purchased ranks.
    /// Powers the rulebook gives no rank (rank_type "default" or "special") have no
    /// effective rank and return 0.
    /// </summary>
    public int GetEffectiveRank(SelectedPower selected, CharacterSheet sheet)
    {
        var power = _rules.GetPower(selected.PowerId)
                    ?? throw new InvalidOperationException($"Unknown power id '{selected.PowerId}'.");

        if (power.RankType is "default" or "special") return 0;

        return GetBaselineRank(power, sheet, selected) + selected.PurchasedRanks;
    }

    /// <summary>
    /// The rank a Power uses when another Power acts on it — Drain, Nullify, Dispel, Power
    /// Absorption, Power Mimicry.
    ///
    /// <para>For a ranked Power that is simply its effective rank. For one the rulebook
    /// gives no rank, Ch.2 p.15 substitutes a <em>default rank</em> taken from an Ability
    /// chosen by the Power's Source: Toughness for Innate, Super and Tech; Willpower for
    /// Magic, Psychic and Trained.</para>
    ///
    /// <para>Deliberately separate from <see cref="GetEffectiveRank"/>, which still answers
    /// 0 for a rankless Power. The default rank stands in only against other Powers; it is
    /// not the Power's rank, and folding it into the effective rank would feed Edge and
    /// Resolve figures the published Hero sheets contradict.</para>
    ///
    /// <para>Returns 0 for a rankless Power with no Source recorded, since there is then
    /// no Ability to read. The validator reports that gap.</para>
    /// </summary>
    public int GetRankAgainstPowers(SelectedPower selected, CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(sheet);

        var power = _rules.GetPower(selected.PowerId)
                    ?? throw new InvalidOperationException($"Unknown power id '{selected.PowerId}'.");

        if (power.RankType is not ("default" or "special"))
            return GetEffectiveRank(selected, sheet);

        if (selected.SourceId is null) return 0;

        var source = _rules.GetSource(selected.SourceId)
                     ?? throw new InvalidOperationException(
                            $"Power '{selected.PowerId}' names unknown Source '{selected.SourceId}'.");

        return sheet.GetAbilityRank(source.DefaultRankAbility);
    }
}
