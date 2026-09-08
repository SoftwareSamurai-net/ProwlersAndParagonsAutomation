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

        foreach (var seed in Hundred.Take(30))
        {
            var careful = LedgerReading.Purchases(RunOne(new Standard(_play), LevelEdges(), seed)).Count;
            var greedy = LedgerReading.Purchases(RunOne(new MinMax(_play), LevelEdges(), seed)).Count;

            if (careful == 0 && greedy > 0) apart.Add(seed);
        }

        Assert.True(apart.Count > 0,
            "on none of thirty level-Edged seeds did min_max buy anything standard did not — so "
            + "the two styles are measuring the same thing and the report's style name says "
            + "nothing.");
    }

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

    private static string Chosen(StylePolicy policy, EncounterState state, Combatant actor) =>
        policy.Choose(state, actor) is Attack attack
            ? attack.Target
            : throw new InvalidOperationException("the policy held its action rather than attacking.");

    private static int AllOutLines(EncounterState state) =>
        state.Ledger.Lines.Count(l => string.Equals(l.Rule, "going_all_out", StringComparison.Ordinal)
                                      && l.Text.Contains("goes all-out", StringComparison.Ordinal));

    private static int SeizeLines(EncounterState state) =>
        state.Ledger.Lines.Count(l => string.Equals(l.Rule, "seizing_initiative", StringComparison.Ordinal)
                                      && LedgerReading.IsAPurchase(l, state[l.Actor].Name));
}
