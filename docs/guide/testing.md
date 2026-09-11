# Tests, static analysis and the visual check

Read before adding a test, changing a guard, running Qodana, or touching the pixel diff and its goldens.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## Tests

`tests/ProwlersAndParagonsAutomation.Tests` (xunit.v3). Two things to know before touching it:

- The suite loads the **real** `data/rules/*.json` via `RulesFixture`, not hand-built fixtures. That is deliberate: its main job is to catch a rules file drifting away from the rulebook.
- `CanonicalPowers.cs` is the transcribed Range/Rank/Cost of all 141 Powers, `CanonicalPowerProsCons.cs` the 106 Power-specific Pros and Cons, `GearTests` the twelve Ch.6 gear feature prices, and `RulesDataTests` the tier/ability/talent/pro/con/perk/flaw values. Four more were added when an audit found them unpinned: `CanonicalFlawTypes.cs` (all 53), `CanonicalPowerBaselines.cs` (all 27 baseline prerequisites), `CanonicalGradedCons.cs` (which grade earns which value — only the *multiset* of prices was checked before, so swapping two grades passed) and `CanonicalTalentLinkedAbilities.cs`. **Do not "fix" a failing test by editing these to match the code** — they are the rulebook. Check the page named in the entry's `source_ref` and fix whichever side is wrong.

  **A flaw's `type` is pinned to its consequence as well as its string**, because that field feeds the Resolve formula: retyping `obligation` from `plot_hook` moves a character's computed Resolve from 25 to 24, and a test asserts the figure, not only the word.

  **`linked_ability` on a Talent is the exception, and it is this project's own invention.** The rulebook prints **no Talent→Ability table at all**; the only explicit pairing anywhere in Ch.1–2 is Covert : Agility on p.17, via the half-Agility substitution rule. The other eleven values have been in `data/rules/talents.json` unsourced since the first commit and are used only to group Talents under an Ability heading in the CLI and the browser — no cost, no rank, no validity depends on one. They are pinned as a **regression snapshot**, and the transcription file says so per entry rather than carrying invented page citations. Do not add a `source_ref` to them and do not let a later reader mistake them for transcribed values.
- `PrebuiltHeroes.cs` transcribes the 20 published Heroes from Ch.8 and `PrebuiltHeroTests` rebuilds each one, asserting the printed Edge, Health and Resolve. Same rule applies: those numbers are the authors', not ours. They are the only tests that check the rules as *applied* rather than as transcribed, so a failure there usually means a rule was misread, not that a number is stale.
- **17 of the 20 Heroes rebuild to exactly 125 Hero Points**, and each of the seventeen is named on `HeroRebuildsToExactly125`. **That roster and the residuals in `BuildByHero` are two statements of one fact, and `TheExactHeroListNamesEveryHeroRecordedExact` is what makes them agree.** A count and a list cannot check each other: Herald (Airmid) sat recorded at a residual of 0 with no `InlineData` case rebuilding her, so `MostHeroesReconcileExactly` counted seventeen while sixteen were being rebuilt one by one, and nothing went red because neither list was derived from the other. It is the quiet form of a check that stops running. The other three are held at a recorded residual in `PrebuiltHeroes.BuildByHero`, none more than 1 HP out **as modelled** — Shadow's printed Gear box carries a Silenced pair his transcription does not, which would put him at +2, so the bound is a fact about what is counted rather than about the authors' arithmetic. Do not tune an ambiguous variant just to force one of those to zero — that is fitting the model to the answer. Fix the underlying gap instead.

  **T-Kay is the one Hero whose grade was decided rather than derived, and the distinction is the whole of why it is not the tuning the rule above forbids.** Her sheet prints `Lightning Reflexes (Limited: only for Telekinesis)` with no grade, and the rulebook prints no rule mapping a restriction onto one of the three. The owner ruled it *somewhat limited* on 2026-09-06, and she closes at 125 as a consequence. **A ruling is a person deciding an ambiguity the book left open; tuning is choosing the reading that makes a total come out.** Nobody may promote a residual into a ruling on their own — take it to the owner, or leave it recorded.
- **`EveryPublishedHeroIsALegalCharacter` asks the one question the rest of that file does not: would this tool accept the character the authors printed?** Nothing did until it existed, and two rules were wrong because of it — Blastwave came back with four `DUPLICATE_PRO` errors and T-Kay with `PRO_NOT_APPLICABLE` on the Zone Pro printed on her sheet, so two Heroes in the rulebook could not be built here. The budget exemption is keyed to the residual recorded for each Hero, so a Hero who starts costing the wrong amount fails the residual test rather than being excused. A cost test and a legality test are different questions; keep both.
- The package each Hero used is inferred, not printed. `ExactlyOnePackageLandsAnExactHeroOn125` re-runs that inference and asserts exactly one package fits each exact Hero, so the attribution cannot quietly become a convenient guess; for the other three it is the closest fit. Vector is why that test exists — his package was recorded as Superhero on a closest-fit basis while his Deflection was underpriced, and correcting the Power made Hero the only fit.

The root `.csproj` sits at the repository root, so it carries a `<Compile Remove="…" />` for every sibling project directory; without them the default `**/*.cs` glob pulls their sources into the CLI. Shared build settings — target framework, nullability, the analyzer contract — live in `Directory.Build.props`, so the seven projects in the solution — the CLI at the root, `engine`, `sheets`, `web`, `mcp` and the two test projects — cannot drift into different strictness.

The project targets **.NET 10** (`global.json` pins SDK `10.0.100` with `latestMinor` rollForward). The 9.x SDK cannot build it; install with `winget install --id Microsoft.DotNet.SDK.10`.

**Both test projects run on Microsoft.Testing.Platform (MTP), not VSTest — `global.json`'s `test.runner` says so, and it is what `dotnet test` reads the opt-in from** (a `dotnet.config` `[dotnet.test.runner]` section does *not* take on this SDK; `dotnet test --help` names `global.json` as the place). The test projects carry only `xunit.v3`: `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` were removed, because under MTP the test project self-hosts and the VSTest adapter is dead weight. See PROGRESS.md item 20 for the migration itself.

**The old VSTest trap this section used to warn about is closed, not just reworded — proved by planting the crash and reading the real output.** A `[Fact]` with unbounded recursion (`dotnet test tests/ProwlersAndParagonsAutomation.Tests --configuration Release --filter "FullyQualifiedName~UnboundedRecursionCrashesTheProcess"`, on SDK 10.0.303 with xunit.v3 4.0.0) printed:

```
Error output: Stack overflow.
  Repeated 31418 times:
  --------------------------------
     at ProwlersAndParagonsAutomation.Tests.ZZZTempCrashTest.Recurse(Int32)
  --------------------------------
     ...
Test run summary: Zero tests ran
  error: 1

  total: 0
  failed: 0
  succeeded: 0
  skipped: 0
  duration: 847ms
Test run completed with non-success exit code: -1073741571 (see: https://aka.ms/testingplatform/exitcodes)
```

No `Passed!` anywhere, no `Failed: 0` — the crash is reported as an infrastructure `error`, the exit code names the same `0xC00000FD` VSTest used to hide, and `total: 0` is honest about the fact that nothing ran to completion. **So `Catastrophic` is no longer the thing to grep for** — under MTP that word does not appear at all; a crashed run is `Zero tests ran` with a non-zero exit code and an `Error output:` block naming the fault. CI notices the same way it always did, through the exit code; a human tailing the log now sees the crash instead of a line that lies about it. `node --test` still has its own trap in another spelling — an empty glob exits 0 reporting zero tests — and that suite's CI step still asserts the count for that reason.

**A filter matching nothing is refused the same way, not reported as success**: `dotnet test <project> --filter "FullyQualifiedName~NoSuchTest"` prints `Test run summary: Zero tests ran` and exits non-zero (`8`, MTP's own "zero tests ran" code) rather than `0`. The VSTest-style filter syntax itself (`FullyQualifiedName~Foo`, `FullyQualifiedName=Foo`) is unchanged under MTP — measured, not assumed — so nothing that already filters this way needs to change its spelling.

**`dotnet test` used to print one `Passed!` per project because VSTest ran each project as a separate pass; MTP prints a single combined `total:`/`succeeded:`/`failed:`/`skipped:` block for a solution-wide run**, with one `passed (…)`/`failed with N error(s) (…)` line per assembly above it rather than a per-project count. `./scripts/count-tests.sh` reads a per-project figure by invoking each test project on its own (`dotnet test tests/<project> --configuration Release`) and grepping that run's own `succeeded:` line — **never `--nologo` or `-v q` on a project-scoped MTP invocation**: both are forwarded to the test host rather than consumed by the `dotnet` CLI, and the host reads them as unrecognised arguments and reports `Zero tests ran` without running a single test. That was measured while updating the script for this migration, not assumed.


## Static analysis

- .NET analyzers run at `AnalysisLevel=latest-recommended` with `EnforceCodeStyleInBuild`. `TreatWarningsAsErrors` is conditional on `ContinuousIntegrationBuild`, so local builds stay warning-only while CI is strict. **Keep the CI build at zero warnings.**
- Deliberate rule exceptions live in `.editorconfig` with an inline rationale — CA1305/CA1304 are off because all formatted output is human-facing terminal/sheet text, and CA1822 is a suggestion so `CostCalculator`/`DerivedStatsCalculator` keep a uniform instance API. Add rationale when adding an exception; do not add bare suppressions.
- Qodana (`qodana.yaml`, `jetbrains/qodana-cdnet:2026.2`) runs ReSharper inspections in `.github/workflows/qodana_code_quality.yml`. Two non-obvious constraints: the `dotnet.solution` key is required (without it Qodana finds no project and reports nothing), and the **Community** linter (`cdnet`) is deliberate. **The release linter needs a Cloud licence, not just a token — and a token alone breaks `cdnet` too.** That was tried: with a `QODANA_TOKEN` secret set, both images linked the Cloud project and exited on "License request: token was declined by Qodana Cloud server", having inspected nothing. So the workflow deliberately does **not** pass a token: a scan cannot be broken by a credential it never reads. `cdnet` needs no token, no account and no licence.
- **Qodana is the only thing that sees a Razor deprecation, and `.razor` files really are inspected — measured, not assumed.** A `.razor` file sets a component parameter by string key rather than by referencing the property, so the C# compiler never sees an `[Obsolete]` attribute on it: `Router.NotFound` was deprecated in .NET 10 and `dotnet build` reported zero warnings with warnings-as-errors on. Do not read a clean build as a clean bill of health for the components.

  **Do not conclude the opposite from an empty SARIF, which is a mistake made here.** Four whole-tree scans in one slice produced no finding referencing any `.razor` file, and that was read as "Razor is not inspected at all" — a conclusion that would have condemned the whole tool. It is wrong: no *finding* in a file is not the same as the file not being analysed. Proved by planting the same unresolvable `<see cref="DoesNotExist"/>` in a `.razor` `@code` block and in a `.cs` file and scanning once: **both** reported `InvalidXmlDocComment`, and the SARIF named `web/Components/RulebookEntry.razor`. Razor `@code` blocks get `UnusedMember.Local` too.

- **What Qodana does *not* catch is a doubled `<summary>` block, and this file used to imply it does.** The note further down cites, as a real bug no compiler sees, "a doc comment stranded on the wrong method by an insertion, so one member carried two `<summary>` blocks and a `<paramref>` for a parameter it did not have". Those are two different things and only the second fires: a `<paramref>` naming a parameter that is not there is `InvalidXmlDocComment`; **two `<summary>` blocks on one member is reported by nothing.** Measured both ways — planted in a `.cs` file and left in place in `MainLayout.razor`, where one has sat since before this slice; neither was reported, on a scan that found six other things. ReSharper ships duplicate-tag rules for `param` and `typeparam` and none for `summary`. So a stranded summary is found by reading, and by nothing else.

  **Both of the above were established with a control that fires.** A probe that reports nothing tells you nothing unless something in the same run reports — which is why the cref pair is the instrument: the `.cs` half is known to fire, so the `.razor` half reporting or not is a real answer either way. And plant probes in a `git archive` export, then **strip `bin/` and `obj/` after checking they compile** — a probe that does not compile suppresses inspections and reads exactly like "not inspected".
- **Qodana does not run on a pull request any more, and the local scan is now the only one before a merge.** `.github/workflows/qodana_code_quality.yml` runs it on `main` and weekly; the `pull_request` trigger was taken off because it was 214 of one session's 403 Actions minutes and all 10 GB of the cache — see [`hosting.md`](hosting.md) for the measurement. **What made that safe is that `CLAUDE.md`'s process already required `./scripts/qodana-scan.sh` locally, reading zero, before opening a pull request.** It is no longer a belt-and-braces step: skip it and the first thing that sees your branch is `main` after the merge.
- **And when it does run on a pull request — a `workflow_dispatch`, or a branch someone re-enables it on — its counts are not comparable to a scan of the whole tree.** It runs in PR mode there, only changed files, so moving a file re-reports every finding in it as new. The Blazor slice moved `engine/` and `sheets/` into new projects and the count went from 144 to 249 without any of that code changing. Read the SARIF (`gh run download <run-id>`, then `qodana.sarif.json`) rather than the summary table before concluding anything moved.
- **A whole-tree Qodana scan reports zero, and the config that gets it there is in `.editorconfig`, not `qodana.yaml`.** `qodana.yaml`'s `exclude:` list accepts an inspection *name* and silently ignores it — the .NET linter is ReSharper, which takes severities from EditorConfig. Only the path exclusions in `qodana.yaml` do anything. Each `resharper_*_highlighting = none` there is scoped as tightly as the tool allows and says why; nothing is baselined and there is no severity floor. A pull-request scan would only cover changed files, and there is no longer one — to see the real number, run it over the whole tree yourself:

  **This claim rots, and it has now rotted four times.** It was 3 on `master` and 37 across the three reconciled audit slices before anybody measured; a previous slice found the same thing. Two of those 37 were real bugs no compiler sees — a doc comment stranded on the wrong method by an insertion, so one member carried two `<summary>` blocks and a `<paramref>` for a parameter it did not have. **Do not repeat the "reports zero" sentence without re-running the scan**, and read the count out of the log *and* the SARIF: a `grep` for the summary line prints nothing when the scan never ran, which looks identical to clean.

  **The third and fourth came on the same day, from one commit, and they are the reason the figure is no longer written down in `PROGRESS.md` at all.** A whole-tree scan of `main` reported **2** — both `InvalidXmlDocComment`, on a single unclosed `<para>` in `WorkflowFilterTests` that arrived with the executable-bit guard in #114 and was reported by nothing for four days. A scan of an eight-package NuGet bump reported **5**: the same 2, plus three `MethodHasAsyncOverload` in `AdminPageTests.cs`, **a file that bump does not touch**. That second one is the shape worth remembering — a package upgrade moved an inspection in code nobody had edited, so the finding belongs to no diff and a pull-request-mode scan structurally cannot see it. Both were fixed. **Only a whole-tree scan of an export ever finds this class of thing, and nothing schedules one but a person.**

  ```bash
  docker run --rm -v "$(pwd -W):/data/project/" -v "$PWD/results:/data/results/" jetbrains/qodana-cdnet:2026.2 --save-report
  ```

  The report is `results/qodana.sarif.json`; the summary counts by rule, never by file, so group it yourself.

  **Run it on a clean export of the commit, not on your working directory.** That command mounts the directory as it is, `bin/` and `obj/` included, and a tree that has been built a few times scans very differently: the same commit reported **0** from `git archive HEAD | tar -x -C <tmp>` and **1471** in place — including `.CSharpErrors`, which is *compile* errors, on test files that build clean. Do not read a number off an in-place scan and conclude anything about the change; export first, and scan the parent commit the same way if you want a comparison.


## Do not run that command by hand. Run `./scripts/qodana-scan.sh`

```bash
./scripts/qodana-scan.sh
```

**Because a scan that never ran is indistinguishable from a clean one, and the by-hand version has now produced exactly that.** `qodana` exits **0** when it cannot find a project to inspect — it prints its own `--help` and one line of error at the end of a long log, writes no SARIF, and every `grep` for a summary line comes back empty, which reads as "nothing found". The script closes the three traps that make a by-hand scan worthless, each with a control that fires:

- **A Git Bash path handed to `-v` unconverted mounts an empty directory.** `-v /tmp/export:/data/project/` gives Docker Desktop a path that means nothing inside the VM, so the container finds no `qodana.yaml`, exits 0, and inspects nothing. That is what went wrong. **The fix is `pwd -W`, not avoiding `/tmp`** — measured both ways: with `pwd -W` an export under `/tmp` scans perfectly, and without it the same export scans nothing. The script converts every host path and additionally stages under the repository (`.qodana-scan/`, gitignored), which is belt to that brace rather than the fix. It then **proves the mount** by looking for `qodana.yaml` from inside a container before starting the scan — and that guard has been watched to fire: an unconverted path exits 1 naming the path, rather than scanning nothing and reporting zero.
- **The exit code is not evidence.** The script requires `qodana.sarif.json` to exist and to parse, and exits non-zero with the tail of the log when it does not. A zero it prints is a zero from a report that exists. Watched to fire too: with no report written it exits 1 saying `NOTHING WAS INSPECTED` and reports that the container's own status was 0, which is the whole trap in one line.
- **The export needs its own control.** A `git archive` that produced nothing scans an empty tree, which is trap one again; the script checks `qodana.yaml` is in the export before mounting anything.

**The export is deleted on the way out, and on Windows that is a bug fix rather than tidiness.**
Qodana *builds* the project it is given, so `.qodana-scan/project/` does not stay the clean tree
`git archive` wrote — it grows `obj/Release/net10.0/…` under every test project, about half a
gigabyte of it. In a worktree whose root is already 125 characters the deepest of those lands near
288, and Windows' 260-character limit starts refusing operations on it: **`git worktree remove`
fails with "Filename too long" *after* deregistering the worktree**, which leaves the files orphaned
and git no longer able to delete them. `results/` and `qodana.log` survive, because they are what
the run is for and what a failure is read from; the export is `git archive <commit>` and one command
reproduces it. It is a `trap … EXIT`, so the SARIF-missing bail-out and a Ctrl-C leave no more behind
than a clean run — **a trap that only fires when nothing went wrong never fires on the runs that
matter**, and that was watched by planting an `exit 1` immediately after it and checking the export
was gone and the results were not.

It also groups the findings by file, which the tool's own summary never does — that summary counts by rule.
- What is silenced and why, in one line each: `engine/Models/*.cs` exists to be deserialized by reflection (four inspections), the test transcription records document a rulebook page rather than being read, a `[Theory]` body asserting on its parameter is not a precondition guard, `JsonValue.Create(...)!` is load-bearing (removing it fails the warnings-as-errors build), and this codebase writes explicit constructors and named backing fields on purpose.
- `data/rules/*.json` is copied to the output directory by the csproj, so a published build works without the repo checked out.


## Five suites, and one of them is not a test project

Two `dotnet test` projects, and three `node --test` directories beside them. Each JavaScript one has
its own script and its own CI step, because each drives a different thing and a shared glob would
make "the suite passed" ambiguous:

| Suite | Runner | What it drives |
|---|---|---|
| `tests/ProwlersAndParagonsAutomation.Tests` | `dotnet test` | the engine, and every source-reading guard |
| `tests/ProwlersAndParagons.Web.Tests` | `dotnet test` | components, rendered, with bUnit |
| `tests/worker` | `./scripts/test-worker.sh` | the accounts server, against real SQLite |
| `tests/visual` | `./scripts/test-visual.sh` | the pixel comparator and its PNG codec |
| `tests/deploy` | `./scripts/test-deploy-gate.sh` | the deploy's D1 migration gate |

**There is a sixth thing that drives the project and is deliberately not in that table.**
`./scripts/e2e.sh` runs the assembled application in real Chrome against a real server, reports
verdicts rather than test counts, and cannot run without publishing a site first — so
`./scripts/count-tests.sh` does not know about it and should not: a suite whose figure is "six
checks" alongside four suites' thousands would make the total meaningless. See **Driving the
assembled app** at the end of this file.

**And a seventh, for the same reason: `./scripts/test-kill-tree.sh`.** It reports nine verdicts
about whether the harness can stop a server it started, and about what it says when it could not —
see **Proving `kill_tree`** below. It is a
step in `build.yml` and is not totalled anywhere either.

**`tests/e2e` is a .NET project and is still not a `dotnet test` project, for the same reason.** It
is the Playwright driver `e2e.sh` can be pointed at, it prints verdicts, and `dotnet test` would
report it as a suite of zero. It is in the solution so that `dotnet build` compiles it under the
same analyzer contract as everything else; nothing else picks it up.

**The fifth is the newest of the five and the reason it exists is worth stating: a workflow cannot
be executed by any of the other four.** `scripts/d1-migrations/gate.mjs` is the *decision* the deploy makes about
pending migrations — apply, refuse, or proceed — pulled out of the shell so it can be driven with
canned wrangler output. That is the same shape `scripts/visual/diff.mjs` took for the same reason,
and its six branches are each proved by mutation rather than by reading.

**A solution-wide `dotnet test` prints one combined `total:`/`succeeded:`/`failed:` block for both
.NET projects, not one `Passed!` line each** — see the note above on Microsoft.Testing.Platform for
what that block looks like and why a crash cannot hide inside it any more.

## Counting them is `./scripts/count-tests.sh`, and the count is not written down anywhere

`PROGRESS.md`'s **Tests** row used to carry the five figures and a running account of each slice's
delta. **It went wrong four separate ways**, and the four are kept here because the lesson outlives
any particular number:

1. **Summands that did not add up.** It read *5023 across 4040 / 730 / 228 / 14 / 19*, whose own
   parts total 5031 — the figures had been copied out of mid-branch commit messages and three more
   commits landed after them. **A row that does not add up is the cheapest tell there is.**
2. **A count read off CI and compared against a local baseline measured days earlier**, from which
   somebody concluded that one engine test exists on a Linux runner and not on a Windows checkout.
   It does not. Six Dependabot pull requests had landed underneath the branch while it was open.
   **A stale baseline and a platform bug look identical from inside a long-lived branch, and the
   difference is one `git fetch`.**
3. **The same again, one slice later**, which is what turned a mistake into a pattern worth a rule.
4. **The row rewritten on a branch while `main` rewrote it too.** One slice rebased five times in
   an afternoon and three of those conflicts were this single line — a guaranteed collision between
   any two concurrent branches, on a fact neither of them disagreed about.

So the figures are gone and the script is the answer. Two properties of it are load-bearing:

- **A missing count is an error, not a zero.** Each suite's number is read out of the line its
  runner printed, and nothing is totalled when one is absent — because a suite that did not run and
  a suite with no tests produce the same silence, which is the fault this whole file is about.
- **Zero is refused as well as empty, and that was found by breaking it.** Pointing the pixel
  comparator's glob at a file name that does not exist did not produce silence: `node --test`
  matched nothing, printed `pass 0`, and the first version of the script totalled the other four
  and called it a result. None of the five has ever held fewer than 14 tests, so zero is a state to
  refuse rather than to report.

**There is deliberately no committed baseline to compare against.** A stored number is the thing
that goes stale, and a test count is not a quality gate — the suites failing is. Comparing two
commits is `git worktree add` and a second run.

## Two lists in two projects that cannot see each other

`CampaignTableNamesTests` requires every `bool` on `play/Encounter/TableRules.cs` to have a
same-named property on `engine/CampaignTable`, and the reverse. They are two types because
`engine/` may not reference `play/`, so nothing in the compiler holds them together.

- **It reads `TableRules.cs` as source text rather than by reflection**, because reflecting over it
  would mean this test project referencing `play/` in order to enforce that `engine/` does not.
  The campaign side *is* reflected over, since this project already references the engine and a
  real type is a better witness than a second regular expression.
- **The count assertion is the positive control and is not decoration.** A regular expression that
  has stopped matching — a file moved, a record rewritten with primary-constructor parameters —
  makes two empty sets, and two empty sets are equal. That is a green test asserting nothing,
  which is three of this repository's four historical guard faults.
- **The failure message says which direction is wrong**, because the two are different bugs: a
  switch only `play/` has is a rule nobody can choose, and one only the campaign has is a promise
  the simulator will never keep.
- `CampaignTableExportTests` extends the same chain to the JSON export, holding its key list to the
  switch list rather than typing thirteen names out — so the guarantee runs from the simulator
  through the campaign to the file the encounter server reads.

`HousePriceReadTests` is the other guard this slice added, and it is `TraitCapReadTests`' shape:
every `PowerCost` call in `engine/`, `sheets/`, `web/`, `cli/` and `mcp/` passes the character's
house price, or the file is named with the reason it prices the book. Its own positive control
requires the scan to find the qualified calls in every one of the five projects, because a pattern
that has stopped matching reports no offences.

**It shipped with the half of `TraitCapReadTests` that makes an exemption an exemption missing**,
and the failure is worth keeping because it is the shape an allowlist fails in. The entries carried
a count and a doc comment saying the count was there so that "an exemption that permitted a file
outright would let a second, wrong read in beside a right one" — and the code tested `Calls == 0`
and otherwise permitted the file. Breaking *both* of `CharacterSheetRenderer`'s calls under an entry
declaring one left it green. A stale entry was invisible for the same reason, so a file whose
sanctioned calls had gone kept permitting whatever was written there next. Both are compared now,
`Sanctioned` is empty — nothing in the five projects prices the book for a character — and comments
are stripped per line before the scan, because a paragraph explaining why `costs.PowerCost(sp)` is
wrong was otherwise reported as an offence. Each of the four was watched going red: the count, the
stale entry, the dead pattern, and the comment.


## Two guards read `PROGRESS.md` itself

**Both exist because a stale claim in that file is inherited by every agent at once.** It is the first thing `CLAUDE.md` sends anybody to, and an audit on 2026-09-05 found **21 dead pointers and ten factual drifts** in it — one of which had already sent a reader off to build something that had shipped three days earlier. `CLAUDE.md`'s standing rule is that a dead pointer is worse than no pointer, because it reads as though the reasoning was written down and sends the reader to the one place it is not. These two are that rule applied to the index of open work.

| Guard | What it holds |
|---|---|
| `ProgressPointerTests` | every link, anchor, archive reference, named test and item number in `PROGRESS.md` leads somewhere |
| `ProgressCurrentStateTests` | no cell of its `Current state` table quotes a measured figure instead of naming what measures it |

**Read each one's doc comment before changing it, because both say what they cannot do**, per `CLAUDE.md`'s rule. `ProgressPointerTests` is about *pointers*, so a claim with no link is invisible to it — "`CLAUDE.md` is 290 lines" where it is not, a page count one out. Those are the other half of that audit's findings and they still need somebody to read the file against the code. `ProgressCurrentStateTests` matches *shapes* of figure — a count of tests, migrations, sections or checks; a commit sha; a pending state — so a count spelled a way it does not know walks straight through. Its positive control is the `Tests` row's own pointer: the table has to be there, that row has to name `./scripts/count-tests.sh`, and the script has to exist, so a scan matching no cells fails rather than passing vacuously.

**Why that second one is a test and not a style note.** Of the 201 commits that have touched `PROGRESS.md`, **122 touch its 23-line `Current state` table** — 61% of the file's churn in about 1.5% of its lines, and every one of those commits is somebody transcribing a number measured somewhere else. Two branches editing two different `###` items merge clean; two editing that table are odds-on to collide, and a re-push costs a ~19-minute CI job. The rule the `Tests` row demonstrates is the fix: **a cell whose content is a measured figure names the command that measures it.** A figure that moves when the *data* moves stays — `Powers: 141 entries` is what that row is for.

**The one that keeps needing explaining is why the sha check is an allow-list rather than `git cat-file`.** Reachability is the check anybody reaches for first, and it cannot run where it matters: `build.yml` checks out at `actions/checkout`'s default depth of **1**, so on CI every sha older than the tip is unreachable and a reachability test would fail the build on facts that are perfectly true. Making it conditional on a full clone is worse — it would pass by *not running*, which is the failure this guide's first section is about. So the check that runs everywhere is the list, and its cost is deliberate: naming a new sha means editing a test and saying what the sha is for. Every sha on the list must also still be in the file, so the list cannot quietly become the next place things rot.

## Two test projects, and the difference between them

- **`tests/ProwlersAndParagonsAutomation.Tests`** — the rules engine, plus `WebPresentationTests`, which *reads the source* of `web/` because the disciplines below are statements about how it is written, and `HeadlessBuildTests`, which drives the `build` command end to end, and the seven `Mcp*Tests`, two of which — `McpServerTests` and `McpPlayServerTests` — drive a server over a pair of pipes, through the one helper described further down. **The wizard itself still has no harness** — that is the CLI gap, and it is narrower than it was rather than closed.
- **`tests/ProwlersAndParagons.Web.Tests`** — bUnit. It *renders components* and asserts on the output, and it is the only project that may reference `web/`.

**The split is the point.** A source-reading test cannot see a bug in rendered output, and one duly shipped: Razor swallowed the space in `@name` + `<text> @(rank)d</text>` and the sheet printed **"Armor8d"**. It was fixed on the sheet and the same bug in a second spelling survived on the Powers tab for another whole slice, because no source file looks wrong. Anything about what a component *produces* belongs in the bUnit project; anything about how the source is *written* belongs in the other.

**Assert on `TextContent`, never on markup with the tags stripped out.** Stripping a tag leaves a separator where it was, so `<b>Armor</b><span>8d</span>` reads as "Armor 8d" to any test that does it — which is how the Powers tab kept the Armor8d bug through a test written to catch it. It cuts the other way too, and worse: a `DoesNotContain("Communications 0d")` over stripped markup is satisfied by printing exactly that with the two halves in different elements. An adversarial pass did it, visibly, with the suite green. The browser concatenates text nodes; so must the test.

**And the trap was inside the helper the render tests use to catch it.** `SheetRenderTests.Rendered` replaced every tag with a newline and one test then collapsed all whitespace, so `<b>Armor</b><span>8d</span>` read as "Armor 8d" — the exact string the assertions look for, produced by the exact bug they exist to find. It concatenates text nodes now. A test-side helper is as capable of being the bug as the component is; read the helper before trusting the assertion.

**A typographic rule lives in the stylesheet, where no rendering test can see it.** Emptying `.hp` puts Hero Point costs back in the same size, weight and ink as ranks and every bUnit test still passes, because the class is still on the element. Anything whose whole substance is CSS — the `.hp` treatment, print font sizes, the break rules — is asserted in `WebPresentationTests` against the parsed rule, not inferred from markup.

**And when you do measure in a browser instead, the units bite — the instrument is as capable of being wrong as the thing it measures.** A contrast probe read `getComputedStyle`, which returns a colour as `rgb(0–255)` *or* as `color(srgb 0–1)` depending on how it was written, and read both on one scale. Everything was therefore measured against black, and it reported **1.00 for a pair that is plainly legible**. It carries a **white-on-black positive control that must read 21**, and nothing it says is worth reading until that passes.

This is the sibling of the `getBoundingClientRect` border-box failure in `CLAUDE.md`'s list of checks that passed for the wrong reason — same genus, and only that one was written down. Both say: *a measurement is a claim about the instrument before it is a claim about the page.* The C# contrast resolver in `WebPresentationTests` never had this failure mode, because it works from parsed CSS source rather than from a computed style — so if a browser-side probe is ever built again, this is the trap, and it will not be caught by the existing tests.

**A bUnit event is *dispatched*, not applied — and the synchronous trigger does not wait for it.** `element.KeyDown(…)`, `Click()` and `Input(…)` post the event and return; only the `…Async` forms come back when the render it caused has finished. While the renderer is idle the post runs inline and the difference never shows, which is why the synchronous form works in almost every test here and why the exception is invisible until a loaded runner finds it. **It found one**: `PaletteBookTests.TheArrowKeysReachTheBooksRowsAndEnterChoosesOne` failed one CI run out of many and passed six times out of six locally, reading `aria-selected="false"` off a row the palette moved to a moment later. Nothing was wrong in the component — the drive was reading one render early.

So **use the awaited form wherever anything the component subscribes to can still be in flight**, which in this project means anything that has been over the wire: an answer arrives on a thread-pool continuation and the redraw it raises is queued through `InvokeAsync`, so for that moment a keypress cannot be handled inline. A `WaitForAssertionAsync` after the press is the other honest shape; what is not honest is a synchronous press followed by a bare assertion.

**Fixing the one that failed does not fix the file, and this one did not.** The same class went red a second time in the same test class, on a `Click` rather than a `KeyDown` and with a failure that reads nothing like the first: `TypingAgainDropsTheRowsForTheQueryBeforeIt` had a synchronous click on a book row followed by `Assert.Equal("trait cap", TakeRequestedSearch())`, and under load the handler had not run, so the request had never been made and the assertion read `null`. **That is the dangerous shape** — a dispatch that has not landed yet is indistinguishable from a product that did not do the thing, so the flake accuses the component instead of the drive. Two more reads in that one test were one render early for the same reason. So when one of these is found, **fix every synchronous drive in the file whose next line reads the result**, rather than the one that happened to be caught; the awaited form costs nothing where the renderer is idle.

**And the fix is unprovable without holding the renderer busy on purpose**, so that test does — a work item posted from another thread, because one posted from the test's own thread runs inline and occupies nothing — with a positive control saying it really was busy, since an instrument that has stopped occupying anything leaves a drive that passes for the wrong reason. (That control was a stopwatch reading at first, and the hold was a 250ms sleep; the last paragraph of this section says why neither is any more.) Reverting that one press to `KeyDown` fails it every run; reverting it *without* the instrument passes on any quiet machine, which is precisely the check that was never there. The instrument is `BusyRenderer.Occupying`, generic over the component and shared by every drive that needs it — one copy, so the positive control cannot be left off the next one.

**It happened a third time, and "fix every drive in the file" turned out to be the wrong unit too.** Run 33981303114 failed `AWordTypedIntoTheBannerReachesTheBookThroughThePaletteAndOnlyOnce` at `PaletteBookTests.cs:315` on `Assert.Empty() Failure: Collection was not empty` — an `HtmlDivElement`, which is the palette overlay still up one line after a synchronous `KeyDown` of Escape. A third failure, a third spelling, and a third message reading nothing like the two before it. The renderer is busy after **every** book answer in that class, because `Settle` → `BookAnswered` → `Redraw` queues through `InvokeAsync`, so any drive that follows a `WaitForAssertionAsync` there is exposed — and the first two fixes had each converted one test.

So the unit is now the **surface**, not the file: `PaletteBookTests`, `BannerTests` and `CommandPaletteTests` were swept together, **56 synchronous drives converted to the awaited form** — 36, 10 and 10 — and the drives whose next line *is* the assertion the test exists for are driven under `Occupying`, which goes from 3 sites to 26, so the losing order is taken every run rather than waited for. Two of those were not merely early but wrong about themselves: the debounce test released its pause gate before either keystroke had reached the pause, under a comment claiming both were inside it, and `EnterOnAPowerRequestsItAndAddsNothing` pressed Enter on whatever row a posted `Input` had not yet moved off. The failure the *first* commit found was still the shape of all of them — **a dispatch that has not landed is indistinguishable from a product that did not do the thing**.

**And a fourth cannot be written by hand any more.** `PaletteDispatchTests` scans those three files for `.Input(`, `.Click(`, `.KeyDown(`, `.Change(` and `.Submit(` and fails naming the file and line, with the two positive controls that stop "no synchronous drive" being satisfied by three files that drive nothing: the pattern is asserted to fire on a known-bad line and not to fire on the awaited one, and each scanned file is asserted still to contain an awaited drive and still to reach for `Occupying`. **Its own doc comment says what it cannot do**, per `CLAUDE.md`'s rule: a drive routed through a helper or through `TriggerEvent` walks straight through it, and no scan of source text has an opinion about ordering, which is the actual defect. The scan is the cheap catch; `Occupying` is the proof.

**And then the fifth sighting was the instrument itself, which is a new shape and the one worth reading twice.** `CommandPaletteTests.TypingFindsAPower` failed one full-suite run and passed every re-run — but that test had been converted by the sweep, so the trap above could not be the cause, and it was not. Everything in it is deterministic once you look: the palette there is anonymous, so `AskTheBookAsync` returns before its first `await` and `NoteWhoIsAskingAsync` finds the offer unchanged and returns without recounting; nothing raises `Commands.Changed` after the `Open()` that precedes the render, so `Refresh` never runs and cannot empty the box; and the drive is awaited. **The only assertion in it that could be false while the product and the drive were both right was inside `Occupying`** — and it was a stopwatch reading.

Both halves of the old instrument depended on a wall clock, and neither dependence is visible from the call site:

- **The hold was `Thread.Sleep(250)`**, so whether the drive really landed behind it depended on everything before the post fitting inside those 250ms — and the post is not the first thing a drive does. Most call sites spell the drive `() => page.Find(".palette-box").InputAsync(…)`, and that `Find` is inside the window: **measured at 178ms of the 250** the first time AngleSharp and the CSS engine are touched in a process. Slower than that and the hold was over before the event was posted, so the losing order was not driven at all — and the elapsed control could not tell, because the same 178ms it spent on `Find` is what made the reading look healthy. **An instrument passing for the wrong reason, which is the exact failure it exists to prevent.**
- **The control was `elapsed > 125ms`**, which is the sleep minus however long the test thread took to post the drive after being woken. Stall that thread for a fifth of a second — what a loaded runner does for free — and the control goes false with nothing wrong anywhere. Reproduced deterministically: a 200ms stall between the renderer going busy and the drive being posted turns it red every run while the test's own assertions about the palette still pass.

**So the hold is now a gate the helper opens and the control is a fact about the queue.** The renderer is held until `Occupying` itself releases it, however slow the machine and however slow the `Find`; then two things are read while it is still held — that the drive has *not* come back (it would have, had it been handled inline) and that the hold is still holding — and only then is the gate opened and the drive awaited. There is no duration anywhere in the file. `BusyRendererTests` watches the first control fire on the two spellings that must fire it, and the second is watched by editing the helper, because a gate cannot be observed opening early by anything except the line that opens it — its doc comment says so rather than leaving it looking covered.

**The rule this leaves behind is bigger than the palette**: an instrument built to remove a timing dependence must not have one of its own. A sleep is a guess about how long the code under it takes, and a stopwatch assertion is a guess about how busy the machine is; both are the thing being tested for, moved into the test. Where a gate will do — a `ManualResetEventSlim`, a `TaskCompletionSource`, `Commands.Pausing` — use the gate.

**A source-scanning regex gets the linear engine, never a wall-clock timeout — build it with `ScanRegex.Build`.** Both projects used to spell every scan `new Regex(pattern, options, TimeSpan.FromSeconds(5))`, and a full run on a ten-core machine at load average ~176 lost `AccountsContractTests.NoKeyOrTokenIsInTheRepository` to a `RegexMatchTimeoutException` — a credential scan reporting a bad tree when what was wrong was the scheduler. **The cap was the smaller half of the problem.** Sweeping all 212 pattern literals in the two projects against the files they really read found three that are super-linear on realistic input: the credential pattern is *quadratic* in the length of any unbroken run of token characters (64k costs 1.9s idle and 128k throws at five seconds with no load at all, so one minified bundle or base64 blob committed under `web/` would break it on an idle laptop), and `WebPresentationTests`' two `([^{}]+)\{…\}` stylesheet scanners cost ~640ms each over the real CSS, because the unanchored leading class re-scans every brace block for a declaration that is almost never in it. `RegexOptions.NonBacktracking` makes match time linear — the two stylesheet scanners drop to under 1ms with byte-identical results, captures included — and with no runaway left to catch, the timeout is `Regex.InfiniteMatchTimeout`, because the only thing a cap can still do to a linear matcher is turn a busy runner into a false verdict, and **a guard that flakes is a guard that gets retried past**. A pattern needing a lookaround, a backreference or an atomic group falls back to backtracking under a 60-second hang detector; that refusal happens at *construction*, so nothing slides quietly back to exponential. Three traps if you touch this. A lookaround silently costs the linear guarantee, so prefer a formulation without one. **A fallback that swallowed everything would look exactly like a fix**, which is why `ScanRegexTests` asserts the engine rather than the answer — forcing `Build` to always fall back was watched turning it red, and a theory walks every construct the linear engine refuses to prove the fallback is actually reached rather than throwing. And **`Group.Captures` is the one thing the two engines disagree about**: the linear engine keeps only the last capture of a quantified group. Both engines were run over all 206 linear patterns against every file they scan — 61,594 comparisons — and match count, span, group value, group index and `Replace` output are identical, so "byte-identical" is true of everything a caller here reads; nothing reads `Captures`, and a test pins the difference so the first one to try finds an assertion rather than a wrong answer.

**Do not put a wall clock in the control either, which is the trap this paragraph originally fell into.** The first version of the linearity control timed `Build` plus one match against five seconds, on the stated grounds that the true cost was "under a millisecond". It is not: the timed region includes building the matcher, which costs about fifty times the match — 23ms idle, 39ms under 30-way load, and **528–756ms under the ~176 load average that produced the original sighting**. A 6.6x margin, on the exact machine state that had already broken this repository once. The control now keeps its only clock on the side that must *fail*: the backtracking twin of the pattern must not finish the blob inside a one-second cap, which load can only make more certain. Finding this also cost the blob: word-boundary anchors on the credential pattern mean the engine starts an attempt only at a boundary, so a solid 192k run of token characters has one start instead of 192,000 and drops from 13.7s to 23ms — the control went red saying its input had stopped being pathological. It is an alternating `A-` run now, which stays quadratic at 15.9s because `-` is in the character class and is not a word character. **An anchor removes one blowup shape; only the engine removes the class.**

**And the scan's corpus is part of the guard, not scenery.** `NoKeyOrTokenIsInTheRepository` read `worker/*.js` and `web/**/*.cs` — not `functions/`, which is the other half of the account server; not one of the 62 `.razor` files under `web/`; not `scripts/probe-mail.mjs`, which sends mail with the owner's own credentials; and not `tests/`, so a key in a fixture was invisible to the guard whose subject is keys in files. It now reads all of them, 6.47MB in ~66ms — **which is the engine change cashed in, because under backtracking that sweep is the thing that timed out**. A wall-clock cap does not merely flake; it prices you out of scanning your own repository. Exclusions are on what was *matched*, never on which file it was in: skipping a file is a standing permission to commit a key into it.

bUnit pulls AngleSharp transitively at a version carrying a published advisory, so `web/`'s test project pins AngleSharp forward. Do not suppress NU1902 instead — see the comment in its csproj.


## The in-process MCP server has one clean shutdown, and it is end of input

**The sibling of the bUnit dispatch trap above, in the other test project.** `McpServerTests` and `McpPlayServerTests` each stand a real MCP server up over a pair of `Pipe`s and drive a real client at it, and both go through **one** helper — `InProcessMcpServer.Drive`. Read its comment before touching the teardown; what follows is the short version.

**The trap.** `ModelContextProtocol.Core` 2.2.0 — the pinned version, `6fa3825` — has exactly one shutdown its `StreamServerTransport` treats as clean, and it is **end of input**: the read loop reads a null line, breaks, and calls `SetDisconnected(null)`, which completes the transport's message channel with no error, so `McpSessionHandler.ProcessMessagesCoreAsync`'s `await foreach` ends normally and `McpServerImpl.RunAsync` returns.

**Disposing the transport instead is a race.** `StreamServerTransport.DisposeAsync` cancels its shutdown token and then, four lines later, disposes the input reader — which completes the `PipeReader` under it. A completed reader raises `InvalidOperationException: Reading is not allowed after reader was completed` both from `Pipe.ReadAsync`'s entry check, which runs *before* the cancellation token is looked at, and from `Pipe.AdvanceReader`, which is where a read already in flight lands when it comes back. Whichever of the two arrives first decides whether the loop comes out with an `OperationCanceledException` the transport calls clean or with that one, which it hands to `SetDisconnected(error)`. A faulted channel faults `ProcessMessagesCoreAsync` — it catches `OperationCanceledException` and nothing else — which faults `RunAsync`, which is a red test in a run where nothing was wrong. Run `34040527190` lost that race on a **docs-only** branch, minutes after the identical tree passed on `main`.

**So the order the helper has to keep is:** dispose the client, **complete the client's writer**, wait a bounded time for `RunAsync` to end *on its own*, and only then dispose the transport and the server. The client cannot send that end-of-input itself — `StreamClientSessionTransport.CleanupAsync` cancels its own token and waits for its own read task and never disposes the streams it was handed — so the harness owns the pipe and completes the writer.

**A narrow `catch (InvalidOperationException)` is the wrong fix**, for the reason `CLAUDE.md` gives about denylists: it swallows one spelling and leaves the race under it. The ordering removes the race; nothing completes the reader while the loop is still live.

**One helper, because there were two.** Both classes carried the same code under different names, which is the shape a fix lands in one of and not the other. There is no third: `McpStdioTests`, `McpPlayStdioTests`, `McpQuestionPolicyTests`, `McpPlayPolicyTests` and `McpSetupDocumentationTests` read source or spawn a process and never stand a transport up.

**The positive control is the bounded wait**, not the assertion that reads like one. Every assertion in either class is otherwise satisfied by a helper that leaks a server task per test, which is a slow suite rather than a failing one — so `ShutDown` gives the run `EndsWithin` to end on its own, and one that never ends is a `TimeoutException` and a red test. The `Assert.True(running.IsCompletedSuccessfully)` after it is a restatement rather than a second check: `WaitAsync` has already thrown for both of the ways it could be false, so deleting the line leaves the EOF-removal control red in exactly the same place. **And the bound is per call**, with 77 tests going through it: a shutdown that stops working everywhere costs about 38 minutes rather than hanging the job outright, which is the trade the number is making.

**Nothing in the teardown may escape it, and that is wider than the run.** Its complaints are raised only when the body itself was happy, so a harness fault can never be what a failing test reports — this repository has a written history of reading one as the other. That holds only because *every* step is caught and handed back, disposals included: `ShutDown` runs inside the caller's `finally`, so anything thrown in there replaces the body's own exception on the way out. Measured against the version that caught only the wait — with `client.DisposeAsync()` made to throw, a test whose body failed its own assertion reported the teardown's exception instead. With every step caught, the same mutation leaves the body's failure intact **and** turns a *passing* body red with the teardown's own message. Both directions were run; a step skipped after an earlier one failed would be the next hole, so each is attempted and the first failure is the one reported.

**What was measured, including what would not reproduce.** The race did not fire locally at all: 30 runs of the two classes on a machine at load ~165, four concurrent 10-run loops on top of that, and **32,000 in-process old-teardown cycles at 64-way concurrency** — 0 occurrences, and the old order stays green 10 runs out of 10. That is the honest reading of why it was invisible until CI hit it, and it is why the mutation that proves the fix is **not** the old order. **Break it by completing the reader out from under the live loop** — one line in `Drive`, `await toServer.Reader.CompleteAsync()` before the shutdown — and 77 of the 206 tests in those two classes go red, every one carrying the sighting's own inner stack: `ThrowInvalidOperationException_NoReadingAllowed` → `Pipe.AdvanceReader` → `PipeReaderStream.HandleReadResult` → `StreamReader.ReadLineAsyncInternal` → `StreamServerTransport.ReadMessagesAsync` → `ProcessMessagesCoreAsync` → `McpServerImpl.RunAsync`.


## The pixel diff, and why the goldens are the fragile part

`scripts/visual-regression.sh` screenshots seven proof pages and compares each against a committed
golden under `tests/visual-goldens/`, with a per-pixel tolerance. It runs in CI beside the verdict
harnesses. The PNG codec is ~150 lines against `node:zlib` rather than a dependency.

- **Goldens are Linux-rendered or they are worthless.** On Linux the script drives whatever Chrome
  is on PATH; everywhere else — including a Windows development machine — it drives a digest-pinned
  `selenium/standalone-chrome` in Docker. A golden generated from Windows Chrome fails every CI run
  for ever, which is a check that has to be deleted rather than fixed.
- **Linux-rendered is not enough: it has to be the *same* Chrome.** The digest-pinned Docker Chrome
  and `ubuntu-latest`'s own Google Chrome are both real Chrome on Linux and they still disagree —
  `shell-villain-light` by exactly **32,462 pixels**, across repeated CI runs, unchanged by forcing
  the colour scheme, while the pages beside it came back pixel-identical on the same runs. Four
  pages were deleted from the manifest over this before the cause was understood. **So goldens are
  generated by `.github/workflows/visual-goldens.yml`**, on the runner, and `--update-goldens` from
  a developer machine puts the gap straight back. The Docker path in the script is for *looking* at
  a page locally.
- **Regenerate deliberately, and never as a side effect.** A golden updated because something else
  changed is a regression signed off by nobody — which is why the workflow is `workflow_dispatch`
  only and uploads an artifact rather than committing. It also re-runs the ordinary comparison
  against what it just wrote: two captures seconds apart from one Chrome must be pixel-identical,
  and if they are not, the page is non-deterministic and committing it installs a flaky check.
- **A per-pixel count with a per-pixel threshold cannot see a uniform shift, at any threshold.**
  `channelThreshold` was 24, so a **uniform +20 per channel across every pixel** — a whole-page
  colour change, on an app with four palettes — reported `pixel-identical` and exited 0. Halving
  the threshold only moves the exploit to +11, so the answer is a second, independent measure:
  mean absolute channel difference over the whole image, failing if *either* is exceeded. Keep
  both, and keep the summary line naming which one fired.
- **The comparator is code and gets tested like code.** `diff.mjs` and the ~150-line `png.mjs`
  codec beneath it had no tests while being the only thing standing between four palettes and
  nobody looking. `tests/visual/*.test.mjs` synthesises images rather than committing fixtures.
- **`--virtual-time-budget` is not optional.** `.panel` carries `animation: rise var(--enter) both`,
  which starts at `opacity: 0`; a bare screenshot proofs a washed-out lie.
- **A proof page whose content depends on which test ran last cannot be pixel-checked.** Three pages
  were written by a `[Theory]` over both palettes into *fixed* filenames, so the palette was a coin
  toss between runs and the comparison failed against goldens generated from its own tree. The mode
  belongs in the filename, as the shell proofs have always had it. Nothing before this compared
  those pages byte for byte, which is why it survived.




## Driving the assembled app

`./scripts/e2e.sh` is the only thing here that runs the application. Everything else runs a *part*
of it: `dotnet test` drives the engine, bUnit renders components, the proof harnesses drive markup
and CSS over `file://`, and the pixel diff compares pictures of that markup. None of them boots
Blazor WebAssembly, follows a link, reloads a page, executes a line of `js/*.js` for real, is
subject to the Content-Security-Policy the deploy generates, or asks axe what a screen reader would
be told.

It publishes the site, serves it with the same `wrangler pages dev` version
`.github/workflows/deploy.yml` pins, and drives real Chrome.

```bash
./scripts/e2e.sh                     # publish, serve, drive, and drive every twin
./scripts/e2e.sh --driver dotnet     # the same, with the Playwright driver
./scripts/e2e.sh --real-only         # the ten-second loop while writing a check. NOT a full run
```

### Two drivers, and `e2e.sh` is neither of them

**What that script owns is everything around a drive** — publishing, parsing the wrangler version,
migrating and seeding a local D1, starting the server from a directory where wrangler finds
`functions/`, building each twin, and deciding what the verdicts mean. A driver takes a URL and prints three kinds of line. That split is why a
second driver cost a flag rather than a rewrite.

| `--driver` | What it is | Checks |
|---|---|---|
| `node` (default) | `scripts/e2e/drive.mjs` over `scripts/e2e/cdp.mjs`, a hand-rolled DevTools Protocol client against Node's own global `WebSocket` — the same trade `scripts/visual/png.mjs` makes against an image library | BOOT, BUILD, THEME, PALETTE, ROUTES |
| `dotnet` | `tests/e2e`, over `Microsoft.Playwright` and `Deque.AxeCore.Playwright` | the same five, plus **A11Y**, **ADMIN**, **RULES** and **ACCOUNT_SAVE** |

**The last three are stage two and need a second browser context apiece**, which the hand-rolled
client has no way to make — so a `--driver node` run reports five checks, skips four twins
(`html-lang-dropped` and the three seed ones), and says so on each line. That is a second asymmetry of exactly `A11Y`'s shape; see *Signed in, seeded from
outside the application* below.

**Neither is retired.** `PROGRESS.md` item 10 states the condition under which `scripts/e2e/` goes,
and removing a working harness before its replacement has a record is how an upgrade becomes a
regression. Both are run by `build.yml`.

**The Playwright driver does not bring a browser with it, and that decided the whole design.**
`Channel = "chrome"` launches the Google Chrome already on the machine — the one `cdp.mjs` finds
and the one `ubuntu-latest` ships. So there is no `playwright install`, nothing to cache, and no
*third* renderer beside the runner's Chrome and the digest-pinned `selenium/standalone-chrome` the
goldens need; a third one would mean regenerating every golden, and again on every upgrade. It also
keeps the no-npm rule: both packages are NuGet, restored by the `dotnet restore` that already runs.
**Measured on the runner: +4 seconds to Restore** (the `Microsoft.Playwright` package is 201.6 MB),
and nothing anywhere else.

**And Playwright for .NET has no snapshot comparison and no baseline management.**
`ToHaveScreenshotAsync` and `--update-snapshots` belong to `@playwright/test`, the JavaScript
runner; the .NET `PageAssertions` and `LocatorAssertions` surfaces have no such member. Nothing here
goes near the pixel path, which is unchanged.

**The question it answers is not "does the app work".** It is *is this reachable* — and that is a
question nothing else here asks. A feature shipped in this repository while nothing in the
application ever wrote to the store it read from: the manager's list, the banner's switcher and
both undo buffers all read an index, every unit and component test passed honestly, and every one
of them called the store directly. A test that reaches a feature by hand cannot notice that
nothing else reaches it. So four rules, and each of them is load-bearing:

- **No driver may reach past the browser.** No `localStorage.setItem` to arrange a state, no
  calling into a component, no planted storage pointer. Every state a check needs is arrived at by
  clicking what a person clicks — real `Input.dispatchMouseEvent` at real coordinates (which is
  what `ILocator.ClickAsync` does too), not `el.click()` from inside the page, which is the same
  mistake one layer out. The only reads that go round the front are the ones *asserting* on storage
  after the app wrote it. `E2eDriverTests` scans both drivers for the lazy spelling — and says in
  its own doc comment what a denylist cannot do, so nobody reads it as the guarantee.
- **Every check states its positive control first, and a failed control is reported as its own
  sentence.** `[CONTROL] the work did not happen: …` and `[OUTCOME] …` are different bug reports —
  "the palette never changed" and "the palette changed to the wrong colour" — and this repository
  has a history of reporting the first as the second.
- **Every check has a deliberately-broken twin, and a check with no twin fails the run.** The
  names the driver reported and the names the twins cover are compared, so a check cannot join the
  suite unproven.

  **The converse moved when the second driver arrived, and this is the one thing here that got
  weaker.** `e2e.sh` used to require the two sets to be *equal*, which also caught a twin naming a
  check nobody runs. It cannot any more: the two drivers do not run the same checks — `A11Y` needs
  axe-core — so equality would fail every `node` run over a twin the other driver covers perfectly
  well. What is kept in the script is the direction whose failure costs a missed regression. The
  orphan direction is now `E2eDriverTests.EveryCheckHasATwinAndEveryTwinHasACheck`, which reads
  *both* drivers and `defects.mjs` as source — strictly more than the script could ever see, since
  it runs one driver and cannot tell "no driver has this check" from "not this one" — and costs a
  second in `dotnet test` rather than a publish, a server and a browser.

- **A twin is driven with `--only <CHECK>`, and the real site never is.** Exactly one verdict is
  read out of a twin's run, and the other five were a server round trip and a browser boot apiece
  against a site broken in a way unrelated to them: with `A11Y` scanning four palettes at 45s a
  drive, six twins spent four and a half minutes re-measuring accessibility nothing looked at. It
  cannot make a run quietly smaller — the check names compared against the twin list come from the
  *unfiltered* real-site run, and a name matching nothing leaves the verdict absent, which is
  already read as a failed negative control rather than a pass.
- **A twin must *say* FAIL, never merely fail to say PASS.** Each check catches internally and
  prints a verdict either way, because a driver that died before reaching a check leaves the line
  out entirely — and "not PASS" would call that a working negative control. `e2e.sh` treats a
  missing verdict as a failure of the twin.
- **And it must fail for the reason it claims.** Every twin declares `expects: 'control'` or
  `expects: 'outcome'`, and `e2e.sh` requires the `FAIL` line to carry that kind. Any red line used
  to count, and the Playwright driver mints a third: `[HARNESS]`, which is the *driver* having a
  bug — an environment slot the shell forgot to seed throws out of `Account.cs` and prints
  `FAIL — [HARNESS] no sign-in token was seeded for RULES`, which said nothing about whether the
  check could see its defect and was read as a working negative control anyway. `Runner.cs` had
  said so in a comment since the day it was written; nothing enforced it. `HARNESS` is not
  declarable, so a twin that starts producing one turns the run red. **Every declared value was set
  by watching the twin fail**, and two are not what a reader would guess: `boot-app-never-mounts` is
  an `outcome` (both drivers' wait for the boot screen to go throws an ordinary failure) and
  `store-writes-nothing` is a `control` ("the application wrote this character down" *is* `BUILD`'s
  positive control). `--list` prints `name:CHECK:kind:expects`.

`scripts/e2e/defects.mjs` builds each twin by copying the published directory and substituting
**one documented line**, and **throws if that line does not occur exactly once** — the
`ProofPages.WithDefect` property, against a published site rather than a single file. Zero
occurrences means the twin has stopped reproducing anything and would pass for the wrong reason;
more than one means it is not the single change it documents.

### Signed in, seeded from outside the application

**Stage two of `PROGRESS.md` item 10, and the whole of it is one row.** `worker/tokens.js` stores
only the SHA-256 of a sign-in token — `worker/crypto.js`'s `hash`, plain WebCrypto, which Node
provides identically — the raw token travels by email, and `db.spendLoginToken` verifies by hash
lookup and burns the row in one statement. So `scripts/e2e/seed.mjs` does what an email does:

1. mint a token and hash it;
2. `INSERT` the row into the **local** D1;
3. hand the raw token to the driver, which drives `/signin?t=<token>`.

Everything after that is the application's own verify path — hash lookup, expiry test, single-use
burn, invitation check, session cookie — reached by a real navigation. **Nothing is bypassed and
nothing is faked but the row.** There is no code in the shipped bundle, no secret, no localhost
test and nothing to compile out, which is the whole difference between this and the authentication
seam the original plan called for. It does not weaken *nothing reaches past the browser* either:
that rule is about the browser, and a reader whose mail has arrived is an ordinary person.

**A raw token lives exactly as long as the run that minted it, and no longer.** `.e2e/seed.json`
is how one drive's environment reaches the next without re-seeding, and every token in it is a
bearer secret in the clear; it used to be deleted at the *start* of the next run, which left a file
full of credentials in a working tree for however long that was. The `EXIT` trap removes it now,
beside `stop_server`. The SQL beside it stays — it carries hashes and addresses and no raw token,
and it is what a reader debugging a failed sign-in needs. And `start_server`'s failure arms print
`redacted_tail` rather than `tail`: that log is a *request* log and stage two drives
`/signin?t=<raw token>`, so the tail pasted into a CI log, an issue or a chat window carried the
secret with it. `scripts/test-kill-tree.sh` drives the redactor, positive control first — an
ordinary line survives, and the fixture really did carry a token — because "no token in the output"
is satisfied perfectly by a redactor that printed nothing. **The drivers obey the same rule, and did
not until run 34040527190 printed three raw tokens into a public CI log**: Playwright's own
`ERR_CONNECTION_REFUSED at http://…/signin?t=<token>` went straight into a verdict, because only
the shell half of this harness had ever thought about it. Every `FAIL` message from either driver
now goes through the same `[?&]t=` substitution — and is flattened to one line, because `e2e.sh`
reads verdicts with `grep ^E2E CHECK` and Playwright appends a multi-line `Call log:`.

**The seed lives in the shell, not in a driver, and the reasons are arithmetic.** Writing a row
needs the pinned wrangler version, the database id out of `d1/wrangler.toml`, the `--persist-to`
directory and the migration state — four things `scripts/e2e.sh` already owns and a driver has no
business knowing. And there are two drivers, so a seed inside one is a seed the other cannot have.

**The server is given no `ADMIN_EMAIL`, and that is arranged rather than left out.**
`worker/invitations.js` treats that address as an administrator who is never in the table, so with
none bound, every answer about who may sign in and who may manage the list comes from the seeded
`invitations` rows — which is what makes `ADMIN`'s subject the invitation list rather than an
environment variable. But *not binding it* is not the same as *it not being bound*: wrangler runs
from the repository root and loads `$root/.dev.vars` if there is one, so on a developer's machine
that variable arrived anyway and the check quietly changed subject. The server is now started with
`--env-file .e2e/no-env.vars` — an empty file of the harness's own, so the files wrangler would
otherwise find are never asked for — and an explicit `--binding ADMIN_EMAIL=`, which
`bootstrapAdmin` maps to `null` because it requires an `@`. **Nothing reads, copies, moves or
deletes `.dev.vars`**; a run on a machine that has one prints a `::warning::` naming what would be
at stake if either flag ever stopped working. That the binding reaches `worker/invitations.js` at
all is measured, not assumed: binding it to a seeded address turns `ADMIN` red against the real
site.

**`scripts/apply-migrations.sh` is not what migrates it, and both `PROGRESS.md` and the stage-two
brief said it was.** Every one of that script's wrangler invocations carries `--remote`; its body is
`scripts/d1-migrations/gate.mjs` deciding whether it is safe to *deploy*. What the harness needs is
`wrangler d1 migrations apply --local --persist-to .e2e/d1` with no gate in front of it, because
nothing is deployed past that point. `e2e.sh` then asks the database for its tables rather than
believing the exit code — the same positive control `apply-migrations.sh` carries for the same
reason.

#### The three checks, and what each would be worth without its control

| Check | Control, before anything else | Outcome |
|---|---|---|
| `ADMIN` | the link signed somebody in; `/admin` rendered its own heading; the banner *still* names that account on that page | the page says there is nothing here for this account, **and** does not carry the list |
| `RULES` | `/rules` rendered; the banner **there** names the seeded account; a search box arrived; a search came back | ≥1 passage with a printed page citation and real prose — then, in a context that never signed in, a `401` from `/api/rulebook/` **read off the wire** and the page saying so |
| `ACCOUNT_SAVE` | signed in; a non-`GET` to `/api/characters` was answered under 400; **the shell says "Saved" while the whole name is in the field**; a second context began with storage that had never heard of the character; it signed in | the wizard in the second context holds the character |

**`ACCOUNT_SAVE`'s third control is new, and the run that argued for it reported the defect
correctly.** "A write was answered" says a write happened; it does not say *which* body was in it,
and a name typed one character at a time is a run of writes. So the check could open its second
browser having only established that the first letter reached the account — and when it found
`Account Bound H` there, "the app wrote a prefix and lost the rest" and "this harness looked too
early" were indistinguishable from the verdict. `CharacterSession.Saved` carries the version a
completed write started at, and the shell's word stands only while that version is the character's
current one, so waiting for "Saved" *with the whole name in the field* is waiting for the finished
name rather than for a prefix of it. It was the app — see `Autosave`, which now keeps one
write open at a time — but the check had no way to say so.

**`ADMIN` is the one that is easiest to get vacuous and the brief said so in advance.** "Not
allowed" is satisfied by a page that failed to load, by a page that refused because nobody was
signed in, and by a page that never asked the server anything. All three are excluded by name.

**And an assertion has to run before it can be a check.** `ACCOUNT_SAVE` asserted *the second
context is the same account* before it navigated to the wizard, so
`second-context-is-another-account` — which signs that context in as a different invited account —
went red on the identity and never reached the character at all: deleting the navigation, the wait
and "the wizard holds it" changed neither the real run nor the twin. The character assertion is the
outcome now and the identity one is gone, because against the real site it could not fail — both
slots are seeded for the same account at the same address, so it was true by construction. The leak
it named is caught more loudly without it: a server that served the character to whoever asked would
turn that twin **green**, and a twin that cannot turn its own check red fails the run.

**A twin has to land inside the check it names, and `rules-token-expired` did not.** It expires the
seeded token, so `Account.SignIn` threw on the way in — a helper `ADMIN` and `ACCOUNT_SAVE` share,
reached before `RULES` had opened `/rules` at all. Measured: replacing everything after that call
with `return "ok"` left the twin red, which means the results-panel scoping, the citation regex, the
`401` off the wire and the refusal sentence had **no negative control**. `RULES` now spends its link
silently — `Account.Spend`, which reports rather than throws — and judges it on its own second
control, the banner on `/rules`. Truncating the check now turns the twin green and the run red.

**What is still untwinned, said plainly.** No twin lands on the rulebook's *content* assertions,
and none can be built the two ways this harness has: the corpus is baked into `worker/corpus.js` and
the gate is `worker/index.js`'s "somebody is signed in", so **nothing in the published bundle a site
twin copies decides any of it**, and **no seeded row produces a live session that the rulebook prefix
then refuses** — `currentUser` reads the session and nothing re-checks the invitation list, which
`worker/invitations.js` says in as many words. The honest third shape would be a twin that
substitutes a documented line in `worker/` and serves it from a root of its own; it is not built.

**Two things in these were found by running them rather than by reading them**, which is the whole
argument of the discipline:

- `RULES` first counted `.chosen li .cost` across the page. `/rules` draws *two* lists — the results
  and "What is here", the table of contents, whose rows carry a page range in the same slot — so
  before anything was typed there were already ten matches: the wait returned instantly and the
  citation assertion read `pp.5–8` off the contents. It is scoped to the results panel by its own
  heading now.
- The `ACCOUNT_SAVE` twin went red for the wrong reason. Every profile shared one `saver` account,
  so the twin signed in to a world the real run had already built a character in, typed into a
  field that was not empty, and failed on "the name field never held what was typed" instead of on
  the identity it exists to test. Each profile has a world of its own now.

#### Two kinds of twin, because a site defect cannot break a server-side rule

`scripts/e2e/defects.mjs` is still **the one place a negative control is declared**, and it now
declares two kinds. `--list` prints `name:CHECK:kind`.

- **`site`** — the six original ones: the published output copied, with **one documented line**
  substituted, served from a directory of its own. Throws if that line does not match exactly one
  line.
- **`seed`** — the three new ones: the *same* site, with a different set of seeded rows. What
  `ADMIN` and `RULES` measure is a rule `worker/invitations.js` and `worker/auth.js` enforce, and
  nothing in `index.html`, `js/*.js` or `css/*.css` can break one — the browser half that touches
  them at all is compiled into a WebAssembly payload with no line to substitute. So the twin
  changes the row the sign-in was seeded from: an account the list makes an administrator, a token
  that expired an hour ago, a second context signed in as somebody else.

**Both of the site twin's properties are kept for a seed twin**, against a row rather than a line:
`seed.mjs` throws if a defect names a slot or an account it does not mint, and throws again if a
twin's plan comes out identical to the real run's — compared on what the *application* can tell
apart (which account each slot signs in as, and whether its token is live), never on the token
strings, which differ by construction and would make the assertion pass for free.

**Neither of those had ever been watched to fire, and the module could not be imported to make
them.** `seed.mjs` ran its command line on import, so `import('scripts/e2e/seed.mjs')` printed a
usage line and exited 2 — it took whoever imported it with it, the same fault `defects.mjs`'s
`invokedDirectly` guard already fixed one file over. It has that guard now, `plan(now, defects)`
takes the defect list so a test can pass synthetic ones, and `tests/worker/e2e-seed.test.mjs`
drives both throws plus the one claim about *this server*: that the hash a row is seeded under is
`worker/crypto.js`'s. If those two ever disagreed, every seeded link would be a lookup that finds
nothing and the whole of stage two would fail as "the link did not sign anybody in", with nothing
in the output saying why. It lives in the accounts server's suite because `scripts/test-worker.sh`
is this repository's only Node test runner, and because half of what it asserts is about `worker/`.

**A seed twin costs one drive and no server.** All four worlds — the real run's and one per twin —
are minted in a single `wrangler d1 execute --file` before the first server starts, so the three
seed twins share one server and add three `--only` drives rather than three of each. That is what
kept stage two inside the job's budget; the figures are below.

#### What it costs, measured rather than projected

The baseline is on the runner. Every `Build` run on `main` since the Playwright driver merged —
three of them, `ae03aae`, `8f2add6` and `3c9be82` — took **992s, 1125s and 1127s**: 16m32s to
18m47s against a `timeout-minutes: 30` cap. **A projection from local timings once said 13–14
minutes and was wrong**, so what follows is measured piecewise and added, and it is an estimate
until a run on `main` says otherwise.

Measured on a developer's Mac, wrangler already cached, `PP_E2E_SITE_ALREADY_BUILT=1`:

| Added | Cost |
|---|---|
| `d1 migrations apply --local`, the table probe, planning the seed, and one `d1 execute --file` | **3.9s**, once per run |
| bundling `functions/` — the difference between a server started from the root and one started from `.e2e/` | **+0.6s** per server start, so about **+5s** across a `dotnet` run's eight |
| ADMIN, RULES and ACCOUNT_SAVE against the real site | **11.9s** (2.2 + 4.0 + 5.7) |
| the seed-twin phase: one server, three `--only` drives | **≈31s**, of which 22 is the `rules-token-expired` twin waiting out a sign-in that will never happen |

So **about +52s on the Playwright step and about +8s on the node one** — roughly a minute on the
job, which puts `Build` at about **17m30s–19m50s**. That is inside the cap with ten minutes to
spare, so **no lever was pulled**; `PROGRESS.md` item 10 names them in order and the first is
dropping a driver from the workflow.

**Two decisions are what kept it to that**, and either one reversed roughly doubles the figure:

- **One server configuration, not two.** The six anonymous checks were driven against a
  functions-bundled server *before* anything else was written, and all six passed unchanged — so
  there is no second server for the stage-one checks. `/api/me` answering a real JSON `401` instead
  of falling through to `index.html` turns out to be indistinguishable from the app's point of view:
  both are "anonymous".
- **A seed twin has no site and no server of its own.** All four worlds are minted in the one
  seeding pass, so three negative controls cost one server start and three drives instead of three
  of each.

Locally, the whole thing is **4m36s** with `--driver dotnet` and **2m25s** with `--driver node`,
against a pre-built site.

### Proving `kill_tree`, and why a green run was never evidence about it

```bash
./scripts/test-kill-tree.sh                  # nine checks: the parses, three trees, the orphan
                                             # backstop, the log redactor, the server-state
                                             # reading and its bound, and what a dead wrangler's
                                             # report says; ~13s
./scripts/test-kill-tree.sh --skip-wrangler  # the synthetic ones only. NOT a full run
```

**The harness's own cleanup was the one fix in it that had never been watched to work.**
`stop_server` used `pkill -P` on Linux, which kills *direct* children only. A real
`wrangler pages dev` tree is **four processes deep** — measured on 2026-09-05, not assumed:

```
npm exec wrangler@4.127.0 pages dev …    <- $!, the pid stop_server is handed
 node                                    <- npx's own runner
  node                                   <- wrangler
   workerd                               <- holds 127.0.0.1:<port>
```

so `workerd` outlived its step still holding a port. The recursive `/proc/<pid>/stat` walk fixed
that, and the identical defect then reappeared on macOS, which has no `/proc` — `children_of`
returned nothing there too, silently, and `kill_tree` again killed only the wrapper.

**Both fixes were verified by outcome — "no leaked processes after a run" — and that check cannot
see this leak.** `next_free_port` steps over a held port and never asks for it again, so the run is
green either way; the runner emitted `something is still listening on port N after 30s` for every
twin of every run for weeks underneath a green tick. **Do not accept an outcome check as proof for
this again.**

So the script asks it the other way round, and three properties are load-bearing:

- **The tree is enumerated with `ps -eo pid,ppid`, never with `children_of`.** Using the function
  under test to collect the pids it is then asked about makes an empty answer look like a clean
  kill — which is the exact fault being hunted.
- **The positive control comes first and is reported as its own sentence.** "Everything is dead" is
  satisfied by a tree that never started and "the port is free" by a fixture that never bound it,
  so the run asserts ≥3 live processes at three depths *and* `port_in_use` saying busy, before it
  stops anything. `[CONTROL]` and `[OUTCOME]` are different bug reports here as everywhere else.
- **The fixture is a genuine multi-process tree.** `bash -c 'node … & node … & wait'` collapses:
  `bash -c` with a single command *execs* it, so you get one process where you meant three, and a
  `kill_tree` that only kills the pid it was handed passes against it. The synthetic case is a
  bash wrapper that backgrounds a node spawner which spawns a node listener; the second case is a
  real `wrangler pages dev`, because nothing synthetic reproduces four levels by accident.

**The `/proc` arm is driven on macOS too, and that needs a seam.** `children_of` reads
`$proc_root`, which is `/proc` in every real use and which the test points at a synthetic tree of
`stat` files built from the real process table — so the parse (the `(comm) ` longest-match trim,
the field offset, the ppid comparison) is exercised wherever this is run. That is a test of the
*parse* and not of the kill. Only CI runs the Linux arm against real processes, which is why the
script is a step in `build.yml` before the two e2e steps.

#### What the CI step answered: the holder is not in the tree, and it is not a missing level

**The paragraph that used to sit here listed what was still unknown. Run `33949251306` on
`ubuntu-latest` answered it, and the answer was none of the candidates it named:**

```
KILL-TREE CHECK WRANGLER_TREE: FAIL — [OUTCOME] every pid in the tree is gone and port 8880
is still listening, so something outside the tree of 10448 is holding it.
```

Every pid the check enumerated *died*, and the port stayed. So the fault was never a wrong field
offset, a naive `(comm)` parse, or a walk that stopped a level short — a correct walk cannot reach
a process that is not in the tree. **This is also the first thing that separated the platforms:
macOS had been green throughout, and that green was never evidence about Linux.**

**Where the holder comes from, measured on a real `wrangler pages dev` rather than reasoned about.**
Kill the `workerd` that holds the port and miniflare starts another one, which rebinds the same port
within a second. Its own log says so:

```
[wrangler:warn] The Workers runtime crashed unexpectedly and is being restarted (crash #1).
[wrangler:info] Updated and ready on http://127.0.0.1:8860
```

The mechanism is in the pinned miniflare: `Runtime.updateConfig` attaches an exit handler to the
`workerd` child that calls `onWorkerdCrashRestart`, which reassembles the config and spawns a
replacement. **`SIGKILL` is indistinguishable from a crash.** So the replacement is a pid that did
not exist when the tree was enumerated; kill its supervisor next and it is reparented onto init,
where a ppid of 1 puts it outside any walk of parent links. That is the whole of the `FAIL` line.

**Why the old depth-first kill lost this race on Linux and won it on macOS.** The real tree is
`npm exec` → node → node → {esbuild, esbuild, workerd, workerd}. The killed `workerd` is not the
last child, so the old code ran `children_of` for each remaining sibling *after* killing it. On
Linux that was a `/proc` scan forking `cat` ~150 times — hundreds of milliseconds of window, which
is exactly the interval the supervisor respawns in. On macOS it is one `pgrep` fork, so macOS won
the race and looked fixed. **The platform difference is the size of a window, not a missing arm.**

**The fix removes the window rather than shortening it, and adds a backstop for when that is not
enough.** Both, because they answer different questions:

- **`freeze_tree` first.** `SIGSTOP` the whole tree before killing any of it — root before its
  children are enumerated, each child before its own are read. A stopped supervisor cannot run the
  exit handler, so it cannot spawn a replacement, and the set being killed cannot grow. Only then
  `SIGKILL`, children before parents. No `SIGCONT` is ever sent: continuing a supervisor is
  precisely what must not happen.
- **`port_holders` after.** Parent links cannot find a process that was never in the tree, so
  `stop_server` asks the other question — *who has this socket open* — and kills that answer's tree
  too, before `release_port` starts waiting.

**`port_holders` reads `/proc` alone, and that constraint is the design.** `ss`, `lsof`, `pgrep`
and `python3` may all be missing — `port_in_use` already learned that about `ss`, which is not in
`mcr.microsoft.com/dotnet/sdk:10.0`. `/proc/net/tcp` cannot be missing on Linux. So: `/proc/net/tcp`
and `/proc/net/tcp6`, field 4 == `0A` (TCP_LISTEN) and field 2's port half matching the port in
hex, gives the socket inode in field 10; then one `ls -l` of each `/proc/<pid>/fd` finds the process
whose fd is `socket:[<inode>]`. **Field 2 and not field 3** — field 3 is the *remote* address, and a
connection *to* the listener carries the wanted port there, so the wrong column names the client
instead of the server. macOS has no `/proc` and takes an `lsof` arm; Windows keeps `netstat`.

**A process-group kill was considered and rejected on evidence, not taste.** The obvious reading of
the `FAIL` is "it escaped into a session of its own", and it is wrong twice: miniflare spawns
`workerd` with **no `detached`**, and neither `setsid` nor `process.setpgid` appears anywhere in
wrangler or miniflare on the `pages dev` path — so there is no second group to reach for. And
`( … ) &` in a non-interactive shell is not a group leader, so the tree's pgid is the *harness's
own*: `kill -- -$pgid` would take out `e2e.sh` itself.

**Two new checks hold this, and both were watched red.** `RESPAWNING_TREE` starts 203 processes of
which 100 are siblings a supervisor replaces on sight, and fails if anything was spawned *while*
the tree was being killed — remove the `SIGSTOP` from `freeze_tree` and 11 marked processes survive.
`ORPHANED_LISTENER` binds a port from a process reparented onto init, asserts as a control that it
is genuinely outside the tree, and fails if it outlives `stop_server` — disable the port-holder
backstop and it goes red. `PROC_NET_PARSE` drives the socket lookup against a synthetic
`/proc/net/tcp{,6}` and five decoys: reading field 3 instead of field 2 names the client decoy,
and matching a state other than `0A` names the connected-not-listening one.

**And the failure message names the holder now.** A still-held port used to print a number nobody
could act on; `::error::` and `::warning::` both carry the holder's pid and cmdline, so the next run
that fails this way is diagnostic on its own. **What would disprove the fix is a `WRANGLER_TREE` or
`ORPHANED_LISTENER: FAIL` naming a surviving pid — not another green run**, for the reason this
whole section opens with.

### What a failed run prints, and the one thing it used to leave out

A failing drive prints, in this order: every verdict the driver reached; an `::error::` naming how
many of how many checks failed; the names of any checks that **did not run**; one `::error::` line
saying whether the server was **still alive, already dead (with its exit status), or gone** when
the drive ended, and whether anything was still listening on its port; then — when it is not alive
— **the fatal error wrangler logged**, **the tail of wrangler's own debug log**, and the last forty
lines of that server's own log. Everything quoted out of a log goes through the same `[?&]t=`
substitution.

**That reading is bounded and cannot hang.** `capture_server_state` takes the exit status from
`wait`, and `wait` on a live child blocks until that child exits — so a *wrong* aliveness reading
does not make it say the wrong thing, it makes it say nothing, for ever. Measured before the bound
existed: inverting the test turned `./scripts/test-kill-tree.sh` into an eleven-minute hang, which
no CI log distinguishes from a broken runner. The reading is now a seam, `server_is_live`, and the
dead branch confirms it with a bounded poll before `wait` is allowed to run; when the two disagree
the state reads `UNREADABLE` and says so, because a harness that invents an exit status for a
running process is worse than one that admits it has none.

**The last two were missing, and CI run `34040527190` is what that cost.** `wrangler pages dev`
died four seconds into a nine-check drive. `A11Y` reported a 45-second wait on `/build`, the seven
checks after it each reported `net::ERR_CONNECTION_REFUSED` as if it were their own finding, and
the script printed the verdicts, called `stop_server`, and said nothing about the server — so the
run's whole evidence for a dead server was eight red lines that look identical to eight broken
checks.

**The ordering is the fix, not the printing.** `capture_server_state` runs *before* `stop_server`,
at the moment the drive returns and whatever the verdicts were, because afterwards there is
nothing left to ask: the tree is killed and the port released. `say_server_state` then prints it on
every failure arm — the 300-second timeout, a missing summary line, zero checks, a failed check,
and each of the four ways a twin can fail. The asymmetry it removes is that `start_server`'s two
arms have quoted a `redacted_tail` since the day they were written, because a server that never
comes up is obviously a server question; a server that comes up and then dies is the same question
and nothing asked it.

**Both halves are driven by `scripts/test-kill-tree.sh`'s `SERVER_STATE` case**, positive control
first: a live, bound fixture has to read as alive before a `SIGKILL`ed one is allowed to read as
`ALREADY DEAD … exit status 137`, and the two readings have to differ — which a constant cannot
do. Watched red four ways: reporting alive unconditionally, dropping the exit status, swapping
`redacted_tail` for `tail` (the fixture log carries a token), and printing no log at all.

**A fifth stage drives the reading being wrong**, which is the one thing the four above cannot
reach: a helper in a shell of its own — bash blocks in `wait` only for *its own* children, so a
subshell asking about somebody else's returns 127 at once and would prove nothing — replaces
`server_is_live` with one that says *gone* about a process that is running, and the case requires
an answer within seconds. Its control is that the helper really did have a live child to block on.
Watched red two ways: with the bound removed and with the bound made too long to be one.

**What the shape of that failure already rules out, so the next reader does not start from
nothing.** A `workerd` that has died under a live wrangler does **not** produce refused
connections: wrangler keeps the port and answers by hanging for ever (which is why readiness is
the served body — see below), and miniflare respawns a crashed `workerd` and rebinds the same port
within a second, measured here from run `33949251306`. Run 34040527190 got a document for
`/build`, then nothing, then a connection *refused* within 18ms on the next check and on every one
after it — so the listener was gone and stayed gone, which is the supervisor process itself
exiting or being killed rather than its worker crashing. The two candidates left are an OOM kill
and wrangler exiting on an error, and they are told apart by exactly the two things that were
missing: the wrapper's exit status (137 is a signal; the OOM killer leaves that one) and the last
lines of its log. Nothing about the check that was running is implicated, and that is measured
rather than assumed. The run on `main` minutes earlier passed the same `A11Y` check in 52,775ms
for the same 560 passing rule instances — about 3.3 seconds a scan — and the failing run reached
its *second* address 5.4 seconds in, which is that same rate. So the server was answering
normally right up to the navigation it died on: it had already served the BOOT check, two more
full page loads and an axe scan, and the node driver's own drive in the same job made about
fifteen navigations against an identically-configured server without trouble. There is no
degradation before the death to attribute it to.

#### The second death, and where wrangler puts the half it does not print

**There have been two in about seventy-five runs, and they are not the same shape.** Run
`34040527190` is the one above: the listener gone and staying gone, no error line, nothing on the
way out. Run `34120157313` — a merge commit on `main`, the Playwright step, twin
`html-lang-dropped` on port 8793 — is the other, and it is the first the reporting above was able
to describe. The server was **ALREADY DEAD with exit status 1**, and its last forty lines were:

```
[wrangler:info] GET /build 200 OK (11ms)

✘ [ERROR]

If you think this is a bug then please create an issue at …
Note that there is a newer version of Wrangler available (4.129.0). …
🪵  Logs were written to "/home/runner/.config/.wrangler/logs/wrangler-2026-09-07_12-19-49_785.log"
[wrangler:info] GET /css/theme.css 304 Not Modified (38ms)
```

An exit status of 1 is wrangler refusing to continue rather than a signal, so this one is not the
OOM candidate the paragraph above weighs. Two things were wrong with reading it. **The cause was
in the middle of a request log** rather than stated — `304 Not Modified` on both sides of it — and
**the error had no message at all.**

**Where the message went is in the pinned wrangler's own source, and it decides the fix.**
`src/core/handle-errors.ts` ends with

```js
logger.error(loggableException instanceof Error ? loggableException.message : loggableException);
if (loggableException instanceof Error) { logger.debug(loggableException.stack); }
```

— the message to stdout, **the stack to `logger.debug`**, which the default log level never
prints. And `Logger.doLog` appends **every** level to wrangler's debug *file* unconditionally while
filtering only the console, so that file has the stack whether or not anybody asked for a debug
level. Raising `WRANGLER_LOG` would buy a flooded stdout and nothing else. The file is what the
`🪵` line names, it lives under `~/.config/.wrangler/logs` on Linux and
`~/Library/Preferences/.wrangler/logs` on macOS, and run `33827524692` had already carried a
`kj::Exception` stack in one. Nothing collected any of them.

**So the harness owns that file now.** `start_server` sets `WRANGLER_LOG_PATH` per server to
`.e2e/logs/wrangler/<name>/`. Four things about it, each of which was checked rather than assumed:

- **It is the variable 4.127.0 honours.** In `src/utils/log-file.ts` it is an environment-variable
  factory whose default is `<global config dir>/logs`, and `getDebugFilepath` treats the value as a
  **directory** unless it ends in `.log`.
- **Measured against a real `wrangler pages dev` of the pinned version**, not read off the source
  alone: the log appeared in the named directory, the file count under
  `~/Library/Preferences/.wrangler/logs` was unchanged at 406 across the run, and the collected
  file held 21 `debug` entries against a 23-line stdout log.
- **Handed over as a path relative to the repository root**, like everything else wrangler is
  given here, for the reason the server section below states: wrangler is Node and `path.resolve`
  turns a Git Bash `/c/Users/…` into `C:\c\Users\…`. Proved to resolve against the server's own
  working directory by driving it from there.
- **Under `.e2e/` and never under `$HOME`**, so one `rm -rf .e2e` is still the whole of this
  harness's state and nothing accumulates in a directory where a reader would have to guess which
  run wrote which file. `scripts/test-kill-tree.sh`'s own wrangler gets the same treatment into its
  temp directory.

**And the report leads with the cause.** When the server is not alive, `say_server_state` prints
`wrangler_error_block` — twenty lines from the **last** `[ERROR]` line, matched with `grep -F` and
never a pattern containing the `✘`, which is three bytes whose meaning to `grep`'s `.` depends on
the locale — then the tail of the **newest** file in that server's debug directory, and only then
the forty-line request tail. When there is no `[ERROR]` line it says so, because that separates a
death from outside the process from wrangler refusing to continue; when there is no debug log it
says that too, because an uncollected stack read as an all-clear is this repository's oldest
failure shape.

**`WRANGLER_DEATH` in `scripts/test-kill-tree.sh` is the check, and its control is the whole of
it.** The fixture's `✘ [ERROR]` sits at line 5 of a 60-line log, so it is **outside** the last
forty — asserted, not assumed — and its presence in the report is therefore evidence that
something quoted it rather than the tail happening to contain it. A fixture with the error inside
the tail would pass against a function that does nothing new, which is exactly the tautology this
file keeps having to un-ship. Two more controls: the newer of the two planted debug logs is the one
`newest_debug_log` picks, and it really holds the planted stack; and the report printed the
ordinary tail, so the token-absence assertions are not satisfied by a report that said nothing.
Watched red seven ways — the error block unquoted, the debug log unquoted, its tail through `tail`
rather than `redacted_tail`, every file in the directory quoted instead of the newest, the block
printed after the tail, a missing debug log passed over in silence, and the fixture shortened so
its error falls inside the forty lines.

**The whole file is downloadable, because forty lines of a stack is not a stack.** `build.yml`
uploads `.e2e/logs/` with `actions/upload-artifact@v7` when either drive fails —
`include-hidden-files: true` and `if-no-files-found: error` for the reason the visual-regression
upload's own comment gives at length, and gated on the two steps' `outcome` rather than on
`failure()`, since a job that falls over at `dotnet test` never creates the directory. **The
artifact carries no raw sign-in token**: `e2e.sh`'s EXIT trap puts the directory through
`redact_in_place` first, which is the same substitution every failure tail uses, because an
artifact is a copy of the bytes those tails are careful not to print. `REDACTED_TAIL` holds that
too, with a fixture nested three deep — `wrangler/<server>/wrangler-….log` is — and a control that
the ordinary line survived, since "no token on disk" holds perfectly against a file that was
emptied.

**And a finding that came out of watching that run rather than reasoning about it, because it
corrects a claim this file and `redacted_tail` both made.** The premise for redacting a server log
has always been that stage two drives `/signin?t=<raw token>` and the request line therefore
carries the bearer secret. **`wrangler@4.127.0` does not log it**: `pages dev` records the
*pathname* and drops the query, so a sign-in navigation appears as `GET /signin 200 OK`, and a full
run of both drivers produced not one `?t=` across the fourteen server logs it writes. Run
34040527190's three leaked tokens came from the drivers' own verdict lines, which is a different
path and already fixed. The redactor stays, for three reasons — that is a property of a wrangler
version and not of this harness, the same path now also quotes wrangler's own debug log, and the
whole directory is uploaded — but the consequence is worth stating plainly: **a clean run is not
evidence that the redactor works.** It never was. What proves it is `REDACTED_TAIL`'s planted
token, because a corpus with no secret in it and a redactor that does nothing are
indistinguishable from the outside.

**What would name the cause next time is now collected rather than hoped for.** If a third death
has this shape, the run's own output carries the error block and the stack, and the artifact
carries the rest of the file.

**A check that did not run is not a failure, and the figures say so.** Both drivers stop when the
server stops answering rather than driving the rest into a refused connection each, and print
`E2E CHECK <NAME>: NOT RUN` for what is left. `E2E RAN n CHECKS, m PASSED` counts only the ones
that ran, so `n` shrinking is the shape of this failure; `e2e.sh` prints the `NOT RUN` names beside
its own count so the two figures cannot be added into a suite that is quietly smaller.

**A green run says it too, and that arm was the last silent one.** "All n checks passed" and "all n
checks passed and the server was already dead when the last verdict was printed" used to be the
same four words. It is a `::warning::` and not an `::error::` — every verdict printed stands, and a
check that could not reach the server does not pass, so failing the run on it would be a claim
about those verdicts that nothing has measured — but it arrives before the twins start failing to
reach servers of their own.

**And "stopped answering" includes the server that answers by hanging, which is the shape this
harness should expect first.** The paragraph above records it: a `workerd` that has died under a
live `wrangler pages dev` does not refuse connections, it holds the port and hangs. Against that
server the first version of this reporting was no better than what it replaced — the probe counted
only `ECONNREFUSED` and `ECONNRESET`, so every check spent its full 30-second navigation timeout
and reported the dead server as its own `[OUTCOME]`. **The node driver did not even get that far**:
`cdp.mjs` awaited `Page.navigate` before the load event, Chrome does not answer `Page.navigate`
until a navigation commits, and the load wait therefore rejected with nothing awaiting it yet — an
unhandled rejection, which Node answers by printing a stack and exiting. No verdict, no summary
line, nothing for `e2e.sh` to read. Measured against a socket that accepts and never answers, at
exactly 30 seconds.

So: **an answer counts whatever it says** — a 500, a redirect, `boot-app-never-mounts` serving `/`
perfectly while never mounting the app — and **no answer within ten seconds counts as stopped**.
The bound is three orders of magnitude above a local static server's measured cost for `GET /`, and
it is only ever asked after a check has already failed. Both drivers classify identically and print
the same sentence, the POSIX spelling of the socket error included, because `e2e.sh` reads them
with one `grep` and a reader compares two runs by eye.

**Every verdict goes through one redaction point in each driver, whole line.** The first version
wrapped `error.message` and interpolated the probe's own answer beside it raw, so a driver pointed
at a base URL carrying a token printed `&t=<redacted>` in the half somebody had remembered and the
token in full in the half they had not — one line, one verdict, the same shape as the leak the rule
was written for. Green verdicts and `NOT RUN` lines go through it too: "only the red half is
cleaned" is a rule that holds exactly until something passes.

### Three things about the server, each of which cost a debugging round

- **`wrangler pages dev` is run from the repository root, and the placement is the configuration.**
  Wrangler bundles a `functions/` directory found in the *working directory* — there is no flag for
  it — so where it is started from decides whether the accounts API exists at all. Stage one ran it
  from `.e2e/`, where there is none to find, and every `/api/` address fell through `_redirects` to
  `index.html`; stage two runs it from the root with `--d1 DB=<id> --persist-to .e2e/d1`, so
  `/api/me` answers a real JSON `401`. Everything handed to wrangler is a *relative* path for a
  second reason: wrangler is Node and cannot read a Git Bash path like `/c/Users/…`. The two that
  go through `--cwd d1` are relative to **that** — `../.e2e/…` — which was measured off wrangler's
  own output rather than assumed.
- **Readiness is the served body, not the status code and not wrangler's own log line.** Wrangler
  prints `Ready on http://…` and then, if its worker has died, answers requests by hanging for
  ever — which is exactly what happened here, twice, and read as a broken page. The probe requires
  the app's own boot screen to be in the answer.
- **MSYS pids and Windows pids are two namespaces and mixing them broke this twice.** `$!` is an
  MSYS pid and `taskkill` speaks Windows pids, so the first cleanup killed nothing and the script
  hung for ever after printing five green verdicts. Translating with `ps -W` was worse: it lists
  Windows-only processes with their *Windows* pid in the first column, so the match hit an
  unrelated process and the tree kill took out the driver — which then reported no verdict and
  looked exactly like a harness that could not see its own defect. The answer is neither: MSYS
  `kill` stops the wrapper, and the port's listener — a real Windows pid, read out of `netstat` —
  is cleared afterwards.

### What it does not cover, stated so nobody assumes otherwise

- **The real mail send.** A link is seeded straight into the local D1, so nothing here exercises
  the provider that would have carried it. That is `scripts/probe-mail.mjs`'s job and not a
  browser's.
- **The deployed site, its real Functions and the remote D1.** Everything below is local, and the
  remote database cannot be seeded by anybody and must not be.
- **A `_redirects` regression.** `wrangler pages dev` **rejects** this site's own
  `/* /index.html 200` rule as an infinite loop and ignores it, then serves `index.html` for
  unmatched paths by its own default — so deep links work locally for a different reason than they
  work in production, and a change to `_redirects` is invisible here. Measured, not assumed: the
  rule is named in wrangler's startup output as the one invalid rule it found.
- **Screen readers.** Still owed and no harness closes it. `aria-pressed` being the string
  `"true"` is not the same as having been listened to. **The A11Y check narrows this and does not
  close it**: axe finds a missing `lang`, a heading level skipped, a control with no accessible
  name — the things a machine can see. It cannot tell you whether the result is usable.

### What the A11Y check measured, and the readings it corrected

**axe's full default ruleset, nothing turned off, four palettes × four addresses: 536 passing rule
instances and, at the time, one violation, the same one in every palette, exempt under WCAG's own
text.** So `theme.css`'s contrast claims hold in the assembled app — which nothing had ever
checked. It writes its ratios into its comments as claims beside its own "re-measure if you change
it; do not eyeball", two of its tokens are `color-mix()` which only a browser resolves, and
`EveryScreenPairInUseHoldsItsContrastFloor` measures the *tokens*, not every rendered combination.

**The one finding there used to be, with its figures, because somebody had to decide about it.**
The wizard's Next control on `/build` before a tier is chosen — `StepButtons.razor` renders an
anchor with `aria-disabled="true"`, `app.css` used to paint it at `opacity: 0.45` — measured
2.23:1 Hero/Light, 3.28:1 Hero/Dark, 2.54:1 Villain/Light, 3.22:1 Villain/Dark, against 4.5:1.
WCAG 1.4.3 exempts text in an *inactive* component and this one is inactive; axe could not apply
that exemption itself because it looks for the `disabled` attribute, which an anchor cannot carry.
So the check used to drop those nodes **node by node rather than turning `color-contrast` off**,
count them, print the count in its verdict, and go red if the exemption ever matched nothing.
Whether a disabled Next should be legible anyway was a design decision, not a conformance one —
`PROGRESS.md` item 10 carried it, and the owner's ruling (2026-09-06) was to make it legible
rather than rely on the exemption.

**The exemption is gone, not narrowed, because the fix cleared the floor outright.**
`.btn.disabled` in `app.css` no longer fades the primary fill by `opacity`; it paints `--muted`
text on `--panel-sunk`, the same recessed, secondary-text combination `.btn.quiet` already uses on
`--panel`, one step further sunk so the two are not the same control at a glance. That pair
measures 6.01:1 Hero/Light, 7.26:1 Hero/Dark, 6.01:1 Villain/Light, 7.21:1 Villain/Dark — clear of
4.5:1 in every palette, asserted alongside every other pair `theme.css` promises in
`EveryScreenPairInUseHoldsItsContrastFloor`. With nothing left for `color-contrast` to find on
that element, keeping the per-node exemption would have meant dead code guarding against a
violation that can no longer occur — worse than no exemption, since a dead one hides a future
regression instead of merely permitting a known one. If a later palette change ever pushes that
pair back under 4.5:1, `Accessibility.cs` should simply go red on it like any other finding.

**Getting there produced two wrong readings and nearly discarded a right one.** The first said 32
violations; the second said 2, then 3, then 2, on different elements each run, reporting one
background as `#15151a`, `#17171c` and `#19191e`. Three shades of one colour is the tell: axe
measures contrast against the composited pixel, and `.panel` carries
`animation: rise var(--enter) both`, which starts at `opacity: 0`. **This is the same trap the
pixel diff has one layer over**, where a screenshot without `--virtual-time-budget` "proofs a
washed-out lie". `Scan` now awaits every animation's own `finished` promise.

**Then a correct reading was nearly deleted as a third artefact, and that is the sharper lesson.**
A draft had found the `.disabled` control at a stable 2.23:1; a later pass could not reproduce it
and concluded the whole thing was the animation race. Both were measuring honestly — A11Y ran
*last*, so `BUILD` had already saved a character, the wizard's Next was enabled, and the element
was not on the page. Under `--only A11Y`, which is how every twin drives it, it is. **Re-running a
measurement is not reproducing it**: for a driver, "the same state" includes which checks ran
before. A11Y is second in the list now, and its position is part of what it measures.

**Rules are selected by name, never by WCAG tag.** The obvious `RunOnly` on `wcag2a,wcag2aa` is
wrong here: `color-contrast` carries `wcag2aa`, but `heading-order` and `page-has-heading-one` carry
only `cat.semantics, best-practice` — axe never WCAG-tags a best-practice rule — so a tag filter
would run one of the three rules this work was scoped around and drop the other two.
