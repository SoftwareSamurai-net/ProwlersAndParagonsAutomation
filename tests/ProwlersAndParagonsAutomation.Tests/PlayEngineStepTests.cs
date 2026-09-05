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
                ["might"] = 3, ["agility"] = 2, ["toughness"] = 4, ["energy_blast"] = 12
            },
            ["toughness"]);

        var available = policy.TraitsAvailableTo(exotic);

        // A Power the table does not name is one of theirs, and the Abilities it names are there too.
        Assert.Contains("energy_blast", available, StringComparer.Ordinal);
        foreach (var trait in attacking) Assert.Contains(trait, available, StringComparer.Ordinal);

        // The Traits the table names only as defences are not attacking Traits.
        Assert.DoesNotContain("toughness", available, StringComparer.Ordinal);

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
    /// </summary>
    [Fact]
    public void TheGuidesNotAppliedListsAreTheEnginesNotAppliedLists()
    {
        // The controls: the parse found a table, and the sets are not empty — an empty-equals-empty
        // comparison is the shape of a guard that proves nothing.
        var entries = ListedUnder("**`Encounter.EntriesNotYetApplied`**");
        var switches = ListedUnder("**`Encounter.SwitchesNotYetApplied`**");

        Assert.NotEmpty(entries);
        Assert.NotEmpty(switches);

        Assert.Equal(
            Encounter.EntriesNotYetApplied.Order(StringComparer.Ordinal),
            entries.Order(StringComparer.Ordinal));

        Assert.Equal(
            Encounter.SwitchesNotYetApplied.Order(StringComparer.Ordinal),
            switches.Order(StringComparer.Ordinal));

        // And every entry the engine says it does not apply is an entry that exists.
        var known = _play.EntryIds().Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var id in Encounter.EntriesNotYetApplied)
            Assert.True(known.Contains(id), $"the engine lists '{id}', which is in none of the five files");
    }

    /// <summary>
    /// <b>Every purchase either refuses by name against a listed entry, or resolves.</b>
    ///
    /// <para>The list above is a static field, and a static field is a claim like any other: it stays
    /// true until somebody implements one of the things on it and forgets. So every member of both
    /// spend enums is driven through <see cref="Encounter.Step"/> and sorted by what actually
    /// happened — a purchase that refuses must be on the list, and one that does something must not
    /// be.</para>
    ///
    /// <para>Both halves have to be non-empty, which is the positive control: a run in which nothing
    /// refused, or nothing resolved, would satisfy the comparison and prove nothing.</para>
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

        foreach (var kind in Enum.GetValues<AdversitySpend>())
        {
            var encounter = new Encounter(_play, new SeededDice(21));
            var state = encounter.Begin([hero, villain]);

            var step = encounter.Step(state, new SpendAdversity("villain", kind));

            foreach (var line in step.Added.Where(l =>
                         l.Text.Contains("not yet implemented", StringComparison.Ordinal)))
            {
                refused.Add(line.Rule);
            }
        }

        // The controls: both halves happened.
        Assert.NotEmpty(refused);
        Assert.NotEmpty(resolved);

        foreach (var rule in refused)
        {
            Assert.True(Encounter.EntriesNotYetApplied.Contains(rule),
                $"'{rule}' refuses as not yet implemented and is not on Encounter.EntriesNotYetApplied");
        }

        // And the purchases that are implemented say so by being missing from the refusals.
        Assert.Contains(ResolveSpend.Reroll, resolved);
        Assert.Contains(ResolveSpend.InstantRecovery, resolved);
        Assert.DoesNotContain("instant_recovery", refused, StringComparer.Ordinal);
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

        var step = encounter.Step(
            encounter.Begin(Wrestlers()), new GrappleIntent("holder", "held", move));

        Assert.Equal(0, dice.Remaining);

        var grapple = Assert.Single(step.State.Grapples);

        Assert.Equal(move, grapple.Move);
        Assert.Equal(expected, grapple.Kind);
        Assert.Equal("holder", grapple.Holder);
        Assert.Equal("held", grapple.Held);
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

        var encounter = new Encounter(_play, new SeededDice(6), TableRules.Book with { FatalDamage = true });
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
            new SpendAdversity("villain", AdversitySpend.Villainy),
            new SpendAdversity("villain", AdversitySpend.Misfortune, Points: 999)
        };

        foreach (var intent in refusals) state = encounter.Step(state, intent).State;

        state = encounter.RunToEnd(state, new AttackTheWeakest(_play), maxPages: 20);

        // The control: the refusals really happened, so the assertions below are about lines that
        // exist. A run that produced only ordinary lines would satisfy them trivially.
        Assert.Contains(state.Ledger.Lines, l =>
            l.Text.Contains("holds no Resolve", StringComparison.Ordinal));
        Assert.Contains(state.Ledger.Lines, l =>
            l.Text.Contains("not yet implemented", StringComparison.Ordinal));
        Assert.Contains(state.Ledger.Lines, l =>
            l.Text.Contains("is not the Villain's turn", StringComparison.Ordinal));

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
}
