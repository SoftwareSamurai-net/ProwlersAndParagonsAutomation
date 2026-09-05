namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// The one policy this slice ships: hit the enemy with the least Health left, with the best Trait
/// available, and buy a reroll when the roll came close.
///
/// <para><b>It is a guess about how people play, stated so it can be argued with.</b> Focusing fire
/// on whoever is nearly down is a real table habit and it is not the only one — spreading damage,
/// protecting a hurt ally, going after whoever hits hardest are all as plausible — so a measurement
/// made under this policy is a measurement about a party that plays this way. Its name goes in every
/// report for exactly that reason.</para>
///
/// <para><b>Sides are Heroes against everybody else</b>, which is <c>Encounter</c>'s reading rather
/// than a rule the book prints; see that class's own summary.</para>
/// </summary>
public sealed class AttackTheWeakest : IPolicy
{
    /// <summary>
    /// How near a roll has to have come before this policy pays for a second look — the shortfall,
    /// in successes, at or below which it buys a reroll.
    ///
    /// <para>Two is a judgement, not a rule: the page puts no condition on the purchase at all
    /// (Ch.5 p.84, "you may spend 1 Resolve to reroll"). A policy that rerolled everything would
    /// empty a pool on the first page and one that never rerolled would leave the currency unused,
    /// and both would answer a different question from the one a balance run is asking.</para>
    /// </summary>
    public int RerollWithinSuccesses { get; init; } = 2;

    /// <summary>
    /// Which Traits this policy will attack with, best first by the rank the combatant has.
    ///
    /// <para>The list is Chapter 4's own attack Traits — the ones the Attack and Defense table names
    /// as an attacker's — plus the two commonest attack Powers. A combatant with none of them holds
    /// their action rather than attacking with something the table does not offer.</para>
    /// </summary>
    public IReadOnlyList<string> AttackTraits { get; init; } =
        ["blast", "strike", "might", "agility"];

    /// <inheritdoc/>
    public string Name => $"AttackTheWeakest(reroll within {RerollWithinSuccesses})";

    /// <inheritdoc/>
    public Intent Choose(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        var enemies = state.Combatants.Values
            .Where(c => IsEnemyOf(actor, c))
            .Where(c => !c.Defeated(0) || c.Kind == CombatantKind.MinionGroup && c.GroupSize > 0)
            .OrderBy(c => c.Kind == CombatantKind.MinionGroup ? c.GroupSize : c.CurrentHealth)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        var trait = AttackTraits
            .Where(id => actor.Rank(id) > 0)
            .OrderByDescending(actor.Rank)
            .ThenBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();

        return enemies.Count == 0 || trait is null
            ? new Hold(actor.Id)
            : new Attack(actor.Id, enemies[0].Id, trait);
    }

    /// <inheritdoc/>
    public Intent? AfterRoll(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.HoldsResolve || actor.Resolve <= 0) return null;

        if (state.LastAttack is not { } last
            || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return null;
        }

        // What it needed was one more success than the defence scored. A roll that already landed
        // is left alone; one that fell short by more than the threshold is not worth a point.
        var shortfall = last.DefenceSuccesses + 1 - last.AttackSuccesses;

        return shortfall > 0 && shortfall <= RerollWithinSuccesses
            ? new SpendResolve(actor.Id, ResolveSpend.Reroll)
            : null;
    }

    /// <summary>
    /// Whether the second combatant is on the other side from the first. Heroes against everybody
    /// else — <c>Encounter</c>'s reading, recorded in the guide.
    /// </summary>
    private static bool IsEnemyOf(Combatant actor, Combatant other) =>
        (actor.Kind == CombatantKind.Hero) != (other.Kind == CombatantKind.Hero);
}
