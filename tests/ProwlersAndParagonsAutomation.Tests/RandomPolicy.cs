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

    // <b>A counter of its own, because the two cycles are independent.</b> Sharing one with the
    // intent kinds above made every AfterRoll advance it, so Choose stopped round-robining and
    // three seeds quietly lost an intent kind — which the control below caught.
    private int _nextPurchase;

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

        // <b>p.75's cover rides on two of the attack branches rather than adding two of its own</b>,
        // and the reason is arithmetic: the cycle's length is what decides how many turns a fight
        // has to last for every branch to be reached, and lengthening it from ten to twelve pushed
        // the fourth Adversity purchase past the end of one seed's fight. Both halves of the clause
        // are still here — an attack merely behind cover, and one going through it, which is what a
        // Structure declares.
        Intent intent = Cycle(10) switch
        {
            0 => new Attack(
                actor.Id, target, rolled, Damage(), Row(),
                AllOut: Coin(), Area: Coin(), Cover: Behind(), VulnerablePart: WeakPoint()),
            1 => new Attack(
                actor.Id, target, rolled, Damage(), Row(),
                Effect: "Ensnare", Cover: Behind(), CoverStructure: Pick(9)),
            2 => new Attack(actor.Id, target, rolled, Damage(), Row(), Charge: true),
            3 => new Attack(
                actor.Id, target, rolled, Damage(), Row(), Team: true, CloseRangeOnly: Thrown()),
            4 => new Move(actor.Id, target, Closer: Coin()),
            5 => new Hold(actor.Id),
            6 => new GrappleIntent(actor.Id, target, (GrappleMove)Pick(3)),
            7 => new BreakFree(actor.Id, rolled, Threshold: Pick(4)),
            8 => new Stabilise(actor.Id, target),
            _ => Adversity(actor, target)
        };

        Emitted.Add(intent.GetType().Name);

        if (intent is Attack behind) Covers.Add(behind.Cover);

        return intent;
    }

    /// <summary>Every band of cover this policy has put on an attack, for a fixture's control.</summary>
    public HashSet<Cover> Covers { get; } = [];

    /// <summary>Both answers p.80's <c>vulnerable_part</c> declaration takes, for the same control.</summary>
    public HashSet<bool> WeakPoints { get; } = [];

    /// <summary>Both answers p.79's <c>close_range_only</c> declaration takes, for the same control.</summary>
    public HashSet<bool> ThrownWeapons { get; } = [];

    private int _nextThrown;

    private bool Thrown()
    {
        var lobbed = _nextThrown++ % 2 == 0;
        ThrownWeapons.Add(lobbed);
        return lobbed;
    }

    // <b>Its own counter and cycled, for the reason the cover band is</b>, and without lengthening
    // the intent cycle: a flag chosen off a die is one some seed will not reach, and the branch
    // that costs four dice and cancels a doubling is exactly the one a purity property wants inside
    // it.
    private int _nextWeakPoint;

    private bool WeakPoint()
    {
        var aimed = _nextWeakPoint++ % 2 == 0;
        WeakPoints.Add(aimed);
        return aimed;
    }

    // <b>Cycled rather than rolled, and its own counter</b>, for the reason the intent kinds above
    // are: a band chosen off a die is a band some seed will not reach, and `Cover.Complete` is the
    // one that refuses — the branch a purity property most wants inside it.
    private int _nextCover;

    private Cover Behind() =>
        (Cover)(_nextCover++ % Enum.GetValues<Cover>().Length);

    /// <summary>Every Resolve purchase this policy has emitted, for a fixture's positive control.</summary>
    public HashSet<ResolveSpend> Purchases { get; } = [];

    /// <summary>Every Adversity purchase this policy has emitted, for the same control.</summary>
    public HashSet<AdversitySpend> GmPurchases { get; } = [];

    // <b>Cycled, and half of them carry the GM's words.</b> p.85's three own purchases are refused
    // unless the spend says what the point bought, so a generator that never sent a narration would
    // drive the refusal branch of all three and none of the branches that change the state — which
    // is the half the purity property is about. Its own counter, for the reason AfterRoll has one:
    // sharing one makes every other call advance it and quietly narrows the coverage.
    private int _nextGmPurchase;

    private SpendAdversity Adversity(Combatant actor, string target)
    {
        var kind = (AdversitySpend)(_nextGmPurchase++ % Enum.GetValues<AdversitySpend>().Length);

        GmPurchases.Add(kind);

        return new SpendAdversity(
            actor.Id, kind, Points: 1 + Pick(2), Target: target,
            Narration: Coin() ? "a hot temper" : null);
    }

    /// <inheritdoc/>
    public Intent? AfterRoll(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        // <b>Every Resolve purchase, cycled rather than rolled</b>, for the reason the kinds above
        // are cycled: a purchase chosen off a die is one some seed will not reach, and ten of them
        // over sixty turns leaves the coverage a probability instead of a fact. The ones the actor
        // cannot afford and the ones that make no sense where they are are the point — a refusal is
        // a branch of Step like any other, and the property is about all of them.
        var kind = (ResolveSpend)(_nextPurchase++ % Enum.GetValues<ResolveSpend>().Length);

        var other = state.Combatants.Keys
            .Where(id => !string.Equals(id, actor.Id, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();

        // p.79's luring is the one purchase that points at somebody, so half of them do and half of
        // them do not: both branches are Step's.
        var spend = new SpendResolve(
            actor.Id, kind, Points: 1 + Pick(2), Target: Coin() ? other : null);

        Emitted.Add(nameof(SpendResolve));
        Purchases.Add(kind);

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
