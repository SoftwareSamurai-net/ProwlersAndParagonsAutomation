# s6-rulesdata: pinning four unpinned areas of data/rules/ to the book

Branch `s6-rulesdata`. Scope: the four gaps named in the task — talents' `linked_ability`,
49 of 53 flaw `type` values, the 27 baseline-power prerequisites, and the graded Cons'
variant-to-value mapping — none of which had a transcription holding `data/rules/*.json`
to `data/rulebook/*.json` before this slice.

## What was added

Four transcription files (`tests/ProwlersAndParagonsAutomation.Tests/Canonical*.cs`) and four
test files, following `CanonicalPowers.cs` / `PowerDataTests.cs`'s existing shape:

| Area | Transcription | Tests | Entries |
|---|---|---|---|
| Flaw `type` | `CanonicalFlawTypes.cs` | `FlawTypeTests.cs` | 53/53 |
| Baseline-power prerequisite | `CanonicalPowerBaselines.cs` | `PowerBaselineTests.cs` | 27/27 |
| Graded Con variant → value | `CanonicalGradedCons.cs` | `GradedConTests.cs` | 4/4 |
| Talent `linked_ability` | `CanonicalTalentLinkedAbilities.cs` | `TalentLinkedAbilityTests.cs` | 12/12 |

Every test file asserts coverage first (the transcription names exactly the ids the rules file
has, nothing missing and nothing extra) before asserting content, per the task's discipline.

`data/rules/*.json` was **not edited** — every value transcribed from the book agreed with the
shipped data. See "Disagreements found" below for the one caveat.

## Sourcing, per area

### Flaw `type` (53 entries, `data/rulebook/ch02-characters.json` pp.56-60)

Every Flaw's own printed entry either states its type in so many words — "This Flaw is a
Plot Hook that grants you 1 extra point of Resolve..." / "...is a Condition that grants..." —
or says neither, which is `regular` per the FLAWS section's own definition (p.55: a Hero
brings a Flaw into play to earn Resolve, up to once per scene, "unless" it is one of the two
named exceptions). Five printed headings pair two Flaws (Compulsion/Severe Compulsion,
Heavy/Very Heavy, Reaction/Severe Reaction, Requirement/Severe Requirement, Unlucky/Jinx);
each half's type is transcribed from its own clause in the shared entry. All 53 transcribed
values matched `data/rules/flaws.json` exactly — no disagreement.

Also added: `FlawTypeTests.EachFlawsResolveContributionMatchesItsType`, a per-flaw Theory
(53 cases) that adds each flaw to a sheet and asserts `DerivedStatsCalculator.CalculateResolve`
moves by +1 for `condition`/`plot_hook`/`plot_hook_and_condition` and by +0 for `regular`. This
is the "type wired to consequence" test the task asked for, beyond the pre-existing
`DerivedStatsCalculatorTests.ConditionAndPlotHookFlawsEachGrantOneResolve`, which only sampled
two flaws by filtering on the field already under test rather than checking each id against
an independently transcribed expectation.

### Baseline-power prerequisite (27 entries, `data/rulebook/ch02-characters.json` pp.22-43)

Read each baseline Power's own stat line ("Self • Baseline Rank (X) • ...") and, where the
relationship isn't obvious from the stat line alone (`baseline_selected_trait`,
`baseline_greater_of`), its body text. All 27 — including all 16 Super Senses options, verified
individually rather than assumed from the shared cost table — matched `data/rules/powers.json`
exactly: relationship, ability, `fixed_value` (Running = 3), and `powers` (Strike →
`martial_arts`). No disagreement.

### Graded Cons (4 entries: Conditional, Limited, Shutdown, Side Effect; pp.49-53)

Each carries one printed grading sentence — "This is a -1 Con if [mild], a -2 Con if
[moderate], or a -4 Con if [severe]" — transcribed variant-key-to-value. All four matched
`data/rules/cons.json` exactly. No disagreement.

### Talent `linked_ability` (12 entries) — the one real finding

**The rulebook does not print a Talent→Ability table.** Every Ability entry (pp.17-18) and
every Talent entry (p.18-19) was read looking for one. The only explicit pairing anywhere in
the book is inside the Agility entry (p.17): "you can substitute half your Agility for your
Covert when making challenge rolls to hide, move quietly, or avoid detection" — which backs
exactly one of the twelve `linked_ability` values, Covert : agility. The Intellect entry (p.17)
states a second substitution rule, but it is generic to *any* Talent ("substitute half your
Intellect for any Talent when making challenge rolls to determine what you know about
something"), not a pairing to one. Challenge rolls (Ch.1 p.9) use "the Trait that applies to
whatever your character is doing" — one Trait at a time — with no Ability+Talent combination
rule that would require a fixed pairing to exist.

`git log -p -- data/rules/talents.json` shows all twelve `linked_ability` values present,
unchanged, in the very first commit that added rules data (`0601c24`), with no comment
recording where they came from. They are read by the CLI, the browser and the MCP server
(`AbilitiesTab.razor`, `TalentsTab.razor`, `BuyCharacteristicsStep.cs`, `CharacterTools.cs`) to
group Talents under an Ability heading in a display — a UI grouping decision, not a mechanical
rule with a Resolve-formula-style consequence the way flaw `type` has.

**This is reported rather than "fixed"** because there is nothing in the book to fix it
against — eleven of the twelve values are this project's own editorial call, and the task's own
instruction ("if you cannot find a value in `data/rulebook/`, say so per-entry rather than
falling back to the JSON") is exactly this case. `CanonicalTalentLinkedAbilities.cs` records
this per-entry (`BookSourced: false` for eleven, `true` for Covert with its page), and
`TalentLinkedAbilityTests` still pins the current value of all twelve as a regression snapshot
— so a future change is still caught and has to be a deliberate decision — while
`ExactlyOneLinkedAbilityIsStatedInTheRulebook` keeps the finding itself from rotting silently
if nobody re-reads this.

## Disagreements found

**None.** All 53 + 27 + 4 = 84 book-grounded values (plus the one book-grounded talent pairing)
matched `data/rules/*.json` exactly on first transcription. `PrebuiltHeroTests` was re-run and
no published Hero moved, as expected since nothing in `data/rules/` changed.

## Mutation table

Each area was committed clean first (`5d92142`), then mutated in place, run, watched red,
restored via `Edit` (never `git checkout --`), and re-run green. `git diff --stat` confirmed a
byte-identical restore after every one before moving to the next.

| # | Area | Mutation | File : line | Failing test(s) | Failure message |
|---|---|---|---|---|---|
| 1 | Flaw type | `obligation` flaw_type `plot_hook` → `regular` | `data/rules/flaws.json` | `FlawTypeTests.FlawTypeMatchesItsRulebookEntry("obligation")`, `FlawTypeTests.EachFlawsResolveContributionMatchesItsType("obligation")` | `Expected: "plot_hook" / Actual: "regular"`; Resolve `Expected: 25 / Actual: 24` |
| 2 | Talent linked_ability | `academics` linked_ability `intellect` → `might` | `data/rules/talents.json` | `TalentLinkedAbilityTests.LinkedAbilityMatchesTheTranscription("academics")` | `Expected: "intellect" / Actual: "might"` |
| 3 | Baseline power | `armor` prerequisite ability `toughness` → `willpower` | `data/rules/powers.json` | `PowerBaselineTests.BaselinePrerequisiteMatchesItsRulebookStatLine("armor")` | `Expected: "toughness" / Actual: "willpower"` |
| 4 | Graded Con | `limited` grades reversed (`somewhat_limited`/`severely_limited` swapped: -1↔-4) | `data/rules/cons.json` | `GradedConTests.EachGradeMapsToTheValuePrintedForIt("limited")` | Collection mismatch: `somewhat_limited` expected -1, actual -4 (and reverse for `severely_limited`) |

Mutation 4 also confirms the gap this slice closes: the pre-existing
`RulesDataTests.GradedConsRunFromMinusOneToMinusFour` sorts the three values before comparing,
so it stayed green through the exact swap that failed the new per-variant test.

Mutation 1 was first tried against `enemy` — one of the four flaws already pinned by
`RulesDataTests.TheNamedPlotHooksAndConditionsAreClassifiedThatWay` — and switched to
`obligation` (previously unpinned) so the drill demonstrates new coverage rather than
re-exercising an existing guard.

## Verification

```
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
```

Two `Passed!` lines (no `Catastrophic`):

```
Passed!  - Failed: 0, Passed: 3893, Skipped: 0, Total: 3893  - ProwlersAndParagonsAutomation.Tests.dll
Passed!  - Failed: 0, Passed:  482, Skipped: 0, Total:  482  - ProwlersAndParagons.Web.Tests.dll
```

`ProwlersAndParagonsAutomation.Tests` gained 159 test cases (108 + 29 + 6 + 16 across the four
new Theory-driven files), from 3734 before this slice.

## Files touched

- `tests/ProwlersAndParagonsAutomation.Tests/CanonicalFlawTypes.cs` (new)
- `tests/ProwlersAndParagonsAutomation.Tests/CanonicalPowerBaselines.cs` (new)
- `tests/ProwlersAndParagonsAutomation.Tests/CanonicalGradedCons.cs` (new)
- `tests/ProwlersAndParagonsAutomation.Tests/CanonicalTalentLinkedAbilities.cs` (new)
- `tests/ProwlersAndParagonsAutomation.Tests/FlawTypeTests.cs` (new)
- `tests/ProwlersAndParagonsAutomation.Tests/PowerBaselineTests.cs` (new)
- `tests/ProwlersAndParagonsAutomation.Tests/GradedConTests.cs` (new)
- `tests/ProwlersAndParagonsAutomation.Tests/TalentLinkedAbilityTests.cs` (new)
- `docs/notes/s6-rulesdata.md` (this file, new)

No file under `data/rules/`, `mcp/`, `.claude/skills/`, `web/`, `worker/`, `tools/`,
`scripts/`, `tests/visual-goldens/`, or `tests/ProwlersAndParagons.Web.Tests/` was changed.
