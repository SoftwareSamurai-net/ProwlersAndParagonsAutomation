// The end-to-end checks: real Chrome, against the published site as `wrangler pages dev` serves
// it. Run by scripts/e2e.sh, which starts the server and then runs this file against it.
//
//     node scripts/e2e/drive.mjs http://127.0.0.1:8788
//
// **Read `PROGRESS.md` item 10 before changing anything here.** The argument that bought this
// harness is not "the app is untested" — three suites and nineteen browser verdict harnesses say
// otherwise. It is that every one of them reaches the machinery by hand, and *a test that reaches
// a feature by hand cannot notice that nothing else reaches it*: a whole feature shipped here
// while nothing in the application ever wrote to the store it read from, and every unit and
// component test passed honestly the whole time. So the rules below are not stylistic.
//
// **Nothing in this file may reach past the browser.** No `localStorage.setItem` to arrange a
// state, no calling a component's method, no planting a storage pointer to reach a branch. Every
// state a check needs is arrived at by clicking what a person clicks, and the only reads that go
// round the front are the ones *asserting* on storage after the app has written it. The moment a
// check sets up its own world it stops answering the one question this harness exists to answer.
//
// **Every check carries a positive control, and it comes first.** CLAUDE.md records four separate
// occasions where a feature that never ran was mistaken for a feature that worked — three of this
// repository's four historical guard faults are that one shape. So each check below asserts that
// the work *happened* (an interop counter moved, the framework started, the document did not
// reload, the stylesheet painted something) before it asserts that the outcome was right, and a
// failed control is reported as its own sentence rather than folded into the outcome.
//
// **And the negative control is a whole second site.** scripts/e2e/defects.mjs builds a twin of
// the published site with one documented defect in it, and scripts/e2e.sh requires the named
// check to say FAIL there while this same byte-identical file drives it. A denylist of forbidden
// spellings cannot make a verdict honest — `|| true` walked through one in this repository and
// reported PASS against a strip that had moved 483px — so the twin proves behaviour instead.

import { launch, sleep } from './cdp.mjs';

const base = (process.argv[2] ?? '').replace(/\/$/, '');

if (!base) {
    console.error('usage: node scripts/e2e/drive.mjs <base-url> [--only CHECK[,CHECK...]]');
    process.exit(2);
}

/**
 * The checks to run, if the caller named some. Empty means all of them.
 *
 * **One caller and one reason: a twin needs one check, not five.** scripts/e2e.sh drives a
 * deliberately-broken twin per check and reads exactly one verdict out of each run — the check
 * that twin must turn red. The other four verdicts are read by nothing, and each one is a server,
 * a boot and a set of waits. Six twins times four unread checks is most of a CI job.
 *
 * **It cannot make a run quietly smaller.** e2e.sh drives the *real* site with no filter, reads
 * the check names out of that run, and requires each to have a twin; and a name that matches
 * nothing here leaves the twin's verdict line absent, which e2e.sh already treats as a failed
 * negative control rather than as a pass.
 */
const only = new Set();

for (let i = 3; i < process.argv.length; i++) {
    if (process.argv[i] !== '--only') continue;

    if (!process.argv[i + 1]) {
        console.error('--only needs a comma-separated list of check names');
        process.exit(2);
    }

    for (const name of process.argv[i + 1].split(',')) if (name.trim()) only.add(name.trim());
}

/**
 * The character this harness builds, and the tier it builds it at.
 *
 * **Not the default tier.** A check that picks whatever was already selected cannot tell a click
 * that landed from a click that did nothing, which is the same vacuity as a control that never
 * fires. `High Level` is 150 Hero Points against `Standard`'s 125, so the budget strip moves too.
 */
const NAME = 'Harness Test Hero';
const TIER = 'High Level';
const TIER_POINTS = '150';

/** The local-storage keys the application writes. Read here, never written. */
const INDEX_KEY = 'pp.character.v1.index';
const THEME_KEY = 'pp.theme.v1';

/**
 * Installed into every document before anything else runs.
 *
 * Two recordings that cannot be taken after the fact: whether the app's own boot screen was in
 * the document that was served (by the time a check can look, Blazor has replaced it), and a
 * per-document token the routes check reads back to tell a client-side navigation from a reload.
 */
const RECORDER = `
    window.__ppE2E = { sawBootScreen: null, document: Math.random().toString(36).slice(2) };
    document.addEventListener('DOMContentLoaded', () => {
        window.__ppE2E.sawBootScreen = !!document.querySelector('.boot');
    });
`;

// ---------------------------------------------------------------------------------------------
// The little that is shared between checks.

/** Thrown by a control. Reported separately from an ordinary failure — see `run` below. */
class ControlFailed extends Error {}

/**
 * A message fit to print as a verdict: no raw sign-in token in it, and one line.
 *
 * **The redaction is the same rule `scripts/e2e/process.sh`'s `redacted_tail` obeys, one layer
 * out.** Stage two navigates to `/signin?t=<raw token>`, so an error naming the address it could
 * not reach carries a bearer secret — and CI run 34040527190 duly printed three of them into a
 * public log, because only the *shell* half of this harness had ever thought about it. Same
 * pattern: `[?&]t=` and not a bare `t=`, and a base64url value.
 *
 * **One line, because `scripts/e2e.sh` reads these with `grep ^E2E CHECK`.** A message with a
 * newline in it puts everything after the newline outside the verdict, where the script's
 * `case` on the whole line cannot see it.
 */
function redact(message) {
    return String(message ?? '')
        .replace(/([?&]t=)[A-Za-z0-9_-]+/g, '$1<redacted>')
        .replace(/\s*\n\s*/g, ' ');
}

/**
 * Print one verdict. **The only place a verdict reaches stdout, and the whole line goes through
 * `redact` rather than the piece somebody remembered to wrap.**
 *
 * Measured, not reasoned about: the first version redacted `error.message` and interpolated the
 * probe's answer beside it raw, so driving this file at `http://…/?x=1&t=<token>` printed
 * `&t=<redacted>` in the half that had been wrapped and the token in full in the half that had
 * not, on one line. That is the same shape as the CI leak this rule was written for — a rule
 * obeyed at the call sites somebody thought of — and one choke point is what makes it a rule.
 *
 * It costs nothing to redact a green verdict or a `NOT RUN` line, and "only the red half is
 * cleaned" is a rule that holds exactly until something passes.
 */
function say(line) {
    console.log(redact(line));
}

/**
 * Assert that the work happened.
 *
 * **Distinct from `outcome` on purpose.** "The palette never changed" and "the palette changed to
 * the wrong colour" are different bug reports, and this repository has a documented history of
 * reporting the first as the second and then of not reporting it at all.
 */
function control(condition, what) {
    if (!condition) throw new ControlFailed(`the work did not happen: ${what}`);
}

/** Assert that the outcome is right. */
function outcome(condition, what) {
    if (!condition) throw new Error(what);
}

/** The visible text of the page's own heading, or null. */
const H1 = 'document.querySelector("#main-content h1, main h1, h1") ? ' +
    'document.querySelector("#main-content h1, main h1, h1").textContent.trim() : null';

/**
 * Whether the app has ever rendered in this run. `null` until asked.
 *
 * **This latch exists to keep a site that cannot boot at all from costing ten timeouts, and it is
 * deliberately one-way.** Once the app has rendered *once*, it never goes back to `false`: the
 * question is "is this app capable of starting", and if the answer is yes then every later wait
 * is measuring something real and must be allowed to run to its deadline.
 *
 * **The one-way part is not a nicety, and it was arrived at by watching the twins.** With a
 * two-way latch the `base-href-dropped` twin — where the front door boots perfectly and every
 * deep link cannot fetch the framework — turned `ROUTES` red for the *latch's* reason rather than
 * for the address it could not load. That is a red verdict that proves nothing about the check.
 *
 * In the other direction it can hide nothing: it only ever short-circuits after a wait has
 * already failed with nothing having rendered yet, so the run is red and staying red.
 */
let appEverRendered = null;

/** Wait until Blazor has rendered a page into the shell. */
async function waitForApp(page) {
    if (appEverRendered === false) {
        throw new Error(
            'the app has not rendered once in this run — see the BOOT verdict above. Not waited '
            + 'for again: this run is already red, and each further wait would be a full timeout.');
    }

    try {
        // Forty-five seconds against a measured four: the payload is ~27 MiB of WebAssembly and a
        // loaded runner is slower than a developer's machine, but ten times the real cost is
        // headroom rather than a guess, and a deadline nobody has measured is how a hung step
        // once cost this project a six-hour job.
        await page.waitFor('!!window.Blazor', { timeout: 45000, what: 'the framework to start' });
        await page.waitFor(`!document.querySelector(".boot") && (${H1})`,
            { timeout: 45000, what: 'the app to render a page' });
        appEverRendered = true;
    } catch (error) {
        if (appEverRendered !== true) appEverRendered = false;
        throw error;
    }
}

/**
 * Go to an address and wait for the app to render it.
 *
 * **The address is put in front of whatever `waitForApp` says.** Without it a failure reads
 * "waited 45000ms for the framework to start", which is the same sentence whichever of nine
 * addresses it was — and the `base-href-dropped` twin's whole point is that the front door works
 * and a deep link does not.
 */
async function open(page, path) {
    await page.goto(`${base}${path}`);

    try {
        await waitForApp(page);
    } catch (error) {
        throw new Error(`${path}: ${error.message}`);
    }
}

/** Open the settings menu if it is not already open. Its controls are not in the document until then. */
async function openSettings(page) {
    const open = await page.evaluate(
        'document.querySelector(".settings-open")?.getAttribute("aria-expanded") === "true"');

    if (!open) await page.click('document.querySelector(".settings-open")', 'the Settings button');

    await page.waitFor('!!document.querySelector(".settings-menu-list")',
        { timeout: 10000, what: 'the settings menu to open' });
}

/** Click a button in one of the settings menu's groups, by the word on it. */
async function clickSettingsButton(page, group, label) {
    await openSettings(page);
    await page.click(
        `[...document.querySelectorAll(".settings-menu-list .${group} button")]` +
        `.find(b => b.textContent.trim() === ${JSON.stringify(label)})`,
        `the ${label} button in .${group}`);
}

/** Whether a settings button reports itself pressed. */
function settingsPressed(group, label) {
    return `[...document.querySelectorAll(".settings-menu-list .${group} button")]` +
        `.find(b => b.textContent.trim() === ${JSON.stringify(label)})` +
        `?.getAttribute("aria-pressed") === "true"`;
}

/**
 * The colours a palette actually resolves to, on real elements as well as in the tokens.
 *
 * **`.banner`'s `backgroundImage` and not its `backgroundColor`.** The banner is a
 * `linear-gradient` over `--primary`, so its background *colour* is transparent and reading that
 * reports every palette as unpainted — which this harness duly did on its first run, as a control
 * failure, which is what a control is for.
 *
 * **Nothing identifying is in here.** An earlier version returned `data-mode` and `data-theme`
 * alongside the colours and then asserted the four readings were pairwise different — which they
 * are by construction, because the two attributes differ. The comparison was vacuous and would
 * have passed against a site with one palette in it. Read only what the palette decides.
 */
const PALETTE_READING = `(() => {
    const root = getComputedStyle(document.documentElement);
    const banner = document.querySelector(".banner");
    const bannerStyle = banner ? getComputedStyle(banner) : null;
    return {
        surface: root.getPropertyValue("--surface").trim(),
        ink: root.getPropertyValue("--ink").trim(),
        primary: root.getPropertyValue("--primary").trim(),
        bannerFill: bannerStyle ? bannerStyle.backgroundImage : null,
        bannerInk: bannerStyle ? bannerStyle.color : null,
        bodyBackground: getComputedStyle(document.body).backgroundColor,
        bodyInk: getComputedStyle(document.body).color,
    };
})()`;

// ---------------------------------------------------------------------------------------------
// The checks.

/**
 * The app boots.
 *
 * <b>Control</b>: the document that was served was the app's own shell (its boot screen was in
 * it), and the framework then started — `window.Blazor` exists and a `_framework/` payload really
 * came down the wire with bytes in it. Without that second half a check on the rendered page is
 * satisfied by any page at all.
 *
 * <b>Outcome</b>: the boot screen is gone, a page is rendered inside `#app`, the banner is there,
 * and nothing threw on the way. Nothing in this repository has ever run the boot before: a proof
 * page is markup and CSS, and a boot-time failure was invisible until the deploy.
 */
async function checkBoot(page) {
    await page.goto(`${base}/`);

    const served = page.lastDocument;
    control(served?.status === 200, `the server answered ${served?.status ?? 'nothing'} for /`);
    control(await page.evaluate('window.__ppE2E?.sawBootScreen === true'),
        'the served document carried no boot screen, so it was not this app');

    await page.waitFor('!!window.Blazor', { timeout: 45000, what: 'the framework to start' })
        .catch((error) => {
            if (appEverRendered !== true) appEverRendered = false;
            throw error;
        });

    const payload = await page.evaluate(`performance.getEntriesByType("resource")
        .filter(r => r.name.includes("/_framework/") && r.decodedBodySize > 0).length`);
    control(payload > 0, 'no _framework payload was fetched, so WebAssembly never started');

    // Latched either way, so a twin whose app never mounts costs one timeout rather than ten.
    // See `appEverRendered` — it can only ever shorten a run that is already failing.
    await page.waitFor(`!document.querySelector(".boot") && (${H1})`,
        { timeout: 45000, what: 'the app to replace its boot screen' })
        .then(() => { appEverRendered = true; })
        .catch((error) => { appEverRendered = false; throw error; });

    const heading = await page.evaluate(H1);
    outcome(heading === 'Prowlers & Paragons', `the front door rendered "${heading}"`);
    outcome(await page.evaluate('!!document.querySelector(".banner")'), 'the banner did not render');
    outcome(await page.evaluate('document.title.includes("Prowlers")'),
        `the document title was "${await page.evaluate('document.title')}"`);

    // The real Content-Security-Policy is in force here — `wrangler pages dev` applies `_headers`
    // — so a policy that refuses one of the app's own scripts shows up as a console error. That
    // is a whole class of fault a `file://` proof page cannot see.
    const refused = page.consoleErrors.filter(m => /Content Security Policy/i.test(m));
    outcome(refused.length === 0, `the CSP refused something: ${refused.join(' | ')}`);
    outcome(page.exceptions.length === 0, `the page threw: ${page.exceptions.join(' | ')}`);

    return `booted, ${payload} framework payload(s), heading "${heading}"`;
}

/**
 * A character can be built, and it survives a reload.
 *
 * <b>Control</b>: the application itself wrote to local storage. The index key is read before and
 * after, and the check requires it to have gone from holding nothing to naming this character —
 * <em>by the app's own hand</em>, from two clicks and some typing. This is the exact control that
 * would have caught the defect `PROGRESS.md` item 10 records: a manager, a switcher and two undo
 * buffers all reading an index nothing ever wrote to.
 *
 * <b>Outcome</b>: after a reload the tier and the name are still there.
 */
async function checkBuild(page) {
    await open(page, '/build');

    const before = await page.evaluate(`localStorage.getItem(${JSON.stringify(INDEX_KEY)})`);
    control(!before || !before.includes(NAME),
        'this browser already knew about the harness character before it was built');

    const tiers = await page.evaluate('document.querySelectorAll(".cards .option-row, .cards button").length');
    control(tiers > 0, 'the tier page offered nothing to click');

    await page.click(
        `[...document.querySelectorAll("button")].find(b => b.querySelector(".name")` +
        `&& b.querySelector(".name").textContent.trim().startsWith(${JSON.stringify(TIER)}))`,
        `the ${TIER} tier card`);

    await page.waitFor(
        `[...document.querySelectorAll("button")].some(b => b.querySelector(".name")` +
        `&& b.querySelector(".name").textContent.includes(${JSON.stringify(TIER)})` +
        `&& b.querySelector(".name").textContent.includes("Selected"))`,
        { timeout: 15000, what: `the ${TIER} card to report itself selected` });

    await open(page, '/build/finishing');
    await page.click('document.querySelector("#ft-name")', 'the name field');
    await page.type(NAME);

    await page.waitFor(`document.querySelector("#ft-name").value === ${JSON.stringify(NAME)}`,
        { timeout: 10000, what: 'the name field to hold what was typed' });

    // The autosave is a fire-and-forget continuation on every change, so this waits for the write
    // rather than assuming it has already happened. A timeout here is a real finding: it means
    // nothing in the application wrote the character down.
    const stored = await page.waitFor(
        `(() => { const raw = localStorage.getItem(${JSON.stringify(INDEX_KEY)});` +
        `return raw && raw.includes(${JSON.stringify(NAME)}) ? raw : false; })()`,
        { timeout: 20000, what: 'the application to write the character to local storage' })
        .catch(() => null);

    control(stored, 'the application never wrote this character to local storage');

    await page.reload();
    await waitForApp(page);

    const name = await page.evaluate('document.querySelector("#ft-name")?.value ?? null');
    outcome(name === NAME, `after a reload the name field held ${JSON.stringify(name)}`);

    await open(page, '/build');
    const selected = await page.evaluate(
        `[...document.querySelectorAll("button")].some(b => b.querySelector(".name")` +
        `&& b.querySelector(".name").textContent.includes(${JSON.stringify(TIER)})` +
        `&& b.querySelector(".name").textContent.includes("Selected"))`);
    outcome(selected, `after a reload the ${TIER} tier was no longer selected`);

    // **The budget strip and not the page text, because the page text was a vacuous assertion.**
    // The first version asked whether "150" appeared anywhere in `document.body.textContent` —
    // and the tier list on this page prints all six tiers' point totals whatever is selected, so
    // it would have passed against a character that restored nothing at all. This reads the
    // strip's own `/ {budget}`, which is the *session's* figure and comes from the restored tier.
    const strip = await page.evaluate(
        'document.querySelector(".budget-of")?.textContent?.trim() ?? null');
    outcome(strip !== null, 'after a reload there was no Hero Point budget strip on the page');
    outcome(strip.includes(TIER_POINTS),
        `after a reload the budget strip read ${JSON.stringify(strip)} rather than the `
        + `${TIER_POINTS} the ${TIER} tier sets`);

    return `built at ${TIER}, named, and survived a reload`;
}

/**
 * A chosen light/dark theme survives a reload.
 *
 * <b>Control</b>: `window.ppThemeStats.stamps` moved when the button was pressed. That counter is
 * in `js/theme.js` for exactly this purpose, and until now nothing read it across a real reload —
 * `proof-theme.html` re-executes the module in one document, which is a different claim.
 *
 * <b>Outcome</b>: after a genuine reload the attribute is stamped before the app boots, and the
 * choice is in storage.
 */
async function checkTheme(page) {
    await open(page, '/');

    const stampsBefore = await page.evaluate('window.ppThemeStats?.stamps ?? -1');
    control(stampsBefore >= 1, 'js/theme.js never ran, so it stamped nothing to begin with');

    await clickSettingsButton(page, 'theme-switch', 'Dark');

    await page.waitFor(`(window.ppThemeStats?.stamps ?? -1) > ${stampsBefore}`,
        { timeout: 10000, what: 'the theme script to record a stamp' })
        .catch(() => { throw new ControlFailed('pressing Dark never reached js/theme.js'); });

    control(await page.evaluate('document.documentElement.getAttribute("data-theme") === "dark"'),
        'pressing Dark did not stamp the document');
    control(await page.evaluate(settingsPressed('theme-switch', 'Dark')),
        'the Dark button did not report itself pressed');

    await page.reload();

    // Read *before* waiting for the app: the whole point of js/theme.js is that the attribute is
    // there in the head, seconds before WebAssembly lands. Reading it after boot would pass just
    // as happily on a build that only stamped it from C#.
    const stampedEarly = await page.evaluate(
        'document.documentElement.getAttribute("data-theme")');
    outcome(stampedEarly === 'dark',
        `after a reload the document was stamped ${JSON.stringify(stampedEarly)} before boot`);

    await waitForApp(page);

    const remembered = await page.evaluate(`localStorage.getItem(${JSON.stringify(THEME_KEY)})`);
    outcome(remembered === 'dark', `the stored theme was ${JSON.stringify(remembered)}`);
    outcome(await page.evaluate('document.documentElement.getAttribute("data-theme") === "dark"'),
        'the app un-stamped the theme once it booted');

    // The menu has to be reopened: its buttons are not in the document while it is closed, and
    // asserting `aria-pressed` on an element that is not there reads as "not pressed" — which is
    // how this assertion failed on its first run against a perfectly good app.
    await openSettings(page);
    outcome(await page.evaluate(settingsPressed('theme-switch', 'Dark')),
        'after a reload the Dark button no longer reported itself pressed');

    return 'dark chosen, stamped before boot, still dark after a reload';
}

/**
 * The four palettes are four palettes.
 *
 * <b>Control</b>: each combination is actually reached — the two attributes read back what was
 * asked for — and the stylesheet is actually painting, so the banner has an opaque background
 * rather than the transparent one an unstyled element has. A pixel golden proves a page looks
 * right; nothing proved that clicking these two switches gets you to it.
 *
 * <b>Outcome</b>: the four readings are pairwise different. Hero/Villain is an identity and
 * light/dark is a reader's preference, and neither is derivable from the other — so two of these
 * collapsing into one is a real fault and not a cosmetic one.
 */
async function checkPalettes(page) {
    await open(page, '/');

    const wanted = [
        { mode: 'Hero', theme: 'Light' },
        { mode: 'Hero', theme: 'Dark' },
        { mode: 'Villain', theme: 'Light' },
        { mode: 'Villain', theme: 'Dark' },
    ];

    const readings = [];

    for (const want of wanted) {
        await clickSettingsButton(page, 'mode-switch', want.mode);
        await clickSettingsButton(page, 'theme-switch', want.theme);

        const expectedMode = want.mode.toLowerCase();
        const expectedTheme = want.theme.toLowerCase();

        await page.waitFor(
            `document.documentElement.getAttribute("data-mode") === "${expectedMode}"` +
            ` && document.documentElement.getAttribute("data-theme") === "${expectedTheme}"`,
            { timeout: 15000, what: `the document to be stamped ${expectedMode}/${expectedTheme}` })
            .catch(() => {
                throw new ControlFailed(
                    `${want.mode}/${want.theme} never reached the document element`);
            });

        const reading = await page.evaluate(PALETTE_READING);

        control(reading.surface !== '' && reading.ink !== '',
            `${want.mode}/${want.theme} resolved no palette tokens, so theme.css did not load`);
        control(reading.bannerFill && reading.bannerFill !== 'none',
            `${want.mode}/${want.theme} left the banner unpainted, so app.css did not apply`);
        control(reading.bodyBackground && reading.bodyBackground !== 'rgba(0, 0, 0, 0)',
            `${want.mode}/${want.theme} left the page ground unpainted`);

        readings.push({ want: `${want.mode}/${want.theme}`, reading });
    }

    control(readings.length === 4, `only ${readings.length} palettes were reached`);

    for (let i = 0; i < readings.length; i++) {
        for (let j = i + 1; j < readings.length; j++) {
            const a = readings[i];
            const b = readings[j];
            outcome(
                JSON.stringify(a.reading) !== JSON.stringify(b.reading),
                `${a.want} and ${b.want} are the same palette: ` +
                `surface ${a.reading.surface}, ink ${a.reading.ink}, primary ${a.reading.primary}`);
        }
    }

    // Ground and fill, because either alone repeats: Hero light and Hero dark share `--primary`
    // and differ in `--surface`, and a summary quoting one of the two reads as a collapsed pair.
    return readings.map(r => `${r.want} ${r.reading.surface} on ${r.reading.primary}`).join(', ');
}

/**
 * The routes.
 *
 * <b>Control, part one</b>: a token planted on `window` survives the click, which is what proves
 * the router handled the navigation rather than the browser reloading the document. Without it,
 * "the address bar changed and the right page is on screen" is satisfied by a full page load,
 * which is not what a `NavLink` is for.
 *
 * <b>Control, part two</b>: each deep link was answered 200 <em>at the address that was asked
 * for</em>. `web/wwwroot/_redirects` rewrites every path to `index.html` with a 200 rather than a
 * redirect precisely so a shared link keeps its path; a 302 would drop it and land every reader
 * on step one, and reading only the rendered heading would never see that.
 *
 * <b>Outcome</b>: the right page renders at each address, including one that is routed nowhere.
 */
async function checkRoutes(page) {
    await open(page, '/');

    const token = await page.evaluate('window.__ppE2E.document');
    control(typeof token === 'string' && token.length > 0, 'no per-document token was planted');

    await page.click(
        '[...document.querySelectorAll(".avenue-nav .banner-link")]' +
        '.find(a => a.textContent.trim() === "Build")',
        'the Build link in the banner');

    await page.waitFor('location.pathname.startsWith("/build")',
        { timeout: 15000, what: 'the router to follow the Build link' });

    control(await page.evaluate('window.__ppE2E.document') === token,
        'the document reloaded, so that navigation was not client-side routing');

    await page.waitFor(`${H1} === "Choose a tier"`,
        { timeout: 15000, what: 'the tier page to render' });

    const addresses = [
        ['/', 'Prowlers & Paragons'],
        ['/build', 'Choose a tier'],
        ['/build/gear', 'Gear'],
        ['/build/review', 'GM review'],
        ['/build/characters', 'Your characters'],
        ['/rules', 'Rules reference'],
        ['/campaign', 'Campaigns'],
        ['/signin', 'Your account'],
        ['/no-such-address', 'No such page'],
    ];

    for (const [path, expected] of addresses) {
        await page.goto(`${base}${path}`);

        const served = page.lastDocument;
        control(served?.status === 200,
            `${path} was answered ${served?.status ?? 'nothing'}`);
        control(new URL(served.url).pathname === path,
            `${path} was served as ${new URL(served.url).pathname}, so the path was dropped`);

        // Named, for the reason `open` gives: nine addresses share one timeout message otherwise.
        try {
            await waitForApp(page);
        } catch (error) {
            throw new Error(`${path}: ${error.message}`);
        }

        const heading = await page.evaluate(H1);
        outcome(heading === expected,
            `${path} rendered "${heading}" rather than "${expected}"`);
    }

    return `client-side routing plus ${addresses.length} addresses served at their own paths`;
}

// ---------------------------------------------------------------------------------------------
// Running them.

const ALL_CHECKS = [
    ['BOOT', checkBoot],
    ['BUILD', checkBuild],
    ['THEME', checkTheme],
    ['PALETTE', checkPalettes],
    ['ROUTES', checkRoutes],
];

if (only.size > 0) {
    const known = new Set(ALL_CHECKS.map(([name]) => name));
    const unknown = [...only].filter(name => !known.has(name));

    if (unknown.length > 0) {
        console.error(`--only names checks this driver does not have: ${unknown.join(', ')}. `
            + `Known: ${[...known].join(', ')}`);
        process.exit(2);
    }
}

const CHECKS = only.size > 0 ? ALL_CHECKS.filter(([name]) => only.has(name)) : ALL_CHECKS;

/**
 * How long the probe below gives the server to answer at all. See its comment for the bound, and
 * `Runner.ProbeTimeoutMs`, which is the same figure for the same reason.
 */
const PROBE_TIMEOUT_MS = 10000;

/**
 * Whether the server this run is driving has stopped answering — **measured, not inferred from
 * the wording of an exception**.
 *
 * **The class this replaces is what CI run 34040527190 reported.** `wrangler pages dev` died four
 * seconds into a drive; the check that was running reported a 45-second render timeout as its own
 * `[OUTCOME]`, and the seven after it each reported `net::ERR_CONNECTION_REFUSED` as its own
 * finding. Eight verdicts about eight checks, none of them about the one thing that had happened.
 *
 * **A string match on the error would be the third denylist this repository has been burnt by.**
 * `net::ERR_CONNECTION_REFUSED` is one of a dozen spellings a dead server can produce — reset,
 * empty response, a socket that hangs — and a render timeout, which is how the *first* check saw
 * it, contains none of them. So this asks the server directly instead: one request from Node, and
 * a refused connection is a fact about the world rather than a guess about a message.
 *
 * **An answer counts, whatever it says.** A 500, a redirect, a slow but arriving response — all of
 * those are a server that is still there and a check that is entitled to its own verdict.
 * Narrowing it that way is what keeps a twin's honest red from being relabelled: the whole point
 * of a twin is a check failing while the server is perfectly alive, and `boot-app-never-mounts`
 * serves `/` perfectly well while never mounting the app.
 *
 * **No answer at all counts too, and leaving it out was the half of this that CI has not yet
 * billed us for.** `docs/guide/testing.md` records the state in as many words: a `workerd` that
 * has died under a live `wrangler pages dev` does not refuse connections — wrangler keeps the port
 * and **answers by hanging for ever**, which is why readiness here is the served body rather than
 * the status code. Against that server every check below spends its full navigation timeout and
 * reports it as its own `[OUTCOME]`, which is precisely the reporting run 34040527190 was fixed
 * for, arrived at by the other road. So a probe that does not come back within
 * `PROBE_TIMEOUT_MS` is a server that has stopped answering, in the plain meaning of the sentence.
 *
 * **The bound is what keeps that honest.** Ten seconds for a `GET /` off a local static server
 * that has already served this run — the measured cost is milliseconds — so it separates "hung"
 * from "loaded" with three orders of magnitude in hand. And it is only ever asked after a check
 * has already failed: a site that cannot serve its front page in ten seconds has no verdict worth
 * reading anyway.
 *
 * **`tests/e2e/Runner.cs` classifies identically, deliberately.** Both drivers print the same
 * sentence for the same fact, including the POSIX spelling of the socket error, because `e2e.sh`
 * reads both with one `grep` and a reader compares two runs by eye.
 */
async function serverStoppedAnswering() {
    try {
        await fetch(`${base}/`, { signal: AbortSignal.timeout(PROBE_TIMEOUT_MS) });
        return null;
    } catch (error) {
        // `fetch` wraps the real reason; `cause.code` is where Node puts `ECONNREFUSED`, and
        // `AbortSignal.timeout` arrives as a `TimeoutError` either directly or as the cause.
        const code = error?.cause?.code ?? error?.code ?? '';
        if (code === 'ECONNREFUSED' || code === 'ECONNRESET') return `${base}/ answered ${code}`;

        const name = error?.name ?? error?.cause?.name ?? '';
        if (name === 'TimeoutError' || name === 'AbortError') {
            return `${base}/ did not answer within ${PROBE_TIMEOUT_MS}ms`;
        }

        return null;
    }
}

/**
 * Run one check and print its verdict.
 *
 * **A check that throws says FAIL rather than ending the run**, for the same reason the proof
 * harnesses' twins must *say* FAIL rather than merely fail to say PASS: scripts/e2e.sh drives a
 * deliberately-broken twin of this site and requires the named check to be red there, and a
 * harness that died on the way would leave that line absent — which is not a verdict.
 *
 * **Three kinds now, and the third is the driver rather than the site.** `[HARNESS]` is what the
 * Playwright driver has always minted for its own bugs, and `scripts/e2e.sh` refuses to count one
 * as a working negative control — a twin whose only red verdict is a `[HARNESS]` one has not been
 * watched to fail for the reason it claims. A server that has gone away is exactly that: nothing
 * it produces says anything about whether this check can see its defect.
 *
 * Returns `'pass'`, `'fail'`, or `'server-gone'`.
 */
async function run(name, check, page) {
    const started = Date.now();

    try {
        const detail = await check(page);
        say(`E2E CHECK ${name}: PASS — ${detail} (${Date.now() - started}ms)`);
        return 'pass';
    } catch (error) {
        const gone = await serverStoppedAnswering();

        if (gone) {
            say(`E2E CHECK ${name}: FAIL — [HARNESS] the server stopped answering `
                + `(${gone}), so this verdict is about the server and not about ${name}. `
                + `What the check saw first: ${error?.message} (${Date.now() - started}ms)`);
            return 'server-gone';
        }

        const kind = error instanceof ControlFailed ? 'CONTROL' : 'OUTCOME';
        say(`E2E CHECK ${name}: FAIL — [${kind}] ${error?.message} (${Date.now() - started}ms)`);
        return 'fail';
    }
}

const { page, close } = await launch({});

let passed = 0;
let ran = 0;
let notRun = [];

try {
    await page.send('Page.addScriptToEvaluateOnNewDocument', { source: RECORDER });

    for (let i = 0; i < CHECKS.length; i++) {
        const [name, check] = CHECKS[i];

        // Each check starts from a page it navigated to itself, and reads only what the
        // application put there. State left behind by an earlier check is deliberate — a browser
        // a person has used is not a fresh one — and no check depends on another having run.
        const verdict = await run(name, check, page);

        ran++;
        if (verdict === 'pass') passed++;

        // Console noise is per-check: an error logged while the boot check ran must not be
        // reported by the palette check three minutes later.
        page.exceptions.length = 0;
        page.consoleErrors.length = 0;

        // **Stopping is the honest answer, and it is not only about time.** Every check below
        // would navigate to a server that is not there, fail on the first request, and print a
        // verdict that reads as a finding about itself. `NOT RUN` is neither a pass nor a fail;
        // the summary line counts what ran, so the figures cannot quietly describe a suite that
        // did not happen.
        if (verdict === 'server-gone') {
            notRun = CHECKS.slice(i + 1).map(([rest]) => rest);
            break;
        }
    }
} finally {
    await close();
    await sleep(50);
}

for (const name of notRun) {
    say(`E2E CHECK ${name}: NOT RUN — the server stopped answering before this check`);
}

say(`E2E RAN ${ran} CHECKS, ${passed} PASSED`);
process.exit(passed === CHECKS.length ? 0 : 1);
