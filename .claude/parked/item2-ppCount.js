
// ─────────────────────────────────────────────────────────────────────────────
// Feedback on change — the spent figure counts to its new value rather than jumping.
//
// **It counts *to* the engine's answer and never predicts one.** Both ends are figures
// CostCalculator returned; nothing here does arithmetic about a character beyond interpolating
// between two numbers it was handed, and the last frame is assigned rather than computed, so
// what comes to rest on screen is the engine's number exactly and not a rounding of it.
window.ppCount = (element, from, to) => {
    if (!element) return;

    // Reduced motion, or nothing to count: show the answer and stop.
    if (still() || from === to) {
        element.textContent = to;
        return;
    }

    // The duration comes from the stylesheet rather than from here, so the app keeps one set of
    // durations and this moves with the rail beneath it — they are the same event.
    const ms = parseFloat(
        window.getComputedStyle(document.documentElement).getPropertyValue("--enter"));

    if (!(ms > 0)) { element.textContent = to; return; }

    // A change arriving mid-count: drop the one in flight and start from here, or the two run
    // together and the figure jitters between them.
    if (element.ppCount) element.ppCount.cancel();

    let handle = 0;
    const started = performance.now();

    const step = (now) => {
        const t = Math.min(1, (now - started) / ms);

        // Cubic ease out, to match --ease-out: quick away, settling into place.
        const eased = 1 - Math.pow(1 - t, 3);

        if (t < 1) {
            element.textContent = Math.round(from + ((to - from) * eased));
            handle = requestAnimationFrame(step);
        } else {
            // The resting frame is the engine's number, assigned rather than interpolated.
            element.textContent = to;
            element.ppCount = null;
        }
    };

    element.ppCount = { cancel: () => cancelAnimationFrame(handle) };
    handle = requestAnimationFrame(step);
};
