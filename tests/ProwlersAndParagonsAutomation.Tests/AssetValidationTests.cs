using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>What the validator says about a vehicle, a headquarters, a Gadget and a contribution to a
/// campaign's shared object.</b>
///
/// <para><b>Reported, never repaired</b> is the thing every test here is really about: each one
/// asserts the finding <em>and</em> that the arithmetic still answers what the sheet says. A
/// validator that quietly clamped a Mecha's Might, or dropped a feature it did not recognise, would
/// hand back a legal character nobody had been told about — the same failure a misspelled field
/// name in a submitted payload is refused for.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class AssetValidationTests
{
    private readonly RulesFixture _f;

    public AssetValidationTests(RulesFixture fixture) => _f = fixture;

    private IReadOnlyList<ValidationIssue> Issues(CharacterSheet sheet) =>
        _f.Validator.Validate(sheet).Issues;

    private bool Reports(CharacterSheet sheet, string code) =>
        Issues(sheet).Any(i => i.Code == code);

    private ValidationIssue Only(CharacterSheet sheet, string code) =>
        Assert.Single(Issues(sheet), i => i.Code == code);

    // ── Budgets, in the second currency ───────────────────────────────────────

    /// <summary>
    /// <b>A machine that costs more Vehicle Points than its Perk bought is reported, in Vehicle
    /// Points.</b> The finding carries both figures, so a caller can act on it without parsing the
    /// sentence — and the Hero Point total is untouched, because a vehicle over budget is not a
    /// character over budget.
    /// </summary>
    [Fact]
    public void AVehicleOverItsVehiclePointBudgetIsReportedAndStillPriced()
    {
        var sheet = _f.LegalSheet();
        var legal = new OwnedVehicle("The Wing") { PerkHeroPoints = 1, Body = 15, Speed = 10 };

        sheet.Vehicles.Add(legal);
        Assert.False(Reports(sheet, "VEHICLE_OVER_BUDGET"));

        // One Hero Point buys twenty-five Vehicle Points; this is twenty-six.
        sheet.Vehicles[0] = legal with { Body = 16 };

        var issue = Only(sheet, "VEHICLE_OVER_BUDGET");
        Assert.Equal(ValidationSubject.Vehicle, issue.SubjectKind);
        Assert.Equal("The Wing", issue.SubjectId);
        Assert.Equal(26, issue.Value);
        Assert.Equal(25, issue.Limit);

        // Never repaired: the machine is still priced at what it says.
        Assert.Equal(26, _f.Costs.VehiclePointsSpent(sheet.Vehicles[0]));
    }

    /// <summary>
    /// <b>The same for a base, in Base Points</b> — three to the Hero Point, and the building
    /// itself free, which is why a base with one feature and one Hero Point is legal.
    /// </summary>
    [Fact]
    public void AHeadquartersOverItsBasePointBudgetIsReported()
    {
        var sheet = _f.LegalSheet();

        sheet.Headquarters.Add(new OwnedHeadquarters("The Vault")
        {
            PerkHeroPoints = 1,
            Features = [new SelectedAssetFeature("hidden"), new SelectedAssetFeature("disguised")]
        });
        Assert.False(Reports(sheet, "HEADQUARTERS_OVER_BUDGET"));

        sheet.Headquarters[0] = sheet.Headquarters[0] with
        {
            Features =
            [
                new SelectedAssetFeature("hidden"),
                new SelectedAssetFeature("disguised"),
                new SelectedAssetFeature("size") { GradeKey = "sprawling" }
            ]
        };

        var issue = Only(sheet, "HEADQUARTERS_OVER_BUDGET");
        Assert.Equal(ValidationSubject.Headquarters, issue.SubjectKind);
        Assert.Equal(4, issue.Value);
        Assert.Equal(3, issue.Limit);
    }

    /// <summary>
    /// <b>One unpriceable machine silences its own budget check and nobody else's.</b> A sheet-wide
    /// gate would have thrown the second vehicle's finding away with the first's — which is the
    /// difference between a report that names what is wrong and one that goes quiet.
    /// </summary>
    [Fact]
    public void AnUnpriceableVehicleDoesNotSilenceTheOneBesideIt()
    {
        var sheet = _f.LegalSheet();

        sheet.Vehicles.Add(new OwnedVehicle("The Mystery")
        {
            PerkHeroPoints = 1,
            Features = [new SelectedAssetFeature("teleport_bay")]
        });
        sheet.Vehicles.Add(new OwnedVehicle("The Barge") { PerkHeroPoints = 1, Body = 40 });

        Assert.True(Reports(sheet, "UNKNOWN_ASSET_FEATURE"));

        var overBudget = Only(sheet, "VEHICLE_OVER_BUDGET");
        Assert.Equal("The Barge", overBudget.SubjectId);
    }

    // ── The two printed constraints ───────────────────────────────────────────

    /// <summary>
    /// <b>p.99's Mecha floor: Might at least half the Body.</b> A floor on what the limbs cost, not
    /// a cap on them — and the finding names the feature with the machine beside it, so a caller
    /// can find the row.
    /// </summary>
    [Fact]
    public void AMechaWithTooLittleMightIsReported()
    {
        var sheet = _f.LegalSheet();

        sheet.Vehicles.Add(new OwnedVehicle("Ironshod")
        {
            PerkHeroPoints = 1,
            Body = 14,
            Features = [new SelectedAssetFeature("mecha") { Units = 3 }]
        });

        var issue = Only(sheet, "MECHA_MIGHT_BELOW_HALF_BODY");
        Assert.Equal(ValidationSubject.AssetFeature, issue.SubjectKind);
        Assert.Equal("mecha", issue.SubjectId);
        Assert.Equal("Ironshod", issue.OwnerId);
        Assert.Equal(3, issue.Value);
        Assert.Equal(7, issue.Limit);

        // Exactly half is legal, which is what "may not be lower than" says.
        sheet.Vehicles[0] = sheet.Vehicles[0] with
        {
            Features = [new SelectedAssetFeature("mecha") { Units = 7 }]
        };
        Assert.False(Reports(sheet, "MECHA_MIGHT_BELOW_HALF_BODY"));

        // And a machine that is not a Mecha is never asked about, however low its Might would be.
        sheet.Vehicles[0] = sheet.Vehicles[0] with { Features = [] };
        Assert.False(Reports(sheet, "MECHA_MIGHT_BELOW_HALF_BODY"));
    }

    /// <summary>
    /// <b>p.96's Control rules, both ends.</b> Control may not exceed half the Speed, and a
    /// negative Control stops paying back at −3.
    ///
    /// <para>The comparison is doubled Control against Speed rather than half of Speed against
    /// Control, because the page prints no rounding rule for this sentence and inventing one would
    /// be this project making up a rule. Speed 10 with Control 5 is therefore legal, which is what
    /// the Jet Fighter on the same page is.</para>
    /// </summary>
    [Fact]
    public void ControlIsHeldToHalfTheSpeedAndToItsFloor()
    {
        var sheet = _f.LegalSheet();

        sheet.Vehicles.Add(new OwnedVehicle("The Dart") { PerkHeroPoints = 2, Speed = 10, Control = 5 });
        Assert.False(Reports(sheet, "VEHICLE_CONTROL_ABOVE_HALF_SPEED"));

        sheet.Vehicles[0] = sheet.Vehicles[0] with { Control = 6 };
        var above = Only(sheet, "VEHICLE_CONTROL_ABOVE_HALF_SPEED");
        Assert.Equal(6, above.Value);
        Assert.Equal(5, above.Limit);

        sheet.Vehicles[0] = sheet.Vehicles[0] with { Control = -3 };
        Assert.False(Reports(sheet, "VEHICLE_CONTROL_BELOW_MINIMUM"));

        sheet.Vehicles[0] = sheet.Vehicles[0] with { Control = -4 };
        var below = Only(sheet, "VEHICLE_CONTROL_BELOW_MINIMUM");
        Assert.Equal(-4, below.Value);
        Assert.Equal(-3, below.Limit);

        // Never repaired: −4 still pays back eight Vehicle Points, which is what the sheet says.
        Assert.Equal(-8, _f.Costs.VehiclePointsSpent(sheet.Vehicles[0] with { Speed = 0 }));
    }

    // ── Features the rulebook does not have ───────────────────────────────────

    /// <summary>
    /// <b>An unknown feature is reported with the whole list to choose from</b>, and a graded one
    /// with no grade is reported with its own grades — the two shapes
    /// <see cref="ValidationIssue.Options"/> exists for.
    /// </summary>
    [Fact]
    public void AnUnknownOrUngradedFeatureIsReportedWithItsOptions()
    {
        var sheet = _f.LegalSheet();
        sheet.Headquarters.Add(new OwnedHeadquarters("The Keep")
        {
            PerkHeroPoints = 3,
            Features = [new SelectedAssetFeature("moat")]
        });

        var unknown = Only(sheet, "UNKNOWN_ASSET_FEATURE");
        Assert.Equal("The Keep", unknown.OwnerId);
        Assert.Contains("training_facilities", unknown.Options);

        sheet.Headquarters[0] = sheet.Headquarters[0] with
        {
            Features = [new SelectedAssetFeature("science_labs")]
        };

        var ungraded = Only(sheet, "ASSET_FEATURE_NEEDS_GRADE");
        Assert.Equal(["advanced", "standard"], ungraded.Options.Order(StringComparer.Ordinal));

        // And the repair the options describe really does clear it, which is what makes them a
        // repair rather than a list.
        sheet.Headquarters[0] = sheet.Headquarters[0] with
        {
            Features = [new SelectedAssetFeature("science_labs") { GradeKey = ungraded.Options[0] }]
        };
        Assert.False(Reports(sheet, "ASSET_FEATURE_NEEDS_GRADE"));
    }

    /// <summary>
    /// <b>A per-unit feature bought no times costs nothing and does nothing</b>, which is the same
    /// finding a per-unit Perk gets and for the same reason: a line on the sheet the character did
    /// not buy.
    /// </summary>
    [Fact]
    public void APerUnitFeatureWithNoUnitsIsReported()
    {
        var sheet = _f.LegalSheet();
        sheet.Vehicles.Add(new OwnedVehicle("The Van")
        {
            PerkHeroPoints = 1,
            Features = [new SelectedAssetFeature("passengers") { Units = 0 }]
        });

        Assert.True(Reports(sheet, "PER_UNIT_WITHOUT_UNITS"));
    }

    // ── Gadgets ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A Gadget that spends more than its pool paid out is reported</b>, and the sentence says
    /// the pool is not the character's own budget — which is the thing somebody reading it will
    /// otherwise assume they can top it up from.
    /// </summary>
    [Fact]
    public void AGadgetOverItsPoolIsReported()
    {
        var sheet = ABuilder();

        sheet.Gadgets.Add(new BuiltGadget("Freeze Ray")
        {
            Complexity = 6,
            Powers = [new SelectedPower("blast", 12)]
        });
        Assert.False(Reports(sheet, "GADGET_OVER_POOL"));

        sheet.Gadgets[0] = sheet.Gadgets[0] with { Powers = [new SelectedPower("blast", 13)] };

        var issue = Only(sheet, "GADGET_OVER_POOL");
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Freeze Ray", issue.SubjectId);
        Assert.Equal(13, issue.Value);
        Assert.Equal(12, issue.Limit);

        // Never charged to the character either way: a Gadget makes nobody more expensive.
        Assert.DoesNotContain(Issues(sheet), i => i.Code == "HP_BUDGET_EXCEEDED");
    }

    /// <summary>
    /// <b>p.94's two prerequisites this sheet can actually answer</b>: six dice of Technology
    /// before the rules open at all, and a Complexity no higher than that Technology.
    ///
    /// <para>They are reported one at a time on purpose — a builder below the minimum is told the
    /// minimum, not also told their Complexity is above a rank they were never allowed to build
    /// at.</para>
    /// </summary>
    [Fact]
    public void TheTwoGadgetPrerequisitesAreReported()
    {
        var sheet = _f.LegalSheet();
        sheet.TalentRanks["technology"] = 4;
        sheet.Gadgets.Add(new BuiltGadget("Bodge") { Complexity = 4 });

        var below = Only(sheet, "GADGET_BUILDER_BELOW_TECHNOLOGY_MINIMUM");
        Assert.Equal(4, below.Value);
        Assert.Equal(6, below.Limit);
        Assert.False(Reports(sheet, "GADGET_COMPLEXITY_ABOVE_TECHNOLOGY"));

        sheet.TalentRanks["technology"] = 6;
        sheet.Gadgets[0] = sheet.Gadgets[0] with { Complexity = 8 };

        var above = Only(sheet, "GADGET_COMPLEXITY_ABOVE_TECHNOLOGY");
        Assert.Equal(8, above.Value);
        Assert.Equal(6, above.Limit);
    }

    /// <summary>
    /// <b>Complexity starts at three</b>, which is the floor the page prints — and the figure is
    /// read off <c>gadgets.json</c> rather than written into the check.
    /// </summary>
    [Fact]
    public void AGadgetBelowTheMinimumComplexityIsReported()
    {
        var sheet = ABuilder();
        sheet.Gadgets.Add(new BuiltGadget("Trinket") { Complexity = 2 });

        var issue = Only(sheet, "GADGET_COMPLEXITY_BELOW_MINIMUM");
        Assert.Equal(2, issue.Value);
        Assert.Equal(_f.Rules.Assets.MinimumGadgetComplexity, issue.Limit);
    }

    /// <summary>
    /// <b>A Power the rulebook does not have is reported rather than throwing</b>, and the pool
    /// comparison is skipped — because the answer genuinely cannot be had, not because it is
    /// inconvenient.
    /// </summary>
    [Fact]
    public void AGadgetCarryingAnUnknownPowerIsReportedAndNotPriced()
    {
        var sheet = ABuilder();
        sheet.Gadgets.Add(new BuiltGadget("Whatsit")
        {
            Complexity = 3,
            Powers = [new SelectedPower("time_ray", 40)]
        });

        Assert.True(Reports(sheet, "UNKNOWN_GADGET_POWER"));
        Assert.False(Reports(sheet, "GADGET_OVER_POOL"));

        // The whole validation still answers rather than throwing, which is what the gate is for.
        Assert.NotEmpty(Issues(sheet));
    }

    // ── Shared objects, and the double-count ──────────────────────────────────

    /// <summary>
    /// <b>A contribution with no id, and one naming a kind Chapter 6 does not have.</b> The id is
    /// what a campaign sums on, so a record without one is points put into nothing.
    /// </summary>
    [Fact]
    public void AContributionWithoutAnIdOrWithAnUnknownKindIsReported()
    {
        var sheet = _f.LegalSheet();
        sheet.CampaignAssets.Add(new CampaignAssetContribution("")
        {
            Name = "The Aerie", Kind = "space_station", HeroPoints = 2
        });

        Assert.True(Reports(sheet, "CAMPAIGN_ASSET_WITHOUT_ID"));

        var kind = Only(sheet, "UNKNOWN_CAMPAIGN_ASSET_KIND");
        Assert.Equal(["headquarters", "vehicle"], kind.Options.Order());

        sheet.CampaignAssets[0] = new CampaignAssetContribution("asset-1")
        {
            Name = "The Aerie", Kind = CampaignAssetContribution.Vehicle, HeroPoints = 2
        };
        Assert.False(Reports(sheet, "CAMPAIGN_ASSET_WITHOUT_ID"));
        Assert.False(Reports(sheet, "UNKNOWN_CAMPAIGN_ASSET_KIND"));
    }

    /// <summary>
    /// <b>The same machine recorded twice is warned about and charged twice.</b> A warning rather
    /// than an error because nothing here is illegal — a character may buy the Perk twice over —
    /// and the total stays what the sheet says, which is the "never repaired" half.
    /// </summary>
    [Fact]
    public void RecordingAnAssetPerkTwiceIsWarnedAboutAndStillCharged()
    {
        var sheet = _f.LegalSheet();
        sheet.Vehicles.Add(new OwnedVehicle("The Wing") { PerkHeroPoints = 1, Body = 10 });

        Assert.False(Reports(sheet, "ASSET_PERK_RECORDED_TWICE"));
        var withOne = _f.Costs.TotalCost(sheet);

        sheet.Perks.Add(new SelectedPerk("unique_vehicle", 2));

        var issue = Only(sheet, "ASSET_PERK_RECORDED_TWICE");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("unique_vehicle", issue.SubjectId);

        // Both are charged, which is what the warning says and why it is worth saying.
        Assert.Equal(withOne + 2, _f.Costs.TotalCost(sheet));

        // And a character who bought the Perk without detailing a machine is not warned at all —
        // that is an ordinary way to hold the points.
        var perkOnly = _f.LegalSheet();
        perkOnly.Perks.Add(new SelectedPerk("unique_vehicle", 2));
        Assert.False(Reports(perkOnly, "ASSET_PERK_RECORDED_TWICE"));
    }

    // ── Nameless things ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>All three are identified by their name, so a nameless one cannot be reported about.</b>
    /// The same rule gear follows, and the finding is filed against the character rather than
    /// naming a subject it cannot name.
    /// </summary>
    [Theory]
    [InlineData("VEHICLE_WITHOUT_NAME")]
    [InlineData("HEADQUARTERS_WITHOUT_NAME")]
    [InlineData("GADGET_WITHOUT_NAME")]
    public void ANamelessAssetIsReportedAgainstTheCharacter(string code)
    {
        var sheet = ABuilder();
        sheet.Vehicles.Add(new OwnedVehicle("  "));
        sheet.Headquarters.Add(new OwnedHeadquarters(""));
        sheet.Gadgets.Add(new BuiltGadget(" ") { Complexity = 3 });

        var issue = Only(sheet, code);
        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Null(issue.SubjectId);
    }

    /// <summary>A legal sheet whose Technology is high enough to build a Gadget at all.</summary>
    private CharacterSheet ABuilder()
    {
        var sheet = _f.LegalSheet();
        sheet.TalentRanks["technology"] = 6;
        return sheet;
    }
}
