using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
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
    /// <summary>
    /// What the account control says.
    ///
    /// <para><b>It is read off <c>.banner-account</c> and not <c>.banner-link</c>, and the
    /// change is the point rather than a rename.</b> That class carries the underline marking the
    /// two avenues as destinations; an identity is not one, and the account wore it while being
    /// none of those things. The tools cluster — Search, the account, Settings — is one idiom
    /// with no underline on any of it.</para>
    ///
    /// <para>The old spelling is also what made these tests necessary in the first place:
    /// <c>Find(".banner-link")</c> returns the first match, which was an avenue, so the account
    /// was reached by nothing. A class of its own cannot be shadowed by a neighbour.</para>
    /// </summary>
    private static IEnumerable<string> BannerLinks(IRenderedComponent<MainLayout> layout) =>
        layout.FindAll(".banner-account").Select(a => a.TextContent.Trim());

    /// <summary>
    /// Opens the settings menu and hands back the layout, because both palette switches live
    /// behind it now.
    ///
    /// <para><b>The click is asserted to have done something before anything is read out of the
    /// menu.</b> A disclosure that rendered no list satisfies every "exactly one is pressed"
    /// check completely — there being no buttons at all — which is this repository's single most
    /// common way for a guard to be wrong.</para>
    /// </summary>
    private static IRenderedComponent<MainLayout> WithSettingsOpen(RenderContext ctx)
    {
        var layout = ctx.Render<MainLayout>();

        Assert.Equal("false", layout.Find(".settings-open").GetAttribute("aria-expanded"));
        Assert.Empty(layout.FindAll(".settings-menu-list"));

        layout.Find(".settings-open").Click();

        Assert.Equal("true", layout.Find(".settings-open").GetAttribute("aria-expanded"));
        Assert.Single(layout.FindAll(".settings-menu-list"));

        return layout;
    }

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
    /// <b>The banner says the palette's chord exists, which nothing on any screen used to do.</b>
    ///
    /// <para>The key was bound from the day the palette shipped and was written down only
    /// <em>inside</em> the palette — visible to somebody who had already pressed it, which is
    /// the whole of what a shortcut for whoever wrote it means. This asserts the two halves
    /// together: the button names the thing it opens, and the chord is printed beside it.</para>
    /// </summary>
    [Fact]
    public void TheBannerNamesThePalettesChord()
    {
        using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();

        var trigger = layout.Find(".palette-open");

        Assert.Contains("Search", trigger.TextContent, StringComparison.Ordinal);
        Assert.Equal(["Ctrl", "K"], Keys(layout));
    }

    /// <summary>
    /// <b>The modifier is the reader's, not the developer's.</b>
    ///
    /// <para><c>palette.js</c> listens for <c>ctrlKey</c> <em>or</em> <c>metaKey</c> precisely
    /// because the chord is Ctrl on Windows and Linux and Command on a Mac, so a hard-coded word
    /// is wrong for half the readers — and wrong in the way that costs the affordance, since
    /// somebody who presses the key they were told about and gets nothing stops reaching for
    /// it.</para>
    ///
    /// <para>Both rows, because either alone is satisfied by a constant.</para>
    /// </summary>
    [Theory]
    [InlineData(false, "Ctrl")]
    [InlineData(true, "Cmd")]
    public void TheChordIsPrintedForTheKeyboardTheReaderHas(bool mac, string expected)
    {
        using var ctx = new RenderContext();
        ctx.JSInterop.Setup<bool?>("ppPalette.onAMac").SetResult(mac);

        var layout = ctx.Render<MainLayout>();

        Assert.Equal([expected, "K"], Keys(layout));
    }

    /// <summary>
    /// <b>A deployment with no <c>palette.js</c> promises no shortcut, and still offers the way
    /// in.</b>
    ///
    /// <para>The script is the listener: without it the chord does nothing at all, so printing
    /// it would teach a key that is not there. The button is a click Blazor handles and keeps
    /// working, which is why this is a missing hint rather than a missing control — the same
    /// bargain every guarded interop call in this app makes.</para>
    /// </summary>
    [Fact]
    public void AMissingScriptPrintsNoChordAndStillOpensThePalette()
    {
        using var ctx = new RenderContext();
        ctx.JSInterop.Setup<bool?>("ppPalette.onAMac").SetResult(null);

        var layout = ctx.Render<MainLayout>();

        Assert.Empty(Keys(layout));

        layout.Find(".palette-open").Click();

        Assert.True(ctx.Services.GetRequiredService<Commands>().IsOpen);
    }

    /// <summary>
    /// <b>The button opens the palette.</b>
    ///
    /// <para>Asserted through the rendered overlay rather than through the service alone: a
    /// handler that set the flag and drew nothing is the state a reader would read as the
    /// control being broken.</para>
    /// </summary>
    [Fact]
    public void TheButtonOpensThePalette()
    {
        using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();
        Assert.Empty(layout.FindAll(".palette"));

        layout.Find(".palette-open").Click();

        Assert.Single(layout.FindAll(".palette"));
    }

    /// <summary>
    /// <b>It is offered on every route, because the chord works on every route.</b>
    ///
    /// <para>The step band and the budget strip are the builder's and are drawn there alone;
    /// this is not one of those. A shortcut that works everywhere and is named in one place is
    /// the same defect one step smaller, and a reader who met the button only inside the builder
    /// would reasonably conclude the key stops at its edge.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("build/tier")]
    [InlineData("rules")]
    [InlineData("signin")]
    public void TheWayIntoThePaletteIsOfferedEverywhere(string route)
    {
        using var ctx = new RenderContext();
        ctx.Services.GetRequiredService<NavigationManager>()
           .NavigateTo(route);

        var layout = ctx.Render<MainLayout>();

        Assert.Equal(["Ctrl", "K"], Keys(layout));
    }

    /// <summary>The words in the key boxes of the banner's palette button, in order.</summary>
    private static List<string> Keys(IRenderedComponent<MainLayout> layout) =>
        layout.FindAll(".palette-open .key").Select(k => k.TextContent.Trim()).ToList();

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

        // The switch is inside the settings menu now, so the assertion follows it there rather
        // than being dropped. `WithSettingsOpen` proves the disclosure actually opened first: a
        // menu that rendered nothing satisfies every claim below by having no buttons at all.
        var layout = WithSettingsOpen(ctx);

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

        var layout = WithSettingsOpen(ctx);

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

        var layout = WithSettingsOpen(ctx);
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

        var layout = WithSettingsOpen(ctx);
        layout.FindAll(".theme-switch button").Single(b => b.TextContent.Trim() == "Dark").Click();
        layout.FindAll(".theme-switch button").Single(b => b.TextContent.Trim() == "Light").Click();

        Assert.Equal(before, CharacterSheetJson.Write(ctx.Session.Sheet));

        // The positive control: the *other* switch does change it, so this is a fact about the
        // theme rather than about a sheet that ignores the banner entirely.
        layout.FindAll(".mode-switch button").Single(b => b.TextContent.Trim() == "Hero").Click();

        Assert.NotEqual(before, CharacterSheetJson.Write(ctx.Session.Sheet));
    }

    /// <summary>
    /// <b>The rearranged bar still offers everything the old one did, on every route.</b>
    ///
    /// <para>This change moved four things and deleted none, which is exactly the claim a
    /// rearrangement is least able to make about itself: the two switches went into a menu, the
    /// account changed class, "Saved" moved across the bar, and every one of those is a diff a
    /// reviewer reads as "still there". So the four survivors are asserted together, per route —
    /// both avenues, the way into the palette, the account, and the control that opens the
    /// menu.</para>
    ///
    /// <para><b>Per route, because three of the bar's five groups are gated on the address.</b>
    /// The step band, the budget strip and the character switcher are the builder's; these are
    /// not, and a control that appeared on some routes and not others would say the thing behind
    /// it stops at the builder's edge.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("build/tier")]
    [InlineData("rules")]
    [InlineData("signin")]
    public void TheBarOffersBothAvenuesTheSearchTheAccountAndTheSettingsOnEveryRoute(string route)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(route);

        var layout = ctx.Render<MainLayout>();

        var avenues = layout.Find(".avenue-nav").TextContent;
        Assert.Contains("Build", avenues, StringComparison.Ordinal);
        Assert.Contains("Rules", avenues, StringComparison.Ordinal);

        Assert.Single(layout.FindAll(".palette-open"));
        Assert.Single(layout.FindAll(".banner-account"));
        Assert.Single(layout.FindAll(".settings-open"));
    }

    /// <summary>
    /// <b>The three tools are one idiom, and the account is no longer wearing navigation's
    /// clothes.</b>
    ///
    /// <para>The underline on <c>.banner-link</c> is what says "this is a destination". Search
    /// never carried it, correctly — it opens an overlay — and the account did, while being an
    /// identity rather than a place. Asserted as a pair: the account is in the tools cluster and
    /// is <em>not</em> a <c>.banner-link</c>, and the two avenues still are, because a test that
    /// only checked the first half passes just as well with the underline taken off everything,
    /// which would delete the distinction rather than apply it.</para>
    /// </summary>
    [Fact]
    public void TheAccountIsATooAndNotAnAvenue()
    {
        using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();

        var account = layout.Find(".banner-account");
        Assert.DoesNotContain("banner-link", account.GetAttribute("class") ?? "", StringComparison.Ordinal);
        Assert.Contains("banner-tool", account.GetAttribute("class") ?? "", StringComparison.Ordinal);

        // All three tools carry the shared class, and they are the only three.
        Assert.Equal(3, layout.FindAll(".banner-tools .banner-tool").Count);

        // The positive control: the avenues kept the marking that makes them destinations.
        //
        // **Three now, not two.** `Run` shipped with the campaign screens — the third door
        // `MainLayout`'s own note had been reserving, which it said would cost one `NavLink` and
        // did. The number is asserted rather than left loose because the point of this control is
        // that the avenues are a closed set with a marking of their own: a tool that had quietly
        // grown the underline would arrive here as a fourth avenue.
        var avenues = layout.FindAll(".avenue-nav .banner-link");
        Assert.Equal(3, avenues.Count);
    }

    /// <summary>
    /// <b>"Saved" sits with the character it reports, not with the account.</b>
    ///
    /// <para>It reports a write of the <em>document</em>. Beside the account link it read as a
    /// comment on whoever was signed in — and on a shared machine that is the reading that
    /// matters. It is in the character cluster now, with the switcher.</para>
    ///
    /// <para><b>And the live region is <em>not</em> gated on the builder, though the switcher
    /// is.</b> That asymmetry is the load-bearing part: the same region carries the undo offer
    /// for four acts that replace the character wherever the reader happens to be standing — the
    /// portfolio's two sample buttons and a recording that then navigates away from itself are
    /// both outside the builder. Gating it with the switcher would take the offer off the two
    /// routes that need it most. Asserted on a route where the switcher is absent, or the claim
    /// is untested.</para>
    /// </summary>
    [Fact]
    public void TheSaveRegionSitsWithTheCharacterAndSurvivesLeavingTheBuilder()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("build/tier");

        var building = ctx.Render<MainLayout>();

        Assert.Single(building.FindAll(".banner-character .character-switch"));
        Assert.Single(building.FindAll(".banner-character .save-status"));

        // ...and nothing of the character's is in the tools cluster any more.
        Assert.Empty(building.FindAll(".banner-tools .save-status"));

        using var reading = new RenderContext().With(SheetMode.Hero);
        reading.Services.GetRequiredService<NavigationManager>().NavigateTo("admin/portfolio");

        var elsewhere = reading.Render<MainLayout>();

        // The switcher is the builder's and is gone; the region it sits beside is not and stays.
        Assert.Empty(elsewhere.FindAll(".character-switch"));
        Assert.Single(elsewhere.FindAll(".save-status"));
        Assert.Equal("polite", elsewhere.Find(".save-status").GetAttribute("aria-live"));
    }

    /// <summary>
    /// <b>Nothing the settings menu draws reaches paper.</b>
    ///
    /// <para>Both switches used to be named in <c>app.css</c>'s <c>@@media print</c> block by their
    /// own classes. They have moved inside a disclosure, and a selector that has stopped matching
    /// anything is a print rule that quietly does nothing — the failure shape
    /// <c>ThePrintedSheetLeavesOutTheToolAroundIt</c> already guards the other direction of.</para>
    ///
    /// <para><b>It crosses the two halves, which is the only way to see it.</b> The classes come
    /// from a rendered, opened menu — whatever the component actually writes today — and each is
    /// looked for in the stylesheet's print block. A list of names in this file would go stale the
    /// first time somebody renamed one, which is precisely the event it exists to catch.</para>
    /// </summary>
    [Fact]
    public void TheSettingsMenusControlsDoNotPrint()
    {
        using var ctx = new RenderContext();

        var layout = WithSettingsOpen(ctx);

        var drawn = layout.FindAll(".settings-menu-list [role=\"group\"]")
            .SelectMany(g => (g.GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The positive control. An opened menu that drew no groups would satisfy every claim
        // below by iterating nothing at all — three of this repository's four historical guard
        // faults are that exact shape.
        Assert.Equal(2, drawn.Count);

        var print = PrintBlockOfAppCss();

        foreach (var name in drawn)
            Assert.Contains($".{name}", print, StringComparison.Ordinal);

        // And the disclosure itself, which is what actually holds them on the page.
        Assert.Contains(".settings-menu", print, StringComparison.Ordinal);
        Assert.Contains("display: none", print, StringComparison.Ordinal);
    }

    /// <summary>
    /// The <c>@@media print</c> block of <c>app.css</c>, comments stripped.
    ///
    /// <para>Comments stripped because that block's own prose names three of the selectors it
    /// hides while explaining why they are named separately — so a scan of the raw text finds
    /// every one of them whether or not the rule still does.</para>
    /// </summary>
    private static string PrintBlockOfAppCss()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "web", "wwwroot", "css", "app.css"));

        css = new Regex(@"/\*.*?\*/", RegexOptions.Singleline, TimeSpan.FromSeconds(5))
            .Replace(css, " ");

        var at = css.IndexOf("@media print", StringComparison.Ordinal);
        Assert.True(at >= 0, "app.css no longer has a print block at all.");

        return css[at..];
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
