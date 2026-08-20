using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>A character downloaded from the app can be loaded back into it.</b>
///
/// <para>Written because it could not, and nothing said so. The importer was built to read the
/// inputs shape and the app's only data download writes the <em>report</em> — derived stats, costs
/// and findings — which the strict reader refuses, correctly, because reading it back would rebuild
/// a character from its own conclusions. Both halves were right on their own and the pair was
/// useless: a player could download their character and had nowhere to take it, including the
/// headless <c>build --from</c> command, which reads the same inputs shape.</para>
///
/// <para><b>This is an end-to-end test on purpose.</b> Each half already has its own — the
/// exporter's output is asserted elsewhere, the importer's refusals are asserted in
/// <c>CharacterImportTests</c> — and both suites were green while the round trip did not exist.
/// The only thing that catches that is a test which takes what one produces and hands it to the
/// other.</para>
/// </summary>
public sealed class ExportImportRoundTripTests
{
    /// <summary>
    /// What the review step's "Download to keep" button writes, taken from the same call it makes
    /// rather than reconstructed here — a test that built its own payload would pass against a
    /// button that writes something else entirely.
    /// </summary>
    private static string Downloaded(RenderContext ctx)
    {
        ctx.Render<Review>().FindAll("button")
            .Single(b => b.TextContent.Contains("Download to keep", StringComparison.Ordinal))
            .Click();

        // ppDownload(name, mimeType, body) — the body is the third argument.
        var calls = ctx.JSInterop.Invocations["ppDownload"];
        var call = calls[^1];

        return (string)call.Arguments[2]!;
    }

    [Fact]
    public void ACharacterDownloadedFromTheAppCanBeLoadedBackIntoIt()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);
        ctx.Session.Sheet.Name = "The Quiet Hour";

        var file = Downloaded(ctx);

        // The positive control: the file is really the character, not an empty envelope.
        Assert.Contains("The Quiet Hour", file, StringComparison.Ordinal);

        var back = ctx.Services.GetRequiredService<CharacterImport>().Read(file);

        Assert.Null(back.Problem);
        Assert.NotNull(back.Imported);
        Assert.Equal("The Quiet Hour", back.Imported!.Value.Sheet.Name);
    }

    /// <summary>
    /// The reloaded character costs and validates identically — which is the claim that matters,
    /// since a name surviving says nothing about the eighteen Traits and the Powers under it.
    /// </summary>
    [Fact]
    public void TheReloadedCharacterIsPricedAndJudgedTheSame()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var costs = ctx.Services.GetRequiredService<CostCalculator>();
        var validator = ctx.Services.GetRequiredService<CharacterValidator>();

        var before = costs.TotalCost(ctx.Session.Sheet);
        var findingsBefore = validator.Validate(ctx.Session.Sheet).Issues.Count;

        var back = ctx.Services.GetRequiredService<CharacterImport>().Read(Downloaded(ctx));

        Assert.NotNull(back.Imported);
        Assert.Equal(before, costs.TotalCost(back.Imported!.Value.Sheet));
        Assert.Equal(findingsBefore, validator.Validate(back.Imported.Value.Sheet).Issues.Count);

        // And the sample is not trivially cheap, or the equality above would prove little.
        Assert.True(before > 50, $"the sample costs only {before} HP; this asserts almost nothing");
    }

    /// <summary>
    /// The Hero/Villain mode survives, because it is on the character and an exported sheet should
    /// still be a Villain when it is read back.
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void TheModeSurvivesTheRoundTrip(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        var back = ctx.Services.GetRequiredService<CharacterImport>().Read(Downloaded(ctx));

        Assert.NotNull(back.Imported);
        Assert.Equal(mode, back.Imported!.Value.Mode);
    }

    /// <summary>
    /// <b>And the report download is still refused</b>, which is the other half of why there are
    /// two buttons. Without this the round trip could be "fixed" at any point by pointing the
    /// importer at the report and teaching it to skip the answers.
    /// </summary>
    [Fact]
    public void TheReportDownloadIsStillNotSomethingThatCanBeLoaded()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Render<Review>().FindAll("button")
            .Single(b => b.TextContent.Contains("Download as data", StringComparison.Ordinal))
            .Click();

        var calls = ctx.JSInterop.Invocations["ppDownload"];
        var report = (string)calls[^1].Arguments[2]!;

        // The positive control: it is a real report, carrying answers rather than inputs.
        Assert.Contains("hero_points", report, StringComparison.OrdinalIgnoreCase);

        var back = ctx.Services.GetRequiredService<CharacterImport>().Read(report);

        Assert.NotNull(back.Problem);
        Assert.Null(back.Imported);
    }
}
