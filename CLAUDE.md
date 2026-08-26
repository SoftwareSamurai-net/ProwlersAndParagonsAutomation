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

# Run the accounts server's tests — a separate suite, because that server is JavaScript.
# Uses local Node 22+ if there is one, Docker otherwise. Also run in CI.
./scripts/test-worker.sh

# Ask the mail provider why it refused a send, instead of deploying to find out.
# Needs .dev.vars — see "Diagnosing the mail path" below, and never write to that path.
node scripts/probe-mail.mjs you@example.com
```

## Two disciplines that are commands, not cautions

Both of these were written here as warnings first, and both were then ignored by somebody who
had read them in the same session. A caution does not fire hundreds of steps later, when you are
thinking about something else. These are phrased as things to *run*.

### Before any destructive revert, stash

```bash
git stash push -u -m pre-experiment
```

**Run it before `git checkout -- .`, `git checkout -- <file>`, `git reset --hard`, or letting any
mutation pass revert for you.** `git checkout --` takes uncommitted work with it, silently and
with no confirmation. That has now cost this project rework three times, the last of them by an
agent that had read this paragraph's predecessor earlier in the same session and reverted one
file to undo a mutation, taking an unrelated uncommitted change with it.

There is no judgement call to make about whether a particular revert is risky. Stash first. If the
stash turns out to be empty, it cost nothing; `git stash pop` afterwards is one command.

**Committing first is better still** where the work is in a committable state — a mutation
experiment run against committed work has nothing to lose. Stash is for when it is not.

**And the loss does not announce itself at the commit — it announces itself as a commit message
that describes a change the commit does not contain.** That has now happened here too, to somebody
who had read the paragraph above in the same session: a fix was written, the suite was run green,
a mutation was applied *on top of it* to check a guard, and `git checkout -- <file>` reverted both.
The staged razor and test files still looked like the change, `git commit` succeeded, and the
stylesheet half was simply gone. Two habits catch it and neither is a judgement call:

- **Re-run the suite *after* the revert, never only before it.** A green run taken before a
  `checkout` says nothing about the tree being committed.
- **Read `git show --stat HEAD` against what the message claims.** A missing file in that list is
  the whole failure, visible in one line.

The deeper rule is the one at the top of this section: a mutation belongs against *committed* work.
If the fix had been committed before the guard was mutated, there would have been nothing to lose.


**And when it goes wrong anyway, git has probably still got it.**

```bash
git reflog                                  # every HEAD move: bad reset, bad rebase, lost commit
git fsck --unreachable | grep commit        # dropped stashes and orphaned commits
git stash apply <sha>                       # recover one by hand
git show <sha>:path/to/file                 # or just read one file out of it
```

`git stash pop` **prints the SHA it dropped** — `Dropped refs/stash@{0} (c1c89e…)`. That line is the
cheapest recovery handle there is, and piping the pop to `/dev/null` throws it away. Do not.

**The line that decides whether any of this works is whether an object was ever created.** A stash,
a commit, even a bare `git add`, all write objects that survive being dropped and are findable
above. A working-tree edit that was never stashed, added or committed is not an object, and
`git checkout -- <file>` over it is unrecoverable by any means — which is exactly the loss this
section opens with. So the stash rule is not only prevention: **it is what makes recovery possible
at all.**

Related, for the other direction: when something *is* broken and nobody knows since when,
`git bisect run <command>` will find the commit. It takes any command whose exit code says
good-or-bad, so the harness drivers work directly — a script that regenerates the proofs and greps
`<title>` for `PASS` is a usable bisect predicate, and would have located a regression this project
shipped inside a fix.

### A check is not done until you have broken it and watched it fail

**Write the guard, then deliberately break the thing it guards, then run it and see it go red.**
Not "reason about whether it would catch it" — run it. A check that has never failed is a claim,
and the claim is usually wrong: this repository has now shipped, on separate occasions,

- a guard that passed because a one-character inversion left every asserted string in place,
- a guard that passed because its `setTimeout` was present and did nothing,
- a proof whose four checks passed because a missing stylesheet meant **the animation never ran
  at all**, so every assertion about the end state held trivially,
- an inset measurement that reported a spread of `0.00px` while one band was visibly 60px out of
  line, because `getBoundingClientRect()` returns the border box and the break was padding.

Every one of those was found by breaking it. None was found by reading it.

**And every harness carries a positive control**: assert that the work *happened* — an execution
counter, `getAnimations().length`, an element count, a scroll position that actually moved —
**before** asserting that its outcome was right. Three of the four failures above were a feature
that did not run being mistaken for a feature that worked, which is the single most common way a
check in this repository has been wrong.

A mutation that is semantically null does not count as breaking it. Removing the assigned resting
frame from the counting figure changes nothing observable, because the easing already reaches
exactly 1 at `t=1`; the honest report is that the mutation was a no-op, not that the guard has a
hole. Break it with something that changes the answer.

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

**A crashed test process still prints `Passed! - Failed: 0`.** A stack overflow (e.g. an endless render loop) exits with `Catastrophic failure ... exit code -1073741571` — that is `0xC00000FD`; the process is dead, the summary line is a lie. CI notices via the exit code; a human tailing the log for `Passed!` does not. **Grep for `Catastrophic` and check the total moved.** `node --test` has the same trap in another spelling: an empty glob exits 0 reporting zero tests. Both suites' CI steps assert the count for this reason.

## Static analysis

- .NET analyzers run at `AnalysisLevel=latest-recommended` with `EnforceCodeStyleInBuild`. `TreatWarningsAsErrors` is conditional on `ContinuousIntegrationBuild`, so local builds stay warning-only while CI is strict. **Keep the CI build at zero warnings.**
- Deliberate rule exceptions live in `.editorconfig` with an inline rationale — CA1305/CA1304 are off because all formatted output is human-facing terminal/sheet text, and CA1822 is a suggestion so `CostCalculator`/`DerivedStatsCalculator` keep a uniform instance API. Add rationale when adding an exception; do not add bare suppressions.
- Qodana (`qodana.yaml`, `jetbrains/qodana-cdnet:2026.2`) runs ReSharper inspections in `.github/workflows/qodana_code_quality.yml`. Two non-obvious constraints: the `dotnet.solution` key is required (without it Qodana finds no project and reports nothing), and the **Community** linter (`cdnet`) is deliberate. **The release linter needs a Cloud licence, not just a token — and a token alone breaks `cdnet` too.** That was tried: with a `QODANA_TOKEN` secret set, both images linked the Cloud project and exited on "License request: token was declined by Qodana Cloud server", having inspected nothing. So the workflow deliberately does **not** pass a token: a scan cannot be broken by a credential it never reads. `cdnet` needs no token, no account and no licence.
- **Qodana is the only thing that sees a Razor deprecation, and `.razor` files really are inspected — measured, not assumed.** A `.razor` file sets a component parameter by string key rather than by referencing the property, so the C# compiler never sees an `[Obsolete]` attribute on it: `Router.NotFound` was deprecated in .NET 10 and `dotnet build` reported zero warnings with warnings-as-errors on. Do not read a clean build as a clean bill of health for the components.

  **Do not conclude the opposite from an empty SARIF, which is a mistake made here.** Four whole-tree scans in one slice produced no finding referencing any `.razor` file, and that was read as "Razor is not inspected at all" — a conclusion that would have condemned the whole tool. It is wrong: no *finding* in a file is not the same as the file not being analysed. Proved by planting the same unresolvable `<see cref="DoesNotExist"/>` in a `.razor` `@code` block and in a `.cs` file and scanning once: **both** reported `InvalidXmlDocComment`, and the SARIF named `web/Components/RulebookEntry.razor`. Razor `@code` blocks get `UnusedMember.Local` too.

- **What Qodana does *not* catch is a doubled `<summary>` block, and this file used to imply it does.** The note further down cites, as a real bug no compiler sees, "a doc comment stranded on the wrong method by an insertion, so one member carried two `<summary>` blocks and a `<paramref>` for a parameter it did not have". Those are two different things and only the second fires: a `<paramref>` naming a parameter that is not there is `InvalidXmlDocComment`; **two `<summary>` blocks on one member is reported by nothing.** Measured both ways — planted in a `.cs` file and left in place in `MainLayout.razor`, where one has sat since before this slice; neither was reported, on a scan that found six other things. ReSharper ships duplicate-tag rules for `param` and `typeparam` and none for `summary`. So a stranded summary is found by reading, and by nothing else.

  **Both of the above were established with a control that fires.** A probe that reports nothing tells you nothing unless something in the same run reports — which is why the cref pair is the instrument: the `.cs` half is known to fire, so the `.razor` half reporting or not is a real answer either way. And plant probes in a `git archive` export, then **strip `bin/` and `obj/` after checking they compile** — a probe that does not compile suppresses inspections and reads exactly like "not inspected".
- **Qodana's counts on a pull request are not comparable to a scan of the whole tree.** It runs in PR mode — only changed files — so moving a file re-reports every finding in it as new. The Blazor slice moved `engine/` and `sheets/` into new projects and the count went from 144 to 249 without any of that code changing. Read the SARIF (`gh run download <run-id>`, then `qodana.sarif.json`) rather than the summary table before concluding anything moved.
- **A whole-tree Qodana scan reports zero, and the config that gets it there is in `.editorconfig`, not `qodana.yaml`.** `qodana.yaml`'s `exclude:` list accepts an inspection *name* and silently ignores it — the .NET linter is ReSharper, which takes severities from EditorConfig. Only the path exclusions in `qodana.yaml` do anything. Each `resharper_*_highlighting = none` there is scoped as tightly as the tool allows and says why; nothing is baselined and there is no severity floor. Qodana runs in PR mode, so its count only covers changed files — to see the real number, run it over the whole tree yourself:

  **This claim rots, and it has rotted twice.** It was 3 on `master` and 37 across the three reconciled audit slices before anybody measured; a previous slice found the same thing. Two of those 37 were real bugs no compiler sees — a doc comment stranded on the wrong method by an insertion, so one member carried two `<summary>` blocks and a `<paramref>` for a parameter it did not have. **Do not repeat the "reports zero" sentence without re-running the scan**, and read the count out of the log *and* the SARIF: a `grep` for the summary line prints nothing when the scan never ran, which looks identical to clean.

  ```bash
  docker run --rm -v "$(pwd -W):/data/project/" -v "$PWD/results:/data/results/" jetbrains/qodana-cdnet:2026.2 --save-report
  ```

  The report is `results/qodana.sarif.json`; the summary counts by rule, never by file, so group it yourself.

  **Run it on a clean export of the commit, not on your working directory.** That command mounts the directory as it is, `bin/` and `obj/` included, and a tree that has been built a few times scans very differently: the same commit reported **0** from `git archive HEAD | tar -x -C <tmp>` and **1471** in place — including `.CSharpErrors`, which is *compile* errors, on test files that build clean. Do not read a number off an in-place scan and conclude anything about the change; export first, and scan the parent commit the same way if you want a comparison.

### Do not run that command by hand. Run `./scripts/qodana-scan.sh`

```bash
./scripts/qodana-scan.sh
```

**Because a scan that never ran is indistinguishable from a clean one, and the by-hand version has now produced exactly that.** `qodana` exits **0** when it cannot find a project to inspect — it prints its own `--help` and one line of error at the end of a long log, writes no SARIF, and every `grep` for a summary line comes back empty, which reads as "nothing found". The script closes the three traps that make a by-hand scan worthless, each with a control that fires:

- **A Git Bash path handed to `-v` unconverted mounts an empty directory.** `-v /tmp/export:/data/project/` gives Docker Desktop a path that means nothing inside the VM, so the container finds no `qodana.yaml`, exits 0, and inspects nothing. That is what went wrong. **The fix is `pwd -W`, not avoiding `/tmp`** — measured both ways: with `pwd -W` an export under `/tmp` scans perfectly, and without it the same export scans nothing. The script converts every host path and additionally stages under the repository (`.qodana-scan/`, gitignored), which is belt to that brace rather than the fix. It then **proves the mount** by looking for `qodana.yaml` from inside a container before starting the scan — and that guard has been watched to fire: an unconverted path exits 1 naming the path, rather than scanning nothing and reporting zero.
- **The exit code is not evidence.** The script requires `qodana.sarif.json` to exist and to parse, and exits non-zero with the tail of the log when it does not. A zero it prints is a zero from a report that exists. Watched to fire too: with no report written it exits 1 saying `NOTHING WAS INSPECTED` and reports that the container's own status was 0, which is the whole trap in one line.
- **The export needs its own control.** A `git archive` that produced nothing scans an empty tree, which is trap one again; the script checks `qodana.yaml` is in the export before mounting anything.

It also groups the findings by file, which the tool's own summary never does — that summary counts by rule.
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
- **`data/transcripts/`** — the second data input, and **not rules**: four recorded conversations the browser replays, read by `engine/TranscriptLibrary`. They are read *through* the engine rather than by it — every character in one goes through `CharacterSheetJson`'s strict reader — and nothing in the engine's rules logic knows they exist. Only `web/` reads them this way; `worker/` also holds a baked copy of the same bytes, gated behind an account, but relays them without parsing a word of them — see "The accounts server". See "The replay".
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
- **An animation may interpolate between two engine answers; it may never invent one.** The rule
  above is about *authority*, not about every pixel: a frame part-way through a counting figure is
  transient presentation, and the engine stays authoritative for any figure that **comes to rest,
  is exported, or is read back**. So `ppCount` is allowed to draw the numbers between 105 and 118
  because both ends are `CostCalculator` answers and the resting frame is **assigned rather than
  computed** — the figure that settles is the engine's exactly, not a rounding of an interpolation.
  What stays forbidden is the shape this permits people to reach for: counting *towards* a figure
  the engine has not returned yet, easing a bar to a predicted width, or holding a stale number on
  screen because the animation is still running. If an animation would show a number nobody asked
  the engine for, it is the bug this rule has always been about.
- `CharacterSession.TryCost` exists because the engine throws rather than guessing on an incomplete selection — a variable-cost Power with no variant. The editors never commit one, so this is only for the always-on budget bar.
- **`CharacterStore` decides what a stored character is by asking the engine, not by checking its shape.** A saved sheet is nested several levels deep, and `System.Text.Json` will put a null at any of them without the type system objecting — so the guard costs and validates the sheet once and rejects a payload the engine cannot answer for. The first version stripped nulls level by level and missed `"Pros":[null]`, which restored cleanly and then took the app down on the first frame, because the budget bar renders on every route. **Do not replace this with a list of shapes**: the list goes stale the first time somebody adds a field. `InvalidOperationException` is deliberately not caught there — that is a half-finished character, not a corrupt one.
- **Trimming is disabled on publish.** `RulesRepository` deserializes by reflection, so the trimmer can quietly remove model properties and leave the site running on empty rules. See `PROGRESS.md` item 5 before turning it back on.

### Four areas, and the address decides which

**`Areas.Of` reads the first path segment and every band of chrome follows it.** `""` is the front
door, `build` the six creation steps, `rules` the reference, and `admin` (with `signin`) the account
pages. `MainLayout` draws the step list and the budget strip in `Play` alone.

- **The builder is under `/build` and `/` is a chooser, which reverses the old shape.** The tier page
  was both the first screen *and* step one, so a visitor who had not decided what they came for met
  step one of a job they had not chosen — and the only other thing the site does was a single link in
  the banner. This is also the objection `ChooseTier.razor` already recorded when the two sample
  characters were moved off it: a demonstration is not a step in making your own character.
- **An unrouted address falls to `Home`, not to `Play`.** The default is what the not-found page gets,
  and a numbered step list with one step marked current, above "no such address", offers to continue
  something that never started. Falling back to the builder was safe only while the builder was every
  address.
- **Both avenues are offered from everywhere, rather than one link naming whichever half you are not
  in.** That flipping label works for two rooms and fails for three: it identifies a destination only
  while there is exactly one elsewhere. `EveryAvenueIsOfferedFromEverywhere` pins it on four routes.
- **Only the builder names the palette in the banner.** A rules search is not a Hero or a Villain, and
  saying so there is the banner reporting the visitor's own character over a page with nothing to do
  with it — the fault the budget strip was pulled off three areas to fix.

### The front door

`/` presents what the site does and offers a way into each, with **every figure on it the engine's or
the server's and none written into the page**. That is the claim the whole site rests on: the Powers
count is read off the rules the app is running, and the spend beside a character in progress is the
same `TryCost` call the budget strip makes. A figure typed into `Home.razor` would be the one number
on the site nobody had checked.

- **It draws no builder chrome**, for the reason in the area note above.
- **The spend is shown only when the engine can give one.** A half-chosen Power is a question the
  engine refuses rather than guesses at, and the honest front door for that character is its name and
  no figure.
- **`.figure` is the same face, size and ink as the four derived stats and the budget strip's spend.**
  One numeral treatment in this app, used wherever a number is the point of the screen; a second one
  here would make the front door's figures read as a different kind of thing from the ones the rest
  of the app answers with. They are the same engine's answers.

### The rules reference

`/rules` searches the whole book and cites the printed page. The corpus is bundled into the worker
and never staged into `wwwroot` — that placement is the access control — so every address under
`/api/rulebook/` is refused to anybody not signed in, **on the prefix rather than on the four
addresses**, because a fifth added below the check but matched above it would be reachable by
anybody.

- **All ten chapters, and the entitlement question is settled.** It was Chapter 2 alone while the only
  reader was a Power's entry beside the editor; the owner has decided an account may read the book,
  and lifted the older restrictions on shipping the rulebook text and the published characters.
  `scripts/inline-rulebook.mjs` globs `data/rulebook/` rather than naming files, and there is a test
  that it names none — a filename in that script is a list that goes stale the first time a chapter
  is added, and the failure would be a chapter silently missing from the search.
- **The matching rule is `Mentions` from the MCP server, ported to `worker/search.js`.** Word by word
  with a shared-prefix rule, never substring.
- **What that rule buys here is narrower than what it buys there, and the first version of the comment
  claimed the wider thing.** Over there the baker's sentence — *"she bakes bread in the city"* —
  finds nothing, because the haystack is 141 short Power entries. Here it is the whole book, where
  "city" is a word the text genuinely uses: *City of Heroes* in the introduction, "a city, forest,
  jungle" in Attuned. **Twenty-one real matches, measured.** The property worth pinning is that the
  sentence must not reach **Plasticity**, which is what substring matching did — with the positive
  control beside it, since a search that has stopped working satisfies every absence.
- **The stopword list is deliberately not the Powers search's.** That one drops "power", "powers",
  "character" and "super" because they carry no information *about a Power*; here they are section
  headings a reader will actually type.
- **The index is built on the first search, not when the module loads.** A Worker gets a small budget
  of startup CPU and three quarters of a megabyte of prose is not a thing to spend it on for a request
  that may never ask a question. It is **keyed on the corpus it was built from** — cached on a bare
  null check it would answer a second corpus from the first one's index, silently and plausibly.
- **The flags say how the results matched and never what to conclude.** `found: 0` is the only answer
  that means the book is silent; `nothingMatchedByHeading` says every passage matched in its body,
  which is ordinary for a question phrased as a question. It is computed over the whole result and
  **then** the list is cut, or a caller asking for one row turns a heading match at position two into
  "nothing matched by heading at all".
- **The row cap is the server's and the caller cannot raise it.** The alternative is one response
  carrying fifteen hundred passages and their snippets.

### The sheet beside the editors

`SheetView` is drawn in a second column on the characteristics step above 1500px — Phase 4 of
`docs/FRONT-END-PLAN.md`. It is the same component the review step and the recordings render; there
is one sheet in this app by design.

- **`--column` widens on the token, so all five bands follow it.** The shell, the banner, the step
  list, the budget strip and the breakdown agree on one figure and there is a test holding them
  together; widening the shell alone would leave four bands behind and read as columns that nearly
  line up. It widens the sheet and the recordings too, which is a decision rather than a side effect.
- **`SheetView` had to be told to redraw, and nothing could have told us.** It reads the session and
  takes no parameter that changes, so Blazor has nothing to compare and skips it when the parent
  re-renders — measured, with the tab strip above it reporting one Power beside a sheet still drawing
  twelve blank rules. It was invisible while the only sheet on screen was the review step's, where
  the character is finished before anybody looks. **It subscribes only when `Character` is null**: a
  recording is handed over as a parameter, and tying it to the visitor's edits is the influence the
  replay renders two pages to forbid.
- **The preview is on the characteristics step alone.** That is where the character is built and
  nothing there types letter by letter — the ranks are steppers and the lists are pickers, so the
  sheet redraws on a choice rather than on a keystroke. The finishing step is where the free text is.
- **It reflows to fewer columns and that is right.** `.sheet-columns` is `auto-fit, minmax(280px, …)`,
  so a ~560px column fits one or two; three at 180px each would be worse. The three-column
  arrangement is a fact about the paper, judged on the review step and in the PDF.

### An option or a Trait says what it is, on hover and on focus

The lists priced things and never said what they were: a Powers row printed a name, a stat line and a
category, and an Ability row a name, a rank and the rulebook's word for it. The descriptions were in
`data/rules` the whole time with nothing showing them.

- **Never a `title` attribute** — the rule the app already lives by, with a guard.
- **On an option row the row is the trigger**, because it is a button already and 141 extra tab stops
  would undo `OptionList`'s one-tab-stop keyboard model. **On a Trait row the name is a real button**,
  because that row is a slider and two steppers and making all of it the trigger opens a description
  every time somebody reaches for the `+`.
- **`display: none` when shut, not `visibility: hidden`** — the precedent on `AClosedTipTakesNoLayoutBox`
  is exact: a hidden element keeps its box, and an absolutely-positioned tip that keeps its box put
  real horizontal overflow into CI once already. The `sr-only` copy is what `aria-describedby` names,
  so the description never leaves the document.
- **A row that already prints its description gets no tip.** Perks, Flaws and both kinds of Pro and
  Con print theirs as the row's caveat; a tip there says the same sentence twice and covers the row
  below. Asserted, with the control that the Powers list still has them.
- **The marking is `--muted`, not `--rule`.** `--rule` is the hairline between sections and under a
  word it is invisible, which made the only marking on the control no marking at all. Found in a
  screenshot; no rendering test could have, because the class is on the element either way.

### The replay

`/admin/portfolio/replay` plays back four real conversations for somebody who has no way to hold one
— the MCP server needs a Claude of your own. The transcripts are in `data/transcripts/`, read by
`engine/TranscriptLibrary`, and bundled into the worker at build time — see `worker/transcripts-corpus.js`
and `scripts/inline-transcripts.mjs` — the same way the rulebook corpus is. They are fetched, all
four at once, from `api/transcripts` by `ReplayLoader`, on demand rather than at startup.

**They are behind the account pages now, and that reverses a settled decision deliberately.** The
older entry read that a visitor cannot bring their own Claude and the replay is the answer; the
site's owner has decided otherwise — this is not a sign-up, and the recordings and the two samples
are a thing to show somebody rather than a thing to publish. The pages are wrapped in `AdminOnly`,
which asks the server on every visit and holds no claim of its own.

**It is a real gate now, not a front door.** This entry used to say the opposite: the transcripts
were ordinary files under `wwwroot`, so anybody who knew a filename could fetch one and only the
*pages* were gated. They are bundled into the worker instead and answered only to a signed-in
caller at `api/transcripts` — the same placement that is the whole access control for the rulebook
corpus, and for the same reason: a file under `wwwroot` is a public URL, and no amount of checking
sessions in the browser would make it not be one. `tests/worker/transcripts.test.mjs` holds the
gate to a signed-in caller, with a positive control, and asserts the refused body carries none of
the recorded text. Moving them off `wwwroot` also took them out of every visitor's startup fetch —
`ReplayLoader` fetches once, the first time a component actually asks for a recording, so a visitor
who never opens the replay never asks the server for one at all.

- **A transcript holds characters, never answers about them.** A turn carries a `CharacterSheet`
  — the inputs — and the replay costs and validates it in the browser as the visitor reveals it.
  **If a transcript ever holds a Hero Point total, that is the bug**: the number would sit there
  looking identical while being wrong. `TranscriptTests` refuses a recorded line that quotes a
  Hero Point figure, an Edge, a Health or a Resolve. Ranks are allowed and should be — a rank is
  an input the transcript already carries. **Two rules do that, and both are needed**: one bans
  the *shape* — a number next to a word about money, in one clause — and one asks the engine
  what the figures actually are and bans those numerals, in digits and spelled out. The second
  exists because "She lands on 75 exactly, and the tier hands her 75 to spend" matches no
  vocabulary anybody could write; the first because "over by a full nineteen" is a quoted figure
  whether or not nineteen is the right answer. The scan reads the **character** as well as the
  prose, by reflection rather than by naming its free-text fields.
- **The question-count tests catch drift, not a questionnaire written to evade them.** A demand
  phrased with an unlisted verb scores zero, and the companion test counts the person's
  *replies*, so demands bundled into one turn cost one reply. That is recorded in the tests
  themselves; the real guarantee is the same one the prose has — read a changed transcript.
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
  library is fetched once, the first time something asks `ReplayLoader` for it, and shared by
  every visit after that; handing the instance over lets the first edit rewrite the recording.
- **A failed transcript fetch must not stop the app.** Missing rules are a broken deployment;
  missing recordings are a missing demonstration. `ReplayLibrary.LoadAsync` catches, returns an
  empty library and carries the reason so the page can print it — and **it is a method rather
  than a block wherever it is called from, because that is where nothing could reach it.** It
  was once a `try`/`catch` in `Program.cs`'s top-level statements, back when the fetch happened
  at startup; deleting the `try` left the whole suite green while one 404 took the character
  generator to a blank page. One file short leaves *no* recordings rather than most of them,
  which is deliberate: a library holding three of four looks like a decision and answers the
  fourth address with "no such recording". `ReplayLoader` is what makes the fetch lazy —
  `WebPresentationTests.TheBrowserDoesNotFetchTheReplayLibraryAtStartup` holds `Program.cs` to
  never building one itself, and
  `ReplayRenderTests.NothingFetchesTheRecordingsUntilOneIsOpened` is the behavioural half, with
  a positive control: opening a recording really does ask.
- **Nothing on a replayed sheet may come from the visitor's own character**, and that is
  asserted by rendering the same character twice — once held by the session, once passed as a
  parameter over a *different* session character — and requiring the two pages to be identical.
  Naming the fields does not work: seven of them were free at once. Anything that legitimately
  comes from outside the character, which is `ShowBudget` and only `ShowBudget`, has to be
  passed explicitly in both renderings or it hides every illegitimate difference behind itself.
- **`ReplayLibrary.Find` is `OrdinalIgnoreCase` on purpose.** Blazor's route matching is
  case-insensitive, so `/Replay/The-Conductor` reaches the page and only the lookup can refuse
  it — which is a confident lie about a link that is fine. Same rule as `MainLayout`'s
  first-path-segment check, and both now have tests.
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
- **A served document goes stale silently, and three sentences in this one had.** It claimed a
  Villain has no Hero Point budget (retired when `UnlimitedBudget` became an independent toggle —
  and `CLAUDE.md` already said so), that there is no Villain flag to set (`IsVillain` is a real
  field), and said nothing about Resolve at all. Every conversation the server has starts from
  this file, so a wrong sentence here is wrong everywhere at once and nothing compiles it. **When
  a decision changes in `CLAUDE.md`, grep this document for it.** The skill is the same pair and
  drifts the same way — both now carry the optimise-first default and the Villain Resolve rule,
  and both have tests naming the retired claims so they cannot come back.
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
  The source half exists because **a stray line does not necessarily break a client** — the
  first runtime test drove the binary through the SDK's own client and asserted the session
  worked, and a real stray line left it perfectly happy, because the client skips what it cannot
  parse. Do not replace either with the other. **This is also why the
  setup guide (`docs/MCP-SETUP.md`) points a client at the published binary rather than at `dotnet run`**, which writes
  MSBuild's own progress to standard output.
- **The two halves are complementary only as far as the runtime half is driven, and this note
  used to claim more than that.** It said the runtime test existed to catch "a spelling split
  across two lines". It did not: it sent `initialize`, `notifications/initialized` and
  `tools/list` and stopped, so it never entered a tool body. `Console` and `.WriteLine(…)` on
  two lines inside `SearchPowers` contains the token `Console.` on neither line, and reached a
  real client's stdout as message two with **both guards green**. The same write in
  `ListOptions` was caught, and only because `ReadEverything` calls it at startup — that is how
  narrow the cover was. The runtime test now calls all six tools and requires each answer to
  carry something only the far end of that body produces, since a call answered "unknown tool"
  would otherwise satisfy it while running no code at all. **The fix for a hole in the source
  scan is another driven path, never another regex**: the spelling after a multi-line one is a
  helper in another file, or a library.
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
  substring: substring matching answered "she bakes bread in the city" with **Plasticity**, and
  a match like that is worse than none because nothing in it looks wrong. (This note and
  `Mentions`' own summary both said *Elasticity*, which is not a Power in this rulebook —
  a reproduction searches for an entry that is not there.)
- **`Mentions` had no test at all until slice A1, and one line put the substring search back.**
  Every search test was either a positive assertion or a negative on a query whose words happen
  not to be substrings of anything, so the property the method exists for was unpinned. What
  holds it now is four fragments that occur inside a Power's name and nowhere in the rules files
  as a word — `city`/Plasticity, `ration`/Regeneration, `art`/Martial Arts,
  `kinesis`/Telekinesis — each with the positive control beside it, so the guard cannot be
  satisfied by a search that has stopped working. **Any change to matching has to keep the
  baker's sentence at `found: 0`.**
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

### The accounts server

`worker/` is a Cloudflare Pages Functions server over D1, reached through the one routed file
`functions/api/[[path]].js`. It is JavaScript because Workers is, so it is invisible to
`dotnet test` and has its own suite: `./scripts/test-worker.sh` (local Node 22+, or Docker).
Setting it up is `docs/ACCOUNTS-SETUP.md`; the reasoning is in `PROGRESS.md`.

- **It is an allow-list, not a sign-up, and the refusal is silent.** Only an address on the
  invitation list may ask for a sign-in link; every other address gets the same `204` a sent link
  gets, because anything else makes the endpoint a way of asking who is on the list, one address
  at a time. **The bootstrap is `ADMIN_EMAIL`, an environment variable, and nothing is seeded into
  the database** — a committed address would be this repository owner's own, silently making him
  the administrator of every fork. A deployment with neither the variable nor a row allows nobody,
  which is the direction this should fail in. **What the list does not hide is time**: an invited
  address waits on a call to the mail provider and an uninvited one returns at once. Recorded
  rather than padded, because padding trades the real defence for the look of one.
- **Withdrawing an invitation ends that address's sessions and keeps its characters.** Deleting
  the row alone is a gesture — the person is holding a month-long cookie — and deleting their work
  would make one button on an administration page the most dangerous control in the application.
  Adding the address back gives them everything as they left it.
- **Adding an address mails it a one-click sign-in link, and the link carries a real token on
  purpose.** `worker/tokens.js` mints and hashes it exactly the way the public request path does
  — same table, same single-use guarantee — and only the lifetime differs:
  `INVITATION_TOKEN_LIFETIME_MS` in `worker/auth.js` is three days against the public path's
  fifteen minutes, a trade that is acceptable here and nowhere else because an administrator chose
  this address on purpose, rather than a stranger typing one in. **The row still grants the
  permission and the mail is only ever a shortcut to using it**: `worker/invitations.js` writes
  the invitation first and mails second, catches a failed send, and answers the admin page with
  `mailed: false` rather than a 500 that would read as nothing having happened — the address can
  still ask for an ordinary link. **The failure is still written to `error_log`, `mail` category**,
  because the fault that breaks this breaks every ordinary sign-in too and the owner should be
  able to find it from either. Do not let a probe or a second builder assemble this message's link
  itself; `signInLink` is the one place either sender's URL is built, same as the token mint.
- **The administrator's page is reached by its address, not by a link that appears for some
  people.** `Identity` still carries a key and a name and no role, deliberately, so the browser
  holds no claim about who somebody is; the server checks on every request and answers an
  ordinary account with the same `404` an unrouted address gets, so the page cannot be discovered
  by trying. The link on the account panel is therefore shown to everybody signed in, and an
  account it is not for is told so plainly.
- **`/admin` is its own `Area`, and the reason is the one recorded for the recordings.** Six
  numbered creation steps and a running Hero Point total above a list of email addresses are an
  offer to continue something the reader is not doing, and the budget is a different subject in
  the same six-label format. `Areas.Of` answers it; `MainLayout` draws neither there. **It now
  covers the portfolio and the sign-in page too** — there are four areas; see "Four areas, and the
  address decides which".
- **It holds no rules and must never gain one.** A character is stored as an opaque string it
  never parses — the engine decides cost and legality and runs in the browser. A second place
  that understood the shape of a character is a second place to keep in step.
- **No password anywhere.** A magic link; the token and the session are both stored as SHA-256
  and never in the clear, so a dump of the database lets nobody sign in as anybody. The session
  is an `HttpOnly` cookie, so the WebAssembly app never holds a credential.
- **Same origin is load-bearing.** Pages Functions rather than a Worker on `workers.dev`,
  because a cookie set by another host is a third-party cookie that browsers now partition
  away. Move the API to its own hostname and sign-in stops working and nothing else does.
- **Every refusal to sign in says the same thing**, and asking for a link always answers 204 —
  otherwise the endpoint is a way of asking whether an address has an account here.
- **`wrangler pages deploy <dir>` bundles a `functions` directory found in the working
  directory, not in the directory being uploaded.** There is no flag; the placement *is* the
  configuration, and getting it wrong deploys a healthy-looking site that signs nobody in.
- **Diagnosing the mail path is `node scripts/probe-mail.mjs`, never a deploy.** The server drops
  the provider's `message` field on purpose — it can quote the address, and it reaches a visitor's
  screen and a log line — so a refusal arrives as a status and a machine code and nothing else.
  That is right, and it means the owner cannot see the sentence naming the broken field. The probe
  reads it locally from `.dev.vars`. **It sends what the server sends**, importing `signInMessage`
  from `worker/mail.js` rather than assembling a lookalike, and two tests hold it there — a
  hand-written probe was tried first with a different key and a literal `YOUR_ADDRESS` in `to`,
  returned *the same provider code the site was returning* for an unrelated reason, and read as a
  confirmation. **A probe that builds its own payload can agree with the bug.**
  - **Read the status, not the code.** Resend answers a bad key with `name: validation_error` at
    `401` — the same name a malformed field gets at `400`. `docs/ACCOUNTS-SETUP.md` said the code
    told the four checks apart; it does not, and four deploy cycles were spent on the strength of
    that. `401` is the key, `403` usually the domain, `400` a field in the message.
  - **Never write to `.dev.vars`, and never delete it.** It is gitignored and holds a live
    credential, so there is no reflog, no stash and nothing to recover — and a provider will not
    show a key twice. Testing this probe destroyed the owner's, which is why `PP_DEV_VARS` exists:
    point it at a scratch file. The rule generalises past this one path — **`ls` a target before
    any `>`, `rm` or `mv`, and do not assume a file is yours because you wrote one like it.**
- **A failure is classified into four categories, and the set is closed.** `mail`, `storage`,
  `configuration`, `unknown`, in `worker/errors.js`. The visitor gets the category and a
  reference and nothing else; the owner gets a row in `error_log`, readable by hand with
  `wrangler d1 execute` (see `docs/ACCOUNTS-SETUP.md`) and, now, through a panel on `/admin`.
  **This reverses an earlier decision recorded here — "there is no admin endpoint and there must
  not be one" — and the reversal is deliberate, not drift.** The reasoning against it was sound
  at the time: `Identity` carried a key and a name and no role, so "am I an admin" was not a
  question the client could ask. What changed underneath it is the invitation list: the *server*
  now answers exactly that question on every request, via `invitations.isAdministrator(env,
  user)`, to gate `/api/admin/invitations` — and `/admin` already answers an ordinary account the
  same `404` an unrouted address gets, so the page cannot be discovered by trying. A read-only
  `/api/admin/error-log`, gated by that identical check, adds no role to `Identity` and no new
  concept; it is the same question asked once more. The unrelated precedent —
  `users.character_limit`, raised by hand in SQL — still stands: that is a *write* with no gate
  built for it, which reading a table never needed one for in the first place.
  - **A category is assigned where a failure is caught, never at a throw site.** `handle()`
    wraps the two subsystems on the way in — `taggedStorage` round the D1 binding,
    `taggedMail` round the send — so `db.js` and `mail.js` know nothing about any of it. A
    category per throw site becomes a description of the internals by enumeration, which is the
    disclosure this exists to avoid. The first tag wins: a storage failure raised *inside* the
    mail call stays `storage`, because the innermost boundary is the one that knows.
  - **`unknown` must stay reachable.** A taxonomy with no default grows a category for every new
    failure, and the pressure is then to classify by guessing.
  - **A category may never depend on whether an account exists**, and this is a security property
    rather than a style rule. Asking for a link always answers 204 precisely so the endpoint
    cannot be used to ask whether an address is registered; a category that appeared only for
    known addresses would put that oracle straight back through the error path. `errors.test.mjs`
    provokes the same subsystem failure for a registered and an unregistered address and requires
    **byte-identical** bodies — which is also why the reference is injected through `deps` like
    the clock, since a random one per failure makes every body differ for an unrelated reason.
  - **`configuration` must never advise retrying**, because retrying cannot set an environment
    variable. That is the category the one failure this site has actually had would have landed
    in. `AccountsContractTests` scans the sentence — and note that *"trying again will not help"*
    is deliberately allowed and deliberately pinned: it is the denial, not the advice. The scan
    carries a positive control on the `mail` sentence, which is known to advise retrying, or an
    absence-only assertion would pass against a regex that captured nothing.
  - **The row is bounded by construction, not by a cap somebody remembers to enforce.** The
    primary key is `(category, route)` and `route` is a *pattern* from a closed list, so
    `/api/characters/{id}` is one row however many ids a caller invents — otherwise the error log
    is a table anybody passing by can fill, with a caller-chosen string in it. Occurrences are
    counted against the one row rather than appended: **`occurrences` is the record of what was
    dropped**, because a silently truncated log reads as a quiet period.
  - **The retention window rolls inside the write statement**, the same shape as `countAttempt`,
    so a stale row starts a fresh count rather than continuing last month's into this morning's
    outage. A prune written as a separate pass is a prune that does not happen.
  - **The logger may never throw.** The thing that just broke is often the database it writes to,
    and a logger that threw out of the catch would cost the visitor the reference and category
    that are the entire visitor-facing half of the design.
  - **Redaction buys less than it looks like and is still worth having.** `users.email` is in
    that database in the clear already, so an error row is not a new exposure *boundary*; what it
    protects is that the log — the artefact most likely to be pasted into an issue — does not
    carry an address. It over-redacts on purpose: any run of twenty or more token-alphabet
    characters goes, with no test for randomness, because a session secret is 43 base64url
    characters and a hash is 64 hex ones and neither is guaranteed to contain a digit.
  - **The absence tests all carry a positive control, and it is not optional.** Every assertion
    about redaction is an absence, and an absence is satisfied completely by a logger that writes
    nothing — the failure shape this repository has shipped four times. Each asserts a row was
    written *and* that the message still says what happened, since a `redact` returning the empty
    string would satisfy every absence while destroying the column.
  - **`console.error` in the catch is one JSON object, not a formatted sentence**, so
    `wrangler pages deployment tail` can filter and read it. `worker/index.js` computes the
    category, route pattern, exception kind, redacted detail and reference once and shares the
    same object with the write to `error_log` — a second computation here could redact
    differently from the row the caller's own reference points at. The exception's raw message is
    never in it, for the same reason the visitor is not shown it either.

- **`/api/admin/error-log` reads the table over the wire, gated exactly as
  `/api/admin/invitations`** — 401 signed out, the same 404 an unrouted address gets if signed in
  but not an administrator, 200 with the rows otherwise. **Read-only, on purpose**: there is no
  route here that deletes or clears a row, because the table needs none — see the migration. A
  panel on `/admin` renders it, beside who can sign in, using the identical gate the invitation
  list already had; an empty table reads as reassurance ("nothing has failed"), not as a blank
  page, and a row whose most recent failure is well in the past says so rather than reading as an
  ongoing outage.

- **A missing server is a missing feature, not a blank page** — and the shape that makes that
  work is also the shape that hides the mistake. `_redirects` serves every unmatched path as
  `index.html` with a 200, so a site without its Functions answers `/api/me` with HTML; the
  client parses the body rather than trusting the status, and answers `Identity.Anonymous`. **So
  the deploy is the only place the fault is ever visible**, and it checks for JSON there.
- **`data/rulebook/` is bundled into the server and never staged into `wwwroot`.** A file under
  `wwwroot` is a public URL; that placement is the entire access control, and there is a test on
  both sides of the repository. **The four recorded conversations the portfolio replays are
  bundled the same way**, into `worker/transcripts-corpus.js` by `scripts/inline-transcripts.mjs`,
  and answered at `api/transcripts` behind the same "signed in, nothing more" check as the
  rulebook routes — see "The replay". `ReplayLoader` fetches it once, on demand, rather than at
  startup, which is also what stopped every visitor's browser paying for four files almost none
  of them could ever open.
- **The two halves are different languages and both suites stay green while they disagree.**
  `AccountsContractTests` is the only thing that reads both — addresses asked for against
  addresses routed, and the keys the server returns against the names the client binds. Do not
  write a guard there with `Contains`: the first version was one, and a rename walked through it
  because the same word occurred elsewhere in the server's own source.

- **Wrangler's bundled esbuild is older than Node's, and both suites plus a whole-tree Qodana
  scan will happily ship an incompatibility to the deploy.** This has happened once:
  `import ... with { type: 'json' }` in `worker/corpus.js` ran under Node 22 (both the accounts
  suite and my local `npx wrangler`) and failed on the deploy pipeline with
  *"Expected ';' but found 'with'"* — because `cloudflare/wrangler-action@v3` pins wrangler at
  **3.90.0**, whose bundled esbuild predates JSON import attributes. `assert { type: 'json' }`
  is the older spelling and is deprecated in Node 22; that trade breaks the tests instead of
  the deploy. **So the corpus is baked into `worker/corpus.js` as an object literal by
  `scripts/inline-rulebook.mjs`**, and both are guarded: `tests/worker/router.test.mjs` asserts
  the bake is byte-for-byte the JSON on disk, and the build workflow runs
  `wrangler pages functions build` at the same version the deploy uses (read out of
  `.github/workflows/deploy.yml`'s marker comment), so a wrangler-vs-Node parse difference
  fails the PR rather than the way to production. That marker comment is load-bearing — see it.


### An account has a name it can change

`display_name` is set once at first sign-in to the email's local part, and `PUT /api/me/display-name`
is how it stops being that. It needed no migration — the column has existed since `0001_accounts.sql`.

- **Scoped by construction, not by a check.** `db.setDisplayName` takes the id off the session the
  caller already authenticated with. There is no address and no id in the body, so the route has no
  way to name a row other than its own.
- **Uniqueness is deliberately never checked**, on either side. The name a fresh sign-in gets was
  never unique either, and "is this name taken" is the same oracle the invitation list exists to keep
  this site from answering, asked about names instead of addresses. A name is what the banner calls
  somebody; nothing reads it as proof of anything.
- **What is refused is a shape that cannot be rendered, not a judgement about what somebody calls
  themselves**: a control character (the banner is one line), and more than 60 characters.
- **Blank resets to the email's local part rather than being refused.** Storing an empty string
  would leave the banner naming nobody while `Identity.IsSignedIn` still read true, since that only
  checks the name is not null. A cleared name should look exactly like one nobody has set.
- `Identity` still carries a key and a name and **nothing else**. There is a test.

### The error log is visible to an administrator, and that reverses a recorded decision

`/api/admin/error-log` reads the `error_log` table; a panel on `/admin` renders it. Both are gated by
`invitations.isAdministrator` — 401 signed out, and the same **404** an unrouted address gets for an
ordinary account, so the endpoint cannot be found by trying. Read-only: there is no route that
clears or deletes a row.

**`0004_error_log.sql` said there would never be an admin endpoint, and its reasoning was sound at
the time**: `Identity` carried a key and a name and no role, so "am I an admin" was not a question
the client could ask, and inventing a role to answer it was a much larger change than the log needed.
The invitation work made it a question the *server* answers on every single request. So this adds no
role to `Identity`, no claim in the browser, and no new concept — it is the same gate the invitation
list already uses, asked once more. The migration comment records the reversal rather than being
left to contradict the code.

**The live database is `prowlers-and-paragons`.** That comment named `prowlers-accounts`, which does
not exist, so the worked example in it failed for anybody who followed it.

### Inviting somebody emails them, and the token is the exception rather than the rule

`invitations.add` writes the row and then sends a one-click link. **It sent nothing at all until
somebody noticed**: adding an address granted permission and told nobody, while being called an
invitation, so an invited person had no way of knowing they could sign in.

- **`worker/tokens.js` is the only place a token is minted, hashed, or turned into a URL.** Both the
  public request path and the invitation go through it, so there is one mint rather than two that
  could drift on the hashing or the link.
- **`INVITATION_TOKEN_LIFETIME_MS` is three days against the public path's fifteen minutes**, and
  that is the whole difference: same table, same `used_at` single-use guarantee, same verify path. A
  longer-lived credential in an inbox is acceptable **only** because an administrator chose that
  address deliberately, which is not true of the public endpoint — do not carry the three days
  across.
- **A dead mail provider must not lose the invitation.** The row is written first; the send is
  caught; the failure reaches `error_log` as a `mail` category, because the fault that breaks this
  breaks ordinary sign-in too; and the page says which of the three things happened.
- **Re-adding an address already on the list sends nothing** and answers `alreadyAllowed`. So an
  address invited before this existed is not retrospectively mailed — withdraw and re-add.

### The pixel diff, and why the goldens are the fragile part

`scripts/visual-regression.sh` screenshots seven proof pages and compares each against a committed
golden under `tests/visual-goldens/`, with a per-pixel tolerance. It runs in CI beside the verdict
harnesses. The PNG codec is ~150 lines against `node:zlib` rather than a dependency.

- **Goldens are Linux-rendered or they are worthless.** On Linux the script drives whatever Chrome
  is on PATH; everywhere else — including a Windows development machine — it drives a digest-pinned
  `selenium/standalone-chrome` in Docker. A golden generated from Windows Chrome fails every CI run
  for ever, which is a check that has to be deleted rather than fixed.
- **Regenerate with `--update-goldens`, and only ever deliberately.** A golden updated as a side
  effect of an unrelated change is a regression signed off by nobody.
- **`--virtual-time-budget` is not optional.** `.panel` carries `animation: rise var(--enter) both`,
  which starts at `opacity: 0`; a bare screenshot proofs a washed-out lie.
- **A proof page whose content depends on which test ran last cannot be pixel-checked.** Three pages
  were written by a `[Theory]` over both palettes into *fixed* filenames, so the palette was a coin
  toss between runs and the comparison failed against goldens generated from its own tree. The mode
  belongs in the filename, as the shell proofs have always had it. Nothing before this compared
  those pages byte for byte, which is why it survived.

### Hosting

Cloudflare Pages at `superheroes.softwaresamurai.net`, by `.github/workflows/deploy.yml` on push to `master`. Direct upload, not Cloudflare's Git integration — two deploy paths can disagree.

- **The deploy workflow must never trigger on `pull_request`.** That trigger runs a contributor's workflow file with the base repository's secrets in scope, which puts the Cloudflare token one PR away from anyone. Adding it would be the single most damaging change available in this repository.
- **`_headers` is generated, never hand-edited.** `scripts/write-cloudflare-headers.sh` hashes the inline import map Blazor writes into `index.html`, whose contents change whenever the framework assets are re-fingerprinted — a hard-coded hash would rot silently and stop the app booting on some later deploy. The script exits non-zero if it finds no inline script rather than shipping a policy that would break the site, and CI runs it too, so a broken policy fails on the PR.
- `style-src` needs `'unsafe-inline'` because the budget bar's width is a live inline style attribute. `script-src` does **not**, and should not gain it.
- `web/wwwroot/_redirects` sends every path to `index.html` with a **200**, not a redirect: a 302 would drop the path and land every shared link on step one.
- `<base href="/">` assumes a root path. A subdomain is fine; a subpath is not, and getting it wrong breaks every asset fetch at once.

### The two sample characters

`SampleCharacters.Hero()` and `.Villain()` return finished Standard-tier sheets, offered on **`/portfolio`** so a sheet can be previewed without building one. They fill every section a printed sheet has, which an empty sheet does not.

**This entry said "on the tier page" for a while after they stopped being there, and then said "on the portfolio" after that moved too.** They are at **`/admin/portfolio`** now, behind the account pages. The reason for the first move is worth keeping and is the same one: a demonstration is not a step in making your own character, and somebody who came to build one had to walk past them. `AreaTests.TheSamplesAreBehindTheAccountAndNotOnTheTierPage` pins both halves.

- **They are this project's own characters.** The published Ch.8 Heroes stay in the test suite, where they verify the engine against printed numbers. **The reason has changed and the practice has not:** shipping them used to be barred as redistributing the authors' content, and the owner has since lifted that — the book's text and the published characters may be served to an account. These two are still the ones the app offers, because they were written for this tool and fill every section a sheet has, which is what a preview is for.
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

**And the trap was inside the helper the render tests use to catch it.** `SheetRenderTests.Rendered` replaced every tag with a newline and one test then collapsed all whitespace, so `<b>Armor</b><span>8d</span>` read as "Armor 8d" — the exact string the assertions look for, produced by the exact bug they exist to find. It concatenates text nodes now. A test-side helper is as capable of being the bug as the component is; read the helper before trusting the assertion.

**A typographic rule lives in the stylesheet, where no rendering test can see it.** Emptying `.hp` puts Hero Point costs back in the same size, weight and ink as ranks and every bUnit test still passes, because the class is still on the element. Anything whose whole substance is CSS — the `.hp` treatment, print font sizes, the break rules — is asserted in `WebPresentationTests` against the parsed rule, not inferred from markup.

bUnit pulls AngleSharp transitively at a version carrying a published advisory, so `web/`'s test project pins AngleSharp forward. Do not suppress NU1902 instead — see the comment in its csproj.

### The browser front end's four presentation rules

All four are asserted by `WebPresentationTests`, which reads the source because none of them is visible to a compiler.

1. **No component names a colour.** Checked by hex, by keyword, *and* by `rgb()`/`hsl()`/`oklch()` function syntax — that last one is the loophole a hex grep leaves open. `transparent` is allowed; it is the absence of a colour. Radii and durations are tokens for the same reason, and `prefers-reduced-motion` turns every animation off by setting three duration tokens to `0.01ms` — not `0`, which makes some engines skip `transitionend` entirely.

   **Nor a typeface.** `--font-display` (Oswald) and `--font-body` (Public Sans) are declared in `theme.css` and nothing else names a family; `font:` shorthand is checked as well as `font-family`, because the shorthand carries a family too and `font: inherit` is everywhere. **Both faces are self-hosted under `web/wwwroot/fonts/` and both are SIL OFL, so the licence text ships beside them** — this repository redistributes them on every deploy and every fork, which is a condition rather than a courtesy, and there is a test. **A missing font file fails silently**: the stacks name system fallbacks on purpose, so a renamed file degrades the whole app to them with every other test green — which is why one test reads the bytes on disk. They are `.ttf` and would be ~40% smaller as `.woff2`; converting them is a one-line change per face.
2. **Nothing on screen names an internal type or a build command.** Asserted on the *prose*, which `VisibleText` derives by stripping `@* *@` comments, the `@code` block, every tag (and so every attribute) and every Razor expression — so `@PowerFormatter.StatLine(p)` is fine and the same characters in a paragraph are not. The rule is general: no compound PascalCase type declared in `engine/` or `sheets/` may appear. The reverse is asserted too — `Ch.6`, `Ch.9`, `Trait Cap` and `Hero Point` must still appear *in the prose*, since deleting the rulebook references would satisfy a naive reading of this rule and ruin the app. (Asserted against the raw file, that test passed while `Ch.6` survived only in a comment.)

   **And the rule is one step wider than "no jargon": copy answers what the reader came to do, and anything explaining *why the app is built this way* belongs in a `@* *@` comment.** Four places broke that and the owner found all four by reading the app — the sign-in page explaining that it will not say whether an address has an account (noise to somebody signing in, and an advertisement of the defence), the replay page accounting for who would pay for the model in a sentence that had also stopped being true, a sample character vouched for by "there is a test that says so", and "nothing was pre-computed". `NoPageExplainsItselfToADeveloper` is a denylist and cannot be anything else — no pattern separates a sentence about a character from a sentence about the program — so it grows when somebody reads the app. `NoPagePointsAtAFileInThisRepository` is the structural half: any `.md`/`.json`/`.cs`/`.razor`/`.css` path in visible prose fails, whatever it is called. That one would have caught the worst instance on its own, which sent a reader wanting the live version to `docs/MCP-SETUP.md`.

   **The validator's messages are the other half of this surface**, and `web/`'s tests cannot see them — they are engine strings, printed verbatim on the GM review step and in both exports. `ValidationMessageTests` provokes them from real sheets and holds them to the same rule: no file name, no internal flag, no bare id where the rulebook has a name, no `flaw(s)`, and every message a sentence.
3. **One component owns each repeated class.** `Panel`, `Field`, `SheetSection`, `StatBlock`, `DerivedStatBlocks`, `OptionList`/`OptionRow`, `ChosenList`/`ChosenRow`, `Tooltip`. Writing `class="panel"` by hand anywhere else fails a test.

   **A `title` attribute is not a tooltip, and no component may use one** — there is a test. It never appears on a touch screen, is unreliable for keyboard users, cannot be styled, cannot be dismissed, and is announced inconsistently by screen readers. It is the easiest way to undo `Tooltip` because it is the obvious thing to write. The component's own traps: the trigger is a **real button** (a `<span>` with a mouse handler is a tooltip only for people with a mouse); the hover handlers are on the **wrapper**, because `mouseenter` does not bubble and a tip that closes as you reach for it fails WCAG 1.4.13; the tip is **always in the document**, hidden by `visibility`/`opacity` and never `display: none`, which would take the `aria-describedby` description with it while every rendering test stayed green; and the id is **derived from the term**, since a generated one differs per render and breaks the replay guard that requires two renders of one character to be identical. It opens **downward** — an upward tip is clipped by the window edge inside the budget breakdown, which hangs off a strip stuck to `top: 0`. The budget bar's live fill width is the **only** inline style left, and it is the sole justification for `style-src 'unsafe-inline'` in the CSP.
4. **No screen rule names a raw length**, in px any more than in rem. Padding, margin, gap and font-size come from `--space-0`…`-8` and `--text-xs`…`-3xl` in `theme.css`; the print block is out of scope because mm and pt are a different medium with its own scale. Before the scales existed the screen half of `app.css` spent **twenty-seven** distinct spacing lengths and **twenty** font sizes, ten of the latter between 0.68rem and 0.9rem — an accumulation nothing could flag, because every value in it was locally reasonable.

   Three literals are exempt, each **paired with the selector it belongs to and asserted to still exist**: an exemption whose selector has been renamed away permits its declaration everywhere and reports nothing.

   **Two rungs are pinned to measured values, not to a ratio, and must not be tidied onto one.** `--text-xs` is 0.72rem because that is the size `--muted`'s 4.5:1 floor was measured at — round it down to fit a ratio and the colour still passes its own test at a size nobody checked. `--text-3xl` is 2.15rem because it is the masthead. The spacing scale is 2px at the bottom and 4px above it for the same kind of reason: a strict 4px base doubles the tightest spacing in the app.

   **`--shadow-3` belongs to the sticky budget strip and nothing else, asserted by count.** One `--shadow` used to carry the banner, every panel, the sheet, the cards and the strip — which did not look wrong, it just meant nothing on the page had a height. Spreading the top step back would undo that without changing a value.

   **A `-var(…)` is not a negative length.** A minus sign in front of a `var()` invalidates the whole declaration and the browser drops it, so the budget strip's bleed disappears and nothing looks broken. Write `calc(-1 * var(--space-6))`. The bleed matching the shell's padding used to be a comment asking to be remembered; two references to one token made it a test.

   **`app.css` may not declare a custom property at all.** Narrowing that rule to `--space-*` and `--text-*` defended the *names* of the scales rather than the property that makes a scale mean anything, which is that lengths are decided in one file — and two mutations walked straight through it: `--table-inset: 1.2rem` beside `width: calc(100% - var(--table-inset))`, and `--pad-lg: 4rem` behind an ordinary `padding`. A custom-property declaration is not one of the four scanned properties, and every `var()` is stripped before the scan looks for a literal.

**A CSS guard is worth one spelling of the property it reads, and CSS has several.** One helper reading `border-bottom` was beaten by `border-bottom-color: transparent`; one reading `border-left` by `border-left-width: 0`; one reading `margin` by `margin-left: 0`; and one reading `padding-left`/`padding-right` by `padding-inline`. Four separate guards, one cause. `EffectiveValue` now gathers the property, its longhands **and its logical equivalents** in source order and **refuses to answer when the last of them is a spelling it does not model** — a red test is the safe direction. Order is what makes that correct rather than merely strict: `.budget-toggle` writes `border: none` then `border-bottom: …`, which the cascade resolves as intended, so a check refusing any related spelling fails on correct CSS.

Two more of the same family. **A zero width is not a visible edge** — `border-left: 0 solid var(--rule)` names the right token, contains no `none`, and draws nothing. And **CSS formatting is not a property a guard may depend on**: a media-query scan that ended at the first newline-brace could not see a query written on one line, and swallowed its contents into the next block that *was* formatted. Brace-match.

**A later declaration of the same thing beats a `Contains`, and this one root cause has defeated five guards in `WebPresentationTests`.** `Contains("position:sticky")` is satisfied by a declaration overridden on the next line; a pinned `--space-4: 0.75rem` is satisfied while a duplicate lower down wins the cascade; `border-bottom:` is satisfied by `border-bottom: none`; a filter on `Selector == ".budget"` misses `.budget, .breakdown { … }`; and reading the *first* `@page` misses a second one that prints the sheet A5 landscape. The instruments are `EffectiveValue` — comma lists split, suffix-matched, last declaration wins — and `RulesTargeting` beside it. **Do not write a new guard in that file with `Contains`**, and where a guard asks "does this rule still say this" rather than "what applies here", match the selector *exactly*: suffix matching let a rule matching no element in the app supply an exemption's whole justification.

**An allow-list of units is the wrong shape for a ban.** That was got wrong twice, the second time in a fix whose own comment said so: `px|rem|em|ch|vh|vw|%` let `9pt` through, and the thirty-unit replacement let `9dvmin`, `3svb`, `2lvi` and `4PX` through. Invert it — a digit followed immediately by letters or a percent is a length, whatever the letters are. No whitespace between the two, or `margin: 0 auto` reads as a length.

**`OptionList` owns the filter box, and `OptionRow` decides whether to draw itself.** The five pickable lists — Powers, Pros and Cons, Perks, Flaws, gear features — get a filter by being lists of options rather than by five tabs each growing a search box; the Powers tab had the only one and now has none of its own, keeping its category facet. A row is passed `Keywords` for words it can be *found* by but does not print, because the Powers box read tags and moving it would otherwise have narrowed the one list that worked.

- **The filter is cascaded as a record that is replaced every render, never mutated.** Blazor only re-renders a child when something it can compare has changed, so a cascading value that is the same object with different contents leaves every row on its last answer and the list stops responding to the box above it. `Pass` on that record is what makes each render's value distinct.
- **A row counts itself once per pass, however many times Blazor asks.** A row inside a `CascadingValue` is reached from both directions when the value changes — the parent re-renders the fragment holding it *and* the cascading value notifies its subscribers — so `OnParametersSet` runs twice and the naive count reported **282 of 282 Powers where the rulebook has 141**. It read as a plausible number beside a list nobody counts. Do not move the decision back into a property the markup calls; asking the filter is what counts the row.
- **The count lags its own render by one pass and `OnAfterRender` catches it up**, guarded by comparing against what was drawn. Remove the guard and it is an endless render loop rather than a count.
- **The "nothing matches" line appears only when a filter is the reason.** A list that is empty for its own reasons says so in its own words — "None yet." — and answering an unasked question would contradict it.

**The budget is chrome, not content.** `HpBudgetBar` is a sticky strip with a 3px rail on its own bottom edge, not a panel in the column — as a panel it cost ~110px above every one of six steps, most of it a table consulted occasionally. Three things it has already been got wrong on:

- **The negative-margin bleed must follow `.shell`'s padding.** The strip is pulled out by `-1.25rem` to run edge to edge; the ≤620px query cuts that padding to `0.75rem`, and a fixed pull is then 8px wider than its container on both sides — measured as real horizontal overflow at 375px. Change one and you must change the other.
- **`aria-valuenow` is clamped to `aria-valuemax` and `aria-valuetext` carries the truth.** An over-budget character spends more than the budget, and a `progressbar` reporting 132 of 125 is out of range; the fill was already clamped in the same block while the announced value was not. The bar also carries its own `aria-label` — the one on the enclosing `<section>` names the section, not the bar.
- **`aria-controls` only while the target exists.** The breakdown renders inside an `@if`, so naming it unconditionally leaves a dangling IDREF. `aria-expanded` is what carries the state.
- Whether the disclosure is open is a field on the component, **never on `CharacterSheet`** — that is a fact about a screen, and the sheet is what gets exported and restored.

**`--accent-soft` and `--danger-soft` are grounds for tints, not for text.** Villain `--heading` on `--accent-soft` measures **4.08:1** and `--danger` on `--danger-soft` **3.94:1**, both under the 4.5:1 text needs — and WCAG 1.4.3 applies to a **hover state**, which is where all three instances were. Hover grounds are `--panel-sunk`. This was found twice: the first fix moved the tier card and left `.btn:hover` and `.btn.danger:hover` on the same pairs, so the file carried a comment naming the fault eleven lines above two live instances of it. **Re-measure; the screen palette has no luminance test, unlike print.**

Two Razor traps this surface has already hit:

- **Razor strips the leading whitespace inside a `<text>` block, and inside an element that follows an expression.** `<text> @(rank)d</text>` after a name printed `Armor8d` on the sheet, and `<span class="muted"> @(rank)d</span>` did the same on the Powers tab. Put the separator inside one expression: `@(rank > 0 ? $" {rank}d" : "")`.
- **Blazor will not mix implicit child content with a named fragment.** Once any child is written as a named element the rest must be too — so `<Panel>` with a `<Head>` also needs an explicit `<ChildContent>`, and `ChosenRow` names both its slots `Body` and `Actions`. Implicit content on its own is fine, which is why most `<Panel>` call sites do not write `<ChildContent>`.
- **A `true` bool bound to an `aria-*` attribute renders as `aria-pressed=""`.** Blazor drops the attribute when the value is false and emits an empty string when it is true — and empty is invalid ARIA that assistive technology reads as *not* pressed, so the obvious spelling announces the opposite of the state in both directions. Bind a `"true"`/`"false"` string.

### Hero and Villain are one app with four palettes, on two independent axes

Ch.9 builds Villains exactly like Heroes and prints no separate stat-block format, so the mode is presentation and nothing else.

**Identity and darkness are separate questions.** Hero-or-Villain is a fact about the character; light-or-dark is a fact about a person and a browser. They used to be one switch — Hero was a light theme and Villain a dark one — so somebody who wanted a dark screen had to make their Hero a Villain to get it. There are now four sets: hero-light, hero-dark, villain-light, villain-dark. Hero-light and villain-dark are the two that always existed and their values are unchanged.

- Six screen blocks in three shapes per identity, in `web/wwwroot/css/theme.css`: a bare one (light), an OS-dark one guarded by `:not([data-theme="light"])` so an explicit light choice beats the system, and a `:root[data-theme="dark"][data-mode="x"]` one so an explicit dark choice beats a light system. **Three theme states, not two** — the default stamps no attribute at all, because a `data-theme="system"` would match neither path.
- **The dark half is scoped to `@media screen`, and that is load-bearing.** `@media` contributes nothing to specificity, so a dark block at (0,3,0) beats the print block at (0,2,0) — on paper, in dark mode, you would print the full-bleed near-black page the print block exists to prevent. Measured in a browser, not reasoned about. `screen` means the dark palettes do not apply on paper at all, which is truer and cheaper than padding the print selectors with repeated `:root`s.
- **The guard for that had to become a cascade resolver, and the first attempt at it was wrong in a way only mutation showed.** `PrintKeepsThePaperWhiteAndTheInkReadable` used to read the print block's own declarations, which cannot see a screen block outranking it. Its replacement resolves the whole stylesheet for a given state — but the first version applied rules in **source order** and passed with `screen` deleted from the OS-dark query, because source order is not the cascade. It weighs specificity now. `EveryThemeStateResolvesToTheIntendedPalette` pins all twelve routes in, and `TheTwoRoutesIntoDarkAgree` holds the deliberately duplicated dark blocks together.
- **The theme preference is per-browser and is not on the character**, and not on the account either: `pp.theme.v1` in local storage, read and stamped by `js/theme.js`. A theme on `CharacterSheet` would travel through an export and change the screen of whoever imported somebody else's character; a theme on the account would let somebody signed in on a shared machine impose it on the next reader. `localStorage` over a cookie because a cookie rides on every asset request to a server with no use for it.
- **`js/theme.js` is loaded from `<head>` and is the only render-blocking script in the app.** The payload is ~27 MiB, so there are seconds of boot screen: a theme applied from C# lands after the reader has already seen the wrong one, and so does one applied from the foot of `<body>`. Both leave every test in both suites green. `TheThemeIsStampedBeforeTheFirstPaint` reads the tag's **offset** against `</head>` — its first version searched the head slice for the file name and passed with the script moved, because a comment near the top of `index.html` mentions it.
- **Persistence has no C# guard and cannot have one.** Deleting the `localStorage.setItem` — so a choice applies for the visit and is forgotten on reload — left all 4,115 tests green: the C# side checks that the right word goes out and that a stored value is read back, and both are true of a script that stores nothing. `proof-theme.html` drives the shipped file in a browser and re-executes the module, which is what a reload does. It is in the build workflow beside the other harnesses.
- Both palettes are CSS custom properties on `:root[data-mode="hero"]` and `[data-mode="villain"]` in `web/wwwroot/css/theme.css`. **No component ever names a colour** — that is what keeps the switch a one-attribute change, and there is a grep in the PR notes proving it holds.
- **`--[a-z-]+` does not match `--shadow-1`.** The contrast instrument's token regex was written that way and silently dropped every shadow, space and type token from every palette it resolved. It is `--[a-z0-9-]+` now. A palette resolver that skips tokens reports a palette nobody is looking at.
- **Headless Chrome here reports `prefers-color-scheme: dark`**, so an un-stamped proof page renders the *dark* palette. Correct behaviour; it means judging a light palette from a screenshot needs an explicit `data-theme="light"` on the harness.
- `--primary` is a **fill** and `--heading` is **text**. They coincide in the Hero theme and must still be kept apart: Villain `--primary` measures 2.0:1 on its surface and is unreadable as type. Hero `--accent` is 1.8:1 for the same reason. Re-measure if you restyle; do not eyeball it.
- **The mode is `CharacterSheet.IsVillain`, and no rules code may read it.** This entry used to say the opposite — do not add the flag — and the reason it changed is the whole point. The refusal was correct while "Villain" meant a palette *and* no Hero Point budget: the second half is mechanical, and a mechanical flag on the sheet is the browser deciding a rule. The budget half is now `UnlimitedBudget`, an independent toggle, so what is left really is only a colour, and it belongs on the character because an exported sheet should still be a Villain when it is read back. **`PresentationFlagsTests` asserts nothing under `engine/` or `sheets/` so much as names either field, with a positive control** — a scan for two names is satisfied completely by two names that no longer exist. Put a mechanic back on `IsVillain` and the old objection applies again in full.
- **`UnlimitedBudget` is not a Villain thing.** Ch.9 builds Villains by exactly the Hero rules, so "no budget" was never a fact about Villains — it is a GM building to whatever the scene needs, which a Hero campaign does too. A Villain can be held to a tier's points and a Hero need not be; the toggle is on the tier page, where the budget is introduced. The validator is still never told, and still reports `HP_BUDGET_EXCEEDED` — the browser shows a running total and `build --from` reports every finding, because a report that dropped one on the strength of a flag in its own input would be worth less than no report.
- **Without a limit the strip is a running total, not an absence.** Absent was the old Villain behaviour and it took the breakdown with it, so somebody building without a limit lost the one panel saying where the points went. No cap, no remaining figure, and **no rail** — a `progressbar` needs a maximum to be a proportion of, and one drawn against the tier's points would put back the limit that was just switched off.
- **One route puts the palette on the document.** `MainLayout` applies it from the character on the render after any change of character — restored, sampled, taken from a recording, switched by hand. Three call sites used to push `ppSetMode` themselves. That made it render-reached, so it left the interop guard's by-hand allow-list and goes through `Theme`, guarded like `Motion` and `Shortcuts`; unguarded it would throw out of every render of the shell. The **theme** switch is different and deliberately so: it pushes from the click, because it follows the reader rather than the character and changes on nothing else.
- **`.mode-switch` names the Hero/Villain control, not the pill shape.** The light/dark control briefly carried the same class, which made `.mode-switch button` match five buttons and the identity switch report three pressed states at once. The shape is shared by selector list; `BannerTests` caught it in under a minute.
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

**The engine computes Resolve for every character, and only Heroes have any.** Ch.2 says it twice
— *"Only Heroes have Resolve"* — and Ch.5 gives the GM **Adversity** instead, spendable "on behalf
of any NPC whether they're Villains, Foes, Minions, or Extras". Ch.9's *"the only difference
between Foes and Villains is that Foes have less Health"* is not a contradiction; it compares two
kinds of antagonist, and Resolve is not in scope of that comparison.

This is deliberately **not** modelled. The engine is never told which it is looking at — that is
the same rule `IsVillain` lives under — so it answers the Hero question and the figure is noise on
a Villain. What must not drift is the guidance that reads it: `mcp/QUESTION-POLICY.md` and the
skill both say not to quote the figure, and name the three purchases that turn on it. **Never buy
Determination on a Villain** (Hero Points for Resolve); **Plot Hook and Condition Flaws grant
nothing mechanical** to one; and **the Trait Cap trade does not apply** — Resolve is what a *Hero*
pays for a rank at the cap, so a Villain caps for free. All three were got wrong in the session
that found this, in advice already given to the owner.

**And it is why a Villain's Flaws are the players' handles.** A Hero's Flaw is a drawback bought
with the Resolve it pays out; with the payout gone the drawback is all there is, so the one to
three slots are where the GM decides how the character can be beaten. Prefer a Flaw that bites
without a Resolve payout to notice it — Vulnerability halves active *and* passive defence as a
printed rule, which is the strongest in the book.

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
- **It is not in the browser payload, and that is deliberate rather than an oversight.** `web/`'s csproj copies `data/rules` into `wwwroot` and nothing else, so the deployed public site does not serve the book. **The reader exists now** — `/rules`, searching all ten chapters — and it reaches the text through `/api/rulebook/`, which asks who is calling. **The placement is still the whole access control**: a file under `wwwroot` is a public URL and no amount of checking sessions in the browser would make it not be one. There is a test on both sides of the repository. Turning it on is one `ItemGroup` — do not turn it on by accident. The recorded conversations are bundled the same way, into `worker/transcripts-corpus.js`, and answered at `api/transcripts` — see "The replay".
- **The corpus is generated, and the generator is `tools/RulebookExtractor/`** — in the solution so it cannot rot. Regenerate with `dotnet run --project tools/RulebookExtractor -- <pdf> data/rulebook`. **Do not hand-edit `data/rulebook/`**; an edit there is lost on the next run and hides whatever the extractor is doing wrong. The first extractor was a scratch project that no longer existed by the time its output was found to be wrong, which meant the corpus could be neither audited nor regenerated.
- **The book is two-column, and both naive readings destroy it in opposite directions.** Reading by baseline alone interleaves the columns — printed p.52's heading came out as `OVERKILL PHASE SHIFT`, which is two entries. Splitting every page at a fixed midpoint instead destroys anything set **full width**, cutting each line in half and filing the halves in different blocks; **every chapter opening in the book is set full width**, and all of them shipped scrambled. So the gutter is found per page, and a line counts as full-width only when **a word actually sits astride it** — the test that distinguishes a real full-width line from two facing headings sharing a baseline.
- **The damage from all of this reads as English.** Ch.2 opened "…from the Heroes the GM. They include not only sentient beings but also animals, and so on", with two runs of the printed sentence missing and nothing about it looking broken. Judge a change here by re-running the extractor and the corpus tests, never by reading a paragraph and finding it plausible.
- **The PDF watermarks every page with the purchaser's name and order number, as four separate words.** A filter written against the whole phrase matches none of them, which put somebody's personal data into the prose of every chapter. The extractor drops it **by font** — 6pt Helvetica occurs 780 times, four per page across 195 pages, and nowhere else in the book — and the test asserts each of the four tokens separately, because a version checking the surname, the order number and the literal `(Order #` was defeated by injecting `Dorian Order #`.
- **The display faces are faked bold by drawing the text twice a fraction of a point apart**, so the passes interleave: page 29 reads `2299`, the wordmark `PPRROOWWLLEERRSS`. Every element that does it lives in the running foot, so the one rule that drops the foot removes them all. A glyph-level de-duplicator was written for this and **changed not one byte of the output**, so it is not in the tool; what guards the outcome is a test over the corpus, which still bites if the furniture rule moves.
- **A heading with no body of its own qualifies the headings beneath it**, by point size. Discarding those instead lost **every name in Ch.8** — all twenty Heroes and all twenty Villains — leaving pages of anonymous `ABILITIES` and `POWERS`, and it ate the printed title of every table. Chaining the qualifiers instead of replacing them at the same level produced one 1,795-character heading listing the whole chapter.
- **Words are split on the page's own space glyphs**, which the PDF really carries. Guessing from letter gaps merged "FORCE FIELD" into "FORCEFIELD", because in the condensed display face a word space is barely wider than the gap between two letters. A gap break is kept beside it, since two facing headings have no space glyph between them at all.
- **Rotated text is furniture, never prose**: the chapter title runs up the outer margin a letter at a time and the word "chapter" beside it, which extracts reversed and landed in the corpus as the heading `retpahc`. Filtered on text orientation.
- Headings are detected by **typeface** — the display faces are a closed list — so a mis-detected heading is a *structural* error; the prose beneath one is verbatim.
- **Ch.8's stat-block headings are set in small capitals and come out as `aBIlItIes` and `FlaWs`. That is known, cosmetic, and deliberately not "fixed".** Two rules were tried; the better of them uppercased "Points" to "POINTS" while leaving the real cases alone, because a lowercase `t` is genuinely shorter than cap height. Do not tune a third heuristic until it happens to look right on the examples in front of you. **A table of three or more columns is read across rather than down** — the Powers list on printed p.20 and the location lists in Ch.9 — which is the other known limit; the two-column model is what the body text needs and a third column is rare enough not to have earned the complexity.
- **Judge a change here by the tests and by the PDF, in that order — and the tests can now see the failures that matter.** They pin the total volume of prose, that every one of the 116 Ch.2 Power entries opens with the stat line `data/rules` records for it, and that every published character is named in Ch.8. Before that they were shape checks: an adversarial pass rotated all 1,492 section bodies onto the wrong headings, and separately deleted 90% of the book, and the suite stayed green through both.
- **`ColumnLayout` is unit-tested against made-up pages**, because the committed corpus cannot show you a layout the book happens not to contain, and every failure this extractor has had was layout-shaped. **Two detectors, and both are needed**: an ordinary two-column page has almost no line containing the gutter (each line sits in one column), so it is found by which column of the page few words cross; but printed p.81 sets two sidebars above full-width body text, where no column is empty top to bottom and the gutter shows up only as a gap repeated at the same x. Removing either one turns real pages back into interleaved nonsense, and there is a test for each.

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
- Hero and Villain are **one app with four palettes**, since light/dark became an axis of its own — Hero/Villain is an identity and light/dark is a reader's preference, and neither is derivable from the other. The mode *is* a field on `CharacterSheet` — `IsVillain` — and no rules code may read it, which a test enforces. That reverses an earlier entry, and only because the budget moved off the switch: "a Villain has no Hero Point budget" was never a rule about Villains, and is now `UnlimitedBudget`, an independent toggle either kind of character can carry
- `engine/`, `sheets/`, `cli/` and `web/` are **separate projects**, so the dependency arrows hold at compile time rather than by convention
- Assisted creation *in this repository, for somebody with it checked out*, is a **non-interactive command plus a skill** — and the model proposes while the engine decides, never the other way round. **For somebody else, connecting their own Claude, it is an MCP server**, which is the mechanism built for exactly that and lets us handle no credentials at all. The two are not in tension and both call the same engine; the earlier flat "not an MCP server" note was scoped to the first case and is superseded
- **A visitor to the site cannot bring their own Claude, and that is settled — do not re-investigate it.** A claude.ai subscription cannot be lent to a third-party site, the API is separate billing with no dependable free tier, and custom connectors are gated to paid plans. A proxy funded by the owner was rejected — it costs money, invites abuse, and breaks the static-site property the README advertises. **What has changed is who the answer is for:** the recordings are at `/admin/portfolio/replay`, behind an account, because the owner decided the demonstrations are a thing to show somebody rather than a thing to publish. The technical finding above is unaffected; only the audience is
- An illegal character is **reported, never repaired**: the engine is a judge and does not make design decisions about somebody's character
- An assisted build **starts at full strength and trades down out loud**, rather than being built tastefully and quietly leaving points unspent. Trading down is a decision the person makes; trading up is a correction they have to notice they need. It does not license overruling a weakness they stated, dropping what they asked for, or exceeding the budget
- **Only Heroes have Resolve**; the GM gets Adversity, spendable on any NPC. The engine computes the figure anyway and it is noise on a Villain — never quote it, never buy Determination on one, and cap a Villain's Traits freely, because the Resolve a Hero pays for a rank at the cap is not a currency a Villain holds
- **A Villain's one to three Flaws are the players' handles** — the payout half of the bargain is gone, so the slots are where the GM says how the character can be beaten. A slot spent on colour, or on something the fiction carries free, is a handle the party does not get

Each of these was wrong at some point and is now covered by a regression test naming the rule. If one appears to be violated, read `PROGRESS.md` and the test before changing the code.

### The 1d minimum on every Trait

Ch.2 states it twice — once for Abilities (p.17), once for Talents (p.18): **no rank can be lower than 1d**, and "ordinary people have 2d in every" one. So **a character has all six Abilities and all twelve Talents**, and 0d is not a low rank but a Trait nobody can be without. `TRAIT_BELOW_MINIMUM` enforces it.

- **It costs Hero Points.** Without a package you pay for all eighteen at 1d — 18 HP before anything interesting. The Civilian Package's 35 HP for 2d in all eighteen is 36 points of ranks, which is exactly the "small discount" the rulebook calls a package. That is the corroboration, and it is why the reading is not negotiable.
- **All twenty published Heroes take a package**, so every one of their Traits sits at or above its floor. That is why rebuilding them never caught this, and why a rule can be missing for a long time without the strongest test in the suite noticing.
- **`TRAIT_BELOW_PACKAGE` is the other floor**: a package's granted ranks "cannot be lowered below the package rank". It costs nothing to break — `AbilityCost` and `TalentCost` charge only for ranks above what the package covers — so the mistake was free and therefore silent. It is the rule that proved Herald (Airmid)'s recorded package impossible.
- **A printed sheet still shows 0d for a Trait not filled in yet**, and should: the sheet is a form, and 0d is a fact about the page in front of you. The validator is what says the character is not finished.
