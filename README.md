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

**A front door, a builder, and the whole rulebook searchable.** `/` offers the two things this site
does; `/build` is the six creation steps; `/rules` searches the printed text of all ten chapters and
**cites the page it came from**, so an answer is checkable against the book on the table. Matching is
word by word with a shared-prefix rule rather than by substring — a plausible wrong match is worse
than none — and the flags on a result say *how* it matched and never what to conclude.

**Hovering an option or a Trait says what it is.** The lists priced things and never said what they
were; the descriptions were in the rules data the whole time with nothing showing them. Never a
`title` attribute — on a row the row is the trigger, on a Trait the name is a real button.

**The rules, complete for character creation and locked by tests.** All 141 Powers with their
printed Range, rank type and cost; 27 baseline-rank Powers; 23 generic Pros and 28 generic Cons
including the variable-cost ones; the 106 Power-specific Pros and Cons, several of which change a
Power's cost *per rank*; 53 Flaws, 13 Perks, the six Sources and the tiers. A data edit that
contradicts the book fails CI. → [The rules engine](docs/guide/rules-engine.md)

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

**Run the fight through the engine too.** A second MCP server, `prowlers-and-paragons-play`, resolves
an encounter a turn at a time out of Chapters 3–5 — or the same matchup over a few hundred seeded
runs — and every answer names the rule it applied and the page it is printed on. The engine
resolves; the model narrates. → [Setting it up](docs/MCP-SETUP.md)

**And if you have no Claude of your own**, four real conversations are recorded and play back at
`/admin/portfolio/replay`. The words are a recording and say so; **the numbers are not** — every
Hero Point figure and every finding is worked out in your browser as you reveal it, from the
character the recording carries. No transcript holds a total.

**They are behind an account now, and that is a change rather than an oversight.** They used to be
public, on the reasoning that a visitor who cannot bring their own Claude should still see assisted
creation working. The site's owner moved them: this is not a sign-up, the demonstrations are a
thing to show somebody rather than a thing to publish, and a visitor arriving to build a character
had to walk past them. The character builder and everything it needs are still open to anybody.

**The character is kept in this browser between visits**, so a refresh or a shared link does not
throw it away, and nothing is sent anywhere. A saved character this build cannot read is discarded
rather than restored: a tool that will not open is worse than one that forgets.

**Invitation only, and no password anywhere.** An account is what opens the rulebook, and it is a
list somebody maintains rather than a sign-up form. Adding an address emails it a one-click sign-in
link; the token is single-use and stored only as a SHA-256. Asking for a link always answers the
same thing whether or not the address is known, so the endpoint cannot be used to ask who has an
account. → [Setting up accounts](docs/ACCOUNTS-SETUP.md)

**Every screenshot is checked, not eyeballed.** Nine browser harnesses assert measured verdicts —
does the strip actually stick, does anything overflow at 375px, does the theme survive a reload —
and seven proof pages are pixel-diffed against goldens rendered on Linux. Four palettes are more
pixels than a person can hold in their head.

---

## Prerequisites

**Building and running the four front ends needs only the .NET SDK.** The engine, both interactive
apps, the headless build command and the MCP server are all `dotnet`.

| Tool | Version |
|---|---|
| .NET SDK | **10.0.100 or newer** (pinned in `global.json`, `rollForward: latestMinor`) |
| IDE | JetBrains Rider, Visual Studio, or VS Code + C# Dev Kit — optional |

If `dotnet build` fails with *"A compatible .NET SDK was not found"*, you are on an older SDK. Install .NET 10:

```bash
winget install --id Microsoft.DotNet.SDK.10
```

**Running everything under `scripts/`, including most of the test suite, needs more.** The accounts
server is JavaScript because Cloudflare Workers is, so eight of the twelve scripts there need Node;
two of those also want Chrome or Docker, each for a specific, narrow reason rather than as a general
dependency:

| Tool | Version | What needs it |
|---|---|---|
| Node | **22 or newer** | `test-worker.sh`, `e2e.sh`, `test-visual.sh`, `test-deploy-gate.sh`, `apply-migrations.sh`, `count-tests.sh`, the inline-\*.mjs data bakers, `probe-mail.mjs` |
| Chrome | any real build | `e2e.sh` drives it over the DevTools Protocol — no pixel comparison, so any Chrome answers |
| Docker | running daemon | **only** `qodana-scan.sh` (no non-container form) and `visual-regression.sh`'s pixel comparison specifically — [`docs/guide/testing.md`](docs/guide/testing.md) measures why even two different real *Linux* Chromes disagree by tens of thousands of pixels, so nothing installed locally makes that comparison trustworthy off Linux; `e2e.sh` and everything else fall back to Docker only when Node itself is missing |

On macOS with Homebrew, `./scripts/dev-setup.sh` installs the .NET SDK, Node and Chrome at the
versions this repository already pins elsewhere (`global.json`'s SDK version, the `# wrangler=`
comment `deploy.yml` carries), reading each rather than restating it — and explains why it does not
install Docker, and what stays unavailable without it. Linux CI already has Node and Chrome on the
runner image; a Windows machine follows the `winget` command each script prints when it cannot find
what it needs.

The source rulebook PDF is **not included** in this repository (copyright), and never will be. Keep your own copy wherever you like and pass its path to the extractor — `dotnet run --project tools/RulebookExtractor -- <path-to-pdf> data/rulebook`. `*.pdf` is gitignored repository-wide and CI fails the build if a PDF is ever tracked, so there is no in-repo location to put it in; `docs/` is not one either, and saying so sent more than one reader looking for a file that has never been there.

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

Then open the address it prints. `dotnet publish web/ProwlersAndParagons.Web.csproj -c Release` produces a `wwwroot/` any static host can serve, and **the character generator needs nothing else** — the rules JSON is copied into `wwwroot/data/rules/` by the build and fetched over HTTP at startup, and every cost and every verdict is worked out in the browser. `data/rules/` remains the only copy in the repository.

**What does need a server is the account half**: the rulebook reader, the saved characters, and the recorded conversations. Those are Cloudflare Pages Functions under `functions/` and `worker/`, and the book and the recordings are **bundled into the server rather than staged into `wwwroot`** — a file under `wwwroot` is a public URL, and that placement is the whole access control. There is a test on both sides of the repository.

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

**Working in a checkout of this repository, one command sets it up** — `dotnet publish mcp/ProwlersAndParagons.Mcp.csproj -c Release -o mcp-server`, and [`.mcp.json`](.mcp.json) registers that copy for any clone, machine or git worktree with no absolute path to install. It is a published copy rather than the build output because a server running out of `mcp/bin/` blocks a Release build of this repository. Claude Code asks once per checkout before it will start a server a repository proposed — answer it in the session, or list the server in `enabledMcpjsonServers` in that checkout's `.claude/settings.local.json`.

**→ [Setting it up on your machine](docs/MCP-SETUP.md)** — that checked-in registration, publishing a copy for a client working outside the checkout, registering it with Claude Code or Claude Desktop, what the six tools are for, and what to check when it does not connect.

It is one file rather than a section here because the setup is the part a stranger needs and the part with the traps in it — and every one of them belongs to the published copy, which is why a checkout should not make one: the path has to outlive a git worktree, and the published server carries its own copy of the rules, so it keeps answering with old ones, perfectly happily, until you re-publish. A registration naming a path that stopped existing is how this server failed here once, reporting `CONNECTION_CLOSED` with no log to read because nothing had started.

No Claude of your own? Four of these conversations are recorded and play back at `/admin/portfolio/replay`, with the engine costing and validating in your browser as you read. That page needs an account — see the note above.

---

## Documentation

The detail lives in its own file per domain, so this page stays something you can read in one go.

| | |
|---|---|
| [**Architecture**](docs/ARCHITECTURE.md) | The four layers and the no-upward-dependency rule, and what every directory in the repository is for |
| [**The rules engine**](docs/guide/rules-engine.md) | Power costs, rank types, baseline ranks, derived statistics, the Sources, and the JSON conventions the rules data follows |
| [**The play engine**](docs/guide/play-engine.md) | The second engine, which resolves a fight rather than costing a character: the dice contract, the ledger, and the book's worked examples replayed through it |
| [**Tests and code quality**](docs/guide/testing.md) | The two test projects, the analyzer contract, what CI actually enforces, and the pixel goldens |
| [**Hosting**](docs/HOSTING.md) | Where the running site lives — the DNS chain, the Pages project, the D1 database, the mail provider, and why each piece is where it is |
| [**Deploying the browser front end**](docs/DEPLOYING.md) | Cloudflare Pages, the generated security headers, and the payload |
| [**Connecting the MCP server**](docs/MCP-SETUP.md) | Pointing your own Claude at the published binary |
| [**Turning accounts on**](docs/ACCOUNTS-SETUP.md) | The database, the binding and the sending domain that sign-in needs — and why nothing breaks before they exist |
| [**Rulebook coverage**](docs/RULEBOOK-COVERAGE.md) | Which chapters are extracted and verified, and which are deliberately not |

Two more that are not reference material:

- **[`PROGRESS.md`](PROGRESS.md)** — the single source of truth for what is done and what remains,
  with the reasoning behind each decision. Read it before starting anything.
- **[`CLAUDE.md`](CLAUDE.md)** — how to *work* in this codebase: the disciplines and traps that
  apply whatever you are touching, and a routing table into the guide set. Written for an
  assistant and just as useful to a person.
- **[`docs/guide/`](docs/guide/)** — one file per area: the rules engine, the browser front end,
  the printed sheet, the replay, the MCP server, the accounts server, the rulebook corpus, tests,
  hosting and the terminal wizard. Each holds the rules that have already been got wrong once in
  that area. `CLAUDE.md` says which to read for what you are about to touch.

---
## Roadmap

**[PROGRESS.md](PROGRESS.md) is the single source of truth** for what is done and what remains, with the reasoning behind each item.

It is deliberately **not** summarised here. This section twice grew a numbered copy of that list, and both times it drifted: the second one still advertised the Blazor front end and the Hero/Villain sheet as future work after both had shipped. A short version is not cheaper than one list — it is a second list that nobody remembers to update.

---

## Versioning

Two things are stable across changes here and tested to stay that way: `data/rules/` and the
character JSON (what a saved sheet, `.character.json` and `build --from` all depend on).

The HTTP API is not stable yet — `/api/character` became `/api/characters/{id}` recently. When
it stops moving this repo will adopt [Semantic Versioning](https://semver.org/) and
[Conventional Commits](https://www.conventionalcommits.org/) at 1.0.

---

## License

**MIT** — see [LICENSE](LICENSE). It covers this repository's own code and text and nothing else. The tool is not sold, and any hosted instance is self-hosted.

Prowlers & Paragons is © LakeSide Games, Inc. (2013–2021), by Leonard A. Pimentel and Sean Patrick Fannon. The rulebook itself is required to play and is not included in this repository.

**What lives in `data/rules/` is structured metadata** — names, costs, ranges, rank types — together with this project's own explanations of what each option does. Those descriptions are written from scratch rather than copied, and that has not changed: `data/rules/` is what the deployed site serves.

**`data/rulebook/` is different, and exists by the author's permission.** It holds the printed text of the book, extracted chapter by chapter, so a player at the owner's table can be shown what a rule actually says. It is **not** part of the browser payload — the web project copies `data/rules` and `data/transcripts` into `wwwroot` and nothing else — so the public site does not serve it. If you have forked this repository, that permission is not yours: it was given to this repository's owner for their table.
