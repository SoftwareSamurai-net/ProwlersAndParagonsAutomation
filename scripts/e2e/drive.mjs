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
    console.error('usage: node scripts/e2e/drive.mjs <base-url>');
    process.exit(2);
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

const CHECKS = [
    ['BOOT', checkBoot],
    ['BUILD', checkBuild],
    ['THEME', checkTheme],
    ['PALETTE', checkPalettes],
    ['ROUTES', checkRoutes],
];

/**
 * Run one check and print its verdict.
 *
 * **A check that throws says FAIL rather than ending the run**, for the same reason the proof
 * harnesses' twins must *say* FAIL rather than merely fail to say PASS: scripts/e2e.sh drives a
 * deliberately-broken twin of this site and requires the named check to be red there, and a
 * harness that died on the way would leave that line absent — which is not a verdict.
 */
async function run(name, check, page) {
    const started = Date.now();

    try {
        const detail = await check(page);
        console.log(`E2E CHECK ${name}: PASS — ${detail} (${Date.now() - started}ms)`);
        return true;
    } catch (error) {
        const kind = error instanceof ControlFailed ? 'CONTROL' : 'OUTCOME';
        console.log(`E2E CHECK ${name}: FAIL — [${kind}] ${error.message} (${Date.now() - started}ms)`);
        return false;
    }
}

const { page, close } = await launch({});

let passed = 0;

try {
    await page.send('Page.addScriptToEvaluateOnNewDocument', { source: RECORDER });

    for (const [name, check] of CHECKS) {
        // Each check starts from a page it navigated to itself, and reads only what the
        // application put there. State left behind by an earlier check is deliberate — a browser
        // a person has used is not a fresh one — and no check depends on another having run.
        if (await run(name, check, page)) passed++;

        // Console noise is per-check: an error logged while the boot check ran must not be
        // reported by the palette check three minutes later.
        page.exceptions.length = 0;
        page.consoleErrors.length = 0;
    }
} finally {
    await close();
    await sleep(50);
}

console.log(`E2E RAN ${CHECKS.length} CHECKS, ${passed} PASSED`);
process.exit(passed === CHECKS.length ? 0 : 1);
