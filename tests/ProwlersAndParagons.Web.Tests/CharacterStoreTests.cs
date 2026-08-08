using ProwlersAndParagonsAutomation.Engine;

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

    private static (CharacterStore Store, FakeLocalStorage Storage) Fresh()
    {
        var storage = new FakeLocalStorage();
        return (new CharacterStore(storage), storage);
    }

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
            [new SelectedProCon("area_burst", "large") { Units = 3 }],
            [new SelectedProCon("charges", "three_charges")])
        {
            CostVariantKey = "standard",
            Units = 2,
            BaselineTraitId = "might",
            SourceId = "tech"
        });

        sheet.Gear.Add(new SelectedGear("Jo Sticks")
        {
            Features = [new SelectedGearFeature("accurate", "very_accurate")],
            Pros = [new SelectedProCon("powerful")],
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
        Assert.Equal("large", power.Pros.Single().VariantKey);
        Assert.Equal(3, power.Pros.Single().Units);
        Assert.Equal("three_charges", power.Cons.Single().VariantKey);

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
