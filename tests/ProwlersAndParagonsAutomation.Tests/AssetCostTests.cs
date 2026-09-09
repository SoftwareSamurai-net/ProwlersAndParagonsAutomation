using System.Globalization;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>What a vehicle, a headquarters and a Gadget cost, through <see cref="CostCalculator"/>.</b>
///
/// <para><see cref="Chapter6RulesDataTests"/> holds the shipped JSON to the printed pages, which is
/// a different claim from this one: a file can be a perfect transcription and the calculator built
/// on it can still add the wrong columns. The centrepiece here is the six stock vehicles — p.96
/// prints both their statistics and their totals, so building each one out of this engine's own
/// arithmetic and getting the printed figure back is the strongest check the chapter offers.</para>
///
/// <para><b>And the currencies do not mix.</b> A vehicle costs Vehicle Points, a base costs Base
/// Points, and only the Perks that buy them are in Hero Points. Two tests below exist purely to
/// stop a later slice folding one into the other.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class AssetCostTests
{
    private readonly RulesFixture _f;

    public AssetCostTests(RulesFixture fixture) => _f = fixture;

    // ── The six stock vehicles ────────────────────────────────────────────────

    /// <summary>
    /// <b>Every one of p.96's six stock vehicles comes out at its printed total.</b>
    ///
    /// <para>The page prints four ranks, a feature list and a Vehicle Point total for each, and the
    /// total is not used by anything else — so it is a free second witness on the whole pricing
    /// rule at once: the four characteristic rates, Control at double, the negative features that
    /// pay back, the per-unit ones, and Unique Systems converting a Hero Point one for one.</para>
    ///
    /// <para><b>The two feature lines that are not bare names are read as the entry's own
    /// interpretation block reads them</b>: "Passengers 4" is four <em>extra</em> passengers, which
    /// is what the feature is priced per; "Rader (Sonar)" is the Radar Power carrying the Sonar Con
    /// printed inside its own Ch.2 entry, taken through Unique Systems. Both readings are recorded
    /// on the data and neither is invented here — and the Radar figure is asked of
    /// <see cref="CostCalculator.PowerCost"/> rather than restated, so a re-priced Power moves this
    /// test rather than leaving it asserting a stale number.</para>
    /// </summary>
    [Theory]
    [InlineData("Helicopter")]
    [InlineData("Jet Fighter")]
    [InlineData("Motorcycle")]
    [InlineData("Speedboat")]
    [InlineData("Sports Car")]
    [InlineData("Submersible")]
    public void EachStockVehicleComesOutAtItsPrintedTotal(string name)
    {
        var stock = _f.Rules.Assets.StockVehicles.Single(v => v.Name == name);

        var vehicle = new OwnedVehicle(stock.Name)
        {
            Body     = stock.Body,
            Speed    = stock.Speed,
            Control  = stock.Control,
            Weapons  = stock.Weapons,
            Features = [.. stock.Features.Select(FeatureFromPrintedLine)]
        };

        // The positive control, and it is not decoration: a Submersible whose feature list came
        // back empty would still price to *something*, and "something" is what three of this
        // repository's four historical guard faults reported as success.
        Assert.Equal(stock.Features.Count, vehicle.Features.Count);

        Assert.Equal(stock.VehiclePoints, _f.Costs.VehiclePointsSpent(vehicle));
    }

    /// <summary>
    /// <b>The four features that pay Vehicle Points back really do.</b> p.96 says a negative price
    /// lowers the total in as many words and prints no floor, so nothing here clamps at zero —
    /// which is the opposite of gear, where the floor at 0 <em>is</em> printed.
    /// </summary>
    [Fact]
    public void AFeatureWithANegativeCostLowersTheTotalAndNothingFloorsIt()
    {
        var paidBack = new OwnedVehicle("Wreck")
        {
            Features =
            [
                new SelectedAssetFeature("giant"),        // −6, ground vehicles
                new SelectedAssetFeature("swimming"),     // −4
                new SelectedAssetFeature("open_cockpit")  // −2
            ]
        };

        Assert.Equal(-12, _f.Costs.VehiclePointsSpent(paidBack));
    }

    // ── The two currencies ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Twenty-five Vehicle Points a Hero Point, three Base Points a Hero Point</b> — and the
    /// budget is what the Perk on <em>this</em> machine bought, not a pool shared across the sheet.
    /// </summary>
    [Fact]
    public void TheBudgetIsTheSecondCurrencyThisAssetsOwnPerkBought()
    {
        Assert.Equal(50, _f.Costs.VehiclePointBudget(new OwnedVehicle("Two") { PerkHeroPoints = 2 }));
        Assert.Equal(12, _f.Costs.BasePointBudget(new OwnedHeadquarters("Four") { PerkHeroPoints = 4 }));

        // A base with no features spends none of its allowance and is still a base: p.100 grants
        // the building itself for the Perk alone.
        Assert.Equal(0, _f.Costs.BasePointsSpent(new OwnedHeadquarters("Warehouse") { PerkHeroPoints = 1 }));
    }

    /// <summary>
    /// <b>Only the Perks reach the Hero Point total.</b> A vehicle worth fifty Vehicle Points and a
    /// base worth twelve Base Points cost the character four Hero Points between them and not
    /// sixty-two — the failure this guards against is a later slice quietly summing the second
    /// currency into the first, which would blow every tier's budget on the first machine.
    /// </summary>
    [Fact]
    public void OnlyThePerksReachTheHeroPointTotal()
    {
        var sheet = _f.LegalSheet();
        var before = _f.Costs.TotalCost(sheet);

        sheet.Vehicles.Add(new OwnedVehicle("The Bus")
        {
            PerkHeroPoints = 2,
            Body = 10, Speed = 10, Control = 5, Weapons = 8,
            Features = [new SelectedAssetFeature("sensors")]   // 10 Vehicle Points on its own
        });

        sheet.Headquarters.Add(new OwnedHeadquarters("The Loft")
        {
            PerkHeroPoints = 2,
            Features = [new SelectedAssetFeature("size") { GradeKey = "sprawling" }]
        });

        Assert.Equal(before + 4, _f.Costs.TotalCost(sheet));
        Assert.Equal(4, _f.Costs.TotalAssetPerkCost(sheet));

        // …and the second currencies are still being counted, in their own units. Without this the
        // test above would pass just as happily on a calculator that priced a vehicle at nothing.
        Assert.Equal(48, _f.Costs.VehiclePointsSpent(sheet.Vehicles[0]));
        Assert.Equal(2,  _f.Costs.BasePointsSpent(sheet.Headquarters[0]));
    }

    /// <summary>
    /// <b>Hero Points put into a campaign's shared object are charged here and nothing else is.</b>
    /// Both p.96 and p.100 let a team pool their allowances, and a character sheet is one
    /// character — so what this sheet can honestly say is how much went in.
    /// </summary>
    [Fact]
    public void AContributionToASharedObjectCostsHeroPointsAndNothingElse()
    {
        var sheet = _f.LegalSheet();
        var before = _f.Costs.TotalCost(sheet);

        sheet.CampaignAssets.Add(new CampaignAssetContribution("asset-1")
        {
            Name = "The Aerie", Kind = CampaignAssetContribution.Headquarters, HeroPoints = 3
        });

        Assert.Equal(before + 3, _f.Costs.TotalCost(sheet));

        // Nothing else about the object is on this sheet, which is the point: five members would
        // otherwise carry five copies of one base's feature list.
        Assert.Empty(sheet.Headquarters);
    }

    // ── Gadgets, which run the other way ──────────────────────────────────────

    /// <summary>
    /// <b>A Gadget pays out twice its Complexity and charges the character nothing.</b> p.94, and
    /// the multiplier is read off <c>gadgets.json</c>.
    /// </summary>
    [Fact]
    public void AGadgetPaysOutTwiceItsComplexityAndIsNotInTheTotal()
    {
        var sheet = _f.LegalSheet();
        var before = _f.Costs.TotalCost(sheet);

        var gadget = new BuiltGadget("Freeze Ray")
        {
            Complexity = 6,
            Powers = [new SelectedPower("blast", 5)]
        };
        sheet.Gadgets.Add(gadget);

        Assert.Equal(12, _f.Costs.GadgetPool(gadget));

        // Not netted off, not added on: a Gadget makes a character neither cheaper nor dearer.
        Assert.Equal(before, _f.Costs.TotalCost(sheet));
    }

    /// <summary>
    /// <b>The pool is spent under the ordinary cost rules, and the Item Con is not credited.</b>
    /// The same answer gear gets and for the same reason: p.94 puts the Con on every Gadget by
    /// default, and a statement of what a thing is is not a discount to claim. Crediting it would
    /// make a 1 HP purchase free, which is the outcome the rule exists to prevent.
    /// </summary>
    [Fact]
    public void TheItemConIsOnAGadgetAndIsWorthNothing()
    {
        var itemConId = _f.Rules.Equipment.GearProsAndCons.GearProsAndCons!.ItemConId!;

        // The control: the Con really is priced at a discount everywhere it *is* credited, so the
        // equality below is a decision this engine makes rather than a Con that costs nothing.
        Assert.True(_f.Rules.GetCon(itemConId)!.CostModifier < 0);

        var plain = new BuiltGadget("Ray") { Powers = [new SelectedPower("blast", 4)] };
        var withCon = new BuiltGadget("Ray")
        {
            Powers = [new SelectedPower("blast", 4, [], [new SelectedProCon(itemConId)])]
        };

        Assert.Equal(_f.Costs.GadgetSpend(plain), _f.Costs.GadgetSpend(withCon));

        // …and every other Con still discounts, which p.94 allows in as many words.
        var withOther = new BuiltGadget("Ray")
        {
            Powers = [new SelectedPower("blast", 4, [], [new SelectedProCon("exclusive")])]
        };
        Assert.NotEqual(_f.Costs.GadgetSpend(plain), _f.Costs.GadgetSpend(withOther));
    }

    /// <summary>
    /// <b>Abilities and Talents come out of the pool at a Hero Point a rank</b>, which is what p.94
    /// means by spending it "on Abilities, Talents and Powers" under the ordinary rules.
    /// </summary>
    [Fact]
    public void AbilityAndTalentRanksComeOutOfThePoolAtOneAPoint()
    {
        var gadget = new BuiltGadget("Exo-frame")
        {
            Complexity = 5,
            AbilityRanks = new Dictionary<string, int> { ["might"] = 4 },
            TalentRanks  = new Dictionary<string, int> { ["vehicles"] = 2 }
        };

        Assert.Equal(6, _f.Costs.GadgetSpend(gadget));
        Assert.Equal(10, _f.Costs.GadgetPool(gadget));
    }

    // ── Feature prices, in all three shapes ───────────────────────────────────

    /// <summary>
    /// <b>Flat, graded and per-unit, on both tables.</b> Every price in either table is one of the
    /// three, which the chapter's own interpretation block states — so a fourth shape appearing
    /// would be a data change nothing else here would notice.
    /// </summary>
    [Fact]
    public void EveryFeatureShapePricesOnBothTables()
    {
        // Vehicle: flat, per unit, and the one graded feature.
        Assert.Equal(2,  _f.Costs.VehicleFeatureCost(new SelectedAssetFeature("flight")));
        Assert.Equal(3,  _f.Costs.VehicleFeatureCost(new SelectedAssetFeature("passengers") { Units = 3 }));
        Assert.Equal(2,  _f.Costs.VehicleFeatureCost(
            new SelectedAssetFeature("hidden_compartments") { GradeKey = "large" }));

        // Base: flat, graded at two and at three, and the one priced per unit.
        Assert.Equal(1, _f.Costs.BaseFeatureCost(new SelectedAssetFeature("hidden")));
        Assert.Equal(2, _f.Costs.BaseFeatureCost(
            new SelectedAssetFeature("science_labs") { GradeKey = "advanced" }));
        Assert.Equal(3, _f.Costs.BaseFeatureCost(
            new SelectedAssetFeature("size") { GradeKey = "awe_inspiring" }));
        Assert.Equal(2, _f.Costs.BaseFeatureCost(
            new SelectedAssetFeature("alternate_headquarters") { Units = 2 }));
    }

    /// <summary>
    /// <b>An unpriceable selection throws rather than answering.</b> The validator reports each of
    /// these by name before anything asks for a total, exactly as it does for a gear feature —
    /// and the calculator refusing is what makes that ordering load-bearing rather than polite.
    /// </summary>
    [Fact]
    public void AnUnpriceableFeatureIsRefusedRatherThanGuessed()
    {
        Assert.Throws<InvalidOperationException>(
            () => _f.Costs.VehicleFeatureCost(new SelectedAssetFeature("teleporter")));

        Assert.Throws<InvalidOperationException>(
            () => _f.Costs.BaseFeatureCost(new SelectedAssetFeature("size")));       // graded, no grade

        Assert.Throws<InvalidOperationException>(
            () => _f.Costs.BaseFeatureCost(new SelectedAssetFeature("size") { GradeKey = "huge" }));
    }

    // ── Teamwork, which is Resolve's twin ─────────────────────────────────────

    /// <summary>
    /// <b>Training Facilities grants a point of Teamwork an issue, and the engine computes it for
    /// anybody.</b> Only Heroes hold it, exactly as only Heroes hold Resolve — but that is a
    /// presentation rule a host keeps, because a rule branching on the palette flag would be the
    /// browser deciding a rule. <c>PresentationFlagsTests</c> is what stops it moving in here.
    /// </summary>
    [Fact]
    public void ABaseWithTrainingFacilitiesGrantsAPointOfTeamwork()
    {
        var sheet = _f.LegalSheet();
        Assert.Equal(0, _f.Derived.CalculateTeamwork(sheet));

        sheet.Headquarters.Add(new OwnedHeadquarters("The Gym")
        {
            PerkHeroPoints = 1,
            Features = [new SelectedAssetFeature("training_facilities")]
        });

        Assert.Equal(1, _f.Derived.CalculateTeamwork(sheet));

        // A base without the feature grants none, which is what stops the count being "how many
        // bases does this character own".
        sheet.Headquarters.Add(new OwnedHeadquarters("The Shed") { PerkHeroPoints = 1 });
        Assert.Equal(1, _f.Derived.CalculateTeamwork(sheet));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// One printed feature line off a stock vehicle's list, as a selection. The two lines that are
    /// not a bare feature name are read as the data's own interpretation block reads them.
    /// </summary>
    private SelectedAssetFeature FeatureFromPrintedLine(string printed)
    {
        if (printed.StartsWith("Passengers ", StringComparison.Ordinal))
        {
            var extra = int.Parse(printed["Passengers ".Length..], CultureInfo.InvariantCulture);
            return new SelectedAssetFeature("passengers") { Units = extra / 4 };
        }

        // "Rader (Sonar)": the Radar Power with the Con printed inside its own Ch.2 entry, taken
        // through Unique Systems, which converts a Hero Point to a Vehicle Point one for one.
        if (printed.StartsWith("Rader", StringComparison.Ordinal))
            return new SelectedAssetFeature("unique_systems") { Units = RadarWithItsSonarCon() };

        var id = GearCatalogue.Slug(printed);
        return new SelectedAssetFeature(id);
    }

    /// <summary>
    /// What Radar costs with the Sonar Con, asked of the calculator rather than written down. A
    /// constant here would have said 3 — Radar without its Con — which is exactly what made the
    /// Submersible look like a misprint.
    /// </summary>
    private int RadarWithItsSonarCon() =>
        _f.Costs.PowerCost(new SelectedPower("radar", 0, [], [new SelectedProCon("sonar")]));
}
