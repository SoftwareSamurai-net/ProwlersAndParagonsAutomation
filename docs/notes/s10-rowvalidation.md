# S10 — Validation on the row where the mistake is made

Branch `s10-rowvalidation`. Implements the last open item of Phase 3 in
`docs/FRONT-END-PLAN.md`: a broken rule shows up on the row that broke it, not only at the end
of the wizard.

## The design

`CharacterValidator.Validate()` already runs on every change and already tags each
`ValidationIssue` with `SubjectKind`, `SubjectId` and `OwnerId` — fields the engine's own doc
comment says exist "so a caller does not have to parse the message back into the facts it was
built from." Nothing needed inventing on the engine side; the gap was entirely on the display
side, where those three fields were read nowhere.

Two new pieces close it:

- **`web/Services/SheetFindings.cs`** — a static class, one method per row kind (`ForAbility`,
  `ForTalent`, `ForPower`, `ForFlaw`, `ForGear`, `ForPerk`). Each filters a `ValidationResult`'s
  `Issues` down to the ones that belong on that row. No arithmetic, no rule of its own — a tab
  that wants to know whether the *character* is legal still calls
  `CharacterSession.Validate()`, which asks the engine, same as `Review.razor` always has.
- **`web/Components/RowFinding.razor`** — takes the filtered list and draws it, visibly, under
  the row. Errors and warnings both print, each with a leading word ("Error"/"Warning") and a
  border that differs in *style* as well as colour (solid vs dashed).

`ChosenRow` gained a `Findings` parameter and its own markup moved into a `.chosen-row` wrapper
div, so a `RowFinding` can sit under the row's content and still be one `<li>`. `RankRow` was
left alone — `AbilitiesTab` and `TalentsTab` render `<RowFinding>` as a plain sibling after each
`<RankRow>`, the same way they already render the Pros/Cons summary paragraph after it.

Six call sites wire this up: `AbilitiesTab`, `TalentsTab`, `PowersTab`, `PerksTab`, `FlawsTab`,
and `Gear.razor` (gear wasn't named in the task's required scope list, but the wiring is the
same three lines everywhere else and costs nothing extra to include).

## Why `OwnerId`, not just `SubjectKind`/`SubjectId`

A Pro or Con "not applicable to the Power it is on" is filed by `CharacterValidator` at
`ValidationSubject.Character` — the finding is about the Pro or Con, not about the Power — with
the Power's id in `OwnerId` instead. `SheetFindings.ForPower` and `ForAbility` union the
`SubjectKind`/`SubjectId` match with an `OwnerId` match for exactly this reason; `ForTalent`
doesn't need the second half, because no generic Pro or Con in the rulebook names a Talent.

## Perks: no `ValidationSubject` case

`ValidationSubject` has `None, Character, Tier, Ability, Talent, Power, Gear, GearFeature, Flaw`
— no `Perk`. `CheckPerks` and the per-unit/negative-quantity checks in `CheckQuantities` file a
perk's findings at `ValidationSubject.Character` with the perk's own id as `SubjectId`, because
a sixth subject kind for one collection wasn't worth adding to the engine for this slice.
`SheetFindings.ForPerk` matches on `SubjectKind == Character && SubjectId == perkId`, restricted
to the three codes that actually use a perk id there (`PER_UNIT_WITHOUT_UNITS`,
`NEGATIVE_UNITS`, `UNKNOWN_PERK`) rather than trusting every Character-kind finding that happens
to carry a matching string — a Pro or Con id is drawn from a different collection and the type
system does not keep the two apart. Checked for real: `data/rules/perks.json`,
`pros.json` and `cons.json` share no id today, so the restriction is defence in depth rather
than a fix for an observed collision.

**If a future slice wants this cleaner, the honest fix is adding `ValidationSubject.Perk` in the
engine** — out of scope here per the task's instruction to touch `engine/` read-only and stop
rather than change it.

## Findings with no single row

`HP_BUDGET_EXCEEDED` (`ValidationSubject.Character`, no `OwnerId`) and `NO_TIER_SELECTED` /
`UNKNOWN_TIER` (`Character`/`Tier`) are never routed to any row by design — none of
`SheetFindings`' methods can match them, since none carries a matching `SubjectId` against an
Ability, Talent, Power, Flaw, Gear or Perk id. `FindingsWithNoSingleSubjectAreNeverRoutedToARow`
pins this. The budget strip (`HpBudgetBar`) already shows the running total and the over-budget
state continuously; the GM review step still prints every finding, row-routable or not, in full.
Nothing about this slice changes either of those two surfaces.

## Contrast

No new colour tokens. `.finding`'s background is `--panel` (not `--panel-sunk`) specifically so
the `--danger`/`--heading` text pairs it uses are ones `EveryScreenPairInUseHoldsItsContrastFloor`
already measures at the 4.5:1 floor in all four palettes:

| Pair | Already asserted at |
|---|---|
| `--danger` on `--panel` | 4.5:1 (all four palettes) |
| `--heading` on `--panel` | 4.5:1 (all four palettes) |
| `--ink` on `--panel` (the message body text, inherited) | 4.5:1 (all four palettes) |

`--danger-soft` was deliberately not used as a ground, per the task's own note that `--danger` on
`--danger-soft` measures 3.94:1 — under the floor. No new pair needed measuring, so no new row was
added to `EveryScreenPairInUseHoldsItsContrastFloor`.

## Errors vs. warnings, without colour

Three independent signals, so losing any one still leaves the distinction:

1. The word "Error" or "Warning" is printed in the markup (`.finding-kind`), read the same way by
   a screen reader and a sighted reader — not `aria-label`, not `sr-only`.
2. The border is `solid` for an error and `dashed` for a warning (`border-left` shorthand, so the
   whole declaration is one atomic change per state).
3. The colour differs too (`--danger` vs `--heading`), but is the one signal that WCAG 1.4.1
   explicitly forbids relying on alone — hence 1 and 2 existing independently of it.

`.finding-kind` deliberately does **not** carry `text-transform: uppercase`, even though most of
this app's labels do. `UppercasedTextTests.NothingSetInCapitalsCarriesARankOrACitation` renders a
fixed list of pages and requires every uppercased selector to actually appear on one of them —
none of that list's characters carry an active validation finding, so `.finding-kind` would have
failed the theory's own "found on no page" refusal. Since the two-word label never carries a rank
or a citation anyway, dropping the transform sidesteps the conflict rather than growing that
theory's rendered-page list for a selector it wasn't written to reach.

## Never hover-only

`RowFinding` deliberately does not reuse `Tooltip`'s or `OptionRow`/`RankRow`'s
`aria-describedby` + `sr-only` + hover-reveal plumbing, even though it was built for exactly this
kind of "extra sentence about a row." A finding renders unconditionally in the document flow
whenever `Issues` is non-empty — nothing to hover, nothing to focus to reveal it, nothing
dismissible. `ARowFindingIsOnScreenNotOnlyToAssistiveTechnology` in `WebPresentationTests` pins
both halves of that: the CSS never hides `.row-findings` (`display: grid`, not `none`), and the
component's own source never mentions `sr-only` or `aria-describedby`.

## Scope covered

Every item the task named, each with a routing test (`SheetFindingsTests`) and a rendered test
(`RowFindingRenderTests`):

- Trait over the Trait Cap (`TRAIT_ABOVE_CAP`) — Ability and Talent rows.
- Trait below its package floor (`TRAIT_BELOW_PACKAGE`) — Talent row.
- Pro/Con not applicable to the Power it's on (`PRO_NOT_APPLICABLE`) — routed via `OwnerId`.
- Duplicate Pro/Con (`DUPLICATE_PRO`) — routed via `OwnerId`.
- Per-unit purchase with no units (`PER_UNIT_WITHOUT_UNITS`) — Perk row.
- Power with ranks bought against `max_rank: 0` (`POWER_HAS_NO_RANK`) — Power row.
- Power missing a Source (`POWER_WITHOUT_SOURCE`, warning) — shown distinctly from an error.

Also wired, beyond the required list, because it cost nothing extra: gear (`ForGear`, on
`Gear.razor`'s "Carried" list) and flaws (`ForFlaw`, already required indirectly through the
`ChosenRow` change but exercised on its own tab too).

Not attempted: showing a Pro/Con-level finding on the individual Pro/Con row inside
`ProConPicker`'s own `ChosenList` (nested one level deeper than a Power or Ability row). The
task's own phrasing — "a Pro or Con not applicable to the Power it is on" — reads naturally as
belonging to the Power's row, which is what's implemented; adding a second, more granular display
inside `ProConPicker` (shared across three scopes: Power, Ability, Gear) would need its own
`Findings` plumbing threaded through a component with no `Validate()` access today, and the task's
required-scope wording doesn't ask for it.

## Files touched

- `web/Services/SheetFindings.cs` — new.
- `web/Components/RowFinding.razor` — new.
- `web/Components/ChosenRow.razor` — `Findings` parameter, markup restructured into `.chosen-row`.
- `web/Components/AbilitiesTab.razor`, `TalentsTab.razor`, `PowersTab.razor`, `PerksTab.razor`,
  `FlawsTab.razor` — wired to `SheetFindings`.
- `web/Pages/Gear.razor` — wired to `SheetFindings.ForGear`.
- `web/wwwroot/css/app.css` — `.row-findings`/`.finding`/`.finding-kind` rules; `.chosen > li`
  split into the `<li>` (padding/border) and `.chosen-row` (the flex layout that used to be on
  the `<li>` itself).
- `tests/ProwlersAndParagons.Web.Tests/SheetFindingsTests.cs` — new (routing, 8 tests).
- `tests/ProwlersAndParagons.Web.Tests/RowFindingRenderTests.cs` — new (rendering, 8 tests).
- `tests/ProwlersAndParagonsAutomation.Tests/WebPresentationTests.cs` — three `OwnedClasses`
  entries (`chosen-row`, `row-findings`, `finding`), plus two new CSS-only tests
  (`AFindingsErrorAndWarningStatesDifferInShapeNotOnlyColour`,
  `ARowFindingIsOnScreenNotOnlyToAssistiveTechnology`).

**Not touched**: `web/Services/CharacterSession.cs`, `web/Pages/ChooseTier.razor` (the other
stream's files), `engine/` (read-only — used to understand `ValidationIssue`'s existing fields,
never edited), `mcp/`, `worker/`, `tools/`, `data/`, `.claude/skills/`, `scripts/visual*`,
`tests/visual-goldens/`, `AccountsContractTests.cs`.

## Test count

Before: 3734 (`ProwlersAndParagonsAutomation.Tests`) + 482 (`ProwlersAndParagons.Web.Tests`) = 4216.
After: 3742 + 498 = 4240. Delta: +8 (`WebPresentationTests`: 6 `OwnedClasses` cases + 2 new CSS
tests) + 16 (`SheetFindingsTests` 8 + `RowFindingRenderTests` 8) = +24.

Both suites print exactly one `Passed!` line each on every run recorded below; no
`Catastrophic` anywhere in the output.

## Mutation table

Every mutation below: committed baseline → mutate → run → **red**, observed → `git checkout --
<file>` (working tree was clean before each mutation, so this is safe per CLAUDE.md) → re-run →
**green**.

| # | File | Mutation | Test(s) that caught it | Observed failure |
|---|---|---|---|---|
| 1 | `SheetFindings.cs` | `ForPower` drops its `ByOwner` half, keeping only `BySubject` | `AProNotApplicableToThePowerRoutesToThatPowerByOwnerId`, `ADuplicateProRoutesToItsPower` (unit); `AProNotApplicableToItsPowerShowsOnThatPowersRow`, `ADuplicateProShowsOnItsPowersRow` (render) | `Assert.Contains() Failure: Filter not matched in collection` — the `PRO_NOT_APPLICABLE`/`DUPLICATE_PRO` findings, filed at `Character` with `OwnerId="armor"`, stopped reaching Armor's row; only the unrelated `POWER_WITHOUT_SOURCE` warning (filed directly at `Power`) still showed. |
| 2 | `RowFinding.razor` | `IsError` inverted: `issue.Severity != ValidationSeverity.Error` | `ATraitOverTheCapShowsAnErrorOnItsOwnRow`, `APowerMissingItsSourceShowsAsAWarningNotAnErrorOnItsRow` | An error printed the word "Warning" and a warning's row carried no `.finding.warning` class at all — the two severities swapped their visible labels and their CSS classes together. |
| 3 | `AbilitiesTab.razor` | `SheetFindings.ForAbility(Validation, id)` → `SheetFindings.ForTalent(Validation, id)` | `ATraitOverTheCapShowsAnErrorOnItsOwnRow` | `Assert.NotNull() Failure: Value is null` — Might's row, over the Trait Cap, showed nothing at all, because `ForTalent` never matches an ability id. |
| 4 | `app.css` | `.row-findings { … display: none; … }` | `ARowFindingIsOnScreenNotOnlyToAssistiveTechnology` | `Expected: "grid" / Actual: "none"` — the exact regression this test exists to catch: a finding present in the DOM but invisible on screen, which every rendering assertion in the bUnit suite would have missed. |
| 5 | `app.css` | `.finding.warning { border-left: 3px solid var(--heading); }` (dropped `dashed`) | `AFindingsErrorAndWarningStatesDifferInShapeNotOnlyColour` | `Sub-string not found: "dashed"` — with this reverted, an error and a warning would differ by colour alone, which is exactly the WCAG 1.4.1 failure the test is written to catch. |

All five were genuinely observed red before being restored; none was reasoned about instead of run.
