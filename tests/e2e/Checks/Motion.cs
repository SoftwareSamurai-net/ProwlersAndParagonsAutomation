using Microsoft.Playwright;

namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// Somebody who asked their system for less movement gets none.
///
/// <para><b>Nothing in this repository had ever pressed this.</b> `js/motion.js` checks
/// `prefers-reduced-motion` at every entry point — the tokens in `theme.css` collapse for a reduced
/// -motion reader, but a token cannot reach `element.animate()` or `startViewTransition()`, so the
/// script has to ask for itself. Two guards in `WebPresentationTests` assert only that the file
/// *mentions* `still()`; `CLAUDE.md` records that both stay green against a script doing the
/// opposite, because `|| !still()) return` is one character and serves the animation to exactly the
/// people who asked for none. Everything else was a `file://` proof page, which has no media
/// preference to emulate.</para>
///
/// <para><b>Control: the same page, in a browser that has not asked for less movement, animates.</b>
/// This is the half that matters. "Nothing animated" is satisfied by a page that never rendered, by
/// a script that failed to load, and by a navigation that did not happen — three of this
/// repository's four historical guard faults were exactly that, and `motion.js` carries its own
/// counters (`window.ppMotionStats`) because of it. So the check runs the identical steps twice, in
/// two browser contexts that differ in one property, and requires the counters to move in one and
/// stay still in the other.</para>
///
/// <para><b>Outcome: under `prefers-reduced-motion: reduce` the counters do not move, and the end
/// state is right anyway.</b> The second half is not decoration — the whole point of the reduced
/// path is that the reader gets the answer immediately rather than a shorter animation, so a
/// script that simply did nothing at all would satisfy "no animation" and be a worse bug than the
/// one this guards.</para>
/// </summary>
public static class Motion
{
    public static Check Check => new("MOTION", Run);

    /// <summary>The tier this picks, and what its budget strip must read. See <see cref="Build"/>
    /// on why it is deliberately not the default one.</summary>
    private const string Tier = "High Level";

    private const string TierPoints = "150";

    /// <summary>Every counter `js/motion.js` keeps, as one comparable number.</summary>
    private const string Moved =
        "(window.ppMotionStats ? window.ppMotionStats.counts + window.ppMotionStats.transitions "
        + "+ window.ppMotionStats.landings : -1)";

    private static async Task<string> Run(Harness harness)
    {
        // The control first, in a second context with no preference set — deliberately not the
        // harness's own page, so that both halves are the same fresh browser arriving at the same
        // address, and neither is reading state the other left.
        var allowed = await harness.InAnotherBrowser(
            new BrowserNewContextOptions { ReducedMotion = ReducedMotion.NoPreference },
            page => Exercise(page, reduced: false));

        Harness.Control(allowed.Moved > 0,
            "with motion allowed, js/motion.js recorded no movement at all — so this browser "
            + "animates nothing on this page for a reason that has nothing to do with the "
            + "preference, and the reduced-motion half below would pass against a broken script, "
            + "a failed navigation or a page that never rendered");

        Harness.Control(allowed.Budget?.Contains(TierPoints, StringComparison.Ordinal) == true,
            $"with motion allowed the budget strip read \"{allowed.Budget}\" rather than the "
            + $"{TierPoints} the {Tier} tier sets, so the steps this check drives did not happen");

        var reduced = await harness.InAnotherBrowser(
            new BrowserNewContextOptions { ReducedMotion = ReducedMotion.Reduce },
            page => Exercise(page, reduced: true));

        Harness.Control(reduced.Matched,
            "the second context did not report prefers-reduced-motion as matching, so the "
            + "preference was never emulated and the outcome below is about nothing");

        Harness.Outcome(reduced.Moved == 0,
            $"under prefers-reduced-motion: reduce, js/motion.js still recorded {reduced.Moved} "
            + $"movement(s) — counts {reduced.Counts}, transitions {reduced.Transitions}, "
            + $"landings {reduced.Landings}. The tokens collapse for this reader, but a token "
            + "cannot reach element.animate(): every entry point has to ask still() for itself.");

        Harness.Outcome(reduced.Animations == 0,
            $"under prefers-reduced-motion: reduce the document still had {reduced.Animations} "
            + "running animation(s) after the same steps");

        // The other half of the promise, and the reason "does nothing" is not the right answer
        // either: the reduced path assigns the end state immediately.
        Harness.Outcome(reduced.Budget?.Contains(TierPoints, StringComparison.Ordinal) == true,
            $"under prefers-reduced-motion the budget strip read \"{reduced.Budget}\" rather than "
            + $"the {TierPoints} the {Tier} tier sets — the animation was skipped and so was the "
            + "answer, which is a worse fault than animating");

        return $"motion allowed: {allowed.Moved} movement(s); reduced: {reduced.Moved}, "
            + $"{reduced.Animations} running animation(s), budget still \"{reduced.Budget}\"";
    }

    /// <summary>What one context saw.</summary>
    private sealed record Seen(
        int Counts, int Transitions, int Landings, int Animations, string? Budget, bool Matched)
    {
        public int Moved => Counts + Transitions + Landings;
    }

    /// <summary>
    /// The identical steps in whichever context: arrive, choose a tier — which moves the budget, so
    /// `ppCount` runs — and walk to the next step, which is what opens a view transition.
    /// </summary>
    private static async Task<Seen> Exercise(Harness harness, bool reduced)
    {
        await harness.Open("/build");

        var matched = await harness.Eval<bool>(
            "window.matchMedia('(prefers-reduced-motion: reduce)').matches");

        Harness.Control(matched == reduced,
            $"the context asked for reduced-motion={reduced} and the page reports {matched}, so "
            + "the emulation did not take");

        Harness.Control(await harness.Eval<bool>("!!window.ppMotionStats"),
            "js/motion.js did not run at all on this page, so its counters cannot say anything");

        await harness.Page.Locator($".cards button:has-text(\"{Tier}\")").ClickAsync();

        await harness.WaitFor(
            "[...document.querySelectorAll('button')].some(b => b.querySelector('.name') "
            + $"&& b.querySelector('.name').textContent.includes('{Tier}') "
            + "&& b.querySelector('.name').textContent.includes('Selected'))",
            $"the {Tier} card to report itself selected", 15_000);

        // A step link rather than `Open`, because `ppMotion.begin` is wired to Blazor's
        // LocationChanging: a fresh navigation from outside the app would not open a transition
        // at all, in either context, and the control would then be measuring nothing.
        var next = harness.Page.Locator("a[href='build/characteristics'], a[href='/build/characteristics']");

        if (await next.CountAsync() > 0)
        {
            await next.First.ClickAsync();
            await harness.WaitForApp();
        }

        // Read *after* the work and with no wait of its own: a running animation is running now or
        // it is not. Under reduced motion `ppCount` assigns the figure and returns before it ever
        // creates one, so there is nothing to wait for and waiting would only give the allowed
        // context's animations time to finish and disappear.
        var animations = await harness.Eval<int>("document.getAnimations().length");

        var counts = await harness.Eval<int>("window.ppMotionStats?.counts ?? -1");
        var transitions = await harness.Eval<int>("window.ppMotionStats?.transitions ?? -1");
        var landings = await harness.Eval<int>("window.ppMotionStats?.landings ?? -1");

        await harness.Open("/build");
        var budget = await harness.Eval<string?>(
            "document.querySelector('.budget-of')?.textContent?.trim() ?? null");

        return new Seen(counts, transitions, landings, animations, budget, matched);
    }
}
