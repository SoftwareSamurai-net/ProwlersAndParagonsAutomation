// Home and End on a rank's role="slider" also scroll the document, and Blazor cannot suppress
// that selectively: it fixes preventDefault at render time rather than per key, so suppressing
// it on the whole element would swallow Tab along with Home and End and trap focus inside a
// rank row. This is the narrow fix — one native listener, two keys, nothing else touched.
//
// **A positive control.** Every check in this repository that asserts an outcome can be
// satisfied by a feature that never ran — that has happened four times and is written up in
// CLAUDE.md. A harness asserts these counters moved before it asserts anything about what a
// key did.
window.ppSliderStats = { listeners: 0, suppressed: 0 };

window.ppSlider = {
    /// Attach the guard to one slider element. Idempotent: a component calls this on every
    /// first render of every row, and a second call on the same element must not stack a
    /// second listener — which would suppress the key twice for one press, harmless here but
    /// a sign the guard is not doing what it claims.
    guard: (el) => {
        if (!el || el.__ppSliderGuarded) return;
        el.__ppSliderGuarded = true;
        window.ppSliderStats.listeners++;

        el.addEventListener("keydown", (e) => {
            // Only these two. Left, Right, Up, Down and Tab are untouched — Tab has to keep
            // moving focus, and the arrows have nothing to scroll: this app is held to no
            // horizontal overflow by a harness in CI.
            if (e.key !== "Home" && e.key !== "End") return;

            e.preventDefault();
            window.ppSliderStats.suppressed++;
        });
    },
};
