using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Web.Layout;
using ProwlersAndParagonsAutomation.Web.Services;

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

        var begins = () => ctx.JSInterop.Invocations
            .Count(i => i.Identifier == "ppMotion.begin");

        var before = begins();
        int duringChanging = -1;

        nav.RegisterLocationChangingHandler(_ =>
        {
            duringChanging = begins();
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
}
