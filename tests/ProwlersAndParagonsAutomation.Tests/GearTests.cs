using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Custom gear, from Ch.6 (pp.92-93). Two things are being checked: that the twelve
/// features carry the prices the rulebook prints, and that the surrounding rules hold —
/// mundane gear is free, and gear floors at 0 HP rather than at a Power's 1.
///
/// <para>The prices below are transcribed from the book. If one of these fails, check
/// p.92 — do not edit the expected value to match the code.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class GearTests
{
    private readonly RulesFixture _f;

    public GearTests(RulesFixture fixture) => _f = fixture;

    private static CharacterSheet SheetWith(params SelectedGear[] gear)
    {
        var sheet = RulesFixture.StandardSheet();
        foreach (var g in gear) sheet.Gear.Add(g);
        return sheet;
    }

    // ── Against the rulebook ─────────────────────────────────────────────────

    [Theory]
    [InlineData("bonded", 1)]
    [InlineData("collapsible", 1)]
    [InlineData("concealed", 1)]
    [InlineData("deflecting", 2)]
    [InlineData("fitted", 2)]
    [InlineData("hardened", 2)]
    [InlineData("masterpiece", 2)]
    [InlineData("reinforced", 2)]
    [InlineData("silenced", 1)]
    [InlineData("upgraded", 2)]
    public void FlatFeaturesMatchTheRulebook(string id, int expected)
    {
        var feature = _f.Rules.GetGearFeature(id);

        Assert.NotNull(feature);
        Assert.Equal("flat", feature.CostType);
        Assert.Equal(expected, feature.Cost);
    }

    /// <summary>
    /// Two features are printed at "1 to 2 Hero Points": the cheaper grade gives +1d and
    /// the dearer one +2d. Accurate applies against an active defence, Powerful against
    /// a passive one.
    /// </summary>
    [Theory]
    [InlineData("accurate", "accurate", "very_accurate")]
    [InlineData("powerful", "powerful", "very_powerful")]
    public void GradedFeaturesCostOneOrTwo(string id, string lowKey, string highKey)
    {
        var feature = _f.Rules.GetGearFeature(id);

        Assert.NotNull(feature);
        Assert.Equal("flat_variable", feature.CostType);
        Assert.Null(feature.Cost);
        Assert.NotNull(feature.CostRange);
        Assert.Equal(1, feature.CostRange[lowKey]);
        Assert.Equal(2, feature.CostRange[highKey]);
    }

    [Fact]
    public void ThereAre12CustomFeatures() => Assert.Equal(12, _f.Rules.GearFeatures.Count);

    [Fact]
    public void NoFeatureCostsMoreThanTwoHeroPoints() =>
        Assert.All(_f.Rules.GearFeatures, f =>
        {
            var prices = f.Cost is { } c ? [c] : f.CostRange!.Values.ToList();
            Assert.All(prices, p => Assert.InRange(p, 1, 2));
        });

    [Fact]
    public void EveryFeatureHasANameDescriptionAndSource() =>
        Assert.All(_f.Rules.GearFeatures, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Name));
            Assert.False(string.IsNullOrWhiteSpace(f.Description));
            Assert.False(string.IsNullOrWhiteSpace(f.AppliesTo));
            Assert.Contains("Ch.6", f.SourceRef, StringComparison.Ordinal);
        });

    [Fact]
    public void FeatureIdsAreUnique()
    {
        var ids = _f.Rules.GearFeatures.Select(f => f.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // ── Costing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ch.6: "Players don't have to worry about buying mundane gear... none of this needs
    /// to be tracked." A free-text item is free, and the wizard's gear step charging
    /// nothing for it is correct rather than a gap.
    /// </summary>
    [Fact]
    public void MundaneGearIsFree()
    {
        var sheet = SheetWith(new SelectedGear("Padded costume"), new SelectedGear("Grapple gun"));

        Assert.Equal(0, _f.Costs.TotalGearCost(sheet));
        Assert.All(sheet.Gear, g => Assert.False(g.IsCustomised));
    }

    [Fact]
    public void FeaturesOnOneItemAddUp()
    {
        var gear = new SelectedGear("Jo Sticks")
        {
            Features = [new("upgraded"), new("accurate", "very_accurate")]
        };

        Assert.Equal(4, _f.Costs.GearCost(gear));       // 2 + 2
    }

    [Fact]
    public void AGradedFeatureChargesTheGradeChosen()
    {
        int Cost(string grade) =>
            _f.Costs.GearCost(new SelectedGear("Pistol") { Features = [new("powerful", grade)] });

        Assert.Equal(1, Cost("powerful"));
        Assert.Equal(2, Cost("very_powerful"));
    }

    [Fact]
    public void AGradedFeatureWithoutAGradeThrows() =>
        Assert.Throws<InvalidOperationException>(() =>
            _f.Costs.GearCost(new SelectedGear("Pistol") { Features = [new("accurate")] }));

    /// <summary>
    /// Ch.6: "Pros and Cons cost the same when applied to gear as when applied to Powers."
    /// </summary>
    [Fact]
    public void ProsAndConsCostTheSameOnGearAsOnAPower()
    {
        var gear = new SelectedGear("Rifle")
        {
            Features = [new("upgraded")],           // +2
            Pros     = [new("penetrating")],
            Cons     = [new("charges", "3_per_scene")]
        };

        var expected = 2
                     + _f.Rules.GetPro("penetrating")!.CostModifier!.Value
                     + _f.Rules.GetCon("charges")!.CostModifierRange!["3_per_scene"];

        Assert.Equal(Math.Max(0, expected), _f.Costs.GearCost(gear));
    }

    /// <summary>
    /// Gear's floor is not a Power's. Ch.6: "Regardless of Cons, no piece of gear can cost
    /// less than 0 Hero Points (in other words, no piece of gear will end up granting you
    /// extra Hero Points)." A Power in the same position would floor at 1.
    /// </summary>
    [Fact]
    public void ConsCannotTakeGearBelowZero()
    {
        var gear = new SelectedGear("Cursed blade")
        {
            Features = [new("bonded")],             // 1 HP
            Cons     = [new("limited", "severely_limited"), new("burnout")]
        };

        Assert.Equal(0, _f.Costs.GearCost(gear));
    }

    /// <summary>
    /// The Item Con is not credited against a piece of gear. Ch.6 says "As physical
    /// objects, every piece of gear has the Item Con" — that is what gear *is*, not a
    /// discount to claim, and Item is absent from the list of Cons the same page says are
    /// commonly applied to gear. Crediting it would make every 1 HP feature free.
    /// </summary>
    [Fact]
    public void GearIsNotAutomaticallyDiscountedForBeingAnItem()
    {
        var gear = new SelectedGear("Silenced pistol") { Features = [new("silenced")] };

        Assert.Equal(1, _f.Costs.GearCost(gear));
    }

    [Fact]
    public void CustomisedGearCountsTowardTheTotal()
    {
        var plain = RulesFixture.StandardSheet();
        var armed = SheetWith(new SelectedGear("Jo Sticks") { Features = [new("upgraded")] });

        Assert.Equal(_f.Costs.TotalCost(plain) + 2, _f.Costs.TotalCost(armed));
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public void AnUnknownFeatureIsAnError()
    {
        var sheet = SheetWith(new SelectedGear("Mystery box") { Features = [new("teleporting")] });

        Assert.Contains(_f.Validator.Validate(sheet).Issues,
            i => i is { Code: "UNKNOWN_GEAR_FEATURE", Severity: ValidationSeverity.Error });
    }

    [Fact]
    public void AGradedFeatureWithNoGradeIsAnError()
    {
        var sheet = SheetWith(new SelectedGear("Pistol") { Features = [new("accurate")] });

        Assert.Contains(_f.Validator.Validate(sheet).Issues,
            i => i is { Code: "GEAR_FEATURE_NEEDS_GRADE", Severity: ValidationSeverity.Error });
    }

    [Fact]
    public void GearAlreadyFreeAfterConsWarns()
    {
        var sheet = SheetWith(new SelectedGear("Cursed blade")
        {
            Features = [new("bonded")],
            Cons     = [new("limited", "severely_limited")]
        });

        Assert.Contains(_f.Validator.Validate(sheet).Issues,
            i => i is { Code: "GEAR_COST_AT_MINIMUM", Severity: ValidationSeverity.Warning });
    }

    /// <summary>
    /// Two-Fisted is what lets a matched pair be customised "for the price of one", so a
    /// pair recorded without the Power is claiming a discount it has not bought.
    /// </summary>
    [Fact]
    public void APairedItemNeedsTheTwoFistedPower()
    {
        var gear = new SelectedGear("Jo Sticks")
        {
            Features = [new("upgraded")],
            PairedUnderTwoFisted = true
        };

        var without = SheetWith(gear);
        Assert.Contains(_f.Validator.Validate(without).Issues,
            i => i is { Code: "TWO_FISTED_PAIR_WITHOUT_POWER", Severity: ValidationSeverity.Error });

        var with = SheetWith(gear);
        with.SelectedPowers.Add(new SelectedPower("two_fisted", 0));
        Assert.DoesNotContain(_f.Validator.Validate(with).Issues,
            i => i.Code == "TWO_FISTED_PAIR_WITHOUT_POWER");

        // The pair is one entry, so it is already charged once and no more.
        Assert.Equal(2, _f.Costs.GearCost(gear));
    }

    /// <summary>
    /// Gear that cannot be priced has to be reported, not thrown. The validator already
    /// checks Power selections before anything prices a Power for exactly this reason;
    /// gear needs the same ordering, and did not have it at first.
    /// </summary>
    [Fact]
    public void UnpriceableGearIsReportedRatherThanThrown()
    {
        var sheet = SheetWith(
            new SelectedGear("Mystery box") { Features = [new("teleporting")] },
            new SelectedGear("Pistol") { Features = [new("accurate")] });

        var result = _f.Validator.Validate(sheet);   // must not throw

        Assert.Contains(result.Issues, i => i.Code == "UNKNOWN_GEAR_FEATURE");
        Assert.Contains(result.Issues, i => i.Code == "GEAR_FEATURE_NEEDS_GRADE");

        // The budget check is skipped rather than run on a total that cannot be computed.
        Assert.DoesNotContain(result.Issues, i => i.Code == "HP_BUDGET_EXCEEDED");
    }

    /// <summary>
    /// <b>A misspelled catalogue row is reported and does not take the budget check down with
    /// it.</b>
    ///
    /// <para><c>CheckGear</c> returns a <em>priceability</em> flag, and the caller spends it on
    /// one thing: whether to run <c>CheckHpBudget</c>, which is one of the two limits a character
    /// can break. <c>GearCost</c> never reads <c>CatalogueId</c>, so an id that resolves to
    /// nothing cannot make an item unpriceable — and clearing the flag for one silently dropped
    /// <c>HP_BUDGET_EXCEEDED</c> from a character that really was over. That is the failure
    /// <c>CheckTierSelected</c>'s own doc comment calls the worst answer this validator can give:
    /// a character reported legal, confidently, about something that is not.</para>
    /// </summary>
    [Fact]
    public void AnUnknownCatalogueRowIsReportedWithoutSilencingTheBudget()
    {
        var overspent = _f.LegalSheet();

        // Ranks alone, all of them under the Standard tier's 12d cap, so the only thing wrong with
        // this character is the price: nothing else here can be mistaken for the finding below.
        foreach (var ability in overspent.AbilityRanks.Keys.ToList())
            overspent.AbilityRanks[ability] = 12;

        foreach (var talent in overspent.TalentRanks.Keys.ToList())
            overspent.TalentRanks[talent] = 6;

        // The positive control, and the whole instrument: without it, "the finding is there" is
        // satisfied by a character nobody could be over budget on.
        Assert.Contains(_f.Validator.Validate(overspent).Issues,
            i => i.Code == "HP_BUDGET_EXCEEDED");

        overspent.Gear.Add(new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battel_axe"
        });

        var result = _f.Validator.Validate(overspent);

        Assert.Contains(result.Issues,
            i => i is { Code: "UNKNOWN_GEAR_CATALOGUE_ROW", Severity: ValidationSeverity.Error });

        Assert.Contains(result.Issues, i => i.Code == "HP_BUDGET_EXCEEDED");
    }

    [Fact]
    public void PlainGearRaisesNoIssues()
    {
        var sheet = SheetWith(new SelectedGear("Padded costume"));

        Assert.DoesNotContain(_f.Validator.Validate(sheet).Issues,
            i => i.Code.StartsWith("GEAR", StringComparison.Ordinal) ||
                 i.Code.StartsWith("TWO_FISTED", StringComparison.Ordinal));
    }
}
