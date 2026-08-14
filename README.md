# Prowlers & Paragons Automation

A character-creation tool for the **Prowlers & Paragons Ultimate Edition** tabletop RPG by LakeSide Games, Inc. — in a terminal, in a browser, with no interface at all, or by describing a character to your own Claude.

The two interactive front ends walk players and GMs through the full creation process, tracking the Hero Point budget live, validating every choice against the system rules, and exporting a finished character sheet. The third way in is `build --from character.json`, which costs and validates a character and asks nothing: it exists so a language model can propose a character during play and have the engine decide whether it is legal. The fourth is an **MCP server**: connect it to your own Claude, describe a character out loud, and answer the two or three questions that actually change the build.

All four run the *same* rules engine — the browser build compiles it to WebAssembly rather than reimplementing it — so nothing can disagree about what a Power costs.

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
- **Generic pro/con applicability derived from the rulebook**, not curated per power — each option states which Powers it applies to, so nothing legal is hidden from the player, and the constraints the book prints for every Power (its Range and rank type) are enforced on a submitted character as well as filtered in the pickers. Where a Power's *own* printed text names a generic option its Range would otherwise forbid, that sentence is recorded and the option is offered: Force Field is Self and the rulebook tells you to apply the Zone Pro to it, which is how T-Kay is printed
- **The six Sources**, with Powers grouped under Source headings on every sheet the way the published ones print them
- **53 flaws and 13 perks**, wired into Resolve and the HP budget
- **Validation engine** — errors for budget overruns, both Trait floors and the Trait Cap, flaw-count breaches, ranks bought on rankless powers, unresolved player choices, and every id or quantity a hand-written character can get wrong; warnings for anything still unverified. **Each finding carries the facts as well as the sentence** — which Trait, what it is, what it may be, and the values a fix must be chosen from — so a repair loop never has to parse English
- **Both Trait floors, which nothing used to enforce** — Ch.2 states twice that no Ability or Talent can be lower than 1d, so a character has all eighteen and 0d is a Trait nobody can be without; and a starting package's granted ranks cannot be lowered below what it gives. Neither costs anything to break, which is why both were silent
- **An MCP server, for describing a character to your own Claude** — six tools over stdio, wrapping the same engine: the question policy, the catalogues, a Power search, one Power in full, the judge, and the printed sheet. It handles no credentials and holds no key; the conversation happens in the client you already pay for. Setting it up: [`docs/MCP-SETUP.md`](docs/MCP-SETUP.md)
- **A headless `build` command and a skill to drive it** — one JSON report on standard output for each of its three exits, and a `SKILL.md` teaching the schema and the propose/validate/repair loop. The model proposes and the engine decides: nothing in the command computes a Hero Point, and an illegal character is reported, never repaired
- **Every rules value verified against the rulebook and locked by tests** — the suite holds the printed Range, Rank and Cost of all 141 powers, so a data edit that contradicts the book fails CI
- **Dual export** — formatted `.txt` and structured `.json`, written to `output/` by the CLI and downloaded by the browser, from one implementation
- **Four front ends on one engine** — a Spectre.Console wizard, a Blazor WebAssembly app that runs `CostCalculator` and `CharacterValidator` as the same compiled code with Hero and Villain palettes, the headless `build` command, and the MCP server. None of them holds a second copy of a rule
- **Four recorded conversations, replayed in the browser** — for anyone who wants to see what describing a character out loud gets you and has no Claude of their own to ask. The words are a recording and say so on the page; the numbers are not. Every Hero Point figure, every derived stat and every finding beside them is worked out in your browser as you reveal it, from the character the recording carries — **no transcript holds a total** — and at the end the character opens in the editors so you can change a rank and watch them move
- **Two sample characters** — a finished Hero and Villain, loadable in one click, for seeing a sheet without building one first; both held to the rules by tests
- **A printed sheet modelled on the published Hero Sheet** — one A4 page, three columns, ruled boxes with centred headings, every Ability and Talent listed, and blank ruled space for the fields a pen fills in. White paper and readable ink in both modes: a Hero sheet prints navy, a Villain crimson, and colour appears as ink and as a tint behind a heading bar, never as a fill
- **The character is kept in the browser between visits** — a refresh, a bookmark or a shared link no longer throws it away, and nothing is sent anywhere. A saved character that this build cannot read is discarded rather than restored, because a tool that will not open is worse than one that forgets

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

Then open the address it prints. It is a static site — `dotnet publish web/ProwlersAndParagons.Web.csproj -c Release` produces a `wwwroot/` that any static host can serve, with no server-side component. The rules JSON is copied into `wwwroot/data/rules/` by the build and fetched over HTTP at startup; `data/rules/` remains the only copy in the repository. The recorded conversations at `/replay` are staged from `data/transcripts/` the same way.

To cost and validate a character without a terminal:

```bash
dotnet run -- build --from character.json --no-export
```

It writes one JSON report to standard output and exits **0** if the character is legal, **1** if it breaks a rule, or **2** if the input could not be read. `--help` lists the rest. The input is the character-sheet shape — the *inputs* of a character, which is also what the browser keeps in local storage — not the JSON export, which is a report and would mean rebuilding a character from its own conclusions.

`.claude/skills/prowlers-and-paragons-character/SKILL.md` teaches that loop to a language model: propose a character, submit it, read the structured findings, adjust, resubmit. The ordering is the whole point — the model proposes and the engine decides what anything costs.

To run the test suite:

```bash
dotnet test
```

---

## Connecting it to your own Claude

The MCP server lets you describe a character in ordinary words — *"a washed-up boxer who punches through time"* — and get a legal, costed one back, with Claude asking you the two or three questions the description leaves open. **It handles no credentials and holds no API key**: the server is a local program that answers questions about the rules, and the conversation happens in the Claude client you already use.

**→ [Setting it up on your machine](docs/MCP-SETUP.md)** — publishing the server, registering it with Claude Code or Claude Desktop, what the six tools are for, and what to check when it does not connect.

It is one file rather than a section here because the setup is the part a stranger needs and the part with the traps in it: the path has to outlive a git worktree, the client has to point at the built binary rather than at `dotnet run`, and the published server carries its own copy of the rules — so it keeps answering with old ones, perfectly happily, until you re-publish.

No Claude of your own? [`/replay`](https://prowlers-and-paragons-chargen.pages.dev/replay) plays four of these conversations back, with the engine costing and validating in your browser as you read.

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
├── data/transcripts/             # Four recorded conversations the browser replays — characters, never totals
│   ├── vera-nunn.json            # Street Level, and a Power the first search very nearly buried
│   ├── chrono-jab.json           # "punches through time" — one Power or three
│   ├── sheet-lightning.json      # a first draft over budget, the trade offered, the settlement
│   └── the-conductor.json        # a Villain, who has no Hero Point budget at all (Ch.9)
│
├── engine/                       # Rules logic — pure C#, zero Spectre.Console
│   ├── Models/                   # Immutable records mapping to the JSON schemas
│   ├── CharacterSheet.cs         # Mutable wizard state
│   ├── RulesRepository.cs        # Lazy JSON loader (snake_case, cached lookups)
│   ├── CostCalculator.cs         # HP cost logic for every trait type
│   ├── DerivedStatsCalculator.cs # Edge, Health, Resolve, baseline/effective rank
│   ├── CharacterValidator.cs     # Validation with Error/Warning severity
│   ├── SampleCharacters.cs       # Two finished characters for preview — no rules logic
│   ├── Transcript.cs             # A recorded conversation: turns, and the characters put to the engine
│   └── TranscriptLibrary.cs      # Reads them, strictly — a renamed field fails at load
│
├── sheets/                       # Rendering shared by every host, no host coupling
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
│   ├── Pages/                    # The six creation steps, mirroring the CLI, plus the replay
│   ├── Components/               # Panel, Field, SheetSection, OptionRow… and SheetView
│   ├── Services/CharacterSession.cs  # The CharacterSheet plus the calculators
│   ├── Services/Labels.cs        # Turns a rules key into something a player can read
│   ├── Services/CharacterStore.cs    # Keeps the character in the browser between visits
│   ├── Services/ReplayLibrary.cs # The recorded conversations. Has no method that returns a number
│   └── wwwroot/
│       ├── css/theme.css         # Hero, Villain and print palettes, as CSS custom properties
│       ├── css/app.css           # Layout, components and the print stylesheet. Names no colour
│       ├── js/download.js        # The whole of the JavaScript: a blob download and the mode switch
│       ├── _redirects            # Cloudflare: every path serves the app, with a 200
│       ├── data/rules/           # Staged from data/rules/ by the build (gitignored)
│       └── data/transcripts/     # Staged from data/transcripts/ the same way (gitignored)
│
├── mcp/                          # MCP server — the engine, in somebody else's Claude
│   ├── QUESTION-POLICY.md        # The two or three questions worth asking. Embedded, and served verbatim
│   ├── CharacterTools.cs         # The six tools, and why there are six
│   ├── CharacterServer.cs        # Wire names, server instructions, the tool collection
│   ├── Judgement.cs              # What the engine said, written down. Computes nothing
│   ├── QuestionPolicy.cs         # Serves QUESTION-POLICY.md from the assembly, verbatim
│   ├── RulesLocation.cs          # Finds data/rules beside the binary, not by walking up for a .sln
│   ├── CommandLine.cs            # The two arguments, where a test can reach them
│   └── Program.cs                # stdio. Standard output carries the protocol and nothing else
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
│   ├── WebPresentationTests.cs   # no colour outside theme.css, no jargon on screen, print rules
│   ├── ValidationMessageTests.cs # every message a player can be shown, held to the same rule
│   ├── TranscriptTests.cs        # the recordings, held to the engine — and no figure in their prose
│   └── McpSetupDocumentationTests.cs  # docs/MCP-SETUP.md, held to the code it describes
│
├── tests/ProwlersAndParagons.Web.Tests/   # bUnit — renders components and reads the output
│   ├── RenderContext.cs          # the app's own services, on the real data/rules
│   ├── FakeLocalStorage.cs       # an IJSRuntime backed by a dictionary, and able to refuse
│   ├── SheetRenderTests.cs       # what the sheet actually renders, "Armor8d" and all
│   ├── StartAgainTests.cs        # all three controls that destroy work ask first, then clear
│   ├── CharacterStoreTests.cs    # a character survives the round trip; bad storage never throws
│   └── ReplayRenderTests.cs      # every figure on a replayed page equals the calculator's own answer
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
├── Directory.Build.props         # Target framework and the analyzer contract, shared by every project
├── qodana.yaml                   # Linter, profile and the load-bearing dotnet.solution key
├── PROGRESS.md                   # What is done and what remains — kept current
├── CLAUDE.md                     # Working notes: the decisions that are expensive to re-derive
├── docs/HANDOVER.md              # Where the last session stopped and what the next one is for
├── docs/MCP-SETUP.md             # Connecting the server to Claude Code or Claude Desktop
├── docs/RULES_EXTRACTION_GUIDE.md
└── Program.cs                    # CLI entry point
```

---

## Architecture

Four layers with a strict no-upward-dependency rule, and three hosts sharing the top one:

```
                                          ↗   cli/
data/rules/   →   engine/   →   sheets/   →   web/   ←   data/transcripts/
                                          ↘   mcp/
```

| Layer | Rule |
|---|---|
| `data/rules/` | JSON only. No logic lives here. |
| `data/transcripts/` | The second data input, and not rules: the recorded conversations `/replay` plays. Read through the engine — every character in one goes through the strict reader — and loaded only by `web/`. |
| `engine/` | Pure C#, zero Spectre.Console references and no filesystem coupling — rules arrive through `IRulesSource`, so the same assembly runs in a browser. `CostCalculator` and `CharacterValidator` are the authority on cost and validity. |
| `sheets/` | The exports, as strings. Shared because three hosts need the same two documents; separate from `engine/` because that layer stays free of presentation. |
| `cli/` | Terminal rendering and prompting. **The CLI never tallies points itself.** |
| `web/` | Browser rendering. Same rule, and it is now enforced by the build rather than by discipline — `web/` cannot reach a calculator it does not have, and it has no copy of one. |
| `mcp/` | Protocol plumbing and the question policy. Same rule again: it references `engine/` and `sheets/` and cannot reference `cli/`, so nothing in it can compute a Hero Point or write a file. |

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

Halves round **up** throughout, per the rulebook's global "Whenever we refer to half of an odd number (or half of an odd number of dice), always round up, regardless of the context" rule.

### Perk costs

Flat perks cost their `cost`. Per-unit perks cost `cost_per_unit × units`.

### Derived statistics

| Stat | Formula |
|---|---|
| **Edge** | (Danger Sense **or** Perception) + max(Agility, Intellect) + Lightning Reflexes, floored at Super Speed × 3 |
| **Health** | max( ⌈(Toughness + Might) ÷ 2⌉, ⌈(Toughness + Willpower) ÷ 2⌉ ) |
| **Resolve** | max(0, (TraitCap − highestRelevantRank) × 2) + Determination Resolve + count of Condition/Plot Hook flaws |

Three powers touch Edge, each differently:

- **Danger Sense** *replaces* Perception in the sum — "Use this Power instead of Perception when making rolls to detect danger and when determining your Edge". It is not added on top.
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

On top of that, the **20 pre-built Heroes from Chapter 8** are transcribed and rebuilt through the engine. They are finished, playable Standard-tier characters the authors published, so they check the rules as *applied* rather than as transcribed. The engine reproduces all sixty of their printed Edge, Health and Resolve values, and rebuilds **16 of the 20 to exactly their 125 Hero Point budget**; the other four are 1 HP out, each for a reason recorded in [PROGRESS.md](PROGRESS.md).

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
| `cons.json` | 28 | Ch.2 Pros and Cons, pp.48–54 |
| `perks.json` | 13 | Ch.2 Perks, pp.54–55 |
| `flaws.json` | 53 | Ch.2 Flaws, pp.55–60 |
| `abilities.json` | 6 | Ch.2, p.17 |
| `talents.json` | 12 | Ch.2, p.18 |
| `tiers.json` | 6 | Ch.2 Power Levels, p.15 |
| `gear_features.json` | 12 | Ch.6 Equipment, p.93 — custom gear features |
| `sources.json` | 6 | Ch.2 Sources, p.16 — default rank per Source |

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

Data coverage is everything character creation needs: chapters 1–2 (Basics and Characters) in full, plus Ch.6's twelve custom gear features and Ch.7's three toxin Pros and Cons. Chapters 3, 4, 5 and 7 are play rules, 8 is the pre-built characters — transcribed in the test suite, where they verify the engine — and Ch.9 builds Villains by the Hero rules, which is why the mode is presentation only. The one genuine gap is Ch.6's vehicles and headquarters; see [PROGRESS.md](PROGRESS.md).

---

## Code Quality

Tests and static analysis both run on every push and pull request.

- **Tests** run with `dotnet test` under the same CI flags as the build, so the rules-data checks gate every change.
- **.NET analyzers** at `latest-recommended`, with `EnforceCodeStyleInBuild`. Warnings become **errors** in CI (`ContinuousIntegrationBuild=true`) but stay warnings locally, so iteration is not blocked. The test project uses the same contract.
- **Qodana Community for .NET** (`jetbrains/qodana-cdnet:2026.2`, `qodana.recommended` profile) runs ReSharper inspections and publishes the report as a build artifact. Upload to GitHub code scanning is **skipped** while the repository is private, because that path needs GitHub Advanced Security; the step turns itself on if the repository becomes public. It is skipped rather than run-and-swallowed on purpose — letting it fail left a red annotation on every run, which trains you to ignore annotations.
- Deliberate analyzer exceptions are documented inline in `.editorconfig` rather than left as bare suppressions.

> **Why the Community linter?** The *release* linters (`jetbrains/qodana-dotnet`) need a valid Qodana Cloud **licence**, not merely a token — and **a token alone breaks `cdnet` too.** That was measured, not assumed: with a `QODANA_TOKEN` secret set, both images linked the Cloud project and then exited on `License request: token was declined by Qodana Cloud server`, having inspected nothing. So the workflow deliberately passes **no** token — a scan cannot be broken by a credential it never reads — and `cdnet` needs none, along with no account and no licence.
>
> Qodana is therefore entirely self-contained here: a Docker image, a SARIF file, no service. Cloud is a separate paid product whose two selling points are the fuller release linters and a hosted dashboard with history, neither of which a one-developer repository has an audience for. Upgrading needs all three of a licensed plan, a `QODANA_TOKEN` secret and an `env:` block restored on the scan step; any two without the third break the scan. If a dashboard is what you actually want, making the repository public turns on the free GitHub code-scanning upload below by itself.

**A whole-tree scan reports zero**, and it is worth knowing how, because the obvious mechanism does not work. `qodana.yaml`'s `exclude:` list accepts an inspection *name*, looks like it silences it, and does nothing — the .NET linter is ReSharper, which takes severities from EditorConfig. Only the path exclusions in `qodana.yaml` have any effect. Every deliberate exception is therefore a `resharper_*_highlighting = none` in `.editorconfig`, scoped as tightly as the tool allows and carrying its reason. Nothing is baselined and there is no severity floor; both hide a finding rather than answer it. The side benefit is that Rider and the ReSharper command-line tools now agree with CI.

What is silenced, in one line each: `engine/Models/*.cs` exists to be deserialized by reflection and must keep its setters; the test transcription records document a rulebook page rather than being read; a `[Theory]` body asserting on its parameter is not a precondition guard; `JsonValue.Create(...)!` is load-bearing; and this codebase writes explicit constructors and named backing fields on purpose.

Qodana runs in **pull-request mode**, inspecting changed files only — so moving a file re-reports every finding in it as new, and the counts are not comparable between runs. Splitting `engine/` and `sheets/` into their own projects took the count from 144 to 249 without any of that code changing, of which six were genuinely actionable. The summary comment lists rules, never files; download the run's artifact and read `qodana.sarif.json` before drawing conclusions.

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

**Do not add a `QODANA_TOKEN` secret to publish reports to Qodana Cloud** — without a licensed plan that stops the scan dead rather than publishing anything, and it does so to the Community linter as well. The report is downloadable from the workflow run instead: `gh run download <run-id>`, then read `qodana.sarif.json`. That is currently the only way to see *which files* the findings are in.

Since CI only ever sees changed files, scan the whole tree yourself before concluding anything about the total:

```bash
docker run --rm -v "$(pwd -W):/data/project/" -v "$PWD/results:/data/results/" jetbrains/qodana-cdnet:2026.2 --save-report
```

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

**MIT** — see [LICENSE](LICENSE). It covers this repository's own code and text and nothing else. The tool is not sold, and any hosted instance is self-hosted.

Prowlers & Paragons is © LakeSide Games, Inc. (2013–2021), by Leonard A. Pimentel and Sean Patrick Fannon. **No rulebook content is redistributed here.** What lives in `data/rules/` is structured metadata — names, costs, ranges, rank types — together with this project's own explanations of what each option does. Descriptions are written from scratch, not copied. The rulebook itself is required to play, and is not included in this repository.
