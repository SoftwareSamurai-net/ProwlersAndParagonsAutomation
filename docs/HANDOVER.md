# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is only the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the slice it describes.** It is a note between
sessions, not documentation. (The previous one was deleted on exactly that instruction when the
replay shipped, and this replaces it.)

---

## Where things stand

Forty pull requests merged, the most recent being [#41](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/41). 3365 tests, zero warnings at CI strictness, a whole-tree Qodana scan at
zero, MIT in `LICENSE`, and the site live on Cloudflare Pages. The tool creates, prices,
validates, prints and exports characters through **four** front ends — the terminal wizard, the
browser app, `build --from character.json`, and an MCP server somebody connects to their own
Claude — and a visitor with no account can watch four real conversations build one at `/replay`.

The last slice built that replay. Its entry in `PROGRESS.md` and the "The replay" section of
`CLAUDE.md` carry the reasoning. The setup a stranger needs for the MCP server moved out of the
README into [`MCP-SETUP.md`](MCP-SETUP.md), which is held to the code by
`McpSetupDocumentationTests`.

**Nothing on the remaining list is a defect.** What follows is a judgement about which of them is
worth a slice, not a queue of bugs.

---

## The slice to build: pick one of these three

They are genuinely different kinds of work, and the right choice depends on what you want the
project to be next. Read the full entry in `PROGRESS.md` before starting any of them.

### A. Close the last four Heroes — `PROGRESS.md` item 1

Sixteen of the twenty published Heroes rebuild to exactly 125 Hero Points. Four sit at ±1, each
with a recorded reason. **The method that closed the other three is spent**: Vector, Talon and
Airmid were transcription faults, and all four transcriptions have since been read line by line
against the printed sheets and are faithful. So the remaining ±1 is in the **pricing model**, and
finding it needs a per-element cost breakdown compared against a hand-computed expectation from
the sheet — not another read of the page.

**Do not tune an ambiguous variant to force a zero.** T-Kay closes exactly if `Limited: only for
Telekinesis` is read as the milder grade, and the only thing recommending that reading is that it
produces the answer. The rulebook prints no rule mapping the words onto a grade. That is the trap
this item names, and it has been walked up to twice.

This is the highest-value slice if you want the engine's arithmetic proved further, and it is the
only one where a finding would change a number the tool reports.

### B. `search_powers` ranks ties alphabetically — `PROGRESS.md` item 4

The Power search is a word match, and when several Powers score the same it puts them in name
order under a caution calling them "the closest entries". **"Walks through walls" is the case to
reproduce**: twenty-two matches, of which twenty tie on a single word — eighteen on "through",
two on "walls" — so which of them a caller sees is alphabetical, with Phasing eleventh, where
anybody asking for eight rows never sees it.

**Weighting each word by how much of the rulebook uses it was implemented and reverted**, and
that is the finding rather than the fix: it sorted that query and broke "reads minds". Two
examples are not evidence. Closing this properly needs **a set of twenty or thirty descriptions
with expected answers, written from the Powers rather than from the scorer**, and then a scoring
change measured against them. Build the evidence first or do not start.

The replay's own cheap conversation is a live example of what the weakness costs: the model
concluded the rulebook had no Power for detecting a lie, from a search that had told it there
were more matches than it had shown. A better ranking would have put the answer on the first
page.

### C. The browser payload — `PROGRESS.md` item 5

27 MiB uncompressed, about a third of that over the wire, cached hard after the first visit.
**The site works and this is not a fault.** It is large because IL trimming is disabled, because
`RulesRepository` deserializes by reflection and a trimmed-away model property is a silently
empty rules set rather than a build error.

Two ways to close it, and the second **was tried and does not drop in**: a source-generated
`JsonSerializerContext` returns null for six collection properties declared non-null with an
`= []` initialiser, and `RulesLoadingTests.NoCollectionOnAnyLoadedRulesModelComesBackNull` is
what caught it. Anything done here has to keep that test green, and **the local toolchain cannot
verify any of it** — the ILLink task host crashes without the `wasm-tools` workload, which needs
elevation. CI can. "It built" is not evidence, because the failure is a runtime silence: whatever
is done needs a check that loads the published site and reads a rule out of it.

Lowest value of the three, and the one most likely to eat a day for nothing.

---

## Traps whichever you pick

- **The rulebook PDFs are in `docs/` and a worktree cannot see them.** `*.pdf` is gitignored, so
  they live in the main working directory only. `ls docs/*.pdf` from a worktree reports nothing,
  which reads as "there is no rulebook" and is wrong. A whole slice was worked on that mistake.
- **The printed page offset is a constant +3**, and each page prints its number twice,
  interleaved, so a footer extracts as `151 5` for printed 15. Decode carefully or cross-check
  against the table of contents on PDF 4. Every one of the twenty Hero citations was once ten
  pages out because nothing read them.
- **A whole-tree Qodana scan means nothing run in place.** The same commit reports 0 from
  `git archive HEAD | tar -x -C <tmp>` and 1471 from a built working directory, `.CSharpErrors`
  included, on files that compile. Export first.
- **Run the suite on Linux at CI strictness before pushing.** Warnings are only errors under
  `ContinuousIntegrationBuild`, so a local `dotnet test` passes over things CI fails on — that
  happened last slice, on a `CA1826` inside a new test. Four minutes closes it:

  ```bash
  docker run --rm -v "$(pwd -W):/src" -w //src mcr.microsoft.com/dotnet/sdk:10.0 bash -c "dotnet test --configuration Release -p:ContinuousIntegrationBuild=true"
  ```

  Export the tree first, or the Linux build leaves Linux artifacts in your `bin`/`obj`.
- **Commit before letting anything mutate files.** A mutation pass reverts with
  `git checkout -- .`, which takes uncommitted work with it. That cost two rounds of rework in
  the replay slice, both times on work written minutes earlier — and the hazard was already
  recorded from [#30](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/30) in
  its single-file form, so knowing about it is demonstrably not enough. **Also verify a mutation
  applied** (`git diff --numstat` non-empty) before believing a green result: a silently-failed
  edit and a passing test look exactly the same.

---

## How this project expects to be worked on

Not preferences — this is what the last few slices cost when they were skipped.

1. **Update `PROGRESS.md` in the same change**, not afterwards. It is the only place the
   reasoning survives.
2. **Have the work adversarially reviewed by agents that know nothing about it**, act on the
   findings, re-review, and only then merge. Ask each reviewer, for every guard test, to name a
   plausible bug the test claims to cover but would not catch — **and to demonstrate it by
   mutation rather than argue it.** Last slice that question found six of seven guards were
   theatre on the first pass.
3. **Ask a reviewer to audit the fixes, not just the code.** It has been the most valuable
   reviewer of the last three sessions every time: four of six fixes did not hold, then two of
   eight, then three more — one of them a hole inside a fix, where a test widened from one of
   three figures to three of four still missed the fourth.
4. **Look at the thing, do not only test it.** Render a component through bUnit into a static
   page against the real stylesheets and screenshot it with headless Chrome; there is no dev
   server in this workflow and starting one raises an approval dialogue. **Pass
   `--force-prefers-reduced-motion` or `--virtual-time-budget`**, or you will photograph panels
   mid-entry-animation and read washed-out styling as a palette fault. That happened, and was
   half-fixed as one before a second screenshot showed nothing was wrong.
5. **Check a rulebook citation before repeating it**, and read a changed transcript against the
   rulebook. The test suite holds the recorded characters to the engine and bans figures from the
   prose; **it cannot tell whether a recorded sentence about the rules is true.** One shipped for
   two commits asserting the rulebook has no Power for detecting a lie. It has one.
6. **Do not start a dev server.** It raises an approval dialogue that blocks unattended work.
   `dotnet build`, `dotnet test`, the Docker Qodana scan and the bUnit-plus-headless-Chrome
   screenshot route above all run without one.
