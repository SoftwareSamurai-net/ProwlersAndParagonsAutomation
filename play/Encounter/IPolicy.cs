namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// What a combatant does when it is their turn.
///
/// <para><b>A policy is not a rule and must never be read as one.</b> The engine says what an intent
/// produces; a policy says which intent a character would have chosen, which is a person's decision
/// at a table and a guess anywhere else. That is why <see cref="Name"/> exists and why every report
/// built on a run has to print it beside the seed, the N and the table settings: a balance figure is
/// a figure about a particular way of playing, and one quoted without its policy is a figure about
/// nothing.</para>
/// </summary>
public interface IPolicy
{
    /// <summary>What a report calls this policy.</summary>
    string Name { get; }

    /// <summary>The intent this combatant takes on their turn.</summary>
    Intent Choose(EncounterState state, Combatant actor);

    /// <summary>
    /// An optional second intent, taken once the dice are on the table.
    ///
    /// <para>Chapter 5's two commonest purchases are both decided after the roll (p.84), so a policy
    /// that could only choose beforehand could not spend Resolve the way the book means it to be
    /// spent. Return null to do nothing.</para>
    /// </summary>
    Intent? AfterRoll(EncounterState state, Combatant actor) => null;
}
