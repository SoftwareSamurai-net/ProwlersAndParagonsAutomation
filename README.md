# Prowlers & Paragons Automation

A CLI character creation wizard for the **Prowlers & Paragons Ultimate Edition** tabletop RPG by LakeSide Games, Inc.

The wizard guides players and GMs through the full character creation process — tracking Hero Point budgets, validating choices against system rules, and exporting a completed character sheet.

---

## Features

- 🎲 **Interactive step-by-step wizard** covering all six creation phases
- 💰 **Live HP budget tracking** — colour-coded remaining points at every step
- ⚡ **125 powers** with baseline-rank calculations (Strike = Might baseline, Armor = ½ Toughness, etc.)
- 🔧 **Pros & Cons** — including variable-cost variants (Charges, Area/Burst, etc.)
- 🛡️ **Validation engine** — errors for budget overruns, trait cap violations, flaw count; warnings for unverified rules
- 📄 **Character sheet export** — formatted `.txt` to `output/`

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 9.x |
| JetBrains Rider (or any C# IDE) | Any recent |

The source PDF (`Prowlers_&_Paragons_Ultimate_Edition.pdf`) is **not included** in this repository (copyright). Place it at `docs/P&P/` if you need to re-run data extraction.

---

## Getting Started

```bash
# Clone
git clone https://github.com/DorianSheiles/ProwlersAndParagonsAutomation.git
cd ProwlersAndParagonsAutomation

# Run
dotnet run
```

The wizard launches immediately — no configuration required. All rules data is already extracted and lives in `data/rules/`.

---

## Project Structure

```
ProwlersAndParagonsAutomation/
│
├── data/rules/                  # Extracted rules data (JSON)
│   ├── meta.json                # System metadata
│   ├── tiers.json               # 6 power tiers (Street Level → Iconic)
│   ├── creation_rules.json      # Creation sequence, packages, flaw rules
│   ├── abilities.json           # 6 core abilities
│   ├── talents.json             # 12 talents with linked abilities
│   ├── powers.json              # 125 powers with prerequisites, pros/cons
│   ├── pros.json                # 23 Power Pros
│   └── cons.json                # 28 Power Cons
│
├── engine/                      # Rules logic (no UI concerns)
│   ├── Models/                  # Immutable records mapping to JSON schemas
│   │   ├── TierModel.cs
│   │   ├── AbilityModel.cs
│   │   ├── TalentModel.cs
│   │   ├── PowerModel.cs
│   │   ├── PowerPrerequisiteModel.cs
│   │   ├── ProModel.cs
│   │   ├── ConModel.cs
│   │   └── CreationRulesModel.cs
│   ├── CharacterSheet.cs        # Mutable wizard state
│   ├── RulesRepository.cs       # Lazy JSON loader (snake_case, cached)
│   ├── CostCalculator.cs        # HP cost logic for all trait types
│   ├── DerivedStatsCalculator.cs# Edge, Health, baseline/effective rank
│   └── CharacterValidator.cs    # Validation with Error/Warning severity
│
├── cli/                         # Presentation layer (Spectre.Console)
│   ├── Steps/
│   │   ├── IWizardStep.cs
│   │   ├── ChooseTierStep.cs
│   │   ├── BuyCharacteristicsStep.cs
│   │   ├── ChooseGearStep.cs
│   │   ├── CalculateDerivedStep.cs
│   │   ├── FinishingTouchesStep.cs
│   │   └── GmReviewStep.cs
│   ├── Powers/
│   │   ├── PowerBrowser.cs      # Category browse + search, rank config
│   │   └── ProConSelector.cs    # Variable-cost variant selection
│   ├── Export/
│   │   └── CharacterSheetExporter.cs
│   ├── WizardOrchestrator.cs    # Step sequencer + HP panel
│   └── HpBudgetDisplay.cs       # Persistent budget panel
│
├── output/                      # Generated character sheets (.txt)
├── docs/
│   ├── RULES_EXTRACTION_GUIDE.md
│   └── P&P/                     # Source PDFs (gitignored)
│
└── Program.cs                   # Entry point
```

---

## Architecture

Three clean layers with no upward dependencies:

```
┌─────────────────────────────┐
│         cli/                │  Spectre.Console — wizard steps, rendering, export
├─────────────────────────────┤
│         engine/             │  Pure C# — cost calc, validation, derived stats
├─────────────────────────────┤
│         data/rules/         │  JSON — all extracted rules data
└─────────────────────────────┘
```

The engine has zero knowledge of the CLI. `CostCalculator.TotalCost()` and `CharacterValidator.Validate()` are the sources of truth — the CLI never tallies points itself.

---

## Wizard Steps

| # | Step | What happens |
|---|------|-------------|
| 1 | **Choose Tier** | Pick power level (Street Level → Iconic), optionally apply a starting package |
| 2 | **Buy Characteristics** | Set ability/talent ranks, browse/search powers with pros & cons, choose flaws |
| 3 | **Choose Gear** | Free-text mundane gear (no HP cost) |
| 4 | **Derived Stats** | Edge and Health auto-calculated and displayed |
| 5 | **Finishing Touches** | Name, appearance, motivation, quote, connections |
| 6 | **GM Review** | Full sheet display, validation results, export to `output/` |

---

## Rules Engine

### Baseline-rank powers

Several powers have a free baseline rank derived from an ability score. Purchased ranks stack on top.

| Relationship | Example | Formula |
|---|---|---|
| `baseline_equal` | Strike, Evasion, Martial Arts | Total = Ability rank + purchased |
| `baseline_half` | Armor, Leaping | Total = ⌈Ability / 2⌉ + purchased |
| `baseline_fixed` | Running | Total = 3d + purchased |

### Derived statistics

| Stat | Formula |
|---|---|
| **Edge** | Perception + max(Agility, Intellect) + Danger Sense bonus + Lightning Reflexes bonus |
| **Health** | max( ⌈(Toughness + Might) / 2⌉, ⌈(Toughness + Willpower) / 2⌉ ) |
| **Resolve** | Chapter 5 — not yet extracted |

### Power costs

```
HP cost = max(1, ⌈purchased_ranks × cost_per_rank⌉ + Σ pro costs + Σ con discounts)
```

Variable-cost pros/cons (e.g. Charges, Area/Burst) prompt for a variant key that maps to the correct value in `cost_modifier_range`.

The Overkill and Weak cons halve the per-rank cost (Brute Option), reducing the multiplier to 0.5 before flat modifiers are applied.

---

## Data Notes

All JSON files in `data/rules/` were extracted from chapters 1–2 of the P&P Ultimate Edition PDF. Entries marked `"needs_review": true` are rules that could not be fully verified from the source — the validator surfaces these as warnings during character creation.

Known `needs_review` items:
- **Lightning Reflexes** — Edge bonus may be a flat +6 rather than +2/rank
- **Iconic tier** — HP budget is "200+" with no stated upper bound
- **Resolve** — formula is in Chapter 5, not yet extracted
- Various powers where PDF text was ambiguous

---

## Versioning

This project uses [Semantic Versioning](https://semver.org/) and [Conventional Commits](https://www.conventionalcommits.org/).

| Version | Branch | Description |
|---------|--------|-------------|
| `0.1.0` | `feat/rules-data-baseline` | JSON rules data baseline |
| `0.1.0` | `feat/engine-baseline` | C# rules engine |
| `0.1.0` | `feat/cli-wizard` | Interactive CLI wizard |

---

## Roadmap

- [ ] `flaws.json` — structured flaw definitions
- [ ] Resolve calculation (Chapter 5 extraction)
- [ ] Perks support (`perks.json`)
- [ ] Back-navigation between wizard steps
- [ ] JSON character sheet export (alongside `.txt`)
- [ ] Unit tests for engine layer
- [ ] Verify all `needs_review` entries against PDF

---

## License

Personal project. Prowlers & Paragons is © LakeSide Games, Inc. No rulebook content is redistributed — only structured metadata derived from it for personal tooling use.
