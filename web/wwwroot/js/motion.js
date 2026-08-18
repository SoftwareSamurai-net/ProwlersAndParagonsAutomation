// Motion. The whole of it — there is no animation library, and the reasoning is in
// docs/FRONT-END-PLAN.md: everything here is a platform call, the payload is already this
// project's largest open item, and the View Transitions API does a thing no library can.
//
// **Every entry point checks prefers-reduced-motion itself.** The CSS tokens collapse to
// 0.01ms for a reduced-motion user, but a token cannot reach a script: `element.animate()`
// and `startViewTransition()` know nothing about them. Somebody who has asked their system
// for less movement must get the end state immediately, not a shorter animation.
const still = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

// ─────────────────────────────────────────────────────────────────────────────
// Continuity across steps — the View Transitions API.
//
// Blazor's router does not integrate with it, and the shape of the mismatch is the whole
// reason this is a shim rather than a call. `startViewTransition(cb)` snapshots the document,
// runs `cb`, then snapshots again and animates between the two — so the DOM change has to
// happen *inside* the callback. Blazor's render happens on its own schedule, after
// LocationChanged, which is already too late to capture the old state.
//
// So the callback returns a promise that is held open: `begin()` takes the first snapshot and
// stops there, Blazor navigates and renders, and `end()` resolves the promise, at which point
// the API takes the second snapshot and animates. LocationChanging fires before the
// navigation, which is what makes the first half possible at all.
let release = null;
let guard = 0;

window.ppMotion = {
    /// Snapshot the page as it is now and hold the transition open.
    begin: () => {
        // Not supported, or not wanted: do nothing at all. An unsupported browser navigates
        // exactly as it did before, which is the graceful degradation the plan asks for.
        if (!document.startViewTransition || still()) return;

        // A navigation while one is still open — somebody clicking through steps faster than
        // 260ms. Release the old one rather than stranding it: two overlapping transitions
        // leave the first's snapshot on screen for ever, which is a frozen page.
        window.ppMotion.end();

        const held = new Promise((resolve) => { release = resolve; });
        document.startViewTransition(() => held);

        // Positive control: a harness must be able to assert a transition actually opened,
        // not merely that the page looks right afterwards. Declared further down; this runs
        // long after the script has executed, so the order is fine.
        window.ppMotionStats.transitions++;

        // **The safety net, and it is not optional.** While a transition is open the live DOM
        // is hidden behind a snapshot overlay. If `end()` never arrives — a navigation that
        // throws, a handler that is disposed mid-flight — the page is left showing a still
        // image of itself with no way back. Releasing on a timer costs a transition that does
        // not animate; not releasing costs the app.
        //
        // **1000 is an infrastructure timeout, not a design duration — do not tokenise it.**
        // Every other number about movement in this app is a token in theme.css, so the reflex
        // on reading this line is to reach for `--enter`. That would be wrong twice over: this
        // is not how long anything takes to move, it is how long the app waits before deciding
        // a release is never coming, and it must stay comfortably longer than the animation it
        // backstops rather than equal to it. Tying it to a design token would mean shortening
        // the failsafe every time somebody made a transition quicker — and under
        // prefers-reduced-motion the tokens collapse to 0.01ms, which would arm a failsafe that
        // fires before the thing it protects has begun.
        guard = window.setTimeout(() => window.ppMotion.end(), 1000);
    },

    /// Let the second snapshot be taken. Safe to call when nothing is open.
    end: () => {
        if (guard) { window.clearTimeout(guard); guard = 0; }
        if (release) { release(); release = null; }
    },
};

// ─────────────────────────────────────────────────────────────────────────────
// Feedback on change — the spent figure counts to its new value rather than jumping.
//
// **It counts *to* the engine's answer and never invents one.** Both ends are figures
// CostCalculator returned; nothing here does arithmetic about a character beyond interpolating
// between two numbers it was handed, and the resting frame is *assigned* rather than computed,
// so what comes to rest is the engine's number exactly. CLAUDE.md states the rule.
//
// **The clock is a Web Animations object rather than a requestAnimationFrame loop, and the
// reason is testability rather than taste.** `--virtual-time-budget`, which every screenshot
// and every driven proof in this repository needs, suppresses frame production: rAF does not
// tick and the document timeline does not advance. Measured — a probe reports `RAF-FIRED-1`
// without the flag and never fires with it, identically under `--dump-dom`, `--screenshot`
// and `--run-all-compositor-stages-before-draw`. A WAAPI animation's `currentTime` is
// *settable*, so a test can seek it to a fixed moment and read the figure, which is the only
// way this is checkable by the instrument CI actually has.
//
// rAF still pumps the redraw in a real browser, because that is what a browser is for. What
// the animation object owns is the *clock*, and `draw()` is a pure function of it — so the
// path a test drives and the path a visitor sees compute the figure the same way.

// **A positive control.** Every counting check asserts an outcome, and an outcome is satisfied
// by a feature that never ran — twice now. This counts the counts, so a harness can assert the
// work happened before it asserts the work was right.
window.ppMotionStats = { counts: 0, transitions: 0, landings: 0 };

window.ppCount = (element, from, to) => {
    if (!element) return;

    // **Cancel first, before any early return.** An in-flight count has to be stopped whatever
    // this call decides to do next: taking the immediate path below while an older count keeps
    // pumping lets that older count write its own frames *after* this one has assigned the
    // answer, and the figure comes to rest on a number the engine no longer returns.
    if (element.ppCount) element.ppCount.cancel();

    // Reduced motion, or nothing to count: the answer, immediately.
    if (still() || from === to) {
        element.textContent = to;
        return;
    }

    // The duration comes from the stylesheet, so the app keeps one set of durations and this
    // moves with the rail beneath it — they are the same event. Unreadable means no animation
    // rather than a guessed one; the harness asserts this token resolves, because a script that
    // silently degrades to assigning the end state passes every test of the end state.
    //
    // **Read as milliseconds explicitly.** `parseFloat` on `0.26s` yields 0.26 and would make
    // the count last a quarter of a millisecond, with every driven check still green because
    // they are all expressed in terms of this same number.
    const raw = window.getComputedStyle(document.documentElement)
        .getPropertyValue("--enter").trim();
    const ms = raw.endsWith("ms") ? parseFloat(raw)
        : raw.endsWith("s") ? parseFloat(raw) * 1000
        : NaN;

    if (!(ms > 0)) { element.textContent = to; return; }

    // The clock. It animates nothing anybody can see — opacity from 1 to 1 — because what is
    // wanted is a timeline, not an effect. The figure is text, and text is not interpolable.
    const clock = element.animate(
        [{ opacity: 1 }, { opacity: 1 }],
        { duration: ms, fill: "forwards" });

    // The displayed figure, as a pure function of the clock. **Seeking `clock.currentTime` and
    // calling this is exactly what a visitor's frame does**, which is what makes the driven
    // test a test of the shipped path rather than of a reproduction.
    //
    // **A null `currentTime` means the clock is finished or cancelled, and it must read as the
    // end — not as the beginning.** `clock.currentTime ?? 0` made it read as `t = 0`, so a frame
    // still scheduled when the tidy-up ran wrote `from` over the answer and rescheduled itself
    // for ever. The figure came to rest on the *previous* Hero Point total with rAF spinning
    // behind it, which is worse than the leak that change was made to fix, and invisible to
    // every driven check because `--virtual-time-budget` produces no frames at all.
    const draw = () => {
        const now = clock.currentTime;

        if (now === null || now === undefined || Number(now) >= ms) {
            // Assigned, never interpolated: the resting figure is the engine's.
            element.textContent = to;
            return;
        }

        const t = Math.min(1, Math.max(0, Number(now) / ms));

        // Cubic ease out, matching --ease-out: quick away, settling into place.
        element.textContent = Math.round(from + ((to - from) * (1 - Math.pow(1 - t, 3))));
    };

    let handle = 0;

    // The record this count owns the element with. A pump frame that finds a different record —
    // superseded, or tidied away — is a leftover and must not draw: it would be writing an older
    // count's numbers over a newer one's.
    let record = null;

    // The pump draws the frames a visitor sees. It draws only: completion is the clock's own
    // business, below. Tying cleanup to the pump made it depend on frames being produced, and
    // frames are exactly what `--virtual-time-budget` suppresses.
    const pump = () => {
        if (element.ppCount !== record) return;

        draw();

        const now = clock.currentTime;
        if (now !== null && now !== undefined && Number(now) < ms) {
            handle = requestAnimationFrame(pump);
        }
    };

    // **Completion, from the animation's own promise.** `fill: "forwards"` keeps a finished
    // animation *relevant*, so it stays attached to the element — one per count, on the single
    // element in the budget strip, measured growing 1..10 over ten counts.
    //
    // `cancel()` rejects this promise, which is the interrupt path and wants no tidy-up of its
    // own — the interrupting call has already taken over.
    clock.finished
        .then(() => {
            draw();
            cancelAnimationFrame(handle);
            clock.cancel();
            element.ppCount = null;
        })
        .catch(() => { /* cancelled: the newer count owns this element now */ });

    record = {
        clock,
        draw,
        cancel: () => { cancelAnimationFrame(handle); clock.cancel(); element.ppCount = null; },
    };

    element.ppCount = record;


    window.ppMotionStats.counts++;
    handle = requestAnimationFrame(pump);
};

// ─────────────────────────────────────────────────────────────────────────────
// A row arriving in a chosen list should land, not appear.
//
// **Identity lives here rather than in the component, and that is the whole trick.** Blazor
// reuses DOM nodes across renders, so "which row is new" is not a question the render tree
// answers cheaply — but a row that has already landed can be marked, and a row without the mark
// has never been seen. `data-landed` is that mark. A key would work too, and would have to be
// threaded through the eight components that build these lists: `ChosenRow` carries no identity
// today. The mark needs nothing from any of them.
// `announce` is false on a component's first render, so restoring a saved character marks its
// twelve rows without playing twelve animations at once. Marking still happens — otherwise the
// next genuine addition would land the whole list.
window.ppLand = (list, announce) => {
    if (!list) return;

    // `:scope >` so a nested list cannot have its rows landed by the outer one. No call site
    // nests an <li> today; this is what the selector always meant.
    const arrived = list.querySelectorAll(":scope > li:not([data-landed])");

    for (const row of arrived) {
        row.setAttribute("data-landed", "");

        if (!announce || still()) continue;

        const styles = window.getComputedStyle(document.documentElement);
        const ms = parseFloat(styles.getPropertyValue("--enter"));
        const ease = styles.getPropertyValue("--ease-emphasised").trim();

        if (!(ms > 0) || !ease) continue;

        // The one place --ease-emphasised is used. It overshoots, which is what makes this read
        // as landing rather than as a repaint; the curve is the stylesheet's, not this file's.
        row.animate(
            [
                { opacity: 0, transform: "translateY(-6px)" },
                { opacity: 1, transform: "translateY(0)" },
            ],
            { duration: ms, easing: ease });

        window.ppMotionStats.landings++;
    }
};
