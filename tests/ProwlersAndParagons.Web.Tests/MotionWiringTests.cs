using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The Blazor-to-script wiring, driven rather than read.
///
/// <para><b>This is the only cover the wiring has, and until it existed there was none.</b> The
/// source guard in <c>WebPresentationTests</c> reads <c>MainLayout.razor</c>; the browser harness
/// drives <c>motion.js</c> directly and never renders a component. Between them sat the part that
/// is actually specific to this app — which hook the snapshot is taken from — and a reviewer moved
/// it to the wrong one with the whole suite and all six harnesses green.</para>
/// </summary>
public sealed class MotionWiringTests
{
    /// <summary>
    /// Navigating opens a transition, and does so <b>while the old page is still on screen</b>.
    ///
    /// <para><b>Asserting that <c>ppMotion.begin</c> was called is not enough</b>, and the first
    /// version of this test made exactly that mistake: a snapshot taken from
    /// <c>LocationChanged</c> is also recorded, just uselessly late, and the test passed against
    /// the very mutation it was written for.</para>
    ///
    /// <para>What separates the two hooks is <em>when</em>. This registers its own
    /// <c>LocationChanging</c> handler and counts the interop calls already made by the time it
    /// runs. <c>MainLayout</c> registers during initialisation, before this one, and handlers run
    /// in registration order — so a transition opened from the changing handler has already been
    /// recorded here, and one opened from <c>LocationChanged</c> has not been recorded at all.</para>
    /// </summary>
    [Fact]
    public void NavigatingOpensATransitionWhileTheOldPageIsStillOnScreen()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Render<MainLayout>(p => p.Add(l => l.Body, "<p>body</p>"));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        // A local function over the interop rather than a delegate over `ctx`: capturing the
        // disposable in a closure is what ReSharper flags, and the interop object is the only
        // part actually wanted here.
        var interop = ctx.JSInterop;
        int Begins() => interop.Invocations.Count(i => i.Identifier == "ppMotion.begin");

        var before = Begins();
        int duringChanging = -1;

        nav.RegisterLocationChangingHandler(_ =>
        {
            duringChanging = Begins();
            return ValueTask.CompletedTask;
        });

        nav.NavigateTo("powers");

        Assert.True(
            duringChanging >= 0,
            "The navigation never reached a LocationChanging handler; this test has lost its subject.");

        Assert.True(
            duringChanging > before,
            "No transition had been opened by the time the navigation was still pending. The "
            + "snapshot is being taken from LocationChanged, which fires after the old page has "
            + "gone — the API is called, nothing is raised, and the result is silently no "
            + "animation at all.");
    }

    /// <summary>
    /// The transition is released once the new page has rendered — or the app is left showing a
    /// still image of itself with no way back.
    /// </summary>
    [Fact]
    public void TheTransitionIsReleasedAfterTheRender()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Render<MainLayout>(p => p.Add(l => l.Body, "<p>body</p>"));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("powers");

        Assert.Contains(
            ctx.JSInterop.Invocations,
            i => i.Identifier == "ppMotion.end");
    }

    /// <summary>
    /// <b>A missing <c>motion.js</c> must not stop the app.</b>
    ///
    /// <para>The interop runs on every internal navigation and after every render of every chosen
    /// list, so an unguarded call would take navigation down with a 404. <c>Motion</c> swallows
    /// the script's failure; this drives a runtime that throws on every call and requires the
    /// navigation to complete anyway.</para>
    /// </summary>
    [Fact]
    public void NavigationSurvivesAMissingScript()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        // **A JSException, because that is what an absent `window.ppMotion` actually raises.**
        // bUnit's strict mode raises a bUnit type instead, which no production catch would ever
        // see — a test written that way would demand a `catch (Exception)` in `Motion` and
        // "prove" a robustness this app does not have.
        ctx.JSInterop.SetupVoid(_ => true)
            .SetException(new JSException("window.ppMotion is not defined"));

        ctx.Render<MainLayout>(p => p.Add(l => l.Body, "<p>body</p>"));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("powers");

        Assert.EndsWith("powers", nav.Uri, StringComparison.Ordinal);

        // And the failure is recorded rather than merely ignored, so it is inspectable.
        Assert.True(ctx.Services.GetRequiredService<Motion>().ScriptIsMissing);
    }

    /// <summary>
    /// <b>No interop for a strip that is not on the page.</b>
    ///
    /// <para>Villain mode removes the whole budget section, but Blazor never clears an
    /// <c>@ref</c> when its element stops rendering — and the component stays alive and
    /// subscribed. So every change to the character fired a count at a detached node, absorbed by
    /// a null guard in the script that nobody had written down as load-bearing.</para>
    ///
    /// <para>The guard that fixed it had no cover at all: deleting the line left the whole suite
    /// green. <c>TheChromeAlwaysEndsInAVisibleEdge</c> looked like cover and is not — it asserts
    /// the string <c>Session.ShowBudget</c> occurs in the file, which the markup's own
    /// <c>@if</c> satisfies.</para>
    /// </summary>
    [Fact]
    public async Task NoCountIsIssuedWhileTheStripIsNotRendered()
    {
        await using var ctx = new RenderContext().With(SheetMode.Hero);

        var strip = ctx.Render<HpBudgetBar>();
        Assert.Contains("budget-figure", strip.Markup, StringComparison.Ordinal);

        // Switch to a Villain: Ch.9 gives no budget, so the section stops rendering entirely.
        await strip.InvokeAsync(() => ctx.Session.Mode = SheetMode.Villain);
        strip.Render();
        Assert.DoesNotContain("budget-figure", strip.Markup, StringComparison.Ordinal);

        var before = ctx.JSInterop.Invocations.Count(i => i.Identifier == "ppCount");

        // Anything that changes the spend. The component is still subscribed and still renders.
        await strip.InvokeAsync(() => ctx.Session.Sheet.AbilityRanks["might"] = 7);
        strip.Render();

        var after = ctx.JSInterop.Invocations.Count(i => i.Identifier == "ppCount");

        Assert.True(
            after == before,
            $"ppCount was called {after - before} more time(s) while the strip was not rendered. "
            + "The @ref still points at a detached node; the call is absorbed by a null guard in "
            + "the script rather than doing anything.");
    }
}
