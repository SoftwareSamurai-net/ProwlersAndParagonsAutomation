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
    public EncounterState RunToEnd(EncounterState state, IPolicy policy, int maxPages)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);

        var floor = DefeatFloor;
        var opening = _play.GetCombat("pages_and_turns");

        state = state with
        {
            Ledger = state.Ledger.Plus([
                new LedgerLine(
                    state.Page, "", opening.Id, opening.SourceRef,
                    $"running to at most {maxPages} pages under the policy {policy.Name}")
            ])
        };

        while (!state.Over && state.Page <= maxPages)
        {
            var actor = state.Current;

            if (actor is null)
            {
                state = Step(state, new EndPage("")).State;
                continue;
            }

            if (actor.Defeated(floor))
            {
                state = Step(state, new EndTurn(actor.Id)).State;
                continue;
            }

            state = Step(state, policy.Choose(state, actor)).State;

            if (policy.AfterRoll(state, state[actor.Id]) is { } follow)
                state = Step(state, follow).State;

            state = Step(state, new EndTurn(actor.Id)).State;
        }

        if (state.Over) return state;

        return state with
        {
            Over = true,
            Ledger = state.Ledger.Plus([
                new LedgerLine(
                    state.Page, "", opening.Id, opening.SourceRef,
                    $"the run reached its limit of {maxPages} pages with both sides still standing")
            ])
        };
    }
}
