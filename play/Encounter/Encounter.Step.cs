using ProwlersAndParagonsAutomation.Play.Rules;
using ProwlersAndParagonsAutomation.Play.Rules.Models;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

public sealed partial class Encounter
{
    /// <summary>
    /// Resolves one intent and hands back a new state.
    ///
    /// <para><b>The state passed in is not touched.</b> Every branch below builds a new one, and a
    /// property test serialises the input before and after to be sure — "I did not modify the
    /// argument" is a claim that stays true right up until somebody reaches for <c>Add</c>.</para>
    /// </summary>
    public StepResult Step(EncounterState state, Intent intent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);

        var lines = new List<LedgerLine>();

        var next = intent switch
        {
            Attack attack => ResolveAttack(state, attack, lines),
            Move move => ResolveMove(state, move, lines),
            Hold hold => ResolveHold(state, hold, lines),
            GrappleIntent grapple => ResolveGrapple(state, grapple, lines),
            Toss toss => ResolveToss(state, toss, lines),
            BreakFree free => ResolveBreakFree(state, free, lines),
            Stabilise steady => ResolveStabilise(state, steady, lines),
            SpendResolve spend => ResolveResolveSpend(state, spend, lines),
            SpendAdversity spend => ResolveAdversitySpend(state, spend, lines),
            EndTurn => ResolveEndTurn(state, lines),
            EndPage => ResolveEndPage(state, lines),
            _ => RefuseUnknownIntent(state, intent, lines)
        };

        return new StepResult(next with { Ledger = next.Ledger.Plus(lines) }, lines);
    }

    /// <summary>
    /// An intent type this engine does not know at all, refused against <c>actions</c> — the entry
    /// that says what a character may do on their turn, and so the rule such a request falls outside
    /// of.
    /// </summary>
    private EncounterState RefuseUnknownIntent(EncounterState state, Intent intent, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("actions");

        return Refuse(state, intent.Actor, entry.Id, entry.SourceRef, lines,
            $"not yet implemented: {intent.GetType().Name}");
    }

    /// <summary>
    /// Whether a Hero can pay for what they asked for, refused on the ledger where they cannot.
    ///
    /// <para><b>An unaffordable purchase used to reach <see cref="Combatant.Spending"/> and throw an
    /// exception out of <see cref="Step"/>.</b> That type's throw is right — a spend charged to a
    /// pool that does not hold it is a rule applied to the wrong character — but it is a
    /// programming-error guard, and asking for something you cannot afford is not a programming
    /// error: it is an intent the rules refuse. A run that dies on one has no verdict at all.</para>
    /// </summary>
    private bool CannotAfford(
        EncounterState state, Combatant actor, int cost, string ruleId, string sourceRef,
        List<LedgerLine> lines)
    {
        if (actor.Resolve >= cost) return false;

        Refuse(state, actor.Id, ruleId, sourceRef, lines,
            $"{actor.Name} has {actor.Resolve} Resolve and this costs {cost}");

        return true;
    }

    // ── Attacks ──────────────────────────────────────────────────────────────

    private EncounterState ResolveAttack(
        EncounterState state, Attack attack, List<LedgerLine> lines, bool strayShot = false)
    {
        if (NotTheirTurn(state, attack.Actor, lines)) return state;
        if (OutOfTheFight(state, attack.Actor, "actor", lines)) return state;
        if (OutOfTheFight(state, attack.Target, "target", lines)) return state;
        if (GrappleForbids(state, attack.Actor, null, lines)) return state;

        var actor = state[attack.Actor];
        var target = state[attack.Target];
        var entry = _play.GetCombat("attacks_and_defenses");

        var rank = actor.Rank(attack.TraitId);

        if (rank <= 0)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has no rank in {attack.TraitId}, so there is no pool to throw");
        }

        // p.76's two refusals about the object, and p.87's ceiling on what it carries. <b>Before
        // p.75's refusals below, and that ordering is load-bearing.</b> An attack made with
        // something the actor is not holding, or with the object of a standing partial grab, is not
        // an attack at all — so neither refusal below should report on it first — and the rank the
        // cover refusal compares with an obstacle's Structure is the rank the item actually brings
        // to bear, which p.87 caps and Chapter 6's Weapon Bonus then adds to.
        if (attack.Item is { Length: > 0 })
        {
            // p.76's deadlock, and it is checked before the hand is, so both parties are refused
            // for the reason the page gives rather than one of them for a reason it does not.
            if (FightingOverIt(state, actor, attack.Item.Trim(), lines) is { } locked) return locked;
            if (UsingWhatTheyDoNotHold(state, actor, attack.Item.Trim(), lines) is { } empty) return empty;

            rank = GearLimited(state, actor, attack, attack.Item.Trim(), rank, lines);
        }

        // Ch.7 pp.107-108, where the caller named the obstacle instead of rating it: the Structure
        // comes off the page and the ledger cites it. Before the refusal below, because that
        // refusal compares an attack rank with the very figure this resolves.
        if (attack.CoverScenery is { Length: > 0 })
        {
            if (TheCoverIsNamedScenery(state, actor, attack, lines, out var rated) is { } unrated)
            {
                return unrated;
            }

            attack = rated;
        }

        // p.75's cover, and the half of it that is a refusal rather than a penalty. Before the
        // dice, because an attack that cannot be made is one nothing else about should happen to:
        // no team attack recorded against the target, no defences halved by a charge, nothing
        // thrown.
        if (CoverRefused(state, actor, attack, target, rank, lines) is { } hidden) return hidden;
        if (attack.Charge && ChargeRefused(state, actor, attack, lines) is { } refused) return refused;
        if (attack.Team && TeamAttackRefused(state, actor, target, lines) is { } spent) return spent;

        // p.76's "use it ... on that same page".
        //
        // <b>After every refusal above, which is load-bearing rather than tidy.</b>
        // `HeldItem.Used` is what discharges "use it or toss it aside on that same page", so an
        // attack that was refused marking it would keep a weapon past a page turn that should have
        // taken it — a state change bought by a refusal. Moving this block up is the mutation
        // ARefusedAttackDoesNotSpendThePageTheItemWasWonOn exists to catch.
        if (attack.Item is { Length: > 0 })
        {
            state = UseTheItem(state, actor, attack.Item.Trim(), lines);
            actor = state[actor.Id];
        }

        var pool = rank + AttackModifiers(state, actor, attack, strayShot, lines);
        var attackRoll = _counter.Roll(pool, _dice);

        state = CommitToTheAttack(state, actor, attack);

        var (defenceTrait, defencePool, defenceIsActive) = ChooseDefence(state, target, attack, rank, lines);
        var defenceRoll = _counter.Roll(defencePool, _dice);

        var after = defenceIsActive ? CountActiveDefence(state, target.Id) : state;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} attacks {target.Name} with {attack.TraitId} {pool}d for "
            + $"{attackRoll.Successes} successes; {target.Name} defends with {defenceTrait} "
            + $"{defencePool}d for {defenceRoll.Successes}"));

        var resolved = new ResolvedAttack(
            actor.Id, target.Id, pool, attackRoll.Successes, defenceRoll.Successes,
            target, state.Effects, attack.Effect, attack.Area, attack.Damage, rank,
            attackRoll.Faces, attack.Team, attack.TraitId, attack.Type, defenceIsActive);

        if (attack.Team) after = after with { TeamAttacked = [.. after.TeamAttacked, target.Id] };

        after = ApplyAttackOutcome(after, attack, resolved, lines);

        if (attack.Charge && !defenceIsActive)
        {
            after = TheImpactComesBack(
                after, actor, target, attack, attackRoll.Successes, defenceRoll.Successes, lines);
        }

        after = after with { LastAttack = resolved };

        // p.80's Friendly Fire, last: a shot into a melee that landed nothing has to go somewhere,
        // and where it goes is a second attack resolved for real. A stray round never sends another.
        return strayShot ? after : TheShotGoesWide(after, actor, attack, resolved, lines);
    }

    /// <summary>
    /// Whether <c>charge_attacks</c>'s <c>attack_traits</c> admit the Trait this charge is rolling,
    /// and a refusal on the ledger where they do not.
    ///
    /// <para><b>The list was modelled and never read, so a charge could be made with anything at
    /// all</b> — including a mental Power, which is not a thing anybody slams into a target with, and
    /// which collected the +2d and the halved defences anyway.</para>
    ///
    /// <para><b>Three of the entry's four clauses are ids and one is prose, and the prose is where
    /// the reading is.</b> "Density", "Growth" and "any Travel Power" resolve to Trait ids; "any
    /// Trait usable for a close combat attack" does not, and Chapter 2 has no close-combat flag. This
    /// engine reads it as the attack Trait of p.75's two close-combat rows — Unarmed and Melee
    /// Weapon, both Might — which is the narrowest reading that keeps every charge the book
    /// describes legal. <c>docs/guide/play-engine.md</c> records it.</para>
    /// </summary>
    private EncounterState? ChargeRefused(
        EncounterState state, Combatant actor, Attack attack, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("charge_attacks");
        var allowed = ChargeTraits();

        if (allowed.Contains(attack.TraitId)) return null;

        return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{actor.Name} cannot charge with {attack.TraitId}: p.78 allows "
            + $"{string.Join(", ", entry.Charge!.AttackTraits)}, which for this engine is "
            + $"{string.Join(", ", allowed.Order(StringComparer.Ordinal))}");
    }

    /// <summary>
    /// p.79's one-a-battle limit on being team-attacked, refused on the ledger where it bites.
    ///
    /// <para><b>The two ways out of it are a person's decision and are quoted rather than
    /// applied</b> — <c>the_limit_may_be_lifted_by</c> is "the Heroes being clever about it, or the
    /// GM ruling otherwise", and this engine has nobody to ask. A caller who has been told the GM
    /// ruled otherwise attacks without the flag.</para>
    /// </summary>
    private EncounterState? TeamAttackRefused(
        EncounterState state, Combatant actor, Combatant target, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("team_attacks");
        var rule = entry.TeamAttack!;

        var already = state.TeamAttacked.Count(id => string.Equals(id, target.Id, StringComparison.Ordinal));

        if (already < rule.LimitPerTargetPerBattle) return null;

        return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{target.Name} has already been the target of {already} team attack this battle, and "
            + $"p.79 allows {rule.LimitPerTargetPerBattle} — the limit is lifted by "
            + $"{rule.TheLimitMayBeLiftedBy}, neither of which is this engine's to decide");
    }

    /// <summary>Every Trait id a charge may be rolled with, out of the entry and out of p.75's table.</summary>
    private HashSet<string> ChargeTraits()
    {
        var entry = _play.GetCombat("charge_attacks");
        var table = _play.GetCombat("attack_and_defense_table");

        var closeCombat = table.AttackDefenseTable!
            .Where(r => r.Type.Contains("Unarmed", StringComparison.Ordinal)
                        || r.Type.Contains("Melee", StringComparison.Ordinal))
            .Select(r => Normalise(r.AttackTrait));

        var named = entry.Charge!.AttackTraits
            .Where(t => !t.StartsWith("any ", StringComparison.OrdinalIgnoreCase))
            .Select(Normalise);

        return [.. closeCombat, .. named, .. Movement.TravelPowerIds];
    }

    /// <summary>
    /// p.78's price on a charge that meets a braced target: <c>if_the_target_uses_a_passive_defense</c>
    /// — "the charger makes their own passive defense roll against the attack to see whether the
    /// impact hurts them" — less <c>self_damage_reduced_by</c>, "the damage inflicted on the target".
    ///
    /// <para><b>Both fields were modelled and neither was read</b>, so a charge was two free dice
    /// against anybody who stood still, and a balance run would have said charging is always worth
    /// it.</para>
    ///
    /// <para><b>The arithmetic is a reading and the guide records it.</b> The page says the charger
    /// rolls their passive defence "against the attack", which this engine takes to mean against the
    /// attack roll's own successes — the only figure on the table for it — and turns the net into
    /// damage at <c>damage.damage_per_net_success</c>, the one rate the book has. "Reduced by the
    /// damage inflicted on the target" is then a subtraction floored at nothing, because a charge
    /// that hurt the target more than it hurt the charger cannot heal the charger.</para>
    /// </summary>
    private EncounterState TheImpactComesBack(
        EncounterState state, Combatant actor, Combatant target, Attack attack,
        int attackSuccesses, int defenceSuccesses, List<LedgerLine> lines)
    {
        var charge = _play.GetCombat("charge_attacks");
        var damage = _play.GetCombat("damage");
        var rate = damage.Damage!.DamagePerNetSuccess;

        var defenses = _play.GetCombat("active_and_passive_defenses").Defenses!;
        var actives = defenses.CommonActiveTraits.Select(Normalise).ToHashSet(StringComparer.Ordinal);

        var own = actor.Defences
            .Where(d => !actives.Contains(d))
            .Select(d => (Trait: d, Rank: actor.Rank(d)))
            .OrderByDescending(d => d.Rank)
            .ThenBy(d => d.Trait, StringComparer.Ordinal)
            .FirstOrDefault();

        var roll = _counter.Roll(own.Rank, _dice);

        var inflicted = attack.Effect is null
                        && target.Kind != CombatantKind.MinionGroup
                        && attackSuccesses - defenceSuccesses > 0
            ? (attackSuccesses - defenceSuccesses) * rate
            : 0;

        var impact = Math.Max(0, (attackSuccesses - roll.Successes) * rate - inflicted);
        var health = Math.Max(
            state.Table.FatalDamage ? int.MinValue : damage.Damage.DefeatedAtHealth,
            state[actor.Id].CurrentHealth - impact);

        lines.Add(new LedgerLine(
            state.Page, actor.Id, charge.Id, charge.SourceRef,
            $"{target.Name} braced, so {charge.Charge!.IfTheTargetUsesAPassiveDefense}: "
            + $"{actor.Name} answers their own {attackSuccesses} with "
            + $"{(own.Trait is null ? "no passive defence" : $"{own.Trait} {own.Rank}d")} for "
            + $"{roll.Successes}, and the impact of {(attackSuccesses - roll.Successes) * rate} less "
            + $"{charge.Charge.SelfDamageReducedBy} ({inflicted}) leaves them on {health} Health"));

        if (impact == 0) return state;

        // p.80's Slow Healing: a charger standing on nothing is a character in that condition, and
        // the impact of their own charge is damage like any other.
        var charger = state[actor.Id];
        var hurt = charger.WithHealth(health);

        return state.With(AnyDamageAtAllPutsThemDown(state, charger, impact, lines) ? hurt.Overcome() : hurt);
    }

    /// <summary>
    /// p.80's Slow Healing, the sharp end of the sentence that let a character stand up at all:
    /// somebody conscious at or below the figure that defeats them "is defeated if you take even a
    /// single point of damage in this condition".
    ///
    /// <para><b>It lives here rather than inside one damage path because there is more than
    /// one.</b> An attack's damage is the obvious one and was the only one this test was made in;
    /// p.78's charge hurts the <em>charger</em> through <see cref="TheImpactComesBack"/>, which
    /// writes a Health of its own and never went near it. A character on their feet at the defeat
    /// figure could charge a braced opponent, take the impact on the ledger, and still be standing
    /// — and with Fatal Damage off the Health clamps at the floor they were already on, so the
    /// damage left no trace in the state at all.</para>
    ///
    /// <para><b>"Even a single point" is the threshold and nothing softer.</b> Zero damage is not a
    /// point: an attack that landed nothing, and an effect that does no damage at all, leave them
    /// standing — which is the control the fixtures for this rule are written around.</para>
    ///
    /// <para>Taking the state away is the whole of the consequence — see
    /// <see cref="Combatant.Overcome"/> — so this returns whether it should be taken and the caller
    /// applies it to whatever it was about to store.</para>
    /// </summary>
    private bool AnyDamageAtAllPutsThemDown(
        EncounterState state, Combatant victim, int damage, List<LedgerLine> lines)
    {
        if (damage <= 0 || !victim.ConsciousAtZeroOrLess || !state.Table.SlowHealing) return false;

        var slow = _play.GetGritty("gritty_slow_healing");

        lines.Add(new LedgerLine(
            state.Page, victim.Id, slow.Id, slow.SourceRef,
            $"{victim.Name} was on their feet at {victim.CurrentHealth} Health under Slow "
            + $"Healing, and p.80 puts them down again for {damage} point"
            + (damage == 1 ? "" : "s") + " of damage: any at all does it"));

        return true;
    }

    /// <summary>
    /// The half of going all-out and of charging that costs something: the attacker's own defences
    /// are halved until after their next turn.
    ///
    /// <para><b>The bonus without the cost is not the rule</b>, and a measurement made under half of
    /// it would say all-out attacking is better than the book makes it. Going all-out halves every
    /// defence; a charge halves only the active ones. The page says "until after your next turn to
    /// act" and this expires it at the end of the following page — that sentence to within a turn,
    /// and recorded as a reading in the guide.</para>
    /// </summary>
    private static EncounterState CommitToTheAttack(EncounterState state, Combatant actor, Attack attack)
    {
        if (!attack.AllOut && !attack.Charge) return state;

        var halved = new Dictionary<string, DefencePenalty>(state.DefencesHalved, StringComparer.Ordinal)
        {
            [actor.Id] = new(state.Page + 1, ActiveOnly: !attack.AllOut)
        };

        return state with { DefencesHalved = halved };
    }

    /// <summary>
    /// Everything that moves an attack pool off the Trait's own rank: the two-dice bonuses for going
    /// all-out and for charging, the Minion group's size bonus, and the wound penalty.
    ///
    /// <para>Every figure is the entry's. Going all-out and charging also cost the attacker their
    /// own defences until after their next turn, which is recorded here rather than in the ledger's
    /// prose — see <see cref="EncounterState.DefencesHalved"/>.</para>
    /// </summary>
    private int AttackModifiers(
        EncounterState state, Combatant actor, Attack attack, bool strayShot, List<LedgerLine> lines)
    {
        var modifier = 0;

        if (attack.AllOut)
        {
            var all = _play.GetCombat("going_all_out");
            modifier += all.AllOutAttack!.AttackBonusDice;

            lines.Add(new LedgerLine(
                state.Page, actor.Id, all.Id, all.SourceRef,
                $"{actor.Name} goes all-out: +{all.AllOutAttack.AttackBonusDice}d, and every defence "
                + "of theirs is halved until after their next turn"));
        }

        if (attack.Charge)
        {
            var charge = _play.GetCombat("charge_attacks");
            modifier += charge.Charge!.AttackBonusDice;

            lines.Add(new LedgerLine(
                state.Page, actor.Id, charge.Id, charge.SourceRef,
                $"{actor.Name} charges: +{charge.Charge.AttackBonusDice}d, and their active defences "
                + "are halved until after their next turn"));
        }

        if (attack.Team)
        {
            var team = _play.GetCombat("team_attacks");
            var rule = team.TeamAttack!;

            modifier += rule.AttackBonusDice;

            lines.Add(new LedgerLine(
                state.Page, actor.Id, team.Id, team.SourceRef,
                $"{actor.Name} attacks as part of a team attack: +{rule.AttackBonusDice}d, and "
                + $"{rule.CostResolveToMakeSixesExplode} Resolve afterwards would make the sixes "
                + $"explode. That the participants {rule.ParticipantsActAt} and "
                + $"{(rule.AllParticipantsMustTargetTheSameEnemy ? "all target the same enemy" : "need not agree a target")} "
                + "is not applied: a step here is one character's action, and this engine has no "
                + "intent that binds several"));
        }

        if (actor.Kind == CombatantKind.MinionGroup)
        {
            var table = _play.GetCombat("minion_group_attack_table");
            var group = _play.GetCombat("minions_attacking");
            var rule = group.MinionsAttacking!;

            // p.77 caps how many of a mob can reach one target, and the cap depends on whether they
            // have to reach them or merely shoot at them. A group of twenty in close combat is six
            // for the purpose of the size bonus, because the other fourteen are not on them.
            var close = state.RangeBetween(actor.Id, attack.Target) == RangeBand.Close;
            var cap = close
                ? rule.MaximumAttackingOneTargetInCloseCombat
                : rule.MaximumAttackingOneTargetAtRange;

            var attacking = Math.Min(actor.GroupSize, cap);

            if (attacking < actor.GroupSize)
            {
                lines.Add(new LedgerLine(
                    state.Page, actor.Id, group.Id, group.SourceRef,
                    $"{actor.GroupSize} Minions cannot all reach one target: p.77 allows {cap} "
                    + $"{(close ? "in close combat" : "at range")}, so {attacking} of them attack"));
            }

            var row = table.MinionGroupAttack!
                .FirstOrDefault(r => attacking >= r.MinMinions && attacking <= r.MaxMinions);

            if (row is not null)
            {
                modifier += row.BonusDice;

                // <b>The line used to say "on the attack roll and nothing else", which claimed a rule
                // this engine does not apply.</b> The entry's second clause — the bonus does not
                // count towards penetrating cover or harming somebody behind Armor or a Force Field —
                // is carried by the entry's own `ambiguity`: both of those are decided by the same
                // attack roll the bonus is granted to, so read strictly it asks for two totals
                // against one defence roll and the page offers no mechanism for that. It is on the
                // guide's not-applied list, and the line says so rather than implying otherwise.
                lines.Add(new LedgerLine(
                    state.Page, actor.Id, table.Id, table.SourceRef,
                    $"{attacking} Minions attacking as a group is +{row.BonusDice}d, which p.77 "
                    + $"applies to {rule.TheGroupBonusAppliesTo} — that it does not apply to "
                    + $"{rule.TheGroupBonusDoesNotApplyTo} is not yet implemented"));
            }
        }

        // p.75's MODIFIERS, on the attacking half of the exchange: what is between the two of them,
        // and what the light is like. Size is the defender's and is applied on the other side.
        modifier += CoverPenalty(state, actor, attack, lines);
        modifier += VisibilityPenalty(state, actor, state[attack.Target], "attack", lines);

        // p.80's Hard Targets, on the attacking half: the price of aiming at a weak point. The
        // doubling it buys off is applied on the other side of the roll, in ChooseDefence.
        modifier += HardTargetNegation(state, actor, attack, lines);

        // p.80's Friendly Fire: four dice for shooting into a scrum, and nothing at all for the
        // round that has already gone wide.
        modifier += FriendlyFire(state, actor, attack, strayShot, lines);

        modifier += WoundPenalty(state, actor, lines);

        return modifier;
    }


    // ── p.75's three situational modifiers ────────────────────────────────

    /// <summary>
    /// The label a ledger line gives the cover's own durability when it answers an attack.
    ///
    /// <para>It is a name for a thing this engine has no catalogue of rather than a Trait id: there
    /// is no scenery here, and the Structure is a number the caller supplied.</para>
    /// </summary>
    private const string CoversStructure = "the cover's Structure";

    /// <summary>A signed count of dice, for a ledger line: <c>+2d</c>, <c>-1d</c>, <c>0d</c>.</summary>
    private static string Dice(int dice) => dice > 0 ? $"+{dice}d" : $"{dice}d";

    /// <summary>
    /// One band of <c>modifier_cover</c>, by the word p.75 prints for it.
    ///
    /// <para><b>The dice are never typed here.</b> The band is found by its printed word and its
    /// <c>dice</c> is whatever the file says; a band this engine names and the file has stopped
    /// printing is a throw, because a modifier silently worth nothing is the shape of defect this
    /// whole store exists to prevent.</para>
    /// </summary>
    private CombatBandModel CoverBand(Cover cover)
    {
        var entry = _play.GetCombat("modifier_cover");
        var printed = PrintedCover(cover);

        return entry.Cover!.Bands.SingleOrDefault(b =>
                   string.Equals(b.Cover, printed, StringComparison.Ordinal))
               ?? throw new InvalidOperationException(
                   $"modifier_cover prints no band called '{printed}'. Its bands are "
                   + string.Join(", ", entry.Cover.Bands.Select(b => b.Cover))
                   + ", and this engine reads a band's dice off the file rather than carrying its "
                   + "own copy of the table.");
    }

    /// <summary>One band of <c>modifier_visibility</c>, by the word p.75 prints for it.</summary>
    private CombatBandModel VisibilityBand(Visibility visibility)
    {
        var entry = _play.GetCombat("modifier_visibility");
        var printed = PrintedVisibility(visibility);

        return entry.Visibility!.Bands.SingleOrDefault(b =>
                   string.Equals(b.Visibility, printed, StringComparison.Ordinal))
               ?? throw new InvalidOperationException(
                   $"modifier_visibility prints no band called '{printed}'. Its bands are "
                   + string.Join(", ", entry.Visibility.Bands.Select(b => b.Visibility))
                   + ", and this engine reads a band's dice off the file rather than carrying its "
                   + "own copy of the table.");
    }

    /// <summary>The word p.75 prints for a band of cover. <see cref="Cover.Complete"/> has none.</summary>
    private static string PrintedCover(Cover cover) => cover switch
    {
        Cover.Light => "light",
        Cover.Heavy => "heavy",
        Cover.AlmostFull => "almost full",
        var other => throw new ArgumentOutOfRangeException(
            nameof(cover), other, "p.75's cover table prices three bands, and that is not one of them.")
    };

    /// <summary>The word p.75 prints for a band of visibility. <see cref="Visibility.Clear"/> has none.</summary>
    private static string PrintedVisibility(Visibility visibility) => visibility switch
    {
        Visibility.Poor => "poor",
        Visibility.None => "none",
        var other => throw new ArgumentOutOfRangeException(
            nameof(visibility), other, "p.75's visibility table prices two bands, and that is not one of them.")
    };

    /// <summary>
    /// p.75's two refusals about cover, both taken before anything is rolled.
    ///
    /// <para><b>A completely hidden target cannot be hit</b> — <c>a_completely_hidden_target_cannot_be_hit</c>
    /// — and the page's own way past that is to attack <em>through</em> the obstacle, which
    /// <c>attacking_through_cover_requires</c> prices at an attack rank greater than the cover's
    /// Structure. Declaring a Structure is the caller saying the shot goes through; without one
    /// there is nothing to shoot at and nothing is thrown.</para>
    ///
    /// <para><b>The rank test is applied at every band and not only at the last one</b>, because
    /// what it gates is the shot going through a solid thing, and a wall does not become permeable
    /// because some of the target is sticking out from behind it. A caller who means to shoot at the
    /// exposed part of a partly-covered target leaves the Structure out and pays the band alone;
    /// this is the same field doing both jobs, and it is the declaration rather than a fact about
    /// the wall. <c>docs/guide/play-engine.md</c> records the reading.</para>
    /// </summary>
    private EncounterState? CoverRefused(
        EncounterState state, Combatant actor, Attack attack, Combatant target, int attackRank,
        List<LedgerLine> lines)
    {
        if (attack.Cover == Cover.None && attack.CoverStructure is null) return null;

        var entry = _play.GetCombat("modifier_cover");
        var rule = entry.Cover!;

        if (attack.CoverStructure is not { } structure)
        {
            if (attack.Cover != Cover.Complete || !rule.ACompletelyHiddenTargetCannotBeHit) return null;

            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{target.Name} is completely hidden behind cover, and p.75 says such a target "
                + $"cannot be hit. The way through is to attack through the cover, which requires "
                + $"{rule.AttackingThroughCoverRequires} — say what the cover's Structure is. "
                + "Nothing was rolled.");
        }

        if (GetsThrough(attackRank, structure)) return null;

        return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{actor.Name} attacks {target.Name} through cover of Structure {structure} with "
            + $"{attack.TraitId} at rank {attackRank}d, and p.75 requires "
            + $"{rule.AttackingThroughCoverRequires}. Nothing was rolled.");
    }

    /// <summary>
    /// <c>modifier_cover</c>'s band, on the attack roll.
    ///
    /// <para><b>Complete cover contributes nothing here on purpose.</b> The printed table stops at
    /// "almost full"; what the page says about a target hidden altogether is that you cannot hit
    /// one, which <see cref="CoverRefused"/> has already applied, and an attack that got past it is
    /// one going through the obstacle rather than one shooting at a fraction of a target.</para>
    /// </summary>
    private int CoverPenalty(EncounterState state, Combatant actor, Attack attack, List<LedgerLine> lines)
    {
        if (attack.Cover is Cover.None or Cover.Complete) return 0;

        var entry = _play.GetCombat("modifier_cover");
        var band = CoverBand(attack.Cover);
        var target = state[attack.Target];

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"cover affects {entry.Cover!.Affects}: {target.Name} has {band.Cover} cover, so "
            + $"{actor.Name}'s attack is {Dice(band.Dice)}"));

        return band.Dice;
    }

    /// <summary>
    /// <c>modifier_size</c>, on the defender's active defence roll and on nothing else.
    ///
    /// <para><b>The factor is derived from two sizes and never taken as a band</b>: p.75's bands are
    /// "at least twice your size" and "no more than one-fifth your size", which are comparisons, and
    /// a caller handing over a pre-computed band would be handing over the answer. The dice are the
    /// file's; what this method supplies is what the four English phrases mean as a comparison, in
    /// the same shape as <c>seize_initiative_gm_alternative</c>'s "doubles" — a phrase the engine
    /// requires to still be there and throws on if it is not.</para>
    ///
    /// <para><b>Where two bands both apply, the narrower one wins.</b> An attacker five times your
    /// size is also twice your size, and the page plainly means the larger bonus; taking the band
    /// with the greater magnitude is that, and it is recorded as a reading in the guide.</para>
    /// </summary>
    private int SizeModifier(
        EncounterState state, Combatant defender, Combatant attacker, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("modifier_size");
        var rule = entry.Size!;

        var factor = attacker.Size / defender.Size;

        var band = rule.Bands
            .Where(b => SizeBandApplies(b.AttackerRelativeSize!, factor))
            .OrderByDescending(b => Math.Abs(b.Dice))
            .ThenBy(b => b.AttackerRelativeSize, StringComparer.Ordinal)
            .FirstOrDefault();

        if (band is null) return 0;

        lines.Add(new LedgerLine(
            state.Page, defender.Id, entry.Id, entry.SourceRef,
            $"size affects {rule.Affects}: {attacker.Name} is {factor:0.##}× {defender.Name}'s size, "
            + $"which is \"{band.AttackerRelativeSize}\", so {defender.Name}'s active defence is "
            + $"{Dice(band.Dice)}"));

        return band.Dice;
    }

    /// <summary>
    /// What one of p.75's four size phrases means as a comparison of two sizes.
    ///
    /// <para><b>The threshold is here and the dice are not, and the split is the point.</b> "twice",
    /// "5 times", "half" and "one-fifth" are English printed inside a band's own label, and no
    /// amount of data modelling extracts a number from them — so this engine reads the phrase and
    /// supplies the comparison, which is a reading and is recorded as one. A band whose phrase this
    /// does not know is a throw rather than a band quietly skipped, because a modifier that silently
    /// stopped applying is exactly the failure a ledger exists to make impossible.</para>
    /// </summary>
    private static bool SizeBandApplies(string printed, double factor) => printed switch
    {
        "at least twice your size" => factor >= 2,
        "at least 5 times your size" => factor >= 5,
        "no more than half your size" => factor <= 1d / 2,
        "no more than one-fifth your size" => factor <= 1d / 5,
        _ => throw new InvalidOperationException(
            $"modifier_size prints a band called '{printed}', and this engine does not know what "
            + "that phrase means as a comparison of two sizes. The four it knows are p.75's; a new "
            + "or reworded one is a rule it cannot apply, and applying nothing quietly would be "
            + "worse. See docs/guide/play-engine.md's readings table.")
    };

    /// <summary>
    /// <c>modifier_visibility</c>, on whichever roll is being made — p.75 costs an attack roll and
    /// an active defence roll alike, so this is called from both sides of the same exchange.
    ///
    /// <para><b>Two things can make the visibility bad and the worse of them wins.</b> The scene's
    /// light is <see cref="EncounterState.Visibility"/>; an opponent nobody can see is
    /// <see cref="Combatant.Invisible"/>, which
    /// <c>an_invisible_opponent_counts_as_no_visibility</c> makes equivalent to the worst band there
    /// is. Both are read rather than assumed, so an entry that stopped saying the second moves this
    /// with it.</para>
    ///
    /// <para><b>A compensating Power removes the penalty and the line says which one did.</b>
    /// <c>powers_that_compensate_given</c> names Blind Fighting and Radar; the field is named
    /// <em>given</em> because the printed sentence says "a Power that compensates for this, like
    /// Blind Fighting or Radar", so the list is examples rather than a closed set — a GM who rules
    /// that some other Power compensates says so by not putting the character in the dark. The
    /// ledger line says the list is the page's examples, so a reader is never left thinking this
    /// engine adjudicated the question.</para>
    /// </summary>
    private int VisibilityPenalty(
        EncounterState state, Combatant roller, Combatant opponent, string roll, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("modifier_visibility");
        var rule = entry.Visibility!;

        var unseen = opponent.Invisible && rule.AnInvisibleOpponentCountsAsNoVisibility;
        var effective = unseen ? Visibility.None : state.Visibility;

        if (effective == Visibility.Clear) return 0;

        var because = unseen
            ? $"{opponent.Name} cannot be seen, which p.75 counts as no visibility"
            : $"the visibility here is {PrintedVisibility(effective)}";

        if (Compensating(roller) is { } power)
        {
            lines.Add(new LedgerLine(
                state.Page, roller.Id, entry.Id, entry.SourceRef,
                $"{because}, and {roller.Name} has {power}, which p.75 gives as a Power that "
                + $"compensates — so their {roll} roll is unpenalised"));

            return 0;
        }

        var band = VisibilityBand(effective);

        lines.Add(new LedgerLine(
            state.Page, roller.Id, entry.Id, entry.SourceRef,
            $"visibility affects {rule.Affects}: {because}, so {roller.Name}'s {roll} roll is "
            + $"{Dice(band.Dice)}"));

        return band.Dice;
    }

    /// <summary>
    /// The Power on this combatant's sheet that p.75 gives as compensating for not being able to
    /// see, in the book's own spelling, or null.
    ///
    /// <para><b>It is read off <see cref="Combatant.Powers"/> and not off a rank</b>, because Blind
    /// Fighting and Radar are default-rank Powers and the character engine answers 0 for both by
    /// design — so a rank cannot tell "has it" from "has never bought it".</para>
    /// </summary>
    private string? Compensating(Combatant combatant)
    {
        var rule = _play.GetCombat("modifier_visibility").Visibility!;

        return rule.PowersThatCompensateGiven
            .FirstOrDefault(printed => combatant.Powers.Contains(Normalise(printed)));
    }

    // ── The Gritty Combat Rules, pp.79-81 ─────────────────────────────────

    /// <summary>
    /// p.80's Hard Targets, on the attacking half of the exchange:
    /// <c>penalty_dice_to_negate_it</c> for an attacker who aims at
    /// <c>negation_available_against</c> instead of at the thing as a whole.
    ///
    /// <para><b>A declaration that buys nothing says so rather than costing nothing in
    /// silence.</b> <see cref="Attack.VulnerablePart"/> on a target who is not a hard target, or in
    /// a fight that never took the setting, is a caller believing they paid for something; the
    /// penalty is not taken and the line says which of the two reasons it was. A flag accepted and
    /// quietly ignored is the shape of defect this whole engine is written against.</para>
    ///
    /// <para><b>Whether the target is complex enough to <em>have</em> a weak point is the GM's, and
    /// the line says so.</b> The page offers the negation "against the vulnerable parts of a
    /// complex machine or vehicle" — narrower than the doubling, which covers thick inanimate
    /// objects as well — and <see cref="Combatant.HardTarget"/> is one flag rather than two. A
    /// second flag for "complex" would be a distinction nothing on a sheet backs, so this engine
    /// applies the negation wherever the doubling applies and hands the narrower question back
    /// rather than deciding it. <c>docs/guide/play-engine.md</c> records the reading.</para>
    /// </summary>
    private int HardTargetNegation(
        EncounterState state, Combatant actor, Attack attack, List<LedgerLine> lines)
    {
        if (!attack.VulnerablePart) return 0;

        var entry = _play.GetGritty("gritty_hard_targets");
        var rule = entry.HardTargets!;
        var target = state[attack.Target];

        if (!state.Table.HardTargets || !target.HardTarget)
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{actor.Name} aims at a vulnerable part of {target.Name} and there is nothing to "
                + "negate: "
                + (state.Table.HardTargets
                    ? $"{target.Name} is none of {rule.AppliesTo}"
                    : "this table did not take Hard Targets")
                + $". The {Dice(rule.PenaltyDiceToNegateIt)} was not taken"));

            return 0;
        }

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} aims at {rule.NegationAvailableAgainst}: {Dice(rule.PenaltyDiceToNegateIt)}, "
            + $"and {target.Name}'s passive defence is not {rule.PassiveDefenseRank}. Whether this "
            + "one is complex enough to have a weak point is the GM's call, not this engine's"));

        return rule.PenaltyDiceToNegateIt;
    }

    /// <summary>
    /// What p.80's Hard Targets multiplies a passive defence rank by:
    /// <c>passive_defense_rank</c>, which is the printed word "doubled".
    ///
    /// <para><b>The word is read and the factor is supplied here</b>, in the same shape as
    /// <c>seize_initiative_gm_alternative</c>'s "doubles" and for the same reason: the entry states
    /// an effect in prose rather than a multiplier, so an entry that has stopped saying it is a rule
    /// this engine cannot apply and it throws rather than going on doubling.</para>
    ///
    /// <para><b>It multiplies the rank before either printed halving, and the order is not
    /// arithmetic.</b> The page doubles a hard target's passive defence <em>rank</em>, which is the
    /// figure on the sheet; the table's <c>1/2 Toughness</c> and <c>lethal_and_subdual</c>'s lethal
    /// clause then halve what the rules say answers this attack. Doubling afterwards would put an
    /// odd rank through <see cref="Halve"/> first and hand back one die more than the page allows —
    /// a Toughness of 7 halves to 4 and doubles to 8, where the page's own order gives 14 and then
    /// 7.</para>
    ///
    /// <para><b>Active defences never double</b>, because the page says passive: dodging a tank is
    /// no harder for the tank being a tank. And a <see cref="Attack.CoverStructure"/> is left alone
    /// too — a wall is plainly one of the "thick, inanimate objects" the rule names, but the
    /// declaration is made on a <em>combatant</em> and a wall is not one. The Structure is a number
    /// the caller supplies, so a caller who wants a doubled wall doubles the number.</para>
    /// </summary>
    private int HardTargetFactor(
        EncounterState state, Combatant target, Attack attack, List<LedgerLine> lines)
    {
        if (!state.Table.HardTargets || !target.HardTarget || attack.VulnerablePart) return 1;

        var entry = _play.GetGritty("gritty_hard_targets");
        var rule = entry.HardTargets!;

        if (!rule.PassiveDefenseRank.Contains("double", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"gritty_hard_targets now says a hard target's passive defence rank is "
                + $"'{rule.PassiveDefenseRank}'. This engine reads the printed word \"doubled\" and "
                + "supplies the factor of 2 itself, because the entry states the effect in prose "
                + "rather than as a multiplier; a rule that no longer says it is a rule this engine "
                + "cannot apply. See docs/guide/play-engine.md's readings table.");
        }

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"{target.Name} is one of {rule.AppliesTo}, so every passive defence of theirs answers "
            + $"at a {rule.PassiveDefenseRank} rank; the active ones do not move. p.80 also "
            + $"recommends the {rule.RecommendedProForVehicleScaleWeapons} Pro on vehicle-scale "
            + $"weapons and the {rule.RecommendedProForThePhysicalAttacksOfPowerfulSuperhumanCharacters} "
            + "Pro on the physical attacks of powerful superhuman characters, which is advice about "
            + "how characters are built rather than a rule of this fight, and is not applied"));

        return 2;
    }

    /// <summary>
    /// Whether p.75's table calls this a <b>ranged</b> attack — which for the two Power rows is a
    /// question only the Power's own Ch.2 Range can answer.
    ///
    /// <para><b>Every part of it is derived from the table rather than listed here.</b> A row whose
    /// printed type names <em>Ranged</em> is a ranged attack; the rows whose attacking Trait is the
    /// table's bare <c>Power</c> column are the ones the table declines to classify, and for those
    /// the answer is on the sheet, in <see cref="Combatant.RangedPowers"/>. Everything left is one
    /// of p.73's close combat attacks — a fist or a swung weapon — which
    /// <c>close_combat_attacks_require</c> puts against an adjacent target and nowhere else.</para>
    /// </summary>
    private bool IsARangedAttack(Combatant attacker, Attack attack)
    {
        var table = _play.GetCombat("attack_and_defense_table").AttackDefenseTable!;
        var printed = PrintedType(attack.Type);

        var row = table.SingleOrDefault(r => string.Equals(r.Type, printed, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"attack_and_defense_table has no row '{printed}', so this engine cannot tell "
                + "whether p.79's Close Range rule reaches this attack.");

        if (row.Type.Contains("Ranged", StringComparison.OrdinalIgnoreCase)) return true;

        return TheRowLeavesItToTheSheet(attack) && attacker.RangedPowers.Contains(attack.TraitId);
    }

    /// <summary>
    /// Whether p.75's table declines to classify this attack's reach — which is exactly the rows
    /// whose attacking Trait is the bare <c>Power</c> column, and is where
    /// <see cref="Combatant.RangedPowers"/> is consulted instead.
    ///
    /// <para><b>It is separate from <see cref="IsARangedAttack"/> because the ledger needs the
    /// question as well as the answer.</b> A fist that collects no Close Range penalty collected
    /// none because p.73 puts a close combat attack against an adjacent target and the rule was
    /// never about it; a Power that collects none collected none because this engine read the
    /// Power's own Ch.2 Range and decided. The second is a reading and says so on the line.</para>
    /// </summary>
    private bool TheRowLeavesItToTheSheet(Attack attack)
    {
        var table = _play.GetCombat("attack_and_defense_table").AttackDefenseTable!;
        var printed = PrintedType(attack.Type);

        var row = table.SingleOrDefault(r => string.Equals(r.Type, printed, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"attack_and_defense_table has no row '{printed}', so this engine cannot tell "
                + "whether p.79's Close Range rule reaches this attack.");

        return string.Equals(row.AttackTrait, PowerColumn, StringComparison.Ordinal);
    }

    /// <summary>
    /// p.79's Close Range penalty: <c>penalty_dice_to_active_defense</c> on the dodger when a
    /// ranged attack is made from inside the nearest band.
    ///
    /// <para><b>Three facts have to line up and each is read where it lives.</b> That the pair is
    /// at Close Range is <see cref="EncounterState.Ranges"/>, which is pairwise because p.73's
    /// bands are. That the attack is ranged comes off p.75's table and, for a Power, off the
    /// sheet — see <see cref="IsARangedAttack"/>. That such an attack <em>can</em> be used at
    /// Distant or Extreme Range is <c>range_classes</c>' own <c>ranged_attacks_reach</c>, which is
    /// the rule the entry's <c>applies_only_to_attacks_usable_at</c> is asking about; both strings
    /// are read and a pair that no longer agrees is a throw rather than a penalty applied on
    /// nothing.</para>
    ///
    /// <para><b>The exception is the caller's, and now the item's too.</b> Thrown weapons are
    /// printed as an exception to the reach, not as the reach — so <see cref="Attack.CloseRangeOnly"/>
    /// is what turns the penalty off, and a declaration that turned nothing off says so. <b>This
    /// paragraph used to end "there is no equipment in a fight here, so nothing else could tell a
    /// pistol from a throwing knife", and that stopped being true</b> when an attack started naming
    /// an item and Chapter 6 started saying what each printed weapon is: an item whose row carries
    /// the <c>Thrown</c> feature is exempt whether or not the flag was set. See
    /// <c>ThrownItemIsExempt</c>.</para>
    ///
    /// <para>It moves an <b>active</b> defence and nothing else, because the entry says
    /// <c>penalty_dice_to_active_defense</c>: a soak is a soak whatever is being shot at you.</para>
    /// </summary>
    private int RangedUpClose(
        EncounterState state, Combatant target, Combatant attacker, Attack attack, List<LedgerLine> lines)
    {
        if (!state.Table.CloseRangePenalty) return 0;

        var entry = _play.GetGritty("gritty_close_range");
        var rule = entry.CloseRangePenalty!;

        var ranged = IsARangedAttack(attacker, attack);

        var close = state.RangeBetween(attacker.Id, target.Id) == RangeBand.Close;

        if (!ranged || !close)
        {
            if (attack.CloseRangeOnly)
            {
                lines.Add(new LedgerLine(
                    state.Page, target.Id, entry.Id, entry.SourceRef,
                    $"{attacker.Name}'s attack is declared one of {rule.IgnoredFor}, and the rule "
                    + $"would not have reached it anyway: {(ranged ? "the two are not at Close Range" : "this is not a ranged attack")}. "
                    + $"{target.Name}'s defence is unmoved"));
            }

            // <b>A Power the reading declined to call ranged says so.</b> Everything else that
            // lands here is a fist or a swung weapon, which p.73 puts against an adjacent target
            // and which the rule was never about; a Power row is the case where this engine made a
            // choice, and a choice made in silence is one nobody can argue with. See
            // TheRowLeavesItToTheSheet.
            if (!ranged && close && TheRowLeavesItToTheSheet(attack))
            {
                lines.Add(new LedgerLine(
                    state.Page, target.Id, entry.Id, entry.SourceRef,
                    $"{attacker.Name} is inside Close Range of {target.Name}, and p.75's table leaves "
                    + $"it to the sheet whether {attack.TraitId} is one of the {rule.AppliesAgainst} "
                    + $"— its own Ch.2 Range is not ranged, so it is not one this engine will apply "
                    + "a penalty to. Only ranged counts: self and touch plainly cannot be used at a "
                    + "distance, and zone and special are neither said to nor said not to, so the "
                    + "narrow reading is the one that never costs a dodger dice the page may not "
                    + $"have meant them to lose. {target.Name}'s active defence keeps its dice"));
            }

            return 0;
        }

        if (attack.CloseRangeOnly)
        {
            lines.Add(new LedgerLine(
                state.Page, target.Id, entry.Id, entry.SourceRef,
                $"p.79 says to ignore this rule for {rule.IgnoredFor}, and {attacker.Name}'s attack "
                + $"is declared one: {target.Name}'s active defence keeps its dice"));

            return 0;
        }

        // And the item's own printed row says the same thing the declaration does, where the caller
        // named one: p.79 ignores the rule for thrown weapons, and Chapter 6 prints which weapons
        // those are. See ThrownItemIsExempt.
        if (ThrownItemIsExempt(state, target, attacker, attack, entry, lines)) return 0;

        ReachIsStillPrinted(rule.AppliesOnlyToAttacksUsableAt);

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"{rule.AppliesAgainst}: {attacker.Name} is inside Close Range of {target.Name}, and a "
            + $"ranged attack reaches {_play.GetCombat("range_classes").RangeRules!.RangedAttacksReach}, "
            + $"which is one of {rule.AppliesOnlyToAttacksUsableAt} — so {target.Name}'s active "
            + $"defence is {Dice(rule.PenaltyDiceToActiveDefense)}"));

        return rule.PenaltyDiceToActiveDefense;
    }

    /// <summary>
    /// That a ranged attack still reaches past the nearest band, which is the whole of what makes
    /// p.79's rule apply to one.
    ///
    /// <para><b>Both halves are read and neither is typed.</b> The entry asks for an attack usable
    /// at <c>applies_only_to_attacks_usable_at</c>; <c>range_classes</c> says how far a ranged
    /// attack reaches. Where the two stop naming a band past Close, this engine is being asked to
    /// apply a penalty whose own condition it can no longer establish, so it throws rather than
    /// applying it on nothing — the band names are <see cref="RangeBand"/>'s, which are p.73's.
    /// </para>
    /// </summary>
    private void ReachIsStillPrinted(string required)
    {
        var reach = _play.GetCombat("range_classes").RangeRules!.RangedAttacksReach;

        var beyondClose = Enum.GetNames<RangeBand>()
            .Where(band => !string.Equals(band, nameof(RangeBand.Close), StringComparison.Ordinal))
            .ToList();

        var named = beyondClose.Where(band =>
            required.Contains(band, StringComparison.OrdinalIgnoreCase)
            && reach.Contains(band, StringComparison.OrdinalIgnoreCase));

        if (named.Any()) return;

        throw new InvalidOperationException(
            $"gritty_close_range applies only to attacks usable at '{required}', and range_classes "
            + $"says a ranged attack reaches '{reach}'. The two no longer name a range class past "
            + $"{nameof(RangeBand.Close)} between them, so this engine cannot establish the rule's "
            + "own condition and will not apply the penalty on nothing. See "
            + "docs/guide/play-engine.md's readings table.");
    }

    /// <summary>
    /// Everybody else in the melee p.80's Friendly Fire rule is about: the characters at Close
    /// Range with the target, who are neither the target nor the attacker and are still in the
    /// fight.
    ///
    /// <para><b>"Engaged in close combat or otherwise bunched up with other characters" is derived
    /// from the range bands rather than declared</b>, and it can be, because p.73's bands are
    /// pairwise: a target at Close Range with somebody who is not the person shooting at them is a
    /// target with somebody else close enough to catch a stray round. That is the whole of what the
    /// clause needs, and nothing on a sheet or in an intent could say it better.</para>
    ///
    /// <para><b>Defeated characters are not in the melee</b>, for the reason they are not the actor
    /// or the target of anything: a body on the floor is not somebody a shot can go wide into, and
    /// the second attack this list feeds would be refused against one anyway.</para>
    ///
    /// <para>Ordered by id, because the second attack picks out of it with a die and a run has to
    /// give the same answer twice.</para>
    /// </summary>
    private List<Combatant> Melee(EncounterState state, Combatant actor, Attack attack)
    {
        if (!state.Table.FriendlyFire || !IsARangedAttack(actor, attack)) return [];

        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        return
        [
            .. state.Combatants.Values
                .Where(c => !string.Equals(c.Id, actor.Id, StringComparison.Ordinal)
                            && !string.Equals(c.Id, attack.Target, StringComparison.Ordinal)
                            && !c.Defeated(floor)
                            && state.RangeBetween(c.Id, attack.Target) == RangeBand.Close)
                .OrderBy(c => c.Id, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// p.80's Friendly Fire, on the attack roll: <c>penalty_dice</c> for shooting into a scrum, and
    /// <c>second_attack_penalty_dice</c> — which is nothing — for the shot that went wide.
    ///
    /// <para><b>Both figures are the entry's and the second is applied rather than assumed away.</b>
    /// "This time at no penalty" is a printed number, and reading it is what keeps a later edit of
    /// the file from leaving a stray round quietly cheaper or dearer than the page says.</para>
    /// </summary>
    private int FriendlyFire(
        EncounterState state, Combatant actor, Attack attack, bool strayShot, List<LedgerLine> lines)
    {
        var melee = Melee(state, actor, attack);

        if (melee.Count == 0) return 0;

        var entry = _play.GetGritty("gritty_friendly_fire");
        var rule = entry.FriendlyFire!;
        var target = state[attack.Target];

        if (strayShot)
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"this is the shot that went wide, and p.80 makes it {Dice(rule.SecondAttackPenaltyDice)} "
                + $"— {actor.Name} attacks {target.Name} at no penalty"));

            return rule.SecondAttackPenaltyDice;
        }

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{rule.AppliesWhen}: {target.Name} is at Close Range with "
            + $"{string.Join(", ", melee.Select(c => c.Name))}, so {actor.Name}'s attack is "
            + $"{Dice(rule.PenaltyDice)}"));

        return rule.PenaltyDice;
    }

    /// <summary>
    /// p.80's other half: a shot into a melee that landed nothing "must" go somewhere, so a second
    /// attack is resolved for real against another character in the tangle.
    ///
    /// <para><b>It is resolved and not announced</b>, which is the whole difference between this
    /// rule being applied and this rule being reported. The stray round goes through
    /// <see cref="ResolveAttack"/> like any other attack — it rolls, it is defended against, and it
    /// does damage or a special effect — and the one thing it may not do is trigger a third, which
    /// is what <c>strayShot</c> carries.</para>
    ///
    /// <para><b>The GM's random choice comes off <see cref="IDiceSource"/></b>, because a seeded run
    /// has to give the same fight twice and a policy that reached for its own randomness would make
    /// a balance figure unreproducible from the seed printed beside it. The face is on the ledger,
    /// so the choice is auditable rather than merely random.</para>
    ///
    /// <para><b>What the stray shot carries is the weapon, and what it drops is everything that was
    /// a fact about the first shot.</b> The Trait, the row of p.75's table, the damage kind, the
    /// special effect, the thrown-weapon declaration and p.76's grabbed <see cref="Attack.Item"/>
    /// are properties of what is being fired;
    /// cover and its Structure are a line of sight to somebody else, and going all-out, charging,
    /// an area attack, a team attack and aiming at a weak point are all things the attacker
    /// declared about the target they meant to hit. The line says so.</para>
    /// </summary>
    private EncounterState TheShotGoesWide(
        EncounterState state, Combatant actor, Attack attack, ResolvedAttack resolved,
        List<LedgerLine> lines)
    {
        var melee = Melee(state, actor, attack);

        if (melee.Count == 0) return state;

        var entry = _play.GetGritty("gritty_friendly_fire");
        var rule = entry.FriendlyFire!;
        var net = resolved.AttackSuccesses - resolved.DefenceSuccesses;

        if (net > rule.SecondAttackTriggeredAtNetSuccesses) return state;

        var (faces, index) = ThePick(melee.Count);
        var unlucky = melee[index % melee.Count];

        var rolled = faces.Count == 1
            ? $"a die came up {faces[0]}"
            : $"{faces.Count} dice came up {string.Join(" and ", faces)}";

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{net} net successes is {rule.SecondAttackTriggeredAtNetSuccesses} or fewer, so the "
            + $"shot goes somewhere: p.80 asks for a second attack against {rule.SecondAttackIsAgainst}, "
            + $"selected {rule.SecondTargetSelected} by the {rule.SecondTargetSelectedBy.ToUpperInvariant()} — "
            + $"{rolled} against {melee.Count} of them, which is {unlucky.Name}. It is the "
            + "same weapon at a different person: what the attacker declared about the first shot — "
            + "cover, going all-out, charging, an area or team attack, a weak point — was about that "
            + "target and does not come with it"));

        var stray = attack with
        {
            Target = unlucky.Id,
            Cover = Cover.None,
            CoverStructure = null,
            CoverScenery = null,
            AllOut = false,
            Charge = false,
            Area = false,
            Team = false,
            VulnerablePart = false
        };

        return ResolveAttack(state, stray, lines, strayShot: true);
    }

    /// <summary>
    /// The GM's random pick, off <see cref="IDiceSource"/>: the faces thrown and the index they
    /// spell, for a melee of <paramref name="candidates"/>.
    ///
    /// <para><b>Enough dice are thrown that every candidate can be picked, which one cannot
    /// do.</b> A d6 read as <c>(face - 1) % count</c> reaches indices 0 to 5 and no further, so in a
    /// melee of seven the seventh character could never be hit, of nine the last three could not,
    /// and the ledger would go on saying the target was "selected randomly" while naming a set the
    /// die had quietly cut down to six. p.80 says the GM selects the second target randomly, and a
    /// character who cannot be selected at all is not part of a random selection.</para>
    ///
    /// <para>So the faces are read as one number in base six — <c>face - 1</c> per die, most
    /// significant first — and as many are thrown as it takes for that number to span the melee:
    /// one die up to six of them, two up to thirty-six, and so on. <b>At least one is always
    /// thrown</b>, even where the melee holds one character and there is nothing to choose between:
    /// the face is the audit trail, and a pick recorded with no die behind it is one nobody can
    /// reproduce from the seed printed beside the run.</para>
    ///
    /// <para><b>What this does not claim is a uniform distribution.</b> Six faces do not divide
    /// four candidates evenly and no number of d6 divides seven at all, so the low indices stay a
    /// little likelier; the property being bought here is that the set of reachable candidates is
    /// the whole melee, which is the half a fixture can hold the engine to and the half that was
    /// wrong.</para>
    /// </summary>
    private (IReadOnlyList<int> Faces, int Index) ThePick(int candidates)
    {
        var faces = new List<int>();
        var span = 1L;
        var index = 0;

        do
        {
            var face = _dice.Roll(1)[0];

            faces.Add(face);
            index = (index * 6) + (face - 1);
            span *= 6;
        }
        while (span < candidates);

        return (faces, index);
    }

    /// <summary>
    /// Which Trait answers an attack, and what pool it throws.
    ///
    /// <para><b>The candidates come from p.75's Attack and Defense table, by the row the attack is
    /// on.</b> The table was modelled and never read, and the cost of that was not academic: every
    /// defence a character had was offered against every attack, so a Mind Control could be soaked
    /// with Toughness — which the mental row does not list at all — and a melee weapon met a
    /// Toughness the row halves. The row also says what "Power" means as a defence, and that is
    /// derived from the table rather than listed here: a Power is any defence Trait the table never
    /// names by name.</para>
    ///
    /// <para><b>Two printed rules could disagree about halving a Toughness, and this halves once.</b>
    /// The table's three weapon-and-Power rows print <c>1/2 Toughness</c>; <c>lethal_and_subdual</c>
    /// says a Toughness answers a lethal attack at half and a subdual one in full. They agree
    /// wherever the book's own defaults hold — the unarmed row is one of that entry's two named
    /// subdual sources, and everything else physical defaults to lethal — and where a caller puts
    /// them at odds this halves if <em>either</em> says to, never twice. p.81's Example of Combat is
    /// what settles the shape: the mecha's Might attack is answered by 12d Armor at its full rank,
    /// which is the table's "Power" column doing the work and no halving in sight. Recorded as a
    /// reading in <c>docs/guide/play-engine.md</c>, with both pages.</para>
    ///
    /// <para><b><c>defenses_used_per_attack</c> and <c>defense_chosen</c> are read rather than
    /// assumed.</b> One defence answers each attack and it is the greatest — after the halvings,
    /// because a Toughness of 8 against a lethal attack is a 4 and an Armor of 6 beside it is the
    /// better answer. Both are a throw if the entry stops saying them, because an engine that went on
    /// rolling one greatest defence against an entry that had been corrected would be applying a rule
    /// the book no longer prints.</para>
    ///
    /// <para>A character in a full hold has no active defence: p.75 lists being immobilized among
    /// the states that take them away.</para>
    /// </summary>
    private (string Trait, int Pool, bool Active) ChooseDefence(
        EncounterState state, Combatant target, Attack attack, int attackRank, List<LedgerLine> lines)
    {
        var types = _play.GetCombat("lethal_and_subdual").DamageTypes!;
        var entry = _play.GetCombat("active_and_passive_defenses");
        var defenses = entry.Defenses!;

        if (defenses.DefensesUsedPerAttack != 1)
        {
            throw new InvalidOperationException(
                $"active_and_passive_defenses says {defenses.DefensesUsedPerAttack} defences answer "
                + "each attack. This engine rolls exactly one, because the entry said one; a "
                + "different number is a rule it cannot apply.");
        }

        if (!defenses.DefenseChosen.Contains("greatest rank", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"active_and_passive_defenses now chooses the defence by '{defenses.DefenseChosen}'. "
                + "This engine takes the greatest rank, because the entry said greatest; picking the "
                + "largest against an entry that says something else would be applying a rule the "
                + "book no longer prints.");
        }

        var actives = defenses.CommonActiveTraits
            .Select(Normalise)
            .ToHashSet(StringComparer.Ordinal);

        var immobilised = NoActiveDefenceAgainst(state, target, attack.Actor, lines);

        var halved = state.DefencesHalved.TryGetValue(target.Id, out var penalty) && state.Page <= penalty.UntilPage
            ? penalty
            : null;

        var candidates = DefenceCandidates(target, attack, lines, state);

        // pp.87-88's other half: a defender holding a printed melee weapon has a Weapon Bonus this
        // engine does not lend them and a ceiling it does not put on them. Neither is applied and
        // the line says which nothing it is — see ArmedDefence.
        ArmedDefence(state, target, attack, lines);

        // p.80's Hard Targets, on the defending half: a machine's passive defences answer at twice
        // their rank, and the doubling is applied to the rank before either printed halving.
        var hard = HardTargetFactor(state, target, attack, lines);

        var best = ("", 0, false);
        var guarded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (trait, tableHalves) in candidates)
        {
            var active = actives.Contains(trait);
            if (active && immobilised) continue;

            var rank = target.Rank(trait);
            if (rank <= 0) continue;

            if (!active) rank *= hard;

            // p.75, twice over: the row may print "1/2 Toughness", and a lethal attack halves a
            // Toughness whatever row it came from. Halve once if either says so.
            var lethalHalves = string.Equals(trait, "toughness", StringComparison.Ordinal)
                && attack.Damage == DamageKind.Lethal
                && string.Equals(types.ToughnessAgainstLethal, "half", StringComparison.Ordinal);

            if (tableHalves || lethalHalves) rank = Halve(rank);

            // p.78: dodging a blast is deliberately awkward. See DodgingAnAreaAttack.
            if (active && attack.Area) rank = Halve(rank);

            if (halved is not null && (!halved.ActiveOnly || active))
            {
                // p.78's guard on going all-out: an opponent who could not penetrate the passive
                // defence at its full rank still cannot, so for them it is not halved at all.
                if (active || CouldPenetrate(attackRank, target.Rank(trait) * hard))
                {
                    rank = Halve(rank);
                }
                else
                {
                    guarded.Add(trait);
                }
            }

            if (rank > best.Item2) best = (trait, rank, active);
        }

        // p.75, the other half of the cover clause: <c>target_may_use_the_covers_structure_as_a_passive
        // _defense</c>. It is offered after the halvings above and never touched by them, because
        // every one of those — going all-out, charging, dodging a blast — is about the character's
        // own defences, and a wall is not one of theirs. It answers the attack when it is the
        // greater, which is <c>defense_chosen</c>'s own rule applied to one more candidate.
        if (attack.CoverStructure is { } structure) best = TheCoverAnswers(state, target, structure, best, lines);

        if (best.Item1.Length == 0)
        {
            // Nothing left to answer with. p.75 still has the attack resolved against a threshold,
            // and a threshold of nothing is what "no defence available" means.
            return ("no defence", 0, false);
        }

        if (attack.Area && best.Item3) DodgingAnAreaAttack(state, target, lines);
        if (guarded.Contains(best.Item1))
            StillCannotPenetrate(state, target, best.Item1, target.Rank(best.Item1) * hard, lines);

        var pool = best.Item2 + WoundPenalty(state, target, lines);

        if (best.Item3 && state.Table.ActiveDefensesCost)
        {
            var gritty = _play.GetGritty("gritty_active_defenses");
            var rule = gritty.ActiveDefensePenalty!;
            var already = state.ActiveDefencesThisPage.GetValueOrDefault(target.Id);

            if (already > 0)
            {
                var penalise = rule.CumulativePenaltyDicePerExtraActiveDefense * already;
                pool += penalise;

                lines.Add(new LedgerLine(
                    state.Page, target.Id, gritty.Id, gritty.SourceRef,
                    $"{target.Name}'s active defence number {already + 1} this page is {penalise}d"));
            }
        }

        // <b>p.75's other two modifiers, and both of them are on this roll only where it is an
        // active one.</b> Size "affects your active defense rolls" in as many words, so a Toughness,
        // an Armor or the cover's Structure never moves for it — a giant is no easier to soak.
        // Visibility affects both halves of the exchange, and this is the defending half: the same
        // method the attack pool went through, with the roles the other way round.
        if (best.Item3)
        {
            pool += SizeModifier(state, target, state[attack.Actor], lines);
            pool += VisibilityPenalty(state, target, state[attack.Actor], "active defence", lines);

            // p.79's Close Range, which is an active defence's rule too: a gun in your face is hard
            // to dodge, and a soak is a soak whatever is being shot at you.
            pool += RangedUpClose(state, target, state[attack.Actor], attack, lines);
        }

        return (best.Item1, pool, best.Item3);
    }

    /// <summary>
    /// p.75's <c>target_may_use_the_covers_structure_as_a_passive_defense</c>: the obstacle's own
    /// durability, offered beside the target's own defences and taken when it is the greater.
    ///
    /// <para><b>It is a passive defence and the page says so</b>, which is what keeps the size
    /// modifier off it and what makes p.79's luring — bought off a dodge — unavailable to a target
    /// who hid behind a wall instead of moving.</para>
    ///
    /// <para><b>A line is written either way.</b> A Structure that lost to the target's own Armor is
    /// still a thing the caller declared and a thing the engine considered; saying nothing about it
    /// would leave a reader unable to tell that from a Structure the engine had dropped on the
    /// floor.</para>
    ///
    /// <para><b>The comparison is made here, before p.75's size band.</b> That is a reading and the
    /// guide's table records it: p.75 says a defender "always use[s] the best defense available",
    /// and whether "best" means the greater rank or the greater roll is a question the page does not
    /// put, because none of its own modifiers touch a wall. Comparing before the band is the reading
    /// that keeps which defence answers a fact about the two figures rather than about how big the
    /// attacker happens to be — and it can leave a dodger with the smaller roll, so the ledger line
    /// says which comparison it made.</para>
    /// </summary>
    private (string Trait, int Pool, bool Active) TheCoverAnswers(
        EncounterState state, Combatant target, int structure,
        (string Trait, int Pool, bool Active) best, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("modifier_cover");

        if (!entry.Cover!.TargetMayUseTheCoversStructureAsAPassiveDefense) return best;

        // <b>A tie is not "the greater", and the line used to say it was.</b> `structure > best.Pool`
        // leaves a Structure equal to the target's own defence unused — which is right, because the
        // one they keep may be an active defence and p.75's size band moves one of those — but the
        // line reported it as the target's figure being greater, which at a tie is false. What the
        // ledger says now is which of the three comparisons actually held.
        var takes = structure > best.Pool;

        var own = best.Trait.Length == 0
            ? "nothing of their own"
            : $"their {best.Trait} at {best.Pool}d";

        var comparison =
            takes ? $"it is the greater and it is what answers, ahead of {own}"
            : best.Trait.Length == 0 ? "and they have nothing of their own either, so nothing answers the roll"
            : structure == best.Pool ? $"{own} matches it, so their own answers and stays active"
            : $"{own} is the greater, so that answers instead";

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"the attack comes through the cover, so p.75 lets {target.Name} answer with "
            + $"{CoversStructure} of {structure}d as a passive defence: {comparison}. The two are "
            + "compared as they stand here, before p.75's size band, which moves an active defence "
            + "and never a wall"));

        return takes ? (CoversStructure, structure, false) : best;
    }

    /// <summary>
    /// The Traits p.75's table lets this target answer this attack with, and whether the row halves
    /// each one.
    ///
    /// <para>A Minion group is outside the table: p.77 gives them one characteristic and it answers
    /// everything, so their own defence list stands.</para>
    /// </summary>
    private List<(string Trait, bool TableHalves)> DefenceCandidates(
        Combatant target, Attack attack, List<LedgerLine> lines, EncounterState state)
    {
        var table = _play.GetCombat("attack_and_defense_table");
        var rows = table.AttackDefenseTable!;

        if (target.Kind == CombatantKind.MinionGroup)
        {
            return [.. target.Defences.Select(trait => (trait, false))];
        }

        var printed = PrintedType(attack.Type);

        var row = rows.SingleOrDefault(r => string.Equals(r.Type, printed, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"attack_and_defense_table has no row '{printed}'. Its rows are "
                + $"{string.Join(", ", rows.Select(r => r.Type))}, and this engine resolves an attack "
                + "by finding the row it is on.");

        // "Power" is whatever the table does not name by name — derived from the table rather than
        // listed here, so a corrected table moves this with it.
        var named = rows
            .SelectMany(r => r.DefenseTraits)
            .Where(t => !string.Equals(t, PowerColumn, StringComparison.Ordinal))
            .Select(t => Normalise(WithoutHalf(t)))
            .ToHashSet(StringComparer.Ordinal);

        var candidates = new List<(string Trait, bool TableHalves)>();

        foreach (var offered in row.DefenseTraits)
        {
            if (string.Equals(offered, PowerColumn, StringComparison.Ordinal))
            {
                candidates.AddRange(target.Defences
                    .Where(d => !named.Contains(d))
                    .Select(d => (d, false)));

                continue;
            }

            candidates.Add((Normalise(WithoutHalf(offered)), !string.Equals(
                offered, WithoutHalf(offered), StringComparison.Ordinal)));
        }

        lines.Add(new LedgerLine(
            state.Page, target.Id, table.Id, table.SourceRef,
            $"a {row.Type} attack is answered with {string.Join(" or ", row.DefenseTraits)}, which for "
            + $"{target.Name} is {string.Join(", ", candidates.Select(c => c.Trait).DefaultIfEmpty("nothing"))}"));

        return candidates;
    }

    /// <summary>
    /// p.78's price on dodging a blast, and the ledger line that says which half of it was paid.
    ///
    /// <para><b>The rule is a choice and the engine has to make it, so it makes it out loud.</b>
    /// <c>area_attacks</c> says an active defence against an area attack must either halve its rank
    /// or forfeit the next turn to act; this engine halves, and the line names the option it did not
    /// take. Halving is the choice that keeps a fight comparable across runs — forfeiting a turn
    /// moves a character's whole page and would make an area attack's cost depend on where in the
    /// order the dodger happened to be — and a policy that could choose is what would settle it
    /// properly, which is a later slice's problem rather than a licence to apply neither.</para>
    ///
    /// <para>The two options are read off the entry rather than typed, and the halving one is found
    /// by its printed word: an entry that stopped offering it is a rule this engine cannot apply, so
    /// it throws rather than quietly halving anyway.</para>
    /// </summary>
    private void DodgingAnAreaAttack(EncounterState state, Combatant target, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("area_attacks");
        var options = entry.AreaAttack!.AnActiveDefenseMustEither;

        var halving = options.FirstOrDefault(o => o.Contains("halve", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "area_attacks no longer offers halving as one of the things an active defence must "
                + $"do — it offers {string.Join(" or ", options)}. This engine takes the halving "
                + "option and records the other, so a rule without one is a rule it cannot apply.");

        var alternative = options.Where(o => !string.Equals(o, halving, StringComparison.Ordinal));

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"{target.Name} dodges an area attack, so the defence must either {string.Join(" or ", options)}: "
            + $"this engine takes \"{halving}\" and not \"{string.Join(" or ", alternative)}\""));
    }

    /// <summary>
    /// p.78's one guard on going all-out: <c>opponents_who_could_not_penetrate_your_passive_defense:
    /// "still cannot"</c>.
    ///
    /// <para><b>It was modelled and never read, and what that cost was the whole point of the
    /// clause.</b> Going all-out halves every defence, so an Armor of 15 became a 7 and an attacker
    /// who could never have got through it started getting through it — which is the one outcome the
    /// page writes a sentence to forbid. The halving still applies to everybody who could already
    /// hurt them, and to every active defence; it is only the opponent the guard names who sees the
    /// full rank.</para>
    ///
    /// <para><b>What "penetrate" means is a reading, and it is derived rather than invented.</b>
    /// Chapter 4 states the test once, under Cover: <c>modifier_cover.attacking_through_cover_requires</c>
    /// is "an attack rank greater than the cover's Structure". So an attack penetrates a passive
    /// defence when the attacking Trait's rank is greater than the defence's, and the phrase is read
    /// out of that entry rather than typed — an entry that stops saying "greater than" is a rule this
    /// engine cannot apply. <c>docs/guide/play-engine.md</c> records it as a reading.</para>
    /// </summary>
    private bool CouldPenetrate(int attackRank, int passiveRank) =>
        GetsThrough(attackRank, passiveRank);

    /// <summary>
    /// <c>modifier_cover</c>'s one test — <c>attacking_through_cover_requires</c>, "an attack rank
    /// greater than the cover's Structure" — read out of the entry rather than typed.
    ///
    /// <para><b>One method, because two rules turn on the same sentence.</b> p.75's own
    /// attack-through-cover clause is one of them; p.78's guard on going all-out is the other, since
    /// this phrase is the only place Chapter 4 says what penetrating a passive defence means. A
    /// different test is a rule this engine cannot apply, so it throws rather than falling back on
    /// a comparison the book no longer prints. <c>docs/guide/play-engine.md</c> records the reading.
    /// </para>
    /// </summary>
    private bool GetsThrough(int attackRank, int obstacleRank)
    {
        var cover = _play.GetCombat("modifier_cover");
        var test = cover.Cover!.AttackingThroughCoverRequires;

        if (!test.Contains("greater than", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"modifier_cover now says getting through an obstacle requires '{test}'. That "
                + "phrase is the only place Chapter 4 says what penetrating a passive defence means, "
                + "and both p.75's attack through cover and p.78's guard on going all-out are read "
                + "out of it; a different test is a rule this engine cannot apply. See "
                + "docs/guide/play-engine.md's readings table.");
        }

        return attackRank > obstacleRank;
    }

    /// <summary>The ledger line for an attack that p.78's guard has kept out.</summary>
    private void StillCannotPenetrate(
        EncounterState state, Combatant target, string trait, int rank, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("going_all_out");

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"{target.Name} went all-out, so every defence of theirs is halved — but an opponent who "
            + $"could not penetrate their {trait} of {rank} "
            + $"{entry.AllOutAttack!.OpponentsWhoCouldNotPenetrateYourPassiveDefense}, so this one "
            + "meets it at its full rank"));
    }

    /// <summary>
    /// Whether a grapple has taken this target's active defences away against this attacker.
    ///
    /// <para><b>Two printed rules, and the engine used to apply half of one.</b> p.75 lists being
    /// immobilized among the states that take active defences away, which is what a <em>full hold</em>
    /// does — and the engine applied it to every full grapple, so a character who had lost their
    /// sword to a full grab could not dodge either. p.76 says a <em>partial</em> grab and a partial
    /// hold each block active defences "against anyone else", on both characters, and that was not
    /// applied at all: the other party of the grapple is exactly who a character can still dodge, and
    /// everybody else is exactly who they cannot.</para>
    ///
    /// <para>Both booleans are read off <c>grab</c> and <c>hold</c> rather than assumed, so a
    /// corrected entry moves this with it.</para>
    /// </summary>
    private bool NoActiveDefenceAgainst(
        EncounterState state, Combatant target, string attacker, List<LedgerLine> lines)
    {
        var held = state.Grapples.FirstOrDefault(g =>
            g.Move == GrappleMove.Hold
            && g.Kind == GrappleKind.Full
            && string.Equals(g.Held, target.Id, StringComparison.Ordinal));

        if (held is not null)
        {
            var entry = _play.GetCombat("active_and_passive_defenses");

            lines.Add(new LedgerLine(
                state.Page, target.Id, entry.Id, entry.SourceRef,
                $"{target.Name} is in a full hold, and p.75 lists "
                + $"{entry.Defenses!.ActiveUnusableWhen[0]} among the states that take an active "
                + "defence away"));

            return true;
        }

        foreach (var grapple in state.Grapples.Where(g =>
                     g.Kind == GrappleKind.Partial
                     && (string.Equals(g.Held, target.Id, StringComparison.Ordinal)
                         || string.Equals(g.Holder, target.Id, StringComparison.Ordinal))))
        {
            var other = string.Equals(grapple.Held, target.Id, StringComparison.Ordinal)
                ? grapple.Holder
                : grapple.Held;

            // "Against anyone else" — the character they are tangled with is the one they can still
            // meet.
            if (string.Equals(other, attacker, StringComparison.Ordinal)) continue;

            var entry = _play.GetCombat(grapple.Move == GrappleMove.Grab ? "grab" : "hold");

            var blocks = grapple.Move == GrappleMove.Grab
                ? entry.Grab!.PartialBlocksActiveDefensesAgainstAnyoneElse
                : entry.Hold!.PartialBlocksActiveDefensesAgainstAnyoneElse;

            if (!blocks) continue;

            lines.Add(new LedgerLine(
                state.Page, target.Id, entry.Id, entry.SourceRef,
                $"{target.Name} is in a partial {grapple.Move.ToString().ToLowerInvariant()} with "
                + $"{state[other].Name}, so they have no active defence against anybody else"));

            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a grapple leaves this character unable to take the action they asked for, refused on
    /// the ledger.
    ///
    /// <para><b>p.76 restrains both parties and the engine restrained neither.</b> A fully held
    /// character "can only try to escape" and was still attacking third parties; a partial hold
    /// leaves both characters with one physical action, "an opposed Might roll, aiming for a full
    /// hold or an escape", and both were free to do anything. A partial <em>grab</em> restrains
    /// nothing but the defences above — the page says the two are fighting over an item, not that
    /// they are pinned — so it is deliberately not here.</para>
    ///
    /// <para><b>What is refused rather than adjudicated is stated out loud.</b> <c>hold</c> also says
    /// a held character may use "any Power they could reasonably use while physically restrained",
    /// adjudicated case by case by the GM. That is a judgement and this engine has nobody to ask, so
    /// it refuses and the line says which clause it applied and which one it could not — recorded in
    /// the guide rather than silently resolved either way.</para>
    /// </summary>
    private bool GrappleForbids(
        EncounterState state, string actorId, GrappleMove? move, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("hold");
        var rule = entry.Hold!;

        var fullyHeld = state.Grapples.Any(g =>
            g.Move == GrappleMove.Hold
            && g.Kind == GrappleKind.Full
            && string.Equals(g.Held, actorId, StringComparison.Ordinal));

        if (fullyHeld)
        {
            if (move == GrappleMove.Escape) return false;

            Refuse(state, actorId, entry.Id, entry.SourceRef, lines,
                $"{state[actorId].Name} is in a full hold, which leaves them only "
                + $"{rule.FullLeavesTheHeldCharacterOnly}. Whether "
                + $"\"{rule.AHeldCharacterMayUse}\" covers this is "
                + $"{rule.AdjudicatedCaseByCaseBy} discretion, which this engine has nobody to ask");

            return true;
        }

        var partiallyHeld = state.Grapples.Any(g =>
            g.Move == GrappleMove.Hold
            && g.Kind == GrappleKind.Partial
            && (string.Equals(g.Held, actorId, StringComparison.Ordinal)
                || string.Equals(g.Holder, actorId, StringComparison.Ordinal)));

        if (!partiallyHeld || move is not null) return false;

        Refuse(state, actorId, entry.Id, entry.SourceRef, lines,
            $"{state[actorId].Name} is in a partial hold: {rule.PartialMeans}, and the only physical "
            + $"action either of them has is {rule.PartialOnlyPhysicalAction}");

        return true;
    }

    /// <summary>The table's own name for the column that means "one of the target's Powers".</summary>
    private const string PowerColumn = "Power";

    /// <summary>The table's half marker, stripped: <c>1/2 Toughness</c> is a Toughness, halved.</summary>
    private static string WithoutHalf(string printed) =>
        printed.StartsWith("1/2 ", StringComparison.Ordinal) ? printed["1/2 ".Length..] : printed;

    /// <summary>The row heading p.75 prints for one kind of attack.</summary>
    private static string PrintedType(AttackType type) => type switch
    {
        AttackType.Unarmed => "Unarmed",
        AttackType.MeleeWeapon => "Melee Weapon",
        AttackType.RangedWeapon => "Ranged Weapon",
        AttackType.PhysicalPower => "Physical Power",
        AttackType.MentalPower => "Mental Power",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No such row of p.75's table.")
    };

    /// <summary>
    /// What the net successes did: Minions removed, a special effect started, or Health taken off.
    ///
    /// <para>Split out from the roll so that a purchase made after the roll — a bought die, a
    /// reroll — can re-apply it against the target as they were, rather than compounding.</para>
    /// </summary>
    private EncounterState ApplyAttackOutcome(
        EncounterState state, Attack attack, ResolvedAttack resolved, List<LedgerLine> lines)
    {
        var net = resolved.AttackSuccesses - resolved.DefenceSuccesses;
        var target = resolved.TargetBefore;
        var actor = state[resolved.Actor];

        var after = state with { Effects = resolved.EffectsBefore };
        after = after.With(target);

        NarrativeControl(after, actor, net, lines);

        if (net <= 0)
        {
            var entry = _play.GetCombat("attacks_and_defenses");

            lines.Add(new LedgerLine(
                after.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{net} net successes: the attack misses, or hits with no effect"));

            return after;
        }

        if (target.Kind == CombatantKind.MinionGroup) return DefeatMinions(after, attack, target, net, lines);
        if (attack.Effect is not null) return StartSpecialEffect(after, actor, target, attack.Effect, net, lines);

        return InflictDamage(after, actor, target, attack, net, lines);
    }

    /// <summary>
    /// How many of a Minion group one attack removes, and a ledger line that says what was applied.
    ///
    /// <para><b>The line used to announce the figure before the cap and then skip the line that
    /// applied it.</b> Under Tough Minions the engine printed "5 net successes defeat 5" and then
    /// quietly removed the two Minions that were actually there, suppressing the capping line
    /// altogether — so the ledger's number and the state's number disagreed, and the ledger's was the
    /// one a reader would have quoted. There is one line now and it carries both figures.</para>
    /// </summary>
    private EncounterState DefeatMinions(
        EncounterState state, Attack attack, Combatant target, int net, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("attacking_minions");
        var rule = entry.AttackingMinions!;

        var perNet = Math.Min(
            attack.Area ? rule.MinionsDefeatedPerNetSuccessWithAnAreaAttack : rule.MinionsDefeatedPerNetSuccess,
            rule.MaximumMinionsPerNetSuccess);

        var couldDefeat = net * perNet;
        var id = entry.Id;
        var sourceRef = entry.SourceRef;
        var rate = $"{net} net successes could defeat {couldDefeat} Minions";

        if (state.Table.ToughMinions)
        {
            var gritty = _play.GetGritty("gritty_tough_minions");
            var tough = gritty.ToughMinions!;

            if (!string.Equals(tough.Rounding, "down", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "gritty_tough_minions is the book's one printed exception to the round-up rule "
                    + $"and its rounding now says '{tough.Rounding}'.");
            }

            couldDefeat = attack.Area
                ? net * tough.AreaAttackMinionsPerNetSuccess
                : net / tough.NetSuccessesPerMinionDefeated;

            id = gritty.Id;
            sourceRef = gritty.SourceRef;

            rate = $"Tough Minions: {net} net successes could defeat {couldDefeat}, rounding down — "
                + "the one place in the book where a half goes downward";
        }

        // <b>The cap is applied to both rates, and the entry's ambiguity is why that is a
        // decision.</b> p.77 prints "(up to the number of Minions in the area of effect or within
        // reach)" as a parenthesis on the area-attack clause alone, and the entry records it there,
        // named for that clause. Its second half — "or within reach" — is the phrase for an ordinary
        // attack, which reads as though the whole rule was meant to be capped. Capping both is the
        // only reading under which an attack cannot knock out a Minion who is not there.
        var defeated = Math.Min(couldDefeat, target.GroupSize);

        lines.Add(new LedgerLine(
            state.Page, "", id, sourceRef,
            $"{rate}, capped by the {target.GroupSize} actually there: {defeated} defeated"));

        return state.With(target.WithGroupSize(target.GroupSize - defeated));
    }

    private EncounterState StartSpecialEffect(
        EncounterState state, Combatant actor, Combatant target, string effect, int net, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("special_effects");
        var pages = Half(net, entry.Interpretation!.DurationRounds!);

        var existing = state.Effects.FirstOrDefault(e =>
            string.Equals(e.Target, target.Id, StringComparison.Ordinal)
            && string.Equals(e.Name, effect, StringComparison.Ordinal));

        var duration = existing is null || !entry.SpecialEffect!.DurationStacksByAttackingTheSameTargetAgain
            ? pages
            : existing.RemainingPages + pages;

        var effects = state.Effects
            .Where(e => !ReferenceEquals(e, existing))
            .Append(new SpecialEffect(target.Id, actor.Id, effect, duration))
            .ToList();

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{net} net successes is {pages} pages of {effect} on {target.Name} — half the net "
            + $"successes, rounding {entry.Interpretation.DurationRounds}, which is this project's "
            + "reading of p.76 and is what its own worked example computes"));

        var after = state with { Effects = effects };

        return duration >= target.CurrentHealth
            ? DefeatByEffect(after, target, effect, duration, lines)
            : after;
    }

    /// <summary>
    /// p.76's second way to be defeated: an effect whose duration has reached what is left of the
    /// target.
    ///
    /// <para><b>The line used to be the whole of it, and that is the worst defect this repository
    /// recognises.</b> The ledger announced that the target was out for the rest of the scene and
    /// the state said otherwise: they kept their place in the order, kept defending, and
    /// <see cref="EncounterState.Over"/> never saw them go down. A run reading its own ledger and a
    /// run reading its own numbers would have given two different accounts of the same fight.</para>
    ///
    /// <para>It is recorded on the combatant rather than as a Health total, because it is not one —
    /// an Ensnare long enough to end a fight ends it without doing a point of damage, and writing it
    /// as damage is a lie the wound penalties and the Fatal Damage threshold would both read.</para>
    /// </summary>
    private EncounterState DefeatByEffect(
        EncounterState state, Combatant target, string effect, int duration, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("special_effects");

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"{duration} pages of {effect} reaches {target.Name}'s current Health of "
            + $"{target.CurrentHealth}, so they are out for {entry.SpecialEffect!.DefeatByEffectLasts}"));

        return state.With(target.OutForTheScene(effect));
    }

    /// <summary>
    /// Health off, and — under p.79's Fatal Damage rule — the clock that starts when it goes far
    /// enough below nothing.
    ///
    /// <para><b>Half the rule used to be missing.</b> Health went negative and the killing line was
    /// announced, and <c>dying_begins_when_lethal_damage_reduces_you_to</c>,
    /// <c>dying_damage_per_page</c> and everything that stops the clock were modelled and unread —
    /// so a character bled out on paper and lay there at a fixed Health for the rest of the fight,
    /// which is the opposite of what the setting is for. The guide said it was applied.</para>
    ///
    /// <para><b>It is the <em>lethal</em> kind that starts it</b>, which is why
    /// <see cref="ResolvedAttack"/> now carries the damage kind: a knockout blow that takes somebody
    /// past the threshold does not start them dying, and a re-applied attack that had forgotten which
    /// kind it was would have started it anyway.</para>
    /// </summary>
    private EncounterState InflictDamage(
        EncounterState state, Combatant actor, Combatant target, Attack attack, int net,
        List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("damage");
        var rule = entry.Damage!;
        var damage = net * rule.DamagePerNetSuccess;
        var health = target.CurrentHealth - damage;

        // p.80's Slow Healing, the sharp end of the sentence that let them stand up at all: a
        // character conscious at or below the defeat figure "is defeated if you take even a single
        // point of damage in this condition". Shared with p.78's charge, which hurts the charger.
        var overcome = AnyDamageAtAllPutsThemDown(state, target, damage, lines);

        Combatant Down(Combatant hurt) => overcome ? hurt.Overcome() : hurt;

        if (!state.Table.FatalDamage)
        {
            health = Math.Max(rule.DefeatedAtHealth, health);

            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{net} net successes is {damage} damage; {target.Name} is on {health} Health"
                + (health <= rule.DefeatedAtHealth ? $", which is {rule.DefeatedMeans}" : "")));

            return state.With(Down(target.WithHealth(health)));
        }

        var gritty = _play.GetGritty("gritty_fatal_damage");
        var fatal = gritty.FatalDamage!;
        var killedAt = -target.FullHealth;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{net} net successes is {damage} damage; {target.Name} is on {health} Health"));

        var hurt = Down(target.WithHealth(health));

        if (health <= killedAt)
        {
            lines.Add(new LedgerLine(
                state.Page, target.Id, gritty.Id, gritty.SourceRef,
                $"{health} reaches {killedAt}, the negative of {target.Name}'s full Health of "
                + $"{target.FullHealth}, which is {fatal.KilledAt} — "
                + $"{fatal.CostResolveToAvoid} Resolve buys it back"));

            return state.With(hurt.Bleeding(dying: false));
        }

        if (attack.Damage != DamageKind.Lethal
            || health > fatal.DyingBeginsWhenLethalDamageReducesYouTo
            || target.Dying)
        {
            return state.With(hurt);
        }

        lines.Add(new LedgerLine(
            state.Page, target.Id, gritty.Id, gritty.SourceRef,
            $"lethal damage has taken {target.Name} to {health}, and dying begins at "
            + $"{fatal.DyingBeginsWhenLethalDamageReducesYouTo}: {fatal.DyingDamagePerPage} a page "
            + $"until {fatal.DyingEndsAt}"));

        return state.With(hurt.Bleeding(dying: true));
    }

    /// <summary>
    /// p.79's clock, one page on: <c>dying_damage_per_page</c> off everybody who is bleeding out,
    /// and death at the negative of their full Health.
    /// </summary>
    private EncounterState TickTheDying(EncounterState state, int page, List<LedgerLine> lines)
    {
        if (!state.Table.FatalDamage) return state;

        var gritty = _play.GetGritty("gritty_fatal_damage");
        var fatal = gritty.FatalDamage!;

        foreach (var dying in state.Combatants.Values.Where(c => c.Dying).ToList())
        {
            var health = dying.CurrentHealth - fatal.DyingDamagePerPage;
            var killedAt = -dying.FullHealth;

            lines.Add(new LedgerLine(
                page, dying.Id, gritty.Id, gritty.SourceRef,
                $"{dying.Name} is bleeding out: {fatal.DyingDamagePerPage} more off, to {health}"
                + (health <= killedAt
                    ? $", which reaches {killedAt} and is {fatal.KilledAt}"
                    : $", and {fatal.DyingEndsAt} is what stops it")));

            state = state.With(health <= killedAt
                ? dying.WithHealth(health).Bleeding(dying: false)
                : dying.WithHealth(health));
        }

        return state;
    }

    /// <summary>
    /// Chapter 3's band table, reached from a fight: who describes what happened, and whether the
    /// loser gets to add a detail. It changes no number and is written down because a fight is
    /// still a challenge roll and the page says so.
    /// </summary>
    private void NarrativeControl(EncounterState state, Combatant actor, int net, List<LedgerLine> lines)
    {
        var entry = _play.GetChallenge("narrative_control");

        var band = entry.Bands!.Single(b =>
            (b.MinNetSuccesses is null || net >= b.MinNetSuccesses)
            && (b.MaxNetSuccesses is null || net <= b.MaxNetSuccesses));

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{net} net successes hands narrative control to the {band.Outcome}"
            + (band.Embellishment == true ? ", with an embellishment for the other side" : "")));
    }

    // ── Movement ─────────────────────────────────────────────────────────────

    private EncounterState ResolveMove(EncounterState state, Move move, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, move.Actor, lines)) return state;
        if (OutOfTheFight(state, move.Actor, "actor", lines)) return state;
        if (GrappleForbids(state, move.Actor, null, lines)) return state;

        var actor = state[move.Actor];
        var entry = _play.GetCombat("movement");
        var rule = entry.Movement!;

        var pagesNeeded = Movement.PagesToCrossARangeClass(_play, actor);
        var key = $"{move.Actor}|{EncounterState.PairKey(move.Actor, move.Toward)}";
        var banked = state.MoveProgress.GetValueOrDefault(key) + 1;

        var band = state.RangeBetween(move.Actor, move.Toward);

        if (banked < pagesNeeded)
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{actor.Name} spends a page moving, {banked} of the {pagesNeeded} a range class "
                + "takes them"));

            return state with
            {
                MoveProgress = new Dictionary<string, int>(state.MoveProgress, StringComparer.Ordinal) { [key] = banked }
            };
        }

        var moved = move.Closer
            ? (RangeBand)Math.Max((int)RangeBand.Close, (int)band - 1)
            : (RangeBand)Math.Min((int)RangeBand.Extreme, (int)band + 1);

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} {(move.Closer ? "closes to" : "opens to")} {moved} Range with "
            + $"{state[move.Toward].Name} after {pagesNeeded} page(s); moving does not use up the "
            + $"turn's action ({nameof(rule.MovingPreventsActions)} is {rule.MovingPreventsActions})"));

        var progress = new Dictionary<string, int>(state.MoveProgress, StringComparer.Ordinal);
        progress.Remove(key);

        return (state with { MoveProgress = progress }).WithRange(move.Actor, move.Toward, moved);
    }

    // ── Holding, grappling, breaking free ────────────────────────────────────

    private EncounterState ResolveHold(EncounterState state, Hold hold, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, hold.Actor, lines)) return state;
        if (OutOfTheFight(state, hold.Actor, "actor", lines)) return state;
        if (GrappleForbids(state, hold.Actor, null, lines)) return state;

        var entry = _play.GetCombat("holding_an_action");
        var actor = state[hold.Actor];

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} holds their action; if the cue never comes, {entry.Holding!.IfItNeverHappens}"));

        return state with { Holds = [.. state.Holds, hold.Actor] };
    }

    private EncounterState ResolveGrapple(EncounterState state, GrappleIntent grapple, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, grapple.Actor, lines)) return state;
        if (OutOfTheFight(state, grapple.Actor, "actor", lines)) return state;
        if (OutOfTheFight(state, grapple.Target, "target", lines)) return state;
        if (GrappleForbids(state, grapple.Actor, grapple.Move, lines)) return state;

        var entry = _play.GetCombat("grappling");
        var table = _play.GetCombat("grappling_table");
        var rule = entry.Grappling!;

        var actor = state[grapple.Actor];
        var target = state[grapple.Target];

        if (grapple.Move == GrappleMove.Escape && NothingToEscape(state, actor, lines)) return state;

        // <b>A grab of nothing is a hold by another name, so it is refused before a die is thrown.</b>
        // p.76 tells the three moves apart by what they are aimed at, and this engine's whole
        // treatment of a grab — the contested item on a partial, the item that moves on a full — is
        // about the object. Rolling one anyway would put a contest over nothing in particular on the
        // ledger and, at three net successes, hand somebody control of it.
        if (grapple.Move == GrappleMove.Grab && string.IsNullOrWhiteSpace(grapple.Item))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has not said what of {target.Name}'s they are grabbing. A grab is "
                + $"{rule.AGrabIs}, and a grab that names no item is {rule.AHoldIs} by another name — "
                + "this engine holds no inventory, so the object is the caller's word. Nothing was "
                + "rolled");
        }

        if (grapple.Move == GrappleMove.Grab
            && GrabbingWhatTheyDoNotHold(state, actor, target, grapple.Item!.Trim(), entry, rule, lines)
                is { } absent)
        {
            return absent;
        }

        // p.76: Might against Might, both ways. The roll and the threshold are the entry's own.
        var trait = Normalise(rule.Roll);

        var attackRoll = _counter.Roll(actor.Rank(trait) + WoundPenalty(state, actor, lines), _dice);
        var defenceRoll = _counter.Roll(target.Rank(trait) + WoundPenalty(state, target, lines), _dice);

        var net = attackRoll.Successes - defenceRoll.Successes;

        var row = table.GrapplingTable!.Single(r =>
            (r.MinNetSuccesses is null || net >= r.MinNetSuccesses)
            && (r.MaxNetSuccesses is null || net <= r.MaxNetSuccesses));

        var result = grapple.Move switch
        {
            GrappleMove.Grab => row.Grab,
            GrappleMove.Hold => row.Hold,
            GrappleMove.Escape => row.Escape,
            _ => throw new ArgumentOutOfRangeException(nameof(grapple))
        };

        lines.Add(new LedgerLine(
            state.Page, actor.Id, table.Id, table.SourceRef,
            $"{actor.Name} tries a {grapple.Move} on {target.Name}: {attackRoll.Successes} against "
            + $"{defenceRoll.Successes} is {net} net, which is a {result}"));

        return grapple.Move == GrappleMove.Escape
            ? ApplyEscape(state, actor, result, lines)
            : ApplyGrappleResult(state, actor, target, grapple.Move, grapple.Item?.Trim(), result, lines);
    }

    /// <summary>
    /// Whether there is a hold to get out of at all, refused on the ledger where there is not.
    ///
    /// <para><b>An escape by the character doing the holding used to print "a partial escape" and
    /// change nothing.</b> <c>escape</c> defines the move as "an attempt to break out of a hold", and
    /// the holder is not in one — so the roll was made, the table was read, the result was announced
    /// and the state was untouched, which is a ledger line that describes something that did not
    /// happen.</para>
    /// </summary>
    private bool NothingToEscape(EncounterState state, Combatant actor, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("escape");
        var grappling = _play.GetCombat("grappling").Grappling!;

        if (state.Grapples.Any(g =>
                g.Move == GrappleMove.Hold
                && string.Equals(g.Held, actor.Id, StringComparison.Ordinal)))
        {
            return false;
        }

        var holding = state.Grapples.Any(g =>
            string.Equals(g.Holder, actor.Id, StringComparison.Ordinal));

        Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{actor.Name} is not in a hold, and an escape is {grappling.AnEscapeIs}"
            + (holding ? " — they are the one doing the holding" : ""));

        return true;
    }

    /// <summary>
    /// What a grab or a hold leaves behind.
    ///
    /// <para><b>A grab and a hold are different states and were stored as the same one.</b> The old
    /// code keyed on the word "full" in the table's result and recorded every one of them as a hold,
    /// so a full grab — which p.76 defines as control of an <em>object</em> — immobilised the
    /// character who lost it. A character who had had their sword taken could not dodge.</para>
    ///
    /// <para><b>The item a full grab wins is state, and what losing it means for the loser is the
    /// GM's.</b> p.76 gives the winner "control of the object", so <see cref="Combatant.Holding"/>
    /// carries it and the page it was won on; the loser stops holding it if this engine had them
    /// holding it at all, which it only ever does when a previous grab put it there.</para>
    ///
    /// <para><b>What is deliberately not applied is any change to the loser's own attacks</b>, and
    /// the ledger line says so rather than leaving a reader to assume it was handled. An
    /// <see cref="Attack"/> here names a Trait and there is no field anywhere saying which Trait a
    /// weapon backs — the same absence that makes p.80's Gear Limit inapplicable — so an engine that
    /// docked the loser's Might for losing a sword would be inventing a figure no sheet supports.
    /// </para>
    ///
    /// <para><b>A grab between the pair is replaced whichever way round it was recorded.</b> The
    /// filter used to match the actor as <see cref="Grapple.Holder"/> only, so a character who won a
    /// partial grab and then lost the item outright left their own partial in the list — and a
    /// partial grab takes both characters' active defences away against everyone else, for the rest
    /// of the fight. One contest between two characters is one record.</para>
    /// </summary>
    private EncounterState ApplyGrappleResult(
        EncounterState state, Combatant actor, Combatant target, GrappleMove move, string? item,
        string result, List<LedgerLine> lines)
    {
        if (result.Contains("no effect", StringComparison.Ordinal)) return state;

        var kind = result.Contains("full", StringComparison.Ordinal) ? GrappleKind.Full : GrappleKind.Partial;
        var entry = _play.GetCombat(move == GrappleMove.Grab ? "grab" : "hold");

        if (move == GrappleMove.Grab && kind == GrappleKind.Full)
        {
            state = ApplyFullGrab(state, actor, target, item!, entry, lines);
        }
        else
        {
            var means = move == GrappleMove.Grab
                ? entry.Grab!.PartialMeans
                : kind == GrappleKind.Full ? entry.Hold!.FullMeans : entry.Hold!.PartialMeans;

            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"a {kind.ToString().ToLowerInvariant()} {move.ToString().ToLowerInvariant()}"
                + (item is null ? "" : $" over {item}") + $": {means}"));
        }

        var grapples = state.Grapples
            .Where(g => !(Between(g, actor.Id, target.Id) && g.Move == move))
            .Append(new Grapple(actor.Id, target.Id, move, kind, item))
            .ToList();

        return state with { Grapples = grapples };
    }

    /// <summary>Whether a grapple is the one between these two, whichever way round it was won.</summary>
    private static bool Between(Grapple grapple, string a, string b) =>
        (string.Equals(grapple.Holder, a, StringComparison.Ordinal)
         && string.Equals(grapple.Held, b, StringComparison.Ordinal))
        || (string.Equals(grapple.Holder, b, StringComparison.Ordinal)
            && string.Equals(grapple.Held, a, StringComparison.Ordinal));

    /// <summary>
    /// p.76's full grab: the object changes hands, and the page it changed hands on is recorded
    /// because the page is what the next clause is measured in.
    /// </summary>
    private EncounterState ApplyFullGrab(
        EncounterState state, Combatant actor, Combatant target, string item, CombatEntry entry,
        List<LedgerLine> lines)
    {
        var grab = entry.Grab!;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"a full grab is {grab.FullMeans}: {actor.Name} has {item}, and {target.Name} is not "
            + "restrained by it"));

        // <b>The clause and what it is worth here, both said out loud.</b> `full_is_in_effect_a_free
        // _action` is an exemption from the multiple action penalty, and this engine does not count
        // actions per page at all — that mechanic is on the guide's list of ones with no intent yet.
        // So what the page buys here is that using or tossing it does not take the actor's turn, and
        // saying that is honest where "no multiple action penalty was charged" would imply a penalty
        // this engine has.
        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} may use or toss {item} on this page — "
            + $"{nameof(grab.FullSuffersTheMultipleActionPenalty)} is "
            + $"{grab.FullSuffersTheMultipleActionPenalty} and a full grab is in effect a free "
            + $"action, so neither spends their turn. Nothing else this page and it is dropped"));

        // <b>What the loss means for the loser's rolls is the GM's, and the line says which fact is
        // missing rather than that the question was considered.</b> An attack names a Trait and no
        // sheet anywhere says which Trait a weapon backs.
        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"what losing {item} means for {target.Name}'s own attacks is the GM's: an attack here "
            + "names a Trait and nothing in this repository's rules data says which Trait a weapon "
            + "backs, so no figure of theirs is changed"));

        if (target.Holding is { } theirs
            && string.Equals(theirs.Name, item, StringComparison.OrdinalIgnoreCase))
        {
            state = state.With(target.Dropped());
        }

        if (actor.Holding is { } already
            && !string.Equals(already.Name, item, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{actor.Name} lets go of {already.Name} to take {item}: this engine models a pair of "
                + "hands and one object in them, which is what p.76 is about"));
        }

        return state.With(actor.Holds(item, state.Page));
    }

    private EncounterState ApplyEscape(
        EncounterState state, Combatant actor, string result, List<LedgerLine> lines)
    {
        if (result.Contains("no effect", StringComparison.Ordinal)) return state;

        var entry = _play.GetCombat("escape");
        var escape = entry.Escape!;

        // p.77 defines an escape as getting out of a hold. A grab is left where it is: the two are
        // fighting over an item, and letting go of it is the exit the grab entry prints.
        var held = state.Grapples
            .Where(g => g.Move == GrappleMove.Hold
                        && string.Equals(g.Held, actor.Id, StringComparison.Ordinal))
            .ToList();

        var grapples = state.Grapples.ToList();

        foreach (var grapple in held)
        {
            grapples.Remove(grapple);

            // p.77: a partial escape slips out of a partial hold and downgrades a full one.
            if (!result.Contains("full", StringComparison.Ordinal) && grapple.Kind == GrappleKind.Full)
            {
                grapples.Add(grapple with { Kind = GrappleKind.Partial });

                lines.Add(new LedgerLine(
                    state.Page, actor.Id, entry.Id, entry.SourceRef,
                    $"a partial escape out of a full hold: {escape.PartialOutOfAFullHold}"));

                continue;
            }

            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{actor.Name} is out of the hold"));
        }

        return state with { Grapples = grapples };
    }

    /// <summary>
    /// Whether a grab is aimed at something its target is not recorded as holding, refused on the
    /// ledger with nothing rolled.
    ///
    /// <para><b>p.76 aims a grab at "a weapon or other handheld item away from your opponent", and
    /// the engine used to take the object's existence off the grabber's own say-so.</b> The result
    /// was a line that lied in both directions: on three net successes the winner held an object
    /// nobody had ever been recorded as carrying, and the ledger said in as many words what losing it
    /// meant for a loser who had never had it. Nothing on the wire could open a fight holding
    /// anything, so <em>every</em> grab a caller made was of that kind.</para>
    ///
    /// <para><b>So the target's hand is a fact and it is the caller's word</b>, the way
    /// <see cref="Combatant.Size"/> and <see cref="Combatant.Invisible"/> are:
    /// <see cref="Combatant.Carrying"/> puts it there at the start of the fight and a full grab moves
    /// it afterwards. The refusal names what the target <em>is</em> holding, because "they have not
    /// got that" sends a caller guessing.</para>
    ///
    /// <para><b>The contested object of a partial grab is the one exception, and it is the page's
    /// own.</b> A half-measure leaves the item in nobody's hands and has the pair "make opposed Might
    /// rolls on your turn to act to try gaining control" — so the second roll of a deadlock is aimed
    /// at an object neither of them holds, and refusing it would make the printed way out of a
    /// partial grab unreachable.</para>
    /// </summary>
    private EncounterState? GrabbingWhatTheyDoNotHold(
        EncounterState state, Combatant actor, Combatant target, string item, CombatEntry entry,
        CombatGrapplingModel rule, List<LedgerLine> lines)
    {
        if (target.Holding is { } theirs
            && string.Equals(theirs.Name, item, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Contested(state, actor.Id, target.Id, item)) return null;

        return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{target.Name} is not holding {item} — "
            + (target.Holding is { } other
                ? $"what they have in their hands is {other.Name}"
                : "they are recorded as holding nothing")
            + $". A grab is {rule.AGrabIs}, so there has to be something of theirs to take: say what "
            + "a combatant walked in with when the fight is opened, or take it off them with a grab "
            + "first. Nothing was rolled");
    }

    /// <summary>
    /// Whether a standing partial grab between these two is over this object — p.76's "you and your
    /// opponent are fighting over an item".
    /// </summary>
    private static bool Contested(EncounterState state, string a, string b, string item) =>
        state.Grapples.Any(g =>
            g.Move == GrappleMove.Grab
            && g.Kind == GrappleKind.Partial
            && Between(g, a, b)
            && string.Equals(g.Item, item, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether an attack is made with the object a partial grab is being fought over, refused on the
    /// ledger citing <c>grab</c>'s own <c>partial_means</c>.
    ///
    /// <para><b>p.76 states the prohibition and this engine could not see it, until an attack could
    /// name an item.</b> "A partial grab means you and your opponent are fighting over an item. They
    /// can't use it, but neither can you." The half of the deadlock this engine already applied is
    /// the defences — neither has an active one against anybody else — and the prohibition on
    /// <em>using</em> it was recorded as unenforceable because no roll named a weapon. One does now:
    /// <see cref="Attack.Item"/> names exactly the object the contest is over, and the character who
    /// walked in with it is still recorded as holding it, so without this the printed sentence would
    /// be false in the one case the page wrote it for.</para>
    ///
    /// <para><b>It refuses an attack and not a toss</b>, because the same paragraph prints the exit:
    /// "you can exit grappling combat at any time by letting go of the object". Using it is what the
    /// deadlock forbids; letting go of it is what the deadlock is escaped by.</para>
    ///
    /// <para>Both parties are refused and neither is refused for the other's reason — the grabber is
    /// not holding the object either, so this has to be asked before the hand is.</para>
    /// </summary>
    private EncounterState? FightingOverIt(
        EncounterState state, Combatant actor, string item, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("grab");

        var contest = state.Grapples.FirstOrDefault(g =>
            g.Move == GrappleMove.Grab
            && g.Kind == GrappleKind.Partial
            && string.Equals(g.Item, item, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(g.Holder, actor.Id, StringComparison.Ordinal)
                || string.Equals(g.Held, actor.Id, StringComparison.Ordinal)));

        if (contest is null) return null;

        var other = string.Equals(contest.Holder, actor.Id, StringComparison.Ordinal)
            ? contest.Held
            : contest.Holder;

        return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{actor.Name} cannot attack with {item}: a partial grab is {entry.Grab!.PartialMeans}, "
            + $"and {state[other].Name} has hold of the other end of it. "
            + $"{nameof(entry.Grab.MayExitByLettingGoOfTheObject)} is "
            + $"{entry.Grab.MayExitByLettingGoOfTheObject}, so tossing it is one way out and "
            + $"{entry.Grab.PartialResolvedBy} is the other. Nothing was rolled");
    }

    /// <summary>
    /// Whether an intent names an item its actor is not holding, refused on the ledger citing
    /// <c>grab</c> — the only entry in Chapters 3–5 that puts an object in anybody's hands.
    ///
    /// <para><b>The refusal says what they <em>are</em> holding, because "you do not have that" sends
    /// a caller guessing.</b> A model that has lost track of a grab it made two pages ago has to be
    /// able to correct itself from the answer.</para>
    /// </summary>
    private EncounterState? UsingWhatTheyDoNotHold(
        EncounterState state, Combatant actor, string item, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("grab");

        if (actor.Holding is { } held
            && string.Equals(held.Name, item, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{actor.Name} is not holding {item} — "
            + (actor.Holding is { } other
                ? $"what they have in their hands is {other.Name}"
                : "they are recorded as holding nothing")
            + $". A full grab is {entry.Grab!.FullMeans}, and the only other way anybody here holds "
            + "anything is by having walked into the fight with it, which is said when the fight is "
            + "opened. An item this engine does not know about is one it cannot resolve anything "
            + "with. Nothing was rolled");
    }

    /// <summary>
    /// p.76's "you gain control of the object and can use it ... on that same page": the attack that
    /// spends it, which changes no figure and stops the page turn taking the item away.
    /// </summary>
    private EncounterState UseTheItem(
        EncounterState state, Combatant actor, string item, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("grab");
        var grab = entry.Grab!;
        var held = actor.Holding!;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} attacks with {item}, which "
            + (held.CarriedIn
                ? "they brought into the fight"
                : $"a full grab won them on page {held.WonOnPage}")
            + ": "
            + $"{nameof(grab.FullAllowsUsingOrTossingItTheSamePage)} is "
            + $"{grab.FullAllowsUsingOrTossingItTheSamePage} and it does not spend their turn. "
            + "No figure of this attack is changed by it — nothing in this repository's rules data "
            + "gives a weapon a bonus or says which Trait it backs — and "
            + (held.CarriedIn
                ? "no grab won it, so p.76's one page was never a limit on it"
                : "having been used, it stays with them past the page turn, where what becomes of "
                  + "it is the GM's")));

        return state.With(actor.Used());
    }

    /// <summary>
    /// p.76's other half of the same sentence: throwing the object away.
    ///
    /// <para><b>It goes through the same grapple guard every other action does.</b> A character in a
    /// full hold may only try to escape and one in a partial hold has one physical action, and
    /// throwing something away is neither — the free-action clause is an exemption from the multiple
    /// action penalty, not from being pinned.</para>
    /// </summary>
    private EncounterState ResolveToss(EncounterState state, Toss toss, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, toss.Actor, lines)) return state;
        if (OutOfTheFight(state, toss.Actor, "actor", lines)) return state;
        if (GrappleForbids(state, toss.Actor, null, lines)) return state;

        var actor = state[toss.Actor];
        var item = toss.Item?.Trim() ?? "";

        // p.76's printed exit from the deadlock, and it is the same intent because it is the same
        // act: "you can exit grappling combat at any time by letting go of the object."
        if (LettingGo(state, actor, item, lines) is { } loosed) return loosed;

        if (UsingWhatTheyDoNotHold(state, actor, item, lines) is { } empty) return empty;

        var entry = _play.GetCombat("grab");
        var grab = entry.Grab!;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} tosses {actor.Holding!.Name} aside, which "
            + (actor.Holding.CarriedIn
                ? "they walked into the fight with — no grab won it, so p.76's page was never a "
                  + "limit on it"
                : $"{nameof(grab.FullAllowsUsingOrTossingItTheSamePage)} allows on the page a full "
                  + "grab won it")
            + $", and {nameof(grab.FullSuffersTheMultipleActionPenalty)} is "
            + $"{grab.FullSuffersTheMultipleActionPenalty} — so it does not spend their turn. Nobody "
            + "holds it now; where it landed is the GM's"));

        return state.With(actor.Dropped());
    }

    /// <summary>
    /// p.76's exit from a partial grab: "you can exit grappling combat at any time by letting go of
    /// the object" — and the state that ends with it.
    ///
    /// <para><b>Without this a partial grab was a deadlock nothing could end.</b> The half-measure
    /// takes both characters' active defences away against everybody else and is otherwise settled
    /// only by somebody rolling three net successes; the character who had walked in with the object
    /// could toss it and stay tangled, and the one grabbing at it could not toss it at all, because
    /// a toss was refused unless the actor was holding what they named. So a fight in which neither
    /// ever rolled a full grab was a fight in which two characters could not dodge anybody for the
    /// rest of it — which is exactly the defect a full grab replacing a partial one the other way
    /// round was, reached by another route.</para>
    ///
    /// <para><b>Nobody gains control of it, and that is the half worth stating.</b> Only a full grab
    /// is "control of the object"; treating a concession as one would be inventing a fourth outcome
    /// for a table that prints three. The character who let go is not holding it, whoever else had
    /// hold of it is not either, and where it landed is the GM's — the same silence a toss already
    /// keeps.</para>
    ///
    /// <para>It ends only the grab between these two over this object. A hold is untouched: p.77
    /// gets out of one of those with an escape, and letting go of an item is not one.</para>
    /// </summary>
    private EncounterState? LettingGo(
        EncounterState state, Combatant actor, string item, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("grab");
        var grab = entry.Grab!;

        var contest = state.Grapples.FirstOrDefault(g =>
            g.Move == GrappleMove.Grab
            && g.Kind == GrappleKind.Partial
            && string.Equals(g.Item, item, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(g.Holder, actor.Id, StringComparison.Ordinal)
                || string.Equals(g.Held, actor.Id, StringComparison.Ordinal)));

        if (contest is null) return null;

        var other = string.Equals(contest.Holder, actor.Id, StringComparison.Ordinal)
            ? contest.Held
            : contest.Holder;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} lets go of {item}, and "
            + $"{nameof(grab.MayExitByLettingGoOfTheObject)} is "
            + $"{grab.MayExitByLettingGoOfTheObject} — so the partial grab with "
            + $"{state[other].Name} is over and both of them have their active defences against "
            + "everybody else back. Nobody has control of it: a full grab is "
            + $"{grab.FullMeans} and this was not one, so where it landed is the GM's"));

        // Whoever was recorded as holding it is not any more — which is only ever the character who
        // walked into the fight with it, since a partial grab moves nothing.
        var after = state with
        {
            Grapples = [.. state.Grapples.Where(g => !ReferenceEquals(g, contest))]
        };

        foreach (var id in new[] { actor.Id, other })
        {
            if (after[id].Holding is { } held
                && string.Equals(held.Name, item, StringComparison.OrdinalIgnoreCase))
            {
                after = after.With(after[id].Dropped());
            }
        }

        return after;
    }

    private EncounterState ResolveBreakFree(EncounterState state, BreakFree free, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, free.Actor, lines)) return state;
        if (OutOfTheFight(state, free.Actor, "actor", lines)) return state;
        if (GrappleForbids(state, free.Actor, null, lines)) return state;

        var entry = _play.GetCombat("breaking_free");
        var rule = entry.BreakingFree!;
        var actor = state[free.Actor];

        var effect = state.Effects.FirstOrDefault(e =>
            string.Equals(e.Target, free.Actor, StringComparison.Ordinal));

        if (effect is null)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} is not suffering a special effect, so there is nothing to break free of");
        }

        var roll = _counter.Roll(actor.Rank(free.TraitId) + WoundPenalty(state, actor, lines), _dice);
        var net = roll.Successes - free.Threshold;
        var removed = net > 0 ? Half(net, entry.Interpretation!.ReductionRounds!) : 0;
        var remaining = Math.Max(0, effect.RemainingPages - removed);

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} rolls {free.TraitId} for {roll.Successes} against the Power's rank of "
            + $"{free.Threshold}: {net} net removes {removed} of the {effect.RemainingPages} pages, "
            + $"leaving {remaining}"));

        var effects = state.Effects.Where(e => !ReferenceEquals(e, effect)).ToList();

        if (remaining > rule.FreeWhenTheDurationReaches)
        {
            effects.Add(effect with { RemainingPages = remaining });
            return state with { Effects = effects };
        }

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} is free, and may act on this page"));

        return state with { Effects = effects };
    }

    // ── Spending ─────────────────────────────────────────────────────────────

    private EncounterState ResolveResolveSpend(EncounterState state, SpendResolve spend, List<LedgerLine> lines)
    {
        var actor = state[spend.Actor];

        if (!actor.HoldsResolve)
        {
            var combat = _play.GetResolve("spend_combat");

            return Refuse(state, actor.Id, combat.Id, combat.SourceRef, lines,
                $"{actor.Name} is a {actor.Kind} and holds no Resolve — only Heroes have any, and "
                + "the GM spends Adversity on an NPC instead");
        }

        return spend.Kind switch
        {
            ResolveSpend.ExtraDice => BuyDice(state, actor, spend.Points, lines),
            ResolveSpend.Reroll => BuyReroll(state, actor, lines),
            ResolveSpend.SeizeInitiative => SeizeInitiative(state, actor, lines),
            ResolveSpend.AvoidFatalDamage => AvoidFatalDamage(state, actor, lines),
            ResolveSpend.Stabilise => StabiliseWithResolve(state, actor, lines),
            ResolveSpend.InstantRecovery => InstantRecovery(state, actor, lines),
            ResolveSpend.KeepingHold => KeepHold(state, actor, lines),
            ResolveSpend.Knockback => Knockback(state, actor, lines),
            ResolveSpend.Luring => Lure(state, actor, spend.Target, lines),
            ResolveSpend.TeamAttack => ExplodeTheSixes(state, actor, lines),
            var other => throw new ArgumentOutOfRangeException(
                nameof(spend), other,
                "Every Resolve purchase Chapters 4 and 5 print is resolved, so a member of the enum "
                + "with no branch here is a purchase nobody has written.")
        };
    }

    /// <summary>
    /// Ch.5 p.84: one point, one die, on a roll that has already been made.
    ///
    /// <para>The dice are added to the attack pool and the outcome recomputed against the target as
    /// they were, which is why <see cref="EncounterState.LastAttack"/> keeps that snapshot.</para>
    ///
    /// <para><b>The pool grows, and that is the half that was missing.</b> A bought die is part of
    /// the roll from the moment it is bought — p.84 offers both purchases on "a challenge roll" and
    /// draws no line round the dice that were there first — so a reroll after a purchase picks the
    /// whole pool back up. Adding the successes and leaving <c>AttackPool</c> where it was meant a
    /// Hero who spent two points on a 10d attack and then bought a reroll threw ten dice for it and
    /// silently lost what they had paid for.</para>
    /// </summary>
    private EncounterState BuyDice(
        EncounterState state, Combatant actor, int points, List<LedgerLine> lines,
        bool fromAdversity = false)
    {
        var entry = _play.GetResolve("spend_challenge_roll_dice");
        var spend = entry.Spend!;

        if (state.LastAttack is not { } last || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has no roll on the table to buy dice for");
        }

        // <b>This is the one purchase whose price is the caller's figure, so it is the one that can
        // be priced backwards.</b> The rate below multiplies, and <see cref="Combatant.Spending"/>
        // subtracts what it is handed — so a Hero buying −5 dice was charged −5 Resolve, which is
        // five points of Resolve arriving out of a purchase. <see cref="CannotAfford"/> could not
        // see it either: a pool is always at least a negative cost. p.84 prices a die at a point and
        // gives a die for it; a request for none or for fewer than none is not a purchase the page
        // has any reading of.
        if (points < 1)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} asked for {points} dice, and p.84 buys {spend.DiceGained} die for "
                + $"{spend.CostResolve} point — a purchase of fewer than one is not one");
        }

        var cost = spend.CostResolve!.Value * points;
        var extra = spend.DiceGained!.Value * points;

        if (!fromAdversity && CannotAfford(state, actor, cost, entry.Id, entry.SourceRef, lines)) return state;

        var roll = _counter.Roll(extra, _dice);

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, cost, fromAdversity)
            + $" for {extra} more dice after the roll, scoring "
            + $"{roll.Successes} more: {last.AttackSuccesses} becomes {last.AttackSuccesses + roll.Successes}, "
            + $"and the roll on the table is now {last.AttackPool + extra}d"));

        // <b>The faces go on the roll beside the successes, because they are one roll.</b>
        // `includes_dice_bought_with_resolve` says a bought die is one of the roll's own, which is
        // why the pool grows; its face is the same claim from the other side. p.79 explodes the
        // roll's sixes, and a six a Hero paid for that was not on `AttackFaces` was a six the
        // purchase could not see.
        var improved = last with
        {
            AttackPool = last.AttackPool + extra,
            AttackSuccesses = last.AttackSuccesses + roll.Successes,
            AttackFaces = [.. last.AttackFaces, .. roll.Faces]
        };

        var after = Charge(state, actor, cost, fromAdversity);
        after = ReapplyLastAttack(after, improved, lines);

        return after with { LastAttack = improved };
    }

    /// <summary>
    /// Takes the price out of whichever pool is paying: a Hero's Resolve, or the GM's Adversity.
    ///
    /// <para>p.85's first purchase is "whatever a point of Resolve could have done, on behalf of any
    /// NPC", so the two are the same purchase with different money behind them — and an NPC has no
    /// Resolve pool to draw on, which <see cref="Combatant.Spending"/> makes unconstructible rather
    /// than merely wrong.</para>
    /// </summary>
    private static EncounterState Charge(
        EncounterState state, Combatant actor, int cost, bool fromAdversity) =>
        fromAdversity ? ChargeAdversity(state, cost) : state.With(actor.Spending(cost));

    /// <summary>
    /// How a ledger line says a purchase was paid for: whose pool paid, and how much.
    ///
    /// <para><b>One helper rather than ten copies of a ternary.</b> All ten purchases now say the
    /// same sentence in the two currencies, and a copy that drifted would report a Hero's Resolve
    /// leaving the GM's pool. The strings are the ones the first six already printed.</para>
    /// </summary>
    private static string Paid(Combatant actor, int cost, bool fromAdversity) =>
        fromAdversity
            ? $"the GM spends {cost} Adversity on {actor.Name}"
            : $"{actor.Name} spends {cost} Resolve";

    /// <summary>
    /// The field a Minion refusal quotes, required to still say what the refusal says it says.
    ///
    /// <para><b>Each of the four refusals below is a claim about a printed page, and it makes the
    /// claim by quoting the field it was derived from</b> — "p.77 gives them no Health at all
    /// (minions_have_health is False)". The quote is interpolated, so an entry corrected the other
    /// way would go on refusing and print <em>its own contradiction</em>: a ledger line saying a
    /// group has no Health because the field says they have. A refusal is a rule applied, and a rule
    /// applied against an entry that no longer supports it is the failure this store exists to
    /// prevent — so it throws, the way <c>gritty_the_drop</c>'s and
    /// <c>seize_initiative_gm_alternative</c>'s printed words already do when they go away.</para>
    ///
    /// <para>The four fields are <c>edge_ties.minions_have_an_edge</c> and <c>minions_act</c>, which
    /// p.73's refusal reads, and <c>attacking_minions.minions_have_health</c>, which p.77's two
    /// read. Turning any of them round makes the purchase applicable rather than refusable, which is
    /// a rule this engine would have to be taught rather than one it may keep declining.</para>
    /// </summary>
    private static void TheEntryMustStillSay(bool holds, string entryId, string field, object reads)
    {
        if (holds) return;

        throw new InvalidOperationException(
            $"{entryId}'s {field} now reads '{reads}'. This engine refuses p.85's first purchase for "
            + "a group of Minions on the strength of that field, and the ledger line quotes it — so "
            + "an entry that no longer says it is a rule this engine cannot go on applying, and the "
            + "refusal would print its own contradiction. See docs/guide/play-engine.md's readings "
            + "table.");
    }

    /// <summary>
    /// p.85's first purchase against a group of Minions, refused by name where the page gives the
    /// group nothing to buy.
    ///
    /// <para><b>A Minion group reaches these four purchases because p.85 sends it there</b> — the
    /// pool is spent "on behalf of any NPC whether they're Villains, Foes, Minions, or Extras", so
    /// <c>npc_kinds</c> admits one — and four of the ten have nothing for one when it arrives. A
    /// point taken for a state change nothing could receive is the defect this engine's ledger rules
    /// exist to prevent, and it is the argument p.79's luring refuses a lure that names nobody on.
    /// </para>
    ///
    /// <para>The reason is the purchase's own and is passed in; the rule cited is the entry that
    /// gives the group nothing, so a reader of the line lands on the page that decided it rather
    /// than on the page selling the purchase.</para>
    /// </summary>
    private static EncounterState RefuseTheMinions(
        EncounterState state, Combatant actor, string ruleId, string sourceRef,
        List<LedgerLine> lines, string why) =>
        Refuse(state, actor.Id, ruleId, sourceRef, lines,
            $"{actor.Name} is a group of Minions, and {why} — so a point of Adversity would buy "
            + "them nothing here, and it is refused rather than spent");

    /// <summary>
    /// Ch.5 p.84: one point picks the whole roll back up — and p.85's tip puts a floor under it, so
    /// a worse reroll is discarded and the first roll stands.
    /// </summary>
    private EncounterState BuyReroll(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetResolve("spend_reroll_challenge_roll");
        var floor = _play.GetResolve("reroll_floor");

        if (state.LastAttack is not { } last || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has no challenge roll on the table to pick back up");
        }

        var cost = entry.Spend!.CostResolve!.Value;

        // <b>That a reroll picks up the dice a Hero bought is the entry's own field, not a
        // reading.</b> `includes_dice_bought_with_resolve` says so in as many words, which is why
        // `BuyDice` grows `AttackPool` rather than leaving it where the attack was rolled. This
        // engine keeps one pool figure and could not separate the bought dice from the rest, so an
        // entry that said otherwise is a rule it cannot apply.
        if (entry.Spend.IncludesDiceBoughtWithResolve != true)
        {
            throw new InvalidOperationException(
                "spend_reroll_challenge_roll's includes_dice_bought_with_resolve is now "
                + $"'{entry.Spend.IncludesDiceBoughtWithResolve}'. This engine keeps the roll on the "
                + "table as one pool and cannot strand the dice that were bought on their first "
                + "result.");
        }

        if (!fromAdversity && CannotAfford(state, actor, cost, entry.Id, entry.SourceRef, lines)) return state;

        var roll = _counter.Roll(last.AttackPool, _dice);

        var kept = floor.RerollFloor!.KeepTheFirstRollIfTheRerollIsWorse
            ? Math.Max(last.AttackSuccesses, roll.Successes)
            : roll.Successes;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, cost, fromAdversity)
            + $" to reroll {last.AttackPool}d: {roll.Successes} "
            + $"against the first {last.AttackSuccesses}, keeping {kept}"));

        // <b>The faces follow whichever roll is kept, because the faces are that roll's.</b> p.85's
        // floor discards the reroll when it comes up worse, and the first roll's faces are then the
        // ones on the table; where the reroll stands, the dice it threw away are gone with it. An
        // engine that moved the count and left the faces alone let p.79's explosion reroll the sixes
        // of a roll nobody is looking at any more.
        var improved = last with
        {
            AttackSuccesses = kept,
            AttackFaces = kept == roll.Successes ? roll.Faces : last.AttackFaces
        };

        var after = Charge(state, actor, cost, fromAdversity);
        after = ReapplyLastAttack(after, improved, lines);

        return after with { LastAttack = improved };
    }

    private EncounterState ReapplyLastAttack(
        EncounterState state, ResolvedAttack resolved, List<LedgerLine> lines)
    {
        var attack = new Attack(
            resolved.Actor, resolved.Target, "(already rolled)", resolved.Damage, resolved.Type,
            Effect: resolved.Effect, Area: resolved.Area);

        return ApplyAttackOutcome(state, attack, resolved, lines);
    }

    /// <summary>
    /// Ch.4 p.76's <c>keeping_hold</c>: a point carries an effect that has put somebody out past the
    /// end of this scene and into the next, and buying it again carries it further still.
    ///
    /// <para><b>What it changes is the effect's clock, not a line about the effect's clock.</b> The
    /// entry's <c>extends_to</c> is "the end of the following scene", and a scene is longer than any
    /// page count this engine has — an encounter <em>is</em> the scene, which is what
    /// <c>defeat_by_effect_lasts</c> and instant recovery's once-a-scene limit already mean here. So
    /// a kept effect stops being measured in pages: <see cref="ResolveEndPage"/> leaves it alone
    /// instead of ticking it down, and it is still running when the fight ends.</para>
    ///
    /// <para><b>The trigger is the entry's and is checked against the state rather than trusted.</b>
    /// "Whenever you defeat a target with a special effect" — so the buyer must have an effect of
    /// their own running on somebody whom that same effect has put out, which is
    /// <see cref="Combatant.DefeatedByEffect"/> and nothing else. An effect that merely landed buys
    /// nothing.</para>
    ///
    /// <para><b>A buyer who is out of the fight is refused</b>, which is the one place these four
    /// Chapter 4 purchases part company with Chapter 5's: p.76's instant recovery and p.79's rescue
    /// are what a character who has just gone down buys, and keeping a hold, knocking somebody
    /// across the street, luring and leading a team attack are things a character does while they
    /// are still in it.</para>
    /// </summary>
    private EncounterState KeepHold(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetCombat("keeping_hold");
        var rule = entry.KeepingHold!;

        if (OutOfTheFight(state, actor.Id, "buyer", lines)) return state;

        var held = state.Effects
            .Where(e => string.Equals(e.Source, actor.Id, StringComparison.Ordinal)
                        && string.Equals(
                            state[e.Target].DefeatedByEffect, e.Name, StringComparison.Ordinal))
            .OrderBy(e => e.Target, StringComparer.Ordinal)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (held is null)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has nobody down under an effect of theirs, and this buys "
                + $"{rule.ExtendsTo} for {rule.Trigger}");
        }

        if (!fromAdversity
            && CannotAfford(state, actor, rule.CostResolve, entry.Id, entry.SourceRef, lines))
        {
            return state;
        }

        var kept = held with { KeptScenes = held.KeptScenes + 1 };

        var effects = state.Effects
            .Select(e => ReferenceEquals(e, held) ? kept : e)
            .ToList();

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, rule.CostResolve, fromAdversity)
            + $" to keep hold of {state[held.Target].Name}: the {held.Name} that put them out now "
            + $"lasts to {rule.ExtendsTo}, and stops counting down in pages"
            + (rule.MayBeRepeatedSceneAfterScene
                ? $" — bought {kept.KeptScenes} time{(kept.KeptScenes == 1 ? "" : "s")}, and it may "
                  + "be bought again scene after scene"
                : "")));

        return Charge(state, actor, rule.CostResolve, fromAdversity) with { Effects = effects };
    }

    /// <summary>
    /// Ch.4 p.78's <c>knockback</c>: a point turns a heavy subdual blow into a flight, and both
    /// halves of what the page says happens are applied to the state.
    ///
    /// <para><b>How far is read off the throwing table, because the entry points at it.</b> The
    /// target is thrown "as if they were thrown by someone with a Might rank equal to your attack
    /// rank", and p.74 says what a Might that size throws something: at or below
    /// <c>throwing_range.table_used_when_might_exceeds</c> it is
    /// <c>ordinary_people_reach</c>, and above it the <c>throwing_table</c> row the rank falls in.
    /// That is why the table's own <c>ambiguity</c> — nothing printed below 3d — never bites here:
    /// the sentence above the table covers every rank up to six.</para>
    ///
    /// <para><b>The target's weight rank is the one figure the data cannot supply</b>, and
    /// <c>docs/guide/play-engine.md</c> records the reading. <c>throwing_range.rank_formula</c>
    /// subtracts the object's weight rank from the thrower's Might; nothing in Chapters 3–5 gives a
    /// character a weight rank, so this reads the throwing rank as the attack rank itself, which is
    /// the longest throw the sentence can mean. The ledger line says so.</para>
    ///
    /// <para><b>What is not applied is the object.</b>
    /// <c>damage_on_striking_a_solid_object</c>, <c>the_object_must_be_tougher_than_the_target</c>
    /// and <c>a_passive_defense_above_the_objects_structure</c> all need a piece of scenery with a
    /// Structure, and this engine has no scenery and no Structure — there is nothing to hit and
    /// nothing to compare a passive defence with. The line names the clause rather than leaving a
    /// reader to assume the extra damage was rolled.</para>
    /// </summary>
    private EncounterState Knockback(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetCombat("knockback");
        var rule = entry.Knockback!;

        if (OutOfTheFight(state, actor.Id, "buyer", lines)) return state;

        if (state.LastAttack is not { } last
            || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has no blow of their own on the table to turn into a knockback");
        }

        if (!Enum.TryParse<DamageKind>(rule.RequiresDamageType, ignoreCase: true, out var required))
        {
            throw new InvalidOperationException(
                $"knockback requires '{rule.RequiresDamageType}' damage, which is none of the kinds "
                + $"p.75 prints: {string.Join(", ", Enum.GetNames<DamageKind>())}.");
        }

        if (last.Damage != required)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"knockback is bought off {rule.RequiresDamageType} damage and that blow was "
                + $"{last.Damage.ToString().ToLowerInvariant()}");
        }

        var target = state[last.Target];
        var rate = _play.GetCombat("damage").Damage!.DamagePerNetSuccess;

        // The damage the blow actually did: an attack carrying a special effect does none, and a
        // Minion group takes bodies off rather than Health.
        var inflicted = last.Effect is null && target.Kind != CombatantKind.MinionGroup
            ? Math.Max(0, last.AttackSuccesses - last.DefenceSuccesses) * rate
            : 0;

        if (inflicted < rule.MinimumDamage)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"that blow did {inflicted} damage and knockback needs {rule.MinimumDamage}");
        }

        if (!fromAdversity
            && CannotAfford(state, actor, rule.CostResolve, entry.Id, entry.SourceRef, lines))
        {
            return state;
        }

        var (thrown, printed, past) = ThrowReach(last.AttackRank);
        var here = state.RangeBetween(actor.Id, target.Id);

        // They fly backwards, so the pair cannot end up nearer than they started.
        var landed = (RangeBand)Math.Max((int)here, (int)thrown);

        var after = state.WithRange(actor.Id, target.Id, landed);

        if (rule.TargetLosesTheirNextTurnToAct) after = ForfeitNextTurn(after, target.Id);

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, rule.CostResolve, fromAdversity)
            + $" to knock {target.Name} back off {inflicted} points of "
            + $"{rule.RequiresDamageType} damage: thrown by {rule.TargetIsThrownAsIfByAMightRankEqualTo} "
            + $"of {last.AttackRank}, which p.74 reaches {printed}"
            + (past ? ", further than this engine's outermost range class" : "")
            + $", leaving them at {landed} Range"
            + (rule.TargetFallsProne ? ", prone" : "")
            + (rule.TargetLosesTheirNextTurnToAct ? " and out of their next turn to act" : "")
            + $". Striking something solid would cost them {rule.DamageOnStrikingASolidObject}, and "
            + "that is not applied: this engine has no scenery and nothing in it has a Structure to "
            + "measure a passive defence against"));

        return Charge(after, actor, rule.CostResolve, fromAdversity);
    }

    /// <summary>
    /// How far a throw of <paramref name="rank"/> reaches, out of p.74's sentence and p.74's table.
    ///
    /// <para>The band is resolved against <c>range_classes</c>'s own rows rather than parsed as an
    /// enum name, so the ladder the fight uses and the ladder the throw lands on are the same one.
    /// A row past the outermost class — the table's <c>25</c> and up — is reported as past the
    /// ladder and clamped to it, because this engine's furthest apart is its outermost class.</para>
    /// </summary>
    private (RangeBand Band, string Printed, bool Past) ThrowReach(int rank)
    {
        var throwing = _play.GetCombat("throwing_range").Throwing!;
        var table = _play.GetCombat("throwing_table").ThrowingTable!;

        var printed = rank > throwing.TableUsedWhenMightExceeds
            ? table.SingleOrDefault(r => rank >= r.MinRank && (r.MaxRank is null || rank <= r.MaxRank))?.Range
              ?? throw new InvalidOperationException(
                  $"throwing_table has no row for a throwing rank of {rank}, and p.74 sends every "
                  + $"rank above {throwing.TableUsedWhenMightExceeds} to it.")
            : throwing.OrdinaryPeopleReach;

        var classes = _play.GetCombat("range_classes").Ranges!.Select(c => c.Class).ToList();
        var index = classes.IndexOf(printed.Replace(" Range", "", StringComparison.Ordinal));

        return index >= 0
            ? ((RangeBand)index, printed, false)
            : ((RangeBand)(classes.Count - 1), printed, true);
    }

    /// <summary>
    /// "Losing their next turn to act" (p.78, and p.79 for the lurer), applied to the order.
    ///
    /// <para><b>Which turn it is depends on where the page has got to</b>, and both cases are the
    /// same sentence: a character who has still to act on this page loses that turn, so they come
    /// out of the order now; one who has already acted loses the next page's, so their id waits on
    /// <see cref="EncounterState.LosesNextTurn"/> until the page turns. Removing somebody after the
    /// current index leaves the index pointing at the same combatant it did.</para>
    /// </summary>
    private static EncounterState ForfeitNextTurn(EncounterState state, string id)
    {
        var at = -1;

        for (var i = 0; i < state.TurnOrder.Count; i++)
        {
            if (!string.Equals(state.TurnOrder[i], id, StringComparison.Ordinal)) continue;

            at = i;
            break;
        }

        if (at > state.TurnIndex)
        {
            return state with { TurnOrder = [.. state.TurnOrder.Where((_, i) => i != at)] };
        }

        return state.LosesNextTurn.Contains(id, StringComparer.Ordinal)
            ? state
            : state with { LosesNextTurn = [.. state.LosesNextTurn, id] };
    }

    /// <summary>
    /// Ch.4 p.79's <c>luring</c>: the attack the buyer has just dodged goes into somebody standing
    /// behind them instead, and the buyer forgoes their next turn to act for it.
    ///
    /// <para><b>Every one of the entry's four conditions is checked against the roll that happened.</b>
    /// The attack has to have been aimed at the buyer, it has to be one of
    /// <c>applies_to_attack_types</c>, the defence has to have been an active one
    /// (<c>requires_an_active_defense</c>), and it has to have beaten the attack by
    /// <c>defense_must_exceed_the_attack_roll_by</c>. The new target then makes their own defence
    /// roll — <c>the_new_target_makes_their_own_defense_roll</c> — against the same attack roll,
    /// and whatever that leaves is applied to them.</para>
    ///
    /// <para><b>Two readings, both in the guide.</b> <c>declared_before</c> is "the attacker makes
    /// their attack roll", and this engine has no point between declaring an attack and resolving
    /// it: a <see cref="Step"/> is the whole exchange. So the purchase is decided on the roll that
    /// has just happened, which is where p.79 puts the <em>payment</em> anyway — "if your defense
    /// roll exceeds their attack roll by 3 or more, you can spend 1 Resolve". And "a physical or
    /// energy attack" is neither of the words p.75's table prints, so it is read as every row of
    /// that table but the mental one, derived from the table rather than listed here.</para>
    ///
    /// <para><b>A lure into the scenery is refused rather than charged for.</b>
    /// <c>redirects_to</c> is "whatever lies directly behind you", and this engine has no scenery,
    /// no Structure and nothing behind anybody — the attack had already missed the buyer, so a
    /// point taken for it would buy a state change nothing could receive. Naming a person is what
    /// this engine can do, and the refusal says so.</para>
    /// </summary>
    private EncounterState Lure(
        EncounterState state, Combatant actor, string? onto, List<LedgerLine> lines,
        bool fromAdversity = false)
    {
        var entry = _play.GetCombat("luring");
        var rule = entry.Luring!;

        if (OutOfTheFight(state, actor.Id, "buyer", lines)) return state;

        if (state.LastAttack is not { } last
            || !string.Equals(last.Target, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"nothing has just been aimed at {actor.Name}, and luring is bought off an attack "
                + "that was");
        }

        var mental = _play.GetCombat("attack_and_defense_table").AttackDefenseTable!
            .Single(r => r.Type.Contains("Mental", StringComparison.Ordinal));

        if (string.Equals(PrintedType(last.Type), mental.Type, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"luring is for {string.Join(" or ", rule.AppliesToAttackTypes)} attacks, and a "
                + $"{mental.Type} is neither — there is nothing behind you for it to strike");
        }

        if (rule.RequiresAnActiveDefense && !last.DefenceWasActive)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} answered that attack with a passive defence, and luring is moving out "
                + "of the way at the last instant");
        }

        var margin = last.DefenceSuccesses - last.AttackSuccesses;

        if (margin < rule.DefenseMustExceedTheAttackRollBy)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} beat that attack by {margin} and luring asks for "
                + $"{rule.DefenseMustExceedTheAttackRollBy}");
        }

        if (onto is not { Length: > 0 })
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"a lure sends the attack into {rule.RedirectsTo}, and this engine has no scenery "
                + "to send it into — name somebody to lure it onto instead");
        }

        if (!rule.MayRedirectOntoAPerson)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                "p.79 no longer lets a lure be aimed at a person, and a piece of scenery is not "
                + "something this engine has");
        }

        if (string.Equals(onto, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} cannot lure an attack onto themselves — it is already there");
        }

        if (OutOfTheFight(state, onto, "new target", lines)) return state;

        if (!rule.RedirectingOntoAPersonCosts.Contains("turn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"luring's redirecting_onto_a_person_costs now reads "
                + $"'{rule.RedirectingOntoAPersonCosts}', which is not a turn this engine can take "
                + "away. See docs/guide/play-engine.md.");
        }

        if (!fromAdversity
            && CannotAfford(state, actor, rule.CostResolve, entry.Id, entry.SourceRef, lines))
        {
            return state;
        }

        var newTarget = state[onto];
        var attacker = state[last.Actor];

        var redirected = new Attack(
            last.Actor, newTarget.Id, last.TraitId, last.Damage, last.Type,
            Effect: last.Effect, Area: last.Area);

        var (trait, pool, active) = rule.TheNewTargetMakesTheirOwnDefenseRoll
            ? ChooseDefence(state, newTarget, redirected, last.AttackRank, lines)
            : ("no defence of their own", 0, false);

        var answered = rule.TheNewTargetMakesTheirOwnDefenseRoll
            ? _counter.Roll(pool, _dice).Successes
            : 0;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, rule.CostResolve, fromAdversity)
            + $" to lure {attacker.Name}: their {last.AttackSuccesses} was beaten by {margin}, so "
            + $"the attack strikes {newTarget.Name} instead, who answers with {trait} {pool}d for "
            + $"{answered}. It costs {actor.Name} {rule.RedirectingOntoAPersonCosts}"));

        var after = active ? CountActiveDefence(state, newTarget.Id) : state;

        var resolved = last with
        {
            Target = newTarget.Id,
            DefenceSuccesses = answered,
            TargetBefore = newTarget,
            EffectsBefore = after.Effects,
            DefenceWasActive = active
        };

        after = ApplyAttackOutcome(after, redirected, resolved, lines);
        after = ForfeitNextTurn(after, actor.Id);

        return Charge(after, actor, rule.CostResolve, fromAdversity) with { LastAttack = resolved };
    }

    /// <summary>
    /// Ch.4 p.79's <c>cost_resolve_to_make_sixes_explode</c>: the sixes on a team attack's roll are
    /// thrown again, and again for as long as they keep coming.
    ///
    /// <para><b>This is the purchase the dice contract exists for.</b> <see cref="IDiceSource"/>
    /// answers in faces rather than in successes precisely so that a rule which rerolls a
    /// <em>face</em> can be applied at all: an engine handed a count could not say which dice were
    /// sixes. The face is <see cref="SuccessCounter.HighestFace"/>, which is the map's own top key
    /// rather than a literal, and the recursion is the entry's
    /// <c>explosion_recurses_while_sixes_keep_coming</c> rather than an assumption.</para>
    ///
    /// <para><b>A six that has been rerolled is gone from the roll.</b> The faces are rewritten as
    /// the explosion goes, so a second point buys the sixes that came up in the reroll and never the
    /// ones that have already been spent — and a roll with none left is refused rather than charged
    /// for.</para>
    ///
    /// <para>The successes are added to the roll on the table and the outcome recomputed against the
    /// target as they were, which is the same path <see cref="BuyDice"/> takes and the reason
    /// <see cref="EncounterState.LastAttack"/> keeps that snapshot.</para>
    /// </summary>
    private EncounterState ExplodeTheSixes(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetCombat("team_attacks");
        var rule = entry.TeamAttack!;

        if (OutOfTheFight(state, actor.Id, "buyer", lines)) return state;

        if (state.LastAttack is not { } last
            || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has no roll of their own on the table to explode");
        }

        if (!last.Team)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name}'s last attack was not a team attack, and p.79 sells the exploding "
                + "sixes as part of one");
        }

        var face = _counter.HighestFace;

        if (!last.AttackFaces.Contains(face))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"there is no {face} left on that roll to explode");
        }

        if (!fromAdversity
            && CannotAfford(
                state, actor, rule.CostResolveToMakeSixesExplode, entry.Id, entry.SourceRef, lines))
        {
            return state;
        }

        var faces = last.AttackFaces.ToList();
        var gained = 0;
        var thrown = 0;
        var rounds = 0;

        while (true)
        {
            var exploding = faces.Count(f => f == face);
            if (exploding == 0) break;

            faces = [.. faces.Where(f => f != face)];

            var roll = _counter.Roll(exploding, _dice);

            gained += roll.Successes;
            thrown += exploding;
            rounds++;
            faces.AddRange(roll.Faces);

            if (!rule.ExplosionRecursesWhileSixesKeepComing) break;
        }

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, rule.CostResolveToMakeSixesExplode, fromAdversity)
            + $" to explode the team attack's {face}s: {thrown} thrown again over {rounds} "
            + $"round{(rounds == 1 ? "" : "s")} for {gained} more, so {last.AttackSuccesses} becomes "
            + $"{last.AttackSuccesses + gained}"));

        var improved = last with
        {
            AttackSuccesses = last.AttackSuccesses + gained,
            AttackFaces = faces
        };

        var after = Charge(state, actor, rule.CostResolveToMakeSixesExplode, fromAdversity);
        after = ReapplyLastAttack(after, improved, lines);

        return after with { LastAttack = improved };
    }

    /// <summary>
    /// Ch.4 p.73's <c>seizing_initiative</c>: a point buys a place at the front for the rest of the
    /// fight, or — where the GM has taken the alternative — doubles the buyer's effective Edge.
    ///
    /// <para><b>The GM's pool buys it for an NPC, and the purchase's own limits are unchanged by
    /// which pool pays.</b> p.85's first Adversity spend is "whatever a point of Resolve could have
    /// done, on behalf of any NPC", so the duration is still the whole fight and a second purchase
    /// by the same character is still refused; only <see cref="Charge"/> knows the difference.</para>
    ///
    /// <para><b>A group of Minions is refused, and p.73 is what refuses them.</b> The default effect
    /// is a place ahead of everyone and the alternative is a doubled Edge, and the same page denies
    /// a Minion group both: <c>minions_have_an_edge</c> is false, so there is nothing to double, and
    /// <c>minions_act</c> puts them after everyone else, which is the outermost key of this engine's
    /// own order. So the point would leave the pool and the order would not move — see the readings
    /// table in <c>docs/guide/play-engine.md</c>, where the two printed sentences are set against
    /// each other.</para>
    /// </summary>
    private EncounterState SeizeInitiative(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetCombat("seizing_initiative");
        var rule = entry.SeizeInitiative!;

        if (actor.Kind == CombatantKind.MinionGroup)
        {
            var ties = _play.GetCombat("edge_ties");
            var tie = ties.TieBreak!;

            TheEntryMustStillSay(
                !tie.MinionsHaveAnEdge, ties.Id, "minions_have_an_edge", tie.MinionsHaveAnEdge);

            TheEntryMustStillSay(
                tie.MinionsAct.Contains(MinionsActLast, StringComparison.Ordinal),
                ties.Id, "minions_act", tie.MinionsAct);

            return RefuseTheMinions(state, actor, ties.Id, ties.SourceRef, lines,
                $"p.73 gives them no Edge at all (minions_have_an_edge is {tie.MinionsHaveAnEdge}) "
                + $"and has them act {tie.MinionsAct}: there is nothing for the GM's alternative to "
                + "double, and nothing this order would move");
        }

        if (state.Seized.Contains(actor.Id, StringComparer.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has already seized the initiative, and it lasts {rule.Duration}");
        }

        if (!fromAdversity
            && CannotAfford(state, actor, rule.CostResolve, entry.Id, entry.SourceRef, lines))
        {
            return state;
        }

        var alternative = state.Table.GmAlternativeToSeizingInitiative
            ? _play.GetCombat("seize_initiative_gm_alternative")
            : null;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, alternative?.Id ?? entry.Id, alternative?.SourceRef ?? entry.SourceRef,
            Paid(actor, rule.CostResolve, fromAdversity) + " to seize the initiative — "
            + (alternative is null
                ? $"{rule.Effect}, for {rule.Duration}"
                : $"the GM has taken the alternative, so it {alternative.GmAlternative!.Effect} instead, "
                  + $"for the duration it inherits from {alternative.Interpretation!.DurationIsInheritedFrom}")
            + "; it takes effect when the page turns"));

        return Charge(state, actor, rule.CostResolve, fromAdversity)
            with { Seized = [.. state.Seized, actor.Id] };
    }

    /// <summary>
    /// Ch.4 p.79's Fatal Damage rescue, and the one place this engine consumes an
    /// <c>interpretation</c> in anger.
    ///
    /// <para><b>The page contradicts itself and the entry records both halves.</b>
    /// <c>gritty_fatal_damage.resolve_reduces_damage_to</c> carries the printed word — "1 point
    /// below this fatal threshold", which at Clint Castle's −5 is −6 — and the worked example two
    /// sentences later leaves him at −4, one point <em>above</em> it, alive. This follows the
    /// interpretation, because the authors' own arithmetic is the better witness to what they meant,
    /// and the ledger line cites the entry's <c>ambiguity</c> so a reader of the run can see the
    /// choice was made rather than assumed.</para>
    ///
    /// <para><b>The GM's pool buys it for an NPC, and every one of the page's own conditions
    /// stays.</b> Fatal Damage still has to be one of the table's settings, the blow still has to
    /// have reached the threshold, and the same point still stabilises where
    /// <c>resolve_also_stabilises_if_necessary</c> asks it to — p.85 changes which pool pays and
    /// nothing else.</para>
    ///
    /// <para><b>A group of Minions is refused, and p.77 is what refuses them.</b> The threshold this
    /// buys back from is the negative of a full Health, and <c>attacking_minions</c>'
    /// <c>minions_have_health</c> is false: a Minion group is defeated by bodies rather than by a
    /// pool, so there is no threshold to be past and no blow to buy back.</para>
    /// </summary>
    private EncounterState AvoidFatalDamage(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetGritty("gritty_fatal_damage");
        var fatal = entry.FatalDamage!;

        if (!state.Table.FatalDamage)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                "Fatal Damage is not one of this table's settings, so there is no threshold to buy back from");
        }

        if (actor.Kind == CombatantKind.MinionGroup) return TheMinionsHaveNoClock(state, actor, lines);

        var threshold = -actor.FullHealth;

        // <b>Two refusals that used to be a throw and a rescue of somebody who did not need one.</b>
        // A spend with no points behind it reached Combatant.Spending and threw an exception out of
        // Step, which is not a refusal — an intent the rules do not allow belongs on the ledger, not
        // in a stack trace. And the arithmetic below sets a Health rather than reducing one, so
        // against a character nowhere near the line it did not rescue them, it dropped them to one
        // point above a threshold they were far above already.
        if (!fromAdversity && actor.Resolve < fatal.CostResolveToAvoid)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has {actor.Resolve} Resolve and buying back a fatal blow costs "
                + $"{fatal.CostResolveToAvoid}");
        }

        if (actor.CurrentHealth > threshold)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} is on {actor.CurrentHealth} Health and the fatal threshold is "
                + $"{threshold}, so there is no blow to buy back");
        }
        var reading = entry.Interpretation!.ResolveReducesDamageTo!;

        // <b>Both halves of the reading come out of the sentence, and neither is typed here.</b>
        // The direction is the word the interpretation ends on and the distance is the number it
        // opens with — "1 point above the fatal threshold". Taking the distance from
        // cost_resolve_to_avoid instead would be an arithmetic coincidence: that field is a price in
        // Resolve and this is a distance in Health, and they are both 1 for no connected reason.
        var distance = int.Parse(
            System.Text.RegularExpressions.Regex.Match(
                reading, @"^(\d+)\s+point", System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(5)).Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        var direction = reading.Contains("above", StringComparison.Ordinal) ? 1
            : reading.Contains("below", StringComparison.Ordinal) ? -1
            : throw new InvalidOperationException(
                $"gritty_fatal_damage's interpretation reads '{reading}', which names neither direction.");

        var rescued = threshold + direction * distance;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, fatal.CostResolveToAvoid, fromAdversity) + " against a fatal blow: "
            + $"{actor.CurrentHealth} becomes {rescued}, one point above the threshold of {threshold}. "
            + "The printed word says one point below it and the printed example says above; the "
            + $"entry's ambiguity records the contradiction: {fatal.ResolveReducesDamageTo} is what "
            + "p.79 says, and the example is what this follows"));

        var rescuedActor = actor.WithHealth(rescued);

        if (fatal.ResolveAlsoStabilisesIfNecessary && actor.Dying)
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"the same point steadies {actor.Name}: the entry says a spent Resolve also "
                + "stabilises if necessary, so the dying clock stops"));

            rescuedActor = rescuedActor.Bleeding(dying: false);
        }

        // <b>The rescued combatant is written in before the charge and the charge is handed that
        // one</b>, because a Hero's half of <see cref="Charge"/> writes the actor it is given: given
        // the combatant this purchase started from, it would put the unrescued Health back.
        return Charge(state.With(rescuedActor), rescuedActor, fatal.CostResolveToAvoid, fromAdversity);
    }

    /// <summary>
    /// p.79's <c>cost_resolve_to_stabilise_immediately</c>: a point stops the clock with no roll.
    ///
    /// <para><b>The GM's pool buys it for an NPC</b>, and the one condition the page puts on it is
    /// unchanged: somebody has to be bleeding out. p.79's roll is available as often as necessary
    /// and so is this, which is why there is no per-scene limit to carry across.</para>
    ///
    /// <para><b>A group of Minions is refused for the reason the rescue above refuses one</b>: the
    /// clock is started by lethal damage taking a Health past a threshold, and
    /// <c>attacking_minions.minions_have_health</c> is false, so a Minion group never has one to
    /// stop. Refusing by name rather than falling into "is not dying" is the difference between the
    /// page having nothing for them and the fight not being in a state for it.</para>
    /// </summary>
    private EncounterState StabiliseWithResolve(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetGritty("gritty_fatal_damage");
        var fatal = entry.FatalDamage!;

        if (!state.Table.FatalDamage)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                "Fatal Damage is not one of this table's settings, so nobody is dying to be steadied");
        }

        if (actor.Kind == CombatantKind.MinionGroup) return TheMinionsHaveNoClock(state, actor, lines);

        if (!actor.Dying)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} is not dying, so there is no clock to stop");
        }

        if (!fromAdversity && CannotAfford(
                state, actor, fatal.CostResolveToStabiliseImmediately, entry.Id, entry.SourceRef, lines))
        {
            return state;
        }

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, fatal.CostResolveToStabiliseImmediately, fromAdversity)
            + $" to stabilise at once, on {actor.CurrentHealth} Health"));

        var steadied = actor.Bleeding(dying: false);

        return Charge(
            state.With(steadied), steadied, fatal.CostResolveToStabiliseImmediately, fromAdversity);
    }

    /// <summary>
    /// The refusal p.79's two Fatal Damage purchases both make of a group of Minions, cited to the
    /// entry that decides it rather than to the entry selling the purchase.
    ///
    /// <para>Both turn on a Health total — the threshold is the negative of a full one and the clock
    /// starts when lethal damage takes a Health past it — and p.77 says a Minion group has none.
    /// A group is defeated by bodies, so it is never past a threshold and never on a clock.</para>
    /// </summary>
    private EncounterState TheMinionsHaveNoClock(
        EncounterState state, Combatant actor, List<LedgerLine> lines)
    {
        var minions = _play.GetCombat("attacking_minions");
        var mob = minions.AttackingMinions!;

        TheEntryMustStillSay(
            !mob.MinionsHaveHealth, minions.Id, "minions_have_health", mob.MinionsHaveHealth);

        return RefuseTheMinions(state, actor, minions.Id, minions.SourceRef, lines,
            "p.77 gives them no Health at all (minions_have_health is "
            + $"{mob.MinionsHaveHealth}): a group is defeated by the bodies "
            + "taken out of it, so there is no threshold to be past and no dying clock to stop");
    }

    /// <summary>
    /// p.76's instant recovery, and the one place p.79 reaches into it:
    /// <c>instant_recovery_requires_being_stable</c>.
    ///
    /// <para><b>It was on the unimplemented list, and that made the Fatal Damage gate unreadable.</b>
    /// The field is a rule about this purchase, so leaving the purchase unresolved left the field
    /// decorative — which is the shape the whole store exists to prevent. The purchase is small and
    /// entirely stated: a point brings a character round with <c>after_a_damaging_defeat_restores_health</c>
    /// back, or shakes off a special effect whether or not the effect had beaten them, once a
    /// scene.</para>
    ///
    /// <para>What is <em>not</em> applied is <c>taken_on: "your next turn to act"</c>. This engine
    /// does not turn-gate a Resolve purchase — a defeated character has no turn to be theirs — and
    /// the guide records it as a reading rather than leaving it silent.</para>
    ///
    /// <para><b>The GM's pool buys it for an NPC, and the two limits the page prints stay.</b>
    /// <c>limit_per_scene</c> is counted on the character rather than on the pool, so a Villain
    /// bought back onto their feet is not bought back a second time out of the GM's money; and
    /// p.79's <c>instant_recovery_requires_being_stable</c> still refuses one who is bleeding out.
    /// </para>
    ///
    /// <para><b>A group of Minions is refused, and p.77 is what refuses them.</b> Both halves of
    /// this purchase need something a Minion group has not got: the Health half is
    /// <c>minions_have_health</c>, which is false, and the effect half cannot arise, because p.77
    /// resolves a special effect against a group by taking bodies out of it — the defeated Minions
    /// "are subject to it for the rest of the scene" — so a group never carries an effect there is
    /// anything to shake off.</para>
    /// </summary>
    private EncounterState InstantRecovery(
        EncounterState state, Combatant actor, List<LedgerLine> lines, bool fromAdversity = false)
    {
        var entry = _play.GetCombat("instant_recovery");
        var rule = entry.InstantRecovery!;
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        if (actor.Kind == CombatantKind.MinionGroup)
        {
            var minions = _play.GetCombat("attacking_minions");
            var mob = minions.AttackingMinions!;

            TheEntryMustStillSay(
                !mob.MinionsHaveHealth, minions.Id, "minions_have_health", mob.MinionsHaveHealth);

            return RefuseTheMinions(state, actor, minions.Id, minions.SourceRef, lines,
                $"p.77 gives them no Health to bring back (minions_have_health is "
                + $"{mob.MinionsHaveHealth}) and resolves an effect against a group by taking bodies "
                + $"out of it instead — {mob.OnASpecialEffect} — so there is neither a defeat to "
                + "recover from nor an effect to shake off");
        }

        if (actor.InstantRecoveriesUsed >= rule.LimitPerScene)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} has already taken {rule.LimitPerScene} instant recovery this scene");
        }

        if (state.Table.FatalDamage && actor.Dying)
        {
            var gritty = _play.GetGritty("gritty_fatal_damage");

            if (gritty.FatalDamage!.InstantRecoveryRequiresBeingStable)
            {
                return Refuse(state, actor.Id, gritty.Id, gritty.SourceRef, lines,
                    $"{actor.Name} is still bleeding out, and under Fatal Damage nothing brings a "
                    + "character back to their feet until the clock has been stopped");
            }
        }

        var effects = state.Effects
            .Where(e => !string.Equals(e.Target, actor.Id, StringComparison.Ordinal))
            .ToList();

        var freed = effects.Count != state.Effects.Count;

        if (!freed && actor.CurrentHealth > floor && actor.DefeatedByEffect is null)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{actor.Name} is on their feet and free of any effect, so there is nothing to recover from");
        }

        if (!fromAdversity
            && CannotAfford(state, actor, rule.CostResolve, entry.Id, entry.SourceRef, lines))
        {
            return state;
        }

        // p.80's Slow Healing: "you do not heal ... when you regain consciousness after a defeat".
        // So the point still brings them round and it brings back nothing else.
        var slowly = state.Table.SlowHealing;

        var health = actor.CurrentHealth <= floor && !slowly
            ? rule.AfterADamagingDefeatRestoresHealth
            : actor.CurrentHealth;

        var standing = slowly && health <= floor;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            Paid(actor, rule.CostResolve, fromAdversity) + " on an instant recovery: "
            + (actor.CurrentHealth <= floor
                ? $"back on their feet with {health} Health"
                : $"still on {health} Health")
            + (freed ? ", and free of the effect that had them" : "")
            + $" — {rule.LimitPerScene} a scene"));

        if (standing)
        {
            var gritty = _play.GetGritty("gritty_slow_healing");
            var slow = gritty.SlowHealing!;

            lines.Add(new LedgerLine(
                state.Page, actor.Id, gritty.Id, gritty.SourceRef,
                $"under Slow Healing nobody heals on regaining consciousness after a defeat "
                + $"(healing_on_regaining_consciousness_after_a_defeat is {slow.HealingOnRegainingConsciousnessAfterADefeat}), "
                + $"so {actor.Name} is up on {health} Health, which p.80 allows — and in that "
                + "condition a single point of damage puts them straight back down"));
        }

        // The recovered combatant goes in before the charge and the charge is handed that one, for
        // the reason the Fatal Damage rescue above says: a Hero's half of Charge writes the actor it
        // is given, and the one this started from is still on the floor.
        var recovered = actor.Recovered(health, standing);

        return Charge(state.With(recovered), recovered, rule.CostResolve, fromAdversity)
            with { Effects = effects };
    }

    /// <summary>
    /// p.79's stabilisation roll: the Trait <c>stabilise_roll</c> names, at the difficulty and
    /// threshold beside it.
    ///
    /// <para><c>stabilise_also_by</c> — "a Power like Healing" — is prose and a GM's call, so it is
    /// named on the line rather than applied.</para>
    /// </summary>
    private EncounterState ResolveStabilise(EncounterState state, Stabilise steady, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, steady.Actor, lines)) return state;

        var entry = _play.GetGritty("gritty_fatal_damage");
        var fatal = entry.FatalDamage!;

        var actor = state[steady.Actor];
        var patient = state[steady.Target];

        if (!state.Table.FatalDamage)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                "Fatal Damage is not one of this table's settings, so nobody is dying to be steadied");
        }

        if (!patient.Dying)
        {
            return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
                $"{patient.Name} is not dying, so there is no clock to stop");
        }

        var trait = Normalise(fatal.StabiliseRoll);
        var roll = _counter.Roll(actor.Rank(trait) + WoundPenalty(state, actor, lines), _dice);
        var net = roll.Successes - fatal.StabiliseThreshold;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} rolls {fatal.StabiliseRoll} {actor.Rank(trait)}d for {roll.Successes} "
            + $"against a {fatal.StabiliseDifficulty} threshold of {fatal.StabiliseThreshold}: "
            + (net >= 0
                ? $"{patient.Name} is stable"
                : $"{patient.Name} is still bleeding out")
            + $". {fatal.StabiliseAlsoBy} would do it too, which is the GM's call and not this "
            + "engine's"));

        return net >= 0 ? state.With(patient.Bleeding(dying: false)) : state;
    }

    private EncounterState ResolveAdversitySpend(
        EncounterState state, SpendAdversity spend, List<LedgerLine> lines)
    {
        var entry = _play.GetResolve(spend.Kind switch
        {
            AdversitySpend.AnythingResolveCan => "adversity_spend_anything_resolve_can",
            AdversitySpend.SuppressFlaw => "adversity_spend_suppress_flaw",
            AdversitySpend.Misfortune => "adversity_spend_misfortune",
            AdversitySpend.Villainy => "adversity_spend_villainy",
            _ => throw new ArgumentOutOfRangeException(nameof(spend))
        });

        // <b>A spend of fewer than one point is refused before the pool is looked at, and the gate
        // below is why it has to be.</b> That gate compares the pool with what the <em>intent</em>
        // asked for, and nine of the ten purchases charge their own printed price and ignore the
        // figure entirely — so a spend of 0 points walked through it on an empty pool and then
        // charged 1, leaving the GM on −1 Adversity with a purchase they had not paid for. A
        // negative one is worse: <see cref="BuyDice"/> multiplies the rate by the figure, so
        // −5 points of extra dice <em>added</em> five to the pool. Neither is a purchase Chapters 4
        // or 5 price: every <c>cost_resolve</c> and <c>cost_adversity</c> in the store is a whole
        // number of points and none of them is below one.
        if (spend.Points < 1)
        {
            return Refuse(state, spend.Actor, entry.Id, entry.SourceRef, lines,
                $"this spend asks for {spend.Points} points, and Chapters 4 and 5 price every "
                + "purchase at a whole point or more — nothing is spent and nothing is bought");
        }

        if (state.Adversity < spend.Points)
        {
            return Refuse(state, spend.Actor, entry.Id, entry.SourceRef, lines,
                $"the GM has {state.Adversity} Adversity and the spend costs {spend.Points}");
        }

        // <b>p.85's first purchase is the one that is not a rule of its own.</b> "Whatever a point of
        // Resolve could have done, on behalf of any NPC" is the Resolve purchases with different
        // money behind them, so the intent names which one and the engine runs it against the GM's
        // pool. The other three are rules of their own and each is applied below.
        switch (spend.Kind)
        {
            case AdversitySpend.SuppressFlaw:
                return SuppressFlaw(state, entry, spend, lines);

            case AdversitySpend.Misfortune:
                return Misfortune(state, entry, spend, lines);

            case AdversitySpend.Villainy:
                return ActOfVillainy(state, entry, spend, lines);

            case AdversitySpend.AnythingResolveCan:
                break;

            default:
                return NotYetImplementedSpend(
                    state, spend.Actor, spend.Kind.ToString(), entry.Id, entry.SourceRef, lines);
        }

        var npc = state[spend.Actor];

        // <b>p.85 says who the point may be spent for, and a Hero is not on the list.</b> The
        // sentence that makes this a purchase at all is "you can spend Adversity on behalf of any
        // NPC whether they're Villains, Foes, Minions, or Extras", transcribed as `npc_kinds` — and
        // the pool is the GM's precisely because the players have one of their own. Without this
        // the GM's pool bought a Hero the die their own Resolve would have bought: `BuyDice` and its
        // five neighbours are handed a combatant and charge whichever pool the flag names, so every
        // one of the six was reachable for a character on the other side of the screen. That is the
        // one thing a two-pool economy exists to make impossible, and nothing anywhere refused it.
        //
        // The list is the entry's; the singular is `PrintedKind`'s, as p.85's other two eligibility
        // refusals read theirs. A fixture requires the four the page names to be the four kinds this
        // engine has besides a Hero, so a pluralisation that stopped matching would refuse everybody
        // rather than pass quietly.
        var buyableFor = entry.Spend!.NpcKinds!;
        var kind = PrintedKind(npc.Kind);

        if (!buyableFor.Contains(kind + "s", StringComparer.Ordinal))
        {
            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"p.85 spends a point of Adversity on behalf of any NPC — {string.Join(", ", buyableFor)} "
                + $"— and {npc.Name} is a {kind}. Only Heroes hold Resolve and the GM's pool is not "
                + "theirs to spend");
        }

        if (spend.AsResolve is not { } as_)
        {
            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"a point of Adversity does what a point of Resolve would have done, and this spend "
                + $"on {npc.Name} does not say which purchase that is");
        }

        // <b>It says "not yet implemented" because that is what it is, and because the words are
        // load-bearing.</b> This branch is reached for a purchase the book allows the GM to buy and
        // this slice does not run, which is the same thing every other unimplemented spend is — and
        // the guard that holds `mcp-play/PLAY-POLICY.md` to the engine sorts a spend by whether its
        // line carries that phrase. Refusing in different words put this case in neither pile, so a
        // document could claim the GM's pool bought all six and nothing disagreed.
        if (!AdversityBuys.Contains(as_))
        {
            return NotYetImplementedSpend(
                state, npc.Id, $"{AdversitySpend.AnythingResolveCan} naming {as_}",
                entry.Id, entry.SourceRef, lines);
        }

        // <b>The announcement is written after the purchase and only if the pool actually paid.</b>
        // It used to be written before the dispatch, and every one of these can refuse — no roll on
        // the table, nobody down under an effect, the wrong kind of blow — so a refusal left "the GM
        // spends Adversity on X, which buys Y" standing above a line saying nothing happened, with
        // the pool untouched. A reader counting spends off the ledger and a reader reading the pool
        // would have given two different accounts of the same fight, which is the one thing a ledger
        // exists to make impossible. It is *inserted* at the position it would have occupied, so the
        // reading order is still the announcement and then what it bought.
        var at = lines.Count;

        var after = as_ switch
        {
            ResolveSpend.ExtraDice => BuyDice(state, npc, spend.Points, lines, fromAdversity: true),
            ResolveSpend.Reroll => BuyReroll(state, npc, lines, fromAdversity: true),
            ResolveSpend.SeizeInitiative => SeizeInitiative(state, npc, lines, fromAdversity: true),
            ResolveSpend.InstantRecovery => InstantRecovery(state, npc, lines, fromAdversity: true),
            ResolveSpend.AvoidFatalDamage => AvoidFatalDamage(state, npc, lines, fromAdversity: true),
            ResolveSpend.Stabilise => StabiliseWithResolve(state, npc, lines, fromAdversity: true),
            ResolveSpend.KeepingHold => KeepHold(state, npc, lines, fromAdversity: true),
            ResolveSpend.Knockback => Knockback(state, npc, lines, fromAdversity: true),
            ResolveSpend.Luring => Lure(state, npc, spend.Target, lines, fromAdversity: true),
            ResolveSpend.TeamAttack => ExplodeTheSixes(state, npc, lines, fromAdversity: true),
            var other => throw new ArgumentOutOfRangeException(
                nameof(spend), other,
                "AdversityBuys names a purchase the GM's pool has no branch for.")
        };

        // The pool is what says a point was spent: `Charge` is the only thing that moves it, and it
        // is reached only past every refusal each purchase makes.
        if (after.Adversity != state.Adversity)
        {
            lines.Insert(at, new LedgerLine(
                state.Page, npc.Id, entry.Id, entry.SourceRef,
                $"the GM spends {state.Adversity - after.Adversity} Adversity on {npc.Name}, which "
                + $"buys {as_}: p.85 says one point does whatever a point of Resolve could have "
                + "done, on behalf of any NPC"));
        }

        return after;
    }

    /// <summary>
    /// The Resolve purchases p.85's first Adversity spend runs for an NPC.
    ///
    /// <para><b>It is one list rather than a condition beside a switch</b>, because the two would
    /// drift and the drift is invisible: a purchase admitted by the gate and missing from the
    /// dispatch throws in the middle of a fight, and one implemented in the dispatch and missing
    /// from the gate refuses a purchase that works. <c>mcp-play/PLAY-POLICY.md</c>'s table of what
    /// <c>anything_resolve_can</c> may name is held to this by driving every member of the enum
    /// through <see cref="Step"/>.</para>
    ///
    /// <para><b>It is every purchase now, and the gate is kept rather than deleted.</b> p.85 says a
    /// point of Adversity does whatever a point of Resolve could have done, and the four that used
    /// to be missing — <c>SeizeInitiative</c>, <c>InstantRecovery</c>, <c>AvoidFatalDamage</c> and
    /// <c>Stabilise</c> — charged the buyer's own pool, which an NPC has none of. They go through
    /// <see cref="Charge"/> now. What the gate is worth with nothing left out is the <em>next</em>
    /// purchase: a member added to <see cref="ResolveSpend"/> and not added here is answered with a
    /// <c>not yet implemented</c> line, where the dispatch below would throw in the middle of a
    /// fight. A list that has to be edited twice is the point, not an oversight.</para>
    /// </summary>
    private static readonly HashSet<ResolveSpend> AdversityBuys =
    [
        ResolveSpend.ExtraDice, ResolveSpend.Reroll, ResolveSpend.SeizeInitiative,
        ResolveSpend.InstantRecovery, ResolveSpend.AvoidFatalDamage, ResolveSpend.Stabilise,
        ResolveSpend.KeepingHold, ResolveSpend.Knockback, ResolveSpend.Luring,
        ResolveSpend.TeamAttack
    ];

    /// <summary>
    /// Ch.5 p.85's <c>adversity_spend_suppress_flaw</c>: a point buys a Villain, a Foe or an Extra
    /// out of one of their Flaws for the rest of the scene.
    ///
    /// <para><b>Half of this rule is state and half of it is narration, and the ledger line says
    /// which is which.</b> Nothing in <c>play/</c> makes a Flaw bite — the page says an NPC's Flaws
    /// come into play "whenever the opportunity presents itself", which is the GM's judgement and
    /// not a roll this engine could win or lose — so what the suppression saves the character from
    /// never reaches the state. What does reach it is everything the page states in figures: the
    /// pool pays <c>cost_adversity</c> exactly once, only the three kinds on
    /// <c>eligible_characters</c> may be bought out, and the character carries the suppression for
    /// the rest of the scene, which is what refuses the second purchase against them.</para>
    ///
    /// <para><b>The limit is read as the sentence prints it: per character.</b> "No character can
    /// benefit from this more than once per issue" names a character and not a Flaw, and the
    /// entry's own <c>ambiguity</c> records that the two differ for anybody carrying more than one.
    /// So a second purchase is refused whichever Flaw it names, and the refusal quotes the
    /// ambiguity rather than settling it.</para>
    ///
    /// <para><b>A purchase that does not say which Flaw is refused with nothing spent</b>, the same
    /// shape p.79's luring refuses a lure that names nobody: this engine holds no Flaws, so an
    /// unnamed one would put a suppression of "some weakness or other" on the ledger and on the
    /// public state, which is a record nobody can narrate from and nobody can audit.</para>
    /// </summary>
    private EncounterState SuppressFlaw(
        EncounterState state, ResolveEntry entry, SpendAdversity spend, List<LedgerLine> lines)
    {
        var rule = entry.Spend!;
        var cost = rule.CostAdversity!.Value;

        if (WrongPrice(state, entry, spend, cost, lines) is { } priced) return priced;

        var npc = state[spend.Actor];
        var printed = PrintedKind(npc.Kind);
        var eligible = rule.EligibleCharacters!;

        if (!eligible.Contains(printed, StringComparer.Ordinal))
        {
            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"p.85 buys a Flaw off a {string.Join(", a ", eligible)}, and {npc.Name} is a "
                + printed);
        }

        if (npc.SuppressedFlaw is { } already)
        {
            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"{npc.Name} has already been bought out of {already} in this fight, and p.85 "
                + $"allows {rule.LimitPerCharacterPerIssue} per character per issue. {CountedHere} "
                + "The limit is printed per character rather than per Flaw, which the entry's own "
                + "ambiguity says is unclear for anybody carrying two — this engine refuses on the "
                + "character, which is the reading that never allows more than the page does");
        }

        if (Said(spend) is not { } flaw)
        {
            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"a point prevents {rule.Prevents}, and this spend does not say which Flaw — name "
                + "it, because this engine holds none of them and a suppression of nothing in "
                + "particular is a suppression of nothing");
        }

        var bite = rule.NpcFlawsBiteWhenTheOpportunityArises == true
                   && rule.NpcsCannotChooseWhenTheirFlawsBite == true
            ? "An NPC's Flaws bite when the opportunity arises and the NPC cannot choose when, so "
            : "";

        lines.Add(new LedgerLine(
            state.Page, npc.Id, entry.Id, entry.SourceRef,
            $"the GM spends {cost} Adversity on {npc.Name}: {flaw} is prevented from "
            + $"{rule.Prevents} for {rule.Duration}, which is the whole of this encounter. "
            + $"{bite}what that saves {npc.Name} from is the GM's to narrate — this engine has "
            + "recorded the point and the suppression and nothing else. p.85 allows "
            + $"{rule.LimitPerCharacterPerIssue} per character per issue. {CountedHere}"));

        return ChargeAdversity(state, cost).With(npc.Suppressing(flaw));
    }

    /// <summary>
    /// Ch.5 p.85's <c>adversity_spend_misfortune</c>: a point throws a piece of bad luck at the
    /// Heroes.
    ///
    /// <para><b>Nothing about this rule is mechanical, and the entry's own <c>ambiguity</c> says so
    /// in as many words</b>: a misfortune is defined by three examples and two prohibitions, and no
    /// roll, threshold, duration or way of resisting one is printed anywhere. So the whole of what
    /// this engine can honestly do is take the point out of the pool and write down what the GM
    /// said the point bought — and the ledger line says that is what it did, rather than announcing
    /// an effect the state never received.</para>
    ///
    /// <para><b>It is refused unless it says what the misfortune is.</b> A purchase recorded with no
    /// words behind it is a pool that has moved for nothing: nobody reading the run could narrate
    /// from it and nobody could audit it. That is the same refusal p.79's luring makes of a lure
    /// that names nobody, and for the same reason — the engine has nothing of its own to put
    /// there.</para>
    ///
    /// <para><b>It is aimed at a side and not at a character</b>, which is why it is the one spend
    /// whose <see cref="Intent.Actor"/> is not read and whose ledger line names no actor: p.85
    /// throws it at "the Heroes", and the line names the side they are on.</para>
    /// </summary>
    private EncounterState Misfortune(
        EncounterState state, ResolveEntry entry, SpendAdversity spend, List<LedgerLine> lines)
    {
        var rule = entry.Spend!;
        var cost = rule.CostAdversity!.Value;

        if (WrongPrice(state, entry, spend, cost, lines) is { } priced) return priced;

        if (Said(spend) is not { } what)
        {
            return Refuse(state, "", entry.Id, entry.SourceRef, lines,
                $"a point buys {rule.WhatItIs}, and this spend does not say what it is. p.85 gives "
                + $"a misfortune no roll, no threshold and no duration — {string.Join("; ", rule.ExamplesGiven!)} "
                + "are the whole of what it prints — so the words are the GM's and this engine has "
                + "none of its own");
        }

        var asked = new List<string>();
        if (rule.MustBeAChallengeNotAPunishment == true) asked.Add("a challenge rather than a punishment");
        if (rule.MustNotBeAPlotDevice == true) asked.Add("never a heavy-handed plot device");

        // <b>The side, because p.85 aims a misfortune at "the Heroes" and this engine has no such
        // category — only combatants who are on a side.</b> There is no refusal for a fight with no
        // Heroes in it and there cannot usefully be one: the opening pool is a point per Hero plus
        // the Challenge Level times the same number, so a fight without one opens on nothing at all
        // and the spend is refused for want of a point long before it could be refused for want of
        // a target. A guard there would be a branch no encounter can reach.
        var sides = state.Combatants.Values
            .Where(c => c.Kind == CombatantKind.Hero)
            .Select(c => c.Side)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var aimedAt = sides.Count == 0 ? "the Heroes" : "the Heroes on " + string.Join(" and ", sides);

        lines.Add(new LedgerLine(
            state.Page, "", entry.Id, entry.SourceRef,
            $"the GM spends {cost} Adversity on a misfortune aimed at {aimedAt}: "
            + $"\"{what}\". p.85 asks that it be {string.Join(", and ", asked)}. Nothing about a "
            + "misfortune is mechanical — the page gives it no roll, no threshold and no duration — "
            + "so this engine has recorded the point and the GM's words, and the misfortune itself "
            + "is the GM's to narrate"));

        return ChargeAdversity(state, cost);
    }

    /// <summary>
    /// Ch.5 p.85's <c>adversity_spend_villainy</c>: once in a story, a point has a Villain
    /// automatically do whatever the story needs — throw the switch, take the hostage, get away.
    ///
    /// <para><b>The act is the GM's and the limits are the engine's, and the ledger line says
    /// which is which.</b> "Anything necessary to advance the story" is not a mechanic and this
    /// engine has no plot to advance, so what the Villain does never reaches the state and the line
    /// hands it back. What does reach the state is everything the page states: the pool pays
    /// <c>cost_adversity</c> once, only a <c>Villain</c> may be handed it — "Foes and Minions lack
    /// what it takes", read off <c>excluded_characters</c> rather than restated here — and
    /// <see cref="EncounterState.Villainy"/> records that the story's one act is spent, which is
    /// what refuses the second.</para>
    ///
    /// <para><b>What "once per story" means here is a reading and is recorded as one.</b> Chapter 5
    /// defines no story — its own <c>ambiguity</c> says the pools it governs are counted per issue
    /// and that the two are the same thing only in a one-session game — and the largest unit this
    /// engine can see is an encounter, which is a scene. So the limit holds across this fight and
    /// no further, the line says so, and a GM running a second scene of the same story knows the
    /// count did not travel with them. Enforcing it over a unit the engine cannot see would be a
    /// limit it could not honestly claim; enforcing nothing would drop a printed one.</para>
    ///
    /// <para><b>A purchase that does not say what the act is is refused with nothing spent</b>, for
    /// the reason the other two are: the act <em>is</em> the purchase, and a point recorded against
    /// "a Villain does something" is a line nobody can narrate from.</para>
    /// </summary>
    private EncounterState ActOfVillainy(
        EncounterState state, ResolveEntry entry, SpendAdversity spend, List<LedgerLine> lines)
    {
        var rule = entry.Spend!;
        var cost = rule.CostAdversity!.Value;

        if (WrongPrice(state, entry, spend, cost, lines) is { } priced) return priced;

        var npc = state[spend.Actor];
        var printed = PrintedKind(npc.Kind);
        var eligible = rule.EligibleCharacters!;

        if (!eligible.Contains(printed, StringComparer.Ordinal))
        {
            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"villainy applies only to {Plural(eligible)} — {Plural(rule.ExcludedCharacters!)} "
                + $"lack what it takes — and {npc.Name} is a {printed}");
        }

        if (state.Villainy.Count >= rule.LimitPerStory!.Value)
        {
            var already = state.Villainy.Select(id => state[id].Name);

            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"p.85 allows {rule.LimitPerStory} act of villainy per story, and "
                + $"{string.Join(", ", already)} has had it. A story is not a unit the chapter "
                + "defines, and the largest one this engine can see is this encounter — so the "
                + "count is per fight, and it does not follow the GM into the next scene");
        }

        if (Said(spend) is not { } act)
        {
            return Refuse(state, npc.Id, entry.Id, entry.SourceRef, lines,
                $"a point buys what p.85 calls it — {rule.Effect} — and this spend does not say "
                + $"what the act is. The page's examples are {string.Join("; ", rule.ExamplesGiven!)}, "
                + "and this engine has no story of its own to read one off");
        }

        var automatically = rule.Automatic == true ? ", automatically and with no roll" : "";
        var sparingly = rule.UseSparingly == true
            ? " The page says to use it sparingly: done often, it tells the players their choices "
              + "did not matter."
            : "";

        lines.Add(new LedgerLine(
            state.Page, npc.Id, entry.Id, entry.SourceRef,
            $"the GM spends {cost} Adversity on {npc.Name}: {rule.Effect}{automatically} — "
            + $"\"{act}\". "
            + $"That is the {rule.LimitPerStory} this story allows, counted over this encounter "
            + $"because a story is a unit the chapter does not define.{sparingly} The act itself is "
            + "the GM's to narrate; this engine has recorded the point and that the story's one act "
            + "is spent"));

        return ChargeAdversity(state, cost) with { Villainy = [.. state.Villainy, npc.Id] };
    }

    /// <summary>
    /// The refusal every one of p.85's three own purchases makes of a spend that asks for a number
    /// of points the page does not price, or a throwaway null where it asks for the printed one.
    ///
    /// <para><b>Silently charging the printed price for a spend of three would be worse than
    /// refusing.</b> Each of the three is priced at one point and buys one thing; a caller who asks
    /// for three has either misread the page or meant three purchases, and an engine that took one
    /// and said nothing would leave a pool and a ledger that disagree about what was bought.</para>
    /// </summary>
    private static EncounterState? WrongPrice(
        EncounterState state, ResolveEntry entry, SpendAdversity spend, int cost,
        List<LedgerLine> lines) =>
        spend.Points == cost
            ? null
            : Refuse(state, spend.Actor, entry.Id, entry.SourceRef, lines,
                $"p.85 prices {entry.Name} at {cost} Adversity and this spend asks for "
                + $"{spend.Points}. Nothing was spent");

    /// <summary>
    /// Which unit p.85's suppress-a-Flaw limit was actually counted in, on both of the lines that
    /// mention it.
    ///
    /// <para><b>The page counts it per issue and this engine counts it per encounter, and a line
    /// that did not say so was a line that lied.</b> The refusal used to read "has already been
    /// bought out of X <em>this issue</em>", which is a claim about a unit an <c>Encounter</c>
    /// cannot see: <see cref="Step"/> turns pages, nothing in it ends a scene, and an issue is
    /// several scenes. A GM reading that would take the count to have travelled, and it does not —
    /// so a second scene of the same issue would silently allow a second suppression the page does
    /// not. That is the permissive direction, which is the one worth saying out loud; the same
    /// reading and the same sentence are on p.85's act of villainy, whose unit is a story.</para>
    /// </summary>
    private const string CountedHere =
        "An issue is larger than anything this engine can see, so the count is per fight: a second "
        + "scene of the same issue starts it again, and keeping track across scenes is the GM's.";

    /// <summary>
    /// What the GM said the point bought, trimmed — or null, which every one of p.85's three own
    /// purchases refuses with nothing spent.
    ///
    /// <para><b>It is a test of what the narration says and not of how long it is.</b> Each of the
    /// three used to ask <c>Narration is not { Length: > 0 }</c>, and a string of spaces is longer
    /// than nothing while saying less. A misfortune or an act of villainy bought with one took the
    /// point and put an empty pair of quotes on the ledger — the pool that has moved with no words
    /// behind it, which is the exact failure those refusals exist to prevent — and a suppression
    /// bought with one reached <see cref="Combatant.Suppressing"/>, which guards on whitespace, and
    /// threw out of <see cref="Step"/>. A purchase the rules refuse is a ledger line and not an
    /// exception: a run that dies on one has no verdict at all, which is the same reason
    /// <see cref="CannotAfford"/> exists.</para>
    /// </summary>
    private static string? Said(SpendAdversity spend) =>
        string.IsNullOrWhiteSpace(spend.Narration) ? null : spend.Narration.Trim();

    /// <summary>
    /// The kinds on one of p.85's eligible or excluded lists, as a sentence names them.
    ///
    /// <para>Each is pluralised on its own rather than the join being: "Foe and Minions" is what a
    /// single trailing letter produces, and the page reads "Foes and Minions lack what it takes".
    /// </para>
    /// </summary>
    private static string Plural(IReadOnlyList<string> kinds) =>
        string.Join(" and ", kinds.Select(kind => kind + "s"));

    /// <summary>
    /// A <see cref="CombatantKind"/> as p.85 spells it, so the eligible and excluded lists on those
    /// entries can be read rather than restated here.
    ///
    /// <para>The one that is not the member's own name is <see cref="CombatantKind.MinionGroup"/>:
    /// this engine's combatant is the group, and the page names the individual.</para>
    /// </summary>
    private static string PrintedKind(CombatantKind kind) =>
        kind == CombatantKind.MinionGroup ? "Minion" : kind.ToString();

    /// <summary>The GM's pool, less what a purchase cost. The one thing that moves it.</summary>
    private static EncounterState ChargeAdversity(EncounterState state, int cost) =>
        state with { Adversity = state.Adversity - cost };

    // ── Turns and pages ──────────────────────────────────────────────────────

    private EncounterState ResolveEndTurn(EncounterState state, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("pages_and_turns");
        var index = state.TurnIndex + 1;

        if (index < state.TurnOrder.Count) return state with { TurnIndex = index, LastAttack = null };

        lines.Add(new LedgerLine(
            state.Page, "", entry.Id, entry.SourceRef,
            $"the page ends when {entry.Page!.PageEndsWhen}, and it has"));

        return state with { TurnIndex = index, LastAttack = null };
    }

    private EncounterState ResolveEndPage(EncounterState state, List<LedgerLine> lines)
    {
        var special = _play.GetCombat("special_effects");
        var holding = _play.GetCombat("holding_an_action");
        var keeping = _play.GetCombat("keeping_hold");
        var turns = _play.GetCombat("pages_and_turns");
        var page = state.Page + 1;

        var effects = new List<SpecialEffect>();

        foreach (var effect in state.Effects)
        {
            // p.76's keeping_hold: a kept effect is no longer measured in pages. It lasts to the end
            // of the following scene, which is past the end of this encounter, so it does not tick.
            if (effect.KeptScenes > 0)
            {
                lines.Add(new LedgerLine(
                    page, effect.Target, keeping.Id, keeping.SourceRef,
                    $"{effect.Name} on {state[effect.Target].Name} does not run out: it has been "
                    + $"kept, and lasts to {keeping.KeepingHold!.ExtendsTo}"));

                effects.Add(effect);
                continue;
            }

            var remaining = effect.RemainingPages - 1;
            if (remaining <= 0)
            {
                lines.Add(new LedgerLine(
                    page, effect.Target, special.Id, special.SourceRef,
                    $"{effect.Name} on {state[effect.Target].Name} runs out"));

                continue;
            }

            effects.Add(effect with { RemainingPages = remaining });
        }

        foreach (var holder in state.Holds)
        {
            lines.Add(new LedgerLine(
                page, holder, holding.Id, holding.SourceRef,
                $"{state[holder].Name} held an action and the cue never came: "
                + holding.Holding!.IfItNeverHappens));
        }

        state = TossWhatWasNeverUsed(state, page, lines);
        state = TickTheDying(state, page, lines);

        // After the clock, because the clock is one of the things that puts a character out.
        state = LetGoOfWhatTheDefeatedHold(state, page, lines);

        foreach (var forfeited in state.LosesNextTurn)
        {
            lines.Add(new LedgerLine(
                page, forfeited, turns.Id, turns.SourceRef,
                $"{state[forfeited].Name} forfeited a turn, so this page has none for them"));
        }

        var order = TurnOrder(
            state.Combatants, state.EffectiveEdge, state.Seized, state.LosesNextTurn, lines, page);

        return state with
        {
            Page = page,
            TurnOrder = order,
            TurnIndex = 0,
            Effects = effects,
            Holds = [],
            LosesNextTurn = [],
            ActiveDefencesThisPage = new Dictionary<string, int>(StringComparer.Ordinal),
            LastAttack = null,
            Over = OneSideIsDown(state)
        };
    }

    /// <summary>
    /// p.76's "on that same page", applied as the page turns: an item a full grab won on the page
    /// that is ending, and which its winner neither used nor tossed, is tossed aside.
    ///
    /// <para><b>The page is the whole of the printed limit, so it is where the clause has to bite.</b>
    /// "You gain control of the object and can use it or toss it aside on that same page" is the only
    /// duration p.76 gives an item, and an engine that let a winner carry it silently into page four
    /// would be keeping a state the book stops describing — which is the shape of defect a full grab
    /// immobilising its loser already was.</para>
    ///
    /// <para><b>An item that <em>was</em> used is left alone</b>, because the page has stopped
    /// talking about it: the clause it was under has been discharged, and what happens to a weapon
    /// somebody has actually swung is the GM's. That is why <see cref="HeldItem.Used"/> exists rather
    /// than the page alone deciding.</para>
    /// </summary>
    private EncounterState TossWhatWasNeverUsed(EncounterState state, int page, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("grab");
        var grab = entry.Grab!;

        // Ordered so a fight with two dropped items writes its lines the same way twice, and
        // snapshotted because the loop replaces combatants in the state it is walking.
        foreach (var combatant in state.Combatants.Values
                     .OrderBy(c => c.Id, StringComparer.Ordinal)
                     .ToList())
        {
            if (combatant.Holding is not { Used: false } won || won.WonOnPage != state.Page) continue;

            lines.Add(new LedgerLine(
                page, combatant.Id, entry.Id, entry.SourceRef,
                $"{combatant.Name} won {won.Name} on page {won.WonOnPage} and neither used nor tossed "
                + $"it. A full grab is {grab.FullMeans} and "
                + $"{nameof(grab.FullAllowsUsingOrTossingItTheSamePage)} is that page and no further, "
                + "so it is tossed aside as the page turns and nobody holds it now"));

            state = state.With(combatant.Dropped());
        }

        return state;
    }

    /// <summary>
    /// What becomes of an object in the hands of a character who is out of the fight: they let go of
    /// it as the page turns, and the ledger says so.
    ///
    /// <para><b>p.76 says nothing about it, and the two candidate silences are not equally
    /// honest.</b> The clause that gives anybody an object at all is "a full grab means you gain
    /// control of the object", and a defeated character controls nothing — this engine refuses them
    /// every intent that is a character <em>doing</em> something. Leaving the item on them is worse
    /// than a state the book stops describing: a grab against a defeated target is refused before it
    /// is rolled, so the object would be out of the fight for good, with nothing on the ledger to
    /// say it had gone.</para>
    ///
    /// <para><b>It is applied at the page turn rather than at the moment of defeat</b>, because
    /// there is no such moment here — damage, a special effect and p.79's clock all put a character
    /// out through paths of their own, and <see cref="Combatant.Defeated"/> is computed rather than
    /// recorded. The page turn is the one place that asks the question once, after the clock has
    /// ticked, which is why this runs after it. Recorded in <c>docs/guide/play-engine.md</c>.</para>
    ///
    /// <para>Where it landed is the GM's, the same silence a toss keeps.</para>
    /// </summary>
    private EncounterState LetGoOfWhatTheDefeatedHold(
        EncounterState state, int page, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("grab");
        var grab = entry.Grab!;
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        foreach (var combatant in state.Combatants.Values
                     .OrderBy(c => c.Id, StringComparer.Ordinal)
                     .ToList())
        {
            if (combatant.Holding is not { } held || !combatant.Defeated(floor)) continue;

            lines.Add(new LedgerLine(
                page, combatant.Id, entry.Id, entry.SourceRef,
                $"{combatant.Name} is out of the fight and lets go of {held.Name}: a full grab is "
                + $"{grab.FullMeans}, and somebody this engine refuses every action to has none. "
                + "Nobody could have taken it off them either — a grapple against a defeated target "
                + "is refused before it is rolled — so leaving it in their hands would take it out "
                + "of the fight with nothing saying so. Where it landed is the GM's"));

            state = state.With(combatant.Dropped());
        }

        return state;
    }

    /// <summary>
    /// Whether one side has nobody standing.
    ///
    /// <para><b>It partitions on <see cref="Combatant.Side"/> and never on
    /// <see cref="Combatant.Kind"/>.</b> Reading the Kind for it made p.73's fight between Heroes
    /// unendable — every combatant was on the Hero side, so no side could ever be down — and put a
    /// Villain's Minions on the same side as the Foe they were fighting. Kind still decides tie
    /// order, Health and who holds Resolve.</para>
    ///
    /// <para>A fight with everybody on one side is never over by this rule, which is the same answer
    /// the previous reading gave for a party with no opposition in it: there is no side to have been
    /// beaten.</para>
    /// </summary>
    private bool OneSideIsDown(EncounterState state)
    {
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        var sides = state.Combatants.Values
            .GroupBy(c => c.Side, StringComparer.Ordinal)
            .ToList();

        return sides.Count > 1 && sides.Exists(side => side.All(c => c.Defeated(floor)));
    }

    // ── Shared ───────────────────────────────────────────────────────────────

    private int WoundPenalty(EncounterState state, Combatant combatant, List<LedgerLine> lines)
    {
        if (!state.Table.WoundPenalties || combatant.Kind == CombatantKind.MinionGroup) return 0;

        var entry = _play.GetGritty("gritty_wound_penalties");
        var rule = entry.WoundPenalties!;

        // The entry's ambiguity: the two bands are printed as thresholds rather than steps, and at
        // zero Health a character is already below half of any positive Health — so the deeper band
        // is read as replacing the shallower one rather than adding to it.
        var penalty = combatant.CurrentHealth <= 0
            ? rule.AtOrBelowZeroHealthPenaltyDice
            : combatant.CurrentHealth * 2 <= combatant.FullHealth
                ? rule.AtOrBelowHalfFullHealthPenaltyDice
                : 0;

        if (penalty != 0)
        {
            lines.Add(new LedgerLine(
                state.Page, combatant.Id, entry.Id, entry.SourceRef,
                $"{combatant.Name} is on {combatant.CurrentHealth} of {combatant.FullHealth} Health: {penalty}d"));
        }

        return penalty;
    }

    private static EncounterState CountActiveDefence(EncounterState state, string id)
    {
        var used = new Dictionary<string, int>(state.ActiveDefencesThisPage, StringComparer.Ordinal)
        {
            [id] = state.ActiveDefencesThisPage.GetValueOrDefault(id) + 1
        };

        return state with { ActiveDefencesThisPage = used };
    }

    /// <summary>
    /// Whether it is somebody else's turn, refused on the ledger citing the entry the refusal is
    /// about.
    ///
    /// <para><b>The citation is <c>pages_and_turns</c>, in both halves of the line.</b> It used to
    /// print the id of whatever the character was trying to do — <c>attacks_and_defenses</c>,
    /// <c>movement</c> — against <c>pages_and_turns</c>'s page reference, so the rule named and the
    /// page cited were two different entries and a reader chasing the citation landed on a rule that
    /// says nothing about turn order. The rule this refusal applies is "every character gets one
    /// turn a page", and that is the entry it names.</para>
    /// </summary>
    private bool NotTheirTurn(EncounterState state, string actor, List<LedgerLine> lines)
    {
        if (string.Equals(state.Current?.Id, actor, StringComparison.Ordinal)) return false;

        var entry = _play.GetCombat("pages_and_turns");

        lines.Add(new LedgerLine(
            state.Page, actor, entry.Id, entry.SourceRef,
            $"it is not {state[actor].Name}'s turn — every character gets "
            + $"{entry.Page!.TurnsPerCharacterPerPage} turn a page, and this one belongs to "
            + $"{state.Current?.Name ?? "nobody: the page is out of turns"}"));

        return true;
    }

    /// <summary>
    /// Whether somebody named by an intent is already out of the fight, refused on the ledger.
    ///
    /// <para><b>A defeated character does not act and is not attacked.</b> p.75 says defeat means
    /// out of the fight and p.76 says an effect that reaches a character's Health puts them out for
    /// the scene; an engine that let either of them keep swinging was applying no rule at all, and a
    /// balance run would have counted their attacks. The refusal cites whichever of the two rules
    /// put them there, because "unconscious at zero Health" and "held for the rest of the scene" are
    /// different states with different ways out.</para>
    ///
    /// <para><b>Resolve purchases are deliberately not guarded by this.</b> Chapter 5's spends are
    /// exactly what a character who has just gone down does — p.76's instant recovery brings them
    /// round, and p.79's Fatal Damage rescue is bought at a Health well past the defeat figure. A
    /// refusal there would block the two purchases the book prints for the situation.</para>
    /// </summary>
    private bool OutOfTheFight(EncounterState state, string id, string role, List<LedgerLine> lines)
    {
        var combatant = state[id];
        var damage = _play.GetCombat("damage");

        if (!combatant.Defeated(damage.Damage!.DefeatedAtHealth)) return false;

        if (combatant.DefeatedByEffect is { } effect)
        {
            var special = _play.GetCombat("special_effects");

            lines.Add(new LedgerLine(
                state.Page, id, special.Id, special.SourceRef,
                $"{combatant.Name} is out for {special.SpecialEffect!.DefeatByEffectLasts} under "
                + $"{effect}, so they are not the {role} of anything"));

            return true;
        }

        lines.Add(new LedgerLine(
            state.Page, id, damage.Id, damage.SourceRef,
            $"{combatant.Name} is {damage.Damage.DefeatedMeans}, so they are not the {role} of anything"));

        return true;
    }

    /// <summary>
    /// A refusal, on the ledger, <b>citing the entry it is about</b>.
    ///
    /// <para>Every line the engine writes carries the <c>source_ref</c> of the rule it applied, so
    /// any figure in a run traces to a printed page — and a refusal is a rule applied just as much as
    /// a hit is. These used to print <c>—</c> where the citation goes, which made the one class of
    /// line a reader most needs to check the least checkable.</para>
    /// </summary>
    private static EncounterState Refuse(
        EncounterState state, string actor, string ruleId, string sourceRef,
        List<LedgerLine> lines, string why)
    {
        lines.Add(new LedgerLine(state.Page, actor, ruleId, sourceRef, why));
        return state;
    }

    /// <summary>
    /// A spend this slice does not resolve, named on the ledger and changing nothing.
    ///
    /// <para><b>The <c>Rule</c> is the entry's id and not the enum member's name.</b> A ledger whose
    /// rule column reads <c>KeepingHold</c> names a C# identifier, which is not a thing anybody can
    /// look up in the book; <c>keeping_hold</c> is. The sentence still carries the enum name, because
    /// that is what a caller passed in and what they will search for.</para>
    /// </summary>
    private static EncounterState NotYetImplementedSpend(
        EncounterState state, string actor, string what, string ruleId, string sourceRef,
        List<LedgerLine> lines)
    {
        lines.Add(new LedgerLine(
            state.Page, actor, ruleId, sourceRef,
            $"not yet implemented: {what}. Nothing was spent and nothing changed — see "
            + "docs/guide/play-engine.md for the list"));

        return state;
    }

    /// <summary>Half, in the direction the entry's own reading names.</summary>
    private static int Half(int value, string direction) => Rounding.Half(value, direction);

    /// <summary>
    /// Half, rounding the way the Glossary's book-wide rule (p.7) rounds — <b>read out of
    /// <c>play_meta.half_rounds_up</c> and not typed here</b>. It used to be a
    /// <c>Math.Ceiling(value / 2.0)</c>, which is a second transcription of the convention and would
    /// have gone on agreeing with the file right up until somebody corrected the file.
    /// </summary>
    private int Halve(int value) => Rounding.Half(_play, value);

    /// <summary>A printed Trait name as the character rules spell its id.</summary>
    private static string Normalise(string name) =>
        name.ToLowerInvariant().Replace(' ', '_');
}
