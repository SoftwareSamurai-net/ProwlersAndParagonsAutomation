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

    /// <summary>
    /// The seeds every property here is driven with, as figures — so a fixture that has to sweep
    /// the whole range rather than be parameterised over it walks the same list rather than a
    /// second copy of it.
    /// </summary>
    private static IEnumerable<int> SeedRange => Enumerable.Range(1, 25);

    public static TheoryData<int> Seeds()
    {
        var data = new TheoryData<int>();
        foreach (var seed in SeedRange) data.Add(seed);
        return data;
    }

    /// <summary>
    /// <b>p.76's full grab is reached somewhere in the seed range, and this is where that is
    /// measured rather than assumed.</b>
    ///
    /// <para><see cref="AStepNeverChangesTheStateItWasGiven"/> cannot claim it. A full grab needs
    /// three net successes on a Might contest between characters of the same Might, so no seed can
    /// be made to produce one — and the control there that a toss dropped what its actor was
    /// holding is satisfied by the combatants who <em>open</em> the fight holding something, whether
    /// or not a grab ever landed. Read as a claim about the band it would be a control that cannot
    /// fail for the reason it appears to be about, which is this repository's most common guard
    /// fault: a feature that did not run mistaken for a feature that worked.</para>
    ///
    /// <para>So the band is measured once, over the whole range, and the failure message names the
    /// count. <b>The driver here is deliberately narrower</b> than the property's — no toss hook, no
    /// GM purchases, no Resolve — because its subject is not purity: it is whether the generator and
    /// the engine between them still reach the three branches that only a full grab opens, which are
    /// the item changing hands, an attack made with it, and the page turn taking it away.</para>
    /// </summary>
    [Fact]
    public void AFullGrabIsReachedSomewhereInTheSeedRange()
    {
        var landed = new List<int>();
        var aimedAtAHolder = new List<int>();

        foreach (var seed in SeedRange)
        {
            var encounter = new Encounter(_play, new SeededDice(seed));
            var state = encounter.Begin(Party());
            var policy = new RandomPolicy(new SeededDice(seed * 7919));

            for (var i = 0; i < 60 && !state.Over; i++)
            {
                if (state.Current is not { } actor)
                {
                    state = encounter.Step(state, new EndPage("")).State;
                    continue;
                }

                state = encounter.Step(state, policy.Choose(state, actor)).State;

                if (state.Grapples.Any(g => g.Move == GrappleMove.Grab && g.Kind == GrappleKind.Full)
                    && !landed.Contains(seed))
                {
                    landed.Add(seed);
                }

                state = encounter.Step(state, new EndTurn(actor.Id)).State;
            }

            if (policy.GrabbedWhatTheyHeld.Contains(true)) aimedAtAHolder.Add(seed);
        }

        // The first control, and the one the guard above turns on: the generator aims a grab at a
        // combatant who is actually holding something. Without it every grab in every fixture here
        // is the refusal, and the band below could never be reached at all.
        Assert.True(
            aimedAtAHolder.Count > 0,
            "no seed aimed a grab at a combatant who was holding anything, so every grab in the "
            + "property was refused before a die was thrown — see Party(), which is what puts the "
            + "objects in the fight.");

        Assert.True(
            landed.Count > 0,
            $"no seed in 1..25 landed a full grab. {aimedAtAHolder.Count} of them aimed one at a "
            + "combatant who was holding something, so the generator is reaching the move; what is "
            + "not being reached is p.76's three-net-success band, and the branches that only a "
            + "full grab opens are outside every property here.");
    }

    /// <summary>
    /// <b>p.87's Gear Limit is reached by the property's own generator, and it is measured over the
    /// whole seed range rather than assumed.</b>
    ///
    /// <para>The generator names an item on half its attacks and picks a row of p.75's table at
    /// random, so an attack that is <em>both</em> item-backed and on one of the two weapon rows is
    /// a conjunction no single seed can be made to produce. Read as a per-seed control that would
    /// be a check that cannot fail for the reason it appears to be about, which is this
    /// repository's most common guard fault.</para>
    ///
    /// <para>Two things are measured, because the cap and the bonus are different branches: that
    /// some attack was capped at all, and that some attack was matched to a weapon Chapter 6 prints.
    /// The party walks in carrying a sword, a wand and a club, and two of those three are printed
    /// types — which is the point of taking the object off the generator rather than naming one
    /// here.</para>
    ///
    /// <para><b>This control has already earned its keep.</b> The generator named a fixed object on
    /// half its attacks whoever was swinging, so every swing but one combatant's was refused for
    /// empty hands and not one attack in twenty-five seeds reached the branch at all — see
    /// <c>RandomPolicy.Swung</c>.</para>
    /// </summary>
    [Fact]
    public void TheGearLimitIsReachedSomewhereInTheSeedRange()
    {
        var capped = new List<int>();
        var priced = new List<int>();

        foreach (var seed in SeedRange)
        {
            var encounter = new Encounter(_play, new SeededDice(seed));
            var state = encounter.Begin(Party());
            var policy = new RandomPolicy(new SeededDice(seed * 7919));

            for (var i = 0; i < 60 && !state.Over; i++)
            {
                if (state.Current is not { } actor)
                {
                    state = encounter.Step(state, new EndPage("")).State;
                    continue;
                }

                state = encounter.Step(state, policy.Choose(state, actor)).State;
                state = encounter.Step(state, new EndTurn(actor.Id)).State;
            }

            var lines = state.Ledger.Lines
                .Where(l => string.Equals(l.Rule, "gear_limit", StringComparison.Ordinal))
                .ToList();

            if (lines.Exists(l => l.Text.Contains("comes to bear as", StringComparison.Ordinal)))
                capped.Add(seed);

            if (lines.Exists(l => l.Text.Contains(" at +", StringComparison.Ordinal)))
                priced.Add(seed);
        }

        Assert.True(
            priced.Count > 0,
            "no seed made an attack with a weapon Chapter 6 prints, so the Weapon Bonus branch is "
            + "outside every property here. Check that RandomPolicy still swings what its actor is "
            + "holding, and that Party() still walks in with an object one of the tables prints.");

        Assert.True(
            capped.Count > 0,
            $"{priced.Count} seeds priced a weapon and none of them was capped, so p.87's ceiling "
            + "never bit inside the property. Check that Party() still carries a Trait above the "
            + "Gear Limit.");
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
    ///
    /// <para><b>And so is p.75's whole MODIFIERS block</b>, which is three more things
    /// <c>Step</c> reaches for and which would otherwise be outside every property here: the fight
    /// is fought in the dark, the party carries three sizes and one invisible combatant, and the
    /// generator cycles all five bands of cover with and without a Structure to attack through. The
    /// refusals those produce are branches of <c>Step</c> like any other.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AStepNeverChangesTheStateItWasGiven(int seed)
    {
        var table = TableRules.Book with
        {
            FatalDamage = true, WoundPenalties = true, HardTargets = true,
            CloseRangePenalty = true, TheDrop = true, FriendlyFire = true, SlowHealing = true
        };
        var encounter = new Encounter(_play, new SeededDice(seed), table);

        var state = encounter.Begin(Party(), visibility: Visibility.Poor) with { Table = table };

        var policy = new RandomPolicy(new SeededDice(seed * 7919));
        var moved = false;
        var reached = new HashSet<ResolveSpend>();

        for (var i = 0; i < 60 && !state.Over; i++)
        {
            var actor = state.Current;

            if (actor is null)
            {
                state = StepAndCheck(encounter, state, new EndPage(""), ref moved);
                continue;
            }

            state = StepAndCheck(encounter, state, policy.Choose(state, actor), ref moved);

            // p.76's toss, on every turn — see the hook's own comment for why it is not another
            // branch of the cycle. It names what the actor is holding when they are holding
            // anything, so both the drop and the refusal are inside the property.
            state = StepAndCheck(encounter, state, policy.Tosses(state[actor.Id]), ref moved);

            if (policy.AfterRoll(state, state[actor.Id]) is { } follow)
                state = StepAndCheck(encounter, state, follow, ref moved);

            // p.85's first purchase, on every turn, naming the next of a Hero's ten on behalf of an
            // NPC — see the hook's own comment for why it is not another branch of the cycle.
            if (policy.GmBuysForAnNpc(state) is { } gm)
            {
                state = StepAndCheck(encounter, state, gm, ref moved, out var said);

                // <b>What the generator named and what <c>Step</c> reached are different facts, and
                // this is where they are told apart.</b> p.85's pool gate refuses a spend before any
                // purchase is dispatched, so a run whose pool has run dry names all ten and reaches
                // none of them — and a control counting names would be satisfied by ten refusals.
                if (!said.Any(l => l.Text.Contains(ForWantOfAPoint, StringComparison.Ordinal)))
                    reached.Add(gm.AsResolve!.Value);
            }

            state = StepAndCheck(encounter, state, new EndTurn(actor.Id), ref moved);
        }

        Assert.True(moved, "no step changed the state at all, so the comparison proved nothing.");

        // The second control: the generator really did reach every kind of intent it claims to —
        // and every Resolve purchase, which is what makes this a statement about Chapter 4's four
        // as well as Chapter 5's six.
        Assert.Equal(
            [
                nameof(Attack), nameof(BreakFree), nameof(GrappleIntent), nameof(Hold),
                nameof(Move), nameof(SpendAdversity), nameof(SpendResolve), nameof(Stabilise),
                nameof(Toss)
            ],
            policy.Emitted.Order(StringComparer.Ordinal));

        Assert.Equal(Enum.GetValues<ResolveSpend>().Order(), policy.Purchases.Order());

        // And every band of p.75's cover, including the one that refuses before anything is rolled.
        Assert.Equal(Enum.GetValues<Cover>().Order(), policy.Covers.Order());

        // p.76's grab and the item it wins, on both sides of each of its refusals: a grab that names
        // an object and one that names none, a grab aimed at what its target is holding and at what
        // they are not, an attack made with an item and one made with nothing, and a toss of what
        // the actor is holding as well as of what they are not.
        Assert.Equal([false, true], policy.GrabbedItems.Order());
        Assert.Equal([false, true], policy.SwungItems.Order());
        Assert.Contains(true, policy.TossedWhatTheyHeld);
        Assert.Contains(false, policy.TossedWhatTheyHeld);

        // <b>What no seed can promise is said here rather than implied.</b> A full grab needs three
        // net successes on a Might contest between characters of the same Might, and across this
        // range exactly one seed lands one — so `TossedWhatTheyHeld` carrying true is satisfied by
        // the combatants who open the fight holding something, whether or not a grab ever landed.
        // It is a control on the generator reaching the branch, not on the engine reaching the
        // band, and the band is measured once over the whole range by
        // AFullGrabIsReachedSomewhereInTheSeedRange below.

        // p.80's Hard Targets is inside the property too: somebody in the fight is a machine, and
        // the generator both aims at a weak point and does not.
        Assert.Contains(state.Combatants.Values, c => c.HardTarget);
        Assert.Equal([false, true], policy.WeakPoints.Order());

        // p.79's Close Range too: the generator declares a thrown weapon and does not, and the
        // party carries a Power whose own Range reaches past the nearest band.
        Assert.Equal([false, true], policy.ThrownWeapons.Order());
        Assert.Contains(state.Combatants.Values, c => c.RangedPowers.Count > 0);

        // p.79's Drop is inside it too: somebody has a weapon levelled and somebody has not, which
        // is the only arrangement in which the doubling reaches the order at all.
        Assert.Contains(state.Combatants.Values, c => c.Ready);
        Assert.Contains(state.Combatants.Values, c => !c.Ready);

        // The light really was bad and somebody really was invisible, or the paragraph above
        // describes a fight this property did not run.
        Assert.Equal(Visibility.Poor, state.Visibility);
        Assert.Contains(state.Combatants.Values, c => c.Invisible);
        Assert.True(
            state.Combatants.Values.Select(c => c.Size).Distinct().Count() > 1,
            "every combatant in the property's party is the same size, so no size band was reached");

        // And every Adversity purchase, which is what makes this a statement about p.85's three own
        // spends as well as its first: each is emitted with the GM's words and without them, so the
        // branch that changes the state and the branch that refuses are both inside the property.
        Assert.Equal(Enum.GetValues<AdversitySpend>().Order(), policy.GmPurchases.Order());

        // <b>And every purchase p.85's first spend may name, out of the GM's pool.</b> That the
        // party bought all ten with their own Resolve says nothing about the ten the GM's pool now
        // buys for an NPC: they are different branches, and until this control existed every
        // anything_resolve_can the generator emitted named nothing and was refused for it — so the
        // whole of that purchase sat outside a property whose subject is every branch of Step.
        Assert.Equal(Enum.GetValues<ResolveSpend>().Order(), policy.GmNamed.Order());

        // <b>And every one of them got past p.85's pool gate, which is the half the set above
        // cannot say.</b> <c>GmNamed</c> records what the generator asked for, which is a fact about
        // the generator; the gate refuses a spend before any purchase is dispatched, so a fight
        // whose pool had run dry would name all ten and enter none of their branches — and a control
        // counting names would read ten refusals as ten branches exercised. That is the shape of
        // guard fault this repository has shipped four times: a feature that did not run mistaken
        // for one that worked.
        //
        // <b>The pool holds today because most of these purchases are refused by their own rules —
        // no roll on the table, nobody down, nobody dying — and a refusal costs nothing.</b> That is
        // an arithmetic accident of this fight rather than a property of the engine, which is
        // exactly why it is measured here instead of assumed. Watched red by opening the fight on a
        // pool of 0.
        var unreached = Enum.GetValues<ResolveSpend>().Except(reached).Order().ToList();

        Assert.True(
            unreached.Count == 0,
            $"the GM's pool did not pay for {string.Join(", ", unreached)}: each was named by the "
            + "generator and refused for want of a point before p.85's first purchase dispatched "
            + "anything, so the branch it names was never entered. The fight has to open on enough "
            + "Adversity to reach the branches this property claims to cover — raise the Challenge "
            + "Level it begins with, or top the pool up between turns.");
    }

    private static EncounterState StepAndCheck(
        Encounter encounter, EncounterState state, Intent intent, ref bool moved) =>
        StepAndCheck(encounter, state, intent, ref moved, out _);

    private static EncounterState StepAndCheck(
        Encounter encounter, EncounterState state, Intent intent, ref bool moved,
        out IReadOnlyList<LedgerLine> added)
    {
        var before = Serialise(state);
        var result = encounter.Step(state, intent);
        var after = Serialise(state);

        Assert.Equal(before, after);

        if (!string.Equals(before, Serialise(result.State), StringComparison.Ordinal)) moved = true;

        added = result.Added;

        return result.State;
    }

    /// <summary>
    /// The refusal p.85's pool gate makes before any purchase is dispatched, as
    /// <c>ResolveAdversitySpend</c> writes it: "the GM has 0 Adversity and the spend costs 1".
    ///
    /// <para><b>It is matched on because a purchase refused here never reached the branch it
    /// names.</b> The generator records what it asked for, which is a fact about the generator; this
    /// phrase is what separates that from a fact about <see cref="Encounter.Step"/>.</para>
    /// </summary>
    private const string ForWantOfAPoint = "Adversity and the spend costs";

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

    /// <summary>
    /// Three Heroes against a Villain, a Foe and a group of Minions — <b>one of them holding
    /// something</b>.
    ///
    /// <para><b>The items are here because a full grab is not something a seed can promise.</b> p.76
    /// needs three net successes on a Might contest, the generator reaches a grab on one turn in ten
    /// and names an object on half of those, and across twenty-five seeds exactly one fight lands
    /// one — so the branches that <em>read</em> <see cref="Combatant.Holding"/> (the toss that drops
    /// an item, the attack made with one) sat outside a property whose whole subject is every branch
    /// of <c>Step</c>.</para>
    ///
    /// <para><b>One item was not enough once a grab had to be aimed at somebody who has one.</b> The
    /// toss hook fired on every turn, so the Hero who opened with the sword threw it away on their
    /// first, and every grab for the rest of the fight was refused before a die was thrown. Three
    /// combatants walk in holding something and the hook now tosses on one turn in three, which is
    /// what makes <see cref="AFullGrabIsReachedSomewhereInTheSeedRange"/> reach the band at all.
    /// </para>
    ///
    /// <para><b>And it is no longer a construction the wire cannot express</b>, which it was when
    /// this was written: <c>start_encounter</c> takes a <c>holding</c> on a combatant, because p.76
    /// aims a grab at an opponent who has something and nothing else could say that anybody did.
    /// </para>
    /// </summary>
    private static List<Combatant> Party()
    {
        var heroes = Enumerable.Range(1, 3).Select(i => Combatant.Hero(
            $"hero{i}", $"Hero {i}", edge: 8 + i, health: 8, resolve: 4,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["might"] = 8, ["toughness"] = 6, ["agility"] = 5, ["blast"] = 7
            },
            ["toughness", "agility"],
            // <b>A Blast, whose own Ch.2 Range is <c>ranged</c></b>, so p.79's Close Range rule is
            // inside the property rather than beside it: the generator rolls that Trait on a Power
            // row and the pair opens at Close.
            rangedPowers: new HashSet<string>(StringComparer.Ordinal) { "blast" }))
            .Select((hero, i) => i == 0 ? hero.Carrying("the sword") : hero)
            .ToList();

        // The control on that construction: exactly one Hero is holding something, so the toss
        // branches below are reached from a fact rather than from a hope.
        Assert.Single(heroes, hero => hero.Holding is not null);

        return
        [
            .. heroes,
            // <b>Five times the Heroes' size, which is p.75's widest size band</b>, so every
            // fixture and every property below is fought against somebody the bands actually reach.
            Combatant.Villain(
                "villain", "the Villain", edge: 10, health: 14,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["might"] = 10, ["toughness"] = 8, ["agility"] = 6
                },
                // <b>And with a weapon levelled</b>, so p.79's Drop reaches the order of action
                // inside the property rather than beside it.
                ["toughness", "agility"], size: 5, ready: true).Carrying("the wand"),

            // A fifth of their size, which is the band at the other end — and invisible, which p.75
            // makes equivalent to no visibility for whoever is facing them.
            Combatant.Foe(
                "foe", "the Foe", edge: 7, health: 6,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["might"] = 7, ["toughness"] = 5, ["agility"] = 4
                },
                ["toughness", "agility"], size: 0.2, invisible: true).Carrying("the club"),

            // <b>A machine, so p.80's Hard Targets is inside the property rather than beside
            // it</b>: their passive defence doubles while that setting is on, and an attacker may
            // buy the doubling off at four dice.
            Combatant.Minions(
                "minions", "the Minions", threat: 4, groupSize: 6, "threat", hardTarget: true)
        ];
    }
}
