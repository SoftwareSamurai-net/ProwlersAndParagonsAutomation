using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The character is kept in the browser's local storage between visits, and the only part
/// of that which can go quietly wrong is the round trip: a field added to
/// <see cref="CharacterSheet"/> and not carried across comes back as a default, and a
/// default is a plausible value. Nobody notices until a player's Pros have vanished.
///
/// <para>So these do not check a list of fields — a list is the thing that goes stale.
/// They build the most complicated sheet the app can produce, put it through the same
/// serializer the browser uses, and check the <b>engine's own answers</b> match: same cost,
/// same derived stats, same validation. If anything at all is lost, one of those moves.
/// </para>
/// </summary>
[Collection("web")]
public sealed class CharacterStoreTests
{
    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(FindRepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly DerivedStatsCalculator Derived = new(Rules);
    private static readonly CharacterValidator Validator = new(Rules, Costs, Derived);

    public static TheoryData<string> Samples() => ["hero", "villain"];

    /// <summary>
    /// Both samples fill every section a sheet has — that is what they are for — so between
    /// them they exercise ranks, packages, Powers with Pros and Cons and cost variants,
    /// customised gear, perks with units and narrative detail, flaws, and every free-text
    /// field.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void AWholeCharacterSurvivesTheRoundTrip(string which)
    {
        var original = which == "hero" ? SampleCharacters.Hero() : SampleCharacters.Villain();

        var restored = CharacterStore.RoundTrip(original);

        Assert.NotNull(restored);

        // The engine's answers are the assertion: they depend on every input there is.
        Assert.Equal(Costs.TotalCost(original), Costs.TotalCost(restored));
        Assert.Equal(Derived.CalculateEdge(original), Derived.CalculateEdge(restored));
        Assert.Equal(Derived.CalculateHealth(original), Derived.CalculateHealth(restored));
        Assert.Equal(Derived.CalculateResolve(original), Derived.CalculateResolve(restored));

        Assert.Equal(
            Validator.Validate(original).Issues.Select(i => i.Message),
            Validator.Validate(restored).Issues.Select(i => i.Message));
    }

    /// <summary>
    /// And the inputs themselves, because two different characters can cost the same.
    ///
    /// <para>Compared as JSON rather than field by field. A field list is exactly the thing
    /// that goes stale — the failure this whole file exists to catch is somebody adding a
    /// field and not thinking about persistence, and they will not think about this list
    /// either. Serialising both and comparing the text covers whatever the type happens to
    /// hold today.</para>
    ///
    /// <para>It cannot stand alone: a field dropped by the <em>serializer</em> would be
    /// missing from both sides and match. That is what the test above is for — the engine's
    /// answers are computed from the object, not from the JSON.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void EverySelectionSurvivesTheRoundTrip(string which)
    {
        var original = which == "hero" ? SampleCharacters.Hero() : SampleCharacters.Villain();
        var restored = CharacterStore.RoundTrip(original)!;

        Assert.Equal(CharacterStore.Describe(original), CharacterStore.Describe(restored));

        // A handful by hand as well, so a failure says which part moved rather than handing
        // over two walls of JSON.
        Assert.Equal(original.SelectedTierId, restored.SelectedTierId);
        Assert.Equal(original.SelectedPackageId, restored.SelectedPackageId);
        Assert.Equal(original.AbilityRanks, restored.AbilityRanks);
        Assert.Equal(original.TalentRanks, restored.TalentRanks);
        Assert.Equal(original.Perks, restored.Perks);
        Assert.Equal(original.Flaws, restored.Flaws);
        Assert.Equal(original.Connections, restored.Connections);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Quote, restored.Quote);

        Assert.Equal(
            original.SelectedPowers.Select(p => (p.PowerId, p.PurchasedRanks, p.SourceId, p.Units)),
            restored.SelectedPowers.Select(p => (p.PowerId, p.PurchasedRanks, p.SourceId, p.Units)));
    }

    /// <summary>
    /// The parts of a Power that are easiest to lose, because they are optional and a lost
    /// one still deserializes: the cost variant, the nominated baseline Trait, the unit
    /// count, the Source, and the Pros and Cons with their own variants and units.
    /// </summary>
    [Fact]
    public void ThePartsOfAPowerThatAreEasiestToLoseSurvive()
    {
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

        var restored = CharacterStore.RoundTrip(sheet)!;
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
    /// An empty character round-trips to an empty character rather than to null. A player
    /// who has chosen a tier and nothing else has state worth keeping.
    /// </summary>
    [Fact]
    public void AnEmptyCharacterRoundTripsToAnEmptyCharacter()
    {
        var restored = CharacterStore.RoundTrip(new CharacterSheet { SelectedTierId = "street_level" })!;

        Assert.Equal("street_level", restored.SelectedTierId);
        Assert.Empty(restored.SelectedPowers);
        Assert.Empty(restored.Gear);
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
