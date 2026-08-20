# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the work it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

**4128 tests** — 3685 engine, 386 bUnit, 57 driving the accounts server — zero warnings at CI
strictness, MIT in `LICENSE`, the site live on Cloudflare Pages. Five front ends on one engine
assembly: the terminal wizard, the browser app, `build --from character.json`, an MCP server, and
now an accounts server that holds no rules at all. **CI drives a browser too**: six proof harnesses
on `ubuntu-latest`, each required to *say* `PASS` in its `<title>`.

**Everything below is in `master` — start from it.** Since the front-end plan's Phases 0–2 merged
as [#46](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/46):

| | |
|---|---|
| [#49](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/49) | Phase 3's `Ctrl-K` command palette; the Hero/Villain mode became `CharacterSheet.IsVillain` and the budget limit an independent `UnlimitedBudget` toggle |
| [#50](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/50) | `Tooltip`, and the guard refusing a `title` attribute anywhere |
| [#51](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/51) | The rank pips became a real control (`role="slider"`) |
| [#52](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/52) | **One site, two areas** — the play aide and `/portfolio` — plus the `ICharacterStore` / `IIdentitySource` seam |
| [#54](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/54) | The README split into one file per domain |
| [#55](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/55) | **Accounts** — magic-link sign-in, one character per account following you between browsers, and the rulebook's own text behind the sign-in |
| *this slice* | **Characters, plural** — import from a file, up to five per account (25 for a GM), and a manager panel replacing "Starting over" at the top of the tier page. Also: `Download to keep` on the review step, because the app was writing no file it could read back |

`PROGRESS.md` has the account of each. The old branches — `…-221eb7`, `…-c88220`,
`claude/reconcile-a1-a3` — are far behind and must not be started from.

**The most valuable reviewer, six sessions running, is the one pointed at the fixes rather than at
the code.** On this one it found that **six of nine fixes caught only the mutation they had been
shown** — each had guarded the absence of one spelling rather than the property, and a variant
reaching the same end state walked past it with every suite green. One of the six would have
established a session while telling its owner they were not signed in. Do not skip it, and ask it
for a *variant*, never a re-run.

---

## The one thing blocking accounts, and no commit can do it

**The code is deployed; the account system does nothing until five things exist that only the owner
of the Cloudflare account can create.** [`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md) is the five steps:
a D1 database, its migration applied, a binding named `DB`, a Resend key, and SPF/DKIM records on a
domain that can send mail. **`pages.dev` cannot send mail**, so this is also the reason to attach
`pp.softwaresamurai.net` if it is still not attached.

**Until then nothing is broken and nothing looks broken**, which is the property to be careful
about. Every visitor is anonymous, the character lives in their browser under the historical key,
and the reader shows nothing — all deliberate, all tested. The deploy is the only place a
misconfiguration is ever visible, so it checks that `/api/me` answers **401 carrying JSON**; a site
whose Functions did not deploy answers that address with `index.html` and a 200, works perfectly,
and signs nobody in for ever.

**Do not start a session by "fixing" the accounts feature on the strength of it not working
locally.** There is no local server; `dotnet run` serves the static app only, so `/api/me` 404s or
falls through and the app is correctly anonymous. The suites are the instrument:
`./scripts/test-worker.sh` for the server, `dotnet test` for the client.

---

## What the next session could be for

Three candidates, in the order that gets the most value.

### Visual regression, which nothing still catches

Unchanged from the last handover and now slightly more overdue, because this slice added two
surfaces whose faults were **found by looking at a rendered page and invisible to every test**: a
proof that rendered the signed-out form under a heading saying "Signed in", and an edge measuring
1.57:1 in one palette while looking deliberate in the other.

The six harnesses assert *measurements* — overflow, insets, whether an animation ran — never
appearance. Golden PNGs of the proof pages with a per-pixel tolerance would close it. Two things
make it viable here that usually do not: the fonts are self-hosted, and CI already drives Chrome at
a fixed viewport. **Generate the goldens in CI on Linux, never from a Windows run** — antialiasing
differs and every one will mismatch. The real cost is not the harness, it is reviewing golden
updates; a lazy "accept new goldens" step makes the whole thing worthless.

### Read-only share links

**The one place a bearer key is straightforwardly better than a session** — a viewer with no
account should be able to see a shared character without signing in as anyone. The
`add-read-only-share-links` task carries the brief, with the design in outline:
`shares(sha256(key), character_id, expires_at)`; `GET /api/shares/{key}` answers the payload
without a session; a `/shared/{key}` page renders through `SheetView` with no editing controls.

The engine still costs and validates in the browser — the server just hands the bytes back — and
`data/rulebook/` is still not on the open web, so a shared sheet does not open the reader.

### What remains of the front-end plan

Phase 3's last two items — validation on the row where the mistake is made, and undo — plus Phase 4
(the sheet as a live preview column, which absorbs Phase 1's deferred `--column` widening) and
Phase 5 (skip link and landmarks, "Saved" feedback, an honest print preview). The plan is
[`docs/FRONT-END-PLAN.md`](FRONT-END-PLAN.md).

**Phase 5's "Saved" feedback is now owed rather than merely wanted.** A save to local storage
effectively cannot fail; a save over a network can, and `ApiCharacterStore` swallows it, because
nothing in `ICharacterStore` may throw — it runs before the first render, so an exception is a
blank page rather than a lost character. The character then exists only in that tab and nobody is
told.

**Two Phase 2 findings are recorded rather than fixed, both measured** — see `PROGRESS.md`: a
held-open view transition swallows pointer input for ~260ms, and there is no `aria-live` anywhere,
so crossing into over-budget is announced to nobody. If you add one it must go on a sibling summary,
**never** on `.budget-figure strong`, which `ppCount` rewrites up to 60×/s.

### Known warts, all deliberate

- **Home and End on a rank slider also scroll the document.** Blazor fixes `preventDefault` at
  render time rather than per event, so suppressing it there would swallow Tab and trap focus in a
  rank row. The fix is a small interop shim like `palette.js`, not a Razor attribute.
- **Screen-reader testing is owed** on the command palette, the pips, and now the sign-in page.
  `aria-expanded` is asserted to be the string `"true"`, which is not the same as having been
  listened to.
- **The rulebook reader serves Chapter 2 only.** Adding a chapter is one line in `worker/corpus.js`
  — and a decision about what an account is entitled to read, which is why it is not a glob.

---

## Prerequisites — run these before writing any code

Each one has cost this project real time when skipped.

1. **Confirm the toolchain.** `dotnet --version` must report **10.0.x**; the 9.x SDK cannot build
   this. `global.json` pins `10.0.100` with `latestMinor`.

   **And for the accounts server, Node 22 or Docker.** `./scripts/test-worker.sh` uses local Node
   if it is 22+, Docker otherwise, and says which. Node 22 is the floor because the tests run the
   real migration against real SQLite through `node:sqlite`, and because `worker/corpus.js` imports
   JSON with an import attribute. This slice was written on a machine with no Node at all.
2. **Start from `master`.** Everything through the table above is merged. The old branches —
   `…-c88220`, `…-221eb7`, `claude/reconcile-a1-a3` — are far behind it.
3. **Establish the baseline before you change anything — both suites.**
   ```bash
   dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
   ```
   ```bash
   ./scripts/test-worker.sh
   ```
   They must report **3685 + 386** and **57**, and zero warnings. **Warnings are errors only under
   that flag**, so a plain `dotnet test` passes over things CI fails on.

   **Take the numbers from the runs, not from this document, and update this document from the
   runs.** The figure here has been wrong three times, once on both sides of a merge conflict at
   the same time, because it was copied from a run taken before the last test was added.

   **And run both when you mutate.** This slice's mutation harness ran `dotnet test` only, and
   duly reported a rename as surviving that the accounts suite had caught. A harness that does not
   run every suite reports the wrong answer confidently.
4. **Read the summary line properly.** A crashed test process still prints
   `Passed!  -  Failed: 0` — a stack overflow reports `Catastrophic failure ... exit code
   -1073741571`, skips tests, and the summary still reads green. **Grep for `Catastrophic` and
   check the total moved.** `node --test` has the same trap in another spelling: a glob that
   matches nothing exits 0 and reports zero tests, which is why CI asserts the count.
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

   **`proof-accounts-hero.html` and `-villain.html` are short on purpose.** The full screen proof
   runs to some 7000px, where a new surface is a strip in an image nobody can read at a glance —
   which is "verified by looking" at a page too big to look at.

   **A proof that renders the wrong state looks entirely plausible.** Signing a context in must
   happen *before* `.With(mode)`: loading a sample saves the character, which asks who is here and
   remembers the answer.
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
   like catastrophic overflow and is not.

   **And make the harness print the measurement rather than leaving it to the eye.** The bug this
   guards against was 8px of overflow, which is invisible in a screenshot and unmistakable as
   `clientWidth 360, scrollWidth 368`.

   **A measuring probe is as capable of being wrong as the thing it measures.** This slice's
   contrast probe read `rgb(0–255)` and `color(srgb 0–1)` on one scale, so it measured every
   colour against black and reported 1.00 for a pair that is plainly legible. Give a probe a
   positive control with a known answer — white on black is 21 — and check it first.
9. **`--virtual-time-budget` suppresses frame production.** It is *required* for screenshots and
   under it `requestAnimationFrame` never fires and animation timelines do not advance. Measured:
   a probe reports `RAF-FIRED-1` without the flag and `NO-FRAME` with it, identically under
   `--dump-dom`, `--screenshot` and `--run-all-compositor-stages-before-draw`. **So anything
   animated must be checked by seeking** (`anim.currentTime = x`), never by waiting.
10. **The six browser harnesses run in CI**, so a change that breaks one fails the PR rather than
    waiting for somebody to run it by hand. `gh pr checks <n> --watch` is the authority. Verify the
    step *ran* — the harness output names each page — because a step that silently did nothing
    looks exactly like one that passed.
11. **Do not run two reviewers concurrently in one worktree.** Both mutate files and revert with
    `git checkout`, so they poison each other: one caught the other's `--text-sm: 2rem` and read it
    as a finding, and both lost runs to `index.lock`. **Give each reviewer its own worktree** —
    that is what this slice did, and it worked. Their own scratch files under `web/wwwroot` also
    break `PrintRestatesEveryTokenTheScreenPalettesDeclare` by naming tokens.

---

## How this project expects to be worked on

Not preferences — this is what the last few slices cost when they were skipped.

1. **Update `PROGRESS.md` in the same change**, not afterwards. It is the only place the
   reasoning survives.
2. **Have the work adversarially reviewed by agents that know nothing about it.** For every guard
   test, ask the reviewer to name a plausible bug the test claims to cover but would not catch —
   **and to demonstrate it by mutation rather than argue it.**
3. **Then ask a reviewer to audit the fixes, not the code.** This has been the most valuable
   reviewer five sessions running. Ask it for a *variant* that reaches the same end state, not a
   re-run of the original mutation.
4. **A later declaration of the same thing beats a `Contains`, and that one root cause has now
   defeated six guards.** Five in `WebPresentationTests`, and one in `AccountsContractTests` this
   slice: a check that a field name appeared in the server's source was satisfied by the same word
   occurring as a parameter name in another file. **Searching concatenated files for a word says
   nothing about where the word is.** The instruments are `EffectiveValue` and `RulesTargeting`;
   across two languages, read the structure — the keys of the literal, the attributes on the record.
5. **Placement in the cascade is part of aiming a mutation.** A mutation inserted *earlier* in the
   file than the rule it was meant to override reports as a survivor, because the cascade genuinely
   resolves the right way. Insert after the rule you are overriding, and check the numstat **and**
   the marker after the run as well as before.

   **And keep the arity.** Deleting `AND expires_at > ?` from a bound query leaves three parameters
   against two placeholders, so what goes red is a broken statement rather than the missing check.
   Bind a value that changes the answer instead.
6. **Look at the thing, do not only test it — and *generating* a proof is not looking at it.**
   Every visual bug in slice B was found by looking at a rendered page and none was visible to any
   test; this slice found two more the same way. **If you generate a page per palette, open every
   one of them.**
7. **Fix the class of defect, not the defect.** This slice's `CouldOverride` fix is the example to
   copy: the guard refused to answer about correct CSS because `border-radius` starts with
   `border-`, and the fix was to teach the helper which `border-` properties are actually part of
   the shorthand — then to re-check that the three real overrides it exists to catch still refuse,
   because that change *narrows* a guard.
8. **A guard that grows subjects without growing coverage is worth less each time.** If a test
   enumerates things, make it refuse a subject it never found. `UppercasedTextTests` did exactly
   that to this slice's new class, which is why a signed-in Power editor is now in its render list
   rather than the class being added to an exemption list.
9. **Check a rulebook citation before repeating it.** A comment cited "Elasticity", which is not
   a Power in this rulebook; `CLAUDE.md` already recorded that exact slip being made once before.

---

## Traps whatever you touch

- **The rulebook PDFs are in `docs/` and a worktree cannot see them.** `*.pdf` is gitignored, so
  they live in the main working directory only. A whole slice was worked on that mistake.
- **The printed page offset is a constant +3.** Each page prints its number twice interleaved, so
  a footer extracts as `151 5` for printed 15.
- **`data/rulebook/` is generated. Do not hand-edit it** — an edit is lost on the next run of
  `tools/RulebookExtractor` and hides whatever the extractor is doing wrong.
- **A whole-tree Qodana scan means nothing run in place.** The same commit reports 0 from
  `git archive HEAD | tar -x -C <tmp>` and 1471 from a built working directory, `.CSharpErrors`
  included, on files that compile. Export first.
- **Warnings are errors only under `ContinuousIntegrationBuild`**, so a green `dotnet test` does
  not cover it. Run
  `dotnet build --configuration Release -p:ContinuousIntegrationBuild=true` before pushing.
- **Before any destructive revert, run `git stash push -u -m pre-experiment`.** This was a caution
  twice and was ignored twice by people who had read it. `CLAUDE.md` carries the rule; there is no
  judgement call about whether a given revert is risky.
- **`perl -pi` silently edits nothing on this machine.** It exits 0, prints nothing, and leaves the
  file untouched — so a mutation "applied" that way looks exactly like a fix that holds. Use
  `sed -i` or the editor, and check `git diff --numstat` every time.
- **`sed` mangles Windows paths**: `\c` becomes a backspace and `\r` a carriage return, silently.
  Use the editor for anything containing a path — and for anything with heavy punctuation: a
  heredoc full of JavaScript failed to parse at all here, and one that did parse turned a
  `join(' ')` into a NUL byte, which made git treat the file as **binary** and show no diff in
  review.
- **The workflow files are CRLF.** An `Edit` matching an LF-terminated line silently finds nothing;
  append through `sed 's/$/\r/'` or match without trailing context.
- **A check that never ran looks exactly like one that passed.** Do not pipe a verification through
  `grep` and read empty output as green; assert on the positive.
- **The authority on CI is CI.** `gh pr checks <n> --watch` runs the same strict flags on Linux and
  needs no local daemon. Note that a Docker run piped through `grep` while the daemon happens to be
  stopped exits **0** with empty output, which a hurried reader takes for green — and that Git Bash
  rewrites container paths unless `MSYS_NO_PATHCONV=1` is set, which fails as
  `the working directory 'W:/' is invalid` and reads as a Docker fault.
- **A guard test that reads the shipped data cannot tell you the mechanism reads it too.** To pin a
  mechanism, drive it against a synthetic model that differs only in the field.

---

## Two more, carried from earlier slices

- **Measure against the thing you are replacing.** The extractor rewrite regressed 26 of Ch.2's
  Power entries that the *old* extractor got right, and the only reason that did not ship as a
  fix is that the old corpus was scored on the same check.
- **The mutation question has found a third to a half of new guards were theatre every time it
  has been asked.** Expect it, and budget for the second pass rather than treating it as bad news.
