using Bunit;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The Hero Point budget strip.
///
/// <para><b>It shipped with no rendering coverage at all</b> — the only thing referring to it
/// outside the proofing harness was a source-reading assertion about its inline style, so the
/// disclosure, the progress bar, the over-budget branch and the Villain hide were all
/// unasserted. A fix-audit found three defects in it on first reading.</para>
/// </summary>
public sealed class BudgetStripTests
{
    /// <summary>
    /// Ch.9 gives a Villain no budget, so there is no strip — not a strip reading 0 of 0, and
    /// not an over-budget warning against a budget that does not exist.
    /// </summary>
    [Fact]
    public void AVillainHasNoBudgetStrip()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);

        Assert.Empty(ctx.Render<HpBudgetBar>().FindAll(".budget"));
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
