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
}
