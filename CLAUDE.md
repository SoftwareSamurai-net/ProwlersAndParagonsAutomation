# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Read PROGRESS.md first, and update it before you finish

[`PROGRESS.md`](PROGRESS.md) is the single source of truth for what is done and what remains. Read it before starting anything so you do not re-implement finished work or re-verify locked data.

**Updating it is part of the task, not a follow-up.** Any change that finishes a piece of work, moves a headline number, or uncovers a new gap updates `PROGRESS.md` in the same commit series. Do not leave the reasoning only in a commit message — commit messages are hard to find six months later.

This used to live in two places (the README roadmap and a gaps list further down this file) and drifted out of step with the code. Both now point at `PROGRESS.md`. Do not reintroduce a second list.

## Commands

```bash
# Run the terminal wizard
dotnet run

# Cost and validate a character without a terminal (see "The headless build command")
dotnet run -- build --from character.json --no-export

# Run the browser front end
dotnet run --project web/ProwlersAndParagons.Web.csproj

# Publish the MCP server where a client can launch it (see "The MCP server")
dotnet publish mcp/ProwlersAndParagons.Mcp.csproj -c Release -o mcp-server

# Publish the browser front end as a static site
dotnet publish web/ProwlersAndParagons.Web.csproj --configuration Release

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
- `CanonicalPowers.cs` is the transcribed Range/Rank/Cost of all 141 Powers, `CanonicalPowerProsCons.cs` the 106 Power-specific Pros and Cons, `GearTests` the twelve Ch.6 gear feature prices, and `RulesDataTests` the tier/ability/talent/pro/con/perk/flaw values. **Do not "fix" a failing test by editing these to match the code** — they are the rulebook. Check the page named in the entry's `source_ref` and fix whichever side is wrong.
- `PrebuiltHeroes.cs` transcribes the 20 published Heroes from Ch.8 and `PrebuiltHeroTests` rebuilds each one, asserting the printed Edge, Health and Resolve. Same rule applies: those numbers are the authors', not ours. They are the only tests that check the rules as *applied* rather than as transcribed, so a failure there usually means a rule was misread, not that a number is stale.
- **16 of the 20 Heroes rebuild to exactly 125 Hero Points** and are asserted as such. The other four are held at a recorded residual in `PrebuiltHeroes.BuildByHero`, none more than 1 HP out **as modelled** — Shadow's printed Gear box carries a Silenced pair his transcription does not, which would put him at +2, so the bound is a fact about what is counted rather than about the authors' arithmetic. Do not tune an ambiguous variant just to force one of those to zero — that is fitting the model to the answer. Fix the underlying gap instead.
- **`EveryPublishedHeroIsALegalCharacter` asks the one question the rest of that file does not: would this tool accept the character the authors printed?** Nothing did until it existed, and two rules were wrong because of it — Blastwave came back with four `DUPLICATE_PRO` errors and T-Kay with `PRO_NOT_APPLICABLE` on the Zone Pro printed on her sheet, so two Heroes in the rulebook could not be built here. The budget exemption is keyed to the residual recorded for each Hero, so a Hero who starts costing the wrong amount fails the residual test rather than being excused. A cost test and a legality test are different questions; keep both.
- The package each Hero used is inferred, not printed. `ExactlyOnePackageLandsAnExactHeroOn125` re-runs that inference and asserts exactly one package fits each exact Hero, so the attribution cannot quietly become a convenient guess; for the other four it is the closest fit. Vector is why that test exists — his package was recorded as Superhero on a closest-fit basis while his Deflection was underpriced, and correcting the Power made Hero the only fit.

The root `.csproj` sits at the repository root, so it carries a `<Compile Remove="…" />` for every sibling project directory; without them the default `**/*.cs` glob pulls their sources into the CLI. Shared build settings — target framework, nullability, the analyzer contract — live in `Directory.Build.props`, so the seven projects in the solution — the CLI at the root, `engine`, `sheets`, `web`, `mcp` and the two test projects — cannot drift into different strictness.

The project targets **.NET 10** (`global.json` pins SDK `10.0.100` with `latestMinor` rollForward). The 9.x SDK cannot build it; install with `winget install --id Microsoft.DotNet.SDK.10`.

## Static analysis

- .NET analyzers run at `AnalysisLevel=latest-recommended` with `EnforceCodeStyleInBuild`. `TreatWarningsAsErrors` is conditional on `ContinuousIntegrationBuild`, so local builds stay warning-only while CI is strict. **Keep the CI build at zero warnings.**
- Deliberate rule exceptions live in `.editorconfig` with an inline rationale — CA1305/CA1304 are off because all formatted output is human-facing terminal/sheet text, and CA1822 is a suggestion so `CostCalculator`/`DerivedStatsCalculator` keep a uniform instance API. Add rationale when adding an exception; do not add bare suppressions.
- Qodana (`qodana.yaml`, `jetbrains/qodana-cdnet:2026.2`) runs ReSharper inspections in `.github/workflows/qodana_code_quality.yml`. Two non-obvious constraints: the `dotnet.solution` key is required (without it Qodana finds no project and reports nothing), and the **Community** linter (`cdnet`) is deliberate. **The release linter needs a Cloud licence, not just a token — and a token alone breaks `cdnet` too.** That was tried: with a `QODANA_TOKEN` secret set, both images linked the Cloud project and exited on "License request: token was declined by Qodana Cloud server", having inspected nothing. So the workflow deliberately does **not** pass a token: a scan cannot be broken by a credential it never reads. `cdnet` needs no token, no account and no licence.
- **Qodana is the only thing that sees a Razor deprecation.** A `.razor` file sets a component parameter by string key rather than by referencing the property, so the C# compiler never sees an `[Obsolete]` attribute on it: `Router.NotFound` was deprecated in .NET 10 and `dotnet build` reported zero warnings with warnings-as-errors on. Do not read a clean build as a clean bill of health for the components.
- **Qodana's counts on a pull request are not comparable to a scan of the whole tree.** It runs in PR mode — only changed files — so moving a file re-reports every finding in it as new. The Blazor slice moved `engine/` and `sheets/` into new projects and the count went from 144 to 249 without any of that code changing. Read the SARIF (`gh run download <run-id>`, then `qodana.sarif.json`) rather than the summary table before concluding anything moved.
- **A whole-tree Qodana scan reports zero, and the config that gets it there is in `.editorconfig`, not `qodana.yaml`.** `qodana.yaml`'s `exclude:` list accepts an inspection *name* and silently ignores it — the .NET linter is ReSharper, which takes severities from EditorConfig. Only the path exclusions in `qodana.yaml` do anything. Each `resharper_*_highlighting = none` there is scoped as tightly as the tool allows and says why; nothing is baselined and there is no severity floor. Qodana runs in PR mode, so its count only covers changed files — to see the real number, run it over the whole tree yourself:

  ```bash
  docker run --rm -v "$(pwd -W):/data/project/" -v "$PWD/results:/data/results/" jetbrains/qodana-cdnet:2026.2 --save-report
  ```

  The report is `results/qodana.sarif.json`; the summary counts by rule, never by file, so group it yourself.

  **Run it on a clean export of the commit, not on your working directory.** That command mounts the directory as it is, `bin/` and `obj/` included, and a tree that has been built a few times scans very differently: the same commit reported **0** from `git archive HEAD | tar -x -C <tmp>` and **1471** in place — including `.CSharpErrors`, which is *compile* errors, on test files that build clean. Do not read a number off an in-place scan and conclude anything about the change; export first, and scan the parent commit the same way if you want a comparison.
- What is silenced and why, in one line each: `engine/Models/*.cs` exists to be deserialized by reflection (four inspections), the test transcription records document a rulebook page rather than being read, a `[Theory]` body asserting on its parameter is not a precondition guard, `JsonValue.Create(...)!` is load-bearing (removing it fails the warnings-as-errors build), and this codebase writes explicit constructors and named backing fields on purpose.
- `data/rules/*.json` is copied to the output directory by the csproj, so a published build works without the repo checked out.

## Architecture

Four layers with a strict no-upward-dependency rule, one project each — and three hosts on the
top layer, none of which may hold a rule of its own:

```
                                          ↗   cli/
data/rules/   →   engine/   →   sheets/   →   web/   ←   data/transcripts/
                                          ↘   mcp/
```

- **`data/rules/`** — JSON files only. No logic. All rules data extracted from the P&P Ultimate Edition PDF lives here.
- **`data/transcripts/`** — the second data input, and **not rules**: four recorded conversations the browser replays, read by `engine/TranscriptLibrary`. They are read *through* the engine rather than by it — every character in one goes through `CharacterSheetJson`'s strict reader — and nothing in the engine's rules logic knows they exist. Only `web/` loads them. See "The replay".
- **`engine/`** — Pure C#, zero Spectre.Console references, no filesystem access. `CostCalculator` and `CharacterValidator` are the authority on HP costs and validity. No front end tallies points itself. The one file here that is not rules logic is `SampleCharacters.cs`, which builds two `CharacterSheet`s for preview — see below.
- **`sheets/`** — The `.txt` and `.json` exports, plus the stat-line and gear-line formatters, all returning strings. Shared by every host — the wizard, the browser, the build command and the MCP server; writing a string somewhere is the host's job.
- **`cli/`** — Terminal presentation. Uses Spectre.Console for all rendering. Each wizard step implements `IWizardStep` and receives `CharacterSheet`, `RulesRepository`, `CostCalculator`, and `DerivedStatsCalculator` via `Execute()`.
- **`web/`** — Browser presentation. Blazor WebAssembly; see below.
- **`mcp/`** — Protocol presentation. An MCP server over stdio; see below.

**These are separate projects on purpose, and splitting them was the point of the Blazor slice.** `engine/` and `sheets/` used to be compiled into the root executable, which a WebAssembly project cannot reference without dragging Spectre.Console in with it. Now the arrows above hold at compile time: `web/` has no calculator of its own and no reference that could reach one. Do not merge them back.

### The browser front end

Blazor WebAssembly, so `CostCalculator` and `CharacterValidator` run in the browser *as the same compiled code* the CLI and the tests run. That is the whole reason it is not an HTTP API with a JavaScript SPA — never reimplement cost or validation in the browser, made true by construction rather than by discipline.

- `Program.cs` fetches every name in `RulesRepository.DataFileNames` **before the first render** and hands them to an `InMemoryRulesSource`. The engine is synchronous by design; a half-loaded repository throws.
- **The rules are copied into `web/wwwroot/data/rules/` by the csproj, not committed there** (`wwwroot/data/` is gitignored). `Content Include` with `LinkBase` looks like it would do this and does not — the asset is registered against a content root the file is not under, so every request answers `200` with an empty body. Copy before static-asset discovery.
- `CharacterSession` (scoped) owns the `CharacterSheet` and forwards to the calculators. **Anything resembling arithmetic in that file is a bug.**
- `CharacterSession.TryCost` exists because the engine throws rather than guessing on an incomplete selection — a variable-cost Power with no variant. The editors never commit one, so this is only for the always-on budget bar.
- **`CharacterStore` decides what a stored character is by asking the engine, not by checking its shape.** A saved sheet is nested several levels deep, and `System.Text.Json` will put a null at any of them without the type system objecting — so the guard costs and validates the sheet once and rejects a payload the engine cannot answer for. The first version stripped nulls level by level and missed `"Pros":[null]`, which restored cleanly and then took the app down on the first frame, because the budget bar renders on every route. **Do not replace this with a list of shapes**: the list goes stale the first time somebody adds a field. `InvalidOperationException` is deliberately not caught there — that is a half-finished character, not a corrupt one.
- **Trimming is disabled on publish.** `RulesRepository` deserializes by reflection, so the trimmer can quietly remove model properties and leave the site running on empty rules. See `PROGRESS.md` item 5 before turning it back on.

### The replay

`/replay` plays back four real conversations for somebody who has no way to hold one — the MCP
server needs a Claude of your own, and a visitor to the site has none. The transcripts are in
`data/transcripts/`, read by `engine/TranscriptLibrary`, staged into `wwwroot` by the csproj
exactly as the rules are, and fetched by `Program.cs` from `TranscriptLibrary.FileNames`.

- **A transcript holds characters, never answers about them.** A turn carries a `CharacterSheet`
  — the inputs — and the replay costs and validates it in the browser as the visitor reveals it.
  **If a transcript ever holds a Hero Point total, that is the bug**: the number would sit there
  looking identical while being wrong. `TranscriptTests` refuses a recorded line that quotes a
  Hero Point figure, an Edge, a Health or a Resolve. Ranks are allowed and should be — a rank is
  an input the transcript already carries.
- **What the tests do not cover is whether a recorded sentence about the rules is true**, and
  that gap is not closeable by a regular expression. The characters are held to the engine and
  figures are banned from the prose, but a line saying "the Trait Cap is a limit on Abilities
  alone" passes everything. The cheap conversation shipped for two commits asserting the
  rulebook has no Power for detecting a lie — it has one, at Perception, for a flat price — and
  a person caught it, not a test. **Read a changed transcript against the rulebook.** A green
  suite says the characters are legal and no figure was quoted; it does not say the recording
  is accurate.
- **Several turns of one transcript may carry a character**, and the one about a draft that did
  not fit depends on it: the draft is stored as a draft, so it is *shown* not fitting rather than
  said to be. `TranscriptLibrary` reads them **strictly**, so a field a character no longer has
  fails at load rather than quietly emptying a section.
- **There is one sheet component.** `SheetView` and `DerivedStatBlocks` take an optional
  `Character`; they do not have replay-shaped twins. The four big figures come from
  `DerivedStatBlocks`, which read the character being built — so a replay printed the *visitor's*
  Edge, Health and Resolve under a recorded name until it was given the recorded one. A bUnit
  test loads a sample first so there is a different character present to be printed by mistake.
- **The hand-off gives the editors a copy**, round-tripped through `CharacterSheetJson`. The
  library is read once at startup and shared by every visit; handing the instance over lets the
  first edit rewrite the recording.
- **A failed transcript fetch must not stop the app.** Missing rules are a broken deployment;
  missing recordings are a missing demonstration. `Program.cs` catches, registers an empty
  library and carries the reason so the page can print it.
- **The Villain recording shows its budget finding rather than hiding it.** The GM review step
  hides `HP_BUDGET_EXCEEDED` in Villain mode; here it is shown with Ch.9 beside it, because one
  recording is about exactly that difference. **That branch claims no verdict at all** — not
  "legal", not "not legal yet" — and the reason is broader than the budget: this program is not
  the one that decides whether somebody's Villain is finished, and a word in a heading would be
  read as though it were. The findings themselves are all still printed and all still mean what
  they say.
- **The shell's budget bar does not render on a replay route.** It is the visitor's own
  character in the same six-label format as the recorded one below it, and the two were
  indistinguishable — worst on the Villain, whose own panel deliberately shows no budget, so
  the only budget on the screen belonged to somebody else entirely. `MainLayout` reads the
  first path segment; there is a test through the layout, because a page cannot see the shell.
- The replay does not change the app's palette while you watch; opening the character does, the
  way loading a sample does. Whether a *recorded* character is a Villain has nothing to do with
  what colour the visitor is wearing, which is why `SheetView` takes `ShowBudget` too.

### The headless build command

`dotnet run -- build --from character.json` costs and validates a character, writes both exports and exits **0** (legal), **1** (breaks a rule) or **2** (unreadable input or bad arguments). It exists so a model can propose a character during play and have the engine decide whether it is legal. The skill that teaches that loop is `.claude/skills/prowlers-and-paragons-character/SKILL.md`.

- **Nothing in `cli/Headless/` computes a Hero Point.** The model proposes and the engine decides; inverted, this is a random number generator with good prose. It also **reports and never repairs** — auto-clamping a rank would be the tool making a design decision about somebody's character, and would hide from a player that their concept did not fit.
- **The input is the `CharacterSheet` shape, not the JSON export.** The export is a report and reading it back would rebuild a character from its own conclusions. Reading and writing that shape is `engine/CharacterSheetJson`, shared with the browser's local storage so the two cannot drift.
- **`Read(json, strict: true)` refuses a field name that is not part of a character; `CharacterStore` deliberately does not.** A misspelled `AbilityRanks` in a submitted file drops every ability and produces a cheaper, legal character nobody notices is wrong. Restored storage wants the opposite: a field removed in a later build should cost a field, not the character.
- **Standard output is exactly one JSON report for each of the three exits**, `--help` excepted. Anything about the run itself goes to stderr, so a caller parses stdout whole. There is a test; do not print a warning above the report. `CommandLine` handles the arguments `BuildCommand` never sees, and reports them the same way — it existed as six lines of top-level statements in `Program.cs`, none of them covered, and one answered a misspelled verb with exit 2 and an empty stdout.
- **Every engine call in `Judge` is guarded, and a figure the engine cannot supply comes back null rather than 0.** Reporting 0 for a character that cannot be priced is a lie a caller would act on. `Validate` is guarded too, and separately, because its failure leaves no findings to report at all — it was the one bare call here, under a comment claiming otherwise, and ten shapes of hand-written character came out as a stack trace through it.
- **`CharacterValidator` reports rather than throws, and `CheckHpBudget` carries a `catch` to keep that true.** The specific checks around it — unknown Pro, Con, Perk, nominated Trait, and variant or grade keys that are present but wrong — are what a repair loop acts on; the `catch` only promises the validator answers. Do not delete either for the other: the set of unpriceable shapes grows with every field added, and a validator that throws costs the caller every other finding.
- **A negative quantity is refused by the validator, and `TotalCost`'s arithmetic is `checked`. Nothing floors the total itself.** A negative `Units` on a per-unit Perk paid the character Hero Points and reported an over-budget character legal at exit 0; a very large one wrapped to a negative total and did the same. What stops both now is `NEGATIVE_UNITS` and `checked`, so the verdict is right — but a report for such a character still quotes a negative spend, because the figures are the engine's and the engine was asked to price nonsense. Do not read this bullet as a floor in `CostCalculator`: there is none, and an earlier version of this sentence said there was. Six fields carry a quantity — ability rank, talent rank, a Power's purchased ranks, a Power's `Units`, a Perk's `Units`, and a Pro or Con's `Units` — and the last was the one missed first time. `checked` on the total alone is not enough: the per-unit multiplications underneath it are where the wrap happens.
- **The same Pro or Con twice is refused — unless the rulebook says to buy it again.** Three options say so in their own entries: Also X on Energy Absorption ("each time you select this Pro") and on Energy Form, and the generic Affect Inanimate ("You can apply this Pro multiple times"). That is `repeatable` on the option, never a list of ids in the validator, and it is **two of the seven Also X entries** — the other five are priced per unit, where the quantity is the mechanism and a second copy really would charge twice for one thing. Missing this made **Blastwave illegal**: his six energy types are five copies, the calculator charged all five and landed him on his printed 125, and the validator returned four errors on the same sheet. Every cost floors at zero, so three Burnouts cancelled a 12d Ability exactly — six Abilities at the Trait Cap for 0 HP, reported legal with an empty issue list. A duplicate flaw is refused too (it paid Resolve twice for one drawback); a duplicate **Power** is only a warning, because the rulebook does not forbid it and two Blasts with different Pros is a shape a player might want — what is wrong is that the budget charges for both while the sheet shows the first.
- **`--from` refuses Windows device names.** `File.ReadAllText("CON")` opens the console and blocks for ever: no output, no exit code, no end. `NUL` and `PRN` fail politely and `CON`, `COM1` and `CONIN$` do not, so the whole reserved set is refused rather than the three that were caught.
- **A report must not blame the caller for a fault here.** A duplicate id in a rules file throws the same exception type as a bad character; `CHARACTER_UNUSABLE` used to say "a null where an id belongs, most likely" and send a repair loop after its own file for ever. It no longer guesses, and `ENGINE_COULD_NOT_ANSWER` exists for the other half: a figure the engine cannot supply with no error beside it is this program's fault, and saying so is what stops a caller looping on a legal character.
- `ValidationIssue` carries `SubjectKind`, `SubjectId`, `OwnerId`, `Value`, `Limit` and `Options` beside the sentence, because a repair loop would otherwise have to parse the message back into the facts it was built from. All six are optional; the messages are unchanged and still held to `ValidationMessageTests`.
- **`UNKNOWN_TIER`, `UNKNOWN_PACKAGE`, `UNKNOWN_ABILITY` and `UNKNOWN_TALENT` exist because the wizard picks from a list and a submitted file does not.** An unknown tier used to skip *both* the budget and the Trait Cap checks, so a 99d Ability reported clean. Do not remove them on the grounds that no front end can produce one.
- `SkillDocumentationTests` feeds the skill's own example through the strict reader and validates it. It is documentation of a schema, which rots silently; it caught two errors in its first run. **It must ask `BuildCommand.SubjectKindName` for the wire names rather than converting the enum itself** — it did convert them itself, and so agreed with a broken copy.
- **`ValidationIssueStructureTests` checks its own case list against the validator's source**, so every code the validator can construct is provoked by some sheet. Without it the structural invariants were worth only what somebody remembered to add: two codes shipped uncovered in the change that introduced the invariants, and one invariant would have failed had they been listed. Three codes are exempt by name with a reason; do not add a fourth without one.

### The MCP server

`mcp/` is a stdio MCP server wrapping the same engine, so somebody can describe a character to
their own Claude and get a legal costed one back. It handles no credentials and holds no key —
the conversation happens in the client the user already pays for, and this program only answers
questions about the rules. It does not replace `build --from`; both call the same engine.

- **The question policy is the deliverable, not the transport.** Which two or three questions
  are worth asking is the whole design problem: ask none and you build somebody else's
  character, ask ten and this is a questionnaire wrapped around a wizard that already exists.
  It is written down in **`mcp/QUESTION-POLICY.md`**, which is *embedded in the assembly and
  served verbatim* as the `creation_guide` tool — one copy, so the document the next person
  reads and the document the assistant is taught cannot drift. `McpQuestionPolicyTests` holds
  it to the same standard as the skill: its example character goes through the strict reader
  and the validator, and the four questions are asserted by name.
- **The four that change the build are tier, one-Power-or-several, what the character is
  deliberately ordinary at, and Source** — and the second is the one a model is most tempted
  to answer silently. Everything else is decided and *shown*. The reasoning for each is in the
  document; do not re-derive it from the tool descriptions.
- **Six tools, chosen by what a conversation needs rather than by mirroring the engine.**
  `cost_character` beside `validate_character` is the engine's API: no turn of a conversation
  wants a price without knowing whether the thing priced is allowed, and a separate costing
  tool is an invitation to quote a number for a character that breaks a rule. So
  `check_character` answers both and is the only place the word "legal" is decided. The ten
  catalogues are one `list_options` for the same reason in reverse — ten tools for a dozen
  entries each would crowd out the ones that matter. Powers get two tools of their own because
  141 entries are searched rather than listed.
- **Standard output carries the protocol and nothing else.** Everything said to a human goes to
  standard error. `McpStdioTests` checks this twice, and needs both: it reads the source for
  `Console.` followed by anything but `Error` (not for `Console.WriteLine`, because
  `Console.Out.Write` and `OpenStandardOutput` are the same mistake in other spellings), **and
  it runs the built program and requires every line on that stream to be a JSON-RPC message.**
  The runtime half exists because a source scan cannot see a write from a library or a spelling
  split across two lines; the source half exists because **a stray line does not necessarily
  break a client** — the first runtime test drove the binary through the SDK's own client and
  asserted the session worked, and a real stray line left it perfectly happy, because the client
  skips what it cannot parse. Do not replace either with the other. **This is also why the
  setup guide (`docs/MCP-SETUP.md`) points a client at the published binary rather than at `dotnet run`**, which writes
  MSBuild's own progress to standard output.
- **The rules are found beside the binary, then upwards — never by walking up for a `.sln`.**
  That is the CLI's answer and it is wrong here: a client launches the published program from a
  directory of its own choosing and there may be no repository on the machine. `PROWLERS_RULES_DIR`
  and a first argument override it, and `RulesLocation.Find` **refuses** rather than guessing,
  because a repository built for a directory that is not there gets as far as a connected
  session and then answers every question with an error. **A directory the user named and that
  is not there is a refusal too, not a candidate that failed** — it used to fall through to the
  shipped copy, so a typo in the variable the setup guide tells a stuck user to set produced a
  working server on somebody else's rules and no message at all.
- **A test for any of this has to run the program, not the method it calls.** The startup check
  and the two refusals all had unit tests that passed while `Program.cs` was mutated back to the
  bug — a method nobody calls is not a check. Three tests start the built binary and read its
  exit code, and one of them bounds its own wait, because "the server started anyway" is the
  failure being looked for and a bare wait turns catching it into a run that never ends.
- **The startup check reads every rules file, and `ReadEverything` is what makes that true.**
  A repository loads each file lazily, so warming the tiers alone let a directory holding
  nothing but `tiers.json` start cleanly and then throw out of five of the six tools — the
  exact failure the check exists to prevent, passing its own check. The embedded guide is read
  there too, so the way it goes missing (a csproj edit) is a startup failure rather than a
  conversation that begins with an empty document.
- **`Judgement` guards every engine call and duplicates `BuildCommand`'s guarding on purpose.**
  What is shared is the part that matters — the engine — and the two reports are different
  documents: one names the files it wrote, this one carries a spending breakdown and no paths,
  because this program writes nothing. A figure the engine cannot supply comes back **null**,
  never 0.
- **The per-Power figures need not add up to the powers total, and the report says so.** Super
  Senses is one Power whose options are stored separately, so the group is costed once with one
  floor. The total is the engine's and the parts are indicative — the alternative is a total
  this program added up itself.
- **`search_powers` is deliberately dull**, and its most valuable field is `nothing_matched_by_name`.
  A search that always returns its five best rows reads as five answers however carefully the
  caution is worded, and the description naming something the rulebook does not have is exactly
  the one a model will build anyway. Matching is word by word with a shared-prefix rule, not by
  substring: substring matching answered "she bakes bread in the city" with **Elasticity**, and
  a match like that is worse than none because nothing in it looks wrong.
- **That flag says how the rows matched and never what to conclude**, and the first version got
  this exactly wrong. It attached "usually means the rulebook has no Power for this" — so
  "he can fly" returned Flight and then told the assistant there is no Power for flight, because
  "fly" is not a prefix of "Flight" and the entry matched on the word inside its own
  description. The three cases are told apart in `caution`, and `found: 0` is the only one that
  means the rulebook has nothing. **It is also computed over the whole result and then
  truncated**: computed after `Take(limit)`, a caller asking for one match turned a name match
  at position two into "nothing matched by name at all".
- **No server-side elicitation.** MCP can ask the user a question from the server; the question
  policy deliberately does not use it. The conversation is the client's, and a question asked
  through a schema-shaped dialogue is the questionnaire this design exists to avoid.
- Three traps that cost time here: the embedded resource name comes from **`RootNamespace`**
  (`ProwlersAndParagonsAutomation.Mcp`) and not from `AssemblyName` (`ProwlersAndParagons.Mcp`),
  which differ in this repository; `CallToolResult.IsError` is a **`bool?`** whose null means
  success, so `Assert.False` on it fails every passing call; and the root `.csproj` globs from
  the repository root, so `mcp/` needed its own `<Compile Remove>` like every other sibling.
- The tests live in `tests/ProwlersAndParagonsAutomation.Tests` beside `HeadlessBuildTests`,
  driven over a real pair of pipes with the SDK's own client. Only `web/` has a test project of
  its own, because rendering components needs one.

### Hosting

Cloudflare Pages at `pp.softwaresamurai.net`, by `.github/workflows/deploy.yml` on push to `master`. Direct upload, not Cloudflare's Git integration — two deploy paths can disagree.

- **The deploy workflow must never trigger on `pull_request`.** That trigger runs a contributor's workflow file with the base repository's secrets in scope, which puts the Cloudflare token one PR away from anyone. Adding it would be the single most damaging change available in this repository.
- **`_headers` is generated, never hand-edited.** `scripts/write-cloudflare-headers.sh` hashes the inline import map Blazor writes into `index.html`, whose contents change whenever the framework assets are re-fingerprinted — a hard-coded hash would rot silently and stop the app booting on some later deploy. The script exits non-zero if it finds no inline script rather than shipping a policy that would break the site, and CI runs it too, so a broken policy fails on the PR.
- `style-src` needs `'unsafe-inline'` because the budget bar's width is a live inline style attribute. `script-src` does **not**, and should not gain it.
- `web/wwwroot/_redirects` sends every path to `index.html` with a **200**, not a redirect: a 302 would drop the path and land every shared link on step one.
- `<base href="/">` assumes a root path. A subdomain is fine; a subpath is not, and getting it wrong breaks every asset fetch at once.

### The two sample characters

`SampleCharacters.Hero()` and `.Villain()` return finished Standard-tier sheets, offered on the tier page so a sheet can be previewed without building one. They fill every section a printed sheet has, which an empty sheet does not.

- **They are this project's own characters.** The published Ch.8 Heroes stay in the test suite, where they verify the engine against printed numbers. Shipping them in the app would redistribute the authors' content.
- **`SampleCharacterTests` holds them to the rules** — legal, inside budget, fully priceable, every section filled, at least one Source heading, and both exports rendering. Writing them caught three real mistakes: ranks bought on rankless Powers (`invisibility`, `lightning_reflexes` are `max_rank: 0`), and Danger Sense and Resistance pushed over the Trait Cap because both take a **baseline equal to** an Ability rather than half it. Check `rank_type` and `prerequisite` before adding ranks to a sample.
- The Villain deliberately leaves one Power without a Source, so the sheet shows the plain `POWERS` fallback heading and the review step shows a warning. Both are things a preview should exercise; it is not an oversight.

### The printed sheet is the deliverable

**It is modelled on the published Ultimate Edition Hero Sheet**, which is at `docs/Prowlers_&_Paragons_Ultimate_Edition_Hero_Sheet.pdf` — untracked, because `*.pdf` is gitignored repository-wide, so get your own copy from the publisher. Look at it before changing the layout.

What is reproduced is the **structure**: a masthead of three boxes, three columns (Traits / the Powers stack / the four figures), a foot of free-text boxes, every section ruled with a centred heading in a bar. What is *not* reproduced is any of the trade dress — no hex pattern, no wordmark, no colour scheme. Those are LakeSide Games'.

Two consequences of the reference being a **form** rather than a summary, both deliberate:

- **Every Ability and all twelve Talents print, bought or not**, with a rule where the number goes. A sheet that hides a Talent at 0d is a report of what the tool knows; the published one is something you can write on.
- **Alias, Team, Origin, Notes and Details have no equivalent in the engine and print as labelled blank rules.** Do not delete them for being unbacked, and do not add fields to `CharacterSheet` to fill them — a pen is the right tool for those.

`web/wwwroot/css/app.css` ends with the print stylesheet and it is load-bearing. **Judge it by the PDF, never by the screen** — computed styles cannot tell you whether a page break lands mid-entry.

- **The sheet is one page and should stay one page.** The three columns are equal height and the box marked `fill` in each — Notes and Origin — absorbs the difference, so a short character still prints a full page instead of a third of one. That is a flex `flex: 1` on `.sheet-section.fill` plus `justify-content: space-between` on its rules, not a tuned line count; do not go back to counting lines.
- **The browser prints its own header, and no page can stop it.** The URL, the date and the page number across the top are the print dialogue's "Headers and footers" setting, which belongs to the person printing. The review step tells them where the switch is; that is the only lever there is. Do not add a `@page` margin box or a page counter to try — Chrome supports neither.

**The PDFs are in `docs/`, and a worktree cannot see them.** `*.pdf` is gitignored repository-wide, so both books sit in the main working directory and `docs/` inside a `.claude/worktrees/…` checkout holds only the extraction guide. `ls docs/*.pdf` from a worktree therefore reports nothing, which reads as "there is no rulebook" and is wrong — a whole slice was worked through on that assumption. Look at `<repo root>/docs/`, not the worktree's.

**To read the rulebook itself, extract its text with PdfPig.** There is no `pdftoppm` and no Python on this machine, and the `Read` tool cannot open a PDF without the former — so a scratch console project referencing `PdfPig` is the way in. Group each page's words by rounded baseline and sort descending to recover lines; `page.Text` unbroken is fine for searching.

**The offset is a constant +3** — printed 15 = PDF 18, printed 127 = PDF 130 — and Ch.8's twenty Heroes are PDF pp.130–149. Do not try to read it off a footer casually: **each page prints its number twice, interleaved**, so PDF 18 extracts as `151 5` (two 15s) and PDF 148 as `14154 5` (two 145s). An earlier note here recorded a variable offset on the strength of misreading one of those, and was ten pages out in the chapter it was offered for. Decode a footer carefully, or cross-check against the table of contents on PDF 4, which is in printed numbers.

Chapter marginalia are extractable too (`2 2` beside `chapter`), which is worth checking before citing one: the Random Hero Generator on printed p.63–64 is still **Chapter 2**, not Chapter 3.

How to actually look at a printed sheet, since the browser pane cannot screenshot and headless Chrome cannot wait for Blazor to boot: **render `SheetView` through bUnit and write `.Markup` into a static page** against the real `theme.css` and `app.css`, then print that with `chrome --headless --print-to-pdf`. A throwaway `[Theory]` in the bUnit project taking the output directory from an environment variable does it in one `dotnet test` run — **no dev server**, which is the point: starting one raises an approval dialogue that blocks unattended work. (An earlier note here said to capture `document.querySelector('.sheet').outerHTML` from the running app. That works and needs a server; this does not.)

The same harness screenshots the **screen** design — `--screenshot` instead of `--print-to-pdf`, with the real `theme.css` and `app.css` linked and `data-mode` set. **Pass `--virtual-time-budget=3000` or you will proof a lie.** `.panel` carries `animation: rise var(--enter) both`, which starts at `opacity: 0`, and a bare `--screenshot` fires before it finishes: every panel comes out washed and everything inside one reads as muted text on a faded ground. That was diagnosed as a palette fault and half-fixed as one before the second screenshot showed the label was bright the whole time.

Two things Chrome will waste your time on: `--print-to-pdf` needs an **absolute Windows path** or it fails with "Access is denied", and `--no-pdf-header-footer` is what removes the URL-and-date band so you are judging the sheet rather than the print dialogue. Set `data-mode` on `<html>` in the harness or you will proof one palette twice.

Rasterising the result needs a PDF library (there is no `pdftoppm` or Python on this machine); Docnet.Core plus ImageSharp 3.1.x in a scratch console project works. Pin ImageSharp below 4.0, which refuses to build without a licence key. Repeat the sheet three times in the harness to force breaks through every kind of block.

- **The rule is white paper and readable ink, not "everything black".** A third palette at the bottom of `theme.css` handles print: a shared block fixes the surfaces white and the body text near-black, then each mode restates its own `--heading`, `--rule`, `--accent` and `--muted` as ink. So a Hero sheet prints navy and a Villain crimson, and neither prints the near-black surface that made a Villain sheet a full-bleed ink dump. Restate *every* token the two screen palettes declare — one left out keeps its screen value through the cascade, which is exactly how that happened. Two tests: one resolves the cascade per mode and checks luminance both ways, one checks nothing is missed.
- **Colour on paper is ink, never fill.** The heading bars use `--accent-soft`, a tint, and they are the largest run of colour on the page at about 6mm. `--primary` stays white in print because it is a *fill* token — the sheet banner used it, and filling a banner strip solid costs a cartridge a character. Backgrounds also need `print-color-adjust: exact`, or browsers drop them and the sheet prints half-styled.
- **Hero Point costs are set apart from ranks** (`.hp`): smaller, lighter, letter-spaced, muted. A rank is what you roll; a cost is bookkeeping consulted only when rebuilding the character, and in the same face the sheet read as a receipt.
- **A Trait with no ranks bought prints `0d`, not a blank rule.** 0d is a fact about the character. The blank rules are only for Alias, Team, Origin, Notes and Details — the fields the engine genuinely has no answer for.
- **A print rule that corrects a screen rule must match its specificity.** The print block is one `@media print` at the bottom of the same file, so it does not win by being later — `@media` adds nothing to specificity. `.ruled { gap: 4mm }` (0,1,0) silently lost to `.sheet-section.fill > .ruled { gap: 0 }` (0,3,0), which left Notes and Origin — the two boxes that exist to be written on — with their lines about 2mm apart. The same trap put gear flush right: `td[colspan]` and `td:last-child` weigh the same, so the fix held on source order alone until it was written `tr > td[colspan]`.
- **`break-inside: avoid` on `.sheet-section`, except the Powers groups *and* the `fill` boxes.** `fill` stretches to the height of the tallest column and the Powers column is unbounded, so it is the second thing on the page that can exceed a page — and the failure is the same one: Chrome pushes the whole box to the next page and abandons the rest of the current one. Both opt out, and a test names both.
- **`break-inside: avoid` on `.sheet-section`, except the Powers groups.** Every small box asks not to be broken, which is what stops a four-line Gear box straddling a page. A Powers group carries `.powers` and opts back out: Chrome honours the request by pushing the whole box to the next page first, and on a fifteen-Power character that left **two thirds of page one blank**. The one box that can exceed a page is the one that must be allowed to break.
- **Set `align-items: start` on any grid of ruled boxes.** The default `stretch` makes a one-line Perks box as tall as the Flaws box beside it — on paper, a ruled void that can run a whole page.
- **`--focus` is a separate token from `--accent`.** A focus ring is a non-text indicator and WCAG 1.4.11 wants 3:1; Hero `--accent` is 1.8:1 on `--surface`, which is a ring nobody can see. `--muted` is held to 4.5:1 rather than 3:1 because it carries the explanatory prose at 0.72–0.82rem. Both were measured, not eyeballed; re-measure if you change them.
- **A `position: fixed` running footer does not work.** Chrome's print output renders it once, at the top of page two, over the content. The character's name repeats across pages via the **document title**, which the browser prints in its own header — that is why `Review.razor`'s `<PageTitle>` leads with the name. A test asserts `position: fixed` never returns to the print block.
- `h1 { display: none }` in print: the page heading is the tool's, not the sheet's.

### Two test projects, and the difference between them

- **`tests/ProwlersAndParagonsAutomation.Tests`** — the rules engine, plus `WebPresentationTests`, which *reads the source* of `web/` because the disciplines below are statements about how it is written, and `HeadlessBuildTests`, which drives the `build` command end to end, and the three `Mcp*Tests`, which drive the MCP server over a pair of pipes. **The wizard itself still has no harness** — that is the CLI gap, and it is narrower than it was rather than closed.
- **`tests/ProwlersAndParagons.Web.Tests`** — bUnit. It *renders components* and asserts on the output, and it is the only project that may reference `web/`.

**The split is the point.** A source-reading test cannot see a bug in rendered output, and one duly shipped: Razor swallowed the space in `@name` + `<text> @(rank)d</text>` and the sheet printed **"Armor8d"**. It was fixed on the sheet and the same bug in a second spelling survived on the Powers tab for another whole slice, because no source file looks wrong. Anything about what a component *produces* belongs in the bUnit project; anything about how the source is *written* belongs in the other.

**Assert on `TextContent`, never on markup with the tags stripped out.** Stripping a tag leaves a separator where it was, so `<b>Armor</b><span>8d</span>` reads as "Armor 8d" to any test that does it — which is how the Powers tab kept the Armor8d bug through a test written to catch it. It cuts the other way too, and worse: a `DoesNotContain("Communications 0d")` over stripped markup is satisfied by printing exactly that with the two halves in different elements. An adversarial pass did it, visibly, with the suite green. The browser concatenates text nodes; so must the test.

**A typographic rule lives in the stylesheet, where no rendering test can see it.** Emptying `.hp` puts Hero Point costs back in the same size, weight and ink as ranks and every bUnit test still passes, because the class is still on the element. Anything whose whole substance is CSS — the `.hp` treatment, print font sizes, the break rules — is asserted in `WebPresentationTests` against the parsed rule, not inferred from markup.

bUnit pulls AngleSharp transitively at a version carrying a published advisory, so `web/`'s test project pins AngleSharp forward. Do not suppress NU1902 instead — see the comment in its csproj.

### The browser front end's three presentation rules

All three are asserted by `WebPresentationTests`, which reads the source because none of them is visible to a compiler.

1. **No component names a colour.** Checked by hex, by keyword, *and* by `rgb()`/`hsl()`/`oklch()` function syntax — that last one is the loophole a hex grep leaves open. `transparent` is allowed; it is the absence of a colour. Radii and durations are tokens for the same reason, and `prefers-reduced-motion` turns every animation off by setting three duration tokens to `0.01ms` — not `0`, which makes some engines skip `transitionend` entirely.
2. **Nothing on screen names an internal type or a build command.** Asserted on the *prose*, which `VisibleText` derives by stripping `@* *@` comments, the `@code` block, every tag (and so every attribute) and every Razor expression — so `@PowerFormatter.StatLine(p)` is fine and the same characters in a paragraph are not. The rule is general: no compound PascalCase type declared in `engine/` or `sheets/` may appear. The reverse is asserted too — `Ch.6`, `Ch.9`, `Trait Cap` and `Hero Point` must still appear *in the prose*, since deleting the rulebook references would satisfy a naive reading of this rule and ruin the app. (Asserted against the raw file, that test passed while `Ch.6` survived only in a comment.)

   **The validator's messages are the other half of this surface**, and `web/`'s tests cannot see them — they are engine strings, printed verbatim on the GM review step and in both exports. `ValidationMessageTests` provokes them from real sheets and holds them to the same rule: no file name, no internal flag, no bare id where the rulebook has a name, no `flaw(s)`, and every message a sentence.
3. **One component owns each repeated class.** `Panel`, `Field`, `SheetSection`, `StatBlock`, `DerivedStatBlocks`, `OptionList`/`OptionRow`, `ChosenList`/`ChosenRow`. Writing `class="panel"` by hand anywhere else fails a test. The budget bar's live fill width is the **only** inline style left, and it is the sole justification for `style-src 'unsafe-inline'` in the CSP.

Two Razor traps this surface has already hit:

- **Razor strips the leading whitespace inside a `<text>` block, and inside an element that follows an expression.** `<text> @(rank)d</text>` after a name printed `Armor8d` on the sheet, and `<span class="muted"> @(rank)d</span>` did the same on the Powers tab. Put the separator inside one expression: `@(rank > 0 ? $" {rank}d" : "")`.
- **Blazor will not mix implicit child content with a named fragment.** Once any child is written as a named element the rest must be too — so `<Panel>` with a `<Head>` also needs an explicit `<ChildContent>`, and `ChosenRow` names both its slots `Body` and `Actions`. Implicit content on its own is fine, which is why most `<Panel>` call sites do not write `<ChildContent>`.
- **A `true` bool bound to an `aria-*` attribute renders as `aria-pressed=""`.** Blazor drops the attribute when the value is false and emits an empty string when it is true — and empty is invalid ARIA that assistive technology reads as *not* pressed, so the obvious spelling announces the opposite of the state in both directions. Bind a `"true"`/`"false"` string.

### Hero and Villain are one app with two palettes

Ch.9 builds Villains exactly like Heroes and prints no separate stat-block format, so the mode is presentation and nothing else.

- Both palettes are CSS custom properties on `:root[data-mode="hero"]` and `[data-mode="villain"]` in `web/wwwroot/css/theme.css`. **No component ever names a colour** — that is what keeps the switch a one-attribute change, and there is a grep in the PR notes proving it holds.
- `--primary` is a **fill** and `--heading` is **text**. They coincide in the Hero theme and must still be kept apart: Villain `--primary` measures 2.0:1 on its surface and is unreadable as type. Hero `--accent` is 1.8:1 for the same reason. Re-measure if you restyle; do not eyeball it.
- **Do not add a Hero/Villain flag to `CharacterSheet`.** The only mechanical difference is that a Villain has no Hero Point budget, which the front end handles by hiding the bar and filtering `HP_BUDGET_EXCEEDED` from the display. The validator is never told the mode, so the export still records every issue.
- Only the palette differs. If a layout change seems necessary for one mode, the layout is wrong for both.

### Key engine types

| Type | Role |
|---|---|
| `CharacterSheet` | Mutable wizard state — all purchases accumulate here |
| `RulesRepository` | Lazy JSON loader with snake_case deserialization and cached lookup dictionaries |
| `CostCalculator` | HP cost logic — `PowerCost()`, `PerkCost()`, `TotalCost()`; all methods are pure |
| `DerivedStatsCalculator` | Edge, Health, Resolve, baseline/effective rank calculations |
| `PowerFormatter` (sheets) | Renders a Power's rulebook stat line (`Self · Baseline Rank (½ Toughness) · 1 HP per rank`) so output can be checked against the book |
| `CharacterSheetRenderer` (sheets) | Builds the `.txt` and `.json` exports as strings, for whichever host asked |
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

Three Edge details are easy to get wrong and were all bugs at one point: Danger Sense **replaces** Perception rather than adding to it, Lightning Reflexes is a **flat +6** with no rank, and Super Speed is missing from most summaries. All three are verified against Ch.2 — p.60 names the three, and their own entries are pp.25, 33 and 44.

Halves always round **up** — the rulebook has a global rule for this (the Glossary in the Introduction, p.7, "Half").

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

- **Overkill/Weak reduce the rate by 1 HP per rank, floored at 0.5** — *not* a ×0.5 multiplier. Ch.2: "reduces a Power's base cost by 1 Hero Point per rank (or changes its base cost from 1 Hero Point per rank to 1 Hero Point per 2 ranks)." A previous version halved the rate, which mispriced every 2 and 3 HP/rank power. The "Brute Option" is the separate Ch.2 p.17 rule for applying Overkill to Might.
- **The minimum is per rank, not per power.** Ch.2: "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons." See `MinimumRankedCost`. Specialty is the sole 0 HP power.
- Variable-cost pros/cons (e.g. Charges, Area/Burst) store their variants in `CostModifierRange`; `SelectedProCon.VariantKey` picks the right value.
- **Generic pros/cons are always flat; a power's own pros/cons may change its rate.** `powers.json` carries `power_pros` / `power_cons` for the 106 entries the rulebook attaches to one named Power. Eleven are per-rank — Constructs' Devices is +2 HP *per rank* — so `CostCalculator.ResolveModifiers` returns flat and rate totals separately and `RankedParts` adds the rate part to the Power's own rate before multiplying. Resolution prefers a power's own entry over a generic one with the same id.
- Of those 106: 102 carry a PRO/CON marker inside a Ch.2 Power entry, three carry one in Ch.7's Toxins section (pp.108-109, on Stun and Slay), and one — Deflection's *Physical and Energy* — has no marker because the Power's own text states it as prose. A whole-book sweep for the marker confirms there are no others.
- The rulebook minimum is **1 HP per 2 ranks**, not 1 HP per rank. Reading it the other way puts the floor exactly at the undiscounted cost of a 1 HP/rank power, which silently voids every Con on it. See `MinimumRankedCost`.
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

### Starting packages

A package **buys the ranks it grants** — `AbilityCost`/`TalentCost` only charge for ranks above the package's own rank. The Superhero Package is 50 HP for 3d in six Abilities and twelve Talents, which is 54 bought separately; the rulebook sells packages "at a small discount", so charging the price on top of full-rate ranks double-pays and makes a package strictly worse than none. That was a real bug, found because seven published Heroes came out exactly 4 HP over — the Superhero discount.

### Pros and Cons on Abilities

Abilities can carry them too, not just Powers — `CharacterSheet.AbilityModifiers`. Overkill on Might is the Brute Option and halves it; everything else is flat, floored at 0. Only ranks the package does not already cover are discountable.

**The Ability is part of the Brute Option, not decoration.** `AbilityCost` did not check *which* Ability carried the Con, so Overkill or Weak on any of the six halved it — 12d Intellect for 6 HP, legal, with one Con on it. Ch.2 p.17 names Might and nothing else. No published Hero moved when it was fixed: Stronghold's four armoured Abilities carry Item, not Overkill.

**A Pro or Con on an Ability the character has bought no ranks in is reported.** `AbilityCost` walks `AbilityRanks`, so a modifier keyed anywhere else never resolves and the Con the player recorded is silently worth nothing.

### Gear costs nothing (mostly)

Ch.6: mundane gear is free and **explicitly not tracked**, so `ChooseGearStep` taking free text with no HP cost is correct — do not "fix" it. A Gear Limit caps the Trait rank usable with mundane gear (6d default); it is not a budget. Signature equipment is a Power with the Item Con.

Custom *features* on mundane gear do cost HP: twelve of them at 1–2 HP each in `gear_features.json`, ten flat and two graded, plus ordinary Pros and Cons on the item. `CostCalculator.GearCost` prices one item and `TotalGearCost` feeds `TotalCost`. Three things about gear differ from Powers:

- **Gear floors at 0 HP, not 1.** "Regardless of Cons, no piece of gear can cost less than 0 Hero Points." Cons discount an item to free and stop.
- **The Item Con is not credited.** Ch.6 says every piece of gear has it, which is a statement of what gear *is*, not a discount to claim — and Item is absent from the same page's list of Cons commonly applied to gear. Crediting it would make every 1 HP feature free.
- **Two-Fisted customises a matched pair for one price.** A pair is one `SelectedGear` with `PairedUnderTwoFisted` set, so it is charged once by construction; the validator checks the Power is actually there.

### Super Senses is one Power

Ch.2: "Regardless of the options you select, Super Senses is always considered a single Power." Its sixteen options are separate `super_senses_*` entries only because each carries its own price. `CostCalculator.TotalPowersCost` therefore sums the group before applying its Cons and its floor **once** — `PowerCost` still answers per option, which is what the wizard and sheet display.

The floor is what this changes: most options cost 1 HP flat, so per option a Con would be swallowed by that option's own floor and be worth nothing. Do not "fix" this by applying the Con to every option instead; that reaches the same numbers but multiplies a Con the sheet wrote once. Super Senses is the **only** such group — Transformation says "Regardless of which Transformation Power you possess", plural, and Form makes no grouping claim, so both stay priced entry by entry.

### Generic Pro/Con applicability is derived, never listed per Power

`powers.json` deliberately has **no** `available_pros` / `available_cons`. The rulebook states applicability inside each generic option — "This Pro applies to Zone Powers", "applies to Powers that only affect you" — not inside the Power, so `ProConApplicability` answers it from the option. Do not reintroduce per-Power lists; the ones that used to exist were invented, left 68 of the 141 Powers with no generic Pro at all, and offered the Ranged Pro on six Self-range Powers.

Only constraints the book prints for every Power are enforced: `applies_to_ranges` (Self/Touch/Ranged/Zone/Special, Ch.2 p.19) and `applies_to_rank_types` (Degrades alone). Ten entries carry one. A Range of **Special** is never filtered out — the book says such Powers work in ways their description defines, so nothing can be ruled out for them.

**One thing does come from the Power, and it only ever widens: `pros_allowed_by_own_text`.** A Power whose own printed text tells you to apply a named generic option overrides that option's Range rule. Force Field is the only entry that carries it: it is Self range, and its entry (Ch.2 p.29) reads "Apply the **Zone** Pro to shield large areas, the **Ranged** Pro to shield things at a distance, or the **Area** Pro to shield large areas at a distance" — while T-Kay, printed on p.143, has `Force Field 12d (Zone)`. Until it existed, both editors refused that Pro and the validator called a Hero in the rulebook illegal, so **a published character could not be built in this tool**. It is the same shape as Deflection covering both attack types, which the book also states as prose rather than as a marked PRO. **This is not the removed `available_pros` list and must not become one**: that list guessed which options suited a Power and filtered absolutely; this records a printed sentence, needs one behind every entry, and there is a test that no other Power claims it and that the three Pros reach no other Self-range Power. A sweep of Ch.2 for prose naming a generic Pro found one other case, Illusions and the Zone Pro, left alone because no printed character exercises it and the book gives no price for Zone on a Zone-range Power.

Everything else an option states — "Powers that inflict physical or energy damage", "that can be activated and deactivated at will" — is an `applicability_caveat`: shown to the player, never enforced. Enforcing it would mean ~7 booleans × 141 Powers of fresh guesswork. Ch.2 calls the list "not intended to cover every possible option" and puts it under GM approval, so a caveat is the honest model. **A caveat must never become a filter** — there is a test.

**Those ten enforced constraints are checked by `CharacterValidator` as well as by the two editors' pickers**, as `PRO_NOT_APPLICABLE` / `CON_NOT_APPLICABLE`. They were the pickers' business alone until then, so a submitted character could carry the Ranged Pro on a Self-range Power and exit 0 — a hole in the claim the headless command exists to make. Only generic options on a Power are checked: a Pro printed inside a Power's own entry is applicable to that Power by definition, and gear and Abilities have no Range for an option to object to. A test builds every one of the 141 Powers with every option the pickers offer it and asserts the validator refuses none of them, so the two can never disagree in either direction.

### `data/rulebook/` is the book; `data/rules/` is the mechanics

Ten files, one per chapter, holding the printed text of the whole Ultimate Edition with the page each section came from. **The rights position changed to allow this** — the author gave the repository owner permission to use the book's data, so the older rule that no rulebook wording may appear here no longer applies to this store. It still applies to `data/rules/`.

- **They answer different questions and must not be merged.** `data/rules/` is the *mechanics* — structured, verified entry by entry against the page, and the only thing the engine reads. `data/rulebook/` is the *text*, so a player can be shown what a Power says. No cost, rank or validity comes from the corpus, and where the two disagree, `data/rules/` wins.
- **It is not in the browser payload, and that is deliberate rather than an oversight.** `web/`'s csproj copies `data/rules` and `data/transcripts` into `wwwroot` and nothing else, so the deployed public site does not serve the book. The intended reader is account-gated and comes after the front-end redesign; until it exists there is nothing to serve and no reason to publish the text to the open web. Turning it on is one `ItemGroup` — do not turn it on by accident.
- **The book is two-column, and reading it by baseline alone interleaves the columns.** Printed p.52's heading extracts as `OVERKILL PHASE SHIFT`, which is two entries, and every entry then carries its neighbour's text. The extractor splits each page at the gutter. `RulebookCorpusTests` checks a known entry reads back with its stat line and without its facing neighbour's name.
- **The PDF watermarks every page with the purchaser's name and order number, as four separate words.** A filter written against the whole phrase matches none of them, which put somebody's personal data into the prose of every chapter. It is filtered per token and asserted absent across the corpus.
- Headings are detected by case and length, so a mis-detected heading is a *structural* error; the prose beneath one is still verbatim.

### The engine never touches the filesystem

`RulesRepository` reads through `IRulesSource`, not `File.ReadAllText`. Two implementations ship: `FileSystemRulesSource` (the CLI) and `InMemoryRulesSource` (any host that loads the data itself — a browser has no filesystem). `RulesRepository(string)` and `FromBasePath` still work exactly as before.

**Keep `IRulesSource` synchronous.** Making it async would push `await` through every lazy collection and from there into `CostCalculator` and `CharacterValidator`, turning a pure instantly-callable engine into an async one for no gain. A host that can only load asynchronously does that once at startup and hands over strings.

`RulesRepository.DataFileNames` lists every file a self-loading host must fetch — it cannot glob a directory that isn't there. **Add a new rules file to that list**, or a browser build silently runs on an incomplete rules set; a test enforces it.

### Sources, and the default rank

Six of them (Ch.2 p.16), in `sources.json`: Innate, Magic, Psychic, Super, Tech, Trained. Each names the Ability that stands in as a **rankless** Power's rank whenever Powers act on other Powers (Drain, Nullify, Dispel, Power Absorption, Power Mimicry). Innate/Super/Tech → Toughness; Magic/Psychic/**Trained** → Willpower. Trained is the one people guess wrong.

`DerivedStatsCalculator.GetRankAgainstPowers` answers that. It is **deliberately separate from `GetEffectiveRank`**, which still returns 0 for a rankless Power. The default rank substitutes only against other Powers — it is not the Power's rank, and folding it in would change Edge and Resolve away from the figures the published sheets print. There is a test; do not "simplify" the two into one.

A Source costs nothing and changes no rank, so a missing one **on a Power** is a warning, not an error. **On an Ability or Talent it is not reported at all**, and that difference is the rule rather than a gap: Ch.2 p.16 gives those two a default — Innate and Trained — so silence means "on its default". A Power has no default, which is why `POWER_WITHOUT_SOURCE` exists and no Trait equivalent does.

### Sheets group Powers by Source

`SourceGrouping` lives in `engine/`, not in a renderer, because the text sheet, the JSON export, the GM review and the browser's sheet all need the same answer. Published sheets print `TECH POWERS`, `MAGIC POWERS` and so on rather than one flat list, and all four surfaces do too.

- Groups follow `sources.json` order, so a sheet does not reshuffle as Powers are added.
- A Power with **no** Source still prints, under a plain `POWERS` heading at the end. Do not "tidy" this by filtering it out — leaving a Power off its own character sheet is worse than showing it unsourced, and the validator already warns. A **Trait** with no Source is different: it is not unsourced, it is on its default, so it prints nothing.
- **A group can hold no Powers at all** — a Trait bought through powered armour on a character with no Tech Power — so nothing that renders groups may gate on `SelectedPowers.Count`. Three places did. The Powers *tab* is the one deliberate exception: it edits Powers, and a heading with nothing under it says less than no heading.

### Sources on Abilities and Talents

Ch.2 p.16: **every** Ability, Talent and Power has a Source; Abilities are usually Innate and Talents usually Trained "at least when dealing with ordinary people. When dealing with supers and characters who aren't human, however, anything goes." `CharacterSheet.AbilitySources` / `TalentSources` record only the Traits that deviate, which is exactly what a sheet prints.

- **Abilities are not marked on the Abilities block, and that is correct.** A sheet records the Source as an `Abilities (Might, Toughness)` line *inside* the relevant Power group. Stronghold's `TECH POWERS` opens with his four armoured Abilities. The Abilities and Talents tables stay plain lists of ranks.
- **Do not derive the line from rank.** Ch.2 p.64 — the *random generation* chapter — says "Sources for your Powers and Abilities with a rank of 7d or greater", and reading that as a threshold is contradicted by the sheets in both directions: Alabama Slammer marks 6d Perception and Toughness; Citizen Soldier leaves 9d Willpower unmarked. It is an author's exception list, so it is stored. `ThePrintedTraitSourcesAreNotARankThreshold` names both counterexamples.
- **Two printed shapes, and one extrapolated from them.** Ch.8 shows named Traits — `Abilities (Might, Toughness)` — and, where every Ability *and* every Talent share a Source, `Abilities and Talents (All)` (both Heralds and Nano). **A single block alone reading `(All)` is this project's extension of that convention**, not something any sheet prints: the three Heroes that mark Talents at all mark every Trait. Named Traits print in `abilities.json`/`talents.json` order, which is the order the sheets print them in — and is also alphabetical, so no test can tell the two apart.
- **A Trait explicitly set to its own default prints nothing**, same as one never touched. Same Source, same statement — so the editors remove the entry rather than storing it, or an ordinary character prints eighteen lines restating the rulebook.
- **Abilities on one Source are split by the Pros and Cons they carry**, because the marking covers the whole printed line. The engine prints `(Item)` where the book prints `(Item: armor)`: `SelectedProCon` has no free-text label. Recorded, not tuned away.
- A Source costs nothing and changes no rank, which is why the **persistence round trip cannot see one** through cost or the derived stats. It compares the Source headings and trait lines instead — still an engine answer, not a field list.

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
- **Power `description` values in `data/rules/` stay original text written from the rulebook entry, never rulebook prose.** The rulebook text now lives in `data/rulebook/` instead — see below — and the two must not be merged: `data/rules/` is what the deployed site serves, and the descriptions there are what a player reads while choosing. Descriptions exist so a player can tell what they are choosing and what resists it, and they must agree with the mechanics beside them — `PowerDescriptionTests` fails a rankless power whose description claims per-rank scaling, which is how the original set went wrong on 44 of the 46 rankless powers.
- `powers.json` has **141** entries. Form, Transformation and Super Senses are single Powers in the rulebook but each of their options is bought separately at its own cost, so each option is its own entry. Super Senses is nonetheless *costed* as one Power — see above; splitting it is a storage decision, not a rules one.
- `gear_features.json` holds the twelve Ch.6 custom features. `cost_type` is `flat` (with `cost`) or `flat_variable` (with `cost_range`, for the two the rulebook prices at 1 to 2 HP).

## Settled — do not redo

Open work lives in [`PROGRESS.md`](PROGRESS.md), not here. What follows is the short list of things already decided, kept inline because the cost of re-litigating them is high.

Back-navigation, JSON export and engine unit tests are **done** — do not re-implement them.

**All rules data in chapters 1–2 is verified and locked by tests.** Every one of the 141 power entries (range, rank type, cost, baseline, description) plus all tiers, abilities, talents, pros, cons, perks and flaws has been checked against the book, and no `needs_review` flag remains anywhere in `data/rules/`. Do not re-verify these, and do not reintroduce a uniform `cost_per_rank`.

Settled rules questions:

- Lightning Reflexes is a **flat +6** Edge bonus on a flat 3 HP unranked Power
- Danger Sense **replaces** Perception in the Edge calculation; it is not added to it
- Super Speed sets Edge to **rank × 3**
- Determination is **5 HP per 1 Resolve** with no rank
- Overkill and Weak are a **−1 HP per rank** rate reduction, floored at 1 HP per 2 ranks — not a halving
- The minimum cost of a ranked power is **1 HP per 2 ranks**; for an unranked one it is 1 HP. A piece of **gear** floors at **0** instead
- Super Senses is costed as **one Power**, not one per option — the rulebook says so in as many words
- Deflection covers one attack type; covering **both doubles its rate** to 2 HP per rank, stated in the Power's own text rather than as a marked Pro
- The Item Con is **not** credited against a piece of gear
- Generic Pro/Con applicability is **derived from the option**, never listed on the Power; unenforceable constraints are caveats, not filters. The single exception widens rather than narrows and is a record of printed text: a Power whose own entry names a generic option overrides that option's Range rule, which is `pros_allowed_by_own_text` and today is Force Field alone
- An option is taken **once**, unless its own entry says to buy it again — Also X on Energy Absorption and on Energy Form, and the generic Affect Inanimate, marked `repeatable` in the data. Not the other five Also X entries, which are priced per unit
- A rankless Power's **default rank** comes from its Source and applies **only** against other Powers — it is not its effective rank
- Sheets group Powers under Source headings; an Ability's or Talent's Source prints as a line **inside** a Power group, never as a marking on the Abilities block, and it is **not** derivable from rank
- The Iconic tier's "200+" is explicitly a bare minimum, so it is GM discretion rather than missing data
- Hero and Villain are **one app with two palettes**, and the mode is not a field on `CharacterSheet`
- `engine/`, `sheets/`, `cli/` and `web/` are **separate projects**, so the dependency arrows hold at compile time rather than by convention
- Assisted creation *in this repository, for somebody with it checked out*, is a **non-interactive command plus a skill** — and the model proposes while the engine decides, never the other way round. **For somebody else, connecting their own Claude, it is an MCP server**, which is the mechanism built for exactly that and lets us handle no credentials at all. The two are not in tension and both call the same engine; the earlier flat "not an MCP server" note was scoped to the first case and is superseded
- **A visitor to the site cannot bring their own Claude, and that is settled — do not re-investigate it.** A claude.ai subscription cannot be lent to a third-party site, the API is separate billing with no dependable free tier, and custom connectors are gated to paid plans. The answer is `/replay`: real conversations recorded, with the engine run for real in the visitor's browser. A proxy funded by the owner was rejected — it costs money, invites abuse, and breaks the static-site property the README advertises
- An illegal character is **reported, never repaired**: the engine is a judge and does not make design decisions about somebody's character

Each of these was wrong at some point and is now covered by a regression test naming the rule. If one appears to be violated, read `PROGRESS.md` and the test before changing the code.

### The 1d minimum on every Trait

Ch.2 states it twice — once for Abilities (p.17), once for Talents (p.18): **no rank can be lower than 1d**, and "ordinary people have 2d in every" one. So **a character has all six Abilities and all twelve Talents**, and 0d is not a low rank but a Trait nobody can be without. `TRAIT_BELOW_MINIMUM` enforces it.

- **It costs Hero Points.** Without a package you pay for all eighteen at 1d — 18 HP before anything interesting. The Civilian Package's 35 HP for 2d in all eighteen is 36 points of ranks, which is exactly the "small discount" the rulebook calls a package. That is the corroboration, and it is why the reading is not negotiable.
- **All twenty published Heroes take a package**, so every one of their Traits sits at or above its floor. That is why rebuilding them never caught this, and why a rule can be missing for a long time without the strongest test in the suite noticing.
- **`TRAIT_BELOW_PACKAGE` is the other floor**: a package's granted ranks "cannot be lowered below the package rank". It costs nothing to break — `AbilityCost` and `TalentCost` charge only for ranks above what the package covers — so the mistake was free and therefore silent. It is the rule that proved Herald (Airmid)'s recorded package impossible.
- **A printed sheet still shows 0d for a Trait not filled in yet**, and should: the sheet is a form, and 0d is a fact about the page in front of you. The validator is what says the character is not finished.
