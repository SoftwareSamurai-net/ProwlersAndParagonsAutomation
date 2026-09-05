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
- **Qodana does not run on a pull request any more, and the local scan is now the only one before a merge.** `.github/workflows/qodana_code_quality.yml` runs it on `main` and weekly; the `pull_request` trigger was taken off because it was 214 of one session's 403 Actions minutes and all 10 GB of the cache — see [`hosting.md`](hosting.md) for the measurement. **What made that safe is that `CLAUDE.md`'s process already required `./scripts/qodana-scan.sh` locally, reading zero, before opening a pull request.** It is no longer a belt-and-braces step: skip it and the first thing that sees your branch is `main` after the merge.
- **And when it does run on a pull request — a `workflow_dispatch`, or a branch someone re-enables it on — its counts are not comparable to a scan of the whole tree.** It runs in PR mode there, only changed files, so moving a file re-reports every finding in it as new. The Blazor slice moved `engine/` and `sheets/` into new projects and the count went from 144 to 249 without any of that code changing. Read the SARIF (`gh run download <run-id>`, then `qodana.sarif.json`) rather than the summary table before concluding anything moved.
- **A whole-tree Qodana scan reports zero, and the config that gets it there is in `.editorconfig`, not `qodana.yaml`.** `qodana.yaml`'s `exclude:` list accepts an inspection *name* and silently ignores it — the .NET linter is ReSharper, which takes severities from EditorConfig. Only the path exclusions in `qodana.yaml` do anything. Each `resharper_*_highlighting = none` there is scoped as tightly as the tool allows and says why; nothing is baselined and there is no severity floor. A pull-request scan would only cover changed files, and there is no longer one — to see the real number, run it over the whole tree yourself:

  **This claim rots, and it has rotted twice.** It was 3 on `master` and 37 across the three reconciled audit slices before anybody measured; a previous slice found the same thing. Two of those 37 were real bugs no compiler sees — a doc comment stranded on the wrong method by an insertion, so one member carried two `<summary>` blocks and a `<paramref>` for a parameter it did not have. **Do not repeat the "reports zero" sentence without re-running the scan**, and read the count out of the log *and* the SARIF: a `grep` for the summary line prints nothing when the scan never ran, which looks identical to clean.

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

**And a seventh, for the same reason: `./scripts/test-kill-tree.sh`.** It reports three verdicts
about whether the harness can stop a server it started — see **Proving `kill_tree`** below. It is a
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

**`dotnet test` prints one `Passed!` per project and there are two.** Count the lines, and grep for
`Catastrophic` — see the note below on why a crashed process still prints `Passed! - Failed: 0`.

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

## Two test projects, and the difference between them

- **`tests/ProwlersAndParagonsAutomation.Tests`** — the rules engine, plus `WebPresentationTests`, which *reads the source* of `web/` because the disciplines below are statements about how it is written, and `HeadlessBuildTests`, which drives the `build` command end to end, and the three `Mcp*Tests`, which drive the MCP server over a pair of pipes. **The wizard itself still has no harness** — that is the CLI gap, and it is narrower than it was rather than closed.
- **`tests/ProwlersAndParagons.Web.Tests`** — bUnit. It *renders components* and asserts on the output, and it is the only project that may reference `web/`.

**The split is the point.** A source-reading test cannot see a bug in rendered output, and one duly shipped: Razor swallowed the space in `@name` + `<text> @(rank)d</text>` and the sheet printed **"Armor8d"**. It was fixed on the sheet and the same bug in a second spelling survived on the Powers tab for another whole slice, because no source file looks wrong. Anything about what a component *produces* belongs in the bUnit project; anything about how the source is *written* belongs in the other.

**Assert on `TextContent`, never on markup with the tags stripped out.** Stripping a tag leaves a separator where it was, so `<b>Armor</b><span>8d</span>` reads as "Armor 8d" to any test that does it — which is how the Powers tab kept the Armor8d bug through a test written to catch it. It cuts the other way too, and worse: a `DoesNotContain("Communications 0d")` over stripped markup is satisfied by printing exactly that with the two halves in different elements. An adversarial pass did it, visibly, with the suite green. The browser concatenates text nodes; so must the test.

**And the trap was inside the helper the render tests use to catch it.** `SheetRenderTests.Rendered` replaced every tag with a newline and one test then collapsed all whitespace, so `<b>Armor</b><span>8d</span>` read as "Armor 8d" — the exact string the assertions look for, produced by the exact bug they exist to find. It concatenates text nodes now. A test-side helper is as capable of being the bug as the component is; read the helper before trusting the assertion.

**A typographic rule lives in the stylesheet, where no rendering test can see it.** Emptying `.hp` puts Hero Point costs back in the same size, weight and ink as ranks and every bUnit test still passes, because the class is still on the element. Anything whose whole substance is CSS — the `.hp` treatment, print font sizes, the break rules — is asserted in `WebPresentationTests` against the parsed rule, not inferred from markup.

**And when you do measure in a browser instead, the units bite — the instrument is as capable of being wrong as the thing it measures.** A contrast probe read `getComputedStyle`, which returns a colour as `rgb(0–255)` *or* as `color(srgb 0–1)` depending on how it was written, and read both on one scale. Everything was therefore measured against black, and it reported **1.00 for a pair that is plainly legible**. It carries a **white-on-black positive control that must read 21**, and nothing it says is worth reading until that passes.

This is the sibling of the `getBoundingClientRect` border-box failure in `CLAUDE.md`'s list of checks that passed for the wrong reason — same genus, and only that one was written down. Both say: *a measurement is a claim about the instrument before it is a claim about the page.* The C# contrast resolver in `WebPresentationTests` never had this failure mode, because it works from parsed CSS source rather than from a computed style — so if a browser-side probe is ever built again, this is the trap, and it will not be caught by the existing tests.

bUnit pulls AngleSharp transitively at a version carrying a published advisory, so `web/`'s test project pins AngleSharp forward. Do not suppress NU1902 instead — see the comment in its csproj.


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
client has no way to make — so a `--driver node` run reports six checks, skips three twins, and says
so on each line. That is a second asymmetry of exactly `A11Y`'s shape; see *Signed in, seeded from
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

**The seed lives in the shell, not in a driver, and the reasons are arithmetic.** Writing a row
needs the pinned wrangler version, the database id out of `d1/wrangler.toml`, the `--persist-to`
directory and the migration state — four things `scripts/e2e.sh` already owns and a driver has no
business knowing. And there are two drivers, so a seed inside one is a seed the other cannot have.

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
| `ACCOUNT_SAVE` | signed in; a non-`GET` to `/api/characters` was answered under 400; a second context began with storage that had never heard of the character; it signed in | the wizard in the second context holds the character |

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
./scripts/test-kill-tree.sh                  # both trees; ~9s
./scripts/test-kill-tree.sh --skip-wrangler  # the synthetic one only. NOT a full run
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

**What is still unknown, stated because guessing it would be the same mistake again.** The `/proc`
walk is *correct* when driven against a synthetic table — measured, three levels deep, including a
command name containing `) ` — and `workerd` is an ordinary child of wrangler's node rather than a
detached one, read out of miniflare's own `spawn` options in the pinned version. So the fault the
runner has been reporting is not any of: a wrong field offset, a naive `(comm)` parse, `set -- $stat`
misbehaving under `set -u`, or `workerd` reparenting away from the tree. One real defect was found
and fixed on the way past — `children_of` returned the exit status of whichever `/proc` entry it
looked at last, which is fatal in `x="$(children_of …)"` under `set -e` and invisible in the
`for child in $(children_of …)` form the harness happens to use. **Whether that was the fault is
what the CI step answers**, and a `WRANGLER_TREE: FAIL` line naming the surviving pids is what
disproves the fix rather than another green run.

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
instances and one violation, the same one in every palette, exempt under WCAG's own text.** So
`theme.css`'s contrast claims hold in the assembled app — which nothing had ever checked. It writes
its ratios into its comments as claims beside its own "re-measure if you change it; do not eyeball",
two of its tokens are `color-mix()` which only a browser resolves, and
`EveryScreenPairInUseHoldsItsContrastFloor` measures the *tokens*, not every rendered combination.

**The one finding, with its figures, because somebody has to decide about it.** The wizard's Next
control on `/build` before a tier is chosen — `StepButtons.razor` renders an anchor with
`aria-disabled="true"`, `app.css` paints it at `opacity: 0.45` — measures 2.23:1 Hero/Light, 3.28:1
Hero/Dark, 2.54:1 Villain/Light, 3.22:1 Villain/Dark, against 4.5:1. WCAG 1.4.3 exempts text in an
*inactive* component and this one is inactive; axe cannot apply that exemption because it looks for
the `disabled` attribute, which an anchor cannot carry. So the check drops those nodes **node by
node rather than turning `color-contrast` off**, counts them, prints the count in its verdict, and
goes red if the exemption ever matches nothing. Whether a disabled Next should be legible anyway is
a design decision, not a conformance one — `PROGRESS.md` item 10 carries it.

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
