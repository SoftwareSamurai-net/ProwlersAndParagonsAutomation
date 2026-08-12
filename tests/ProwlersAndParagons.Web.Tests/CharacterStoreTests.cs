using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The character is kept in the browser's local storage between visits. Two things can go
/// wrong, and they fail in opposite directions.
///
/// <para><b>Losing part of the character.</b> A field added to <see cref="CharacterSheet"/>
/// and not carried across comes back as a default, and a default is a plausible value —
/// nobody notices until a player's Pros have vanished. So the round trip is checked against
/// the <b>engine's own answers</b>: same cost, same derived stats, same validation. A field
/// list would be the first thing to go stale, and whoever forgets the field will forget the
/// list too.</para>
///
/// <para><b>Not starting at all.</b> Restoring runs before the first render, so anything
/// that throws there is a blank page rather than a lost character. The deserializer hands
/// back null for a property whose declared type is non-nullable and an out-of-range number
/// for an enum, and neither is something a compiler can warn about — which is why half of
/// this file is malformed storage rather than valid characters.</para>
///
/// <para>Everything goes through the real <see cref="CharacterStore"/> against a fake
/// storage. It used to go through a public test-only helper that serialized and
/// deserialized in one call, which meant the methods the app actually runs had no coverage,
/// the schema gate could not fail (both sides used the same constant), and the mode was not
/// carried at all.</para>
/// </summary>
public sealed class CharacterStoreTests
{
    private const string Key = "pp.character.v1";

    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(FindRepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly DerivedStatsCalculator Derived = new(Rules);
    private static readonly CharacterValidator Validator = new(Rules, Costs, Derived);

    /// <summary>
    /// The Source headings and the Abilities (…) / Talents (…) lines beneath them, flattened
    /// to strings so a difference reads as text rather than as a failed reference compare.
    /// </summary>
    private static IEnumerable<string> GroupingSummary(CharacterSheet sheet) =>
        new SourceGrouping(Rules).GroupBySource(sheet)
            .SelectMany(g => g.TraitLines.Prepend(g.Heading).Select(l => $"{g.Heading}|{l}"));

    private static (CharacterStore Store, FakeLocalStorage Storage) Fresh()
    {
        var storage = new FakeLocalStorage();
        return (new CharacterStore(storage, Costs, Validator), storage);
    }

    /// <summary>Wraps a Sheet body in a payload this build's version gate accepts.</summary>
    private static string Payload(string sheetBody) =>
        $"{{\"Version\":1,\"Mode\":0,\"Sheet\":{{\"SelectedTierId\":\"standard\",{sheetBody}}}}}";

    public static TheoryData<string> Samples() => ["hero", "villain"];

    private static CharacterSheet Sample(string which) =>
        which == "hero" ? SampleCharacters.Hero() : SampleCharacters.Villain();

    // ── Losing part of the character ────────────────────────────────────────────

    /// <summary>
    /// Both samples fill every section a sheet has — that is what they are for — so between
    /// them they exercise ranks, packages, Powers with Pros and Cons and cost variants,
    /// customised gear, perks with units and narrative detail, flaws, and every free-text
    /// field. The engine's answers depend on all of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public async Task AWholeCharacterSurvivesTheRoundTrip(string which)
    {
        var (store, _) = Fresh();
        var original = Sample(which);

        await store.SaveAsync(original, SheetMode.Hero);
        var restored = await store.LoadAsync();

        Assert.NotNull(restored);
        var sheet = restored.Value.Sheet;

        Assert.Equal(Costs.TotalCost(original), Costs.TotalCost(sheet));
        Assert.Equal(Derived.CalculateEdge(original), Derived.CalculateEdge(sheet));
        Assert.Equal(Derived.CalculateHealth(original), Derived.CalculateHealth(sheet));
        Assert.Equal(Derived.CalculateResolve(original), Derived.CalculateResolve(sheet));

        Assert.Equal(
            Validator.Validate(original).Issues.Select(i => i.Message),
            Validator.Validate(sheet).Issues.Select(i => i.Message));

        // A Trait's Source costs nothing and changes no rank, so all four figures above are
        // blind to it — losing AbilitySources entirely would pass every one of them. This is
        // the engine answer that can see it, and it is still an answer rather than a field
        // list: the headings and the Abilities (…) lines a sheet prints.
        Assert.Equal(GroupingSummary(original), GroupingSummary(sheet));

        // The engine's answers cannot see the free text, and losing all of it would pass
        // every assertion above — so it is checked directly. These are the fields a player
        // typed rather than chose, and the ones they would notice first.
        Assert.Equal(original.Name, sheet.Name);
        Assert.Equal(original.Appearance, sheet.Appearance);
        Assert.Equal(original.Motivation, sheet.Motivation);
        Assert.Equal(original.Quote, sheet.Quote);
        Assert.Equal(original.Connections, sheet.Connections);
        Assert.Equal(original.Gear.Select(g => g.Name), sheet.Gear.Select(g => g.Name));
        Assert.Equal(
            original.Perks.Select(p => p.NarrativeDetail),
            sheet.Perks.Select(p => p.NarrativeDetail));
    }

    /// <summary>The palette is part of the character to a player, so it is stored too.</summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public async Task TheModeSurvivesTheRoundTrip(SheetMode mode)
    {
        var (store, _) = Fresh();

        await store.SaveAsync(SampleCharacters.Hero(), mode);

        Assert.Equal(mode, (await store.LoadAsync())!.Value.Mode);
    }

    /// <summary>
    /// The parts of a Power that are easiest to lose, because each is optional and a lost one
    /// still deserializes into something that looks fine.
    /// </summary>
    [Fact]
    public async Task ThePartsOfAPowerThatAreEasiestToLoseSurvive()
    {
        var (store, _) = Fresh();

        var sheet = new CharacterSheet
        {
            SelectedTierId = "standard",
            AbilityRanks   = { ["might"] = 6 }
        };

        sheet.SelectedPowers.Add(new SelectedPower(
            "blast", 4,
            [new SelectedProCon("area_burst", "area") { Units = 3 }],
            [new SelectedProCon("charges", "3_per_scene")])
        {
            CostVariantKey = "standard",
            Units = 2,
            BaselineTraitId = "might",
            SourceId = "tech"
        });

        sheet.Gear.Add(new SelectedGear("Jo Sticks")
        {
            Features = [new SelectedGearFeature("accurate", "very_accurate")],
            Pros = [new SelectedProCon("armor_piercing")],
            Cons = [new SelectedProCon("item")],
            PairedUnderTwoFisted = true
        });

        await store.SaveAsync(sheet, SheetMode.Hero);
        var restored = (await store.LoadAsync())!.Value.Sheet;

        var power = restored.SelectedPowers.Single();
        Assert.Equal("standard", power.CostVariantKey);
        Assert.Equal(2, power.Units);
        Assert.Equal("might", power.BaselineTraitId);
        Assert.Equal("tech", power.SourceId);
        Assert.Equal("area", power.Pros.Single().VariantKey);
        Assert.Equal(3, power.Pros.Single().Units);
        Assert.Equal("3_per_scene", power.Cons.Single().VariantKey);

        var gear = restored.Gear.Single();
        Assert.True(gear.PairedUnderTwoFisted);
        Assert.Equal("very_accurate", gear.Features.Single().GradeKey);
        Assert.Single(gear.Pros);
        Assert.Single(gear.Cons);
    }

    /// <summary>
    /// An empty character round-trips to an empty character rather than to nothing. A player
    /// who has chosen a tier and stopped there has state worth keeping.
    /// </summary>
    [Fact]
    public async Task AnEmptyCharacterRoundTripsToAnEmptyCharacter()
    {
        var (store, _) = Fresh();

        await store.SaveAsync(new CharacterSheet { SelectedTierId = "street_level" }, SheetMode.Hero);
        var restored = (await store.LoadAsync())!.Value.Sheet;

        Assert.Equal("street_level", restored.SelectedTierId);
        Assert.Empty(restored.SelectedPowers);
        Assert.Empty(restored.Gear);
    }

    // ── Not starting at all ─────────────────────────────────────────────────────

    /// <summary>
    /// Storage a player never wrote: an older build's, a hand-edited one, a truncated one.
    /// Every one means "no saved character", and none may throw — this runs before the first
    /// render, so an exception here is a blank page.
    ///
    /// <para><c>{"Version":1,"Mode":0}</c> is the one that actually shipped broken: the
    /// deserializer hands back a null Sheet for a property whose declared type says it cannot
    /// be null, the version gate passed it, and restoring it threw on the way to the first
    /// render.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"Version\":1,\"Mode\":0}")]
    [InlineData("{\"Version\":1,\"Mode\":0,\"Sheet\":null}")]
    [InlineData("{\"Version\":0,\"Mode\":0,\"Sheet\":{}}")]
    [InlineData("{\"Version\":99,\"Mode\":0,\"Sheet\":{}}")]
    [InlineData("{\"Mode\":0,\"Sheet\":{}}")]
    [InlineData("{\"Version\":\"1\",\"Mode\":0,\"Sheet\":{}}")]
    public async Task StorageThisBuildCannotTrustIsIgnoredRatherThanThrown(string stored)
    {
        var (store, storage) = Fresh();
        storage.Poke(Key, stored);

        Assert.Null(await store.LoadAsync());
    }

    /// <summary>
    /// An enum accepts any number the JSON offers. A mode that is neither Hero nor Villain
    /// gave a character whose palette disagreed with every check made against it, so an
    /// unknown one falls back rather than being carried.
    /// </summary>
    [Theory]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("2147483647")]
    public async Task AModeThisBuildDoesNotKnowFallsBackToHero(string mode)
    {
        var (store, storage) = Fresh();
        storage.Poke(Key, $"{{\"Version\":1,\"Mode\":{mode},\"Sheet\":{{\"SelectedTierId\":\"standard\"}}}}");

        var restored = await store.LoadAsync();

        Assert.NotNull(restored);
        Assert.Equal(SheetMode.Hero, restored.Value.Mode);
    }

    /// <summary>
    /// A Power whose Pros and Cons keys are simply absent. Both are declared non-null and
    /// both come back null, which is a NullReferenceException on the next render — not
    /// anything a JSON catch would ever see.
    /// </summary>
    [Fact]
    public async Task APowerMissingItsProsAndConsIsRepairedRatherThanLeftNull()
    {
        var (store, storage) = Fresh();
        storage.Poke(Key,
            "{\"Version\":1,\"Mode\":0,\"Sheet\":{\"SelectedTierId\":\"standard\"," +
            "\"SelectedPowers\":[{\"PowerId\":\"armor\",\"PurchasedRanks\":4}]}}");

        var restored = await store.LoadAsync();

        Assert.NotNull(restored);
        var power = restored.Value.Sheet.SelectedPowers.Single();

        Assert.Empty(power.Pros);
        Assert.Empty(power.Cons);

        // And the engine can now be asked about it without blowing up.
        Assert.True(Costs.PowerCost(power) > 0);
    }

    /// <summary>A null inside a list is dropped rather than carried into a render.</summary>
    [Fact]
    public async Task NullsInsideTheListsAreDropped()
    {
        var (store, storage) = Fresh();
        storage.Poke(Key,
            "{\"Version\":1,\"Mode\":0,\"Sheet\":{\"SelectedTierId\":\"standard\"," +
            "\"SelectedPowers\":[null],\"Perks\":[null],\"Flaws\":[null],\"Gear\":[null],\"Connections\":[null]}}");

        var sheet = (await store.LoadAsync())!.Value.Sheet;

        Assert.Empty(sheet.SelectedPowers);
        Assert.Empty(sheet.Perks);
        Assert.Empty(sheet.Flaws);
        Assert.Empty(sheet.Gear);
        Assert.Empty(sheet.Connections);
    }

    /// <summary>
    /// A null <b>below</b> the top level, which is where this went wrong.
    ///
    /// <para>The first version of the guard stripped nulls exactly one level deep: the four
    /// top-level lists, and a Power's missing Pros and Cons. Everything deeper passed —
    /// <c>"Pros":[null]</c>, a null <c>PowerId</c>, a null inside <c>AbilityModifiers</c>, a
    /// piece of gear with null Features. Those payloads restored cleanly, got past the
    /// backstop in <c>Program.cs</c>, and then took the app down on the first frame, because
    /// the budget bar renders on every route and costs the sheet to do it. A blank page, from
    /// the class written to prevent one.</para>
    ///
    /// <para>The guard is no longer a list of shapes: the engine is asked to cost and
    /// validate the sheet once, and a payload it cannot answer for is not handed to the app.
    /// A list of shapes goes stale the first time somebody adds a field, and they will not be
    /// thinking about this file when they do.</para>
    ///
    /// <para><b>The null payloads that used to be here are repaired now rather than refused</b>,
    /// and they moved to the test below. They were refused because the engine <em>threw</em> on a
    /// null id instead of reporting one; it reports them, so the right answer changed. What is
    /// left here is the case no repair can help: a value of the wrong type for its field.</para>
    /// </summary>
    [Theory]
    [InlineData("\"AbilityRanks\":{\"might\":\"not a number\"}")]
    [InlineData("\"SelectedPowers\":\"not a list\"")]
    public async Task APayloadTheEngineCannotAnswerForIsNotHandedToTheApp(string sheetBody)
    {
        var (store, storage) = Fresh();
        storage.Poke(Key, Payload(sheetBody));

        Assert.Null(await store.LoadAsync());
    }

    /// <summary>
    /// <b>An entry that names nothing is dropped, and the character survives it.</b>
    ///
    /// <para>These payloads used to be refused outright, because the engine threw on a null id
    /// rather than reporting one and the guard above could not tell that from corruption. The
    /// engine reports them now, which changes the right answer here: an entry naming nothing is
    /// junk, it can only come from a hand-edited or half-written payload, and one lost entry is
    /// worth far less than the character around it.</para>
    ///
    /// <para>The headless command asks <c>Repair</c> for the opposite, and that is the point of
    /// the flag: there the same entry is in a file somebody submitted, and dropping it silently
    /// would hand back a cheaper character than the one that was sent.</para>
    /// </summary>
    [Theory]
    [InlineData("\"SelectedPowers\":[{\"PowerId\":null,\"PurchasedRanks\":2,\"Pros\":[],\"Cons\":[]}]")]
    [InlineData("\"Perks\":[{\"PerkId\":null,\"Units\":1}]")]
    [InlineData("\"Flaws\":[{\"FlawId\":null}]")]
    [InlineData("\"Gear\":[{\"Name\":null}]")]
    [InlineData("\"Gear\":[{\"Name\":\"Rope\",\"Features\":null,\"Pros\":null,\"Cons\":null}]")]
    [InlineData("\"Gear\":[{\"Name\":\"Rope\",\"Features\":[null],\"Pros\":[null],\"Cons\":[null]}]")]
    [InlineData("\"SelectedPowers\":[{\"PowerId\":\"armor\",\"PurchasedRanks\":2,\"Pros\":[null],\"Cons\":[]}]")]
    [InlineData("\"SelectedPowers\":[{\"PowerId\":\"armor\",\"PurchasedRanks\":2,\"Pros\":[],\"Cons\":[null]}]")]
    [InlineData("\"AbilityRanks\":{\"might\":5},\"AbilityModifiers\":{\"might\":[null]}")]
    public async Task AnEntryNamingNothingIsDroppedRatherThanLosingTheCharacter(string sheetBody)
    {
        var (store, storage) = Fresh();
        storage.Poke(Key, Payload(sheetBody));

        var restored = await store.LoadAsync();

        Assert.NotNull(restored);

        var sheet = restored.Value.Sheet;

        Assert.DoesNotContain(sheet.SelectedPowers, p => p.PowerId is null);
        Assert.DoesNotContain(sheet.Perks, p => p.PerkId is null);
        Assert.DoesNotContain(sheet.Flaws, f => f.FlawId is null);
        Assert.DoesNotContain(sheet.Gear, g => g.Name is null);
        Assert.All(sheet.Gear, g =>
        {
            Assert.NotNull(g.Features);
            Assert.NotNull(g.Pros);
            Assert.NotNull(g.Cons);
        });
    }

    /// <summary>
    /// The free text is repaired rather than rejected. A null Name costs nothing and
    /// validates fine, so the engine check above cannot see it — and it is a
    /// NullReferenceException in the text export, which is the last place a player wants one.
    /// Losing a name is also not worth losing a character over.
    /// </summary>
    [Theory]
    [InlineData("\"Name\":null")]
    [InlineData("\"Appearance\":null")]
    [InlineData("\"Motivation\":null")]
    [InlineData("\"Quote\":null")]
    public async Task NullFreeTextComesBackEmptyRatherThanNull(string sheetBody)
    {
        var (store, storage) = Fresh();
        storage.Poke(Key, Payload(sheetBody));

        var restored = await store.LoadAsync();

        Assert.NotNull(restored);
        var sheet = restored.Value.Sheet;

        Assert.NotNull(sheet.Name);
        Assert.NotNull(sheet.Appearance);
        Assert.NotNull(sheet.Motivation);
        Assert.NotNull(sheet.Quote);

        // And the export the null would have crashed now builds.
        Assert.NotEmpty(CharacterSheetRenderer.RenderText(
            sheet, Rules, Costs, Derived, Validator.Validate(sheet), new DateTime(2026, 1, 1)));
    }

    /// <summary>
    /// A half-finished character is <b>not</b> corruption, and must survive. The engine
    /// throws for a variable-cost Power with no variant chosen — the same exception type a
    /// bad payload could produce — so a guard that rejected everything unpriceable would
    /// throw away exactly the work this class exists to keep.
    /// </summary>
    [Fact]
    public async Task ACharacterThatCannotBePricedYetIsStillRestored()
    {
        var (store, _) = Fresh();

        var sheet = new CharacterSheet { SelectedTierId = "standard" };
        // Energy Absorption is priced per rank at a rate that depends on a variant. Without
        // one there is no rate, and the engine says so rather than guessing.
        sheet.SelectedPowers.Add(new SelectedPower("energy_absorption", 3));

        // The premise: this genuinely cannot be costed.
        Assert.Throws<InvalidOperationException>(() => Costs.TotalCost(sheet));

        await store.SaveAsync(sheet, SheetMode.Hero);
        var restored = await store.LoadAsync();

        Assert.NotNull(restored);
        Assert.Single(restored.Value.Sheet.SelectedPowers);
    }

    /// <summary>
    /// Storage this build cannot use is removed, not left. Left in place it is re-read and
    /// re-rejected on every visit — and if one ever gets past a guard, the failure repeats
    /// forever with no way out from inside the app.
    /// </summary>
    [Fact]
    public async Task StorageThatCannotBeUsedIsRemoved()
    {
        var (store, storage) = Fresh();
        storage.Poke(Key, "{\"Version\":99,\"Mode\":0,\"Sheet\":{}}");

        Assert.Null(await store.LoadAsync());
        Assert.Null(storage.Peek(Key));
    }

    /// <summary>
    /// A browser with storage turned off, or full. Saving does nothing and loading finds
    /// nothing, and the app carries on without persistence rather than falling over.
    /// </summary>
    [Fact]
    public async Task ABrowserThatRefusesStorageIsNotAnError()
    {
        var (store, storage) = Fresh();
        storage.Refuses = true;

        await store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);
        Assert.Null(await store.LoadAsync());
        await store.ClearAsync();
    }

    /// <summary>Clearing forgets it, rather than leaving an empty character behind.</summary>
    [Fact]
    public async Task ClearingForgetsTheCharacter()
    {
        var (store, storage) = Fresh();

        await store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);
        Assert.NotNull(storage.Peek(Key));

        await store.ClearAsync();

        Assert.Null(storage.Peek(Key));
        Assert.Null(await store.LoadAsync());
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
