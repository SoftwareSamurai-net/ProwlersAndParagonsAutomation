using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// Which Traits a combatant could attack with, derived from p.75's Attack and Defense table and
/// from the combatant's own sheet.
///
/// <para><b>It is derived and never listed, and the reason is a defect this project shipped.</b>
/// Four ids used to be typed into <see cref="AttackTheWeakest"/> — <c>blast</c>, <c>strike</c>,
/// <c>might</c>, <c>agility</c> — so a character built out of Energy Blast or Mental Blast held
/// their action for a whole fight while holding an obvious weapon. p.75's table names the attacking
/// Trait of every row, and the table's bare "Power" column means the combatant's own, which is the
/// same derivation <c>ChooseDefence</c> makes on the other side of the roll.</para>
///
/// <para><b>And the correction of that defect overshot, which is the second one.</b> "The table's
/// named Traits plus every Trait the table does not name" reads as a derivation and is in fact a
/// complement of a five-row table taken over a dictionary holding six Abilities, twelve Talents and
/// every Power — so a Talent, a passive defence and a movement Power were all attack forms. It is
/// visible in the report that found it: two Heroes attacking with <c>academics</c>, one with
/// <c>covert</c>, and Schism with <c>armor</c>. The Power half is a question about a Chapter 2
/// entry now, and it is asked where a sheet is read.</para>
///
/// <para><b>It lives here rather than on one policy because every policy needs it.</b> A second
/// copy inside the style policies would be a second thing to correct when the table is corrected,
/// and the two would agree until the day somebody corrected one of them — which is the shape of
/// defect this repository's rules-data discipline exists to prevent, reached through code instead
/// of through data. Recorded as a reading in <c>docs/guide/play-engine.md</c>.</para>
/// </summary>
public static class AttackOptions
{
    /// <summary>
    /// The Traits <paramref name="actor"/> could attack with: p.75's own named attacking Traits,
    /// plus the Powers of theirs whose Ch.2 entry is an attack.
    ///
    /// <para><b>The second half used to be "every Trait the table does not name", and that was a
    /// defect that reached a real measurement.</b> <see cref="Combatant.TraitRanks"/> carries the
    /// six Abilities, the twelve Talents and every Power on one dictionary, so the complement of a
    /// five-row table is most of a character sheet: the four Low Level Pinnacle City Heroes against
    /// Schism were measured with two of them attacking with <c>academics</c>, one with
    /// <c>covert</c>, and the Villain with <c>armor</c> — a Talent is not an attack and neither is
    /// a passive defence. Every figure of that report was a figure about a fight nobody could have
    /// played.</para>
    ///
    /// <para><b>What p.75 names is read off the table and never typed here</b>, so a corrected
    /// table moves this with it: the rows attack with Might (Unarmed, Melee Weapon), with Agility
    /// (Ranged Weapon) and with the bare <c>Power</c> column (Physical Power, Mental Power). The
    /// bare column is the combatant's own, and <see cref="Combatant.AttackPowers"/> is which of
    /// theirs it means — a question only a Chapter 2 entry can answer, so it is answered where the
    /// sheet is read.</para>
    ///
    /// <para><b>A group of Minions is the one combatant whose whole Trait list is an attack
    /// form.</b> p.77 gives a group one characteristic and it is what they roll to attack with — no
    /// Ability, no Power and no sheet — so the derivation above would leave them holding their
    /// action for the whole fight.</para>
    /// </summary>
    public static IReadOnlyList<string> AvailableTo(PlayRulesRepository play, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.Kind == CombatantKind.MinionGroup) return [.. actor.TraitRanks.Keys];

        var rows = play.GetCombat("attack_and_defense_table").AttackDefenseTable!;

        var named = rows
            .Select(r => r.AttackTrait)
            .Where(t => !string.Equals(t, PowerColumn, StringComparison.Ordinal))
            .Select(t => t.ToLowerInvariant().Replace(' ', '_'));

        return [.. named.Concat(actor.AttackPowers).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// What p.75's table prints where a row's attacking Trait is the character's own Power rather
    /// than an Ability. Required to still be there, because the whole of the Power half of this
    /// derivation hangs off recognising it.
    /// </summary>
    private const string PowerColumn = "Power";

    /// <summary>
    /// The best one of those <paramref name="actor"/> actually has a rank in, or null where they
    /// have none — the case a policy answers with a held action.
    /// </summary>
    public static string? BestFor(PlayRulesRepository play, Combatant actor) =>
        AvailableTo(play, actor)
            .Where(id => actor.Rank(id) > 0)
            .OrderByDescending(actor.Rank)
            .ThenBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();
}
