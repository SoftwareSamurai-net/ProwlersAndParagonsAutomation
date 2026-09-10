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

    private static readonly string[] BothRulingCodes =
        ["CAMPAIGN_ASSET_CONTRIBUTION_TOO_LARGE", "CAMPAIGN_ASSET_KIND_MISMATCH"];

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

        // **And the other half of that trade, which was missing.** Reading it as a vehicle rather
        // than throwing is only defensible if somebody is told, and the record's own remarks said
        // it was "reported by the host that draws it" while no host did.
        var said = Assert.Single(_f.Validator.CheckSharedAsset(odd));

        Assert.Equal("UNKNOWN_CAMPAIGN_ASSET_KIND", said.Code);
        Assert.Equal(CampaignAssetContribution.Kinds, said.Options);

        // The positive control: both spellings the book has are not reported.
        Assert.Empty(_f.Validator.CheckSharedAsset(Vehicle()));
        Assert.Empty(_f.Validator.CheckSharedAsset(Base()));
    }

    // ── What Chapter 6 says about the object itself ───────────────────────────

    /// <summary>
    /// <b>A shared machine is held to p.96's two sentences about Control, exactly as a machine one
    /// character owns is.</b> They are the same printed rule over the same rates, and the object
    /// the campaign owns had neither of them: a GM could type Control −20 into the editor and the
    /// only thing the screen said was that the object was comfortably inside its budget — which it
    /// was, because a negative Control pays two Vehicle Points back a rank without limit.
    ///
    /// <para>The under-the-limit case is the positive control, without which a check that reported
    /// every machine would pass both halves.</para>
    /// </summary>
    [Fact]
    public void ASharedMachineIsHeldToTheTwoPrintedSentencesAboutControl()
    {
        var floor = _f.Rules.Assets.Characteristics.NegativeControlMinimum;

        var below = _f.Validator.CheckSharedAsset(Vehicle() with { Speed = 20, Control = floor - 1 });
        var above = _f.Validator.CheckSharedAsset(Vehicle() with { Speed = 4, Control = 3 });
        var legal = _f.Validator.CheckSharedAsset(Vehicle() with { Speed = 20, Control = floor });

        Assert.Equal("VEHICLE_CONTROL_BELOW_MINIMUM", Assert.Single(below).Code);
        Assert.Equal("VEHICLE_CONTROL_ABOVE_HALF_SPEED", Assert.Single(above).Code);
        Assert.Empty(legal);

        // Named, so a GM reading a list of objects can tell which one it is about.
        Assert.Contains("The Wing", below[0].Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A characteristic below zero pays Vehicle Points back, and that is now said.</b> p.96
    /// opens Body, Speed and Weapons at nothing and you spend upward, so Body −20 buys twenty
    /// points of features for nothing — the same exploit the character's own collections carry,
    /// in the one record no sheet walk will ever reach.
    ///
    /// <para><b>Measured against the calculator rather than asserted</b>: the point of the finding
    /// is that the figure beside it is genuinely lower, and a test that only counted findings
    /// would not notice if it stopped being.</para>
    /// </summary>
    [Fact]
    public void ACharacteristicBelowZeroPaysPointsBackAndIsReported()
    {
        var machine = Vehicle() with { Body = -20, Speed = 10 };

        Assert.True(_f.Costs.CampaignAssetPointsSpent(machine)
                    < _f.Costs.CampaignAssetPointsSpent(Vehicle() with { Body = 0, Speed = 10 }),
            "a negative Body no longer pays Vehicle Points back, so this fixture proves nothing");

        var found = Assert.Single(_f.Validator.CheckSharedAsset(machine));

        Assert.Equal("NEGATIVE_RANK", found.Code);
        Assert.Contains("Body -20", found.Message, StringComparison.Ordinal);

        // Reported, never repaired: the figure still answers what the campaign says.
        Assert.Equal(-10, _f.Costs.CampaignAssetPointsSpent(machine));
    }

    /// <summary>
    /// <b>A feature these rules do not have, and one bought no times, are the same two findings a
    /// machine on a sheet gets</b> — one vocabulary for one printed table. A base is checked off
    /// pp.100–103's list and a vehicle off pp.96–100's, which is what the kind decides.
    /// </summary>
    /// <summary>
    /// <b>A shared vehicle is held to p.100's prerequisites exactly as an owned one is.</b> This
    /// exists because the orchestrator's mutation — dropping the prerequisite call from
    /// <c>CheckSharedAsset</c> alone — left every test green: the owned-vehicle path was driven
    /// and the shared path was not, which is the "reached by the wrong route" fault
    /// <c>CLAUDE.md</c> names. The same Submersible without Swimming, off the campaign's copy.
    /// </summary>
    [Fact]
    public void ASharedVehicleIsHeldToTheFeaturePrerequisitesToo()
    {
        var unsupported = Assert.Single(_f.Validator.CheckSharedAsset(Vehicle() with
        {
            Features = [new SelectedAssetFeature("submersible")]
        }));

        Assert.Equal("VEHICLE_FEATURE_PREREQUISITE_BELOW_MINIMUM", unsupported.Code);
        Assert.Equal(ValidationSeverity.Warning, unsupported.Severity);
        Assert.Equal("submersible", unsupported.SubjectId);

        // The positive control: with Swimming aboard, nothing is said.
        Assert.Empty(_f.Validator.CheckSharedAsset(Vehicle() with
        {
            Features = [new SelectedAssetFeature("submersible"), new SelectedAssetFeature("swimming")]
        }));
    }

    [Fact]
    public void AFeatureFaultIsReportedOffWhicheverTableTheKindNames()
    {
        var madeUp = Assert.Single(
            _f.Validator.CheckSharedAsset(Vehicle() with
            {
                Features = [new SelectedAssetFeature("warp_nacelles")]
            }));

        Assert.Equal("UNKNOWN_ASSET_FEATURE", madeUp.Code);

        // A vehicle feature is not a base feature: the same id off the wrong table is unknown.
        var wrongTable = Assert.Single(
            _f.Validator.CheckSharedAsset(Base() with
            {
                Features = [new SelectedAssetFeature("passengers")]
            }));

        Assert.Equal("UNKNOWN_ASSET_FEATURE", wrongTable.Code);

        // And the positive control: off the right table it is not reported at all.
        Assert.Empty(_f.Validator.CheckSharedAsset(Vehicle() with
        {
            Features = [new SelectedAssetFeature("passengers") { Units = 1 }]
        }));

        var nothingBought = Assert.Single(_f.Validator.CheckSharedAsset(Vehicle() with
        {
            Features = [new SelectedAssetFeature("passengers") { Units = -4 }]
        }));

        Assert.Equal("PER_UNIT_WITHOUT_UNITS", nothingBought.Code);
    }

    /// <summary>
    /// <b>A headquarters is asked none of the vehicle questions</b>, because pp.100–103 give it no
    /// characteristics to ask them about — the same reason
    /// <see cref="CostCalculator.CampaignAssetPointsSpent"/> charges for none of them. A base
    /// carrying a Body is somebody's payload being odd, not a rule being broken.
    /// </summary>
    [Fact]
    public void ABaseIsAskedNoneOfTheVehicleQuestions() =>
        Assert.Empty(_f.Validator.CheckSharedAsset(
            Base() with { Body = -9, Speed = -9, Control = -99, Weapons = -9 }));

    /// <summary>
    /// <b>An object with no name at all is reported against its id</b> rather than against an
    /// empty string. A machine on a sheet is refused for having no name, because a sheet
    /// identifies one by its name; a campaign's object is identified by the id every contribution
    /// holds, so there is always something to write the sentence about.
    /// </summary>
    [Fact]
    public void AnObjectWithNoNameIsReportedAgainstItsId()
    {
        var found = Assert.Single(
            _f.Validator.CheckSharedAsset(Vehicle() with { Name = "  ", Control = -99 }));

        Assert.Equal(Id, found.SubjectId);
        Assert.Contains(Id, found.Message, StringComparison.Ordinal);
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

    // ── Ruling 8, 2026-09-10: a contribution's kind against the object's own ──────────

    /// <summary>
    /// <b>The two agreeing is silent, and that is the point of the positive control.</b> Nothing
    /// is reported when a contribution's kind is exactly the asset's own, which is the ordinary
    /// case every host writes.
    /// </summary>
    [Fact]
    public void AgreeingKindsAreNotReported() =>
        Assert.Empty(CharacterValidator.CheckContributionAgainstAsset(Put(2), Vehicle()));

    /// <summary>
    /// <b>A hand-written contribution can disagree with the object it names</b>, and Ruling 8 says
    /// so rather than pricing it at the object's currency in silence.
    /// </summary>
    [Fact]
    public void ADisagreeingKindIsReported()
    {
        var contribution = Put(2) with
        {
            Name = "The Wing", Kind = CampaignAssetContribution.Headquarters
        };

        var issue = Assert.Single(CharacterValidator.CheckContributionAgainstAsset(contribution, Vehicle()));

        Assert.Equal("CAMPAIGN_ASSET_KIND_MISMATCH", issue.Code);
        Assert.Equal(Id, issue.SubjectId);
        Assert.Equal(CampaignAssetContribution.Vehicle, issue.OwnerId);
    }

    /// <summary>
    /// <b>A contribution naming a different object is not this question</b> — that is
    /// <c>CampaignAssets.Orphaned</c>'s, which needs the whole campaign to answer. Handed an
    /// asset it does not name, this reports nothing rather than guessing.
    /// </summary>
    [Fact]
    public void AContributionNamingADifferentAssetIsNotAKindMismatch() =>
        Assert.Empty(CharacterValidator.CheckContributionAgainstAsset(
            Put(2, "some-other-asset") with { Kind = CampaignAssetContribution.Headquarters },
            Vehicle()));

    /// <summary>
    /// <b><see cref="CharacterValidator.CheckSharedAsset"/> reaches both Ruling 7 and Ruling 8 for
    /// every contribution it is handed</b>, so a campaign page reviewing one shared object gets
    /// both checks without a second call per contributor. A contribution naming another asset is
    /// skipped, exactly as <see cref="CostCalculator.CampaignAssetBudget"/> skips it.
    /// </summary>
    [Fact]
    public void CheckSharedAssetAlsoWalksEveryContributionItIsHanded()
    {
        var mismatched = Put(2) with { Kind = CampaignAssetContribution.Headquarters };
        var tooLarge   = Put(CampaignAssetContribution.MaxHeroPoints + 1);
        var elsewhere  = Put(5, "some-other-asset") with { Kind = CampaignAssetContribution.Headquarters };

        var codes = _f.Validator.CheckSharedAsset(Vehicle(), [mismatched, tooLarge, elsewhere])
            .Select(i => i.Code)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            BothRulingCodes.Order(StringComparer.Ordinal),
            codes);

        // The positive control: called with no contributions at all, as every existing call site
        // in this file does, the object-only checks still run and neither new code appears.
        var withoutContributions = _f.Validator.CheckSharedAsset(Vehicle())
            .Select(i => i.Code).ToList();

        Assert.DoesNotContain("CAMPAIGN_ASSET_CONTRIBUTION_TOO_LARGE", withoutContributions);
        Assert.DoesNotContain("CAMPAIGN_ASSET_KIND_MISMATCH", withoutContributions);
    }
}
