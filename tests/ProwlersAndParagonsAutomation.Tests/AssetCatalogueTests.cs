using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><see cref="AssetCatalogue"/>: Chapter 6's vehicles, headquarters and gadgets as rows and
/// named figures.</b>
///
/// <para>The data itself is held to the rulebook by <see cref="Chapter6RulesDataTests"/>, which is
/// a different claim from this one. This file says the flattening is complete and the named
/// figures come off the data rather than out of C# — the failure it exists for is a rate typed
/// into a calculator, which no amount of holding the JSON to the book would catch.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class AssetCatalogueTests
{
    private readonly RulesFixture _f;

    public AssetCatalogueTests(RulesFixture fixture) => _f = fixture;

    private AssetCatalogue Assets => _f.Rules.Assets;

    /// <summary>
    /// Every pickable row, and the three tables are all there. The counts are the chapter's own —
    /// six stock vehicles on p.96, twenty-three vehicle features, twenty-two base features — and a
    /// flattening that quietly dropped one table would still answer questions about the other two.
    /// </summary>
    [Fact]
    public void EveryPickableRowOfTheThreeTablesIsThere()
    {
        Assert.Equal(6,  Assets.Rows.Count(r => r.Kind == AssetRowKind.StockVehicle));
        Assert.Equal(23, Assets.Rows.Count(r => r.Kind == AssetRowKind.VehicleFeature));
        Assert.Equal(22, Assets.Rows.Count(r => r.Kind == AssetRowKind.BaseFeature));

        // Three id spaces: a vehicle feature and a base feature are both called Security Systems,
        // and both are both called Communications-ish. Prefixing is what keeps them apart.
        Assert.Equal(Assets.Rows.Count, Assets.Rows.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.NotNull(Assets.Find(AssetCatalogue.VehicleFeaturePrefix + "security_systems"));
        Assert.NotNull(Assets.Find(AssetCatalogue.BaseFeaturePrefix + "security_systems"));
    }

    /// <summary>
    /// <b>Every row carries a price its picker can show.</b> The chapter's own interpretation block
    /// says so in as many words — nothing in either feature table is left to the GM to price — and
    /// a row with no number at all would be one a host had to invent a figure for.
    /// </summary>
    [Fact]
    public void EveryFeatureRowCarriesAPriceOfSomeShape()
    {
        foreach (var row in Assets.Rows.Where(r => r.Kind != AssetRowKind.StockVehicle))
        {
            Assert.True(
                row.Cost is not null || row.Grades is not null || row.CostPerUnit is not null,
                $"{row.Id} carries no price at all, so a picker would have to invent one.");
        }
    }

    /// <summary>
    /// <b>The two exchange rates and the four characteristic rates come off the data.</b> They are
    /// the figures a calculator would otherwise spell in C#, which is the one thing
    /// <c>data/rules/</c> exists to prevent — and each of the six is a number the book prints.
    /// </summary>
    [Fact]
    public void TheExchangeRatesAndCharacteristicRatesComeOffTheData()
    {
        Assert.Equal(25, Assets.VehiclePointsPerHeroPoint);   // p.96
        Assert.Equal(3,  Assets.BasePointsPerHeroPoint);      // p.100

        var rates = Assets.Characteristics;                   // p.96
        Assert.Equal(1, rates.BodyCostPerRank);
        Assert.Equal(1, rates.SpeedCostPerRank);
        Assert.Equal(1, rates.WeaponsCostPerRank);
        Assert.Equal(2, rates.ControlCostPerRank);
        Assert.Equal(-3, rates.NegativeControlMinimum);

        // A vehicle's four ranks are bought from zero. SelectedGear's free baseline is the wrong
        // precedent and this is where that is pinned.
        Assert.Equal(0, rates.InitialBody);
        Assert.Equal(0, rates.InitialSpeed);
        Assert.Equal(0, rates.InitialControl);
    }

    /// <summary>
    /// <b>The Gadget pool's multiplier and floor, p.94.</b> Twice Complexity, minimum three — the
    /// one calculation in this chapter that pays Hero Points out.
    /// </summary>
    [Fact]
    public void TheGadgetPoolMultiplierAndFloorComeOffTheData()
    {
        Assert.Equal(2, Assets.GadgetBuild.HeroPointsGrantedMultiplier);
        Assert.Equal(3, Assets.MinimumGadgetComplexity);

        // And the Item Con is on every Gadget, uncredited — the same answer gear gets.
        Assert.Equal("item", Assets.GadgetBuild.DefaultCon);
        Assert.False(Assets.GadgetBuild.DefaultConIsCredited);
    }

    /// <summary>
    /// <b>The two features the rest of this slice asks about by id are really there.</b> A constant
    /// naming a feature that has been renamed away is a validator check that silently stops firing
    /// — the shape this repository has been bitten by more than once.
    /// </summary>
    [Fact]
    public void TheTwoFeaturesNamedInCodeExist()
    {
        Assert.NotNull(Assets.FindVehicleFeature(AssetCatalogue.MechaFeatureId));
        Assert.NotNull(Assets.FindBaseFeature(AssetCatalogue.TrainingFacilitiesFeatureId));

        // Mecha is the one vehicle feature with a floor printed beside its price, which is what
        // the validator reads rather than a sentence written in C#.
        Assert.NotNull(Assets.FindVehicleFeature(AssetCatalogue.MechaFeatureId)!.Constraint);

        // Training Facilities is what grants Teamwork, and the grant says so from the other end.
        Assert.Equal(AssetCatalogue.TrainingFacilitiesFeatureId, Assets.Teamwork.GrantedByFeature);
        Assert.Equal(1, Assets.Teamwork.PointsPerIssue);
        Assert.Equal("resolve", Assets.Teamwork.BehavesLike);
    }

    /// <summary>
    /// <b>A missing entry throws rather than defaulting.</b> Every figure this catalogue serves is
    /// off the data on purpose; a rules file that lost an entry would otherwise hand back a zero
    /// exchange rate and price every vehicle as free.
    /// </summary>
    [Fact]
    public void AMissingEntryIsRefusedRatherThanDefaulted()
    {
        var files = RulesRepository.DataFileNames.ToDictionary(
            name => name,
            name => name == "vehicles.json"
                ? """{"header":{},"entries":[]}"""
                : File.ReadAllText(Path.Combine(RulesFixture.DataPath, name)),
            StringComparer.Ordinal);

        var assets = new RulesRepository(new InMemoryRulesSource(files)).Assets;

        var ex = Assert.Throws<InvalidOperationException>(() => assets.VehiclePointsPerHeroPoint);
        Assert.Contains("unique_vehicle_perk", ex.Message, StringComparison.Ordinal);
    }
}
