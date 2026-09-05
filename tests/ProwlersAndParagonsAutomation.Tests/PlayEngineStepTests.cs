using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>What <c>Encounter.Step</c> does with the intents and settings no printed example exercises.</b>
///
/// <para>The book works eight examples through and <see cref="PlayWorkedExamples"/> replays every
/// one of them; those are the authority wherever the authors did the arithmetic. What is here is
/// everything they did not: the flags an <see cref="Attack"/> carries, the table settings, and the
/// refusals. Each one is written against the printed rule it applies and names the entry, because a
/// fixture written from the same reading of the page as the code it checks is a third transcription
/// carrying the same defect.</para>
///
/// <para><b>Every fixture here carries a positive control before its outcome</b>, on the same
/// reasoning <c>CLAUDE.md</c> gives: the commonest way a check in this repository has been wrong is
/// a feature that did not run being read as a feature that worked. Where dice are scripted,
/// <see cref="ScriptedDice.Remaining"/> is asserted zero, which catches an engine rolling more or
/// fewer times than the rule says.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlayEngineStepTests
{
    private readonly PlayRulesRepository _play;

    public PlayEngineStepTests(PlayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _play = fixture.Play;
    }

    // ── Sides ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Two Heroes on opposite sides fight, and the fight ends.</b>
    ///
    /// <para>p.73 puts Heroes on one rung of the tie-break ladder and says characters the ladder
    /// cannot separate act simultaneously — so the page contemplates Heroes on both sides of a
    /// fight. An engine that partitioned on <see cref="CombatantKind"/> made that fight unendable:
    /// every combatant was a Hero, so "the Heroes are all down" was the only ending available and it
    /// takes both of them. <see cref="Combatant.Side"/> is what the engine reads instead.</para>
    /// </summary>
    [Fact]
    public void AFightBetweenTwoHeroesEndsWhenOneSideIsDown()
    {
        var encounter = new Encounter(_play, new SeededDice(11));

        var north = Combatant.Hero(
            "north", "the Hero of the North", edge: 9, health: 8, resolve: 2,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 10, ["toughness"] = 4, ["agility"] = 3
            },
            ["toughness", "agility"], side: "north");

        var south = Combatant.Hero(
            "south", "the Hero of the South", edge: 9, health: 8, resolve: 2,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 10, ["toughness"] = 4, ["agility"] = 3
            },
            ["toughness", "agility"], side: "south");

        var state = encounter.Begin([north, south]);

        // p.73: equal Edge and the same rung of the ladder is the case the page calls simultaneous.
        // A stepped engine has to pick, and picks the id — the guide records that as a reading.
        Assert.Equal(north.Edge, south.Edge);
        Assert.Equal(north.Kind, south.Kind);
        Assert.Equal(["north", "south"], state.TurnOrder);

        var final = encounter.RunToEnd(state, new AttackTheWeakest(), maxPages: 40);

        // The control: they actually fought. A fight neither of them could reach would end at the
        // page limit with both on full Health and satisfy "Over" perfectly.
        Assert.Contains(final.Ledger.Lines, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));
        Assert.DoesNotContain(final.Ledger.Lines, l =>
            l.Text.Contains("reached its limit", StringComparison.Ordinal));

        Assert.True(final.Over);

        var standing = final.Combatants.Values.Where(c => !c.Defeated(encounter.DefeatFloor)).ToList();
        Assert.Single(standing);
    }

    /// <summary>
    /// <b>A Villain's Minions stand with their Villain against a Foe on the other side.</b>
    ///
    /// <para>Every combatant here is an NPC, so an engine that read Heroes-against-everybody-else
    /// saw one side and no enemies at all: the policy held its action and the fight never ended.
    /// The sides are the caller's, and the Foe is on the other one.</para>
    /// </summary>
    [Fact]
    public void AVillainsMinionsFightAFoeOnTheOtherSide()
    {
        var encounter = new Encounter(_play, new SeededDice(4));

        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 10, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 10, ["toughness"] = 6, ["agility"] = 4
            },
            ["toughness", "agility"], side: "the syndicate");

        var minions = Combatant.Minions(
            "minions", "the Villain's Minions", threat: 5, groupSize: 4, "threat",
            side: "the syndicate");

        var foe = Combatant.Foe(
            "foe", "a turncoat Foe", edge: 8, health: 6,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 8, ["toughness"] = 4, ["agility"] = 3
            },
            ["toughness", "agility"], side: "the turncoat");

        var policy = new AttackTheWeakest();
        var state = encounter.Begin([villain, minions, foe]);

        // The control, before any dice: the policy sees across the line and not across the kind.
        var villainsChoice = Assert.IsType<Attack>(policy.Choose(state, state["villain"]));
        Assert.Equal("foe", villainsChoice.Target);

        var foesChoice = Assert.IsType<Attack>(policy.Choose(state, state["foe"]));
        Assert.Contains(foesChoice.Target, (string[])["villain", "minions"]);

        var final = encounter.RunToEnd(state, policy, maxPages: 40);

        Assert.True(final.Over);
        Assert.DoesNotContain(final.Ledger.Lines, l =>
            l.Text.Contains("reached its limit", StringComparison.Ordinal));

        var sidesStanding = final.Combatants.Values
            .Where(c => !c.Defeated(encounter.DefeatFloor))
            .Select(c => c.Side)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Single(sidesStanding);
    }

    /// <summary>
    /// <b>The side is the caller's and the kind is not consulted for it.</b> A Villain built onto
    /// the Heroes' side is on the Heroes' side, and a Hero built onto the opposition's is not —
    /// which is the whole of what makes the two fixtures above expressible.
    /// </summary>
    [Fact]
    public void TheCallerSaysWhichSideACombatantIsOn()
    {
        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 5 };

        var turned = Combatant.Villain("v", "a reformed Villain", 5, 10, traits, ["toughness"],
            side: Combatant.HeroSide);
        var fallen = Combatant.Hero("h", "a fallen Hero", 5, 10, 2, traits, ["toughness"],
            side: Combatant.OpposingSide);

        Assert.Equal(CombatantKind.Villain, turned.Kind);
        Assert.Equal(Combatant.HeroSide, turned.Side);

        Assert.Equal(CombatantKind.Hero, fallen.Kind);
        Assert.Equal(Combatant.OpposingSide, fallen.Side);

        // And the default is the book's own arrangement, so nothing else in the suite had to change.
        Assert.Equal(Combatant.HeroSide, Combatant.Hero("d", "d", 5, 10, 0, traits, ["toughness"]).Side);
        Assert.Equal(Combatant.OpposingSide, Combatant.Villain("e", "e", 5, 10, traits, ["toughness"]).Side);
    }
}
