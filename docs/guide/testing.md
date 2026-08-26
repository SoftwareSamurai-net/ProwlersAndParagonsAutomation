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
- **Qodana's counts on a pull request are not comparable to a scan of the whole tree.** It runs in PR mode — only changed files — so moving a file re-reports every finding in it as new. The Blazor slice moved `engine/` and `sheets/` into new projects and the count went from 144 to 249 without any of that code changing. Read the SARIF (`gh run download <run-id>`, then `qodana.sarif.json`) rather than the summary table before concluding anything moved.
- **A whole-tree Qodana scan reports zero, and the config that gets it there is in `.editorconfig`, not `qodana.yaml`.** `qodana.yaml`'s `exclude:` list accepts an inspection *name* and silently ignores it — the .NET linter is ReSharper, which takes severities from EditorConfig. Only the path exclusions in `qodana.yaml` do anything. Each `resharper_*_highlighting = none` there is scoped as tightly as the tool allows and says why; nothing is baselined and there is no severity floor. Qodana runs in PR mode, so its count only covers changed files — to see the real number, run it over the whole tree yourself:

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

It also groups the findings by file, which the tool's own summary never does — that summary counts by rule.
- What is silenced and why, in one line each: `engine/Models/*.cs` exists to be deserialized by reflection (four inspections), the test transcription records document a rulebook page rather than being read, a `[Theory]` body asserting on its parameter is not a precondition guard, `JsonValue.Create(...)!` is load-bearing (removing it fails the warnings-as-errors build), and this codebase writes explicit constructors and named backing fields on purpose.
- `data/rules/*.json` is copied to the output directory by the csproj, so a published build works without the repo checked out.


## Two test projects, and the difference between them

- **`tests/ProwlersAndParagonsAutomation.Tests`** — the rules engine, plus `WebPresentationTests`, which *reads the source* of `web/` because the disciplines below are statements about how it is written, and `HeadlessBuildTests`, which drives the `build` command end to end, and the three `Mcp*Tests`, which drive the MCP server over a pair of pipes. **The wizard itself still has no harness** — that is the CLI gap, and it is narrower than it was rather than closed.
- **`tests/ProwlersAndParagons.Web.Tests`** — bUnit. It *renders components* and asserts on the output, and it is the only project that may reference `web/`.

**The split is the point.** A source-reading test cannot see a bug in rendered output, and one duly shipped: Razor swallowed the space in `@name` + `<text> @(rank)d</text>` and the sheet printed **"Armor8d"**. It was fixed on the sheet and the same bug in a second spelling survived on the Powers tab for another whole slice, because no source file looks wrong. Anything about what a component *produces* belongs in the bUnit project; anything about how the source is *written* belongs in the other.

**Assert on `TextContent`, never on markup with the tags stripped out.** Stripping a tag leaves a separator where it was, so `<b>Armor</b><span>8d</span>` reads as "Armor 8d" to any test that does it — which is how the Powers tab kept the Armor8d bug through a test written to catch it. It cuts the other way too, and worse: a `DoesNotContain("Communications 0d")` over stripped markup is satisfied by printing exactly that with the two halves in different elements. An adversarial pass did it, visibly, with the suite green. The browser concatenates text nodes; so must the test.

**And the trap was inside the helper the render tests use to catch it.** `SheetRenderTests.Rendered` replaced every tag with a newline and one test then collapsed all whitespace, so `<b>Armor</b><span>8d</span>` read as "Armor 8d" — the exact string the assertions look for, produced by the exact bug they exist to find. It concatenates text nodes now. A test-side helper is as capable of being the bug as the component is; read the helper before trusting the assertion.

**A typographic rule lives in the stylesheet, where no rendering test can see it.** Emptying `.hp` puts Hero Point costs back in the same size, weight and ink as ranks and every bUnit test still passes, because the class is still on the element. Anything whose whole substance is CSS — the `.hp` treatment, print font sizes, the break rules — is asserted in `WebPresentationTests` against the parsed rule, not inferred from markup.

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


