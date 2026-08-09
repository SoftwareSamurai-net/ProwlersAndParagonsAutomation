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

    private CharacterSheet Sample(string which) =>
        which == "hero" ? SampleCharacters.Hero() : SampleCharacters.Villain();

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

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void SampleFitsItsHeroPointBudget(string which)
    {
        var sheet  = Sample(which);
        var budget = _rules.GetTier(sheet.SelectedTierId!)!.HeroPoints;
        var spent  = _costs.TotalCost(sheet);

        Assert.True(spent <= budget,
            $"The {which} sample spends {spent} of {budget} HP. A sample that cannot be " +
            "afforded teaches the wrong thing about the budget bar.");
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
