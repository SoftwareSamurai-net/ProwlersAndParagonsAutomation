# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the work it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

**3972 tests** — 3672 engine, 300 bUnit — zero warnings at CI strictness, MIT in `LICENSE`, the
site live on Cloudflare Pages. Four front ends on one engine assembly: the terminal wizard, the
browser app, `build --from character.json`, and an MCP server. **CI drives a browser too**: six
proof harnesses on `ubuntu-latest`, each required to *say* `PASS` in its `<title>`.

**Everything below is in `master` — start from it.** The front-end plan's Phases 0, 1 and 2 merged
as [#46](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/46), and since then:

| | |
|---|---|
| [#49](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/49) | Phase 3's `Ctrl-K` command palette; the Hero/Villain mode became `CharacterSheet.IsVillain` and the budget limit an independent `UnlimitedBudget` toggle |
| [#50](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/50) | `Tooltip`, and the guard refusing a `title` attribute anywhere |
| [#51](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/51) | The rank pips became a real control (`role="slider"`) |
| [#52](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/52) | **One site, two areas** — the play aide and `/portfolio` — plus the `ICharacterStore` / `IIdentitySource` seam this next slice slots into |

`PROGRESS.md` has the account of each. The old branches — `…-221eb7`, `…-c88220`,
`claude/reconcile-a1-a3` — are far behind and must not be started from.

**The most valuable reviewer, four sessions running, is the one pointed at the fixes rather than at
the code.** On the last few it found that mutations reported as caught were no-ops, that a commit
message described a change the commit did not contain, and that a fix had shipped a bug worse than
the one it closed. Do not skip it.

---

## What the next session is for: characters that belong to an account

**This is the brief.** Everything before it is context; this is the work.

A character lives in one browser's local storage and nowhere else. Close that browser on another
machine and it is gone; there is no way to have two characters, no way to share one except by
downloading a file, and no way for the rules reference to know who is reading it. Accounts are what
fixes all four, and they are the last thing on the list that changes what this app *is* rather than
how it looks.

### What already exists, so do not rebuild it

The seam went in with the two-areas split and it is behaviour-preserving — everybody is anonymous
today and the app behaves exactly as it did.

- **`ICharacterStore`** — save the inputs, load them back, throw them away. A server-backed store is
  a registration change in `Program.cs`, not a rewrite. `CharacterStore` is the browser
  implementation.
- **`IIdentitySource`** — answers who the character belongs to, **asynchronously**, because a real
  one has to ask something. `LocalIdentity` returns `Identity.Anonymous` and never fails.
- **`Identity`** carries a key and a display name and **deliberately nothing else** — no claims, no
  token, no expiry. The wrong authentication model is harder to remove than none, so it stays that
  small until a real one is chosen.
- Storage already partitions by identity key, and `IdentityStorageTests` pins the behaviour that
  makes the transition safe.

### The decision that gates everything

**The site is a static Cloudflare Pages deploy with no backend, and accounts need one.** That is not
a detail to design around later — it decides the whole slice, and it costs the "static site" property
the README advertises. Settle it before writing code. The shapes worth weighing:

- **A hosted identity and data provider** (Cloudflare Access/D1 + Workers, Supabase, Firebase,
  Auth0 + a small API). Fastest to a working sign-in; adds a service, a bill and a second place the
  character exists.
- **Cloudflare Workers + D1 in the same account**, so the deploy stays one pipeline and the data
  stays where the site already is. More to write, least new surface.
- **No server: an account is a sync key** — the character stays local and syncs through a
  user-supplied endpoint or a file. Keeps the static property and is honest about it, but it is not
  really an account and will not carry the rules reference's gating.

**What to ask before choosing:** is the goal genuinely multi-device characters, or is it gating the
rulebook reader? Those want different things, and `PROGRESS.md` records the reader as
"account-gated and comes after the front-end redesign" — which has now happened.

### Rules this slice must not break

- **Nothing about the rules may branch on who is signed in.** A character is legal or not regardless
  of who holds it. `PresentationFlagsTests` already enforces the same discipline for the
  Hero/Villain flag; identity deserves the same guard before the first `if`.
- **The anonymous storage key stays `pp.character.v1`.** Changing it empties every returning
  visitor's browser, silently, looking like storage cleared rather than a bug.
- **An account's characters land *beside* the anonymous slot, never on top of it.** Signing in on a
  shared browser must not overwrite what somebody was building, and signing out must not have eaten
  it.
- **Nothing in `ICharacterStore` may throw.** Restoring happens before the first render, so an
  exception is not a lost character but an app that does not start — and a network that is not
  there is now one of the ways it can fail.
- **No credentials, tokens or personal data in the repository or in a log.** The MCP server's own
  note is the standard to hold: this project handles no credentials, and an account system is the
  first thing that could quietly change that.
- **The engine never learns any of this.** `engine/` and `sheets/` have no idea accounts exist and
  must not gain one.

### The vertical slice worth taking first

One character, one account, end to end — sign in, the character follows you to another browser, sign
out and the anonymous one is still there. Resist building a character *list* in the same slice: it
changes `ICharacterStore` from "the character" to "characters", which is a different interface and a
different set of screens, and it is much easier to get right once one character round-trips.

---

## Also asked for, not started

### A Power's rulebook text on hover

**The form-explanation half of tooltips is built** — `Tooltip`, with two call sites; `PROGRESS.md`
has the account, and the guard refusing a `title` attribute is the part not to undo.

What is *not* built is the other half, and it is the more valuable one: a Power's own printed text
where a player is choosing it. `data/rulebook/` has the prose, is already extracted, and is
deliberately **not** in the browser payload, so serving it is one `ItemGroup` in `web/`'s csproj
plus a decision about the public site — which is now an account question rather than a component
one. Three things to know before starting:

- **The corpus is generated.** Do not hand-edit `data/rulebook/`; regenerate with
  `tools/RulebookExtractor`.
- **Where the two disagree, `data/rules/` wins.** The corpus is the text; the mechanics are
  structured and verified entry by entry.
- **`Tooltip` is the wrong container for a paragraph.** It is sized and positioned for a sentence.
  A Power's entry wants a panel or a disclosure, not a floating box.

### Visual regression, which nothing currently catches

The six harnesses assert *measurements* — overflow, insets, whether an animation ran — and never
appearance. Screenshots are generated and looked at once by whoever is working, then thrown away.
Nothing catches drift between commits, and it has already cost: a pip silently went from 7px to 5px
under `border-box` and only a hand-written probe found it, and a tooltip stacking bug was reported
by a person rather than by CI.

Golden PNGs of the proof pages with a per-pixel tolerance would close it. Two things make it viable
here that usually do not: the fonts are self-hosted, and CI already drives Chrome at a fixed
viewport. **Generate the goldens in CI on Linux, never from a Windows run** — antialiasing differs
and every one will mismatch. The real cost is not the harness, it is reviewing golden updates; a
lazy "accept new goldens" step makes the whole thing worthless.

### What remains of the front-end plan

Phase 3's last two items — validation on the row where the mistake is made, and undo — plus Phase 4
(the sheet as a live preview column, which absorbs Phase 1's deferred `--column` widening) and
Phase 5 (skip link and landmarks, "Saved" feedback, an honest print preview). The plan is
[`docs/FRONT-END-PLAN.md`](FRONT-END-PLAN.md).

**Two Phase 2 findings are recorded rather than fixed, both measured** — see `PROGRESS.md`: a
held-open view transition swallows pointer input for ~260ms, and there is no `aria-live` anywhere,
so crossing into over-budget is announced to nobody. If you add one it must go on a sibling summary,
**never** on `.budget-figure strong`, which `ppCount` rewrites up to 60×/s.

### Known warts, both deliberate

- **Home and End on a rank slider also scroll the document.** Blazor fixes `preventDefault` at
  render time rather than per event, so suppressing it there would swallow Tab and trap focus in a
  rank row. The fix is a small interop shim like `palette.js`, not a Razor attribute.
- **Screen-reader testing is owed** on the command palette and the pips. `aria-activedescendant` is
  asserted to point at a row that exists, which is not the same as having been listened to.

---

## Prerequisites — run these before writing any code

Each one has cost this project real time when skipped.

1. **Confirm the toolchain.** `dotnet --version` must report **10.0.x**; the 9.x SDK cannot build
   this. `global.json` pins `10.0.100` with `latestMinor`.
2. **Start from `master`.** Everything through #52 is merged; the table above lists what that is.
   The old branches — `…-c88220`, `…-221eb7`, `claude/reconcile-a1-a3` — are far behind it.
3. **Establish the baseline before you change anything.**
   ```bash
   dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
   ```
   It must report **3672 + 300 = 3972** and zero warnings. **Warnings are errors only under that
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
10. **The six browser harnesses run in CI**, so a change that breaks one fails the PR rather than
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
