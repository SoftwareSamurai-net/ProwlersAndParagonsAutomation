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
//
// ------------------------------------------------------------------------------------------------
// AND A SECOND KIND OF DEFECT, BECAUSE A SIGNED-IN CHECK CANNOT HAVE THE FIRST.
//
// **The published site is a Blazor WebAssembly bundle: the application's own logic is inside
// `_framework/*.wasm`, and there is no line in it to substitute.** Everything the list above
// breaks is one of the four hand-written files beside that bundle — `index.html`, a `js/*.js`, a
// `css/*.css`. A check that a signed-in reader can read the book, or that an ordinary account is
// refused the admin page, is answered by `worker/` on the server, and `wrangler pages dev` bundles
// `functions/` from the working directory: the twin and the real site are served by the *same*
// server code, so a defect in the site cannot reach it.
//
// **So a signed-in check's negative control is a defect in what is seeded, and `SEED_DEFECTS` is
// where it is declared.** The property that matters is kept rather than the mechanism: this file
// is still the one place a negative control is written down, `e2e.sh` still requires every driven
// check to appear here, and a twin is still a whole second run of the byte-identical harness. What
// differs is that the second run's database says something else — an expired token, or an account
// the deployment considers its owner — rather than the second run's `index.html`.
//
// **What that costs, said plainly:** a seed twin does not prove the check would notice a broken
// *page*, because it does not break one. `RULES` is twinned on its positive control — no session,
// so nothing to read — and `ADMIN` on its outcome, which is the stronger of the two and is why the
// admin refusal is worth more than the rulebook check. Neither is offered as more than that.

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
        name: 'reduced-motion-ignored',
        check: 'MOTION',
        why: 'js/motion.js stops asking whether the reader wanted less movement, so every entry '
            + 'point animates for everybody. This is the mutation CLAUDE.md names as the one two '
            + 'existing guards survive: they assert the file *mentions* still(), and a still() '
            + 'that answers false mentions it perfectly. The counters still move — the script '
            + 'runs, and runs wrong — so MOTION goes red on its outcome and not its control.',
        file: 'js/motion.js',
        find: 'const still = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;',
        replace: 'const still = () => false; // pp:e2e twin defect — the preference is ignored',
    },
    {
        name: 'palette-chord-does-nothing',
        check: 'COMMAND',
        why: 'js/palette.js still hears Ctrl-K and still counts the press — so the listener, the '
            + 'chord and the positive control are all intact — and the call back into the '
            + 'component is gone, so nothing opens. The failure a reader sees is a keyboard '
            + 'shortcut that does nothing at all, and every proof page in this repository would '
            + 'still pass: they assert the counter, which is exactly what this leaves working.',
        file: 'js/palette.js',
        find: '            owner.invokeMethodAsync("Toggle");',
        replace: '            /* pp:e2e twin defect — the chord is heard and dropped */',
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
 * The negative controls for the checks that need a signed-in reader.
 *
 * **Each of these is a row, not a file** — see this file's header for why a signed-in check cannot
 * have a file defect. `scripts/e2e/seed.mjs` reads whichever one it is named and writes the world
 * accordingly; the site served is the real one, byte for byte, and so is the server.
 *
 * **Both are faults this deployment could really have.** A clock skew or a slow mail queue makes
 * every link arrive expired; an `ADMIN_EMAIL` pointed at the wrong address, or a `grants_admin`
 * left set on an invitation, makes an ordinary account an administrator. The second is the one
 * worth having: it turns `ADMIN`'s *outcome* red rather than its control, so that check is watched
 * to fail for the reason it exists.
 */
export const SEED_DEFECTS = [
    {
        name: 'signin-token-already-expired',
        check: 'RULES',
        why: 'The seeded sign-in token is minted already expired, so `db.spendLoginToken`\'s '
            + '`expires_at > ?` refuses it and the browser never gets a session. RULES then has '
            + 'no account to read the book with, and says so as a control failure: the work did '
            + 'not happen. This is what proves the check is not reading prose that was in the '
            + 'published site all along — anonymous, `/rules` is a sign-in prompt.',
        lifetimeMs: -1000,
    },
    {
        name: 'seeded-account-is-the-owner',
        check: 'ADMIN',
        why: 'The seeded invitation carries `grants_admin = 1`, so `invitations.isAdministrator` '
            + 'says yes and the admin page lets the driver in. ADMIN exists to prove an ordinary '
            + 'account is refused, and this is that refusal removed — the check goes red on its '
            + 'outcome rather than on its control, which is the shape a negative control should '
            + 'have wherever it can be got.',
        grantsAdmin: 1,
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
//
// **Guarded, because this file is now imported as well as run.** `scripts/e2e/seed.mjs` imports
// `SEED_DEFECTS` from here so that a negative control is declared in exactly one place — and
// without this guard the import executed the block below, which found no command, printed the
// usage line and exited 2. The seed then failed with a message about `defects.mjs --list`, from a
// script that had asked it for nothing.

if (isBeingRun()) run();

/** Whether this file was executed rather than imported. */
function isBeingRun() {
    const entry = process.argv[1];

    return !!entry && pathToFileURL(entry).href === import.meta.url;
}

function run() {
    const [command, ...rest] = process.argv.slice(2);

    if (command === '--list') {
        // `name:check:kind` per line, which is all the shell needs: it drives each twin and
        // requires that check to be red in it.
        //
        // **The kind is the third field rather than a second command**, so the shell has one list
        // to iterate and one loop to keep in step. A `file` twin is a copy of the published site
        // with a line changed; a `seed` twin is the real site served against a differently-seeded
        // database.
        for (const defect of DEFECTS) console.log(`${defect.name}:${defect.check}:file`);
        for (const defect of SEED_DEFECTS) console.log(`${defect.name}:${defect.check}:seed`);
    } else if (command === '--build') {
        const [siteDir, intoDir, name] = rest;
        const defect = DEFECTS.find(d => d.name === name);

        if (!defect) {
            // Named apart rather than lumped into "no such twin", because the two are different
            // mistakes: a seed twin has no directory to build and asking for one means the shell's
            // loop has lost track of which kind it is holding.
            const seed = SEED_DEFECTS.find(d => d.name === name);

            console.error(seed
                ? `'${name}' is a seed twin, not a file twin: there is no site to build. `
                  + `scripts/e2e.sh serves the real site and seeds the database with `
                  + `--defect ${name} instead.`
                : `no such twin: ${name}. Known: ${DEFECTS.map(d => d.name).join(', ')}`);
            process.exit(2);
        }

        buildTwin(siteDir, intoDir, defect);
        console.log(`${defect.name}: ${defect.file} — ${defect.check} must fail against this.`);
    } else if (command === '--why') {
        const defect = [...DEFECTS, ...SEED_DEFECTS].find(d => d.name === rest[0]);
        console.log(defect ? defect.why : `no such twin: ${rest[0]}`);
    } else {
        console.error('usage: defects.mjs --list | --build <site> <into> <name> | --why <name>');
        process.exit(2);
    }
}
