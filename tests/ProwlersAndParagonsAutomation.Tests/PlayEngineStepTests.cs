using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;
using ProwlersAndParagonsAutomation.Play.Rules.Models;

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

        var final = encounter.RunToEnd(state, new AttackTheWeakest(_play), maxPages: 40);

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

        var policy = new AttackTheWeakest(_play);
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

    // ── The Attack and Defense table ─────────────────────────────────────────

    /// <summary>
    /// <b>The row of p.75's table decides what may answer an attack.</b>
    ///
    /// <para>One target with the same four defences meets all five rows, and each row's answer is
    /// read off the table rather than restated here: the fixture asks the entry for the row's
    /// printed defence traits and requires the engine to have picked from exactly those. So a
    /// corrected table moves this with it, and a test written from a second reading of the page
    /// cannot agree with a code path written from the same second reading.</para>
    ///
    /// <para>The case that matters most is the mental row: it does not list Toughness at all, so a
    /// target with 12d Toughness and 6d Willpower answers a Mind Control with the 6d — and the
    /// engine used to hand them the 12d, because it offered every defence to every attack.</para>
    /// </summary>
    [Theory]
    [InlineData(AttackType.Unarmed, "Unarmed")]
    [InlineData(AttackType.MeleeWeapon, "Melee Weapon")]
    [InlineData(AttackType.RangedWeapon, "Ranged Weapon")]
    [InlineData(AttackType.PhysicalPower, "Physical Power")]
    [InlineData(AttackType.MentalPower, "Mental Power")]
    public void TheRowOfThePrintedTableDecidesWhatAnswersAnAttack(AttackType type, string printedRow)
    {
        var row = _play.GetCombat("attack_and_defense_table").AttackDefenseTable!
            .Single(r => string.Equals(r.Type, printedRow, StringComparison.Ordinal));

        // What the row offers, as trait ids: "1/2 Toughness" is a toughness and "Power" is whatever
        // the table never names, which for this target is force_field.
        var offered = row.DefenseTraits
            .Select(t => string.Equals(t, "Power", StringComparison.Ordinal)
                ? "force_field"
                : t.Replace("1/2 ", "", StringComparison.Ordinal).ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var target = Combatant.Hero("target", "the target", edge: 5, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["toughness"] = 12, ["willpower"] = 6, ["agility"] = 4, ["force_field"] = 2, ["might"] = 4
            },
            ["toughness", "willpower", "agility", "force_field"]);

        var attacker = Combatant.Villain("attacker", "the attacker", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(8));
        var state = encounter.Begin([attacker, target]);

        var added = encounter
            .Step(state, new Attack("attacker", "target", "might", DamageKind.Subdual, type))
            .Added;

        // The control: the engine says which row it read, and it is the row asked for.
        Assert.Contains(added, l =>
            string.Equals(l.Rule, "attack_and_defense_table", StringComparison.Ordinal)
            && l.Text.Contains($"a {printedRow} attack is answered with", StringComparison.Ordinal));

        var defence = added.Single(l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal)).Text;

        var used = defence.Split("defends with ", StringSplitOptions.None)[1].Split(' ')[0];

        Assert.True(offered.Contains(used),
            $"a {printedRow} attack was answered with {used}, and the table offers "
            + $"{string.Join(", ", row.DefenseTraits)}");

        // Toughness is on four of the five rows and off the fifth, so the mental row is the one that
        // proves the list is being narrowed rather than merely being wide enough.
        if (type == AttackType.MentalPower) Assert.Equal("willpower", used);
    }

    /// <summary>
    /// <b>The table halves a Toughness where its row prints <c>1/2 Toughness</c>, and once.</b>
    ///
    /// <para>Two printed rules on p.75 could halve the same figure — the table's row and
    /// <c>lethal_and_subdual</c>'s lethal clause — and they agree wherever the book's own defaults
    /// hold. Where a caller puts them at odds this halves if either says so and never twice, which
    /// is the reading the guide's table records: a subdual blow from a melee weapon (p.75 names
    /// light clubbing weapons as a subdual source) meets half a Toughness because the row says so,
    /// not a quarter.</para>
    /// </summary>
    [Fact]
    public void AToughnessIsHalvedOnceHoweverManyRulesSaySo()
    {
        var target = Combatant.Hero("target", "the target", edge: 5, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 12, ["might"] = 4 },
            ["toughness"]);

        var attacker = Combatant.Villain("attacker", "the attacker", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);

        // The control: the unarmed row does not halve, and a subdual blow does not either, so 12d
        // stands — which is what makes the halvings below visible.
        Assert.Contains("defends with toughness 12d",
            Defence(AttackType.Unarmed, DamageKind.Subdual), StringComparison.Ordinal);

        // The row halves it: a light clubbing weapon is p.75's own subdual example.
        Assert.Contains("defends with toughness 6d",
            Defence(AttackType.MeleeWeapon, DamageKind.Subdual), StringComparison.Ordinal);

        // The damage type halves it, on the row that does not.
        Assert.Contains("defends with toughness 6d",
            Defence(AttackType.Unarmed, DamageKind.Lethal), StringComparison.Ordinal);

        // Both say so, and it is still one halving.
        Assert.Contains("defends with toughness 6d",
            Defence(AttackType.MeleeWeapon, DamageKind.Lethal), StringComparison.Ordinal);

        string Defence(AttackType type, DamageKind damage)
        {
            var encounter = new Encounter(_play, new SeededDice(8));
            var state = encounter.Begin([attacker, target]);

            return encounter
                .Step(state, new Attack("attacker", "target", "might", damage, type))
                .Added
                .Single(l => string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                             && l.Text.Contains("defends with", StringComparison.Ordinal))
                .Text;
        }
    }

    /// <summary>
    /// <b><c>defenses_used_per_attack</c> and <c>defense_chosen</c> are read, not assumed.</b>
    ///
    /// <para>Both were modelled and neither was ever consulted, which makes them decoration: the
    /// engine rolled one greatest defence because somebody wrote it that way, and would have gone on
    /// doing so against an entry that had been corrected to say something else. It throws now, and
    /// the throw names the entry — checked with a twin, because reading the code cannot tell you
    /// whether a field is consulted.</para>
    /// </summary>
    [Fact]
    public void TheDefenceCountAndTheChoiceRuleAreReadFromTheEntry()
    {
        var attacker = Combatant.Villain("attacker", "the attacker", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);
        var target = Combatant.Hero("target", "the target", edge: 5, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 8, ["might"] = 4 },
            ["toughness"]);

        // The control: the shipped entry says what the engine assumes, so an attack resolves.
        var defenses = _play.GetCombat("active_and_passive_defenses").Defenses!;
        Assert.Equal(1, defenses.DefensesUsedPerAttack);
        Assert.Contains("greatest rank", defenses.DefenseChosen, StringComparison.Ordinal);

        foreach (var (find, replace) in new[]
                 {
                     ("\"defenses_used_per_attack\": 1", "\"defenses_used_per_attack\": 2"),
                     ("\"defense_chosen\": \"normally the one with the greatest rank\"",
                      "\"defense_chosen\": \"whichever the defender likes\"")
                 })
        {
            var reworded = SubstitutedPlayRules.With(PlayRulesRepository.CombatFile, find, replace);
            var encounter = new Encounter(reworded, new SeededDice(8));
            var state = encounter.Begin([attacker, target]);

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                encounter.Step(state, new Attack("attacker", "target", "might")));

            Assert.Contains("active_and_passive_defenses", thrown.Message, StringComparison.Ordinal);
        }
    }

    // ── Area attacks, going all-out, charging ────────────────────────────────

    /// <summary>
    /// <b>An active defence against an area attack is halved, and the ledger says which of p.78's
    /// two options was taken.</b>
    ///
    /// <para><c>area_attacks</c>: "an active defense must either halve its rank or forfeit the next
    /// turn to act". Neither half was applied, so dodging a blast cost nothing at all and every
    /// balance figure for an area attack was too low. The engine halves and names the option it did
    /// not take, because a rule where the engine had to choose and chose silently is a reading
    /// nobody can argue with.</para>
    ///
    /// <para>The control is the same dodger against the same attack without the area flag: 8d, and
    /// 4d with it. A passive defence is untouched either way, which the second half checks — the
    /// page puts the price on dodging.</para>
    /// </summary>
    [Fact]
    public void AnActiveDefenceAgainstAnAreaAttackIsHalved()
    {
        var attacker = Combatant.Villain("attacker", "the attacker", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["blast"] = 8 }, ["toughness"]);

        var dodger = Combatant.Hero("dodger", "the dodger", edge: 5, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["agility"] = 8, ["might"] = 3 },
            ["agility"]);

        var bracer = Combatant.Hero("bracer", "the bracer", edge: 4, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["force_field"] = 8, ["might"] = 3 },
            ["force_field"]);

        // The control: without the area flag the dodger answers with the whole of their 8d.
        Assert.Contains("defends with agility 8d", Defence("dodger", area: false), StringComparison.Ordinal);

        var dodged = Defence("dodger", area: true);
        Assert.Contains("defends with agility 4d", dodged, StringComparison.Ordinal);

        // p.78 prices dodging and not bracing, so the passive defence is the same either way.
        Assert.Contains("defends with force_field 8d", Defence("bracer", area: false), StringComparison.Ordinal);
        Assert.Contains("defends with force_field 8d", Defence("bracer", area: true), StringComparison.Ordinal);

        string Defence(string target, bool area)
        {
            var encounter = new Encounter(_play, new SeededDice(9));
            var state = encounter.Begin([attacker, dodger, bracer]);

            var added = encounter
                .Step(state, new Attack("attacker", target, "blast", DamageKind.Lethal,
                    AttackType.PhysicalPower, Area: area))
                .Added;

            // The engine says which of the two printed options it took, and only when one was owed.
            var entry = _play.GetCombat("area_attacks");
            var choice = added.Where(l => string.Equals(l.Rule, "area_attacks", StringComparison.Ordinal)).ToList();

            if (area && string.Equals(target, "dodger", StringComparison.Ordinal))
            {
                var line = Assert.Single(choice);

                foreach (var option in entry.AreaAttack!.AnActiveDefenseMustEither)
                    Assert.Contains(option, line.Text, StringComparison.Ordinal);
            }
            else
            {
                Assert.Empty(choice);
            }

            return added.Single(l =>
                string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                && l.Text.Contains("defends with", StringComparison.Ordinal)).Text;
        }
    }

    /// <summary>
    /// <b>Going all-out halves every defence, except against an opponent who could never get through
    /// the passive one anyway.</b>
    ///
    /// <para>p.78 writes one sentence of guard onto the rule — <c>opponents who could not penetrate
    /// your passive defense: "still cannot"</c> — and it was modelled and never read, so an Armor of
    /// 15 became a 7 and an attacker who could never have hurt its wearer started hurting them. That
    /// is the one outcome the clause exists to forbid.</para>
    ///
    /// <para>The fixture drives both sides of the guard against the same all-out attacker: 10d
    /// against 15d Armor still meets 15d, and 16d against the same 15d meets the halved 8d. Without
    /// the second half the first would be satisfied by an engine that had simply stopped halving
    /// passives at all.</para>
    /// </summary>
    [Theory]
    [InlineData(10, 15, false)]
    [InlineData(16, 8, true)]
    public void GoingAllOutDoesNotOpenAPassiveDefenceToSomebodyWhoCouldNeverGetThroughIt(
        int attackRank, int expectedPool, bool penetrates)
    {
        // The armoured character goes all-out on their own turn, which halves every defence of
        // theirs until after their next turn — and then takes a blow.
        var armoured = Combatant.Hero("armoured", "the armoured Hero", edge: 9, health: 30, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["armor"] = 15 },
            ["armor"]);

        var attacker = Combatant.Villain("attacker", "the attacker", edge: 8, health: 30,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = attackRank }, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(12));
        var state = encounter.Begin([armoured, attacker]);

        var committed = encounter.Step(state, new Attack("armoured", "attacker", "might", AllOut: true));
        state = committed.State;

        // The control: the all-out attack really was taken, so there is a halving to be guarded
        // against. Without this the fixture would pass against an engine that ignored AllOut.
        Assert.Contains(committed.Added, l =>
            string.Equals(l.Rule, "going_all_out", StringComparison.Ordinal)
            && l.Text.Contains("every defence", StringComparison.Ordinal));
        Assert.True(state.DefencesHalved.ContainsKey("armoured"));

        state = encounter.Step(state, new EndTurn("armoured")).State;

        var struck = encounter.Step(state, new Attack("attacker", "armoured", "might"));

        Assert.Contains($"defends with armor {expectedPool}d",
            struck.Added.Single(l =>
                string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                && l.Text.Contains("defends with", StringComparison.Ordinal)).Text,
            StringComparison.Ordinal);

        var guard = _play.GetCombat("going_all_out").AllOutAttack!
            .OpponentsWhoCouldNotPenetrateYourPassiveDefense;

        var said = struck.Added.Any(l =>
            string.Equals(l.Rule, "going_all_out", StringComparison.Ordinal)
            && l.Text.Contains(guard, StringComparison.Ordinal));

        Assert.Equal(!penetrates, said);
    }

    /// <summary>
    /// <b>A charge against a braced target comes back on the charger.</b>
    ///
    /// <para>p.78: "if the target uses a passive defense, the charger makes their own passive
    /// defense roll against the attack to see whether the impact hurts them", less "the damage
    /// inflicted on the target". Both fields were modelled and neither was read, so a charge was two
    /// free dice against anybody who stood still and a balance run would have said charging is
    /// always worth it.</para>
    ///
    /// <para>The dice are scripted so the arithmetic is the fixture's rather than a seed's: the
    /// charger's 12d Might scores 8 against a braced 2d Toughness scoring 1, which is 7 net and 7
    /// damage on the target; the charger's own 4d Toughness then answers those 8 successes with 2,
    /// so the impact is 6 and 6 less 7 is nothing. Dropping the target's Toughness to a rank that
    /// takes less damage is what makes the subtraction visible, which the second half does.</para>
    /// </summary>
    [Fact]
    public void AChargeAgainstABracedTargetComesBackOnTheCharger()
    {
        var charger = Combatant.Villain("charger", "the charger", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 10, ["toughness"] = 4 },
            ["toughness"]);

        // A Toughness of 2 against a lethal charge is a 1d defence, and no Agility at all, so the
        // target has nothing but a passive defence — which is the case p.78 prices.
        var braced = Combatant.Hero("braced", "the braced Hero", edge: 5, health: 30, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2, ["might"] = 3 },
            ["toughness"]);

        // 12d for the charge (10 Might + p.78's two), then the target's 1d, then the charger's 4d.
        var dice = new ScriptedDice([.. FacesFor(12, 8), .. FacesFor(1, 0), .. FacesFor(4, 2)]);
        var encounter = new Encounter(_play, dice);

        var state = encounter.Begin([charger, braced]);
        var step = encounter.Step(state, new Attack("charger", "braced", "might", Charge: true));

        // The controls: the rolls are the ones the arithmetic below is about, and no others.
        Assert.Equal(8, step.State.LastAttack!.AttackSuccesses);
        Assert.Equal(0, step.State.LastAttack.DefenceSuccesses);
        Assert.Equal(0, dice.Remaining);

        var impact = Assert.Single(step.Added, l =>
            string.Equals(l.Rule, "charge_attacks", StringComparison.Ordinal)
            && l.Text.Contains("braced", StringComparison.Ordinal));

        Assert.Contains("toughness 4d", impact.Text, StringComparison.Ordinal);

        // 8 net on the target is 8 damage; the charger's own 6 net is 6, less the 8 they dealt, is
        // nothing at all — the charger walks away from a charge that landed hard.
        Assert.Equal(22, step.State["braced"].CurrentHealth);
        Assert.Equal(20, step.State["charger"].CurrentHealth);

        // And the other side of it: a target who soaks the blow leaves the impact behind. A
        // Toughness of 8 answers a lethal charge with 4d and scores 3, so 3 of the charger's 6 net
        // never land on the target — and those 3 are exactly what comes back.
        var stiff = Combatant.Hero("stiff", "the stiff Hero", edge: 4, health: 30, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 8, ["might"] = 3 },
            ["toughness"]);

        var soaked = new ScriptedDice([.. FacesFor(12, 6), .. FacesFor(4, 3), .. FacesFor(4, 0)]);
        var second = new Encounter(_play, soaked);

        var hard = second.Step(
            second.Begin([charger, stiff]), new Attack("charger", "stiff", "might", Charge: true));

        Assert.Equal(0, soaked.Remaining);
        Assert.Equal(27, hard.State["stiff"].CurrentHealth);     // 3 net, 3 damage
        Assert.Equal(17, hard.State["charger"].CurrentHealth);   // 6 impact less the 3 dealt
    }

    /// <summary>
    /// <b>A charge with a Trait p.78 does not allow is refused, not quietly given the two dice.</b>
    ///
    /// <para><c>charge_attacks.attack_traits</c> was modelled and never read, so a charge could be
    /// made with anything — a mental Power included — and collect the +2d and the halved active
    /// defences for it. Three of the entry's four clauses resolve to Trait ids; the fourth is prose,
    /// and the guide records how it is read.</para>
    /// </summary>
    [Fact]
    public void AChargeWithATraitThePageDoesNotAllowIsRefused()
    {
        var charger = Combatant.Villain("charger", "the charger", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 8, ["flight"] = 7, ["mind_control"] = 9, ["toughness"] = 4
            },
            ["toughness"]);

        var target = Combatant.Hero("target", "the target", edge: 5, health: 30, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4, ["might"] = 3 },
            ["toughness"]);

        // The controls: Might is the attack Trait of p.75's close-combat rows and Flight is a Travel
        // Power, so both charges are resolved rather than refused.
        foreach (var trait in (string[])["might", "flight"])
        {
            var allowed = Encounter(new Attack("charger", "target", trait, Charge: true));

            Assert.Contains(allowed, l =>
                string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                && l.Text.Contains("defends with", StringComparison.Ordinal));
        }

        var refused = Encounter(new Attack(
            "charger", "target", "mind_control", DamageKind.Psychic, AttackType.MentalPower,
            Charge: true));

        Assert.Contains(refused, l =>
            string.Equals(l.Rule, "charge_attacks", StringComparison.Ordinal)
            && l.Text.Contains("cannot charge with mind_control", StringComparison.Ordinal));

        // And nothing else happened: no roll, no bonus dice, no halved defences.
        Assert.DoesNotContain(refused, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));

        IReadOnlyList<LedgerLine> Encounter(Attack attack)
        {
            var encounter = new Encounter(_play, new SeededDice(13));
            return encounter.Step(encounter.Begin([charger, target]), attack).Added;
        }
    }

    // ── Spending Resolve after the roll ──────────────────────────────────────

    /// <summary>
    /// <b>A reroll picks up the dice that were bought, too.</b>
    ///
    /// <para>Ch.5 p.84 offers both purchases on "a challenge roll" and draws no line round the dice
    /// that were there first, so a Hero who buys two dice onto a 10d attack and then buys a reroll
    /// throws twelve. The engine kept <c>AttackPool</c> at the figure it was rolled with, so the
    /// reroll threw ten and the Hero silently lost what they had paid for.</para>
    ///
    /// <para>Scripted dice are what prove it rather than an assertion about a field: the fixture
    /// supplies exactly 10 + 2 + 12 faces and requires none to be left over, so an engine rerolling
    /// the wrong pool runs out or leaves faces behind either way.</para>
    /// </summary>
    [Fact]
    public void ARerollPicksUpTheDiceThatWereBoughtAsWell()
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 4,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 10, ["toughness"] = 4 },
            ["toughness"]);

        // No defence at all, so the only pools thrown are the attacker's and the fixture's arithmetic
        // is about nothing else.
        var target = Combatant.Villain("target", "the target", edge: 5, health: 40,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 3 }, []);

        var dice = new ScriptedDice([
            .. FacesFor(10, 2),   // the attack: 10d for 2
            .. FacesFor(1, 0),    // the target has no defence, and p.67's floor still throws one die
            .. FacesFor(2, 2),    // the two dice bought, for 2 more
            .. FacesFor(12, 9)    // the reroll, which has to be twelve dice
        ]);

        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, target]);

        state = encounter.Step(state, new Attack("hero", "target", "might")).State;

        // The controls: the roll on the table is the 10d the Hero has, and it scored what the script
        // says.
        Assert.Equal(10, state.LastAttack!.AttackPool);
        Assert.Equal(2, state.LastAttack.AttackSuccesses);

        var bought = encounter.Step(state, new SpendResolve("hero", ResolveSpend.ExtraDice, Points: 2));
        state = bought.State;

        Assert.Equal(12, state.LastAttack!.AttackPool);
        Assert.Equal(4, state.LastAttack.AttackSuccesses);
        Assert.Contains(bought.Added, l => l.Text.Contains("now 12d", StringComparison.Ordinal));

        var rerolled = encounter.Step(state, new SpendResolve("hero", ResolveSpend.Reroll));

        Assert.Contains(rerolled.Added, l =>
            l.Text.Contains("to reroll 12d", StringComparison.Ordinal));
        Assert.Equal(9, rerolled.State.LastAttack!.AttackSuccesses);

        // The other half of the control: the engine asked for exactly the dice the page describes.
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(1, rerolled.State["hero"].Resolve);
    }

    // ── Minions ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The Tough Minions line says how many were defeated, not how many could have been.</b>
    ///
    /// <para>Under that setting the engine printed the uncapped figure — "3 net successes defeat 3"
    /// — and then suppressed the line that applied the cap, so the ledger said three and the state
    /// said two. A run's number and its audit trail disagreed, and the audit trail's is the one a
    /// reader would have quoted.</para>
    ///
    /// <para>The rate is the one Tough Minions replaces the area rate with, read off the entry: the
    /// fixture asserts the shipped figures first, so a change to the data is a failing assertion here
    /// rather than a fixture that has quietly stopped being about the same rule.</para>
    /// </summary>
    [Fact]
    public void ToughMinionsReportsWhatItDefeatedAndNotWhatItCouldHave()
    {
        var tough = _play.GetGritty("gritty_tough_minions").ToughMinions!;

        // The controls: the setting's own figures, which the arithmetic below is built on.
        Assert.Equal(1, tough.AreaAttackMinionsPerNetSuccess);
        Assert.Equal(2, tough.AreaAttackRateItReplaces);
        Assert.Equal("down", tough.Rounding);

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["blast"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var minions = Combatant.Minions("minions", "the Minions", threat: 4, groupSize: 2, "threat");

        // Six successes against three is three net; under Tough Minions an area attack takes one
        // Minion each, so three could go down and only two are there.
        var dice = new ScriptedDice([.. FacesFor(8, 6), .. FacesFor(4, 3)]);

        var encounter = new Encounter(
            _play, dice, TableRules.Book with { ToughMinions = true });

        var state = encounter.Begin([hero, minions]);

        var step = encounter.Step(state, new Attack(
            "hero", "minions", "blast", DamageKind.Lethal, AttackType.PhysicalPower, Area: true));

        // The controls: the roll is the one the fixture is about, and no extra dice were asked for.
        Assert.Equal(6, step.State.LastAttack!.AttackSuccesses);
        Assert.Equal(3, step.State.LastAttack.DefenceSuccesses);
        Assert.Equal(0, dice.Remaining);

        var line = Assert.Single(step.Added, l =>
            string.Equals(l.Rule, "gritty_tough_minions", StringComparison.Ordinal));

        Assert.Contains("could defeat 3", line.Text, StringComparison.Ordinal);
        Assert.Contains("capped by the 2 actually there: 2 defeated", line.Text, StringComparison.Ordinal);

        // And the state agrees with the sentence, which is the whole of the complaint.
        Assert.Equal(0, step.State["minions"].GroupSize);
        Assert.EndsWith("2 defeated", line.Text, StringComparison.Ordinal);
    }

    // ── Fatal Damage ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A character taken below nothing by lethal damage bleeds out, a page at a time, until
    /// somebody stops it.</b>
    ///
    /// <para>p.79's Fatal Damage rule was documented as applied and was half-built: Health went
    /// negative and the killing line was announced, and <c>dying_begins_when_lethal_damage_reduces_you_to</c>,
    /// <c>dying_damage_per_page</c> and everything that stops the clock were modelled and never read.
    /// So a character bled out on paper and then lay at a fixed Health for the rest of the fight,
    /// which is the opposite of what the setting is for — and the guide said it was applied.</para>
    ///
    /// <para>Every figure is read off the entry rather than typed, so the fixture is about the
    /// shipped rule and not about a second reading of the page. The controls come first: the
    /// character really is past the threshold, and really is dying, before anything is asserted about
    /// what the clock does.</para>
    /// </summary>
    [Fact]
    public void LethalDamagePastTheThresholdBleedsOutAPageAtATimeUntilItIsStopped()
    {
        var fatal = _play.GetGritty("gritty_fatal_damage").FatalDamage!;

        // The controls on the data: the figures the arithmetic below is built on.
        Assert.Equal(-1, fatal.DyingBeginsWhenLethalDamageReducesYouTo);
        Assert.Equal(1, fatal.DyingDamagePerPage);
        Assert.Equal(1, fatal.CostResolveToStabiliseImmediately);

        var table = TableRules.Book with { FatalDamage = true };

        // A Health of 4, on 1, taking 3 lethal damage: -2, which is past the threshold.
        var victim = Combatant.Hero("victim", "the victim", edge: 4, health: 4, resolve: 2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2, ["might"] = 3 },
            ["toughness"]).WithHealth(1);

        var killer = Combatant.Villain("killer", "the killer", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 10 }, ["toughness"]);

        // 4 successes against the victim's 1d Toughness (2, halved for a lethal blow) scoring 1.
        var dice = new ScriptedDice([.. FacesFor(10, 4), .. FacesFor(1, 1)]);
        var encounter = new Encounter(_play, dice, table);

        var state = encounter.Begin([killer, victim]);
        var struck = encounter.Step(state, new Attack(
            "killer", "victim", "might", DamageKind.Lethal, AttackType.MeleeWeapon));

        state = struck.State;

        // The controls: the roll is the one this is about, and the clock has actually started.
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(-2, state["victim"].CurrentHealth);
        Assert.True(state["victim"].Dying);
        Assert.False(state["victim"].Stable);

        Assert.Contains(struck.Added, l =>
            string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
            && l.Text.Contains("dying begins at -1", StringComparison.Ordinal));

        // A page later they are one worse, and not dead: -4 is the killing line for a Health of 4.
        state = encounter.Step(state, new EndTurn("killer")).State;
        state = encounter.Step(state, new EndTurn("victim")).State;

        var turned = encounter.Step(state, new EndPage(""));
        state = turned.State;

        Assert.Equal(-3, state["victim"].CurrentHealth);
        Assert.True(state["victim"].Dying);

        Assert.Contains(turned.Added, l =>
            string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
            && l.Text.Contains("bleeding out", StringComparison.Ordinal));

        // One Resolve stops it, with no roll — cost_resolve_to_stabilise_immediately.
        var steadied = encounter.Step(state, new SpendResolve("victim", ResolveSpend.Stabilise));
        state = steadied.State;

        Assert.False(state["victim"].Dying);
        Assert.True(state["victim"].Stable);
        Assert.Equal(1, state["victim"].Resolve);
        Assert.Equal(-3, state["victim"].CurrentHealth);

        // And the clock has stopped: another page takes nothing more off.
        state = encounter.Step(state, new EndTurn("killer")).State;
        state = encounter.Step(state, new EndTurn("victim")).State;
        state = encounter.Step(state, new EndPage("")).State;

        Assert.Equal(-3, state["victim"].CurrentHealth);
    }

    /// <summary>
    /// <b>The clock runs to the killing line and stops there.</b> p.79 puts death at the negative of
    /// full Health, and this drives the same character down to it a page at a time — which is what
    /// separates a clock that ticks from a clock that ticks forever.
    /// </summary>
    [Fact]
    public void BleedingOutReachesTheNegativeOfFullHealthAndStops()
    {
        var table = TableRules.Book with { FatalDamage = true };

        var victim = Combatant.Hero("victim", "the victim", edge: 4, health: 4, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2, ["might"] = 3 },
            ["toughness"]).WithHealth(-2).Bleeding(dying: true);

        var watcher = Combatant.Villain("watcher", "the watcher", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(17), table);
        var state = encounter.Begin([watcher, victim]);

        // The control: this fixture starts with the clock already running.
        Assert.True(state["victim"].Dying);

        for (var page = 0; page < 3; page++)
        {
            state = encounter.Step(state, new EndTurn("watcher")).State;
            state = encounter.Step(state, new EndTurn("victim")).State;
            state = encounter.Step(state, new EndPage("")).State;
        }

        Assert.Equal(-4, state["victim"].CurrentHealth);
        Assert.Equal(-state["victim"].FullHealth, state["victim"].CurrentHealth);

        // Dead rather than dying: the clock has nothing left to run.
        Assert.False(state["victim"].Dying);
        Assert.Contains(state.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
            && l.Text.Contains("which reaches -4", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Nothing brings a bleeding character back to their feet until the clock has stopped</b> —
    /// <c>instant_recovery_requires_being_stable</c>, which was modelled, unread, and unreadable while
    /// the purchase it governs was on the unimplemented list.
    ///
    /// <para>The roll is p.79's own: the Trait <c>stabilise_roll</c> names, at the threshold beside
    /// it. Both sides of it are driven, because a fixture that only watched the roll succeed would
    /// pass against an engine that stabilised everybody.</para>
    /// </summary>
    [Fact]
    public void InstantRecoveryWaitsForTheClockToStop()
    {
        var fatal = _play.GetGritty("gritty_fatal_damage").FatalDamage!;

        // The controls on the data.
        Assert.True(fatal.InstantRecoveryRequiresBeingStable);
        Assert.Equal("Medicine", fatal.StabiliseRoll);
        Assert.Equal(2, fatal.StabiliseThreshold);

        var table = TableRules.Book with { FatalDamage = true };

        var victim = Combatant.Hero("victim", "the victim", edge: 9, health: 6, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["medicine"] = 6, ["toughness"] = 3 },
            ["toughness"]).WithHealth(-2).Bleeding(dying: true);

        var watcher = Combatant.Villain("watcher", "the watcher", edge: 4, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);

        // A Medicine roll that falls short, then one that clears the threshold.
        var dice = new ScriptedDice([.. FacesFor(6, 1), .. FacesFor(6, 2)]);
        var encounter = new Encounter(_play, dice, table);

        var state = encounter.Begin([victim, watcher]);

        // While the clock runs, the purchase is refused and cites the rule that refuses it.
        var tooSoon = encounter.Step(state, new SpendResolve("victim", ResolveSpend.InstantRecovery));

        Assert.Contains(tooSoon.Added, l =>
            string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
            && l.Text.Contains("still bleeding out", StringComparison.Ordinal));
        Assert.Equal(3, tooSoon.State["victim"].Resolve);
        Assert.Equal(-2, tooSoon.State["victim"].CurrentHealth);

        // A roll of 1 against a threshold of 2 does not steady them.
        state = encounter.Step(state, new Stabilise("victim", "victim")).State;
        Assert.True(state["victim"].Dying);

        // A roll of 2 does.
        var steadied = encounter.Step(state, new Stabilise("victim", "victim"));
        state = steadied.State;

        Assert.Equal(0, dice.Remaining);
        Assert.False(state["victim"].Dying);
        Assert.Contains(steadied.Added, l =>
            l.Text.Contains("rolls Medicine 6d for 2 against a Hard threshold of 2", StringComparison.Ordinal));

        // And now the purchase works.
        var recovered = encounter.Step(state, new SpendResolve("victim", ResolveSpend.InstantRecovery));

        Assert.Equal(
            _play.GetCombat("instant_recovery").InstantRecovery!.AfterADamagingDefeatRestoresHealth,
            recovered.State["victim"].CurrentHealth);
        Assert.Equal(2, recovered.State["victim"].Resolve);
    }

    /// <summary>
    /// <b>The Fatal Damage rescue is refused when there is nothing to rescue, and refused rather
    /// than thrown when there is nothing to pay with.</b>
    ///
    /// <para>Two failures in one purchase. A spend with no points behind it reached
    /// <c>Combatant.Spending</c> and threw an exception out of <c>Step</c>, which is not a refusal —
    /// an intent the rules do not allow belongs on the ledger, and a run that dies on one is a run
    /// with no verdict at all. And the arithmetic <em>sets</em> a Health rather than reducing one, so
    /// a character nowhere near the line was not rescued: they were dropped to one point above a
    /// threshold they were comfortably above already. A Hero on 6 of 6 Health could buy themselves
    /// down to −5.</para>
    /// </summary>
    [Fact]
    public void TheFatalDamageRescueIsRefusedWhenThereIsNothingToBuyBack()
    {
        var table = TableRules.Book with { FatalDamage = true };

        var encounter = new Encounter(_play, new SeededDice(18), table);

        var healthy = Combatant.Hero("healthy", "the healthy Hero", edge: 9, health: 6, resolve: 2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 3, ["might"] = 4 },
            ["toughness"]);

        var broke = Combatant.Hero("broke", "the broke Hero", edge: 8, health: 6, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 3, ["might"] = 4 },
            ["toughness"]).WithHealth(-6);

        var state = encounter.Begin([healthy, broke]);

        // Nowhere near the threshold: refused, and their Health is where it was.
        var untouched = encounter.Step(state, new SpendResolve("healthy", ResolveSpend.AvoidFatalDamage));

        Assert.Contains(untouched.Added, l =>
            string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
            && l.Text.Contains("no blow to buy back", StringComparison.Ordinal));

        Assert.Equal(6, untouched.State["healthy"].CurrentHealth);
        Assert.Equal(2, untouched.State["healthy"].Resolve);

        // At the threshold with nothing to spend: a refusal, not an exception out of Step.
        var penniless = encounter.Step(state, new SpendResolve("broke", ResolveSpend.AvoidFatalDamage));

        Assert.Contains(penniless.Added, l =>
            string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
            && l.Text.Contains("has 0 Resolve", StringComparison.Ordinal));

        Assert.Equal(-6, penniless.State["broke"].CurrentHealth);

        // The control: with a point and a blow to buy back, the purchase still works — so the two
        // refusals are about the cases they name and not about the rescue being switched off. This
        // is p.79's own arithmetic, and PlayWorkedExamples holds it against the printed example.
        var rescued = encounter.Step(
            state.With(state["broke"].WithHealth(-6)),
            new SpendResolve("healthy", ResolveSpend.AvoidFatalDamage));

        Assert.Contains(rescued.Added, l => l.Text.Contains("no blow to buy back", StringComparison.Ordinal));

        var dying = encounter.Step(
            state.With(state["healthy"].WithHealth(-6)),
            new SpendResolve("healthy", ResolveSpend.AvoidFatalDamage));

        Assert.Equal(-5, dying.State["healthy"].CurrentHealth);
        Assert.Equal(1, dying.State["healthy"].Resolve);
    }

    // ── The policy ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The Traits the policy will attack with are derived from p.75's table and the combatant's
    /// own Powers, not from four ids typed into the policy.</b>
    ///
    /// <para>The old list was <c>blast</c>, <c>strike</c>, <c>might</c>, <c>agility</c> — two of them
    /// Powers nobody had said the combatant had — so a character built out of Energy Blast held their
    /// action for the whole fight while holding an obvious weapon, and every balance figure measured
    /// on them was a figure about a character who never attacked.</para>
    ///
    /// <para>The expected list is assembled from the entry rather than restated, so a corrected table
    /// moves this fixture with it.</para>
    ///
    /// <para><b>And the correction of that defect had overshot the other way, which is the second
    /// half of this fixture.</b> "Every Trait the table does not name" is the complement of five
    /// rows taken over a dictionary that holds the Talents as well, so <c>academics</c> was an
    /// attack form — visible in a real report of the Pinnacle City Heroes, two of whom swung it.
    /// The exotic Hero here carries a Talent at a rank <em>above</em> their Ability's, so a
    /// derivation that let one through would both list it and choose it.</para>
    /// </summary>
    [Fact]
    public void ThePolicyAttacksWithWhatTheTableAndTheCharacterOffer()
    {
        var policy = new AttackTheWeakest(_play);

        var rows = _play.GetCombat("attack_and_defense_table").AttackDefenseTable!;

        var attacking = rows
            .Select(r => r.AttackTrait)
            .Where(t => !string.Equals(t, "Power", StringComparison.Ordinal))
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The control: the table really does name Abilities as attacking Traits, so the derivation
        // below has something to derive.
        Assert.NotEmpty(attacking);
        Assert.Contains("might", attacking, StringComparer.Ordinal);

        var exotic = Combatant.Hero("exotic", "the exotic Hero", edge: 7, health: 10, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 3, ["agility"] = 2, ["toughness"] = 4,
                ["energy_blast"] = 12,

                // A Talent, at a rank above every Ability on this sheet — so a derivation that let
                // one through would not merely list it, it would pick it.
                ["academics"] = 9
            },
            ["toughness"],
            attackPowers: new HashSet<string>(StringComparer.Ordinal) { "energy_blast" });

        var available = policy.TraitsAvailableTo(exotic);

        // The Power whose Ch.2 entry is an attack is one of theirs, and the Abilities the table
        // names are there too.
        Assert.Contains("energy_blast", available, StringComparer.Ordinal);
        foreach (var trait in attacking) Assert.Contains(trait, available, StringComparer.Ordinal);

        // The Traits the table names only as defences are not attacking Traits.
        Assert.DoesNotContain("toughness", available, StringComparer.Ordinal);

        // And neither is a Talent, which is the half that shipped: Academics is not an attack, and
        // "every Trait the table does not name" said it was.
        Assert.DoesNotContain("academics", available, StringComparer.Ordinal);

        // And the choice follows the rank: the 12d Power, not the 3d Ability.
        var villain = Combatant.Villain("villain", "the Villain", edge: 5, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 4 }, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(19));
        var state = encounter.Begin([exotic, villain]);

        var chosen = Assert.IsType<Attack>(policy.Choose(state, state["exotic"]));
        Assert.Equal("energy_blast", chosen.TraitId);

        // The old hard-coded list held this character's action for the whole fight, which is what
        // makes the derivation worth having rather than tidier.
        var typed = new AttackTheWeakest(_play) { AttackTraits = ["blast", "strike", "might", "agility"] };
        Assert.Equal("might", Assert.IsType<Attack>(typed.Choose(state, state["exotic"])).TraitId);
    }

    /// <summary>
    /// <b>An attack form is an Ability p.75's table names or a Power whose Ch.2 entry is an attack,
    /// driven end to end off a real sheet.</b>
    ///
    /// <para><b>This is the defect that reached a published measurement.</b> The derivation read
    /// "p.75's named attacking Traits plus every Trait the table does not name", and
    /// <c>Combatant.TraitRanks</c> holds the six Abilities, the twelve Talents and every Power on
    /// one dictionary — so the complement of a five-row table was most of a character sheet. A real
    /// report of the four Low Level Pinnacle City Heroes against Schism has two of them attacking
    /// with <c>academics</c>, one with <c>covert</c> and the Villain with <c>armor</c>: a Talent is
    /// not an attack and a passive defence is not one either.</para>
    ///
    /// <para><b>Every fact this turns on is read out of the character rules before it is used</b>,
    /// so a corrected Chapter 2 moves the fixture with it: the Blast's category, the Armor's, and
    /// that Academics is one of the Talents the factory puts on a combatant at all. The Talent is
    /// given the greatest rank on the sheet, so a derivation that let one through would not merely
    /// list it — <see cref="AttackOptions.BestFor"/> would choose it.</para>
    /// </summary>
    [Fact]
    public void AnAttackFormIsAnAbilityTheTableNamesOrAPowerChapterTwoCallsAnAttack()
    {
        var rules = new RulesFixture();

        // The controls, off the character rules: the two Powers are the two categories this turns
        // on, and Academics is really a Talent the factory carries onto a combatant.
        Assert.Equal("Attack", rules.Rules.GetPower("blast")!.Category, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual("Attack", rules.Rules.GetPower("armor")!.Category, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(rules.Rules.Talents, t => string.Equals(t.Id, "academics", StringComparison.Ordinal));

        var sheet = rules.LegalSheet();
        sheet.Name = "the Hero";

        sheet.AbilityRanks["might"] = 4;
        sheet.AbilityRanks["agility"] = 3;

        // The Talent outranks every Ability and every Power on the sheet.
        sheet.TalentRanks["academics"] = 11;

        sheet.SelectedPowers.Add(new SelectedPower("blast", 8));
        sheet.SelectedPowers.Add(new SelectedPower("armor", 9));

        var hero = CombatantFactory.From(
            sheet, rules.Rules, rules.Derived, _play, CombatantKind.Hero, "hero");

        // The other control: all three really are on this combatant at a rank, so what follows is a
        // derivation refusing them rather than a sheet that never carried them.
        Assert.True(hero.Rank("academics") > 0, "the Talent came off the sheet at 0d");
        Assert.True(hero.Rank("armor") > 0, "the Armor came off the sheet at 0d");
        Assert.True(hero.Rank("blast") > 0, "the Blast came off the sheet at 0d");

        var forms = AttackOptions.AvailableTo(_play, hero);

        Assert.Contains("blast", forms, StringComparer.Ordinal);
        Assert.Contains("might", forms, StringComparer.Ordinal);
        Assert.Contains("agility", forms, StringComparer.Ordinal);

        Assert.DoesNotContain("academics", forms, StringComparer.Ordinal);
        Assert.DoesNotContain("armor", forms, StringComparer.Ordinal);

        // And the choice: the Blast, not the higher-ranked Talent the old derivation would have
        // swung and not the Armor it would have swung on the Villain's side of the same report.
        Assert.Equal("blast", AttackOptions.BestFor(_play, hero));
    }

    /// <summary>
    /// <b>A group of Minions still attacks, and with the one characteristic p.77 gives them.</b>
    ///
    /// <para>They are the one combatant whose whole Trait list is an attack form: no Ability, no
    /// Power and no sheet, so a derivation built out of p.75's Abilities and Chapter 2's attack
    /// Powers would leave a mob holding its action for the whole fight. p.77 prints them rolling
    /// their Threat, so that is what they roll.</para>
    /// </summary>
    [Fact]
    public void AGroupOfMinionsAttacksWithTheOneCharacteristicPageSeventySevenGivesThem()
    {
        var mob = Combatant.Minions("mob", "the mob", threat: 6, groupSize: 4, threatTraitId: "threat");

        Assert.Equal(["threat"], AttackOptions.AvailableTo(_play, mob));
        Assert.Equal("threat", AttackOptions.BestFor(_play, mob));
    }

    // ── The branches no printed example reaches ──────────────────────────────

    /// <summary>
    /// <b>Moving takes the pages p.74 prices it at, and a Travel Power halves the bill.</b>
    ///
    /// <para>The `Move` intent had no fixture at all: the two printed movement examples go through
    /// <see cref="Movement"/>'s pure functions, so the banking of part-crossings, the range band
    /// actually changing and the note that moving does not use up the turn were unexercised.</para>
    ///
    /// <para>It is also where the Travel Power reading is checked. The list of eight ids is this
    /// engine's, not the data's — Chapter 2 has no such category — so the fixture drives one id from
    /// the list and one Power that is not on it, and requires the second to take the long way.</para>
    /// </summary>
    [Fact]
    public void MovingCrossesARangeClassAtThePriceThePageSets()
    {
        var rule = _play.GetCombat("movement").Movement!;

        // The controls on the data: two pages on foot, one with a Travel Power at the entry's rank.
        Assert.Equal(2, rule.PagesPerRangeClass);
        Assert.Equal(1, rule.PagesPerRangeClassWithATravelPower);

        var walker = Combatant.Hero("walker", "the walker", edge: 9, health: 10, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 4, ["mind_control"] = 12   // a big Power, and not a Travel one
            },
            ["toughness"]);

        var flier = Combatant.Hero("flier", "the flier", edge: 8, health: 10, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 4, ["flight"] = rule.TravelPowerRankRequired
            },
            ["toughness"]);

        // The control on the reading: one of these is on the engine's Travel Power list and the
        // other is not, which is what the two answers below turn on.
        Assert.Contains("flight", Movement.TravelPowerIds, StringComparer.Ordinal);
        Assert.DoesNotContain("mind_control", Movement.TravelPowerIds, StringComparer.Ordinal);

        var encounter = new Encounter(_play, new SeededDice(23));
        var state = encounter.Begin([walker, flier], opening: RangeBand.Distant);

        // The flier closes in one page.
        var flown = encounter.Step(state, new Attack("walker", "flier", "might"));   // walker acts first
        state = encounter.Step(flown.State, new EndTurn("walker")).State;

        var closed = encounter.Step(state, new Move("flier", "walker"));

        Assert.Equal(RangeBand.Close, closed.State.RangeBetween("flier", "walker"));
        Assert.Contains(closed.Added, l =>
            string.Equals(l.Rule, "movement", StringComparison.Ordinal)
            && l.Text.Contains("closes to Close Range", StringComparison.Ordinal));

        // Moving does not use up the turn's action, which the line says and the entry decides.
        Assert.Contains(closed.Added, l =>
            l.Text.Contains($"MovingPreventsActions is {rule.MovingPreventsActions}", StringComparison.Ordinal));

        // The walker banks a page and arrives on the second, which is the branch nothing reached.
        var fresh = new Encounter(_play, new SeededDice(23));
        var opening = fresh.Begin([walker, flier], opening: RangeBand.Distant);

        var first = fresh.Step(opening, new Move("walker", "flier"));

        Assert.Equal(RangeBand.Distant, first.State.RangeBetween("walker", "flier"));
        Assert.Contains(first.Added, l =>
            l.Text.Contains("1 of the 2 a range class takes them", StringComparison.Ordinal));

        var second = fresh.Step(first.State, new Move("walker", "flier"));
        Assert.Equal(RangeBand.Close, second.State.RangeBetween("walker", "flier"));

        // And opening again puts the distance back.
        var away = fresh.Step(second.State, new Move("walker", "flier", Closer: false));
        away = fresh.Step(away.State, new Move("walker", "flier", Closer: false));

        Assert.Equal(RangeBand.Distant, away.State.RangeBetween("walker", "flier"));
    }

    /// <summary>
    /// <b>Wound Penalties, and the reading that the deeper band replaces the shallower one.</b>
    ///
    /// <para>p.81 prints the two as thresholds rather than steps and never says whether they stack;
    /// at zero Health a character is already below half of any positive Health, so a stacking reading
    /// would make every −4d a −6d. The fixture drives both bands and requires the deeper one to be
    /// the entry's own figure and not the sum of the two.</para>
    ///
    /// <para>The deeper band is reached through p.79's stabilisation roll, which is the one thing a
    /// character below the defeat figure may still do — everything else is refused, which is what
    /// makes this the branch to check it on.</para>
    /// </summary>
    [Fact]
    public void WoundPenaltiesDeeperBandReplacesTheShallowerOne()
    {
        var rule = _play.GetGritty("gritty_wound_penalties").WoundPenalties!;

        // The controls on the data.
        Assert.Equal(-2, rule.AtOrBelowHalfFullHealthPenaltyDice);
        Assert.Equal(-4, rule.AtOrBelowZeroHealthPenaltyDice);

        var table = TableRules.Book with { WoundPenalties = true, FatalDamage = true };

        var hurt = Combatant.Hero("hurt", "the hurt Hero", edge: 9, health: 8, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 10, ["medicine"] = 10, ["toughness"] = 4
            },
            ["toughness"]).WithHealth(4);

        var target = Combatant.Villain("target", "the target", edge: 4, health: 30,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 4 }, []);

        var encounter = new Encounter(_play, new SeededDice(24), table);
        var state = encounter.Begin([hurt, target], challengeLevel: 0);

        // Half of 8 is 4, so the shallower band: 10d becomes 8d.
        var shallow = encounter.Step(state, new Attack("hurt", "target", "might"));

        Assert.Contains(shallow.Added, l =>
            string.Equals(l.Rule, "gritty_wound_penalties", StringComparison.Ordinal)
            && l.Text.Contains("is on 4 of 8 Health: -2d", StringComparison.Ordinal));
        Assert.Contains("with might 8d", shallow.Added.Single(l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("attacks", StringComparison.Ordinal)).Text, StringComparison.Ordinal);

        // Below nothing, the deeper band — and it replaces rather than adds, so 10d becomes 6d and
        // not 4d. p.79's stabilisation roll is the one thing left to roll it on.
        var dying = state.With(state["hurt"].WithHealth(-1).Bleeding(dying: true));
        var deep = encounter.Step(dying, new Stabilise("hurt", "hurt"));

        Assert.Contains(deep.Added, l =>
            string.Equals(l.Rule, "gritty_wound_penalties", StringComparison.Ordinal)
            && l.Text.Contains("is on -1 of 8 Health: -4d", StringComparison.Ordinal));

        Assert.Single(deep.Added, l =>
            string.Equals(l.Rule, "gritty_wound_penalties", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>p.79's Gritty rule charging for a second active defence on a page.</b> The first is free
    /// and each one after it costs a die more than the last, counted per page — a branch of
    /// <c>ChooseDefence</c> nothing exercised.
    /// </summary>
    [Fact]
    public void ASecondActiveDefenceOnAPageCostsWhatTheEntrySays()
    {
        var rule = _play.GetGritty("gritty_active_defenses").ActiveDefensePenalty!;

        Assert.Equal(-1, rule.CumulativePenaltyDicePerExtraActiveDefense);
        Assert.True(rule.FirstActiveDefenseOnAPageIsUnpenalised);

        var dodger = Combatant.Hero("dodger", "the dodger", edge: 1, health: 30, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["agility"] = 8, ["might"] = 3 },
            ["agility"]);

        var first = Combatant.Villain("first", "the first attacker", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 5 }, ["toughness"]);

        var second = Combatant.Villain("second", "the second attacker", edge: 8, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 5 }, ["toughness"]);

        var encounter = new Encounter(
            _play, new SeededDice(25), TableRules.Book with { ActiveDefensesCost = true });

        var state = encounter.Begin([first, second, dodger]);

        var one = encounter.Step(state, new Attack("first", "dodger", "might"));
        Assert.Contains("defends with agility 8d", Defence(one), StringComparison.Ordinal);

        state = encounter.Step(one.State, new EndTurn("first")).State;

        var two = encounter.Step(state, new Attack("second", "dodger", "might"));

        Assert.Contains("defends with agility 7d", Defence(two), StringComparison.Ordinal);
        Assert.Contains(two.Added, l =>
            string.Equals(l.Rule, "gritty_active_defenses", StringComparison.Ordinal)
            && l.Text.Contains("active defence number 2 this page is -1d", StringComparison.Ordinal));

        // And the count resets when the page turns, which is what "counted per page" means.
        var turned = encounter.Step(
            encounter.Step(two.State, new EndTurn("second")).State, new EndTurn("dodger")).State;

        turned = encounter.Step(turned, new EndPage("")).State;

        var next = encounter.Step(turned, new Attack("first", "dodger", "might"));
        Assert.Contains("defends with agility 8d", Defence(next), StringComparison.Ordinal);

        static string Defence(StepResult step) => step.Added.Single(l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal)).Text;
    }

    /// <summary>
    /// <b>p.73's optional Edge roll, and the successes standing in for the whole battle.</b> The
    /// order comes off the rolls rather than off the derived figures, and a Minion group is left out
    /// because the entry says they have no Edge to roll.
    /// </summary>
    [Fact]
    public void RandomInitiativeRollsForTheOrderAndTheRollStands()
    {
        var slow = Combatant.Villain("slow", "the slow one", edge: 4, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 5 }, ["toughness"]);

        var quick = Combatant.Hero("quick", "the quick one", edge: 12, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 5 }, ["toughness"]);

        var minions = Combatant.Minions("minions", "the Minions", threat: 4, groupSize: 3, "threat");

        // The control: on the derived figures the quick one leads.
        var plain = new Encounter(_play, new SeededDice(26)).Begin([slow, quick, minions]);
        Assert.Equal(["quick", "slow", "minions"], plain.TurnOrder);

        // Rolled: the slow one's 4d scores more than the quick one's 12d, and the order follows.
        // Slow rolls 4 successes; quick rolls 1.
        var dice = new ScriptedDice([.. FacesFor(4, 4), .. FacesFor(12, 1)]);

        var encounter = new Encounter(
            _play, dice, TableRules.Book with { RandomInitiative = true });

        var rolled = encounter.Begin([slow, quick, minions]);

        // The control: exactly two Edge rolls were made — the Minions were left out. (The entry is
        // also the switch's own, so `Begin` writes a third line announcing the setting; the rolls
        // are the ones that say what they scored.)
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(2, rolled.Ledger.Lines.Count(l =>
            string.Equals(l.Rule, "edge_order", StringComparison.Ordinal)
            && l.Text.Contains("Edge for the order", StringComparison.Ordinal)));

        Assert.Equal(4, rolled.EffectiveEdge["slow"]);
        Assert.Equal(1, rolled.EffectiveEdge["quick"]);
        Assert.Equal(0, rolled.EffectiveEdge["minions"]);

        Assert.Equal(["slow", "quick", "minions"], rolled.TurnOrder);

        // And it stands "for this battle": the next page uses the same figures, not new rolls.
        var page = encounter.Step(
            encounter.Step(
                encounter.Step(
                    encounter.Step(rolled, new EndTurn("slow")).State, new EndTurn("quick")).State,
                new EndTurn("minions")).State,
            new EndPage(""));

        Assert.Equal(["slow", "quick", "minions"], page.State.TurnOrder);
        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// <b>The all-out and charge penalties expire at the end of the following page.</b>
    ///
    /// <para>p.78 says "until after your next turn to act", which is a turn rather than a page; this
    /// engine expires it at the end of the page after the one it was taken on, which is that sentence
    /// to within a turn and is recorded in the guide as a reading. It had no fixture, so the expiry
    /// was a claim in a doc comment.</para>
    /// </summary>
    [Fact]
    public void TheAllOutPenaltyExpiresAtTheEndOfTheFollowingPage()
    {
        var reckless = Combatant.Hero("reckless", "the reckless Hero", edge: 9, health: 30, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 8 },
            ["toughness"]);

        var patient = Combatant.Villain("patient", "the patient one", edge: 8, health: 30,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 12 }, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(27));
        var state = encounter.Begin([reckless, patient]);

        state = encounter.Step(state, new Attack("reckless", "patient", "might", AllOut: true)).State;
        state = encounter.Step(state, new EndTurn("reckless")).State;

        // Page 1, the page it was taken on: a Toughness of 8 answers a lethal blow at 4 and the
        // all-out halving takes it to 2.
        Assert.Contains("defends with toughness 2d", Struck(state), StringComparison.Ordinal);

        state = encounter.Step(state, new EndTurn("patient")).State;
        state = encounter.Step(state, new EndPage("")).State;
        Assert.Equal(2, state.Page);

        // Page 2, still inside "until after your next turn to act".
        state = encounter.Step(state, new EndTurn("reckless")).State;
        Assert.Contains("defends with toughness 2d", Struck(state), StringComparison.Ordinal);

        state = encounter.Step(state, new EndTurn("patient")).State;
        state = encounter.Step(state, new EndPage("")).State;
        Assert.Equal(3, state.Page);

        // Page 3: gone.
        state = encounter.Step(state, new EndTurn("reckless")).State;
        Assert.Contains("defends with toughness 4d", Struck(state), StringComparison.Ordinal);

        string Struck(EncounterState now) => encounter
            .Step(now, new Attack("patient", "reckless", "might", Type: AttackType.MeleeWeapon))
            .Added
            .Single(l => string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                         && l.Text.Contains("defends with", StringComparison.Ordinal))
            .Text;
    }

    /// <summary>
    /// <b>The GM's pool buys for an NPC what Resolve buys for a Hero.</b>
    ///
    /// <para>Every `SpendAdversity` used to refuse, which made the whole of p.85's first purchase a
    /// name on a list. It is not a rule of its own — "whatever a point of Resolve could have done, on
    /// behalf of any NPC" is the Resolve purchases with different money behind them — so the intent
    /// names which one, the GM's pool pays, and the NPC's non-existent Resolve is never touched.</para>
    ///
    /// <para><b>All ten are bought now, and the tail of this fixture drives the change.</b> Four of
    /// them charged the buyer's own Resolve and so answered <c>not yet implemented</c> from the GM's
    /// pool; seizing the initiative is the one of those four that needs nothing to have happened
    /// first, so it is the one that fits here — the others have their own fixtures.</para>
    /// </summary>
    [Fact]
    public void AdversityBuysAnNpcTheDiceResolveWouldHaveBought()
    {
        var villain = Combatant.Villain("villain", "the Villain", edge: 9, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);

        var hero = Combatant.Hero("hero", "the Hero", edge: 5, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4, ["might"] = 4 },
            ["toughness"]);

        // 6d for 2, the Hero's 2d Toughness for 2 — nothing lands — then the two bought dice for 2.
        var dice = new ScriptedDice([.. FacesFor(6, 2), .. FacesFor(2, 2), .. FacesFor(2, 2)]);
        var encounter = new Encounter(_play, dice);

        // A Challenge Level puts enough in the GM's pool to buy two dice: one Hero is one point an
        // issue, and p.85's scene award is what a fight of any size actually opens on.
        var state = encounter.Begin([villain, hero], challengeLevel: 2);
        var opening = state.Adversity;

        Assert.True(opening >= 2, $"the GM opened on {opening} Adversity and the spend costs 2");

        state = encounter.Step(state, new Attack(
            "villain", "hero", "might", Type: AttackType.MeleeWeapon)).State;

        // The controls: the attack went nowhere, and the NPC holds no Resolve to have paid with.
        Assert.Equal(2, state.LastAttack!.AttackSuccesses);
        Assert.Equal(2, state.LastAttack.DefenceSuccesses);
        Assert.Equal(20, state["hero"].CurrentHealth);
        Assert.False(state["villain"].HoldsResolve);

        var spent = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, Points: 2, AsResolve: ResolveSpend.ExtraDice));

        Assert.Equal(0, dice.Remaining);

        // The GM's pool paid, the Villain's did not, and the blow landed.
        Assert.Equal(opening - 2, spent.State.Adversity);
        Assert.Equal(0, spent.State["villain"].Resolve);
        Assert.Equal(4, spent.State.LastAttack!.AttackSuccesses);
        Assert.Equal(18, spent.State["hero"].CurrentHealth);

        Assert.Contains(spent.Added, l =>
            string.Equals(l.Rule, "adversity_spend_anything_resolve_can", StringComparison.Ordinal));
        Assert.Contains(spent.Added, l =>
            l.Text.Contains("the GM spends 2 Adversity on the Villain", StringComparison.Ordinal));

        // And the four that used to refuse are bought too. Seizing needs no roll, so it is the one
        // that can be driven from here: the pool pays, the Villain's Resolve stays at nothing, and
        // p.73's own effect lands on the state rather than only on the line.
        var seized = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(opening - 1, seized.State.Adversity);
        Assert.Equal(0, seized.State["villain"].Resolve);
        Assert.Contains("villain", seized.State.Seized);

        Assert.DoesNotContain(seized.Added, l =>
            l.Text.Contains("not yet implemented", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>p.85 spends Adversity "on behalf of any NPC", and a Hero is not one.</b>
    ///
    /// <para>The sentence that makes the first purchase a purchase at all names who it may be spent
    /// for, and the entry transcribes the list: <c>npc_kinds</c> is Villains, Foes, Minions and
    /// Extras. A Hero is on no list on that page, and the pool is the GM's precisely because the
    /// players have one of their own — a point of Adversity buying a Hero the die their own Resolve
    /// would have bought is the one thing the two-pool economy exists to make impossible.</para>
    ///
    /// <para><b>The refusal is read off the entry rather than typed here</b>, the way the other two
    /// eligibility refusals on this page are: a change to <c>npc_kinds</c> moves the engine with
    /// it.</para>
    ///
    /// <para>The control is that the same purchase, in the same fight, against a character who
    /// <em>is</em> an NPC goes through — a run in which nothing at all could be bought would satisfy
    /// the refusal while measuring nothing.</para>
    /// </summary>
    [Fact]
    public void TheGmsPoolIsSpentOnAnNpcAndNeverOnAHero()
    {
        var entry = _play.GetResolve("adversity_spend_anything_resolve_can");
        var rule = entry.Spend!;

        // The control on the data: the page names the four kinds, and a Hero is not among them.
        Assert.True(rule.MayBeSpentOnAnyNpc);
        Assert.Equal(["Villains", "Foes", "Minions", "Extras"], rule.NpcKinds);

        // <b>And the control on the refusal: the four the page names are the four this engine has
        // besides a Hero.</b> The guard admits a combatant by the plural of the kind p.85 spells,
        // so a mapping that had drifted would refuse every NPC as well as every Hero — which the
        // Villain at the end of this fixture would catch, but only after the fact. This says which
        // half is wrong.
        Assert.Equal(
            rule.NpcKinds!.Order(StringComparer.Ordinal),
            Enum.GetValues<CombatantKind>()
                .Where(k => k != CombatantKind.Hero)
                .Select(k => (k == CombatantKind.MinionGroup ? "Minion" : k.ToString()) + "s")
                .Order(StringComparer.Ordinal));

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 5, health: 20, traits, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(21));
        var state = encounter.Begin([hero, villain], challengeLevel: 2);
        var opening = state.Adversity;

        Assert.True(opening >= 1, $"the GM opened on {opening} Adversity");

        // The Hero attacks, so the purchase decided after the roll has a roll of theirs to buy on.
        state = encounter.Step(state, new Attack("hero", "villain", "might")).State;

        Assert.Equal("hero", state.LastAttack!.Actor);

        var onAHero = encounter.Step(state, new SpendAdversity(
            "hero", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.ExtraDice));

        // Nothing was spent, nothing was bought, and the fight is the same objects it was.
        Assert.Equal(opening, onAHero.State.Adversity);
        Assert.Equal(3, onAHero.State["hero"].Resolve);
        Assert.Same(state.Combatants, onAHero.State.Combatants);
        Assert.Equal(state.LastAttack.AttackSuccesses, onAHero.State.LastAttack!.AttackSuccesses);

        Assert.Contains(onAHero.Added, l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)
            && l.Text.Contains("on behalf of any NPC", StringComparison.Ordinal)
            && l.Text.Contains("is a Hero", StringComparison.Ordinal));

        // And it is not that nothing can be bought here: the Villain's own attack buys a die.
        state = encounter.Step(state, new EndTurn("hero")).State;
        state = encounter.Step(state, new Attack("villain", "hero", "might")).State;

        var onAnNpc = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.ExtraDice));

        Assert.Equal(opening - 1, onAnNpc.State.Adversity);
    }

    /// <summary>
    /// <b>A purchase priced below a point is refused, and it used to be paid for backwards.</b>
    ///
    /// <para>Every price Chapters 4 and 5 print is a whole number of points and none of them is
    /// below one — but the number of points is the caller's, and two things read it. p.85's pool
    /// gate compared the pool with what the intent asked for, and nine of the ten purchases charge
    /// their own printed price and ignore that figure: so a spend of <c>0</c> points walked through
    /// the gate on an empty pool and then charged one, leaving the GM on <b>−1 Adversity</b> with a
    /// seized initiative they had not paid for. And p.84's dice purchase <em>multiplies</em> by the
    /// figure, so a spend of <c>−5</c> points took a cost of −5 out of a pool, which is five points
    /// arriving: the GM's pool grew, and a Hero buying −5 dice of their own minted Resolve the same
    /// way. <c>CannotAfford</c> could not see either, because a pool is always at least a negative
    /// cost.</para>
    ///
    /// <para><b>Neither was reachable only from a test.</b> <c>spend_adversity</c> and
    /// <c>spend_resolve</c> both read <c>points</c> off the wire as a plain number, so a
    /// conversation asking for none or for fewer than none is a request this server took.</para>
    ///
    /// <para>Each half carries the same purchase at one point beside it, or these would be
    /// assertions about an engine that had stopped buying anything.</para>
    /// </summary>
    [Fact]
    public void APurchasePricedBelowAPointIsRefusedRatherThanChargedBackwards()
    {
        var dice = _play.GetResolve("spend_challenge_roll_dice").Spend!;

        // The control on the data: the page's rate really is a point for a die, which is what makes
        // a negative figure a negative cost rather than a harmless one.
        Assert.Equal(1, dice.CostResolve);
        Assert.Equal(1, dice.DiceGained);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 5, health: 20, traits, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(21));
        var opened = encounter.Begin([hero, villain], challengeLevel: 2);

        // ── Nothing in the pool, and a spend of no points ────────────────────
        var empty = opened with { Adversity = 0 };

        var free = encounter.Step(empty, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, Points: 0,
            AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(0, free.State.Adversity);
        Assert.DoesNotContain("villain", free.State.Seized);

        Assert.Contains(free.Added, l =>
            l.Text.Contains("asks for 0 points", StringComparison.Ordinal)
            && l.Text.Contains("whole point or more", StringComparison.Ordinal));

        // The control: the same purchase at the price p.73 prints does go through.
        var bought = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(opened.Adversity - 1, bought.State.Adversity);
        Assert.Contains("villain", bought.State.Seized);

        // ── A purchase of fewer than no dice, out of each pool ───────────────
        var rolled = encounter.Step(
            encounter.Step(
                encounter.Step(opened, new Attack("hero", "villain", "might")).State,
                new EndTurn("hero")).State,
            new Attack("villain", "hero", "might")).State;

        Assert.Equal("villain", rolled.LastAttack!.Actor);

        var minted = encounter.Step(rolled, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, Points: -5,
            AsResolve: ResolveSpend.ExtraDice));

        Assert.Equal(rolled.Adversity, minted.State.Adversity);
        Assert.Equal(rolled.LastAttack.AttackPool, minted.State.LastAttack!.AttackPool);

        Assert.Contains(minted.Added, l =>
            l.Text.Contains("asks for -5 points", StringComparison.Ordinal));

        // The Hero's own pool, on the Hero's own roll: p.84's purchase does not run backwards there
        // either, and the refusal is the dice entry's rather than p.85's.
        var heroRolled = encounter.Step(opened, new Attack("hero", "villain", "might")).State;

        var backwards = encounter.Step(
            heroRolled, new SpendResolve("hero", ResolveSpend.ExtraDice, Points: -5));

        Assert.Equal(3, backwards.State["hero"].Resolve);
        Assert.Equal(heroRolled.LastAttack!.AttackPool, backwards.State.LastAttack!.AttackPool);

        Assert.Contains(backwards.Added, l =>
            string.Equals(l.Rule, "spend_challenge_roll_dice", StringComparison.Ordinal)
            && l.Text.Contains("asked for -5 dice", StringComparison.Ordinal));

        // And the control on that half: one point still buys one die.
        var one = encounter.Step(heroRolled, new SpendResolve("hero", ResolveSpend.ExtraDice));

        Assert.Equal(2, one.State["hero"].Resolve);
        Assert.Equal(heroRolled.LastAttack.AttackPool + 1, one.State.LastAttack!.AttackPool);
    }

    /// <summary>
    /// <b>p.73's seized initiative, bought out of the GM's pool for an NPC.</b>
    ///
    /// <para>It used to answer <c>not yet implemented</c> from that pool, because it charged the
    /// buyer's own Resolve and an NPC has none. It goes through <c>Charge(fromAdversity: true)</c>
    /// now, and everything p.73 prints about the purchase is unchanged by which pool paid: the place
    /// at the front, the duration that refuses the second purchase, and the GM's alternative, which
    /// doubles an effective Edge instead.</para>
    ///
    /// <para><b>The effect is read off the order rather than off the line</b>, because a ledger line
    /// saying somebody went first and an order that has not moved is the one failure this engine's
    /// ledger rules exist to prevent. So the page is turned and the order is looked at, twice: under
    /// the book, where a seizer precedes everybody whatever their Edge, and under the GM's
    /// alternative, with figures only the doubling wins on.</para>
    ///
    /// <para><b>A Hero is refused, and so is a group of Minions.</b> The first is p.85's
    /// <c>npc_kinds</c>; the second is p.73's own <c>minions_have_an_edge</c> and <c>minions_act</c>,
    /// which leave a Minion group nothing either form of this purchase could give them — see the
    /// readings table in <c>docs/guide/play-engine.md</c>.</para>
    /// </summary>
    [Fact]
    public void TheGmsPoolSeizesTheInitiativeForAnNpcAndNotForAHeroOrAMob()
    {
        var rule = _play.GetCombat("seizing_initiative").SeizeInitiative!;
        var tie = _play.GetCombat("edge_ties").TieBreak!;

        // The controls on the data: the price this fixture's arithmetic uses, and the two fields the
        // Minion refusal is read off.
        Assert.Equal(1, rule.CostResolve);
        Assert.False(tie.MinionsHaveAnEdge);
        Assert.Contains("after everyone else", tie.MinionsAct, StringComparison.Ordinal);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 5, health: 20, traits, ["toughness"]);
        var mob = Combatant.Minions("mob", "the Minions", threat: 4, groupSize: 4, "threat");

        var encounter = new Encounter(_play, new SeededDice(19));
        var opened = encounter.Begin([hero, villain, mob], challengeLevel: 2);

        // The controls on the fixture: two points in the pool, and the Hero in front to start with,
        // so a Villain at the front below is this purchase and not the order it opened in.
        Assert.True(opened.Adversity >= 2, $"the fight opened on {opened.Adversity} Adversity");
        Assert.Equal("hero", opened.TurnOrder[0]);

        var bought = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        // The pool paid exactly once, and the Villain's Resolve is the field this asserts: a Villain
        // holds none at all, so a purchase charged to one would be a throw rather than a wrong count.
        Assert.Equal(opened.Adversity - rule.CostResolve, bought.State.Adversity);
        Assert.Equal(0, bought.State["villain"].Resolve);
        Assert.False(bought.State["villain"].HoldsResolve);

        // p.85 beside p.73: the announcement cites the Chapter 5 page and the purchase its own.
        Assert.Contains(bought.Added, l =>
            string.Equals(l.Rule, "adversity_spend_anything_resolve_can", StringComparison.Ordinal)
            && l.SourceRef.Contains("p.85", StringComparison.Ordinal)
            && l.Text.Contains("the GM spends 1 Adversity on the Villain", StringComparison.Ordinal));

        Assert.Contains(bought.Added, l =>
            string.Equals(l.Rule, "seizing_initiative", StringComparison.Ordinal)
            && l.SourceRef.Contains("p.73", StringComparison.Ordinal));

        // The effect lands where p.73 puts it: at the front of the next page's order.
        var turned = encounter.Step(bought.State, new EndPage("")).State;

        Assert.Equal("villain", turned.TurnOrder[0]);

        // And it lasts the rest of the fight, so the second purchase is refused with nothing spent.
        var again = encounter.Step(turned, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(turned.Adversity, again.State.Adversity);
        Assert.Contains(again.Added, l =>
            l.Text.Contains("has already seized the initiative", StringComparison.Ordinal));

        // A Hero is not an NPC: nothing spent, and nobody seized.
        var onAHero = encounter.Step(opened, new SpendAdversity(
            "hero", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(opened.Adversity, onAHero.State.Adversity);
        Assert.Equal(3, onAHero.State["hero"].Resolve);
        Assert.DoesNotContain("hero", onAHero.State.Seized);

        // A Minion group is an NPC, and p.73 leaves this purchase nothing to give them.
        var onTheMob = encounter.Step(opened, new SpendAdversity(
            "mob", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(opened.Adversity, onTheMob.State.Adversity);
        Assert.DoesNotContain("mob", onTheMob.State.Seized);

        Assert.Contains(onTheMob.Added, l =>
            string.Equals(l.Rule, "edge_ties", StringComparison.Ordinal)
            && l.Text.Contains("no Edge at all", StringComparison.Ordinal)
            && l.Text.Contains(tie.MinionsAct, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The GM's alternative to seizing, bought out of the GM's pool.</b>
    ///
    /// <para>p.73 offers a table a different effect for the same point: the buyer's effective Edge
    /// doubles rather than their going first outright. Driven separately from the fixture above
    /// because the two are different answers to the same purchase, and the figures here are chosen
    /// so that only the doubling wins — a Villain on 5 against a Hero on 9 goes second unless the 5
    /// has become 10.</para>
    /// </summary>
    [Fact]
    public void TheGmsAlternativeToSeizingIsBoughtOutOfTheGmsPoolToo()
    {
        var alternative = _play.GetCombat("seize_initiative_gm_alternative");

        // The control on the data: the effect this fixture's arithmetic reads as a doubling.
        Assert.Contains("doubles", alternative.GmAlternative!.Effect, StringComparison.Ordinal);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 5, health: 20, traits, ["toughness"]);

        var encounter = new Encounter(
            _play, new SeededDice(19), TableRules.Book with { GmAlternativeToSeizingInitiative = true });

        var opened = encounter.Begin([hero, villain], challengeLevel: 2);

        // The controls: a pool to spend, and the Hero ahead on the undoubled figures.
        Assert.True(opened.Adversity >= 1, $"the fight opened on {opened.Adversity} Adversity");
        Assert.Equal("hero", opened.TurnOrder[0]);

        var bought = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(opened.Adversity - 1, bought.State.Adversity);
        Assert.Equal(0, bought.State["villain"].Resolve);

        // The line names the alternative's own entry rather than the purchase's.
        Assert.Contains(bought.Added, l =>
            string.Equals(l.Rule, "seize_initiative_gm_alternative", StringComparison.Ordinal)
            && l.Text.Contains(alternative.GmAlternative.Effect, StringComparison.Ordinal));

        // And a doubled 5 goes ahead of an undoubled 9.
        var turned = encounter.Step(bought.State, new EndPage("")).State;

        Assert.Equal("villain", turned.TurnOrder[0]);
    }

    /// <summary>
    /// <b>The fourfold reading, reached out of the GM's pool, in a fight with a Minion group in
    /// it.</b>
    ///
    /// <para><see cref="BothDoublingsCompoundOnAReadyCharacterWhoSeizesTheInitiative"/> holds the
    /// composition itself and holds it between two Heroes buying with their own Resolve, because
    /// that is the only way either of them could buy it before this slice. <b>The buyer p.85 wrote
    /// the purchase for is an NPC</b>, and an NPC reaches p.73's seize down a different branch, in
    /// a fight where the other two things this engine's order sorts on are live: a Minion group,
    /// which <c>minions_act</c> puts outside the whole comparison, and p.79's Drop, which p.73
    /// declines to give that group anything to double.</para>
    ///
    /// <para>The figures are the composition's: a Villain on 5 against a Hero on 9, both ready, so
    /// the Drop makes it 10 against 18 and the Hero leads — and the alternative doubles the 10 to 20
    /// against a figure the seize itself never touches. The first assertion is the control, and the
    /// second is the reading.</para>
    ///
    /// <para><b>And the Minions stay last through both</b>, which is the interaction worth driving:
    /// <c>minions_act</c> is the outermost key of this order, so a ready group that the Drop could
    /// not double and a purchase that the same page refuses them leave them exactly where p.73 puts
    /// them.</para>
    /// </summary>
    [Fact]
    public void TheGmsPoolReachesBothDoublingsWithAMinionGroupInTheFight()
    {
        var table = TableRules.Book with { TheDrop = true, GmAlternativeToSeizingInitiative = true };
        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant.Hero(
            "hero", "the Hero", edge: 9, health: 10, resolve: 2, traits, ["toughness"], ready: true);

        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 5, health: 10, traits, ["toughness"], ready: true);

        // Ready as well, and p.73 gives them no Edge for the Drop to double — the line saying so is
        // asserted below, because a group quietly doubled would still sort last and say nothing.
        var mob = Combatant.Minions("mob", "the Minions", threat: 4, groupSize: 4, "threat", ready: true);

        // <b>And somebody with nothing levelled, because p.79's Drop is held against a person.</b>
        // A fight in which everybody is ready is a fight in which nobody has the drop on anybody,
        // and the entry says so on page one rather than doubling every figure for nothing.
        var foe = Combatant.Foe("foe", "the Foe", edge: 3, health: 6, traits, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(19), table);
        var opened = encounter.Begin([hero, villain, mob, foe], challengeLevel: 2);

        // The controls on the fixture: a pool, the Drop doubling the two who have an Edge, and
        // nothing to double for the group.
        Assert.True(opened.Adversity >= 1, $"the fight opened on {opened.Adversity} Adversity");
        Assert.Equal(18, opened.EffectiveEdge["hero"]);
        Assert.Equal(10, opened.EffectiveEdge["villain"]);

        Assert.Contains(opened.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_the_drop", StringComparison.Ordinal)
            && l.Text.Contains("the Minions", StringComparison.Ordinal)
            && l.Text.Contains("nothing here to double", StringComparison.Ordinal));

        // One doubling is not enough: the Hero leads on 18 against the Villain's doubled 10.
        Assert.Equal(["hero", "villain", "foe", "mob"], opened.TurnOrder);

        var bought = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(opened.Adversity - 1, bought.State.Adversity);
        Assert.Equal(0, bought.State["villain"].Resolve);

        // Both together are: 5 doubled by the Drop and doubled again by the GM's alternative goes
        // ahead of a figure the purchase never touched — and the Minions are still last.
        var turned = encounter.Step(bought.State, new EndPage("")).State;

        Assert.Equal(["villain", "hero", "foe", "mob"], turned.TurnOrder);

        // And the group cannot buy its way out of that placement, off the page that made it.
        var onTheMob = encounter.Step(turned, new SpendAdversity(
            "mob", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        Assert.Equal(turned.Adversity, onTheMob.State.Adversity);
        Assert.DoesNotContain("mob", onTheMob.State.Seized);
        Assert.Equal(
            ["villain", "hero", "foe", "mob"],
            encounter.Step(onTheMob.State, new EndPage("")).State.TurnOrder);
    }

    /// <summary>
    /// <b>p.76's instant recovery, bought out of the GM's pool for an NPC who has just gone down.</b>
    ///
    /// <para>Every limit the page prints survives the change of pool. The Health it brings back is
    /// <c>after_a_damaging_defeat_restores_health</c>, read off the entry; <c>limit_per_scene</c> is
    /// counted on the character rather than on the pool, so a Villain bought back onto their feet is
    /// not bought back a second time out of the GM's money; and the purchase is refused where there
    /// is nothing to recover from.</para>
    ///
    /// <para><b>A group of Minions is refused, off p.77.</b> A group has no Health to bring back and
    /// cannot be carrying an effect to shake off, because an effect against a group takes bodies out
    /// of it instead.</para>
    /// </summary>
    [Fact]
    public void TheGmsPoolBringsAnNpcRoundAndNotAMob()
    {
        var entry = _play.GetCombat("instant_recovery");
        var rule = entry.InstantRecovery!;

        // The controls on the data: the price, the Health it restores, and the once-a-scene limit.
        Assert.Equal(1, rule.CostResolve);
        Assert.Equal(1, rule.LimitPerScene);
        Assert.True(rule.AfterADamagingDefeatRestoresHealth > 0);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        // <b>The Hero is on the floor beside the Villain, and that is what makes the refusal below
        // worth anything.</b> A Hero on their feet is refused this purchase for having nothing to
        // recover from, so a fixture built that way passes whether or not p.85's eligibility list is
        // read at all — which was watched: deleting the npc_kinds gate left it green.
        var hero = Combatant
            .Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3, traits, ["toughness"])
            .WithHealth(0);

        var mob = Combatant.Minions("mob", "the Minions", threat: 4, groupSize: 4, "threat");

        var villain = Combatant
            .Villain("villain", "the Villain", edge: 5, health: 10, traits, ["toughness"])
            .WithHealth(0);

        var encounter = new Encounter(_play, new SeededDice(23));
        var opened = encounter.Begin([hero, villain, mob], challengeLevel: 2);

        // The controls on the fixture: two points to spend, and the Villain really is down.
        Assert.True(opened.Adversity >= 2, $"the fight opened on {opened.Adversity} Adversity");
        Assert.True(opened["villain"].Defeated(_play.GetCombat("damage").Damage!.DefeatedAtHealth));

        var bought = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.InstantRecovery));

        // The pool paid once, the Villain's own pool is untouched, and they are on their feet with
        // the Health the entry names.
        Assert.Equal(opened.Adversity - rule.CostResolve, bought.State.Adversity);
        Assert.Equal(0, bought.State["villain"].Resolve);
        Assert.Equal(rule.AfterADamagingDefeatRestoresHealth, bought.State["villain"].CurrentHealth);
        Assert.False(bought.State["villain"].Defeated(_play.GetCombat("damage").Damage!.DefeatedAtHealth));

        Assert.Contains(bought.Added, l =>
            string.Equals(l.Rule, "adversity_spend_anything_resolve_can", StringComparison.Ordinal)
            && l.SourceRef.Contains("p.85", StringComparison.Ordinal));

        Assert.Contains(bought.Added, l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)
            && l.SourceRef.Contains("p.76", StringComparison.Ordinal)
            && l.Text.Contains("the GM spends 1 Adversity on the Villain", StringComparison.Ordinal));

        // The limit is the character's and the pool cannot buy round it: knocked down again, the
        // second purchase is refused with nothing spent.
        var downAgain = bought.State.With(bought.State["villain"].WithHealth(0));

        var twice = encounter.Step(downAgain, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.InstantRecovery));

        Assert.Equal(downAgain.Adversity, twice.State.Adversity);
        Assert.Contains(twice.Added, l =>
            l.Text.Contains("has already taken 1 instant recovery this scene", StringComparison.Ordinal));

        // A Hero is not an NPC — and this one is down, so the refusal is p.85's list and not the
        // purchase declining to help somebody who is fine.
        var onAHero = encounter.Step(opened, new SpendAdversity(
            "hero", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.InstantRecovery));

        Assert.Equal(opened.Adversity, onAHero.State.Adversity);
        Assert.Equal(3, onAHero.State["hero"].Resolve);
        Assert.Equal(0, onAHero.State["hero"].CurrentHealth);
        Assert.Equal(0, onAHero.State["hero"].InstantRecoveriesUsed);

        // And a Minion group has neither half of what this purchase gives back.
        var onTheMob = encounter.Step(opened, new SpendAdversity(
            "mob", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.InstantRecovery));

        Assert.Equal(opened.Adversity, onTheMob.State.Adversity);
        Assert.Equal(4, onTheMob.State["mob"].GroupSize);

        Assert.Contains(onTheMob.Added, l =>
            string.Equals(l.Rule, "attacking_minions", StringComparison.Ordinal)
            && l.Text.Contains("no Health to bring back", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>p.76's once-a-scene is counted per character, and the two pools do not share a count.</b>
    ///
    /// <para>The fixture above drives the limit down one pool: a Villain bought back onto their feet
    /// is not bought back a second time out of the GM's money. <b>What it cannot say is that the
    /// limit is the character's rather than the purchase's</b> — a count kept on the encounter, or
    /// on the pool that paid, would satisfy every assertion there and would refuse a Villain the
    /// recovery a Hero had already taken. p.76 puts the sentence on the buyer: "you can only use
    /// instant recovery once per scene".</para>
    ///
    /// <para>So a Hero spends their own Resolve and comes round, and then the GM's pool brings a
    /// Villain round in the same scene — and the second purchase against each of them is refused
    /// while the other's count stands where it was.</para>
    /// </summary>
    [Fact]
    public void TheOnceASceneLimitIsTheCharactersAndNotThePools()
    {
        var rule = _play.GetCombat("instant_recovery").InstantRecovery!;
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        Assert.Equal(1, rule.LimitPerScene);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant
            .Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3, traits, ["toughness"])
            .WithHealth(floor);

        var villain = Combatant
            .Villain("villain", "the Villain", edge: 5, health: 10, traits, ["toughness"])
            .WithHealth(floor);

        var encounter = new Encounter(_play, new SeededDice(23));
        var opened = encounter.Begin([hero, villain], challengeLevel: 2);

        // The controls on the fixture: a pool to spend, and both of them really down.
        Assert.True(opened.Adversity >= 2, $"the fight opened on {opened.Adversity} Adversity");
        Assert.True(opened["hero"].Defeated(floor));
        Assert.True(opened["villain"].Defeated(floor));

        // The Hero's own point, out of their own pool.
        var heroUp = encounter.Step(opened, new SpendResolve("hero", ResolveSpend.InstantRecovery)).State;

        Assert.Equal(2, heroUp["hero"].Resolve);
        Assert.Equal(opened.Adversity, heroUp.Adversity);
        Assert.Equal(1, heroUp["hero"].InstantRecoveriesUsed);
        Assert.Equal(0, heroUp["villain"].InstantRecoveriesUsed);

        // And the GM's, for the Villain, in the same scene: a count kept anywhere but on the
        // character would refuse this one because the Hero had already taken theirs.
        var both = encounter.Step(heroUp, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.InstantRecovery));

        Assert.Equal(heroUp.Adversity - rule.CostResolve, both.State.Adversity);
        Assert.Equal(rule.AfterADamagingDefeatRestoresHealth, both.State["villain"].CurrentHealth);
        Assert.Equal(1, both.State["villain"].InstantRecoveriesUsed);
        Assert.Equal(1, both.State["hero"].InstantRecoveriesUsed);

        // And each of them is refused a second, on their own count and with nothing spent.
        var down = both.State
            .With(both.State["hero"].WithHealth(floor))
            .With(both.State["villain"].WithHealth(floor));

        var heroAgain = encounter.Step(down, new SpendResolve("hero", ResolveSpend.InstantRecovery));

        Assert.Equal(2, heroAgain.State["hero"].Resolve);
        Assert.Contains(heroAgain.Added, l =>
            l.Text.Contains("has already taken 1 instant recovery this scene", StringComparison.Ordinal));

        var villainAgain = encounter.Step(down, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.InstantRecovery));

        Assert.Equal(down.Adversity, villainAgain.State.Adversity);
        Assert.Contains(villainAgain.Added, l =>
            l.Text.Contains("has already taken 1 instant recovery this scene", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>p.79's Fatal Damage rescue and its stabilisation, both bought out of the GM's pool.</b>
    ///
    /// <para>The two are driven together because the page ties them together:
    /// <c>resolve_also_stabilises_if_necessary</c> makes the rescue do both, so a fixture that
    /// bought the rescue and never looked at the clock would leave half the entry unchecked. The
    /// rescue's own arithmetic is the interpretation's — one point <em>above</em> the threshold,
    /// which is what the worked example computes — and is unchanged by which pool paid.</para>
    ///
    /// <para><b>A group of Minions is refused for both, off p.77.</b> The threshold is the negative
    /// of a full Health and the clock starts when lethal damage takes a Health past it;
    /// <c>minions_have_health</c> is false, so a group is never at either.</para>
    /// </summary>
    [Fact]
    public void TheGmsPoolBuysAnNpcBackFromAFatalBlowAndStopsTheClock()
    {
        var entry = _play.GetGritty("gritty_fatal_damage");
        var fatal = entry.FatalDamage!;

        // The controls on the data: the two prices, and the clause that makes the rescue steady them.
        Assert.Equal(1, fatal.CostResolveToAvoid);
        Assert.Equal(1, fatal.CostResolveToStabiliseImmediately);
        Assert.True(fatal.ResolveAlsoStabilisesIfNecessary);

        var table = TableRules.Book with { FatalDamage = true };
        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        // <b>The Hero is past their own threshold and bleeding too</b>, for the reason the instant
        // recovery fixture says: a Hero nowhere near the line is refused both of these purchases by
        // the purchases themselves, so a fixture built that way would pass with p.85's eligibility
        // list deleted. Watched: it was, and it did.
        var hero = Combatant
            .Hero("hero", "the Hero", edge: 9, health: 4, resolve: 3, traits, ["toughness"])
            .WithHealth(-5).Bleeding(dying: true);

        var mob = Combatant.Minions("mob", "the Minions", threat: 4, groupSize: 4, "threat");

        // A Health of 4 puts the fatal threshold at -4; this one is at -5, past it and bleeding.
        var villain = Combatant
            .Villain("villain", "the Villain", edge: 5, health: 4, traits, ["toughness"])
            .WithHealth(-5).Bleeding(dying: true);

        var encounter = new Encounter(_play, new SeededDice(23), table);
        var opened = encounter.Begin([hero, villain, mob], challengeLevel: 2);

        // The controls on the fixture: a pool, and a Villain who is both past the line and on the
        // clock, so the two halves below are each about something that was true first.
        Assert.True(opened.Adversity >= 2, $"the fight opened on {opened.Adversity} Adversity");
        Assert.Equal(-5, opened["villain"].CurrentHealth);
        Assert.True(opened["villain"].Dying);

        var rescued = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.AvoidFatalDamage));

        // One point out of the pool, none out of a Resolve the Villain does not have, and the blow
        // is bought back to one point above the threshold of -4.
        Assert.Equal(opened.Adversity - fatal.CostResolveToAvoid, rescued.State.Adversity);
        Assert.Equal(0, rescued.State["villain"].Resolve);
        Assert.Equal(-3, rescued.State["villain"].CurrentHealth);

        // And the same point steadied them, which is the clause the page attaches to the rescue.
        Assert.False(rescued.State["villain"].Dying);

        Assert.Contains(rescued.Added, l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)
            && l.SourceRef.Contains("p.79", StringComparison.Ordinal)
            && l.Text.Contains("the GM spends 1 Adversity on the Villain against a fatal blow", StringComparison.Ordinal));

        Assert.Contains(rescued.Added, l =>
            l.Text.Contains("the same point steadies the Villain", StringComparison.Ordinal));

        Assert.Contains(rescued.Added, l =>
            string.Equals(l.Rule, "adversity_spend_anything_resolve_can", StringComparison.Ordinal)
            && l.SourceRef.Contains("p.85", StringComparison.Ordinal));

        // p.79's other purchase, on its own: a point stops the clock and moves no Health.
        var steadied = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.Stabilise));

        Assert.Equal(opened.Adversity - fatal.CostResolveToStabiliseImmediately, steadied.State.Adversity);
        Assert.Equal(0, steadied.State["villain"].Resolve);
        Assert.False(steadied.State["villain"].Dying);
        Assert.Equal(-5, steadied.State["villain"].CurrentHealth);

        Assert.Contains(steadied.Added, l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)
            && l.SourceRef.Contains("p.79", StringComparison.Ordinal)
            && l.Text.Contains("the GM spends 1 Adversity on the Villain to stabilise at once", StringComparison.Ordinal));

        Assert.Contains(steadied.Added, l =>
            string.Equals(l.Rule, "adversity_spend_anything_resolve_can", StringComparison.Ordinal)
            && l.SourceRef.Contains("p.85", StringComparison.Ordinal));

        // A Hero is not an NPC, for either of them.
        foreach (var purchase in new[] { ResolveSpend.AvoidFatalDamage, ResolveSpend.Stabilise })
        {
            var onAHero = encounter.Step(opened, new SpendAdversity(
                "hero", AdversitySpend.AnythingResolveCan, AsResolve: purchase));

            Assert.Equal(opened.Adversity, onAHero.State.Adversity);
            Assert.Equal(3, onAHero.State["hero"].Resolve);
            Assert.Equal(-5, onAHero.State["hero"].CurrentHealth);
            Assert.True(onAHero.State["hero"].Dying);
        }

        // And a Minion group has no Health, so it is at neither the threshold nor the clock — the
        // refusal is p.77's rather than "is not dying", which is a different thing to be told.
        foreach (var purchase in new[] { ResolveSpend.AvoidFatalDamage, ResolveSpend.Stabilise })
        {
            var onTheMob = encounter.Step(opened, new SpendAdversity(
                "mob", AdversitySpend.AnythingResolveCan, AsResolve: purchase));

            Assert.Equal(opened.Adversity, onTheMob.State.Adversity);

            Assert.Contains(onTheMob.Added, l =>
                string.Equals(l.Rule, "attacking_minions", StringComparison.Ordinal)
                && l.Text.Contains("no dying clock to stop", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// <b>A Minion refusal quotes the field it was derived from, so an entry that stops saying it is
    /// a throw and not a ledger line that lies.</b>
    ///
    /// <para>Each of the four refusals p.85's first purchase makes of a group of Minions is a claim
    /// about a printed page, and each makes the claim by interpolating the field it read — "p.77
    /// gives them no Health at all (minions_have_health is False)". <b>Nothing read the field back
    /// out.</b> An entry corrected the other way would have gone on refusing and printed its own
    /// contradiction: a line saying a group has no Health <em>because</em> the field says they have
    /// one. And <c>minions_act</c> is worse than a wrong sentence, because the order of action reads
    /// the same phrase — a group that had stopped acting last would have been refused a seized
    /// initiative on the grounds that the order would not move, while it would.</para>
    ///
    /// <para>Each half is driven against the shipped bytes first, or a throw would prove only that
    /// the substitution machinery works.</para>
    /// </summary>
    [Theory]
    [InlineData("\"minions_have_health\": false", "\"minions_have_health\": true",
        "minions_have_health", ResolveSpend.InstantRecovery)]
    [InlineData("\"minions_have_health\": false", "\"minions_have_health\": true",
        "minions_have_health", ResolveSpend.AvoidFatalDamage)]
    [InlineData("\"minions_have_health\": false", "\"minions_have_health\": true",
        "minions_have_health", ResolveSpend.Stabilise)]
    [InlineData("\"minions_have_an_edge\": false", "\"minions_have_an_edge\": true",
        "minions_have_an_edge", ResolveSpend.SeizeInitiative)]
    [InlineData("\"minions_act\": \"after everyone else\"", "\"minions_act\": \"first\"",
        "minions_act", ResolveSpend.SeizeInitiative)]
    public void AMinionRefusalWhoseEntryStoppedSayingItIsAThrow(
        string printed, string reworded, string field, ResolveSpend purchase)
    {
        var table = TableRules.Book with { FatalDamage = true };
        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        List<Combatant> Fight() =>
        [
            Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3, traits, ["toughness"]),
            Combatant.Minions("mob", "the Minions", threat: 4, groupSize: 4, "threat")
        ];

        SpendAdversity Buying() => new(
            "mob", AdversitySpend.AnythingResolveCan, AsResolve: purchase);

        // The control: against the shipped bytes the purchase is refused, on the ledger, quoting the
        // field this case is about.
        var shipped = new Encounter(_play, new SeededDice(23), table);
        var opened = shipped.Begin(Fight(), challengeLevel: 2);
        var refused = shipped.Step(opened, Buying());

        Assert.Equal(opened.Adversity, refused.State.Adversity);
        Assert.Contains(refused.Added, l =>
            l.Text.Contains("is a group of Minions", StringComparison.Ordinal));

        // And against an entry that has been turned round, the refusal is not made at all.
        var corrected = SubstitutedPlayRules.With(PlayRulesRepository.CombatFile, printed, reworded);
        var other = new Encounter(corrected, new SeededDice(23), table);
        var began = other.Begin(Fight(), challengeLevel: 2);

        var thrown = Assert.Throws<InvalidOperationException>(() => other.Step(began, Buying()));

        Assert.Contains(field, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("no longer says it", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>p.85's suppress-a-Flaw spend: the pool pays, the character carries it, and the ledger says
    /// the rest is the GM's.</b>
    ///
    /// <para>Half this rule is not a mechanic at all — nothing in <c>play/</c> makes a Flaw bite, so
    /// what the suppression saves the character from cannot reach the state and the line says so
    /// rather than announcing an effect nothing received. The other half is entirely stated in
    /// figures and every one of them is driven here: the price, the three eligible kinds, and the
    /// one purchase per character per issue.</para>
    ///
    /// <para><b>Nothing here rolls a die, and the dice source is what proves it.</b> One face is
    /// scripted and required to be untouched at the end: <see cref="ScriptedDice"/> throws when it
    /// runs out, so an engine that had started rolling for this purchase fails either way.</para>
    /// </summary>
    [Fact]
    public void TheGmsPointBuysAFlawOffAnNpcAndTheSuppressionIsRead()
    {
        var entry = _play.GetResolve("adversity_spend_suppress_flaw");
        var rule = entry.Spend!;

        // The controls on the data: a price, the three kinds the page names, and one per character.
        Assert.Equal(1, rule.CostAdversity);
        Assert.Equal(["Villain", "Foe", "Extra"], rule.EligibleCharacters);
        Assert.Equal(1, rule.LimitPerCharacterPerIssue);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 5, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 9, health: 20, traits, ["toughness"]);
        var robots = Combatant.Minions("robots", "the robots", threat: 5, groupSize: 4, "threat");

        var dice = new ScriptedDice(6);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain, robots], challengeLevel: 2);
        var opening = state.Adversity;

        // The controls on the fixture: there is a pool to spend, and nobody carries a suppression.
        Assert.True(opening >= rule.CostAdversity, $"the GM opened on {opening} Adversity");
        Assert.Null(state["villain"].SuppressedFlaw);

        // Four refusals, and none of them spends anything. The price the page prints...
        var priced = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.SuppressFlaw, Points: 2, Narration: "a hot temper"));

        Assert.Equal(opening, priced.State.Adversity);
        Assert.Null(priced.State["villain"].SuppressedFlaw);

        // ...the three kinds it buys for, which a Hero is not...
        var onAHero = encounter.Step(state, new SpendAdversity(
            "hero", AdversitySpend.SuppressFlaw, Narration: "a hot temper"));

        Assert.Equal(opening, onAHero.State.Adversity);
        Assert.Contains(onAHero.Added, l =>
            l.Text.Contains("is a Hero", StringComparison.Ordinal));

        // ...nor is a group of Minions, whom p.85 leaves off the list...
        var onMinions = encounter.Step(state, new SpendAdversity(
            "robots", AdversitySpend.SuppressFlaw, Narration: "a hot temper"));

        Assert.Equal(opening, onMinions.State.Adversity);
        Assert.Contains(onMinions.Added, l =>
            l.Text.Contains("is a Minion", StringComparison.Ordinal));

        // ...and a purchase that does not say which Flaw, which this engine holds none of.
        var unnamed = encounter.Step(state, new SpendAdversity("villain", AdversitySpend.SuppressFlaw));

        Assert.Equal(opening, unnamed.State.Adversity);
        Assert.Null(unnamed.State["villain"].SuppressedFlaw);

        // The purchase: one point out of the GM's pool, and the Villain carries what was named.
        var bought = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.SuppressFlaw, Narration: "a hot temper"));

        Assert.Equal(opening - rule.CostAdversity, bought.State.Adversity);
        Assert.Equal("a hot temper", bought.State["villain"].SuppressedFlaw);

        // The NPC's own pool was never touched — they have none, and that is the point of p.85.
        Assert.Equal(0, bought.State["villain"].Resolve);
        Assert.Equal(3, bought.State["hero"].Resolve);

        var line = bought.Added.Single(l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains("a hot temper", line.Text, StringComparison.Ordinal);
        Assert.Contains(rule.Duration!, line.Text, StringComparison.Ordinal);

        // And the line hands the half this engine cannot model back to the GM rather than claiming it.
        Assert.Contains("the GM's to narrate", line.Text, StringComparison.Ordinal);

        // The limit, driven: a second purchase against the same character buys nothing, whichever
        // Flaw it names — the page's sentence is about the character.
        var again = encounter.Step(bought.State, new SpendAdversity(
            "villain", AdversitySpend.SuppressFlaw, Narration: "a glass jaw"));

        Assert.Equal(bought.State.Adversity, again.State.Adversity);
        Assert.Equal("a hot temper", again.State["villain"].SuppressedFlaw);

        var refusal = again.Added.Single(l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Contains("already been bought out of a hot temper", refusal.Text, StringComparison.Ordinal);

        // <b>And both lines say which unit the limit was counted in, because the page's unit is not
        // the engine's.</b> p.85 allows one per character per *issue* and an issue is several
        // scenes; this engine counts over one fight, which is the permissive direction. The refusal
        // used to say "already been bought out of a hot temper *this issue*", which a GM would read
        // as a count that travels — and it does not.
        foreach (var said in new[] { line.Text, refusal.Text })
        {
            Assert.Contains("per character per issue", said, StringComparison.Ordinal);
            Assert.Contains("the count is per fight", said, StringComparison.Ordinal);
            Assert.Contains("keeping track across scenes is the GM's", said, StringComparison.Ordinal);
        }

        // And it is true rather than said: the next scene of the same issue starts the count again.
        var nextScene = encounter.Begin([hero, villain, robots], challengeLevel: 2);

        Assert.Null(nextScene["villain"].SuppressedFlaw);

        var second = encounter.Step(nextScene, new SpendAdversity(
            "villain", AdversitySpend.SuppressFlaw, Narration: "a glass jaw"));

        Assert.Equal(nextScene.Adversity - rule.CostAdversity, second.State.Adversity);
        Assert.Equal("a glass jaw", second.State["villain"].SuppressedFlaw);

        // The whole of that, and not one die was thrown.
        Assert.Equal(1, dice.Remaining);
    }

    /// <summary>
    /// <b>p.85's misfortune: the point moves, the GM's words go on the ledger, and nothing else in
    /// the fight is touched.</b>
    ///
    /// <para>The entry's own <c>ambiguity</c> says nothing here is mechanical — three examples, two
    /// prohibitions, and no roll, threshold, duration or means of resisting one anywhere on the
    /// page. So the claim this fixture makes is unusually flat and is the honest one: the pool falls
    /// by the printed price, the line carries what the GM said, and <b>every other part of the state
    /// is the same object it was</b>. That last assertion is the one that would catch an engine
    /// inventing a mechanic the book has not got.</para>
    ///
    /// <para>The refusal is driven too: a spend that does not say what the misfortune is buys
    /// nothing. And the end of the fixture is why there is no "nobody to throw it at" refusal — a
    /// fight with no Hero in it opens on no Adversity at all, so such a guard would be a branch no
    /// encounter can reach.</para>
    /// </summary>
    [Fact]
    public void AMisfortuneCostsThePoolAndRecordsTheGmsWordsAndNothingElse()
    {
        var entry = _play.GetResolve("adversity_spend_misfortune");
        var rule = entry.Spend!;

        // The controls on the data: a price, and the two things the page asks of a misfortune.
        Assert.Equal(1, rule.CostAdversity);
        Assert.True(rule.MustBeAChallengeNotAPunishment);
        Assert.True(rule.MustNotBeAPlotDevice);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 5, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 9, health: 20, traits, ["toughness"]);

        // Nothing here rolls, and the one scripted face is required to be untouched at the end.
        var dice = new ScriptedDice(6);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain], challengeLevel: 2);
        var opening = state.Adversity;

        Assert.True(opening >= rule.CostAdversity, $"the GM opened on {opening} Adversity");

        // Refused, with nothing spent: a purchase that does not say what the misfortune is.
        var unnamed = encounter.Step(state, new SpendAdversity("villain", AdversitySpend.Misfortune));

        Assert.Equal(opening, unnamed.State.Adversity);
        Assert.Contains(unnamed.Added, l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)
            && l.Text.Contains("does not say what it is", StringComparison.Ordinal));

        // The purchase.
        const string What = "the fire escape gives way under them";

        var bought = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.Misfortune, Narration: What));

        Assert.Equal(opening - rule.CostAdversity, bought.State.Adversity);

        var line = bought.Added.Single(l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains(What, line.Text, StringComparison.Ordinal);
        Assert.Contains("the Heroes on " + Combatant.HeroSide, line.Text, StringComparison.Ordinal);
        Assert.Contains("the GM's to narrate", line.Text, StringComparison.Ordinal);

        // <b>And nothing else moved — every property of the state, not the four somebody thought
        // of.</b> Same objects, not merely equal ones: an engine that had invented a mechanic for a
        // rule the page gives none to would have rebuilt one of them.
        //
        // Four `Assert.Same` calls and two `Assert.Equal`s used to stand here, and they left fifteen
        // of the twenty-one properties unwatched. A misfortune that rebuilt nine of the untouched
        // collections and cleared `LastAttack` — which is a Hero's reroll gone, a real consequence
        // and not a copy — passed this fixture and the whole of the rest of the suite. The walk is
        // reflective so that a property added later is watched the day it is added, which is the
        // half a hand-written list can never have.
        NothingButThePoolAndTheLedgerMoved(state, bought.State);

        // <b>And again with a roll on the table.</b> The walk above cannot see `LastAttack` in a
        // fight where nothing has been rolled yet, and that is the property whose loss is a
        // consequence rather than a copy: cleared, a Hero's reroll and their bought dice go with it.
        var rollingTraits = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["might"] = 6, ["toughness"] = 4
        };

        var puncher = Combatant.Villain("villain", "the Villain", edge: 9, health: 20, rollingTraits, ["toughness"]);
        var punched = Combatant.Hero("hero", "the Hero", edge: 5, health: 20, resolve: 3, rollingTraits, ["toughness"]);

        var rolling = new Encounter(_play, new SeededDice(31));
        var mid = rolling.Begin([punched, puncher], challengeLevel: 2);

        mid = rolling.Step(mid, new Attack("villain", "hero", "might")).State;

        // The control: there is an attack to lose.
        Assert.NotNull(mid.LastAttack);

        var afterwards = rolling.Step(mid, new SpendAdversity(
            "villain", AdversitySpend.Misfortune, Narration: What));

        Assert.Equal(mid.Adversity - rule.CostAdversity, afterwards.State.Adversity);
        NothingButThePoolAndTheLedgerMoved(mid, afterwards.State);

        static void NothingButThePoolAndTheLedgerMoved(EncounterState before, EncounterState after)
        {
            var properties = typeof(EncounterState).GetProperties()
                .Where(p => p.GetIndexParameters().Length == 0)
                .ToList();

            // The control on the walk: it really did look at the parts a mechanic would have moved.
            var names = properties.Select(p => p.Name).ToList();

            Assert.Contains(nameof(EncounterState.Combatants), names, StringComparer.Ordinal);
            Assert.Contains(nameof(EncounterState.LastAttack), names, StringComparer.Ordinal);
            Assert.Contains(nameof(EncounterState.Holds), names, StringComparer.Ordinal);
            Assert.True(names.Count >= 20, $"only {names.Count} properties were walked.");

            var moved = properties
                .Where(p => !string.Equals(p.Name, nameof(EncounterState.Adversity), StringComparison.Ordinal))
                .Where(p => !string.Equals(p.Name, nameof(EncounterState.Ledger), StringComparison.Ordinal))
                .Where(p => !Unmoved(p.GetValue(before), p.GetValue(after)))
                .Select(p => p.Name)
                .ToList();

            Assert.True(moved.Count == 0,
                "p.85 gives a misfortune no roll, no threshold and no duration, so the pool and the "
                + "ledger are the whole of what it may move — and this one moved "
                + string.Join(", ", moved.Order(StringComparer.Ordinal)) + ".");
        }

        // A value is unmoved when it is the same object, or — for the figures — the same figure.
        static bool Unmoved(object? before, object? after) =>
            before is null || after is null
                ? ReferenceEquals(before, after)
                : before.GetType().IsValueType
                    ? before.Equals(after)
                    : ReferenceEquals(before, after);

        // <b>And a fight with nobody to throw one at cannot buy one, without a guard for it.</b>
        // The opening pool is a point per Hero plus the Challenge Level times the same number, so a
        // fight with no Hero in it opens on nothing and the spend is refused for want of a point.
        // A "there is nobody to throw it at" refusal would be a branch no encounter can reach,
        // which is why there is not one — and this is what makes that claim true rather than said.
        var foe = Combatant.Foe("foe", "the Foe", edge: 4, health: 8, traits, ["toughness"], side: "third");
        var villainsOnly = encounter.Begin([villain, foe], challengeLevel: 3);

        Assert.Equal(0, villainsOnly.Adversity);

        var nobody = encounter.Step(villainsOnly, new SpendAdversity(
            "villain", AdversitySpend.Misfortune, Narration: What));

        Assert.Equal(0, nobody.State.Adversity);
        Assert.Contains(nobody.Added, l =>
            l.Text.Contains("the GM has 0 Adversity", StringComparison.Ordinal));

        Assert.Equal(1, dice.Remaining);
    }

    /// <summary>
    /// <b>p.85's act of villainy: once per story, only a Villain, and the act itself is the GM's.</b>
    ///
    /// <para>"Anything necessary to advance the story" is not a mechanic and this engine has no
    /// story to advance, so what the Villain does never reaches the state and the line hands it
    /// back. Every figure the page does print is driven here: the price, the one kind it applies to
    /// against the two it names as excluded, and the once-per-story limit — which this engine counts
    /// over the encounter, because a story is a unit the chapter defines nowhere and a fight is the
    /// largest thing it can see.</para>
    ///
    /// <para>Nothing rolls, and the one scripted face is required to be untouched at the end.</para>
    /// </summary>
    [Fact]
    public void AnActOfVillainyIsOncePerStoryAndOnlyAVillainCanBeHandedOne()
    {
        var entry = _play.GetResolve("adversity_spend_villainy");
        var rule = entry.Spend!;

        // The controls on the data: a price, one eligible kind, two excluded, one act, no roll.
        Assert.Equal(1, rule.CostAdversity);
        Assert.Equal(["Villain"], rule.EligibleCharacters);
        Assert.Equal(["Foe", "Minion"], rule.ExcludedCharacters);
        Assert.Equal(1, rule.LimitPerStory);
        Assert.True(rule.Automatic);

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 5, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 9, health: 20, traits, ["toughness"]);
        var other = Combatant.Villain("other", "the other Villain", edge: 8, health: 20, traits, ["toughness"]);
        var foe = Combatant.Foe("foe", "the Foe", edge: 4, health: 8, traits, ["toughness"]);
        var robots = Combatant.Minions("robots", "the robots", threat: 5, groupSize: 4, "threat");

        var dice = new ScriptedDice(6);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain, other, foe, robots], challengeLevel: 3);
        var opening = state.Adversity;

        // The controls on the fixture: a pool to spend, and no act spent yet.
        Assert.True(opening >= 2, $"the GM opened on {opening} Adversity");
        Assert.Empty(state.Villainy);

        const string Act = "throws the switch and floods the lower deck";

        // Refused, with nothing spent: the printed price...
        var priced = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.Villainy, Points: 2, Narration: Act));

        Assert.Equal(opening, priced.State.Adversity);
        Assert.Empty(priced.State.Villainy);

        // ...a Foe, whom the page names as lacking what it takes...
        var onAFoe = encounter.Step(state, new SpendAdversity(
            "foe", AdversitySpend.Villainy, Narration: Act));

        Assert.Equal(opening, onAFoe.State.Adversity);
        Assert.Contains(onAFoe.Added, l =>
            l.Text.Contains("Foes and Minions lack what it takes", StringComparison.Ordinal)
            && l.Text.Contains("is a Foe", StringComparison.Ordinal));

        // ...a group of Minions, likewise...
        var onMinions = encounter.Step(state, new SpendAdversity(
            "robots", AdversitySpend.Villainy, Narration: Act));

        Assert.Equal(opening, onMinions.State.Adversity);
        Assert.Contains(onMinions.Added, l => l.Text.Contains("is a Minion", StringComparison.Ordinal));

        // ...and one that does not say what the act is, which is the whole of what it buys.
        var unnamed = encounter.Step(state, new SpendAdversity("villain", AdversitySpend.Villainy));

        Assert.Equal(opening, unnamed.State.Adversity);
        Assert.Empty(unnamed.State.Villainy);

        // The purchase: one point, and the story's one act is spent.
        var bought = encounter.Step(state, new SpendAdversity(
            "villain", AdversitySpend.Villainy, Narration: Act));

        Assert.Equal(opening - rule.CostAdversity, bought.State.Adversity);
        Assert.Equal(["villain"], bought.State.Villainy);
        Assert.Equal(0, bought.State["villain"].Resolve);

        var line = bought.Added.Single(l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains(Act, line.Text, StringComparison.Ordinal);
        Assert.Contains("automatically and with no roll", line.Text, StringComparison.Ordinal);
        Assert.Contains("the GM's to narrate", line.Text, StringComparison.Ordinal);

        // The limit, driven twice over: the same Villain again, and a different one. p.85's sentence
        // is about the story and not about the character, so both refuse.
        var again = encounter.Step(bought.State, new SpendAdversity(
            "villain", AdversitySpend.Villainy, Narration: "grabs a hostage"));

        Assert.Equal(bought.State.Adversity, again.State.Adversity);
        Assert.Equal(["villain"], again.State.Villainy);
        Assert.Contains(again.Added, l =>
            l.Text.Contains("act of villainy per story", StringComparison.Ordinal));

        var someoneElse = encounter.Step(bought.State, new SpendAdversity(
            "other", AdversitySpend.Villainy, Narration: "escapes down the service tunnel"));

        Assert.Equal(bought.State.Adversity, someoneElse.State.Adversity);
        Assert.Equal(["villain"], someoneElse.State.Villainy);

        Assert.Equal(1, dice.Remaining);
    }

    /// <summary>
    /// <b>A narration of nothing but spaces is a spend that does not say what it bought, and all
    /// three of p.85's own purchases refuse it with nothing spent.</b>
    ///
    /// <para>Each of the three guards its narration with a length test, and a length test is not a
    /// content test. A string of spaces is longer than nothing and says less: the misfortune and the
    /// villainy both took the point and wrote a ledger line with an empty pair of quotes in it —
    /// which is precisely the pool that has moved with no words behind it those refusals exist to
    /// prevent — and the suppression threw <see cref="ArgumentException"/> out of
    /// <see cref="Encounter.Step"/>, because <see cref="Combatant.Suppressing"/> guards on whitespace
    /// where its caller guarded on length. That throw is the shape this engine has already been
    /// through once: a purchase the rules refuse is a ledger line, not an exception, and a run that
    /// dies on one has no verdict at all.</para>
    ///
    /// <para>The control is the other half: the same three spends, in the same fight, with words in
    /// them, all bought. A guard that refused everything would satisfy the refusals alone.</para>
    /// </summary>
    [Fact]
    public void ASpendWhoseNarrationIsOnlySpacesIsRefusedAndNotCharged()
    {
        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 5, health: 20, resolve: 3, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 9, health: 20, traits, ["toughness"]);

        var dice = new ScriptedDice(6);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain], challengeLevel: 3);
        var opening = state.Adversity;

        // The control on the fixture: the pool covers all three, so a refusal below is the
        // narration's and not "the GM has 0 Adversity".
        Assert.True(opening >= 3, $"the GM opened on {opening} Adversity");

        AdversitySpend[] narrated =
            [AdversitySpend.SuppressFlaw, AdversitySpend.Misfortune, AdversitySpend.Villainy];

        foreach (var kind in narrated)
        {
            var blank = encounter.Step(state, new SpendAdversity("villain", kind, Narration: "   "));

            Assert.True(opening == blank.State.Adversity,
                $"{kind} charged the pool for a narration of nothing but spaces.");

            Assert.Null(blank.State["villain"].SuppressedFlaw);
            Assert.Empty(blank.State.Villainy);

            // And the refusal is on the ledger rather than in an exception, in the words the spend's
            // own missing-narration refusal uses.
            Assert.Contains(blank.Added, l => l.Text.Contains("does not say", StringComparison.Ordinal));
        }

        // The control: with words in them, every one of the three is bought.
        foreach (var kind in narrated)
        {
            var said = encounter.Step(state, new SpendAdversity(
                "villain", kind, Narration: "the floor gives way"));

            Assert.True(opening - 1 == said.State.Adversity,
                $"{kind} refused a narration that says something.");
        }

        Assert.Equal(1, dice.Remaining);
    }

    /// <summary>
    /// <b>An odd pool banking automatic successes keeps the even half and nothing for the leftover
    /// die.</b>
    ///
    /// <para>p.67 prices the offer in pairs and its printed example is 12d, which is even and settles
    /// nothing; the entry's own `ambiguity` says so. Integer division is the reading that never gives
    /// a character more than the page promises, and it had no fixture — the arm-wrestling example
    /// only ever banks an even pool.</para>
    /// </summary>
    [Fact]
    public void AnOddPoolBanksTheEvenHalfAndNoMore()
    {
        var counter = new SuccessCounter(_play);
        var rate = _play.GetMeta("automatic_successes").AutomaticSuccesses!.DicePerSuccess;

        // The control: the rate is the entry's, and the printed example is the even case.
        Assert.Equal(2, rate);
        Assert.Equal(6, counter.AutomaticSuccesses(12));

        // The odd cases: the leftover die buys nothing, in either direction.
        Assert.Equal(5, counter.AutomaticSuccesses(11));
        Assert.Equal(5, counter.AutomaticSuccesses(10));
        Assert.Equal(0, counter.AutomaticSuccesses(1));
        Assert.Equal(0, counter.AutomaticSuccesses(0));

        // And rounding the other way would give six for eleven, which is the reading this is not.
        Assert.NotEqual((int)Math.Ceiling(11 / 2.0), counter.AutomaticSuccesses(11));
    }

    // ── The dice ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>What may be asserted about <see cref="SeededDice"/>, and what may not.</b>
    ///
    /// <para><c>Random</c>'s seeded sequence is deterministic for a given .NET implementation and
    /// explicitly not across versions or platforms — the algorithm changed at .NET 6. So a checked-in
    /// ledger, report or Health total produced from a seed is a check that passes on the machine that
    /// wrote it and goes red on the next runtime the CI image picks up, for a reason nobody can
    /// reproduce locally. There is no such golden in this repository and this fixture is the reason
    /// there will not be one: it asserts only what is true of any d6 stream.</para>
    ///
    /// <para>Faces are in range; a seed repeats within one run; different seeds diverge; and every
    /// face turns up across enough throws, which is the loosest possible statement that a source
    /// returning a constant would fail.</para>
    /// </summary>
    [Fact]
    public void SeededDiceAreDiceAndNothingIsGoldenAboutThem()
    {
        var faces = new SeededDice(101).Roll(6000);

        // In range, and the count asked for.
        Assert.Equal(6000, faces.Length);
        Assert.All(faces, face => Assert.InRange(face, 1, 6));

        // Every face turns up. A source returning a constant, or one missing a face, fails here —
        // and nothing about the *order* is asserted, which is what keeps this runtime-independent.
        foreach (var face in Enumerable.Range(1, 6))
        {
            var count = faces.Count(f => f == face);

            Assert.True(count > 0, $"6000 throws produced no {face}s at all");

            // A very wide band: half to double the expected share. This is a smoke test for a broken
            // generator, not a claim about the quality of one.
            Assert.InRange(count, 500, 2000);
        }

        // The same seed repeats within a run, which is what makes a balance figure reproducible...
        Assert.Equal(new SeededDice(101).Roll(50), new SeededDice(101).Roll(50));

        // ...and a different seed does not, which is what makes a spread of seeds worth running.
        Assert.NotEqual(new SeededDice(101).Roll(50), new SeededDice(102).Roll(50));

        // The seed and the throw count are properties, because a figure without its seed is a figure
        // nobody can reproduce.
        var counted = new SeededDice(7);
        counted.Roll(9);

        Assert.Equal(7, counted.Seed);
        Assert.Equal(9, counted.Thrown);
    }

    // ── The unimplemented list ───────────────────────────────────────────────

    /// <summary>The guide the two lists below are a claim about.</summary>
    private static string Guide() =>
        File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "docs", "guide", "play-engine.md"));

    /// <summary>
    /// The first column of the markdown table under <paramref name="heading"/>, as the backticked
    /// names in it.
    /// </summary>
    private static HashSet<string> ListedUnder(string heading)
    {
        var guide = Guide();
        var at = guide.IndexOf(heading, StringComparison.Ordinal);

        Assert.True(at >= 0, $"docs/guide/play-engine.md no longer contains \"{heading}\".");

        var rest = guide[at..];
        var table = rest.IndexOf("|---|", StringComparison.Ordinal);

        Assert.True(table >= 0, $"no table follows \"{heading}\" in the guide.");

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in rest[table..].Split('\n').Skip(1))
        {
            if (!line.StartsWith('|')) break;

            var first = line.Split('|')[1].Trim();
            if (first.StartsWith('`') && first.EndsWith('`')) names.Add(first.Trim('`'));
        }

        return names;
    }

    /// <summary>
    /// <b>The guide's not-applied lists and the engine's are the same lists.</b>
    ///
    /// <para>The guide's account of what is unimplemented is a claim about the code, and a claim
    /// nothing checks is a claim that goes stale — this branch had already shipped a guide saying
    /// Fatal Damage was applied when half of it was not. Both directions are checked: an entry the
    /// engine lists and the guide does not, and one the guide lists that the engine has quietly
    /// implemented since.</para>
    ///
    /// <para><b><c>EntriesNotYetApplied</c> is empty, and an empty-equals-empty comparison proves
    /// nothing</b> — so the entries half branches on the engine's own list. It used to assert the
    /// set was empty and then require the guide to carry the words "is empty", which is a guard
    /// that reads the document and never reads the engine into it: <b>adding one id to
    /// <see cref="Encounter.EntriesNotYetApplied"/> left both document assertions green</b>, because
    /// a document nobody had changed still said what it had always said. Only the
    /// <see cref="Assert.Empty{T}(System.Collections.Generic.IEnumerable{T})"/> above them moved,
    /// which is a fact about the code and not the two-way agreement this test is named for.</para>
    ///
    /// <para>Now the guide's obligation depends on the list: empty means the sentence and no table,
    /// and non-empty means a table naming exactly the ids — so the id the mutation adds is one the
    /// parse cannot find, and both directions of the disagreement are red. The switches table stays
    /// non-empty and is what keeps the parse itself honest: a <see cref="ListedUnder"/> that had
    /// stopped finding rows would fail on it rather than pass in silence on both.</para>
    /// </summary>
    [Fact]
    public void TheGuidesNotAppliedListsAreTheEnginesNotAppliedLists()
    {
        // The control: the parse found a table with rows in it. Both halves below lean on this one
        // working, and the entries half cannot supply its own while the set is empty.
        var switches = ListedUnder("**`Encounter.SwitchesNotYetApplied`**");

        Assert.NotEmpty(switches);

        Assert.Equal(
            Encounter.SwitchesNotYetApplied.Order(StringComparer.Ordinal),
            switches.Order(StringComparer.Ordinal));

        if (Encounter.EntriesNotYetApplied.Count == 0)
        {
            // Nothing is unapplied, so the guide carries no table of unapplied entries — and it
            // still has to name the field, or a reader has no way to tell "empty" from "this
            // document has stopped tracking it".
            Assert.Contains(
                "`Encounter.EntriesNotYetApplied` is empty", Guide(), StringComparison.Ordinal);

            Assert.DoesNotContain(
                "**`Encounter.EntriesNotYetApplied`**", Guide(), StringComparison.Ordinal);

            return;
        }

        // Something is unapplied, so the guide names it in a table and stops saying there is
        // nothing to name.
        Assert.DoesNotContain(
            "`Encounter.EntriesNotYetApplied` is empty", Guide(), StringComparison.Ordinal);

        Assert.Equal(
            Encounter.EntriesNotYetApplied.Order(StringComparer.Ordinal),
            ListedUnder("**`Encounter.EntriesNotYetApplied`**").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>Every entry the engine says it does not apply is an entry that exists.</b>
    ///
    /// <para>Kept as its own fact rather than folded into the check above, because the set is empty
    /// today and the loop would run zero times: what makes it worth anything is the control beside
    /// it, which proves the lookup it would use can actually fail an id that is not there.</para>
    /// </summary>
    [Fact]
    public void EveryEntryTheEngineDoesNotApplyIsAnEntryThatExists()
    {
        var known = _play.EntryIds().Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        // The control: the instrument really can tell a real id from an invented one.
        Assert.Contains("modifier_cover", known, StringComparer.Ordinal);
        Assert.DoesNotContain("modifier_moonlight", known, StringComparer.Ordinal);

        foreach (var id in Encounter.EntriesNotYetApplied)
            Assert.True(known.Contains(id), $"the engine lists '{id}', which is in none of the five files");
    }

    /// <summary>
    /// <b>Every purchase resolves, and nothing refuses by name any more.</b>
    ///
    /// <para>The list above is a static field, and a static field is a claim like any other: it stays
    /// true until somebody implements one of the things on it and forgets. So every member of both
    /// spend enums is driven through <see cref="Encounter.Step"/> and sorted by what actually
    /// happened — a purchase that refuses must be on the list, and one that does something must not
    /// be.</para>
    ///
    /// <para><b>The refusal half is now empty, and it is asserted empty rather than dropped.</b>
    /// p.85's three own purchases were the last spends on the list; what is left there is the three
    /// situational modifiers, which no intent can reach. So the check that used to be "something
    /// refused" becomes the stronger one in both directions: every purchase resolved, and any that
    /// starts refusing goes red here with a message saying it has to be added to the list and to the
    /// two documents that publish it.</para>
    ///
    /// <para><b>The classifier's positive control has moved to the Gear Limit switches, and the
    /// move is the whole point of writing it down.</b> It used to be p.85's first purchase naming
    /// one of the four Chapter 4 spends that still charged the buyer's own pool — and those four go
    /// through the GM's pool now, so <em>no spend of either enum can produce that phrase any more</em>
    /// and the sort below would be measuring an instrument nobody had checked. The two-valued case
    /// that is left is <see cref="Encounter.SwitchesNotYetApplied"/>: a run with
    /// <c>RaisedGearLimit</c> on says <c>not yet implemented</c> on page one and a run with an
    /// applied setting on does not, so the same substring test is watched answering both ways in the
    /// same file. Delete that and this test goes green whatever the engine does.</para>
    /// </summary>
    [Fact]
    public void EveryPurchaseEitherRefusesByNameOrResolves()
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 9,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 5 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 5 },
            ["toughness"]);

        var refused = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new HashSet<ResolveSpend>();

        foreach (var kind in Enum.GetValues<ResolveSpend>())
        {
            var encounter = new Encounter(_play, new SeededDice(21), TableRules.Book with { FatalDamage = true });
            var state = encounter.Begin([hero, villain]);

            // Give the purchases that need one a roll to work on.
            state = encounter.Step(state, new Attack("hero", "villain", "might")).State;

            var step = encounter.Step(state, new SpendResolve("hero", kind));

            if (step.Added.Any(l => l.Text.Contains("not yet implemented", StringComparison.Ordinal)))
            {
                foreach (var line in step.Added.Where(l =>
                             l.Text.Contains("not yet implemented", StringComparison.Ordinal)))
                {
                    refused.Add(line.Rule);
                }
            }
            else
            {
                resolved.Add(kind);
            }
        }

        var gm = new HashSet<AdversitySpend>();

        foreach (var kind in Enum.GetValues<AdversitySpend>())
        {
            var encounter = new Encounter(_play, new SeededDice(21), TableRules.Book with { FatalDamage = true });
            var state = encounter.Begin([hero, villain], challengeLevel: 3);

            state = encounter.Step(state, new Attack("hero", "villain", "might")).State;

            // Each is handed what its own rule asks for, so a purchase that ends up in neither pile
            // did so because the rules refused it and not because an argument was missing.
            var step = encounter.Step(state, new SpendAdversity(
                "villain", kind,
                AsResolve: kind == AdversitySpend.AnythingResolveCan ? ResolveSpend.ExtraDice : null,
                Narration: "a hot temper"));

            foreach (var line in step.Added.Where(l =>
                         l.Text.Contains("not yet implemented", StringComparison.Ordinal)))
            {
                refused.Add(line.Rule);
            }

            gm.Add(kind);
        }

        // The control: every purchase in both enums was driven and every one of them resolved.
        Assert.Equal(Enum.GetValues<ResolveSpend>().Order(), resolved.Order());
        Assert.Equal(Enum.GetValues<AdversitySpend>().Order(), gm.Order());

        Assert.True(
            refused.Count == 0,
            "these entries refuse as not yet implemented: " + string.Join(", ", refused.Order(StringComparer.Ordinal))
            + ". No spend does any more, so a new one has to be added to Encounter.EntriesNotYetApplied, "
            + "to docs/guide/play-engine.md and to mcp-play/PLAY-POLICY.md before this can pass.");

        // <b>The classifier's own control, and it is no longer a spend.</b> Every purchase the GM's
        // pool may name is bought, so nothing either enum can produce carries that phrase any more —
        // and a substring test that can only ever answer one way is an instrument nobody has
        // checked. The two-valued case left in this engine is the Gear Limit switch, announced on
        // page one, so the same test is driven over both answers here.
        var unapplied = new Encounter(_play, new SeededDice(21), TableRules.Book with { RaisedGearLimit = true })
            .Begin([hero, villain]);

        var applied = new Encounter(_play, new SeededDice(21), TableRules.Book with { FatalDamage = true })
            .Begin([hero, villain]);

        Assert.Contains(unapplied.Ledger.Lines, l =>
            l.Text.Contains("not yet implemented", StringComparison.Ordinal));

        Assert.DoesNotContain(applied.Ledger.Lines, l =>
            l.Text.Contains("not yet implemented", StringComparison.Ordinal));

        // And the settings really did both go on, or the pair above is two silent runs agreeing.
        Assert.Contains(applied.Ledger.Lines, l =>
            l.Text.Contains("table setting FatalDamage is on", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A mob cannot all reach one target, and the size bonus follows how many can.</b>
    ///
    /// <para><c>minions_attacking</c>'s two per-target caps were modelled and never read, so twenty
    /// Minions piled onto one character in close combat and collected the bonus for twenty. The caps
    /// are read now, and the bonus is taken from the table row the capped number falls in.</para>
    ///
    /// <para>The ledger line beside it used to say the bonus applies "on the attack roll and nothing
    /// else", which claimed a clause the engine does not apply — the entry's own `ambiguity` says the
    /// page offers no mechanism for it. The line names it as not yet implemented instead.</para>
    /// </summary>
    [Fact]
    public void AMobIsCappedAtWhatCanReachOneTargetAndTheLineDoesNotOverclaim()
    {
        var rule = _play.GetCombat("minions_attacking").MinionsAttacking!;
        var table = _play.GetCombat("minion_group_attack_table").MinionGroupAttack!;

        // The controls on the data: the two caps differ, so the fixture can tell them apart.
        Assert.Equal(6, rule.MaximumAttackingOneTargetInCloseCombat);
        Assert.Equal(12, rule.MaximumAttackingOneTargetAtRange);

        var expectedClose = table.Single(r => 6 >= r.MinMinions && 6 <= r.MaxMinions).BonusDice;
        var expectedRanged = table.Single(r => 12 >= r.MinMinions && 12 <= r.MaxMinions).BonusDice;
        Assert.NotEqual(expectedClose, expectedRanged);

        var minions = Combatant.Minions("minions", "the Minions", threat: 4, groupSize: 20, "threat");

        var target = Combatant.Hero("target", "the target", edge: 5, health: 40, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4, ["might"] = 3 },
            ["toughness"]);

        Assert.Equal(4 + expectedClose, PoolAt(RangeBand.Close));
        Assert.Equal(4 + expectedRanged, PoolAt(RangeBand.Distant));

        int PoolAt(RangeBand band)
        {
            var encounter = new Encounter(_play, new SeededDice(22));
            var opening = encounter.Begin([minions, target], opening: band);

            var turn = opening with { TurnIndex = opening.TurnOrder.ToList().IndexOf("minions") };
            var added = encounter.Step(turn, new Attack("minions", "target", "threat")).Added;

            // The cap is announced, and the bonus line no longer claims the exclusion is applied.
            Assert.Contains(added, l =>
                string.Equals(l.Rule, "minions_attacking", StringComparison.Ordinal)
                && l.Text.Contains("cannot all reach one target", StringComparison.Ordinal));

            var bonus = added.Single(l =>
                string.Equals(l.Rule, "minion_group_attack_table", StringComparison.Ordinal));

            Assert.Contains("is not yet implemented", bonus.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("and nothing else", bonus.Text, StringComparison.Ordinal);

            var sentence = added.Single(l =>
                string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                && l.Text.Contains("attacks", StringComparison.Ordinal));

            return int.Parse(
                sentence.Text.Split("with threat ")[1].Split('d')[0],
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // ── The order of action ──────────────────────────────────────────────────

    /// <summary>
    /// <b>The tie-break ladder is the entry's, and Minions are on none of its rungs.</b>
    ///
    /// <para>p.73 breaks a tie on Edge by what kind of character each one is, and says Minions have
    /// no Edge at all and act after everyone else. The engine used to map a Minion group onto the
    /// "extras" rung — a claim the entry does not make, and one that happened not to matter only
    /// because the Minions-last sort ran first. It sorts them after the whole ladder now, which is
    /// the same answer honestly spelled.</para>
    ///
    /// <para>That the ladder is read at all is proved with a twin: reordering the entry's own
    /// <c>order</c> reorders the fight. Without it this fixture would be a second transcription of
    /// the ladder agreeing with the first.</para>
    /// </summary>
    [Fact]
    public void TheOrderOfActionComesFromTheLadderAndPutsMinionsAfterAllOfIt()
    {
        var tie = _play.GetCombat("edge_ties").TieBreak!;

        // The controls on the data.
        Assert.False(tie.MinionsHaveAnEdge);
        Assert.Contains("after everyone else", tie.MinionsAct, StringComparison.Ordinal);

        // Everybody on the same Edge, so the ladder is the only thing separating them — and the ids
        // are chosen to sort the other way round, so an engine falling through to the id tie-break
        // gives a different answer.
        List<Combatant> Cast() =>
        [
            Combatant.Minions("a_minions", "the Minions", threat: 4, groupSize: 3, "threat"),
            Combatant.Extra("b_extra", "the Extra", edge: 0, health: 6,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 3 }, ["toughness"]),
            Combatant.Foe("c_foe", "the Foe", edge: 0, health: 6,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 3 }, ["toughness"]),
            Combatant.Villain("d_villain", "the Villain", edge: 0, health: 6,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 3 }, ["toughness"]),
            Combatant.Hero("e_hero", "the Hero", edge: 0, health: 6, resolve: 0,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 3 }, ["toughness"])
        ];

        var shipped = new Encounter(_play, new SeededDice(20)).Begin(Cast());

        Assert.Equal(["e_hero", "d_villain", "c_foe", "b_extra", "a_minions"], shipped.TurnOrder);

        // The twin: swap the top two rungs and the fight reorders. An engine with the ladder typed
        // into it would come back with the same answer.
        var swapped = SubstitutedPlayRules.With(
            PlayRulesRepository.CombatFile,
            "\"heroes\",\n          \"villains\"",
            "\"villains\",\n          \"heroes\"");

        var reordered = new Encounter(swapped, new SeededDice(20)).Begin(Cast());

        Assert.Equal(["d_villain", "e_hero", "c_foe", "b_extra", "a_minions"], reordered.TurnOrder);

        // And the Minions stay last through the reordering, because they are not on the ladder at
        // all — which is the half the "extras" mapping was quietly claiming otherwise.
        Assert.Equal("a_minions", reordered.TurnOrder[^1]);
    }

    // ── Grappling ────────────────────────────────────────────────────────────

    /// <summary>Three characters: a grappler, somebody to grapple, and a bystander to be dodged.</summary>
    private static List<Combatant> Wrestlers() =>
    [
        Combatant.Villain("holder", "the holder", edge: 10, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 10, ["agility"] = 8, ["toughness"] = 4
            },
            ["agility", "toughness"]),

        Combatant.Hero("held", "the held Hero", edge: 8, health: 20, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 10, ["agility"] = 8, ["toughness"] = 4
            },
            ["agility", "toughness"]),

        Combatant.Villain("bystander", "the bystander", edge: 6, health: 20,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 },
            ["toughness"])
    ];

    /// <summary>
    /// The same three, with the sword in the hand p.76 aims a grab at.
    ///
    /// <para><b>A grab needs an opponent who has something to take</b>, and this is how a caller
    /// says so — <see cref="Combatant.Carrying"/> is the wire's <c>holding</c> field. Every fixture
    /// below that means a grab to <em>land</em> opens from here; the ones that mean it to be refused
    /// open from <see cref="Wrestlers"/>.</para>
    /// </summary>
    private static List<Combatant> ArmedWrestlers() =>
        [.. Wrestlers().Select(c =>
            string.Equals(c.Id, "held", StringComparison.Ordinal) ? c.Carrying("the sword") : c)];

    /// <summary>
    /// <b>A grab is stored as a grab and a hold as a hold, in both bands.</b>
    ///
    /// <para>The engine keyed on the word "full" in p.76's table and wrote every result down as a
    /// hold, so a full grab — which the page defines as control of an <em>object</em> — became
    /// control of a person. The four rows are driven here through the table itself, with the net
    /// successes each band needs.</para>
    /// </summary>
    [Theory]
    [InlineData(GrappleMove.Grab, 5, GrappleKind.Full)]
    [InlineData(GrappleMove.Grab, 2, GrappleKind.Partial)]
    [InlineData(GrappleMove.Hold, 5, GrappleKind.Full)]
    [InlineData(GrappleMove.Hold, 2, GrappleKind.Partial)]
    public void AGrappleIsStoredAsTheMoveItWas(GrappleMove move, int net, GrappleKind expected)
    {
        // The control: the band this fixture is aiming at is the band the shipped table names.
        var row = _play.GetCombat("grappling_table").GrapplingTable!.Single(r =>
            (r.MinNetSuccesses is null || net >= r.MinNetSuccesses)
            && (r.MaxNetSuccesses is null || net <= r.MaxNetSuccesses));

        var printed = move == GrappleMove.Grab ? row.Grab : row.Hold;
        Assert.Contains(expected.ToString().ToLowerInvariant(), printed, StringComparison.Ordinal);

        var dice = new ScriptedDice([.. FacesFor(10, net), .. FacesFor(10, 0)]);
        var encounter = new Encounter(_play, dice);

        // p.76's grab is aimed at an object, so one has to be named or nothing is rolled — see
        // AGrabThatNamesNoItemIsRefusedBeforeAnythingIsRolled — and the target has to be holding it,
        // which is what ArmedWrestlers says. See
        // AGrabForSomethingTheTargetIsNotHoldingIsRefusedBeforeAnythingIsRolled.
        var item = move == GrappleMove.Grab ? "the sword" : null;

        var step = encounter.Step(
            encounter.Begin(ArmedWrestlers()), new GrappleIntent("holder", "held", move, item));

        Assert.Equal(0, dice.Remaining);

        var grapple = Assert.Single(step.State.Grapples);

        Assert.Equal(move, grapple.Move);
        Assert.Equal(expected, grapple.Kind);
        Assert.Equal("holder", grapple.Holder);
        Assert.Equal("held", grapple.Held);

        // And the object is on the record for a grab and on nothing else: a hold is aimed at a
        // person, so an item field carrying anything there would be a fact about the wrong move.
        Assert.Equal(item, grapple.Item);
    }

    /// <summary>
    /// <b>Each band of p.76 does what the page says it does, to both characters.</b>
    ///
    /// <para>Four things were wrong at once. A full grab immobilised the character who lost the item,
    /// which is a rule about an object applied to a person. A partial grab and a partial hold each
    /// say active defences are gone "against anyone else" and neither was applied, so a character
    /// wrestling one opponent dodged a third as though nothing were happening. A partial hold leaves
    /// both characters one physical action and both were free to do anything. And a full hold leaves
    /// its loser "only trying to escape" while the engine let them attack somebody across the
    /// room.</para>
    ///
    /// <para>Each row asserts against the party it is about — the dodge the character keeps as well
    /// as the one they lose — so an engine that had simply switched every active defence off would
    /// fail here rather than pass.</para>
    /// </summary>
    [Fact]
    public void EachGrappleBandDoesWhatThePageSaysToBothCharacters()
    {
        // A full grab restrains nobody: p.76 gives the winner the item, not the loser's balance.
        var fullGrab = InGrapple(GrappleMove.Grab, GrappleKind.Full);
        Assert.Contains("defends with agility", DefenceOf(fullGrab, "held", "bystander"), StringComparison.Ordinal);
        Assert.True(MayAct(fullGrab, "held"));

        // A partial grab: still dodging the character they are wrestling, and nobody else.
        var partialGrab = InGrapple(GrappleMove.Grab, GrappleKind.Partial);
        Assert.Contains("defends with agility", DefenceOf(partialGrab, "held", "holder"), StringComparison.Ordinal);
        Assert.Contains("defends with toughness", DefenceOf(partialGrab, "held", "bystander"), StringComparison.Ordinal);
        Assert.Contains("defends with toughness", DefenceOf(partialGrab, "holder", "bystander"), StringComparison.Ordinal);

        // ...and it does not stop either of them acting: the page has them fighting over an item,
        // not pinned.
        Assert.True(MayAct(partialGrab, "held"));
        Assert.True(MayAct(partialGrab, "holder"));

        // A partial hold: the same loss of defences, and one physical action each.
        // (There is no "against each other" case to check here: a partial hold leaves neither of
        // them able to attack the other, so the only attack that can be made against them is a third
        // party's — which is the one the page takes the dodge away from.)
        var partialHold = InGrapple(GrappleMove.Hold, GrappleKind.Partial);
        Assert.Contains("defends with toughness", DefenceOf(partialHold, "held", "bystander"), StringComparison.Ordinal);
        Assert.Contains("defends with toughness", DefenceOf(partialHold, "holder", "bystander"), StringComparison.Ordinal);
        Assert.False(MayAct(partialHold, "held"));
        Assert.False(MayAct(partialHold, "holder"));

        // A full hold: no active defence against anybody, and the held character may only escape.
        var fullHold = InGrapple(GrappleMove.Hold, GrappleKind.Full);
        Assert.Contains("defends with toughness", DefenceOf(fullHold, "held", "holder"), StringComparison.Ordinal);
        Assert.Contains("defends with toughness", DefenceOf(fullHold, "held", "bystander"), StringComparison.Ordinal);
        Assert.False(MayAct(fullHold, "held"));

        // The holder of a full hold is not restrained by it — p.76 lets them keep hitting.
        Assert.True(MayAct(fullHold, "holder"));
        Assert.Contains("defends with agility", DefenceOf(fullHold, "holder", "bystander"), StringComparison.Ordinal);
    }

    /// <summary>An encounter opened with one grapple already in progress, of the band asked for.</summary>
    private EncounterState InGrapple(GrappleMove move, GrappleKind kind)
    {
        var encounter = new Encounter(_play, new SeededDice(15));

        return encounter.Begin(Wrestlers()) with
        {
            Grapples = [new Grapple("holder", "held", move, kind)]
        };
    }

    /// <summary>The ledger sentence for one attack, with the attacker moved to the front of the order.</summary>
    private string DefenceOf(EncounterState state, string target, string attacker)
    {
        var encounter = new Encounter(_play, new SeededDice(15));

        var turn = state with { TurnIndex = state.TurnOrder.ToList().IndexOf(attacker) };

        return encounter
            .Step(turn, new Attack(attacker, target, "might"))
            .Added
            .Single(l => string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                         && l.Text.Contains("defends with", StringComparison.Ordinal))
            .Text;
    }

    /// <summary>Whether an ordinary attack by this character is resolved rather than refused.</summary>
    private bool MayAct(EncounterState state, string actor)
    {
        var encounter = new Encounter(_play, new SeededDice(15));

        var turn = state with { TurnIndex = state.TurnOrder.ToList().IndexOf(actor) };

        return encounter
            .Step(turn, new Attack(actor, "bystander", "might"))
            .Added
            .Any(l => string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
                      && l.Text.Contains("defends with", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An escape by the character doing the holding is refused, not announced.</b>
    ///
    /// <para><c>escape</c> defines the move as "an attempt to break out of a hold", and the holder is
    /// not in one — but the engine rolled, read the table, announced "a partial escape" and changed
    /// nothing, which is a ledger line describing something that did not happen. It refuses now, and
    /// says which of them is doing the holding.</para>
    /// </summary>
    [Fact]
    public void AnEscapeByTheHolderIsRefused()
    {
        var state = InGrapple(GrappleMove.Hold, GrappleKind.Full);
        var encounter = new Encounter(_play, new SeededDice(16));

        var refused = encounter.Step(state, new GrappleIntent("holder", "held", GrappleMove.Escape));

        Assert.Contains(refused.Added, l =>
            string.Equals(l.Rule, "escape", StringComparison.Ordinal)
            && l.Text.Contains("they are the one doing the holding", StringComparison.Ordinal));

        // Nothing was announced and nothing moved.
        Assert.DoesNotContain(refused.Added, l => l.Text.Contains("escape:", StringComparison.Ordinal));
        Assert.DoesNotContain(refused.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal));
        Assert.Equal(state.Grapples, refused.State.Grapples);

        // The control: the character who IS held gets a roll, so the refusal is about who asked and
        // not about escapes being switched off.
        var held = state with { TurnIndex = state.TurnOrder.ToList().IndexOf("held") };
        var tried = encounter.Step(held, new GrappleIntent("held", "holder", GrappleMove.Escape));

        Assert.Contains(tried.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal));
    }

    // ── p.76's grab, and the item it wins ────────────────────────────────────

    /// <summary>
    /// <b>A grab for an item its target is not holding is refused with nothing rolled, and the
    /// winner is not handed an object that came from nowhere.</b>
    ///
    /// <para>p.76: a grab is "an attempt to take a weapon or other handheld item away from your
    /// opponent". The object had to be the caller's word — there is no inventory here — but the
    /// engine took its <em>existence</em> from the caller too, and nothing on the wire could open a
    /// fight holding anything, so every grab a real caller ever made was for an item its target was
    /// not recorded as carrying. At three net successes the winner ended up holding it and the ledger
    /// said, in as many words, what losing it meant for somebody who had never had it. A ledger line
    /// that says a thing happened is the one thing this engine sells.</para>
    ///
    /// <para>The control is the other half, and it is the fixture below this one: the same grab
    /// against a target who <em>is</em> holding the sword rolls and lands.</para>
    /// </summary>
    [Fact]
    public void AGrabForSomethingTheTargetIsNotHoldingIsRefusedBeforeAnythingIsRolled()
    {
        var dice = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);
        var encounter = new Encounter(_play, dice);

        var refused = encounter.Step(
            encounter.Begin(Wrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword"));

        Assert.Contains(refused.Added, l =>
            string.Equals(l.Rule, "grappling", StringComparison.Ordinal)
            && l.Text.Contains("the held Hero is not holding the sword", StringComparison.Ordinal)
            && l.Text.Contains("they are recorded as holding nothing", StringComparison.Ordinal));

        // Nothing rolled, nothing announced, and above all nothing conjured into anybody's hands.
        Assert.Equal(20, dice.Remaining);
        Assert.DoesNotContain(refused.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal));
        Assert.Empty(refused.State.Grapples);
        Assert.Null(refused.State["holder"].Holding);

        // And the line that used to lie is not written at all.
        Assert.DoesNotContain(refused.Added, l =>
            l.Text.Contains("what losing the sword means", StringComparison.Ordinal));

        // The refusal names what the target does have, because "they have not got that" sends a
        // caller guessing. Here the sword has already changed hands to a third party.
        var opened = encounter.Begin(Wrestlers());
        var elsewhere = opened.With(opened["held"].Carrying("a shield"));

        var wrong = encounter.Step(
            elsewhere, new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword"));

        Assert.Contains(wrong.Added, l =>
            string.Equals(l.Rule, "grappling", StringComparison.Ordinal)
            && l.Text.Contains("what they have in their hands is a shield", StringComparison.Ordinal));

        Assert.Equal(20, dice.Remaining);

        // The control: with the sword in the target's hands the same grab is resolved, and takes the
        // faces the two refusals left on the table.
        var held = encounter.Step(
            opened.With(opened["held"].Carrying("the sword")),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword"));

        Assert.Equal(0, dice.Remaining);
        Assert.Contains(held.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal));
        Assert.Equal("the sword", held.State["holder"].Holding!.Name);
    }

    /// <summary>
    /// <b>The second roll of a partial grab is aimed at an object neither of them holds, and is
    /// allowed.</b>
    ///
    /// <para>p.76: "you and your opponent are fighting over an item ... you each get to make opposed
    /// Might rolls on your turn to act to try gaining control." A partial grab takes the item out of
    /// everybody's hands, so the guard above would have refused the printed way out of a deadlock —
    /// and a partial grab takes both characters' active defences away against everyone else, so a
    /// contest nothing could end would take them away for the rest of the fight.</para>
    ///
    /// <para>The control is that the exception is exactly as wide as the page: a <em>different</em>
    /// object, contested by nobody, is still refused while the same deadlock stands.</para>
    /// </summary>
    [Fact]
    public void EitherPartyMayGoOnRollingForTheItemAPartialGrabIsOver()
    {
        var partial = new ScriptedDice([.. FacesFor(10, 2), .. FacesFor(10, 0)]);
        var opening = new Encounter(_play, partial);

        var opened = opening.Begin(Wrestlers());

        var deadlock = opening.Step(
            opened.With(opened["held"].Carrying("the sword")),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword")).State;

        // The controls: the deadlock really is one, and the half-measure moved nothing — the
        // grabber has not got the sword, so their next roll is aimed at an item that is not in
        // their hands either.
        Assert.Equal(GrappleKind.Partial, Assert.Single(deadlock.Grapples).Kind);
        Assert.Null(deadlock["holder"].Holding);

        var dice = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);
        var encounter = new Encounter(_play, dice);

        // The direction the exception is really for: the character who walked in with the sword
        // rolls to get it back, and the grabber is not holding it — so without p.76's own clause
        // this roll is the one that would be refused.
        var back = encounter.Step(
            deadlock with { TurnIndex = deadlock.TurnOrder.ToList().IndexOf("held") },
            new GrappleIntent("held", "holder", GrappleMove.Grab, "the sword"));

        Assert.Equal(0, dice.Remaining);
        Assert.Contains(back.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal));
        Assert.Equal("the sword", back.State["held"].Holding!.Name);

        // The exception is only over the contested object: something else is still refused.
        var other = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);

        var elsewhere = new Encounter(_play, other).Step(
            deadlock with { TurnIndex = deadlock.TurnOrder.ToList().IndexOf("held") },
            new GrappleIntent("held", "holder", GrappleMove.Grab, "a shield"));

        Assert.Equal(20, other.Remaining);
        Assert.Contains(elsewhere.Added, l =>
            string.Equals(l.Rule, "grappling", StringComparison.Ordinal)
            && l.Text.Contains("the holder is not holding a shield", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A grab that names no item is refused before a die is thrown, because p.76 tells the three
    /// moves apart by what they are aimed at.</b>
    ///
    /// <para>A grab is "an attempt to take a weapon or other handheld item away from your opponent"
    /// and a hold is "an attempt to control or restrain your opponent" — so a grab with no object is
    /// a hold by another name, and resolving one would put a contest over nothing in particular on
    /// the ledger and, at three net successes, hand somebody control of it. It is the same refusal
    /// p.79's lure that names nobody makes.</para>
    ///
    /// <para>The control is the other half: the same grab naming an object rolls, so the refusal is
    /// about the missing word rather than about grabs being switched off. Both are driven with a
    /// scripted source and the faces left over are counted, because "nothing was rolled" is exactly
    /// the claim a fixture reading successes alone could not make.</para>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AGrabThatNamesNoItemIsRefusedBeforeAnythingIsRolled(string? item)
    {
        var dice = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);
        var encounter = new Encounter(_play, dice);

        var refused = encounter.Step(
            encounter.Begin(ArmedWrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Grab, item));

        Assert.Contains(refused.Added, l =>
            string.Equals(l.Rule, "grappling", StringComparison.Ordinal)
            && l.Text.Contains("has not said what of the held Hero's they are grabbing",
                StringComparison.Ordinal));

        // Nothing rolled, nothing announced, nothing recorded.
        Assert.Equal(20, dice.Remaining);
        Assert.DoesNotContain(refused.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal));
        Assert.Empty(refused.State.Grapples);
        Assert.Null(refused.State["holder"].Holding);

        // The control: the same move with an object named is resolved, and takes the same faces the
        // refusal left on the table.
        var named = encounter.Step(
            encounter.Begin(ArmedWrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword"));

        Assert.Equal(0, dice.Remaining);
        Assert.Contains(named.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A full grab moves the object: the winner holds it and the loser does not.</b>
    ///
    /// <para>p.76: "a full grab means you gain control of the object". This engine has no inventory,
    /// so what is claimed is exactly that — <see cref="Combatant.Holding"/> carries the caller's word
    /// for the thing and the page it was won on, and the loser stops holding it where a previous grab
    /// had put it there. The fixture drives the round trip: the item is taken off a character who was
    /// holding it, so "the loser no longer holds it" is a state change rather than a sentence about a
    /// field that was already null.</para>
    ///
    /// <para><b>What the loss does <em>not</em> do is on the ledger and is asserted here</b>, because
    /// it is the reading this slice makes: an attack names a Trait and nothing in this repository's
    /// rules data says which Trait a weapon backs, so the loser's own rolls are untouched and the
    /// line hands the question to the GM rather than leaving a reader to assume it was handled.</para>
    /// </summary>
    [Fact]
    public void AFullGrabTakesTheItemOffTheLoserAndGivesItToTheWinner()
    {
        var dice = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);
        var encounter = new Encounter(_play, dice);

        var state = encounter.Begin(ArmedWrestlers());

        // The control: the loser really is holding it before the grab, so the assertion afterwards
        // is about something that moved.
        Assert.Equal("the sword", state["held"].Holding!.Name);

        var step = encounter.Step(
            state, new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword"));

        Assert.Equal(0, dice.Remaining);

        // The second control: the band this fixture is about really is the one the table gave.
        Assert.Contains(step.Added, l =>
            string.Equals(l.Rule, "grappling_table", StringComparison.Ordinal)
            && l.Text.Contains("full grab", StringComparison.Ordinal));

        Assert.Null(step.State["held"].Holding);
        Assert.Equal("the sword", step.State["holder"].Holding?.Name);
        Assert.Equal(step.State.Page, step.State["holder"].Holding?.WonOnPage);
        Assert.Equal(false, step.State["holder"].Holding?.Used);

        // And the half that is the GM's, said out loud rather than silently not done.
        Assert.Contains(step.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("what losing the sword means for the held Hero's own attacks is the GM's",
                StringComparison.Ordinal));

        // Nothing of the loser's moved: same ranks, same defences, same Health.
        Assert.Equal(state["held"].TraitRanks, step.State["held"].TraitRanks);
        Assert.Equal(state["held"].Defences, step.State["held"].Defences);
        Assert.Equal(state["held"].CurrentHealth, step.State["held"].CurrentHealth);
    }

    /// <summary>
    /// <b>A partial grab records the object both characters have hold of, and it is one record
    /// because the page's clause is about the pair.</b>
    ///
    /// <para>p.76: "you and your opponent are fighting over an item ... They can't use it, but
    /// neither can you." <see cref="Grapple"/> names both characters, so one field answers for both
    /// of them and there is no second place for the same fact to rot in.</para>
    ///
    /// <para><b>And neither of them is holding it</b>, which is the difference between the two bands:
    /// the half-measure is a deadlock and only the full one hands the object over.</para>
    /// </summary>
    [Fact]
    public void APartialGrabRecordsTheContestedItemForBothCharacters()
    {
        var dice = new ScriptedDice([.. FacesFor(10, 2), .. FacesFor(10, 0)]);
        var encounter = new Encounter(_play, dice);

        var step = encounter.Step(
            encounter.Begin(ArmedWrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword"));

        Assert.Equal(0, dice.Remaining);

        var grapple = Assert.Single(step.State.Grapples);

        Assert.Equal(GrappleKind.Partial, grapple.Kind);
        Assert.Equal("the sword", grapple.Item);

        // One record, and it names both of them — so a reader looking either character up finds the
        // same item.
        Assert.Equal(["held", "holder"], new[] { grapple.Held, grapple.Holder }.Order(StringComparer.Ordinal));

        // <b>The half-measure moves nothing.</b> Only a full grab hands the object over, so the
        // grabber has not got it and the character who walked in with it still has hold of it —
        // "they can't use it, but neither can you" is a prohibition on using it and not a claim
        // about whose hand it is in.
        Assert.Null(step.State["holder"].Holding);
        Assert.Equal("the sword", step.State["held"].Holding!.Name);

        // And the object is on the ledger line, so a reader of the run knows what they are fighting
        // over rather than that they are fighting.
        Assert.Contains(step.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("partial grab over the sword", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Winning the contest outright ends it, whichever character the partial was recorded
    /// under.</b>
    ///
    /// <para>The filter that replaces a grapple matched the actor as <see cref="Grapple.Holder"/>
    /// only, so a character who won a partial grab and then lost the item outright left their own
    /// partial standing beside the winner's full one. That is not bookkeeping: p.76 takes both
    /// characters' active defences away against everyone else <em>while a partial grab lasts</em>,
    /// and a partial nothing could remove took them away for the rest of the fight — the bystander in
    /// this fixture would have been swinging at two characters who could not dodge, over a sword one
    /// of them was plainly holding.</para>
    ///
    /// <para>The consequence is what is asserted and not the list length alone, because the list is
    /// the mechanism and the dodge is the rule.</para>
    /// </summary>
    [Fact]
    public void AFullGrabEndsAPartialRecordedTheOtherWayRound()
    {
        var opened = new Encounter(_play, new SeededDice(15)).Begin(Wrestlers());

        var contested = opened with
        {
            Grapples = [new Grapple("holder", "held", GrappleMove.Grab, GrappleKind.Partial, "the sword")],
            TurnIndex = opened.TurnOrder.ToList().IndexOf("held")
        };

        // The control: while the partial stands, neither of them may dodge the bystander — which is
        // the state this fixture is about ending.
        Assert.Contains("defends with toughness",
            DefenceOf(contested, "holder", "bystander"), StringComparison.Ordinal);

        var dice = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);

        var won = new Encounter(_play, dice).Step(
            contested, new GrappleIntent("held", "holder", GrappleMove.Grab, "the sword"));

        Assert.Equal(0, dice.Remaining);

        var grapple = Assert.Single(won.State.Grapples);

        Assert.Equal(GrappleKind.Full, grapple.Kind);
        Assert.Equal("held", grapple.Holder);
        Assert.Equal("the sword", won.State["held"].Holding?.Name);

        // And the rule: the contest is over, so the character who lost it has their dodge back.
        Assert.Contains("defends with agility",
            DefenceOf(won.State, "holder", "bystander"), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A character in a hold cannot throw anything away, and the free-action clause does not
    /// excuse them.</b>
    ///
    /// <para>p.76's "in effect, a free action" is an exemption from the multiple action penalty, not
    /// from being pinned: a fully held character "can only try to escape", and a partial hold leaves
    /// both of them one physical action — "an opposed Might roll, aiming for a full hold or an
    /// escape". Throwing something away is neither.</para>
    ///
    /// <para><b>This was a documented claim with nothing driving it.</b> Deleting the grapple guard
    /// from the toss left the whole suite green, so a held character could have discarded the sword
    /// they were pinned with and nothing here would have noticed. Both bands are driven, because
    /// they refuse through different branches and cite different clauses.</para>
    ///
    /// <para>The control is the same toss by the same character with no hold on them: it is
    /// resolved, so the refusal is about the hold rather than about tosses being switched off.</para>
    /// </summary>
    [Theory]
    [InlineData(5, "leaves them only trying to escape")]
    [InlineData(2, "is in a partial hold")]
    public void ACharacterInAHoldCannotTossWhatTheyAreHolding(int net, string expected)
    {
        var dice = new ScriptedDice([.. FacesFor(10, net), .. FacesFor(10, 0)]);
        var opening = new Encounter(_play, dice);

        var pinned = opening.Step(
            opening.Begin(ArmedWrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Hold, null)).State;

        // The controls: the hold really landed in the band this case is about, and the character it
        // landed on really is holding something to throw away.
        Assert.Equal(0, dice.Remaining);
        var hold = Assert.Single(pinned.Grapples);
        Assert.Equal(GrappleMove.Hold, hold.Move);
        Assert.Equal("the sword", pinned["held"].Holding!.Name);

        var encounter = new Encounter(_play, new SeededDice(21));

        var refused = encounter.Step(
            pinned with { TurnIndex = pinned.TurnOrder.ToList().IndexOf("held") },
            new Toss("held", "the sword"));

        Assert.Contains(refused.Added, l =>
            string.Equals(l.Rule, "hold", StringComparison.Ordinal)
            && l.Text.Contains(expected, StringComparison.Ordinal));

        Assert.Equal("the sword", refused.State["held"].Holding!.Name);
        Assert.DoesNotContain(refused.Added, l =>
            l.Text.Contains("tosses the sword aside", StringComparison.Ordinal));

        // The control: with no hold on them the same toss goes through.
        var loose = pinned with
        {
            Grapples = [],
            TurnIndex = pinned.TurnOrder.ToList().IndexOf("held")
        };

        var tossed = encounter.Step(loose, new Toss("held", "the sword"));

        Assert.Null(tossed.State["held"].Holding);
        Assert.Contains(tossed.Added, l =>
            l.Text.Contains("tosses the sword aside", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An attack that is refused does not spend p.76's page: the item is not marked used.</b>
    ///
    /// <para><c>HeldItem.Used</c> is what discharges "use it or toss it aside on that same page", so
    /// an attack that never happened marking it would keep a weapon past a page turn that should
    /// have taken it — a state change bought by a refusal. The order in <c>ResolveAttack</c> is the
    /// whole of the guarantee, and an order is exactly the kind of thing a later edit moves without
    /// noticing, so it is pinned here across three different refusals rather than reasoned about.
    /// </para>
    ///
    /// <para>Each carries the control that it really was the refusal it says: the ledger line naming
    /// it, no exchange resolved, and <c>Used</c> still false. And the fixture ends on the positive
    /// control that the same attack, unrefused, does mark it — otherwise every case here would pass
    /// against an engine that had stopped marking anything at all.</para>
    /// </summary>
    [Theory]
    [InlineData("defeated target")]
    [InlineData("no rank in the Trait")]
    [InlineData("complete cover")]
    public void ARefusedAttackDoesNotSpendThePageTheItemWasWonOn(string refusal)
    {
        var encounter = new Encounter(_play, new SeededDice(21));
        var state = WithTheSwordGrabbed();

        // The control on the setup: p.76's clause is still live, so "not marked used" below is a
        // claim about something that could have changed.
        Assert.False(state["holder"].Holding!.Used);

        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        var (attack, expected) = refusal switch
        {
            "defeated target" => (
                new Attack("holder", "held", "might", Item: "the sword"),
                "knocked out for the rest of the scene"),
            "no rank in the Trait" => (
                new Attack("holder", "held", "nothing_they_have", Item: "the sword"),
                "has no rank in nothing_they_have"),
            _ => (
                new Attack("holder", "held", "might", Cover: Cover.Complete, Item: "the sword"),
                "completely hidden behind cover")
        };

        if (string.Equals(refusal, "defeated target", StringComparison.Ordinal))
        {
            state = state.With(state["held"].WithHealth(floor));
            Assert.True(state["held"].Defeated(floor));
        }

        var dice = new ScriptedDice([.. FacesFor(10, 4), .. FacesFor(8, 1)]);
        var scripted = new Encounter(_play, dice);

        var refused = scripted.Step(state, attack);

        Assert.Contains(refused.Added, l => l.Text.Contains(expected, StringComparison.Ordinal));

        // Nothing rolled, nothing resolved, and above all p.76's page not spent.
        Assert.Equal(18, dice.Remaining);
        Assert.DoesNotContain(refused.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));
        Assert.DoesNotContain(refused.Added, l =>
            l.Text.Contains("attacks with the sword", StringComparison.Ordinal));

        Assert.False(refused.State["holder"].Holding!.Used);

        // And the item is still there to be taken away by the page turn, which is the consequence
        // the field exists for.
        while (refused.State.Current is { } acting)
            refused = refused with { State = encounter.Step(refused.State, new EndTurn(acting.Id)).State };

        var turned = encounter.Step(refused.State, new EndPage(""));

        Assert.Null(turned.State["holder"].Holding);

        // The positive control: unrefused, the same attack marks it — so none of the above is
        // passing against an engine that stopped marking anything.
        var allowed = new Encounter(_play, new SeededDice(21))
            .Step(WithTheSwordGrabbed(), new Attack("holder", "held", "might", Item: "the sword"));

        Assert.True(allowed.State["holder"].Holding!.Used);
    }

    /// <summary>
    /// <b>A character who is out of the fight lets go of what they were holding, and the ledger says
    /// where the object went.</b>
    ///
    /// <para>p.76 says nothing about a defeated holder, and the two silences are not equally honest.
    /// The clause that puts an object in anybody's hands is "a full grab means you gain control of
    /// the object", and a defeated character controls nothing — this engine refuses them every
    /// intent that is a character doing something, and refuses a grapple <em>against</em> them
    /// before it is rolled. So an item left on a body is an item out of the fight for good, with
    /// nothing on the ledger to say it had gone.</para>
    ///
    /// <para><b>The item here was used</b>, which is what makes this fixture about defeat rather
    /// than about p.76's one page: an unused one would have been tossed by the page turn anyway, so
    /// a fixture built on one would pass against an engine that had never heard of defeat.</para>
    ///
    /// <para>The control is the other half: the same fight, the same used item, the same page turn,
    /// with the holder still standing — they keep it.</para>
    /// </summary>
    [Fact]
    public void ACharacterWhoIsOutOfTheFightLetsGoOfWhatTheyHeld()
    {
        var standing = AfterAPageTurnWithTheHolder(defeated: false);

        // The control: used, so p.76's page has been discharged, and they still have it.
        Assert.True(standing.State["holder"].Holding!.Used);
        Assert.Equal("the sword", standing.State["holder"].Holding!.Name);

        var out_ = AfterAPageTurnWithTheHolder(defeated: true);

        Assert.Null(out_.State["holder"].Holding);
        Assert.Contains(out_.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("is out of the fight and lets go of the sword", StringComparison.Ordinal)
            && l.Text.Contains("control of the object", StringComparison.Ordinal));
    }

    /// <summary>
    /// A full grab, an attack made with what it won — so the item survives p.76's page — and then a
    /// page turn, with the winner either standing or beaten down to the entry's own defeat figure.
    /// </summary>
    private StepResult AfterAPageTurnWithTheHolder(bool defeated)
    {
        var encounter = new Encounter(_play, new SeededDice(21));
        var state = WithTheSwordGrabbed();

        state = encounter.Step(state, new Attack("holder", "held", "might", Item: "the sword")).State;

        // The control on the setup: the item is theirs and the clause p.76 measures in pages has
        // been discharged, so what happens below is about the holder and not about the page.
        Assert.True(state["holder"].Holding!.Used);

        if (defeated)
        {
            var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

            state = state.With(state["holder"].WithHealth(floor));

            Assert.True(state["holder"].Defeated(floor));
        }

        while (state.Current is { } acting)
            state = encounter.Step(state, new EndTurn(acting.Id)).State;

        return encounter.Step(state, new EndPage(""));
    }

    /// <summary>
    /// <b>Either party may let go of the contested object, which ends the deadlock and gives both of
    /// them their active defences back.</b>
    ///
    /// <para>p.76 prints the exit in the same paragraph as the deadlock: "you can exit grappling
    /// combat at any time by letting go of the object." Without it a partial grab was a state nothing
    /// could end — the half-measure takes both characters' active defences away against everybody
    /// else and is otherwise settled only by three net successes, the character who walked in with
    /// the object could toss it and stay tangled, and the one grabbing at it could not toss it at
    /// all, because a toss was refused unless the actor held what they named.</para>
    ///
    /// <para><b>The active defences are driven rather than asserted about the record</b>: a third
    /// party attacks each of them before and after, and the ledger line p.76 writes for the blocked
    /// defence is required to be there and then not to be. That is what makes this a fixture about
    /// the fight rather than about a list.</para>
    ///
    /// <para>Both directions are driven, because they are different code: the character holding the
    /// object drops it, and the one who never had it lets go of something they were not holding —
    /// which the ordinary toss refuses by name.</para>
    /// </summary>
    [Theory]
    [InlineData("held")]
    [InlineData("holder")]
    public void EitherPartyMayLetGoOfTheContestedObject(string quitter)
    {
        var partial = new ScriptedDice([.. FacesFor(10, 2), .. FacesFor(10, 0)]);
        var opening = new Encounter(_play, partial);

        var deadlock = opening.Step(
            opening.Begin(ArmedWrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword")).State;

        Assert.Equal(GrappleKind.Partial, Assert.Single(deadlock.Grapples).Kind);

        // The positive control, and it is about the fight rather than about the record: the
        // bystander swings at each of them and p.76 takes the active defence away.
        Assert.True(NoActiveDefenceLine(deadlock, "held"));
        Assert.True(NoActiveDefenceLine(deadlock, "holder"));

        var encounter = new Encounter(_play, new SeededDice(21));

        var loosed = encounter.Step(
            deadlock with { TurnIndex = deadlock.TurnOrder.ToList().IndexOf(quitter) },
            new Toss(quitter, "the sword"));

        Assert.Contains(loosed.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("lets go of the sword", StringComparison.Ordinal)
            && l.Text.Contains("MayExitByLettingGoOfTheObject is True", StringComparison.Ordinal));

        // Both of them can dodge again — asserted first, because it is the consequence the page
        // attaches to the exit and the record is only how this engine spells it.
        Assert.False(NoActiveDefenceLine(loosed.State, "held"));
        Assert.False(NoActiveDefenceLine(loosed.State, "holder"));

        // And the deadlock is over, with nobody in control of the object: only a full grab is that.
        Assert.Empty(loosed.State.Grapples);
        Assert.Null(loosed.State["held"].Holding);
        Assert.Null(loosed.State["holder"].Holding);

        // And letting go is free, the way p.76's use and toss are: the quitter still has their turn.
        Assert.Equal(quitter, loosed.State.Current!.Id);
    }

    /// <summary>
    /// Whether p.76 takes <paramref name="who"/>'s active defence away against a third party, driven
    /// by having the bystander actually swing at them.
    /// </summary>
    private bool NoActiveDefenceLine(EncounterState state, string who)
    {
        var dice = new ScriptedDice([.. FacesFor(6, 2), .. FacesFor(10, 1)]);

        var swung = new Encounter(_play, dice).Step(
            state with { TurnIndex = state.TurnOrder.ToList().IndexOf("bystander") },
            new Attack("bystander", who, "might"));

        // The control on the control: the attack really was resolved, so "no line" means the
        // defence was allowed rather than that nothing happened.
        Assert.Contains(swung.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));

        return swung.Added.Any(l =>
            l.Text.Contains("no active defence against anybody else", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Neither party may attack with the object a partial grab is over, which is the clause p.76
    /// prints and this engine could not see until an attack could name an item.</b>
    ///
    /// <para>"A partial grab means you and your opponent are fighting over an item. <b>They can't use
    /// it, but neither can you.</b>" The half of the deadlock already applied is the defences; the
    /// prohibition on using it was recorded as unenforceable because no roll named a weapon. One
    /// does now — and the character who walked in with the sword is still recorded as holding it, so
    /// without this the printed sentence is false in exactly the case it was written for.</para>
    ///
    /// <para><b>Both parties, and neither for the other's reason.</b> The grabber is not holding it
    /// either, so a refusal that only asked about the hand would refuse them for the wrong thing.
    /// </para>
    ///
    /// <para>Three controls: nothing is rolled, the same attack <em>without</em> the item is
    /// resolved — so this is about the object and not about the deadlock switching attacks off — and
    /// the same attack with the item is resolved once the contest is over.</para>
    /// </summary>
    [Fact]
    public void NeitherPartyMayAttackWithWhatAPartialGrabIsOver()
    {
        var partial = new ScriptedDice([.. FacesFor(10, 2), .. FacesFor(10, 0)]);
        var opening = new Encounter(_play, partial);

        var deadlock = opening.Step(
            opening.Begin(ArmedWrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword")).State;

        // The controls on the setup: the deadlock is a partial grab over the sword, and the
        // character who brought it is still recorded as holding it — so the refusal below is not
        // the empty-hands one wearing a different hat.
        var contest = Assert.Single(deadlock.Grapples);
        Assert.Equal(GrappleKind.Partial, contest.Kind);
        Assert.Equal("the sword", contest.Item);
        Assert.Equal("the sword", deadlock["held"].Holding!.Name);

        // The party whose hands it is in.
        var owner = new ScriptedDice([.. FacesFor(10, 4), .. FacesFor(8, 1)]);

        var refusedOwner = new Encounter(_play, owner).Step(
            deadlock with { TurnIndex = deadlock.TurnOrder.ToList().IndexOf("held") },
            new Attack("held", "bystander", "might", Item: "the sword"));

        Assert.Contains(refusedOwner.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("cannot attack with the sword", StringComparison.Ordinal)
            && l.Text.Contains("both characters are fighting over the item and neither can use it",
                StringComparison.Ordinal));

        Assert.Equal(18, owner.Remaining);
        Assert.DoesNotContain(refusedOwner.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));

        // And the other party, who is not holding it — refused for the page's reason and not for
        // the state of their hands.
        var grabber = new ScriptedDice([.. FacesFor(10, 4), .. FacesFor(8, 1)]);

        var refusedGrabber = new Encounter(_play, grabber).Step(
            deadlock, new Attack("holder", "bystander", "might", Item: "the sword"));

        Assert.Contains(refusedGrabber.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("cannot attack with the sword", StringComparison.Ordinal)
            && l.Text.Contains("neither can use it", StringComparison.Ordinal));

        Assert.DoesNotContain(refusedGrabber.Added, l =>
            l.Text.Contains("is not holding the sword", StringComparison.Ordinal));

        Assert.Equal(18, grabber.Remaining);

        // The control: the same attack naming no item is resolved, so the deadlock has not switched
        // attacking off — p.76 takes the active defences and the use of the object, and no more.
        var plain = new ScriptedDice([.. FacesFor(10, 4), .. FacesFor(8, 1)]);

        var allowed = new Encounter(_play, plain).Step(
            deadlock, new Attack("holder", "bystander", "might"));

        Assert.NotEqual(18, plain.Remaining);
        Assert.Contains(allowed.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));

        // And the second control: once the contest is settled outright the winner may swing it.
        var settling = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);

        var won = new Encounter(_play, settling).Step(
            deadlock, new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword")).State;

        Assert.Equal("the sword", won["holder"].Holding!.Name);

        var after = new ScriptedDice([.. FacesFor(10, 4), .. FacesFor(8, 1)]);

        var swung = new Encounter(_play, after).Step(
            won, new Attack("holder", "held", "might", Item: "the sword"));

        Assert.NotEqual(18, after.Remaining);
        Assert.Contains(swung.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An item won and neither used nor tossed is tossed aside as the page turns.</b>
    ///
    /// <para>p.76 gives the winner one page: "you gain control of the object and can use it or toss
    /// it aside on that same page". That is the only duration the page attaches to an item, so an
    /// engine that let a winner carry it silently into page four would be keeping a state the book
    /// has stopped describing.</para>
    ///
    /// <para><b>The control is the other branch of the same clause</b>: an item the winner actually
    /// attacked with survives the page turn, because the page it was given has been spent. Without
    /// that half this fixture would pass against an engine that simply cleared everybody's hands
    /// whenever a page ended.</para>
    /// </summary>
    [Fact]
    public void AnItemWonAndNeverUsedIsTossedWhenThePageTurns()
    {
        var dropped = AfterAPageTurn(useIt: false);

        Assert.Null(dropped.State["holder"].Holding);
        Assert.Contains(dropped.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("neither used nor tossed it", StringComparison.Ordinal)
            && l.Text.Contains("tossed aside as the page turns", StringComparison.Ordinal));

        var kept = AfterAPageTurn(useIt: true);

        Assert.Equal("the sword", kept.State["holder"].Holding?.Name);
        Assert.Equal(true, kept.State["holder"].Holding?.Used);
        Assert.DoesNotContain(kept.Added, l =>
            l.Text.Contains("tossed aside as the page turns", StringComparison.Ordinal));
    }

    /// <summary>
    /// A full grab, optionally an attack made with what it won, and then the page turn — with the
    /// lines the page turn itself added.
    /// </summary>
    private StepResult AfterAPageTurn(bool useIt)
    {
        var encounter = new Encounter(_play, new SeededDice(21));
        var state = WithTheSwordGrabbed();

        if (useIt)
        {
            state = encounter.Step(state, new Attack("holder", "held", "might", Item: "the sword")).State;
        }

        // The control: the grab really landed, so the page turn below is acting on an item somebody
        // has rather than on an empty hand.
        Assert.Equal("the sword", state["holder"].Holding?.Name);

        while (state.Current is { } acting)
            state = encounter.Step(state, new EndTurn(acting.Id)).State;

        return encounter.Step(state, new EndPage(""));
    }

    /// <summary>
    /// A fight in which "holder" has just won the sword off "held" by a full grab.
    ///
    /// <para><b>The grab is scripted rather than seeded, and stepped by an encounter of its own.</b>
    /// A full grab needs three net successes and no seed can promise them, so a helper built on one
    /// would sometimes hand back a fight with nobody holding anything — and every fixture below it
    /// would then pass by testing nothing. <c>Step</c> is a pure function of its arguments, so the
    /// state a scripted encounter produces is a state any other encounter may go on from.</para>
    /// </summary>
    private EncounterState WithTheSwordGrabbed()
    {
        var dice = new ScriptedDice([.. FacesFor(10, 5), .. FacesFor(10, 0)]);
        var grabbing = new Encounter(_play, dice);

        var state = grabbing.Step(
            grabbing.Begin(ArmedWrestlers()),
            new GrappleIntent("holder", "held", GrappleMove.Grab, "the sword")).State;

        // The control: the faces this fixture scripted are the faces the engine took, and the grab
        // really landed full.
        Assert.Equal(0, dice.Remaining);
        Assert.Equal("the sword", state["holder"].Holding?.Name);

        return state;
    }

    /// <summary>
    /// <b>Tossing the item drops it, and does not spend the holder's turn.</b>
    ///
    /// <para>p.76: "you can use it or toss it aside on that same page without suffering a multiple
    /// action penalty. In effect, a full grab is a free action." Nothing in this engine counts
    /// actions per page — that mechanic is on the guide's list of ones with no intent yet — so what
    /// the clause buys here is that the actor may still take their ordinary action, and this drives
    /// exactly that rather than asserting a penalty this engine does not have.</para>
    ///
    /// <para>The same holds for using it: an attack made with the item is the actor's attack, and the
    /// toss beside it is free.</para>
    /// </summary>
    [Fact]
    public void TossingWhatAGrabWonIsFreeAndTheHolderStillActs()
    {
        var encounter = new Encounter(_play, new SeededDice(21));
        var state = WithTheSwordGrabbed();

        var tossed = encounter.Step(state, new Toss("holder", "the sword"));

        Assert.Null(tossed.State["holder"].Holding);
        Assert.Contains(tossed.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("tosses the sword aside", StringComparison.Ordinal)
            && l.Text.Contains("does not spend their turn", StringComparison.Ordinal));

        // The free action, driven: it is still the holder's turn, and their ordinary attack is
        // resolved rather than refused.
        Assert.Equal("holder", tossed.State.Current!.Id);

        var swung = encounter.Step(tossed.State, new Attack("holder", "held", "might"));

        Assert.Contains(swung.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An intent naming an item its actor is not holding is refused by name, with nothing
    /// rolled.</b>
    ///
    /// <para>The only way anything reaches <see cref="Combatant.Holding"/> is a full grab taking it
    /// off somebody, so an attack or a toss naming something else is a claim about equipment this
    /// engine cannot answer for — and an engine that accepted it would let a caller conjure a weapon
    /// into a fight by mentioning one. The refusal says what the actor <em>is</em> holding, because
    /// "you do not have that" sends a model guessing again.</para>
    ///
    /// <para>The control is on both sides: naming the item they do hold is resolved.</para>
    /// </summary>
    [Fact]
    public void NamingAnItemTheActorDoesNotHoldIsRefused()
    {
        var encounter = new Encounter(_play, new SeededDice(21));
        var state = WithTheSwordGrabbed();

        var dice = new ScriptedDice([.. FacesFor(10, 4), .. FacesFor(8, 1)]);
        var scripted = new Encounter(_play, dice);

        var attack = scripted.Step(state, new Attack("holder", "held", "might", Item: "a rocket launcher"));

        Assert.Contains(attack.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("is not holding a rocket launcher", StringComparison.Ordinal)
            && l.Text.Contains("what they have in their hands is the sword", StringComparison.Ordinal));

        Assert.Equal(18, dice.Remaining);
        Assert.DoesNotContain(attack.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));

        // The same refusal for a toss, and against a character holding nothing at all — where the
        // line has to say so rather than name an item.
        var tossed = encounter.Step(state, new Toss("holder", "a rocket launcher"));

        Assert.Contains(tossed.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("is not holding a rocket launcher", StringComparison.Ordinal));

        var empty = encounter.Step(
            state with { TurnIndex = state.TurnOrder.ToList().IndexOf("held") },
            new Toss("held", "the sword"));

        Assert.Contains(empty.Added, l =>
            string.Equals(l.Rule, "grab", StringComparison.Ordinal)
            && l.Text.Contains("they are recorded as holding nothing", StringComparison.Ordinal));

        // The control: the item they really do hold is accepted, and the attack is resolved.
        var allowed = scripted.Step(state, new Attack("holder", "held", "might", Item: "the sword"));

        Assert.Equal(0, dice.Remaining);
        Assert.Contains(allowed.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal));

        Assert.Equal(true, allowed.State["holder"].Holding?.Used);
    }

    // ── Defeat ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An effect long enough to reach what is left of a target puts them out for the scene, and
    /// the state says so and not only the ledger.</b>
    ///
    /// <para>p.76: "if the duration of the effect is equal to or greater than the target's current
    /// Health, the target is defeated for the rest of the scene". Six net successes buy three pages
    /// of Mind Control, which reaches a target on three Health. The engine used to write that
    /// sentence on the ledger and change nothing — the target kept their place in the order, kept
    /// defending, and the fight could not end on them — which is a ledger that lies about the run it
    /// is the audit trail for.</para>
    ///
    /// <para>It is not recorded as damage, and the fixture says so: the target's Health has not
    /// moved. An Ensnare long enough to end a fight ends it without doing a point.</para>
    /// </summary>
    [Fact]
    public void AnEffectThatReachesATargetsHealthPutsThemOutForTheScene()
    {
        var controller = Combatant.Villain("controller", "the Controller", edge: 10, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["mind_control"] = 12, ["willpower"] = 5 },
            ["willpower"]);

        var subject = Combatant.Hero("subject", "the Subject", edge: 8, health: 3, resolve: 1,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["willpower"] = 6, ["might"] = 6 },
            ["willpower"]);

        // Nine successes against three is six net; half of six, rounding the Glossary's way, is the
        // three pages that reach a Health of three.
        var dice = new ScriptedDice([.. FacesFor(12, 9), .. FacesFor(6, 3)]);
        var encounter = new Encounter(_play, dice);

        var state = encounter.Begin([controller, subject]);
        var step = encounter.Step(state, new Attack(
            "controller", "subject", "mind_control", DamageKind.Psychic, AttackType.MentalPower,
            Effect: "Mind Control"));

        state = step.State;

        // The controls, before the outcome: the rolls are the ones this fixture is about, and the
        // engine made exactly those.
        Assert.Equal(9, state.LastAttack!.AttackSuccesses);
        Assert.Equal(3, state.LastAttack.DefenceSuccesses);
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(3, Assert.Single(state.Effects).RemainingPages);

        Assert.Contains(step.Added, l =>
            string.Equals(l.Rule, "special_effects", StringComparison.Ordinal)
            && l.Text.Contains("out for the rest of the scene", StringComparison.Ordinal));

        // And the state agrees with the sentence.
        Assert.Equal("Mind Control", state["subject"].DefeatedByEffect);
        Assert.True(state["subject"].Defeated(encounter.DefeatFloor));
        Assert.Equal(3, state["subject"].CurrentHealth);

        // The fight is over once the page turns, which is when Over is recomputed.
        state = encounter.Step(state, new EndTurn("controller")).State;
        state = encounter.Step(state, new EndTurn("subject")).State;
        state = encounter.Step(state, new EndPage("")).State;

        Assert.True(state.Over);

        // And on the next page the subject does not act: the attack is refused, citing p.76.
        var refused = encounter.Step(state with { TurnIndex = state.TurnOrder.ToList().IndexOf("subject") },
            new Attack("subject", "controller", "might"));

        Assert.Equal(10, refused.State["controller"].CurrentHealth);
        Assert.Contains(refused.Added, l =>
            string.Equals(l.Rule, "special_effects", StringComparison.Ordinal)
            && l.Text.Contains("is out for the rest of the scene under Mind Control", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A combatant who has been beaten down neither acts nor is acted on.</b>
    ///
    /// <para>p.75: falling to the defeat figure means out of the fight. Every intent that is a
    /// character doing something is refused for a defeated actor and against a defeated target, and
    /// the refusal cites <c>damage</c> — the entry that says what defeat is.</para>
    ///
    /// <para><b>The Resolve purchases are deliberately still open</b>, and the fixture pins that:
    /// Chapter 5's spends are what a character who has just gone down does, and p.79's Fatal Damage
    /// rescue is bought at a Health well past the defeat figure.</para>
    /// </summary>
    [Fact]
    public void ADefeatedCombatantNeitherActsNorIsActedOn()
    {
        var traits = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["might"] = 6, ["toughness"] = 4, ["agility"] = 3
        };

        var down = Combatant.Hero("down", "the fallen Hero", edge: 9, health: 8, resolve: 2,
            traits, ["toughness", "agility"]).WithHealth(0);
        var up = Combatant.Villain("up", "the Villain", edge: 7, health: 10, traits, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(3));
        var state = encounter.Begin([down, up]);

        // The control: the fallen Hero is at the entry's own defeat figure and it is their turn, so
        // nothing but being defeated is stopping them.
        Assert.Equal(encounter.DefeatFloor, state["down"].CurrentHealth);
        Assert.Equal("down", state.Current!.Id);

        foreach (var intent in new Intent[]
                 {
                     new Attack("down", "up", "might"),
                     new Move("down", "up"),
                     new Hold("down"),
                     new GrappleIntent("down", "up", GrappleMove.Hold),
                     new BreakFree("down", "toughness", Threshold: 2)
                 })
        {
            var step = encounter.Step(state, intent);

            Assert.Contains(step.Added, l =>
                string.Equals(l.Rule, "damage", StringComparison.Ordinal)
                && l.Text.Contains("the fallen Hero is", StringComparison.Ordinal)
                && l.Text.Contains("not the actor of anything", StringComparison.Ordinal));

            Assert.Equal(10, step.State["up"].CurrentHealth);
            Assert.Empty(step.State.Grapples);
            Assert.Empty(step.State.Holds);
        }

        // And nobody attacks them either. It is the Villain's turn for this one.
        var onDown = encounter.Step(
            state with { TurnIndex = state.TurnOrder.ToList().IndexOf("up") },
            new Attack("up", "down", "might"));

        Assert.Contains(onDown.Added, l =>
            string.Equals(l.Rule, "damage", StringComparison.Ordinal)
            && l.Text.Contains("not the target of anything", StringComparison.Ordinal));
        Assert.Null(onDown.State.LastAttack);

        // A Resolve purchase is not refused: p.76 and p.79 are both bought from exactly here, and
        // p.76's instant recovery is the one that brings a defeated character back to their feet.
        var spend = encounter.Step(state, new SpendResolve("down", ResolveSpend.InstantRecovery));

        Assert.DoesNotContain(spend.Added, l =>
            l.Text.Contains("not the actor of anything", StringComparison.Ordinal));

        var restored = _play.GetCombat("instant_recovery").InstantRecovery!.AfterADamagingDefeatRestoresHealth;

        Assert.Equal(restored, spend.State["down"].CurrentHealth);
        Assert.False(spend.State["down"].Defeated(encounter.DefeatFloor));
    }

    // ── Chapter 4's Resolve purchases ────────────────────────────────────────

    /// <summary>
    /// <b>p.76's <c>keeping_hold</c> stops the effect's clock, and the ledger line is not the whole
    /// of it.</b>
    ///
    /// <para>The page prints no worked example of the purchase, so this is a property with its own
    /// control beside it: the <em>same</em> scripted faces are replayed twice, once with the point
    /// spent and once without, and the effect that runs out in the second run has to still be
    /// running in the first. A run on its own would prove nothing — an effect that was still there
    /// might simply have had pages left.</para>
    ///
    /// <para>The counts come first, as everywhere here: the attack is required to have scored the
    /// successes the script pays for and the target to have been <em>defeated by the effect</em>,
    /// because "whenever you defeat a target with a special effect" is the entry's trigger and a
    /// fixture where the effect merely landed would be buying something else.</para>
    /// </summary>
    [Fact]
    public void AKeptHoldStopsCountingDownAndAnUnkeptOneRunsOut()
    {
        var rule = _play.GetCombat("keeping_hold").KeepingHold!;

        // The control on the data: the purchase costs something, or "the pool moved" below is empty.
        Assert.True(rule.CostResolve > 0);

        var kept = Hold(buying: true);
        var lapsed = Hold(buying: false);

        // The unkept effect ran out on the page its duration says, and the kept one did not.
        Assert.Contains(lapsed.Ledger.Lines, l =>
            string.Equals(l.Rule, "special_effects", StringComparison.Ordinal)
            && l.Text.Contains("runs out", StringComparison.Ordinal));
        Assert.Empty(lapsed.Effects);

        var still = Assert.Single(kept.Effects);
        Assert.Equal("Mind Control", still.Name);
        Assert.Equal(1, still.KeptScenes);

        Assert.Contains(kept.Ledger.Lines, l =>
            string.Equals(l.Rule, "keeping_hold", StringComparison.Ordinal)
            && l.Text.Contains(rule.ExtendsTo, StringComparison.Ordinal));
        Assert.DoesNotContain(kept.Ledger.Lines, l =>
            string.Equals(l.Rule, "special_effects", StringComparison.Ordinal)
            && l.Text.Contains("runs out", StringComparison.Ordinal));

        // And the point was paid for it.
        Assert.Equal(3 - rule.CostResolve, kept["hero"].Resolve);
        Assert.Equal(3, lapsed["hero"].Resolve);
    }

    /// <summary>
    /// The same fight twice: 4d of Mind Control for 4 successes against 2d of Willpower for 1, which
    /// is 3 net and — half of it, rounding the way p.7 rounds — 2 pages of effect on a target with 2
    /// Health left, so the effect defeats them. Then two pages pass.
    /// </summary>
    private EncounterState Hold(bool buying)
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["mind_control"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["willpower"] = 2, ["might"] = 5 },
            ["willpower"]);

        var dice = new ScriptedDice(6, 6, 1, 1, 4, 1);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);

        var attack = encounter.Step(state, new Attack(
            "hero", "villain", "mind_control", DamageKind.Psychic, AttackType.MentalPower,
            Effect: "Mind Control"));

        state = attack.State;

        // The positive controls, before any outcome: the printed counts, and the defeat the entry's
        // trigger names.
        Assert.Contains(attack.Added, l =>
            l.Text.Contains("mind_control 4d for 4 successes", StringComparison.Ordinal)
            && l.Text.Contains("willpower 2d for 1", StringComparison.Ordinal));
        Assert.Equal("Mind Control", state["villain"].DefeatedByEffect);
        Assert.Equal(2, Assert.Single(state.Effects).RemainingPages);

        if (buying) state = encounter.Step(state, new SpendResolve("hero", ResolveSpend.KeepingHold)).State;

        state = encounter.Step(state, new EndTurn("hero")).State;
        state = encounter.Step(state, new EndTurn("villain")).State;
        state = encounter.Step(state, new EndPage("")).State;
        state = encounter.Step(state, new EndPage("")).State;

        // Nothing rolled anything the page does not: the purchase and the page turns take no dice.
        Assert.Equal(0, dice.Remaining);

        return state;
    }

    /// <summary>
    /// <b>A buyer who is out of the fight is refused, and the refusal cites the rule that put them
    /// there.</b>
    ///
    /// <para>This is where Chapter 4's four purchases part company with Chapter 5's. p.76's instant
    /// recovery and p.79's Fatal Damage rescue are what a character who has just gone down buys, so
    /// <c>OutOfTheFight</c> deliberately does not guard them; keeping a hold, knocking somebody
    /// across the street, luring and leading a team attack are things a character does while they
    /// are still in the fight, and all four are driven here.</para>
    ///
    /// <para>The control is the other refusal: standing up, each of these is refused for want of a
    /// situation rather than for want of a buyer, so the line below is the defeat and not the same
    /// refusal twice.</para>
    /// </summary>
    [Theory]
    [InlineData(ResolveSpend.KeepingHold, "has nobody down under an effect of theirs")]
    [InlineData(ResolveSpend.Knockback, "no blow of their own on the table")]
    [InlineData(ResolveSpend.Luring, "nothing has just been aimed at")]
    [InlineData(ResolveSpend.TeamAttack, "no roll of their own on the table")]
    public void ADefeatedBuyerIsRefusedEveryChapterFourPurchase(ResolveSpend kind, string standingRefusal)
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["mind_control"] = 4, ["might"] = 6 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["willpower"] = 2, ["toughness"] = 3 },
            ["willpower"]);

        var encounter = new Encounter(_play, new SeededDice(4));
        var state = encounter.Begin([hero, villain]);

        // The control: on their feet, the purchase is refused for want of a situation.
        var standing = Assert.Single(encounter.Step(state, new SpendResolve("hero", kind)).Added);

        Assert.Contains(standingRefusal, standing.Text, StringComparison.OrdinalIgnoreCase);

        var down = encounter.Step(
            state.With(hero.WithHealth(encounter.DefeatFloor)),
            new SpendResolve("hero", kind));

        var line = Assert.Single(down.Added);

        Assert.Equal("damage", line.Rule);
        Assert.Contains("not the buyer of anything", line.Text, StringComparison.Ordinal);
        Assert.Equal(3, down.State["hero"].Resolve);
    }

    /// <summary>
    /// <b>A knockback throws the target exactly as far as p.74's table says and no farther, and
    /// takes the turn it says it takes.</b>
    ///
    /// <para>p.78 prints no worked example, so this is a property: the distance is looked up in the
    /// shipped <c>throwing_table</c> here rather than typed, and the band the pair end at has to be
    /// that row's — and, separately, never past it, which is the half a reading that guessed
    /// generously would fail.</para>
    ///
    /// <para><b>Both halves of "losing their next turn to act" are driven</b>, because which turn it
    /// is depends on where the page has got to. A target who has still to act loses that turn and
    /// comes out of this page's order; one who has already acted loses the next page's, and the
    /// order built when the page turns has to be missing them.</para>
    ///
    /// <para>The count comes first as always: the blow is required to have scored the successes the
    /// script pays for and to have done at least the <c>minimum_damage</c> the entry demands, or the
    /// purchase under test would be a refusal wearing a knockback's name.</para>
    /// </summary>
    [Fact]
    public void AKnockbackThrowsATargetAsFarAsTheThrowingTableSaysAndTakesATurn()
    {
        var rule = _play.GetCombat("knockback").Knockback!;
        var table = _play.GetCombat("throwing_table").ThrowingTable!;

        // The distance the data says, for the 8d Might these fixtures attack with.
        var row = table.Single(r => 8 >= r.MinRank && (r.MaxRank is null || 8 <= r.MaxRank));
        var reach = Enum.Parse<RangeBand>(row.Range.Replace(" Range", "", StringComparison.Ordinal));

        // The controls on the data: the row is not the one the fight opens in, so "it moved" below
        // is the throw and not the opening band, and the purchase costs something.
        Assert.NotEqual(RangeBand.Close, reach);
        Assert.True(rule.CostResolve > 0);

        var (pending, villainStillToAct, _) = Knocked(targetActsFirst: false);

        Assert.Equal(reach, pending.RangeBetween("hero", "villain"));
        Assert.True((int)pending.RangeBetween("hero", "villain") <= (int)reach,
            "the throw went farther than the throwing table's own row for that rank");

        // The turn they had not taken is the turn they lose: they are out of this page's order.
        Assert.Equal(["hero", "villain"], villainStillToAct);
        Assert.Equal(["hero"], pending.TurnOrder);
        Assert.Empty(pending.LosesNextTurn);

        // And where they had already acted, it is the next page's order they are missing from.
        var (turned, _, fight) = Knocked(targetActsFirst: true);

        Assert.Equal(2, turned.Page);
        Assert.Equal(["hero"], turned.TurnOrder);

        Assert.Contains(turned.Ledger.Lines, l =>
            string.Equals(l.Rule, "pages_and_turns", StringComparison.Ordinal)
            && l.Text.Contains("forfeited a turn", StringComparison.Ordinal));

        // <b>And it is one turn, not every turn from here on.</b> p.78 takes the target's next turn
        // to act; a forfeit left standing on the state would take the page after that as well, and
        // the one after that, and a combatant nobody ever rolls for is a fight measured short. The
        // page turn that spends the forfeit has to clear it, so the page after has them back — and
        // the ledger says it once.
        var back = fight.Step(fight.Step(turned, new EndTurn("hero")).State, new EndPage("")).State;

        Assert.Equal(3, back.Page);
        Assert.Equal(["villain", "hero"], back.TurnOrder);
        Assert.Empty(back.LosesNextTurn);

        Assert.Equal(1, back.Ledger.Lines.Count(l =>
            string.Equals(l.Rule, "pages_and_turns", StringComparison.Ordinal)
            && l.Text.Contains("forfeited a turn", StringComparison.Ordinal)));

        // The clause this engine cannot apply is named rather than left to be assumed.
        Assert.Contains(pending.Ledger.Lines, l =>
            string.Equals(l.Rule, "knockback", StringComparison.Ordinal)
            && l.Text.Contains(rule.DamageOnStrikingASolidObject, StringComparison.Ordinal)
            && l.Text.Contains("no scenery", StringComparison.Ordinal));
    }

    /// <summary>
    /// 8d of Might for 6 successes against 2d of Toughness for none — 6 points of subdual damage,
    /// which is what the entry's <c>minimum_damage</c> asks for — and then the point spent.
    /// </summary>
    /// <param name="targetActsFirst">
    /// Whether the target has already had their turn when the blow lands, which decides which turn
    /// the knockback takes off them. The page turns in that case, so the order can be read.
    /// </param>
    private (EncounterState State, IReadOnlyList<string> OpeningOrder, Encounter Fight) Knocked(
        bool targetActsFirst)
    {
        var rule = _play.GetCombat("knockback").Knockback!;

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: targetActsFirst ? 11 : 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        var dice = new ScriptedDice(6, 6, 6, 1, 1, 1, 1, 1, 1, 1);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);
        var opening = state.TurnOrder;

        if (targetActsFirst)
        {
            state = encounter.Step(state, new Hold("villain")).State;
            state = encounter.Step(state, new EndTurn("villain")).State;
        }

        var blow = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed));

        state = blow.State;

        // The positive controls: the printed counts, and a blow big enough for the entry's floor.
        Assert.Contains(blow.Added, l =>
            l.Text.Contains("might 8d for 6 successes", StringComparison.Ordinal)
            && l.Text.Contains("toughness 2d for 0", StringComparison.Ordinal));

        Assert.Equal(12 - rule.MinimumDamage, state["villain"].CurrentHealth);
        Assert.Equal(RangeBand.Close, state.RangeBetween("hero", "villain"));

        state = encounter.Step(state, new SpendResolve("hero", ResolveSpend.Knockback)).State;

        Assert.Equal(3 - rule.CostResolve, state["hero"].Resolve);

        if (targetActsFirst)
        {
            state = encounter.Step(state, new EndTurn("hero")).State;
            state = encounter.Step(state, new EndPage("")).State;
        }

        // The purchase and the page turn roll nothing the page does not.
        Assert.Equal(0, dice.Remaining);

        return (state, opening, encounter);
    }

    /// <summary>
    /// <b>A knockback moves the pair and no other pair.</b>
    ///
    /// <para>This is a reading, and <c>docs/guide/play-engine.md</c> says so: p.73's ranges are
    /// pairwise — "the GM always determines the initial range class between combatants" — because
    /// there is no board and no distance from a fixed point, so "flies backwards" can only be said
    /// of the two characters involved. A third character standing by is neither nearer nor farther
    /// for it, and an engine that had moved every pair the target is in would have thrown the whole
    /// room apart on one point of Resolve.</para>
    ///
    /// <para>The control is the pair that <em>does</em> move: a fixture in which nothing moved at
    /// all would satisfy every assertion below.</para>
    /// </summary>
    [Fact]
    public void AKnockbackMovesThePairAndLeavesEveryOtherPairWhereItWas()
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        var bystander = Combatant.Villain("bystander", "the bystander", edge: 5, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        // 8d of Might for 6 successes against 2d of Toughness for none, which is the entry's
        // minimum_damage exactly.
        var dice = new ScriptedDice([.. FacesFor(8, 6), .. FacesFor(2, 0)]);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain, bystander], opening: RangeBand.Close);

        state = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed)).State;

        state = encounter.Step(state, new SpendResolve("hero", ResolveSpend.Knockback)).State;

        // The control: the pair the blow was between did move.
        Assert.NotEqual(RangeBand.Close, state.RangeBetween("hero", "villain"));

        // And nobody else did. The target is no farther from the bystander for having been thrown,
        // because there is nothing in p.73 that a pairwise band could be measured against.
        Assert.Equal(RangeBand.Close, state.RangeBetween("villain", "bystander"));
        Assert.Equal(RangeBand.Close, state.RangeBetween("hero", "bystander"));

        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// <b>Knockback is bought off the damage type the entry names and nothing else.</b> p.78 opens
    /// on "an attack that inflicts subdual damage"; a killing blow of the same size buys nothing,
    /// and the refusal says which kind it was.
    /// </summary>
    [Fact]
    public void AKnockbackIsBoughtOffSubdualDamageAndNothingElse()
    {
        var rule = _play.GetCombat("knockback").Knockback!;

        // The control on the data: the entry names a damage kind this engine has.
        Assert.True(Enum.TryParse<DamageKind>(rule.RequiresDamageType, ignoreCase: true, out var required));
        Assert.Equal(DamageKind.Subdual, required);

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        // A lethal blow halves the 2d Toughness answering it, so this is one die fewer than the
        // subdual fixture above rolls.
        var dice = new ScriptedDice(6, 6, 6, 1, 1, 1, 1, 1, 1);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);

        state = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Lethal, AttackType.Unarmed)).State;

        // The control: the blow is big enough, so the refusal below is about the kind and not the size.
        Assert.Equal(12 - rule.MinimumDamage, state["villain"].CurrentHealth);

        var refused = encounter.Step(state, new SpendResolve("hero", ResolveSpend.Knockback));
        var line = Assert.Single(refused.Added);

        Assert.Equal("knockback", line.Rule);
        Assert.Contains(rule.RequiresDamageType, line.Text, StringComparison.Ordinal);
        Assert.Contains("that blow was lethal", line.Text, StringComparison.Ordinal);

        // Nothing was spent and nobody moved.
        Assert.Equal(3, refused.State["hero"].Resolve);
        Assert.Equal(RangeBand.Close, refused.State.RangeBetween("hero", "villain"));
        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// <b>A lured attack lands on the person it was lured onto, who rolls their own defence, and the
    /// lurer loses the turn p.79 charges them.</b>
    ///
    /// <para>p.79 prints no worked example, so this is a property with the controls the page itself
    /// supplies: the attack has to have <em>missed</em> the lurer by at least
    /// <c>defense_must_exceed_the_attack_roll_by</c> before the purchase is legal at all, so the
    /// Health the new target loses cannot be damage the lurer had already taken — nothing was taken.
    /// </para>
    ///
    /// <para>The new target answers with their own defence against the <em>same</em> attack roll,
    /// which is the entry's <c>the_new_target_makes_their_own_defense_roll</c> and is what makes the
    /// redirect a fresh outcome rather than the old one moved sideways.</para>
    /// </summary>
    [Fact]
    public void ALureSendsTheAttackIntoSomebodyElseAndCostsTheLurerTheirTurn()
    {
        var rule = _play.GetCombat("luring").Luring!;
        var rate = _play.GetCombat("damage").Damage!.DamagePerNetSuccess;

        // The controls on the data: the purchase costs something, asks for a margin, and is allowed
        // to be aimed at a person at all.
        Assert.True(rule.CostResolve > 0);
        Assert.True(rule.DefenseMustExceedTheAttackRollBy > 0);
        Assert.True(rule.MayRedirectOntoAPerson);
        Assert.True(rule.TheNewTargetMakesTheirOwnDefenseRoll);

        var villain = Combatant.Villain("villain", "the Villain", edge: 10, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 },
            ["toughness"]);

        var hero = Combatant.Hero("hero", "the Hero", edge: 8, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["agility"] = 8 },
            ["agility"]);

        var bystander = Combatant.Villain("bystander", "the bystander", edge: 5, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        // 6d of Might for 1 success, 8d of Agility for 4 — a miss by 3 — and then 2d of the
        // bystander's Toughness for none against that same 1.
        var dice = new ScriptedDice(4, 1, 1, 1, 1, 1, 6, 6, 1, 1, 1, 1, 1, 1, 1, 1);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([villain, hero, bystander]);

        Assert.Equal(["villain", "hero", "bystander"], state.TurnOrder);

        var swing = encounter.Step(state, new Attack(
            "villain", "hero", "might", DamageKind.Subdual, AttackType.Unarmed));

        state = swing.State;

        // The positive controls: the printed counts, an active defence, and nothing landed on the
        // Hero — so the Health the bystander loses below is the redirect and not a transfer.
        Assert.Contains(swing.Added, l =>
            l.Text.Contains("might 6d for 1 successes", StringComparison.Ordinal)
            && l.Text.Contains("agility 8d for 4", StringComparison.Ordinal));

        Assert.True(state.LastAttack!.DefenceWasActive);
        Assert.Equal(rule.DefenseMustExceedTheAttackRollBy,
            state.LastAttack.DefenceSuccesses - state.LastAttack.AttackSuccesses);
        Assert.Equal(10, state["hero"].CurrentHealth);
        Assert.Equal(10, state["bystander"].CurrentHealth);

        var lured = encounter.Step(state, new SpendResolve(
            "hero", ResolveSpend.Luring, Target: "bystander"));

        state = lured.State;

        // The attack landed on the bystander: 1 net success against no successes at all.
        Assert.Equal(10 - rate, state["bystander"].CurrentHealth);
        Assert.Equal(10, state["hero"].CurrentHealth);
        Assert.Equal(3 - rule.CostResolve, state["hero"].Resolve);

        Assert.Contains(lured.Added, l =>
            string.Equals(l.Rule, "luring", StringComparison.Ordinal)
            && l.Text.Contains("strikes the bystander instead", StringComparison.Ordinal)
            && l.Text.Contains(rule.RedirectingOntoAPersonCosts, StringComparison.Ordinal));

        // And the turn it costs: the Hero had not acted this page, so this is the turn they lose.
        Assert.Equal(["villain", "bystander"], state.TurnOrder);

        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// <b>The three things p.79 asks of a lure, each refused on its own.</b> A passive defence is
    /// not moving out of the way; a margin below
    /// <c>defense_must_exceed_the_attack_roll_by</c> is not a lure; and a lure with nobody named has
    /// only <c>redirects_to</c> to land on, which is scenery this engine has not got. None of the
    /// three spends a point.
    /// </summary>
    [Theory]
    [InlineData(true, 3, "beat that attack by")]
    [InlineData(false, 1, "passive defence")]
    [InlineData(true, 1, "no scenery")]
    public void ALureIsRefusedWithoutAnActiveDefenceAMarginAndSomebodyToLureItOnto(
        bool dodges, int attackSuccesses, string why)
    {
        var villain = Combatant.Villain("villain", "the Villain", edge: 10, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 },
            ["toughness"]);

        // The one Trait they have is the one they answer with: p.75's Unarmed row offers Agility and
        // Toughness to anybody who has them, so a Hero holding both would dodge whatever this row is
        // about.
        var hero = Combatant.Hero("hero", "the Hero", edge: 8, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { [dodges ? "agility" : "toughness"] = 8 },
            [dodges ? "agility" : "toughness"]);

        var bystander = Combatant.Villain("bystander", "the bystander", edge: 5, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        var dice = new ScriptedDice([.. FacesFor(6, attackSuccesses), .. FacesFor(8, 4)]);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([villain, hero, bystander]);

        state = encounter.Step(state, new Attack(
            "villain", "hero", "might", DamageKind.Subdual, AttackType.Unarmed)).State;

        // The control: the roll really is the one this row is about.
        Assert.Equal(attackSuccesses, state.LastAttack!.AttackSuccesses);
        Assert.Equal(4, state.LastAttack.DefenceSuccesses);
        Assert.Equal(dodges, state.LastAttack.DefenceWasActive);

        var refused = encounter.Step(state, new SpendResolve(
            "hero", ResolveSpend.Luring,
            Target: string.Equals(why, "no scenery", StringComparison.Ordinal) ? null : "bystander"));

        var line = Assert.Single(refused.Added);

        Assert.Equal("luring", line.Rule);
        Assert.Contains(why, line.Text, StringComparison.Ordinal);

        // Nothing was spent, nobody was hit, and nobody lost a turn.
        Assert.Equal(3, refused.State["hero"].Resolve);
        Assert.Equal(10, refused.State["bystander"].CurrentHealth);
        Assert.Equal(["villain", "hero", "bystander"], refused.State.TurnOrder);
        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// <b>A team attack's sixes are thrown again, and again while they keep coming.</b>
    ///
    /// <para>This is the purchase <c>IDiceSource</c>'s shape exists for: p.79 rerolls a <em>face</em>,
    /// so a source answering in successes could not say which dice were sixes. The script is chosen
    /// so that the reroll itself produces one — two sixes go back in, one of them comes up a six
    /// again, and that one goes back in a third time — and
    /// <see cref="ScriptedDice.Remaining"/> at zero is what proves the recursion happened rather
    /// than the engine stopping after one round: a single round would leave the last scripted face
    /// unasked for.</para>
    ///
    /// <para>The counts come first. The pool has to have carried the entry's own
    /// <c>attack_bonus_dice</c>, and the roll has to have scored what the script pays for, before
    /// the purchase is asked to add anything to it.</para>
    /// </summary>
    [Fact]
    public void ATeamAttacksSixesExplodeAndKeepExplodingWhileTheyComeUp()
    {
        var rule = _play.GetCombat("team_attacks").TeamAttack!;
        var rate = _play.GetCombat("damage").Damage!.DamagePerNetSuccess;

        // The controls on the data: there is a bonus, the purchase costs something, and the
        // explosion is meant to recurse — the last is what the third scripted throw is about.
        Assert.True(rule.AttackBonusDice > 0);
        Assert.True(rule.CostResolveToMakeSixesExplode > 0);
        Assert.True(rule.ExplosionRecursesWhileSixesKeepComing);

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        // 10d — 8d of Might and the entry's two — showing two sixes for 4 successes; 2d of Toughness
        // for none; then the two sixes thrown again as a 6 and a 4, and that 6 thrown again as a 1.
        var dice = new ScriptedDice(6, 6, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 6, 4, 1);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);

        var blow = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed, Team: true));

        state = blow.State;

        // The positive controls: the bonus is in the pool, the roll scored what it should, and the
        // outcome landed once already — so the Health below moves because of the explosion.
        Assert.Contains(blow.Added, l =>
            l.Text.Contains($"might {8 + rule.AttackBonusDice}d for 4 successes", StringComparison.Ordinal)
            && l.Text.Contains("toughness 2d for 0", StringComparison.Ordinal));

        Assert.Equal(12 - (4 * rate), state["villain"].CurrentHealth);
        Assert.Equal(["villain"], state.TeamAttacked);

        var exploded = encounter.Step(state, new SpendResolve("hero", ResolveSpend.TeamAttack));

        state = exploded.State;

        // Two sixes are worth 2 successes apiece and a four is worth one, so the reroll adds 3:
        // 4 successes become 7, and the outcome is recomputed against the target as they were.
        Assert.Equal(7, state.LastAttack!.AttackSuccesses);
        Assert.Equal(12 - (7 * rate), state["villain"].CurrentHealth);
        Assert.Equal(3 - rule.CostResolveToMakeSixesExplode, state["hero"].Resolve);

        Assert.Contains(exploded.Added, l =>
            string.Equals(l.Rule, "team_attacks", StringComparison.Ordinal)
            && l.Text.Contains("over 2 rounds", StringComparison.Ordinal));

        // Every scripted face was asked for, which is only true if the reroll's own six was thrown
        // again — and no six is left on the roll for a second point to buy.
        Assert.Equal(0, dice.Remaining);

        var again = encounter.Step(state, new SpendResolve("hero", ResolveSpend.TeamAttack));

        Assert.Contains("left on that roll to explode", Assert.Single(again.Added).Text,
            StringComparison.Ordinal);
        Assert.Equal(3 - rule.CostResolveToMakeSixesExplode, again.State["hero"].Resolve);
    }

    /// <summary>
    /// <b>p.79's one team attack per target per battle, and the two ways out of it quoted rather
    /// than taken.</b> The limit is per battle, so a page turn does not clear it — which is why the
    /// second attack here is made on the following page.
    /// </summary>
    [Fact]
    public void ATargetIsTeamAttackedOnceABattleAndThePageTurnDoesNotResetIt()
    {
        var rule = _play.GetCombat("team_attacks").TeamAttack!;

        Assert.Equal(1, rule.LimitPerTargetPerBattle);

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 40,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(12));
        var state = encounter.Begin([hero, villain]);

        state = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed, Team: true)).State;

        // The control: the first one went through and is on the record.
        Assert.Equal(["villain"], state.TeamAttacked);

        state = encounter.Step(state, new EndTurn("hero")).State;
        state = encounter.Step(state, new EndTurn("villain")).State;
        state = encounter.Step(state, new EndPage("")).State;

        Assert.Equal(2, state.Page);

        var second = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed, Team: true));

        var line = Assert.Single(second.Added);

        Assert.Equal("team_attacks", line.Rule);
        Assert.Contains(rule.TheLimitMayBeLiftedBy, line.Text, StringComparison.Ordinal);

        // Nothing was rolled and nobody was hit: the refusal came before the dice.
        Assert.Equal(state["villain"].CurrentHealth, second.State["villain"].CurrentHealth);
        Assert.Equal(["villain"], second.State.TeamAttacked);

        // And without the flag the attack is an ordinary one, which is the way p.79's own exception
        // is taken: the GM ruling otherwise is not this engine's decision.
        var ordinary = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed));

        Assert.Contains(ordinary.Added, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The faces on the table are the faces of the roll on the table.</b>
    ///
    /// <para><c>ResolvedAttack</c> carries the successes and the faces, and they are two accounts of
    /// one roll. p.84's reroll picks the whole pool back up, so the faces it came up with are the
    /// discarded ones — and p.79 explodes "your 6s", which is a rule about the faces. An engine
    /// that moved the count and left the faces where they were threw the sixes of a roll nobody is
    /// looking at any more.</para>
    ///
    /// <para>The controls come first, as everywhere here: the first roll has to have shown the sixes
    /// the fixture is about, and the reroll has to have been the one kept — p.85's floor keeps the
    /// better of the two, so a reroll that came up worse would leave the first roll standing and its
    /// faces would be the right ones.</para>
    /// </summary>
    [Fact]
    public void ARerollExplodesItsOwnSixesAndNotTheOnesItThrewAway()
    {
        // The face is the success map's own top key, not a 6 typed here.
        var top = new SuccessCounter(_play).HighestFace;

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 5,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 40,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        // 10d showing two sixes for 4; 2d of Toughness for none; then the reroll of all ten, as
        // five fours and five ones — five successes, and not a six among them.
        var dice = new ScriptedDice(
        [
            .. FacesFor(10, 4),
            .. FacesFor(2, 0),
            .. Enumerable.Repeat(4, 5), .. Enumerable.Repeat(1, 5)
        ]);

        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);

        state = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed, Team: true)).State;

        // The control: the roll the reroll discards is the one with the sixes on it.
        Assert.Equal(4, state.LastAttack!.AttackSuccesses);
        Assert.Contains(top, state.LastAttack.AttackFaces);

        state = encounter.Step(state, new SpendResolve("hero", ResolveSpend.Reroll)).State;

        // The reroll was the better of the two, so it is the roll on the table — and the faces have
        // to be its own.
        Assert.Equal(5, state.LastAttack!.AttackSuccesses);
        Assert.DoesNotContain(top, state.LastAttack.AttackFaces);

        // So there is nothing left to explode, and the refusal says so rather than throwing dice
        // nobody is holding.
        var explode = encounter.Step(state, new SpendResolve("hero", ResolveSpend.TeamAttack));

        Assert.Contains("left on that roll to explode", Assert.Single(explode.Added).Text,
            StringComparison.Ordinal);

        // Nothing was spent on the refusal: the attack and the reroll are the only points gone.
        Assert.Equal(4, explode.State["hero"].Resolve);
        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// <b>A die bought with Resolve is part of the roll that explodes.</b>
    ///
    /// <para><c>spend_reroll_challenge_roll</c>'s <c>includes_dice_bought_with_resolve</c> says in as
    /// many words that a bought die is one of the roll's own, which is why <c>BuyDice</c> grows the
    /// pool. Its face is the same claim from the other side: a six a Hero paid for is a six on the
    /// roll, and p.79 explodes the roll's sixes.</para>
    ///
    /// <para>The control is the first roll, which is required to have no six on it at all — so the
    /// explosion below can only be the bought die, and a fixture where the pool already held one
    /// would prove nothing.</para>
    /// </summary>
    [Fact]
    public void ADieBoughtWithResolveIsOneOfTheSixesATeamAttackExplodes()
    {
        var top = new SuccessCounter(_play).HighestFace;
        var gained = _play.GetResolve("spend_challenge_roll_dice").Spend!.DiceGained!.Value;

        // The control on the data: a point buys a die, or there is nothing to put a six on.
        Assert.True(gained > 0);

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 5,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 40,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"]);

        // 10d of fours and ones for five successes and no six; 2d of Toughness for none; the bought
        // die coming up a six for two more; and that six thrown again as a one.
        var dice = new ScriptedDice(
        [
            .. Enumerable.Repeat(4, 5), .. Enumerable.Repeat(1, 5),
            .. FacesFor(2, 0),
            .. Enumerable.Repeat(top, gained),
            .. Enumerable.Repeat(1, gained)
        ]);

        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);

        state = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed, Team: true)).State;

        // The control: no six was rolled, so the one that explodes below is the one that was bought.
        Assert.Equal(5, state.LastAttack!.AttackSuccesses);
        Assert.DoesNotContain(top, state.LastAttack.AttackFaces);

        state = encounter.Step(state, new SpendResolve("hero", ResolveSpend.ExtraDice)).State;

        Assert.Equal(5 + (2 * gained), state.LastAttack!.AttackSuccesses);
        Assert.Contains(top, state.LastAttack.AttackFaces);

        var exploded = encounter.Step(state, new SpendResolve("hero", ResolveSpend.TeamAttack));

        Assert.Contains(exploded.Added, l =>
            string.Equals(l.Rule, "team_attacks", StringComparison.Ordinal)
            && l.Text.Contains($"{gained} thrown again", StringComparison.Ordinal));

        // The reroll of it scored nothing, so the count stands where the bought die left it — and
        // every scripted face was asked for, which is what says the bought six was thrown again.
        Assert.Equal(5 + (2 * gained), exploded.State.LastAttack!.AttackSuccesses);
        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// <b>A purchase the GM's pool did not make does not say it did.</b>
    ///
    /// <para>p.85's first spend announces itself — "the GM spends Adversity on X, which buys Y" —
    /// and then hands the purchase to the same code a Hero's own point runs. Every one of those can
    /// refuse: there is no roll on the table, nobody is down under an effect, the blow was the wrong
    /// kind. The announcement was written before the purchase was attempted, so a refusal left a
    /// line claiming a point had been spent standing above a line saying nothing happened, with the
    /// pool untouched — a reader counting spends off the ledger and a reader reading the pool would
    /// have given two different accounts of the same fight.</para>
    ///
    /// <para><b>Every purchase the GM may name is driven</b>, and the situation is one in which none
    /// of them can succeed. The positive control is the other half of the same test: with a roll on
    /// the table the announcement is there and the pool has moved, so this is not a check satisfied
    /// by an engine that had stopped announcing anything.</para>
    ///
    /// <para><b>Seizing the initiative had to be bought before the loop rather than left out of
    /// it.</b> It is the one purchase that needs nothing to have happened — a fresh page is exactly
    /// when a character buys a place at the front — so on an untouched page it succeeds, correctly,
    /// and the situation this test needs is not "nothing has happened" but "nothing any of these can
    /// act on". Buying it first is what produces that, and it keeps the purchase inside the loop:
    /// the second one is refused by p.73's own duration.</para>
    /// </summary>
    [Fact]
    public void TheGmsPoolSaysNothingMovedWhenNothingMoved()
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 3 },
            ["toughness"]);

        // Every line that claims the GM paid says so in these words — p.85's announcement and the
        // purchase's own line both — and no refusal anywhere does.
        const string Claim = "Adversity on";

        var encounter = new Encounter(_play, new SeededDice(31));
        var opened = encounter.Begin([hero, villain], challengeLevel: 2);

        // The control on the fixture: there is a pool to spend twice over, so a refusal below is the
        // purchase's and not "the GM has 0 Adversity".
        Assert.True(opened.Adversity > 1, $"the fight opened on {opened.Adversity} Adversity");

        // Seizing the initiative is bought first, because it is the one purchase a page where
        // nothing has happened is the right moment for. That both makes it a spend this loop can
        // ask about — the second is refused by the duration p.73 prints — and leaves the page
        // otherwise as it was.
        var first = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.SeizeInitiative));

        var quiet = first.State;

        Assert.True(quiet.Adversity < opened.Adversity,
            "the GM's pool did not move buying a seized initiative, which needs no roll and nobody "
            + "down — so the loop below would be measuring a purchase that had stopped working.");

        foreach (var purchase in Enum.GetValues<ResolveSpend>())
        {
            var step = encounter.Step(quiet, new SpendAdversity(
                "villain", AdversitySpend.AnythingResolveCan, AsResolve: purchase));

            Assert.True(quiet.Adversity == step.State.Adversity,
                $"the GM's pool moved buying {purchase} on a page where nothing had happened yet.");

            Assert.DoesNotContain(step.Added, l => l.Text.Contains(Claim, StringComparison.Ordinal));
        }

        // The positive control: with a roll on the table the announcement is made and the pool
        // falls. The roll is the Villain's, because p.85 spends the GM's pool "on behalf of any
        // NPC" — this control used to buy the dice for the Hero, which the engine now refuses.
        var passed = encounter.Step(opened, new EndTurn("hero")).State;

        var rolled = encounter.Step(passed, new Attack(
            "villain", "hero", "might", DamageKind.Subdual, AttackType.Unarmed)).State;

        var bought = encounter.Step(rolled, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.ExtraDice));

        Assert.Contains(bought.Added, l => l.Text.Contains(Claim, StringComparison.Ordinal));
        Assert.True(bought.State.Adversity < rolled.Adversity);
    }

    /// <summary>
    /// <b>The announcement is read before what it bought, and the position is the whole of what the
    /// fix above left to check.</b>
    ///
    /// <para>p.85's line used to be written before the dispatch, and every one of these purchases can
    /// refuse — so a refusal left "the GM spends Adversity on X, which buys Y" standing above a line
    /// saying nothing happened. It is written after the purchase now and <em>inserted</em> at the
    /// index it would have occupied, so that a reader still meets the announcement first.
    /// <b>Nothing held the second half.</b> Appending it instead leaves every test in this
    /// repository green — 4,849 of them — with the ledger reading "the Villain seizes the
    /// initiative" and then, underneath, the GM paying for it.</para>
    ///
    /// <para>That is not cosmetic in a document whose purpose is that any figure in a run traces to
    /// a printed page: the announcement is the only line naming p.85, and a reader working down the
    /// page meets the effect with no purchase above it. Two purchases are driven — one that needs
    /// nothing to have happened, one that needs somebody on the floor — because the insert is one
    /// index shared by all ten.</para>
    /// </summary>
    [Theory]
    [InlineData(ResolveSpend.SeizeInitiative, "seizing_initiative")]
    [InlineData(ResolveSpend.InstantRecovery, "instant_recovery")]
    public void TheGmsAnnouncementIsReadBeforeThePurchaseItPaidFor(ResolveSpend purchase, string bought)
    {
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;
        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3, traits, ["toughness"]);

        // Down, so p.76's purchase has somebody to bring round; p.73's does not mind either way.
        var villain = Combatant
            .Villain("villain", "the Villain", edge: 5, health: 10, traits, ["toughness"])
            .WithHealth(floor);

        var encounter = new Encounter(_play, new SeededDice(21));
        var opened = encounter.Begin([hero, villain], challengeLevel: 2);

        var step = encounter.Step(opened, new SpendAdversity(
            "villain", AdversitySpend.AnythingResolveCan, AsResolve: purchase));

        // The control: the pool really paid, or the two lines below are being looked for in a
        // refusal and their order would mean nothing.
        Assert.True(step.State.Adversity < opened.Adversity,
            $"the GM's pool did not pay for {purchase}, so there is no announcement to place.");

        var announcement = step.Added
            .Select((line, at) => (line, at))
            .Single(l => string.Equals(
                l.line.Rule, "adversity_spend_anything_resolve_can", StringComparison.Ordinal));

        var effect = step.Added
            .Select((line, at) => (line, at))
            .First(l => string.Equals(l.line.Rule, bought, StringComparison.Ordinal));

        Assert.True(
            announcement.at < effect.at,
            $"p.85's announcement is at line {announcement.at} of this step and the {bought} line it "
            + $"paid for is at {effect.at}: a reader working down the ledger meets the effect before "
            + "anything says the GM bought it. The line is written after the purchase, because a "
            + "purchase that refuses must not leave an announcement standing above it, and it is "
            + "inserted at the index it would have occupied for exactly this reason.");
    }

    /// <summary>
    /// <b>p.85's "on behalf of any NPC" means any NPC, and the pool pays once.</b>
    ///
    /// <para>A Minion group is an NPC like any other and holds no Resolve — <c>Combatant.Hero</c> is
    /// the only factory that takes a pool — so the GM's point is the only way one of these purchases
    /// reaches them at all. The figure asserted is the pool's arithmetic and never a die roll: the
    /// price is the entry's own <c>cost_resolve</c> times the points asked for, and a purchase that
    /// charged the gate's figure as well as the entry's would take twice that.</para>
    /// </summary>
    [Fact]
    public void TheGmsPoolBuysForAMinionGroupAndIsChargedExactlyOnce()
    {
        var cost = _play.GetResolve("spend_challenge_roll_dice").Spend!.CostResolve!.Value;

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 20, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var minions = Combatant.Minions("robots", "the robots", threat: 5, groupSize: 4, "threat");

        var encounter = new Encounter(_play, new SeededDice(33));
        var state = encounter.Begin([hero, minions], challengeLevel: 3);

        // The Minions act last, so the page has to reach them before they can swing.
        state = encounter.Step(state, new Hold("hero")).State;
        state = encounter.Step(state, new EndTurn("hero")).State;

        state = encounter.Step(state, new Attack(
            "robots", "hero", "threat", DamageKind.Subdual, AttackType.Unarmed)).State;

        // The controls: the roll on the table is the Minions' own, and they hold no Resolve to draw
        // on — so what pays below can only be the GM's pool.
        Assert.Equal("robots", state.LastAttack!.Actor);
        Assert.Equal(0, state["robots"].Resolve);

        var before = state.Adversity;

        var bought = encounter.Step(state, new SpendAdversity(
            "robots", AdversitySpend.AnythingResolveCan, Points: 2, AsResolve: ResolveSpend.ExtraDice));

        Assert.Equal(before - (cost * 2), bought.State.Adversity);
        Assert.Equal(0, bought.State["robots"].Resolve);
        Assert.Equal(3, bought.State["hero"].Resolve);
    }

    // ── Citations ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every line the engine writes names a real entry and carries that entry's page.</b>
    ///
    /// <para>The ledger's whole value is that any figure in a run traces to a printed page, and a
    /// refusal is a rule applied just as much as a hit is — so the class of line a reader most needs
    /// to check must not be the one class that cannot be checked. Refusals used to print <c>—</c>
    /// where the citation goes, an unimplemented spend printed a C# enum member's name where the
    /// rule id goes, and <c>NotTheirTurn</c> printed one entry's id against a different entry's page.
    /// </para>
    ///
    /// <para>Both halves are checked against the store itself rather than against a list written
    /// here: the id has to be an entry <see cref="PlayRulesRepository.EntryIds"/> knows, and the
    /// citation has to be that entry's own <c>source_ref</c>.</para>
    /// </summary>
    [Fact]
    public void EveryLedgerLineCitesAnEntryThatExistsAndThatEntrysPage()
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 8, resolve: 4,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 8, ["toughness"] = 5, ["agility"] = 4
            },
            ["toughness", "agility"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 8, ["toughness"] = 5, ["agility"] = 4
            },
            ["toughness", "agility"]);

        // <b>The Gear Limit is on so that page one carries the one `not yet implemented` line this
        // engine can still produce.</b> It used to come off p.85's first purchase naming one of the
        // four that charged the buyer's own pool; those are bought now, so the switch is where that
        // control has moved — and the assertions below check the entry it cites is real and cited to
        // its own page, which is the whole subject of this test.
        var encounter = new Encounter(
            _play, new SeededDice(6), TableRules.Book with { FatalDamage = true, RaisedGearLimit = true });

        var state = encounter.Begin([hero, villain]);

        // Every refusal this slice can produce, and then a whole fight on top of them.
        var refusals = new List<Intent>
        {
            new Attack("villain", "hero", "might"),                  // not their turn
            new Attack("hero", "villain", "no_such_trait"),           // no rank to throw
            new BreakFree("hero", "willpower", Threshold: 3),         // no effect to break out of
            new SpendResolve("villain", ResolveSpend.Reroll),         // holds no Resolve
            new SpendResolve("hero", ResolveSpend.Reroll),            // no roll on the table
            new SpendResolve("hero", ResolveSpend.ExtraDice),         // ditto
            new SpendResolve("hero", ResolveSpend.KeepingHold),
            new SpendResolve("hero", ResolveSpend.InstantRecovery),  // refused: nothing to recover from
            new SpendResolve("hero", ResolveSpend.Stabilise),        // refused: nobody is dying
            new SpendResolve("hero", ResolveSpend.Knockback),
            new SpendResolve("hero", ResolveSpend.Luring),
            new SpendResolve("hero", ResolveSpend.TeamAttack),
            new SpendAdversity("villain", AdversitySpend.Villainy),          // refused: names no act
            new SpendAdversity("villain", AdversitySpend.Misfortune, Points: 999),
            // p.85's first purchase, refused by the purchase it names rather than by the pool: the
            // Villain is not on a clock, so there is nothing to stabilise and nothing is spent.
            new SpendAdversity(
                "villain", AdversitySpend.AnythingResolveCan, AsResolve: ResolveSpend.Stabilise)
        };

        foreach (var intent in refusals) state = encounter.Step(state, intent).State;

        state = encounter.RunToEnd(state, new AttackTheWeakest(_play), maxPages: 20);

        // The control: the refusals really happened, so the assertions below are about lines that
        // exist. A run that produced only ordinary lines would satisfy them trivially.
        Assert.Contains(state.Ledger.Lines, l =>
            l.Text.Contains("holds no Resolve", StringComparison.Ordinal));
        Assert.Contains(state.Ledger.Lines, l =>
            l.Text.Contains("not yet implemented", StringComparison.Ordinal)
            && string.Equals(l.Rule, "gritty_raised_gear_limit", StringComparison.Ordinal));
        Assert.Contains(state.Ledger.Lines, l =>
            l.Text.Contains("is not the Villain's turn", StringComparison.Ordinal));
        Assert.Contains(state.Ledger.Lines, l =>
            l.Text.Contains("there is no clock to stop", StringComparison.Ordinal));

        var pages = _play.EntryIds()
            .Select(e => e.Id)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, SourceRefOf, StringComparer.Ordinal);

        foreach (var line in state.Ledger.Lines)
        {
            Assert.True(pages.ContainsKey(line.Rule),
                $"the ledger names a rule '{line.Rule}' that is in none of the five play files: {line.Text}");

            Assert.True(string.Equals(pages[line.Rule], line.SourceRef, StringComparison.Ordinal),
                $"the ledger cites '{line.SourceRef}' for {line.Rule}, whose own source_ref is "
                + $"'{pages[line.Rule]}': {line.Text}");
        }
    }

    /// <summary>
    /// Faces for a pool of <paramref name="pool"/> dice worth exactly <paramref name="successes"/>
    /// under the printed map — sixes first, one four if an odd success is left, then ones.
    ///
    /// <para>The same bridge <see cref="PlayWorkedExamples"/> needs and for the same reason: a rule
    /// is stated in successes and <c>IDiceSource</c> answers in faces. It throws rather than
    /// approximating, so a fixture cannot quietly assert a count its pool could not produce.</para>
    /// </summary>
    private static int[] FacesFor(int pool, int successes)
    {
        var sixes = successes / 2;
        var four = successes % 2;

        if (sixes + four > pool)
        {
            throw new ArgumentOutOfRangeException(
                nameof(successes), successes,
                $"{pool} dice cannot be made to score {successes} successes under the printed map.");
        }

        return
        [
            .. Enumerable.Repeat(6, sixes),
            .. Enumerable.Repeat(4, four),
            .. Enumerable.Repeat(1, pool - sixes - four)
        ];
    }

    /// <summary>One entry's <c>source_ref</c>, whichever of the five files it is in.</summary>
    private string SourceRefOf(string id)
    {
        foreach (var (file, entryId) in _play.EntryIds())
        {
            if (!string.Equals(entryId, id, StringComparison.Ordinal)) continue;

            return file switch
            {
                PlayRulesRepository.PlayMetaFile => _play.GetMeta(id).SourceRef,
                PlayRulesRepository.ChallengeFile => _play.GetChallenge(id).SourceRef,
                PlayRulesRepository.CombatFile => _play.GetCombat(id).SourceRef,
                PlayRulesRepository.GrittyFile => _play.GetGritty(id).SourceRef,
                PlayRulesRepository.ResolveFile => _play.GetResolve(id).SourceRef,
                var other => throw new InvalidOperationException($"Unknown play rules file {other}.")
            };
        }

        throw new KeyNotFoundException($"No entry '{id}' in the play rules.");
    }

    // ── Rounding, and the one factor that is prose ───────────────────────────

    /// <summary>
    /// <b>Every halving reads <c>play_meta.half_rounds_up</c>, and none of them is a
    /// <c>Math.Ceiling</c> typed beside the rule.</b>
    ///
    /// <para>The check is a twin, on <c>CLAUDE.md</c>'s reasoning: reading the code cannot tell you
    /// whether a number came from the file, so the file's one rounding line is flipped and the
    /// engine's answers have to move with it. Three halvings are driven — the Toughness that answers
    /// a lethal attack (p.75), the defences going all-out costs (p.78), and a Foe's Health (p.75) —
    /// and each is required to differ under the flip. A hard-coded ceiling leaves all three
    /// unchanged, which is what this fixture was written against.</para>
    /// </summary>
    [Fact]
    public void EveryHalvingReadsTheGlossarysRoundingDirection()
    {
        var flipped = SubstitutedPlayRules.With(
            PlayRulesRepository.PlayMetaFile, "\"direction\": \"up\"", "\"direction\": \"down\"");

        // The control: the shipped file really does round up, so "the answers moved" below is the
        // flip biting rather than two arbitrary numbers.
        Assert.Equal("up", _play.GetMeta("half_rounds_up").Rounding!.Direction);
        Assert.Equal("down", flipped.GetMeta("half_rounds_up").Rounding!.Direction);

        // p.75: a Toughness of 5 answers a lethal attack at half — 3 rounding up, 2 rounding down.
        Assert.Contains("defends with toughness 3d", DefenceAgainst(_play, DamageKind.Lethal, allOut: false),
            StringComparison.Ordinal);
        Assert.Contains("defends with toughness 2d", DefenceAgainst(flipped, DamageKind.Lethal, allOut: false),
            StringComparison.Ordinal);

        // p.78: going all-out halves the attacker's own defences. Subdual, so the lethal halving
        // above is out of the way and this is the only halving in the figure.
        Assert.Contains("defends with toughness 3d", DefenceAgainst(_play, DamageKind.Subdual, allOut: true),
            StringComparison.Ordinal);
        Assert.Contains("defends with toughness 2d", DefenceAgainst(flipped, DamageKind.Subdual, allOut: true),
            StringComparison.Ordinal);

        // p.75: "Foes halve the result", of a Health the character engine computed as an odd 3.
        var rules = new RulesFixture();
        var sheet = rules.LegalSheet();
        sheet.Name = "Odd Health";
        sheet.AbilityRanks["toughness"] = 3;
        sheet.AbilityRanks["might"] = 2;

        Assert.Equal(3, rules.Derived.CalculateHealth(sheet));
        Assert.Equal(2, CombatantFactory
            .From(sheet, rules.Rules, rules.Derived, _play, CombatantKind.Foe).FullHealth);
        Assert.Equal(1, CombatantFactory
            .From(sheet, rules.Rules, rules.Derived, flipped, CombatantKind.Foe).FullHealth);
    }

    /// <summary>
    /// One attack, and the ledger line saying what the target answered it with. The pool is what the
    /// halvings landed on, so reading it out of the sentence the engine printed is the cheapest way
    /// to watch a rounding rule move.
    /// </summary>
    private static string DefenceAgainst(PlayRulesRepository play, DamageKind damage, bool allOut)
    {
        // The target has the higher Edge, so they act first and can commit to an all-out attack of
        // their own before the blow that measures their defences lands. The attacker's 8d Might is
        // greater than the target's 5d Toughness, so p.78's "still cannot" guard does not fire and
        // the all-out halving is what this fixture is measuring.
        var target = Combatant.Hero("target", "the target", edge: 9, health: 12, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 4, ["toughness"] = 5 },
            ["toughness"]);

        var attacker = Combatant.Villain("attacker", "the attacker", edge: 8, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 5 },
            ["toughness"]);

        var encounter = new Encounter(play, new SeededDice(2));
        var state = encounter.Begin([target, attacker]);

        if (allOut)
        {
            state = encounter.Step(state, new Attack("target", "attacker", "might", AllOut: true)).State;
        }

        state = encounter.Step(state, new EndTurn("target")).State;

        var added = encounter.Step(state, new Attack("attacker", "target", "might", damage)).Added;

        return added.Single(l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal)
            && l.Text.Contains("defends with", StringComparison.Ordinal)).Text;
    }

    /// <summary>
    /// <b>The GM's alternative to seizing the initiative reads the printed word it doubles.</b>
    ///
    /// <para>The entry states an effect in prose — "doubles the buyer's effective Edge" — and carries
    /// no multiplier, so the factor of 2 is this engine's reading and the guide's table records it as
    /// one. What the engine may not do is default to 2 against an entry that has stopped saying
    /// "doubles", which would be applying a rule the book no longer prints; it throws instead, and
    /// the throw names the entry.</para>
    /// </summary>
    [Fact]
    public void TheGmAlternativeToSeizingInitiativeReadsThePrintedWordDoubles()
    {
        var table = TableRules.Book with { GmAlternativeToSeizingInitiative = true };

        var fast = Combatant.Villain("fast", "the fast one", edge: 10, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);
        var hero = Combatant.Hero("hero", "the Hero", edge: 6, health: 10, resolve: 2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(1), table);
        var state = encounter.Begin([fast, hero]);

        // The control: 6d Edge is behind 10d before the purchase.
        Assert.Equal(["fast", "hero"], state.TurnOrder);

        state = encounter.Step(state, new EndTurn("fast")).State;
        state = encounter.Step(state, new SpendResolve("hero", ResolveSpend.SeizeInitiative)).State;
        state = encounter.Step(state, new EndTurn("hero")).State;
        state = encounter.Step(state, new EndPage("")).State;

        // Doubled to 12d, the Hero is now in front — and it is a doubling and not a jump to the
        // front, which is the whole of what the alternative changes.
        Assert.Equal(["hero", "fast"], state.TurnOrder);

        var reworded = SubstitutedPlayRules.With(
            PlayRulesRepository.CombatFile,
            "\"effect\": \"doubles the buyer's effective Edge\"",
            "\"effect\": \"puts the buyer one rung up the tie-break ladder\"");

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            new Encounter(reworded, new SeededDice(1), table).Begin([fast, hero]));

        Assert.Contains("seize_initiative_gm_alternative", thrown.Message, StringComparison.Ordinal);
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

    // ── p.75's three situational modifiers ────────────────────────────────────

    /// <summary>
    /// A fight of two, each with exactly the Traits the case under test wants them to have.
    /// </summary>
    private static (Combatant Attacker, Combatant Target) Pair(
        string defenceTrait, int defenceRank, double attackerSize = Combatant.SameSize,
        double targetSize = Combatant.SameSize, bool attackerInvisible = false,
        bool targetInvisible = false, IReadOnlySet<string>? attackerPowers = null,
        IReadOnlySet<string>? targetPowers = null, int attackRank = 8,
        bool targetHardTarget = false)
    {
        var attacker = Combatant.Hero(
            "hero", "the Hero", edge: 9, health: 12, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = attackRank },
            [], side: "heroes", size: attackerSize, invisible: attackerInvisible,
            powers: attackerPowers);

        var target = Combatant.Villain(
            "villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { [defenceTrait] = defenceRank },
            [defenceTrait], side: "villains", size: targetSize, invisible: targetInvisible,
            powers: targetPowers, hardTarget: targetHardTarget);

        return (attacker, target);
    }

    /// <summary>
    /// <b>How many dice one exchange actually threw.</b>
    ///
    /// <para>The instrument for every band below, and it is chosen over parsing a pool out of the
    /// ledger's prose because it measures the thing itself: a modifier that moved the pool by
    /// <em>n</em> dice is a roll that asked the source for <em>n</em> fewer. Every face is a 4 —
    /// one success under <c>play_meta</c>'s map — so nothing here turns on which faces came up.
    /// </para>
    ///
    /// <para><see cref="ScriptedDice"/> throws when it runs out, so a script this long is not a
    /// silent allowance: it is why <see cref="Assert.NotEqual{T}(T, T)"/> on the count below is a
    /// real reading rather than a saturated one.</para>
    /// </summary>
    private (int Thrown, IReadOnlyList<LedgerLine> Lines) Exchange(
        Combatant attacker, Combatant target, Attack attack, Visibility light = Visibility.Clear,
        TableRules? table = null)
    {
        const int Plenty = 300;

        var dice = new ScriptedDice([.. Enumerable.Repeat(4, Plenty)]);
        var encounter = new Encounter(_play, dice, table);
        var state = encounter.Begin([attacker, target], visibility: light);

        // Begin makes no roll with random initiative off, so everything the source hands out from
        // here is the exchange's — asserted rather than assumed.
        Assert.Equal(Plenty, dice.Remaining);

        var step = encounter.Step(state, attack);

        var thrown = Plenty - dice.Remaining;

        // <b>The control every caller below leans on.</b> An attack refused before the roll throws
        // no dice at all, and a comparison of two zeroes is an equality that proves nothing — which
        // is how this helper first reported a modifier working on a fight whose turn order put the
        // wrong combatant first. Every fixture that wants a refusal drives one directly instead.
        Assert.True(thrown > 0,
            "the exchange threw no dice, so it was refused rather than resolved and every "
            + "comparison built on it would be a comparison of nothing");

        return (thrown, step.Added);
    }

    /// <summary>The dice one band of an entry's table prints, off the shipped file.</summary>
    private int BandDice(string entryId, Func<CombatBandModel, string?> word, string printed)
    {
        var entry = _play.GetCombat(entryId);

        var bands = entryId switch
        {
            "modifier_cover" => entry.Cover!.Bands,
            "modifier_size" => entry.Size!.Bands,
            _ => entry.Visibility!.Bands
        };

        var band = Assert.Single(bands, b => string.Equals(word(b), printed, StringComparison.Ordinal));

        // The control on the reading itself: a band worth nothing would make every assertion below
        // an equality between two unchanged numbers.
        Assert.NotEqual(0, band.Dice);

        return band.Dice;
    }

    /// <summary>
    /// <b>Each band of cover moves the attack pool by exactly the dice p.75 prints for it.</b>
    ///
    /// <para>The expected figure is read out of <c>modifier_cover</c>'s own table by the printed
    /// word, never restated here — so a file whose light cover became −2d moves this fixture with
    /// it, and a fixture that had the numbers typed into it would go on agreeing with a page it no
    /// longer matched.</para>
    ///
    /// <para>The control is the exchange in the open, measured first: an engine that threw no dice
    /// at all, or that refused the attack, would satisfy a bare "fewer dice" assertion perfectly.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(Cover.Light, "light")]
    [InlineData(Cover.Heavy, "heavy")]
    [InlineData(Cover.AlmostFull, "almost full")]
    public void EachBandOfCoverMovesTheAttackPoolByExactlyTheDiceItPrints(Cover cover, string printed)
    {
        var (attacker, target) = Pair("toughness", 6);
        var entry = _play.GetCombat("modifier_cover");

        var open = Exchange(attacker, target, new Attack("hero", "villain", "might"));

        // The control: the fight happened, and it threw the attacker's whole rank plus the
        // defender's — an exchange that refused would have thrown nothing.
        Assert.Equal(8 + Halved(6), open.Thrown);
        Assert.Contains(open.Lines, l => string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));
        Assert.DoesNotContain(open.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        var behind = Exchange(attacker, target, new Attack("hero", "villain", "might", Cover: cover));

        Assert.Equal(open.Thrown + BandDice("modifier_cover", b => b.Cover, printed), behind.Thrown);

        // And the line, naming the entry, its printed page, the band and the dice it moved.
        var line = Assert.Single(behind.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains(printed, line.Text, StringComparison.Ordinal);
        Assert.Contains($"{BandDice("modifier_cover", b => b.Cover, printed)}d", line.Text, StringComparison.Ordinal);
        Assert.Contains(entry.Cover!.Affects, line.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Bad light costs the attacker and the defender alike, and the two are separate lines.</b>
    ///
    /// <para>p.75: "Visibility affects your attack rolls <em>and</em> your active defense rolls."
    /// So an exchange in poor light throws two bands' worth fewer dice than the same exchange in
    /// clear air, not one — and the check that distinguishes "applied twice" from "applied once and
    /// doubled" is the pair of ledger lines beside it, one per roller.</para>
    ///
    /// <para><b>The defender answers with an active defence on purpose.</b> The band is
    /// <c>active defense rolls</c>, so the two-sidedness is only reachable where the defence chosen
    /// is one; the passive half of the same claim is the fixture below.</para>
    /// </summary>
    [Theory]
    [InlineData(Visibility.Poor, "poor")]
    [InlineData(Visibility.None, "none")]
    public void BadLightCostsTheAttackerAndTheActiveDefenderAlike(Visibility light, string printed)
    {
        var (attacker, target) = Pair("agility", 6);
        var entry = _play.GetCombat("modifier_visibility");
        var dice = BandDice("modifier_visibility", b => b.Visibility, printed);

        var clear = Exchange(attacker, target, new Attack("hero", "villain", "might"));

        // The control: in clear air both rolls are their full rank and nothing cites this entry.
        Assert.Equal(8 + 6, clear.Thrown);
        Assert.DoesNotContain(clear.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        var dark = Exchange(attacker, target, new Attack("hero", "villain", "might"), light);

        Assert.Equal(clear.Thrown + (2 * dice), dark.Thrown);

        var lines = dark.Lines
            .Where(l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, lines.Count);
        Assert.Equal(["hero", "villain"], lines.Select(l => l.Actor).Order(StringComparer.Ordinal));
        Assert.All(lines, l => Assert.Equal(entry.SourceRef, l.SourceRef));
        Assert.All(lines, l => Assert.Contains(printed, l.Text, StringComparison.Ordinal));
        Assert.All(lines, l => Assert.Contains($"{dice}d", l.Text, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The other half: bad light does not touch a passive defence.</b>
    ///
    /// <para>Same fight, same light, a Toughness answering instead of an Agility — so the exchange
    /// loses one band and not two, and only the attacker's line is written. Without this the
    /// fixture above is satisfied by an engine that applied the band to every defence there is.
    /// </para>
    /// </summary>
    [Fact]
    public void BadLightDoesNotTouchAPassiveDefence()
    {
        var (attacker, target) = Pair("toughness", 6);
        var entry = _play.GetCombat("modifier_visibility");
        var dice = BandDice("modifier_visibility", b => b.Visibility, "poor");

        var clear = Exchange(attacker, target, new Attack("hero", "villain", "might"));
        var dim = Exchange(attacker, target, new Attack("hero", "villain", "might"), Visibility.Poor);

        Assert.Equal(clear.Thrown + dice, dim.Thrown);

        var line = Assert.Single(dim.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal("hero", line.Actor);
    }

    /// <summary>
    /// <b>Each band of size moves the defender's active defence by exactly the dice p.75 prints —
    /// and the factor is derived from two sizes rather than taken as a band.</b>
    ///
    /// <para>Each case gives the two combatants sizes whose ratio lands in one band and passes
    /// through no other: 2 and 5 in each direction. The expected figure is read off
    /// <c>modifier_size</c>'s own table by the printed phrase.</para>
    ///
    /// <para><b>The 5× cases are what stop the nesting reading from being an assumption.</b> An
    /// attacker five times your size is also at least twice your size, so an engine that took the
    /// first matching band would give +1d where the page gives +2d.</para>
    /// </summary>
    [Theory]
    [InlineData(2.0, 1.0, "at least twice your size")]
    [InlineData(5.0, 1.0, "at least 5 times your size")]
    [InlineData(1.0, 2.0, "no more than half your size")]
    [InlineData(1.0, 5.0, "no more than one-fifth your size")]
    public void EachBandOfSizeMovesTheDefendersActiveDefenceByExactlyTheDiceItPrints(
        double attackerSize, double targetSize, string printed)
    {
        var entry = _play.GetCombat("modifier_size");
        var dice = BandDice("modifier_size", b => b.AttackerRelativeSize, printed);

        var (evenAttacker, evenTarget) = Pair("agility", 6);
        var even = Exchange(evenAttacker, evenTarget, new Attack("hero", "villain", "might"));

        // The control: the same size is no band at all, which is what the guide's "against somebody
        // the same size" default statement rests on.
        Assert.Equal(8 + 6, even.Thrown);
        Assert.DoesNotContain(even.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        var (attacker, target) = Pair("agility", 6, attackerSize: attackerSize, targetSize: targetSize);
        var mismatched = Exchange(attacker, target, new Attack("hero", "villain", "might"));

        Assert.Equal(even.Thrown + dice, mismatched.Thrown);

        var line = Assert.Single(mismatched.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal("villain", line.Actor);
        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains(printed, line.Text, StringComparison.Ordinal);
        Assert.Contains($"{dice}d", line.Text, StringComparison.Ordinal);
        Assert.Contains(entry.Size!.Affects, line.Text, StringComparison.Ordinal);
    }


    /// <summary>
    /// <b>Every one of p.75's four size thresholds is driven from both sides of itself.</b>
    ///
    /// <para><b>This fixture exists because the four above were not a test of the thresholds at
    /// all.</b> They drive factors of exactly 2, 5, ½ and ⅕ — every one of them sitting on a
    /// boundary, none of them anywhere near the inside of a band — so the numbers in
    /// <c>SizeBandApplies</c> were free. Substituting <c>factor &gt;= 3</c> for <c>factor &gt;= 5</c>
    /// left all 88 of this class's tests green while handing an attacker four times the defender's
    /// size the +2d the page gives one five times it. A threshold nothing drives on both sides is a
    /// number nobody has checked.</para>
    ///
    /// <para>So each case pairs a factor just inside a band with one just outside it: 1.9 against 2,
    /// 4.9 against 5, 0.51 against ½, 0.21 against ⅕. The expected dice are read out of
    /// <c>modifier_size</c> by the printed phrase the page's own words pick, and a factor in no band
    /// is expected to move nothing <em>and</em> to write no line — the two halves of a modifier that
    /// did not apply.</para>
    ///
    /// <para><b>The last two cases are the ratio itself.</b> 9.8 against 2 and 2 against 4 are the
    /// same two bands reached without either combatant being the unit, so an engine that subtracted
    /// sizes, or that read the attacker's alone, cannot satisfy them.</para>
    /// </summary>
    [Theory]
    // Just below "at least twice", and on it.
    [InlineData(1.9, 1.0, null)]
    [InlineData(2.0, 1.0, "at least twice your size")]
    // Between the two upward bands: the page gives +1d here and not +2d.
    [InlineData(3.0, 1.0, "at least twice your size")]
    [InlineData(4.9, 1.0, "at least twice your size")]
    [InlineData(5.0, 1.0, "at least 5 times your size")]
    // Just above "no more than half", and on it.
    [InlineData(0.51, 1.0, null)]
    [InlineData(0.5, 1.0, "no more than half your size")]
    // Between the two downward bands: −1d here and not −2d.
    [InlineData(0.25, 1.0, "no more than half your size")]
    [InlineData(0.21, 1.0, "no more than half your size")]
    [InlineData(0.2, 1.0, "no more than one-fifth your size")]
    // The same two bands with neither size being 1, so the comparison has to be a ratio.
    [InlineData(9.8, 2.0, "at least twice your size")]
    [InlineData(2.0, 4.0, "no more than half your size")]
    public void EverySizeThresholdIsDrivenFromBothSidesOfItself(
        double attackerSize, double targetSize, string? printed)
    {
        var entry = _play.GetCombat("modifier_size");

        var (evenAttacker, evenTarget) = Pair("agility", 6);
        var even = Exchange(evenAttacker, evenTarget, new Attack("hero", "villain", "might"));

        // The control: the same size throws the two full pools and cites nothing.
        Assert.Equal(8 + 6, even.Thrown);
        Assert.DoesNotContain(even.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        var (attacker, target) = Pair("agility", 6, attackerSize: attackerSize, targetSize: targetSize);
        var sized = Exchange(attacker, target, new Attack("hero", "villain", "might"));

        var lines = sized.Lines
            .Where(l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal))
            .ToList();

        if (printed is null)
        {
            // No band applies, so nothing moved and nothing was written. Both halves, because an
            // engine that moved the pool and wrote no line is as wrong as one that did neither.
            Assert.Equal(even.Thrown, sized.Thrown);
            Assert.Empty(lines);
            return;
        }

        var dice = BandDice("modifier_size", b => b.AttackerRelativeSize, printed);

        Assert.Equal(even.Thrown + dice, sized.Thrown);

        var line = Assert.Single(lines);

        // The band that applied is named, so a pool that happened to land on the right figure by
        // applying the wrong band cannot pass: +1d is +1d whichever phrase produced it.
        Assert.Contains(printed, line.Text, StringComparison.Ordinal);
        Assert.Equal("villain", line.Actor);
        Assert.Equal(entry.SourceRef, line.SourceRef);
    }

    /// <summary>
    /// <b>Where two size bands both apply, the one with the greater magnitude wins — and that this
    /// engine picks is a reading, not a printed rule.</b>
    ///
    /// <para>p.75 prints four bands and says nothing about what happens when a factor satisfies two
    /// of them, which every factor at or past 5 (and at or under ⅕) does. The guide records the
    /// reading; this drives it, and it drives the half that makes it a choice at all: <b>both</b>
    /// bands are shown to apply at the same factor, by asking the engine for a factor that sits
    /// inside the narrower one and for one that sits inside the wider one alone.</para>
    /// </summary>
    [Theory]
    [InlineData(5.0, "at least 5 times your size", "at least twice your size")]
    [InlineData(0.2, "no more than one-fifth your size", "no more than half your size")]
    public void WhereTwoSizeBandsApplyTheNarrowerOneWins(double factor, string narrower, string wider)
    {
        var narrow = BandDice("modifier_size", b => b.AttackerRelativeSize, narrower);
        var wide = BandDice("modifier_size", b => b.AttackerRelativeSize, wider);

        // The control on the claim itself: the two bands really are different figures, so "the
        // narrower wins" is an observable statement about this fight rather than a tie.
        Assert.True(Math.Abs(narrow) > Math.Abs(wide),
            $"'{narrower}' is {narrow}d and '{wider}' is {wide}d, so this fixture cannot tell them apart");

        var (evenAttacker, evenTarget) = Pair("agility", 6);
        var even = Exchange(evenAttacker, evenTarget, new Attack("hero", "villain", "might"));

        // At the factor where both apply, it is the narrower band's dice and the narrower band's
        // phrase — an engine taking the first match, or the wider one, gives the other figure.
        var (attacker, target) = Pair("agility", 6, attackerSize: factor, targetSize: 1);
        var both = Exchange(attacker, target, new Attack("hero", "villain", "might"));

        Assert.Equal(even.Thrown + narrow, both.Thrown);

        var line = Assert.Single(
            both.Lines, l => string.Equals(l.Rule, "modifier_size", StringComparison.Ordinal));

        Assert.Contains(narrower, line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(wider, line.Text, StringComparison.Ordinal);

        // And the control that the wider band is a band this fight can reach at all: a factor
        // inside it and outside the narrower one gives the wider band's own dice.
        var inside = factor > 1 ? factor - 1 : factor + 0.05;

        var (widerAttacker, widerTarget) = Pair("agility", 6, attackerSize: inside, targetSize: 1);
        var only = Exchange(widerAttacker, widerTarget, new Attack("hero", "villain", "might"));

        Assert.Equal(even.Thrown + wide, only.Thrown);
        Assert.Contains(wider, Assert.Single(
            only.Lines, l => string.Equals(l.Rule, "modifier_size", StringComparison.Ordinal)).Text,
            StringComparison.Ordinal);
    }
    /// <summary>
    /// <b>A size that cannot be a ratio is refused by the type, whichever door it comes in at.</b>
    ///
    /// <para><c>Combatant.Build</c> refuses a zero, a negative, a NaN and an infinity, and the
    /// reason it has to is arithmetic: <c>SizeModifier</c> divides one size by the other, and a zero
    /// defender yields an infinity that satisfies every band p.75 prints — a standing +2d nothing
    /// anywhere reports. <b>Nothing drove that guard.</b> Replacing its condition with
    /// <c>false</c> left all 4,697 tests in this project green, because the only refusal anybody had
    /// written was the encounter server's, and <c>play/</c> is a library with more callers than
    /// that one.</para>
    ///
    /// <para>Every factory is driven, because each passes <c>size</c> down separately, and
    /// <see cref="CombatantFactory"/> is driven too — it is the door a host actually uses. The
    /// control is the same call with a real size, which has to build.</para>
    /// </summary>
    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ASizeThatCannotBeARatioIsRefusedByEveryFactory(double size)
    {
        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 5 };

        var factories = new (string Name, Func<double, Combatant> Build)[]
        {
            ("Hero", s => Combatant.Hero("a", "A", 5, 10, 3, traits, ["toughness"], size: s)),
            ("Villain", s => Combatant.Villain("a", "A", 5, 10, traits, ["toughness"], size: s)),
            ("Foe", s => Combatant.Foe("a", "A", 5, 10, traits, ["toughness"], size: s)),
            ("Extra", s => Combatant.Extra("a", "A", 5, 10, traits, ["toughness"], size: s)),
            ("Minions", s => Combatant.Minions("a", "A", 5, 4, "threat", size: s))
        };

        foreach (var (name, build) in factories)
        {
            // The control first: the same call with a real size builds, so the throw below is about
            // the figure and not about the arguments around it.
            Assert.Equal(2, build(2).Size);

            var refused = Assert.Throws<ArgumentOutOfRangeException>(() => build(size));

            Assert.Contains("p.75", refused.Message, StringComparison.Ordinal);
            Assert.Contains("ratio", refused.Message, StringComparison.Ordinal);
            Assert.True(
                string.Equals(refused.ParamName, "size", StringComparison.Ordinal),
                $"{name} refused a size of {size} on '{refused.ParamName}' rather than on 'size'");
        }

        // And the door a host actually comes in at: a sheet plus a size, through the one place in
        // play/ that reads a CharacterSheet.
        var rules = new RulesFixture();
        var sheet = rules.LegalSheet();

        Assert.Equal(2, CombatantFactory.From(
            sheet, rules.Rules, rules.Derived, _play, CombatantKind.Hero, size: 2).Size);

        Assert.Throws<ArgumentOutOfRangeException>(() => CombatantFactory.From(
            sheet, rules.Rules, rules.Derived, _play, CombatantKind.Hero, size: size));
    }

    /// <summary>
    /// <b>Size touches the dodging kind of defence and no other.</b>
    ///
    /// <para>p.75 says "Size affects your <em>active</em> defense rolls" in as many words, so a
    /// Toughness soaking a blow does not move however big the attacker is — and neither does an
    /// Armor, which is the case that would matter to anybody measuring a fight against a giant.
    /// Both are driven, at the largest band the page prints, and both have to come back unchanged
    /// with no line written; the active pair beside them is the control that the same sizes really
    /// do move a roll.</para>
    /// </summary>
    [Theory]
    [InlineData("toughness")]
    [InlineData("armor")]
    public void SizeNeverMovesAPassiveDefence(string passive)
    {
        var entry = _play.GetCombat("modifier_size");
        var dice = BandDice("modifier_size", b => b.AttackerRelativeSize, "at least 5 times your size");

        var (giant, small) = Pair(passive, 6, attackerSize: 5, targetSize: 1);
        var soaked = Exchange(giant, small, new Attack("hero", "villain", "might"));

        var (evenAttacker, evenTarget) = Pair(passive, 6);
        var even = Exchange(evenAttacker, evenTarget, new Attack("hero", "villain", "might"));

        Assert.Equal(even.Thrown, soaked.Thrown);
        Assert.DoesNotContain(soaked.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        // The control: the same two sizes against an active defence do move the roll, so the
        // equality above is a statement about the defence and not about the sizes.
        var (dodgingAttacker, dodger) = Pair("agility", 6, attackerSize: 5, targetSize: 1);
        var dodged = Exchange(dodgingAttacker, dodger, new Attack("hero", "villain", "might"));

        var (openAttacker, openTarget) = Pair("agility", 6);
        var open = Exchange(openAttacker, openTarget, new Attack("hero", "villain", "might"));

        Assert.Equal(open.Thrown + dice, dodged.Thrown);
    }

    /// <summary>
    /// <b>A band whose printed word this engine does not know is a throw, and so is a cover test it
    /// no longer recognises.</b>
    ///
    /// <para>Four lookups turn a printed phrase into behaviour: the cover band, the visibility band,
    /// what one of p.75's four size phrases means as a comparison, and the sentence p.75 states the
    /// rank test in. Every one of them is written to throw rather than to skip a band, because
    /// <b>a modifier silently worth nothing is the failure the whole ledger exists to prevent</b> —
    /// and the guide says so in as many words about all four. <b>Nothing drove any of the throws.</b>
    /// Reading the code cannot tell you whether a field is consulted, which is the same reason
    /// <see cref="TheDefenceCountAndTheChoiceRuleAreReadFromTheEntry"/> is a twin rather than an
    /// assertion; these are the same instrument pointed at p.75's three modifiers.</para>
    ///
    /// <para>Each case reads the shipped bytes, rewords exactly one phrase — <c>WithDefect</c>
    /// throws if the phrase has moved, so a case cannot quietly stop reproducing — and drives the
    /// exchange that needs it. The control is the same exchange against the shipped file, which has
    /// to resolve.</para>
    /// </summary>
    [Theory]
    [InlineData(
        "\"cover\": \"heavy\"", "\"cover\": \"a fair bit of\"",
        "modifier_cover", "prints no band called 'heavy'")]
    [InlineData(
        "\"visibility\": \"poor\"", "\"visibility\": \"murky\"",
        "modifier_visibility", "prints no band called 'poor'")]
    [InlineData(
        "\"attacker_relative_size\": \"at least twice your size\"",
        "\"attacker_relative_size\": \"a good deal bigger than you\"",
        "modifier_size", "a good deal bigger than you")]
    [InlineData(
        "\"attacking_through_cover_requires\": \"an attack rank greater than the cover's Structure\"",
        "\"attacking_through_cover_requires\": \"a knack for finding the gap\"",
        "modifier_cover", "a knack for finding the gap")]
    public void ABandOrATestThisEngineNoLongerRecognisesIsAThrow(
        string find, string replace, string entryId, string names)
    {
        // One exchange that reaches all four: heavy cover, in poor light, from an attacker twice the
        // defender's size, through an obstacle with a Structure — against an active defence, so the
        // size band is consulted at all.
        var (attacker, target) = Pair("agility", 4, attackerSize: 2, targetSize: 1, attackRank: 9);

        var attack = new Attack(
            "hero", "villain", "might", Cover: Cover.Heavy, CoverStructure: 3);

        // The control: against the shipped file this exchange resolves and cites the entry under
        // test, so the throw below is about the wording and not about an exchange that never ran.
        var shipped = Exchange(attacker, target, attack, Visibility.Poor);

        Assert.Contains(shipped.Lines, l => string.Equals(l.Rule, entryId, StringComparison.Ordinal));

        var reworded = SubstitutedPlayRules.With(PlayRulesRepository.CombatFile, find, replace);
        var encounter = new Encounter(reworded, new ScriptedDice([.. Enumerable.Repeat(4, 300)]));

        var thrown = Assert.Throws<InvalidOperationException>(() =>
        {
            var state = encounter.Begin([attacker, target], visibility: Visibility.Poor);
            encounter.Step(state, attack);
        });

        Assert.Contains(entryId, thrown.Message, StringComparison.Ordinal);

        // And it names the phrase it could not read, so somebody who reworded the file can see
        // which line they moved rather than being told only that something is wrong.
        Assert.Contains(names, thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A completely hidden target cannot be hit, and nothing is rolled.</b>
    ///
    /// <para>The refusal is driven rather than asserted about: the dice source is handed a script
    /// and has to come back untouched, which is the only way to tell a refusal from an attack that
    /// was rolled and missed. The state comes back unchanged too.</para>
    /// </summary>
    [Fact]
    public void ACompletelyHiddenTargetCannotBeHitAndNothingIsRolled()
    {
        var (attacker, target) = Pair("toughness", 6);
        var entry = _play.GetCombat("modifier_cover");

        Assert.True(entry.Cover!.ACompletelyHiddenTargetCannotBeHit,
            "the entry no longer says a completely hidden target cannot be hit");

        var dice = new ScriptedDice(4, 4, 4, 4, 4, 4);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([attacker, target]);

        var step = encounter.Step(state, new Attack("hero", "villain", "might", Cover: Cover.Complete));

        // Nothing was rolled, and nothing about the fight moved.
        Assert.Equal(6, dice.Remaining);
        Assert.Null(step.State.LastAttack);
        Assert.Equal(target.CurrentHealth, step.State["villain"].CurrentHealth);

        var line = Assert.Single(step.Added, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains("cannot be hit", line.Text, StringComparison.Ordinal);
        Assert.Contains(entry.Cover.AttackingThroughCoverRequires, line.Text, StringComparison.Ordinal);

        // The control: the same fight, the same attack with no cover on it, is resolved and does
        // throw dice — so the refusal above is about the cover and not about anything else here.
        var open = Exchange(attacker, target, new Attack("hero", "villain", "might"));

        Assert.Equal(8 + Halved(6), open.Thrown);
    }

    /// <summary>
    /// <b>Attacking through cover: the rank has to be greater than the Structure, and then the
    /// Structure answers the roll.</b>
    ///
    /// <para>Three cases in one fixture because they are one rule. At a rank equal to the Structure
    /// nothing is rolled — <c>attacking_through_cover_requires</c> is "greater than", and equal is
    /// not. Above it the attack goes through, and the substitution has to <em>move the roll</em>:
    /// the defender's own Toughness of 2 is put aside and the obstacle's 9d answers instead, which
    /// is seven more dice on the table. A clause applied by writing a ledger line and changing no
    /// pool would pass a fixture that only read the prose.</para>
    /// </summary>
    [Fact]
    public void AnAttackThroughCoverNeedsARankAboveTheStructureAndThenTheStructureAnswersIt()
    {
        var entry = _play.GetCombat("modifier_cover");

        Assert.True(entry.Cover!.TargetMayUseTheCoversStructureAsAPassiveDefense,
            "the entry no longer lets the target answer with the cover's Structure");

        var (attacker, target) = Pair("toughness", 2, attackRank: 9);

        // Equal is not greater: refused, with nothing rolled.
        var script = new ScriptedDice(4, 4, 4, 4, 4, 4);
        var blocked = new Encounter(_play, script);
        var start = blocked.Begin([attacker, target]);

        var stopped = blocked.Step(start, new Attack(
            "hero", "villain", "might", Cover: Cover.Complete, CoverStructure: 9));

        Assert.Equal(6, script.Remaining);
        Assert.Null(stopped.State.LastAttack);
        Assert.Contains(stopped.Added, l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)
            && l.Text.Contains(entry.Cover.AttackingThroughCoverRequires, StringComparison.Ordinal));

        // One rank higher and it goes through — and the Structure, not the Toughness, answers.
        var (harder, sameTarget) = Pair("toughness", 2, attackRank: 10);

        var open = Exchange(harder, sameTarget, new Attack("hero", "villain", "might"));
        var through = Exchange(harder, sameTarget, new Attack(
            "hero", "villain", "might", Cover: Cover.Complete, CoverStructure: 9));

        // The control first: in the open the target answers with half their own Toughness.
        Assert.Equal(10 + Halved(2), open.Thrown);

        // And through the cover, the roll moves: the obstacle's 9d in place of that.
        Assert.Equal(10 + 9, through.Thrown);

        var line = Assert.Single(through.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal("villain", line.Actor);
        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains("passive defence", line.Text, StringComparison.Ordinal);

        // The defence that answered was passive, which is what makes p.79's luring unavailable to a
        // target who hid rather than moved — and what keeps every size band off the Structure.
        Assert.False(through.Lines.Count == 0);
    }

    /// <summary>
    /// <b>Which defence answers a shot through cover: the greater of the two, a tie to the target's
    /// own, and the comparison made before p.75's size band.</b>
    ///
    /// <para><b>The tie was free.</b> <c>structure &gt; best.Pool</c> and
    /// <c>structure &gt;= best.Pool</c> gave the same answer to every fixture in this class, and
    /// they are not the same rule: at a tie the target keeps an <em>active</em> defence, which
    /// p.75's size band moves and a wall's Structure never does. The line said "their agility at 6d
    /// is the greater" in that case, which is not true of two equal figures — a ledger line that
    /// lied about the comparison it had just made.</para>
    ///
    /// <para><b>And the comparison is made before the size band</b>, which the guide records as a
    /// reading: a defender whose Agility would out-roll the wall once their +2d is on still answers
    /// with the wall, because "the best defense available" is settled on the two figures as they
    /// stand. Driven here so that the reading is a behaviour rather than a sentence — and so that
    /// changing the order later is a red test rather than a silent change of answer.</para>
    /// </summary>
    [Fact]
    public void ATieOverCoverGoesToTheTargetsOwnDefenceAndIsSettledBeforeTheSizeBand()
    {
        var entry = _play.GetCombat("modifier_cover");
        var bonus = BandDice("modifier_size", b => b.AttackerRelativeSize, "at least 5 times your size");

        // Structure 6 against an Agility of 6: equal, so the target keeps their own — and keeps it
        // active, which the size band below is the proof of.
        var (attacker, target) = Pair("agility", 6, attackRank: 10);

        var open = Exchange(attacker, target, new Attack("hero", "villain", "might"));
        Assert.Equal(10 + 6, open.Thrown);

        var tied = Exchange(attacker, target, new Attack(
            "hero", "villain", "might", CoverStructure: 6));

        Assert.Equal(10 + 6, tied.Thrown);

        var line = Assert.Single(tied.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Contains("matches it", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("is the greater", line.Text, StringComparison.Ordinal);

        // One more and the wall answers, which is the control that 6 against 6 was a tie and not a
        // Structure the engine had dropped on the floor.
        var taken = Exchange(attacker, target, new Attack(
            "hero", "villain", "might", CoverStructure: 7));

        Assert.Equal(10 + 7, taken.Thrown);
        Assert.Contains("it is the greater", Assert.Single(
            taken.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)).Text,
            StringComparison.Ordinal);

        // The reading: a five-times-sized attacker would put the dodge at 6 + the band, above the
        // wall's 7 — and the wall still answers, because the comparison happened before the band.
        var (giant, small) = Pair("agility", 6, attackerSize: 5, targetSize: 1, attackRank: 10);

        var dodged = Exchange(giant, small, new Attack("hero", "villain", "might"));

        // The control on the reading: with no wall in the way the band really is worth this much,
        // and it really would beat the 7.
        Assert.Equal(10 + 6 + bonus, dodged.Thrown);
        Assert.True(6 + bonus > 7, "the fixture's size band no longer beats the Structure it is set against");

        var behindTheWall = Exchange(giant, small, new Attack(
            "hero", "villain", "might", CoverStructure: 7));

        Assert.Equal(10 + 7, behindTheWall.Thrown);

        // And no size band was written, because the defence that answered is passive.
        Assert.DoesNotContain(behindTheWall.Lines, l =>
            string.Equals(l.Rule, "modifier_size", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A −3d on a 2d pool reaches p.67's floor, and one die is thrown that scores only on a
    /// six.</b>
    ///
    /// <para>The floor is <c>SuccessCounter</c>'s and this proves the modifiers reach it: the pool
    /// after visibility is −1, which is not a pool of nothing. The script is exactly as long as the
    /// exchange needs, so an engine that threw the full 2d would run <see cref="ScriptedDice"/> out
    /// and an engine that threw fewer would leave <c>Remaining</c> above zero.</para>
    ///
    /// <para>Both halves of the floor are driven. A four is a success under
    /// <c>play_meta.success_map</c> and scores nothing under the floor; a six is worth two under the
    /// map and scores one under the floor. The control is the same script in clear air, where the
    /// six is worth its full two — so the difference is the floor and not the fixture.</para>
    /// </summary>
    [Fact]
    public void ATripleVisibilityPenaltyOnATwoDicePoolReachesTheSubOneDieFloor()
    {
        var floor = _play.GetMeta("sub_one_die_floor").SubOneDie!;
        var rate = _play.GetCombat("damage").Damage!.DamagePerNetSuccess;

        Assert.Equal(-3, BandDice("modifier_visibility", b => b.Visibility, "none"));

        var (attacker, target) = Pair("toughness", 4, attackRank: 2);

        // Toughness 4 against a lethal attack is halved to 2, so the exchange is 1 + 2 dice: one
        // for the floored attack and two for the soak. Both scripts are exactly that long.
        Assert.Equal(2, Halved(4));

        // A four scores nothing at the floor, where it is a success under the ordinary map.
        var missed = Blind(attacker, target, floor.CountingFaces[0] == 4 ? 5 : 4);

        Assert.Equal(target.CurrentHealth, missed.CurrentHealth);

        // A six scores exactly the floor's own figure — one, not the two the map gives it. The
        // defender rolls two ones and scores nothing, so the net is that one success.
        var hit = Blind(attacker, target, floor.CountingFaces[0]);

        Assert.Equal(target.CurrentHealth - (floor.SuccessesWhenHit * rate), hit.CurrentHealth);

        // The control: the same three faces in clear air. The attack is 2d, the six is worth two
        // under the map and the second die adds a success, so the target takes more than the floor
        // allowed — an engine that never floored anything would give this answer in the dark too.
        var lit = new Encounter(_play, new ScriptedDice(6, 4, 1, 1));
        var open = lit.Begin([attacker, target]);
        var struck = lit.Step(open, new Attack("hero", "villain", "might")).State["villain"];

        Assert.True(struck.CurrentHealth < hit.CurrentHealth,
            $"in clear air the same faces left {struck.CurrentHealth} Health and the floor left {hit.CurrentHealth}");
    }

    /// <summary>One exchange in the dark, with the attacker's single floored die showing <paramref name="face"/>.</summary>
    private Combatant Blind(Combatant attacker, Combatant target, int face)
    {
        var dice = new ScriptedDice(face, 1, 1);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([attacker, target], visibility: Visibility.None);

        var step = encounter.Step(state, new Attack("hero", "villain", "might"));

        // The whole of the control: exactly three faces were wanted, so the attack threw one die
        // and not two, and the defence threw the two the halved Toughness asks for.
        Assert.Equal(0, dice.Remaining);

        return step.State["villain"];
    }

    /// <summary>
    /// <b>An opponent nobody can see counts as no visibility, for whichever of them is facing
    /// them.</b>
    ///
    /// <para>Two fights, in clear air both times, and the same band lands on opposite people: an
    /// invisible target costs the attacker their attack roll, and an invisible attacker costs the
    /// defender their dodge. The band is <c>none</c> in both, read off the file.</para>
    /// </summary>
    [Fact]
    public void AnInvisibleOpponentCountsAsNoVisibilityForWhicheverOfThemFacesThem()
    {
        var entry = _play.GetCombat("modifier_visibility");
        var dice = BandDice("modifier_visibility", b => b.Visibility, "none");

        Assert.True(entry.Visibility!.AnInvisibleOpponentCountsAsNoVisibility,
            "the entry no longer says an invisible opponent counts as no visibility");

        var (evenAttacker, evenTarget) = Pair("agility", 6);
        var seen = Exchange(evenAttacker, evenTarget, new Attack("hero", "villain", "might"));

        Assert.Equal(8 + 6, seen.Thrown);

        // An invisible target: the attacker is the one in the dark, and only they lose dice.
        var (attacker, ghost) = Pair("agility", 6, targetInvisible: true);
        var atAGhost = Exchange(attacker, ghost, new Attack("hero", "villain", "might"));

        Assert.Equal(seen.Thrown + dice, atAGhost.Thrown);
        Assert.Equal("hero", Assert.Single(
            atAGhost.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)).Actor);

        // An invisible attacker: now it is the defender's dodge that suffers.
        var (unseen, defender) = Pair("agility", 6, attackerInvisible: true);
        var fromAGhost = Exchange(unseen, defender, new Attack("hero", "villain", "might"));

        Assert.Equal(seen.Thrown + dice, fromAGhost.Thrown);
        Assert.Equal("villain", Assert.Single(
            fromAGhost.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)).Actor);
    }

    /// <summary>
    /// <b>A Power p.75 gives as compensating cancels the penalty, and the ledger says which one.</b>
    ///
    /// <para>Both names the entry gives are driven, and the combatant is built by
    /// <see cref="CombatantFactory"/> from a real sheet rather than handed the id — because that
    /// read is the half that would break silently. Blind Fighting and Radar are default-rank
    /// Powers, so the character engine answers <b>0</b> for both, and an engine that had looked for
    /// a rank would compensate nobody for ever.</para>
    ///
    /// <para>The control is the same sheet without the Power, in the same darkness: it loses the
    /// band, so the equality is about the Power and not about the fight.</para>
    /// </summary>
    [Theory]
    [InlineData("blind_fighting")]
    [InlineData("radar")]
    public void APowerThatCompensatesCancelsTheVisibilityPenaltyAndTheLineNamesIt(string powerId)
    {
        var entry = _play.GetCombat("modifier_visibility");
        var rules = new RulesFixture();

        var sheet = rules.LegalSheet();
        sheet.Name = "the Hero";
        sheet.AbilityRanks["might"] = 8;
        sheet.AbilityRanks["agility"] = 1;
        sheet.AbilityRanks["toughness"] = 1;

        var blind = CombatantFactory.From(
            sheet, rules.Rules, rules.Derived, _play, CombatantKind.Hero, "hero", "heroes");

        sheet.SelectedPowers.Add(new SelectedPower(powerId, 0));

        var seeing = CombatantFactory.From(
            sheet, rules.Rules, rules.Derived, _play, CombatantKind.Hero, "hero", "heroes");

        // The control on the read itself: the Power is on the combatant and its rank is nothing, so
        // TraitRanks could not have answered this question.
        Assert.Contains(powerId, seeing.Powers, StringComparer.Ordinal);
        Assert.DoesNotContain(powerId, blind.Powers, StringComparer.Ordinal);
        Assert.Equal(0, seeing.Rank(powerId));

        // Edge 1, so p.73's order puts the Hero first and the attack is theirs to make: an attack
        // out of turn is refused, which would make every comparison below one between two zeroes.
        var target = Combatant.Villain(
            "villain", "the Villain", edge: 1, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 6 },
            ["toughness"], side: "villains");

        Assert.True(blind.Edge > target.Edge, "the fixture's Hero does not act first");

        var lit = Exchange(blind, target, new Attack("hero", "villain", "might"));
        var dark = Exchange(blind, target, new Attack("hero", "villain", "might"), Visibility.None);
        var compensated = Exchange(seeing, target, new Attack("hero", "villain", "might"), Visibility.None);

        // The control: without the Power the darkness really does cost the band.
        Assert.Equal(lit.Thrown + BandDice("modifier_visibility", b => b.Visibility, "none"), dark.Thrown);

        // With it, the pool is the one it was in clear air.
        Assert.Equal(lit.Thrown, compensated.Thrown);

        var line = Assert.Single(
            compensated.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains("unpenalised", line.Text, StringComparison.Ordinal);

        var printed = Assert.Single(
            entry.Visibility!.PowersThatCompensateGiven,
            p => string.Equals(p.ToLowerInvariant().Replace(' ', '_'), powerId, StringComparison.Ordinal));

        Assert.Contains(printed, line.Text, StringComparison.Ordinal);
    }


    /// <summary>
    /// <b>A compensating Power answers whichever of the two reasons put the character in the dark,
    /// and that it answers both is a reading rather than a printed rule.</b>
    ///
    /// <para><b>p.75 hangs the "unless" off one sentence and this engine applies it to both.</b> The
    /// printed clause is "You effectively have no visibility against an invisible opponent unless
    /// you have a Power that compensates for this, like Blind Fighting or Radar" — so read alone it
    /// covers an invisible opponent and not the dark. <b>The book settles it on the Power's own
    /// page, and this engine may not read that page</b>: Blind Fighting (Ch.2, p.24) is "no
    /// penalties or adverse effects when fighting in the dark or against opponents you can't see,
    /// whatever the reason". That is a Chapter 2 entry and <c>play/</c> reads
    /// <c>data/rules/play/</c> and nothing else, so the scope is read off the field's placement —
    /// <c>powers_that_compensate_given</c> sits under <c>visibility</c> as a whole rather than under
    /// the invisible clause — and the guide's readings table records why.</para>
    ///
    /// <para><b>Only the light half was driven.</b> Narrowing the compensation to
    /// <c>!unseen &amp;&amp; Compensating(roller)</c> — the engine declining to compensate for
    /// exactly the case p.75 attaches the clause to — left every test in this class and every one in
    /// the two MCP classes green. Both causes are driven here, each against its own unhelped
    /// control.</para>
    /// </summary>
    [Theory]
    [InlineData("blind_fighting", false)]
    [InlineData("blind_fighting", true)]
    [InlineData("radar", false)]
    [InlineData("radar", true)]
    public void ACompensatingPowerAnswersWhicheverReasonPutTheRollerInTheDark(
        string powerId, bool invisibleOpponent)
    {
        var entry = _play.GetCombat("modifier_visibility");
        var dice = BandDice("modifier_visibility", b => b.Visibility, "none");

        // The scene's light where the cause is the light, and clear air where the cause is an
        // opponent nobody can see: one cause at a time, so neither can stand in for the other.
        var light = invisibleOpponent ? Visibility.Clear : Visibility.None;

        var (blindAttacker, blindTarget) =
            Pair("toughness", 6, targetInvisible: invisibleOpponent);

        var (seeingAttacker, seeingTarget) = Pair(
            "toughness", 6, targetInvisible: invisibleOpponent,
            attackerPowers: new HashSet<string>(StringComparer.Ordinal) { powerId });

        var (openAttacker, openTarget) = Pair("toughness", 6);
        var open = Exchange(openAttacker, openTarget, new Attack("hero", "villain", "might"));

        // The control: without the Power, this cause really does cost the "none" band — so the
        // equality below is about the Power and not about a cause that was never applied.
        var blind = Exchange(blindAttacker, blindTarget, new Attack("hero", "villain", "might"), light);

        Assert.Equal(open.Thrown + dice, blind.Thrown);

        // And with it, the pool is the one it was in clear air against somebody visible.
        var seeing = Exchange(
            seeingAttacker, seeingTarget, new Attack("hero", "villain", "might"), light);

        Assert.Equal(open.Thrown, seeing.Thrown);

        var line = Assert.Single(
            seeing.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Equal("hero", line.Actor);
        Assert.Equal(entry.SourceRef, line.SourceRef);
        Assert.Contains("unpenalised", line.Text, StringComparison.Ordinal);

        // The line names which cause it answered, so a reader can tell the two apart — and names
        // the Power in the book's own spelling rather than in the id's.
        Assert.Contains(
            invisibleOpponent ? "cannot be seen" : "the visibility here is none",
            line.Text, StringComparison.Ordinal);

        Assert.Contains(
            Assert.Single(
                entry.Visibility!.PowersThatCompensateGiven,
                p => string.Equals(p.ToLowerInvariant().Replace(' ', '_'), powerId, StringComparison.Ordinal)),
            line.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Two things can make the visibility bad at once, and the worse of them is the one that
    /// applies.</b>
    ///
    /// <para>An invisible opponent in poor light is both of p.75's cases at the same time, and the
    /// page's answer is the worse one: <c>an_invisible_opponent_counts_as_no_visibility</c> is not
    /// a further −1d on top of the fog, it is the "none" band. The two bands are different figures,
    /// so an engine that took the scene's light, or that added the two, gives a different pool —
    /// and neither could be told from the right answer while the only fixtures drove one cause at
    /// a time.</para>
    ///
    /// <para>The three controls are the same fight with one cause each and with neither, so the
    /// figure asserted is the "none" band reached through the combination rather than through
    /// either half of it.</para>
    /// </summary>
    [Fact]
    public void AnInvisibleOpponentInPoorLightIsTheWorseOfTheTwoBandsAndNotTheirSum()
    {
        var entry = _play.GetCombat("modifier_visibility");
        var poor = BandDice("modifier_visibility", b => b.Visibility, "poor");
        var none = BandDice("modifier_visibility", b => b.Visibility, "none");

        // The control on the claim: the two bands are different, so "the worse wins" is observable.
        Assert.True(Math.Abs(none) > Math.Abs(poor),
            $"the two bands are {none}d and {poor}d, so this fixture cannot tell them apart");

        var attack = new Attack("hero", "villain", "might");

        var (openAttacker, openTarget) = Pair("toughness", 6);
        var open = Exchange(openAttacker, openTarget, attack);

        // Each cause on its own, as the controls.
        var fog = Exchange(openAttacker, openTarget, attack, Visibility.Poor);
        Assert.Equal(open.Thrown + poor, fog.Thrown);

        var (atGhost, ghost) = Pair("toughness", 6, targetInvisible: true);
        var unseen = Exchange(atGhost, ghost, attack);
        Assert.Equal(open.Thrown + none, unseen.Thrown);

        // Both at once: the worse band, and not the sum of the two.
        var both = Exchange(atGhost, ghost, attack, Visibility.Poor);

        Assert.Equal(open.Thrown + none, both.Thrown);
        Assert.NotEqual(open.Thrown + none + poor, both.Thrown);

        // And the line says which of the two it counted, so the pool is not the only evidence.
        var line = Assert.Single(
            both.Lines, l => string.Equals(l.Rule, entry.Id, StringComparison.Ordinal));

        Assert.Contains("cannot be seen", line.Text, StringComparison.Ordinal);
        Assert.Contains($"{none}d", line.Text, StringComparison.Ordinal);
    }
    /// <summary>
    /// <b>The default statement both documents make is true of the code and is still in both of
    /// them.</b>
    ///
    /// <para>"In clear air, in the open, against somebody the same size" is what a reader of a
    /// balance figure off this engine is told it was measured under. It used to be a statement about
    /// three <em>absences</em> — no intent could express any of them — and it is now a statement
    /// about three <em>defaults</em>, which is the weaker kind of claim and the one that needs a
    /// guard: a default that moved would leave both documents describing a fight nobody ran.</para>
    ///
    /// <para>Driven rather than read: a fight nobody said anything about cites none of the three
    /// entries, with the control that it resolved an attack at all.</para>
    /// </summary>
    [Fact]
    public void AFightNobodySaidAnythingAboutIsTheOneBothDocumentsDescribe()
    {
        var (attacker, target) = Pair("agility", 6);

        var encounter = new Encounter(_play, new SeededDice(75));
        var state = encounter.Begin([attacker, target]);

        Assert.Equal(Visibility.Clear, state.Visibility);
        Assert.All(state.Combatants.Values, c => Assert.Equal(Combatant.SameSize, c.Size));
        Assert.All(state.Combatants.Values, c => Assert.False(c.Invisible));

        var plain = new Attack("hero", "villain", "might");

        Assert.Equal(Cover.None, plain.Cover);
        Assert.Null(plain.CoverStructure);

        var step = encounter.Step(state, plain);

        // The control: an attack really was resolved, so the three absences below are about the
        // defaults and not about a step that did nothing.
        Assert.Contains(step.Added, l => l.Text.Contains("defends with", StringComparison.Ordinal));

        foreach (var id in new[] { "modifier_cover", "modifier_size", "modifier_visibility" })
        {
            Assert.DoesNotContain(step.Added, l =>
                string.Equals(l.Rule, id, StringComparison.Ordinal));
        }

        // And both documents still say it — the guide for whoever changes the engine, the play
        // policy for whoever quotes a number out of it.
        const string Statement = "in clear air, in the open, against somebody the same size";

        Assert.Contains(Statement, Prose(Guide()), StringComparison.Ordinal);

        Assert.Contains(
            Statement,
            Prose(File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "mcp-play", "PLAY-POLICY.md"))),
            StringComparison.Ordinal);
    }


    // ── p.80's Hard Targets ──────────────────────────────────────────────────

    /// <summary>The Hard Targets table setting, and nothing else.</summary>
    private static readonly TableRules HardTargetsOn = TableRules.Book with { HardTargets = true };

    /// <summary>
    /// <b>A hard target's passive defence answers at twice its rank, and its active one does
    /// not.</b>
    ///
    /// <para>p.80 says "double a hard target's passive defense rank" and says nothing about the
    /// dodging kind: a tank is no harder to duck for being a tank. Both halves are driven, and the
    /// factor is read as a difference rather than typed — the doubled pool has to be exactly one
    /// whole rank more than the undoubled one.</para>
    ///
    /// <para><b>The switch off is the baseline and is measured first.</b> A setting that is off has
    /// to change nothing, which is what makes every balance figure this engine produces a figure
    /// about the game the book prints; the same combatant declared a hard target in a fight that
    /// did not take the setting throws exactly the dice they would have thrown anyway.</para>
    /// </summary>
    [Fact]
    public void AHardTargetsPassiveDefenceDoublesAndItsActiveDefenceDoesNot()
    {
        const int Rank = 6;

        // Subdual, so `lethal_and_subdual` leaves the Toughness whole and the only thing moving the
        // defence pool is the doubling under test.
        var blow = new Attack("hero", "villain", "might", DamageKind.Subdual);

        var (attacker, soft) = Pair("toughness", Rank);
        var open = Exchange(attacker, soft, blow, table: HardTargetsOn);

        var (_, machine) = Pair("toughness", Rank, targetHardTarget: true);
        var doubled = Exchange(attacker, machine, blow, table: HardTargetsOn);

        Assert.Equal(open.Thrown + Rank, doubled.Thrown);

        // The switch off changes nothing at all, for the same combatant.
        var off = Exchange(attacker, machine, blow);

        Assert.Equal(open.Thrown, off.Thrown);
        Assert.DoesNotContain(off.Lines, l =>
            string.Equals(l.Rule, "gritty_hard_targets", StringComparison.Ordinal));

        // And the active kind never moves: a dodging machine dodges at its own rank.
        var (dodgeAttacker, dodger) = Pair("agility", Rank);
        var dodge = Exchange(dodgeAttacker, dodger, blow, table: HardTargetsOn);

        var (_, dodgingMachine) = Pair("agility", Rank, targetHardTarget: true);
        var machineDodge = Exchange(dodgeAttacker, dodgingMachine, blow, table: HardTargetsOn);

        Assert.Equal(dodge.Thrown, machineDodge.Thrown);
    }

    /// <summary>
    /// <b>The doubling multiplies the rank before the printed halving, not after it.</b>
    ///
    /// <para>p.80 doubles a <em>rank</em>; p.75's table and <c>lethal_and_subdual</c> then halve
    /// what answers this attack. Both directions are computed and the wrong one is required to be
    /// wrong, because on an odd rank they differ: a Toughness of 7 against a lethal blow is 14 and
    /// then 7 the page's way round, and 4 — the Glossary rounds a half up — and then 8 the other,
    /// which is a die more than p.80 allows.</para>
    /// </summary>
    [Fact]
    public void AHardTargetsRankIsDoubledBeforeThePrintedHalvingAndNotAfter()
    {
        const int Odd = 7;

        // Lethal, so p.75 halves the Toughness — and odd, so the two orders disagree.
        var blow = new Attack("hero", "villain", "might");

        var (attacker, machine) = Pair("toughness", Odd, targetHardTarget: true);
        var hard = Exchange(attacker, machine, blow, table: HardTargetsOn);

        var (_, soft) = Pair("toughness", Odd);
        var open = Exchange(attacker, soft, blow, table: HardTargetsOn);

        // The fixture's own control: the halving really is in play, so this is a statement about
        // the order of the two operations and not about a rank nothing touched.
        Assert.Equal(Halved(Odd), open.Thrown - AttackPool);
        Assert.NotEqual(Odd, Halved(Odd));

        Assert.Equal(AttackPool + Halved(Odd * 2), hard.Thrown);
        Assert.NotEqual(AttackPool + (Halved(Odd) * 2), hard.Thrown);
    }

    /// <summary>The attack rank <see cref="Pair"/> gives its attacker, which is the whole pool in the open.</summary>
    private const int AttackPool = 8;

    /// <summary>
    /// <b>Aiming at a vulnerable part costs exactly the dice p.80 prints and cancels the
    /// doubling.</b>
    ///
    /// <para>The penalty is read off <c>penalty_dice_to_negate_it</c> rather than restated, and the
    /// negation is measured against the doubled exchange beside it — so the two halves of the
    /// sentence are driven separately: the attack pool falls by the printed figure, and the defence
    /// pool falls back to the rank on the sheet.</para>
    /// </summary>
    [Fact]
    public void AimingAtAVulnerablePartCostsThePrintedDiceAndCancelsTheDoubling()
    {
        const int Rank = 6;

        var rule = _play.GetGritty("gritty_hard_targets").HardTargets!;

        // The control on the reading: a penalty of nothing would make every equality below hold of
        // an engine that applied no rule at all.
        Assert.NotEqual(0, rule.PenaltyDiceToNegateIt);

        var blow = new Attack("hero", "villain", "might", DamageKind.Subdual);
        var weakPoint = blow with { VulnerablePart = true };

        var (attacker, machine) = Pair("toughness", Rank, targetHardTarget: true);

        var doubled = Exchange(attacker, machine, blow, table: HardTargetsOn);
        var aimed = Exchange(attacker, machine, weakPoint, table: HardTargetsOn);

        // The doubling is gone — one whole rank off — and the attacker paid the printed dice for it.
        Assert.Equal(doubled.Thrown - Rank + rule.PenaltyDiceToNegateIt, aimed.Thrown);

        var line = Assert.Single(aimed.Lines, l =>
            string.Equals(l.Rule, "gritty_hard_targets", StringComparison.Ordinal));

        Assert.Contains(rule.NegationAvailableAgainst, line.Text, StringComparison.Ordinal);
        Assert.Contains("GM's call", line.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A declaration that buys nothing says so and is not charged for.</b>
    ///
    /// <para>Two ways to reach it: the target is not a hard target, or the table never took the
    /// setting. Neither takes the four dice — an attacker cannot pay to negate a doubling that is
    /// not there — and both write a line saying which of the two it was, because a flag accepted
    /// and quietly ignored is the worst of the three possible behaviours.</para>
    /// </summary>
    [Fact]
    public void AVulnerablePartDeclarationThatNegatesNothingSaysSoAndCostsNothing()
    {
        var blow = new Attack("hero", "villain", "might", DamageKind.Subdual);
        var weakPoint = blow with { VulnerablePart = true };

        var (attacker, soft) = Pair("toughness", 6);
        var (_, machine) = Pair("toughness", 6, targetHardTarget: true);

        var open = Exchange(attacker, soft, blow, table: HardTargetsOn);

        var atFlesh = Exchange(attacker, soft, weakPoint, table: HardTargetsOn);
        var switchOff = Exchange(attacker, machine, weakPoint);

        Assert.Equal(open.Thrown, atFlesh.Thrown);
        Assert.Equal(open.Thrown, switchOff.Thrown);

        Assert.Contains("is none of", Line(atFlesh, "gritty_hard_targets"), StringComparison.Ordinal);
        Assert.Contains(
            "did not take Hard Targets", Line(switchOff, "gritty_hard_targets"), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The Penetrating recommendation is named on the ledger and not applied.</b>
    ///
    /// <para>p.80 closes by advising a table that vehicle-scale weapons and the strongest
    /// characters should carry the Penetrating Pro. That is advice about how a character is built,
    /// which is the <em>first</em> engine's question and one <c>play/</c> may not answer — so it is
    /// quoted rather than applied, and the line says as much in as many words.</para>
    /// </summary>
    [Fact]
    public void ThePenetratingRecommendationIsQuotedRatherThanApplied()
    {
        var rule = _play.GetGritty("gritty_hard_targets").HardTargets!;

        var (attacker, machine) = Pair("toughness", 6, targetHardTarget: true);
        var hit = Exchange(
            attacker, machine, new Attack("hero", "villain", "might", DamageKind.Subdual),
            table: HardTargetsOn);

        var line = Line(hit, "gritty_hard_targets");

        Assert.Contains(rule.RecommendedProForVehicleScaleWeapons, line, StringComparison.Ordinal);
        Assert.Contains("is not applied", line, StringComparison.Ordinal);
        Assert.Contains(rule.AppliesTo, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An entry that has stopped saying "doubled" is a rule this engine refuses to apply.</b>
    ///
    /// <para>The factor is not in the data and cannot be: <c>passive_defense_rank</c> is a printed
    /// word, so the engine reads the word and supplies the 2 — the same shape as
    /// <c>seize_initiative_gm_alternative</c>'s "doubles", and recorded as a reading for the same
    /// reason. A silent fallback to 2 against an entry somebody had corrected would apply a rule
    /// the book no longer prints, which is the failure this whole store exists to prevent.</para>
    ///
    /// <para>The control is the same exchange against the shipped file, which resolves and cites
    /// the entry — so the throw is about the wording and not about an exchange that never ran.</para>
    /// </summary>
    [Fact]
    public void AHardTargetsEffectThisEngineNoLongerRecognisesIsAThrow()
    {
        const string Printed = "\"passive_defense_rank\": \"doubled\"";
        const string Reworded = "\"passive_defense_rank\": \"sturdier than these rules suggest\"";

        var (attacker, machine) = Pair("toughness", 6, targetHardTarget: true);
        var blow = new Attack("hero", "villain", "might", DamageKind.Subdual);

        // The control: against the shipped bytes this resolves and the rule is cited.
        var shipped = Exchange(attacker, machine, blow, table: HardTargetsOn);

        Assert.Contains(shipped.Lines, l =>
            string.Equals(l.Rule, "gritty_hard_targets", StringComparison.Ordinal));

        var reworded = SubstitutedPlayRules.With(PlayRulesRepository.GrittyFile, Printed, Reworded);
        var encounter = new Encounter(reworded, new ScriptedDice([.. Enumerable.Repeat(4, 300)]), HardTargetsOn);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            encounter.Step(encounter.Begin([attacker, machine]), blow));

        Assert.Contains("gritty_hard_targets", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("sturdier than these rules suggest", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A wall is not a combatant, so Hard Targets never doubles a cover's Structure.</b>
    ///
    /// <para>p.80 plainly covers "thick, inanimate objects", and a wall being shot through is one —
    /// but the declaration this engine takes is made on a <em>combatant</em>, and the Structure is
    /// a bare number the caller supplied for one attack. So the number stands as sent, and a caller
    /// who wants a doubled wall doubles it. Driven rather than asserted in a comment: the target's
    /// own doubled defence is the greater here, and a Structure that had doubled too would have
    /// answered instead.</para>
    /// </summary>
    [Fact]
    public void HardTargetsNeverDoublesACoversStructure()
    {
        const int Rank = 3;
        const int Structure = 5;

        var (attacker, machine) = Pair("toughness", Rank, targetHardTarget: true);

        var through = new Attack(
            "hero", "villain", "might", DamageKind.Subdual, CoverStructure: Structure);

        var shot = Exchange(attacker, machine, through, table: HardTargetsOn);

        // Their own Toughness doubles to 6 and answers; a Structure doubled to 10 would have.
        Assert.Equal(AttackPool + (Rank * 2), shot.Thrown);
        Assert.NotEqual(AttackPool + (Structure * 2), shot.Thrown);

        Assert.Contains(
            "is the greater, so that answers instead",
            Line(shot, "modifier_cover"),
            StringComparison.Ordinal);
    }


    // ── p.79's Close Range ───────────────────────────────────────────────────

    /// <summary>The Close Range table setting, and nothing else.</summary>
    private static readonly TableRules CloseRangeOn = TableRules.Book with { CloseRangePenalty = true };

    /// <summary>
    /// <b>A ranged attack from inside Close Range costs the dodger exactly the dice p.79 prints,
    /// and a close combat attack costs them nothing.</b>
    ///
    /// <para>The figure is read off <c>penalty_dice_to_active_defense</c> rather than restated, and
    /// the rule's own condition is driven from both sides: a Ranged Weapon takes it and a fist does
    /// not, which is the difference p.75's table draws and this engine reads off the row's printed
    /// type rather than off a list of its own.</para>
    ///
    /// <para><b>The switch off is the baseline and is measured first.</b> A setting that is off has
    /// to change nothing — the same exchange, the same pair, the same band, and the same pool.</para>
    /// </summary>
    [Fact]
    public void ARangedAttackFromInsideCloseRangeCostsTheDodgerThePrintedDice()
    {
        var penalty = _play.GetGritty("gritty_close_range").CloseRangePenalty!.PenaltyDiceToActiveDefense;

        // The control on the reading: a penalty of nothing would make every equality below hold of
        // an engine applying no rule at all.
        Assert.NotEqual(0, penalty);

        var (attacker, dodger) = Pair("agility", 6);

        var fist = new Attack("hero", "villain", "might");
        var gun = new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon);

        // A fight opens at Close Range unless the GM says otherwise, which is where this rule bites.
        var punched = Exchange(attacker, dodger, fist, table: CloseRangeOn);
        var shot = Exchange(attacker, dodger, gun, table: CloseRangeOn);

        Assert.Equal(punched.Thrown + penalty, shot.Thrown);
        Assert.DoesNotContain(punched.Lines, l =>
            string.Equals(l.Rule, "gritty_close_range", StringComparison.Ordinal));

        // The switch off changes nothing, for the same shot.
        var off = Exchange(attacker, dodger, gun);

        Assert.Equal(punched.Thrown, off.Thrown);
        Assert.DoesNotContain(off.Lines, l =>
            string.Equals(l.Rule, "gritty_close_range", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>It costs the dodging kind of defence and nothing else.</b>
    ///
    /// <para>The entry's field is <c>penalty_dice_to_active_defense</c> in as many words: a soak is
    /// a soak whatever is being shot at you. The control is the same shot against a dodger, which
    /// does move — so the equality below is a statement about the defence and not about a shot that
    /// never happened.</para>
    /// </summary>
    [Fact]
    public void CloseRangeNeverMovesAPassiveDefence()
    {
        var gun = new Attack("hero", "villain", "might", DamageKind.Subdual, AttackType.RangedWeapon);

        var (attacker, soaker) = Pair("toughness", 6);
        var soaked = Exchange(attacker, soaker, gun, table: CloseRangeOn);
        var openSoak = Exchange(attacker, soaker, gun);

        Assert.Equal(openSoak.Thrown, soaked.Thrown);

        var (_, dodger) = Pair("agility", 6);
        var dodged = Exchange(attacker, dodger, gun, table: CloseRangeOn);
        var openDodge = Exchange(attacker, dodger, gun);

        Assert.NotEqual(openDodge.Thrown, dodged.Thrown);
    }

    /// <summary>
    /// <b>It only reaches a pair who are at Close Range with each other.</b>
    ///
    /// <para>p.79 prices a gun in your face, and the same gun from across the street is the
    /// ordinary exchange. The bands are pairwise on the state because p.73's are, so this is driven
    /// by opening the fight at Distant Range rather than by moving a flag.</para>
    /// </summary>
    [Fact]
    public void CloseRangeDoesNotReachAShotFromTheNextRangeClassOut()
    {
        var (attacker, dodger) = Pair("agility", 6);
        var gun = new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon);

        var dice = new ScriptedDice([.. Enumerable.Repeat(4, 300)]);
        var encounter = new Encounter(_play, dice, CloseRangeOn);
        var state = encounter.Begin([attacker, dodger], opening: RangeBand.Distant);

        var step = encounter.Step(state, gun);
        var far = 300 - dice.Remaining;

        // The control: the shot really was resolved, so the comparison below is not of two zeroes.
        Assert.Contains(step.Added, l => l.Text.Contains("defends with", StringComparison.Ordinal));

        Assert.DoesNotContain(step.Added, l =>
            string.Equals(l.Rule, "gritty_close_range", StringComparison.Ordinal));

        // And the same shot at Close Range does cost the dice, so the band is what decided it.
        Assert.NotEqual(far, Exchange(attacker, dodger, gun, table: CloseRangeOn).Thrown);
    }

    /// <summary>
    /// <b>A Power is ranged when its own Ch.2 Range says so, and that is read off the sheet.</b>
    ///
    /// <para>p.75's table declines to classify its two Power rows — their attacking Trait is the
    /// bare <c>Power</c> column — so whether a Physical Power reaches past Close Range is a property
    /// of the Power. <c>CombatantFactory</c> answers it, and the two Powers driven here are a Blast,
    /// whose printed Range is <c>ranged</c>, and an Armor, whose printed Range is <c>self</c>.</para>
    ///
    /// <para>The controls come out of the character rules rather than being assumed: each Power's
    /// Range is asserted before the exchange that turns on it.</para>
    /// </summary>
    [Fact]
    public void APowerIsRangedWhenItsOwnPrintedRangeSaysSoAndNotOtherwise()
    {
        var rules = new RulesFixture();
        var penalty = _play.GetGritty("gritty_close_range").CloseRangePenalty!.PenaltyDiceToActiveDefense;

        // The controls: the two Powers really do print the two Ranges this fixture turns on.
        Assert.Equal("ranged", rules.Rules.GetPower("blast")!.Range, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("self", rules.Rules.GetPower("armor")!.Range, StringComparer.OrdinalIgnoreCase);

        var sheet = rules.LegalSheet();
        sheet.Name = "the Hero";

        // Enough Edge to be first in the order, so the exchange below is theirs to make.
        sheet.AbilityRanks["perception"] = 6;
        sheet.AbilityRanks["agility"] = 6;
        sheet.SelectedPowers.Add(new SelectedPower("blast", 8));
        sheet.SelectedPowers.Add(new SelectedPower("armor", 8));

        var attacker = CombatantFactory.From(
            sheet, rules.Rules, rules.Derived, _play, CombatantKind.Hero, "hero");

        Assert.Contains("blast", attacker.RangedPowers, StringComparer.Ordinal);
        Assert.DoesNotContain("armor", attacker.RangedPowers, StringComparer.Ordinal);

        // The other control: both came back at a real rank, so neither exchange below is a pool of
        // nothing. The two ranks are read rather than assumed equal — the character engine decides
        // what a Power is worth on a sheet, and this fixture is about the Range beside it.
        Assert.True(attacker.Rank("blast") > 0, "the Blast came back at 0d");
        Assert.True(attacker.Rank("armor") > 0, "the Armor came back at 0d");

        var (_, dodger) = Pair("agility", 6);

        var blast = new Attack("hero", "villain", "blast", Type: AttackType.PhysicalPower);
        var swung = blast with { TraitId = "armor" };

        var shot = Exchange(attacker, dodger, blast, table: CloseRangeOn);
        var clubbed = Exchange(attacker, dodger, swung, table: CloseRangeOn);

        // The two pools differ by the two Powers' own ranks and by the penalty, and by nothing
        // else: the Blast is ranged and pays it, the Armor is not and does not.
        Assert.Equal(
            attacker.Rank("blast") - attacker.Rank("armor") + penalty,
            shot.Thrown - clubbed.Thrown);

        Assert.Contains(shot.Lines, l =>
            string.Equals(l.Rule, "gritty_close_range", StringComparison.Ordinal)
            && l.Text.Contains($"is {penalty}d", StringComparison.Ordinal));

        // <b>And the Power the reading declined says so rather than saying nothing.</b> The dice do
        // not move — the equality above is what holds that — but a switch that is on and did not
        // reach an attack is the one case this ledger exists to make visible, and until this
        // fixture demanded the line there was none: a Power whose own Range is zone or special
        // walked out of the rule in silence, indistinguishable from a rule that had been dropped.
        var declined = Assert.Single(clubbed.Lines, l =>
            string.Equals(l.Rule, "gritty_close_range", StringComparison.Ordinal));

        Assert.Contains("armor", declined.Text, StringComparison.Ordinal);
        Assert.Contains("its own Ch.2 Range is not ranged", declined.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The page's own exception is the caller's word, and a declaration that turned nothing off
    /// says so.</b>
    ///
    /// <para>p.79 tells a table to ignore the rule for "ordinary thrown weapons and other
    /// short-range attacks that can only be used at Close Range". A fight here has no equipment in
    /// it, so nothing but the person running it can tell a pistol from a throwing knife — and the
    /// default is a pistol, because <c>range_classes</c> makes reaching Distant Range the rule and
    /// prints thrown weapons beside it as the exception.</para>
    /// </summary>
    [Fact]
    public void AThrownWeaponIsTheCallersWordAndADeclarationThatBuysNothingSaysSo()
    {
        var rule = _play.GetGritty("gritty_close_range").CloseRangePenalty!;

        var (attacker, dodger) = Pair("agility", 6);

        var gun = new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon);
        var knife = gun with { CloseRangeOnly = true };
        var fist = new Attack("hero", "villain", "might", CloseRangeOnly: true);

        var shot = Exchange(attacker, dodger, gun, table: CloseRangeOn);
        var thrown = Exchange(attacker, dodger, knife, table: CloseRangeOn);
        var punched = Exchange(attacker, dodger, fist, table: CloseRangeOn);

        Assert.Equal(shot.Thrown - rule.PenaltyDiceToActiveDefense, thrown.Thrown);
        Assert.Equal(thrown.Thrown, punched.Thrown);

        Assert.Contains(rule.IgnoredFor, Line(thrown, "gritty_close_range"), StringComparison.Ordinal);
        Assert.Contains(
            "would not have reached it anyway",
            Line(punched, "gritty_close_range"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A pair of entries that no longer name a range class past Close is a throw.</b>
    ///
    /// <para>The whole of what makes p.79's rule apply to an attack is that such an attack could
    /// have been made from farther off, and this engine establishes that by reading two strings:
    /// the entry's <c>applies_only_to_attacks_usable_at</c> and <c>range_classes</c>'
    /// <c>ranged_attacks_reach</c>. A penalty applied when its own condition can no longer be
    /// established is the shape of defect the ledger exists to prevent, so it throws.</para>
    ///
    /// <para>Both sides are driven, and the control is the same exchange against the shipped bytes.</para>
    /// </summary>
    [Theory]
    [InlineData(
        "\"ranged_attacks_reach\": \"Close Range or Distant Range\"",
        "\"ranged_attacks_reach\": \"about as far as you would expect\"",
        PlayRulesRepository.CombatFile)]
    [InlineData(
        "\"applies_only_to_attacks_usable_at\": \"Distant or Extreme Range\"",
        "\"applies_only_to_attacks_usable_at\": \"anything you could have shot from farther off\"",
        PlayRulesRepository.GrittyFile)]
    public void ARangeReachThisEngineCanNoLongerEstablishIsAThrow(
        string find, string replace, string file)
    {
        var (attacker, dodger) = Pair("agility", 6);
        var gun = new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon);

        // The control: against the shipped bytes the exchange resolves and cites the rule.
        var shipped = Exchange(attacker, dodger, gun, table: CloseRangeOn);

        Assert.Contains(shipped.Lines, l =>
            string.Equals(l.Rule, "gritty_close_range", StringComparison.Ordinal));

        var reworded = SubstitutedPlayRules.With(file, find, replace);
        var encounter = new Encounter(
            reworded, new ScriptedDice([.. Enumerable.Repeat(4, 300)]), CloseRangeOn);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            encounter.Step(encounter.Begin([attacker, dodger]), gun));

        Assert.Contains("gritty_close_range", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RangeBand.Close), thrown.Message, StringComparison.Ordinal);
    }


    // ── p.79's Drop ──────────────────────────────────────────────────────────

    /// <summary>The Drop table setting, and nothing else.</summary>
    private static readonly TableRules TheDropOn = TableRules.Book with { TheDrop = true };

    /// <summary>Two combatants, one of whom may have a weapon levelled.</summary>
    private static List<Combatant> Standoff(bool heroReady, bool villainReady) =>
    [
        Combatant.Hero(
            "hero", "the Hero", edge: 5, health: 10, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 },
            ["toughness"], ready: heroReady),
        Combatant.Villain(
            "villain", "the Villain", edge: 8, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 },
            ["toughness"], ready: villainReady)
    ];

    /// <summary>
    /// <b>A readied weapon doubles its holder's effective Edge, and it puts them in front of
    /// somebody who was ahead of them.</b>
    ///
    /// <para>Driven as an order rather than as a number, because the order is the only thing an
    /// Edge decides — a doubling nothing sorted on would be a field this engine wrote and never
    /// read. The Hero's 5 against the Villain's 8 is the pair that shows it: the doubling has to be
    /// real to move them, and 5 is deliberately below 8 so that an engine which had merely recorded
    /// the flag would leave the order alone.</para>
    ///
    /// <para><b>The switch off is the baseline and is measured first.</b> A setting that is off has
    /// to change nothing: the same two combatants, the same Edges and the same order.</para>
    /// </summary>
    [Fact]
    public void AReadiedWeaponDoublesItsHoldersEffectiveEdgeAndMovesTheOrder()
    {
        var off = new Encounter(_play, new SeededDice(79)).Begin(Standoff(heroReady: true, villainReady: false));

        // The control, and the baseline: with the setting off the flag changes nothing at all.
        Assert.Equal(["villain", "hero"], off.TurnOrder);
        Assert.Equal(5, off.EffectiveEdge["hero"]);
        Assert.DoesNotContain(off.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_the_drop", StringComparison.Ordinal));

        var drawn = new Encounter(_play, new SeededDice(79), TheDropOn)
            .Begin(Standoff(heroReady: true, villainReady: false));

        Assert.Equal(["hero", "villain"], drawn.TurnOrder);
        Assert.Equal(10, drawn.EffectiveEdge["hero"]);

        // And the character who is not ready is untouched, which is the half that makes it a
        // doubling of the holder rather than a penalty on everybody else.
        Assert.Equal(off.EffectiveEdge["villain"], drawn.EffectiveEdge["villain"]);

        var line = Assert.Single(drawn.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_the_drop", StringComparison.Ordinal)
            && l.Text.Contains("the Hero", StringComparison.Ordinal));

        var rule = _play.GetGritty("gritty_the_drop").TheDrop!;

        Assert.Contains(rule.HeldBy, line.Text, StringComparison.Ordinal);
        Assert.Contains(rule.HeldAgainst, line.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Where everybody is ready, nobody has the drop on anybody — and the order says so.</b>
    ///
    /// <para>This is the case that makes one global doubling exact rather than approximate: an
    /// order compares two characters at a time, and two ready ones double alike, so 2a against 2b
    /// sorts exactly as a against b. The fixture drives both ends of that — everybody ready and
    /// nobody ready — and requires the order to be the one the setting-off run produced.</para>
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void WhereEverybodyIsReadyOrNobodyIsTheOrderIsTheOneTheBookPrints(bool hero, bool villain)
    {
        var baseline = new Encounter(_play, new SeededDice(79)).Begin(Standoff(false, false));
        var drawn = new Encounter(_play, new SeededDice(79), TheDropOn).Begin(Standoff(hero, villain));

        Assert.Equal(baseline.TurnOrder, drawn.TurnOrder);
        Assert.Equal(baseline.EffectiveEdge, drawn.EffectiveEdge);

        // The control: the rule was reached and said so, rather than being skipped in silence.
        Assert.Contains(drawn.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_the_drop", StringComparison.Ordinal)
            && l.Text.Contains("nobody has the drop on anybody", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The half of p.79 a single order cannot carry is named, not applied.</b>
    ///
    /// <para><c>also_held_by</c> gives the drop to a shooter against anyone closing to engage them
    /// — held against one opponent and not the rest, which one order of action cannot express,
    /// because doubled against one and not another admits a cycle. The line quotes the clause and
    /// the GM's final say over the whole rule, which is what stops a reader taking the setting for
    /// the whole page.</para>
    /// </summary>
    [Fact]
    public void TheClauseAboutClosingToEngageIsNamedRatherThanApplied()
    {
        var rule = _play.GetGritty("gritty_the_drop").TheDrop!;

        var drawn = new Encounter(_play, new SeededDice(79), TheDropOn)
            .Begin(Standoff(heroReady: true, villainReady: false));

        var named = Assert.Single(drawn.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_the_drop", StringComparison.Ordinal)
            && l.Text.Contains("is not applied", StringComparison.Ordinal));

        Assert.Contains(rule.AlsoHeldBy, named.Text, StringComparison.Ordinal);
        Assert.Contains(rule.FinalSay, named.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A ready group of Minions has no Edge to double, and the ledger says so rather than
    /// doubling a zero.</b>
    ///
    /// <para>p.73 gives a Minion group no Edge at all and puts them after everyone else, and
    /// <c>minions_have_an_edge</c> is read rather than assumed. A line announcing that their
    /// effective Edge had been doubled from 0 to 0 would be a rule reported and not applied, which
    /// is the one thing this engine's ledger exists to prevent.</para>
    /// </summary>
    [Fact]
    public void AReadyGroupOfMinionsHasNoEdgeToDouble()
    {
        var fight = new List<Combatant>
        {
            Combatant.Hero("hero", "the Hero", edge: 5, health: 10, resolve: 0,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 }, ["toughness"]),
            Combatant.Minions("minions", "the Minions", threat: 4, groupSize: 3, "threat", ready: true)
        };

        var drawn = new Encounter(_play, new SeededDice(79), TheDropOn).Begin(fight);

        Assert.Equal(0, drawn.EffectiveEdge["minions"]);
        Assert.Equal(["hero", "minions"], drawn.TurnOrder);

        Assert.Contains(drawn.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_the_drop", StringComparison.Ordinal)
            && l.Text.Contains("nothing here to double", StringComparison.Ordinal));
    }

    /// <summary>
    /// Two Heroes on opposite sides, so both can hold Resolve and either can buy p.73's seize —
    /// the Drop's Edges are the caller's, so the order below is arithmetic rather than luck.
    /// </summary>
    private static List<Combatant> Standing(int north, int south, bool northReady)
    {
        var traits = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["might"] = 6, ["toughness"] = 4
        };

        return
        [
            Combatant.Hero(
                "north", "the Hero of the North", edge: north, health: 10, resolve: 2, traits,
                ["toughness"], side: "north", ready: northReady),
            Combatant.Hero(
                "south", "the Hero of the South", edge: south, health: 10, resolve: 2, traits,
                ["toughness"], side: "south")
        ];
    }

    /// <summary>The order of action on the page after <paramref name="seizer"/> buys p.73's seize.</summary>
    private IReadOnlyList<string> AfterTheSeize(
        TableRules table, IReadOnlyList<Combatant> fight, string? seizer)
    {
        var encounter = new Encounter(_play, new SeededDice(73), table);
        var state = encounter.Begin(fight);

        if (seizer is not null)
            state = encounter.Step(state, new SpendResolve(seizer, ResolveSpend.SeizeInitiative)).State;

        return encounter.Step(state, new EndPage("")).State.TurnOrder;
    }

    /// <summary>
    /// <b>p.79's Drop does not overtake p.73's seize, and the page never says whether it should.</b>
    ///
    /// <para>Seizing the initiative puts a character "first on every page of the action" — it is not
    /// an Edge effect at all, and the Drop only doubles an Edge — so composing them the way this
    /// engine composes everything leaves a seizer in front of a doubled figure however large it
    /// gets. That is a reading rather than a transcription, because neither page mentions the other,
    /// and it is in <c>docs/guide/play-engine.md</c>'s table with this fixture beside it.</para>
    ///
    /// <para><b>The control is the same fight without the purchase</b>, where the doubled Edge does
    /// win — otherwise this would pass against an engine in which the Drop did nothing at all.</para>
    /// </summary>
    [Fact]
    public void TheDropDoesNotOvertakeASeizedInitiative()
    {
        // The North's 5 doubles to 10 and beats the South's 4, which is the control: the Drop is
        // deciding this order until the purchase is made.
        Assert.Equal(
            ["north", "south"],
            AfterTheSeize(TheDropOn, Standing(north: 5, south: 4, northReady: true), seizer: null));

        // And the purchase takes the front regardless of it.
        Assert.Equal(
            ["south", "north"],
            AfterTheSeize(TheDropOn, Standing(north: 5, south: 4, northReady: true), seizer: "south"));
    }

    /// <summary>
    /// <b>Where the GM takes p.73's alternative, a ready buyer's Edge is multiplied by four, and
    /// that is a reading this fixture is here to make arguable.</b>
    ///
    /// <para>Two printed sentences each double an effective Edge — <c>the_drop.effect</c> and
    /// <c>seize_initiative_gm_alternative</c>'s — and no page in Chapters 3 to 5 contemplates both
    /// at once. This engine applies each where it is triggered, so a ready character who buys the
    /// seize under the GM's alternative acts on four times their figure. <b>Nothing settles that
    /// against the alternative of capping it at twice</b>, and a composition this large arriving
    /// silently out of two unrelated branches is exactly the shape of reading the guide's table
    /// exists for.</para>
    ///
    /// <para>The figures are chosen so that only the fourfold answer wins: the North's 5 is 10
    /// under either doubling alone and 20 under both, against a South of 18. So the first assertion
    /// is a control on the second — one doubling is not enough — and an engine that applied the
    /// Drop and then ignored the alternative, or the other way round, fails the second.</para>
    /// </summary>
    [Fact]
    public void BothDoublingsCompoundOnAReadyCharacterWhoSeizesTheInitiative()
    {
        var table = TheDropOn with { GmAlternativeToSeizingInitiative = true };

        // One doubling is not enough: 5 doubled is 10, and the South is on 18.
        Assert.Equal(
            ["south", "north"],
            AfterTheSeize(table, Standing(north: 5, south: 18, northReady: true), seizer: null));

        Assert.Equal(
            ["south", "north"],
            AfterTheSeize(table, Standing(north: 5, south: 18, northReady: false), seizer: "north"));

        // Both together are: 5 doubled by the Drop and doubled again by the GM's alternative.
        Assert.Equal(
            ["north", "south"],
            AfterTheSeize(table, Standing(north: 5, south: 18, northReady: true), seizer: "north"));
    }

    /// <summary>
    /// <b>An entry that has stopped saying "doubled" is a rule this engine refuses to apply.</b>
    ///
    /// <para><c>the_drop.effect</c> is a printed sentence rather than a multiplier, so the engine
    /// reads the word and supplies the 2 — the third rule in this chapter to work that way, beside
    /// <c>seize_initiative_gm_alternative</c> and <c>gritty_hard_targets</c>. A silent fallback
    /// against an entry somebody had corrected would apply a rule the book no longer prints.</para>
    /// </summary>
    [Fact]
    public void ADropEffectThisEngineNoLongerRecognisesIsAThrow()
    {
        const string Printed = "\"effect\": \"the holder's effective Edge is doubled against them\"";
        const string Reworded = "\"effect\": \"the holder goes first, whatever their Edge\"";

        // The control: against the shipped bytes the fight opens and the rule is cited.
        var shipped = new Encounter(_play, new SeededDice(79), TheDropOn).Begin(Standoff(true, false));

        Assert.Contains(shipped.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_the_drop", StringComparison.Ordinal));

        var reworded = SubstitutedPlayRules.With(PlayRulesRepository.GrittyFile, Printed, Reworded);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            new Encounter(reworded, new SeededDice(79), TheDropOn).Begin(Standoff(true, false)));

        Assert.Contains("gritty_the_drop", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("goes first, whatever their Edge", thrown.Message, StringComparison.Ordinal);
    }


    // ── p.80's Friendly Fire ─────────────────────────────────────────────────

    /// <summary>The Friendly Fire table setting, and nothing else.</summary>
    private static readonly TableRules FriendlyFireOn = TableRules.Book with { FriendlyFire = true };

    /// <summary>
    /// A shooter, the person they are shooting at, and however many other characters are in the
    /// tangle with the target — all of them at Close Range, which is where a fight opens.
    /// </summary>
    private static List<Combatant> Scrum(int bystanders)
    {
        var fight = new List<Combatant>
        {
            Combatant.Hero(
                "hero", "the Hero", edge: 12, health: 10, resolve: 0,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 }, ["toughness"]),
            Combatant.Villain(
                "villain", "the Villain", edge: 7, health: 10,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4 }, ["toughness"])
        };

        for (var i = 1; i <= bystanders; i++)
        {
            fight.Add(Combatant.Extra(
                $"bystander{i}", $"Bystander {i}", edge: 3, health: 10,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4 },
                ["toughness"], side: "villains"));
        }

        return fight;
    }

    /// <summary>One shot into a scrum, and everything the engine did about it.</summary>
    private (int Thrown, IReadOnlyList<LedgerLine> Lines, EncounterState State) Shoot(
        IReadOnlyList<Combatant> fight, TableRules? table, params int[] faces)
    {
        var dice = new ScriptedDice(faces);
        var encounter = new Encounter(_play, dice, table);
        var state = encounter.Begin(fight);

        Assert.Equal(faces.Length, dice.Remaining);

        var step = encounter.Step(
            state, new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon));

        return (faces.Length - dice.Remaining, step.Added, step.State);
    }

    /// <summary>
    /// <b>Shooting into a melee costs exactly the dice p.80 prints, and shooting at somebody alone
    /// costs nothing.</b>
    ///
    /// <para>"Engaged in close combat or otherwise bunched up with other characters" is derived
    /// from p.73's range bands rather than declared, so the fixture drives it by putting a
    /// bystander in the fight and taking them out again — the same shooter, the same target, the
    /// same band.</para>
    ///
    /// <para><b>The switch off is the baseline and is measured first.</b></para>
    /// </summary>
    [Fact]
    public void ShootingIntoAMeleeCostsExactlyTheDiceThePagePrints()
    {
        var rule = _play.GetGritty("gritty_friendly_fire").FriendlyFire!;

        // The control on the reading: a penalty of nothing would make the equalities below hold of
        // an engine that applied no rule at all.
        Assert.NotEqual(0, rule.PenaltyDice);

        // Faces of 6 so the shot lands and no second attack is triggered — this fixture is about
        // the penalty, and the stray round is the one below.
        int[] plenty = [.. Enumerable.Repeat(6, 60)];

        var alone = Shoot(Scrum(bystanders: 0), FriendlyFireOn, plenty);
        var crowd = Shoot(Scrum(bystanders: 1), FriendlyFireOn, plenty);

        Assert.Equal(alone.Thrown + rule.PenaltyDice, crowd.Thrown);
        Assert.DoesNotContain(alone.Lines, l =>
            string.Equals(l.Rule, "gritty_friendly_fire", StringComparison.Ordinal));

        // The switch off changes nothing, in the same crowd.
        var off = Shoot(Scrum(bystanders: 1), table: null, plenty);

        Assert.Equal(alone.Thrown, off.Thrown);
        Assert.DoesNotContain(off.Lines, l =>
            string.Equals(l.Rule, "gritty_friendly_fire", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A bystander the target is not close to is not in the melee, and the band is what decides
    /// it.</b>
    ///
    /// <para>"Engaged in close combat or otherwise bunched up with other characters" is derived
    /// from p.73's range bands rather than declared, and every fixture for that derivation opened
    /// the fight where the bands put everybody at Close — so the presence of a third character and
    /// the band they were standing in were never told apart. <b>A rule that had counted anybody in
    /// the fight at all would have passed every one of them.</b></para>
    ///
    /// <para>Here the same three characters open a class further out, the shooter walks up to the
    /// target, and the bystander stays where they were. The shot then costs exactly what a shot at
    /// somebody standing alone costs — read off the dice rather than off the prose — and no stray
    /// round is sent.</para>
    /// </summary>
    [Fact]
    public void ABystanderTheTargetIsNotCloseToIsNotInTheMelee()
    {
        int[] plenty = [.. Enumerable.Repeat(6, 80)];

        // The two figures this is measured between: nobody else in the fight, and somebody else
        // standing in the tangle. They have to differ, or the assertion below means nothing.
        var alone = Shoot(Scrum(bystanders: 0), FriendlyFireOn, plenty);
        var crowd = Shoot(Scrum(bystanders: 1), FriendlyFireOn, plenty);

        Assert.NotEqual(alone.Thrown, crowd.Thrown);

        var dice = new ScriptedDice(plenty);
        var encounter = new Encounter(_play, dice, FriendlyFireOn);
        var state = encounter.Begin(Scrum(bystanders: 1), opening: RangeBand.Distant);

        // The shooter walks up to the target, which p.74 gives them two pages' worth of; the
        // bystander does not move, so they are a class further out than the melee.
        state = encounter.Step(state, new Move("hero", "villain")).State;
        state = encounter.Step(state, new Move("hero", "villain")).State;

        Assert.Equal(RangeBand.Close, state.RangeBetween("hero", "villain"));
        Assert.Equal(RangeBand.Distant, state.RangeBetween("bystander1", "villain"));

        var before = dice.Remaining;

        var step = encounter.Step(
            state, new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon));

        Assert.Equal(alone.Thrown, before - dice.Remaining);

        Assert.DoesNotContain(step.Added, l =>
            string.Equals(l.Rule, "gritty_friendly_fire", StringComparison.Ordinal));

        Assert.Equal(
            step.State["bystander1"].FullHealth, step.State["bystander1"].CurrentHealth);
    }

    /// <summary>
    /// <b>A close combat attack into the same scrum costs nothing</b>, because p.80 prices a ranged
    /// one. Derived off p.75's table by the row's printed type, the same reading p.79's Close Range
    /// rule uses — so this is one fixture holding both.
    /// </summary>
    [Fact]
    public void FriendlyFireIsPricedOnARangedAttackAndNotOnAFist()
    {
        int[] plenty = [.. Enumerable.Repeat(6, 60)];

        var dice = new ScriptedDice(plenty);
        var encounter = new Encounter(_play, dice, FriendlyFireOn);
        var state = encounter.Begin(Scrum(bystanders: 1));

        var punch = encounter.Step(state, new Attack("hero", "villain", "might"));

        // The control: the attack really was resolved.
        Assert.Contains(punch.Added, l => l.Text.Contains("defends with", StringComparison.Ordinal));

        Assert.DoesNotContain(punch.Added, l =>
            string.Equals(l.Rule, "gritty_friendly_fire", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A shot that lands nothing sends a second attack, and the second attack is resolved rather
    /// than announced.</b>
    ///
    /// <para>This is the half of the rule a ledger line on its own would have got wrong: the page
    /// says "you must make a second attack", and an engine that wrote the sentence and rolled
    /// nothing would leave the bystander untouched while the record said otherwise. So the fixture
    /// checks the bystander's Health, not the prose — and requires the stray round to have been
    /// thrown, defended against and applied.</para>
    ///
    /// <para>The faces are scripted so that the first shot lands nothing and the second one lands:
    /// the shooter throws its pool of ones, the target defends, and then the stray round's pool
    /// comes up sixes against a defence of ones.</para>
    /// </summary>
    [Fact]
    public void AShotThatLandsNothingSendsASecondAttackThatIsActuallyResolved()
    {
        var rule = _play.GetGritty("gritty_friendly_fire").FriendlyFire!;
        var rate = _play.GetCombat("damage").Damage!.DamagePerNetSuccess;

        // p.75's Ranged Weapon row halves a Toughness, so the defence pool is half the rank.
        var soak = Halved(4);

        // 8d Might less the printed penalty, all missing; then the target's soak, likewise; then
        // one die for the GM's pick; then the stray round's whole 8d, of which two land; then the
        // bystander's soak, missing.
        int[] faces =
        [
            .. Enumerable.Repeat(1, 8 + rule.PenaltyDice),
            .. Enumerable.Repeat(1, soak),
            3,
            4, 4, .. Enumerable.Repeat(1, 6),
            .. Enumerable.Repeat(1, soak)
        ];

        var shot = Shoot(Scrum(bystanders: 1), FriendlyFireOn, faces);

        // The control: every scripted face was consumed, so the engine made exactly the rolls this
        // fixture accounts for — the first attack, the defence, the pick, and the stray round.
        Assert.Equal(faces.Length, shot.Thrown);

        var bystander = shot.State["bystander1"];

        Assert.True(
            bystander.CurrentHealth < bystander.FullHealth,
            "the stray round was announced and never landed: the bystander is untouched");

        // Two successes against nothing, at the printed rate.
        Assert.Equal(bystander.FullHealth - (2 * rate), bystander.CurrentHealth);

        var sent = Assert.Single(shot.Lines, l =>
            string.Equals(l.Rule, "gritty_friendly_fire", StringComparison.Ordinal)
            && l.Text.Contains("the shot goes somewhere", StringComparison.Ordinal));

        Assert.Contains(rule.SecondAttackIsAgainst, sent.Text, StringComparison.Ordinal);
        Assert.Contains(rule.SecondTargetSelected, sent.Text, StringComparison.Ordinal);
        Assert.Contains("a die came up 3", sent.Text, StringComparison.Ordinal);

        // And the stray round itself was at no penalty, which is the entry's own second figure.
        Assert.Contains(shot.Lines, l =>
            string.Equals(l.Rule, "gritty_friendly_fire", StringComparison.Ordinal)
            && l.Text.Contains("went wide", StringComparison.Ordinal)
            && l.Text.Contains(rule.SecondAttackPenaltyDice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A shot that landed something sends nothing</b>, which is the other side of the entry's
    /// own <c>second_attack_triggered_at_net_successes</c>. Driven at the threshold rather than
    /// past it: the same fixture, with the first shot beating the defence by one.
    /// </summary>
    [Fact]
    public void AShotThatLandedSomethingSendsNoSecondAttack()
    {
        var rule = _play.GetGritty("gritty_friendly_fire").FriendlyFire!;

        Assert.Equal(0, rule.SecondAttackTriggeredAtNetSuccesses);

        // One success on the attack — one four is one under play_meta's map — against a defence
        // that scores nothing.
        int[] faces =
        [
            4, .. Enumerable.Repeat(1, 8 + rule.PenaltyDice - 1),
            .. Enumerable.Repeat(1, Halved(4))
        ];

        var shot = Shoot(Scrum(bystanders: 1), FriendlyFireOn, faces);

        // The control: every face was consumed and no more were asked for, so no second attack was
        // rolled — which is the thing under test rather than an absence in the prose.
        Assert.Equal(faces.Length, shot.Thrown);

        var bystander = shot.State["bystander1"];

        Assert.Equal(bystander.FullHealth, bystander.CurrentHealth);
        Assert.DoesNotContain(shot.Lines, l =>
            l.Text.Contains("the shot goes somewhere", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A stray round never sends another one.</b>
    ///
    /// <para>p.80 asks for "a second attack", not for a cascade, and the guard is worth driving
    /// because the second attack goes through the whole of <see cref="Encounter.Step"/>'s attack
    /// path — including the rule that sent it. Two bystanders, both shots missing everything, and
    /// the dice source is handed exactly the faces two attacks need: a third would throw.</para>
    /// </summary>
    [Fact]
    public void AStrayRoundNeverSendsAnotherStrayRound()
    {
        var rule = _play.GetGritty("gritty_friendly_fire").FriendlyFire!;

        int[] faces =
        [
            .. Enumerable.Repeat(1, 8 + rule.PenaltyDice),    // the first attack, missing
            .. Enumerable.Repeat(1, Halved(4)),               // the target's defence
            2,                                                // the GM's pick
            .. Enumerable.Repeat(1, 8),                       // the stray round, at no penalty
            .. Enumerable.Repeat(1, Halved(4))                // the bystander's defence
        ];

        var shot = Shoot(Scrum(bystanders: 2), FriendlyFireOn, faces);

        // The control: ScriptedDice throws when it runs out, so consuming exactly this many faces
        // is the assertion that a third attack was never rolled.
        Assert.Equal(faces.Length, shot.Thrown);

        Assert.Single(shot.Lines, l =>
            l.Text.Contains("the shot goes somewhere", StringComparison.Ordinal));
    }

    /// <summary>
    /// The name p.80's random pick landed on, out of the line that records the choice — or null
    /// where the shot landed something and no second attack was sent.
    /// </summary>
    private static string? PickedOutOf(IReadOnlyList<LedgerLine> lines)
    {
        var line = lines.SingleOrDefault(l =>
            l.Text.Contains("the shot goes somewhere", StringComparison.Ordinal));

        return line?.Text.Split("which is ", StringSplitOptions.None)[1]
            .Split(". It is the", StringSplitOptions.None)[0];
    }

    /// <summary>
    /// Every scripted face one exchange in a scrum of <paramref name="bystanders"/> needs, with
    /// <paramref name="pick"/> standing in for the GM's dice — both shots missing everything, so the
    /// only variable is who the second one goes to.
    /// </summary>
    private int[] AMissIntoAScrum(int bystanders, params int[] pick) =>
    [
        .. Enumerable.Repeat(1, 8 + _play.GetGritty("gritty_friendly_fire").FriendlyFire!.PenaltyDice),
        .. Enumerable.Repeat(1, Halved(4)),
        .. pick,
        .. Enumerable.Repeat(1, 8),
        .. Enumerable.Repeat(1, Halved(4))
    ];

    /// <summary>
    /// <b>Everybody in the melee can be the one the stray round finds — including the seventh, the
    /// eighth and the ninth.</b>
    ///
    /// <para>p.80 has the GM select the second target "randomly", and this engine takes the choice
    /// off <see cref="IDiceSource"/> so a seeded run reproduces it. <b>One d6 cannot make that
    /// choice out of more than six.</b> Read as <c>(face - 1) % count</c> it reaches indices 0 to 5
    /// and no further, so in the scrum below three of the nine could never be hit at all while the
    /// ledger went on saying the target had been selected randomly — a set silently cut down to the
    /// size of the die.</para>
    ///
    /// <para>The fixture walks every face the engine can be handed, which for a melee of nine is
    /// every pair of them, and requires the names that came back to be the whole melee. <b>The
    /// control is the count</b>: an engine reaching six of the nine passes every other assertion
    /// here, and the equality against the roster is what catches it.</para>
    ///
    /// <para>The smaller scrum beside it is the other control — a melee inside one die's reach
    /// still resolves off one die, so this is not a fixture that would pass an engine which threw
    /// dice until something came up.</para>
    /// </summary>
    [Fact]
    public void EverybodyInTheMeleeCanBeTheOneTheStrayRoundFinds()
    {
        var reached = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var first in Enumerable.Range(1, 6))
        {
            foreach (var second in Enumerable.Range(1, 6))
            {
                var shot = Shoot(
                    Scrum(bystanders: 9), FriendlyFireOn, AMissIntoAScrum(9, first, second));

                // The control: every face was consumed, so the pick really was made off these dice
                // and the stray round really was resolved.
                Assert.Equal(shot.Thrown, AMissIntoAScrum(9, first, second).Length);

                var picked = PickedOutOf(shot.Lines);

                Assert.NotNull(picked);
                reached.Add(picked);
            }
        }

        var everyone = new SortedSet<string>(
            Enumerable.Range(1, 9).Select(i => $"Bystander {i}"), StringComparer.Ordinal);

        Assert.Equal(everyone, reached);

        // And a melee a single die can span is still settled by a single die, so the fix is dice
        // enough for the melee rather than dice for their own sake.
        var small = Shoot(Scrum(bystanders: 2), FriendlyFireOn, AMissIntoAScrum(2, 5));

        Assert.Equal(small.Thrown, AMissIntoAScrum(2, 5).Length);
        Assert.Contains("a die came up 5", small.Lines.Single(l =>
            l.Text.Contains("the shot goes somewhere", StringComparison.Ordinal)).Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The pick is reproducible from the seed, which is what taking it off
    /// <see cref="IDiceSource"/> is for.</b>
    ///
    /// <para>A balance figure is quoted with its seed, so a fight that picked its stray target out
    /// of some other randomness would be a fight nobody could replay. Two runs on one seed have to
    /// agree, and the control beside it is that a different seed is capable of disagreeing —
    /// otherwise an engine that always picked the first name in the melee would satisfy the
    /// equality perfectly.</para>
    /// </summary>
    [Fact]
    public void TwoSeededRunsPickTheSameUnluckyBystander()
    {
        string? Pick(int seed)
        {
            var encounter = new Encounter(_play, new SeededDice(seed), FriendlyFireOn);
            var state = encounter.Begin(Scrum(bystanders: 9));

            var step = encounter.Step(
                state, new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon));

            return PickedOutOf(step.Added);
        }

        var picks = Enumerable.Range(1, 60)
            .Select(seed => (Seed: seed, Name: Pick(seed)))
            .Where(run => run.Name is not null)
            .ToList();

        // The control: some of these seeds do send a stray round, so the equality below is about
        // picks that happened rather than about sixty absences agreeing with each other.
        Assert.NotEmpty(picks);

        foreach (var (seed, name) in picks) Assert.Equal(name, Pick(seed));

        // And the other control: the choice moves with the seed, so an engine that always named the
        // first character in the melee could not satisfy this.
        Assert.True(
            picks.Select(run => run.Name).Distinct(StringComparer.Ordinal).Count() > 1,
            "every seed picked the same character, so the pick is not coming off the dice");
    }


    /// <summary>
    /// <b>The stray round is answered by the second target's own defence, it can find the shooter's
    /// own side, and it costs the shooter neither a point of Resolve nor their turn.</b>
    ///
    /// <para><b>Hitting your own people is the whole of what p.80 is about</b> — "another target
    /// involved in the melee", with no word about sides — so the fixture puts a Hero-side ally in
    /// the tangle and requires the round to find them. Nothing in <c>Melee</c> partitions on
    /// <see cref="Combatant.Side"/>, and this is what says so from outside.</para>
    ///
    /// <para><b>The defence is driven by counting faces rather than by reading the prose.</b> The
    /// ally's Toughness is twice the first target's, so the pool the second exchange throws is a
    /// different number from the pool the first one threw, and a stray round answered by the wrong
    /// character's rank runs the scripted dice out or leaves faces on the table. That is a
    /// mechanical difference an engine copying the first defence across could not fake.</para>
    ///
    /// <para>The two figures beside it are the ones p.80 never charges for: the shot is a
    /// consequence of the first attack, not a second action, so the shooter's Resolve is untouched
    /// and it is still their turn when it is over.</para>
    /// </summary>
    [Fact]
    public void TheStrayRoundFindsTheShootersOwnAllyAndIsAnsweredByTheirOwnDefence()
    {
        var rule = _play.GetGritty("gritty_friendly_fire").FriendlyFire!;
        var rate = _play.GetCombat("damage").Damage!.DamagePerNetSuccess;

        var fight = new List<Combatant>
        {
            Combatant.Hero(
                "hero", "the Hero", edge: 12, health: 10, resolve: 2,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 }, ["toughness"]),
            Combatant.Villain(
                "villain", "the Villain", edge: 7, health: 10,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4 }, ["toughness"]),
            Combatant.Extra(
                "ally", "the Sidekick", edge: 3, health: 10,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 8 },
                ["toughness"], side: Combatant.HeroSide)
        };

        // The control on the fixture's own arithmetic: the two defences really are different pools,
        // so counting faces can tell them apart.
        Assert.NotEqual(Halved(4), Halved(8));

        int[] faces =
        [
            .. Enumerable.Repeat(1, 8 + rule.PenaltyDice),   // the shot into the scrum, missing
            .. Enumerable.Repeat(1, Halved(4)),              // the Villain's own soak
            3,                                               // the GM's pick, of one candidate
            4, 4, .. Enumerable.Repeat(1, 6),                // the stray round: two successes
            .. Enumerable.Repeat(1, Halved(8))               // and the Sidekick's own soak
        ];

        var dice = new ScriptedDice(faces);
        var encounter = new Encounter(_play, dice, FriendlyFireOn);
        var state = encounter.Begin(fight);

        var step = encounter.Step(
            state, new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon));

        // The control: every face was consumed and no more asked for, which is the assertion that
        // the second exchange threw the Sidekick's pool and not the Villain's.
        Assert.Equal(0, dice.Remaining);

        var sent = Assert.Single(step.Added, l =>
            l.Text.Contains("the shot goes somewhere", StringComparison.Ordinal));

        Assert.Contains("the Sidekick", sent.Text, StringComparison.Ordinal);

        var ally = step.State["ally"];

        Assert.Equal(Combatant.HeroSide, ally.Side);
        Assert.Equal(ally.FullHealth - (2 * rate), ally.CurrentHealth);

        // And what the second attack does not cost: p.80 makes it a consequence of the first shot
        // rather than a second action.
        Assert.Equal(state["hero"].Resolve, step.State["hero"].Resolve);
        Assert.Equal("hero", step.State.Current!.Id);
    }

    /// <summary>
    /// <b>The stray round can find the shooter's own Minions, and it defeats them the way any
    /// attack on a group does.</b>
    ///
    /// <para>A group of Minions is a target like any other and p.80's melee is derived from p.73's
    /// range bands, which know nothing about who brought whom — so a mob standing beside the person
    /// their own side was shooting at is in the tangle. Worth driving separately because a Minion
    /// group takes a different path out of the attack: there is no Health to remove, and what the
    /// round does is take bodies off the count.</para>
    /// </summary>
    [Fact]
    public void TheStrayRoundCanFindTheShootersOwnMinions()
    {
        var rule = _play.GetGritty("gritty_friendly_fire").FriendlyFire!;

        var fight = new List<Combatant>
        {
            Combatant.Hero(
                "hero", "the Hero", edge: 12, health: 10, resolve: 0,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 }, ["toughness"]),
            Combatant.Villain(
                "villain", "the Villain", edge: 7, health: 10,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4 }, ["toughness"]),
            Combatant.Minions(
                "mob", "our own robots", threat: 2, groupSize: 4, threatTraitId: "threat",
                side: Combatant.HeroSide)
        };

        int[] faces =
        [
            .. Enumerable.Repeat(1, 8 + rule.PenaltyDice),   // the shot into the scrum, missing
            .. Enumerable.Repeat(1, Halved(4)),              // the Villain's own soak
            2,                                               // the GM's pick
            .. Enumerable.Repeat(6, 8),                      // the stray round, landing hard
            .. Enumerable.Repeat(1, 30)                       // whatever the mob answers with
        ];

        var dice = new ScriptedDice(faces);
        var encounter = new Encounter(_play, dice, FriendlyFireOn);
        var state = encounter.Begin(fight);

        var step = encounter.Step(
            state, new Attack("hero", "villain", "might", Type: AttackType.RangedWeapon));

        var sent = Assert.Single(step.Added, l =>
            l.Text.Contains("the shot goes somewhere", StringComparison.Ordinal));

        Assert.Contains("our own robots", sent.Text, StringComparison.Ordinal);

        // The state, not the prose: bodies came off the count that the shooter's own side brought.
        Assert.Equal(Combatant.HeroSide, step.State["mob"].Side);
        Assert.True(
            step.State["mob"].GroupSize < state["mob"].GroupSize,
            "the round was announced against the mob and never landed on it");
    }


    // ── p.80's Slow Healing ──────────────────────────────────────────────────

    /// <summary>Slow Healing, and nothing else.</summary>
    private static readonly TableRules SlowHealingOn = TableRules.Book with { SlowHealing = true };

    /// <summary>
    /// A Hero beaten down to the defeat figure with a point of Resolve left to get up on, and a
    /// Villain standing over them.
    /// </summary>
    private (EncounterState State, Encounter Fight) Floored(TableRules table)
    {
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        var hero = Combatant.Hero(
            "hero", "the Hero", edge: 12, health: 8, resolve: 2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 2 },
            ["toughness"]);

        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 7, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 2 },
            ["toughness"]);

        var fight = new Encounter(_play, new ScriptedDice([.. Enumerable.Repeat(4, 400)]), table);
        var state = fight.Begin([hero, villain]);

        // Put the Hero on the floor without rolling for it, so this fixture is about what happens
        // next rather than about how they got there.
        state = state.With(state["hero"].WithHealth(floor));

        Assert.True(state["hero"].Defeated(floor), "the fixture did not actually put the Hero down");

        return (state, fight);
    }

    /// <summary>
    /// <b>Under Slow Healing a character brought round comes up on the Health they went down with,
    /// and stays up.</b>
    ///
    /// <para>p.80 takes away the healing "when you regain consciousness after a defeat", which is
    /// exactly what p.76's instant recovery is — so the purchase still buys the character their feet
    /// and buys them no Health at all. <b>The state is what is checked</b>: an engine that had
    /// written the sentence and left <see cref="Combatant.Defeated"/> answering true would have the
    /// character down again on the very next line, and the point would have bought a ledger
    /// entry.</para>
    ///
    /// <para><b>The setting off is the baseline and is measured first</b>, and it is the entry's own
    /// figure rather than a number typed here.</para>
    /// </summary>
    [Fact]
    public void SlowHealingBringsACharacterRoundOnTheHealthTheyWentDownWith()
    {
        var restores = _play.GetCombat("instant_recovery").InstantRecovery!.AfterADamagingDefeatRestoresHealth;
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        // The control on the reading: p.76 really does restore something, or "restores nothing" is
        // a statement about a rule that never gave anything back.
        Assert.True(restores > floor);

        var (open, ordinary) = Floored(TableRules.Book);
        var healed = ordinary.Step(open, new SpendResolve("hero", ResolveSpend.InstantRecovery)).State;

        Assert.Equal(restores, healed["hero"].CurrentHealth);
        Assert.False(healed["hero"].ConsciousAtZeroOrLess);
        Assert.False(healed["hero"].Defeated(floor));

        var (slow, gritty) = Floored(SlowHealingOn);
        var up = gritty.Step(slow, new SpendResolve("hero", ResolveSpend.InstantRecovery));

        Assert.Equal(floor, up.State["hero"].CurrentHealth);
        Assert.True(up.State["hero"].ConsciousAtZeroOrLess);

        // The half that makes it a state rather than a flag: they are on their feet at a Health
        // that would otherwise have them out of the fight.
        Assert.False(up.State["hero"].Defeated(floor));

        var line = Assert.Single(up.Added, l =>
            string.Equals(l.Rule, "gritty_slow_healing", StringComparison.Ordinal));

        Assert.Contains("single point of damage", line.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A character standing at the defeat figure goes down again to one point of damage.</b>
    ///
    /// <para>The other half of p.80's sentence, and the reason the first half is safe to apply: a
    /// character who may be conscious at nothing "is defeated if you take even a single point of
    /// damage in this condition". Driven at one point rather than at a comfortable number, because
    /// the page's word is <em>single</em> and an engine that had used the ordinary defeat test would
    /// pass at anything larger.</para>
    /// </summary>
    [Fact]
    public void OnePointOfDamagePutsAStandingCharacterBackDown()
    {
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;
        var rate = _play.GetCombat("damage").Damage!.DamagePerNetSuccess;

        Assert.Equal(1, rate);   // the fixture's control: one net success really is one point

        var (slow, fight) = Floored(SlowHealingOn);
        var state = fight.Step(slow, new SpendResolve("hero", ResolveSpend.InstantRecovery)).State;

        Assert.False(state["hero"].Defeated(floor));

        // The Villain's turn, and a single net success: 6d Might for one success against the Hero's
        // halved 2d Toughness, which scores nothing.
        state = fight.Step(state, new EndTurn("hero")).State;

        var scripted = new ScriptedDice([4, 1, 1, 1, 1, 1, 1, 1]);
        var villainsTurn = new Encounter(_play, scripted, SlowHealingOn);

        var struck = villainsTurn.Step(
            state, new Attack("villain", "hero", "might", DamageKind.Subdual)).State;

        Assert.Equal(floor, struck["hero"].CurrentHealth);
        Assert.False(struck["hero"].ConsciousAtZeroOrLess);
        Assert.True(struck["hero"].Defeated(floor), "one point of damage left them standing");
    }

    /// <summary>
    /// <b>A character p.80 leaves conscious is a character who can act: they attack, they hurt
    /// somebody, and they spend the Resolve they have left.</b>
    ///
    /// <para>"You may be conscious while at 0 or negative Health" is the whole of what this state
    /// is, and being in the fight is what conscious means — so <see cref="Combatant.Defeated"/>
    /// reading <see cref="Combatant.ConsciousAtZeroOrLess"/> is not a bookkeeping detail, it is
    /// every refusal in <c>Encounter.Step</c> at once. Every intent that is a character doing
    /// something is refused for a defeated actor, so an engine that had set the flag and left the
    /// defeat test alone would have sold a point of Resolve for a character who could stand there
    /// and nothing else.</para>
    ///
    /// <para><b>The control is the same fight without the setting</b>, where the Hero is down and
    /// each of the same two intents is refused by name. Without it this fixture would pass against
    /// an engine that had stopped refusing a defeated character anything.</para>
    /// </summary>
    [Fact]
    public void ACharacterStandingAtNothingActsLikeAnybodyElse()
    {
        (EncounterState State, IReadOnlyList<LedgerLine> Lines) Fight(TableRules table)
        {
            var (opening, fight) = Floored(table);
            var state = fight.Step(opening, new SpendResolve("hero", ResolveSpend.InstantRecovery)).State;

            var lines = new List<LedgerLine>();

            var struck = fight.Step(state, new Attack("hero", "villain", "might", DamageKind.Subdual));
            lines.AddRange(struck.Added);

            var seized = fight.Step(struck.State, new SpendResolve("hero", ResolveSpend.SeizeInitiative));
            lines.AddRange(seized.Added);

            return (seized.State, lines);
        }

        var standing = Fight(SlowHealingOn);

        Assert.True(standing.State["hero"].ConsciousAtZeroOrLess);

        // They attacked, and it landed: the state moved, not just the ledger.
        Assert.Contains(standing.Lines, l => l.Text.Contains("defends with", StringComparison.Ordinal));
        Assert.True(standing.State["villain"].CurrentHealth < standing.State["villain"].FullHealth);

        // And they bought something with the point they had left.
        Assert.Contains("hero", standing.State.Seized, StringComparer.Ordinal);
        Assert.Equal(0, standing.State["hero"].Resolve);

        // The control: the same attack by a Hero on the same Health who never stood up, which the
        // engine refuses. Without it this would pass against an engine that had stopped refusing a
        // defeated character anything.
        var (floored, ordinary) = Floored(TableRules.Book);
        var refused = ordinary.Step(floored, new Attack("hero", "villain", "might", DamageKind.Subdual));

        Assert.DoesNotContain(refused.Added, l =>
            l.Text.Contains("defends with", StringComparison.Ordinal));

        Assert.Equal(
            refused.State["villain"].FullHealth, refused.State["villain"].CurrentHealth);
    }

    /// <summary>
    /// <b>A charge's own impact is damage too, and it puts a standing character back down.</b>
    ///
    /// <para>p.80's sentence is about damage and not about attacks: "you are defeated if you take
    /// even a single point of damage in this condition". <b>An attack aimed at the character is not
    /// the only way they take one.</b> p.78 hurts the <em>charger</em> — "the charger makes their
    /// own passive defense roll against the attack to see whether the impact hurts them" — through
    /// a path that writes a Health of its own, and that path never asked the question. A character
    /// on their feet at nothing could charge a braced opponent, take two points on the ledger and
    /// walk away still standing.</para>
    ///
    /// <para><b>With Fatal Damage off the state hid it completely</b>, which is why this is driven
    /// against the defeat flag rather than against a Health: the clamp at the defeat figure leaves a
    /// character already on that figure exactly where they were, so the impact left no trace at all
    /// beyond a ledger line saying it had happened.</para>
    ///
    /// <para>The controls are the impact and the switch. The impact is required to be a real
    /// figure, so this cannot pass against a charge that hurt nobody; and the same exchange with
    /// Slow Healing off is required to leave a character who never stood up in the first
    /// place.</para>
    /// </summary>
    [Fact]
    public void AChargesOwnImpactPutsAStandingCharacterBackDown()
    {
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        // 6d of Might and the charge's two for eight dice, of which two land; the Villain's 2d
        // passive Toughness comes up sixes for four, so the charge lands nothing and the
        // self-damage is reduced by nothing; then the charger's own 2d passive answers their own
        // two successes with none, which is two points of impact.
        int[] faces = [4, 4, 1, 1, 1, 1, 1, 1, 6, 6, 1, 1];

        var (slow, fight) = Floored(SlowHealingOn);
        var state = fight.Step(slow, new SpendResolve("hero", ResolveSpend.InstantRecovery)).State;

        Assert.True(state["hero"].ConsciousAtZeroOrLess);
        Assert.False(state["hero"].Defeated(floor));

        var dice = new ScriptedDice(faces);

        var charged = new Encounter(_play, dice, SlowHealingOn)
            .Step(state, new Attack("hero", "villain", "might", DamageKind.Subdual, Charge: true));

        // The control: the charge really did come back on the charger, and for a figure rather than
        // for nothing — an impact of zero would satisfy "still standing" honestly.
        var impact = Assert.Single(charged.Added, l =>
            l.Text.Contains("the impact of", StringComparison.Ordinal));

        Assert.Contains("the impact of 2 ", impact.Text, StringComparison.Ordinal);
        Assert.Equal(0, dice.Remaining);

        Assert.False(charged.State["hero"].ConsciousAtZeroOrLess);
        Assert.True(
            charged.State["hero"].Defeated(floor),
            "the charger took the impact of their own charge and stayed on their feet");

        // And the switch off is the baseline: a Hero who never stood up is down the whole time, so
        // the flag above is Slow Healing's and not something every charge does.
        var (ordinary, plain) = Floored(TableRules.Book);
        var up = plain.Step(ordinary, new SpendResolve("hero", ResolveSpend.InstantRecovery)).State;

        Assert.False(up["hero"].ConsciousAtZeroOrLess);
    }

    /// <summary>
    /// <b>An attack that lands nothing leaves them standing</b> — "even a single point" is a point,
    /// and a miss is not one. The control on the fixture above: without this, an engine that put a
    /// standing character down on every attack aimed at them would pass it perfectly.
    /// </summary>
    [Fact]
    public void AMissLeavesAStandingCharacterOnTheirFeet()
    {
        var floor = _play.GetCombat("damage").Damage!.DefeatedAtHealth;

        var (slow, fight) = Floored(SlowHealingOn);
        var state = fight.Step(slow, new SpendResolve("hero", ResolveSpend.InstantRecovery)).State;

        state = fight.Step(state, new EndTurn("hero")).State;

        // Nothing on either side, so the attack has no net successes and does no damage.
        var scripted = new ScriptedDice([.. Enumerable.Repeat(1, 8)]);
        var missed = new Encounter(_play, scripted, SlowHealingOn)
            .Step(state, new Attack("villain", "hero", "might", DamageKind.Subdual)).State;

        Assert.True(missed["hero"].ConsciousAtZeroOrLess);
        Assert.False(missed["hero"].Defeated(floor));
    }

    /// <summary>
    /// <b>Page one says which clauses this scene carries and which it cannot.</b>
    ///
    /// <para>Half of p.80's rule is about the days after a fight, and a setting announced as on
    /// while only part of it runs is the shape of defect this ledger exists to prevent. So the
    /// out-of-scene half is named — every band of the daily rate, the after-battle healing, the
    /// Medicine limit — and so are the entry's own two notes: the lowest band's hourly figure, which
    /// is this project's arithmetic, and the ambiguity about which way the Medicine rate halves.
    /// </para>
    ///
    /// <para>Every figure is read out of the shipped entry, so a corrected file moves the fixture
    /// with it.</para>
    /// </summary>
    [Fact]
    public void PageOneSaysWhichHalfOfSlowHealingTheSceneCarries()
    {
        var entry = _play.GetGritty("gritty_slow_healing");
        var rule = entry.SlowHealing!;

        var (state, _) = Floored(SlowHealingOn);

        var opening = state.Ledger.Lines
            .Where(l => string.Equals(l.Rule, "gritty_slow_healing", StringComparison.Ordinal))
            .ToList();

        // Three: the setting's own announcement, then the two halves of what it reaches.
        Assert.Equal(3, opening.Count);
        Assert.Contains("table setting SlowHealing is on", opening[0].Text, StringComparison.Ordinal);

        var inside = opening[1].Text;
        var outside = opening[2].Text;

        Assert.Contains("regaining consciousness", inside, StringComparison.Ordinal);
        Assert.Contains("any damage at all", inside, StringComparison.Ordinal);

        // Every band of the daily rate, by the figures the file carries.
        Assert.Equal(4, rule.Bands.Count);

        foreach (var band in rule.Bands)
        {
            Assert.Contains(
                $"{band.HealthPerDay} a day", outside, StringComparison.Ordinal);
        }

        // The lowest band prints no hourly figure and the interpretation supplies one.
        Assert.Null(rule.Bands[0].OnePointEveryHours);
        Assert.Contains(
            $"1 every {entry.Interpretation!.OnePointEveryHoursForTheLowestBand} hours",
            outside,
            StringComparison.Ordinal);

        Assert.Contains(rule.MedicineHealingLimit, outside, StringComparison.Ordinal);
        Assert.Contains(entry.Ambiguity!, outside, StringComparison.Ordinal);

        // And the setting off says none of it.
        var (quiet, _) = Floored(TableRules.Book);

        Assert.DoesNotContain(quiet.Ledger.Lines, l =>
            string.Equals(l.Rule, "gritty_slow_healing", StringComparison.Ordinal));
    }


    // ── The baseline every one of these figures is measured against ──────────

    /// <summary>
    /// A fight with a Hero, a Villain and a mob in it, either declaring every fact this slice added
    /// to a <see cref="Combatant"/> or declaring none of them.
    ///
    /// <para><b>The readiness is deliberately mixed.</b> p.79's Drop doubles a holder's Edge
    /// against everyone who has not got one levelled, so a party in which everybody is ready
    /// doubles every figure and comes out in the order it went in — which would make the run below
    /// agree with its baseline for the wrong reason. Only the Villain and the mob are ready, and
    /// the Villain's doubled Edge would overtake the Hero's.</para>
    /// </summary>
    private static List<Combatant> Declaring(bool everything)
    {
        var traits = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["might"] = 6, ["agility"] = 4, ["toughness"] = 4
        };

        return
        [
            Combatant.Hero(
                "hero", "the Hero", edge: 9, health: 10, resolve: 3, traits, ["toughness", "agility"],
                hardTarget: everything),
            Combatant.Villain(
                "villain", "the Villain", edge: 8, health: 10, traits, ["toughness", "agility"],
                hardTarget: everything, ready: everything),
            Combatant.Minions(
                "mob", "the robots", threat: 5, groupSize: 4, threatTraitId: "threat",
                hardTarget: everything, ready: everything)
        ];
    }

    /// <summary>
    /// <b>A fact declared for a switch that is off leaves the whole fight where it was — the order,
    /// every Health, and the ledger line for line.</b>
    ///
    /// <para>This is the baseline every balance figure off this engine is quoted against, and it is
    /// the property the five switches in this slice most needed and had least of: each of them was
    /// driven with its own setting off, and none of them was driven against a run that had never
    /// heard of the setting at all. <b>The difference matters because the declarations are new
    /// fields on <see cref="Combatant"/></b> — a <c>hard_target</c> read one branch too early, or a
    /// <c>ready</c> that doubled an Edge before the switch was consulted, moves a measurement that
    /// is supposed to be the book's own.</para>
    ///
    /// <para>Two whole runs to the end on one seed, one party declaring every flag and one
    /// declaring none, compared on the state and on the ledger. <b>The control is that the run did
    /// something</b>: it has to have turned pages, thrown dice and taken Health off somebody, or
    /// two empty fights would agree perfectly.</para>
    /// </summary>
    [Fact]
    public void EveryDeclarationThisSliceAddedChangesNothingWhileItsSwitchIsOff()
    {
        EncounterState Run(bool declaring)
        {
            var encounter = new Encounter(_play, new SeededDice(80), TableRules.Book);

            return encounter.RunToEnd(
                encounter.Begin(Declaring(declaring)), new AttackTheWeakest(_play), maxPages: 30);
        }

        var plain = Run(declaring: false);
        var declared = Run(declaring: true);

        // The control: this was a fight and not a formality.
        Assert.True(plain.Page > 1, "the baseline run never turned a page");
        Assert.True(plain.Ledger.Lines.Count > 20, "the baseline run barely happened");
        Assert.Contains(
            plain.Combatants.Values,
            c => c.Kind != CombatantKind.MinionGroup && c.CurrentHealth < c.FullHealth);

        Assert.Equal(plain.TurnOrder, declared.TurnOrder);
        Assert.Equal(plain.Page, declared.Page);

        foreach (var id in plain.Combatants.Keys)
        {
            Assert.Equal(plain[id].CurrentHealth, declared[id].CurrentHealth);
            Assert.Equal(plain[id].GroupSize, declared[id].GroupSize);
            Assert.Equal(plain[id].Defeated(DefeatFloor), declared[id].Defeated(DefeatFloor));
        }

        Assert.Equal(
            plain.Ledger.Lines.Select(l => $"{l.Page}|{l.Rule}|{l.Text}"),
            declared.Ledger.Lines.Select(l => $"{l.Page}|{l.Rule}|{l.Text}"));
    }

    /// <summary>The defeat figure, for a fixture that has no encounter of its own to ask.</summary>
    private int DefeatFloor => _play.GetCombat("damage").Damage!.DefeatedAtHealth;

    /// <summary>
    /// <b>The two declarations this slice added to an <see cref="Attack"/> throw the same pool and
    /// take the same Health while their switches are off.</b>
    ///
    /// <para>The same reasoning as the fixture above, on the other kind of declaration. Both flags
    /// price something — <c>vulnerable_part</c> costs four dice and <c>close_range_only</c> saves a
    /// dodger two — so a flag consulted before its switch would be visible in the pool, and the pool
    /// is what the scripted dice count.</para>
    ///
    /// <para><b>Only the ledger may differ, and only in one direction</b>: a weak point declared
    /// against a table that never took Hard Targets says so, because a caller who believed they had
    /// bought something should be told they had not. That line is the only difference allowed here,
    /// and it is required to be present rather than merely tolerated.</para>
    /// </summary>
    [Fact]
    public void TheTwoDeclarationsOnAnAttackChangeNoPoolWhileTheirSwitchesAreOff()
    {
        (int Thrown, EncounterState State, IReadOnlyList<LedgerLine> Lines) Shot(bool declaring)
        {
            var dice = new ScriptedDice([.. Enumerable.Repeat(4, 60)]);
            var encounter = new Encounter(_play, dice, TableRules.Book);
            var state = encounter.Begin(Declaring(everything: false));

            var step = encounter.Step(state, new Attack(
                "hero", "villain", "might", DamageKind.Subdual, AttackType.RangedWeapon,
                VulnerablePart: declaring, CloseRangeOnly: declaring));

            return (60 - dice.Remaining, step.State, step.Added);
        }

        var plain = Shot(declaring: false);
        var declared = Shot(declaring: true);

        // The control: an attack was resolved and it took Health off somebody, so this is not two
        // refusals agreeing.
        Assert.Contains(plain.Lines, l => l.Text.Contains("defends with", StringComparison.Ordinal));
        Assert.True(plain.State["villain"].CurrentHealth < plain.State["villain"].FullHealth);

        Assert.Equal(plain.Thrown, declared.Thrown);
        Assert.Equal(plain.State["villain"].CurrentHealth, declared.State["villain"].CurrentHealth);

        // The one line the declarations are allowed to add, and it has to be there.
        Assert.Contains(declared.Lines, l =>
            string.Equals(l.Rule, "gritty_hard_targets", StringComparison.Ordinal)
            && l.Text.Contains("this table did not take Hard Targets", StringComparison.Ordinal));

        Assert.Equal(
            plain.Lines.Select(l => l.Text),
            declared.Lines
                .Where(l => !string.Equals(l.Rule, "gritty_hard_targets", StringComparison.Ordinal))
                .Select(l => l.Text));
    }

    /// <summary>
    /// <b>Page one no longer says "not yet implemented" for the five this slice applied, and still
    /// says it for the two it did not.</b>
    ///
    /// <para>That sentence is the whole of what a reader has to tell an applied setting from an
    /// accepted one, and it is generated off <see cref="Encounter.SwitchesNotYetApplied"/> rather
    /// than written per rule — so a switch removed from the code's list and left in the sentence,
    /// or the other way round, is the exact drift this fixture exists to catch. Driven on the
    /// printed text of a run with all twelve of them on at once.</para>
    /// </summary>
    [Fact]
    public void PageOneNamesTheFiveAsAppliedAndTheGearLimitAsNot()
    {
        var everything = TableRules.Book with
        {
            CloseRangePenalty = true,
            TheDrop = true,
            FriendlyFire = true,
            HardTargets = true,
            SlowHealing = true,
            RaisedGearLimit = true,
            GearLimitRank = 9
        };

        var state = new Encounter(_play, new SeededDice(80), everything)
            .Begin(Declaring(everything: false));

        string Announcement(string name) => Assert.Single(
            state.Ledger.Lines,
            l => l.Text.StartsWith($"table setting {name} is on", StringComparison.Ordinal)).Text;

        foreach (var applied in new[]
        {
            nameof(TableRules.CloseRangePenalty), nameof(TableRules.TheDrop),
            nameof(TableRules.FriendlyFire), nameof(TableRules.HardTargets),
            nameof(TableRules.SlowHealing)
        })
        {
            Assert.DoesNotContain("not yet implemented", Announcement(applied), StringComparison.Ordinal);
        }

        // And the two the engine still declines, which is what keeps the assertions above from
        // being satisfied by a page that had stopped saying "not yet implemented" about anything.
        foreach (var listed in new[]
        {
            nameof(TableRules.RaisedGearLimit), nameof(TableRules.GearLimitRank)
        })
        {
            Assert.Contains("not yet implemented", Announcement(listed), StringComparison.Ordinal);
        }
    }


    // ── p.80's Gear Limit, and why both its switches are still listed ────────

    /// <summary>
    /// <b>Nothing in a fight here is held to a Gear Limit, and the raised one cannot honestly be
    /// applied before the default one is.</b>
    ///
    /// <para>p.80 defines the limit as "the maximum effective Trait rank you can bring to bear
    /// <em>when using mundane equipment</em>", and its worked example is a Might roll plus a sword's
    /// +2d Weapon Bonus. Both halves of that are missing here, and this fixture drives both rather
    /// than asserting them.</para>
    ///
    /// <para><b>There is no equipment in a fight.</b> An <see cref="Attack"/> names a Trait id and
    /// no item, and a <see cref="Combatant"/> carries no gear — so a character built from a sheet
    /// carrying a sword is byte-for-byte the character built from the same sheet without one, and
    /// nothing downstream could tell an attack made with it from a bare-handed one.</para>
    ///
    /// <para><b>And there is no Weapon Bonus to cap.</b> <c>data/rules/</c> has no such figure at
    /// all: Ch.6's catalogue is not extracted, and a <c>SelectedGear</c> is a name with optional
    /// custom features on it. So a limit would have nothing to bite on even if an attack could name
    /// a weapon.</para>
    ///
    /// <para><b>The consequence is the reason both switches stay listed</b>, and it is checked
    /// rather than written down: <see cref="TableRules.GearLimit"/> computes the figure and no rule
    /// in <c>play/</c> reads it. A run that turns <c>RaisedGearLimit</c> on is a run whose numbers
    /// do not carry it, and page one says so.</para>
    ///
    /// <para><b>This test is written to fail when the gap closes.</b> A divergence recorded as a
    /// check that still passes after the fix is one nobody notices was closed — the same shape as
    /// the Expertise carve-out this repository already went through.</para>
    /// </summary>
    [Fact]
    public void NothingInAFightIsHeldToAGearLimitSoBothSwitchesStayListed()
    {
        var rules = new RulesFixture();

        var bare = rules.LegalSheet();
        bare.Name = "the Hero";
        bare.AbilityRanks["might"] = 8;

        var armed = rules.LegalSheet();
        armed.Name = "the Hero";
        armed.AbilityRanks["might"] = 8;
        armed.Gear.Add(new SelectedGear("a basic sword"));

        // The control: the sword really is on the second sheet, so the equality below is between
        // two different characters rather than between two copies of one.
        Assert.Empty(bare.Gear);
        Assert.Single(armed.Gear);

        var without = CombatantFactory.From(bare, rules.Rules, rules.Derived, _play, CombatantKind.Hero, "hero");
        var with = CombatantFactory.From(armed, rules.Rules, rules.Derived, _play, CombatantKind.Hero, "hero");

        // A piece of gear reaches nothing an encounter can see.
        Assert.Equal(without.TraitRanks.OrderBy(t => t.Key, StringComparer.Ordinal), with.TraitRanks.OrderBy(t => t.Key, StringComparer.Ordinal));
        Assert.Equal(without.Rank("might"), with.Rank("might"));

        // And an attack has no piece of equipment to name, so the limit could not be applied per
        // attack either: every field of p.75's attack is a Trait, a row, or a modifier.
        Assert.DoesNotContain(
            typeof(Attack).GetProperties().Select(p => p.Name),
            name => name.Contains("Gear", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Weapon", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(Combatant).GetProperties().Select(p => p.Name),
            name => name.Contains("Gear", StringComparison.OrdinalIgnoreCase));

        // <b><see cref="Attack.Item"/> is the one thing on an attack that names an object, and it is
        // not equipment this limit could bite on.</b> It carries what p.76's full grab put in the
        // actor's hands, and the claim above survives it only because naming it moves no figure —
        // which is driven rather than argued, because a field that had quietly started adding a
        // Weapon Bonus is exactly what would make the paragraph above stop being true.
        Assert.Equal(RollOf(Item: null), RollOf(Item: "a basic sword"));

        string RollOf(string? Item)
        {
            var encounter = new Encounter(_play, new SeededDice(15));

            var state = encounter.Begin(Wrestlers());
            state = state.With(state["holder"].Holds("a basic sword", state.Page));

            return encounter
                .Step(state, new Attack("holder", "held", "might", Item: Item))
                .Added
                .Single(l => l.Text.Contains("attacks", StringComparison.Ordinal)
                             && l.Text.Contains("defends with", StringComparison.Ordinal))
                .Text;
        }

        // The figure exists and is the entry's; nothing reads it. That is the whole reason the two
        // switches are still on Encounter.SwitchesNotYetApplied.
        var entry = _play.GetGritty("gritty_raised_gear_limit").GearLimit!;

        Assert.Equal(entry.DefaultRank, TableRules.Book.GearLimit(_play));

        var readers = Directory
            .EnumerateFiles(Path.Combine(RulesFixture.RepoRoot, "play"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("GearLimit(", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .ToList();

        // The control on the scan: it really can find a name, and the one it finds is the
        // definition rather than a use.
        Assert.Equal(["TableRules.cs"], readers);

        Assert.Contains(nameof(TableRules.RaisedGearLimit), Encounter.SwitchesNotYetApplied);
        Assert.Contains(nameof(TableRules.GearLimitRank), Encounter.SwitchesNotYetApplied);
    }

    /// <summary>
    /// <b>Three tests sort ledger lines by whether they say "not yet implemented", and the Gear
    /// Limit switch is the only thing left in this engine that says it.</b>
    ///
    /// <para>They used to key on p.85's first purchase naming one of the four Chapter 4 spends that
    /// charged the buyer's own pool. Those four are bought out of the GM's pool now, so no spend of
    /// either enum can produce the phrase, and each of the three moved its control to
    /// <see cref="Encounter.SwitchesNotYetApplied"/> instead. <b>That leaves all three resting on one
    /// set, and nothing said so.</b> A substring test whose subject can only ever answer one way is
    /// an instrument nobody has checked — it passes against a classifier that has stopped
    /// recognising the phrase at all, which is the shape of guard fault this repository has shipped
    /// four times.</para>
    ///
    /// <para><b>So this is the guard that fires when the last two-valued case goes.</b> The day
    /// p.80's Gear Limit is applied for real, that set empties, three controls die in the same
    /// commit and nothing else here would say a word about it. The message names them, because a
    /// failure that says "the set is empty" sends a reader to delete the assertion rather than to
    /// the three tests that then have nothing behind them.</para>
    ///
    /// <para>The pair below is the instrument itself, driven once in the place that owns it: a run
    /// with a listed switch on says the phrase on page one and a run with an applied one does not.
    /// A switch removed from the list and left unapplied fails here rather than in three tests at
    /// once, each of them saying something else.</para>
    /// </summary>
    [Fact]
    public void TheThreeClassifierTestsRestOnASwitchThisEngineStillDeclines()
    {
        Assert.True(
            Encounter.SwitchesNotYetApplied.Count > 0,
            "Encounter.SwitchesNotYetApplied is empty, so nothing in this engine writes a `not yet "
            + "implemented` ledger line any more — and three tests sort on that phrase and have "
            + "nothing left to prove they can still see it: "
            + $"{nameof(EveryPurchaseEitherRefusesByNameOrResolves)} and "
            + $"{nameof(EveryLedgerLineCitesAnEntryThatExistsAndThatEntrysPage)} in this file, and "
            + "McpPlayPolicyTests.EveryPurchaseTheGmsPoolMayNameAnswersTheWayThePolicySaysItDoes. "
            + "Each needs a new two-valued control, or the classifier is measuring nothing and "
            + "should be retired with them.");

        var traits = new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6, ["toughness"] = 4 };

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 2, traits, ["toughness"]);
        var villain = Combatant.Villain("villain", "the Villain", edge: 5, health: 10, traits, ["toughness"]);

        // The listed switch, which page one announces as carried by nothing.
        var unapplied = new Encounter(_play, new SeededDice(21), TableRules.Book with { RaisedGearLimit = true })
            .Begin([hero, villain]);

        Assert.Contains(unapplied.Ledger.Lines, l =>
            l.Text.Contains("not yet implemented", StringComparison.Ordinal)
            && string.Equals(l.Rule, "gritty_raised_gear_limit", StringComparison.Ordinal));

        // And an applied one, which does not — or "says the phrase" would be true of every run and
        // the three tests would be sorting a pile with one thing in it.
        var applied = new Encounter(_play, new SeededDice(21), TableRules.Book with { FatalDamage = true })
            .Begin([hero, villain]);

        Assert.DoesNotContain(applied.Ledger.Lines, l =>
            l.Text.Contains("not yet implemented", StringComparison.Ordinal));

        Assert.Contains(applied.Ledger.Lines, l =>
            l.Text.Contains("table setting FatalDamage is on", StringComparison.Ordinal));
    }

    /// <summary>The one line an exchange wrote citing <paramref name="ruleId"/>.</summary>
    private static string Line((int Thrown, IReadOnlyList<LedgerLine> Lines) exchange, string ruleId) =>
        Assert.Single(exchange.Lines, l => string.Equals(l.Rule, ruleId, StringComparison.Ordinal)).Text;

    /// <summary>
    /// Markdown with its emphasis and its hard wrapping taken out, so a sentence is looked for as a
    /// sentence — a phrase tested for raw passes or fails on where the wrap and the bold markers
    /// happened to land, which is a check that breaks on a reflow and says nothing on a deletion.
    /// </summary>
    private static string Prose(string markdown) =>
        string.Join(' ', markdown.Replace("*", "", StringComparison.Ordinal)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Half, the way the Glossary's book-wide rule rounds — the engine's own halving.</summary>
    private int Halved(int value) => Rounding.Half(_play, value);

}
