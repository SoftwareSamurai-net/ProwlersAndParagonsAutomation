using ProwlersAndParagonsAutomation.Engine;
using Bunit;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The Hero Point budget strip.
///
/// <para><b>It shipped with no rendering coverage at all</b> — the only thing referring to it
/// outside the proofing harness was a source-reading assertion about its inline style, so the
/// disclosure, the progress bar, the over-budget branch and the no-limit shape were all
/// unasserted. A fix-audit found three defects in it on first reading.</para>
/// </summary>
public sealed class BudgetStripTests
{
    /// <summary>
    /// A Villain is held to the tier's budget like anybody else.
    ///
    /// <para><b>This test used to assert the opposite, and its old name said so.</b> Ch.9 builds
    /// Villains by exactly the Hero rules and prints no separate stat-block format, so "no
    /// budget" was never a fact about Villains — it was a GM building to whatever the scene
    /// needs, which is a way of working and not a kind of character. The palette no longer
    /// decides it; the sandbox toggle does, and either can apply to either.</para>
    /// </summary>
    [Fact]
    public void AVillainIsStillHeldToTheTiersBudget()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);

        var strip = ctx.Render<HpBudgetBar>();

        Assert.Single(strip.FindAll(".budget"));
        Assert.Contains("left", strip.Find(".budget-left").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without a limit the strip is a running total: the spend, no denominator, no remaining
    /// figure, and <b>no rail</b>.
    ///
    /// <para><b>The rail is the half worth asserting.</b> A <c>progressbar</c> needs a maximum to
    /// be a proportion of, and drawing one against the tier's points would put the limit back on
    /// screen that the person building has just switched off — while announcing a figure to a
    /// screen reader that nothing is being measured against.</para>
    /// </summary>
    [Fact]
    public void WithoutALimitTheStripIsARunningTotal()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Session.UnlimitedBudget = true;

        var strip = ctx.Render<HpBudgetBar>();

        Assert.Single(strip.FindAll(".budget"));
        Assert.Empty(strip.FindAll(".budget-rail"));
        Assert.Empty(strip.FindAll(".over-text"));
        Assert.Contains("HP spent", strip.Find(".budget-figure").TextContent, StringComparison.Ordinal);
        Assert.Equal("No limit", strip.Find(".budget-left").TextContent.Trim());

        // The breakdown still opens. Losing it was the real cost of the old behaviour: the strip
        // was absent altogether, so somebody building without a limit lost the one panel that
        // says where the points went.
        Assert.Single(strip.FindAll(".budget-toggle"));
    }

    /// <summary>
    /// The sandbox is independent of the palette, in both directions.
    ///
    /// <para>This is the whole point of the split, and it is the assertion that fails if anybody
    /// re-couples them — which is easy to do by accident, since one of them used to imply the
    /// other.</para>
    /// </summary>
    [Fact]
    public async Task ThePaletteAndTheLimitAreIndependent()
    {
        await using var ctx = new RenderContext().With(SheetMode.Villain);

        // A Villain held to a budget: rail present.
        var strip = ctx.Render<HpBudgetBar>();
        Assert.Single(strip.FindAll(".budget-rail"));

        // Through the dispatcher, because the strip is already rendered and subscribed — the
        // session raises Changed and the component redraws on it, which Blazor refuses from
        // another thread.
        await strip.InvokeAsync(() => ctx.Session.UnlimitedBudget = true);
        strip.Render();

        Assert.Empty(strip.FindAll(".budget-rail"));
        Assert.Equal("No limit", strip.Find(".budget-left").TextContent.Trim());

        // ...and the palette did not move while the limit did. Both directions matter: this is
        // the assertion that fails if anybody re-couples them, which is easy to do by accident
        // since one used to imply the other.
        Assert.Equal(SheetMode.Villain, ctx.Session.Mode);
        Assert.True(ctx.Session.Sheet.IsVillain);
    }

    /// <summary>
    /// The palette is the character's own answer, so it survives being written out and read back.
    ///
    /// <para>That portability is the only reason the field is on the character rather than beside
    /// it, so it is the thing worth asserting rather than the field's presence.</para>
    /// </summary>
    [Fact]
    public void ThePaletteAndTheSandboxSurviveARoundTrip()
    {
        var sheet = SampleCharacters.Villain();
        sheet.IsVillain = true;
        sheet.UnlimitedBudget = true;

        var back = CharacterSheetJson.Read(CharacterSheetJson.Write(sheet), strict: true);
        Assert.NotNull(back);

        Assert.True(back.IsVillain);
        Assert.True(back.UnlimitedBudget);
    }

    [Fact]
    public void AHeroSeesTheSpendAgainstTheBudget()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var strip = ctx.Render<HpBudgetBar>();

        Assert.NotEmpty(strip.FindAll(".budget"));
        Assert.NotEmpty(strip.FindAll(".budget-rail"));

        // The figures are the engine's, and the strip must not be inventing one.
        var spent = ctx.Session.Costs.TotalCost(ctx.Session.Sheet);
        Assert.Contains(spent.ToString(), strip.Find(".budget-figure").TextContent, StringComparison.Ordinal);
        Assert.Contains(ctx.Session.Budget.ToString(), strip.Find(".budget-figure").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// The breakdown is disclosed on request, and the button says so both ways.
    ///
    /// <para><c>aria-controls</c> is asserted to be <b>absent</b> while collapsed: the target
    /// only exists inside the open branch, so naming it unconditionally leaves a dangling
    /// IDREF, which assistive technology may report as a broken relationship rather than as a
    /// closed one.</para>
    /// </summary>
    [Fact]
    public void TheBreakdownIsDisclosedAndAnnounced()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var strip = ctx.Render<HpBudgetBar>();
        var toggle = strip.Find(".budget-toggle");

        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Null(toggle.GetAttribute("aria-controls"));
        Assert.Empty(strip.FindAll(".breakdown"));

        toggle.Click();

        var opened = strip.Find(".budget-toggle");
        Assert.Equal("true", opened.GetAttribute("aria-expanded"));
        Assert.Equal("budget-breakdown", opened.GetAttribute("aria-controls"));
        Assert.NotEmpty(strip.FindAll("#budget-breakdown"));

        strip.Find(".budget-toggle").Click();
        Assert.Empty(strip.FindAll(".breakdown"));
    }

    /// <summary>
    /// Over budget says so in words, not in colour alone — around one man in twelve has some
    /// colour-vision deficiency, and this is the one state on the strip that matters.
    /// </summary>
    [Fact]
    public void OverBudgetSaysSoInWords()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        // The Hero sample costs about 105; the 75-point tier puts it over.
        ctx.Session.Sheet.SelectedTierId = "street_level";

        var strip = ctx.Render<HpBudgetBar>();

        Assert.NotEmpty(strip.FindAll(".over-text"));
        Assert.Contains("over", strip.Find(".over-text").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(strip.FindAll(".budget-left"));
    }

    /// <summary>
    /// <b>The progress bar stays inside its own range, and carries the real figure in words.</b>
    /// An over-budget character spends more than the budget, and a <c>progressbar</c> reporting
    /// <c>valuenow=132</c> against <c>valuemax=125</c> is out of range and invalid — the fill
    /// was already clamped to 100% in the same block while the announced value was not, so the
    /// two disagreed. It also needs its own name: the label on the enclosing section names the
    /// section, not the bar inside it.
    /// </summary>
    [Theory]
    [InlineData("standard")]
    [InlineData("street_level")]
    public void TheProgressBarIsValidInBothDirections(string tier)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Session.Sheet.SelectedTierId = tier;

        var rail = ctx.Render<HpBudgetBar>().Find(".budget-rail");

        var now = int.Parse(rail.GetAttribute("aria-valuenow")!, System.Globalization.CultureInfo.InvariantCulture);
        var min = int.Parse(rail.GetAttribute("aria-valuemin")!, System.Globalization.CultureInfo.InvariantCulture);
        var max = int.Parse(rail.GetAttribute("aria-valuemax")!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.InRange(now, min, max);

        Assert.False(string.IsNullOrWhiteSpace(rail.GetAttribute("aria-label")),
            "The progress bar has no accessible name of its own.");

        // The true spend survives in the text even where the number is clamped.
        var spent = ctx.Session.Costs.TotalCost(ctx.Session.Sheet);
        Assert.Contains(spent.ToString(), rail.GetAttribute("aria-valuetext") ?? "", StringComparison.Ordinal);
    }
}
