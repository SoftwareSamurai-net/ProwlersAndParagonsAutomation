# A second driver, and the reading it had to correct three times

`tests/e2e` drives the assembled application with `Microsoft.Playwright` and measures it with
`Deque.AxeCore.Playwright`. It runs the five checks `scripts/e2e/drive.mjs` runs, plus `A11Y`, which
the hand-rolled DevTools Protocol client cannot do at all. Both drivers run in `build.yml`;
`PROGRESS.md` item 10 carries the condition under which the first is deleted, and this slice did not
delete it.

Pull requests: [#142](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/142)
(skeleton), [#145](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/145)
(guards), [#147](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/147)
(BUILD), [#143](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/143)
(THEME), [#146](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/146)
(PALETTE), [#144](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/144)
(ROUTES), [#148](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/148)
(A11Y), and the integration change that landed the wiring, the workflow and these notes.

## The seam was already there, which is most of why this was cheap

`scripts/e2e.sh` owns everything around a drive — publishing, parsing the wrangler version out of
`deploy.yml`, starting the server from a directory with no `functions/` in it, building each
deliberately-broken twin, and deciding what the verdicts mean. A driver takes a URL and prints
`E2E CHECK <NAME>: PASS|FAIL` plus a summary line.

That split was not designed for a second driver. `PP_E2E_DRIVER` existed as a *test seam*, so that
the script's own 300-second deadline could be watched to fire against a driver that never returns.
It is the reason the skeleton landed without touching a line of the shell.

**It could not have carried the second driver in the end, and the reason is worth keeping**:
`read -r -a driver <<< "$PP_E2E_DRIVER"` word-splits, this repository's checkout is under "Personal
Projects", and the .NET driver's path therefore tears in half at the space. A seam that works for
`node scripts/e2e/drive.mjs` and silently cannot express the thing you want next is a seam that
looks like it works. `--driver node|dotnet` builds the array itself.

## What Playwright is here for, and what it is deliberately not

**For**: web-first waiting that replaces a hand-rolled `waitFor`, real `Input.dispatchMouseEvent`
clicks with actionability checks on top, and — the only genuinely new capability — axe-core inside
the page.

**Not for**: anything on the pixel path. `scripts/visual-regression.sh`, `scripts/visual/diff.mjs`,
`png.mjs`, `visual-goldens.yml` and `tests/visual-goldens/` are untouched. Two reasons, both already
written down: a golden must be rendered by the *same* Chrome that compares it, and this repository
already manages two renderers. **And Playwright for .NET could not do it anyway** —
`ToHaveScreenshotAsync` and `--update-snapshots` belong to `@playwright/test`, the JavaScript
runner; the .NET `PageAssertions` and `LocatorAssertions` surfaces have no such member. That was the
single most likely thing to get wrong and it was checked before anything was built.

**It also does not bring a browser.** `Channel = "chrome"` launches the Google Chrome already on the
machine — Chrome 152 locally, `ubuntu-latest`'s own `google-chrome` on the runner, which
`build.yml` resolves with `command -v` and passes as `PP_E2E_CHROME` rather than trusting
Playwright's channel resolution. So no `playwright install`, nothing to cache, and no third
renderer.

## What it cost, measured on the runner rather than estimated

Same job, one commit apart, on the exact parent commit rather than against a figure carried from
elsewhere — `docs/guide/hosting.md` records Restore as 11s and the honest baseline today is 14s.

| Step | `main` @0bb84847 | + the Playwright packages |
|---|---|---|
| Restore | 14s | **18s** |
| Build | 57s | 57s |
| Test | 53s | 54s |
| Whole Build job | 533s | **537s** |

**+4 seconds**, for a 201.6 MB `Microsoft.Playwright` nupkg (773 MB expanded — it carries Node
driver binaries for five platforms; the package's own targets copy only the building platform's, so
the build output is 105 MB). No NuGet cache was added: hosting.md's measurement says a cache loses
to an 11-second restore, and 4 seconds does not change that arithmetic.

## The integration run, which is the evidence rather than the six branches' own runs

Two branches that merge cleanly can still contradict each other, and two of these did: the THEME
and PALETTE slices each wrote a `SettingsMenu` helper, in different namespaces, and the merge
compiled perfectly with both. So the run that counts is one branch, both drivers, every twin.

| | checks | twins | wall clock |
|---|---|---|---|
| `--driver node` | 5 green | 5 driven, all red; `html-lang-dropped` skipped and said so | **4m02s** |
| `--driver dotnet` | 6 green | 6 driven, all red | **6m14s** |

Local, Windows, Chrome 152. Before `--only`, the node run was 6m17s — so the twin filter paid for
the second driver's twins and most of the first's.

**On the runner, and this is the number that matters rather than the projection:** the whole Build
job went from **533s to 1103s and 1132s** on two consecutive green runs — 18m23s and 18m52s,
against a 30-minute cap. I projected 13–14 minutes from the local ratios and was wrong, which is
the argument for measuring rather than scaling a laptop's figures.

Per step, over those two runs: node 392s and 395s, Playwright 521s and 528s — steady to within one
percent. **A third, earlier run put the node driver at 200s, and that one is the outlier**: worth
recording because a single fast reading is exactly what a budget gets set from. Two agreeing runs
beat one convenient one.

**So there is headroom but not a lot of it, and the levers are named here so nobody has to rederive
them.** In order of what they cost:

1. **Drop one driver from the job.** Cheapest and reversible — the file stays, and this is what
   `PROGRESS.md` item 10 says to do if CI gets tight rather than deleting anything.
2. **Scan fewer palettes in A11Y.** 45s of every drive is the four-palette loop, and only
   `color-contrast` can tell the palettes apart. One palette costs ~12s. This trades the coverage
   the check was built for, so it is the second choice and not the first.
3. **Publish once and share it** — already done; both steps set `PP_E2E_SITE_ALREADY_BUILT=1`.

Do not reach for a `timeout-minutes` increase: the 30-minute cap exists because a hung step ran to
GitHub's own six-hour limit, and `docs/guide/hosting.md` says why raising it is the wrong direction.

`dotnet test` is green at 4,065 engine tests, including the seven new cross-driver guards.

## Every check was broken and watched to fail

Each against the existing twin for its check, driven by the byte-identical harness.

| Check | real site | twin | its verdict |
|---|---|---|---|
| BOOT | 6.5s | `boot-app-never-mounts` | `[OUTCOME] waited 45000ms for the app to replace its boot screen` |
| BUILD | 6.5s | `store-writes-nothing` | `[CONTROL] the work did not happen: the application never wrote this character to local storage` |
| THEME | 2.8s | `theme-not-restored` | `[OUTCOME] after a reload the document was stamped "" before boot` |
| PALETTE | 1.8s | `villain-palette-missing` | `[OUTCOME] Hero/Light and Villain/Light are the same palette` |
| ROUTES | 14.5s | `base-href-dropped` | `[OUTCOME] /build/gear: waited 45000ms for the framework to start` |
| A11Y | 47.9s | `html-lang-dropped` (new) | `[OUTCOME] 16 accessibility violation(s): … html-has-lang …` |

**THEME's twin is the one that proves the two verdict kinds are worth separating.** That defect
leaves `js/theme.js` running and incrementing its own counter — the positive control passes — and
stamps the default instead of the stored preference. It reports `[OUTCOME]`, not `[CONTROL]`,
which is the difference between "the feature never ran" and "it ran and was wrong".

**PALETTE's slice went further and reproduced the original fault.** With `data-mode` and
`data-theme` put back into the palette reading — the shape this check shipped with the first time —
the `villain-palette-missing` twin reports **PASS**: four readings pairwise different by the two
attributes alone, on a site with three palettes in it. Reverted, and re-confirmed red.

## Three guard faults found by breaking things, not by reading them

**1. A name pattern with no digits in it, in six places.** `E2eDriverTests`' "every driven check
has a twin" assertion was mutated by adding a driven check with no twin — and it stayed **green**.
The extraction was `[A-Z][A-Z_]*`, so `A11Y` — the one check about to be added — matched as the
single letter `A`. The same pattern was copied four times in that file and **twice more in
`scripts/e2e.sh`**, including the comparison that decides whether every check has a negative
control. Reading them would not have shown it; one mutation did, and then the fix had to be hunted
wherever the pattern had been copied.

**2. The A11Y check scanned whichever palette ran before it**, and then whichever *page state* ran
before it. It runs last, so it inherited Villain/Dark from the PALETTE check while every comment
written about it described Hero/Light — and it inherited a built character from the BUILD check,
which changes which elements exist on `/build`. It now clicks its way to each palette, asserts it
arrived, and sits second in the list so that a full run and a `--only A11Y` run scan the same
pages. See below: the second half of this cost a correct finding being deleted as an artefact.

**3. The exemption has a positive control, because a dead one rots a check quietly.** The one
violation A11Y drops is matched by selector; if that selector stops matching, the check reports
`[CONTROL] the color-contrast exemption for '.disabled' matched nothing` rather than passing a
little more easily every year. Watched to fail by pointing it at a class that does not exist.

## What axe measured, and the two readings before it that were wrong

**The answer: axe's full default ruleset, nothing turned off, four palettes × four addresses — one
violation, in every palette, on the same element, and it is exempt under WCAG's own text.**
Everything else is clean: 536 passing rule instances, identical across repeated runs.

The one finding is the wizard's Next control on `/build` before a tier is chosen.
`web/Components/StepButtons.razor` renders it as an anchor with `aria-disabled="true"`, painted at
`opacity: 0.45` with `pointer-events: none`, and axe measures **2.23:1 Hero/Light, 3.28:1
Hero/Dark, 2.54:1 Villain/Light, 3.22:1 Villain/Dark** against a 4.5:1 floor. WCAG 1.4.3 exempts
text that is part of an inactive user interface component, and this control is inactive; axe cannot
apply the exemption because it looks for the `disabled` *attribute*, which an anchor cannot carry.
So the check drops those nodes — **node by node, not by turning `color-contrast` off** — counts
them, prints the count in its verdict line, and fails if the exemption ever matches nothing.

**That last part is the owner's judgement rather than this file's**, which is why the figures are
here and in `PROGRESS.md`: a disabled Next is exactly what a reader looks at to work out why they
cannot go on, and 2.23:1 is faint. WCAG exempts it; legibility does not.

**What `theme.css` claims about itself holds.** It writes its ratios into its comments beside its
own "re-measure if you change it; do not eyeball", two of its tokens are `color-mix()` which only a
browser resolves, and `EveryScreenPairInUseHoldsItsContrastFloor` measures the *tokens* — a
different claim from every rendered combination. Now measured, in four palettes, and clean.

### Two wrong readings on the way, and neither was simply a wrong number

**The first said 32 violations. The second said 2, then 3, then 2, on different elements each run.**
Both were the check measuring pages mid-animation: `.panel` carries `animation: rise var(--enter)
both`, which starts at `opacity: 0`, and axe measures contrast against the *composited* pixel. The
tell was that one background came back as `#15151a`, `#17171c` and `#19191e` — three shades of one
colour is not palette drift and is not a font race. **This repository already knew this trap one
layer over**: `docs/guide/testing.md` says `--virtual-time-budget` "is not optional" for the pixel
diff, because a bare screenshot "proofs a washed-out lie". Nobody connected the two until the
numbers moved three times. `Scan` now awaits
`Promise.all(document.getAnimations().map(a => a.finished))` — the elements' own promises rather
than a sleep, because a sleep is a guess that gets shorter as the machine gets busier.

**And a third reading was right and was very nearly thrown away as a fourth artefact.** A middle
draft found the `.disabled` control at a stable 2.23:1 and disabled `color-contrast` wholesale,
with a long, dated, sincere written reason. A later pass could not reproduce it, concluded the
whole thing was the animation race, deleted the disabled rule, and wrote that up as the finding.
**Both halves of that were wrong, and why is the part worth keeping**: A11Y ran last, so in a full
run `BUILD` had already saved a character, the wizard's Next was *enabled*, and the element was not
on the page at all. Under `--only A11Y` — which is how every twin drives it — it is. Two runs of
one check against one build, disagreeing about a real defect because of what ran before them.

So A11Y is now **second in the list**, immediately after BOOT, and that position is part of what it
measures. Every other check here is indifferent to what ran before it; this one is not, and a check
whose subject depends on execution order is not reproducible however good its waits are.

**The lesson that cost the most here: re-running a measurement is not reproducing it.** The second
run has to be in the same state as the first, and for a driver "the same state" includes which
checks ran before it. A disagreement between two runs is not automatically the earlier one being
wrong.

## Rules are selected by name, never by WCAG tag

The obvious `RunOnly` on `wcag2a,wcag2aa` is wrong here. Verified against this build with
`page.GetAxeRules()`: `color-contrast` carries `wcag2aa`, but `heading-order` and
`page-has-heading-one` carry only `cat.semantics, best-practice` — axe never WCAG-tags a
best-practice rule. A tag filter would have run one of the three rules this work was scoped around
and silently dropped the other two.

## What got weaker, said plainly

**`e2e.sh` no longer requires the driven set and the twinned set to be equal.** It cannot: the two
drivers do not run the same checks, so equality would fail every `node` run over a twin the other
driver covers. What is kept in the script is the direction whose failure costs a missed
regression — a driven check with no twin. The orphan direction moved to
`E2eDriverTests.EveryCheckHasATwinAndEveryTwinHasACheck`, which reads *both* drivers and
`defects.mjs` as source. That is strictly more than the script could see, since it runs one driver
and cannot tell "no driver has this check" from "not this one", and it costs a second rather than a
publish, a server and a browser. Both files say so at the point of change; do not restore the
equality test without reading them.

**And a twin is now driven with `--only <CHECK>`.** Exactly one verdict is read out of a twin's run
and the rest were a server round trip and a browser boot apiece: with `A11Y` scanning four palettes
at 45s a drive, six twins spent four and a half minutes re-measuring the accessibility of sites
broken on purpose in ways unrelated to it. It cannot make a run quietly smaller — the real site is
driven unfiltered and it is *that* run whose names are compared against the twin list, and a name
matching nothing leaves the verdict absent, which is already read as a failed negative control.

## A Windows note that will cost somebody an afternoon

Playwright's Node driver lives at `<output>/.playwright/node/win32_x64/node.exe`. A build output
path over 260 characters makes `Playwright.CreateAsync()` fail with
`Win32Exception (2): The system cannot find the file specified`, **naming a path that exists**.
Measured: 184 characters from a `.claude/worktrees/` worktree and 136 from the main checkout, so the
repository is fine; a scratch build at 270 was not.

## One claim in the brief that did not survive checking

"Playwright is Microsoft's own recommendation for Blazor WebAssembly E2E" is overstated. The
ASP.NET Core testing page calls it "an example of an E2E testing framework that can be used with
Blazor apps" — descriptive, not an endorsement. Nothing here rests on it; the decision rests on the
axe integration and the +4 seconds.
