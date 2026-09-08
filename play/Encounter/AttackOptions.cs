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
/// <para><b>It lives here rather than on one policy because every policy needs it.</b> A second
/// copy inside the style policies would be a second thing to correct when the table is corrected,
/// and the two would agree until the day somebody corrected one of them — which is the shape of
/// defect this repository's rules-data discipline exists to prevent, reached through code instead
/// of through data. Recorded as a reading in <c>docs/guide/play-engine.md</c>.</para>
/// </summary>
public static class AttackOptions
{
    /// <summary>
    /// The Traits <paramref name="actor"/> could attack with: the table's named attacking Traits,
    /// plus every Trait of theirs the table does not name by name.
    /// </summary>
    public static IReadOnlyList<string> AvailableTo(PlayRulesRepository play, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(actor);

        var rows = play.GetCombat("attack_and_defense_table").AttackDefenseTable!;

        var named = rows
            .SelectMany(r => r.DefenseTraits.Append(r.AttackTrait))
            .Where(t => !string.Equals(t, "Power", StringComparison.Ordinal))
            .Select(t => t.Replace("1/2 ", "", StringComparison.Ordinal).ToLowerInvariant().Replace(' ', '_'))
            .ToHashSet(StringComparer.Ordinal);

        var abilities = rows
            .Select(r => r.AttackTrait)
            .Where(t => !string.Equals(t, "Power", StringComparison.Ordinal))
            .Select(t => t.ToLowerInvariant().Replace(' ', '_'));

        var powers = actor.TraitRanks.Keys.Where(id => !named.Contains(id));

        return [.. abilities.Concat(powers).Distinct(StringComparer.Ordinal)];
    }

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
