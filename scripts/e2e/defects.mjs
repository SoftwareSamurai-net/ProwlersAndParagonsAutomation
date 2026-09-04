// The deliberately-broken twins of the published site.
//
// **A denylist of spellings cannot make a verdict honest — CLAUDE.md's own section says so, and
// says it because a denylist here failed.** `ProofPages.MustNotShow` banned the literal
// `say(true` in a browser harness so a verdict could not be hard-coded; `|| true` is textually
// distinct, walked straight through it, and made that harness report PASS against a strip which
// had moved the full 483px it was supposed to have stayed pinned against. The spelling space is
// unbounded. So each check in scripts/e2e/drive.mjs gets a **negative control that is a whole
// second site**: the same published output with one documented line changed, driven by the
// byte-identical harness, and required to say FAIL.
//
// **Two properties are load-bearing and both come from the same place.**
//
// - **The substitution throws if its target has moved.** `apply(read())` moving in `theme.js`, or
//   the villain palette's selector being rewritten, must not quietly turn a twin into a second
//   copy of the real site that passes for the wrong reason. `WithDefect` in
//   `tests/ProwlersAndParagons.Web.Tests/ProofPages.cs` has exactly this shape for exactly this
//   reason; this is the same idea against a published directory rather than a single file.
// - **A twin must be broken in the *site*, never in the harness.** The driver is one file and
//   both runs execute it unchanged. A twin with a doctored script proves nothing at all.
//
// **Each defect is real rather than convenient.** Every one of the five below is a fault this
// project could plausibly ship: a root element renamed, a storage wrapper that swallows a write,
// a preference that is read but never applied, a palette block whose selector stops matching, and
// a `<base href>` that is not `/`. The last is named in `docs/guide/hosting.md` as a way to break
// every asset fetch at once.

import { cpSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * One defect: which check it must turn red, which published file it lives in, and the exact one
 * line it substitutes.
 *
 * **`find` is a whole line, compared exactly — indentation included — and it must match exactly
 * one line.** Two things fall out of that and both were arrived at by watching a substring match
 * fail:
 *
 * - **Line endings stop mattering.** These files are checked out CRLF on Windows and LF on the CI
 *   runner, so a `find` containing `\n` matches on one platform and silently nothing on the
 *   other. A comparison per line has no newline in it to get wrong.
 * - **Indentation is a discriminator rather than noise.** `css/theme.css` holds
 *   `:root[data-mode="villain"] {` four times — once at the top level, which is the light
 *   villain palette, and three times indented inside media queries. A substring search found
 *   three and refused, correctly; the unindented one is unique as a line.
 */
export const DEFECTS = [
    {
        name: 'boot-app-never-mounts',
        check: 'BOOT',
        why: 'The element Blazor is told to render into is renamed, so the framework starts, '
            + 'fetches its whole payload, and then has nowhere to put the application. The boot '
            + 'screen stays on screen for ever. This is the failure a proof page cannot see at '
            + 'all: markup and CSS are perfect and the running app is a loading message.',
        file: 'index.html',
        find: '    <div id="app">',
        replace: '    <div id="app-not-mounted">',
    },
    {
        name: 'store-writes-nothing',
        check: 'BUILD',
        why: 'ppStore.save becomes a no-op that reports success. This is the defect class '
            + 'PROGRESS.md item 10 was sharpened by — a feature reading a store nothing writes '
            + 'to — and every unit and component test in this repository would pass against it, '
            + 'because they all call the store directly.',
        file: 'js/download.js',
        find: '    save: (key, value) => { try { localStorage.setItem(key, value); } catch { /* full or blocked */ } },',
        replace: '    save: (key, value) => { /* pp:e2e twin defect — accepts the write and drops it */ },',
    },
    {
        name: 'theme-not-restored',
        check: 'THEME',
        why: 'js/theme.js still runs, still stamps, and still increments its own counter — so the '
            + 'positive control passes — but it stamps the default rather than what was stored. '
            + 'A reader who chose dark gets a light screen on every return visit. Exactly the '
            + 'shape that makes a positive control worth having: the feature ran and was wrong.',
        file: 'js/theme.js',
        find: '    apply(read());',
        replace: '    apply(null); // pp:e2e twin defect — ignores the stored preference',
    },
    {
        name: 'villain-palette-missing',
        check: 'PALETTE',
        why: "The villain palette's selector stops matching, so a Villain in light mode is drawn "
            + 'in the Hero palette. Four palettes silently become three. The pixel goldens would '
            + 'catch it on a proof page; nothing checked that the two switches in the settings '
            + 'menu actually get a reader to all four.',
        file: 'css/theme.css',
        find: ':root[data-mode="villain"] {',
        replace: ':root[data-mode="villain-not-a-mode"] { /* pp:e2e twin defect */',
    },
    {
        name: 'html-lang-dropped',
        check: 'A11Y',
        why: 'The <html> element loses its lang attribute, so a screen reader cannot tell which '
            + "language to pronounce the page in. It is axe's html-has-lang rule, tagged wcag2a, "
            + 'and it is live in the A11Y check because only color-contrast is disabled there. '
            + 'Structural, and therefore invisible to every other check here: the pixel goldens '
            + 'compare a picture, and no bUnit test asks what a screen reader would be told.',
        file: 'index.html',
        find: '<html lang="en" data-mode="hero">',
        replace: '<html data-mode="hero"> <!-- pp:e2e twin defect - the lang attribute is gone -->',
    },
    {
        name: 'base-href-dropped',
        check: 'ROUTES',
        why: 'Without <base href="/"> every relative fetch resolves against the current path, so '
            + 'the front door works perfectly and every deep link fails to load the framework at '
            + 'all. docs/guide/hosting.md names this one: getting the base wrong breaks every '
            + 'asset fetch at once, and it does it only on the addresses nobody tests by hand.',
        file: 'index.html',
        find: '    <base href="/" />',
        replace: '    <!-- pp:e2e twin defect — the base element is gone -->',
    },
];

/**
 * Write a twin of `siteDir` into `intoDir` with one defect applied.
 *
 * The whole published directory is copied rather than symlinked or overlaid: `wrangler pages dev`
 * serves a directory, and a twin that shared bytes with the real site would be one edit away from
 * breaking the run it is the control for.
 */
export function buildTwin(siteDir, intoDir, defect) {
    rmSync(intoDir, { recursive: true, force: true });
    cpSync(siteDir, intoDir, { recursive: true });

    const path = join(intoDir, defect.file);
    const original = readFileSync(path, 'utf8');

    // Rejoined with whatever the file already used, so a twin differs from the real site by one
    // line and not by every line ending in it.
    const newline = original.includes('\r\n') ? '\r\n' : '\n';
    const lines = original.split(/\r?\n/);

    const at = lines.reduce((found, line, index) =>
        line === defect.find ? [...found, index] : found, []);

    if (at.length !== 1) {
        throw new Error(
            `twin '${defect.name}' expected its documented line to match exactly one line of `
            + `${defect.file} and it matched ${at.length}`
            + `${at.length > 1 ? ` (lines ${at.map(i => i + 1).join(', ')})` : ''}. The shipped `
            + `file has moved, so this twin would either reproduce nothing — and pass for the `
            + `wrong reason — or change more than the one line it documents. Re-read `
            + `${defect.file} and update the entry in scripts/e2e/defects.mjs.\n`
            + `  looking for, exactly: ${JSON.stringify(defect.find)}`);
    }

    lines[at[0]] = defect.replace;
    writeFileSync(path, lines.join(newline));
}

// ---------------------------------------------------------------------------------------------
// Called by scripts/e2e.sh.

const [command, ...rest] = process.argv.slice(2);

if (command === '--list') {
    // `name:check` per line, which is all the shell needs: it drives each twin and requires that
    // check to be red in it.
    for (const defect of DEFECTS) console.log(`${defect.name}:${defect.check}`);
} else if (command === '--build') {
    const [siteDir, intoDir, name] = rest;
    const defect = DEFECTS.find(d => d.name === name);

    if (!defect) {
        console.error(`no such twin: ${name}. Known: ${DEFECTS.map(d => d.name).join(', ')}`);
        process.exit(2);
    }

    buildTwin(siteDir, intoDir, defect);
    console.log(`${defect.name}: ${defect.file} — ${defect.check} must fail against this.`);
} else if (command === '--why') {
    const defect = DEFECTS.find(d => d.name === rest[0]);
    console.log(defect ? defect.why : `no such twin: ${rest[0]}`);
} else {
    console.error('usage: defects.mjs --list | --build <site> <into> <name> | --why <name>');
    process.exit(2);
}
