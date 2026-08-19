using Bunit;
using ProwlersAndParagonsAutomation.Web.Layout;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The banner's two controls, both of which were completely untested.
///
/// <para><b>Found by a reviewer, and by mutation rather than by reading.</b> Hardcoding the
/// account link to say "Sign in" whoever is reading it left all 333 tests passing; so did making
/// <c>Pressed</c> always return <c>"true"</c>, which announces <em>both</em> Hero and Villain as
/// pressed at once — an invalid ARIA state that says the opposite of the truth for one of
/// them.</para>
///
/// <para><b>The reason nothing caught either is worth recording: <c>Find(".banner-link")</c>
/// returns the first match.</b> `AreaTests` uses it for the door between the two halves of the
/// site, which sits immediately before the account link in the markup — so the second one was
/// never reached by anything, and `UppercasedTextTests` had already listed both `.banner-link`
/// and `.mode-switch button` among the selectors it cannot reach. Two tests acknowledging a gap
/// is not the same as a test covering it.</para>
/// </summary>
public sealed class BannerTests
{
    private static IEnumerable<string> BannerLinks(IRenderedComponent<MainLayout> layout) =>
        layout.FindAll(".banner-link").Select(a => a.TextContent.Trim());

    [Fact]
    public void AVisitorWithNoAccountIsOfferedOne()
    {
        using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();

        Assert.Contains("Sign in", BannerLinks(layout));
    }

    /// <summary>
    /// Somebody signed in is named, because the character on screen belongs to somebody and a
    /// shared machine is where that matters.
    /// </summary>
    [Fact]
    public void SomebodySignedInIsNamedRatherThanOfferedASignIn()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "dorian");

        var layout = ctx.Render<MainLayout>();

        Assert.Contains("dorian", BannerLinks(layout));
        Assert.DoesNotContain("Sign in", BannerLinks(layout));
    }

    /// <summary>
    /// The name follows a sign-in without a reload, which is the whole reason identity is a
    /// service that can change its mind rather than a value read once at startup.
    /// </summary>
    [Fact]
    public void TheNameFollowsASignInWithoutAReload()
    {
        using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();
        Assert.Contains("Sign in", BannerLinks(layout));

        ctx.Api.SignedIn = ("acct-7", "dorian");
        ctx.Render<MainLayout>();   // a second component, to prove the change is not per-instance

        var accounts = ctx.Services.GetRequiredService<Accounts>();
        layout.InvokeAsync(async () => await accounts.CompleteSignInAsync("a-token")).Wait();

        Assert.Contains("dorian", BannerLinks(layout));
    }

    /// <summary>
    /// <b>Exactly one of the two mode buttons announces itself as pressed.</b>
    ///
    /// <para>Asserted as a pair rather than one at a time, because the mutation that got through
    /// made <em>both</em> say <c>"true"</c> — which each button on its own would satisfy. And
    /// asserted on the string, because Blazor renders a <c>true</c> bool bound to an
    /// <c>aria-*</c> attribute as <c>aria-pressed=""</c>, which assistive technology reads as
    /// <em>not</em> pressed: the obvious spelling announces the opposite of the state in both
    /// directions.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero, "Hero")]
    [InlineData(SheetMode.Villain, "Villain")]
    public void ExactlyOneModeButtonAnnouncesItselfAsPressed(SheetMode mode, string expected)
    {
        using var ctx = new RenderContext();
        ctx.Session.Mode = mode;

        var layout = ctx.Render<MainLayout>();

        var pressed = layout.FindAll(".mode-switch button")
            .Where(b => b.GetAttribute("aria-pressed") == "true")
            .Select(b => b.TextContent.Trim())
            .ToList();

        Assert.Equal([expected], pressed);

        // And the other one says so explicitly rather than dropping the attribute — an absent
        // aria-pressed is a button with no state at all.
        var notPressed = layout.FindAll(".mode-switch button")
            .Where(b => b.TextContent.Trim() != expected)
            .ToList();

        Assert.All(notPressed, b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }
}
