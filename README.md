# Prowlers & Paragons Automation

A CLI character-creation wizard for the **Prowlers & Paragons Ultimate Edition** tabletop RPG by LakeSide Games, Inc.

The wizard walks players and GMs through the full creation process — tracking the Hero Point budget live, validating every choice against the system rules, and exporting a finished character sheet.

[![Build](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/build.yml/badge.svg)](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/build.yml)
[![Qodana](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/qodana_code_quality.yml/badge.svg)](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/qodana_code_quality.yml)

---

## Features

- **Interactive step-by-step wizard** covering all six creation phases, with back-navigation between steps
- **Live HP budget tracking** — colour-coded remaining points rendered before every step
- **125 powers** with baseline-rank derivation (Strike = Might baseline, Armor = ½ Toughness, Running = flat 3d)
- **23 pros and 28 cons**, including variable-cost variants (Charges, Area/Burst) and the Overkill/Weak "Brute Option" half-cost rule
- **53 flaws and 13 perks**, wired into Resolve and the HP budget
- **Validation engine** — errors for budget overruns, trait-cap violations and flaw-count breaches; warnings for rules still marked unverified
- **Dual export** — formatted `.txt` and structured `.json` written to `output/`

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | **10.0.100 or newer** (pinned in `global.json`, `rollForward: latestMinor`) |
| IDE | JetBrains Rider, Visual Studio, or VS Code + C# Dev Kit — optional |

If `dotnet build` fails with *"A compatible .NET SDK was not found"*, you are on an older SDK. Install .NET 10:

```bash
winget install --id Microsoft.DotNet.SDK.10
```

The source rulebook PDF is **not included** in this repository (copyright). Place it under `docs/P&P/` only if you need to re-run data extraction; `docs/**/*.pdf` is gitignored.

---

## Getting Started

```bash
git clone https://github.com/DorianSheiles/ProwlersAndParagonsAutomation.git
```

```bash
dotnet run
```

The wizard launches immediately — no configuration required. All rules data is already extracted and lives in `data/rules/`.

---

## Project Structure

```
ProwlersAndParagonsAutomation/
│
├── data/rules/                   # Extracted rules data (JSON) — no logic
│   ├── meta.json                 # System metadata and extraction provenance
│   ├── tiers.json                # 6 power tiers (Street Level → Iconic)
│   ├── creation_rules.json       # Creation sequence, packages, flaw rules
│   ├── abilities.json            # 6 core abilities
│   ├── talents.json              # 12 talents with linked abilities
│   ├── powers.json               # 125 powers with prerequisites and pro/con lists
│   ├── pros.json                 # 23 Power Pros
│   ├── cons.json                 # 28 Power Cons
│   ├── flaws.json                # 53 flaws
│   └── perks.json                # 13 perks
│
├── engine/                       # Rules logic — pure C#, zero Spectre.Console
│   ├── Models/                   # Immutable records mapping to the JSON schemas
│   ├── CharacterSheet.cs         # Mutable wizard state
│   ├── RulesRepository.cs        # Lazy JSON loader (snake_case, cached lookups)
│   ├── CostCalculator.cs         # HP cost logic for every trait type
│   ├── DerivedStatsCalculator.cs # Edge, Health, Resolve, baseline/effective rank
│   └── CharacterValidator.cs     # Validation with Error/Warning severity
│
├── cli/                          # Presentation only (Spectre.Console)
│   ├── Steps/                    # One class per wizard step, all IWizardStep
│   ├── Powers/                   # PowerBrowser (browse + search), ProConSelector
│   ├── Export/                   # CharacterSheetExporter (.txt + .json)
│   ├── WizardOrchestrator.cs     # Step sequencer, HP panel, back-navigation
│   └── HpBudgetDisplay.cs        # Persistent budget panel
│
├── output/                       # Generated character sheets (gitignored)
├── docs/RULES_EXTRACTION_GUIDE.md
└── Program.cs                    # Entry point
```

---

## Architecture

Three layers with a strict no-upward-dependency rule:

```
data/rules/   →   engine/   →   cli/
```

| Layer | Rule |
|---|---|
| `data/rules/` | JSON only. No logic lives here. |
| `engine/` | Pure C#, zero Spectre.Console references. `CostCalculator` and `CharacterValidator` are the authority on cost and validity. |
| `cli/` | Rendering and prompting only. **The CLI never tallies points itself.** |

---

## Wizard Steps

| # | Step | What happens |
|---|---|---|
| 1 | **Choose Tier** | Pick power level (Street Level → Iconic), optionally apply a starting package |
| 2 | **Buy Characteristics** | Ability and talent ranks; browse/search powers with pros & cons; flaws and perks |
| 3 | **Choose Gear** | Free-text mundane gear (no HP cost) |
| 4 | **Derived Stats** | Edge, Health and Resolve calculated and displayed |
| 5 | **Finishing Touches** | Name, appearance, motivation, quote, connections |
| 6 | **GM Review** | Full sheet display, validation results, export to `output/` |

Steps 1–5 offer a **← Back** option; GM Review is the terminus.

---

## Tiers

| Tier | Hero Points | Trait cap |
|---|---|---|
| Street Level | 75 | 8d |
| Low Level | 100 | 10d |
| Standard | 125 | 12d |
| High Level | 150 | 16d |
| Legendary | 175 | 20d |
| Iconic | 200 | 24d |

Iconic is stated as "200+" in the rulebook with no upper bound; the data uses a flat 200 and flags the entry `needs_review`.

---

## Rules Engine

### Power costs

```
HP = max(1, ⌈purchasedRanks × perRankMultiplier⌉ + Σ pro costs + Σ con discounts)
```

- `perRankMultiplier` halves to `0.5` when the **Overkill** or **Weak** con is applied (the "Brute Option")
- Variable-cost pros/cons store their variants in `cost_modifier_range`; the selected `VariantKey` picks the value
- Costs clamp to a minimum of 1 HP; `CharacterValidator` raises a warning when clamping occurred
- Con cost modifiers are stored as **negative** integers, pros as positive

### Baseline-rank powers

Some powers grant a free rank derived from an ability. Purchased ranks stack on top.

| Relationship | Example | Formula |
|---|---|---|
| `baseline_equal` | Strike, Evasion, Martial Arts | ability rank + purchased |
| `baseline_half` | Armor, Leaping | ⌈ability ÷ 2⌉ + purchased |
| `baseline_fixed` | Running | 3 + purchased |

### Perk costs

Flat perks cost their `cost`. Per-unit perks cost `cost_per_unit × units`.

### Derived statistics

| Stat | Formula |
|---|---|
| **Edge** | Perception + max(Agility, Intellect) + Danger Sense bonus + Lightning Reflexes bonus |
| **Health** | max( ⌈(Toughness + Might) ÷ 2⌉, ⌈(Toughness + Willpower) ÷ 2⌉ ) |
| **Resolve** | max(0, (TraitCap − highestRelevantRank) × 2) + Determination ranks + count of Condition/Plot Hook flaws |

*Highest relevant rank* is the maximum across all ability ranks and the effective ranks of powers whose `affects_resolve` is true. Talents are excluded, as are Movement and Sensory powers by default — `PowerModel.AffectsResolve` overrides that per power.

---

## Data Conventions

- All JSON keys are `snake_case`, matched by `JsonNamingPolicy.SnakeCaseLower`
- Entries that could not be fully verified against the PDF carry `"needs_review": true`; the validator surfaces these as `Warning` severity
- Powers with `cost_type: "special"` have a `null` `cost_per_rank` — `CostCalculator` must handle each explicitly or throw

### Current `needs_review` coverage

| File | Entries flagged |
|---|---|
| `powers.json` | 73 of 125 |
| `cons.json` | 10 of 28 |
| `pros.json` | 5 of 23 |
| `tiers.json` | 1 of 6 |

Specific known gaps:

- **Lightning Reflexes** — Edge bonus may be a flat +6 rather than +2 per rank
- **Iconic tier** — HP budget is "200+" with no stated upper bound
- **Determination** — the Resolve-per-rank ratio is unverified

Data coverage is limited to chapters 1–2 of the rulebook (Basics and Characters).

---

## Code Quality

Static analysis runs on every push and pull request.

- **.NET analyzers** at `latest-recommended`, with `EnforceCodeStyleInBuild`. Warnings become **errors** in CI (`ContinuousIntegrationBuild=true`) but stay warnings locally, so iteration is not blocked.
- **Qodana Community for .NET** (`jetbrains/qodana-cdnet:2026.2`, `qodana.recommended` profile) runs ReSharper inspections and publishes the report as a build artifact. SARIF upload to GitHub code scanning is attempted but non-fatal — this repo is private, so that path needs GitHub Advanced Security.
- Deliberate analyzer exceptions are documented inline in `.editorconfig` rather than left as bare suppressions.

> **Why the Community linter?** Since 2023.2 the *release* linters (`jetbrains/qodana-dotnet`) refuse to start without a Qodana Cloud `QODANA_TOKEN`, which would fail CI outright. `qodana-cdnet` needs no token or account. To upgrade: register at [qodana.cloud](https://qodana.cloud), add the project token as a `QODANA_TOKEN` repository secret (the workflow already passes it through), and change the `linter:` line in `qodana.yaml`.

Current Qodana baseline: **144 problems — 32 warnings, 112 notes, no errors.** All are style or dead-code notes; the warning bucket is dominated by "auto-property accessor is never used" on the JSON model records, where the setters exist for `System.Text.Json` to bind. Triaging these into a committed baseline is on the roadmap.

Reproduce the CI build locally:

```bash
dotnet build --configuration Release -p:ContinuousIntegrationBuild=true
```

Run Qodana locally (requires Docker and the [Qodana CLI](https://github.com/JetBrains/qodana-cli)):

```bash
qodana scan --show-report
```

To publish reports to Qodana Cloud, add a `QODANA_TOKEN` repository secret. Without it the scan still runs and results land in GitHub code scanning.

---

## Roadmap

- [ ] Unit tests for the engine layer
- [ ] Verify the remaining `needs_review` entries against the PDF
- [ ] Establish a Qodana baseline so only *new* problems fail CI
- [ ] Extract the remaining rulebook chapters

---

## Versioning

This project follows [Semantic Versioning](https://semver.org/) and [Conventional Commits](https://www.conventionalcommits.org/).

---

## License

Personal project. Prowlers & Paragons is © LakeSide Games, Inc. (2013–2021), by Leonard A. Pimentel and Sean Patrick Fannon. No rulebook content is redistributed here — only structured metadata derived from it for personal tooling use.
