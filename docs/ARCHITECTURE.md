# Architecture

How the projects fit together, and what lives where. The rules for *working* in this codebase are in [`CLAUDE.md`](../CLAUDE.md) — the ones that apply whatever you are touching — and in [`docs/guide/`](guide/), one file per area, which `CLAUDE.md`'s routing table indexes. This is the map.


Four layers with a strict no-upward-dependency rule, and three hosts sharing the top one:

```
                                          ↗   cli/
data/rules/   →   engine/   →   sheets/   →   web/   ←   data/transcripts/
                                          ↘   mcp/
```

| Layer | Rule |
|---|---|
| `data/rules/` | JSON only. No logic lives here. |
| `data/transcripts/` | The second data input, and not rules: the recorded conversations the replay plays. Read through the engine — every character in one goes through the strict reader — and loaded only by `web/`. |
| `engine/` | Pure C#, zero Spectre.Console references and no filesystem coupling — rules arrive through `IRulesSource`, so the same assembly runs in a browser. `CostCalculator` and `CharacterValidator` are the authority on cost and validity. |
| `sheets/` | The exports, as strings. Shared because three hosts need the same two documents; separate from `engine/` because that layer stays free of presentation. |
| `cli/` | Terminal rendering and prompting. **The CLI never tallies points itself.** |
| `web/` | Browser rendering. Same rule, and it is now enforced by the build rather than by discipline — `web/` cannot reach a calculator it does not have, and it has no copy of one. |
| `mcp/` | Protocol plumbing and the question policy. Same rule again: it references `engine/` and `sheets/` and cannot reference `cli/`, so nothing in it can compute a Hero Point or write a file. |

Each is its own project, which is what makes the arrows above true at compile time. `engine/` and `sheets/` were part of the root executable until the browser front end needed them without Spectre.Console attached.

## The second engine, which is a second stack rather than another layer

```
data/rules/play/   →   play/   →   (hosts, in a later slice)
                        ↑
                     engine/
```

`play/` resolves an action — a page of combat, a challenge roll, a Resolve spend — where `engine/`
costs and validates a character. `CLAUDE.md`'s settled list is why they are two things: **the first
engine is the authority on cost and validity and knows nothing about resolving an action**, so a
combat simulator is a *second* engine beside it and never a change to it.

**The arrows are one-way in both directions and both are enforced.** `play/` references `engine/`
for `CharacterSheet`, `IRulesSource` and the derived statistics; `engine/` and `sheets/` never name
`play/`, and `play/` never names `CostCalculator` or `CharacterValidator` — `PlayContractTests`. It
inherits the four promises `AccountsContractTests` and `PresentationFlagsTests` already hold the
first engine to: no account, no filesystem, no network, no presentation flag.

**`data/rules/play/` is a separate store for a reason of payload rather than of taste**: every
csproj copies `data\rules\*.json` non-recursively, so the play rules reach no browser and no
published MCP server. See [`guide/play-rules.md`](guide/play-rules.md) and
[`guide/play-engine.md`](guide/play-engine.md).

| | |
|---|---|
| `play/` | `PlayRulesRepository` over the five play files; `IDiceSource` returning raw d6 faces; an immutable `EncounterState` and a pure `Encounter.Step`; a ledger where every line cites the page its rule came from. Nothing references it yet. |

## The one thing outside those layers: `worker/`

There is a fifth directory, and it is deliberately not on the diagram: `worker/` is the accounts
server — JavaScript, because Cloudflare Workers is — reached through the single routed file
`functions/api/[[path]].js`. It sits **beside** the stack rather than on top of it, and the reason
is a rule rather than a layout preference:

**It contains no rules and cannot.** It stores a character as an opaque string it never parses,
because the engine is the authority on what a character costs and whether it is legal, and the
engine runs in the browser. So the arrow from `engine/` never reaches it, in either direction, and
`AccountsContractTests` asserts that `engine/` and `sheets/` make no HTTP call at all.

| | |
|---|---|
| `worker/` | Sign-in, sessions, an account's characters and the cap they are held to, and the rulebook's text behind a session. Every SQL statement is in `db.js`; every route is in `index.js`. |
| `functions/api/[[path]].js` | The only file Cloudflare routes. Two lines, so "what is exposed?" has one answer. |
| `d1/` | The schema, as migrations, plus the config that applies them. Not at the repository root on purpose — see the comment in `d1/wrangler.toml`. |
| `tests/worker/` | Driven against real SQLite running the real migration, since D1 *is* SQLite. Run with `./scripts/test-worker.sh`. |

`data/rulebook/` is bundled **into** `worker/` and is never staged into `wwwroot`. That placement is
the whole of the access control: a file under `wwwroot` is a public URL. Setting the server up is
[`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md).

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
│   ├── gear_features.json        # 12 custom gear features (Ch.6 p.93)
│   ├── gear.json                 # Ch.6 pp.88–93: armour, shields, the weapon-features
│   │                             #   glossary, the equipment list, custom gear. Nothing
│   │                             #   loads it yet — see docs/guide/rules-engine.md
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
│   ├── Pages/                    # The seven creation steps, mirroring the CLI; plus the replay,
│   │                              # sign-in, the character/campaign manager, campaign approval,
│   │                              # the rulebook reader, the portfolio and the admin page —
│   │                              # this list is illustrative, not exhaustive; see the directory
│   ├── Components/               # Panel, Field, SheetSection, OptionRow… and SheetView
│   ├── Services/CharacterSession.cs  # The CharacterSheet plus the calculators
│   ├── Services/Labels.cs        # Turns a rules key into something a player can read
│   ├── Services/ICharacterStore.cs   # Plural now — see docs/CHARACTERS-API.md; CharacterStore.cs
│   │                                  # is the local-storage half, AccountCharacterStore.cs and
│   │                                  # ApiCharacterStore.cs the account half, with the same shape
│   │                                  # again for campaigns (AccountCampaignStore/ApiCampaignStore)
│   ├── Services/ReplayLibrary.cs # The recorded conversations. Has no method that returns a number
│   └── wwwroot/
│       ├── css/theme.css         # Hero, Villain and print palettes, as CSS custom properties
│       ├── css/app.css           # Layout, components and the print stylesheet. Names no colour
│       ├── js/download.js        # A blob download, the mode switch and the local-storage wrapper
│       ├── js/theme.js           # Stamps light/dark before first paint, render-blocking in <head>
│       ├── js/motion.js          # View Transitions API, counting numbers — docs/FRONT-END-PLAN.md
│       ├── js/palette.js         # The command palette's one document-level key listener (Ctrl/Cmd+K)
│       ├── js/slider.js          # Suppresses Home/End's page-scroll on a rank's role="slider"
│       ├── _redirects            # Cloudflare: every path serves the app, with a 200
│       ├── data/rules/           # Staged from data/rules/ by the build (gitignored)
│       └── data/transcripts/     # Staged from data/transcripts/ the same way (gitignored)
│
├── play/                         # The second engine — resolving an action, not costing a character
│   ├── Rules/PlayRulesRepository.cs  # The five data/rules/play files, lazily, through IRulesSource
│   ├── Rules/Models/             # One record per file, covering every key it carries
│   ├── Rules/SuccessCounter.cs   # Faces to successes. Holds no literal 2, 4 or 6
│   ├── Dice/IDiceSource.cs       # Raw d6 faces — SeededDice and ScriptedDice
│   ├── Encounter/Combatant.cs    # An immutable snapshot; only a Hero can hold Resolve
│   ├── Encounter/TableRules.cs   # The ten gritty switches and three more, every one naming its entry
│   ├── Encounter/Encounter*.cs   # Begin, Step, RunToEnd — pure over an immutable state
│   └── Encounter/AttackTheWeakest.cs # The one policy that ships, named in every report
│
├── mcp/                          # MCP server — the engine, in somebody else's Claude
│   ├── QUESTION-POLICY.md        # The two or three questions worth asking. Embedded, and served verbatim
│   ├── CharacterTools.cs         # The seven tools, and why there are seven
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
│   ├── deploy.yml                # Cloudflare Pages, on push to main only
│   └── qodana_code_quality.yml   # ReSharper inspections
│
├── output/                       # Generated character sheets (gitignored)
├── Directory.Build.props         # Target framework and the analyzer contract, shared by every project
├── qodana.yaml                   # Linter, profile and the load-bearing dotnet.solution key
├── PROGRESS.md                   # What is done and what remains — kept current
├── CLAUDE.md                     # The disciplines that apply everywhere, and the guide routing table
├── docs/guide/                   # One file per area: the decisions that are expensive to re-derive
├── docs/MCP-SETUP.md             # Connecting the server to Claude Code or Claude Desktop
└── Program.cs                    # CLI entry point
```

---

