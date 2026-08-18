// The command palette's one job that a component cannot do: hear a key that was pressed
// somewhere else.
//
// Blazor can only handle a key event on an element it rendered and that has focus. Ctrl-K
// has to work while the reader is in a rank stepper, a gear box, or nothing at all — which
// is a listener on the document, and a document is not in the render tree. Everything else
// about the palette is in the component: what it offers, what it looks like, what the arrow
// keys do once focus is inside it. This file is the doorbell and nothing more.
//
// **The whole of it is one listener and two focus calls.** Resist growing it. The reason
// motion.js is 3.5 KB and not a library is the same reason this is not a keyboard manager.

// **A positive control.** Every check in this repository that asserts an outcome can be
// satisfied by a feature that never ran — that has happened four times and is written up in
// CLAUDE.md. A harness asserts this counter moved before it asserts anything about what the
// key did.
window.ppPaletteStats = { presses: 0, listeners: 0 };

// The element focus came from, so Escape can put it back. A palette that swallows focus is a
// keyboard trap, which is the one thing a keyboard affordance must not be.
let cameFrom = null;

// Registered once. The layout that calls this is created once per session, but a component
// that is disposed and rebuilt would otherwise stack listeners and toggle the palette twice
// per press — which reads as the key not working at all.
let listening = false;

window.ppPalette = {
    /// Start listening for the opening chord. `owner` is the component, reached back into
    /// through Blazor's own interop object.
    listen: (owner) => {
        if (listening || !owner) return;
        listening = true;
        window.ppPaletteStats.listeners++;

        document.addEventListener("keydown", (e) => {
            // Ctrl on Windows and Linux, Command on a Mac. `metaKey` alone is not enough on
            // Windows, where it is the Windows key and belongs to the desktop.
            if (e.key !== "k" && e.key !== "K") return;
            if (!e.ctrlKey && !e.metaKey) return;

            // Chrome focuses its address bar on Ctrl-K and Firefox its search box. Both are
            // reasonable defaults and both make this affordance unreachable, so the page
            // takes the key. This is the only key this file claims.
            e.preventDefault();

            window.ppPaletteStats.presses++;
            owner.invokeMethodAsync("Toggle");
        });
    },

    /// Remember where focus was, then move it into the palette's own box.
    ///
    /// Called after the palette has rendered, because focus cannot move to an element that
    /// is not in the document yet.
    enter: (box) => {
        if (!box) return;
        cameFrom = document.activeElement;
        box.focus();
    },

    /// Put focus back where it came from. Called on close, however the palette was closed.
    ///
    /// **Not `document.body.focus()` as a fallback.** Focusing the body is indistinguishable
    /// from focus being lost, and a reader who opened the palette from a rank stepper and
    /// pressed Escape would be returned to the top of the document — which is worse than
    /// where they started, not merely different.
    leave: () => {
        const back = cameFrom;
        cameFrom = null;
        if (back && typeof back.focus === "function" && back.isConnected) back.focus();
    },
};
