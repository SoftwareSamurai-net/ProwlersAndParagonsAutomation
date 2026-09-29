// Hands a string to the browser as a file download. This is the whole of the JavaScript
// in the app: everything else — the rules, the costing, the validation — is the C# engine
// running in WebAssembly.
window.ppDownload = (fileName, mimeType, contents) => {
    const blob = new Blob([contents], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

// The theme is a data-mode attribute on <html>, so every CSS custom property switches at
// once and no component ever needs to know which palette is active.
window.ppSetMode = (mode) => {
    document.documentElement.setAttribute("data-mode", mode);
};

// The name a printed page carries in its top margin, as a custom property on <html>.
//
// **A page in the middle of a printed sheet is otherwise anonymous.** The name is on page one
// and in a colophon on the last; every page between them used to rely on the browser's own
// print header, which the person printing can turn off — and is told to, because that header
// is also where the web address comes from. `@page { @top-center { content: var(--sheet-name)
// … } }` in app.css is what repeats the name on every page, and Chrome has honoured margin
// boxes since 131 (measured on 153: a filled box prints on every page, whether or not the
// browser's own header is on, and stands in for it). The property has to be on the root
// element, because the page context inherits from nothing else — a `<style>` in a component
// would do, and a test forbids one; this is the only other way in.
//
// **The value is a CSS string, escaped here rather than trusted.** A character is named by
// whoever built it, and `content:` takes a quoted string: an unescaped quote or backslash
// makes the declaration invalid and the box vanishes, silently. Line breaks are not a thing a
// margin box can print, so they become spaces.
//
// **Null removes the property rather than writing an empty name.** An unset `var()` makes the
// declaration invalid at computed-value time, the box is not generated, and Chrome prints its
// own header there instead — which is exactly the right thing on a page with no sheet on it.
// An empty string would print " · page 1 of 1" over every page of the roster.
window.ppSheetHeadStats = { sets: 0, clears: 0 };

window.ppSetSheetName = (name) => {
    const root = document.documentElement.style;

    if (name === null || name === undefined) {
        root.removeProperty("--sheet-name");
        window.ppSheetHeadStats.clears++;
        return;
    }

    const escaped = String(name)
        .replace(/[\\"]/g, (c) => "\\" + c)
        .replace(/[\r\n\f]+/g, " ");

    root.setProperty("--sheet-name", `"${escaped}"`);
    window.ppSheetHeadStats.sets++;
};

// Local storage, wrapped so a browser that refuses it — private mode, a storage quota, a
// user who has turned it off — is a "no character saved" rather than an exception that
// stops the app booting. The C# side treats every failure the same way.
window.ppStore = {
    save: (key, value) => { try { localStorage.setItem(key, value); } catch { /* full or blocked */ } },
    load: (key) => { try { return localStorage.getItem(key); } catch { return null; } },
    clear: (key) => { try { localStorage.removeItem(key); } catch { /* nothing to do */ } }
};
