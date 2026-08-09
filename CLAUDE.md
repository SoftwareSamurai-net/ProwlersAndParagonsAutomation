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

# Run the browser front end
dotnet run --project web/ProwlersAndParagons.Web.csproj

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
- **15 of the 20 Heroes rebuild to exactly 125 Hero Points** and are asserted as such. The other five are held at a recorded residual in `PrebuiltHeroes.BuildByHero`, none more than 2 HP out. Do not tune an ambiguous variant just to force one of those to zero — that is fitting the model to the answer. Fix the underlying gap instead.
- The package each Hero used is inferred, not printed. `ExactlyOnePackageLandsAnExactHeroOn125` re-runs that inference and asserts exactly one package fits each exact Hero, so the attribution cannot quietly become a convenient guess; for the other five it is the closest fit. Vector is why that test exists — his package was recorded as Superhero on a closest-fit basis while his Deflection was underpriced, and correcting the Power made Hero the only fit.

The root `.csproj` sits at the repository root, so it carries a `<Compile Remove="…" />` for every sibling project directory; without them the default `**/*.cs` glob pulls their sources into the CLI. Shared build settings — target framework, nullability, the analyzer contract — live in `Directory.Build.props`, so the five projects cannot drift into different strictness.

The project targets **.NET 10** (`global.json` pins SDK `10.0.100` with `latestMinor` rollForward). The 9.x SDK cannot build it; install with `winget install --id Microsoft.DotNet.SDK.10`.

## Static analysis

- .NET analyzers run at `AnalysisLevel=latest-recommended` with `EnforceCodeStyleInBuild`. `TreatWarningsAsErrors` is conditional on `ContinuousIntegrationBuild`, so local builds stay warning-only while CI is strict. **Keep the CI build at zero warnings.**
- Deliberate rule exceptions live in `.editorconfig` with an inline rationale — CA1305/CA1304 are off because all formatted output is human-facing terminal/sheet text, and CA1822 is a suggestion so `CostCalculator`/`DerivedStatsCalculator` keep a uniform instance API. Add rationale when adding an exception; do not add bare suppressions.
- Qodana (`qodana.yaml`, `jetbrains/qodana-cdnet:2026.2`) runs ReSharper inspections in `.github/workflows/qodana_code_quality.yml`. Two non-obvious constraints: the `dotnet.solution` key is required (without it Qodana finds no project and reports nothing), and the **Community** linter (`cdnet`) is deliberate — the release linter (`dotnet`) refuses to start without a Qodana Cloud `QODANA_TOKEN`.
- **Qodana is the only thing that sees a Razor deprecation.** A `.razor` file sets a component parameter by string key rather than by referencing the property, so the C# compiler never sees an `[Obsolete]` attribute on it: `Router.NotFound` was deprecated in .NET 10 and `dotnet build` reported zero warnings with warnings-as-errors on. Do not read a clean build as a clean bill of health for the components.
- **Qodana's counts on a pull request are not comparable to a scan of the whole tree.** It runs in PR mode — only changed files — so moving a file re-reports every finding in it as new. The Blazor slice moved `engine/` and `sheets/` into new projects and the count went from 144 to 249 without any of that code changing. Read the SARIF (`gh run download <run-id>`, then `qodana.sarif.json`) rather than the summary table before concluding anything moved.
- **A whole-tree Qodana scan reports zero, and the config that gets it there is in `.editorconfig`, not `qodana.yaml`.** `qodana.yaml`'s `exclude:` list accepts an inspection *name* and silently ignores it — the .NET linter is ReSharper, which takes severities from EditorConfig. Only the path exclusions in `qodana.yaml` do anything. Each `resharper_*_highlighting = none` there is scoped as tightly as the tool allows and says why; nothing is baselined and there is no severity floor. Qodana runs in PR mode, so its count only covers changed files — to see the real number, run it over the whole tree yourself:

  ```bash
  docker run --rm -v "$(pwd -W):/data/project/" -v "$PWD/results:/data/results/" jetbrains/qodana-cdnet:2026.2 --save-report
  ```

  The report is `results/qodana.sarif.json`; the summary counts by rule, never by file, so group it yourself.
- What is silenced and why, in one line each: `engine/Models/*.cs` exists to be deserialized by reflection (four inspections), the test transcription records document a rulebook page rather than being read, a `[Theory]` body asserting on its parameter is not a precondition guard, `JsonValue.Create(...)!` is load-bearing (removing it fails the warnings-as-errors build), and this codebase writes explicit constructors and named backing fields on purpose.
- `data/rules/*.json` is copied to the output directory by the csproj, so a published build works without the repo checked out.

## Architecture

Four layers with a strict no-upward-dependency rule, one project each:

```
data/rules/   →   engine/   →   sheets/   →   cli/
                                          ↘   web/
```

- **`data/rules/`** — JSON files only. No logic. All rules data extracted from the P&P Ultimate Edition PDF lives here.
- **`engine/`** — Pure C#, zero Spectre.Console references, no filesystem access. `CostCalculator` and `CharacterValidator` are the authority on HP costs and validity. No front end tallies points itself. The one file here that is not rules logic is `SampleCharacters.cs`, which builds two `CharacterSheet`s for preview — see below.
- **`sheets/`** — The `.txt` and `.json` exports, plus the stat-line and gear-line formatters, all returning strings. Shared by both front ends; writing a string somewhere is the host's job.
- **`cli/`** — Terminal presentation. Uses Spectre.Console for all rendering. Each wizard step implements `IWizardStep` and receives `CharacterSheet`, `RulesRepository`, `CostCalculator`, and `DerivedStatsCalculator` via `Execute()`.
- **`web/`** — Browser presentation. Blazor WebAssembly; see below.

**These are separate projects on purpose, and splitting them was the point of the Blazor slice.** `engine/` and `sheets/` used to be compiled into the root executable, which a WebAssembly project cannot reference without dragging Spectre.Console in with it. Now the arrows above hold at compile time: `web/` has no calculator of its own and no reference that could reach one. Do not merge them back.

### The browser front end

Blazor WebAssembly, so `CostCalculator` and `CharacterValidator` run in the browser *as the same compiled code* the CLI and the tests run. That is the whole reason it is not an HTTP API with a JavaScript SPA — never reimplement cost or validation in the browser, made true by construction rather than by discipline.

- `Program.cs` fetches every name in `RulesRepository.DataFileNames` **before the first render** and hands them to an `InMemoryRulesSource`. The engine is synchronous by design; a half-loaded repository throws.
- **The rules are copied into `web/wwwroot/data/rules/` by the csproj, not committed there** (`wwwroot/data/` is gitignored). `Content Include` with `LinkBase` looks like it would do this and does not — the asset is registered against a content root the file is not under, so every request answers `200` with an empty body. Copy before static-asset discovery.
- `CharacterSession` (scoped) owns the `CharacterSheet` and forwards to the calculators. **Anything resembling arithmetic in that file is a bug.**
- `CharacterSession.TryCost` exists because the engine throws rather than guessing on an incomplete selection — a variable-cost Power with no variant. The editors never commit one, so this is only for the always-on budget bar.
- **`CharacterStore` decides what a stored character is by asking the engine, not by checking its shape.** A saved sheet is nested several levels deep, and `System.Text.Json` will put a null at any of them without the type system objecting — so the guard costs and validates the sheet once and rejects a payload the engine cannot answer for. The first version stripped nulls level by level and missed `"Pros":[null]`, which restored cleanly and then took the app down on the first frame, because the budget bar renders on every route. **Do not replace this with a list of shapes**: the list goes stale the first time somebody adds a field. `InvalidOperationException` is deliberately not caught there — that is a half-finished character, not a corrupt one.
- **Trimming is disabled on publish.** `RulesRepository` deserializes by reflection, so the trimmer can quietly remove model properties and leave the site running on empty rules. See `PROGRESS.md` item 4 before turning it back on.

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

**To read the rulebook itself, extract its text with PdfPig.** There is no `pdftoppm` and no Python on this machine, and the `Read` tool cannot open a PDF without the former — so a scratch console project referencing `PdfPig` is the way in. Group each page's words by rounded baseline and sort descending to recover lines; `page.Text` unbroken is fine for searching. **The page offset is not constant**: the front matter runs +3 (printed 5 = PDF 8) and Ch.8 runs −7 (printed 144 = PDF 137), so find the offset near the page you actually want rather than computing it once. Ch.8's twenty Heroes are PDF pp.130–149.

How to actually look at a printed sheet, since the browser pane cannot screenshot and headless Chrome cannot wait for Blazor to boot: **render `SheetView` through bUnit and write `.Markup` into a static page** against the real `theme.css` and `app.css`, then print that with `chrome --headless --print-to-pdf`. A throwaway `[Theory]` in the bUnit project taking the output directory from an environment variable does it in one `dotnet test` run — **no dev server**, which is the point: starting one raises an approval dialogue that blocks unattended work. (An earlier note here said to capture `document.querySelector('.sheet').outerHTML` from the running app. That works and needs a server; this does not.)

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

- **`tests/ProwlersAndParagonsAutomation.Tests`** — the rules engine, plus `WebPresentationTests`, which *reads the source* of `web/` because the disciplines below are statements about how it is written.
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

Three Edge details are easy to get wrong and were all bugs at one point: Danger Sense **replaces** Perception rather than adding to it, Lightning Reflexes is a **flat +6** with no rank, and Super Speed is missing from most summaries. All three are verified against Ch.2/Ch.5.

Halves always round **up** — the rulebook has a global rule for this (Ch.1, "Half").

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

- **Overkill/Weak reduce the rate by 1 HP per rank, floored at 0.5** — *not* a ×0.5 multiplier. Ch.2: "reduces a Power's base cost by 1 Hero Point per rank (or changes its base cost from 1 Hero Point per rank to 1 Hero Point per 2 ranks)." A previous version halved the rate, which mispriced every 2 and 3 HP/rank power. The "Brute Option" is the separate Ch.1 rule for applying Overkill to Might.
- **The minimum is per rank, not per power.** Ch.2: "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons." See `MinimumRankedCost`. Specialty is the sole 0 HP power.
- Variable-cost pros/cons (e.g. Charges, Area/Burst) store their variants in `CostModifierRange`; `SelectedProCon.VariantKey` picks the right value.
- **Generic pros/cons are always flat; a power's own pros/cons may change its rate.** `powers.json` carries `power_pros` / `power_cons` for the 106 entries the rulebook attaches to one named Power. Eleven are per-rank — Constructs' Devices is +2 HP *per rank* — so `CostCalculator.ResolveModifiers` returns flat and rate totals separately and `RankedParts` adds the rate part to the Power's own rate before multiplying. Resolution prefers a power's own entry over a generic one with the same id.
- Of those 106: 102 carry a PRO/CON marker inside a Ch.2 Power entry, three carry one in Ch.7's Toxins section (p.108, on Stun and Slay), and one — Deflection's *Physical and Energy* — has no marker because the Power's own text states it as prose. A whole-book sweep for the marker confirms there are no others.
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

Everything else an option states — "Powers that inflict physical or energy damage", "that can be activated and deactivated at will" — is an `applicability_caveat`: shown to the player, never enforced. Enforcing it would mean ~7 booleans × 141 Powers of fresh guesswork. Ch.2 calls the list "not intended to cover every possible option" and puts it under GM approval, so a caveat is the honest model. **A caveat must never become a filter** — there is a test.

### The engine never touches the filesystem

`RulesRepository` reads through `IRulesSource`, not `File.ReadAllText`. Two implementations ship: `FileSystemRulesSource` (the CLI) and `InMemoryRulesSource` (any host that loads the data itself — a browser has no filesystem). `RulesRepository(string)` and `FromBasePath` still work exactly as before.

**Keep `IRulesSource` synchronous.** Making it async would push `await` through every lazy collection and from there into `CostCalculator` and `CharacterValidator`, turning a pure instantly-callable engine into an async one for no gain. A host that can only load asynchronously does that once at startup and hands over strings.

`RulesRepository.DataFileNames` lists every file a self-loading host must fetch — it cannot glob a directory that isn't there. **Add a new rules file to that list**, or a browser build silently runs on an incomplete rules set; a test enforces it.

### Sources, and the default rank

Six of them (Ch.2 p.15), in `sources.json`: Innate, Magic, Psychic, Super, Tech, Trained. Each names the Ability that stands in as a **rankless** Power's rank whenever Powers act on other Powers (Drain, Nullify, Dispel, Power Absorption, Power Mimicry). Innate/Super/Tech → Toughness; Magic/Psychic/**Trained** → Willpower. Trained is the one people guess wrong.

`DerivedStatsCalculator.GetRankAgainstPowers` answers that. It is **deliberately separate from `GetEffectiveRank`**, which still returns 0 for a rankless Power. The default rank substitutes only against other Powers — it is not the Power's rank, and folding it in would change Edge and Resolve away from the figures the published sheets print. There is a test; do not "simplify" the two into one.

A Source costs nothing and changes no rank, so a missing one **on a Power** is a warning, not an error. **On an Ability or Talent it is not reported at all**, and that difference is the rule rather than a gap: Ch.2 p.15 gives those two a default — Innate and Trained — so silence means "on its default". A Power has no default, which is why `POWER_WITHOUT_SOURCE` exists and no Trait equivalent does.

### Sheets group Powers by Source

`SourceGrouping` lives in `engine/`, not in a renderer, because the text sheet, the JSON export, the GM review and the browser's sheet all need the same answer. Published sheets print `TECH POWERS`, `MAGIC POWERS` and so on rather than one flat list, and all four surfaces do too.

- Groups follow `sources.json` order, so a sheet does not reshuffle as Powers are added.
- A Power with **no** Source still prints, under a plain `POWERS` heading at the end. Do not "tidy" this by filtering it out — leaving a Power off its own character sheet is worse than showing it unsourced, and the validator already warns. A **Trait** with no Source is different: it is not unsourced, it is on its default, so it prints nothing.
- **A group can hold no Powers at all** — a Trait bought through powered armour on a character with no Tech Power — so nothing that renders groups may gate on `SelectedPowers.Count`. Three places did. The Powers *tab* is the one deliberate exception: it edits Powers, and a heading with nothing under it says less than no heading.

### Sources on Abilities and Talents

Ch.2 p.15: **every** Ability, Talent and Power has a Source; Abilities are usually Innate and Talents usually Trained, "but these defaults aren't mandatory". `CharacterSheet.AbilitySources` / `TalentSources` record only the Traits that deviate, which is exactly what a sheet prints.

- **Abilities are not marked on the Abilities block, and that is correct.** A sheet records the Source as an `Abilities (Might, Toughness)` line *inside* the relevant Power group. Stronghold's `TECH POWERS` opens with his four armoured Abilities. The Abilities and Talents tables stay plain lists of ranks.
- **Do not derive the line from rank.** Ch.3 p.64 — the *random generation* chapter — says "Sources for your Powers and Abilities with a rank of 7d or greater", and reading that as a threshold is contradicted by the sheets in both directions: Alabama Slammer marks 6d Perception and Toughness; Citizen Soldier leaves 9d Willpower unmarked. It is an author's exception list, so it is stored. `ThePrintedTraitSourcesAreNotARankThreshold` names both counterexamples.
- **Three printed shapes, all from Ch.8.** Every Ability *and* every Talent on one Source collapses to `Abilities and Talents (All)` — both Heralds and Nano. A whole block alone reads `(All)`. Otherwise the Traits are named, in `abilities.json`/`talents.json` order, which is the order the sheets print them in.
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
- **Power `description` values are original text written from the rulebook entry, never rulebook prose.** Do not paste rulebook text in: only structured metadata plus this project's own explanations are redistributable here. Descriptions exist so a player can tell what they are choosing and what resists it, and they must agree with the mechanics beside them — `PowerDescriptionTests` fails a rankless power whose description claims per-rank scaling, which is how the original set went wrong on 44 of the 46 rankless powers.
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
- Generic Pro/Con applicability is **derived from the option**, never listed on the Power; unenforceable constraints are caveats, not filters
- A rankless Power's **default rank** comes from its Source and applies **only** against other Powers — it is not its effective rank
- Sheets group Powers under Source headings; an Ability's or Talent's Source prints as a line **inside** a Power group, never as a marking on the Abilities block, and it is **not** derivable from rank
- The Iconic tier's "200+" is explicitly a bare minimum, so it is GM discretion rather than missing data
- Hero and Villain are **one app with two palettes**, and the mode is not a field on `CharacterSheet`
- `engine/`, `sheets/`, `cli/` and `web/` are **separate projects**, so the dependency arrows hold at compile time rather than by convention

Each of these was wrong at some point and is now covered by a regression test naming the rule. If one appears to be violated, read `PROGRESS.md` and the test before changing the code.
