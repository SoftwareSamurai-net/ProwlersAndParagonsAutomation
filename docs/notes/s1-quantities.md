# s1-quantities: dedicated coverage for the six negative-quantity guards

## The gap

`engine/CharacterValidator.cs`'s `CheckQuantities` (around line 505) guards six
quantity-carrying fields against negative values, each with its own `< 0` branch:
ability rank, talent rank, a Power's purchased ranks, a Power's `Units`, a Perk's
`Units`, and a Pro-or-Con's `Units`.

An adversarial mutation audit weakened `< 0` to `< -1000` on the Power
purchased-ranks branch, and separately on the Perk-`Units` branch, and the whole
4221-test suite stayed green. The reason: `ValidationIssueStructureTests.Build`
has a case named `"negative quantities"` that builds **one sheet carrying all six
fields negative at once** (ability, talent, Power rank, Power `Units`, Perk
`Units`, Pro/Con `Units`). Every test that reached that sheet was a shared
meta-check — "some `NEGATIVE_RANK`/`NEGATIVE_UNITS` issue exists somewhere" (the
`EveryCodeTheValidatorCanReportIsProvokedBySomeCase` invariant test, and the
`ExpectedKinds` subject-kind table) — and that check is satisfied by whichever of
the other five sources is still guarded, regardless of which one branch a
mutation disabled. So five of the six branches had no test that would go red on
its own.

The sixth, the Pro-or-Con branch, already had an isolating test —
`ANegativeQuantityOnAProIsRefused` — which builds a sheet with *only* a negative
Pro `Units` and nothing else negative. That one was already sound; it is the
model the five new tests follow.

## What was added

Five new `[Fact]` tests in
`tests/ProwlersAndParagonsAutomation.Tests/ValidationIssueStructureTests.cs`,
each on an otherwise-legal sheet (`Legal()`) with exactly one field set
negative, asserting the specific `ValidationIssue`'s `Code`, `SubjectKind`,
`SubjectId` and `Value` via the file's existing `Issue()` helper
(`Assert.Single` on the matching code — so a second source of the same code
anywhere on the sheet would also fail the test), plus `Validate(sheet).IsValid
== false`:

- `ANegativeAbilityRankIsRefused` — `AbilityRanks["might"] = -5`
- `ANegativeTalentRankIsRefused` — `TalentRanks["academics"] = -3`
- `ANegativePowerPurchasedRanksIsRefused` — `SelectedPower("blast", -4)`
- `ANegativePowerUnitsIsRefused` — `SelectedPower("immunity", 0) { Units = -20 }`
- `ANegativePerkUnitsIsRefused` — `SelectedPerk("contacts", -1000)`

### On "the total cost is not silently reduced"

`CostCalculator` has no floor of its own beneath the per-field arithmetic
except where the rulebook actually specifies one (`Math.Max` on `AbilityCost`,
`TalentCost`, and the Power cost floor in `CostParts.Total()`). Reading those
floors mattered for what each test could honestly assert:

- **Ability / Talent rank**: `AbilityCost`/`TalentCost` clamp `chargeable` at
  `Math.Max(0, rank - covered)`, so a negative rank contributes exactly 0 either
  way — it cannot pay HP back through this path. The harm here is a nonsensical
  negative dice pool being recorded (and, absent the guard, printed) rather than
  a budget exploit, so no cost assertion was added for these two; they rely on
  the issue assertion and `IsValid == false` (which, for these two, `
  TRAIT_BELOW_MINIMUM` would also cover independently — see Mutation notes
  below).
- **Power purchased ranks**: `CostParts.Total()` is `Max(Minimum, Base + Flat)`
  with `Minimum = 0` once ranks are non-positive, so `PowerCost` for
  `SelectedPower("blast", -4)` is exactly **0**, not negative. Asserted directly
  (`Assert.Equal(0, _f.Costs.PowerCost(selection))`) as the positive control:
  the raw calculator really does answer 0, silently, and the guard — not the
  price — is what keeps it off a legal sheet.
- **Power `Units`**: every Power floors at `Minimum = 1` (`CostParts.Fixed`), so
  `SelectedPower("immunity", 0) { Units = -20 }` costs exactly **1** HP, not
  negative. Asserted the same way.
- **Perk `Units`**: `PerkCost`'s `per_unit` branch (`checked((perk.CostPerUnit ??
  1) * selection.Units)`) has **no floor at all** — this is the one branch that
  genuinely pays Hero Points back. `SelectedPerk("contacts", -1000)` (Contacts:
  `cost_per_unit = 1`) prices at **-1000**, asserted directly
  (`Assert.True(_f.Costs.PerkCost(perk) < 0)`), matching the harm CLAUDE.md
  names explicitly: "a negative `Units` on a per-unit Perk paid the character
  Hero Points and reported an over-budget character legal at exit 0."

One correction made along the way: the talent test originally used the id
`"athletics"`, copying a plausible-sounding name, but this rulebook's twelve
Talents are academics/charm/command/covert/investigation/medicine/professional/
science/streetwise/survival/technology/vehicles — no Athletics. `"athletics"`
also tripped `UNKNOWN_TALENT`, which didn't break the assertion (it filters by
code) but wasn't the clean single-field isolation intended, so it was corrected
to `"academics"` in a follow-up commit before the mutation pass.

## Mutation table

Each mutation was applied to the committed file (`engine/CharacterValidator.cs`),
run under `dotnet test --configuration Release
-p:ContinuousIntegrationBuild=true --filter
"FullyQualifiedName~ValidationIssueStructureTests"`, watched red, then restored
with `git checkout HEAD -- engine/CharacterValidator.cs` (safe: the file had no
other uncommitted changes at any point in this pass) and re-verified green.

| Branch | Mutation | Test(s) that caught it | Failure observed |
|---|---|---|---|
| Ability rank | `a.Value < 0` → `a.Value < -1000` | `ANegativeAbilityRankIsRefused` | `Assert.Single` found no `NEGATIVE_RANK`; only issue left on the sheet was `TRAIT_BELOW_MINIMUM` (Might is -5d) |
| Talent rank | `t.Value < 0` → `t.Value < -1000` | `ANegativeTalentRankIsRefused` | Same shape: only `TRAIT_BELOW_MINIMUM` (Academics is -3d) remained |
| Power purchased ranks | `p.PurchasedRanks < 0` → `p.PurchasedRanks < -1000` | `ANegativePowerPurchasedRanksIsRefused` | `Assert.Single` found **no issues at all** on the sheet — full isolation confirmed |
| Power `Units` | `p.Units < 0` → `p.Units < -1000` | `ANegativePowerUnitsIsRefused` | `Assert.Single` found no issues at all |
| Perk `Units` | `p.Units < 0` → `p.Units < -1000` | `ANegativePerkUnitsIsRefused` | `Assert.Single` found no issues at all — this is the exact mutation the audit named, confirmed to leave `Units = -1000` unguarded since `-1000` is not `< -1000` |
| Pro/Con `Units` | `m.Choice.Units < 0` → `m.Choice.Units < -1000` | `ANegativeQuantityOnAProIsRefused` (pre-existing) | `Assert.Single` found no issues at all |

No mutation survived. All six are now individually red-then-green verified.

### A finding worth flagging

The Ability and Talent branches' `IsValid == false` assertion is not, by
itself, proof that `NEGATIVE_RANK` fired — `TRAIT_BELOW_MINIMUM` independently
invalidates any Trait rank below 1d, and every negative number is below 1.
Removing the `NEGATIVE_RANK` guard for these two branches entirely would still
leave the sheet invalid for an unrelated reason. This does **not** weaken the
new tests: the `Issue(sheet, "NEGATIVE_RANK")` call (`Assert.Single` on that
exact code) is what catches the mutation, confirmed above, independent of the
`IsValid` assertion. It is worth recording, though, because it means the
`IsValid` half of these two tests is not itself a mutation-proof guard against
this specific branch — only the `Issue()` call is. The three quantity fields
with no equivalent floor rule (Power purchased ranks, Power `Units`, Perk
`Units`) do not have this overlap: their sheets came back with **zero** issues
under mutation, so `IsValid` alone would also have caught those three.

## Suite state

`dotnet test --configuration Release -p:ContinuousIntegrationBuild=true`:
two `Passed!` lines (`ProwlersAndParagonsAutomation.Tests.dll`: 3739,
`ProwlersAndParagons.Web.Tests.dll`: 482 — total 4221), 0 failures, no
`Catastrophic` in the log.

Test count delta, measured directly (swapped the pre-change file back in and
reran the engine test project, rather than inferred from source): the merge
base (`c8fe6e0`) runs 3734 in `ProwlersAndParagonsAutomation.Tests.dll`; this
branch runs 3739 — **+5**, one per new `[Fact]`. Total suite: 4216 → 4221.
