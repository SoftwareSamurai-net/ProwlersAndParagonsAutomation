# s7-gear: covering GearFormatter and pinning the JSON export's shape

An adversarial audit found two gaps: `sheets/GearFormatter.cs` had no tests at all, and
`CharacterSheetRenderer.RenderJson`'s structure was unpinned — nothing asserted its keys, its
nesting, or its types. This note records what was built to close both, the reasoning behind the
one design decision the task asked to be made deliberately, and the mutation table proving each
new check actually fails when the thing it guards breaks.

## What was covered

### `GearFormatter` — `tests/ProwlersAndParagonsAutomation.Tests/GearFormatterTests.cs`

Twelve tests, every one an `Assert.Equal` against the whole rendered line (never a `Contains` on
a fragment — see the class's own doc comment for why: this repository has already shipped a
test-side helper that turned `<b>Armor</b><span>8d</span>` into "Armor 8d" by stripping tags, the
exact string the assertions were looking for, produced by the exact bug they existed to find).

- Plain mundane gear prints just its name (the negative case everything else is a positive
  control for).
- A paired-but-uncustomised item still prints the "Two-Fisted pair" label and a real (zero) cost —
  the early-return guard is `{ IsCustomised: false, PairedUnderTwoFisted: false }`, both have to
  be false, and this is the one test that would catch a guard written as `!IsCustomised` alone.
- A flat feature prints its name and cost (`Silenced`).
- Both grades of both graded features in the rulebook (`accurate`/`very_accurate`,
  `powerful`/`very_powerful`), so `GradeName`'s underscore-splitting and capitalising is exercised
  on every real key it will ever see, not just the one-word case.
- Multiple features join with `", "` in selection order.
- Features, Pros and Cons print in that order — the expected string is built from the same
  `RulesRepository` lookups the formatter uses (the way `GearTests` derives its expected costs),
  so the rulebook's Pro/Con names are not hand-copied a second time as a second source of truth
  that could drift from the first.
- The class's own doc-comment example (`Jo Sticks (Upgraded, Two-Fisted pair) — 2 HP`) renders
  exactly.
- Cons can floor the line at 0 HP and it still names every Con that was bought — a formatter that
  stopped printing Cons once the floor was reached would hide what was actually bought.
- The rendered cost never credits the implicit Item Con (Ch.6: "every piece of gear has the Item
  Con" is a statement of what gear *is*, not a discount to claim).

### The JSON export shape — `tests/ProwlersAndParagonsAutomation.Tests/CharacterSheetJsonExportTests.cs`

Twenty-five tests, parsing the rendered string with `JsonNode.Parse` and asserting on the tree
(never string-matching the JSON text). They cover:

- The exact top-level key set, on both samples, so the shape cannot quietly depend on which
  sample produced it.
- The exact key set of every nested object (`meta`, `tier`, `package`, `hp_budget`, `derived`,
  `narrative`, `validation`) and a representative element of every array (`abilities`, `talents`,
  `source_groups`, `powers`, `perks`, `flaws`, `gear`, and the nested `pros`/`cons`/`features`
  arrays).
- JSON value kinds where a type could regress silently (a cost turned into a string, a null
  represented as `"null"` the string, a bool serialised wrong).
- A handful of values cross-checked against the same calculators the renderer calls
  (`CostCalculator.PowerCost`, `DerivedStatsCalculator.GetBaselineRank`/`GetEffectiveRank`/
  `GetRankAgainstPowers`/`CalculateEdge`/`CalculateHealth`/`CalculateResolve`) rather than
  hand-copied numbers, so a mutation to the arithmetic fails for the arithmetic reason and not
  because a hardcoded expectation went stale.
- Two branches neither sample exercises on its own: `tier`/`package` both null (a bare
  `new CharacterSheet()`), and the Villain's `lightning_reflexes` Power with no Source, which must
  surface under the plain `"POWERS"` fallback heading with a null `source` — the shape a sheet
  falls back to rather than dropping an unsourced Power.
- A real asymmetry worth pinning on its own: a gear item's `pros`/`cons` are bare id **strings**
  (`["armor_piercing"]`), unlike a Power's, which are `{id, variant_key}` **objects**. A
  "helpfully consistent" refactor would be a silent shape change for anything already parsing gear
  Pros as strings, so it has its own test.

## The snapshot-vs-keys decision

**Key sets, checked per object and per representative array element — not a whole-document
byte-for-byte snapshot.**

The document's *numbers* are real engine answers: a total, a derived stat, a per-Power cost. They
are expected to move whenever the two sample characters change, or when a priced rule changes
(and rules data, while described as "locked" in `CLAUDE.md`, is locked in the sense of "verified
against the book," not "never touched again" — a correction to a mistranscribed cost is exactly
the kind of change this project has made before). A byte-for-byte snapshot of the whole document
would fail on every one of those changes, for a reason that has nothing to do with the export's
*shape*. That is the same failure mode `CLAUDE.md` already warns about for the visual goldens:
"Regenerate ... only ever deliberately"; "a golden updated as a side effect of an unrelated change
is a regression signed off by nobody." A whole-document JSON snapshot trains whoever hits the
first failure to diff two walls of text, get lost, and re-save — which is worse than no test,
because it looks like coverage.

What the task actually asked to be pinned — "a renamed or dropped key ships silently" — is a
*shape* problem, not a *value* problem. So the tests assert the key set of every object and one
representative element of every array, plus the JSON value kind of fields where a type
regression would otherwise be invisible (a number rendered as a string still round-trips through
naive string-matching), and cross-check a handful of *values* directly against the engine calls
that produced them rather than against a frozen literal. This catches a rename, a drop, a type
change, and a value that stops agreeing with the engine — everything the task named — without
being brittle to a legitimate change in what the samples cost.

The trade this makes: a key-set assertion does not catch a value going *wrong in a way that keeps
its type* without a corresponding cross-check (e.g., a cost field silently reading the wrong
Power's price while remaining a well-typed integer). That gap is why several tests cross-check
specific values against direct calculator calls rather than stopping at "this is a number" — see
`APowersFieldsAgreeWithTheEngine`, `HpBudgetCarriesTotalSpentAndRemainingAndTheyAgreeWithTheEngine`,
and `DerivedCarriesEdgeHealthAndResolveAndTheyAgreeWithTheEngine`.

## Mutation table

Every mutation was applied on top of a committed baseline (the two new test files, committed
before any mutation), run to a red result with the message below, then reverted with `Edit`
(never a bare `git checkout --`), confirmed clean with `git diff --stat` on the touched file, and
the full suite re-run green afterward. The working tree was `git status` clean after all six.

| # | Property | File mutated | Mutation | Result |
|---|---|---|---|---|
| 1 | GearFormatter | `engine/CostCalculator.cs` | Dropped the `Math.Max(0, ...)` floor in `GearCost` | `ConsCanFloorTheCostAtZeroAndTheLineStillNamesThem` failed: expected `"... — 0 HP"`, got `"... — -7 HP"` |
| 2 | GearFormatter | `engine/CostCalculator.cs` | Simulated an automatic Item-Con credit (`... - 1` in `GearCost`) | 9 of 12 GearFormatter tests failed, every rendered cost off by 1 HP |
| 3 | GearFormatter | `sheets/GearFormatter.cs` | Charged a Two-Fisted pair twice (`cost * 2` when `PairedUnderTwoFisted`) | `TheDocCommentExampleRendersExactly` failed: expected `"... — 2 HP"`, got `"... — 4 HP"` |
| 4 | GearFormatter | `sheets/GearFormatter.cs` | Dropped the `", "` separator (`string.Join("", parts)`) | 4 tests failed with names run together, e.g. `"Rifle (UpgradedPenetratingCharges)"` — the exact "Armor8d" shape of bug this file's doc comment warns about |
| 5 | JSON export shape | `sheets/CharacterSheetRenderer.cs` | Renamed `"hp_budget"` to `"hpBudget"` | `TopLevelKeysAreExactlyThese` and `HpBudgetCarriesTotalSpentAndRemainingAndTheyAgreeWithTheEngine` both failed |
| 6 | JSON export shape | `sheets/CharacterSheetRenderer.cs` | Dropped the entire `"gear"` section from the export | 5 tests failed, including the positive-control `TheExportIsNonTriviallyPopulatedBeforeAnythingElseIsAsserted` and the top-level key-set test |

Full-suite result after every restore: two `Passed!` lines
(`ProwlersAndParagonsAutomation.Tests.dll`: 3771 passed; `ProwlersAndParagons.Web.Tests.dll`: 482
passed), zero failed, no `Catastrophic` in the log, `git status` clean.
