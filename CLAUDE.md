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
- Qodana's "redundant nullable warning suppression" hits on `JsonValue.Create(...)!` in `CharacterSheetRenderer` are false positives — the method returns `JsonValue?`, so removing `!` breaks the warnings-as-errors build. Leave them.
- The ~48 "auto-property accessor is never used" hits on `engine/Models/*.cs` are also expected: the setters exist for `System.Text.Json` to bind to, and nothing in this codebase assigns them.
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
- **Trimming is disabled on publish.** `RulesRepository` deserializes by reflection, so the trimmer can quietly remove model properties and leave the site running on empty rules. See `PROGRESS.md` item 5 before turning it back on.

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

`web/wwwroot/css/app.css` ends with the print stylesheet and it is load-bearing. **Judge it by the PDF, never by the screen** — computed styles cannot tell you whether a page break lands mid-entry.

How to actually look at one, since the browser pane cannot screenshot and headless Chrome cannot wait for Blazor to boot: capture `document.querySelector('.sheet').outerHTML` from the running app, render it in a static page against the real `theme.css` and `app.css`, and print that with `chrome --headless --print-to-pdf`. Rasterising the result needs a PDF library (there is no `pdftoppm` or Python on this machine); Docnet.Core plus ImageSharp 3.1.x in a scratch console project works. Pin ImageSharp below 4.0, which refuses to build without a licence key. Repeat the sheet three times in the harness to force breaks through every kind of block.

- **Both modes print light**, forced by a third block of token overrides at the bottom of `theme.css`. Restate *every* token the two palettes declare — a token left out keeps its mode's value through the cascade, which is how a Villain sheet came to print as a full-bleed near-black page. There is a test.
- **`break-inside: avoid` on `.sheet-section` is safe** even though a Powers group can exceed a page: the property is a request, and a box that fits on no page is broken rather than clipped. Do not "fix" it back to `auto` — that is what let a four-line Gear box straddle a page.
- **A `position: fixed` running footer does not work.** Chrome's print output renders it once, at the top of page two, over the content. The character's name repeats across pages via the **document title**, which the browser prints in its own header — that is why `Review.razor`'s `<PageTitle>` leads with the name. A test asserts `position: fixed` never returns to the print block.
- `h1 { display: none }` in print: the page heading is the tool's, not the sheet's.

### The browser front end's three presentation rules

All three are asserted by `WebPresentationTests`, which reads the source because none of them is visible to a compiler.

1. **No component names a colour.** Checked by hex, by keyword, *and* by `rgb()`/`hsl()`/`oklch()` function syntax — that last one is the loophole a hex grep leaves open. `transparent` is allowed; it is the absence of a colour. Radii and durations are tokens for the same reason, and `prefers-reduced-motion` turns every animation off by setting three duration tokens to `0.01ms` — not `0`, which makes some engines skip `transitionend` entirely.
2. **Nothing on screen names an internal type or a build command.** Asserted on visible markup only: `@* *@` comments and `@code` blocks are stripped first, because that is where engineering detail belongs. The reverse is asserted too — `Ch.6`, `Ch.9`, `Trait Cap` and `Hero Point` must still appear, since deleting the rulebook references would satisfy a naive reading of this rule and ruin the app.
3. **One component owns each repeated class.** `Panel`, `Field`, `SheetSection`, `StatBlock`, `DerivedStatBlocks`, `OptionList`/`OptionRow`, `ChosenList`/`ChosenRow`. Writing `class="panel"` by hand anywhere else fails a test. The budget bar's live fill width is the **only** inline style left, and it is the sole justification for `style-src 'unsafe-inline'` in the CSP.

Two Razor traps this surface has already hit:

- **Razor strips the leading whitespace inside a `<text>` block.** `<text> @(rank)d</text>` after a name printed `Armor8d` on every ranked Power for as long as the sheet existed. Use one expression.
- **Blazor will not mix named and unnamed child content.** A component with an `Actions` fragment cannot also take implicit `ChildContent`; `ChosenRow` therefore names both slots `Body` and `Actions`.

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

A Source costs nothing and changes no rank, so a missing one is a warning, not an error.

### Sheets group Powers by Source

`SourceGrouping` lives in `engine/`, not in a renderer, because the text sheet, the JSON export, the GM review and any future front end all need the same answer. Published sheets print `TECH POWERS`, `MAGIC POWERS` and so on rather than one flat list, and all three surfaces now do too.

- Groups follow `sources.json` order, so a sheet does not reshuffle as Powers are added.
- A Power with **no** Source still prints, under a plain `POWERS` heading at the end. Do not "tidy" this by filtering it out — leaving a Power off its own character sheet is worse than showing it unsourced, and the validator already warns.
- **Abilities are not grouped, and that is correct.** The rulebook gives a Source to any Ability of 7d or greater, but the sheets record it as an `Abilities (…)` entry *inside* a Power group — Stronghold's four armoured Abilities sit under `TECH POWERS` — never as a marking on the Abilities block. Abilities carry no Source in the engine yet; see `PROGRESS.md` item 2.

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
- Sheets group Powers under Source headings; **Abilities are not grouped**, matching the printed layout rather than the rules text
- The Iconic tier's "200+" is explicitly a bare minimum, so it is GM discretion rather than missing data
- Hero and Villain are **one app with two palettes**, and the mode is not a field on `CharacterSheet`
- `engine/`, `sheets/`, `cli/` and `web/` are **separate projects**, so the dependency arrows hold at compile time rather than by convention

Each of these was wrong at some point and is now covered by a regression test naming the rule. If one appears to be violated, read `PROGRESS.md` and the test before changing the code.
