using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Loads the real <c>data/rules/play</c> JSON once for the whole run, through the same
/// <see cref="IRulesSource"/> a host would use.
///
/// <para>Same reasoning as <see cref="RulesFixture"/>: the suite asserts against the shipped data
/// deliberately, because its job is to catch a rules file drifting away from the book — and, here,
/// to catch the engine drifting away from the file.</para>
/// </summary>
public sealed class PlayFixture
{
    /// <summary>The shipped play rules.</summary>
    public PlayRulesRepository Play { get; } = new(new FileSystemRulesSource(DataPath));

    /// <summary>The <c>data/rules/play</c> directory.</summary>
    public static string DataPath => Path.Combine(RulesFixture.RepoRoot, "data", "rules", "play");
}

/// <summary>
/// Shares one <see cref="PlayFixture"/> across the play engine's test classes, so the six files
/// are parsed once per run. Not named *Collection: CA1711 reserves that suffix.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedPlayRules : ICollectionFixture<PlayFixture>
{
    public const string Name = "play rules";
}

/// <summary>
/// The two-valued case three tests in two classes sort ledger lines by: a run that says
/// <c>not yet implemented</c>, and one that does not.
///
/// <para><b>A substring test whose subject can only ever answer one way is an instrument nobody has
/// checked</b> — it passes against a classifier that has stopped recognising the phrase at all,
/// which is the shape of guard fault this repository has shipped four times. So the pair lives in
/// one place, is driven in one place
/// (<c>PlayEngineStepTests.TheThreeClassifierTestsRestOnAClauseThisEngineStillDeclines</c>), and
/// the three tests that need a control call it rather than each building their own.</para>
///
/// <para><b>It used to be <c>Encounter.SwitchesNotYetApplied</c>, announced on page one</b>, and
/// before that p.85's first purchase naming one of the four Chapter 4 spends that charged the
/// buyer's own pool. Both are gone: every purchase resolves, and p.80's Gear Limit is applied now
/// that Chapter 6 is extracted, so that set is empty. <b>What is left is a clause inside a rule
/// that is otherwise applied</b> — <c>minions_attacking</c>'s
/// <c>the_group_bonus_does_not_apply_to</c>, whose own <c>ambiguity</c> says the page offers no
/// mechanism for it — and it is a better control than either, because it is written by
/// <c>Step</c> resolving an attack rather than by <c>Begin</c> reading a flag.</para>
/// </summary>
public static class NotYetImplemented
{
    /// <summary>The entry whose unapplied clause is the phrase's one remaining source.</summary>
    public const string Entry = "minion_group_attack_table";

    /// <summary>The phrase all three tests sort on.</summary>
    public const string Phrase = "not yet implemented";

    /// <summary>
    /// A group of Minions attacking, which collects p.77's size bonus and says on the ledger that
    /// the clause narrowing it is not applied.
    /// </summary>
    public static IReadOnlyList<LedgerLine> ARunThatSaysIt(PlayRulesRepository play)
    {
        ArgumentNullException.ThrowIfNull(play);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["might"] = 6, ["toughness"] = 4
        };

        var encounter = new Encounter(play, new SeededDice(21));

        var state = encounter.Begin([
            Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 2, traits, ["toughness"]),
            Combatant.Minions("mob", "the Minions", threat: 4, groupSize: 4, "threat")
        ]);

        // The Minions act last, so the Hero's turn is stepped past first.
        state = encounter.Step(state, new EndTurn("hero")).State;

        return encounter.Step(state, new Attack("mob", "hero", "threat")).Added;
    }

    /// <summary>The same fight with one character attacking, which does not say it.</summary>
    public static IReadOnlyList<LedgerLine> ARunThatDoesNot(PlayRulesRepository play)
    {
        ArgumentNullException.ThrowIfNull(play);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["might"] = 6, ["toughness"] = 4
        };

        var encounter = new Encounter(play, new SeededDice(21));

        var state = encounter.Begin([
            Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 2, traits, ["toughness"]),
            Combatant.Villain("villain", "the Villain", edge: 5, health: 10, traits, ["toughness"])
        ]);

        return encounter.Step(state, new Attack("hero", "villain", "might")).Added;
    }
}
