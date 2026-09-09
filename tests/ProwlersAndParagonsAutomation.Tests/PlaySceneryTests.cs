using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The three Chapter 4 rules that name something Chapter 7 supplies, driven against the shipped
/// tables.</b>
///
/// <para>p.75's cover wants a Structure, p.78's knockback wants an object "tougher than they are",
/// and p.74's throw wants "the object's weight rank". Every one of those was the caller's word or
/// nothing at all while <c>environment.json</c> was loaded and applied by nothing. What is here is
/// each of them taken off the printed page instead — and, for every one, the case where the page
/// has nothing to give and the engine refuses rather than inventing a figure.</para>
///
/// <para><b>Every fixture drives both sides of its own threshold.</b> A rule that fired on every
/// object would be indistinguishable from one that fired on none, so a named row is always compared
/// with a bare figure that should differ and with one that should not.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlaySceneryTests
{
    private readonly PlayRulesRepository _play;

    public PlaySceneryTests(PlayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _play = fixture.Play;
    }

    // ── p.75's cover, rated off p.107 and p.108 ──────────────────────────────

    /// <summary>
    /// <b>Naming the obstacle takes its Structure off Chapter 7, and the ledger cites the table.</b>
    ///
    /// <para>The instrument is the dice thrown, the same one every modifier fixture in
    /// <see cref="PlayEngineStepTests"/> uses: the wall answers as a passive defence, so the
    /// exchange throws the attacker's pool plus the Structure. "A brick wall" is p.108's
    /// <c>Brick Wall</c> at 6, so the fight has to throw exactly what a bare <c>CoverStructure: 6</c>
    /// throws — <b>and not what a 5 or a 7 throws</b>, which is the control that the 6 came off the
    /// page rather than out of a default.</para>
    ///
    /// <para>The ledger line is asserted on the entry id and the source ref, so a line that had
    /// stopped citing p.108 would fail even while the arithmetic still came out right.</para>
    /// </summary>
    [Fact]
    public void NamingTheCoverTakesItsStructureOffChapterSevensTable()
    {
        var table = _play.GetEnvironment("scenery_table");
        var row = table.SceneryTable!.Single(r => r.Scenery.Contains("Brick Wall", StringComparer.Ordinal));

        var named = Exchange(new Attack("hero", "villain", "might", CoverScenery: "a brick wall"));
        var stated = Exchange(new Attack("hero", "villain", "might", CoverStructure: row.Structure));

        Assert.Equal(stated.Thrown, named.Thrown);

        // Both sides of the threshold: one either way is a different fight.
        Assert.NotEqual(
            Exchange(new Attack("hero", "villain", "might", CoverStructure: row.Structure - 1)).Thrown,
            named.Thrown);

        Assert.NotEqual(
            Exchange(new Attack("hero", "villain", "might", CoverStructure: row.Structure + 1)).Thrown,
            named.Thrown);

        var line = Assert.Single(named.Lines, l =>
            string.Equals(l.Rule, table.Id, StringComparison.Ordinal));

        Assert.Equal(table.SourceRef, line.SourceRef);
        Assert.Contains("Brick Wall", line.Text, StringComparison.Ordinal);
        Assert.Contains($"Structure {row.Structure}", line.Text, StringComparison.Ordinal);

        // And p.75's own two lines are still written about the same figure, which is what makes the
        // pair of citations worth having.
        Assert.Contains(named.Lines, l =>
            string.Equals(l.Rule, "modifier_cover", StringComparison.Ordinal)
            && l.Text.Contains($"{row.Structure}d", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The longest printed spelling wins, and here that is four dice of Structure.</b>
    ///
    /// <para>p.107 rates <c>Glass</c> at 2 and <c>Bulletproof Glass</c> at 6, and a caller types a
    /// phrase rather than a row id. Matching the shorter name would silently price a bank teller's
    /// window as a shop front — the same defect the weapons match shipped once, where a sniper's
    /// rifle collected the ordinary Rifle's bonus.</para>
    ///
    /// <para>The control is the shop front: "a glass window" has to reach the 2, or "longest wins"
    /// would be satisfied by a matcher that had stopped finding the short row at all.</para>
    /// </summary>
    [Fact]
    public void TheLongestPrintedSpellingIsTheRowThatAnswers()
    {
        var rows = _play.GetEnvironment("smashing_table").SmashingTable!.Rows;
        var plain = rows.Single(r => r.Materials.Contains("Glass", StringComparer.Ordinal)).Structure;
        var proof = rows.Single(r => r.Materials.Contains("Bulletproof Glass", StringComparer.Ordinal)).Structure;

        Assert.NotEqual(plain, proof);

        var thick = Exchange(new Attack("hero", "villain", "might", CoverScenery: "a bulletproof glass window"));
        var thin = Exchange(new Attack("hero", "villain", "might", CoverScenery: "a glass window"));

        Assert.Equal(Exchange(new Attack("hero", "villain", "might", CoverStructure: proof)).Thrown, thick.Thrown);
        Assert.Equal(Exchange(new Attack("hero", "villain", "might", CoverStructure: plain)).Thrown, thin.Thrown);

        Assert.Contains("Bulletproof Glass", Assert.Single(thick.Lines, l =>
            string.Equals(l.Rule, "smashing_table", StringComparison.Ordinal)).Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An object Chapter 7 does not print is refused with nothing rolled, and so is one it prints
    /// twice.</b>
    ///
    /// <para>Naming a row is a claim that a page rates the thing. Where no page does, the honest
    /// answer is the bare figure the caller could have supplied instead — inventing one and citing
    /// p.108 for it is exactly the class of line this engine's ledger rules exist to prevent. Where
    /// two rows tie at the longest spelling, "the first one the loop reached" is not an answer to
    /// which of them was meant, which is the same refusal the weapons match makes.</para>
    ///
    /// <para>The refusal is driven rather than asserted about: the dice source is scripted and its
    /// <c>Remaining</c> has to be untouched, so a refusal that had quietly resolved the attack
    /// anyway would fail here rather than pass on the ledger text.</para>
    /// </summary>
    [Theory]
    [InlineData("a rolled-up newspaper", "prints nothing called")]
    [InlineData("a stone and steel barrier", "which of them is meant is the GM's")]
    public void AnObjectChapterSevenCannotRateIsRefusedWithNothingRolled(string named, string says)
    {
        var refused = Refused(new Attack("hero", "villain", "might", CoverScenery: named));

        Assert.Contains(refused, l => l.Text.Contains(says, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Naming a row and stating a figure are two answers to one question, so both are refused.</b>
    ///
    /// <para>Either precedence rule this engine could have taken would put a number on the ledger
    /// the caller did not ask for. And the bare figure is not redundant: p.107 lets the GM move a
    /// Structure by as much as four dice for thickness or condition — the chapter's own second
    /// worked example adds one to a telephone pole — so a stated figure is how a thickened wall is
    /// said, and a named row is how a printed one is.</para>
    /// </summary>
    [Fact]
    public void NamingARowAndStatingAStructureAtOnceIsRefused()
    {
        var refused = Refused(new Attack(
            "hero", "villain", "might", CoverStructure: 9, CoverScenery: "a brick wall"));

        Assert.Contains(refused, l =>
            string.Equals(l.Rule, "modifier_cover", StringComparison.Ordinal)
            && l.Text.Contains("two answers to one question", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A massive object has a weight rank and no Structure, and the refusal says which.</b>
    ///
    /// <para>p.108's <c>massive_objects</c> prints
    /// <c>uses_instead_of_body_or_structure</c> — "the object's weight rank" — so the Empire State
    /// Building is in the data at 23 and that 23 is not a Structure. Reading it as one would hand
    /// p.75's cover clause a figure the page does not print for that purpose, cite p.108 for it, and
    /// be wrong by fifteen dice against the nearest thing the Scenery table does rate.</para>
    ///
    /// <para>The control is the Scenery table's own vault door, which is rated and does answer: a
    /// refusal that fired on every named object would prove nothing about the reason given.</para>
    /// </summary>
    [Fact]
    public void AMassiveObjectHasNoStructureAndTheRefusalSaysSo()
    {
        var refused = Refused(new Attack(
            "hero", "villain", "might", CoverScenery: "the Empire State Building"));

        var line = Assert.Single(refused, l =>
            string.Equals(l.Rule, "massive_objects", StringComparison.Ordinal));

        Assert.Contains("weight rank", line.Text, StringComparison.Ordinal);
        Assert.Contains("no Structure at all", line.Text, StringComparison.Ordinal);

        // The control: a rated object of the same shape resolves.
        var vault = _play.GetEnvironment("scenery_table").SceneryTable!
            .Single(r => r.Scenery.Contains("Vault Door", StringComparer.Ordinal));

        var through = Exchange(
            new Attack("hero", "villain", "might", CoverScenery: "a vault door"),
            attackRank: vault.Structure + 1);

        Assert.Contains(through.Lines, l =>
            string.Equals(l.Rule, "scenery_table", StringComparison.Ordinal));
    }

    // ── p.78's knockback, into something the page rates ──────────────────────

    /// <summary>
    /// <b>p.78's last sentence is applied, and both sides of its one test are driven.</b>
    ///
    /// <para>"They suffer half as much damage as the original attack inflicted, assuming the object
    /// they strike is tougher than they are (if the target's passive defense exceeds the object's
    /// Structure, they smash though unharmed)." The parenthesis is what defines the assumption, so
    /// there is one comparison and not two — and the fixture holds the fight, the blow and the
    /// target's Toughness fixed and moves <em>only</em> the object: a brick wall the target
    /// out-ranks, and a vault door it does not.</para>
    ///
    /// <para><b>The instrument is Health and not the ledger's wording.</b> Half of the six points
    /// the blow did is three, rounded the Glossary's way, and the two runs have to differ by exactly
    /// that — a clause that had gone back to being announced rather than applied would leave both on
    /// the same total, which is the shape of defect the adversarial pass over this engine found
    /// seven of.</para>
    /// </summary>
    [Fact]
    public void AKnockbackIntoANamedObjectCostsHalfTheBlowUnlessTheTargetOutRanksIt()
    {
        var rule = _play.GetCombat("knockback").Knockback!;
        var rows = _play.GetEnvironment("scenery_table").SceneryTable!;

        var wall = rows.Single(r => r.Scenery.Contains("Brick Wall", StringComparer.Ordinal)).Structure;
        var vault = rows.Single(r => r.Scenery.Contains("Vault Door", StringComparer.Ordinal)).Structure;

        // The fixture's own control: the target's passive defence has to sit between the two, or
        // "one either side of the threshold" would be a claim about nothing.
        const int Toughness = 8;
        Assert.True(wall < Toughness && Toughness < vault,
            "the two objects no longer straddle the target's passive defence, so this fixture is "
            + "driving one side of the test twice");

        var through = Knocked("a brick wall", Toughness);
        var into = Knocked("a vault door", Toughness);

        // The positive control: the blow itself did what the entry's floor asks for, in both runs.
        Assert.Equal(12 - rule.MinimumDamage, through.State["villain"].CurrentHealth);

        var half = Rounding.Half(_play, rule.MinimumDamage);
        Assert.True(half > 0, "half the blow is nothing, so the two runs could not differ by it");

        Assert.Equal(12 - rule.MinimumDamage - half, into.State["villain"].CurrentHealth);

        Assert.Contains(through.Lines, l =>
            string.Equals(l.Rule, "knockback", StringComparison.Ordinal)
            && l.Text.Contains(rule.APassiveDefenseAboveTheObjectsStructure, StringComparison.Ordinal));

        var hit = Assert.Single(into.Lines, l =>
            string.Equals(l.Rule, "knockback", StringComparison.Ordinal)
            && l.Text.Contains("Vault Door", StringComparison.Ordinal));

        Assert.Contains(rule.DamageOnStrikingASolidObject, hit.Text, StringComparison.Ordinal);
        Assert.Contains($"Structure {vault}", hit.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>"Half as much damage as the original attack inflicted" is halved the Glossary's way, and
    /// only an odd blow can say so.</b>
    ///
    /// <para>Every other knockback fixture here does exactly the entry's <c>minimum_damage</c> of
    /// six, which halves to three whichever way it is rounded — so substituting an
    /// <c>inflicted / 2</c> for the <c>Rounding.Half</c> that reads <c>play_meta</c>'s
    /// <c>half_rounds_up</c> left the whole suite green. That is the mutation
    /// <see cref="Rounding"/>'s own doc comment says this project keeps being caught by: a second
    /// transcription of p.7's convention agrees with the file exactly until somebody corrects the
    /// file.</para>
    ///
    /// <para>Seven points is the smallest blow above the floor that separates the two directions,
    /// and the fixture asserts that it does before it asserts which way this engine went.</para>
    /// </summary>
    [Fact]
    public void HalfOfAnOddBlowIsRoundedTheGlossarysWay()
    {
        var rule = _play.GetCombat("knockback").Knockback!;

        var vault = _play.GetEnvironment("scenery_table").SceneryTable!
            .Single(r => r.Scenery.Contains("Vault Door", StringComparer.Ordinal)).Structure;

        const int Blow = 7;
        const int Toughness = 8;

        Assert.True(Blow >= rule.MinimumDamage, "the blow is under p.78's own floor, so nothing is bought");
        Assert.True(Toughness < vault, "the target out-ranks the vault door, so nothing is halved at all");

        Assert.NotEqual(Blow / 2, Rounding.Half(_play, Blow));

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 },
            []);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = Toughness },
            ["toughness"]);

        // Three sixes and a four is seven successes on p.67's map, and nothing on the defence.
        var dice = new ScriptedDice(
            [6, 6, 6, 4, .. Enumerable.Repeat(1, 4), .. Enumerable.Repeat(1, Toughness)]);

        var encounter = new Encounter(_play, dice);
        var blow = encounter.Step(encounter.Begin([hero, villain]), new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed));

        // The positive control: the blow really was the odd one this fixture is about.
        Assert.Contains(blow.Added, l =>
            l.Text.Contains($"might 8d for {Blow} successes", StringComparison.Ordinal));

        Assert.Equal(12 - Blow, blow.State["villain"].CurrentHealth);
        Assert.Equal(0, dice.Remaining);

        var spent = encounter.Step(blow.State, new SpendResolve(
            "hero", ResolveSpend.Knockback, SolidObject: "a vault door"));

        Assert.Equal(12 - Blow - Rounding.Half(_play, Blow), spent.State["villain"].CurrentHealth);
    }

    /// <summary>
    /// <b>A passive defence exactly equal to the Structure does not smash through, and the page is
    /// what says so.</b>
    ///
    /// <para>p.78's parenthesis is "if the target's passive defense <em>exceeds</em> the object's
    /// Structure, they smash though unharmed" — so the tie belongs to the object and the target
    /// takes half the blow. The fixture above straddles the boundary with a 6 and a 12 against a
    /// Toughness of 8 and never stands on it, which is exactly how a <c>&gt;=</c> in place of the
    /// <c>&gt;</c> survived the whole suite: an off-by-one at a boundary no fixture stands on is
    /// invisible to every test that stands either side of it.</para>
    ///
    /// <para><b>The instrument is Health, and the control is one rank up.</b> The same fight is run
    /// twice against the same brick wall, once with a Toughness equal to its Structure and once with
    /// a Toughness one above it: the first has to lose half the blow and the second has to lose
    /// nothing, so a comparison that had slipped either way moves one of the two answers.</para>
    /// </summary>
    [Fact]
    public void APassiveDefenceEqualToTheStructureDoesNotSmashThrough()
    {
        var rule = _play.GetCombat("knockback").Knockback!;

        var wall = _play.GetEnvironment("scenery_table").SceneryTable!
            .Single(r => r.Scenery.Contains("Brick Wall", StringComparer.Ordinal)).Structure;

        var half = Rounding.Half(_play, rule.MinimumDamage);
        Assert.True(half > 0, "half the blow is nothing, so the two runs could not differ by it");

        var tied = Knocked("a brick wall", wall);
        var over = Knocked("a brick wall", wall + 1);

        // The tie belongs to the object: p.78 hands the target the parenthesis only where their
        // passive defence exceeds the Structure, and equal is not more.
        Assert.Equal(12 - rule.MinimumDamage - half, tied.State["villain"].CurrentHealth);

        var hit = Assert.Single(tied.Lines, l =>
            string.Equals(l.Rule, "knockback", StringComparison.Ordinal)
            && l.Text.Contains("Brick Wall", StringComparison.Ordinal));

        Assert.Contains("does not exceed the Structure", hit.Text, StringComparison.Ordinal);

        // The control, one rank above the same wall: this is where the parenthesis starts.
        Assert.Equal(12 - rule.MinimumDamage, over.State["villain"].CurrentHealth);

        Assert.Contains(over.Lines, l =>
            string.Equals(l.Rule, "knockback", StringComparison.Ordinal)
            && l.Text.Contains(rule.APassiveDefenseAboveTheObjectsStructure, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An object no page rates refuses the purchase rather than charging for it.</b>
    ///
    /// <para>The extra damage is priced off a Structure. A knockback into "a pile of cardboard" has
    /// no figure behind it, so a point taken for one would buy a state change nothing could
    /// receive — which is the same refusal p.79's lure that names nobody makes, and the reason it is
    /// a refusal rather than a shrug. The proof is the pool: the buyer still holds every point they
    /// started with.</para>
    ///
    /// <para>p.108's Massive Objects rows are the second case and the more interesting one, because
    /// they <em>are</em> printed: the page gives them a weight rank in place of a Structure, so
    /// there is nothing to compare a passive defence with and the refusal says which figure is
    /// missing.</para>
    /// </summary>
    [Theory]
    [InlineData("a pile of cardboard", "prints nothing called")]
    [InlineData("the Golden Gate Bridge", "no Structure at all")]
    public void AKnockbackIntoSomethingNoPageRatesIsRefusedWithNothingSpent(string named, string says)
    {
        var opening = Knocked(null, 8).State["hero"].Resolve;

        var refused = Knocked(named, 8, expectSpend: false);

        Assert.Equal(opening + _play.GetCombat("knockback").Knockback!.CostResolve,
            refused.State["hero"].Resolve);

        // Nothing was thrown either: the target is where the blow left them.
        Assert.Equal(RangeBand.Close, refused.State.RangeBetween("hero", "villain"));

        Assert.Contains(refused.Lines, l => l.Text.Contains(says, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>p.78 says "the target's passive defense", and both halves of that phrase are driven.</b>
    ///
    /// <para>Every other knockback fixture here gives its target exactly one defence, a Toughness —
    /// so the derivation that picks the figure was measured by nothing. Two mutations proved it:
    /// letting an <em>active</em> defence answer, and taking the <em>least</em> passive one instead
    /// of the greatest, each left all 5058 tests green.</para>
    ///
    /// <para><b>One target, three defences, and two objects.</b> The Villain dodges at 20, wears
    /// Armor at 8 and has a Toughness of 2, and is thrown into a brick wall at 6 and then into a
    /// vault door at 12 — so the Armor is what answers both, and each wrong reading moves exactly
    /// one of the two outcomes: the least passive would take damage off the wall, and the Agility
    /// would carry them through the vault door. Health is the instrument and the ledger line has to
    /// name the Trait, because a line naming the Agility beside the right arithmetic is a line that
    /// will be believed.</para>
    /// </summary>
    [Fact]
    public void TheGreatestPassiveDefenceAnswersTheStructureAndAnActiveOneNeverDoes()
    {
        var rule = _play.GetCombat("knockback").Knockback!;
        var rows = _play.GetEnvironment("scenery_table").SceneryTable!;

        var wall = rows.Single(r => r.Scenery.Contains("Brick Wall", StringComparer.Ordinal)).Structure;
        var vault = rows.Single(r => r.Scenery.Contains("Vault Door", StringComparer.Ordinal)).Structure;

        Assert.True(Hide < wall && wall < Plate && Plate < vault && vault < Dodge,
            "the three defences no longer straddle the two Structures, so a wrong reading of "
            + "\"the target's passive defense\" would give the same two answers as the right one");

        var half = Rounding.Half(_play, rule.MinimumDamage);
        Assert.True(half > 0, "half the blow is nothing, so the two runs could not differ by it");

        var through = KnockedIntoWithThreeDefences("a brick wall");
        var into = KnockedIntoWithThreeDefences("a vault door");

        // The Armor carries them through the wall, which the Toughness underneath it would not.
        Assert.Equal(12 - rule.MinimumDamage, through.State["villain"].CurrentHealth);

        // And it does not carry them through the vault door, which the Agility above it would.
        Assert.Equal(12 - rule.MinimumDamage - half, into.State["villain"].CurrentHealth);

        foreach (var run in new[] { through, into })
        {
            Assert.Contains(run.Lines, l =>
                string.Equals(l.Rule, "knockback", StringComparison.Ordinal)
                && l.Text.Contains($"armor at {Plate}d", StringComparison.Ordinal));
        }
    }

    /// <summary>The Villain's active defence, and the biggest figure on their sheet.</summary>
    private const int Dodge = 20;

    /// <summary>The Villain's greatest passive defence.</summary>
    private const int Plate = 8;

    /// <summary>The Villain's least passive defence.</summary>
    private const int Hide = 2;

    /// <summary>
    /// The same six-point blow and the same point of Resolve as <see cref="Knocked"/>, against a
    /// target carrying an active defence and two passive ones — so which figure answers the
    /// Structure is a question the fixture can ask.
    /// </summary>
    private (EncounterState State, IReadOnlyList<LedgerLine> Lines) KnockedIntoWithThreeDefences(
        string solidObject)
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 },
            []);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["agility"] = Dodge, ["armor"] = Plate, ["toughness"] = Hide
            },
            ["agility", "armor", "toughness"]);

        // Three sixes for six successes on the attack, and nothing at all on the defence — which
        // the Villain makes with the Agility, because p.75 answers with the greatest rank available.
        var dice = new ScriptedDice(
            [6, 6, 6, .. Enumerable.Repeat(1, 5), .. Enumerable.Repeat(1, Dodge)]);

        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);

        var blow = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed));

        // The positive controls: the blow was the one the entry's floor asks for, the defence that
        // answered it was the active one, and every scripted face was consumed.
        Assert.Contains(blow.Added, l =>
            l.Text.Contains("might 8d for 6 successes", StringComparison.Ordinal)
            && l.Text.Contains($"agility {Dodge}d for 0", StringComparison.Ordinal));

        Assert.Equal(0, dice.Remaining);

        var spent = encounter.Step(blow.State, new SpendResolve(
            "hero", ResolveSpend.Knockback, SolidObject: solidObject));

        Assert.Equal(0, dice.Remaining);
        Assert.Equal(3 - _play.GetCombat("knockback").Knockback!.CostResolve, spent.State["hero"].Resolve);

        return (spent.State, spent.Added);
    }

    /// <summary>
    /// <b>The line for a purchase that names nothing counts what a purchase can actually name.</b>
    ///
    /// <para>It offered the caller "58 things", which is every row of Chapter 7's three object
    /// tables — and sixteen of those are Massive Objects rows, which the very next branch refuses
    /// because p.108 gives them a weight rank in place of a Structure. A ledger line that sends a
    /// reader at sixteen names this engine will not take is the same defect as a plausible figure
    /// with a page cited beside it, reached from the other end.</para>
    ///
    /// <para>The figure is derived from the shipped tables rather than typed, and both halves are
    /// asserted: the count is the rows that carry a Structure, and it is not the count of all of
    /// them — so a line that went back to counting everything fails here rather than reading
    /// plausibly.</para>
    /// </summary>
    [Fact]
    public void TheLineForAKnockbackIntoNothingCountsOnlyWhatCarriesAStructure()
    {
        var rated = _play.GetEnvironment("smashing_table").SmashingTable!.Rows.Sum(r => r.Materials.Count)
                    + _play.GetEnvironment("scenery_table").SceneryTable!.Sum(r => r.Scenery.Count);

        var massive = _play.GetEnvironment("massive_objects_table").MassiveObjectsTable!
            .Sum(r => r.Objects.Count);

        Assert.True(massive > 0,
            "no Massive Objects rows were read, so 'the count excludes them' is a claim about "
            + "nothing and this fixture could not tell the two counts apart");

        var line = Assert.Single(Knocked(null, 8).Lines, l =>
            string.Equals(l.Rule, "knockback", StringComparison.Ordinal)
            && l.Text.Contains("was named", StringComparison.Ordinal));

        Assert.Contains($"rate {rated} things", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain($"rate {rated + massive} things", line.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Six points of subdual damage — the entry's own floor — and then the point spent, optionally
    /// naming what the target is thrown into.
    /// </summary>
    private (EncounterState State, IReadOnlyList<LedgerLine> Lines) Knocked(
        string? solidObject, int targetToughness, bool expectSpend = true)
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 3,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 4 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = targetToughness },
            ["toughness"]);

        // Three sixes for six successes on the attack, and nothing at all on the defence.
        var dice = new ScriptedDice(
            [6, 6, 6, .. Enumerable.Repeat(1, 5), .. Enumerable.Repeat(1, targetToughness)]);

        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin([hero, villain]);

        var blow = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.Unarmed));

        state = blow.State;

        Assert.Contains(blow.Added, l =>
            l.Text.Contains("might 8d for 6 successes", StringComparison.Ordinal)
            && l.Text.Contains($"toughness {targetToughness}d for 0", StringComparison.Ordinal));

        Assert.Equal(0, dice.Remaining);

        var spent = encounter.Step(state, new SpendResolve(
            "hero", ResolveSpend.Knockback, SolidObject: solidObject));

        // The purchase rolls nothing, whichever way it goes.
        Assert.Equal(0, dice.Remaining);

        var cost = _play.GetCombat("knockback").Knockback!.CostResolve;
        Assert.Equal(expectSpend ? 3 - cost : 3, spent.State["hero"].Resolve);

        return (spent.State, spent.Added);
    }

    // ── p.108's improvised weapons, and p.74's throw ─────────────────────────

    /// <summary>
    /// <b>Something Chapter 7 rates and Chapter 6 does not is priced by p.108, and the object is its
    /// own ceiling.</b>
    ///
    /// <para>Before this, a motorcycle in somebody's hands was an item the weapons tables printed no
    /// row for: the Gear Limit capped the Trait, nothing was added, and the ledger said what a bonus
    /// for it would be was the GM's. p.108 prices it — a die for swinging it, and an attack rank
    /// capped at the object's own rank plus six.</para>
    ///
    /// <para><b>Both sides of the ceiling are driven, because a cap that fired on every object would
    /// be indistinguishable from one that fired on none.</b> Under a raised Gear Limit a rope
    /// (Structure 2, so 8) binds and a brick wall (6, so 12) does not, and the two answers have to
    /// differ by exactly what the two ceilings differ by. The control at the bottom is p.87 alone: a
    /// battle axe is a printed weapon, so Chapter 7 never answers for it.</para>
    /// </summary>
    [Fact]
    public void AnObjectChapterSevenRatesIsPricedByPageOneOhEightAndCappedByItself()
    {
        var rule = _play.GetEnvironment("scenery_as_weapons").SceneryAsWeapons!;
        var rows = _play.GetEnvironment("smashing_table").SmashingTable!.Rows;

        var rope = rows.Single(r => r.Materials.Contains("Rope", StringComparer.Ordinal)).Structure;
        var brick = rows.Single(r => r.Materials.Contains("Brick", StringComparer.Ordinal)).Structure;

        // Under the default limit the object's own ceiling cannot bind — 6 plus a die is under every
        // row's rank plus six — so the raised one is what makes this fixture about p.108's cap.
        var table = new TableRules { RaisedGearLimit = true, GearLimitRank = 12 };

        var swungRope = Swings("a length of rope", table);
        var swungBrick = Swings("a brick", table);

        Assert.Equal(rope + rule.CapBonusDice, swungRope.Rank);
        Assert.Equal(brick + rule.CapBonusDice, swungBrick.Rank);

        // The bonus is applied where the ceiling does not bind: 12 capped, plus the printed die.
        var underTheCeiling = Swings("a brick", new TableRules());
        Assert.Equal(6 + rule.CloseCombatBonusDice, underTheCeiling.Rank);

        Assert.Contains(swungRope.Lines, l =>
            string.Equals(l.Rule, "scenery_as_weapons", StringComparison.Ordinal)
            && l.Text.Contains($"+{rule.CloseCombatBonusDice}d", StringComparison.Ordinal)
            && l.Text.Contains(rule.DegradationAppliesTo, StringComparison.Ordinal));

        // The control: a weapon Chapter 6 prints is Chapter 6's, and p.108 never answers for it.
        Assert.DoesNotContain(Swings("a battle axe", table).Lines, l =>
            string.Equals(l.Rule, "scenery_as_weapons", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Where both chapters could answer, Chapter 6 does — and that reading needs a phrase both
    /// of them match.</b>
    ///
    /// <para><c>ImprovisedFrom</c>'s own comment says a wooden club is p.89's <c>Club</c> and not
    /// p.107's <c>Wood</c>: "the manufactured weapon is what the caller is holding and the material
    /// is what it happens to be made of". Nothing drove it. Every fixture named either a printed
    /// weapon Chapter 7 does not rate ("a battle axe") or a thing Chapter 6 does not print ("a
    /// brick"), so <b>deleting the Chapter 6 test from <c>ImprovisedFrom</c> altogether left the
    /// whole suite green</b> — the precedence was carrying nothing an instrument could see.</para>
    ///
    /// <para><b>"A steel mace" is the phrase that separates them</b>, and the arithmetic is the
    /// instrument rather than the ledger's wording: p.89's Mace is +2d on the capped Trait, and
    /// p.107's Steel would be +1d under a ceiling of its own, so the two readings differ by a die
    /// and only one of them writes a p.108 line. The control is "a steel girder", where Chapter 6
    /// prints nothing and the same Steel row does answer — without it, the first half would be
    /// satisfied by a matcher that had stopped finding materials at all.</para>
    ///
    /// <para><b>An item Chapter 6 prints ambiguously stays ambiguous</b>, which is the second half
    /// of the same sentence: falling through to a different chapter's figure would answer a question
    /// the ledger has just said is the GM's.</para>
    /// </summary>
    [Fact]
    public void APrintedWeaponAnswersBeforeTheMaterialItIsMadeOf()
    {
        var mace = _play.GetEquipment("ancient_weapons").Weapons!
            .Single(w => string.Equals(w.Name, "Mace", StringComparison.Ordinal));

        var steel = _play.GetEnvironment("smashing_table").SmashingTable!.Rows
            .Single(r => r.Materials.Contains("Steel", StringComparer.Ordinal)).Structure;

        var improvised = _play.GetEnvironment("scenery_as_weapons").SceneryAsWeapons!;
        var ceiling = new TableRules().GearLimit(_play);

        // The two readings have to disagree, or this fixture is driving one answer twice.
        Assert.NotEqual(mace.BonusDice, improvised.CloseCombatBonusDice);
        Assert.True(steel + improvised.CapBonusDice > ceiling + improvised.CloseCombatBonusDice,
            "p.108's own ceiling would bind on Steel, so the improvised answer is not the one this "
            + "fixture is telling apart from Chapter 6's");

        var manufactured = Swings("a steel mace", new TableRules());

        Assert.Equal(ceiling + mace.BonusDice, manufactured.Rank);

        Assert.DoesNotContain(manufactured.Lines, l =>
            string.Equals(l.Rule, "scenery_as_weapons", StringComparison.Ordinal));

        Assert.Contains(manufactured.Lines, l =>
            string.Equals(l.Rule, "gear_limit", StringComparison.Ordinal)
            && l.Text.Contains($"Mace at +{mace.BonusDice}d", StringComparison.Ordinal));

        // The control: the same material, in a phrase Chapter 6 prints nothing for.
        var raw = Swings("a steel girder", new TableRules());

        Assert.Equal(ceiling + improvised.CloseCombatBonusDice, raw.Rank);

        Assert.Contains(raw.Lines, l =>
            string.Equals(l.Rule, "scenery_as_weapons", StringComparison.Ordinal)
            && l.Text.Contains("Steel", StringComparison.Ordinal));

        // And the ambiguous half: two printed weapons of the same length tie, and Chapter 7 is not
        // asked to break a tie Chapter 6 made.
        var tied = Swings("a steel shield and dagger", new TableRules());

        Assert.Equal(ceiling, tied.Rank);

        Assert.DoesNotContain(tied.Lines, l =>
            string.Equals(l.Rule, "scenery_as_weapons", StringComparison.Ordinal));

        Assert.Contains(tied.Lines, l =>
            string.Equals(l.Rule, "gear_limit", StringComparison.Ordinal)
            && l.Text.Contains("is the GM's", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A thrown object costs the other printed die, and p.74 finally has the operand it wants.</b>
    ///
    /// <para><c>throwing_range.rank_formula</c> is "throwing rank = Might - the object's weight rank"
    /// and nothing in Chapters 3-5 has ever had a weight rank — which is why p.78's knockback reads
    /// a thrown character's throwing rank as the attack rank instead. p.108's Massive Objects table
    /// prints one, and Ch.2 p.17 is what settles that the column is on that scale at all.</para>
    ///
    /// <para><b>The reach is a limit, and both sides of it are driven.</b> A Might of 22 throwing the
    /// Statue of Liberty is 22 less 14, which p.74's table puts in its Distant Range row; the same
    /// throw with a Might of 15 is a 1, which is under the rank the table is used from at all and so
    /// reaches Close Range, and a target at Distant Range is then out of reach and the attack is
    /// refused with nothing rolled.</para>
    /// </summary>
    [Fact]
    public void AThrownMassiveObjectReachesAsFarAsPageSeventyFourSaysAndNoFurther()
    {
        var scenery = _play.GetEnvironment("scenery_as_weapons").SceneryAsWeapons!;
        var weight = _play.GetEnvironment("massive_objects_table").MassiveObjectsTable!
            .Single(r => r.Objects.Contains("Statue of Liberty", StringComparer.Ordinal)).WeightRank;

        var thrown = Throws("the Statue of Liberty", might: 22, at: RangeBand.Distant);

        var line = Assert.Single(thrown.Lines, l =>
            string.Equals(l.Rule, "throwing_range", StringComparison.Ordinal));

        Assert.Contains($"weight rank of {weight}", line.Text, StringComparison.Ordinal);
        Assert.Contains($"of 22 less {weight} is {22 - weight}", line.Text, StringComparison.Ordinal);
        Assert.Contains(thrown.Lines, l =>
            string.Equals(l.Rule, "scenery_as_weapons", StringComparison.Ordinal)
            && l.Text.Contains($"+{scenery.ThrownAttackBonusDice}d", StringComparison.Ordinal));

        // The positive control: that throw actually resolved.
        Assert.Contains(thrown.Lines, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));

        // And the other side of the threshold: too weak to get it that far, so nothing is rolled.
        var short_ = Throws("the Statue of Liberty", might: 15, at: RangeBand.Distant);

        Assert.DoesNotContain(short_.Lines, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));

        Assert.Contains(short_.Lines, l =>
            string.Equals(l.Rule, "throwing_range", StringComparison.Ordinal)
            && l.Text.Contains("farther than that", StringComparison.Ordinal));

        // The same weak throw at Close Range is inside the reach, which is the control that the
        // refusal is about the distance and not about the object.
        Assert.Contains(Throws("the Statue of Liberty", might: 15, at: RangeBand.Close).Lines, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>"Whatever you hit with a mountain, you hit once", and the second swing is refused.</b>
    ///
    /// <para><c>massive_objects.always_breaks_apart_after</c> is "the first shot", and this engine
    /// already has somewhere for that to land: p.76's page turn drops an object nobody used, so a
    /// thrown freight train is one nobody is holding afterwards. Naming the clause and leaving the
    /// train in place would let it be thrown again every page for the rest of the fight, which is
    /// exactly the "announced but never applied" this ledger exists to prevent.</para>
    ///
    /// <para>The control is a motorcycle, which is on the Scenery table rather than this one and
    /// stays in the wielder's hands — so the drop is the rule and not the engine forgetting.</para>
    /// </summary>
    [Fact]
    public void AMassiveObjectBreaksApartAfterOneShotAndTheSecondIsRefused()
    {
        var entry = _play.GetEnvironment("massive_objects");

        var first = Throws("a freight train", might: 24, at: RangeBand.Close);

        Assert.Contains(first.Lines, l =>
            string.Equals(l.Rule, entry.Id, StringComparison.Ordinal)
            && l.Text.Contains(entry.MassiveObjects!.AlwaysBreaksApartAfter, StringComparison.Ordinal));

        Assert.Null(first.State["hero"].Holding);

        // The control: an object the sentence is not printed against is still in hand.
        Assert.NotNull(Throws("a motorcycle", might: 24, at: RangeBand.Close).State["hero"].Holding);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// One exchange, and how many dice it threw — the instrument
    /// <see cref="PlayEngineStepTests"/> uses for every modifier, for the reason its own comment
    /// gives: a pool parsed out of a ledger sentence is a test of the sentence.
    /// </summary>
    private (int Thrown, IReadOnlyList<LedgerLine> Lines) Exchange(Attack attack, int attackRank = 12)
    {
        const int Plenty = 300;

        var dice = new ScriptedDice([.. Enumerable.Repeat(4, Plenty)]);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin(Pair(attackRank));

        Assert.Equal(Plenty, dice.Remaining);

        var step = encounter.Step(state, attack);
        var thrown = Plenty - dice.Remaining;

        Assert.True(thrown > 0,
            "the exchange threw no dice, so it was refused rather than resolved and every "
            + "comparison built on it would be a comparison of nothing");

        return (thrown, step.Added);
    }

    /// <summary>
    /// One attack that has to be refused before anything is rolled, with the dice source's own
    /// count as the proof rather than the ledger's wording.
    /// </summary>
    private IReadOnlyList<LedgerLine> Refused(Attack attack)
    {
        const int Plenty = 300;

        var dice = new ScriptedDice([.. Enumerable.Repeat(4, Plenty)]);
        var encounter = new Encounter(_play, dice);
        var state = encounter.Begin(Pair(12));

        var step = encounter.Step(state, attack);

        Assert.Equal(Plenty, dice.Remaining);

        return step.Added;
    }

    /// <summary>
    /// An attacker with a Might big enough to get through every obstacle these fixtures name, and a
    /// target whose own defence is small enough that a wall always out-ranks it — so the dice count
    /// moves with the Structure and with nothing else.
    /// </summary>
    private static Combatant[] Pair(int attackRank) =>
    [
        Combatant.Hero(
            "hero", "the Hero", edge: 9, health: 12, resolve: 1,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = attackRank },
            [], side: "heroes"),
        Combatant.Villain(
            "villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 1 },
            ["toughness"], side: "villains")
    ];

    /// <summary>
    /// One close-combat swing with a held object, and the attack rank it brought to bear — read off
    /// the dice thrown less the defence's own, which is the instrument every modifier fixture here
    /// uses.
    /// </summary>
    private (int Rank, IReadOnlyList<LedgerLine> Lines) Swings(string item, TableRules table)
    {
        const int Plenty = 300;
        const int Defence = 1;

        var dice = new ScriptedDice([.. Enumerable.Repeat(4, Plenty)]);
        var encounter = new Encounter(_play, dice, table);

        var hero = Combatant.Hero(
            "hero", "the Hero", edge: 9, health: 12, resolve: 1,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 12 },
            [], side: "heroes").Carrying(item);

        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = Defence * 2 },
            ["toughness"], side: "villains");

        var state = encounter.Begin([hero, villain]);

        var step = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.MeleeWeapon, Item: item));

        // The Villain's Toughness answers a melee weapon at half, so the defence throws exactly one
        // die and the rest of what was thrown is the attack's own rank.
        var thrown = Plenty - dice.Remaining;

        Assert.True(thrown > Defence, "the swing was refused rather than resolved");

        return (thrown - Defence, step.Added);
    }

    /// <summary>One thrown object at a target a given distance away.</summary>
    private (EncounterState State, IReadOnlyList<LedgerLine> Lines) Throws(
        string item, int might, RangeBand at)
    {
        var dice = new ScriptedDice([.. Enumerable.Repeat(4, 300)]);
        var encounter = new Encounter(_play, dice);

        var hero = Combatant.Hero(
            "hero", "the Hero", edge: 9, health: 12, resolve: 1,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = might },
            [], side: "heroes").Carrying(item);

        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["toughness"] = 2 },
            ["toughness"], side: "villains");

        var state = encounter.Begin([hero, villain], opening: at);

        var step = encounter.Step(state, new Attack(
            "hero", "villain", "might", DamageKind.Subdual, AttackType.RangedWeapon, Item: item));

        return (step.State, step.Added);
    }
}
