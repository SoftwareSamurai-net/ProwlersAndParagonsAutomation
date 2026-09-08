using ProwlersAndParagonsAutomation.Play.Rules;

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
/// <para><b>Whose side somebody is on is <see cref="Combatant.Side"/>, which the caller set.</b>
/// This policy never reads <see cref="CombatantKind"/> for it: a fight between Heroes (p.73) and a
/// Villain's Minions against a Foe are both fights this would otherwise refuse to see.</para>
/// </summary>
public sealed class AttackTheWeakest : IPolicy
{
    private readonly PlayRulesRepository _play;

    /// <param name="play">
    /// The play rules, which is where the Traits this policy will attack with come from — see
    /// <see cref="AttackTraits"/>.
    /// </param>
    public AttackTheWeakest(PlayRulesRepository play) =>
        _play = play ?? throw new ArgumentNullException(nameof(play));

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
    /// Which Traits this policy will attack with, or null to derive them.
    ///
    /// <para><b>Four ids used to be typed here — <c>blast</c>, <c>strike</c>, <c>might</c>,
    /// <c>agility</c> — and a hard-coded list is a rule this project invented.</b> Two of them are
    /// Powers nobody had said the combatant has, and a character built out of Energy Blast or Mental
    /// Blast held their action for the whole fight while holding an obvious weapon. The list is
    /// derived now: p.75's Attack and Defense table names the attacking Trait of every row, and the
    /// table's "Power" column means the combatant's own — which is the same derivation
    /// <c>ChooseDefence</c> makes on the other side of the roll. Recorded as a reading in
    /// <c>docs/guide/play-engine.md</c>.</para>
    ///
    /// <para>Set it to override the derivation for one run; leave it null for the derived list.</para>
    /// </summary>
    public IReadOnlyList<string>? AttackTraits { get; init; }

    /// <summary>
    /// The Traits <paramref name="actor"/> could attack with: the table's named attacking Traits at
    /// a rank they have, plus every Power of theirs the table does not name.
    ///
    /// <para>"Power" is derived from the table's own other columns rather than listed, so a corrected
    /// table moves this with it — and a combatant whose defence Powers are all the table names is
    /// left with the Abilities, which is exactly the case the table describes.</para>
    /// </summary>
    public IReadOnlyList<string> TraitsAvailableTo(Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // <b>The derivation itself lives in <c>AttackOptions</c></b>, because the style policies need
        // exactly the same one: a second copy here would be a second thing to correct when p.75's
        // table is corrected, and the two would agree right up until somebody corrected one of them.
        return AttackTraits ?? AttackOptions.AvailableTo(_play, actor);
    }

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

        var trait = TraitsAvailableTo(actor)
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
    /// Whether the second combatant is on the other side from the first — the field the caller set,
    /// never the kind of character they are.
    /// </summary>
    private static bool IsEnemyOf(Combatant actor, Combatant other) =>
        !string.Equals(actor.Side, other.Side, StringComparison.Ordinal);
}
