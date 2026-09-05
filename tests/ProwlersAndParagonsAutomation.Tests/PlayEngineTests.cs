using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The book's own worked examples, replayed through the engine.</b>
///
/// <para><b>The rule these follow.</b> A mechanic is proved by a printed example or by a property,
/// never by a test that restates the code. Two transcriptions can agree and both be wrong — which is
/// why <c>data/rules/play</c> is held to the book by a reflection walk rather than by a second copy
/// of itself — and an engine checked against a test somebody wrote from the same reading of the page
/// is a third transcription with the same defect. The authors' own arithmetic cannot be talked
/// round.</para>
///
/// <para><b>Every fixture carries a positive control before it asserts an outcome.</b> The book
/// prints <em>successes</em> and <see cref="IDiceSource"/> returns <em>faces</em>, so each fixture
/// scripts faces chosen to produce the printed success counts and then asserts that the engine
/// counted exactly those — before asserting what the successes did. Three of this repository's four
/// historical guard faults were a feature that did not run being mistaken for a feature that worked,
/// and an engine that skipped a defence roll would otherwise reach the printed answer by the wrong
/// route. <see cref="ScriptedDice.Remaining"/> is asserted to be zero at the end of the long ones,
/// which catches an engine making <em>more</em> rolls than the page as well as fewer.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlayEngineTests
{
    private readonly PlayRulesRepository _play;

    public PlayEngineTests(PlayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _play = fixture.Play;
    }

    // ── Chapter 3 ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>p.67's arm-wrestling exhibition, through the engine's own dice path.</b> Citizen Soldier
    /// declines to roll his 12d and banks 6; Gatecrasher throws
    /// <c>1,2,2,2,3,3,3,5,5,5,6,6</c> for 7; one net success puts him in the Actor-with-Embellishment
    /// band.
    ///
    /// <para>Distinct from <c>PlayRulesDataTests</c>'s fixture on the same example, which counts the
    /// dice with a test helper. This one goes through <see cref="SuccessCounter"/> and
    /// <see cref="ScriptedDice"/> — the code an encounter actually uses — so an engine that read the
    /// map wrongly fails here where the data test would stay green.</para>
    /// </summary>
    [Fact]
    public void TheArmWrestlingExampleOnPage67ComesOutAsPrinted()
    {
        int[] printedFaces = [1, 2, 2, 2, 3, 3, 3, 5, 5, 5, 6, 6];
        var dice = new ScriptedDice(printedFaces);
        var counter = new SuccessCounter(_play);

        // The fixture's own control: the pool really is the twelve dice the page rolls. A fixture
        // that had quietly lost one would still produce a number.
        Assert.Equal(12, printedFaces.Length);

        var banked = counter.AutomaticSuccesses(12);
        var rolled = counter.Roll(printedFaces.Length, dice);

        Assert.Equal(6, banked);              // "Citizen Soldier … takes 6 automatic successes"
        Assert.Equal(7, rolled.Successes);    // "Gatecrasher rolls … 7 successes"
        Assert.Equal(0, dice.Remaining);

        var net = rolled.Successes - banked;
        Assert.Equal(1, net);

        var band = Band(net);
        Assert.Equal("actor", band.Outcome);
        Assert.True(band.Embellishment);
    }

    // ── Chapter 4: movement and the chase ────────────────────────────────────

    /// <summary>
    /// <b>p.74's movement example.</b> Powermad is on foot and "it'll take him 2 pages to run up to
    /// the thing"; Flicker has 9d Running and "can move to within Close Range … in 1 page".
    ///
    /// <para>The rank that halves the cost is the entry's, not the fixture's: the control asserts
    /// Flicker's 9d actually clears whatever <c>travel_power_rank_required</c> says, so a raised
    /// threshold fails here rather than quietly making both answers 2.</para>
    /// </summary>
    [Fact]
    public void TheMovementExampleOnPage74ComesOutAsPrinted()
    {
        var required = _play.GetCombat("movement").Movement!.TravelPowerRankRequired;

        const int flickerRunning = 9;
        Assert.True(flickerRunning >= required,
            $"Flicker's {flickerRunning}d Running has to clear the {required}d the entry requires, "
            + "or this example is not exercising the rule it illustrates.");

        Assert.Equal(2, Movement.PagesToCrossARangeClass(_play, travelPowerRank: 0));
        Assert.Equal(1, Movement.PagesToCrossARangeClass(_play, flickerRunning));
    }

    /// <summary>
    /// <b>p.74's chase, one exchange at a time.</b> Flicker starts at Distant Range and wins three
    /// exchanges with 3, 1 and 4 net successes: the first closes a class and lends her dice, the
    /// second lends dice and nothing else, and the third ends the pursuit.
    /// </summary>
    [Fact]
    public void TheChaseExampleOnPage74ComesOutAsPrinted()
    {
        var first = Movement.Exchange(_play, net: 3, RangeBand.Distant);
        Assert.True(first.ClosedARangeClass);   // "She closes to within Close Range of the getaway car"
        Assert.Equal(2, first.BonusDiceNextExchange);
        Assert.False(first.Ended);

        var second = Movement.Exchange(_play, net: 1, RangeBand.Close);
        Assert.False(second.ClosedARangeClass); // "not enough to close the distance any further"
        Assert.Equal(2, second.BonusDiceNextExchange);
        Assert.False(second.Ended);

        var third = Movement.Exchange(_play, net: 4, RangeBand.Close);
        Assert.True(third.Ended);               // "scoring 4 net successes and ending the chase"
    }

    // ── Chapter 4: the special effect and the escape from it ─────────────────

    /// <summary>
    /// <b>p.76's Mind Control.</b> Heartbreaker rolls 8 against Parthian's 3 — five net successes —
    /// and "gains control of Parthian's mind for 3 pages", which only half-rounding-up produces.
    ///
    /// <para>This is the fixture the broken twin in <see cref="PlayEngineTwinTests"/> is aimed at:
    /// substituting the rounding direction gives two pages where the page prints three.</para>
    /// </summary>
    [Fact]
    public void TheSpecialEffectExampleOnPage76ComesOutAsPrinted()
    {
        var (encounter, state, dice) = MindControlOnParthian();

        Assert.Equal(0, dice.Remaining);

        // Positive control: the engine counted the successes the page prints, from faces.
        Assert.Equal(8, state.LastAttack!.AttackSuccesses);
        Assert.Equal(3, state.LastAttack.DefenceSuccesses);

        var effect = Assert.Single(state.Effects);
        Assert.Equal("Mind Control", effect.Name);
        Assert.Equal(3, effect.RemainingPages);

        // And the outcome is on the ledger with its citation, not only in the state.
        Assert.Contains(state.Ledger.Lines, l =>
            string.Equals(l.Rule, "special_effects", StringComparison.Ordinal)
            && l.SourceRef.Contains("p.76", StringComparison.Ordinal));

        Assert.NotNull(encounter);
    }

    /// <summary>
    /// <b>p.76's escape from that Mind Control.</b> Parthian rolls 7 against Heartbreaker's 4 —
    /// three net successes — and "this reduces the Mind Control duration by 2 pages", leaving one of
    /// the three, so he is loose after Heartbreaker's next turn rather than at once.
    /// </summary>
    [Fact]
    public void TheBreakFreeExampleOnPage76ComesOutAsPrinted()
    {
        var (encounter, state, _) = MindControlOnParthian();

        state = encounter.Step(state, new EndTurn("heartbreaker")).State;
        Assert.Equal("parthian", state.Current!.Id);

        // Parthian rolls 7 successes on the passive defence the Power names, against the Power's
        // own rank of 4 — the two figures the page prints for this roll.
        var escape = new Encounter(_play, new ScriptedDice(Faces(pool: 8, successes: 7)));

        state = escape.Step(state, new BreakFree("parthian", "willpower", Threshold: 4)).State;

        var effect = Assert.Single(state.Effects);
        Assert.Equal(1, effect.RemainingPages);

        Assert.Contains(state.Ledger.Lines, l =>
            string.Equals(l.Rule, "breaking_free", StringComparison.Ordinal)
            && l.Text.Contains("removes 2 of the 3 pages", StringComparison.Ordinal));
    }

    // ── Chapter 4: Fatal Damage ──────────────────────────────────────────────

    /// <summary>
    /// <b>p.79's Clint Castle, with the Fatal Damage rule on.</b> Five Health, down to one, and a
    /// ninja master stabs him for six: "taking him down to −5 Health. Clint's full Health is 5, so
    /// that's just enough to kill him. Our hero spends 1 Resolve to prevent that from happening,
    /// leaving him at −4 Health."
    ///
    /// <para><b>−4 is one point <em>above</em> the threshold, and p.79's own words say below.</b> The
    /// entry carries the printed word as a fact and the arithmetic the example supports as an
    /// <c>interpretation</c>; the engine follows the interpretation and its ledger line says so, so
    /// a reader of a run can see that a choice was made rather than assumed. That ledger line is
    /// asserted here, because a reading applied silently is a reading nobody can argue with.</para>
    /// </summary>
    [Fact]
    public void TheFatalDamageExampleOnPage79ComesOutAsPrinted()
    {
        var table = TableRules.Book with { FatalDamage = true };

        var clint = Combatant.Hero(
            "clint", "Clint Castle", edge: 6, health: 5, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 4, ["might"] = 4 },
            ["toughness"]).WithHealth(1);

        var ninja = Combatant.Villain(
            "ninja", "a ninja master", edge: 9, health: 8,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 12 },
            ["agility"]);

        // Six damage means six net successes: the ninja scores 8 against Clint's 2. Toughness of 4
        // answers a lethal attack at half, which is 2d.
        var dice = new ScriptedDice([.. Faces(12, 8), .. Faces(2, 2)]);
        var encounter = new Encounter(_play, dice, table);

        var state = encounter.Begin([ninja, clint]);
        Assert.Equal("ninja", state.Current!.Id);

        state = encounter.Step(state, new Attack("ninja", "clint", "might")).State;

        Assert.Equal(8, state.LastAttack!.AttackSuccesses);
        Assert.Equal(2, state.LastAttack.DefenceSuccesses);
        Assert.Equal(0, dice.Remaining);

        // "taking him down to −5 Health … that's just enough to kill him"
        Assert.Equal(-5, state["clint"].CurrentHealth);
        Assert.Equal(-state["clint"].FullHealth, state["clint"].CurrentHealth);

        state = encounter.Step(state, new SpendResolve("clint", ResolveSpend.AvoidFatalDamage)).State;

        // "leaving him at −4 Health"
        Assert.Equal(-4, state["clint"].CurrentHealth);
        Assert.Equal(2, state["clint"].Resolve);

        var line = Assert.Single(state.Ledger.Lines,
            l => string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
                 && l.Text.Contains("spends 1 Resolve", StringComparison.Ordinal));

        // The line quotes the printed word and says it is following the example instead, and both
        // halves are read off the entry rather than typed here — so a correction to either the fact
        // field or the interpretation moves this assertion with it.
        var gritty = _play.GetGritty("gritty_fatal_damage");

        Assert.Contains(gritty.FatalDamage!.ResolveReducesDamageTo, line.Text, StringComparison.Ordinal);
        Assert.NotEqual(gritty.FatalDamage.ResolveReducesDamageTo, gritty.Interpretation!.ResolveReducesDamageTo);
        Assert.Contains("the example is what this follows", line.Text, StringComparison.Ordinal);
    }

    // ── Chapter 4: the whole worked fight ────────────────────────────────────

    /// <summary>
    /// <b>p.81's Example of Combat, stepped through from the top.</b> Every roll the page prints,
    /// in the order it prints them, with faces chosen to produce the printed success counts.
    ///
    /// <para>This is the strongest fixture here and the only one that exercises the engine end to
    /// end: the order of action, an attack and its damage, a Minion group wiped out, an attack that
    /// does nothing, a grapple that becomes a full hold, a failed escape, and a hit worth one net
    /// success whose narrative control belongs to the actor with an embellishment for the GM. Every
    /// printed success count is asserted before the outcome it produced, and the scripted dice are
    /// required to be exhausted at the end.</para>
    ///
    /// <para>Two figures the page never prints are chosen here and asserted against nothing: the
    /// mecha's Health, and the three characters' unremarkable secondary Traits. Nothing below turns
    /// on either — the mecha is never in danger of going down, and every defence the page names is
    /// the largest a character has by a wide margin.</para>
    /// </summary>
    [Fact]
    public void TheExampleOfCombatOnPage81ResolvesThroughTheEngine()
    {
        var mecha = Combatant.Villain(
            "mecha", "the mecha", edge: 13, health: 30,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 13, ["armor"] = 15, ["toughness"] = 4, ["agility"] = 2
            },
            ["armor", "toughness", "agility"]);

        var soldier = Combatant.Hero(
            "soldier", "Citizen Soldier", edge: 12, health: 10, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 12, ["toughness"] = 6, ["agility"] = 4
            },
            ["toughness", "agility"]);

        var gatecrasher = Combatant.Hero(
            "gatecrasher", "Gatecrasher", edge: 9, health: 10, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 12, ["blast"] = 15, ["armor"] = 12, ["toughness"] = 4, ["agility"] = 3
            },
            ["armor", "toughness", "agility"]);

        var robots = Combatant.Minions("robots", "the robotic Minions", threat: 6, groupSize: 4, "threat");

        var dice = new ScriptedDice([
            .. Faces(13, 8),  .. Faces(12, 6),   // the mecha's 13d Might against Gate's 12d Armor
            .. Faces(12, 8),  .. Faces(6, 3),    // the Soldier's 12d Might against 6d Threat
            .. Faces(12, 9),  .. Faces(15, 9),   // Gatecrasher's 12d Might against 15d Armor
            .. Faces(13, 7),  .. Faces(12, 4),   // the mecha's hold
            .. Faces(12, 6),  .. Faces(13, 6),   // the Soldier's escape attempt
            .. Faces(15, 8),  .. Faces(15, 7)    // Gatecrasher's 15d Blast against 15d Armor
        ]);

        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([soldier, mecha, gatecrasher, robots]);

        // "the mecha has an Edge of 13, Citizen Soldier has an Edge of 12, and Gatecrasher has an
        // Edge of 9 … the Minions … act after everyone else"
        Assert.Equal(["mecha", "soldier", "gatecrasher", "robots"], state.TurnOrder);

        // Page 1, the mecha: "It rolls its 13d Might and gets 8 successes. Gate uses his 12d Armor
        // to defend himself and rolls 6 successes. With a total of 2 net successes … 2 points of damage."
        state = encounter.Step(state, new Attack("mecha", "gatecrasher", "might")).State;
        Assert.Equal((8, 6), (state.LastAttack!.AttackSuccesses, state.LastAttack.DefenceSuccesses));
        Assert.Equal(10 - 2, state["gatecrasher"].CurrentHealth);
        state = encounter.Step(state, new EndTurn("mecha")).State;

        // Citizen Soldier: "The Soldier rolls 8 successes … the robots roll 3 successes … With 5 net
        // successes, our Hero could have defeated up to five … turns these four into scrap metal."
        state = encounter.Step(state, new Attack("soldier", "robots", "might")).State;
        Assert.Equal((8, 3), (state.LastAttack!.AttackSuccesses, state.LastAttack.DefenceSuccesses));
        Assert.Equal(0, state["robots"].GroupSize);

        Assert.Contains(state.Ledger.Lines, l =>
            string.Equals(l.Rule, "attacking_minions", StringComparison.Ordinal)
            && l.Text.Contains("could defeat 5 Minions", StringComparison.Ordinal)
            && l.Text.Contains("4 defeated", StringComparison.Ordinal));

        state = encounter.Step(state, new EndTurn("soldier")).State;

        // Gatecrasher: "Using his own 12d Might, he rolls 9 successes. However, the mecha also gets
        // 9 successes when it rolls its 15d Armor … Gatecrasher's attack has no effect."
        state = encounter.Step(state, new Attack("gatecrasher", "mecha", "might")).State;
        Assert.Equal((9, 9), (state.LastAttack!.AttackSuccesses, state.LastAttack.DefenceSuccesses));
        Assert.Equal(30, state["mecha"].CurrentHealth);
        state = encounter.Step(state, new EndTurn("gatecrasher")).State;

        state = encounter.Step(state, new EndTurn("robots")).State;
        state = encounter.Step(state, new EndPage("")).State;
        Assert.Equal(2, state.Page);

        // Page 2, the mecha: "Using its 13d Might, the mecha rolls 7 successes. The Soldier … only
        // manages to score 4 … With 3 net successes, the mecha places our Hero in a full hold."
        state = encounter.Step(state, new GrappleIntent("mecha", "soldier", GrappleMove.Hold)).State;

        var hold = Assert.Single(state.Grapples);
        Assert.Equal(GrappleKind.Full, hold.Kind);
        Assert.Equal("soldier", hold.Held);
        state = encounter.Step(state, new EndTurn("mecha")).State;

        // The Soldier: "He makes a Might roll and gets 6 successes, but the mecha … also gets 6.
        // With no net successes, the Soldier remains trapped."
        state = encounter.Step(state, new GrappleIntent("soldier", "mecha", GrappleMove.Escape)).State;
        Assert.Equal(GrappleKind.Full, Assert.Single(state.Grapples).Kind);
        state = encounter.Step(state, new EndTurn("soldier")).State;

        // Gatecrasher: "fires his eyebeams (a 15d Blast) … 8 successes. The mecha uses its 15d Armor
        // … and gets 7. One net success … is enough for narrative control … the GM gets an embellishment."
        state = encounter.Step(state, new Attack("gatecrasher", "mecha", "blast")).State;
        Assert.Equal((8, 7), (state.LastAttack!.AttackSuccesses, state.LastAttack.DefenceSuccesses));
        Assert.Equal(29, state["mecha"].CurrentHealth);

        var narration = state.Ledger.Lines.Last(l =>
            string.Equals(l.Rule, "narrative_control", StringComparison.Ordinal));

        Assert.Contains("to the actor", narration.Text, StringComparison.Ordinal);
        Assert.Contains("embellishment", narration.Text, StringComparison.Ordinal);

        // The control that catches an engine making a roll the page does not, or skipping one it does.
        Assert.Equal(0, dice.Remaining);
    }

    // ── Chapter 5 ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>p.85's Adversity example.</b> "If you have four Heroes in your game and you designate a
    /// scene as Challenge Level 2, you would gain 8 points of Adversity."
    ///
    /// <para>The award is not asserted directly, because the pool a fight opens with is the sum of
    /// two rules — one point per Hero per issue, plus the scene's award. It is <em>derived</em>: the
    /// same party with no Challenge Level opens on four, and with Challenge Level 2 opens on twelve,
    /// so the scene is worth eight. Dropping the party size from the entry's own factor list gives 2
    /// and turning the product into a sum gives 6; only the transcription the book prints gives 8.
    /// </para>
    /// </summary>
    [Fact]
    public void TheAdversityExampleOnPage85ComesOutAsPrinted()
    {
        var party = Enumerable.Range(1, 4)
            .Select(i => Combatant.Hero(
                $"hero{i}", $"Hero {i}", edge: 6, health: 6, resolve: 4,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 6 },
                ["toughness"]))
            .ToList();

        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 5, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 },
            ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(1));

        var plain = encounter.Begin([.. party, villain]);
        var levelled = encounter.Begin([.. party, villain], challengeLevel: 2);

        Assert.Equal(4, plain.Adversity);            // one per Hero per issue, and four Heroes
        Assert.Equal(12, levelled.Adversity);
        Assert.Equal(8, levelled.Adversity - plain.Adversity);

        Assert.Contains(levelled.Ledger.Lines, l =>
            string.Equals(l.Rule, "adversity_earn_challenge_level", StringComparison.Ordinal)
            && l.Text.Contains("awards 8 Adversity", StringComparison.Ordinal));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// p.76's Mind Control, set up and thrown: Heartbreaker's 8 successes against Parthian's 3.
    /// Shared by the effect fixture and the break-free fixture, which are two halves of one printed
    /// exchange.
    /// </summary>
    private (Encounter Encounter, EncounterState State, ScriptedDice Dice) MindControlOnParthian()
    {
        var heartbreaker = Combatant.Villain(
            "heartbreaker", "Heartbreaker", edge: 10, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["mind_control"] = 10, ["willpower"] = 5 },
            ["willpower"]);

        var parthian = Combatant.Hero(
            "parthian", "Parthian", edge: 8, health: 8, resolve: 0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["willpower"] = 8, ["toughness"] = 5 },
            ["willpower", "toughness"]);

        var dice = new ScriptedDice([.. Faces(10, 8), .. Faces(8, 3)]);
        var encounter = new Encounter(_play, dice);

        var state = encounter.Begin([heartbreaker, parthian]);
        Assert.Equal("heartbreaker", state.Current!.Id);

        state = encounter
            .Step(state, new Attack(
                "heartbreaker", "parthian", "mind_control", DamageKind.Psychic, Effect: "Mind Control"))
            .State;

        return (encounter, state, dice);
    }

    /// <summary>
    /// Faces for a pool of <paramref name="pool"/> dice worth exactly <paramref name="successes"/>
    /// under the printed map — sixes first, then one four if an odd success is left, then ones.
    ///
    /// <para><b>The book prints successes and the dice source returns faces</b>, so a fixture has to
    /// bridge the two, and this is that bridge. It throws rather than approximating: a pool too
    /// small for the successes asked of it is a fixture that would otherwise assert a number the
    /// dice cannot produce.</para>
    /// </summary>
    private static int[] Faces(int pool, int successes)
    {
        var sixes = successes / 2;
        var four = successes % 2;

        if (sixes + four > pool)
        {
            throw new ArgumentOutOfRangeException(
                nameof(successes), successes,
                $"{pool} dice cannot be made to score {successes} successes under the printed map.");
        }

        return [
            .. Enumerable.Repeat(6, sixes),
            .. Enumerable.Repeat(4, four),
            .. Enumerable.Repeat(1, pool - sixes - four)
        ];
    }

    /// <summary>The narrative-control band a net-success figure falls in, out of the shipped data.</summary>
    private Play.Rules.Models.ChallengeBandModel Band(int net) =>
        _play.GetChallenge("narrative_control").Bands!.Single(b =>
            (b.MinNetSuccesses is null || net >= b.MinNetSuccesses)
            && (b.MaxNetSuccesses is null || net <= b.MaxNetSuccesses));
}
