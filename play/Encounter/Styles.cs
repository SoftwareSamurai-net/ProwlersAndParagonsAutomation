using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// <b>Sheets alone.</b> Nobody buys anything: no Resolve, no Adversity, no seized initiative and no
/// reroll — whatever the roll was, it stands.
///
/// <para><b>The rules it uses, in full:</b> attack the opponent the selector picks, with the
/// best-ranked Trait available; hold when there is nobody left to attack or nothing to attack with;
/// never emit a purchase.</para>
///
/// <para><b>What it is for is a floor.</b> Every other figure a run here produces is a figure about
/// a side that spent something, and the pools are the largest thing a policy can be wrong about —
/// so a matchup measured under this style is the one measurement whose rate does not depend on
/// anybody's guess about when a person reaches for a point. It is also the control that makes the
/// other three mean anything: a style whose rate is the same as this one bought nothing worth
/// having.</para>
/// </summary>
public sealed class ManoAMano : StylePolicy
{
    /// <param name="play">The play rules.</param>
    /// <param name="targeting">Which opponent to go after.</param>
    public ManoAMano(PlayRulesRepository play, Targeting targeting = Targeting.Weakest)
        : base(play, targeting) { }

    /// <inheritdoc/>
    public override PlayStyle Style => PlayStyle.ManoAMano;

    /// <inheritdoc/>
    public override string Note =>
        "villain and heroes fight on their sheets alone: not a point of Resolve or Adversity is "
        + "spent, so the rate is the matchup with both pools untouched";

    /// <summary>
    /// <inheritdoc/>
    ///
    /// <para>Always null, and it is written out rather than inherited because it is this style's
    /// whole claim. A fixture scans the ledger of a hundred seeded fights and requires not one
    /// purchase line, with the same fight under <see cref="Standard"/> producing them.</para>
    /// </summary>
    public override Intent? AfterRoll(EncounterState state, Combatant actor) => null;
}

/// <summary>
/// <b>A mixture, which is what the owner's own table does.</b> A side seizes the initiative when
/// the opposition looks faster, and answers the other side's spending with spending of its own.
///
/// <para><b>The rules it uses, in full:</b></para>
/// <list type="number">
/// <item>Attack the opponent the selector picks, with the best-ranked Trait available.</item>
/// <item><b>Seize when out-Edged.</b> Where the greatest <see cref="EncounterState.EffectiveEdge"/>
/// on the other side is greater than this combatant's own, and seizing would actually move them in
/// the order, buy p.73's seized initiative — a Hero out of their Resolve, an NPC out of the GM's
/// pool.</item>
/// <item><b>Answer a spend with a spend.</b> Where somebody on the other side paid for something on
/// the previous page and this combatant's attack fell short by <see cref="RerollWithinSuccesses"/>
/// or fewer, buy p.84's reroll.</item>
/// </list>
///
/// <para><b>"Seems faster or stronger" is read as the Edge, and that is a reading.</b> The owner's
/// sentence is about an impression at a table; the only figure in Chapters 3–5 that is about being
/// faster is p.73's Edge, and it is the one the order is actually built on — which is why the
/// comparison is against <see cref="EncounterState.EffectiveEdge"/> and not against the sheets:
/// p.73's optional random initiative replaces the figure for the battle and p.79's Drop doubles it.
/// "Stronger" has no single figure — a rank is per Trait — so it is not read at all rather than
/// read badly. <c>docs/guide/play-engine.md</c> records it.</para>
///
/// <para><b>"In response to the other side spending" is state the ledger already carries</b>, so it
/// is read back off the previous page's purchase lines rather than tracked in a field of this
/// policy's own. A policy that kept its own count would be a second record of something the engine
/// already writes down, and the two would agree until one of them was wrong.</para>
///
/// <para><b>Nobody moves first, and that is honest rather than a hole.</b> On page one nothing has
/// been spent, so the only purchase available is the seize — which means a fight in which neither
/// side is out-Edged is a fight this style plays exactly like <see cref="ManoAMano"/>. That is the
/// guess: a table that does not feel behind does not reach for its pool.</para>
/// </summary>
public sealed class Standard : StylePolicy
{
    /// <param name="play">The play rules.</param>
    /// <param name="targeting">Which opponent to go after.</param>
    public Standard(PlayRulesRepository play, Targeting targeting = Targeting.Weakest)
        : base(play, targeting) { }

    /// <inheritdoc/>
    public override PlayStyle Style => PlayStyle.Standard;

    /// <inheritdoc/>
    public override string Note =>
        "a mixture: each side seizes the initiative when the other looks faster on Edge, and "
        + "answers a point spent against it on the previous page with a reroll of its own";

    /// <summary>
    /// How near a roll has to have come before this style answers a spend with one — the shortfall,
    /// in successes, at or below which it buys a reroll.
    ///
    /// <para>Two is a judgement and not a rule, exactly as it is on <see cref="AttackTheWeakest"/>:
    /// p.84 puts no condition on the purchase at all.</para>
    /// </summary>
    public int RerollWithinSuccesses { get; init; } = 2;

    /// <inheritdoc/>
    public override Intent? AfterRoll(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        if (PoolFor(state, actor) < 1) return null;

        if (SeizingWouldMove(state, actor) && BestEnemyEdge(state, actor) > EdgeOf(state, actor))
            return Buy(actor, ResolveSpend.SeizeInitiative);

        var shortfall = Shortfall(state, actor);

        if (shortfall is 0 || shortfall > RerollWithinSuccesses) return null;

        return LedgerReading.OtherSideBoughtOn(state, actor.Side, state.Page - 1)
            ? Buy(actor, ResolveSpend.Reroll)
            : null;
    }
}

/// <summary>
/// <b>The cheap upper bound on damage.</b> Everybody goes all-out on every page and nobody spends a
/// point on staying alive.
///
/// <para><b>The rules it uses, in full:</b></para>
/// <list type="number">
/// <item>Attack the opponent the selector picks, with the best-ranked Trait available, always
/// <see cref="Attack.AllOut"/> — p.78's two extra dice, with every defence of the attacker's halved
/// until after their next turn.</item>
/// <item>Buy p.84's reroll whenever the attack fell short at all and there is a point to pay with.
/// </item>
/// <item>Never buy anything else — no seized initiative, no rescue from the Fatal Damage threshold,
/// no stabilisation, no instant recovery.</item>
/// </list>
///
/// <para><b>It is not a way anybody plays and it is not meant to be.</b> It is the shape of a
/// question: how bad can this fight get if both sides simply hit as hard as the rules allow. A rate
/// off it bounds the other three from one side, and the gap between it and
/// <see cref="ManoAMano"/> is what the pools and the caution are worth.</para>
///
/// <para><b>p.78's own guard keeps it from being free</b>, and the engine applies it: an opponent
/// who could not penetrate the passive defence at its full rank still cannot at half. So a reckless
/// run is not simply everybody taking double damage, which is what makes it worth measuring rather
/// than deducing.</para>
/// </summary>
public sealed class Reckless : StylePolicy
{
    /// <param name="play">The play rules.</param>
    /// <param name="targeting">Which opponent to go after.</param>
    public Reckless(PlayRulesRepository play, Targeting targeting = Targeting.Weakest)
        : base(play, targeting) { }

    /// <inheritdoc/>
    public override PlayStyle Style => PlayStyle.Reckless;

    /// <inheritdoc/>
    public override string Note =>
        "all-out on every page and never a point spent on staying alive — not a way anybody plays, "
        + "but the upper bound the other three sit under";

    /// <inheritdoc/>
    protected override Intent Aim(EncounterState state, Combatant actor, Combatant target, string trait) =>
        new Attack(actor.Id, target.Id, trait, AllOut: true);

    /// <inheritdoc/>
    public override Intent? AfterRoll(EncounterState state, Combatant actor) =>
        PoolFor(state, actor) >= 1 && Shortfall(state, actor) > 0
            ? Buy(actor, ResolveSpend.Reroll)
            : null;
}

/// <summary>
/// <b>Both sides exploiting the rules to maximum efficiency</b>, as far as an engine can compute
/// what maximum efficiency is.
///
/// <para><b>The rules it uses, in full — and every one of them is arithmetic this policy can
/// actually do:</b></para>
/// <list type="number">
/// <item><b>Extra dice, priced against the shortfall.</b> Where the attack fell short by <i>s</i>
/// successes, it works out how many dice p.84's purchase would have to buy to cover <i>s</i> on
/// average — the mean successes a die is worth under the success map in force, which it reads off
/// <see cref="SuccessCounter"/> rather than typing a figure — and buys exactly that many when the
/// pool can pay for them.</item>
/// <item><b>A reroll where the dice cannot reach.</b> Where the shortfall is bigger than the pool
/// could buy dice for, one point picks the whole roll back up, which is the only purchase that can
/// move an arbitrary distance. p.85's floor keeps the first roll if the second is worse, so the
/// purchase cannot make things worse either.</item>
/// <item><b>Seize when it changes the order.</b> Where the attack landed and there is somebody
/// standing ahead of this combatant in the order, buy p.73's place at the front — once, because it
/// lasts the rest of the fight.</item>
/// <item><b>Team attacks when two allies are adjacent.</b> Where an ally who is still standing is
/// at Close Range with the same target, and p.79's one-a-battle limit has not already been used on
/// that target, lead a team attack for its two dice.</item>
/// <item><b>All-out when the counterattack cannot defeat them this page.</b> Going all-out halves
/// every defence, so it computes the worst a single opponent could do — the greatest attack rank on
/// the other side at <c>damage.damage_per_net_success</c>, which is the most net successes an attack
/// could possibly produce times the book's own rate — and goes all-out only where surviving it
/// still leaves them above the defeat figure.</item>
/// </list>
///
/// <para><b>It is allowed to be simple; it is not allowed to be dishonest.</b> None of this is
/// optimal play and it does not claim to be: it has no model of the opponent's policy, it never
/// looks a page ahead, and the all-out guard is a worst case rather than a distribution. What it
/// does is spend at the moments where a spend visibly changes the answer, which is the difference
/// between it and <see cref="Standard"/>, and a report quoting a min-max rate is quoting a rate
/// about the five rules above and nothing more.</para>
/// </summary>
public sealed class MinMax : StylePolicy
{
    private SuccessCounter? _counter;
    private bool _counterCheckingYourSwing;

    /// <param name="play">The play rules.</param>
    /// <param name="targeting">Which opponent to go after.</param>
    public MinMax(PlayRulesRepository play, Targeting targeting = Targeting.Weakest)
        : base(play, targeting) { }

    /// <inheritdoc/>
    public override PlayStyle Style => PlayStyle.MinMax;

    /// <inheritdoc/>
    public override string Note =>
        "both sides exploit the rules: dice priced against the shortfall, a reroll where the dice "
        + "cannot reach, a seized initiative that moves the order, team attacks between adjacent "
        + "allies, and all-out only where the counterattack could not put them down";

    /// <inheritdoc/>
    protected override Intent Aim(EncounterState state, Combatant actor, Combatant target, string trait) =>
        new Attack(
            actor.Id, target.Id, trait,
            AllOut: TheCounterattackCannotReachThem(state, actor),
            Team: AnAllyIsAdjacentTo(state, actor, target));

    /// <inheritdoc/>
    public override Intent? AfterRoll(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        var pool = PoolFor(state, actor);

        if (pool < 1) return null;

        var shortfall = Shortfall(state, actor);

        if (shortfall > 0)
        {
            var points = PointsToCover(shortfall, state.Table.CheckingYourSwing);

            return points > 0 && points <= pool
                ? Buy(actor, ResolveSpend.ExtraDice, points)
                : Buy(actor, ResolveSpend.Reroll);
        }

        return SeizingWouldMove(state, actor) ? Buy(actor, ResolveSpend.SeizeInitiative) : null;
    }

    /// <summary>
    /// How many points of p.84's dice it would take to cover a shortfall of
    /// <paramref name="shortfall"/> successes on average, at the file's own rate and under the
    /// success map <paramref name="checkingYourSwing"/> selects.
    ///
    /// <para><b>Neither the die's worth nor the purchase's rate is typed here.</b> The mean is the
    /// average of every face's value under <see cref="SuccessCounter"/>, so Checking Your Swing's
    /// flatter map moves it; the dice a point buys and the points a die costs are
    /// <c>spend_challenge_roll_dice</c>'s own <c>dice_gained</c> and <c>cost_resolve</c>. A figure
    /// written here would be a second transcription of a rule the store already holds.</para>
    ///
    /// <para><b>Public so the claim can be driven at the point it is made.</b> "Checking Your Swing
    /// moves the price" is the whole of what reading the map rather than typing a figure buys, and
    /// inside a fight the two settings are two different fights — the flattened six changes every
    /// roll — so the same shortfall under the two maps cannot be compared through a run. A fixture
    /// asks this directly, with the two maps' values for a six as its control.</para>
    /// </summary>
    public int PointsToCover(int shortfall, bool checkingYourSwing)
    {
        var counter = CounterFor(checkingYourSwing);
        var faces = Enumerable.Range(1, counter.HighestFace).ToList();
        var perDie = faces.Average(counter.Value);

        if (perDie <= 0) return 0;

        var spend = Play.GetResolve("spend_challenge_roll_dice").Spend!;
        var diceNeeded = (int)Math.Ceiling(shortfall / perDie);
        var perPoint = spend.DiceGained!.Value;

        // The purchase is priced per point, so the answer is in points and not in dice.
        return (int)Math.Ceiling((double)diceNeeded / perPoint) * spend.CostResolve!.Value;
    }

    private SuccessCounter CounterFor(bool checkingYourSwing)
    {
        if (_counter is not null && _counterCheckingYourSwing == checkingYourSwing) return _counter;

        _counterCheckingYourSwing = checkingYourSwing;
        _counter = new SuccessCounter(Play, checkingYourSwing);

        return _counter;
    }

    /// <summary>
    /// Whether the worst <b>one</b> opponent could do to this combatant with <b>damage</b> still
    /// leaves them standing — the condition p.78's two dice are worth their halved defences on.
    ///
    /// <para><b>It is a bound on one attack's damage and on nothing else, and that is worth saying
    /// out loud because the rule this policy publishes is broader than the arithmetic under it.</b>
    /// The greatest attack rank on the other side, at <c>damage.damage_per_net_success</c>, is a
    /// true ceiling on what a single attack can take off — every die a success and no defence at
    /// all — and three things a page can do to a character are outside it:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Everybody on the other side acts.</b> The ceiling is the greatest single attack, so
    /// two opponents who could each take half of somebody's Health both pass it.</item>
    /// <item><b>p.76's special effect is not damage.</b> An Ensnare that outlasts what is left of a
    /// target ends their fight without a point coming off, and no figure here can see it.</item>
    /// <item><b>Fatal Damage is a clock rather than a floor.</b> The comparison is against
    /// <c>damage.defeated_at_health</c>, so with that setting on a blow can leave somebody above the
    /// defeat figure and bleeding, and p.79's tick at the page turn finishes them.</item>
    /// </list>
    ///
    /// <para>Wound Penalties are the one omission that cannot bite: they cost a hurt attacker dice,
    /// so the real figure is smaller than this ceiling rather than larger. <b>A policy is allowed to
    /// be simple and is not allowed to read as cleverer than it is</b> — this is a worst case on one
    /// opponent's damage, and <c>docs/guide/play-engine.md</c> records it as one.</para>
    /// </summary>
    private bool TheCounterattackCannotReachThem(EncounterState state, Combatant actor)
    {
        if (actor.Kind == CombatantKind.MinionGroup) return false;

        var damage = Play.GetCombat("damage").Damage!;
        var enemies = Enemies(state, actor);

        if (enemies.Count == 0) return true;

        // The most net successes an attack could produce is its whole pool coming up as successes,
        // which is the attacking rank — an upper bound rather than a distribution, and named as one.
        var worst = enemies.Max(BestRank) * damage.DamagePerNetSuccess;

        return actor.CurrentHealth - worst > damage.DefeatedAtHealth;
    }

    /// <summary>
    /// Whether somebody on this combatant's own side is at Close Range with the target and could
    /// join in, and p.79's limit has not already been used on that target this battle.
    /// </summary>
    private static bool AnAllyIsAdjacentTo(EncounterState state, Combatant actor, Combatant target)
    {
        if (state.TeamAttacked.Contains(target.Id, StringComparer.Ordinal)) return false;
        if (state.RangeBetween(actor.Id, target.Id) != RangeBand.Close) return false;

        return state.Combatants.Values.Any(ally =>
            !string.Equals(ally.Id, actor.Id, StringComparison.Ordinal)
            && string.Equals(ally.Side, actor.Side, StringComparison.Ordinal)
            && !ally.Defeated(0)
            && state.RangeBetween(ally.Id, target.Id) == RangeBand.Close);
    }
}
