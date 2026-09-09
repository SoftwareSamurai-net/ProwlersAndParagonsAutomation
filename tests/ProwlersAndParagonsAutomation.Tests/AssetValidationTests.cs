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

        // <b>Exactly at the budget, which is where the comparison has to be driven.</b> Two
        // features against three Base Points is under it, and a check written `spent >= budget`
        // would have been just as green — so a base that spends every point its Perk bought,
        // which is the ordinary case, would have been reported over budget. Three of three.
        sheet.Headquarters.Add(new OwnedHeadquarters("The Vault")
        {
            PerkHeroPoints = 1,
            Features =
            [
                new SelectedAssetFeature("hidden"),
                new SelectedAssetFeature("disguised"),
                new SelectedAssetFeature("remote")
            ]
        });

        Assert.Equal(3, _f.Costs.BasePointsSpent(sheet.Headquarters[0]));
        Assert.Equal(3, _f.Costs.BasePointBudget(sheet.Headquarters[0]));
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

        // <b>And one rank below half, which is the boundary the pair above straddles.</b> Legal
        // at 7 and reported at 3 leaves four ranks between them, so a floor loosened by exactly
        // one rank — `Units + 1 >= HalfRoundedUp(Body)`, which is what an off-by-one here would
        // look like — kept both assertions true. Might 6 against Body 14 is the case that tells
        // the two apart, and it is the case a player actually writes down.
        sheet.Vehicles[0] = sheet.Vehicles[0] with
        {
            Features = [new SelectedAssetFeature("mecha") { Units = 6 }]
        };

        var justUnder = Only(sheet, "MECHA_MIGHT_BELOW_HALF_BODY");
        Assert.Equal(6, justUnder.Value);
        Assert.Equal(7, justUnder.Limit);

        // And a machine that is not a Mecha is never asked about, however low its Might would be.
        sheet.Vehicles[0] = sheet.Vehicles[0] with { Features = [] };
        Assert.False(Reports(sheet, "MECHA_MIGHT_BELOW_HALF_BODY"));
    }

    /// <summary>
    /// <b>p.96's Control rules, both ends.</b> Control may not exceed half the Speed, and a
    /// negative Control stops paying back at −3.
    ///
    /// <para><b>Half rounds up</b>, per p.7's glossary — "always round up, regardless of the
    /// context" — which p.96 works on its own page when it calls 4 "half of 7". Speed 10 with
    /// Control 5 is legal, which is what the Jet Fighter on the same page is; so is Speed 7 with
    /// Control 4, and that case has a test of its own because this fixture's even Speed cannot
    /// tell the two roundings apart — see <see cref="AnOddSpeedRoundsItsHalfUp"/>.</para>
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

    /// <summary>
    /// <b>The boundary the fixture above straddles without ever touching: an odd Speed.</b>
    ///
    /// <para>"Control ... can't exceed half the vehicle's Speed" was compared as
    /// <c>Control * 2 &gt; Speed</c>, which is <c>Control &gt; floor(Speed / 2)</c> — right at
    /// every even Speed and a rank too tight at every odd one. Speed 10 cannot see the
    /// difference, so the check above was green while this one rejected machines the book prints:
    /// p.97's Helicopter (Military), Helicopter (Personal) and Jet Pack are each Speed 7d with
    /// Control +4d.</para>
    ///
    /// <para><b>p.7 settles it and p.96 demonstrates it.</b> The glossary: half of an odd number
    /// "always round[s] up, regardless of the context". p.96, a few paragraphs above the sentence
    /// under test: a Foe-piloted sedan with 7d Body is disabled after "4 points of damage (half
    /// of 7)". So half of 7 is 4, Control 4 against Speed 7 is legal, and 5 is not.</para>
    ///
    /// <para>The printed machines are driven off the shipped data rather than typed here, so a
    /// corrected transcription moves this test rather than leaving it asserting a stale pair —
    /// and the sweep asserts what it found before asserting anything about it, because an empty
    /// sweep would satisfy every claim in the loop trivially.</para>
    /// </summary>
    [Fact]
    public void AnOddSpeedRoundsItsHalfUp()
    {
        var sheet = _f.LegalSheet();

        // Legal: 4 is half of 7 the way the book halves.
        sheet.Vehicles.Add(new OwnedVehicle("Whirlybird") { PerkHeroPoints = 2, Speed = 7, Control = 4 });
        Assert.False(Reports(sheet, "VEHICLE_CONTROL_ABOVE_HALF_SPEED"));

        // And one past it is not, with the cap quoted as the same 4 the comparison used.
        sheet.Vehicles[0] = sheet.Vehicles[0] with { Control = 5 };
        var above = Only(sheet, "VEHICLE_CONTROL_ABOVE_HALF_SPEED");
        Assert.Equal(5, above.Value);
        Assert.Equal(4, above.Limit);

        // The second witness: every mundane vehicle the book prints has a Control this rule
        // allows. They are not bought with Vehicle Points, but they are the book's own statement
        // of what a Control against a Speed looks like — and three of them are the case above.
        var printed = _f.Rules.Vehicles.Entries
            .Where(e => e.Vehicles is not null)
            .SelectMany(e => e.Vehicles!)
            .Where(v => v.Control > 0)
            .ToList();

        Assert.True(printed.Count > 20,
            $"Only {printed.Count} printed vehicles with a positive Control were found — the "
            + "sweep below would hold of almost nothing.");

        Assert.Contains(printed, v => v.Speed % 2 == 1 && v.Control == (v.Speed + 1) / 2);

        foreach (var row in printed)
        {
            var machine = new CharacterSheet();
            machine.Vehicles.Add(new OwnedVehicle(row.Name)
            {
                PerkHeroPoints = 99, Speed = row.Speed, Control = row.Control
            });

            Assert.DoesNotContain(_f.Validator.Validate(machine).Issues,
                i => i.Code == "VEHICLE_CONTROL_ABOVE_HALF_SPEED");
        }
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

    /// <summary>
    /// <b>A Gadget's Powers are a fourth place a Pro or Con can sit, and nothing walked them.</b>
    ///
    /// <para><c>CheckModifiers</c> walked the character's Powers, their gear and their Abilities,
    /// and <c>modifiersResolvable</c> — the gate <c>CheckGadget</c> takes before pricing anything
    /// — was therefore true whatever a Gadget's Powers carried. p.94 buys a Gadget's Powers with
    /// the ordinary rules, so <c>GadgetSpend</c> prices them through <c>PowerCost</c>, and
    /// <c>ResolveConCost</c> <em>throws</em> on an id the rulebook does not have. One misspelled
    /// Con inside a Gadget took the whole of <c>Validate</c> out with an
    /// <c>InvalidOperationException</c>, which is the one answer a validator may never give: this
    /// is the failure <c>modifiersResolvable</c> exists to prevent, in the collection it did not
    /// cover.</para>
    ///
    /// <para><b>The control is the same Gadget with the id spelled right</b>, which prices and
    /// reports an ordinary finding — so what is being fixed is the crash, not a Gadget that was
    /// never priceable.</para>
    /// </summary>
    [Fact]
    public void AGadgetPowerCarryingAnUnknownModifierIsReportedRatherThanThrowing()
    {
        var sheet = ABuilder();
        sheet.Gadgets.Add(new BuiltGadget("Exo-frame")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("blast", 20, [], [new SelectedProCon("nonexistant_con")])]
        });

        Assert.True(Reports(sheet, "UNKNOWN_CON"));

        // Not priced, because the answer cannot be had — the same gate an unknown Power takes.
        Assert.False(Reports(sheet, "GADGET_OVER_POOL"));

        // The Gadget is named in the finding, because "Blast" alone does not say whose.
        Assert.Contains(Issues(sheet), i => i.Code == "UNKNOWN_CON" && i.Message.Contains("Exo-frame"));

        // The control: spelled right, it prices, and the finding it was hiding comes back.
        sheet.Gadgets[0] = sheet.Gadgets[0] with
        {
            Powers = [new SelectedPower("blast", 20, [], [new SelectedProCon("item")])]
        };

        Assert.False(Reports(sheet, "UNKNOWN_CON"));
        Assert.True(Reports(sheet, "GADGET_OVER_POOL"));
    }

    /// <summary>
    /// <b>The quantity exploit, one budget down.</b> <c>CheckQuantities</c>' own comment records
    /// a per-rank-per-unit Pro at −1000 driving a Power's rate negative until the rulebook floor
    /// caught it at half a point a rank, so 24 Hero Points of Power cost 6 in silence.
    /// <c>EveryModifier</c> walked three collections and a Gadget's Powers were a fourth: the
    /// same 12d Nullify inside a Gadget priced at 6, which is exactly a Complexity-3 pool, and
    /// the sheet reported <b>nothing at all</b>.
    ///
    /// <para>The Power's own two quantity fields are here for the same reason — a negative rank
    /// or a negative unit count on a Gadget's Power was reported on the character's own Powers
    /// and nowhere else.</para>
    ///
    /// <para><b>Reported, never repaired</b>: the assertions on <c>GadgetSpend</c> pin the price
    /// the sheet actually says, so a future fix that clamped the quantity instead of reporting it
    /// fails here.</para>
    /// </summary>
    [Fact]
    public void ANegativeQuantityInsideAGadgetIsReported()
    {
        var sheet = ABuilder();
        sheet.Gadgets.Add(new BuiltGadget("Exo-frame")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("nullify", 12,
                             [new SelectedProCon("also_x") { Units = -1000 }], []) { SourceId = "magic" }]
        });

        var discount = Only(sheet, "NEGATIVE_UNITS");
        Assert.Equal("also_x", discount.SubjectId);
        Assert.Equal(-1000, discount.Value);
        Assert.Contains("Exo-frame", discount.Message);

        // Never repaired: the discount is still applied, which is what makes it worth reporting.
        // Six out of a pool of six is inside the pool, so nothing else would have said a word.
        Assert.Equal(6, _f.Costs.GadgetSpend(sheet.Gadgets[0]));
        Assert.Equal(6, _f.Costs.GadgetPool(sheet.Gadgets[0]));
        Assert.False(Reports(sheet, "GADGET_OVER_POOL"));

        // And the Power's own two quantities, which had the same silence.
        sheet.Gadgets[0] = sheet.Gadgets[0] with
        {
            Powers = [new SelectedPower("blast", -100), new SelectedPower("immunity", 0) { Units = -20 }]
        };

        var rank = Only(sheet, "NEGATIVE_RANK");
        Assert.Equal(ValidationSubject.Gadget, rank.SubjectKind);
        Assert.Equal(-100, rank.Value);

        var units = Only(sheet, "NEGATIVE_UNITS");
        Assert.Equal(ValidationSubject.Gadget, units.SubjectKind);
        Assert.Equal(-20, units.Value);
    }

    /// <summary>
    /// <b>A Gadget's Abilities and Talents are asked about too, and they were not.</b>
    ///
    /// <para><c>GadgetIsPriceable</c> walked <see cref="BuiltGadget.Powers"/> alone while its own
    /// summary said it walked all three, and <c>GadgetSpend</c> skips a rank whose Trait id
    /// resolves to nothing — the same defensive filter <c>AbilityCost</c> has, which is only
    /// honest because <c>UNKNOWN_ABILITY</c> reports the character's own. A Gadget's ranks live in
    /// their own dictionaries and nothing reported those, so a Gadget with <c>"mightt": 99</c>
    /// spent nothing out of its pool, sat comfortably inside it, and raised no finding.</para>
    ///
    /// <para><b>The control is what makes the pool comparison the point</b>: the same Gadget with
    /// the id spelled right really is over its pool, so the silence being fixed was a finding
    /// hidden by a typo rather than a Gadget that happened to be legal.</para>
    /// </summary>
    [Fact]
    public void AGadgetCarryingAnUnknownTraitIsReportedAndNotPriced()
    {
        var sheet = ABuilder();
        sheet.Gadgets.Add(new BuiltGadget("Exo-frame")
        {
            Complexity   = 3,                       // a pool of six
            AbilityRanks = new Dictionary<string, int> { ["mightt"] = 99 },
            TalentRanks  = new Dictionary<string, int> { ["technologee"] = 99 }
        });

        var ability = Only(sheet, "UNKNOWN_GADGET_ABILITY");
        Assert.Equal(ValidationSubject.Gadget, ability.SubjectKind);
        Assert.Equal("Exo-frame", ability.SubjectId);
        Assert.Equal("mightt", ability.OwnerId);
        Assert.Contains("might", ability.Options);

        var talent = Only(sheet, "UNKNOWN_GADGET_TALENT");
        Assert.Equal("technologee", talent.OwnerId);
        Assert.Contains("technology", talent.Options);

        // Not priced, because the answer cannot be had — the same gate the unknown Power takes.
        Assert.False(Reports(sheet, "GADGET_OVER_POOL"));
        Assert.Equal(0, _f.Costs.GadgetSpend(sheet.Gadgets[0]));

        // The control: spelled right, those ranks are 198 Hero Points out of a pool of six, and
        // the finding the typo was hiding comes back.
        sheet.Gadgets[0] = sheet.Gadgets[0] with
        {
            AbilityRanks = new Dictionary<string, int> { ["might"] = 99 },
            TalentRanks  = new Dictionary<string, int> { ["technology"] = 99 }
        };

        Assert.False(Reports(sheet, "UNKNOWN_GADGET_ABILITY"));
        Assert.False(Reports(sheet, "UNKNOWN_GADGET_TALENT"));

        var over = Only(sheet, "GADGET_OVER_POOL");
        Assert.Equal(198, over.Value);
        Assert.Equal(6, over.Limit);
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

    // ── Quantities below zero, which pay rather than cost ─────────────────────

    /// <summary>
    /// <b>A vehicle's characteristics are bought from nothing, so a rank below nothing pays
    /// Vehicle Points back.</b>
    ///
    /// <para>Body −20 buys twenty Vehicle Points of features for free — Sensors, Flight,
    /// Spaceflight, a Cargo Hold and a Com System came to exactly nothing on a machine whose Perk
    /// bought it no allowance at all, inside its budget, with no finding anywhere on the sheet.
    /// This is `CheckQuantities`' own documented exploit — a per-unit Perk at −1000 — in a
    /// collection that arrived after every clause it had.</para>
    ///
    /// <para><b>Control is deliberately not here.</b> p.96 makes a negative Control legal and
    /// floors it at −3, which <c>VEHICLE_CONTROL_BELOW_MINIMUM</c> reports; the other three have
    /// no such sentence.</para>
    /// </summary>
    [Fact]
    public void AVehicleRankBelowZeroIsReportedAndStillPaysBack()
    {
        var sheet = _f.LegalSheet();
        sheet.Vehicles.Add(new OwnedVehicle("Ghost")
        {
            Body = -20,
            Features =
            [
                new SelectedAssetFeature("sensors"),      // 10
                new SelectedAssetFeature("flight"),       //  2
                new SelectedAssetFeature("spaceflight"),  //  4
                new SelectedAssetFeature("cargo_hold"),   //  2
                new SelectedAssetFeature("com_system")    //  2
            ]
        });

        // The control first: this really is a machine whose features were free.
        Assert.Equal(0, _f.Costs.VehiclePointsSpent(sheet.Vehicles[0]));
        Assert.Equal(0, _f.Costs.VehiclePointBudget(sheet.Vehicles[0]));
        Assert.False(Reports(sheet, "VEHICLE_OVER_BUDGET"));

        var issue = Only(sheet, "NEGATIVE_RANK");
        Assert.Equal(ValidationSubject.Vehicle, issue.SubjectKind);
        Assert.Equal("Ghost", issue.SubjectId);
        Assert.Equal(-20, issue.Value);
        Assert.Equal(0, issue.Limit);

        // Never repaired: the twenty points are still paid back, which is what the sheet says.
        Assert.Equal(0, _f.Costs.VehiclePointsSpent(sheet.Vehicles[0]));

        // Speed and Weapons are the same field. Control is not — it is legal below zero.
        sheet.Vehicles[0] = sheet.Vehicles[0] with { Body = 0, Speed = -3, Weapons = -4, Control = -3 };
        Assert.Equal(2, Issues(sheet).Count(i => i.Code == "NEGATIVE_RANK"));
        Assert.False(Reports(sheet, "VEHICLE_CONTROL_BELOW_MINIMUM"));
    }

    /// <summary>
    /// <b>A Perk allowance below zero pays the character Hero Points.</b>
    ///
    /// <para>`PerkHeroPoints` is the same unbounded field a Perk's `Units` is, and it reaches
    /// `TotalCost` by the same route. A vehicle recording −1000 took a legal sheet from 18 Hero
    /// Points to −982, and the only thing said about it was a Vehicle Point budget finding, which
    /// mentions no Hero Points at all. `CampaignAssetContribution.HeroPoints` was checked from
    /// the start; these two are the same field and were not.</para>
    /// </summary>
    [Fact]
    public void APerkAllowanceBelowZeroIsReportedOnBothKindsOfAsset()
    {
        var sheet = _f.LegalSheet();
        var honest = _f.Costs.TotalCost(sheet);

        sheet.Vehicles.Add(new OwnedVehicle("Debt") { PerkHeroPoints = -1000 });
        sheet.Headquarters.Add(new OwnedHeadquarters("Overdraft") { PerkHeroPoints = -500 });

        // The control: the sheet really has been paid, which is what makes the silence a defect
        // rather than a tidy-up.
        Assert.Equal(honest - 1500, _f.Costs.TotalCost(sheet));

        var vehicle = Issues(sheet).Single(
            i => i.Code == "NEGATIVE_UNITS" && i.SubjectKind == ValidationSubject.Vehicle);
        Assert.Equal("Debt", vehicle.SubjectId);
        Assert.Equal(-1000, vehicle.Value);

        var headquarters = Issues(sheet).Single(
            i => i.Code == "NEGATIVE_UNITS" && i.SubjectKind == ValidationSubject.Headquarters);
        Assert.Equal("Overdraft", headquarters.SubjectId);
        Assert.Equal(-500, headquarters.Value);

        // Reported, never repaired.
        Assert.Equal(honest - 1500, _f.Costs.TotalCost(sheet));
    }

    /// <summary>
    /// <b>A Gadget's Trait rank below zero pays its pool back.</b> `might: -50` beside a 20d Blast
    /// spends −30 out of a pool of six, which is comfortably inside it — so a Gadget carrying a
    /// Power worth twenty Hero Points was legal and silent.
    /// </summary>
    [Fact]
    public void AGadgetTraitRankBelowZeroIsReported()
    {
        var sheet = ABuilder();
        sheet.Gadgets.Add(new BuiltGadget("Cheat")
        {
            Complexity   = 3,
            Powers       = [new SelectedPower("blast", 20) { SourceId = "tech" }],
            AbilityRanks = new Dictionary<string, int> { ["might"] = -50 }
        });

        // The control: the pool really is being paid back into.
        Assert.Equal(6, _f.Costs.GadgetPool(sheet.Gadgets[0]));
        Assert.True(_f.Costs.GadgetSpend(sheet.Gadgets[0]) < 0);
        Assert.False(Reports(sheet, "GADGET_OVER_POOL"));

        var issue = Only(sheet, "NEGATIVE_RANK");
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Cheat", issue.SubjectId);
        Assert.Equal("might", issue.OwnerId);
        Assert.Equal(-50, issue.Value);
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
