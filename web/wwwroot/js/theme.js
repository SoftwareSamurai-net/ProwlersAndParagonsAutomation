// Light or dark, stamped on the document element before anything is painted.
//
// **This is a separate file from download.js, and it is loaded from <head> rather than from
// the foot of <body>, for one reason: the app takes seconds to boot.** The WebAssembly payload
// is the largest open item on this project, and until it has started there is a boot screen on
// screen. Read the preference in C# and somebody who has asked for dark watches a bright white
// panel for as long as the download takes, then sees it flip. A render-blocking script in the
// head is the whole of the fix, and it is ~40 lines.
//
// **It is a file rather than an inline block on purpose.** An inline script would need its hash
// added to the Content-Security-Policy that scripts/write-cloudflare-headers.sh generates —
// which is a mechanism that exists, for the import map Blazor writes — but the policy is
// `script-src 'self'`, so a same-origin file needs no policy change at all and cannot rot when
// the file is edited. Never `'unsafe-inline'`; see the note in that script.
//
// **The attribute is removed rather than set to "system".** Three theme states exist and only
// two of them are an attribute: an explicit choice stamps `data-theme`, and the default stamps
// nothing at all, leaving `prefers-color-scheme` to separate light from dark. A stylesheet
// cannot ask "is this attribute absent" except through `:not()`, which is exactly how the dark
// blocks in theme.css are written — so an attribute reading "system" would match neither the
// light path nor the dark one and would be a third palette nobody designed.
(() => {
    const KEY = "pp.theme.v1";

    // Wrapped for the same reason ppStore is: a browser can refuse local storage — private
    // mode, a quota, a user who turned it off — and a theme preference is not worth an
    // exception that stops the app booting. No preference is a perfectly good answer.
    const read = () => {
        try { return localStorage.getItem(KEY); } catch { return null; }
    };

    const apply = (choice) => {
        if (choice === "light" || choice === "dark") {
            document.documentElement.setAttribute("data-theme", choice);
        } else {
            document.documentElement.removeAttribute("data-theme");
        }
    };

    // **A positive control.** Every check in this repository that asserts an outcome can be
    // satisfied by a feature that never ran; a harness asserts this counter moved before it
    // asserts anything about what the attribute says.
    window.ppThemeStats = { stamps: 0 };

    window.ppTheme = {
        /// What has been chosen, as the C# side names it. "system" when nothing has been.
        current: () => read() ?? "system",

        /// Record a choice and apply it. Storing nothing is how "system" is stored, so that a
        /// reader who returns to the default is indistinguishable from one who never chose —
        /// which is what the default means.
        set: (choice) => {
            try {
                if (choice === "light" || choice === "dark") localStorage.setItem(KEY, choice);
                else localStorage.removeItem(KEY);
            } catch { /* refused or full; the attribute below still applies for this visit */ }

            apply(choice);
            window.ppThemeStats.stamps++;
        },
    };

    // Before the first paint, and before Blazor exists.
    apply(read());
    window.ppThemeStats.stamps++;
})();
