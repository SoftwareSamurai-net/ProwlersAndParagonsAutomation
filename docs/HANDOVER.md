# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is only the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the slices it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

**3826 tests** — 3641 engine, 185 bUnit — zero warnings at CI strictness, MIT in `LICENSE`, the
site live on Cloudflare Pages. Four front ends on one engine assembly: the terminal wizard, the
browser app, `build --from character.json`, and an MCP server.

Earlier the work was **verified rather than trusted**, and that verification found
`data/rulebook/` materially wrong — every chapter opening scrambled, 135 empty sections, 83
doubled page numbers inside sentences, and every named character in Ch.8 missing. The extractor
was rebuilt; the corpus now regenerates byte-identical from `tools/RulebookExtractor/`.

**A1, A2 and A3 were then worked in parallel, one branch each, and are reconciled here.** Each
closed its own findings by mutation and was reviewed twice or more; `PROGRESS.md` carries three
completed entries, one per sub-slice. Because they ran concurrently, each branch's own copy of
this file claimed to be "the last session" and counted only its own closures — those counts are
superseded by the reconciled ones below, and the merge changed no test and no source file, only
the four documents the three branches all wrote to.

**Slice B, the visual redesign, is done too, and its section has been deleted from this file
on the instruction that opened it.** All six items shipped — two self-hosted faces, labels
carrying the structure of the long forms, the tier choice as a card grid, the rule under each
derived figure, the rulebook's word beside each rank, and one filter box in the component all
five pickable lists share. `PROGRESS.md` has the entry, including the two things it did not
close.

**So both slices this file was written for are finished.** What remains is `PROGRESS.md` items
4 and 5 — the Power search's tie ordering and the browser payload — neither of which is a
defect, and both of which have a spent approach recorded against them. Read that file before
picking either. **This file has served its purpose; delete it when you pick up the next slice
rather than adding to it.**

---

## Slice A: the mutation-audit backlog — **CLOSED**, all 33

**Where these came from.** Three agents, each told nothing about the work, were asked for every
guard test to name a plausible bug it claims to cover but would not catch, **and to demonstrate it
by mutation rather than argue it**. They ran 64 mutations; **38 survived**. Five of those were in
the rulebook corpus and were fixed then. The other 33 split into A1 (12), A2 (13) and A3 (8), and
**all three are now closed** — the sections below record what each one cost rather than what is
left to do.

**Kept as a record, not a queue.** None of the 33 was a bug in the product — every one was a
**test that did not hold what it claimed to hold**, and the mutation was the evidence. What is
worth carrying out of it is the shape of the mistakes, which is what the three sections below
are for.

### A1 — MCP server: closed, all twelve

Done, with each fix demonstrated by re-applying its mutation and confirming the suite goes red.
The reasoning is in `PROGRESS.md`, in the completed entry named after this slice — including what
it did **not** close, and the two documents whose claims were wrong rather than merely unasserted
(`CLAUDE.md` on the stdout guards being complementary, and both `CLAUDE.md` and `Mentions`' own
summary naming a Power the rulebook does not have).

**Three rounds of adversarial review then found twenty more, most of them inside the fixes.** All
closed. **Do not skip that step, and do not stop at one round** — the third review, run
on the fixes for the second review's findings, still found six, two of them the same defect in a new
spelling. It was worth more than the original slice every time, and the "audit the fixes, not the
code" framing found things the general reviewer did not.

**Seven things worth carrying forward** — written for A2 and A3 while they were still open, and
kept because they generalise past this backlog.

1. **A guard that names its fields will be missing the next one.** Nine of the twelve were "a JSON
   field no test reads", and the fix that worked was one assertion over the whole payload *with
   the key set asserted exactly, both ways*. Asserting only that nothing unexpected is present
   catches an added field and never a removed one.
2. **A runtime test is only worth the paths it drives.** The stdout pair looked complementary and
   was not, because one half never entered a tool body — and after that was fixed, it still only
   drove the *happy path*, so every refusal branch stayed invisible. Ask of any end-to-end test
   which arguments it actually sends.
3. **A marker that proves a path ran must be unproducible by any other path.** `character_sheet`'s
   was the character's name, which the test itself sends and the judge echoes — so serving the
   judge under the sheet's name passed. A2's replay tests are full of this shape: the sheet prints
   figures that *could* come from either character.
4. **A source-reading guard is worth what its instrument can see, and a token is not a read.** A
   token check was defeated by `ListOptions(Categories[0])` keeping the word `Categories`, then by a
   comment mentioning the token, then by `nameof(...)`. Where a runtime property exists, drive it —
   a theory over `RulesRepository.DataFileNames` replaced one grep, and injecting the guide the way
   the clock is already injected replaced the other. **A2 has several CSS guards of exactly this
   kind**, and items 10–12 there are all "the rule is asserted by looking at the text of it".
5. **A phrase assertion cannot survive a "not" in front of the phrase.** Two prose contracts were
   pinned by required phrases and both were *inverted* while keeping every one of them. Where the
   content of a sentence is the deliverable, assert the sentence. This bears directly on A2's
   items 6 and 7, which are both about what a recorded line is allowed to say.
6. **`Zip` truncates in silence.** An emptied array runs every loop zero times and fires no
   assertion inside it. Assert the count first.
7. **Reuse the case list, do not copy it.** `ValidationIssueStructureTests.CaseNames` and `Build`
   are `internal` now precisely so the second consumer cannot go stale independently. A3's first
   finding is about that same table skipping what it omits.

### A2 — browser and replay — **CLOSED**

All thirteen are fixed, plus the flagged-but-undemonstrated weakness in
`SheetRenderTests.Rendered`. Each was closed by re-applying the mutation, confirming red,
reverting and confirming green. The reasoning is in `PROGRESS.md` under "Thirteen guards on the
browser and the replay that were not guarding anything"; two things from it are worth carrying
forward:

- **The five sheet substitutions are not guarded by naming five more fields.** The test renders
  the same character twice — once held by the session, once passed as a parameter over a
  different session character — and asserts the two pages are identical. A new field on the
  sheet is covered the day it is added, which a list of assertions would not be. Anything that
  legitimately comes from outside the character (`ShowBudget`) has to be passed explicitly in
  both renderings, or it hides every illegitimate difference behind a legitimate one.
- **`ReplayLibrary.LoadAsync` exists because a `try`/`catch` in top-level statements is
  unreachable.** If anything else in `Program.cs` ever acquires a guarantee, move it out the same
  way rather than testing the source for a `try`. It takes the `HttpClient` rather than a fetch
  for a reason: with a fetch parameter, `Program.cs` can do the fetching itself and hand the
  guard a delegate that cannot fail, which passes every test and restores the bug exactly.
- **A CSS guard must read every declaration that targets the class, not the first rule it
  finds.** Three of these were defeated the same way — a more specific rule further down, or a
  second declaration in the same block, both of which win the cascade while the first is what
  the test read. `WebPresentationTests.RulesTargeting` is the shape to copy.
- **A guard's precondition rots silently.** Two here had stopped being able to bite: a pool that
  crossed tiers on one row of four because only one recorded character is not Standard, and an
  assertion comparing two characters that both had zero Perks and zero gear. Neither failed;
  they just stopped meaning anything. When writing a guard, assert that the thing it compares
  actually differs.

### A3 — engine and validator — **CLOSED**

All eight, plus the test-file defect. Each fix was demonstrated by re-applying the mutation and
confirming red, then reverting and confirming green. See the completed entry in `PROGRESS.md` for
what was done and, more usefully, for the three similarity framings measured against the Power
descriptions that turned out **not** to be rules — do not re-derive them.

Two things from it worth carrying into A1 and A2:

- **The first three findings were one bug in three places**: a lookup that silently skips what it
  omits turns its own omissions into exemptions nobody chose. `ExpectedKinds` probed with
  `TryGetValue`, the option check was a union over ten collections, and the code scan required an
  underscore. Look for that shape in the remaining findings before treating one as specific.
- **The code scan is now driven against a synthetic source**, not the real validator — reading the
  shipped file cannot tell a pattern that finds every code from one that finds every code somebody
  happened to spell with an underscore. That is the same lesson as the `PowerModel` tests, applied
  to a regex.

### What held up

Worth knowing so it is not re-audited. The engine-facing judge in the MCP server
(`EveryFigureReportedIsTheEnginesOwnAnswer`, the verdict and null-figure tests), the rules-location
logic, the strict-reading discipline, `ReplayTurn` speaker attribution, the `Armor8d` spacing bug in
both its spellings, and the whole own-text/repeatable mechanism in `ProConApplicabilityTests` all
went red under every mutation aimed at them. **The two tests that build synthetic `PowerModel` /
`ProModel` values caught everything thrown at them** — that approach works, and the gap is that it
was applied to one mechanism and nothing else.

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
- **Commit before letting anything mutate files.** A mutation pass reverts with
  `git checkout -- .`, which takes uncommitted work with it. That has cost rework twice.
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

## How this project expects to be worked on

Not preferences — this is what the last few slices cost when they were skipped.

1. **Update `PROGRESS.md` in the same change**, not afterwards.
2. **Have the work adversarially reviewed by agents that know nothing about it**, act on the
   findings, re-review, and only then merge. Ask each reviewer, for every guard test, to name a
   plausible bug it claims to cover but would not catch — **and to demonstrate it by mutation
   rather than argue it.** That question has found a third to a half of new guards were theatre
   every time it has been asked; last time it was 38 of 64.
3. **Ask a reviewer to audit the fixes, not just the code.** The most valuable reviewer of the last
   four sessions, every time. Last session it found the corpus fix still scrambling 16 pages by a
   new mechanism — the same defect class the fix was for.
4. **Measure against the thing you are replacing.** The extractor rewrite regressed 26 of Ch.2's
   Power entries that the *old* extractor got right, and the only reason that did not ship as a
   fix is that the old corpus was scored on the same check. A rewrite is not automatically better
   than what it replaces.
5. **Look at the thing, do not only test it.** Render a component through bUnit into a static page
   against the real stylesheets and screenshot with headless Chrome. Pass
   `--virtual-time-budget=3000` or `--force-prefers-reduced-motion`, or you will photograph panels
   mid-entry-animation and read washed-out styling as a palette fault. That happened and was
   half-fixed as one.
6. **Do not start a dev server.** It raises an approval dialogue that blocks unattended work.
   `dotnet build`, `dotnet test`, the Docker Qodana scan and the screenshot route above all run
   without one.
7. **Check a rulebook citation before repeating it.** The suite holds recorded characters to the
   engine and bans figures from transcript prose; **it cannot tell whether a recorded sentence
   about the rules is true.**
