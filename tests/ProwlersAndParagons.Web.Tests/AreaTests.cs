using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using ProwlersAndParagonsAutomation.Web.Layout;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// One site doing two jobs, and the chrome saying which one you are in.
///
/// <para>The play aide is the rules reference and the sheet helper — the thing somebody has open
/// at a table. The portfolio is the recordings and the sample characters — the thing that shows
/// somebody what was built. The furniture above the page was written for the first and means
/// nothing on the second.</para>
/// </summary>
public sealed class AreaTests
{
    [Theory]
    [InlineData("", Area.Play)]
    [InlineData("characteristics", Area.Play)]
    [InlineData("review", Area.Play)]
    [InlineData("portfolio", Area.Portfolio)]
    [InlineData("portfolio/replay", Area.Portfolio)]
    [InlineData("portfolio/replay/the-conductor", Area.Portfolio)]
    public void AnAddressKnowsWhichHalfOfTheSiteItIsIn(string path, Area expected) =>
        Assert.Equal(expected, Areas.Of(path));

    /// <summary>
    /// <b>Case-insensitively, because Blazor's own route matching is.</b> A capitalised link is
    /// served by the app, and an ordinal comparison here would serve a recording wearing the
    /// character generator's chrome — reachable by anybody who capitalised a shared address.
    /// </summary>
    [Theory]
    [InlineData("Portfolio")]
    [InlineData("PORTFOLIO/replay")]
    [InlineData("Replay/the-conductor")]
    public void TheMatchIsCaseInsensitive(string path) =>
        Assert.Equal(Area.Portfolio, Areas.Of(path));

    /// <summary>
    /// The addresses the recordings used to live at are still portfolio addresses.
    ///
    /// <para><b>A link that still works but arrives wearing the wrong chrome is worse than one
    /// that breaks.</b> The budget strip is the visitor's <em>own</em> character, and it sat over
    /// somebody else's recorded one with nothing saying whose was whose — the exact fault the
    /// strip was hidden here to fix. Moving the route reintroduced it, and this is what caught
    /// it.</para>
    /// </summary>
    [Theory]
    [InlineData("replay")]
    [InlineData("replay/the-conductor")]
    public void TheOldReplayAddressesAreStillPortfolioAddresses(string path) =>
        Assert.Equal(Area.Portfolio, Areas.Of(path));

    /// <summary>
    /// A page whose name merely begins with the prefix is not in the portfolio.
    ///
    /// <para>The positive control for the matching: a <c>StartsWith</c> would pass every test
    /// above and quietly capture a future page called something like "portfolios".</para>
    /// </summary>
    [Theory]
    [InlineData("portfolios")]
    [InlineData("portfolio-of-work")]
    [InlineData("replaying")]
    public void APageMerelyBeginningWithThePrefixIsNot(string path) =>
        Assert.Equal(Area.Play, Areas.Of(path));

    /// <summary>
    /// The tool's chrome is drawn in the tool and not over a recording — the step band as well as
    /// the budget strip.
    ///
    /// <para>Asserted through the layout, because a page cannot see the shell above it.</para>
    /// </summary>
    [Theory]
    [InlineData("", true)]
    [InlineData("portfolio", false)]
    [InlineData("portfolio/replay/the-conductor", false)]
    [InlineData("replay", false)]
    public void TheStepsAndTheBudgetBelongToTheToolAlone(string path, bool expected)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var shell = ctx.Render<MainLayout>();

        Assert.Equal(expected, shell.FindAll(".steps").Count > 0);
        Assert.Equal(expected, shell.FindAll(".budget").Count > 0);
    }

    /// <summary>
    /// The banner's cross-link points at the half you are not in.
    ///
    /// <para>It used to say "Watch one being built" from everywhere, so the only cross-link a
    /// player ever saw pointed away from what they were doing.</para>
    /// </summary>
    [Fact]
    public void TheBannerOffersTheOtherHalf()
    {
        using var play = new RenderContext().With(SheetMode.Hero);
        Assert.Contains(
            "How this was built",
            play.Render<MainLayout>().Find(".banner-link").TextContent,
            StringComparison.Ordinal);

        using var portfolio = new RenderContext().With(SheetMode.Hero);
        portfolio.Services.GetRequiredService<NavigationManager>().NavigateTo("portfolio");
        Assert.Contains(
            "Build a character",
            portfolio.Render<MainLayout>().Find(".banner-link").TextContent,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The demonstrations are on the portfolio and not in the middle of the tool.
    ///
    /// <para>Both halves are asserted: it is the <em>moving</em> that matters, and a test that
    /// only checked the new home would pass just as well with the samples on both pages, which is
    /// the state this slice exists to end.</para>
    /// </summary>
    [Fact]
    public void TheSamplesAreOnThePortfolioAndNotOnTheTierPage()
    {
        using var ctx = new RenderContext();

        var portfolio = ctx.Render<Portfolio>().Markup;
        Assert.Contains("Load a Hero", portfolio, StringComparison.Ordinal);
        Assert.Contains("Load a Villain", portfolio, StringComparison.Ordinal);

        var tier = ctx.Render<ChooseTier>().Markup;
        Assert.DoesNotContain("Load a Hero", tier, StringComparison.Ordinal);
        Assert.DoesNotContain("Load a Villain", tier, StringComparison.Ordinal);

        // ...and the control that does belong to building is still there.
        Assert.Contains("Start a new character", tier, StringComparison.Ordinal);
    }
}
