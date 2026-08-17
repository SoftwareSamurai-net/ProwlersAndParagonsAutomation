using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The two sample characters the front ends offer for previewing a sheet.
///
/// <para>They are demonstration content, so it would be easy to leave them unchecked —
/// and a sample that is over budget, illegal, or cannot be priced is worse than no sample
/// at all, because it is the first thing anyone sees. These hold them to the same rules a
/// player's character is held to.</para>
/// </summary>
public sealed class SampleCharacterTests : IClassFixture<RulesFixture>
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;
    private readonly CharacterValidator _validator;

    public SampleCharacterTests(RulesFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _rules     = fixture.Rules;
        _costs     = fixture.Costs;
        _derived   = fixture.Derived;
        _validator = fixture.Validator;
    }

    public static TheoryData<string> SampleNames => new("hero", "villain");

    /// <summary>
    /// <b>Anything that is not the Hero is the Villain, and that used to be silent.</b> A caller
    /// wrote <c>Sample("Hero")</c> — the comparison is ordinal and case-sensitive, so it built
    /// the Villain and passed, leaving the Hero's export path untested under a test named for it.
    /// The two names are the ones <see cref="SampleNames"/> supplies and nothing else is one.
    /// </summary>
    private static CharacterSheet Sample(string which) => which switch
    {
        "hero"    => SampleCharacters.Hero(),
        "villain" => SampleCharacters.Villain(),
        _ => throw new ArgumentOutOfRangeException(nameof(which),
                 $"'{which}' is not one of the samples: they are 'hero' and 'villain'.")
    };

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void SampleHasNoValidationErrors(string which)
    {
        var result = _validator.Validate(Sample(which));

        var errors = result.Errors.Select(e => $"{e.Code}: {e.Message}").ToList();
        Assert.True(errors.Count == 0,
            $"The {which} sample must be legal — it is the first sheet anyone sees. Errors:\n" +
            string.Join("\n", errors));
    }

    /// <summary>
    /// <b>Both ends of the budget, because only one of them was ever checked.</b> A sample that
    /// cannot be afforded teaches the wrong thing about the budget bar — and so does one that
    /// spends a third of it, which is what the one-sided comparison allowed: deleting
    /// <c>stun</c> outright from the Hero left this green, and every other test with it, since
    /// "fills every section" only asks whether a section is non-empty.
    ///
    /// <para>The floor is half the budget rather than a figure close to what they spend now, so
    /// it bounds the failure it is for without being a number to re-tune every time a sample is
    /// edited. As they stand the Hero spends 105 of 125 and the Villain 119.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(SampleNames))]
    public void SampleFitsItsHeroPointBudgetAndUsesMostOfIt(string which)
    {
        var sheet  = Sample(which);
        var budget = _rules.GetTier(sheet.SelectedTierId!)!.HeroPoints;
        var spent  = _costs.TotalCost(sheet);

        Assert.True(spent <= budget,
            $"The {which} sample spends {spent} of {budget} HP. A sample that cannot be " +
            "afforded teaches the wrong thing about the budget bar.");

        Assert.True(spent * 2 >= budget,
            $"The {which} sample spends only {spent} of {budget} HP. A preview built to show " +
            "what a finished sheet looks like has had something taken out of it.");
    }

    /// <summary>
    /// <b>What each sample is made of, so that trimming it is a change somebody makes on
    /// purpose.</b> The doc comment on <see cref="SampleFillsEverySectionOfTheSheet"/> claims to
    /// catch "someone trimming one down" and does not: it asserts sections are non-empty, so a
    /// Power can be deleted from a list of seven and nothing anywhere objects.
    ///
    /// <para>These are this project's own characters rather than data that evolves, so naming
    /// their Powers is a record, not a duplicate of something else. If one is deliberately
    /// swapped, this line is the place that says so.</para>
    /// </summary>
    [Fact]
    public void TheSamplesCarryThePowersTheyWereBuiltWith()
    {
        Assert.Equal<IEnumerable<string>>(
            ["armor", "danger_sense", "super_senses_thermal_vision", "super_senses_radio_hearing",
             "resistance", "communications", "stun"],
            SampleCharacters.Hero().SelectedPowers.Select(p => p.PowerId));

        Assert.Equal<IEnumerable<string>>(
            ["mind_control", "invisibility", "teleportation", "lightning_reflexes"],
            SampleCharacters.Villain().SelectedPowers.Select(p => p.PowerId));
    }

    /// <summary>
    /// <b>And why those Powers rather than any seven.</b> Each was chosen to put a different
    /// shape on the sheet — a baseline that is half a Trait against one that equals it, a rate
    /// below 1 HP per rank, a Power with no rank at all, the one Power the rulebook costs as a
    /// group, and a Power carrying a Con. Naming the ids above catches a deletion; this catches
    /// a replacement that quietly costs the preview the thing it was previewing.
    /// </summary>
    [Fact]
    public void TheSamplesShowEveryShapeAPrintedSheetHas()
    {
        var hero = SampleCharacters.Hero();
        var powers = hero.SelectedPowers.Select(p => _rules.GetPower(p.PowerId)!).ToList();

        Assert.Contains(powers, p => p.Prerequisite?.Relationship == "baseline_half");
        Assert.Contains(powers, p => p.Prerequisite?.Relationship == "baseline_equal");
        Assert.Contains(powers, p => p.CostPerRank is > 0 and < 1);
        Assert.Contains(powers, p => p.RankType is "default" or "special");

        // Super Senses is costed as one Power however many options are taken (Ch.2), which only
        // shows on a sheet carrying more than one of them.
        Assert.True(powers.Count(p => p.Id.StartsWith("super_senses_", StringComparison.Ordinal)) >= 2);

        // A Trait bought through equipment: the Item Con and a Source on the Ability, which is
        // what makes the sheet print an "Abilities (…)" line inside a Power group.
        Assert.NotEmpty(hero.AbilityModifiers);
        Assert.NotEmpty(hero.AbilitySources);
        Assert.Contains(hero.Gear, g => g.Features.Count > 0);
        Assert.Contains(hero.Perks, p => _rules.GetPerk(p.PerkId)!.CostType == "per_unit");

        var villain = SampleCharacters.Villain();

        Assert.Contains(villain.SelectedPowers, p => p.Cons.Count > 0);
        Assert.Contains(villain.SelectedPowers, p => p.SourceId is null);   // the plain POWERS heading
        Assert.NotEmpty(villain.TalentSources);
    }

    /// <summary>
    /// Every selection must be priceable. The engine throws rather than guessing on an
    /// incomplete one — a variable-cost Power with no variant, a graded gear feature with
    /// no grade — and the front ends call <c>TotalCost</c> on every render, so an
    /// incomplete sample would take the page down rather than look wrong.
    /// </summary>
    [Theory]
    [MemberData(nameof(SampleNames))]
    public void SampleIsFullyPriceable(string which)
    {
        var sheet = Sample(which);

        var exception = Record.Exception(() =>
        {
            foreach (var power in sheet.SelectedPowers) _costs.PowerCost(power);
            foreach (var gear in sheet.Gear) _costs.GearCost(gear);
            foreach (var perk in sheet.Perks) _costs.PerkCost(perk);
            _costs.TotalCost(sheet);
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// The samples exist to make a sheet worth looking at, so an empty section defeats
    /// the point. This is the check that would catch someone trimming one down.
    /// </summary>
    [Theory]
    [MemberData(nameof(SampleNames))]
    public void SampleFillsEverySectionOfTheSheet(string which)
    {
        var sheet = Sample(which);

        Assert.False(string.IsNullOrWhiteSpace(sheet.Name));
        Assert.False(string.IsNullOrWhiteSpace(sheet.Appearance));
        Assert.False(string.IsNullOrWhiteSpace(sheet.Motivation));
        Assert.False(string.IsNullOrWhiteSpace(sheet.Quote));
        Assert.NotEmpty(sheet.Connections);
        Assert.NotEmpty(sheet.SelectedPowers);
        Assert.NotEmpty(sheet.Flaws);
        Assert.NotEmpty(sheet.Gear);
        Assert.Contains(sheet.AbilityRanks, r => r.Value > 0);
        Assert.Contains(sheet.TalentRanks, r => r.Value > 0);
    }

    /// <summary>
    /// Both samples record a Source on at least one Power, so the sheet prints a real
    /// heading — <c>TECH POWERS</c>, <c>MAGIC POWERS</c> — rather than the plain fallback.
    /// Source grouping is the thing most worth seeing in a preview.
    /// </summary>
    [Theory]
    [MemberData(nameof(SampleNames))]
    public void SampleGroupsPowersUnderASourceHeading(string which)
    {
        var groups = new SourceGrouping(_rules).GroupBySource(Sample(which));

        Assert.Contains(groups, g => g.Source is not null);
    }

    /// <summary>
    /// Renders both exports end to end. They are the documents a preview is previewing,
    /// and the renderer touches every calculator on the way through.
    /// </summary>
    [Theory]
    [MemberData(nameof(SampleNames))]
    public void SampleRendersBothExports(string which)
    {
        var sheet      = Sample(which);
        var validation = _validator.Validate(sheet);
        var stamp      = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var text = CharacterSheetRenderer.RenderText(sheet, _rules, _costs, _derived, validation, stamp);
        var json = CharacterSheetRenderer.RenderJson(sheet, _rules, _costs, _derived, validation, stamp);

        Assert.Contains(sheet.Name, text, StringComparison.Ordinal);
        Assert.Contains(sheet.Name, json, StringComparison.Ordinal);
        Assert.Contains("DERIVED STATS", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The text export collapses a repeated option too, and its wiring needs its own
    /// test.</b> The formatter has a unit test and it did not bite: reverting this call site
    /// to a plain join left it green, because it exercises the function rather than the
    /// export. Blastwave's five copies of Also X are what this is about.
    ///
    /// <para>The JSON export is deliberately not collapsed — one array element per selection
    /// is the right shape for the machine-readable half, and a reader counts them.</para>
    /// </summary>
    [Fact]
    public void TheTextExportCollapsesARepeatedOptionAndTheJsonDoesNot()
    {
        var sheet = Sample("hero");
        sheet.SelectedPowers.Add(new SelectedPower("energy_absorption", 6,
            [new SelectedProCon("also_x"), new SelectedProCon("also_x"), new SelectedProCon("also_x")],
            []) { SourceId = "super", CostVariantKey = "kinetic" });

        var validation = _validator.Validate(sheet);
        var stamp      = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var text = CharacterSheetRenderer.RenderText(sheet, _rules, _costs, _derived, validation, stamp);
        var json = CharacterSheetRenderer.RenderJson(sheet, _rules, _costs, _derived, validation, stamp);

        Assert.Contains("also_x ×3", text, StringComparison.Ordinal);
        Assert.DoesNotContain("also_x, also_x", text, StringComparison.Ordinal);

        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(
            json, "\"also_x\"", System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// The Villain is a legal character built by the Hero rules — Ch.9 is explicit that
    /// nothing about building one differs. Only the front end treats it differently, by
    /// hiding the budget, and nothing on the sheet itself records which it is.
    /// </summary>
    [Fact]
    public void TheVillainIsBuiltByTheSameRulesAsTheHero()
    {
        var villain = SampleCharacters.Villain();

        Assert.Equal(SampleCharacters.TierId, villain.SelectedTierId);
        Assert.True(_validator.Validate(villain).IsValid);
        Assert.True(_costs.TotalCost(villain) <= _rules.GetTier(SampleCharacters.TierId)!.HeroPoints);
    }
}
