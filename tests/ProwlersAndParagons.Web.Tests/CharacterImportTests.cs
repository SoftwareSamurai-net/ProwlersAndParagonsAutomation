using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The other half of "Download as data" — reading a character back out of a file.
///
/// <para>Built the same way every other web test builds its engine objects: through
/// <see cref="RenderContext"/>, against the real <c>data/rules/*.json</c>, because a reader
/// that works against invented data and fails against the rulebook has been tested for
/// nothing.</para>
/// </summary>
public sealed class CharacterImportTests
{
    private static CharacterImport Import(RenderContext ctx) =>
        new(ctx.Services.GetRequiredService<CostCalculator>(),
            ctx.Services.GetRequiredService<CharacterValidator>());

    // ── Not JSON at all ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("this is not a data file at all, just some prose.")]
    [InlineData("")]
    [InlineData("   ")]
    public void NotJsonAtAllIsRefused(string text)
    {
        using var ctx = new RenderContext();

        var result = Import(ctx).Read(text);

        Assert.Null(result.Imported);
        Assert.Equal(ImportProblem.NotJson, result.Problem);
        Assert.NotNull(result.Message);
    }

    /// <summary>
    /// The text export is not JSON syntax at all — it opens with a ruled banner of box-drawing
    /// characters — so picking it lands here rather than needing its own case. Guards the
    /// instruction not to offer the printed sheet as something that can be read back.
    /// </summary>
    [Fact]
    public void TheRuledTextSheetIsNotJson()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var rules = ctx.Services.GetRequiredService<RulesRepository>();
        var costs = ctx.Services.GetRequiredService<CostCalculator>();
        var derived = ctx.Services.GetRequiredService<DerivedStatsCalculator>();
        var validator = ctx.Services.GetRequiredService<CharacterValidator>();

        var text = ProwlersAndParagonsAutomation.Sheets.CharacterSheetRenderer.RenderText(
            ctx.Session.Sheet, rules, costs, derived, validator.Validate(ctx.Session.Sheet), DateTime.Now);

        var result = Import(ctx).Read(text);

        Assert.Null(result.Imported);
        Assert.Equal(ImportProblem.NotJson, result.Problem);
    }

    // ── JSON, but not a character ─────────────────────────────────────────────

    [Fact]
    public void AJsonArrayIsNotACharacter()
    {
        using var ctx = new RenderContext();

        var result = Import(ctx).Read("[1, 2, 3]");

        Assert.Null(result.Imported);
        Assert.Equal(ImportProblem.NotACharacter, result.Problem);
    }

    /// <summary>
    /// The report "Download as data" already produces — <c>CharacterSheetRenderer.RenderJson</c>
    /// — is JSON, but none of its top-level fields are this program's character shape; it opens
    /// with <c>meta</c>, which the strict reader has never heard of. This is the concrete shape
    /// of "pick the wrong export" the instruction to refuse the printed report is about.
    /// </summary>
    [Fact]
    public void TheJsonReportIsNotACharacter()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var rules = ctx.Services.GetRequiredService<RulesRepository>();
        var costs = ctx.Services.GetRequiredService<CostCalculator>();
        var derived = ctx.Services.GetRequiredService<DerivedStatsCalculator>();
        var validator = ctx.Services.GetRequiredService<CharacterValidator>();

        var json = ProwlersAndParagonsAutomation.Sheets.CharacterSheetRenderer.RenderJson(
            ctx.Session.Sheet, rules, costs, derived, validator.Validate(ctx.Session.Sheet), DateTime.Now);

        var result = Import(ctx).Read(json);

        Assert.Null(result.Imported);
        Assert.Equal(ImportProblem.NotACharacter, result.Problem);
    }

    /// <summary>
    /// A misspelled field is refused rather than silently dropped — the whole reason this
    /// class reads in <b>strict</b> mode rather than the lenient mode local storage uses.
    /// <c>AbilityRanks</c> misspelled as <c>AblityRanks</c> would otherwise deserialize to an
    /// empty dictionary and hand back a character with no abilities, priced and validated as
    /// though that were deliberate.
    /// </summary>
    [Fact]
    public void AMisspelledFieldIsRefusedRatherThanDropped()
    {
        using var ctx = new RenderContext();

        var json = """{"Name":"Typo","AblityRanks":{"might":6}}""";

        var result = Import(ctx).Read(json);

        Assert.Null(result.Imported);
        Assert.Equal(ImportProblem.NotACharacter, result.Problem);
    }

    // ── A character the rules cannot answer for ───────────────────────────────

    /// <summary>
    /// Determination is 5 Hero Points per unit of Resolve (Ch.2). A unit count large enough
    /// that <c>5 × units</c> overflows a 32-bit total is exactly the case
    /// <c>CostCalculator.TotalCost</c>'s own <c>checked</c> block exists to catch — see its
    /// remarks — and it is a real failure the engine cannot answer for, not a contrived one.
    /// </summary>
    [Fact]
    public void ACharacterThatOverflowsThePricingIsRefused()
    {
        using var ctx = new RenderContext();

        var sheet = new CharacterSheet { Name = "Too Determined" };
        sheet.SelectedPowers.Add(new SelectedPower("determination", 0) { Units = 500_000_000 });

        var json = CharacterSheetJson.Write(sheet);
        var result = Import(ctx).Read(json);

        Assert.Null(result.Imported);
        Assert.Equal(ImportProblem.Unpriceable, result.Problem);
    }

    // ── A character the rules can answer for ──────────────────────────────────

    [Fact]
    public void AWellFormedHeroImports()
    {
        using var ctx = new RenderContext();

        var sheet = SampleCharacters.Hero();
        var json = CharacterSheetJson.Write(sheet);

        var result = Import(ctx).Read(json);

        Assert.NotNull(result.Imported);
        Assert.Equal(SheetMode.Hero, result.Imported!.Value.Mode);
        Assert.Equal(sheet.Name, result.Imported.Value.Sheet.Name);
        Assert.Null(result.Message);
    }

    [Fact]
    public void AVillainImportsWithTheVillainMode()
    {
        using var ctx = new RenderContext();

        var sheet = SampleCharacters.Villain();
        sheet.IsVillain = true;
        var json = CharacterSheetJson.Write(sheet);

        var result = Import(ctx).Read(json);

        Assert.NotNull(result.Imported);
        Assert.Equal(SheetMode.Villain, result.Imported!.Value.Mode);
    }

    /// <summary>
    /// A half-finished character — a variable-cost Power with no variant chosen yet — is a
    /// legitimate state, not corruption. <c>CostCalculator</c> throws
    /// <see cref="InvalidOperationException"/> for it, which <c>CharacterSession.TryCost</c>
    /// already swallows; refusing it here would make importing a work in progress
    /// impossible, which is not what "the rules can't answer for this" is about.
    /// </summary>
    [Fact]
    public void AHalfFinishedSelectionIsStillImported()
    {
        using var ctx = new RenderContext();

        var sheet = new CharacterSheet { Name = "Unfinished" };
        sheet.SelectedPowers.Add(new SelectedPower("omni_power", 1));

        var json = CharacterSheetJson.Write(sheet);
        var result = Import(ctx).Read(json);

        Assert.NotNull(result.Imported);
        Assert.Equal("Unfinished", result.Imported!.Value.Sheet.Name);
    }
}
