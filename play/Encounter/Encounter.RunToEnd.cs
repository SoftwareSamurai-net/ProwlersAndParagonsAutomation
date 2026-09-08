namespace ProwlersAndParagonsAutomation.Play.Encounter;

public sealed partial class Encounter
{
    /// <summary>
    /// The Health at which a character is out of the fight, from <c>damage.defeated_at_health</c>.
    /// </summary>
    public int DefeatFloor => _play.GetCombat("damage").Damage!.DefeatedAtHealth;

    /// <summary>
    /// Runs a fight to its end, or to <paramref name="maxPages"/>, with a policy choosing each turn.
    ///
    /// <para><b>It always terminates, and the page limit is why rather than an argument that it
    /// would.</b> Every path through the loop either advances a turn or turns a page, and a page
    /// past the limit ends the run whatever the state of the fight — a rule that stopped removing
    /// Health, or two combatants who cannot hurt each other, is a fight that would otherwise run for
    /// ever, and a simulator that hangs is worse than one that reports a draw. A property test
    /// drives seeded encounters and requires every one of them to come back.</para>
    ///
    /// <para><b>The policy's name goes in the ledger before anything else happens</b>, because the
    /// run's numbers mean nothing without it.</para>
    /// </summary>
    public EncounterState RunToEnd(EncounterState state, IPolicy policy, int maxPages) =>
        Run(state, policy, maxPages).State;

    /// <summary>
    /// The same run, with what happened on the way through it.
    ///
    /// <para><b>A win rate is not the whole of a balance question and this is the other half.</b>
    /// The owner asks a seeded run two things and only the first is a rate: the second is what a
    /// party has no answer for, and where each character is strongest and weakest — which is a
    /// question about which attacks landed and which defences held. Neither survives into the final
    /// state: a fight that ended 4–0 and a fight that ended 4–0 because one Power went through
    /// everybody are the same two numbers.</para>
    ///
    /// <para><b>It is observed rather than restated.</b> Every figure comes off
    /// <see cref="ResolvedAttack"/> — the engine's own record of the roll — or off the difference
    /// between two immutable states, so a refusal, a halved pool or a capped rate moves the
    /// observation with it. One fact is not there to be read structurally, the Trait that answered
    /// an attack, and it is taken off the ledger by <see cref="LedgerReading"/> with the count of
    /// what it could not read published beside the figures rather than swallowed.</para>
    ///
    /// <para><b>An attack is recorded per turn and not per step</b>, which is what keeps a purchase
    /// from being counted as a second exchange: a bought die and a reroll both re-apply the attack
    /// already on the table, so <see cref="EncounterState.LastAttack"/> is read once the turn's
    /// purchase has been made, and the damage is measured against the target as
    /// <see cref="ResolvedAttack.TargetBefore"/> had them. The one case this deliberately does not
    /// separate is p.80's Friendly Fire, whose stray round is a second attack resolved inside the
    /// first and overwrites the record; that setting is off in every default measurement.</para>
    /// </summary>
    public RunResult RunObserved(EncounterState state, IPolicy policy, int maxPages) =>
        Run(state, policy, maxPages);

    private RunResult Run(EncounterState state, IPolicy policy, int maxPages)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);

        var floor = DefeatFloor;
        var opening = _play.GetCombat("pages_and_turns");

        var attacks = new List<ObservedAttack>();
        var defeats = new List<ObservedDefeat>();
        var alreadyDown = new HashSet<string>(StringComparer.Ordinal);
        var unread = 0;

        state = state with
        {
            Ledger = state.Ledger.Plus([
                new LedgerLine(
                    state.Page, "", opening.Id, opening.SourceRef,
                    $"running to at most {maxPages} pages under the policy {policy.Name}")
            ])
        };

        // Anybody already out before the first turn — a fight opened with a defeated combatant in
        // it — is recorded as down on page one rather than attributed to whoever acts first.
        Note(state);

        while (!state.Over && state.Page <= maxPages)
        {
            var actor = state.Current;

            if (actor is null)
            {
                state = Step(state, new EndPage("")).State;

                // p.79's dying clock ticks at the page turn, so a defeat can happen with nobody
                // acting. Attributed to nobody, which is what the clock is.
                Note(state);
                continue;
            }

            if (actor.Defeated(floor))
            {
                state = Step(state, new EndTurn(actor.Id)).State;
                continue;
            }

            var before = state;

            var acted = Step(state, policy.Choose(state, actor));
            state = acted.State;

            var added = new List<LedgerLine>(acted.Added);

            if (policy.AfterRoll(state, state[actor.Id]) is { } follow)
            {
                var bought = Step(state, follow);
                state = bought.State;
                added.AddRange(bought.Added);
            }

            if (Exchange(before, state, added) is { } exchange)
            {
                attacks.Add(exchange);
                if (exchange.DefenceTrait is null) unread++;
            }

            Note(state);

            state = Step(state, new EndTurn(actor.Id)).State;
        }

        if (!state.Over)
        {
            state = state with
            {
                Over = true,
                Ledger = state.Ledger.Plus([
                    new LedgerLine(
                        state.Page, "", opening.Id, opening.SourceRef,
                        $"the run reached its limit of {maxPages} pages with both sides still standing")
                ])
            };
        }

        var lastPage = state.Combatants.Values.ToDictionary(
            c => c.Id,
            c => defeats.FirstOrDefault(d => string.Equals(d.Combatant, c.Id, StringComparison.Ordinal))?.Page
                 ?? state.Page,
            StringComparer.Ordinal);

        return new RunResult(state, new RunObservation(state.Page, attacks, defeats, lastPage, unread));

        // Whoever has gone down since the last look, and what put them there. A combatant is
        // recorded once: `Defeated` is computed rather than recorded, so asking twice would answer
        // twice.
        void Note(EncounterState now)
        {
            foreach (var combatant in now.Combatants.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                if (!combatant.Defeated(floor) || !alreadyDown.Add(combatant.Id)) continue;

                var last = now.LastAttack;

                var byAttack = last is not null
                               && string.Equals(last.Target, combatant.Id, StringComparison.Ordinal);

                defeats.Add(new ObservedDefeat(
                    now.Page,
                    combatant.Id,
                    byAttack ? last!.Actor : null,
                    byAttack ? last!.Effect ?? last.TraitId : null));
            }
        }
    }

    /// <summary>
    /// The exchange this turn produced, or null where the turn was a hold, a move, a grapple, or an
    /// attack refused before the dice.
    ///
    /// <para><b>"An attack happened" is <see cref="EncounterState.LastAttack"/> having been
    /// replaced</b>, compared by reference against the state the turn opened on — not "the intent
    /// was an <see cref="Attack"/>". Every refusal in <c>ResolveAttack</c> returns without touching
    /// that field, so an attack the rules would not allow is correctly not an exchange, and a report
    /// counting intents rather than outcomes would have credited it with a miss.</para>
    /// </summary>
    private static ObservedAttack? Exchange(
        EncounterState before, EncounterState after, IReadOnlyList<LedgerLine> added)
    {
        if (ReferenceEquals(after.LastAttack, before.LastAttack) || after.LastAttack is not { } last)
            return null;

        var target = after[last.Target];
        var line = added.FirstOrDefault(LedgerReading.IsAnAttack);

        return new ObservedAttack(
            before.Page,
            last.Actor,
            last.Target,
            last.TraitId,
            line is null ? null : LedgerReading.DefenceTraitIn(line),
            last.AttackSuccesses,
            last.DefenceSuccesses,
            Math.Max(0, last.TargetBefore.CurrentHealth - target.CurrentHealth),
            Math.Max(0, last.TargetBefore.GroupSize - target.GroupSize),
            last.Effect);
    }
}
