# Prowlers & Paragons Automation

A character-creation wizard for the **Prowlers & Paragons Ultimate Edition** tabletop RPG by LakeSide Games, Inc., in a terminal or in a browser.

Either front end walks players and GMs through the full creation process — tracking the Hero Point budget live, validating every choice against the system rules, and exporting a finished character sheet. Both run the *same* rules engine: the browser build compiles it to WebAssembly rather than reimplementing it, so the two cannot disagree about what a Power costs.

[![Build](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/build.yml/badge.svg)](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/build.yml)
[![Qodana](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/qodana_code_quality.yml/badge.svg)](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/qodana_code_quality.yml)

---

## Features

- **Interactive step-by-step wizard** covering all six creation phases, with free navigation between steps
- **Live HP budget tracking** — remaining points and a per-category breakdown, always on screen; hidden in Villain mode, which Ch.9 gives no budget
- **141 powers**, every one carrying its rulebook Range, rank type and cost — flat, per rank, per 2 ranks, per unit, variable or Special
- **27 baseline-rank powers** (Armor = ½ Toughness, Evasion = Agility, Running = flat 3d, Strike = Might *or* Martial Arts, Boost/Expertise = a Trait you nominate)
- **23 generic pros and 28 generic cons**, including variable-cost variants (Charges, Area/Burst) and Overkill/Weak's −1 HP per rank
- **106 power-specific pros and cons** the rulebook attaches to one named power — unlike the generic ones, several change a power's cost *per rank* rather than its total
- **Generic pro/con applicability derived from the rulebook**, not curated per power — each option states which Powers it applies to, so nothing legal is hidden from the player
- **The six Sources**, with Powers grouped under Source headings on every sheet the way the published ones print them
- **53 flaws and 13 perks**, wired into Resolve and the HP budget
- **Validation engine** — errors for budget overruns, trait-cap violations, flaw-count breaches, ranks bought on rankless powers and unresolved player choices; warnings for anything still unverified
- **Every rules value verified against the rulebook and locked by tests** — the suite holds the printed Range, Rank and Cost of all 141 powers, so a data edit that contradicts the book fails CI
- **Dual export** — formatted `.txt` and structured `.json`, written to `output/` by the CLI and downloaded by the browser, from one implementation
- **Two front ends on one engine** — a Spectre.Console wizard and a Blazor WebAssembly app that runs `CostCalculator` and `CharacterValidator` as the same compiled code, with Hero and Villain palettes
- **Two sample characters** — a finished Hero and Villain, loadable in one click, for seeing a sheet without building one first; both held to the rules by tests
- **A printed sheet you would hand to someone** — A4 with proper margins, ruled boxes, Powers under small-caps Source headings, and no entry cut in half by a page boundary. Both modes print black on white: paper has no dark mode

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

The source rulebook PDF is **not included** in this repository (copyright), and never will be. Place your own copy in `docs/` if you need to re-run data extraction: `*.pdf` is gitignored repository-wide, and CI fails the build if a PDF is ever tracked.

---

## Getting Started

```bash
git clone https://github.com/DorianSheiles/ProwlersAndParagonsAutomation.git
```

```bash
dotnet run
```

The terminal wizard launches immediately — no configuration required. All rules data is already extracted and lives in `data/rules/`.

For the browser front end:

```bash
dotnet run --project web/ProwlersAndParagons.Web.csproj
```

Then open the address it prints. It is a static site — `dotnet publish web/ProwlersAndParagons.Web.csproj -c Release` produces a `wwwroot/` that any static host can serve, with no server-side component. The rules JSON is copied into `wwwroot/data/rules/` by the build and fetched over HTTP at startup; `data/rules/` remains the only copy in the repository.

To run the test suite:

```bash
dotnet test
```

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
│   ├── powers.json               # 141 powers with range, rank type, cost and their own pros/cons
│   ├── pros.json                 # 23 Power Pros
│   ├── cons.json                 # 28 Power Cons
│   ├── flaws.json                # 53 flaws
│   ├── perks.json                # 13 perks
│   ├── gear_features.json        # 12 custom gear features (Ch.6)
│   └── sources.json              # 6 Sources and the default rank each supplies
│
├── engine/                       # Rules logic — pure C#, zero Spectre.Console
│   ├── Models/                   # Immutable records mapping to the JSON schemas
│   ├── CharacterSheet.cs         # Mutable wizard state
│   ├── RulesRepository.cs        # Lazy JSON loader (snake_case, cached lookups)
│   ├── CostCalculator.cs         # HP cost logic for every trait type
│   ├── DerivedStatsCalculator.cs # Edge, Health, Resolve, baseline/effective rank
│   ├── CharacterValidator.cs     # Validation with Error/Warning severity
│   └── SampleCharacters.cs       # Two finished characters for preview — no rules logic
│
├── sheets/                       # Rendering shared by both front ends, no host coupling
│   ├── CharacterSheetRenderer.cs # The .txt and .json sheets, built as strings
│   ├── PowerFormatter.cs         # A Power's rulebook stat line
│   └── GearFormatter.cs          # A piece of gear as one line
│
├── cli/                          # Terminal presentation only (Spectre.Console)
│   ├── Steps/                    # One class per wizard step, all IWizardStep
│   ├── Powers/                   # PowerBrowser (browse + search), ProConSelector
│   ├── Export/                   # CharacterSheetExporter — writes what sheets/ builds
│   ├── WizardOrchestrator.cs     # Step sequencer, HP panel, back-navigation
│   └── HpBudgetDisplay.cs        # Persistent budget panel
│
├── web/                          # Blazor WebAssembly front end — the engine, in a browser
│   ├── Program.cs                # Fetches the rules over HTTP into an InMemoryRulesSource
│   ├── Pages/                    # One page per creation step, mirroring the CLI's six
│   ├── Components/               # Panel, Field, SheetSection, OptionRow… and SheetView
│   ├── Services/CharacterSession.cs  # The CharacterSheet plus the calculators
│   └── wwwroot/
│       ├── css/theme.css         # Hero, Villain and print palettes, as CSS custom properties
│       ├── css/app.css           # Layout, components and the print stylesheet. Names no colour
│       ├── js/download.js        # The whole of the JavaScript: a blob download and the mode switch
│       ├── _redirects            # Cloudflare: every path serves the app, with a 200
│       └── data/rules/           # Staged from data/rules/ by the build (gitignored)
│
├── tests/ProwlersAndParagonsAutomation.Tests/
│   ├── CanonicalPowers.cs        # Range/Rank/Cost of all 141 powers, from the rulebook
│   ├── PowerDataTests.cs         # powers.json vs the rulebook, plus schema invariants
│   ├── PowerDescriptionTests.cs  # descriptions must agree with their own mechanics
│   ├── RulesDataTests.cs         # tiers, abilities, talents, pros, cons, perks, flaws
│   ├── CostCalculatorTests.cs    # every cost_type, Overkill, the minimum-cost floor
│   ├── DerivedStatsCalculatorTests.cs
│   ├── CharacterValidatorTests.cs
│   ├── PrebuiltHeroes.cs         # the 20 published Heroes from Ch.8, transcribed
│   ├── PrebuiltHeroTests.cs      # rebuilds each and checks their printed Edge/Health/Resolve
│   ├── SampleCharacterTests.cs   # the two preview characters must be legal and printable
│   └── WebPresentationTests.cs   # no colour outside theme.css, no jargon on screen, print rules
│
├── scripts/
│   └── write-cloudflare-headers.sh   # Generates _headers, hashing the inline import map
│
├── .github/workflows/
│   ├── build.yml                 # Build, test, publish the site and check it is complete
│   ├── deploy.yml                # Cloudflare Pages, on push to master only
│   └── qodana_code_quality.yml   # ReSharper inspections
│
├── output/                       # Generated character sheets (gitignored)
├── Directory.Build.props         # Target framework and the analyzer contract, shared by all five projects
├── qodana.yaml                   # Linter, profile and the load-bearing dotnet.solution key
├── PROGRESS.md                   # What is done and what remains — kept current
├── CLAUDE.md                     # Working notes: the decisions that are expensive to re-derive
├── docs/RULES_EXTRACTION_GUIDE.md
└── Program.cs                    # CLI entry point
```

---

## Architecture

Four layers with a strict no-upward-dependency rule:

```
data/rules/   →   engine/   →   sheets/   →   cli/
                                          ↘   web/
```

| Layer | Rule |
|---|---|
| `data/rules/` | JSON only. No logic lives here. |
| `engine/` | Pure C#, zero Spectre.Console references and no filesystem coupling — rules arrive through `IRulesSource`, so the same assembly runs in a browser. `CostCalculator` and `CharacterValidator` are the authority on cost and validity. |
| `sheets/` | The exports, as strings. Shared because both front ends need the same two documents; separate from `engine/` because that layer stays free of presentation. |
| `cli/` | Terminal rendering and prompting. **The CLI never tallies points itself.** |
| `web/` | Browser rendering. Same rule, and it is now enforced by the build rather than by discipline — `web/` cannot reach a calculator it does not have, and it has no copy of one. |

Each is its own project, which is what makes the arrows above true at compile time. `engine/` and `sheets/` were part of the root executable until the browser front end needed them without Spectre.Console attached.

---

## Wizard Steps

| # | Step | What happens |
|---|---|---|
| 1 | **Choose Tier** | Pick power level (Street Level → Iconic), optionally apply a starting package |
| 2 | **Buy Characteristics** | Ability and talent ranks; browse/search powers with pros & cons; flaws and perks |
| 3 | **Choose Gear** | Free-text mundane gear, correctly free per Ch.6, plus optional custom features at 1–2 HP |
| 4 | **Derived Stats** | Edge, Health and Resolve calculated and displayed |
| 5 | **Finishing Touches** | Name, appearance, motivation, quote, connections |
| 6 | **GM Review** | Full sheet display, validation results, export |

Both front ends run these same six steps against the same engine. The terminal wizard offers **← Back** on steps 1–5 and treats GM Review as the terminus, writing its exports to `output/`; the browser keeps every step reachable from the step bar and hands the same two documents to a download.

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

Iconic is stated as "200+" in the rulebook with no upper bound, so the data records a flat 200 and the entry carries a note saying why. It is **not** flagged for review — the verification pass settled that the open end is GM discretion rather than a value nobody has checked. `CharacterValidator` says so with an `ICONIC_TIER_OPEN_BUDGET` notice instead.

---

## Rules Engine

### Power costs

Each power's `cost_type` decides how it is paid for. Only the per-rank types consume purchased ranks.

| `cost_type` | Powers | Cost |
|---|---|---|
| `per_rank` | 71 | `⌈purchasedRanks × cost_per_rank⌉` — `cost_per_rank` is 0.5, 1, 2 or 3 |
| `flat` | 62 | `cost_flat` HP, ranks not purchasable |
| `per_unit` | 3 | `cost_per_unit × units` — Immunity per immunity, Determination per Resolve, Alternate Form per power level |
| `per_rank_variable` | 2 | Energy Absorption (1 or 3), Omni-Power (3 or 5) — `CostVariantKey` picks the rate |
| `flat_variable` | 1 | Stretching (1 / 3 / 6 by reach) |
| `special` | 2 | Boost mirrors the Trait it raises; Summoning is 1 HP per rank per 2d of Minion Threat |

Pro costs and con discounts are then added (cons are stored as **negative** integers, pros positive).

- **Overkill** and **Weak** each reduce the rate by **1 HP per rank**, not by half — the rulebook wording is "reduces a Power's base cost by 1 Hero Point per rank (or changes its base cost from 1 Hero Point per rank to 1 Hero Point per 2 ranks)". Halving is only equivalent for powers already at 1 HP/rank. (The *Brute Option* is the separate rule for applying Overkill to **Might**.)
- The minimum is per rank, not per power, and it is **1 HP per 2 ranks** — the ranked form of the rulebook's "no Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons". Reading it as 1 HP per *rank* puts the floor exactly at the undiscounted cost of a 1 HP/rank power, which silently voids every Con on it; that was a real bug, and it is why the wording matters. `CharacterValidator` warns when a power has bottomed out.
- Specialty is the one power the rulebook prices at 0 HP.

### Rank types

`rank_type` records what the rulebook prints in a power's Rank field.

| `rank_type` | Powers | Meaning |
|---|---|---|
| `power` | 63 | Has its own rank; ranks are purchased |
| `default` | 46 | **No rank at all** — use Toughness or Willpower (by Source) when another power targets it |
| `baseline` | 27 | Rank derived from another Trait, with purchased ranks stacked on top |
| `special` | 5 | Works in a way its own entry describes |

### Baseline-rank powers

| Relationship | Example | Formula |
|---|---|---|
| `baseline_equal` | Evasion (Agility), Martial Arts (Might) | trait rank + purchased |
| `baseline_half` | Armor (½ Toughness), Leaping (½ Might) | ⌈trait ÷ 2⌉ + purchased |
| `baseline_fixed` | Running | 3 + purchased |
| `baseline_greater_of` | Strike | max(Might, Martial Arts) + purchased |
| `baseline_selected_trait` | Boost, Expertise | rank of a Trait the player nominates + purchased |

Halves round **up** throughout, per the rulebook's global "half of an odd number always rounds up" rule.

### Perk costs

Flat perks cost their `cost`. Per-unit perks cost `cost_per_unit × units`.

### Derived statistics

| Stat | Formula |
|---|---|
| **Edge** | (Danger Sense **or** Perception) + max(Agility, Intellect) + Lightning Reflexes, floored at Super Speed × 3 |
| **Health** | max( ⌈(Toughness + Might) ÷ 2⌉, ⌈(Toughness + Willpower) ÷ 2⌉ ) |
| **Resolve** | max(0, (TraitCap − highestRelevantRank) × 2) + Determination Resolve + count of Condition/Plot Hook flaws |

Three powers touch Edge, each differently:

- **Danger Sense** *replaces* Perception in the sum — "use this Power instead of Perception when determining your Edge". It is not added on top.
- **Lightning Reflexes** adds a flat **+6**. It has no rank, so nothing scales.
- **Super Speed** sets Edge to its rank × 3; treated as a floor so it never lowers an already-higher Edge.

Resolve's base term is the rulebook's Resolve table (Trait Cap → 0, Cap−1d → 2, Cap−2d → 4) expressed arithmetically. **Determination** has no rank: every 5 HP spent on it buys 1 extra starting Resolve.

*Highest relevant rank* is the maximum across all ability ranks and the effective ranks of powers whose `affects_resolve` is true. Talents are excluded, as are Movement and Sensory powers by default — `PowerModel.AffectsResolve` overrides that per power. This correctly excludes all eleven powers the rulebook names as Resolve-exempt.

---

## Data Conventions

- All JSON keys are `snake_case`, matched by `JsonNamingPolicy.SnakeCaseLower`
- Powers with `cost_type: "special"` carry no numeric cost — `CostCalculator.PerRankRate` must handle each by id or throw
- `powers.json` records verification **per field** rather than with a single boolean; the other rules files use `"needs_review"`, and nothing is currently flagged

### Verification coverage

Every entry in every rules file has been checked against the rulebook — chapters 1–2 throughout, plus Ch.6 for the gear features and Ch.7 for the three toxin Pros and Cons — and **the test suite is what keeps it that way** — `CanonicalPowers.cs` holds the Range, Rank and Cost printed for all 141 Powers, and `RulesDataTests` holds the tier, ability, talent, pro, con, perk and flaw values. A data edit that contradicts the book fails a test.

On top of that, the **20 pre-built Heroes from Chapter 8** are transcribed and rebuilt through the engine. They are finished, playable Standard-tier characters the authors published, so they check the rules as *applied* rather than as transcribed. The engine reproduces all sixty of their printed Edge, Health and Resolve values, and rebuilds **15 of the 20 to exactly their 125 Hero Point budget**; the other five are within 2 HP for reasons recorded in [PROGRESS.md](PROGRESS.md).

They have earned their keep twice over, catching two cost bugs that unit tests had missed — the minimum-cost floor, and a starting package being charged on top of the ranks it grants. Three of them also pin down rules that are easy to read wrongly:

| Hero | What it proves |
|---|---|
| Black Dragon (Edge 20) | Danger Sense 10d + Agility 10d. Adding Danger Sense to Perception would give 26 — it **replaces** Perception |
| Herald / Scáthach (Edge 22) | Danger Sense 8d + Agility 8d + Lightning Reflexes 6 — confirms both the replacement and the flat +6 |
| Alabama Slammer (Edge 36) | Super Speed 12d × 3, the only printed sheet that exercises that rule |

| File | Entries | Verified against |
|---|---|---|
| `powers.json` | 141 | Ch.2 Powers, pp.21–48 — range, rank type, cost, baseline and description |
| `pros.json` | 23 | Ch.2 Pros and Cons, pp.48–53 |
| `cons.json` | 28 | Ch.2 Pros and Cons, pp.48–53 |
| `perks.json` | 13 | Ch.2 Perks, pp.54–55 |
| `flaws.json` | 53 | Ch.2 Flaws, pp.55–59 |
| `abilities.json` | 6 | Ch.2, p.17 |
| `talents.json` | 12 | Ch.2, p.17 |
| `tiers.json` | 6 | Ch.2 Power Levels, p.17 |
| `gear_features.json` | 12 | Ch.6 Equipment, p.92 — custom gear features |
| `sources.json` | 6 | Ch.2 Sources, p.15 — default rank per Source |

A single `needs_review` boolean could not tell a verified cost from a verified description, and it drifted badly: 27 power entries were unflagged while their costs were wrong. `powers.json` therefore carries `verified_fields` plus a `source_ref` page reference on every entry:

```json
"verified_fields": ["range", "rank_type", "cost", "prerequisite", "description"],
"source_ref": "Ultimate Edition, Ch.2 Powers, p.21"
```

`PowerModel.MechanicsVerified` is true when all four mechanical fields are listed; `NeedsReview` is its inverse. `DescriptionVerified` is tracked separately because descriptions affect no calculation.

Questions the verification pass settled:

- **Lightning Reflexes** — flat 3 HP, no rank, and the Edge bonus **is** a flat +6
- **Determination** — no rank; **5 HP buys 1 Resolve**
- **Iconic tier** — "The 200 Hero Points listed for Iconic Heroes is a bare minimum"; there is no upper bound in the book, so this is GM discretion, not missing data
- **Overkill / Weak** — a −1 HP per rank rate reduction, not a halving
- **Compound entries** — the rulebook prints Form, Transformation and Super Senses as single Powers but prices each of their options separately, so each option is its own entry. Five Flaws and several Pros and Cons pair two options under one heading and are likewise stored separately

### A note on descriptions

Power descriptions are **original text written from the rulebook entry**, not rulebook prose. They exist so a player can tell what they are choosing and what resists it. No rulebook wording is reproduced here — only structured metadata and this project's own explanations. For exact rules wording, read the page named in `source_ref`.

They are held to the mechanics they sit beside: `PowerDescriptionTests` fails a rankless Power whose description claims anything scales per rank, which is how the original set went wrong on 44 of the 46 rankless Powers.

Data coverage is limited to chapters 1–2 of the rulebook (Basics and Characters).

---

## Code Quality

Tests and static analysis both run on every push and pull request.

- **Tests** run with `dotnet test` under the same CI flags as the build, so the rules-data checks gate every change.
- **.NET analyzers** at `latest-recommended`, with `EnforceCodeStyleInBuild`. Warnings become **errors** in CI (`ContinuousIntegrationBuild=true`) but stay warnings locally, so iteration is not blocked. The test project uses the same contract.
- **Qodana Community for .NET** (`jetbrains/qodana-cdnet:2026.2`, `qodana.recommended` profile) runs ReSharper inspections and publishes the report as a build artifact. Upload to GitHub code scanning is **skipped** while the repository is private, because that path needs GitHub Advanced Security; the step turns itself on if the repository becomes public. It is skipped rather than run-and-swallowed on purpose — letting it fail left a red annotation on every run, which trains you to ignore annotations.
- Deliberate analyzer exceptions are documented inline in `.editorconfig` rather than left as bare suppressions.

> **Why the Community linter?** Since 2023.2 the *release* linters (`jetbrains/qodana-dotnet`) refuse to start without a Qodana Cloud `QODANA_TOKEN`, which would fail CI outright. `qodana-cdnet` needs no token or account. To upgrade: register at [qodana.cloud](https://qodana.cloud), add the project token as a `QODANA_TOKEN` repository secret (the workflow already passes it through), and change the `linter:` line in `qodana.yaml`.

Qodana runs in **pull-request mode**, inspecting changed files only — so moving a file re-reports every finding in it as new, and the counts are not comparable between runs. Splitting `engine/` and `sheets/` into their own projects took the count from 144 to 249 without any of that code changing, of which six were genuinely actionable. The summary comment lists rules, never files; download the run's artifact and read `qodana.sarif.json` before drawing conclusions. Committing a baseline is [item 3 on the roadmap](PROGRESS.md) and is what would make the report readable.

Two families of finding are structurally expected rather than bugs: "auto-property accessor is never used" on the JSON model records, where the setters exist for `System.Text.Json` to bind to, and unread positional properties on the test transcription records, which document a rulebook page rather than feed a calculation.

Qodana does catch things the compiler cannot. A `.razor` file sets a component parameter by string key, so `[Obsolete]` on that parameter is invisible to `dotnet build` — `Router.NotFound`'s deprecation in .NET 10 produced zero build warnings with warnings-as-errors on, and Qodana found it.

Reproduce the CI build locally:

```bash
dotnet build --configuration Release -p:ContinuousIntegrationBuild=true
```

```bash
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
```

Run Qodana locally (requires Docker and the [Qodana CLI](https://github.com/JetBrains/qodana-cli)):

```bash
qodana scan --show-report
```

To publish reports to Qodana Cloud, add a `QODANA_TOKEN` repository secret. Without it the scan still runs and the report is downloadable from the workflow run: `gh run download <run-id>`, then read `qodana.sarif.json`. That is currently the only way to see *which files* the findings are in.

---

## Deploying the browser front end

The site is **live on Cloudflare Pages** at [prowlers-and-paragons-chargen.pages.dev](https://prowlers-and-paragons-chargen.pages.dev), deployed by [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) on every push to `master` that touches the app, the engine, the rules or the deploy itself. The custom domain `pp.softwaresamurai.net` is **not attached yet** — that is step 5 below.

There is no server-side component and no build step on Cloudflare's side: the workflow runs `dotnet publish`, writes the security headers, and uploads the result.

### One-time setup

1. **Create the Pages project.** Cloudflare dashboard → Workers & Pages → Create → Pages → *Direct Upload*. Name it `prowlers-and-paragons`, or set a repository variable `CLOUDFLARE_PAGES_PROJECT` to whatever you called it. Do **not** connect it to the Git repository — the workflow uploads, and having both would give you two deploy paths that can disagree.
2. **Create a scoped API token.** My Profile → API Tokens → Create Token → *Custom token*:
   - Permission: **Account → Cloudflare Pages → Edit**, and nothing else.
   - Account Resources: **only** the account holding this project.
   - Not the Global API Key, which can do anything to every zone on the account.
3. **Add the repository secrets** `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` (Settings → Secrets and variables → Actions).
4. **Deploy once and check it before touching DNS.** `gh workflow run deploy.yml --ref master`, then open the `*.pages.dev` URL. Attaching the domain first means debugging the site and the DNS at the same time.
5. **Attach the custom domain.** Pages project → Custom domains → `pp.softwaresamurai.net`. Cloudflare creates the CNAME itself. It serves the **production** deployment, so get step 4 green first.

You do **not** need to line the project's production branch up with this repository's.

That deserves a word, because it is a trap the first deploy fell into. Wrangler's `--branch` is a **label Cloudflare compares against the project's configured production branch** — not a branch it reads. If the two differ, the deploy lands as a *preview*: it succeeds, prints a `<branch>.<project>.pages.dev` alias, the workflow goes green, and the production URL and any custom domain answer **404**, with nothing in the logs to say why. New projects default to `main`; this repository is `master`.

So the workflow asks the project what it calls production and deploys to that, and then checks the production hostname actually serves a page before it will pass. The mismatch cannot happen, and if the site is somehow still not up, the deploy fails instead of reporting success.

### Notes on keeping it safe

The site is static, has no backend, no accounts and no cookies, and nothing a visitor types leaves their browser — so there is very little to attack. What is worth getting right is the blast radius around it:

- **A subdomain, not the apex.** The Pages project answers for `pp.softwaresamurai.net` only. Nothing about it touches routing for the rest of the domain, and a mistake in the Pages config cannot take the apex down with it.
- **The API token is scoped to Pages on one account.** If it ever leaked, the worst it can do is redeploy this one site. Rotate it in the Cloudflare dashboard and update the secret; nothing in the repository holds a copy.
- **The workflow never runs on `pull_request`.** That trigger would execute a contributor's workflow changes with the token in scope. Deploys happen only from `master`, after a merge.
- **Security headers ship with the site**, generated into `_headers` by [`scripts/write-cloudflare-headers.sh`](scripts/write-cloudflare-headers.sh) and applied by Cloudflare to every response: a Content-Security-Policy that permits scripts only from this origin, plus `nosniff`, `Referrer-Policy: no-referrer`, `frame-ancestors 'none'` and a `Permissions-Policy` that turns off every device API the app does not use.

  The CSP is generated rather than written by hand because Blazor emits an inline import map into `index.html` whose contents change whenever the framework assets are re-fingerprinted. A hard-coded hash would rot silently and take the site down on some later deploy; the script hashes whatever was actually published, and fails the build rather than shipping a policy that would stop the app booting. `style-src` still needs `'unsafe-inline'` — the budget bar's width is a live number and arrives as an inline style attribute.

  Both CI and the deploy run the same script, so a change that breaks the policy fails on the pull request.
- **Your Pages project also answers on its `*.pages.dev` address.** That is public. If you would rather only friends reach it, put Cloudflare Access (Zero Trust) in front of the project — it covers both hostnames.

To check the policy locally, publish and generate the headers, then serve the result with any static host that applies them:

```bash
dotnet publish web/ProwlersAndParagons.Web.csproj -c Release -o publish && ./scripts/write-cloudflare-headers.sh publish/wwwroot
```

### One thing to know before you look at the network tab

The first load is about **27 MiB uncompressed** (roughly a third of that over the wire, since Cloudflare applies Brotli), and it is cached hard afterwards because every framework asset is fingerprinted. It is that large because IL trimming is disabled — see [PROGRESS.md](PROGRESS.md) for why, and for what closing it would take.

---

## Roadmap

**[PROGRESS.md](PROGRESS.md) is the single source of truth** for what is done and what remains, with the reasoning behind each item.

It is deliberately **not** summarised here. This section twice grew a numbered copy of that list, and both times it drifted: the second one still advertised the Blazor front end and the Hero/Villain sheet as future work after both had shipped. A short version is not cheaper than one list — it is a second list that nobody remembers to update.

---

## Versioning

This project follows [Semantic Versioning](https://semver.org/) and [Conventional Commits](https://www.conventionalcommits.org/).

---

## License

Intended for open-source release to the P&P community; until a licence is chosen the code is unlicensed and all rights are reserved. It is not sold, and any hosted instance is self-hosted.

Prowlers & Paragons is © LakeSide Games, Inc. (2013–2021), by Leonard A. Pimentel and Sean Patrick Fannon. **No rulebook content is redistributed here.** What lives in `data/rules/` is structured metadata — names, costs, ranges, rank types — together with this project's own explanations of what each option does. Descriptions are written from scratch, not copied. The rulebook itself is required to play, and is not included in this repository.
