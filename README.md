# Prowlers & Paragons Automation

A character-creation tool for the **Prowlers & Paragons Ultimate Edition** tabletop RPG by LakeSide Games, Inc. — in a terminal, in a browser, with no interface at all, or by describing a character to your own Claude.

The two interactive front ends walk players and GMs through the full creation process, tracking the Hero Point budget live, validating every choice against the system rules, and exporting a finished character sheet. The third way in is `build --from character.json`, which costs and validates a character and asks nothing: it exists so a language model can propose a character during play and have the engine decide whether it is legal. The fourth is an **MCP server**: connect it to your own Claude, describe a character out loud, and answer the two or three questions that actually change the build.

All four run the *same* rules engine — the browser build compiles it to WebAssembly rather than reimplementing it — so nothing can disagree about what a Power costs.

[![Build](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/build.yml/badge.svg)](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/build.yml)
[![Qodana](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/qodana_code_quality.yml/badge.svg)](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/actions/workflows/qodana_code_quality.yml)

---

## Features

**Four front ends, one engine.** A Spectre.Console terminal wizard, a Blazor WebAssembly app that
runs `CostCalculator` and `CharacterValidator` as *the same compiled code*, a headless
`build --from character.json`, and an MCP server. None of them holds a second copy of a rule.

**The rules, complete for character creation and locked by tests.** All 141 Powers with their
printed Range, rank type and cost; 27 baseline-rank Powers; 23 generic Pros and 28 generic Cons
including the variable-cost ones; the 106 Power-specific Pros and Cons, several of which change a
Power's cost *per rank*; 53 Flaws, 13 Perks, the six Sources and the tiers. A data edit that
contradicts the book fails CI. → [The rules engine](docs/RULES-ENGINE.md)

**Applicability derived from the rulebook, not curated per Power.** Each generic option states which
Powers it applies to, so nothing legal is hidden — and where a Power's *own* printed text names an
option its Range would otherwise forbid, that sentence is recorded and the option offered. Force
Field is Self range and the book tells you to apply the Zone Pro to it, which is how T-Kay is
printed.

**A validator that reports rather than repairs**, and carries the facts as well as the sentence —
which Trait, what it is, what it may be, and the values a fix must choose from — so a repair loop
never parses English. It covers budget overruns, both Trait floors and the Trait Cap, flaw counts,
ranks bought on rankless Powers, unresolved choices, and every id or quantity a hand-written
character can get wrong.

**A printed sheet modelled on the published Hero Sheet** — one A4 page, three columns, every Ability
and Talent listed, blank ruled space for what a pen fills in. White paper and readable ink in both
palettes: a Hero sheet prints navy, a Villain crimson, colour only as ink or a tint behind a heading
bar, never as a fill.

**Describe a character out loud.** The MCP server wraps the same engine in six tools over stdio, so
your own Claude can ask you the two or three questions a description leaves open and hand back a
costed, checked character. It handles no credentials and holds no key. →
[Setting it up](docs/MCP-SETUP.md)

**And if you have no Claude of your own**, [`/portfolio/replay`](https://prowlers-and-paragons-chargen.pages.dev/portfolio/replay)
plays four real conversations back. The words are a recording and say so; **the numbers are not** —
every Hero Point figure and every finding is worked out in your browser as you reveal it, from the
character the recording carries. No transcript holds a total.

**The character is kept in this browser between visits**, so a refresh or a shared link does not
throw it away, and nothing is sent anywhere. A saved character this build cannot read is discarded
rather than restored: a tool that will not open is worse than one that forgets.

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

Then open the address it prints. It is a static site — `dotnet publish web/ProwlersAndParagons.Web.csproj -c Release` produces a `wwwroot/` that any static host can serve, with no server-side component. The rules JSON is copied into `wwwroot/data/rules/` by the build and fetched over HTTP at startup; `data/rules/` remains the only copy in the repository. The recorded conversations at `/portfolio/replay` are staged from `data/transcripts/` the same way.

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

No Claude of your own? [`/portfolio/replay`](https://prowlers-and-paragons-chargen.pages.dev/portfolio/replay) plays four of these conversations back, with the engine costing and validating in your browser as you read.

---

## Documentation

The detail lives in its own file per domain, so this page stays something you can read in one go.

| | |
|---|---|
| [**Architecture**](docs/ARCHITECTURE.md) | The four layers and the no-upward-dependency rule, and what every directory in the repository is for |
| [**The rules engine**](docs/RULES-ENGINE.md) | Power costs, rank types, baseline ranks, derived statistics, the tiers, the wizard's steps, and the JSON conventions the rules data follows |
| [**Code quality**](docs/CODE-QUALITY.md) | The analyzer contract, the two test projects, and what CI actually enforces |
| [**Deploying the browser front end**](docs/DEPLOYING.md) | Cloudflare Pages, the generated security headers, and the payload |
| [**Connecting the MCP server**](docs/MCP-SETUP.md) | Pointing your own Claude at the published binary |
| [**Rulebook coverage**](docs/RULEBOOK-COVERAGE.md) | Which chapters are extracted and verified, and which are deliberately not |
| [**Extracting the rules**](docs/RULES_EXTRACTION_GUIDE.md) | How the rules data was read out of the PDF, if you need to redo it |

Two more that are not reference material:

- **[`PROGRESS.md`](PROGRESS.md)** — the single source of truth for what is done and what remains,
  with the reasoning behind each decision. Read it before starting anything.
- **[`CLAUDE.md`](CLAUDE.md)** — how to *work* in this codebase: the disciplines, the traps, and
  the rules that have already been got wrong once. Written for an assistant and just as useful to
  a person.

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

Prowlers & Paragons is © LakeSide Games, Inc. (2013–2021), by Leonard A. Pimentel and Sean Patrick Fannon. The rulebook itself is required to play and is not included in this repository.

**What lives in `data/rules/` is structured metadata** — names, costs, ranges, rank types — together with this project's own explanations of what each option does. Those descriptions are written from scratch rather than copied, and that has not changed: `data/rules/` is what the deployed site serves.

**`data/rulebook/` is different, and exists by the author's permission.** It holds the printed text of the book, extracted chapter by chapter, so a player at the owner's table can be shown what a rule actually says. It is **not** part of the browser payload — the web project copies `data/rules` and `data/transcripts` into `wwwroot` and nothing else — so the public site does not serve it. If you have forked this repository, that permission is not yours: it was given to this repository's owner for their table.
