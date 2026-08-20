using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The file picker and its messages — rendered, not just read as source, because the bug
/// this split exists to catch (see the two test projects' own remarks) lives in what a
/// component actually draws, not in how it is written.
///
/// <para><see cref="CharacterImport"/> is not registered on <see cref="RenderContext"/> —
/// nothing else there needs it — so these tests build a plain <see cref="BunitContext"/> of
/// their own rather than adding to one. bUnit seals a context's services the moment the
/// first one is resolved, and <see cref="RenderContext"/>'s own constructor already
/// resolves <c>CharacterSession</c> before returning, so nothing can be added to it
/// afterwards. The calculators still come from the real rules — a throwaway
/// <see cref="RenderContext"/> supplies them, exactly as every other test in this project
/// gets them.</para>
/// </summary>
public sealed class ImportCharacterTests
{
    private static BunitContext NewContext()
    {
        using var engine = new RenderContext();
        var import = new CharacterImport(
            engine.Services.GetRequiredService<CostCalculator>(),
            engine.Services.GetRequiredService<CharacterValidator>());

        var ctx = new BunitContext();
        ctx.Services.AddSingleton(import);
        return ctx;
    }

    [Fact]
    public void RendersALabelledFilePicker()
    {
        using var ctx = NewContext();

        var cut = ctx.Render<ImportCharacter>();

        var input = cut.Find("input");
        Assert.Equal("file", input.GetAttribute("type"));
        Assert.Equal("import-character-file", input.GetAttribute("id"));
        Assert.Equal("import-character-file", cut.Find("label").GetAttribute("for"));
    }

    [Fact]
    public void AWellFormedFileRaisesOnImportedAndShowsNoMessage()
    {
        using var ctx = NewContext();

        var imported = new List<(CharacterSheet Sheet, SheetMode Mode)>();
        var cut = ctx.Render<ImportCharacter>(p => p.Add(x => x.OnImported, imported.Add));

        var sheet = SampleCharacters.Hero();
        var json = CharacterSheetJson.Write(sheet);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(json, "character.json"));

        var result = Assert.Single(imported);
        Assert.Equal(SheetMode.Hero, result.Mode);
        Assert.Equal(sheet.Name, result.Sheet.Name);

        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public void GarbageTextShowsAMessageAndRaisesNothing()
    {
        using var ctx = NewContext();

        var imported = new List<(CharacterSheet Sheet, SheetMode Mode)>();
        var cut = ctx.Render<ImportCharacter>(p => p.Add(x => x.OnImported, imported.Add));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("not a character", "notes.txt"));

        Assert.Empty(imported);

        // TextContent, never markup with tags stripped: the whole reason this project's
        // rendering tests read this way rather than stripping tags and matching a
        // substring — see the two test projects' own remarks.
        var message = cut.Find("[role=alert]").TextContent;
        Assert.False(string.IsNullOrWhiteSpace(message));

        // Nothing on screen may name an internal type or a build command, even when the
        // text is composed at runtime rather than sitting in the markup a source scan
        // could catch — the same discipline WebPresentationTests holds the rest of the
        // front end to.
        Assert.DoesNotContain("Json", message, StringComparison.Ordinal);
        Assert.DoesNotContain("CharacterSheet", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The report "Download as data" already writes is JSON, but not this program's
    /// character shape — see <c>CharacterImportTests.TheJsonReportIsNotACharacter</c> for
    /// the service-level version of the same fact. Here it is picked as a file and the
    /// message a person actually sees is what is asserted.
    /// </summary>
    [Fact]
    public void TheJsonReportShowsAMessageRatherThanImporting()
    {
        using var engine = new RenderContext().With(SheetMode.Hero);

        var rules = engine.Services.GetRequiredService<RulesRepository>();
        var costs = engine.Services.GetRequiredService<CostCalculator>();
        var derived = engine.Services.GetRequiredService<DerivedStatsCalculator>();
        var validator = engine.Services.GetRequiredService<CharacterValidator>();
        var json = ProwlersAndParagonsAutomation.Sheets.CharacterSheetRenderer.RenderJson(
            engine.Session.Sheet, rules, costs, derived, validator.Validate(engine.Session.Sheet), DateTime.Now);

        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(new CharacterImport(costs, validator));

        var imported = new List<(CharacterSheet Sheet, SheetMode Mode)>();
        var cut = ctx.Render<ImportCharacter>(p => p.Add(x => x.OnImported, imported.Add));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(json, "character.json"));

        Assert.Empty(imported);
        Assert.False(string.IsNullOrWhiteSpace(cut.Find("[role=alert]").TextContent));
    }

    /// <summary>
    /// A pick larger than the component will read at all — refused by
    /// <c>OpenReadStream</c>'s own size limit before any text reaches
    /// <see cref="CharacterImport"/>, which is why this is asserted on the component rather
    /// than the service: the limit lives here, not there.
    /// </summary>
    [Fact]
    public void AnOversizedFileIsRefusedWithoutReachingTheReader()
    {
        using var ctx = NewContext();

        var imported = new List<(CharacterSheet Sheet, SheetMode Mode)>();
        var cut = ctx.Render<ImportCharacter>(p => p.Add(x => x.OnImported, imported.Add));

        var oversized = new string('x', 3 * 1024 * 1024);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(oversized, "huge.json"));

        Assert.Empty(imported);

        // Specifically the size message, not merely "some message" — 3MB of the letter x
        // is also not JSON, so a test that only checked for *a* message would pass just as
        // well if the size limit were quietly removed and the pick fell through to that
        // other, unrelated refusal.
        Assert.Contains("too large", cut.Find("[role=alert]").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    // ── The presentation rules this component is held to like every other ──────

    [Fact]
    public void CarriesNoTitleAttribute() =>
        Assert.DoesNotContain("title=",
            File.ReadAllText(Path.Combine(RepoRoot(), "web", "Components", "ImportCharacter.razor")),
            StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void CarriesNoInlineStyle() =>
        Assert.DoesNotContain("style=",
            File.ReadAllText(Path.Combine(RepoRoot(), "web", "Components", "ImportCharacter.razor")),
            StringComparison.OrdinalIgnoreCase);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }
}
