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
    /// <param name="size">
    /// How big this character is, for p.75's size bands. <b>The caller says, and this reads it off
    /// nothing</b> — see <see cref="Combatant.Size"/>: Chapter 2 has no size stat, and the one place
    /// the book turns a rank into a size is Growth's and Shrinking's height tables, which are
    /// printed in the rulebook and are in no file this project may read. Defaults to the same size
    /// as everybody else, which is what every figure this engine produces is measured against.
    /// </param>
    /// <param name="invisible">
    /// Whether this character cannot be seen right now (p.75). <b>Also the caller's</b>, and
    /// deliberately not a read of the Invisibility Power: Ch.2 p.32 prints "You <em>can</em> turn
    /// invisible", so the Power is a capability and this is a state, and nothing in Chapters 3–5
    /// turns one on. The half of that sentence which <em>is</em> a capability — the Powers that
    /// compensate for not seeing — is read off the sheet, below.
    /// </param>
    /// <param name="hardTarget">
    /// Whether this character is one of p.80's hard targets — a machine, a vehicle or a thick
    /// inanimate object — whose passive defence rank the Hard Targets setting doubles. <b>The
    /// caller's too</b>, and for the same reason as the two above: Chapter 2 has no such flag,
    /// nothing on a sheet says a character is a machine, and the same battlesuit is a vehicle in
    /// one GM's game and a person in armour in another's.
    /// </param>
    /// <param name="ready">
    /// Whether this character has a weapon or Power aimed and ready to strike (p.79), which under
    /// the Drop setting doubles their effective Edge against everyone who has not. <b>The caller's
    /// as well</b>, and for the sharper version of the same reason: carrying a gun is a capability
    /// and having it levelled is a state, and nothing in Chapters 3–5 levels one. p.79 hands the
    /// question to the GM in as many words.
    /// </param>
    public static Combatant From(
        CharacterSheet sheet,
        RulesRepository rules,
        DerivedStatsCalculator derived,
        PlayRulesRepository play,
        CombatantKind kind,
        string? id = null,
        string? side = null,
        double size = Combatant.SameSize,
        bool invisible = false,
        bool hardTarget = false,
        bool ready = false)
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
        var powers = PowersOn(sheet);
        var rangedPowers = RangedPowersOn(sheet, rules);

        return kind switch
        {
            CombatantKind.Hero => Combatant.Hero(
                id ?? name, name, edge, health, derived.CalculateResolve(sheet), traits, defences,
                side ?? Combatant.HeroSide, size, invisible, powers, hardTarget, rangedPowers, ready),
            CombatantKind.Villain => Combatant.Villain(
                id ?? name, name, edge, health, traits, defences, side ?? Combatant.OpposingSide,
                size, invisible, powers, hardTarget, rangedPowers, ready),
            CombatantKind.Foe => Combatant.Foe(
                id ?? name, name, edge, health, traits, defences, side ?? Combatant.OpposingSide,
                size, invisible, powers, hardTarget, rangedPowers, ready),
            CombatantKind.Extra => Combatant.Extra(
                id ?? name, name, edge, health, traits, defences, side ?? Combatant.OpposingSide,
                size, invisible, powers, hardTarget, rangedPowers, ready),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of combatant.")
        };
    }

    /// <summary>
    /// Every Power id on the sheet, <b>whatever rank the character engine gives it</b>.
    ///
    /// <para><b>It is a separate read from <see cref="TraitRanks"/> because a rank cannot answer the
    /// question p.75 asks.</b> Blind Fighting and Radar are default-rank Powers, so
    /// <see cref="DerivedStatsCalculator.GetEffectiveRank"/> answers 0 for both by design — and a
    /// visibility penalty that read a rank would compensate nobody, silently, for ever. This is the
    /// list, and <see cref="Combatant.Powers"/> is where it goes.</para>
    /// </summary>
    private static HashSet<string> PowersOn(CharacterSheet sheet) =>
        new(sheet.SelectedPowers.Select(power => power.PowerId), StringComparer.Ordinal);

    /// <summary>
    /// Ch.2 p.19's Range for a Power whose effect reaches a target at a distance.
    ///
    /// <para><b>It is a value of the character rules' own vocabulary and this is the one place
    /// <c>play/</c> spells it</b>, because it is the one place a sheet is read at all. The other
    /// four — <c>self</c>, <c>touch</c>, <c>zone</c> and <c>special</c> — are deliberately not here;
    /// see <see cref="Combatant.RangedPowers"/> for the reading and its direction.</para>
    /// </summary>
    private const string RangedPowerRange = "ranged";

    /// <summary>
    /// Every Power on the sheet whose own Range is <see cref="RangedPowerRange"/> — the half of
    /// Ch.4 p.79's Close Range rule that a character sheet can answer.
    ///
    /// <para><b>A Power id the character rules do not have contributes nothing rather than
    /// throwing</b>, because judging a sheet is the first engine's job and this one only asks for
    /// the figures it needs: an invented Power is a character <c>CharacterValidator</c> refuses,
    /// and a fight that got this far is one somebody has already decided to run.</para>
    /// </summary>
    private static HashSet<string> RangedPowersOn(CharacterSheet sheet, RulesRepository rules) =>
        new(sheet.SelectedPowers
                .Select(power => power.PowerId)
                .Where(id => string.Equals(
                    rules.GetPower(id)?.Range, RangedPowerRange, StringComparison.OrdinalIgnoreCase)),
            StringComparer.Ordinal);

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

        return Rounding.Half(play, health);
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
