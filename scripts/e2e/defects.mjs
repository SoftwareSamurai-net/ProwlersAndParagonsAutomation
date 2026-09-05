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
// **Each defect is real rather than convenient.** Every one of the six site defects below is a
// fault this project could plausibly ship: a root element renamed, a storage wrapper that swallows
// a write, a preference that is read but never applied, a palette block whose selector stops
// matching, an `<html>` element with no `lang`, and a `<base href>` that is not `/`. The last is
// named in `docs/guide/hosting.md` as a way to break every asset fetch at once.
//
// ------------------------------------------------------------------------------------------------
// THERE ARE TWO KINDS OF DEFECT NOW, AND THE SECOND EXISTS BECAUSE A SITE DEFECT CANNOT BREAK A
// SERVER-SIDE RULE.
//
// **A signed-in check asks a question the published bundle does not answer.** `ADMIN` asks whether
// the accounts server refuses an account the invitation list does not make an administrator;
// `RULES` asks whether the rulebook is served to an account and refused to a stranger. Neither
// answer is in `index.html`, `js/*.js` or `css/*.css` — and the parts of the browser side that
// *do* decide anything about them are Blazor components compiled into a WebAssembly payload, where
// there is no line to substitute. A twin built the only way this file could build one before would
// be a second copy of the real site that passes for the wrong reason, which is the precise failure
// `buildTwin`'s "exactly one line" property exists to prevent.
//
// **So the other end of the harness moves instead: the row the sign-in was seeded from.** Stage
// two signs a reader in by writing a `login_tokens` row into the *local* D1 and driving
// `/signin?t=<raw token>` — which is what an email would have caused, and nothing else about the
// application is faked. A seed defect changes that row and only that row: the account the reader
// signs in as is one the list makes an administrator, or the token handed to them expired an hour
// ago, or the second browser context is signed in as somebody else entirely. Each is a state this
// deployment can really be in.
//
// **`scripts/e2e/seed.mjs` mints the rows; this file stays the one place a negative control is
// declared.** A seed defect names the slots it overrides and the accounts it points them at, and
// `seed.mjs` **throws if a name it does not mint appears here, and throws again if a twin's plan
// comes out identical to the real one** — the same two properties as `buildTwin`'s line
// substitution, against a database row rather than a published file. A twin that has quietly
// stopped reproducing its defect is a red run, not a green one.

import { cpSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

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

    // --------------------------------------------------------------------------------------------
    // The seed defects. Same site, different row — see this file's header for why a signed-in
    // check cannot be twinned by substituting a line in the published bundle.

    {
        name: 'reader-is-an-administrator',
        check: 'ADMIN',
        why: 'The account the reader signs in as is one the invitation list marks '
            + '`grants_admin = 1`, so /admin serves them the list instead of refusing them. This '
            + 'is the twin that stops ADMIN being vacuous: a check that asserts "not allowed" '
            + 'passes against a page that failed to load at all, and against one that refused '
            + 'because nobody was signed in. Only an account that *is* an administrator '
            + 'distinguishes the refusal being tested from every other way the page can end up '
            + 'saying nothing.',
        seed: { slots: { ADMIN: { account: 'adminTwin' } } },
    },
    {
        name: 'rules-token-expired',
        check: 'RULES',
        why: 'The token seeded for the rulebook reader expired an hour before the run, so '
            + '`db.spendLoginToken` refuses it — its `expires_at > ?` test is in the UPDATE '
            + 'itself — and the reader arrives at /rules anonymous. The check must then go red '
            + 'on its positive control rather than reporting the book as readable, which is what '
            + 'it would do if it were finding prose that is served to everybody.',
        seed: { slots: { RULES: { expired: true } } },
    },
    {
        name: 'second-context-is-another-account',
        check: 'ACCOUNT_SAVE',
        why: 'The second browser context is signed in with a token minted for a different '
            + 'invited account, so the character built in the first context is not that '
            + "account's. The check has to notice: a character that followed the *browser* would "
            + 'still be absent from a fresh context, and one the server handed to whoever asked '
            + 'would still be present — so this is the twin that proves the check reads the '
            + 'account rather than either.',
        seed: { slots: { SAVE_2: { account: 'saveOther' } } },
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

/**
 * Which of the two kinds of twin this is: `site` for a substituted line in the published output,
 * `seed` for a substituted row in the local D1.
 *
 * **A defect must be exactly one of them and this is where that is enforced**, because the two are
 * driven completely differently — a site twin gets its own copied directory and its own server, a
 * seed twin gets neither and only a different set of tokens. A defect declaring both, or neither,
 * would be silently skipped by whichever arm of `scripts/e2e.sh` looked at it first, which is a
 * negative control that quietly stopped existing.
 */
export function kindOf(defect) {
    const site = defect.file !== undefined;
    const seed = defect.seed !== undefined;

    if (site === seed) {
        throw new Error(
            `twin '${defect.name}' declares ${site ? 'both a file and a seed' : 'neither a file '
            + 'nor a seed'}. A defect is a substituted line in the published site OR a substituted `
            + `row in the local D1, never both and never neither — see this file's header.`);
    }

    return site ? 'site' : 'seed';
}

// ---------------------------------------------------------------------------------------------
// Called by scripts/e2e.sh.
//
// **Guarded, because `scripts/e2e/seed.mjs` imports `DEFECTS` from here.** Without the guard this
// block runs on import with *seed.mjs's* arguments, matches nothing, and exits 2 — a module that
// kills whoever imports it.

const invokedDirectly = process.argv[1] !== undefined
    && import.meta.url === pathToFileURL(process.argv[1]).href;

const [command, ...rest] = invokedDirectly ? process.argv.slice(2) : ['--imported'];

if (command === '--imported') {
    // Nothing. Imported for DEFECTS and kindOf.
} else if (command === '--list') {
    // `name:check:kind` per line, which is all the shell needs: it drives each twin, requires that
    // check to be red in it, and needs the kind to know whether to build a site or re-seed.
    for (const defect of DEFECTS) {
        console.log(`${defect.name}:${defect.check}:${kindOf(defect)}`);
    }
} else if (command === '--build') {
    const [siteDir, intoDir, name] = rest;
    const defect = DEFECTS.find(d => d.name === name);

    if (!defect) {
        console.error(`no such twin: ${name}. Known: ${DEFECTS.map(d => d.name).join(', ')}`);
        process.exit(2);
    }

    if (kindOf(defect) !== 'site') {
        console.error(
            `twin '${name}' is a seed defect: there is no site to build for it. It is driven `
            + `against the real site with a different set of seeded tokens — see scripts/e2e.sh.`);
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
