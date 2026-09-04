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

        // **Both mechanisms, separately, and not their sum.** The sum was the first version of
        // this control and it hid a hole: the counting figure ran, the view transition did not,
        // and one movement satisfied `> 0` while half of what this check claims to cover was
        // never exercised in either context.
        Harness.Control(allowed.Counts > 0,
            "with motion allowed, js/motion.js's counting figure never ran (ppMotionStats.counts "
            + "is 0), so the reduced-motion half below would pass against a script that does "
            + "nothing at all");

        Harness.Control(allowed.Transitions > 0,
            "with motion allowed, no view transition opened (ppMotionStats.transitions is 0) — so "
            + "this browser does not run them here, and asserting that a reduced-motion reader "
            + "gets none would be measuring nothing");

        Harness.Control(allowed.Budget?.Contains(TierPoints, StringComparison.Ordinal) == true,
            $"with motion allowed the budget strip read \"{allowed.Budget}\" rather than the "
            + $"{TierPoints} the {Tier} tier sets, so the steps this check drives did not happen");

        var reduced = await harness.InAnotherBrowser(
            new BrowserNewContextOptions { ReducedMotion = ReducedMotion.Reduce },
            page => Exercise(page, reduced: true));

        Harness.Control(reduced.Matched,
            "the second context did not report prefers-reduced-motion as matching, so the "
            + "preference was never emulated and the outcome below is about nothing");

        Harness.Outcome(reduced.Counts + reduced.Transitions + reduced.Landings == 0,
            "under prefers-reduced-motion: reduce, js/motion.js still recorded movement — counts "
            + $"{reduced.Counts}, transitions {reduced.Transitions}, landings {reduced.Landings}. "
            + "The tokens collapse for this reader, but a token cannot reach element.animate(): "
            + "every entry point has to ask still() for itself.");

        Harness.Outcome(reduced.Animations == 0,
            $"under prefers-reduced-motion: reduce the document still had {reduced.Animations} "
            + "running animation(s) after the same steps");

        // **The other half of the promise, and the reason "does nothing" is the wrong answer
        // too.** Somebody who asked for less movement is owed the end state immediately, not a
        // blank. `ppCount` assigns `to` and returns on the reduced path; a script that simply
        // skipped the work would leave the previous figure on screen, pass every assertion above,
        // and be a worse bug than the one they are guarding against.
        Harness.Outcome(reduced.Spent == allowed.Spent,
            $"under prefers-reduced-motion the spent figure read \"{reduced.Spent}\" where the "
            + $"same steps with motion allowed left \"{allowed.Spent}\". The animation is skipped "
            + "for this reader; the answer is not.");

        Harness.Outcome(reduced.Budget?.Contains(TierPoints, StringComparison.Ordinal) == true,
            $"under prefers-reduced-motion the budget strip read \"{reduced.Budget}\" rather than "
            + $"the {TierPoints} the {Tier} tier sets");

        // The figures, per mechanism, because the sum is what hid a half that never ran.
        return $"motion allowed: {allowed.Counts} count(s), {allowed.Transitions} transition(s), "
            + $"{allowed.Landings} landing(s); reduced: {reduced.Counts}/{reduced.Transitions}/"
            + $"{reduced.Landings}, {reduced.Animations} running animation(s), spent figure "
            + $"\"{reduced.Spent}\" either way";
    }

    /// <summary>What one context saw.</summary>
    private sealed record Seen(
        int Counts, int Transitions, int Landings, int Animations,
        string? Budget, string? Spent, bool Matched);

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

        // **Everything after this point happens without a full navigation, and the ordering is the
        // whole reason this check was wrong twice.** `window.ppMotionStats` lives in the document:
        // `harness.Open` loads a new one and the counters go back to zero. The first version read
        // them after an `Open` and reported `transitions: 0` against a transition that had really
        // happened, which reads exactly like a browser that does not support them.
        await harness.Open("/build/characteristics");

        var budget = await harness.Eval<string?>(
            "document.querySelector('.budget-of')?.textContent?.trim() ?? null");

        // **A purchase, because choosing a tier does not run the counting figure.** The strip
        // counts what has been *spent*, and a tier changes what may be spent — so an earlier
        // version clicked a tier card, recorded `counts: 0`, and passed on the transition alone.
        // Raising a Characteristic is the cheapest thing in this application that moves the figure
        // `ppCount` animates.
        var raise = harness.Page.Locator(".stepper button[aria-label^='Raise']").First;

        Harness.Control(await raise.CountAsync() > 0,
            "there is no rank stepper on /build/characteristics, so nothing here spends a Hero "
            + "Point and the counting figure has no reason to run");

        await raise.ClickAsync();

        // Waited for only where one is expected. Under reduced motion `ppCount` assigns and
        // returns without touching the counter, and waiting for a counter that must stay still
        // would spend the whole timeout on every run.
        if (!reduced)
        {
            await harness.WaitFor("(window.ppMotionStats?.counts ?? 0) > 0",
                "the counting figure to run after a Hero Point was spent", 15_000);
        }

        // Read now, while it is still the same document, and read *after* the count has settled:
        // `ppCount` assigns the engine's own number on its resting frame, so an animation caught
        // mid-flight would compare an interpolated figure against an assigned one.
        if (!reduced) await harness.WaitFor("!document.querySelector('.budget-figure .num')?.ppCount",
            "the counting figure to come to rest", 15_000);

        // **The figure `ppCount` writes, not the budget beside it.** `.budget-of` is the tier's
        // "/ 150" and is rendered by C# whatever the script does; `.budget-figure .num` is the
        // element handed to `ppCount`, and it is the one that is wrong if the reduced path skips
        // the animation *and* the answer.
        var spent = await harness.Eval<string?>(
            "document.querySelector('.budget-figure .num')?.textContent?.trim() ?? null");

        // **A link inside the app rather than `Open`, because `ppMotion.begin` is wired to Blazor's
        // `LocationChanging`** (`MainLayout.OpenTransition`): a navigation from outside the app
        // never fires it, so a check that navigated that way would record no transition in either
        // context and its comparison would be between two zeroes.
        //
        // **And the link is required rather than skipped if absent.** This was
        // `a[href='build/characteristics']`, which is not on the page it looked at — so
        // `CountAsync()` was 0, the click never happened, and the transition half silently measured
        // nothing while still reporting PASS. Found by reading the pass line's own figures.
        var next = harness.Page.Locator("a.banner-link[href='rules']");

        Harness.Control(await next.CountAsync() > 0,
            "the banner's Rules link is not on this page, so nothing here navigates inside the "
            + "app and no view transition can open. Find the link that does rather than skipping "
            + "it: a skipped half reports PASS.");

        await next.First.ClickAsync();
        await harness.WaitForApp();

        // No wait of its own: a running animation is running now or it is not, and waiting would
        // only give the allowed context's animations time to finish and disappear.
        var animations = await harness.Eval<int>("document.getAnimations().length");

        var counts = await harness.Eval<int>("window.ppMotionStats?.counts ?? -1");
        var transitions = await harness.Eval<int>("window.ppMotionStats?.transitions ?? -1");
        var landings = await harness.Eval<int>("window.ppMotionStats?.landings ?? -1");

        return new Seen(counts, transitions, landings, animations, budget, spent, matched);
    }
}
