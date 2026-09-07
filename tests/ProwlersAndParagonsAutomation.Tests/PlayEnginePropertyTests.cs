using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>What has to be true of every fight, not only of the ones the book prints.</b>
///
/// <para>The fixtures beside this file prove the mechanics against the authors' own arithmetic, one
/// case each. These prove the properties no worked example can: that a run always comes back, that
/// Health does not fall through a floor the rules do not have, that a step never modifies the state
/// it was given, and that an encounter cannot touch a character sheet. They are driven with
/// <see cref="SeededDice"/> across a spread of seeds, so a defect that needs a particular roll to
/// show has somewhere to show.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlayEnginePropertyTests
{
    private readonly PlayRulesRepository _play;

    public PlayEnginePropertyTests(PlayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _play = fixture.Play;
    }

    private const int MaxPages = 25;

    public static TheoryData<int> Seeds()
    {
        var data = new TheoryData<int>();
        for (var seed = 1; seed <= 25; seed++) data.Add(seed);
        return data;
    }

    /// <summary>
    /// <b>An encounter always terminates inside its page limit.</b>
    ///
    /// <para>The limit is what makes that true rather than an argument that it would be: two
    /// combatants who cannot get through each other's defences would fight for ever, and a simulator
    /// that hangs is worse than one that reports a draw. The run is also required to have <em>done
    /// something</em> — a loop that exited on its first pass would satisfy "it came back" perfectly
    /// while proving nothing, which is the shape of three of this repository's four historical guard
    /// faults.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AnEncounterAlwaysTerminatesInsideItsPageLimit(int seed)
    {
        var encounter = new Encounter(_play, new SeededDice(seed));
        var final = encounter.RunToEnd(encounter.Begin(Party()), new AttackTheWeakest(_play), MaxPages);

        Assert.True(final.Over);
        Assert.True(final.Page <= MaxPages + 1, $"the run reached page {final.Page}");

        // The positive control: something happened. An encounter that ended on page one with nobody
        // having rolled anything would pass every assertion above.
        Assert.True(final.Ledger.Lines.Count > 10,
            $"the run produced only {final.Ledger.Lines.Count} ledger lines, so it did nothing.");

        Assert.Contains(final.Ledger.Lines, l =>
            string.Equals(l.Rule, "attacks_and_defenses", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>With Fatal Damage off, Health stops at the figure <c>damage.defeated_at_health</c> names.</b>
    ///
    /// <para>p.75 is explicit that nothing worse than being knocked out happens unless the table has
    /// switched the optional harsher rules on, so a run under the book's baseline that produced a
    /// negative Health would be applying a rule the table did not take. The floor is read off the
    /// entry rather than typed as zero.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void HealthNeverFallsBelowTheDefeatFloorWhileFatalDamageIsOff(int seed)
    {
        var encounter = new Encounter(_play, new SeededDice(seed));
        var floor = encounter.DefeatFloor;

        Assert.False(TableRules.Book.FatalDamage, "the baseline has every gritty rule off");

        var final = encounter.RunToEnd(encounter.Begin(Party()), new AttackTheWeakest(_play), MaxPages);

        Assert.All(final.Combatants.Values, c =>
            Assert.True(c.CurrentHealth >= floor,
                $"{c.Name} is on {c.CurrentHealth} Health with Fatal Damage off, and the floor is {floor}"));

        // And the control on the other side: somebody actually lost Health, or the assertion above
        // is true of a fight in which nothing landed.
        Assert.Contains(final.Combatants.Values, c => c.CurrentHealth < c.FullHealth || c.GroupSize == 0);
    }

    /// <summary>
    /// <b>A step never modifies the state it was given.</b>
    ///
    /// <para>Compared by serialising the state before the step and again after it, rather than by
    /// reading a few fields: the realistic failure is somebody adding a collection and reaching for
    /// <c>Add</c>, which no hand-written field comparison would have been extended to cover.</para>
    ///
    /// <para>The control is that the step actually produced a different state — an engine whose
    /// <c>Step</c> returned its argument unchanged would satisfy the immutability assertion
    /// perfectly.</para>
    ///
    /// <para><b>It is driven by <see cref="RandomPolicy"/> and not by the shipped one, because a
    /// property is only as wide as what drives it.</b> <see cref="AttackTheWeakest"/> emits an
    /// attack, a hold and a reroll: this used to be a purity claim about four of the ten intent
    /// types, with every grapple, every other purchase, the stabilisation roll and half the
    /// refusals unexamined. The generator emits all of them, including purchases the actor cannot
    /// afford and moves that make no sense where they are, because a refusal is a branch of
    /// <c>Step</c> like any other. Its own list of what it emitted is the second control: a
    /// generator that had quietly narrowed would fail here rather than pass with less to say. It
    /// cycles the GM's four purchases as well, half of them carrying the narration p.85's three own
    /// spends are refused without, so both sides of that refusal are inside the property.</para>
    ///
    /// <para>Both table settings that change what <c>Step</c> reaches for are on, so the Fatal
    /// Damage clock and the wound penalties are inside the property rather than beside it.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AStepNeverChangesTheStateItWasGiven(int seed)
    {
        var table = TableRules.Book with { FatalDamage = true, WoundPenalties = true };
        var encounter = new Encounter(_play, new SeededDice(seed), table);

        var state = encounter.Begin(Party()) with { Table = table };

        var policy = new RandomPolicy(new SeededDice(seed * 7919));
        var moved = false;

        for (var i = 0; i < 60 && !state.Over; i++)
        {
            var actor = state.Current;

            if (actor is null)
            {
                state = StepAndCheck(encounter, state, new EndPage(""), ref moved);
                continue;
            }

            state = StepAndCheck(encounter, state, policy.Choose(state, actor), ref moved);

            if (policy.AfterRoll(state, state[actor.Id]) is { } follow)
                state = StepAndCheck(encounter, state, follow, ref moved);

            state = StepAndCheck(encounter, state, new EndTurn(actor.Id), ref moved);
        }

        Assert.True(moved, "no step changed the state at all, so the comparison proved nothing.");

        // The second control: the generator really did reach every kind of intent it claims to —
        // and every Resolve purchase, which is what makes this a statement about Chapter 4's four
        // as well as Chapter 5's six.
        Assert.Equal(
            [
                nameof(Attack), nameof(BreakFree), nameof(GrappleIntent), nameof(Hold),
                nameof(Move), nameof(SpendAdversity), nameof(SpendResolve), nameof(Stabilise)
            ],
            policy.Emitted.Order(StringComparer.Ordinal));

        Assert.Equal(Enum.GetValues<ResolveSpend>().Order(), policy.Purchases.Order());

        // And every Adversity purchase, which is what makes this a statement about p.85's three own
        // spends as well as its first: each is emitted with the GM's words and without them, so the
        // branch that changes the state and the branch that refuses are both inside the property.
        Assert.Equal(Enum.GetValues<AdversitySpend>().Order(), policy.GmPurchases.Order());
    }

    private static EncounterState StepAndCheck(
        Encounter encounter, EncounterState state, Intent intent, ref bool moved)
    {
        var before = Serialise(state);
        var result = encounter.Step(state, intent);
        var after = Serialise(state);

        Assert.Equal(before, after);

        if (!string.Equals(before, Serialise(result.State), StringComparison.Ordinal)) moved = true;

        return result.State;
    }

    private static readonly JsonSerializerOptions SerialisationOptions = new() { IncludeFields = false };

    private static string Serialise(EncounterState state) =>
        JsonSerializer.Serialize(state, SerialisationOptions);

    /// <summary>
    /// <b>An encounter cannot touch a character sheet.</b>
    ///
    /// <para>The sheet is round-tripped through <see cref="CharacterSheetJson"/> before a whole fight
    /// and again after it, and the two strings have to be identical. That is the strongest available
    /// statement of the rule the architecture rests on: <see cref="CombatantFactory"/> is the one
    /// place a sheet is read, everything after it works on immutable snapshots, and the first engine
    /// stays the authority on what a character <em>is</em>.</para>
    ///
    /// <para>The control is that the fight really used the sheet — the combatant built from it has
    /// the Edge, Health and Resolve the character engine computes, so a factory that had quietly
    /// stopped reading the sheet would fail here rather than pass by touching nothing.</para>
    ///
    /// <para><b>The sheet carries Powers, and it did not.</b> The round trip used to run against a
    /// character with an empty <c>SelectedPowers</c>, which is the one shape of sheet for which the
    /// whole of <c>CombatantFactory.TraitRanks</c>'s Power loop never executes — so the guard proved
    /// the factory does not modify a sheet it had barely read. Three Powers go on it now, one of them
    /// with a Pro and one bought per unit, because those are the fields
    /// <see cref="DerivedStatsCalculator.GetEffectiveRank"/> reaches for and the ones a factory
    /// mutating a sheet would mutate.</para>
    /// </summary>
    [Fact]
    public void AnEncounterLeavesTheCharacterSheetByteIdentical()
    {
        var rules = new RulesFixture();
        var derived = rules.Derived;
        var sheet = rules.LegalSheet();

        sheet.Name = "The Subject";
        sheet.AbilityRanks["might"] = 8;
        sheet.AbilityRanks["toughness"] = 6;
        sheet.AbilityRanks["agility"] = 5;
        sheet.AbilityRanks["perception"] = 4;

        sheet.SelectedPowers.Add(new SelectedPower("armor", 5, [new SelectedProCon("penetrating")], []));
        sheet.SelectedPowers.Add(new SelectedPower("running", 4));
        sheet.SelectedPowers.Add(new SelectedPower("immunity", 1) { Units = 3 });

        var before = CharacterSheetJson.Write(sheet);

        var hero = CombatantFactory.From(sheet, rules.Rules, derived, _play, CombatantKind.Hero, "subject");

        // The control: the snapshot really came from the sheet, through the character engine — and
        // through the Power loop, which an empty SelectedPowers would have skipped entirely.
        Assert.Equal(derived.CalculateEdge(sheet), hero.Edge);
        Assert.Equal(derived.CalculateHealth(sheet), hero.FullHealth);
        Assert.Equal(derived.CalculateResolve(sheet), hero.Resolve);
        Assert.Equal(8, hero.Rank("might"));

        foreach (var power in sheet.SelectedPowers)
            Assert.Equal(derived.GetEffectiveRank(power, sheet), hero.Rank(power.PowerId));

        // The two ranked ones came through with a rank, so the loop above is not comparing zeroes:
        // Immunity is priced per unit and is rankless by design, which is why it is on the sheet.
        Assert.True(hero.Rank("armor") > 0, "the Armor came back at 0d");
        Assert.True(hero.Rank("running") > 0, "the Running came back at 0d");
        Assert.Contains("armor", hero.Defences, StringComparer.Ordinal);

        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 9, ["toughness"] = 6 },
            ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(7));
        var final = encounter.RunToEnd(encounter.Begin([hero, villain]), new AttackTheWeakest(_play), MaxPages);

        Assert.True(final.Over);

        // Byte for byte, not "equivalent": a reordered list or a defaulted field is exactly the kind
        // of change a round trip is supposed to catch.
        Assert.Equal(before, CharacterSheetJson.Write(sheet), StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>A Foe has half a Villain's Health, rounded the way the Glossary rounds.</b> p.75 says a Foe
    /// halves the result and never says which way an odd total goes; the book-wide rule (p.7) pushes
    /// a half upward, so a Health of 5 halves to 3 and not to 2. Both directions are computed and the
    /// wrong one is required to be wrong, which is what stops this from restating itself.
    /// </summary>
    [Fact]
    public void AFoeHalvesHealthUpwardsBecauseTheGlossarySaysHalvesGoUp()
    {
        var rules = new RulesFixture();
        var sheet = rules.LegalSheet();

        sheet.Name = "Odd Health";
        sheet.AbilityRanks["toughness"] = 3;
        sheet.AbilityRanks["might"] = 2;

        var full = rules.Derived.CalculateHealth(sheet);
        Assert.Equal(3, full);   // the fixture's control: the total really is odd

        var villain = CombatantFactory.From(sheet, rules.Rules, rules.Derived, _play, CombatantKind.Villain);
        var foe = CombatantFactory.From(sheet, rules.Rules, rules.Derived, _play, CombatantKind.Foe);

        Assert.Equal(full, villain.FullHealth);
        Assert.Equal(2, foe.FullHealth);
        Assert.NotEqual(full / 2, foe.FullHealth);   // rounding down would give 1
    }

    /// <summary>
    /// <b>Only a Hero holds Resolve, and the type is what says so.</b> A spend charged to anybody
    /// else is a throw, not a silent draw on a pool that does not exist — and the engine turns that
    /// into a refusal on the ledger rather than an exception escaping into a run.
    /// </summary>
    [Fact]
    public void NobodyButAHeroCanSpendResolve()
    {
        var villain = Combatant.Villain(
            "villain", "the Villain", edge: 7, health: 12,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 9 },
            ["toughness"]);

        Assert.False(villain.HoldsResolve);
        Assert.Equal(0, villain.Resolve);
        Assert.Throws<InvalidOperationException>(() => villain.Spending(1));

        var hero = Combatant.Hero(
            "hero", "the Hero", edge: 8, health: 8, resolve: 2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 },
            ["toughness"]);

        var encounter = new Encounter(_play, new SeededDice(3));
        var state = encounter.Begin([hero, villain]);

        var result = encounter.Step(state, new SpendResolve("villain", ResolveSpend.Reroll));

        Assert.Equal(state.Adversity, result.State.Adversity);
        Assert.Contains(result.Added, l => l.Text.Contains("holds no Resolve", StringComparison.Ordinal));
    }

    /// <summary>Three Heroes against a Villain, a Foe and a group of Minions.</summary>
    private static List<Combatant> Party()
    {
        var heroes = Enumerable.Range(1, 3).Select(i => Combatant.Hero(
            $"hero{i}", $"Hero {i}", edge: 8 + i, health: 8, resolve: 4,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 8, ["toughness"] = 6, ["agility"] = 5, ["blast"] = 7
            },
            ["toughness", "agility"]));

        return
        [
            .. heroes,
            Combatant.Villain(
                "villain", "the Villain", edge: 10, health: 14,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["might"] = 10, ["toughness"] = 8, ["agility"] = 6
                },
                ["toughness", "agility"]),
            Combatant.Foe(
                "foe", "the Foe", edge: 7, health: 6,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["might"] = 7, ["toughness"] = 5, ["agility"] = 4
                },
                ["toughness", "agility"]),
            Combatant.Minions("minions", "the Minions", threat: 4, groupSize: 6, "threat")
        ];
    }
}
