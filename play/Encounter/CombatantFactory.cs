using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// The one place a <see cref="CharacterSheet"/> is read.
///
/// <para><b>One place, on purpose.</b> Everything after this works on
/// <see cref="Combatant"/> snapshots, so an encounter cannot reach a character even by accident and
/// <c>Encounter.Step</c> is a pure function of state and intent. It also puts the whole of the
/// coupling between the two engines in one file: the character engine answers Edge, Health, Resolve
/// and every effective rank, and this asks it. Nothing here recomputes a figure
/// <see cref="DerivedStatsCalculator"/> already answers — a second spelling of one of those is how
/// two surfaces end up disagreeing.</para>
///
/// <para><b>The caller says what kind of combatant this is.</b> It is an argument and not something
/// read off the sheet: the flag on a character is presentation, no rules code may see it, and the
/// same sheet is a Villain in one game and a Foe in another. <see cref="Combatant.Kind"/> is the
/// only thing that decides whether Health is halved and whether Resolve is held.</para>
/// </summary>
public static class CombatantFactory
{
    /// <summary>The six Abilities, in the character rules' own ids.</summary>
    private static readonly string[] AbilityIds =
        ["might", "agility", "toughness", "intellect", "perception", "willpower"];

    /// <summary>
    /// A snapshot of <paramref name="sheet"/> as a combatant of <paramref name="kind"/>.
    ///
    /// <para><b>The sheet is not touched.</b> Every figure is asked for; nothing is written.</para>
    /// </summary>
    /// <param name="sheet">The character, read and not modified.</param>
    /// <param name="rules">The character rules, for the Trait catalogue and the tier.</param>
    /// <param name="derived">The character engine's derived statistics.</param>
    /// <param name="play">The play rules, for the Health halving and the defence lists.</param>
    /// <param name="kind">Which rung of p.73's ladder this character occupies in this fight.</param>
    /// <param name="id">The id the encounter refers to them by; their name by default.</param>
    /// <param name="side">
    /// Whose side they are on. <b>The caller says, and nothing derives it</b> — see
    /// <see cref="Combatant.Side"/>: p.73 names a fight between Heroes, and a Villain's Minions
    /// stand beside their Villain against a Foe who has changed sides. Defaults to the arrangement
    /// every fight the book works through happens to have.
    /// </param>
    public static Combatant From(
        CharacterSheet sheet,
        RulesRepository rules,
        DerivedStatsCalculator derived,
        PlayRulesRepository play,
        CombatantKind kind,
        string? id = null,
        string? side = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(derived);
        ArgumentNullException.ThrowIfNull(play);

        if (kind == CombatantKind.MinionGroup)
        {
            throw new ArgumentException(
                "A group of Minions is not built from a character sheet — Ch.4 p.77 gives them one "
                + "characteristic, Threat, and no sheet to derive it from. Use Combatant.Minions.",
                nameof(kind));
        }

        var name = string.IsNullOrWhiteSpace(sheet.Name) ? "Unnamed" : sheet.Name;
        var traits = TraitRanks(sheet, rules, derived);

        var health = Health(sheet, derived, play, kind);
        var edge = derived.CalculateEdge(sheet);
        var defences = DefencesAvailableTo(traits, play);

        return kind switch
        {
            CombatantKind.Hero => Combatant.Hero(
                id ?? name, name, edge, health, derived.CalculateResolve(sheet), traits, defences,
                side ?? Combatant.HeroSide),
            CombatantKind.Villain => Combatant.Villain(
                id ?? name, name, edge, health, traits, defences, side ?? Combatant.OpposingSide),
            CombatantKind.Foe => Combatant.Foe(
                id ?? name, name, edge, health, traits, defences, side ?? Combatant.OpposingSide),
            CombatantKind.Extra => Combatant.Extra(
                id ?? name, name, edge, health, traits, defences, side ?? Combatant.OpposingSide),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of combatant.")
        };
    }

    /// <summary>
    /// Health as the character engine computes it, halved for a Foe.
    ///
    /// <para><b>Both halves are read rather than typed.</b> That a Foe halves it is
    /// <c>combat.json</c>'s <c>health.foes_halve_the_result</c>; that the half goes up is the
    /// Glossary's book-wide rule in <c>play_meta.json</c>, which p.75 never restates —
    /// <c>docs/guide/play-rules.md</c> records it as one of Chapter 4's three readings. So a Foe
    /// built from a Health of 5 has 3, not 2.</para>
    /// </summary>
    private static int Health(
        CharacterSheet sheet, DerivedStatsCalculator derived, PlayRulesRepository play, CombatantKind kind)
    {
        var health = derived.CalculateHealth(sheet);

        var rule = play.GetCombat("health").Health!;
        if (kind != CombatantKind.Foe || !rule.FoesHalveTheResult) return health;

        var direction = play.GetMeta("half_rounds_up").Rounding!.Direction;

        return direction switch
        {
            "up" => (int)Math.Ceiling(health / 2.0),
            "down" => health / 2,
            var other => throw new InvalidOperationException(
                $"play_meta.json's rounding direction is '{other}', which is neither up nor down.")
        };
    }

    /// <summary>
    /// Every Trait the encounter may roll: the six Abilities, the twelve Talents, and each Power at
    /// the effective rank the character engine gives it.
    ///
    /// <para>A Power appears under its own id, so an intent naming <c>armor</c> or <c>blast</c>
    /// finds it. Where a character has bought the same Power twice the larger rank wins, which is
    /// the only reading that does not silently lower a figure the sheet prints.</para>
    /// </summary>
    private static Dictionary<string, int> TraitRanks(
        CharacterSheet sheet, RulesRepository rules, DerivedStatsCalculator derived)
    {
        var traits = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var ability in AbilityIds) traits[ability] = sheet.GetAbilityRank(ability);
        foreach (var talent in rules.Talents) traits[talent.Id] = sheet.GetTalentRank(talent.Id);

        foreach (var power in sheet.SelectedPowers)
        {
            var rank = derived.GetEffectiveRank(power, sheet);
            traits[power.PowerId] = traits.TryGetValue(power.PowerId, out var already)
                ? Math.Max(already, rank)
                : rank;
        }

        return traits;
    }

    /// <summary>
    /// Which of a combatant's Traits can answer an attack, from <c>active_and_passive_defenses</c>'s
    /// own two lists.
    ///
    /// <para><b>The lists are the file's, not this file's.</b> p.75 names Agility as the active
    /// defence and Toughness, Willpower, Armor and Force Field as the passive ones, and the entry
    /// transcribes exactly that. A Trait the combatant has no rank in is left out, so a character
    /// with no Force Field does not carry a defence of zero into
    /// <c>defense_chosen: "normally the one with the greatest rank"</c>.</para>
    /// </summary>
    private static List<string> DefencesAvailableTo(
        Dictionary<string, int> traits, PlayRulesRepository play)
    {
        var defenses = play.GetCombat("active_and_passive_defenses").Defenses!;

        return defenses.CommonActiveTraits
            .Concat(defenses.CommonPassiveTraits)
            .Select(name => name.ToLowerInvariant().Replace(' ', '_'))
            .Distinct(StringComparer.Ordinal)
            .Where(id => traits.TryGetValue(id, out var rank) && rank > 0)
            .ToList();
    }
}
