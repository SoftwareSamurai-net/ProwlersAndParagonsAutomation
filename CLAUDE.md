# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the wizard
dotnet run

# Build without running
dotnet build

# Run with a specific project file
dotnet run --project ProwlersAndParagonsAutomation.csproj

# Reproduce the CI build — analyzer warnings become errors
dotnet build --configuration Release -p:ContinuousIntegrationBuild=true

# Run the tests (also run in CI, with the same strict flags)
dotnet test
```

## Tests

`tests/ProwlersAndParagonsAutomation.Tests` (xunit.v3). Two things to know before touching it:

- The suite loads the **real** `data/rules/*.json` via `RulesFixture`, not hand-built fixtures. That is deliberate: its main job is to catch a rules file drifting away from the rulebook.
- `CanonicalPowers.cs` is the transcribed Range/Rank/Cost of all 141 Powers, and `RulesDataTests` holds the tier/ability/talent/pro/con/perk/flaw values. **Do not "fix" a failing test by editing these to match the code** — they are the rulebook. Check the page named in the entry's `source_ref` and fix whichever side is wrong.
- `PrebuiltHeroes.cs` transcribes the 20 published Heroes from Ch.8 and `PrebuiltHeroTests` rebuilds each one, asserting the printed Edge, Health and Resolve. Same rule applies: those numbers are the authors', not ours. They are the only tests that check the rules as *applied* rather than as transcribed, so a failure there usually means a rule was misread, not that a number is stale.
- Hero Point **totals** are deliberately not asserted against the 125-point budgets. The sheets use Power-specific Pros this project has not extracted (see the roadmap), which push a rebuild a few HP under, and some carry Ch.6 gear costs, which are not modelled at all. `HeroAbilityTalentAndPerkCostsAreExact` asserts the part that is fully determined.

The root `.csproj` sits at the repository root, so it carries `<Compile Remove="tests\**" />`; without it the default `**/*.cs` glob pulls the test sources into the main project.

The project targets **.NET 10** (`global.json` pins SDK `10.0.100` with `latestMinor` rollForward). The 9.x SDK cannot build it; install with `winget install --id Microsoft.DotNet.SDK.10`.

## Static analysis

- .NET analyzers run at `AnalysisLevel=latest-recommended` with `EnforceCodeStyleInBuild`. `TreatWarningsAsErrors` is conditional on `ContinuousIntegrationBuild`, so local builds stay warning-only while CI is strict. **Keep the CI build at zero warnings.**
- Deliberate rule exceptions live in `.editorconfig` with an inline rationale — CA1305/CA1304 are off because all formatted output is human-facing terminal/sheet text, and CA1822 is a suggestion so `CostCalculator`/`DerivedStatsCalculator` keep a uniform instance API. Add rationale when adding an exception; do not add bare suppressions.
- Qodana (`qodana.yaml`, `jetbrains/qodana-cdnet:2026.2`) runs ReSharper inspections in `.github/workflows/qodana_code_quality.yml`. Two non-obvious constraints: the `dotnet.solution` key is required (without it Qodana finds no project and reports nothing), and the **Community** linter (`cdnet`) is deliberate — the release linter (`dotnet`) refuses to start without a Qodana Cloud `QODANA_TOKEN`. Verified locally: 144 problems, 0 errors, exit 0.
- Qodana's "redundant nullable warning suppression" hits on `JsonValue.Create(...)!` in `CharacterSheetExporter` are false positives — the method returns `JsonValue?`, so removing `!` breaks the warnings-as-errors build. Leave them.
- `data/rules/*.json` is copied to the output directory by the csproj, so a published build works without the repo checked out.

## Architecture

Three layers with a strict no-upward-dependency rule:

```
data/rules/   →   engine/   →   cli/
```

- **`data/rules/`** — JSON files only. No logic. All rules data extracted from the P&P Ultimate Edition PDF lives here.
- **`engine/`** — Pure C#, zero Spectre.Console references. `CostCalculator` and `CharacterValidator` are the authority on HP costs and validity. The CLI never tallies points itself.
- **`cli/`** — Presentation only. Uses Spectre.Console for all rendering. Each wizard step implements `IWizardStep` and receives `CharacterSheet`, `RulesRepository`, `CostCalculator`, and `DerivedStatsCalculator` via `Execute()`.

### Key engine types

| Type | Role |
|---|---|
| `CharacterSheet` | Mutable wizard state — all purchases accumulate here |
| `RulesRepository` | Lazy JSON loader with snake_case deserialization and cached lookup dictionaries |
| `CostCalculator` | HP cost logic — `PowerCost()`, `PerkCost()`, `TotalCost()`; all methods are pure |
| `DerivedStatsCalculator` | Edge, Health, Resolve, baseline/effective rank calculations |
| `PowerFormatter` (cli) | Renders a Power's rulebook stat line (`Self · Baseline Rank (½ Toughness) · 1 HP per rank`) so wizard output can be checked against the book |
| `CharacterValidator` | Returns `ValidationResult` with `Error`/`Warning` severity issues |

### Derived stats

```
Edge    = (DangerSense effective rank, else Perception) + max(Agility, Intellect)
          + 6 if Lightning Reflexes
          then floored at SuperSpeed effective rank × 3
Health  = max(⌈(Toughness + Might) / 2⌉, ⌈(Toughness + Willpower) / 2⌉)
Resolve = max(0, (TraitCap − highestRelevantRank) × 2)
          + Determination Resolve bought (5 HP each)
          + count of Condition/Plot Hook flaws
```

Three Edge details are easy to get wrong and were all bugs at one point: Danger Sense **replaces** Perception rather than adding to it, Lightning Reflexes is a **flat +6** with no rank, and Super Speed is missing from most summaries. All three are verified against Ch.2/Ch.5.

Halves always round **up** — the rulebook has a global rule for this (Ch.1, "Half").

Highest relevant rank = max(all ability ranks, effective ranks of powers where `affects_resolve == true`). Talents excluded. Movement and Sensory category powers excluded by default; `PowerModel.AffectsResolve` overrides this per-power (`super_speed` is explicitly true; 11 non-combat Utility/Special powers are explicitly false). This reproduces the rulebook's list of Resolve-exempt powers exactly — do not "fix" it by naming powers individually.

### Power cost formula

`CostCalculator.PowerCost()` branches on `cost_type`. Only `per_rank` and `per_rank_variable` consume purchased ranks:

| `cost_type` | Cost |
|---|---|
| `per_rank` | `⌈ranks × cost_per_rank⌉` (`cost_per_rank` ∈ 0.5, 1, 2, 3) |
| `flat` | `cost_flat` |
| `per_unit` | `cost_per_unit × Units` |
| `per_rank_variable` | rate from `cost_variants[CostVariantKey]` |
| `flat_variable` | total from `cost_variants[CostVariantKey]` |
| `special` | `boost` mirrors the nominated Trait's rate; `summoning` is ⌈Threat / 2⌉ per rank |

Then pro costs and con discounts are summed in (cons are negative in the data).

- **Overkill/Weak reduce the rate by 1 HP per rank, floored at 0.5** — *not* a ×0.5 multiplier. Ch.2: "reduces a Power's base cost by 1 Hero Point per rank (or changes its base cost from 1 Hero Point per rank to 1 Hero Point per 2 ranks)." A previous version halved the rate, which mispriced every 2 and 3 HP/rank power. The "Brute Option" is the separate Ch.1 rule for applying Overkill to Might.
- **The minimum is per rank, not per power.** Ch.2: "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons." See `MinimumRankedCost`. Specialty is the sole 0 HP power.
- Variable-cost pros/cons (e.g. Charges, Area/Burst) store their variants in `CostModifierRange`; `SelectedProCon.VariantKey` picks the right value.
- `max_rank == 0` means no ranks are purchasable — either the power has no rank or it is bought flat/per-unit. The validator errors if ranks were bought anyway.

### Baseline-rank powers

27 powers derive a free baseline rank from another Trait; purchased ranks stack on top.

| Relationship | Formula |
|---|---|
| `baseline_equal` | trait rank + purchased |
| `baseline_half` | ⌈trait / 2⌉ + purchased |
| `baseline_fixed` | `fixed_value` + purchased (Running = 3d) |
| `baseline_greater_of` | max(`ability`, effective ranks of `powers`) + purchased — Strike = Might or Martial Arts |
| `baseline_selected_trait` | rank of `SelectedPower.BaselineTraitId` + purchased — Boost, Expertise |

`fixed_value` now lives in the JSON, so the old `RulesRepository.LoadPowers()` post-load patch is gone. `baseline_selected_trait` powers need `BaselineTraitId` on the selection; without it the baseline is 0 and the validator raises an error. Boost's *cost* also comes from that nomination.

### Perk cost formula

Flat-cost perks: pay `Cost` HP. Per-unit perks: pay `CostPerUnit × Units` HP. `SelectedPerk(PerkId, Units, NarrativeDetail?)` — Units is always 1 for flat perks.

### Wizard flow

`WizardOrchestrator.Run()` iterates `_steps` in order, rendering the HP budget panel before each step:

1. `ChooseTierStep` — selects tier and optional package
2. `BuyCharacteristicsStep` — abilities, talents, powers (via `PowerBrowser` + `ProConSelector`), flaws
3. `ChooseGearStep` — free-text gear, no HP cost
4. `CalculateDerivedStep` — displays computed Edge and Health
5. `FinishingTouchesStep` — name, appearance, motivation, quote, connections
6. `GmReviewStep` — full validation, sheet display, `.txt` **and** `.json` export to `output/`

Steps 1–5 render a Back/Continue prompt (`WizardOrchestrator.PromptNavigation`); `gm_review` is the terminus and breaks the loop.

## JSON data conventions

- All JSON keys use `snake_case` (matched by `JsonNamingPolicy.SnakeCaseLower`)
- `pros.json` cost modifiers are positive integers; `cons.json` cost modifiers are **negative** integers
- Powers with `cost_type: "special"` have no numeric cost — `CostCalculator.PerRankRate` must handle each such power by id or throw
- **`powers.json` tracks verification per field, not with a boolean.** Every entry has `verified_fields` (any of `range`, `rank_type`, `cost`, `prerequisite`, `description`, `pros_cons`) and a `source_ref` page reference. `PowerModel.MechanicsVerified` requires the first four; `NeedsReview` is its inverse. The old single flag drifted badly — 27 entries were unflagged while their costs were wrong — so when you change a mechanical field, update `verified_fields` to match what you actually checked.
- **Power `description` values are original text written from the rulebook entry, never rulebook prose.** Do not paste rulebook text in: only structured metadata plus this project's own explanations are redistributable here. Descriptions exist so a player can tell what they are choosing and what resists it, and they must agree with the mechanics beside them — `PowerDescriptionTests` fails a rankless power whose description claims per-rank scaling, which is how the original set went wrong on 44 of the 46 rankless powers.
- `powers.json` has **141** entries. Form, Transformation and Super Senses are single Powers in the rulebook but each of their options is bought separately at its own cost, so each option is its own entry.

## Known data gaps (roadmap)

Back-navigation, JSON export and engine unit tests are all **done** — do not re-implement them.

**All rules data in chapters 1–2 is verified and locked by tests.** Every one of the 141 power entries (range, rank type, cost, baseline, description) plus all tiers, abilities, talents, pros, cons, perks and flaws has been checked against the book, and no `needs_review` flag remains anywhere in `data/rules/`. Do not re-verify these, and do not reintroduce a uniform `cost_per_rank`. Settled questions: Lightning Reflexes is a **flat +6** Edge bonus on a flat 3 HP unranked Power; Determination is **5 HP per 1 Resolve** with no rank; the Iconic tier's "200+" is explicitly a bare minimum, so it is GM discretion rather than missing data; Overkill/Weak are a **−1 HP per rank** rate reduction.

Still open:

- `available_pros` / `available_cons` are project guesses; the rulebook lists Pros and Cons inside each Power's entry and those have not been extracted. This is the largest remaining data gap, and it is what stops the published Heroes reconciling to exactly 125 HP.
- Gear costs (Ch.6 Gear Limits) are not modelled — `ChooseGearStep` is free text with no HP cost.
- **Sources** are not modelled. They set the stand-in rank for the 46 rankless (`rank_type: default`) powers — Toughness or Willpower depending on Source — which matters when one Power targets another
- Only chapters 1–2 of the rulebook are extracted
- Establish a Qodana baseline (`--baseline,qodana.sarif.json`) so only new problems fail CI
