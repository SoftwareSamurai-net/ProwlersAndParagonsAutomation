# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is only the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the slices it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

**3464 tests** — 3325 engine, 139 bUnit — zero warnings at CI strictness, MIT in `LICENSE`, the
site live on Cloudflare Pages. Four front ends on one engine assembly: the terminal wizard, the
browser app, `build --from character.json`, and an MCP server.

Two sessions back the work was **verifying the previous slice rather than trusting it**, which
found `data/rulebook/` materially wrong and led to the extractor being rebuilt. That is complete
and described in `PROGRESS.md`; the corpus now regenerates byte-identical from
`tools/RulebookExtractor/`.

The last session took **A2** of the backlog below — the browser and the replay — and closed all
thirteen, each by mutation. `PROGRESS.md` carries the reasoning.

**What is not done is the rest of what that audit turned up.** That is the backlog below, and it
is the reason this file exists.

---

## Slice A: the mutation-audit backlog — 20 open findings

**Where these came from.** Three agents, each told nothing about the work, were asked for every
guard test to name a plausible bug it claims to cover but would not catch, **and to demonstrate it
by mutation rather than argue it**. They ran 64 mutations; **38 survived**. Five of those were in
the rulebook corpus and were fixed then; the thirteen in **A2** are fixed now. The remaining 20 are below.

**Read this before starting.** These are *not* bugs in the product — every one is a **test that
does not hold what it claims to hold**. The mutation is the evidence. Each entry names the test,
the mutation, and why it passed. Every mutation below was confirmed applied with
`git diff --numstat` non-empty before the suite was run, and the suite stayed green at 3304 (the
count before the corpus work).

**Line numbers are as the agents reported them and predate `078b69d`. Verify before trusting.**

### A1 — MCP server (12 open)

The gaps cluster in three places: fields of a tool's JSON output that no test reads, the
documents, and the stdout-hygiene pair whose two halves share a blind spot rather than
complementing each other.

**The two worth doing first.**

1. **A `Console.WriteLine` inside a tool body reaches the client's stdout and neither guard sees
   it.** `CLAUDE.md` claims these two halves are complementary; they are not.
   - Mutation: in `mcp/CharacterTools.cs`, `SearchPowers`, add `Console` and `.WriteLine(...)` on
     two separate lines.
   - `NothingWritesToStandardOutput` scans each line for the token `Console.` — split across two
     lines, neither line contains it. `CLAUDE.md` names "a spelling split across two lines" as
     precisely why the runtime half exists.
   - `TheBuiltProgramSpeaksNothingButTheProtocol` only sends `initialize`,
     `notifications/initialized` and `tools/list`, so it **never enters a tool body**.
   - Confirmed live against the built binary: line 2 of the JSON-RPC stream was
     `searching for turns invisible`.
   - The same write in `ListOptions` **was** caught — only because `ReadEverything` calls it at
     startup. Same for `engine/FileSystemRulesSource.ReadAllText`. So the runtime test covers the
     startup path and nothing else. **The fix is to drive at least one tool call in the runtime
     test**, not to add another regex.
2. **`search_powers` can be widened back to substring matching with the suite green.** This is the
   bug the method's own docstring calls "worse than no match".
   - Mutation: in `Mentions`, add `if (text.Contains(term, StringComparison.OrdinalIgnoreCase)) return true;`
   - Confirmed live: `search_powers("she bakes bread in the city")` returns **Plasticity**,
     `matched_on: ["name"]`, `matched_terms: ["city"]`.
   - Every existing search test is a positive assertion or a negative on one query whose words
     happen not to be substrings. Note this is the same weakness `PROGRESS.md` item 4 is about,
     approached from the test side rather than the ranking side.

**The rest, roughly by value.**

3. `power_detail`'s `cost_variants` can be set to `null` — green. It is the only place a caller
   learns the accepted variant keys, and `check_character` refuses a `per_rank_variable` Power
   without one. `PowerDetailOffersOnlyTheOptionsTheRulebookAllows` loops all 141 Powers and reads
   only the pros/cons arrays.
4. Eight more `power_detail` fields are constants-safe: `category`→`"Offensive"`,
   `stat_line`→`power.Name`, `rank_type`→`"ranked"`, `cost_type`→`"flat"`, `max_rank`→`99`,
   `unit`→`"things"`, `description`→`"A Power."`, `source_ref`→`"Ch.2 p.1"`. Only `range`
   (asserted for `force_field` alone), `ranks_purchasable` and the pro/con lists are read.
5. `list_options` likewise: gear_features `cost_type`→`"flat"` and `grades`→`null` (which kills
   the two graded features' keys), flaws `flaw_type`, perks `unit`, talents
   `ordinary_human_rank`→`0`. `TheCatalogueNumbersAreTheRulesOwn` reads a hand-picked subset per
   category.
6. `Judgement.Issues` can drop `owner_id` and `options` — green. `AnIssueCarriesTheFactsToRepairFrom`
   reads only `subject_kind`/`subject_id`/`value`/`limit`. `HeadlessBuildTests` covers both fields
   for the *build* command's report; the MCP copy — which `CLAUDE.md` says duplicates it on
   purpose — has no equivalent, and `QUESTION-POLICY.md` tells the assistant all six are there.
7. `mcp/QUESTION-POLICY.md`'s schema **prose** is unchecked outside the fenced block. Changing
   `{ "Id", "VariantKey", "Units" }` to `{ "ProId", "GradeKey", "Count" }` in the surrounding text
   left the suite green; only the ```` ```jsonc ```` block goes through the strict reader.
8. **The Claude Desktop half of `docs/MCP-SETUP.md` is entirely unchecked.** Breaking both JSON
   blocks' `command` paths left the suite green. `EveryShellPublishesToThePathItThenRegisters`
   only regexes `claude mcp add`, and `EveryPublishCommandNamesTheProjectThatProducesTheRegisteredBinary`
   only requires the AssemblyName to appear *somewhere* in the guide. A Desktop user follows the
   guide and gets nothing.
9. The startup diagnostic the guide's first troubleshooting bullet points at can be deleted:
   `Console.Error.WriteLine($"Prowlers & Paragons MCP server, rules from '{rulesDirectory}'.")`
   in `mcp/Program.cs`. The guide says it is there; nothing asserts it.
10. `CharacterServer.Instructions` reduces to keyword bait —
    `"creation_guide check_character the engine decides"` passes.
    `TheServerSaysWhoDecidesBeforeAnythingIsCalled` greps three substrings. Note the asymmetry:
    `EveryToolSaysWhatItIsFor` enforces a 60-character floor on tool descriptions; the
    instructions, the model's only pre-call guidance, have none.
11. `ReadEverything` no longer reading the embedded guide (delete `_ = QuestionPolicy.Text.Length;`)
    is green. The claim that a csproj edit dropping the resource becomes a *startup* failure rather
    than an empty first conversation is asserted nowhere.
12. The search limit clamp's upper bound is free: `Math.Clamp(limit, 1, 25)` → `(limit, 1, 400)`
    passes. `ALimitOutsideTheRangeIsBroughtInsideIt` passes `int.MaxValue` but queries `"armor"`,
    which matches fewer than 25 Powers, so the upper bound is never exercised.

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
  way rather than testing the source for a `try`.
### A3 — engine and validator (8 open)

1. **A validation issue can name the wrong kind of thing.** `CharacterValidator`,
   `DUPLICATE_PRO`/`DUPLICATE_CON` `SubjectKind = ValidationSubject.Character` →
   `ValidationSubject.Ability` — green. That is verbatim the failure
   `ValidationIssueStructureTests.EachCodeReportsTheKindOfThingItIsAbout` exists to prevent. It
   passes because `ExpectedKinds` does `TryGetValue(...) continue` on codes it omits, and **18 of
   the validator's 45 codes are absent from that table**: `TRAIT_ABOVE_CAP`,
   `TRAIT_BELOW_MINIMUM`, `TRAIT_BELOW_PACKAGE`, `NEGATIVE_RANK`, `NEGATIVE_UNITS`,
   `UNKNOWN_PRO`/`CON`, `DUPLICATE_PRO`/`CON`, `PRO`/`CON_NOT_APPLICABLE`,
   `PRO`/`CON_VARIANT_NOT_CHOSEN`, `UNKNOWN_SOURCE`, `UNKNOWN_TRAIT_SOURCE`. **A lookup that
   silently skips what it omits is the shape to fix, not the eighteen entries.**
2. **An issue can offer options of the wrong kind entirely.** `POWER_WITHOUT_SOURCE`,
   `Options = SourceIds` → the Ability ids — green. A repair loop is told "this Power has no
   Source; pick one of: agility, intellect, might…" and loops for ever on `UNKNOWN_SOURCE`.
   `EveryOptionOfferedIsOneTheRulesAccept` asks only whether each string is an id of *anything*,
   as a union over ten collections. Same hole for `UNKNOWN_PERK`, `UNKNOWN_FLAW`,
   `UNKNOWN_ABILITY`, `UNKNOWN_TALENT`.
3. **The "every code is provoked" guarantee is spelling-shaped.** Clean A/B on the same
   unreachable check added to `CheckFlawCount`: code `"TOO_MANY_CONNECTIONS"` → **red**; code
   `"TOOMANYCONNECTIONS"` → **green**. The regex is `"([A-Z]+(?:_[A-Z]+)+)"`, so any code without
   an underscore is exempt from the requirement to add a case — and therefore from every
   structural invariant downstream.
4. **A Power can be deleted from a sample character.** Removing
   `new SelectedPower("stun", 6) { SourceId = "tech" }` from `SampleCharacters.Hero()` is green.
   The budget assertion is one-sided (`spent <= budget`) and "fills every section" asserts
   non-empty, so it catches emptying a section but not the trimming its doc comment claims.
5. **A player-facing Power description can be replaced with unrelated prose.** `powers.json:232`,
   Armor's description → `"A quiet afternoon in the garden, with tea."` — green. Presence check
   only; `PowerDescriptionTests` catches only claims that contradict rank scaling. Partly by
   design, but the description is what a player reads while choosing.
6. **An applicability caveat can say the opposite of the rulebook.** `pros.json:176` (penetrating)
   → `"Applies to absolutely any Power at all, no conditions."` — green.
   `AnUncheckableConstraintIsCarriedAsACaveat` asserts non-blank and a trailing full stop. Since
   the whole design is that the caveat is carried to the player *instead of* being enforced, its
   content is the entire deliverable and it is unpinned.
7. The pickers' documented "rules-file order" has no test: adding `.Reverse()` to `ProsFor` is
   green.
8. The defensive intersection in `GradesFor` is behaviourally dead — replacing it with
   `return allowance.Grades.ToList();` is green. Weakest of the eight and honestly caveated: with
   the shipped data the two are equivalent, because `EveryOwnTextAllowanceResolvesAndCitesItsPrintedText`
   guarantees every recorded grade is priced. But the comment says the intersection stops a rules
   file "inventing a key", and nothing drives it with a synthetic allowance that would.

**One test-file defect, not mutated**: `SampleCharacterTests.cs:152` calls `Sample("Hero")` while
`Sample` (line 33) is `which == "hero" ? Hero() : Villain()` — ordinal and case-sensitive, so it
silently builds the **Villain**. The test passes either way; the Hero export path is untested there.

### What held up

Worth knowing so it is not re-audited. The engine-facing judge in the MCP server
(`EveryFigureReportedIsTheEnginesOwnAnswer`, the verdict and null-figure tests), the rules-location
logic, the strict-reading discipline, `ReplayTurn` speaker attribution, the `Armor8d` spacing bug in
both its spellings, and the whole own-text/repeatable mechanism in `ProConApplicabilityTests` all
went red under every mutation aimed at them. **The two tests that build synthetic `PowerModel` /
`ProModel` values caught everything thrown at them** — that approach works, and the gap is that it
was applied to one mechanism and nothing else.

---

## Slice B: the visual redesign

Unchanged from the previous handover and still not started. **Chosen after looking at
[pnpready.com](https://www.pnpready.com/)** — another unofficial companion app for this game,
further along in scope and, more to the point, better presented. Its scope is not worth chasing;
its presentation is.

In rising order of cost:

1. **Two typefaces with distinct jobs.** A condensed uppercase display face for headings and a
   separate body face. This app uses the system stack throughout. Biggest single difference. Cost:
   two self-hosted files and `--font-display` / `--font-body` tokens — no component changes, since
   a component may no more name a font than a colour. Mind the CSP's `font-src` and the print block.
2. **Small uppercase tracked labels carry the structure of a long form**, rather than borders doing
   it. `SheetSection`'s centred heading in a bar is right on *paper*; the editors on screen are a
   different problem.
3. **Choices as a card grid, not a full-width list.** Six tiers as six cards, each with its
   consequence on one line. CSS on `OptionList`, not new markup.
4. **Every derived stat shows its formula** under the figure. `StatBlock` already takes a `Sub`.
   Teaching the rule is the point of running the real engine in the browser.
5. **The rank descriptor beside the rank** — `1d Impaired`, `1d Clueless`. **The data already
   exists**: `rank_guide` on every entry in `abilities.json` and `talents.json`, already read into
   `AbilityModel.RankGuide` / `TalentModel.RankGuide` and locked by
   `RulesFileCoverageTests.ThePrintedRankTablesAreWhatTheRulebookPrints`. No front end shows them.
   **Do not go extracting it.**
6. **A filter box on `OptionList`.** The only item here that came from somebody actually using the
   thing: scrolling the long lists is annoying, and Powers alone is 141 entries. `OptionList` is
   one component, so this belongs in it once rather than in five tabs. Treat it as a requirement,
   not a nice-to-have.

**The palette is in scope and it is the part that fails quietly.** `theme.css`'s contrast figures
are measured and commented, and the print block at the bottom restates *every* token — one left out
keeps its screen value through the cascade, which is exactly how a Villain sheet once printed as a
full-bleed ink dump. Re-measure rather than eyeball, and re-proof the PDF.

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
