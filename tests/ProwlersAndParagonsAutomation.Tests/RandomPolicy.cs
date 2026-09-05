using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// A policy that emits <b>every</b> kind of intent, chosen off a seeded dice source.
///
/// <para><b>It exists because a property is only as wide as what drives it.</b>
/// <see cref="PlayEnginePropertyTests"/> proves that <c>Encounter.Step</c> never modifies the state
/// it is given — and it proved that of four intent types, because <see cref="AttackTheWeakest"/>
/// only ever emits an <see cref="Attack"/>, a <see cref="Hold"/>, a reroll and the
/// <see cref="EndTurn"/> the runner adds. Every grapple, every other purchase, the stabilisation
/// roll and the page turn went unexamined, and a purity claim about a third of the branches is a
/// claim about the wrong thing.</para>
///
/// <para><b>It is not a policy about how anybody plays and must never be read as one.</b> Nothing
/// here is a judgement about tactics; it is a generator, and it lives in the test project rather
/// than beside <see cref="AttackTheWeakest"/> for that reason. A balance figure measured under it
/// would be a figure about nobody.</para>
///
/// <para>The choices come off <see cref="SeededDice"/> so a failing case is reproducible from its
/// seed, which is the same reason the shipped dice source keeps its seed as a property.</para>
/// </summary>
internal sealed class RandomPolicy : IPolicy
{
    private readonly IDiceSource _dice;
    private int _next;

    public RandomPolicy(IDiceSource dice) => _dice = dice;

    /// <inheritdoc/>
    public string Name => "RandomPolicy(every intent kind, for a property test)";

    /// <summary>Every intent type this policy has emitted, for a fixture's positive control.</summary>
    public HashSet<string> Emitted { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Intent Choose(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        var others = state.Combatants.Values
            .Where(c => !string.Equals(c.Id, actor.Id, StringComparison.Ordinal))
            .Select(c => c.Id)
            .Order(StringComparer.Ordinal)
            .ToList();

        var target = others.Count == 0 ? actor.Id : others[Pick(others.Count)];
        var trait = actor.TraitRanks.Keys.Order(StringComparer.Ordinal).ToList();
        var rolled = trait.Count == 0 ? "might" : trait[Pick(trait.Count)];

        Intent intent = Cycle(9) switch
        {
            0 => new Attack(actor.Id, target, rolled, Damage(), Row(), AllOut: Coin(), Area: Coin()),
            1 => new Attack(actor.Id, target, rolled, Damage(), Row(), Effect: "Ensnare"),
            2 => new Attack(actor.Id, target, rolled, Damage(), Row(), Charge: true),
            3 => new Move(actor.Id, target, Closer: Coin()),
            4 => new Hold(actor.Id),
            5 => new GrappleIntent(actor.Id, target, (GrappleMove)Pick(3)),
            6 => new BreakFree(actor.Id, rolled, Threshold: Pick(4)),
            7 => new Stabilise(actor.Id, target),
            _ => new SpendAdversity(actor.Id, (AdversitySpend)Pick(4), Points: 1 + Pick(2))
        };

        Emitted.Add(intent.GetType().Name);
        return intent;
    }

    /// <inheritdoc/>
    public Intent? AfterRoll(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // Every Resolve purchase, including the ones the actor cannot afford and the ones that make
        // no sense where they are: a refusal is a branch of Step like any other, and the property is
        // about all of them.
        var spend = new SpendResolve(actor.Id, (ResolveSpend)Pick(Enum.GetValues<ResolveSpend>().Length),
            Points: 1 + Pick(2));

        Emitted.Add(nameof(SpendResolve));
        return spend;
    }

    /// <summary>
    /// The next branch, round-robin.
    ///
    /// <para><b>The <em>kind</em> of intent is cycled and only its arguments are rolled</b>, and the
    /// reason is the control this generator exists to satisfy. A branch chosen off a die is a branch
    /// that some seed will not reach — nine kinds over sixty turns leaves about one run in a
    /// thousand missing one, which across a theory of twenty-five seeds is a flake rather than a
    /// property. Cycling makes the coverage a fact instead of a probability, and the parameters stay
    /// rolled so the same nine kinds arrive in different shapes on every seed.</para>
    /// </summary>
    private int Cycle(int count) => _next++ % count;

    /// <summary>A number in [0, <paramref name="count"/>), off two dice so the range is wide enough.</summary>
    private int Pick(int count) =>
        (((_dice.Roll(1)[0] - 1) * 6) + (_dice.Roll(1)[0] - 1)) % count;

    private bool Coin() => Pick(2) == 0;

    private DamageKind Damage() => (DamageKind)Pick(3);

    private AttackType Row() => (AttackType)Pick(5);
}
