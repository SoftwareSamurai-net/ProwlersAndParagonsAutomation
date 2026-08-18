# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the work it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

**3893 tests** — 3668 engine, 225 bUnit — zero warnings at CI strictness, MIT in `LICENSE`, the
site live on Cloudflare Pages. Four front ends on one engine assembly: the terminal wizard, the
browser app, `build --from character.json`, and an MCP server. **CI drives a browser too**: five
proof harnesses on `ubuntu-latest`, each required to *say* `PASS` in its `<title>`.

**Everything through Phase 2 is in `master`.** The mutation-audit reconciliation merged as
[#45](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/45); slice B and Phases
0, 1 and 2 of the front-end plan merged as
[#46](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/46). Start from `master`.
The old branches — `…-221eb7`, `…-c88220`, `claude/reconcile-a1-a3` — are all behind it now and
should not be started from.

**Slice B shipped all six items** — two self-hosted faces (Oswald and Public Sans, both SIL OFL
with their licences), labels carrying the structure of the long forms, the tier choice as a card
grid, the rule under each derived figure, the rulebook's word beside each rank, and one filter
box in the component all five pickable lists share. The budget bar then became a sticky strip of
chrome rather than a panel costing ~110px above every step.

**Phases 0, 1 and 2 are done.** The scales exist and are held by
`NoScreenRuleNamesARawSpacingOrTypeLength`; the chrome is one band instead of three and six empty
states name the next action; and motion carries meaning — the chrome persists across a step change
through the View Transitions API, the budget figure counts to its new value, and a row arriving in
a chosen list lands. No animation library: `wwwroot/js/motion.js` is the whole of it, **+3.5 KB
brotli**, and the CSP is unchanged. `PROGRESS.md` has the full account.

**Three reviewers ran on Phase 2 and the third was worth more than the first two.** Two adversarial
passes found three bugs in *shipped code* — an `Animation` leaked per count, an abandoned count
could rest the strip on a figure the engine no longer returns, and `motion.js` had become
load-bearing for navigation. The fix-audit then found that of ten fixes only three held, **and that
one of them had shipped a bug worse than the one it closed**, found with no mutation applied at all.
Do not skip that third reviewer.

---

## What the next session is for: Phases 3, 4 and 5

The plan is [`docs/FRONT-END-PLAN.md`](FRONT-END-PLAN.md) and it is the brief. **Read it before
starting.** Phases 0–2 are done; three remain.

**Phase 3 — the interactions that are still forms.** Two slices, risk medium-high. Where the app
stops feeling like a document and starts feeling like a tool:

- **A `Ctrl-K` command palette** — jump to a step, find a Power, add one. The highest-leverage
  item on the list and mostly built already: `OptionFilter.Admits` does the matching, and 141
  Powers is exactly the catalogue a palette is for.
- **The pips become the control.** They are `aria-hidden` decoration beside a `+`/`−` stepper
  today; clicking the fifth pip should set 5d, with arrow keys and Home/End.
- **Keyboard navigation in the option lists**, with the filter box keeping focus. Every list is
  mouse-only in practice.
- **Validation where the mistake is made.** The engine answers continuously; the findings only
  surface at GM review. A Trait over the cap should say so on its own row.
- **Undo.** Three buttons on the tier page can destroy twenty minutes behind a confirm dialogue.

**This phase is not bUnit-shaped.** A palette and a pip control are new interaction surfaces and
need real keyboard and screen-reader testing. Budget for that rather than discovering it.

**Phase 4 — the sheet as the reward, not the exit.** Half a slice, risk low. A live preview column
so the sheet is visible *while* building, which is nearly free — `SheetView` already takes a
character and there is one sheet component by design. **Phase 1's deferred fourth item lands here**,
because they are one job: widening `--column` above a breakpoint, which every band follows
automatically, but which also widens the sheet and the replay. Decide that deliberately rather than
as a side effect. Watch the render cost — the sheet re-renders on every keystroke unless throttled.

**Phase 5 — the things that are simply missing.** A skip link and landmark roles; "Saved" feedback,
since the character write-through is silent; and a print preview honest about the browser's own
header, which no page can suppress.

**Two Phase 2 findings are recorded rather than fixed, both measured** — see `PROGRESS.md`:

- A held-open view transition **swallows pointer input** for ~260ms, up to 1000ms if the failsafe
  fires. `pointer-events: none` on the pseudo would let the click through *to the new page while
  the visitor still sees the old one*, trading a dead click for a wrong one.
- **There is no `aria-live` anywhere**, so crossing into over-budget is announced to nobody. If you
  add one it must go on a sibling summary, **never** on `.budget-figure strong`, which `ppCount`
  rewrites up to 60×/s.

Phase 2 item 3 — exit animations — is deferred by the plan itself.

---

## Prerequisites — run these before writing any code

Each one has cost this project real time when skipped.

1. **Confirm the toolchain.** `dotnet --version` must report **10.0.x**; the 9.x SDK cannot build
   this. `global.json` pins `10.0.100` with `latestMinor`.
2. **Start from `master`.** Everything through Phase 2 is merged. The old branches — `…-c88220`,
   `…-221eb7`, `claude/reconcile-a1-a3` — are all behind it.
3. **Establish the baseline before you change anything.**
   ```bash
   dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
   ```
   It must report **3668 + 225 = 3893** and zero warnings. **Warnings are errors only under that
   flag**, so a plain `dotnet test` passes over things CI fails on.

   **Take the number from the run, not from a document, and update the document from the run.**
   The figure here read 3849 against a tree of 3850 for one commit, because it was copied from a
   run taken before the last test was added — and both reviewers caught it, which is a waste of a
   reviewer. It has since been wrong twice more, once on both sides of a merge conflict at the
   same time.

4. **Read the summary line properly.** A crashed test process still prints
   `Passed!  -  Failed: 0` — a stack overflow reports `Catastrophic failure ... exit code
   -1073741571`, skips tests, and the summary still reads green. **Grep for `Catastrophic` and
   check the total moved.**
5. **Confirm you can see the app without a dev server.** Do not start one; it raises an approval
   dialogue that blocks unattended work.
   ```bash
   PP_PROOF=1 dotnet test tests/ProwlersAndParagons.Web.Tests
   ```
   should write `web/wwwroot/proof-*.html` (gitignored). Screenshot with headless Chrome at
   `C:\Program Files\Google\Chrome\Application\chrome.exe`, using
   `--headless=new --no-sandbox --allow-file-access-from-files --user-data-dir=<temp>
   --virtual-time-budget=3000`. **The virtual-time budget is not optional** — `.panel` animates
   from `opacity: 0` and a bare screenshot photographs it mid-animation, which has been misread
   as a palette fault and half-fixed as one.
6. **Confirm you can proof the printed sheet**, because it is the deliverable and every phase can
   break it. Add `--no-pdf-header-footer --print-to-pdf=<ABSOLUTE WINDOWS PATH>`; rasterise with
   Docnet.Core + ImageSharp **pinned below 4.0** (both are in the local NuGet cache). Three copies
   of `SheetView` must come out as exactly **three pages** in both palettes.
7. **Know where the rulebook is.** The PDFs live in the **main working directory's** `docs/`, not
   in a worktree — `*.pdf` is gitignored repository-wide, so `ls docs/*.pdf` from a worktree
   reports nothing, which reads as "there is no rulebook" and is wrong. A whole slice was worked
   on that mistake.
8. **Test a 375px viewport with an iframe, not `--window-size=375`.** Headless Chrome clamps its
   window width to about 485px, so a 375-wide screenshot is a 485px render cropped — it looks
   like catastrophic overflow and is not. A reviewer nearly filed that.

   **And make the harness print the measurement rather than leaving it to the eye.** The bug this
   guards against was 8px of overflow, which is invisible in a screenshot and unmistakable as
   `clientWidth 360, scrollWidth 368`. An iframe onto `proof-hero.html` with three lines of script
   does it; the same trick measures box insets, which is how a 3.2px table misalignment was found.

9. **`--virtual-time-budget` suppresses frame production.** It is *required* for screenshots —
   `.panel` animates from `opacity: 0` and a bare capture photographs it mid-animation — and under
   it `requestAnimationFrame` never fires and animation timelines do not advance. Measured: a probe
   reports `RAF-FIRED-1` without the flag and `NO-FRAME` with it, identically under `--dump-dom`,
   `--screenshot` and `--run-all-compositor-stages-before-draw`. **So anything animated must be
   checked by seeking** (`anim.currentTime = x`), never by waiting. This is why the counting figure
   is an `element.animate()` clock rather than a rAF loop, and a regression that hid behind exactly
   this property shipped once already.
10. **The five browser harnesses run in CI**, so a change that breaks one fails the PR rather than
    waiting for somebody to run it by hand. `gh pr checks <n> --watch` is the authority. Verify the
    step *ran* — the harness output names each page — because a step that silently did nothing
    looks exactly like one that passed.

11. **Do not run two reviewers concurrently in one worktree.** Both of Phase 0's reviewers
   mutate files and revert with `git checkout`, so they poison each other: one caught the other's
   `--text-sm: 2rem` and read it as a finding, and both lost runs to `index.lock`. Give each
   reviewer its own worktree, or run them one at a time. Their own scratch files under
   `web/wwwroot` also break `PrintRestatesEveryTokenTheScreenPalettesDeclare` by naming tokens,
   and one reviewer's cleanup deleted the other's harness.

---

## How this project expects to be worked on

Not preferences — this is what the last few slices cost when they were skipped.

1. **Update `PROGRESS.md` in the same change**, not afterwards. It is the only place the
   reasoning survives.
2. **Have the work adversarially reviewed by agents that know nothing about it.** For every guard
   test, ask the reviewer to name a plausible bug the test claims to cover but would not catch —
   **and to demonstrate it by mutation rather than argue it.**
3. **Then ask a reviewer to audit the fixes, not the code.** This has been the most valuable
   reviewer four sessions running, and on this one it found that three fixes did not hold and
   that a brand-new component had three defects and no coverage at all. On Phase 0 it found that
   **nine of eleven fixes caught only the mutation demonstrated to them** — ask it for a *variant*
   that reaches the same end state, not a re-run of the original.

4. **A later declaration of the same thing beats a `Contains`, and that one root cause has now
   defeated five guards in `WebPresentationTests`.** `Contains("position:sticky")` is satisfied by
   a declaration overridden on the next line; a pinned `--space-4: 0.75rem` is satisfied while a
   duplicate lower down wins the cascade; `border-bottom:` is satisfied by `border-bottom: none`.
   The instrument is `EffectiveValue` — comma lists split, suffix-matched, last declaration wins —
   and `RulesTargeting` beside it. **Do not write a new guard in this file with `Contains`.**

5. **Placement in the cascade is part of aiming a mutation.** Twice this slice a mutation inserted
   *earlier* in the file than the rule it was meant to override reported as a survivor, because the
   cascade genuinely resolved the right way and the mutation never reached the state being tested
   for. Insert after the rule you are overriding, and check the numstat **and** the marker after
   the run as well as before — a concurrent revert mid-run reads exactly like a guard holding.
6. **Look at the thing, do not only test it — and *generating* a proof is not looking at it.**
   Every visual bug in slice B — a count reading 282 of 141, a rank printed as `12D`, a citation
   as `CH.6` — was found by looking at a rendered page, and none was visible to any test. Phase 1
   then added a proof of the shell in both palettes, screenshotted the Hero one, wrote "verified by
   looking… both palettes", and shipped a chrome band with **no bottom edge at all** in Villain
   mode, on the tier page before a tier is chosen, and on every replay route. The Villain proof
   showed it. **If you generate a page per palette, open every one of them.**

7. **Fix the class of defect, not the defect.** The same round produced six guards that caught only
   the mutation shown to them, and then — after all six were fixed on exactly that principle — the
   most severe finding of the lot was repaired with no guard at all, and a mutation put it straight
   back. Fixing a defect and not guarding it is the same failure one level up.
8. **A guard that grows subjects without growing coverage is worth less each time.** Adding three
   uppercased classes to the budget strip added three selectors the guard could not reach. If a
   test enumerates things, make it refuse a subject it never found.
9. **Check a rulebook citation before repeating it.** A comment cited "Elasticity", which is not
   a Power in this rulebook; `CLAUDE.md` already recorded that exact slip being made once before.

---

## Traps whatever you touch

- **The rulebook PDFs are in `docs/` and a worktree cannot see them.** `*.pdf` is gitignored, so
  they live in the main working directory only. `ls docs/*.pdf` from a worktree reports nothing,
  which reads as "there is no rulebook" and is wrong. A whole slice was worked on that mistake.
- **The printed page offset is a constant +3.** Verified at both ends of the book. Each page prints
  its number twice interleaved, so a footer extracts as `151 5` for printed 15.
- **`data/rulebook/` is generated. Do not hand-edit it** — an edit is lost on the next run of
  `tools/RulebookExtractor` and hides whatever the extractor is doing wrong.
- **A whole-tree Qodana scan means nothing run in place.** The same commit reports 0 from
  `git archive HEAD | tar -x -C <tmp>` and 1471 from a built working directory, `.CSharpErrors`
  included, on files that compile. Export first.
- **Warnings are errors only under `ContinuousIntegrationBuild`**, so a green `dotnet test` does
  not cover it. Run
  `dotnet build --configuration Release -p:ContinuousIntegrationBuild=true` before pushing.
- **Before any destructive revert, run `git stash push -u -m pre-experiment`.** Not "commit
  first" as a caution — this was a caution twice and was ignored twice by people who had read
  it, most recently by an agent that reverted one file to undo a mutation and took an unrelated
  uncommitted change with it in the same breath. `CLAUDE.md` carries the rule; there is no
  judgement call about whether a given revert is risky.
- **`perl -pi` silently edits nothing on this machine.** It exits 0, prints nothing, and leaves the
  file untouched — so a mutation "applied" that way looks exactly like a fix that holds. Use
  `sed -i` or the editor, and check `git diff --numstat` every time.
- **`sed` mangles Windows paths**: `\c` becomes a backspace and `\r` a carriage return, silently.
  Use the editor for anything containing a path.
- **A check that never ran looks exactly like one that passed.** Do not pipe a verification through
  `grep` and read empty output as green; assert on the positive. A nested `$_` in a PowerShell
  `Where-Object` shadows the outer loop variable and will report everything missing.
- **The authority on CI is CI.** `gh pr checks <n> --watch` runs the same strict flags on Linux and
  needs no local daemon. Prefer it to a local Docker run when a branch is already pushed — and note
  that a Docker run piped through `grep` while the daemon happens to be stopped exits **0** with an
  empty output, which a hurried reader takes for green. Carried from
  [#44](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/44); it was nearly lost
  merging `master` into this branch, because this file had been rewritten on both sides.
- **A crashed test process still prints `Passed!  -  Failed: 0`.** Removing the equality guard in
  `OptionList.OnAfterRender` produces the endless render loop its own comment describes. The run
  ends in `Catastrophic failure: Test process crashed with exit code -1073741571` — `0xC00000FD`,
  stack overflow — **31 of 153 tests never run, and the summary line still reads `Passed!` with
  `Failed: 0`.** The exit code is 1, so CI catches it; a person tailing the log for `Passed!`
  does not, and a reviewer's first pass did exactly that before catching itself. **Grep for
  `Catastrophic` and check the test total moved, never the word `Passed!` alone.**
- **A guard test that reads the shipped data cannot tell you the mechanism reads it too.** To pin a
  mechanism, drive it against a synthetic model that differs only in the field.


---

## Two more, carried from earlier slices

- **Measure against the thing you are replacing.** The extractor rewrite regressed 26 of Ch.2's
  Power entries that the *old* extractor got right, and the only reason that did not ship as a
  fix is that the old corpus was scored on the same check. A rewrite is not automatically better
  than what it replaces — and this applies squarely to a redesign.
- **The mutation question has found a third to a half of new guards were theatre every time it
  has been asked.** Last time it was 38 of 64. This slice it was three fixes out of eight plus a
  whole new component. Expect it, and budget for the second pass rather than treating it as bad
  news.
