using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The book's worked examples, each replayed against whatever play rules it is handed and each
/// returning a <b>verdict</b> rather than asserting.
///
/// <para><b>Verdicts rather than assertions, because these are driven twice.</b>
/// <see cref="PlayEngineTests"/> runs them against the shipped data and requires <c>PASS</c>;
/// <see cref="PlayEngineTwinTests"/> runs the <em>byte-identical</em> methods against a deliberately
/// broken copy of the data and requires the one it aimed at to say <c>FAIL</c>. A twin driven by a
/// doctored harness proves nothing, so there is exactly one copy of each example and only what it is
/// handed differs.</para>
///
/// <para><b>A verdict is always produced, and "not PASS" is never good enough.</b> Each method
/// catches, so an example that threw returns a <c>FAIL</c> saying what threw rather than leaving the
/// verdict absent — the failure three of this repository's four historical guard faults were was a
/// check that never ran being read as a check that passed.</para>
///
/// <para><b>Every example carries a positive control before its outcome.</b> The book prints
/// successes and <see cref="IDiceSource"/> returns faces, so each scripts faces chosen to produce the
/// printed counts and then requires the engine to have counted exactly those. The longer ones also
/// require <see cref="ScriptedDice.Remaining"/> to be zero, which catches an engine making more rolls
/// than the page as well as one making fewer.</para>
/// </summary>
internal static class PlayWorkedExamples
{
    /// <summary>The verdict a healthy run gives.</summary>
    public const string Pass = "PASS";

    /// <summary>Every example, by the name a report prints, in the order the book prints them.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "p.67 arm wrestling",
        "p.74 movement",
        "p.74 chase",
        "p.76 special effect",
        "p.76 breaking free",
        "p.79 fatal damage",
        "p.81 example of combat",
        "p.85 adversity"
    ];

    /// <summary>Runs one example by name against the rules it is handed.</summary>
    public static string Run(string name, PlayRulesRepository play) => name switch
    {
        "p.67 arm wrestling" => Verdict(() => ArmWrestling(play)),
        "p.74 movement" => Verdict(() => MovementExample(play)),
        "p.74 chase" => Verdict(() => ChaseExample(play)),
        "p.76 special effect" => Verdict(() => SpecialEffect(play)),
        "p.76 breaking free" => Verdict(() => BreakingFree(play)),
        "p.79 fatal damage" => Verdict(() => FatalDamage(play)),
        "p.81 example of combat" => Verdict(() => ExampleOfCombat(play)),
        "p.85 adversity" => Verdict(() => Adversity(play)),
        _ => $"FAIL — no worked example called '{name}'"
    };

    /// <summary>
    /// <b>p.67's arm-wrestling exhibition, through the engine's own dice path.</b> Citizen Soldier
    /// declines to roll his 12d and banks 6; Gatecrasher throws <c>1,2,2,2,3,3,3,5,5,5,6,6</c> for 7;
    /// one net success puts him in the Actor-with-Embellishment band.
    ///
    /// <para>Distinct from <c>PlayRulesDataTests</c>'s fixture on the same example, which counts the
    /// dice with a test helper of its own. This one goes through <see cref="SuccessCounter"/> and
    /// <see cref="ScriptedDice"/> — the code an encounter actually uses — so an engine that read the
    /// map wrongly fails here where the data test stays green.</para>
    /// </summary>
    private static void ArmWrestling(PlayRulesRepository play)
    {
        int[] printedFaces = [1, 2, 2, 2, 3, 3, 3, 5, 5, 5, 6, 6];

        // The fixture's own control: the pool really is the twelve dice the page rolls. One quietly
        // lost would still produce a number.
        Require(printedFaces.Length == 12, $"the printed roll is {printedFaces.Length} dice, not 12");

        var dice = new ScriptedDice(printedFaces);
        var counter = new SuccessCounter(play);

        var banked = counter.AutomaticSuccesses(12);
        var rolled = counter.Roll(printedFaces.Length, dice);

        Require(banked == 6, $"Citizen Soldier banked {banked}, not the printed 6");
        Require(rolled.Successes == 7, $"Gatecrasher rolled {rolled.Successes}, not the printed 7");
        Require(dice.Remaining == 0, $"{dice.Remaining} scripted faces went unused");

        var net = rolled.Successes - banked;
        Require(net == 1, $"{net} net successes, not the printed 1");

        var band = Band(play, net);
        Require(string.Equals(band.Outcome, "actor", StringComparison.Ordinal), $"the band is {band.Outcome}");
        Require(band.Embellishment == true, "the band should carry an embellishment");
    }

    /// <summary>
    /// <b>p.74's movement example.</b> Powermad is on foot and "it'll take him 2 pages to run up to
    /// the thing"; Flicker has 9d Running and "can move to within Close Range … in 1 page".
    ///
    /// <para>The rank that halves the cost is the entry's: the control requires Flicker's 9d to
    /// actually clear it, so a raised threshold fails here rather than quietly making both answers 2.
    /// </para>
    /// </summary>
    private static void MovementExample(PlayRulesRepository play)
    {
        const int flickerRunning = 9;
        var required = play.GetCombat("movement").Movement!.TravelPowerRankRequired;

        Require(flickerRunning >= required,
            $"Flicker's {flickerRunning}d Running no longer clears the {required}d the entry requires");

        var onFoot = Movement.PagesToCrossARangeClass(play, travelPowerRank: 0);
        var flying = Movement.PagesToCrossARangeClass(play, flickerRunning);

        Require(onFoot == 2, $"Powermad takes {onFoot} pages, not the printed 2");
        Require(flying == 1, $"Flicker takes {flying} pages, not the printed 1");
    }

    /// <summary>
    /// <b>p.74's chase, one exchange at a time.</b> Flicker starts at Distant Range and wins three
    /// exchanges with 3, 1 and 4 net successes: the first closes a class and lends her dice, the
    /// second lends dice and nothing else, and the third ends the pursuit.
    /// </summary>
    private static void ChaseExample(PlayRulesRepository play)
    {
        var first = Movement.Exchange(play, net: 3, RangeBand.Distant);
        Require(first.ClosedARangeClass, "3 net successes should close a range class");
        Require(first.BonusDiceNextExchange == 2, $"winning lends {first.BonusDiceNextExchange}d, not 2d");
        Require(!first.Ended, "the chase should not end on the first exchange");

        var second = Movement.Exchange(play, net: 1, RangeBand.Close);
        Require(!second.ClosedARangeClass, "1 net success should not close the distance any further");
        Require(second.BonusDiceNextExchange == 2, "the exchange is still won, so the dice are still lent");
        Require(!second.Ended, "the chase should not end on the second exchange");

        var third = Movement.Exchange(play, net: 4, RangeBand.Close);
        Require(third.Ended, "4 net successes from Close Range should end the chase");
    }

    /// <summary>
    /// <b>p.76's Mind Control.</b> Heartbreaker rolls 8 against Parthian's 3 — five net successes —
    /// and "gains control of Parthian's mind for 3 pages", which only half-rounding-<em>up</em>
    /// produces. This is the example the broken twin is aimed at.
    /// </summary>
    private static void SpecialEffect(PlayRulesRepository play)
    {
        var (_, state, dice) = MindControlOnParthian(play);

        Require(dice.Remaining == 0, $"{dice.Remaining} scripted faces went unused");
        Require(state.LastAttack!.AttackSuccesses == 8, $"Heartbreaker rolled {state.LastAttack.AttackSuccesses}, not 8");
        Require(state.LastAttack.DefenceSuccesses == 3, $"Parthian rolled {state.LastAttack.DefenceSuccesses}, not 3");

        Require(state.Effects.Count == 1, $"{state.Effects.Count} effects are running, not 1");

        var effect = state.Effects[0];
        Require(string.Equals(effect.Name, "Mind Control", StringComparison.Ordinal), $"the effect is {effect.Name}");
        Require(effect.RemainingPages == 3, $"the Mind Control lasts {effect.RemainingPages} pages, not the printed 3");

        Require(
            state.Ledger.Lines.Any(l =>
                string.Equals(l.Rule, "special_effects", StringComparison.Ordinal)
                && l.SourceRef.Contains("p.76", StringComparison.Ordinal)),
            "the ledger does not cite p.76 for the effect it applied");
    }

    /// <summary>
    /// <b>p.76's escape from that Mind Control.</b> Parthian rolls 7 against Heartbreaker's 4 — three
    /// net successes — and "this reduces the Mind Control duration by 2 pages", leaving one of the
    /// three, so he is loose after Heartbreaker's next turn rather than at once.
    /// </summary>
    private static void BreakingFree(PlayRulesRepository play)
    {
        var (encounter, state, _) = MindControlOnParthian(play);

        state = encounter.Step(state, new EndTurn("heartbreaker")).State;
        Require(string.Equals(state.Current?.Id, "parthian", StringComparison.Ordinal),
            $"it is {state.Current?.Id}'s turn, not Parthian's");

        // Parthian rolls 7 successes on the passive defence the Power names, against the Power's own
        // rank of 4 — the two figures the page prints for this roll.
        var escape = new Encounter(play, new ScriptedDice(Faces(pool: 8, successes: 7)));
        state = escape.Step(state, new BreakFree("parthian", "willpower", Threshold: 4)).State;

        Require(state.Effects.Count == 1, $"{state.Effects.Count} effects are running, not 1");
        Require(state.Effects[0].RemainingPages == 1,
            $"{state.Effects[0].RemainingPages} pages are left, not the printed 1");

        Require(
            state.Ledger.Lines.Any(l =>
                string.Equals(l.Rule, "breaking_free", StringComparison.Ordinal)
                && l.Text.Contains("removes 2 of the 3 pages", StringComparison.Ordinal)),
            "the ledger does not say two of the three pages were removed");
    }

    /// <summary>
    /// <b>p.79's Clint Castle, with the Fatal Damage rule on.</b> Five Health, down to one, and a
    /// ninja master stabs him for six: "taking him down to −5 Health. Clint's full Health is 5, so
    /// that's just enough to kill him. Our hero spends 1 Resolve to prevent that from happening,
    /// leaving him at −4 Health."
    ///
    /// <para><b>−4 is one point <em>above</em> the threshold, and p.79's own words say below.</b> The
    /// entry carries the printed word as a fact and the arithmetic the example supports as an
    /// <c>interpretation</c>; the engine follows the interpretation and its ledger line says so, so a
    /// reader of a run can see a choice was made rather than assumed. That line is required here,
    /// because a reading applied silently is a reading nobody can argue with.</para>
    /// </summary>
    private static void FatalDamage(PlayRulesRepository play)
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

        // Six damage means six net successes: the ninja scores 8 against Clint's 2. A Toughness of 4
        // answers a lethal attack at half, which is 2d.
        var dice = new ScriptedDice([.. Faces(12, 8), .. Faces(2, 2)]);
        var encounter = new Encounter(play, dice, table);

        var state = encounter.Begin([ninja, clint]);
        Require(string.Equals(state.Current?.Id, "ninja", StringComparison.Ordinal), "the ninja should act first");

        state = encounter.Step(state, new Attack("ninja", "clint", "might")).State;

        Require(state.LastAttack!.AttackSuccesses == 8, $"the ninja rolled {state.LastAttack.AttackSuccesses}, not 8");
        Require(state.LastAttack.DefenceSuccesses == 2, $"Clint rolled {state.LastAttack.DefenceSuccesses}, not 2");
        Require(dice.Remaining == 0, $"{dice.Remaining} scripted faces went unused");

        Require(state["clint"].CurrentHealth == -5, $"Clint is on {state["clint"].CurrentHealth}, not the printed −5");
        Require(state["clint"].CurrentHealth == -state["clint"].FullHealth, "−5 should be exactly the fatal threshold");

        state = encounter.Step(state, new SpendResolve("clint", ResolveSpend.AvoidFatalDamage)).State;

        Require(state["clint"].CurrentHealth == -4, $"Clint is left on {state["clint"].CurrentHealth}, not the printed −4");
        Require(state["clint"].Resolve == 2, $"Clint has {state["clint"].Resolve} Resolve left, not 2");

        var gritty = play.GetGritty("gritty_fatal_damage");

        var line = state.Ledger.Lines.LastOrDefault(l =>
            string.Equals(l.Rule, "gritty_fatal_damage", StringComparison.Ordinal)
            && l.Text.Contains("spends 1 Resolve", StringComparison.Ordinal));

        Require(line is not null, "the ledger does not record the Resolve spent against the fatal blow");

        // Both halves are read off the entry rather than typed here, so a correction to either the
        // fact field or the interpretation moves this with it.
        Require(line!.Text.Contains(gritty.FatalDamage!.ResolveReducesDamageTo, StringComparison.Ordinal),
            "the ledger line does not quote the printed word it is departing from");
        Require(!string.Equals(gritty.FatalDamage.ResolveReducesDamageTo,
                    gritty.Interpretation!.ResolveReducesDamageTo, StringComparison.Ordinal),
            "the printed word and the interpretation agree, so there is no contradiction to record");
        Require(line.Text.Contains("the example is what this follows", StringComparison.Ordinal),
            "the ledger line does not say which of the two readings it followed");
    }

    /// <summary>
    /// <b>p.81's Example of Combat, stepped through from the top.</b> Every roll the page prints, in
    /// the order it prints them, with faces chosen to produce the printed success counts.
    ///
    /// <para>The strongest example here and the only one that exercises the engine end to end: the
    /// order of action, an attack and its damage, a Minion group wiped out, an attack that does
    /// nothing, a grapple that becomes a full hold, a failed escape, and a hit worth one net success
    /// whose narrative control belongs to the actor with an embellishment for the GM.</para>
    ///
    /// <para>Two figures the page never prints are chosen here and required by nothing: the mecha's
    /// Health, and the three characters' unremarkable secondary Traits. Nothing below turns on
    /// either — the mecha is never in danger of going down, and every defence the page names is the
    /// largest that character has by a wide margin.</para>
    /// </summary>
    private static void ExampleOfCombat(PlayRulesRepository play)
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

        var encounter = new Encounter(play, dice);
        var state = encounter.Begin([soldier, mecha, gatecrasher, robots]);

        // "the mecha has an Edge of 13, Citizen Soldier has an Edge of 12, and Gatecrasher has an
        // Edge of 9 … the Minions … act after everyone else"
        Require(state.TurnOrder.SequenceEqual(["mecha", "soldier", "gatecrasher", "robots"]),
            "the order of action is " + string.Join(", ", state.TurnOrder));

        // "It rolls its 13d Might and gets 8 successes. Gate uses his 12d Armor … rolls 6 successes.
        // With a total of 2 net successes … 2 points of damage."
        state = encounter.Step(state, new Attack("mecha", "gatecrasher", "might")).State;
        RequireRoll(state, 8, 6, "the stomp");
        Require(state["gatecrasher"].CurrentHealth == 8,
            $"Gatecrasher is on {state["gatecrasher"].CurrentHealth} of 10 Health, so the damage was not 2");
        state = encounter.Step(state, new EndTurn("mecha")).State;

        // "The Soldier rolls 8 successes … the robots roll 3 … With 5 net successes, our Hero could
        // have defeated up to five of these robotic rogues … turns these four into scrap metal."
        state = encounter.Step(state, new Attack("soldier", "robots", "might")).State;
        RequireRoll(state, 8, 3, "the leap at the Minions");
        Require(state["robots"].GroupSize == 0, $"{state["robots"].GroupSize} robots are still up");
        Require(
            state.Ledger.Lines.Any(l =>
                string.Equals(l.Rule, "attacking_minions", StringComparison.Ordinal)
                && l.Text.Contains("could defeat 5 Minions", StringComparison.Ordinal)
                && l.Text.Contains("4 defeated", StringComparison.Ordinal)),
            "the ledger does not record five that could have been defeated and four that were");
        state = encounter.Step(state, new EndTurn("soldier")).State;

        // "Using his own 12d Might, he rolls 9 successes. However, the mecha also gets 9 … no effect."
        state = encounter.Step(state, new Attack("gatecrasher", "mecha", "might")).State;
        RequireRoll(state, 9, 9, "Gatecrasher's charge");
        Require(state["mecha"].CurrentHealth == 30, "the mecha took damage from an attack with no effect");
        state = encounter.Step(state, new EndTurn("gatecrasher")).State;

        state = encounter.Step(state, new EndTurn("robots")).State;
        state = encounter.Step(state, new EndPage("")).State;
        Require(state.Page == 2, $"the fight is on page {state.Page}, not 2");

        // "Using its 13d Might, the mecha rolls 7 successes. The Soldier … only manages to score 4 …
        // With 3 net successes, the mecha places our Hero in a full hold."
        state = encounter.Step(state, new GrappleIntent("mecha", "soldier", GrappleMove.Hold)).State;
        Require(state.Grapples.Count == 1, $"{state.Grapples.Count} grapples are in progress, not 1");
        Require(state.Grapples[0].Kind == GrappleKind.Full, $"the mecha has a {state.Grapples[0].Kind} hold");
        Require(string.Equals(state.Grapples[0].Held, "soldier", StringComparison.Ordinal),
            "the wrong character is held");
        state = encounter.Step(state, new EndTurn("mecha")).State;

        // "He makes a Might roll and gets 6 successes, but the mecha … also gets 6. With no net
        // successes, the Soldier remains trapped."
        state = encounter.Step(state, new GrappleIntent("soldier", "mecha", GrappleMove.Escape)).State;
        Require(state.Grapples.Count == 1 && state.Grapples[0].Kind == GrappleKind.Full,
            "the Soldier got out of a hold he should still be in");
        state = encounter.Step(state, new EndTurn("soldier")).State;

        // "fires his eyebeams (a 15d Blast) … 8 successes. The mecha uses its 15d Armor … and gets 7.
        // One net success … is enough for narrative control … the GM gets an embellishment."
        state = encounter.Step(state, new Attack("gatecrasher", "mecha", "blast")).State;
        RequireRoll(state, 8, 7, "the eyebeams");
        Require(state["mecha"].CurrentHealth == 29, $"the mecha is on {state["mecha"].CurrentHealth}, so the damage was not 1");

        var narration = state.Ledger.Lines.Last(l =>
            string.Equals(l.Rule, "narrative_control", StringComparison.Ordinal));

        Require(narration.Text.Contains("to the actor", StringComparison.Ordinal),
            "narrative control did not go to the actor");
        Require(narration.Text.Contains("embellishment", StringComparison.Ordinal),
            "the other side did not get an embellishment");

        // The control that catches an engine making a roll the page does not, or skipping one it does.
        Require(dice.Remaining == 0, $"{dice.Remaining} scripted faces went unused");
    }

    /// <summary>
    /// <b>p.85's Adversity example.</b> "If you have four Heroes in your game and you designate a
    /// scene as Challenge Level 2, you would gain 8 points of Adversity."
    ///
    /// <para>The award is <em>derived</em> rather than read off a field, because the pool a fight
    /// opens with is the sum of two rules: one point per Hero per issue, plus the scene's award. The
    /// same party with no Challenge Level opens on four and with Challenge Level 2 opens on twelve,
    /// so the scene is worth eight. Dropping the party size from the entry's own factor list gives 2
    /// and turning the product into a sum gives 6; only the transcription the book prints gives 8.
    /// </para>
    /// </summary>
    private static void Adversity(PlayRulesRepository play)
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

        var encounter = new Encounter(play, new SeededDice(1));

        var plain = encounter.Begin([.. party, villain]);
        var levelled = encounter.Begin([.. party, villain], challengeLevel: 2);

        Require(plain.Adversity == 4, $"a party of four opens the GM on {plain.Adversity}, not 4");
        Require(levelled.Adversity - plain.Adversity == 8,
            $"Challenge Level 2 is worth {levelled.Adversity - plain.Adversity}, not the printed 8");

        Require(
            levelled.Ledger.Lines.Any(l =>
                string.Equals(l.Rule, "adversity_earn_challenge_level", StringComparison.Ordinal)
                && l.Text.Contains("awards 8 Adversity", StringComparison.Ordinal)),
            "the ledger does not record the award");
    }

    // ── Shared ───────────────────────────────────────────────────────────────

    /// <summary>
    /// p.76's Mind Control, set up and thrown: Heartbreaker's 8 successes against Parthian's 3.
    /// Shared by the effect example and the break-free example, which are two halves of one printed
    /// exchange — and which is why the twin aimed at the first turns the second red too.
    /// </summary>
    private static (Encounter Encounter, EncounterState State, ScriptedDice Dice) MindControlOnParthian(
        PlayRulesRepository play)
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
        var encounter = new Encounter(play, dice);

        var state = encounter.Begin([heartbreaker, parthian]);
        Require(string.Equals(state.Current?.Id, "heartbreaker", StringComparison.Ordinal),
            "Heartbreaker should act first");

        state = encounter
            .Step(state, new Attack(
                "heartbreaker", "parthian", "mind_control", DamageKind.Psychic, Effect: "Mind Control"))
            .State;

        return (encounter, state, dice);
    }

    /// <summary>
    /// Faces for a pool of <paramref name="pool"/> dice worth exactly <paramref name="successes"/>
    /// under the printed map — sixes first, one four if an odd success is left, then ones.
    ///
    /// <para><b>The book prints successes and the dice source returns faces</b>, so an example has to
    /// bridge the two. It throws rather than approximating: a pool too small for the successes asked
    /// of it is a fixture that would otherwise assert a number the dice cannot produce.</para>
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

    private static void RequireRoll(EncounterState state, int attack, int defence, string what)
    {
        Require(state.LastAttack is not null, $"{what} produced no roll at all");
        Require(state.LastAttack!.AttackSuccesses == attack,
            $"{what}: the attacker rolled {state.LastAttack.AttackSuccesses}, not the printed {attack}");
        Require(state.LastAttack.DefenceSuccesses == defence,
            $"{what}: the defender rolled {state.LastAttack.DefenceSuccesses}, not the printed {defence}");
    }

    private static Play.Rules.Models.ChallengeBandModel Band(PlayRulesRepository play, int net) =>
        play.GetChallenge("narrative_control").Bands!.Single(b =>
            (b.MinNetSuccesses is null || net >= b.MinNetSuccesses)
            && (b.MaxNetSuccesses is null || net <= b.MaxNetSuccesses));

    private static void Require(bool condition, string why)
    {
        if (!condition) throw new WorkedExampleFailure(why);
    }

    /// <summary>
    /// Runs one example and turns whatever happened into a verdict.
    ///
    /// <para>An example that threw for a reason of its own — a missing entry, a bad cast — still
    /// returns a <c>FAIL</c> naming what threw, because a missing verdict reads exactly like a
    /// working negative control.</para>
    /// </summary>
    private static string Verdict(Action example)
    {
        try
        {
            example();
            return Pass;
        }
        catch (WorkedExampleFailure failure)
        {
            return $"FAIL — {failure.Message}";
        }
#pragma warning disable CA1031 // A verdict harness has to catch everything: see the summary above.
        catch (Exception ex)
        {
            return $"FAIL — {ex.GetType().Name}: {ex.Message}";
        }
#pragma warning restore CA1031
    }

    /// <summary>A worked example not coming out as printed.</summary>
    private sealed class WorkedExampleFailure(string message) : Exception(message);
}
