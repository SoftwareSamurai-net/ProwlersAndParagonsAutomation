# Something finally runs the application

Stage one of [`PROGRESS.md`](../../PROGRESS.md) item 10, approved by the owner on 2026-09-02 and
built the same day. `./scripts/e2e.sh` publishes the browser front end, serves it with the same
`wrangler pages dev` version `.github/workflows/deploy.yml` pins, and drives real Chrome over the
DevTools Protocol. Five checks, anonymous only, no credential and no bypass. It runs on every pull
request.

**Nothing in this repository had ever run the application.** Three suites, nineteen browser verdict
harnesses and a pixel diff of eight proof pages all ran before this, and every one of them ran a
*part* of it: `dotnet test` drives the engine, bUnit renders components, the proof harnesses drive
markup and CSS over `file://`, and the pixel diff compares pictures of that markup. None of them
booted Blazor WebAssembly, followed a link, reloaded a page, executed a line of `js/*.js` for real,
or was subject to the Content-Security-Policy the deploy generates.

## What it drives

| Check | Positive control, first | Outcome |
|---|---|---|
| `BOOT` | The document served carried the app's own boot screen, and the framework then started — `window.Blazor` exists **and** a `_framework/` payload arrived with bytes in it | The boot screen is gone, a page is rendered, the banner is there, nothing threw, and the CSP refused nothing |
| `BUILD` | The **application itself** wrote to local storage: the index key went from holding nothing to naming this character, from two clicks and some typing | After a reload the name is in the field and the tier is still selected, with the tier's 150 Hero Points on the page |
| `THEME` | `window.ppThemeStats.stamps` moved when Dark was pressed, so the click reached `js/theme.js` | After a genuine reload the attribute is stamped **before the app boots** — read before waiting for Blazor, so a build that only stamped it from C# would fail — and the choice is in storage |
| `PALETTE` | Each of the four combinations was actually reached (both attributes read back what was asked) and the stylesheet is painting: the banner has a real fill, the page ground a real colour | The four readings are pairwise different |
| `ROUTES` | A per-document token survives the click, which is what proves the *router* handled it rather than the browser reloading; and each deep link was answered 200 **at the address asked for** | Nine addresses render their own heading, including one that is routed nowhere |

`scripts/e2e/cdp.mjs` is the protocol client — ~330 lines against Node's own global `WebSocket`,
which has been there since v22.4 (checked in `node:22-alpine`, not assumed: `v22.23.2`,
`typeof WebSocket === 'function'`). No `package.json`, no `node_modules`, no second Chrome
downloaded. That is the same trade `scripts/visual/png.mjs` already makes against an image library,
and Puppeteer is the thing not installed this time.

**Clicks are real mouse events at real coordinates**, dispatched by the browser rather than
`el.click()` from inside the page, and text is typed through `Input.insertText`. That is not
fastidiousness: the defect that sharpened item 10 was a feature nothing in the application ever
reached, and a synthetic click is the same mistake one layer out — it reaches the handler without
proving anything is reachable.

## The negative control is a whole second site

`CLAUDE.md`: *a denylist of spellings cannot make a verdict honest — build the broken twin.*
`scripts/e2e/defects.mjs` copies the published directory and substitutes **one documented line**;
`e2e.sh` serves each twin the same way, drives it with the byte-identical harness, and requires the
named check to say `FAIL`.

| Twin | One line | Must turn red |
|---|---|---|
| `boot-app-never-mounts` | `index.html`'s `<div id="app">` renamed | `BOOT` |
| `store-writes-nothing` | `ppStore.save` accepts the write and drops it | `BUILD` |
| `theme-not-restored` | `theme.js` stamps the default instead of the stored value | `THEME` |
| `villain-palette-missing` | the light villain palette's selector stops matching | `PALETTE` |
| `base-href-dropped` | `<base href="/">` is gone | `ROUTES` |

**Each of the five turns exactly its own check red and leaves the other four green** — except the
boot twin, where nothing can render and all five go red, which is the honest shape for the check
everything else stands on.

**`base-href-dropped` is the sharpest of them and its verdicts say why.** Without a base element,
relative fetches resolve against the current path — so `/` and `/build` still work (one segment
resolves `_framework/…` to `/_framework/…`) and only a *two*-segment address breaks. `ROUTES` says
`/build/gear: waited 45000ms for the framework to start` and `BUILD` says `/build/finishing`, while
`THEME` and `PALETTE` pass on the front door. Each failing wait names its address for that reason:
without it, nine addresses share one sentence.

Two properties are load-bearing:

- **A twin must *say* `FAIL`, never merely fail to say `PASS`.** Each check catches internally and
  prints a verdict either way, and `e2e.sh` treats a missing verdict as a failure of the twin. This
  is the fault three of this repository's four historical guard faults were.
- **The substitution throws if its line has moved** — the `ProofPages.WithDefect` property against
  a published directory. It fired for real during this slice: `:root[data-mode="villain"] {`
  appears four times in `theme.css`, so the substring match found three and refused. The repair was
  to match **whole lines, exactly, indentation included**, which makes the top-level occurrence
  unique and incidentally removes a CRLF-versus-LF hazard that would have matched on Windows and
  silently nothing on the CI runner.

**Every check must have a twin, and a check without one fails the run.** The names the driver
reported and the names the twins cover are compared as sets, so a sixth check cannot join the suite
unproven.

## Five faults the harness found in itself, which is the part worth keeping

**Two assertions were vacuous when first written, and both were green.** That is the same failure
shape `CLAUDE.md` opens with, committed twice inside one afternoon by somebody writing a harness
*about* that failure shape — which is the argument for the discipline rather than against it.

- **The palette comparison.** The reading it compared included `data-mode` and `data-theme`
  alongside the colours, and then asserted the four readings were pairwise different — which they
  are by construction, because the two attributes differ. It would have passed against a site with
  one palette in it. It now reads only what the palette decides.
- **The restored budget.** `BUILD` asked whether `"150"` appeared anywhere in
  `document.body.textContent` after the reload — and `/build` prints all six tiers' point totals
  whatever is selected, so it would have passed against a character that restored nothing. It now
  reads `.budget-of`, which is the *session's* figure and comes from the restored tier.

Neither was found by reading the check. The first was found by writing its twin; the second by
asking, of each assertion in turn, *what would still be true if the feature were absent*.

**The palette check then failed for a real reason and the control is what said so.** It reported
`[CONTROL] Hero/Light left the banner unpainted` — because `.banner` is a `linear-gradient` over
`--primary`, so its background *colour* is transparent and the fill is in `backgroundImage`. A
check that had folded control into outcome would have reported "the palettes are identical", which
is a different and wrong bug report.

**The theme check failed on an assertion about a closed menu.** `aria-pressed` on an element that
is not in the document reads as "not pressed", so the post-reload assertion had to reopen the
settings menu. A perfectly good app, an assertion that could not see it.

**And a `readFileSync` race took a whole run down with a stack trace and no verdict.** Chrome's
`DevToolsActivePort` file exists a moment before it is readable; opening it mid-write answers
`EBUSY` on Windows. A not-yet-readable port file is the ordinary case one poll early.

## Three things about the server, each of which cost a debugging round

- **`wrangler pages dev` runs from `.e2e/`, and the placement is the configuration.** Wrangler
  bundles a `functions/` directory found in the *working directory* — there is no flag for it — so
  running from the repository root would bundle the accounts API, which needs a D1 binding stage
  one deliberately does not have. From `.e2e/` there is none to find; `/api/` falls through
  `_redirects` to `index.html` and the app reads an unparseable answer as anonymous, which is its
  own documented behaviour rather than a special case for the harness. The site directories are
  named *relative* to that directory for a second reason: wrangler is Node and cannot read a Git
  Bash path like `/c/Users/…`.
- **Readiness is the served body, not the status code and not wrangler's own log line.** Wrangler
  prints `Ready on http://…` and then, if its worker has died, answers requests by hanging for
  ever. That happened twice here and looked exactly like a broken page. The probe requires the
  app's own boot screen to be in the answer.
- **MSYS pids and Windows pids are two namespaces, and getting the translation wrong broke this
  three times.** `$!` is an MSYS pid and `taskkill` speaks Windows pids, so the first cleanup
  killed nothing and the script hung for ever *after printing five green verdicts*. Translating
  with `ps -W` was worse: it lists Windows-only processes with their **Windows** pid in the first
  column, so the match hit an unrelated process and the tree kill took out the driver — which then
  printed no verdict and read exactly like a harness unable to see its own defect. And killing only
  the port's listener leaves wrangler's supervisor alive to restart `workerd`, so the port came
  back on a new pid as fast as it was cleared. The answer: `ps` **without** `-W` (MSYS processes
  only, so column one is unambiguous), `taskkill //T` on that translation, and the port's listener
  cleared afterwards as a backstop rather than as the mechanism.

**One thing that was not a bug and cost the most time of any of them.** Two overlapping runs shared
one redirected log file, and the second one's writes landed at the first one's stale offset — a
several-hundred-byte hole of spaces, a missing section, and two `grep: No such file` lines for a
file that existed. Three separate hypotheses were chased before the logs themselves turned out to
be the corrupt witness. The lesson is small and general: when a transcript disagrees with the
files it is describing, suspect the transcript.

## What it does not reach, stated so nobody assumes otherwise

- **Anything behind sign-in** — stage two, which is now only a decision about effort. See item 10.
- **Pages Functions and D1** — nothing is bundled, on purpose, as above.
- **A `_redirects` regression.** `wrangler pages dev` *rejects* this site's own `/* /index.html 200`
  rule as an infinite loop and ignores it, then serves `index.html` for unmatched paths by its own
  default — so deep links work locally for a different reason than they work in production.
  Measured rather than assumed: wrangler names that rule in its startup output as the one invalid
  rule it found.
- **`motion.js` and `palette.js`.** The reduced-motion behaviour and the Ctrl-K chord are both
  behavioural and both still only asserted by a `file://` proof page.
- **Screen readers**, which stay owed and which no harness closes.

## Cost

Locally, on Windows with the site already published: **307 seconds** for a full run — the real site
plus all five twins — measured with `PP_E2E_SITE_ALREADY_BUILT=1`. Of that, the real run's five
checks are about 25 seconds of driving, each of six `wrangler pages dev` launches is 10–20 seconds
of startup, and the two twins that cannot render spend a deliberate 45 seconds each on the timeouts
they exist to produce.

**One timeout and not ten, because of a one-way latch** — and the "one-way" was the second attempt.
`appEverRendered` records whether the app has rendered *once*; if it has, no later wait is ever
short-circuited, because the app is demonstrably capable of starting and every wait after that is
measuring something real. The first version was two-way and cost the `base-href-dropped` twin its
whole point: the front door boots perfectly there and only deep links fail, so `ROUTES` went red
for the latch's reason rather than for the address it could not load — a red verdict that proves
nothing about the check. In the other direction the latch can hide nothing: it only short-circuits
after a wait has already failed with nothing having rendered, so the run is red and staying red.

**The CI cost is not measured yet** — this has never run on a runner. It is the last step in
`build.yml` on purpose: it is the slowest thing there and the cheapest failures should be reported
first. `ubuntu-latest` ships Google Chrome and the step before it has already warmed the npm cache
for this exact wrangler, so it costs a step rather than a dependency. Read the real figure off the
first run rather than off this paragraph; `docs/guide/hosting.md` has the command.
