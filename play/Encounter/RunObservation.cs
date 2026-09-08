namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// One exchange, as the run that stepped it saw it happen.
///
/// <para><b>Everything here but <see cref="DefenceTrait"/> is structural.</b> It is read off
/// <see cref="ResolvedAttack"/> — which is the engine's own record of the roll — and off the
/// difference between the target as they were and the target as they are, which is a subtraction
/// between two immutable states. Nothing is restated from the intent that asked for it, so a rule
/// that refused the attack, halved the pool or capped the damage moves these figures with it.</para>
/// </summary>
/// <param name="Page">Which page of the fight.</param>
/// <param name="Attacker">Who swung.</param>
/// <param name="Target">Who they swung at.</param>
/// <param name="TraitId">The Trait or Power that was rolled.</param>
/// <param name="DefenceTrait">
/// The Trait that answered, read back off the ledger — or null where the line it is read from could
/// not be found, which a report publishes as a count rather than hiding.
/// </param>
/// <param name="AttackSuccesses">What the attack scored, after any purchase made on it.</param>
/// <param name="DefenceSuccesses">What the defence scored, which is the threshold.</param>
/// <param name="Damage">Health actually taken off the target, which is nothing where it did not land.</param>
/// <param name="MinionsDefeated">Bodies actually taken out of a group, for a target that is one.</param>
/// <param name="Effect">The special effect this attack inflicts instead of damage, or null.</param>
public sealed record ObservedAttack(
    int Page,
    string Attacker,
    string Target,
    string TraitId,
    string? DefenceTrait,
    int AttackSuccesses,
    int DefenceSuccesses,
    int Damage,
    int MinionsDefeated,
    string? Effect)
{
    /// <summary>Whether the defence was enough: p.75 measures every outcome as successes less threshold.</summary>
    public bool DefenceHeld => AttackSuccesses <= DefenceSuccesses;

    /// <summary>Whether anything actually came off the target.</summary>
    public bool Landed => Damage > 0 || MinionsDefeated > 0;
}

/// <summary>
/// One combatant going out of the fight, and what put them there.
/// </summary>
/// <param name="Page">The page they went down on.</param>
/// <param name="Combatant">Who.</param>
/// <param name="By">
/// Whose attack it was, or null where nothing was attributable — p.79's dying clock ticks at the
/// page turn and belongs to nobody's turn.
/// </param>
/// <param name="With">
/// The Trait or Power that did it, or the special effect where p.76's second way out of a fight is
/// what happened. Null alongside a null <paramref name="By"/>.
/// </param>
public sealed record ObservedDefeat(int Page, string Combatant, string? By, string? With);

/// <summary>
/// What one seeded fight did, beyond the state it ended in.
///
/// <para><b>It exists because a rate is not the whole of a balance question.</b> The owner asks two
/// things of a run and only the first is a win rate: the second is <em>what has this party no
/// answer for, and where is each character strongest and weakest</em>. That is a question about
/// which attacks landed and which defences held, and neither is anywhere in the final state — a
/// fight that ended 4–0 and a fight that ended 4–0 because one Power went through everybody are the
/// same two numbers.</para>
/// </summary>
/// <param name="Pages">How many pages the fight ran to.</param>
/// <param name="Attacks">Every exchange, in order.</param>
/// <param name="Defeats">Everybody who went down, in order, with what put them there.</param>
/// <param name="LastPageStandingOn">
/// For each combatant, the page they went down on — or the last page of the fight, where they were
/// still up when it ended. A survivor and a character defeated on the final page are not
/// distinguished by this figure alone, which is why <see cref="Defeats"/> is beside it.
/// </param>
/// <param name="DefenceTraitsUnread">
/// How many exchanges the defending Trait could not be read back for. <b>Published rather than
/// swallowed</b>: it is zero on every fight this engine resolves today, and a figure above zero
/// means the ledger sentence <see cref="LedgerReading"/> is anchored on has moved and a report's
/// defence table is short of that many rows.
/// </param>
public sealed record RunObservation(
    int Pages,
    IReadOnlyList<ObservedAttack> Attacks,
    IReadOnlyList<ObservedDefeat> Defeats,
    IReadOnlyDictionary<string, int> LastPageStandingOn,
    int DefenceTraitsUnread);

/// <summary>A run, and what was observed of it.</summary>
/// <param name="State">Where the fight ended.</param>
/// <param name="Observed">What happened on the way there.</param>
public sealed record RunResult(EncounterState State, RunObservation Observed);
