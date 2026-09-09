using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <see cref="AssetFormatter"/> and the block the <c>.txt</c> sheet builds out of it.
///
/// <para><b>Assert on the exact rendered line, never on a fragment</b> — the rule
/// <see cref="GearFormatterTests"/> was written under, and it bites harder here: three of these
/// lines carry two numbers and a unit, and a <c>Contains("25")</c> assertion would pass just as
/// happily with the spend and the budget the wrong way round.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class AssetFormatterTests
{
    private readonly RulesFixture _f;

    public AssetFormatterTests(RulesFixture fixture) => _f = fixture;

    // ── The lines ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A vehicle's headline names its own currency and its Perk's.</b> Both figures, because a
    /// machine's whole story is how much of what the Perk bought it has actually used.
    /// </summary>
    [Fact]
    public void AVehicleLineCarriesTheSpendTheBudgetAndThePerk()
    {
        var vehicle = new OwnedVehicle("The Wing")
        {
            PerkHeroPoints = 2,
            Body = 8, Speed = 10, Control = 5, Weapons = 12,
            Features = [new SelectedAssetFeature("flight")]
        };

        Assert.Equal("The Wing — 42/50 Vehicle Points (2 HP)",
                     AssetFormatter.Describe(vehicle, _f.Costs));

        Assert.Equal("Body 8d · Speed 10d · Control +5 · Weapons 12d",
                     AssetFormatter.Characteristics(vehicle));
    }

    /// <summary>
    /// <b>An unarmed machine says so, and a negative Control prints its sign.</b> The printed
    /// tables give an unarmed vehicle an em dash rather than a zero, and Control is "a modifier
    /// rather than a rank in its own right" — so it carries no "d".
    /// </summary>
    [Fact]
    public void AnUnarmedMachineSaysSoAndNegativeControlKeepsItsSign()
    {
        var submersible = new OwnedVehicle("The Sub") { Body = 9, Speed = 5, Control = -2 };

        Assert.Equal("Body 9d · Speed 5d · Control -2 · unarmed",
                     AssetFormatter.Characteristics(submersible));
    }

    /// <summary>
    /// <b>A base's line is the same shape in the other currency</b>, and a Gadget's says the
    /// direction out loud: the pool was paid out, not spent by the character.
    /// </summary>
    [Fact]
    public void ABaseAndAGadgetNameTheirOwnCurrencies()
    {
        var headquarters = new OwnedHeadquarters("The Loft")
        {
            PerkHeroPoints = 2,
            Features = [new SelectedAssetFeature("training_facilities")]
        };

        Assert.Equal("The Loft — 2/6 Base Points (2 HP)",
                     AssetFormatter.Describe(headquarters, _f.Costs));

        var gadget = new BuiltGadget("Freeze Ray")
        {
            Complexity = 5,
            Powers = [new SelectedPower("blast", 4)]
        };

        Assert.Equal("Freeze Ray (Complexity 5) — 4/10 Hero Points the build paid out",
                     AssetFormatter.Describe(gadget, _f.Costs));
    }

    /// <summary>
    /// <b>A contribution prints what went in and what kind of object it went into.</b> Nothing
    /// about the object itself: that is the campaign's answer, and a machine printed on each
    /// member's sheet would be five copies of it.
    /// </summary>
    [Fact]
    public void AContributionPrintsWhatWentInAndNothingElse()
    {
        Assert.Equal("The Aerie (shared headquarters) — 3 HP put in",
            AssetFormatter.Describe(new CampaignAssetContribution("asset-1")
            {
                Name = "The Aerie", Kind = CampaignAssetContribution.Headquarters, HeroPoints = 3
            }));

        // A record with no name falls back to the id, which is at least the thing itself — the
        // same fallback the exports make for an id the rules do not know.
        Assert.Equal("asset-2 (shared vehicle) — 1 HP put in",
            AssetFormatter.Describe(new CampaignAssetContribution("asset-2") { HeroPoints = 1 }));
    }

    /// <summary>
    /// <b>A feature prints its grade or its count, and a name it cannot resolve prints as the
    /// id.</b> The last one is the "reported, never repaired" rule in a formatter: inventing a
    /// name for an unknown row would hide the thing the validator is about to say.
    /// </summary>
    [Fact]
    public void AFeaturePrintsItsGradeItsCountOrTheIdItCouldNotResolve()
    {
        Assert.Equal("Flight",
            AssetFormatter.Feature(new SelectedAssetFeature("flight"), _f.Rules, onAVehicle: true));

        Assert.Equal("Passengers ×3",
            AssetFormatter.Feature(new SelectedAssetFeature("passengers") { Units = 3 },
                                   _f.Rules, onAVehicle: true));

        // One unit of a per-unit feature prints no count: "Mecha ×1" is noise.
        Assert.Equal("Mecha",
            AssetFormatter.Feature(new SelectedAssetFeature("mecha") { Units = 1 },
                                   _f.Rules, onAVehicle: true));

        Assert.Equal("Size (Awe Inspiring)",
            AssetFormatter.Feature(new SelectedAssetFeature("size") { GradeKey = "awe_inspiring" },
                                   _f.Rules, onAVehicle: false));

        Assert.Equal("teleport_bay",
            AssetFormatter.Feature(new SelectedAssetFeature("teleport_bay"),
                                   _f.Rules, onAVehicle: true));
    }

    // ── The block on the text sheet ───────────────────────────────────────────

    /// <summary>
    /// <b>The whole block is absent from a character who owns nothing</b>, unlike Gear, which
    /// prints "(none)". Nearly every character in the game owns no vehicle and no base, and a
    /// heading over four empty lines on every sheet is furniture.
    /// </summary>
    [Fact]
    public void ACharacterWhoOwnsNothingGetsNoBlockAtAll()
    {
        Assert.DoesNotContain("VEHICLES, BASES & GADGETS", Text(SampleCharacters.Hero()),
                              StringComparison.Ordinal);

        // The positive control: the sheet really did render, and the section beside this one is
        // there. An empty string satisfies every absence.
        Assert.Contains("GEAR", Text(SampleCharacters.Hero()), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And the block carries every asset, its features, and the one figure in Hero Points.</b>
    /// The Perks total is the only thing in the section a tier has a budget for; the Vehicle
    /// Points and Base Points above it are a second currency the Perks already bought.
    /// </summary>
    [Fact]
    public void TheBlockCarriesEveryAssetAndOnlyThePerksInHeroPoints()
    {
        var sheet = SampleCharacters.Hero();
        sheet.TalentRanks["technology"] = 6;

        sheet.Vehicles.Add(new OwnedVehicle("The Wing")
        {
            PerkHeroPoints = 2, Body = 8, Speed = 10, Control = 5, Weapons = 12,
            Features = [new SelectedAssetFeature("flight")]
        });
        sheet.Headquarters.Add(new OwnedHeadquarters("The Loft")
        {
            PerkHeroPoints = 2,
            Features = [new SelectedAssetFeature("science_labs") { GradeKey = "advanced" }]
        });
        sheet.Gadgets.Add(new BuiltGadget("Freeze Ray") { Complexity = 5 });
        sheet.CampaignAssets.Add(new CampaignAssetContribution("asset-1")
        {
            Name = "The Aerie", Kind = CampaignAssetContribution.Vehicle, HeroPoints = 1
        });

        var lines = Text(sheet).Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        Assert.Contains("  • The Wing — 42/50 Vehicle Points (2 HP)", lines);
        Assert.Contains("      Body 8d · Speed 10d · Control +5 · Weapons 12d", lines);
        Assert.Contains("      - Flight", lines);
        Assert.Contains("  • The Loft — 2/6 Base Points (2 HP)", lines);
        Assert.Contains("      - Science Labs (Advanced)", lines);
        Assert.Contains("  • Freeze Ray (Complexity 5) — 0/10 Hero Points the build paid out", lines);
        Assert.Contains("  • The Aerie (shared vehicle) — 1 HP put in", lines);

        // Five Hero Points: two Perks and a contribution. Not fifty, and not fifty-six.
        Assert.Contains("  Perks total: 5 HP", lines);
    }

    /// <summary>
    /// <b>Teamwork prints beside Resolve when a base grants it, and not otherwise.</b> The same
    /// rule the worn Armor rank follows: a line reading "Teamwork: 0" on every sheet in the game
    /// is a figure nobody has.
    ///
    /// <para><b>Only a Hero holds it, and this file cannot know which it has.</b> It behaves
    /// exactly like Resolve — the rules data says so in that word — so it is computed for anybody
    /// and quoted by whoever knows what they are showing. <c>PresentationFlagsTests</c> is what
    /// stops <c>sheets/</c> learning the difference.</para>
    /// </summary>
    [Fact]
    public void TeamworkPrintsOnlyWhenABaseGrantsIt()
    {
        var sheet = SampleCharacters.Hero();
        Assert.DoesNotContain("Teamwork", Text(sheet), StringComparison.Ordinal);

        sheet.Headquarters.Add(new OwnedHeadquarters("The Gym")
        {
            PerkHeroPoints = 1,
            Features = [new SelectedAssetFeature("training_facilities")]
        });

        Assert.Contains("  Teamwork: 1 at the start of each issue (Training Facilities)",
                        Text(sheet).Split('\n').Select(l => l.TrimEnd('\r')));

        // A base without the feature grants none, so the line goes again — which is what stops
        // this being "does the character own a base".
        sheet.Headquarters[0] = sheet.Headquarters[0] with { Features = [] };
        Assert.DoesNotContain("Teamwork", Text(sheet), StringComparison.Ordinal);
    }

    private string Text(CharacterSheet sheet) =>
        CharacterSheetRenderer.RenderText(sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    // ── The figures the sheet cannot reach ────────────────────────────────────

    /// <summary>
    /// <b>Neither export may throw over a mistake the validator already reports.</b>
    ///
    /// <para><c>CostCalculator</c> refuses to price a feature the rulebook does not have, a graded
    /// one with no grade, a Pro or Con that resolves to nothing, or an unresolvable cost variant —
    /// deliberately, because a price is not a thing to guess at. Each of those is an
    /// <c>UNKNOWN_ASSET_FEATURE</c>, an <c>ASSET_FEATURE_NEEDS_GRADE</c>, an <c>UNKNOWN_CON</c> or
    /// an <c>UNKNOWN_GADGET_POWER</c> on the same sheet, so the report being asked for is the one
    /// that would have <em>said so</em>.</para>
    ///
    /// <para><b>Five ways in, and the JSON export survived two of them while the <c>.txt</c> one
    /// survived none.</b> <c>RenderJson</c> guarded a vehicle and a base by asking the calculator
    /// (<c>Priceable</c>) and guarded a Gadget by checking its Power ids — which let an unknown
    /// Con and a bad variant key straight through to a <c>GadgetSpend</c> that throws.
    /// <c>AssetFormatter</c>, which is the whole of the <c>.txt</c> block, asked nothing at all:
    /// one mistyped vehicle feature took the printed sheet down.</para>
    ///
    /// <para>Every case is driven through <b>both</b> renderers, because the two had different
    /// holes and a test that ran one of them would have called the other fixed.</para>
    /// </summary>
    [Theory]
    [InlineData("an unknown vehicle feature")]
    [InlineData("a graded base feature with no grade")]
    [InlineData("an unknown Gadget Power")]
    [InlineData("an unknown Con on a Gadget Power")]
    [InlineData("an unresolvable cost variant on a Gadget Power")]
    public void NeitherExportThrowsOverAnAssetItCannotPrice(string what)
    {
        var sheet = _f.LegalSheet();

        switch (what)
        {
            case "an unknown vehicle feature":
                sheet.Vehicles.Add(new OwnedVehicle("The Barge")
                    { PerkHeroPoints = 1, Features = [new SelectedAssetFeature("teleport_bay")] });
                break;

            case "a graded base feature with no grade":
                sheet.Headquarters.Add(new OwnedHeadquarters("The Vault")
                    { PerkHeroPoints = 1, Features = [new SelectedAssetFeature("size")] });
                break;

            case "an unknown Gadget Power":
                sheet.Gadgets.Add(new BuiltGadget("Whatsit")
                    { Complexity = 3, Powers = [new SelectedPower("time_ray", 4)] });
                break;

            case "an unknown Con on a Gadget Power":
                sheet.Gadgets.Add(new BuiltGadget("Whatsit")
                    { Complexity = 3,
                      Powers = [new SelectedPower("blast", 4, [], [new SelectedProCon("nope")])] });
                break;

            default:
                sheet.Gadgets.Add(new BuiltGadget("Whatsit")
                    { Complexity = 3,
                      Powers = [new SelectedPower("omni_power", 4) { CostVariantKey = "nope" }] });
                break;
        }

        // The positive control: this really is a sheet the validator has something to say about,
        // so the report being asked for is the one that carries the finding.
        var validation = _f.Validator.Validate(sheet);
        Assert.False(validation.IsValid);

        var text = CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived, validation, DateTime.UnixEpoch);

        var json = CharacterSheetRenderer.RenderJson(
            sheet, _f.Rules, _f.Costs, _f.Derived, validation, DateTime.UnixEpoch);

        // The `.txt` block says the spend could not be worked out and keeps the budget, which is
        // the half that is knowable — asserted on the exact sentence, not on "it did not throw".
        Assert.Contains("spend cannot be worked out, out of", text, StringComparison.Ordinal);

        // And the JSON carries a null spend beside a budget that is still a number.
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var spends = new[] { "vehicles", "headquarters", "gadgets" }
            .SelectMany(key => node[key]!.AsArray())
            .Select(entry => entry![entry["gadgets"] is null && entry!["complexity"] is not null
                ? "hero_points_spent"
                : entry!["body"] is not null ? "vehicle_points_spent" : "base_points_spent"])
            .ToList();

        Assert.Single(spends);
        Assert.Null(spends[0]);
    }

    /// <summary>
    /// <b>The control on the test above: an asset that <em>can</em> be priced prints its figure.</b>
    /// A <c>Reachable</c> that returned null for everything would satisfy every assertion up there
    /// and quietly stop every sheet in the app printing a spend.
    /// </summary>
    [Fact]
    public void AnAssetThatCanBePricedStillPrintsItsSpend()
    {
        var sheet = _f.LegalSheet();
        sheet.Vehicles.Add(new OwnedVehicle("The Wing") { PerkHeroPoints = 1, Body = 20 });
        sheet.Gadgets.Add(new BuiltGadget("Freeze Ray")
            { Complexity = 3, Powers = [new SelectedPower("blast", 4)] });

        var text = CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), DateTime.UnixEpoch);

        Assert.Contains("The Wing — 20/25 Vehicle Points (1 HP)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot be worked out", text, StringComparison.Ordinal);
    }
}
