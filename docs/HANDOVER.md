# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is only the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the slices it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

**3610 tests** — 3486 engine, 124 bUnit — zero warnings at CI strictness, MIT in `LICENSE`, the
site live on Cloudflare Pages. Four front ends on one engine assembly: the terminal wizard, the
browser app, `build --from character.json`, and an MCP server.

Two sessions back the work was **verified rather than trusted**, and that verification found
`data/rulebook/` materially wrong — every chapter opening scrambled, 135 empty sections, 83
doubled page numbers inside sentences, and every named character in Ch.8 missing. The extractor
was rebuilt; the corpus now regenerates byte-identical from `tools/RulebookExtractor/`.

The last session closed **A3** of the backlog below — the eight engine and validator findings, and
the test-file defect beside them. `PROGRESS.md` has the account.

**What is not done is the rest of what that audit turned up.** That is the backlog below, and it
is the reason this file exists.

---

## Slice A: the mutation-audit backlog — 25 open findings

**Where these came from.** Three agents, each told nothing about the work, were asked for every
guard test to name a plausible bug it claims to cover but would not catch, **and to demonstrate it
by mutation rather than argue it**. They ran 64 mutations; **38 survived**. Five of those were in
the rulebook corpus and are now fixed, and the eight of A3 are closed. The remaining 25 are below.

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

### A2 — browser and replay (13 open)

**The four `.stat-block` values and the Power ranks are genuinely pinned. Nothing else on a
replayed sheet is** — which is the exact bug class `ReplayRenderTests` was written for.

1. **Tier and Trait Cap in the masthead and colophon.** `web/Components/SheetView.razor`, `Tier` →
   `Session.Sheet.SelectedTierId`. Vera Nunn is Street Level and the visitor's sample is Standard,
   so her sheet prints `Standard · Trait Cap 12d`.
   `TheSheetAtTheEndCarriesTheRecordedCharactersOwnFigures` should cover this.
2. **The budget sub-line.** `web/Components/DerivedStatBlocks.razor`, same substitution, so
   `of 75` becomes `of 125`. That test's own doc comment explains it was narrowed to read `.value`
   precisely because the sub-line confused an earlier version — `.sub` is now the unguarded half.
3. **Every prose box.** `SheetView.razor`, `Sheet.Quote`/`Motivation`/`Appearance`/`Connections` →
   `Session.Sheet.*` (8 lines). The recorded character's sheet prints the visitor's words.
4. **`ShowBudget` dropped** from `web/Pages/ReplayConversation.razor` falls back to
   `Session.ShowBudget`, so the Conductor's sheet prints `Hero Points … of 125` against a budget
   Ch.9 says a Villain does not have. `AVillainIsNotCalledIllegalForHavingNoBudget` makes that
   claim for the verdict panel and never for the sheet.
5. Stand-in rank: `SheetView.razor`, `GetRankAgainstPowers(sp, Sheet)` → `Session.Sheet`. Latent —
   no recorded character currently has a rankless Power — but the `Against other Powers:` line has
   no replay assertion at all.
6. **The honesty scan never reads the character.** Putting a figure in
   `data/transcripts/vera-nunn.json`'s `Motivation` passes.
   `NoRecordedLineQuotesAFigureTheEngineIsSupposedToAnswer` reads `Title`, `Blurb` and `turn.Text`
   only — but `Name`, `Motivation`, `Quote`, `Appearance`, `Connections` and flaw
   `NarrativeDetail` are all printed by `SheetView`.
7. **The figure word-set is closed and omits the page's own labels.** "nineteen over … three to
   spare" passes; `ReplayVerdict` prints those as `Over by 19` and `Left 3`. The regex accepts
   `HP|hero points?|points?|edge|health|resolve|budget` — not *over*, *left*, *spare* or
   *remaining*, which is how anyone would naturally write it.
8. **Questions are counted by `?`.** `NoRecordedConversationAsksMoreThanThreeQuestions` sums `'?'`
   characters, so seven imperative demands ("Tell me the tier. Tell me whether she is one Power or
   several. …") pass — a questionnaire, which is the one thing the question policy exists to avoid.
9. **`ReplayLibrary.Find` case-sensitivity is unguarded.** `web/Services/ReplayLibrary.cs`,
   `OrdinalIgnoreCase` → `Ordinal`. Blazor routing is case-insensitive, so `/Replay/The-Conductor`
   reaches the page and answers "that address does not name one of the recorded conversations".
   This is the identical bug `TheVisitorsOwnBudgetBarIsNotShownOverARecordedCharacter` guards for
   `MainLayout`; the sibling call site is untested.
10. `print-color-adjust: exact` → `economy` (`app.css:892-893`) is unasserted. Browsers drop print
    backgrounds by default, so every heading bar prints white — the failure the rule's own comment
    describes.
11. **`.hp` asserts presence, not value.** `app.css:753-754`, `0.72rem/400` → `2.4rem/800` passes;
    `AHeroPointCostIsSetApartFromTheNumbersAPlayerRolls` checks only that `font-size:` appears. The
    neighbouring `ATraitSourceLineIsSetApartFromThePowersBelowIt` was explicitly hardened against
    this; `.hp` was not.
12. **The 7pt print floor only sees `pt`.** `app.css:919,921` → `0.3rem` and `4px` on
    `.stat-table td` and `.power-entry .statline`, the two densest blocks.
    `NothingOnPaperIsSetBelowSevenPoint` matches `font-size:\s*([\d.]+)pt` and its `Assert.NotEmpty`
    is satisfied by the other sizes.
13. **"A failed transcript fetch must not stop the app" has no test.** Removing the `try`/`catch`
    from `web/Program.cs` is green; one 404 then takes the whole character generator to a blank
    page. `RecordingsThatCouldNotBeLoadedAreNotReportedAsABadAddress` tests the *display* of a
    reason a fixture hands it, never the code that produces it.

**One weakness flagged but not demonstrated**: `SheetRenderTests.Rendered` replaces each tag with
`\n`, which `Collapse` then turns into a space — so `Assert.Contains("Also X ×3")` in
`ARepeatedProPrintsOnceWithItsCountOnBothSurfaces` could be satisfied by two adjacent elements.
This is the strip-tags trap `CLAUDE.md` already warns about, in a helper. The sheet and rank tests
read `TextContent` and are sound.

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
