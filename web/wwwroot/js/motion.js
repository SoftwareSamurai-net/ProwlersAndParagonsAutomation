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

        // **The safety net, and it is not optional.** While a transition is open the live DOM
        // is hidden behind a snapshot overlay. If `end()` never arrives — a navigation that
        // throws, a handler that is disposed mid-flight — the page is left showing a still
        // image of itself with no way back. Releasing on a timer costs a transition that does
        // not animate; not releasing costs the app.
        guard = window.setTimeout(() => window.ppMotion.end(), 1000);
    },

    /// Let the second snapshot be taken. Safe to call when nothing is open.
    end: () => {
        if (guard) { window.clearTimeout(guard); guard = 0; }
        if (release) { release(); release = null; }
    },
};
