using ProwlersAndParagonsAutomation.Play.Rules;

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
            BreakFree free => ResolveBreakFree(state, free, lines),
            SpendResolve spend => ResolveResolveSpend(state, spend, lines),
            SpendAdversity spend => ResolveAdversitySpend(state, spend, lines),
            EndTurn => ResolveEndTurn(state, lines),
            EndPage => ResolveEndPage(state, lines),
            _ => Refuse(state, intent.Actor, "unknown_intent", lines,
                     $"not yet implemented: {intent.GetType().Name}")
        };

        return new StepResult(next with { Ledger = next.Ledger.Plus(lines) }, lines);
    }

    // ── Attacks ──────────────────────────────────────────────────────────────

    private EncounterState ResolveAttack(EncounterState state, Attack attack, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, attack.Actor, lines, "attacks_and_defenses")) return state;

        var actor = state[attack.Actor];
        var target = state[attack.Target];
        var entry = _play.GetCombat("attacks_and_defenses");

        var rank = actor.Rank(attack.TraitId);

        if (rank <= 0)
        {
            return Refuse(state, actor.Id, entry.Id, lines,
                $"{actor.Name} has no rank in {attack.TraitId}, so there is no pool to throw");
        }

        var pool = rank + AttackModifiers(state, actor, attack, lines);
        var attackRoll = _counter.Roll(pool, _dice);

        state = CommitToTheAttack(state, actor, attack);

        var (defenceTrait, defencePool, defenceIsActive) = ChooseDefence(state, target, attack, lines);
        var defenceRoll = _counter.Roll(defencePool, _dice);

        var after = defenceIsActive ? CountActiveDefence(state, target.Id) : state;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} attacks {target.Name} with {attack.TraitId} {pool}d for "
            + $"{attackRoll.Successes} successes; {target.Name} defends with {defenceTrait} "
            + $"{defencePool}d for {defenceRoll.Successes}"));

        var resolved = new ResolvedAttack(
            actor.Id, target.Id, pool, attackRoll.Successes, defenceRoll.Successes,
            target, state.Effects, attack.Effect, attack.Area);

        after = ApplyAttackOutcome(after, attack, resolved, lines);

        return after with { LastAttack = resolved };
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
    private int AttackModifiers(EncounterState state, Combatant actor, Attack attack, List<LedgerLine> lines)
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

        if (actor.Kind == CombatantKind.MinionGroup)
        {
            var table = _play.GetCombat("minion_group_attack_table");

            var row = table.MinionGroupAttack!
                .FirstOrDefault(r => actor.GroupSize >= r.MinMinions && actor.GroupSize <= r.MaxMinions);

            if (row is not null)
            {
                modifier += row.BonusDice;

                lines.Add(new LedgerLine(
                    state.Page, actor.Id, table.Id, table.SourceRef,
                    $"{actor.GroupSize} Minions attacking as a group is +{row.BonusDice}d, on the "
                    + "attack roll and nothing else"));
            }
        }

        modifier += WoundPenalty(state, actor, lines);

        return modifier;
    }

    /// <summary>
    /// Which Trait answers an attack, and what pool it throws.
    ///
    /// <para><b>The choices come from <c>active_and_passive_defenses</c> and the halving from
    /// <c>lethal_and_subdual</c>.</b> One defence answers each attack and it is "normally the one
    /// with the greatest rank" — greatest <em>after</em> the halvings, because a Toughness of 8
    /// against a lethal attack is a 4 and an Armor of 6 beside it is the better answer.</para>
    ///
    /// <para>A character in a full hold has no active defence: p.75 lists being immobilized among
    /// the states that take them away.</para>
    /// </summary>
    private (string Trait, int Pool, bool Active) ChooseDefence(
        EncounterState state, Combatant target, Attack attack, List<LedgerLine> lines)
    {
        var types = _play.GetCombat("lethal_and_subdual").DamageTypes!;
        var defenses = _play.GetCombat("active_and_passive_defenses").Defenses!;

        var actives = defenses.CommonActiveTraits
            .Select(Normalise)
            .ToHashSet(StringComparer.Ordinal);

        var immobilised = state.Grapples.Any(g =>
            string.Equals(g.Held, target.Id, StringComparison.Ordinal) && g.Kind == GrappleKind.Full);

        var halved = state.DefencesHalved.TryGetValue(target.Id, out var penalty) && state.Page <= penalty.UntilPage
            ? penalty
            : null;

        var candidates = target.Kind == CombatantKind.MinionGroup
            ? target.Defences
            : attack.Damage == DamageKind.Psychic
                ? [Normalise(types.PsychicDamageResistedWith)]
                : target.Defences;

        var best = ("", 0, false);

        foreach (var trait in candidates)
        {
            var active = actives.Contains(trait);
            if (active && immobilised) continue;

            var rank = target.Rank(trait);
            if (rank <= 0) continue;

            // p.75: Toughness answers a lethal attack at half and a subdual one in full.
            if (string.Equals(trait, "toughness", StringComparison.Ordinal)
                && attack.Damage == DamageKind.Lethal
                && string.Equals(types.ToughnessAgainstLethal, "half", StringComparison.Ordinal))
            {
                rank = Halve(rank);
            }

            if (halved is not null && (!halved.ActiveOnly || active)) rank = Halve(rank);

            if (rank > best.Item2) best = (trait, rank, active);
        }

        if (best.Item1.Length == 0)
        {
            // Nothing left to answer with. p.75 still has the attack resolved against a threshold,
            // and a threshold of nothing is what "no defence available" means.
            return ("no defence", 0, false);
        }

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

        return (best.Item1, pool, best.Item3);
    }

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

        return InflictDamage(after, actor, target, net, lines);
    }

    private EncounterState DefeatMinions(
        EncounterState state, Attack attack, Combatant target, int net, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("attacking_minions");
        var rule = entry.AttackingMinions!;

        var perNet = Math.Min(
            attack.Area ? rule.MinionsDefeatedPerNetSuccessWithAnAreaAttack : rule.MinionsDefeatedPerNetSuccess,
            rule.MaximumMinionsPerNetSuccess);

        var couldDefeat = net * perNet;
        var citation = entry;

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

            citation = null;

            lines.Add(new LedgerLine(
                state.Page, "", gritty.Id, gritty.SourceRef,
                $"Tough Minions: {net} net successes defeat {couldDefeat}, rounding down — the one "
                + "place in the book where a half goes downward"));
        }

        // <b>The cap is applied to both rates, and the entry's ambiguity is why that is a
        // decision.</b> p.77 prints "(up to the number of Minions in the area of effect or within
        // reach)" as a parenthesis on the area-attack clause alone, and the entry records it there,
        // named for that clause. Its second half — "or within reach" — is the phrase for an ordinary
        // attack, which reads as though the whole rule was meant to be capped. Capping both is the
        // only reading under which an attack cannot knock out a Minion who is not there.
        var defeated = Math.Min(couldDefeat, target.GroupSize);

        if (citation is not null)
        {
            lines.Add(new LedgerLine(
                state.Page, "", citation.Id, citation.SourceRef,
                $"{net} net successes could defeat {couldDefeat} Minions, capped by the "
                + $"{target.GroupSize} actually there: {defeated} defeated"));
        }

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

    private EncounterState DefeatByEffect(
        EncounterState state, Combatant target, string effect, int duration, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("special_effects");

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"{duration} pages of {effect} reaches {target.Name}'s current Health of "
            + $"{target.CurrentHealth}, so they are out for {entry.SpecialEffect!.DefeatByEffectLasts}"));

        return state;
    }

    private EncounterState InflictDamage(
        EncounterState state, Combatant actor, Combatant target, int net, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("damage");
        var rule = entry.Damage!;
        var damage = net * rule.DamagePerNetSuccess;
        var health = target.CurrentHealth - damage;

        if (!state.Table.FatalDamage)
        {
            health = Math.Max(rule.DefeatedAtHealth, health);

            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{net} net successes is {damage} damage; {target.Name} is on {health} Health"
                + (health <= rule.DefeatedAtHealth ? $", which is {rule.DefeatedMeans}" : "")));

            return state.With(target.WithHealth(health));
        }

        var gritty = _play.GetGritty("gritty_fatal_damage");
        var fatal = -target.FullHealth;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{net} net successes is {damage} damage; {target.Name} is on {health} Health"));

        if (health <= fatal)
        {
            lines.Add(new LedgerLine(
                state.Page, target.Id, gritty.Id, gritty.SourceRef,
                $"{health} reaches {fatal}, the negative of {target.Name}'s full Health of "
                + $"{target.FullHealth}, which is fatal — 1 Resolve buys it back"));
        }

        return state.With(target.WithHealth(health));
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
        if (NotTheirTurn(state, move.Actor, lines, "movement")) return state;

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
        if (NotTheirTurn(state, hold.Actor, lines, "holding_an_action")) return state;

        var entry = _play.GetCombat("holding_an_action");
        var actor = state[hold.Actor];

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} holds their action; if the cue never comes, {entry.Holding!.IfItNeverHappens}"));

        return state with { Holds = [.. state.Holds, hold.Actor] };
    }

    private EncounterState ResolveGrapple(EncounterState state, GrappleIntent grapple, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, grapple.Actor, lines, "grappling")) return state;

        var entry = _play.GetCombat("grappling");
        var table = _play.GetCombat("grappling_table");
        var rule = entry.Grappling!;

        var actor = state[grapple.Actor];
        var target = state[grapple.Target];

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
            : ApplyGrappleResult(state, actor, target, result);
    }

    private static EncounterState ApplyGrappleResult(
        EncounterState state, Combatant actor, Combatant target, string result)
    {
        if (result.Contains("no effect", StringComparison.Ordinal)) return state;

        var kind = result.Contains("full", StringComparison.Ordinal) ? GrappleKind.Full : GrappleKind.Partial;

        var grapples = state.Grapples
            .Where(g => !(string.Equals(g.Holder, actor.Id, StringComparison.Ordinal)
                          && string.Equals(g.Held, target.Id, StringComparison.Ordinal)))
            .Append(new Grapple(actor.Id, target.Id, kind))
            .ToList();

        return state with { Grapples = grapples };
    }

    private EncounterState ApplyEscape(
        EncounterState state, Combatant actor, string result, List<LedgerLine> lines)
    {
        if (result.Contains("no effect", StringComparison.Ordinal)) return state;

        var entry = _play.GetCombat("escape");
        var escape = entry.Escape!;

        var held = state.Grapples
            .Where(g => string.Equals(g.Held, actor.Id, StringComparison.Ordinal))
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

    private EncounterState ResolveBreakFree(EncounterState state, BreakFree free, List<LedgerLine> lines)
    {
        if (NotTheirTurn(state, free.Actor, lines, "breaking_free")) return state;

        var entry = _play.GetCombat("breaking_free");
        var rule = entry.BreakingFree!;
        var actor = state[free.Actor];

        var effect = state.Effects.FirstOrDefault(e =>
            string.Equals(e.Target, free.Actor, StringComparison.Ordinal));

        if (effect is null)
        {
            return Refuse(state, actor.Id, entry.Id, lines,
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
            return Refuse(state, actor.Id, "spend_combat", lines,
                $"{actor.Name} is a {actor.Kind} and holds no Resolve — only Heroes have any, and "
                + "the GM spends Adversity on an NPC instead");
        }

        return spend.Kind switch
        {
            ResolveSpend.ExtraDice => BuyDice(state, actor, spend.Points, lines),
            ResolveSpend.Reroll => BuyReroll(state, actor, lines),
            ResolveSpend.SeizeInitiative => SeizeInitiative(state, actor, lines),
            ResolveSpend.AvoidFatalDamage => AvoidFatalDamage(state, actor, lines),
            _ => NotYetImplementedSpend(state, actor.Id, spend.Kind.ToString(), lines)
        };
    }

    /// <summary>
    /// Ch.5 p.84: one point, one die, on a roll that has already been made.
    ///
    /// <para>The dice are added to the attack pool and the outcome recomputed against the target as
    /// they were, which is why <see cref="EncounterState.LastAttack"/> keeps that snapshot.</para>
    /// </summary>
    private EncounterState BuyDice(EncounterState state, Combatant actor, int points, List<LedgerLine> lines)
    {
        var entry = _play.GetResolve("spend_challenge_roll_dice");
        var spend = entry.Spend!;

        if (state.LastAttack is not { } last || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, lines,
                $"{actor.Name} has no roll on the table to buy dice for");
        }

        var cost = spend.CostResolve!.Value * points;
        var extra = spend.DiceGained!.Value * points;
        var roll = _counter.Roll(extra, _dice);

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} spends {cost} Resolve for {extra} more dice after the roll, scoring "
            + $"{roll.Successes} more: {last.AttackSuccesses} becomes {last.AttackSuccesses + roll.Successes}"));

        var improved = last with { AttackSuccesses = last.AttackSuccesses + roll.Successes };

        var after = state.With(actor.Spending(cost));
        after = ReapplyLastAttack(after, improved, lines);

        return after with { LastAttack = improved };
    }

    /// <summary>
    /// Ch.5 p.84: one point picks the whole roll back up — and p.85's tip puts a floor under it, so
    /// a worse reroll is discarded and the first roll stands.
    /// </summary>
    private EncounterState BuyReroll(EncounterState state, Combatant actor, List<LedgerLine> lines)
    {
        var entry = _play.GetResolve("spend_reroll_challenge_roll");
        var floor = _play.GetResolve("reroll_floor");

        if (state.LastAttack is not { } last || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, lines,
                $"{actor.Name} has no challenge roll on the table to pick back up");
        }

        var cost = entry.Spend!.CostResolve!.Value;
        var roll = _counter.Roll(last.AttackPool, _dice);

        var kept = floor.RerollFloor!.KeepTheFirstRollIfTheRerollIsWorse
            ? Math.Max(last.AttackSuccesses, roll.Successes)
            : roll.Successes;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{actor.Name} spends {cost} Resolve to reroll {last.AttackPool}d: {roll.Successes} "
            + $"against the first {last.AttackSuccesses}, keeping {kept}"));

        var improved = last with { AttackSuccesses = kept };

        var after = state.With(actor.Spending(cost));
        after = ReapplyLastAttack(after, improved, lines);

        return after with { LastAttack = improved };
    }

    private EncounterState ReapplyLastAttack(
        EncounterState state, ResolvedAttack resolved, List<LedgerLine> lines)
    {
        var attack = new Attack(
            resolved.Actor, resolved.Target, "(already rolled)",
            Effect: resolved.Effect, Area: resolved.Area);

        return ApplyAttackOutcome(state, attack, resolved, lines);
    }

    private EncounterState SeizeInitiative(EncounterState state, Combatant actor, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("seizing_initiative");
        var rule = entry.SeizeInitiative!;

        if (state.Seized.Contains(actor.Id, StringComparer.Ordinal))
        {
            return Refuse(state, actor.Id, entry.Id, lines,
                $"{actor.Name} has already seized the initiative, and it lasts {rule.Duration}");
        }

        var alternative = state.Table.GmAlternativeToSeizingInitiative
            ? _play.GetCombat("seize_initiative_gm_alternative")
            : null;

        lines.Add(new LedgerLine(
            state.Page, actor.Id, alternative?.Id ?? entry.Id, alternative?.SourceRef ?? entry.SourceRef,
            $"{actor.Name} spends {rule.CostResolve} Resolve to seize the initiative — "
            + (alternative is null
                ? $"{rule.Effect}, for {rule.Duration}"
                : $"the GM has taken the alternative, so it {alternative.GmAlternative!.Effect} instead, "
                  + $"for the duration it inherits from {alternative.Interpretation!.DurationIsInheritedFrom}")
            + "; it takes effect when the page turns"));

        return state.With(actor.Spending(rule.CostResolve)) with { Seized = [.. state.Seized, actor.Id] };
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
    /// </summary>
    private EncounterState AvoidFatalDamage(EncounterState state, Combatant actor, List<LedgerLine> lines)
    {
        var entry = _play.GetGritty("gritty_fatal_damage");
        var fatal = entry.FatalDamage!;

        if (!state.Table.FatalDamage)
        {
            return Refuse(state, actor.Id, entry.Id, lines,
                "Fatal Damage is not one of this table's settings, so there is no threshold to buy back from");
        }

        var threshold = -actor.FullHealth;
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
            $"{actor.Name} spends {fatal.CostResolveToAvoid} Resolve against a fatal blow: "
            + $"{actor.CurrentHealth} becomes {rescued}, one point above the threshold of {threshold}. "
            + "The printed word says one point below it and the printed example says above; the "
            + $"entry's ambiguity records the contradiction: {fatal.ResolveReducesDamageTo} is what "
            + "p.79 says, and the example is what this follows"));

        return state.With(actor.Spending(fatal.CostResolveToAvoid).WithHealth(rescued));
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

        if (state.Adversity < spend.Points)
        {
            return Refuse(state, spend.Actor, entry.Id, lines,
                $"the GM has {state.Adversity} Adversity and the spend costs {spend.Points}");
        }

        return NotYetImplementedSpend(state, spend.Actor, entry.Id, lines);
    }

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
        var page = state.Page + 1;

        var effects = new List<SpecialEffect>();

        foreach (var effect in state.Effects)
        {
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

        var order = TurnOrder(state.Combatants, state.EffectiveEdge, state.Seized, lines, page);

        return state with
        {
            Page = page,
            TurnOrder = order,
            TurnIndex = 0,
            Effects = effects,
            Holds = [],
            ActiveDefencesThisPage = new Dictionary<string, int>(StringComparer.Ordinal),
            LastAttack = null,
            Over = OneSideIsDown(state)
        };
    }

    /// <summary>
    /// Whether one side has nobody standing. See this class's own summary: the split into Heroes and
    /// everybody else is this engine's reading, not a rule the book prints.
    /// </summary>
    private bool OneSideIsDown(EncounterState state)
    {
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        var heroes = state.Combatants.Values.Where(c => c.Kind == CombatantKind.Hero).ToList();
        var others = state.Combatants.Values.Where(c => c.Kind != CombatantKind.Hero).ToList();

        return (heroes.Count > 0 && heroes.TrueForAll(c => c.Defeated(floor)))
            || (others.Count > 0 && others.TrueForAll(c => c.Defeated(floor)));
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

    private bool NotTheirTurn(EncounterState state, string actor, List<LedgerLine> lines, string ruleId)
    {
        if (string.Equals(state.Current?.Id, actor, StringComparison.Ordinal)) return false;

        var entry = _play.GetCombat("pages_and_turns");

        lines.Add(new LedgerLine(
            state.Page, actor, ruleId, entry.SourceRef,
            $"it is not {state[actor].Name}'s turn — every character gets "
            + $"{entry.Page!.TurnsPerCharacterPerPage} turn a page, and this one belongs to "
            + $"{state.Current?.Name ?? "nobody: the page is out of turns"}"));

        return true;
    }

    private static EncounterState Refuse(
        EncounterState state, string actor, string ruleId, List<LedgerLine> lines, string why)
    {
        lines.Add(new LedgerLine(state.Page, actor, ruleId, "—", why));
        return state;
    }

    private static EncounterState NotYetImplementedSpend(
        EncounterState state, string actor, string what, List<LedgerLine> lines)
    {
        lines.Add(new LedgerLine(
            state.Page, actor, what, "—",
            $"not yet implemented: {what}. Nothing was spent and nothing changed — see "
            + "docs/guide/play-engine.md for the list"));

        return state;
    }

    /// <summary>Half, in the direction the entry's own reading names.</summary>
    private static int Half(int value, string direction) => direction switch
    {
        "up" => (int)Math.Ceiling(value / 2.0),
        "down" => value / 2,
        var other => throw new InvalidOperationException($"'{other}' is neither up nor down.")
    };

    /// <summary>Half, rounding the way the Glossary's book-wide rule does.</summary>
    private static int Halve(int value) => (int)Math.Ceiling(value / 2.0);

    /// <summary>A printed Trait name as the character rules spell its id.</summary>
    private static string Normalise(string name) =>
        name.ToLowerInvariant().Replace(' ', '_');
}
