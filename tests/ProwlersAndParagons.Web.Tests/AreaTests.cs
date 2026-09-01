using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using ProwlersAndParagonsAutomation.Web.Layout;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// One site doing several jobs, and the chrome saying which one you are in.
///
/// <para>The builder is the six creation steps. The rules reference is the book, searchable. The
/// account pages are who may sign in and the demonstrations kept for showing somebody. The front
/// door is none of them and offers all of them. The furniture above the page was written for the
/// builder and means nothing anywhere else.</para>
/// </summary>
public sealed class AreaTests
{
    [Theory]
    [InlineData("", Area.Home)]
    [InlineData("build", Area.Play)]
    [InlineData("build/characteristics", Area.Play)]
    [InlineData("build/review", Area.Play)]

    // The roster. Under the builder's own prefix, so it is the builder by construction rather
    // than by a case in `Areas.Of` saying "this one is the builder too" — which is what a
    // top-level /characters would have needed, and what the prefix scheme exists to avoid.
    [InlineData("build/characters", Area.Play)]
    [InlineData("rules", Area.Rules)]
    [InlineData("campaign", Area.Campaign)]
    [InlineData("campaign/g_AAAAAAAAAAAAAAAAAAAAAA", Area.Campaign)]
    // Matched on the segment, so a later `/campaigns` would be its own area rather than this one.
    [InlineData("campaigns", Area.Home)]
    [InlineData("sheet", Area.Sheet)]
    [InlineData("sheet/c_AAAAAAAAAAAAAAAAAAAAAA", Area.Sheet)]
    [InlineData("admin", Area.Account)]
    [InlineData("Admin", Area.Account)]
    [InlineData("admin/portfolio", Area.Account)]
    [InlineData("admin/portfolio/replay/the-conductor", Area.Account)]
    [InlineData("signin", Area.Account)]
    public void AnAddressKnowsWhichPartOfTheSiteItIsIn(string path, Area expected) =>
        Assert.Equal(expected, Areas.Of(path));

    /// <summary>
    /// <b>Case-insensitively, because Blazor's own route matching is.</b> A capitalised link is
    /// served by the app, and an ordinal comparison here would serve the page without the chrome
    /// that belongs to it — reachable by anybody who capitalised a shared address.
    /// </summary>
    [Theory]
    [InlineData("Build", Area.Play)]
    [InlineData("BUILD/gear", Area.Play)]
    [InlineData("Rules", Area.Rules)]
    [InlineData("Campaign", Area.Campaign)]
    [InlineData("CAMPAIGN/g_AAAAAAAAAAAAAAAAAAAAAA", Area.Campaign)]
    [InlineData("Sheet", Area.Sheet)]
    [InlineData("SHEET/c_AAAAAAAAAAAAAAAAAAAAAA", Area.Sheet)]
    [InlineData("SignIn", Area.Account)]
    public void TheMatchIsCaseInsensitive(string path, Area expected) =>
        Assert.Equal(expected, Areas.Of(path));

    /// <summary>
    /// A page whose name merely begins with a prefix is not in that area.
    ///
    /// <para>The positive control for the matching: a <c>StartsWith</c> would pass every test
    /// above and quietly capture a future page called something like "buildings".</para>
    /// </summary>
    [Theory]
    [InlineData("buildings")]
    [InlineData("build-a-team")]
    [InlineData("ruleset")]
    [InlineData("sheets")]
    [InlineData("sheet-music")]
    [InlineData("administrators")]
    public void APageMerelyBeginningWithAPrefixIsNot(string path) =>
        Assert.Equal(Area.Home, Areas.Of(path));

    /// <summary>
    /// An address nobody routed falls to the front door, not to the builder.
    ///
    /// <para><b>This reverses the old default and the reason is the not-found page.</b> Everything
    /// unrecognised used to be the builder, which was harmless only while the builder was every
    /// address: a numbered step list with one step marked current, above "no such address", offers
    /// to continue something that never started.</para>
    /// </summary>
    [Fact]
    public void AnUnroutedAddressIsNotTheBuilder() =>
        Assert.Equal(Area.Home, Areas.Of("not-found"));

    /// <summary>
    /// The builder's chrome is drawn in the builder and nowhere else — the step band as well as
    /// the budget strip.
    ///
    /// <para>Asserted through the layout, because a page cannot see the shell above it.</para>
    /// </summary>
    [Theory]
    [InlineData("build", true)]
    [InlineData("build/characteristics", true)]
    [InlineData("", false)]
    [InlineData("rules", false)]
    [InlineData("sheet", false)]
    [InlineData("sheet/c_AAAAAAAAAAAAAAAAAAAAAA", false)]
    [InlineData("signin", false)]
    [InlineData("admin", false)]
    [InlineData("admin/portfolio/replay/the-conductor", false)]
    public void TheStepsAndTheBudgetBelongToTheBuilderAlone(string path, bool expected)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var shell = ctx.Render<MainLayout>();

        Assert.Equal(expected, shell.FindAll(".steps").Count > 0);
        Assert.Equal(expected, shell.FindAll(".budget").Count > 0);
    }

    /// <summary>
    /// All three avenues are offered from every address, rather than one link naming whichever
    /// half the reader is not in.
    ///
    /// <para><b>The flipping cross-link was right for two rooms and wrong for three.</b> It told
    /// you where you were not, which only identifies a destination while there is exactly one such
    /// place; with a front door, a builder and a reference it named one of two elsewheres and hid
    /// the other.</para>
    ///
    /// <para><b>Three now, and this asserted two while there were three.</b> <c>Run</c> shipped
    /// with the campaign screens — the door `MainLayout` had been reserving — and it is offered
    /// from everywhere for exactly the reason the other two are: a reader standing in a campaign
    /// needs the way back into the builder as much as the reverse. The campaign address is in the
    /// theory's own list too, because "from everywhere" has to include the newest room.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("build")]
    [InlineData("rules")]
    [InlineData("sheet")]
    [InlineData("admin")]
    [InlineData("campaign")]
    public void EveryAvenueIsOfferedFromEverywhere(string path)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var nav = ctx.Render<MainLayout>().Find(".avenue-nav").TextContent;

        Assert.Contains("Build", nav, StringComparison.Ordinal);
        Assert.Contains("Run", nav, StringComparison.Ordinal);
        Assert.Contains("Rules", nav, StringComparison.Ordinal);
    }

    /// <summary>
    /// The banner names the palette in the builder and nowhere else.
    ///
    /// <para>A rules search is not a Hero or a Villain. Saying so there is the banner reporting
    /// the visitor's own character over a page with nothing to do with it, which is the fault the
    /// budget strip was pulled off three areas to fix.</para>
    /// </summary>
    [Fact]
    public void OnlyTheBuilderNamesThePalette()
    {
        using var building = new RenderContext().With(SheetMode.Villain);
        building.Services.GetRequiredService<NavigationManager>().NavigateTo("build");
        Assert.Contains("Villain", building.Render<MainLayout>().Find(".banner-title").TextContent,
            StringComparison.Ordinal);

        using var reading = new RenderContext().With(SheetMode.Villain);
        reading.Services.GetRequiredService<NavigationManager>().NavigateTo("rules");
        Assert.DoesNotContain("Villain", reading.Render<MainLayout>().Find(".banner-title").TextContent,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The demonstrations are behind the account pages and not in the middle of the builder.
    ///
    /// <para>Both halves are asserted: it is the <em>moving</em> that matters, and a test that
    /// only checked the new home would pass just as well with the samples on both pages, which is
    /// the state this exists to end.</para>
    /// </summary>
    [Fact]
    public void TheSamplesAreBehindTheAccountAndNotOnTheTierPage()
    {
        using var ctx = new RenderContext().AsAdministrator();

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
