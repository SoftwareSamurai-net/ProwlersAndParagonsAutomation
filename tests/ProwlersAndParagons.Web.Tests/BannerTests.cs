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
    public async Task TheNameFollowsASignInWithoutAReload()
    {
        await using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();
        Assert.Contains("Sign in", BannerLinks(layout));

        ctx.Api.SignedIn = ("acct-7", "dorian");

        // Signed in through the real service, on the renderer's own thread, so what is exercised
        // is the layout's subscription rather than a re-render that happens to read a new value.
        var accounts = ctx.Services.GetRequiredService<Accounts>();
        await layout.InvokeAsync(async () => await accounts.CompleteSignInAsync("a-token"));

        Assert.Contains("dorian", BannerLinks(layout));
        Assert.DoesNotContain("Sign in", BannerLinks(layout));
    }

    /// <summary>
    /// The banner follows a name change the same way it follows a sign-in — through the one
    /// service both it and the account panel share, rather than a reload.
    /// </summary>
    [Fact]
    public async Task TheBannerFollowsAnOwnNameChangeWithoutAReload()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var layout = ctx.Render<MainLayout>();
        Assert.Contains("player", BannerLinks(layout));

        var accounts = ctx.Services.GetRequiredService<Accounts>();
        await layout.InvokeAsync(async () => await accounts.SetDisplayNameAsync("Dorian"));

        Assert.Contains("Dorian", BannerLinks(layout));
        Assert.DoesNotContain("player", BannerLinks(layout));
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

    /// <summary>
    /// <b>The light/dark control has three buttons and exactly one of them is pressed.</b>
    ///
    /// <para>Three, because there are three states and the third is not "off": following the
    /// system is a live setting, and a two-state toggle can only land somebody on whichever
    /// value their system held at the moment they touched it.</para>
    ///
    /// <para>Asserted as a set for the reason the mode switch above gives — the mutation that
    /// got through there made every button say <c>"true"</c>, which each button on its own
    /// satisfies.</para>
    /// </summary>
    [Theory]
    [InlineData("light", "Light")]
    [InlineData("dark", "Dark")]
    [InlineData(null, "Auto")]
    public void ExactlyOneThemeButtonAnnouncesItselfAsPressed(string? stored, string expected)
    {
        using var ctx = new RenderContext();
        ctx.JSInterop.Setup<string?>("ppTheme.current").SetResult(stored);

        var layout = ctx.Render<MainLayout>();

        var buttons = layout.FindAll(".theme-switch button");
        Assert.Equal(3, buttons.Count);

        Assert.Equal(
            [expected],
            buttons.Where(b => b.GetAttribute("aria-pressed") == "true")
                   .Select(b => b.TextContent.Trim())
                   .ToList());

        Assert.All(buttons.Where(b => b.TextContent.Trim() != expected),
            b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    /// <summary>
    /// <b>Clicking a theme button sends the choice to the script, and the control moves.</b>
    ///
    /// <para>Both halves, because either alone is satisfied by a bug: a handler that pushes and
    /// never updates leaves the reader looking at the state they did not choose, and one that
    /// updates and never pushes changes the buttons and nothing else on the page.</para>
    /// </summary>
    [Fact]
    public void ChoosingAThemePushesItAndMovesTheControl()
    {
        using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();
        var dark = layout.FindAll(".theme-switch button").Single(b => b.TextContent.Trim() == "Dark");

        dark.Click();

        Assert.Contains(ctx.JSInterop.Invocations,
            i => i.Identifier == "ppTheme.set" && i.Arguments.Contains("dark"));

        Assert.Equal("true",
            layout.FindAll(".theme-switch button")
                  .Single(b => b.TextContent.Trim() == "Dark")
                  .GetAttribute("aria-pressed"));
    }

    /// <summary>
    /// <b>The light/dark choice never reaches the character.</b>
    ///
    /// <para>It is a fact about a person and a browser, not about a Hero — the same argument
    /// that made <c>UnlimitedBudget</c> a token beside <c>IsVillain</c> rather than a meaning
    /// inside it. A theme on the sheet would travel through an export and change the screen of
    /// whoever imported somebody else's character.</para>
    ///
    /// <para>Asserted by round-tripping the sheet through the reader the app and the headless
    /// command share, rather than by naming fields: a field list goes stale, and this notices
    /// however the leak is spelled.</para>
    /// </summary>
    [Fact]
    public void ChoosingAThemeChangesNothingAboutTheCharacter()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);

        var before = CharacterSheetJson.Write(ctx.Session.Sheet);

        var layout = ctx.Render<MainLayout>();
        layout.FindAll(".theme-switch button").Single(b => b.TextContent.Trim() == "Dark").Click();
        layout.FindAll(".theme-switch button").Single(b => b.TextContent.Trim() == "Light").Click();

        Assert.Equal(before, CharacterSheetJson.Write(ctx.Session.Sheet));

        // The positive control: the *other* switch does change it, so this is a fact about the
        // theme rather than about a sheet that ignores the banner entirely.
        layout.FindAll(".mode-switch button").Single(b => b.TextContent.Trim() == "Hero").Click();

        Assert.NotEqual(before, CharacterSheetJson.Write(ctx.Session.Sheet));
    }
}
