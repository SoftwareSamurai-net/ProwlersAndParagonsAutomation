using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>What each play style claims about itself, driven over seeded fights rather than read off its
/// doc comment.</b>
///
/// <para>A style is a guess about how people play and every report quotes its name beside a rate —
/// so the one thing that must not be true of one is that its name says something its behaviour does
/// not. "Mano a mano spends nothing" is a claim about a hundred fights, not about a
/// <c>return null</c>, and it is checked as one: the ledger of every run is scanned for a purchase,
/// and the same fight on the same seeds under <see cref="Standard"/> has to produce them — otherwise
/// "no purchases found" is also what a broken scan looks like.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlayStyleTests
{
    private readonly PlayRulesRepository _play;

    public PlayStyleTests(PlayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _play = fixture.Play;
    }

    private const int MaxPages = 20;

    /// <summary>The hundred seeds the ledger-scanning properties below are driven over.</summary>
    private static IEnumerable<int> Hundred => Enumerable.Range(9_000, 100);

    // ── mano a mano ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>Not one point of Resolve or Adversity leaves a pool in a hundred fights under
    /// <c>mano_a_mano</c>, and the same hundred fights under <c>standard</c> spend.</b>
    ///
    /// <para><b>The second half is the whole test.</b> "The ledger contains no purchase" is
    /// satisfied perfectly by a scan that has stopped finding purchases, which is this repository's
    /// commonest historical guard fault — a feature that did not run mistaken for a feature that
    /// worked. So the control is the same fight, the same seeds and the same combatants under a
    /// style that does spend: if <see cref="LedgerReading.IsAPurchase"/> ever stops reading
    /// <c>Encounter.Step</c>'s own sentence, this fails on the control rather than passing on the
    /// claim.</para>
    ///
    /// <para>The pools are compared as well, which is the same claim by another route: a purchase
    /// this engine paid for and did not write down would move a pool without a line.</para>
    /// </summary>
    [Fact]
    public void ManoAManoNeverSpendsAndStandardOnTheSameSeedsDoes()
    {
        var quiet = 0;
        var spent = 0;

        foreach (var seed in Hundred)
        {
            var still = RunOne(new ManoAMano(_play), OutEdged(), seed);

            Assert.Empty(LedgerReading.Purchases(still));

            Assert.Equal(
                OutEdged().Sum(c => c.Resolve),
                still.Combatants.Values.Sum(c => c.Resolve));

            Assert.Equal(Opening(OutEdged()), still.Adversity);

            quiet++;

            var busy = RunOne(new Standard(_play), OutEdged(), seed);
            spent += LedgerReading.Purchases(busy).Count;
        }

        Assert.Equal(100, quiet);

        Assert.True(spent > 0,
            "the control failed: a hundred fights under `standard`, on the same seeds and the same "
            + "sheets, bought nothing either — so `mano_a_mano` buying nothing is not evidence "
            + "about `mano_a_mano`. Either the style has stopped spending or LedgerReading has "
            + "stopped reading Encounter.Step's own purchase sentence.");
    }

    // ── reckless ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every exchange <c>reckless</c> makes is all-out, and <c>standard</c> on the same seeds
    /// makes none.</b>
    ///
    /// <para>p.78's line is written once per attack that goes all-out, citing
    /// <c>going_all_out</c>, so the count of those lines against the count of exchanges is the
    /// claim exactly. The control is the other style's zero, which is what tells an always-true
    /// count from a real one.</para>
    /// </summary>
    [Fact]
    public void RecklessGoesAllOutOnEveryExchangeAndStandardOnNone()
    {
        var reckless = 0;
        var exchanges = 0;
        var careful = 0;

        foreach (var seed in Hundred.Take(30))
        {
            var wild = Observe(new Reckless(_play), OutEdged(), seed);

            reckless += AllOutLines(wild.State);
            exchanges += wild.Observed.Attacks.Count;

            careful += AllOutLines(RunOne(new Standard(_play), OutEdged(), seed));
        }

        Assert.True(exchanges > 0, "thirty reckless fights produced no exchange at all to be all-out.");
        Assert.Equal(exchanges, reckless);
        Assert.Equal(0, careful);
    }

    // ── standard ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b><c>standard</c> seizes the initiative when it is out-Edged, and not otherwise.</b>
    ///
    /// <para>Two fights, identical but for the Edges: in the first the Villain is quicker than
    /// everybody, in the second every combatant is on the same figure. p.73's purchase writes a
    /// line citing <c>seize_initiative</c>, so the first has them and the second has none — and the
    /// second is the control, because a style that seized unconditionally would pass the first half
    /// on its own.</para>
    /// </summary>
    [Fact]
    public void StandardSeizesWhenOutEdgedAndNotWhenTheEdgesAreLevel()
    {
        var seized = 0;
        var level = 0;

        foreach (var seed in Hundred.Take(30))
        {
            seized += SeizeLines(RunOne(new Standard(_play), OutEdged(), seed));
            level += SeizeLines(RunOne(new Standard(_play), LevelEdges(), seed));
        }

        Assert.True(seized > 0,
            "thirty fights in which the Villain out-Edges every Hero produced no seized initiative.");

        Assert.Equal(0, level);
    }

    /// <summary>
    /// <b>And <c>standard</c>'s other rule fires too: a point spent against it on the previous page
    /// is answered with a reroll.</b>
    ///
    /// <para><b>This is a second fixture because the two rules cost the same currency and the counts
    /// alone cannot tell them apart.</b> The seize is what moves first — nobody has spent anything
    /// on page one — so a report showing Resolve leaving a pool is consistent with the response rule
    /// never having fired at all, which would make it a branch nothing reaches. Counted by its own
    /// entry, <c>spend_reroll_challenge_roll</c>, with the level-Edged fight as the control: there
    /// nobody is out-Edged, so nobody moves first, so nothing is ever answered.</para>
    /// </summary>
    [Fact]
    public void StandardAnswersAPointSpentAgainstItWithOneOfItsOwn()
    {
        var answered = 0;
        var unprovoked = 0;

        foreach (var seed in Hundred)
        {
            answered += DiceOrReroll(RunOne(new Standard(_play), OutEdged(), seed));
            unprovoked += DiceOrReroll(RunOne(new Standard(_play), LevelEdges(), seed));
        }

        Assert.True(answered > 0,
            "a hundred fights in which one side seizes on page one produced no reroll in answer, so "
            + "standard's second rule is a branch nothing reaches.");

        Assert.Equal(0, unprovoked);
    }

    /// <summary>
    /// <b>The spend <c>standard</c> answers is the <em>other</em> side's, and that is asserted on
    /// the side rather than on the count.</b>
    ///
    /// <para><b>Nothing was checking the direction.</b> Dropping the negation in
    /// <see cref="LedgerReading.OtherSideBoughtOn"/> — so the policy answers a point <em>its own
    /// side</em> spent on the previous page, which is the opposite of the rule it publishes — left
    /// all 170 of <c>PlayStyleTests</c> and <c>McpPlayServerTests</c> green. It would: in the
    /// out-Edged fight the Heroes seize on page one, so a policy answering its own side and a
    /// policy answering the other one both produce rerolls, and only the count was being read.
    /// </para>
    ///
    /// <para>Asked of the reading directly, on a state with exactly one purchase in it: a Hero has
    /// bought, so the Villain's side has been spent against and the Heroes' side has not.</para>
    /// </summary>
    [Fact]
    public void ASpendIsOnlyAnAnswerWhenTheOtherSideMadeIt()
    {
        var encounter = new Encounter(_play, new SeededDice(5));
        var state = encounter.Begin(OutEdged());

        state = encounter.Step(state, new SpendResolve("cho", ResolveSpend.SeizeInitiative)).State;

        // The control: exactly one purchase, it is the Hero's, and it is on page one.
        var purchase = Assert.Single(LedgerReading.Purchases(state));
        Assert.Equal("cho", purchase.Actor);
        Assert.Equal(1, purchase.Page);
        Assert.Equal(Combatant.HeroSide, state["cho"].Side);

        // A Villain has been spent against; a Hero has not, however much their own side spent.
        Assert.True(LedgerReading.OtherSideBoughtOn(state, state["schism"].Side, 1));
        Assert.False(LedgerReading.OtherSideBoughtOn(state, Combatant.HeroSide, 1));

        // And it is a fact about the page as well as about the side: nothing was bought on page two.
        Assert.False(LedgerReading.OtherSideBoughtOn(state, state["schism"].Side, 2));
    }

    /// <summary>
    /// <b>Both readings throw when the sentence they are anchored on moves, and the throw is driven
    /// rather than promised.</b>
    ///
    /// <para><see cref="LedgerReading"/>'s own doc comment and the guide both say a parse that
    /// quietly found nothing "would report a fight in which no defence was ever rolled and nobody
    /// ever hit anybody" — a plausible-looking answer with nothing behind it. Nothing was making
    /// either throw happen, so the promise was the only evidence for it. Each is driven with a line
    /// this engine could write if <c>Encounter.Step</c>'s sentence were rewritten, and each has a
    /// real line of the same fight beside it as the control that the reading works at all.</para>
    /// </summary>
    [Fact]
    public void EachReadingThrowsWhenTheSentenceItIsAnchoredOnMoves()
    {
        var run = Observe(new Standard(_play), OutEdged(), seed: 606);

        // The controls: on a real fight, both readings read.
        var real = run.State.Ledger.Lines.First(LedgerReading.IsAnAttack);
        Assert.False(string.IsNullOrWhiteSpace(LedgerReading.DefenceTraitIn(real)));
        Assert.NotEmpty(LedgerReading.DamageDealtSoFar(run.State.Ledger));

        // The defending half gone altogether.
        var halved = real with { Text = "Cho attacks Schism with might 8d for 3 successes" };
        var noHalf = Assert.Throws<InvalidOperationException>(() => LedgerReading.DefenceTraitIn(halved));
        Assert.Contains("defends with", noHalf.Message, StringComparison.Ordinal);

        // The phrase there and the Trait and pool no longer two words.
        var jammed = real with { Text = "Cho attacks Schism with might 8d for 3; Schism defends with toughnessd for 2" };
        var noPool = Assert.Throws<InvalidOperationException>(() => LedgerReading.DefenceTraitIn(jammed));
        Assert.Contains("Trait and a pool", noPool.Message, StringComparison.Ordinal);

        // And a damage line whose figure has stopped being a figure.
        var vague = new Ledger([
            new LedgerLine(1, "schism", LedgerReading.DamageRule, "p.75",
                "3 net successes is a lot of damage; Cho is on 2 Health")
        ]);

        var unreadable = Assert.Throws<InvalidOperationException>(() => LedgerReading.DamageDealtSoFar(vague));
        Assert.Contains("a lot of", unreadable.Message, StringComparison.Ordinal);
    }

    private static int DiceOrReroll(EncounterState state) =>
        state.Ledger.Lines.Count(l =>
            string.Equals(l.Rule, "spend_reroll_challenge_roll", StringComparison.Ordinal)
            && LedgerReading.IsAPurchase(l, state[l.Actor].Name));

    // ── min-max ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b><c>min_max</c> spends on seeds where <c>standard</c> spends nothing at all.</b>
    ///
    /// <para>The fight is the level-Edged one, where <c>standard</c> has no trigger: nobody is
    /// out-Edged, so nobody moves first, so nobody ever answers a spend. <c>min_max</c> does not
    /// wait to be provoked — it prices dice against a shortfall it can compute — so the two come
    /// apart here and the seeds on which they do are named in the failure.</para>
    /// </summary>
    [Fact]
    public void MinMaxSpendsOnSeedsWhereStandardDoesNot()
    {
        var apart = new List<int>();
        var dice = 0;

        foreach (var seed in Hundred.Take(30))
        {
            var careful = LedgerReading.Purchases(RunOne(new Standard(_play), LevelEdges(), seed)).Count;
            var greedy = LedgerReading.Purchases(RunOne(new MinMax(_play), LevelEdges(), seed)).Count;

            if (careful == 0 && greedy > 0) apart.Add(seed);

            dice += DiceBought(RunOne(new MinMax(_play), LevelEdges(), seed));
        }

        Assert.True(apart.Count > 0,
            "on none of thirty level-Edged seeds did min_max buy anything standard did not — so "
            + "the two styles are measuring the same thing and the report's style name says "
            + "nothing.");

        // <b>And the rule that came apart is named, not merely the difference.</b> Two of this
        // style's five rules can spend and only one of them is the interesting one: a seized
        // initiative would satisfy the count above on its own, so the dice it prices against a
        // shortfall are counted separately. Without this, deleting the pricing rule leaves the
        // fixture green.
        Assert.True(dice > 0,
            "min_max bought no dice at all over thirty seeds, so the rule that prices p.84's "
            + "purchase against the shortfall is not being reached.");
    }

    /// <summary>
    /// <b>Checking Your Swing makes <c>min_max</c>'s dice dearer, because the price is read off the
    /// success map rather than typed.</b>
    ///
    /// <para><b>The claim is the whole of what reading the map buys, and nothing was driving it.</b>
    /// p.84's purchase buys dice and this policy has to work out how many cover a shortfall on
    /// average — so under Ch.3 p.69's flatter map, where a six is worth one success instead of two,
    /// the same shortfall costs more points. It cannot be measured through a run: the two settings
    /// are two different fights, because the flattened six changes every roll on both sides.</para>
    ///
    /// <para>The control is the map itself, read out of the store: the two counters really do value
    /// a six differently, and agree about every other face. Without that, "the price moved" could be
    /// a price that moves for any reason at all.</para>
    /// </summary>
    [Fact]
    public void CheckingYourSwingMakesTheDiceMinMaxPricesDearer()
    {
        var book = new SuccessCounter(_play);
        var checking = new SuccessCounter(_play, checkingYourSwing: true);

        // The control, off the store: one face is worth less and the rest are unchanged.
        Assert.True(checking.Value(book.HighestFace) < book.Value(book.HighestFace),
            "Checking Your Swing no longer flattens the top face, so there is no map here for the "
            + "pricing to have read.");

        for (var face = 1; face < book.HighestFace; face++)
            Assert.Equal(book.Value(face), checking.Value(face));

        var policy = new MinMax(_play);

        // The same shortfall, priced under each map. Every shortfall this policy can see is dearer
        // or the same; at least one of them is strictly dearer, which is what "the map is read"
        // means — a policy with a figure typed into it would answer alike for all of them.
        var dearer = 0;

        for (var shortfall = 1; shortfall <= 6; shortfall++)
        {
            var underTheBook = policy.PointsToCover(shortfall, checkingYourSwing: false);
            var underChecking = policy.PointsToCover(shortfall, checkingYourSwing: true);

            Assert.True(underChecking >= underTheBook,
                $"a shortfall of {shortfall} costs {underChecking} points under the flatter map and "
                + $"{underTheBook} under the book's, which is the wrong way round.");

            if (underChecking > underTheBook) dearer++;
        }

        Assert.True(dearer > 0,
            "no shortfall between one and six cost more under Checking Your Swing, so the price is "
            + "not being read off the success map at all.");
    }

    /// <summary>
    /// <b><c>min_max</c> goes all-out only where the worst one opponent could do still leaves it
    /// standing, and never where it does not.</b>
    ///
    /// <para><b>Nothing was holding the guard at all.</b> Replacing
    /// <c>TheCounterattackCannotReachThem</c> with a bare <c>true</c> — so this style goes all-out
    /// on every page, which is what <see cref="Reckless"/> is for and is the difference between the
    /// two — left every style fixture and every wire fixture green. The rule is one of the five
    /// this style's own doc comment publishes, and a rate measured under it is a rate about those
    /// five.</para>
    ///
    /// <para><b>The two fights differ only in Health, and the figure that separates them is read
    /// out of the store.</b> The guard is the greatest attack rank on the other side at
    /// <c>damage.damage_per_net_success</c>, against <c>damage.defeated_at_health</c> — so the
    /// fixture computes both sides' worst case, asserts that one fight is over the line and the
    /// other under it, and only then counts p.78's lines. A frail fight produces none at all,
    /// because Health only falls.</para>
    /// </summary>
    [Fact]
    public void MinMaxGoesAllOutOnlyWhereTheCounterattackCouldNotPutItDown()
    {
        var damage = _play.GetCombat("damage").Damage!;

        // The controls on the arithmetic, off the store: what the worst blow is worth, and where a
        // character is out of the fight.
        var worstOnTheHero = 9 * damage.DamagePerNetSuccess;   // the Villain's Might
        var worstOnTheVillain = 8 * damage.DamagePerNetSuccess; // the Hero's

        Assert.True(SturdyHealth - worstOnTheHero > damage.DefeatedAtHealth,
            "the sturdy fight no longer leaves the Hero standing under the worst blow, so it is not "
            + "the side of the line this fixture needs it on.");

        Assert.True(FragileHealth - worstOnTheHero <= damage.DefeatedAtHealth);
        Assert.True(FragileHealth - worstOnTheVillain <= damage.DefeatedAtHealth);

        var reckless = 0;
        var careful = 0;
        var exchanges = 0;

        foreach (var seed in Hundred.Take(30))
        {
            var sturdy = Observe(new MinMax(_play), Sturdy(), seed);

            reckless += AllOutLines(sturdy.State);
            exchanges += sturdy.Observed.Attacks.Count;

            careful += AllOutLines(RunOne(new MinMax(_play), Fragile(), seed));
        }

        Assert.True(exchanges > 0, "the sturdy fight produced no exchange to be all-out.");

        Assert.True(reckless > 0,
            "min_max never went all-out in thirty fights where the worst counterattack could not "
            + "put anybody down, so the guard is refusing everything rather than deciding.");

        Assert.Equal(0, careful);
    }

    /// <summary>
    /// <b><c>min_max</c> leads a team attack only where an ally is standing beside it, and p.79's
    /// once-a-battle limit is what stops the second.</b>
    ///
    /// <para><b>Also unheld.</b> Inverting <c>AnAllyIsAdjacentTo</c> — so every attack is a team
    /// attack, including a duel where there is nobody to team up with — left every fixture green.
    /// p.79's two dice are worth something, so a policy claiming them where nobody could join in is
    /// a rate measured on a bonus the fight never earned.</para>
    ///
    /// <para>The control is the duel: the same style, the same seeds, one Hero and one Villain, and
    /// not a team attack in it. The limit is asserted as the page prints it — at most one team
    /// attack against a given target in a battle.</para>
    /// </summary>
    [Fact]
    public void MinMaxLeadsATeamAttackOnlyWithAnAllyBesideItAndOnlyOnceATarget()
    {
        var together = 0;
        var alone = 0;

        foreach (var seed in Hundred.Take(30))
        {
            var pair = RunOne(new MinMax(_play), LevelEdges(), seed);

            together += TeamLines(pair);

            // p.79's limit is per target per battle, and the only target the two Heroes have is the
            // Villain — so one fight can carry at most one of these.
            Assert.InRange(TeamLines(pair), 0, 1);

            alone += TeamLines(RunOne(new MinMax(_play), Duel(), seed));
        }

        Assert.True(together > 0,
            "thirty fights with two Heroes at Close Range produced no team attack, so the rule is "
            + "refusing everything rather than deciding.");

        Assert.Equal(0, alone);
    }

    // ── reckless ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b><c>reckless</c> buys a reroll whenever the roll fell short at all, and that is its second
    /// rule.</b>
    ///
    /// <para>The style's own doc comment publishes three rules and only the first — all-out on every
    /// page — was driven. Switching the reroll off entirely left every fixture green, so a rate
    /// quoted as reckless was a rate about a style that might never have touched a pool.</para>
    ///
    /// <para>Its control is <see cref="ManoAMano"/> on the same seeds and the same sheets, which
    /// buys nothing — the same shape as the mano-a-mano fixture's, and what tells a real count from
    /// a scan that has stopped finding purchases.</para>
    /// </summary>
    [Fact]
    public void RecklessBuysARerollWheneverTheRollFellShort()
    {
        var bought = 0;
        var quiet = 0;

        foreach (var seed in Hundred.Take(30))
        {
            bought += DiceOrReroll(RunOne(new Reckless(_play), OutEdged(), seed));
            quiet += DiceOrReroll(RunOne(new ManoAMano(_play), OutEdged(), seed));
        }

        Assert.True(bought > 0,
            "thirty reckless fights bought no reroll at all, so the second of this style's three "
            + "rules is a branch nothing reaches.");

        Assert.Equal(0, quiet);
    }

    private static int TeamLines(EncounterState state) =>
        state.Ledger.Lines.Count(l => string.Equals(l.Rule, "team_attacks", StringComparison.Ordinal)
                                      && l.Text.Contains("as part of a team attack", StringComparison.Ordinal));

    // ── the target-selection axis ─────────────────────────────────────────

    /// <summary>
    /// <b>The three selectors pick three different opponents out of the same three.</b>
    ///
    /// <para>The fight is built so that no two of them can agree: the opponent with the least Health
    /// is not the one with the most, and the one with the greatest attack rank is neither. A
    /// selector that had collapsed onto another would show up as two of these three being the same
    /// id, which is why they are compared with each other as well as with the expected answer.</para>
    /// </summary>
    [Fact]
    public void EachSelectorPicksItsOwnOpponent()
    {
        var state = new Encounter(_play, new SeededDice(1)).Begin(ThreeOpponents());
        var hero = state["hero"];

        // Built through the factory the server uses, so a style added to the enum and left out of
        // its switch is a build that fails here too.
        var weakest = Chosen(Policy(Targeting.Weakest), state, hero);
        var strongest = Chosen(Policy(Targeting.Strongest), state, hero);
        var threat = Chosen(Policy(Targeting.HighestThreat), state, hero);

        Assert.Equal("frail", weakest);
        Assert.Equal("tank", strongest);

        // Nobody has landed anything yet, so the threat selector falls back on the attack rank —
        // which here belongs to neither of the other two answers.
        Assert.Equal("sniper", threat);

        Assert.Equal(3, new[] { weakest, strongest, threat }.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// <b>The threat selector goes after whoever has actually landed damage, and drops the attack
    /// rank the moment somebody has.</b>
    ///
    /// <para><b>This is the half of <c>highest_threat</c> nothing reached.</b> Inverting
    /// <c>Threatening</c>'s ordering — <c>OrderByDescending</c> to <c>OrderBy</c>, so the selector
    /// goes after whoever has done the <em>least</em> damage — left every style fixture and every
    /// wire fixture green, because the only assertion about this selector was made on an opening
    /// state where nobody had landed anything and the fallback did all the work. A selector that
    /// picked the quietest opponent in the room would have shipped saying it picked the loudest.
    /// </para>
    ///
    /// <para><b>The two answers are made to disagree by construction.</b> The frail one has the
    /// <em>least</em> attack rank of the three, so it is the opponent the rank fallback would never
    /// choose; the sniper has the greatest and is what the opening state picks. Once the frail one
    /// has actually put damage on the Hero — landed by the engine, written on the ledger by
    /// <c>InflictDamage</c> and read back by <see cref="LedgerReading.DamageDealtSoFar"/> — the
    /// answer has to move to them. The opening pick is the positive control: without it, "the
    /// selector answers frail" could be a selector that had stopped reading anything at all.</para>
    /// </summary>
    [Fact]
    public void TheThreatSelectorDropsTheAttackRankOnceSomebodyHasLandedDamage()
    {
        var encounter = new Encounter(_play, new SeededDice(4_242));
        var state = encounter.Begin(ThreeOpponents());
        var policy = Policy(Targeting.HighestThreat);

        // The control, and the reason the fixture proves anything: before a blow is struck the
        // selector answers off the sheets, and the sheets say the sniper.
        Assert.Equal("sniper", Chosen(policy, state, state["hero"]));
        Assert.True(Attacking(state["frail"]) < Attacking(state["sniper"]),
            "the frail one is supposed to be the opponent the rank fallback would never pick, so "
            + "this fight no longer makes the two halves of the selector disagree.");

        // The least dangerous opponent on the sheets lands something. <b>Everybody else takes their
        // turn and does nothing with it</b>, so the damage the ledger carries belongs to exactly one
        // of the three — and it is taken through the ordinary turn order rather than by stepping an
        // attack out of turn, which p.73's own gate refuses.
        var turns = 0;

        while (LedgerReading.DamageDealtSoFar(state.Ledger).GetValueOrDefault("frail") == 0)
        {
            Assert.True(turns++ < 200,
                "two hundred turns and the frail one never landed a blow, so there is no damage for "
                + "the threat selector to have followed or ignored.");

            if (state.Current is not { } acting)
            {
                state = encounter.Step(state, new EndPage("")).State;
                continue;
            }

            if (string.Equals(acting.Id, "frail", StringComparison.Ordinal))
                state = encounter.Step(state, new Attack("frail", "hero", "might")).State;

            state = encounter.Step(state, new EndTurn(acting.Id)).State;
        }

        var dealt = LedgerReading.DamageDealtSoFar(state.Ledger);

        Assert.Equal(0, dealt.GetValueOrDefault("sniper"));
        Assert.Equal(0, dealt.GetValueOrDefault("tank"));

        Assert.Equal("frail", Chosen(policy, state, state["hero"]));
    }

    /// <summary>
    /// <b>Two opponents on the same figure are separated by their id and by nothing else, in both
    /// directions.</b>
    ///
    /// <para>Both selectors sort on <c>Standing</c> and then on the id, ascending, so a tie goes to
    /// the <em>same</em> opponent whichever end of the ladder is being asked for — which is what
    /// makes a seeded run the same run twice. The fight has exactly two opponents and they are on
    /// the same figure, so both ends are a choice between them and nothing else.</para>
    /// </summary>
    [Fact]
    public void ATieBetweenTwoOpponentsIsBrokenOnTheIdAndTheSameWayAtBothEnds()
    {
        var state = new Encounter(_play, new SeededDice(1)).Begin(Tied());
        var hero = state["hero"];

        Assert.Equal(
            state["abel"].CurrentHealth,
            state["zora"].CurrentHealth);

        Assert.Equal("abel", Chosen(Policy(Targeting.Weakest), state, hero));
        Assert.Equal("abel", Chosen(Policy(Targeting.Strongest), state, hero));
    }

    /// <summary>
    /// <b>A group of Minions is compared on bodies and a character on Health, and the reading is
    /// that the two figures are ranked against each other unconverted.</b>
    ///
    /// <para><b>It is a reading and not an arithmetic fact, so it is driven and written down.</b>
    /// p.77 gives a group one characteristic and no Health at all, so there is no exchange rate
    /// between a body and a point of Health anywhere in the book — a policy still has to answer,
    /// and this one answers that three bodies is <em>more</em> left standing than two points of
    /// Health. The consequence is the one worth stating: a Villain on 2 Health is "weaker" than a
    /// group of three, and a group of three is "stronger" than that Villain, so a party focusing
    /// fire finishes the character before it starts thinning the mob.</para>
    ///
    /// <para>Recorded on <c>StylePolicy.Standing</c> and in <c>docs/guide/play-engine.md</c>'s
    /// readings table.</para>
    /// </summary>
    [Fact]
    public void BodiesAndHealthAreRankedAgainstEachOtherUnconverted()
    {
        var state = new Encounter(_play, new SeededDice(1)).Begin(BodiesAgainstHealth());
        var hero = state["hero"];

        Assert.Equal(2, state["hurt"].CurrentHealth);
        Assert.Equal(3, state["mob"].GroupSize);

        Assert.Equal("hurt", Chosen(Policy(Targeting.Weakest), state, hero));
        Assert.Equal("mob", Chosen(Policy(Targeting.Strongest), state, hero));
    }

    /// <summary>
    /// <b>Once somebody has actually landed something, the threat selector follows the damage — and
    /// the damage it follows reconciles with the Health that came off.</b>
    ///
    /// <para><b>The reconciliation is what makes this a measurement.</b>
    /// <see cref="LedgerReading.DamageDealtSoFar"/> parses a figure out of a sentence, and a parse
    /// that read the wrong number would still produce a plausible ordering — so the totals are
    /// checked against a figure taken from somewhere else entirely: the Health the one character on
    /// the other side has actually lost. Nothing here heals, and the fight is one against three, so
    /// the two have to agree exactly.</para>
    /// </summary>
    [Fact]
    public void TheThreatSelectorFollowsDamageThatReconcilesWithTheHealthLost()
    {
        var run = Observe(new ManoAMano(_play, Targeting.Weakest), ThreeOpponents(), seed: 4_242);

        var dealt = LedgerReading.DamageDealtSoFar(run.State.Ledger);

        var opposition = ThreeOpponents()
            .Where(c => !string.Equals(c.Side, Combatant.HeroSide, StringComparison.Ordinal))
            .Select(c => c.Id)
            .ToList();

        var onTheHero = opposition.Sum(id => dealt.GetValueOrDefault(id));
        var hero = ThreeOpponents().Single(c => string.Equals(c.Id, "hero", StringComparison.Ordinal));

        Assert.True(onTheHero > 0,
            "nobody landed anything in this fight, so there is no damage for the reading to have "
            + "got right or wrong.");

        var lost = hero.FullHealth - run.State["hero"].CurrentHealth;

        // <b>The two figures are not equal and the difference is a printed rule, not slack.</b>
        // With Fatal Damage off, p.75 clamps Health at the defeat figure, so the blow that finishes
        // somebody is written on the ledger in full and takes off only what was left. The reading is
        // therefore at least the Health lost, and over it by less than one blow.
        Assert.True(onTheHero >= lost,
            $"the ledger says {onTheHero} damage was done to the Hero and they lost {lost} Health, "
            + "which is the wrong way round: the clamp can only make the reading the larger of the "
            + "two.");

        var biggest = run.Observed.Attacks
            .Where(a => string.Equals(a.Target, "hero", StringComparison.Ordinal))
            .Max(a => a.AttackSuccesses) * 2;

        Assert.True(onTheHero - lost <= biggest,
            $"{onTheHero} on the ledger against {lost} Health lost is a gap of "
            + $"{onTheHero - lost}, which is more than the one clamped blow that can explain it.");

        // And from the other direction: what the observation recorded landing is the Health that
        // actually came off, because the observation measures the state rather than the sentence.
        Assert.Equal(
            lost,
            run.Observed.Attacks.Where(a => string.Equals(a.Target, "hero", StringComparison.Ordinal))
                .Sum(a => a.Damage));
    }

    // ── what the observation carries ──────────────────────────────────────

    /// <summary>
    /// <b>Every exchange a run observes has a defending Trait read back for it.</b>
    ///
    /// <para><c>RunObservation.DefenceTraitsUnread</c> is published in every report precisely
    /// because it could stop being zero: it is the count of exchanges whose ledger sentence
    /// <see cref="LedgerReading"/> could not parse. Its control is that there were exchanges at all,
    /// and that the Traits read back are Traits the combatants actually have.</para>
    /// </summary>
    [Fact]
    public void EveryExchangeCarriesTheTraitThatAnsweredIt()
    {
        var run = Observe(new Standard(_play), OutEdged(), seed: 77);

        Assert.True(run.Observed.Attacks.Count > 0, "the fight produced no exchange to observe.");
        Assert.Equal(0, run.Observed.DefenceTraitsUnread);

        Assert.All(run.Observed.Attacks, attack =>
            Assert.False(string.IsNullOrWhiteSpace(attack.DefenceTrait),
                $"{attack.Attacker}'s attack on {attack.Target} came back with no defending Trait."));

        // And the defending Traits are the defender's own, or the phrase the engine uses when they
        // have none — not, say, the attacking Trait read out of the wrong half of the sentence.
        Assert.All(run.Observed.Attacks, attack =>
        {
            var defender = run.State[attack.Target];

            Assert.True(
                defender.TraitRanks.ContainsKey(attack.DefenceTrait!)
                || attack.DefenceTrait!.Contains("no defence", StringComparison.Ordinal),
                $"{defender.Id} answered with '{attack.DefenceTrait}', which is not a Trait of "
                + "theirs — the defending half of the ledger sentence is being read wrong.");
        });
    }

    /// <summary>
    /// <b>A purchase does not become a second exchange, and the damage the observation reports is
    /// the damage the state lost.</b>
    ///
    /// <para>A bought die and a reroll both re-apply the attack already on the table, so a run that
    /// recorded an exchange per <em>step</em> would count one attack twice — and both copies would
    /// look real. Reconciled the only way that cannot be fooled: the total damage observed against
    /// each combatant is the Health they actually lost.</para>
    /// </summary>
    [Fact]
    public void TheDamageObservedIsTheHealthThatActuallyCameOff()
    {
        var run = Observe(new MinMax(_play), LevelEdges(), seed: 31);

        var opened = LevelEdges().ToDictionary(c => c.Id, c => c.CurrentHealth, StringComparer.Ordinal);

        var landed = run.Observed.Attacks
            .GroupBy(a => a.Target, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Damage), StringComparer.Ordinal);

        Assert.True(landed.Values.Sum() > 0, "nothing landed, so there is nothing to reconcile.");

        foreach (var (id, before) in opened)
        {
            Assert.Equal(before - run.State[id].CurrentHealth, landed.GetValueOrDefault(id));
        }
    }

    /// <summary>
    /// <b>Everybody who went down is recorded once, on the page they went down on, with whoever put
    /// them there.</b>
    ///
    /// <para>Its control is that somebody did go down: a fight nobody lost would satisfy every
    /// assertion below by having nothing to check.</para>
    /// </summary>
    [Fact]
    public void EveryDefeatIsRecordedOnceWithWhatCausedIt()
    {
        var run = Observe(new Reckless(_play), OutEdged(), seed: 12);
        var floor = new Encounter(_play, new SeededDice(1)).DefeatFloor;

        var down = run.State.Combatants.Values.Where(c => c.Defeated(floor)).Select(c => c.Id).ToList();

        Assert.True(down.Count > 0, "nobody was defeated in this fight, so there is nothing to attribute.");

        Assert.Equal(
            down.Order(StringComparer.Ordinal),
            run.Observed.Defeats.Select(d => d.Combatant).Order(StringComparer.Ordinal));

        Assert.All(run.Observed.Defeats, defeat =>
        {
            Assert.InRange(defeat.Page, 1, run.Observed.Pages);

            // Everything in this fight is an attack — nothing is bleeding out at a page turn — so
            // every defeat has somebody's name and Trait against it.
            Assert.NotNull(defeat.By);
            Assert.NotNull(defeat.With);
        });
    }

    /// <summary>
    /// <b>Every exchange and every defence the observation reports is in the ledger of that same
    /// fight, read out of the sentences by hand.</b>
    ///
    /// <para><b>The two derivations are genuinely independent, which is the only reason this is a
    /// reconciliation and not a restatement.</b> The observation is built off
    /// <c>ResolvedAttack</c> and off the difference between two states; the expectation below is
    /// built by parsing the prose of every attack line — attacker, Trait, defending Trait, and the
    /// two success counts — out of the ledger. An error in either shows up as a disagreement, and
    /// the failure names the fight.</para>
    ///
    /// <para>The report the encounter server answers with is these figures grouped and divided by N,
    /// so what is checked here is the thing every row of <c>attack_forms</c> and <c>defences</c> is
    /// made of.</para>
    /// </summary>
    [Fact]
    public void TheObservationReconcilesWithTheLedgerOfTheSameFight()
    {
        var run = Observe(new Standard(_play), OutEdged(), seed: 606);

        // The ledger, by hand: "{who} attacks {whom} with {trait} {pool}d for {n} successes;
        // {whom} defends with {trait} {pool}d for {m}".
        var lines = run.State.Ledger.Lines.Where(LedgerReading.IsAnAttack).ToList();

        Assert.True(lines.Count > 0, "the fight has no attack lines, so there is nothing to reconcile.");

        // <b>Exchanges per attacker and Trait.</b> A purchase re-applies the attack already on the
        // table and writes no second attack line, which is the same thing the observation does by
        // recording one exchange a turn — so the two counts have to agree exactly.
        var fromLedger = lines
            .GroupBy(l => (l.Actor, Trait: Between(l.Text, " with ", " ")))
            .ToDictionary(g => g.Key, g => g.Count());

        var observed = run.Observed.Attacks
            .GroupBy(a => (a.Attacker, Trait: a.TraitId))
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(fromLedger.OrderBy(p => p.Key), observed.OrderBy(p => p.Key));

        // <b>And the defending half, Trait by Trait.</b>
        var defencesFromLedger = lines
            .GroupBy(l => LedgerReading.DefenceTraitIn(l), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var defencesObserved = run.Observed.Attacks
            .GroupBy(a => a.DefenceTrait!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(
            defencesFromLedger.OrderBy(p => p.Key, StringComparer.Ordinal),
            defencesObserved.OrderBy(p => p.Key, StringComparer.Ordinal));

        // <b>And whether each one held.</b> A defence held where it scored at least what the attack
        // did, which the sentence prints as its last two figures.
        var heldInTheLedger = lines.Count(l =>
            Number(l.Text, " successes; ", reverse: false) <= Number(l.Text, "d for ", reverse: true));

        Assert.Equal(run.Observed.Attacks.Count(a => a.DefenceHeld), heldInTheLedger);
    }

    /// <summary>The text between two markers, for reading a ledger sentence by hand.</summary>
    private static string Between(string text, string opens, string closes)
    {
        var from = text.IndexOf(opens, StringComparison.Ordinal) + opens.Length;
        var to = text.IndexOf(closes, from, StringComparison.Ordinal);

        return to < 0 ? text[from..] : text[from..to];
    }

    /// <summary>
    /// One of the two success counts in an attack line: the attack's, which is the number before
    /// " successes; ", or the defence's, which is the number after the last "d for ".
    /// </summary>
    private static int Number(string text, string marker, bool reverse)
    {
        if (reverse)
        {
            var at = text.LastIndexOf(marker, StringComparison.Ordinal) + marker.Length;
            return int.Parse(text[at..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }

        var end = text.IndexOf(marker, StringComparison.Ordinal);
        var start = text.LastIndexOf(' ', end - 1) + 1;

        return int.Parse(text[start..end], System.Globalization.CultureInfo.InvariantCulture);
    }

    // ── The fifth style, which is not one ─────────────────────────────────

    /// <summary>
    /// The claims the refusal of <c>narrative</c> is made of, as the phrases that carry them.
    ///
    /// <para>Three: that "befitting" is a judgement, that nothing here makes a Flaw bite, and that
    /// the place it does live is <c>take_turn</c>. A refusal that dropped any one of them is a
    /// refusal that reads as an oversight, which is the one thing this one exists not to be.</para>
    /// </summary>
    private static readonly string[] WhyNarrativeIsNotSeeded = ["befitting", "Flaw bite", "take_turn"];

    /// <summary>
    /// <b>The reason <c>narrative</c> is refused says the same thing in all three places it is
    /// written down.</b>
    ///
    /// <para><b>The <em>name</em> is already held together by the code</b> — the server compares
    /// against <see cref="StylePolicy.NarrativeStyle"/> and refuses with
    /// <see cref="StylePolicy.NarrativeIsNotSeeded"/>, so there is no second spelling of it to
    /// drift, and renaming the constant is caught by the wire fixture that asks for the style by its
    /// literal name. <b>The <em>reason</em> was not.</b> `docs/guide/play-engine.md` says the
    /// constant "carries that sentence in one place … and `PLAY-POLICY.md` prints it", and both
    /// documents in fact paraphrase it — so any of the three could have been edited into saying
    /// something the other two do not.</para>
    ///
    /// <para>Held on the claims rather than on the bytes, because a paraphrase is the right shape
    /// for a document and a wrong shape to compare literally. The control is the first assertion of
    /// each pair: every phrase has to be in the constant, so a list that had rotted into phrases
    /// nobody uses fails on the code rather than passing on the documents.</para>
    /// </summary>
    [Fact]
    public void TheReasonNarrativeIsRefusedIsTheSameInAllThreePlaces()
    {
        // <b>Each document is read at the section that is about this, and not whole.</b> "A Flaw
        // bites" is a phrase p.85's suppression spend uses too, three hundred lines away — so a
        // whole-document search is satisfied by a sentence about something else, and rewording the
        // styles section left it green.
        var policy = SectionOf(
            ProwlersAndParagonsAutomation.McpPlay.PlayPolicy.Text,
            "## Styles: how a fight is played", "\n## ");

        var guide = SectionOf(
            File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "docs", "guide", "play-engine.md")),
            "### The four styles, and the fifth that is not one", "\n### ");

        Assert.NotEmpty(WhyNarrativeIsNotSeeded);

        foreach (var claim in WhyNarrativeIsNotSeeded)
        {
            Assert.Contains(claim, StylePolicy.NarrativeIsNotSeeded, StringComparison.Ordinal);

            Assert.Contains(claim, policy, StringComparison.Ordinal);
            Assert.Contains(claim, guide, StringComparison.Ordinal);
        }

        // And the name is still absent from the list a caller may choose from, which is the whole
        // of what "refused by name" means.
        Assert.DoesNotContain(StylePolicy.NarrativeStyle, StylePolicy.StyleIds, StringComparer.Ordinal);

        Assert.Contains(StylePolicy.NarrativeStyle, policy, StringComparison.Ordinal);
        Assert.Contains(StylePolicy.NarrativeStyle, guide, StringComparison.Ordinal);
    }

    /// <summary>
    /// One section of a document: from <paramref name="heading"/> to the next heading at
    /// <paramref name="nextLevel"/> or above, so a phrase found somewhere else entirely does not
    /// count as this section saying it.
    /// </summary>
    private static string SectionOf(string text, string heading, string nextLevel)
    {
        var at = text.IndexOf(heading, StringComparison.Ordinal);

        Assert.True(at >= 0,
            $"the section \"{heading}\" is gone, so nothing holds the refusal's reason together "
            + "there any more.");

        var from = at + heading.Length;
        var next = text.IndexOf(nextLevel, from, StringComparison.Ordinal);
        var upper = text.IndexOf("\n## ", from, StringComparison.Ordinal);

        if (upper >= 0 && (next < 0 || upper < next)) next = upper;

        return next < 0 ? text[from..] : text[from..next];
    }

    // ── The fights ────────────────────────────────────────────────────────

    /// <summary>Two Heroes the Villain is quicker than: <c>standard</c>'s seize has its trigger.</summary>
    private static IReadOnlyList<Combatant> OutEdged() =>
    [
        Hero("cho", "Cho", edge: 4),
        Hero("felix", "Felix", edge: 3),
        Villain("schism", "Schism", edge: 12)
    ];

    /// <summary>The same fight with every Edge the same, which is <c>standard</c>'s control.</summary>
    private static IReadOnlyList<Combatant> LevelEdges() =>
    [
        Hero("cho", "Cho", edge: 6),
        Hero("felix", "Felix", edge: 6),
        Villain("schism", "Schism", edge: 6)
    ];

    /// <summary>
    /// One Hero against three, built so that the weakest, the strongest and the hardest hitter are
    /// three different characters.
    /// </summary>
    private static IReadOnlyList<Combatant> ThreeOpponents() =>
    [
        Hero("hero", "the Hero", edge: 6, health: 30),

        Villain("frail", "the frail one", edge: 5, health: 4, might: 5),
        Villain("tank", "the tank", edge: 5, health: 20, might: 6),
        Villain("sniper", "the sniper", edge: 5, health: 12, might: 11)
    ];

    /// <summary>Two opponents on exactly the same Health, so both ends of the ladder are a tie.</summary>
    private static IReadOnlyList<Combatant> Tied() =>
    [
        Hero("hero", "the Hero", edge: 6, health: 30),

        Villain("abel", "Abel", edge: 5, health: 16),
        Villain("zora", "Zora", edge: 5, health: 16)
    ];

    /// <summary>
    /// A group of three Minions against a Villain beaten down to two Health, which is where bodies
    /// and Health have to be ranked against each other.
    /// </summary>
    private static IReadOnlyList<Combatant> BodiesAgainstHealth() =>
    [
        Hero("hero", "the Hero", edge: 6, health: 30),

        Villain("hurt", "the hurt one", edge: 5, health: 2),
        Combatant.Minions("mob", "the mob", threat: 4, groupSize: 3, threatTraitId: "threat")
    ];

    /// <summary>The Health of the fight min-max is allowed to go all-out in.</summary>
    private const int SturdyHealth = 40;

    /// <summary>And of the one it is not: the worst blow on the table would end anybody in it.</summary>
    private const int FragileHealth = 5;

    /// <summary>Two who can each take the worst the other has, so min-max's all-out guard opens.</summary>
    private static IReadOnlyList<Combatant> Sturdy() =>
    [
        Hero("hero", "the Hero", edge: 6, health: SturdyHealth),
        Villain("villain", "the Villain", edge: 5, health: SturdyHealth)
    ];

    /// <summary>The same two, thin enough that one blow could end either — so the guard never opens.</summary>
    private static IReadOnlyList<Combatant> Fragile() =>
    [
        Hero("hero", "the Hero", edge: 6, health: FragileHealth),
        Villain("villain", "the Villain", edge: 5, health: FragileHealth)
    ];

    /// <summary>One Hero and one Villain: nobody has an ally to team up with.</summary>
    private static IReadOnlyList<Combatant> Duel() =>
    [
        Hero("cho", "Cho", edge: 6),
        Villain("schism", "Schism", edge: 6)
    ];

    private static Combatant Hero(string id, string name, int edge, int health = 12) =>
        Combatant.Hero(
            id, name, edge, health, resolve: 5,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 8, ["toughness"] = 5, ["agility"] = 4
            },
            ["toughness", "agility"]);

    private static Combatant Villain(string id, string name, int edge, int health = 16, int might = 9) =>
        Combatant.Villain(
            id, name, edge, health,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = might, ["toughness"] = 5, ["agility"] = 4
            },
            ["toughness", "agility"]);

    // ── Driving one ───────────────────────────────────────────────────────

    private EncounterState RunOne(IPolicy policy, IReadOnlyList<Combatant> fight, int seed) =>
        Observe(policy, fight, seed).State;

    private RunResult Observe(IPolicy policy, IReadOnlyList<Combatant> fight, int seed)
    {
        var encounter = new Encounter(_play, new SeededDice(seed));
        return encounter.RunObserved(encounter.Begin(fight), policy, MaxPages);
    }

    private int Opening(IReadOnlyList<Combatant> fight) =>
        new Encounter(_play, new SeededDice(1)).Begin(fight).Adversity;

    private StylePolicy Policy(Targeting targeting) =>
        StylePolicy.For(PlayStyle.ManoAMano, targeting, _play);

    /// <summary>The rank a combatant would attack at, which is what the threat fallback compares.</summary>
    private int Attacking(Combatant combatant) =>
        AttackOptions.BestFor(_play, combatant) is { } trait ? combatant.Rank(trait) : 0;

    private static string Chosen(StylePolicy policy, EncounterState state, Combatant actor) =>
        policy.Choose(state, actor) is Attack attack
            ? attack.Target
            : throw new InvalidOperationException("the policy held its action rather than attacking.");

    private static int AllOutLines(EncounterState state) =>
        state.Ledger.Lines.Count(l => string.Equals(l.Rule, "going_all_out", StringComparison.Ordinal)
                                      && l.Text.Contains("goes all-out", StringComparison.Ordinal));

    private static int DiceBought(EncounterState state) =>
        state.Ledger.Lines.Count(l =>
            string.Equals(l.Rule, "spend_challenge_roll_dice", StringComparison.Ordinal)
            && LedgerReading.IsAPurchase(l, state[l.Actor].Name));

    private static int SeizeLines(EncounterState state) =>
        state.Ledger.Lines.Count(l => string.Equals(l.Rule, "seizing_initiative", StringComparison.Ordinal)
                                      && LedgerReading.IsAPurchase(l, state[l.Actor].Name));
}
