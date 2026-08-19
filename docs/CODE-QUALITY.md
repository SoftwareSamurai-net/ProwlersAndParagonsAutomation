# Code quality


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

