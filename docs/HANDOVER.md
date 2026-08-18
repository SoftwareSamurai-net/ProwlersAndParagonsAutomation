# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the work it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

**3877 tests** — 3663 engine, 214 bUnit — zero warnings at CI strictness, MIT in `LICENSE`, the
site live on Cloudflare Pages. Four front ends on one engine assembly: the terminal wizard, the
browser app, `build --from character.json`, and an MCP server.

**The reconciliation is in `master`; the visual redesign is not.**

- **`claude/reconcile-a1-a3`** reconciled the three sub-slices of the mutation audit, worked
  concurrently on three branches from `5867340`, each rewriting the same four documents. It
  **merged as [#45](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/45)** and
  is `master`'s tip. All 33 findings are closed. (An earlier version of this file said both slices
  were unmerged; that was true when it was written and is not now.)
- **`claude/slice-b-visual-redesign-c88220`** is the live branch: the visual redesign plus Phases
  0, 1 and 2 of the front-end plan. **`…-221eb7` is the same work two phases stale** — it stops at
  `38131df` and should not be started from.

**Slice B shipped all six items** — two self-hosted faces (Oswald and Public Sans, both SIL OFL
with their licences), labels carrying the structure of the long forms, the tier choice as a card
grid, the rule under each derived figure, the rulebook's word beside each rank, and one filter
box in the component all five pickable lists share. The budget bar then became a sticky strip of
chrome rather than a panel costing ~110px above every step. `PROGRESS.md` has the full account.

**Three reviews ran on it and the third was worth more than the first two.** A general
adversarial pass found eight holes; a typography-and-contrast pass measured a real WCAG failure
and found the print-specificity trap for the third time; and a **fix-audit — a reviewer pointed
at the fixes rather than at the code — found that three of those fixes did not hold**, including
one where the filter written to make a guard robust widened the hole it was closing. Do not skip
that third reviewer.

---

## What the next session is for: the front end as an application

The plan is [`docs/FRONT-END-PLAN.md`](FRONT-END-PLAN.md), in six phases, and it is the brief.
**Read it before starting** — it carries the reasoning, and one load-bearing decision that will
otherwise be re-litigated.

**Do the phases in order. Phase 0 is done** — the spacing, type and elevation scales exist and
`NoScreenRuleNamesARawSpacingOrTypeLength` holds them, so **Phase 1 starts by asking a scale for
a value rather than choosing one.** `PROGRESS.md` has the account; the plan carries the four
things a later phase needs to know, including two rungs that are pinned to measured values and
must not be tidied onto a ratio.

**Phase 1 is done too**, except for one half deliberately moved to Phase 4 — see the plan, which
says which half and why. The chrome is one band instead of three, six empty states name the next
action, and the print block came through untouched (three pages, both palettes).

**Next is Phase 2 — motion that carries meaning**, and its own note is the one to read first: the
principle before the list, because this is where "slick" becomes "noisy". Two things Phase 0 and 1
left ready for it. **`--ease-out` exists and is used; `--ease-emphasised` deliberately does not** —
add it in the phase that has something to overshoot, which the plan says is a row arriving in a
list. And the **View Transitions** item wants the shared elements to persist across a route change:
the step list and the budget strip are now full-width siblings of `main` rather than children of
it, which is the shape that makes them persistable rather than re-rendered.

**The one thing Phase 2 must not break is the sticky strip.** It stays put because its containing
block is the document; anything that wraps it, or gives an ancestor a transform or a filter, ends
that silently. There is a measured check — `proof-sticky.html` scrolls the shell in an iframe and
reports the strip's top, which must be 0 after a scroll.

**The one decision already made: there is no animation library.** `element.animate()` does
everything on the list in ten lines; the payload is already this project's largest open item and
slice B just added 381 KB of fonts to it; there is no network on this machine to fetch, vendor or
verify one; and the View Transitions API does a thing no library can. The shortlist is in the
plan if that is overruled — it is a decision, not a prohibition.

**JavaScript itself is open.** `wwwroot/js/download.js` already exists and is called for the
palette, the download and local storage; `script-src 'self'` allows a same-origin script with no
hash and no policy change. Only an *inline* script would need one, and the header script hashes
exactly one of those today.

---

## Prerequisites — run these before writing any code

Each one has cost this project real time when skipped.

1. **Confirm the toolchain.** `dotnet --version` must report **10.0.x**; the 9.x SDK cannot build
   this. `global.json` pins `10.0.100` with `latestMinor`.
2. **Start from the right commit.** `git log --oneline -1` on
   `claude/slice-b-visual-redesign-c88220` — **not `…-221eb7`, which is two phases behind.**
   `master` has the mutation-audit reconciliation but none of the front-end work.
3. **Establish the baseline before you change anything.**
   ```bash
   dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
   ```
   It must report **3663 + 214 = 3877** and zero warnings. **Warnings are errors only under that
   flag**, so a plain `dotnet test` passes over things CI fails on.

   **Take the number from the run, not from a document, and update the document from the run.**
   The figure here read 3849 against a tree of 3850 for one commit, because it was copied from a
   run taken before the last test was added — and both reviewers caught it, which is a waste of a
   reviewer.
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

9. **Do not run two reviewers concurrently in one worktree.** Both of Phase 0's reviewers
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
