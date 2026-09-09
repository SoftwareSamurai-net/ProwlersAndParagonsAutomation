using System.Reflection;
using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The campaign's own shared vehicle or headquarters</b>: the shape it is written down in, and
/// what <see cref="CostCalculator"/> makes of it.
///
/// <para><see cref="AssetCostTests"/> covers the machine one character owns, whose budget is that
/// character's Perk. This is the other half of the owner's answer to the pooling question — the
/// object several characters paid for, whose budget is the sum of what they put in and which lives
/// on the campaign because a <see cref="CharacterSheet"/> is one character.</para>
///
/// <para><b>The arithmetic is the same arithmetic, deliberately.</b> Two of the tests below exist
/// only to say so: a shared machine and an owned one with the same characteristics and features
/// must cost the same, because they are priced from one printed table. If those two ever disagree,
/// one of the two calculations has drifted and the currency figures below say which.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class CampaignAssetTests
{
    private readonly RulesFixture _f;

    public CampaignAssetTests(RulesFixture fixture) => _f = fixture;

    private const string Id = "a_0000000000000000000000";

    private static CampaignAsset Vehicle(string id = Id) =>
        new(id, CampaignAssetContribution.Vehicle, "The Wing");

    private static CampaignAsset Base(string id = Id) =>
        new(id, CampaignAssetContribution.Headquarters, "The Roost");

    private static CampaignAssetContribution Put(int heroPoints, string id = Id) =>
        new(id) { HeroPoints = heroPoints };

    // ── The budget is what the members put in ─────────────────────────────────

    /// <summary>
    /// <b>Three members putting in one Hero Point each buy the same machine one member putting in
    /// three would.</b> That is the whole of the pooling rule, and it is the reason the object is
    /// on the campaign rather than on a sheet.
    /// </summary>
    [Fact]
    public void PooledHeroPointsBuyTheSameVehiclePointsAsOnePersonsWould()
    {
        var rate = _f.Rules.Assets.VehiclePointsPerHeroPoint;

        var pooled = _f.Costs.CampaignAssetBudget(Vehicle(), [Put(1), Put(1), Put(1)]);
        var alone  = _f.Costs.CampaignAssetBudget(Vehicle(), [Put(3)]);

        Assert.Equal(3 * rate, pooled);
        Assert.Equal(pooled, alone);
    }

    /// <summary>
    /// <b>A headquarters converts at the other rate, and the two are not interchangeable.</b> p.96
    /// gives twenty-five Vehicle Points to the Hero Point and p.100 three Base Points; a figure
    /// that came out the same for both would mean the kind was being ignored.
    /// </summary>
    [Fact]
    public void TheKindDecidesWhichCurrencyTheHeroPointsBuy()
    {
        var vehicle = _f.Costs.CampaignAssetBudget(Vehicle(), [Put(4)]);
        var house   = _f.Costs.CampaignAssetBudget(Base(), [Put(4)]);

        Assert.Equal(4 * _f.Rules.Assets.VehiclePointsPerHeroPoint, vehicle);
        Assert.Equal(4 * _f.Rules.Assets.BasePointsPerHeroPoint, house);
        Assert.NotEqual(vehicle, house);
    }

    /// <summary>
    /// <b>A contribution to a different object does not fund this one.</b> The caller hands in
    /// every contribution it collected across every member, so the filter on the id is what stops
    /// one machine's budget quietly counting the base's.
    ///
    /// <para>The positive control is the same call with the id put back: an implementation that
    /// summed nothing at all would satisfy the first assertion on its own.</para>
    /// </summary>
    [Fact]
    public void AContributionNamingAnotherObjectDoesNotFundThisOne()
    {
        var elsewhere = Put(10, "a_1111111111111111111111");

        Assert.Equal(0, _f.Costs.CampaignAssetBudget(Vehicle(), [elsewhere]));
        Assert.Equal(10 * _f.Rules.Assets.VehiclePointsPerHeroPoint,
                     _f.Costs.CampaignAssetBudget(Vehicle(), [elsewhere with { AssetId = Id }]));
    }

    // ── The spend is the same arithmetic an owned machine gets ────────────────

    /// <summary>
    /// <b>A shared machine and an owned one with the same statistics cost the same.</b> They are
    /// priced from one printed table, and the two records exist only because one carries a Perk
    /// and the other does not — so any difference in this figure is a second copy of the
    /// arithmetic that has drifted.
    /// </summary>
    [Fact]
    public void ASharedVehicleIsPricedExactlyAsAnOwnedOneIs()
    {
        SelectedAssetFeature[] features =
            [new("flight"), new("passengers") { Units = 2 }];

        var owned = new OwnedVehicle("The Wing")
        {
            PerkHeroPoints = 2, Body = 8, Speed = 10, Control = 3, Weapons = 6, Features = features
        };

        var shared = Vehicle() with
        {
            Body = 8, Speed = 10, Control = 3, Weapons = 6, Features = features
        };

        // Not zero, or the equality below is satisfied by two calculations that both do nothing.
        Assert.True(_f.Costs.VehiclePointsSpent(owned) > 0);
        Assert.Equal(_f.Costs.VehiclePointsSpent(owned), _f.Costs.CampaignAssetPointsSpent(shared));
    }

    /// <summary>A shared base is priced exactly as an owned one is, on the same terms.</summary>
    [Fact]
    public void ASharedHeadquartersIsPricedExactlyAsAnOwnedOneIs()
    {
        SelectedAssetFeature[] features = [new("training_facilities")];

        var owned  = new OwnedHeadquarters("The Roost") { PerkHeroPoints = 3, Features = features };
        var shared = Base() with { Features = features };

        Assert.True(_f.Costs.BasePointsSpent(owned) > 0);
        Assert.Equal(_f.Costs.BasePointsSpent(owned), _f.Costs.CampaignAssetPointsSpent(shared));
    }

    /// <summary>
    /// <b>A base is charged for its features and for nothing else, even when somebody has typed a
    /// Body onto it.</b> pp.100–103 give a headquarters no characteristics at all, so there is no
    /// printed rate to charge one at — and charging it at the vehicle table's rate would be this
    /// project inventing a rule. A host that lets the figure be typed reports it; the calculator
    /// does not price it.
    /// </summary>
    [Fact]
    public void AHeadquartersCharacteristicsAreNotCharged()
    {
        var bare    = Base();
        var mistake = bare with { Body = 9, Speed = 9, Control = 9, Weapons = 9 };

        Assert.Equal(_f.Costs.CampaignAssetPointsSpent(bare),
                     _f.Costs.CampaignAssetPointsSpent(mistake));
    }

    /// <summary>
    /// <b>A kind neither spelling names is priced as a vehicle</b>, which is the reading
    /// <see cref="CampaignAsset.IsHeadquarters"/> takes and the direction a host reports from. The
    /// alternative — throwing — would make one mistyped field take a whole campaign page down.
    /// </summary>
    [Fact]
    public void AnUnknownKindIsPricedAsAVehicleRatherThanThrowing()
    {
        var odd = new CampaignAsset(Id, "spaceship", "The Wing") { Body = 4 };

        Assert.False(odd.IsHeadquarters);
        Assert.Equal(_f.Costs.CampaignAssetPointsSpent(Vehicle() with { Body = 4 }),
                     _f.Costs.CampaignAssetPointsSpent(odd));
    }

    // ── The written-down shape ────────────────────────────────────────────────

    /// <summary>
    /// <b>A campaign that has never owned anything shared writes the same bytes it wrote before
    /// this existed.</b> That is what the null is for: an empty array in every payload would be a
    /// byte an older campaign does not carry, for a state that is not a decision anybody made.
    /// </summary>
    [Fact]
    public void ACampaignWithNoSharedObjectWritesNoAssetsKey()
    {
        var written = JsonSerializer.Serialize(
            new Campaign("g_0000000000000000000000", "Nightfall", "standard", 8, false),
            CharacterSheetJson.Options);

        Assert.DoesNotContain("Assets", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An object survives being written down and read back, features and all.</b> The payload
    /// is the only way one reaches a member's browser — the server stores it as an opaque string
    /// and never parses a word of it — so a field that does not round-trip is a field no member
    /// ever sees.
    /// </summary>
    [Fact]
    public void ASharedObjectSurvivesTheRoundTrip()
    {
        var campaign = new Campaign("g_0000000000000000000000", "Nightfall", "standard", 8, false)
        {
            Assets =
            [
                Vehicle() with
                {
                    Body = 8, Speed = 10, Control = 3, Weapons = 6,
                    Features = [new SelectedAssetFeature("passengers") { Units = 2 }]
                },
                Base("a_1111111111111111111111") with
                {
                    Features = [new SelectedAssetFeature("science_labs") { GradeKey = "advanced" }]
                }
            ]
        };

        var read = JsonSerializer.Deserialize<Campaign>(
            JsonSerializer.Serialize(campaign, CharacterSheetJson.Options),
            CharacterSheetJson.Options);

        Assert.NotNull(read);
        Assert.Equal(2, CampaignAsset.On(read).Count);
        Assert.Equal(8, CampaignAsset.On(read)[0].Body);
        Assert.Equal(6, CampaignAsset.On(read)[0].Weapons);
        Assert.Equal(2, CampaignAsset.On(read)[0].Features[0].Units);
        Assert.True(CampaignAsset.On(read)[1].IsHeadquarters);
        Assert.Equal("advanced", CampaignAsset.On(read)[1].Features[0].GradeKey);
    }

    /// <summary><see cref="CampaignAsset.On"/> reads the null as "none" rather than throwing.</summary>
    [Fact]
    public void ACampaignsAssetsReadTheNullAsNone()
    {
        var campaign = new Campaign("g_0000000000000000000000", "Nightfall", null, null, false);

        Assert.Null(campaign.Assets);
        Assert.Empty(CampaignAsset.On(campaign));
    }

    /// <summary>
    /// <b>Every characteristic an owned machine has, the campaign's own object has under the same
    /// name.</b>
    ///
    /// <para><b>The names are what the guard is about, not the fields.</b> Both records are priced
    /// from p.96's one table through one expression, so the arithmetic cannot diverge — what can is
    /// a rename on one side, which leaves a payload spelling <c>Weapons</c> where the sheet spells
    /// something else, and every host copying the wrong one. <see cref="OwnedVehicle.Name"/> and
    /// <see cref="OwnedVehicle.PerkHeroPoints"/> are the two deliberate exceptions: the first is
    /// positional on both records and the second is the difference between them, stated at
    /// length on <see cref="CampaignAsset"/>.</para>
    ///
    /// <para>The count is the positive control. Two empty sets are equal, so a reflection call
    /// that has stopped matching anything would satisfy the loop below and say nothing.</para>
    /// </summary>
    [Fact]
    public void TheSharedObjectNamesAVehiclesCharacteristicsTheSameWay()
    {
        static IReadOnlyDictionary<string, Type> Shape(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name is not ("Name" or "PerkHeroPoints" or "EqualityContract"))
                .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.Ordinal);

        var owned  = Shape(typeof(OwnedVehicle));
        var shared = Shape(typeof(CampaignAsset));

        Assert.Equal(5, owned.Count);   // Body, Speed, Control, Weapons, Features

        foreach (var (name, type) in owned)
        {
            Assert.True(shared.TryGetValue(name, out var mirrored),
                $"OwnedVehicle.{name} has no counterpart on the campaign's own object, so a "
                + "machine the table paid for cannot record what a machine one Hero owns can.");

            Assert.True(type == mirrored,
                $"OwnedVehicle.{name} is {type.Name} and the campaign's own object spells it "
                + $"{mirrored!.Name}. One printed table prices both.");
        }
    }
}
