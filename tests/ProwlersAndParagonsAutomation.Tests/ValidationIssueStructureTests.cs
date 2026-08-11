using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The machine-readable half of a validation issue, which exists so that something other
/// than a person can act on a finding.
///
/// <para><b>Asserting that the fields are populated would be theatre.</b> A field can hold
/// the wrong id, the value and the limit can be the wrong way round, and an option list can
/// offer something the data will not accept — and every one of those passes a test that only
/// checks for a non-null. So most of what follows <em>uses</em> the structure: it reads an
/// issue, applies the repair the structure implies, re-validates, and asserts the finding is
/// gone. A field that does not survive that is not carrying what it claims.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ValidationIssueStructureTests
{
    private readonly RulesFixture _f;

    public ValidationIssueStructureTests(RulesFixture f) => _f = f;

    private static CharacterSheet Legal()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw("code"));
        return sheet;
    }

    private ValidationIssue Issue(CharacterSheet sheet, string code) =>
        Assert.Single(_f.Validator.Validate(sheet).Issues, i => i.Code == code);

    private bool Reports(CharacterSheet sheet, string code) =>
        _f.Validator.Validate(sheet).Issues.Any(i => i.Code == code);

    // ── Repairing from the structure alone ────────────────────────────────

    /// <summary>
    /// The canonical repair: a Trait over the cap. Everything needed to fix it — which
    /// Trait, which kind of Trait, what it is and what it may be — comes off the issue, and
    /// nothing is read out of the sentence.
    /// </summary>
    [Fact]
    public void ATraitOverTheCapCanBeBroughtBackFromTheIssueAlone()
    {
        var sheet = Legal();
        sheet.AbilityRanks["intellect"] = 40;

        var issue = Issue(sheet, "TRAIT_ABOVE_CAP");

        Assert.Equal(ValidationSubject.Ability, issue.SubjectKind);
        Assert.Equal("intellect", issue.SubjectId);
        Assert.Equal(40, issue.Value);
        Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank, issue.Limit);

        // The repair, made entirely out of the issue.
        sheet.AbilityRanks[issue.SubjectId!] = issue.Limit!.Value;

        Assert.False(Reports(sheet, "TRAIT_ABOVE_CAP"));
    }

    /// <summary>
    /// The same, for a Talent — because the kind is what tells a caller which dictionary to
    /// write into, and getting it wrong would put an ability id among the talents, where it
    /// would be reported as an unknown Trait rather than fixed.
    /// </summary>
    [Fact]
    public void ATalentOverTheCapNamesTheTalentsRatherThanTheAbilities()
    {
        var sheet = Legal();
        sheet.TalentRanks["academics"] = 40;

        var issue = Issue(sheet, "TRAIT_ABOVE_CAP");

        Assert.Equal(ValidationSubject.Talent, issue.SubjectKind);
        Assert.Equal("academics", issue.SubjectId);

        sheet.TalentRanks[issue.SubjectId!] = issue.Limit!.Value;
        Assert.False(Reports(sheet, "TRAIT_ABOVE_CAP"));
    }

    /// <summary>
    /// A choice the rules fix the set of. The options have to be the keys the data accepts,
    /// not the prose the message sets them as: the message humanises
    /// <c>very_accurate</c> to "Very Accurate", and writing that back would be a second
    /// unknown key rather than a repair.
    /// </summary>
    [Fact]
    public void AVariantCanBeChosenFromTheOptionsTheIssueOffers()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("omni_power", 2));

        var issue = Issue(sheet, "POWER_VARIANT_NOT_CHOSEN");
        Assert.NotEmpty(issue.Options);

        foreach (var option in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPowers.Add(new SelectedPower("omni_power", 2) { CostVariantKey = option });

            Assert.False(Reports(repaired, "POWER_VARIANT_NOT_CHOSEN"));

            // And the choice is one the engine can actually price, which is the whole reason
            // the finding is an error rather than a warning.
            Assert.True(_f.Costs.TotalCost(repaired) > 0);
        }
    }

    /// <summary>
    /// A gear feature is the one subject that is not top-level: it sits on an item, and the
    /// item is named by nothing else. Without the owner a caller can find the feature and not
    /// the thing to change it on.
    /// </summary>
    [Fact]
    public void AGearFeatureIssueNamesBothTheFeatureAndTheItemItIsOn()
    {
        var sheet = Legal();
        sheet.Gear.Add(new SelectedGear("Pistol") { Features = [new("accurate")] });

        var issue = Issue(sheet, "GEAR_FEATURE_NEEDS_GRADE");

        Assert.Equal(ValidationSubject.GearFeature, issue.SubjectKind);
        Assert.Equal("accurate", issue.SubjectId);
        Assert.Equal("Pistol", issue.OwnerId);

        foreach (var grade in issue.Options)
        {
            var repaired = Legal();
            repaired.Gear.Add(new SelectedGear(issue.OwnerId!)
            {
                Features = [new(issue.SubjectId!, grade)]
            });

            Assert.False(Reports(repaired, "GEAR_FEATURE_NEEDS_GRADE"));
        }
    }

    /// <summary>
    /// The six Sources, offered wherever one is missing or wrong. Every option has to be a
    /// Source the rules have, or the repair swaps one unknown id for another.
    /// </summary>
    [Fact]
    public void ASourceIssueOffersTheSixSourcesAndEachOneClosesIt()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("communications", 0));

        var issue = Issue(sheet, "RANKLESS_POWER_WITHOUT_SOURCE");

        Assert.Equal(_f.Rules.Sources.Select(s => s.Id).Order(), issue.Options.Order());
        Assert.All(issue.Options, o => Assert.NotNull(_f.Rules.GetSource(o)));

        foreach (var source in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPowers.Add(new SelectedPower("communications", 0) { SourceId = source });

            Assert.False(Reports(repaired, "RANKLESS_POWER_WITHOUT_SOURCE"));
        }
    }

    /// <summary>
    /// A tier has to be chosen before anything else can be checked, so the one finding a
    /// fresh character always produces carries the list of tiers to choose from.
    /// </summary>
    [Fact]
    public void TheMissingTierCarriesTheTiersToChooseFrom()
    {
        var issue = Issue(new CharacterSheet(), "NO_TIER_SELECTED");

        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Equal(_f.Rules.Tiers.Select(t => t.Id).Order(), issue.Options.Order());

        foreach (var tier in issue.Options)
            Assert.False(Reports(new CharacterSheet { SelectedTierId = tier }, "NO_TIER_SELECTED"));
    }

    /// <summary>
    /// <b>A misspelled tier used to be reported as a legal character.</b> Both limits a
    /// character can break — the Hero Point budget and the Trait Cap — hang off the tier, and
    /// both were skipped when it could not be found. So a 99d Ability on tier
    /// <c>"stanadrd"</c> came back with no findings at all, which is the worst answer a
    /// validator has available: confident, and wrong.
    /// </summary>
    [Fact]
    public void AnUnknownTierIsReportedRatherThanTurningEveryLimitOff()
    {
        var sheet = Legal();
        sheet.SelectedTierId = "stanadrd";
        sheet.AbilityRanks["might"] = 99;

        var issue = Issue(sheet, "UNKNOWN_TIER");

        Assert.Equal(ValidationSubject.Tier, issue.SubjectKind);
        Assert.Equal("stanadrd", issue.SubjectId);
        Assert.Equal(_f.Rules.Tiers.Select(t => t.Id).Order(), issue.Options.Order());
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // And choosing a real one from the options gets **both** limits back, not just the
        // one. Checking only the Trait Cap here left deleting the budget check undetected.
        sheet.SelectedTierId = issue.Options[0];
        Assert.True(Reports(sheet, "TRAIT_ABOVE_CAP"));

        var expensive = Legal();
        expensive.SelectedTierId = "stanadrd";
        foreach (var a in _f.Rules.Abilities) expensive.AbilityRanks[a.Id] = 12;
        foreach (var t in _f.Rules.Talents) expensive.TalentRanks[t.Id] = 12;

        Assert.False(Reports(expensive, "HP_BUDGET_EXCEEDED"));
        expensive.SelectedTierId = "standard";
        Assert.True(Reports(expensive, "HP_BUDGET_EXCEEDED"));
    }

    /// <summary>
    /// A package that is not one of the three grants nothing and costs nothing, so it was
    /// silently no package — and the character paid full rate for the ranks it would have
    /// covered, which is where the published Heroes were found to be 4 HP out once before.
    /// </summary>
    [Fact]
    public void AnUnknownStartingPackageIsReported()
    {
        var sheet = Legal();
        sheet.SelectedPackageId = "hero";       // the id is hero_package

        var issue = Issue(sheet, "UNKNOWN_PACKAGE");

        Assert.Equal(_f.Rules.CreationRules.OptionalPackages.Select(p => p.Id).Order(),
                     issue.Options.Order());

        foreach (var package in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPackageId = package;
            Assert.False(Reports(repaired, "UNKNOWN_PACKAGE"));
        }
    }

    /// <summary>
    /// Ranks bought against a Trait that does not exist. The character was charged for them
    /// and did not have them: the total counted the ranks, the Trait Cap check saw them, and
    /// the sheet printed nothing. It cost Hero Points for a Trait that is not in the game.
    ///
    /// <para>Found through the skill's own example, which named a Talent the rulebook does not
    /// have and produced a clean report.</para>
    /// </summary>
    [Theory]
    [InlineData("athletics", "UNKNOWN_TALENT", false)]
    [InlineData("strength", "UNKNOWN_ABILITY", true)]
    public void RanksAgainstATraitThatDoesNotExistAreReported(string id, string code, bool ability)
    {
        var sheet = Legal();
        if (ability) sheet.AbilityRanks[id] = 4; else sheet.TalentRanks[id] = 4;

        var issue = Issue(sheet, code);

        Assert.Equal(id, issue.SubjectId);
        Assert.NotEmpty(issue.Options);
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // Every option is a Trait of the right kind, so a caller repairing by choosing one
        // cannot land in the other dictionary.
        Assert.All(issue.Options, o => Assert.NotNull(
            ability ? _f.Rules.GetAbility(o)?.Id : _f.Rules.GetTalent(o)?.Id));
    }

    /// <summary>
    /// <b>The cheapest character in the game.</b> Nothing rejected the same Con listed twice,
    /// and every cost floors at zero, so three Burnouts cancelled a 12d Ability exactly: six
    /// Abilities at the Trait Cap for 0 Hero Points, reported legal with an empty issue list.
    /// Scaled up with Powers it bought 216 HP of character inside a 125 HP budget.
    /// </summary>
    [Fact]
    public void TheSameConTwiceIsRefusedRatherThanDiscountedTwice()
    {
        var sheet = Legal();
        foreach (var ability in _f.Rules.Abilities)
        {
            sheet.AbilityRanks[ability.Id] = 12;
            sheet.AbilityModifiers[ability.Id] = [new("burnout"), new("burnout"), new("burnout")];
        }

        Assert.True(Reports(sheet, "DUPLICATE_CON"));
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // And with the duplicates gone the character is priced, not free.
        var honest = Legal();
        foreach (var ability in _f.Rules.Abilities) honest.AbilityRanks[ability.Id] = 12;

        Assert.True(_f.Costs.TotalCost(honest) > 0);
        Assert.False(Reports(honest, "DUPLICATE_CON"));
    }

    /// <summary>The same trap on a Power, where the floor is per Power rather than per Trait.</summary>
    [Fact]
    public void TheSameConTwiceOnAPowerIsRefused()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 12,
            [], [new SelectedProCon("burnout"), new SelectedProCon("burnout")])
        { SourceId = "tech" });

        var issue = Issue(sheet, "DUPLICATE_CON");
        Assert.Equal("burnout", issue.SubjectId);

        // The Power's id, not its printed name: the owner is there to be looked up.
        Assert.Equal("blast", issue.OwnerId);
    }

    /// <summary>
    /// A flaw taken twice counted twice: twice against the maximum of three, and twice into
    /// Resolve for a Condition or Plot Hook, so three copies of one flaw were worth 21 Resolve
    /// where one is worth 19.
    /// </summary>
    [Fact]
    public void TheSameFlawTwiceIsRefused()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = 3;
        sheet.Flaws.Add(new SelectedFlaw("enemy"));
        sheet.Flaws.Add(new SelectedFlaw("enemy"));

        var issue = Issue(sheet, "DUPLICATE_FLAW");

        Assert.Equal(ValidationSubject.Flaw, issue.SubjectKind);
        Assert.Equal("enemy", issue.SubjectId);
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // One copy is the ordinary case, and its Resolve is the figure the duplicate inflated.
        var single = RulesFixture.StandardSheet();
        single.AbilityRanks["might"] = 3;
        single.Flaws.Add(new SelectedFlaw("enemy"));

        Assert.False(Reports(single, "DUPLICATE_FLAW"));
        Assert.True(_f.Derived.CalculateResolve(sheet) > _f.Derived.CalculateResolve(single));
    }

    /// <summary>
    /// A Power listed twice is a warning, not an error: the rulebook does not forbid it, and
    /// two Blasts with different Pros is a shape a player might want. What is certainly wrong
    /// is that the budget charges for both while the sheet shows the first.
    /// </summary>
    [Fact]
    public void TheSamePowerTwiceIsAWarningRatherThanARefusal()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 2) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });

        var issue = Issue(sheet, "DUPLICATE_POWER");

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.True(_f.Validator.Validate(sheet).IsValid);
        Assert.Equal("blast", issue.SubjectId);
    }

    /// <summary>
    /// <b>The Brute Option is Overkill on Might.</b> The Ability was not checked, so Overkill
    /// or Weak on any of the six halved it — 12d Intellect for 6 HP, legal, with one Con on it.
    /// Ch.2 p.17 names Might and nothing else.
    /// </summary>
    [Fact]
    public void OnlyMightIsHalvedByTheBruteOption()
    {
        var might = Legal();
        might.AbilityRanks["might"] = 12;
        might.AbilityModifiers["might"] = [new("overkill")];

        var intellect = Legal();
        intellect.AbilityRanks["intellect"] = 12;
        intellect.AbilityModifiers["intellect"] = [new("overkill")];

        Assert.Equal(6, _f.Costs.AbilityCost(might));
        Assert.Equal(12, _f.Costs.AbilityCost(intellect));
    }

    /// <summary>
    /// A total that wraps is not a total. Each component sums checked, but the additions
    /// between them did not, so a large enough character came to a negative number of Hero
    /// Points and the budget check passed in silence.
    /// </summary>
    [Fact]
    public void ATotalThatWouldOverflowIsReportedRatherThanWrapped()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("determination", 0)
        { Units = 400_000_000, SourceId = "innate" });
        sheet.Perks.Add(new SelectedPerk("contacts", 200_000_000));

        var result = _f.Validator.Validate(sheet);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code is "CHARACTER_NOT_PRICEABLE" or "HP_BUDGET_EXCEEDED");
        Assert.Throws<OverflowException>(() => _f.Costs.TotalCost(sheet));
    }

    /// <summary>
    /// Ranks against a Trait the rulebook does not have are reported and <b>not charged for</b>.
    /// The total used to include them, so the report printed a price covering a Trait the
    /// character could not possibly have, beside the error saying it does not exist.
    /// </summary>
    [Fact]
    public void AnUnknownTraitIsNotChargedFor()
    {
        var sheet = Legal();
        sheet.AbilityRanks["might"]    = 4;
        sheet.TalentRanks["athletics"] = 3;
        sheet.AbilityRanks["strength"] = 5;

        var honest = Legal();
        honest.AbilityRanks["might"] = 4;

        Assert.Equal(_f.Costs.TotalCost(honest), _f.Costs.TotalCost(sheet));
        Assert.True(Reports(sheet, "UNKNOWN_TALENT"));
        Assert.True(Reports(sheet, "UNKNOWN_ABILITY"));
    }

    /// <summary>
    /// <b>A Power's own Pro or Con needs its grade checked too.</b> The branch that resolves a
    /// Pro or Con printed inside a Power's entry skipped the grade check and went straight on,
    /// so Drain carrying its own ungraded Only X threw out of the validator — the eleventh
    /// shape of character to do that, on the one branch the tenth fix did not cover.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("nope")]
    public void APowerSpecificConPricedByGradeIsCheckedLikeAnyOther(string? key)
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("drain", 6,
            [], [new SelectedProCon("only_x", key)]) { SourceId = "magic" });

        var issue = Issue(sheet, "CON_VARIANT_NOT_CHOSEN");
        Assert.NotEmpty(issue.Options);

        foreach (var grade in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPowers.Add(new SelectedPower("drain", 6,
                [], [new SelectedProCon("only_x", grade)]) { SourceId = "magic" });

            Assert.False(Reports(repaired, "CON_VARIANT_NOT_CHOSEN"));
            Assert.True(_f.Costs.TotalCost(repaired) > 0);
        }
    }

    /// <summary>
    /// A quantity on a Pro or Con was the one of the five that nothing bounded, and it buys a
    /// discount: a per-rank-per-unit Pro at −1000 drove a Power's rate to −498, which the
    /// rulebook floor caught at half a point per rank, so a 24 HP Power cost 6 in silence.
    /// </summary>
    [Fact]
    public void ANegativeQuantityOnAProIsRefused()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("nullify", 12,
            [new SelectedProCon("also_x") { Units = -1000 }], []) { SourceId = "magic" });

        var issue = Issue(sheet, "NEGATIVE_UNITS");

        Assert.Equal("also_x", issue.SubjectId);
        Assert.Equal(-1000, issue.Value);
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    /// <summary>
    /// A quantity large enough to wrap the multiplication. Making the grand total checked was
    /// not enough — the wrap happened in the per-unit multiplication underneath it, so
    /// Determination at 500,000,000 units cost 5 HP and gave 500,000,016 Resolve at exit 0.
    /// </summary>
    [Theory]
    [InlineData("determination")]
    [InlineData("alternate_form")]
    public void AQuantityLargeEnoughToWrapACostIsReported(string powerId)
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower(powerId, 0)
        { Units = 600_000_000, SourceId = "innate" });

        Assert.False(_f.Validator.Validate(sheet).IsValid);
        Assert.Throws<OverflowException>(() => _f.Costs.TotalCost(sheet));
    }

    /// <summary>
    /// Zero of a thing priced by the unit is not a purchase: it costs nothing and does nothing,
    /// so it is a line on the sheet the character did not buy.
    /// </summary>
    [Fact]
    public void APerUnitPurchaseOfNothingIsReported()
    {
        var sheet = Legal();
        sheet.Perks.Add(new SelectedPerk("contacts", 0));

        var issue = Issue(sheet, "PER_UNIT_WITHOUT_UNITS");

        Assert.Equal("contacts", issue.SubjectId);
        Assert.Equal(0, issue.Value);
        Assert.Equal(1, issue.Limit);

        // A flat perk is not affected: Units means nothing to one.
        var flat = Legal();
        flat.Perks.Add(new SelectedPerk("fame"));
        Assert.False(Reports(flat, "PER_UNIT_WITHOUT_UNITS"));
    }

    /// <summary>No package at all is the ordinary case and is not a finding.</summary>
    [Fact]
    public void NoStartingPackageIsNotAnIssue()
    {
        Assert.False(Reports(Legal(), "UNKNOWN_PACKAGE"));
    }

    // ── Invariants across every finding ───────────────────────────────────

    /// <summary>
    /// Sheets chosen to make the validator say as many different things as it can, so that
    /// the invariants below are asserted over the whole surface rather than over the handful
    /// of findings somebody remembered.
    /// </summary>
    public static TheoryData<string> Cases() => [.. CaseNames];

    private static readonly string[] CaseNames =
    [
        "no tier", "over budget", "above cap", "no flaws", "too many flaws",
        "unknown ids", "gear", "ranks on a rankless power", "unresolved selections",
        "iconic", "unknown tier", "unknown package", "unknown traits", "unknown modifiers",
        "ungraded modifiers", "negative quantities", "gear at its floor", "power at its floor",
        "unpriceable", "duplicates", "per-unit with no units",
        "power-specific ungraded", "sample villain"
    ];

    private CharacterSheet Build(string which)
    {
        switch (which)
        {
            case "no tier":
                return new CharacterSheet();

            case "over budget":
            {
                var sheet = Legal();
                foreach (var a in _f.Rules.Abilities) sheet.AbilityRanks[a.Id] = 12;
                foreach (var t in _f.Rules.Talents) sheet.TalentRanks[t.Id] = 12;
                return sheet;
            }

            case "above cap":
            {
                var sheet = Legal();
                sheet.AbilityRanks["intellect"] = 40;
                sheet.TalentRanks["academics"]  = 40;
                sheet.SelectedPowers.Add(new SelectedPower("blast", 40));
                return sheet;
            }

            case "no flaws":
                return RulesFixture.StandardSheet();

            case "too many flaws":
            {
                var sheet = RulesFixture.StandardSheet();
                foreach (var flaw in _f.Rules.Flaws.Take(10)) sheet.Flaws.Add(new SelectedFlaw(flaw.Id));
                return sheet;
            }

            case "unknown ids":
            {
                var sheet = Legal();
                sheet.Flaws.Add(new SelectedFlaw("being_far_too_tall"));
                sheet.SelectedPowers.Add(new SelectedPower("chronomancy", 3));
                sheet.SelectedPowers.Add(new SelectedPower("blast", 3) { SourceId = "cosmic" });
                sheet.AbilitySources["might"]     = "cosmic";
                sheet.TalentSources["academics"]  = "cosmic";
                sheet.AbilitySources["telepathy"] = "tech";
                return sheet;
            }

            case "gear":
            {
                var sheet = Legal();
                sheet.Gear.Add(new SelectedGear("Mystery box") { Features = [new("teleporting")] });
                sheet.Gear.Add(new SelectedGear("Pistol") { Features = [new("accurate")] });
                sheet.Gear.Add(new SelectedGear("Jo Sticks")
                {
                    Features = [new("upgraded")],
                    PairedUnderTwoFisted = true
                });
                return sheet;
            }

            case "ranks on a rankless power":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("invisibility", 4));
                sheet.SelectedPowers.Add(new SelectedPower("communications", 0));
                return sheet;
            }

            case "unresolved selections":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("boost", 2));
                sheet.SelectedPowers.Add(new SelectedPower("omni_power", 4));
                return sheet;
            }

            case "iconic":
            {
                var sheet = Legal();
                sheet.SelectedTierId = "iconic";
                return sheet;
            }

            case "unknown tier":
            {
                var sheet = Legal();
                sheet.SelectedTierId = "stanadrd";
                return sheet;
            }

            case "unknown package":
            {
                var sheet = Legal();
                sheet.SelectedPackageId = "hero";
                return sheet;
            }

            case "unknown traits":
            {
                var sheet = Legal();
                sheet.AbilityRanks["strength"] = 4;
                sheet.TalentRanks["athletics"] = 4;
                sheet.Perks.Add(new SelectedPerk("time_machine"));
                return sheet;
            }

            case "unknown modifiers":
            {
                var sheet = Legal();
                sheet.AbilityRanks["might"] = 6;
                sheet.SelectedPowers.Add(new SelectedPower("armor", 2,
                    [new SelectedProCon("teleporty")], [new SelectedProCon("wibble")])
                { SourceId = "tech" });
                sheet.Gear.Add(new SelectedGear("Sword") { Cons = [new("nonsuch")] });
                sheet.AbilityModifiers["intellect"] = [new SelectedProCon("overkill")];
                return sheet;
            }

            case "ungraded modifiers":
            {
                // A Pro and a Con priced by grade, one with no key and one with a key the
                // rulebook does not have. Both threw out of the middle of the total before
                // they were checked.
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("blast", 3,
                    [new SelectedProCon("area_burst")],
                    [new SelectedProCon("charges", "eleventy_per_scene")])
                { SourceId = "tech" });
                sheet.SelectedPowers.Add(new SelectedPower("boost", 2) { BaselineTraitId = "wibble" });
                sheet.SelectedPowers.Add(new SelectedPower("omni_power", 1) { CostVariantKey = "narrow-ish" });
                sheet.Gear.Add(new SelectedGear("Pistol")
                {
                    Features = [new("accurate", "extremely_accurate")]
                });
                return sheet;
            }

            case "negative quantities":
            {
                var sheet = Legal();
                sheet.AbilityRanks["might"]    = -50;
                sheet.TalentRanks["academics"] = -3;
                sheet.SelectedPowers.Add(new SelectedPower("blast", -4) { SourceId = "tech" });
                sheet.SelectedPowers.Add(new SelectedPower("immunity", 0)
                { Units = -20, SourceId = "tech" });
                sheet.Perks.Add(new SelectedPerk("contacts", -1000));
                sheet.SelectedPowers.Add(new SelectedPower("nullify", 6,
                    [new SelectedProCon("also_x") { Units = -1000 }], []) { SourceId = "magic" });
                return sheet;
            }

            case "power-specific ungraded":
            {
                // Drain's own Only X Con is priced by grade, and the branch that handles a
                // Power's own Pros and Cons skipped the grade check entirely — so this threw
                // out of the validator rather than being reported.
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("drain", 6,
                    [], [new SelectedProCon("only_x")]) { SourceId = "magic" });
                return sheet;
            }

            case "per-unit with no units":
            {
                var sheet = Legal();
                sheet.Perks.Add(new SelectedPerk("contacts", 0));
                sheet.SelectedPowers.Add(new SelectedPower("determination", 0)
                { Units = 0, SourceId = "innate" });
                return sheet;
            }

            case "gear at its floor":
            {
                var sheet = Legal();
                sheet.Gear.Add(new SelectedGear("Battered helmet")
                {
                    Features = [new("bonded")],
                    Cons     = [new("item"), new("unreliable")]
                });
                return sheet;
            }

            case "power at its floor":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("armor", 4,
                    [], [new SelectedProCon("burnout")])
                { SourceId = "tech" });
                return sheet;
            }

            case "duplicates":
            {
                var sheet = Legal();
                sheet.Flaws.Add(new SelectedFlaw("code"));
                sheet.AbilityRanks["might"] = 12;
                sheet.AbilityModifiers["might"] = [new("burnout"), new("burnout")];
                sheet.SelectedPowers.Add(new SelectedPower("blast", 2,
                    [new SelectedProCon("subtle"), new SelectedProCon("subtle")], [])
                { SourceId = "tech" });
                sheet.SelectedPowers.Add(new SelectedPower("blast", 3) { SourceId = "tech" });
                return sheet;
            }

            case "unpriceable":
            {
                // The backstop. Nothing here should reach it — every gap above is reported by
                // name — so this case exists to prove the report stays a report if one ever
                // does, rather than to provoke a particular code.
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("boost", 2));
                sheet.Perks.Add(new SelectedPerk("time_machine"));
                return sheet;
            }

            default:
                return SampleCharacters.Villain();
        }
    }

    private List<ValidationIssue> IssuesFor(string which) =>
        [.. _f.Validator.Validate(Build(which)).Issues];

    /// <summary>
    /// <b>The invariants below are only worth what <see cref="Cases"/> reaches, and nothing
    /// used to hold that list to anything.</b> Two codes added in the same change as those
    /// invariants were not in it, and one of the invariants would have failed had they been —
    /// so all three ran green over a surface that did not include the work they were written
    /// for.
    ///
    /// <para>So the list is checked against the validator's own source: every code it can
    /// construct has to be provoked by some case here. A check added later cannot be quietly
    /// exempt from the structural rules, because adding it fails this test until a sheet that
    /// produces it exists.</para>
    /// </summary>
    private static readonly string[] UnprovokableCodes =
        ["POWER_MECHANICS_UNVERIFIED", "POWER_DESCRIPTION_UNVERIFIED", "CHARACTER_NOT_PRICEABLE"];

    [Fact]
    public void EveryCodeTheValidatorCanReportIsProvokedBySomeCase()
    {
        var source = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "engine", "CharacterValidator.cs"));

        var declared = new Regex(@"ValidationSeverity\.\w+,\s*""([A-Z_]+)""",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(source)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(declared);

        // Three codes cannot be provoked from the shipped rules, and each is exempt for a
        // reason rather than for convenience:
        //
        //   The two POWER_*_UNVERIFIED warnings fire on a per-entry flag that no entry in
        //   data/rules/ sets — every Power is verified. Provoking them would mean shipping a
        //   rules file with a flag set to make a test go green.
        //
        //   CHARACTER_NOT_PRICEABLE is the backstop inside CheckHpBudget, and being
        //   unreachable is the whole of its job: every gap that used to reach it is now
        //   reported by name. Demanding a payload for it would mean leaving one of those
        //   holes open on purpose. If one is ever found, it belongs in Cases() as a bug.
        declared.ExceptWith(UnprovokableCodes);

        var provoked = CaseNames
            .SelectMany(IssuesFor)
            .Select(i => i.Code)
            .ToHashSet(StringComparer.Ordinal);

        var missing = declared.Except(provoked).Order().ToList();

        Assert.True(missing.Count == 0,
            "No sheet in Cases() produces: " + string.Join(", ", missing) +
            ". Add one, or the structural invariants below never see these codes.");
    }

    /// <summary>
    /// <b>Every error says what it is about.</b> This is the rule a check added later would
    /// otherwise quietly break — a new error with no subject reads exactly like the others
    /// until something tries to act on it.
    ///
    /// <para>Warnings are deliberately exempt: one of them names every Power whose wording is
    /// unverified in a single finding, and there is no one subject for it to have.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryErrorSaysWhatItIsAbout(string which)
    {
        foreach (var issue in IssuesFor(which).Where(i => i.Severity == ValidationSeverity.Error))
            Assert.True(issue.SubjectKind != ValidationSubject.None,
                $"The error {issue.Code} carries no subject, so nothing can act on it: {issue.Message}");
    }

    /// <summary>
    /// An issue about a named thing has to name it. A subject kind with no id says "something
    /// among the abilities is wrong", which is not a repair anybody can make.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ASubjectThatIsNotTheWholeCharacterIsNamed(string which)
    {
        foreach (var issue in IssuesFor(which))
        {
            if (issue.SubjectKind is ValidationSubject.None or ValidationSubject.Character) continue;

            Assert.False(string.IsNullOrWhiteSpace(issue.SubjectId),
                $"{issue.Code} says it is about a {issue.SubjectKind} and does not say which.");
        }
    }

    /// <summary>
    /// <b>Value and limit the right way round.</b> Swapping them is the likeliest way this
    /// structure goes wrong and the hardest to see: the report still reads plausibly, and a
    /// repair loop moves the character further from legal on every pass.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueBreachesItsLimitInTheDirectionTheCodeNames(string which)
    {
        foreach (var issue in IssuesFor(which))
        {
            if (issue.Value is not { } value || issue.Limit is not { } limit) continue;

            if (issue.Code.Contains("MIN_NOT_MET", StringComparison.Ordinal)
                || issue.Code.StartsWith("NEGATIVE_", StringComparison.Ordinal)
                || issue.Code.EndsWith("WITHOUT_UNITS", StringComparison.Ordinal))
                Assert.True(value < limit, $"{issue.Code}: {value} is not below its minimum of {limit}.");
            else if (issue.Code.EndsWith("AT_MINIMUM", StringComparison.Ordinal))
                Assert.Equal(limit, value);      // already at the floor; that is the finding
            else
                Assert.True(value > limit, $"{issue.Code}: {value} does not exceed its limit of {limit}.");
        }
    }

    /// <summary>
    /// An option is a value the caller writes back into the character, so every one of them
    /// has to be a value the data will accept. Offering a humanised name, or an id from the
    /// wrong collection, is worse than offering nothing: it looks actionable.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryOptionOfferedIsOneTheRulesAccept(string which)
    {
        foreach (var issue in IssuesFor(which))
        {
            foreach (var option in issue.Options)
            {
                Assert.False(string.IsNullOrWhiteSpace(option), $"{issue.Code} offers a blank option.");

                var known =
                    _f.Rules.GetTier(option) is not null ||
                    _f.Rules.GetSource(option) is not null ||
                    _f.Rules.GetFlaw(option) is not null ||
                    _f.Rules.GetPower(option) is not null ||
                    _f.Rules.GetGearFeature(option) is not null ||
                    _f.Rules.GetAbility(option) is not null ||
                    _f.Rules.GetTalent(option) is not null ||
                    _f.Rules.GetPerk(option) is not null ||
                    _f.Rules.CreationRules.OptionalPackages.Any(p => p.Id == option) ||
                    IsAVariantKey(issue, option);

                Assert.True(known, $"{issue.Code} offers '{option}', which is not anything the rules have.");
            }
        }
    }

    /// <summary>
    /// The one kind of option with no printed name of its own: a key inside a Power's cost
    /// variants or a gear feature's grades. It has to be a key of <em>this</em> subject's
    /// range rather than any key anywhere.
    /// </summary>
    private bool IsAVariantKey(ValidationIssue issue, string option)
    {
        if (issue.SubjectId is null) return false;

        var variants = _f.Rules.GetPower(issue.SubjectId)?.CostVariants?.Keys;
        var grades   = _f.Rules.GetGearFeature(issue.SubjectId)?.CostRange?.Keys;
        var proRange = _f.Rules.GetPro(issue.SubjectId)?.CostModifierRange?.Keys;
        var conRange = _f.Rules.GetCon(issue.SubjectId)?.CostModifierRange?.Keys;

        // A Pro or Con printed inside a Power's own entry keeps its grades there, so the owner
        // is where to look. This is why OwnerId has to be an id: with the printed name in it
        // there was nothing to look the Power up by.
        var owner = issue.OwnerId is null ? null : _f.Rules.GetPower(issue.OwnerId);
        var specific = owner?.PowerPros.Concat(owner.PowerCons)
            .FirstOrDefault(x => x.Id == issue.SubjectId);

        return (variants?.Contains(option) ?? false)
            || (grades?.Contains(option) ?? false)
            || (proRange?.Contains(option) ?? false)
            || (conRange?.Contains(option) ?? false)
            || (specific?.CostModifierRange?.ContainsKey(option) ?? false)
            || (specific?.CostPerRankRange?.ContainsKey(option) ?? false);
    }
}
