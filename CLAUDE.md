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
```

No test suite exists yet (unit tests for the engine are on the roadmap).

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
| `CharacterValidator` | Returns `ValidationResult` with `Error`/`Warning` severity issues |

### Resolve formula

```
starting_resolve = max(0, (TraitCap − highestRelevantRank) × 2)
                 + Determination purchased ranks
                 + count of Condition/Plot Hook flaws
```

Highest relevant rank = max(all ability ranks, effective ranks of powers where `affects_resolve == true`). Talents excluded. Movement and Sensory category powers excluded by default; `PowerModel.AffectsResolve` overrides this per-power (`super_speed` is explicitly true; 11 non-combat Utility/Special powers are explicitly false).

### Power cost formula

```
HP = max(1, ⌈purchasedRanks × perRankMultiplier⌉ + Σ pro costs + Σ con discounts)
```

- `perRankMultiplier` is halved to `0.5` when the Overkill or Weak con is applied (the "Brute Option")
- Variable-cost pros/cons (e.g. Charges, Area/Burst) store their variants in `CostModifierRange`; `SelectedProCon.VariantKey` picks the right value
- `PowerCost()` clamps to minimum 1; `CharacterValidator` surfaces a warning if clamping occurred

### Baseline-rank powers

Powers with a `Prerequisite` record get a free rank derived from an ability:

| Relationship | Formula |
|---|---|
| `baseline_equal` | Total rank = ability rank + purchased |
| `baseline_half` | Total rank = ⌈ability / 2⌉ + purchased |
| `baseline_fixed` | Total rank = fixed value + purchased (Running = 3d) |

`RulesRepository.LoadPowers()` patches `FixedValue = 3` onto `baseline_fixed` powers post-deserialisation so `DerivedStatsCalculator` can consume it without string parsing.

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
- Entries that could not be fully verified from the PDF carry `"needs_review": true`; the validator surfaces these as `Warning` severity issues
- `pros.json` cost modifiers are positive integers; `cons.json` cost modifiers are **negative** integers
- Powers with `cost_type: "special"` have `null` for `cost_per_rank` — `CostCalculator` must handle each such power explicitly or throw

## Known data gaps (roadmap)

Back-navigation and JSON export are both **done** — do not re-implement them.

- Unit tests for the engine layer (nothing exists yet)
- Verify remaining `needs_review` entries against the PDF: 73/125 powers, 10/28 cons, 5/23 pros, 1/6 tiers
- Lightning Reflexes Edge bonus may be flat +6 rather than +2/rank (`needs_review`)
- Iconic tier HP budget is "200+" with no stated upper bound (`needs_review`)
- Determination power Resolve-per-rank ratio unverified (`needs_review`)
- Only chapters 1–2 of the rulebook are extracted
- Establish a Qodana baseline (`--baseline,qodana.sarif.json`) so only new problems fail CI
