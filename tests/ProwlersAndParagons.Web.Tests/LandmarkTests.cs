using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The skip link, and the landmarks it and everything else in the shell has to agree with.
///
/// <para>There was none of either before this. A reader who tabs from the address bar met the
/// banner's five buttons and two links before reaching anything the page was actually about, on
/// every route, every time — and nothing here told a screen reader which of the several regions
/// on the page was the one to land in.</para>
/// </summary>
public sealed class LandmarkTests
{
    /// <summary>
    /// The skip link is the first thing written in the layout, and it points at a target that
    /// actually exists and can actually take focus.
    ///
    /// <para><b>Order matters and markup alone proves it</b>: a skip link placed after the banner
    /// is not a skip link, it is a link with the right words in the wrong position — a reader
    /// tabbing from the address bar would still meet the banner first. Asserted by finding both
    /// substrings and comparing their positions, since <c>bUnit</c> has no concept of tab order
    /// to ask directly.</para>
    /// </summary>
    [Fact]
    public void TheSkipLinkComesBeforeTheBannerAndPointsAtMain()
    {
        using var ctx = new RenderContext();
        var layout = ctx.Render<MainLayout>(p => p.Add(l => l.Body, "<p>content</p>"));

        var skip = layout.Find("a.skip-link");
        Assert.Equal("#main-content", skip.GetAttribute("href"));
        Assert.Equal("Skip to main content", skip.TextContent.Trim());

        var main = layout.Find("main");
        Assert.Equal("main-content", main.GetAttribute("id"));

        // -1, not 0: a landing spot for a deliberate jump, not a stop an ordinary Tab should
        // ever land on. Tabbing through the page must not gain a stray empty stop at <main>.
        Assert.Equal("-1", main.GetAttribute("tabindex"));

        var markup = layout.Markup;
        var skipIndex = markup.IndexOf("skip-link", StringComparison.Ordinal);
        var bannerIndex = markup.IndexOf("class=\"banner\"", StringComparison.Ordinal);

        Assert.True(skipIndex >= 0, "The skip link is not in the rendered markup at all.");
        Assert.True(bannerIndex >= 0, "The banner is not in the rendered markup at all.");
        Assert.True(skipIndex < bannerIndex,
            "The skip link is written after the banner, so a reader tabbing from the address "
            + "bar meets the banner first — the one thing a skip link exists to prevent.");
    }

    /// <summary>
    /// Exactly one banner, exactly one main, and every nav on the page named distinctly.
    ///
    /// <para><b>"Correct and non-duplicated" is a claim about the whole shell, not about any one
    /// element</b>, so this renders the layout on its default route — where the step band and
    /// the budget strip are both absent — and again on a <c>/build</c> route, where the step
    /// band's own <c>&lt;nav&gt;</c> joins the banner's. Two navs with the same name would be
    /// indistinguishable to a reader who jumps between landmarks by name; two with none would be
    /// indistinguishable by nothing at all.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("build/characteristics")]
    public void TheShellHasExactlyOneOfEachLandmarkAndEveryNavIsNamed(string path)
    {
        using var ctx = new RenderContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var layout = ctx.Render<MainLayout>(p => p.Add(l => l.Body, "<p>content</p>"));

        Assert.Single(layout.FindAll("header"));
        Assert.Single(layout.FindAll("main"));

        var navs = layout.FindAll("nav");
        Assert.NotEmpty(navs);

        var labels = navs.Select(n => n.GetAttribute("aria-label")).ToList();
        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l),
            "A <nav> on the shell carries no aria-label, so a reader jumping between landmarks "
            + "by name has nothing to jump to."));

        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }
}
