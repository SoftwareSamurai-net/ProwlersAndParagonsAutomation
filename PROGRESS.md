# Progress

The single source of truth for what is done and what is left in this project.

**This file must be updated as part of any task that changes what is done or what remains.** Not afterwards, not in a follow-up — in the same change, so the record and the code land together. Previously this information lived in two places (the README roadmap and a gaps list in `CLAUDE.md`) and drifted out of step with reality; both now point here instead.

Keep it honest. A half-finished item stays open with a note on what is missing. "Done" means done and verified, not written.

---

## Current state

| | |
|---|---|
| Rulebook coverage | Chapters 1–2 (Basics, Characters) fully extracted and verified; Ch.6 custom gear and Ch.7 toxin Pros/Cons extracted |
| Powers | 141 entries, all mechanically verified against Ch.2 pp.21–48 |
| Power-specific Pros/Cons | 106 entries across 62 Powers, verified |
| Custom gear features | 12 entries, verified against Ch.6 p.92 |
| Other rules data | Tiers, abilities, talents, pros, cons, perks, flaws, sources — all verified, nothing flagged |
| Tests | 2620, run in CI at the same strictness as the build |
| Wizard | All six creation steps working, with back-navigation and `.txt` + `.json` export |
| Front ends | Two — the terminal wizard and a Blazor WebAssembly app, both on the same engine assembly |
| Hosting | **Live** at [prowlers-and-paragons-chargen.pages.dev](https://prowlers-and-paragons-chargen.pages.dev), deployed from `master` by GitHub Actions; `pp.softwaresamurai.net` not yet attached |
| Printed sheet | A4, ruled boxes, light palette in both modes, no mid-entry page breaks — see the completed item below |
| Known-wrong data | None outstanding |

The engine reproduces the printed Edge, Health and Resolve of all 20 pre-built Heroes in Chapter 8, and rebuilds **15 of the 20 to exactly their 125 Hero Point budget**. The remaining five are all within 2 HP, each for a recorded reason — see [Close the last five Heroes](#1-close-the-last-five-heroes).

---

## Remaining work

Roughly in the order that unblocks the most.

### 1. Close the last five Heroes

Fifteen of the twenty published Heroes now rebuild to exactly 125 Hero Points. The other five are held at a known residual in `PrebuiltHeroes.BuildByHero`, each with a reason:

| Hero | Residual | Why |
|---|---|---|
| Herald (Airmid) | +2 | Unresolved |
| Herald (Scathach) | +1 | Strike carries four Pros and Cons at once — most likely a variant reading |
| Shadow | +1 | Unexplained |
| T-Kay | −1 | `Limited: only for Telekinesis` does not say which grade |
| Vigilant | −1 | Its Jo Sticks are *Upgraded*, a custom gear feature worth +2 — which would take him to +1, not to zero |

Nothing left is more than 2 HP out, and the test asserting that bound has been tightened from 6 to 2 so it stays true.

**The "residuals pair up" lead is spent.** It was worth chasing and it paid twice — see the completed item below — but what closed Vector and Talon was reading the rulebook entry in each case, not the pattern. What is left is −1, −1, +1, +1, +2, and five values in a four-point range pair up by chance. Do not read more into it.

The two ambiguous grades (`Side Effect: collateral damage`, `Limited: only for Telekinesis`) remain guesses that could be revisited, but do not tune them just to force a zero — that is fitting the model to the answer.

One thing genuinely cannot be modelled as things stand: Eidolon's `Omni-Power (Mind Link)` applies Telepathy's Pro to a *mimicked* Power. Pros are stored per Power, so there is nowhere for it to live. Eidolon reconciles anyway, so it costs nothing today.

### 1b. Semantic pro/con constraints are still unenforced

The invented per-Power lists are gone — see the completed item below. What is left is the half of the constraints that cannot be checked against anything the rulebook prints per Power: "Powers that inflict physical or energy damage", "Powers that can be activated and deactivated at will", "attack Powers", "Powers that last or can be maintained". These are shown to the player as a caveat on the option and left to the GM, which is how Ch.2 frames the list.

Enforcing them would need roughly seven booleans on each of the 141 Powers — about a thousand fresh judgements against the book. That is worth doing only if something downstream actually needs it, and the obvious candidate is item 6 (assisted creation), where a model proposing a character benefits from the engine ruling out illegal combinations. Until then the caveat is honest and the guess is not.

### 2. Sources on Abilities and Talents

Powers now carry a Source and every sheet groups by it — see the completed item below. What is left is the other half of the rule.

Ch.2 p.15: *"These are the Sources for your Powers **and Abilities** with a rank of 7d or greater."* Abilities of 6d or less default to Innate and Talents to Trained, and the book is explicit those defaults are not mandatory. The engine gives a Source only to Powers, so a 10d Ability bought through powered armour has nowhere to say so.

**The published sheets do print this**, which is the argument for modelling it: an Ability's Source appears as an `Abilities (…)` entry *inside* a Power group, not as a marking on the Abilities block. Stronghold's `TECH POWERS` group opens with `Abilities (Agility, Might, Perception, Toughness) (Item: armor)`, and Alabama Slammer's `SUPER POWERS` with `Abilities (Perception, Toughness)`. So faithful rendering eventually needs it.

Nothing consumes it yet, though — the default-rank rule is about Powers — so adding the field now would be unused data. **There are two surfaces waiting for it now rather than one:** the `.txt` sheet and the browser's `SheetView`, which both group Powers by Source and both stop short of the `Abilities (…)` line. `PrebuiltHeroes.PowerSourcesByHero` is where the transcription would go.

### 3. Qodana baseline

Establish a committed baseline (`--baseline,qodana.sarif.json`) so only *new* problems fail CI. **This is now the blocker on Qodana being useful at all**, and the Blazor slice is what proved it.

Qodana runs in pull-request mode, so it inspects changed files and reports every finding in them as new. Moving a file therefore re-reports all of it. #17 moved `engine/` and `sheets/` into their own projects and the count went 144 → **249 "new problems"** without a line of that code changing. Two genuine findings in the new code were buried in it, and only came out by downloading the SARIF and grouping by file:

| Where the 249 actually were | Count |
|---|---|
| `engine/Models/*.cs` — setters that exist for `System.Text.Json` to bind, nothing else | ~130 |
| `tests/**` — transcription records with deliberately unread positional properties | ~40 |
| Notices: primary constructors, `field` keyword, get-only properties | ~60 |
| Genuinely actionable, all in new code | 6 |

A report where 243 of 249 entries are noise is one nobody reads, which is the state to fix. Two things worth doing together:

- **Commit the baseline** so only new findings surface.
- **Suppress the two families that are structurally expected** rather than baselining them line by line: unused accessors on the JSON model records, and unread positional properties on the test transcription records. Both are "this data exists to be deserialized or to document a source", not dead code, and a baseline would silently re-flag them the next time a model gains a field.

Retrieving the detail: `gh run download <run-id>`, unzip, read `qodana.sarif.json`. The summary comment gives counts by rule, never by file.

### 4. Remaining rulebook chapters

Chapters 3–9 are not extracted, apart from the two pieces pulled out because the engine needed them: Ch.6's custom gear features and Ch.7's three toxin Pros/Cons. Rough order of usefulness to the wizard: 6 (the rest of Equipment), 5 (Resolve, already partly used), 4 (Combat), 8 (Friends and Foes), then the rest.

### 5. Shrink the browser payload

Deployment is done — see the completed item below. What it left open is size: the first load is **27 MiB uncompressed**, about a third of that over the wire once Cloudflare applies Brotli, and cached hard afterwards because every framework asset is fingerprinted.

It is that large because **IL trimming is disabled**. `RulesRepository` deserializes with reflection-based `System.Text.Json`, so the trimmer is free to remove model properties it can only see through reflection, and the failure mode is not a build error but a silently empty rules set at runtime. `System.Private.Xml` alone is 3 MB of assembly nothing references.

Two ways to close it, neither free:

- **Root the engine assembly** for the trimmer (`TrimmerRootAssembly`). Smallest change, but it only preserves what is named, and a Power model gaining a property later would be trimmed away without a warning.
- **Source-generate the JSON contexts** (`JsonSerializerContext`) so deserialization stops being reflective at all. Better, and it would speed up startup, but it touches `RulesRepository` — which every test runs through — and the engine is deliberately the part of this project that does not churn.

**Either needs a machine that can run the trimmer to verify.** It cannot run locally: the ILLink task host crashes without the `wasm-tools` workload, on the stock Blazor template too. CI can, so the work is possible — but "it built" is not evidence here, because a trimmed-away model is a runtime silence. Whatever is done needs a check that actually loads the published site and reads a rule out of it.

Not urgent. The site works, and a returning visitor pays nothing.

### 6. Assisted character creation from a description

Give the tool a prompt like "a washed-up boxer who punches through time" and have it produce
a legal, costed character. This is worth doing *because* the rules engine is now trustworthy:
the model proposes, and `CostCalculator` and `CharacterValidator` decide what is legal, so it
cannot invent a character that does not add up. That ordering is the whole value — a model
inventing costs directly would be a random number generator with good prose.

Wants a machine-usable surface first: something that takes a structured character definition,
validates it, and returns errors the caller can act on. That is close to what
`CharacterSheetExporter`'s JSON already emits, read in reverse.

### 7. Choose and apply a licence

The project is intended for open-source release but is currently unlicensed, which legally means nobody may use it. Apache 2.0 is the working preference: its NOTICE requirement makes the "no rulebook content here, you must own the rulebook" statement travel with any fork. Whatever is chosen must be explicit that it covers this project's code and original text only — not the game system, which is © LakeSide Games. Worth contacting LakeSide before any public release.

---

## Completed work

Newest first. Link the PR so the reasoning stays findable.

### The sheet is fit to hand to a player — [#25](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/25)

Presentation only; `engine/` and `sheets/` are untouched. Three faults, done in the order that made each one smaller.

**The markup went behind components first.** 22 hand-written `class="panel"`, 21 `panel-head`, 19 `field`, 9 `sheet-section`, 8 `chosen`, 6 `stat-block`, 5 `options`, none of them shared. That is what made the print work expensive rather than the print work itself — ruled boxes and break rules had to reach every one of them. Eight components now: `Panel`, `Field`, `SheetSection`, `StatBlock`, `DerivedStatBlocks`, `OptionList`/`OptionRow`, `ChosenList`/`ChosenRow`, following the `RankRow`/`StepButtons` pattern that was already here.

It found a real display bug on the way: **the sheet printed "Armor8d"**. Razor strips the leading whitespace inside a `<text>` block, so a Power's name and its rank ran together on every ranked entry.

**The printed sheet is the substance of the slice.** The whole print stylesheet was three lines that hid the navigation, and every consequence of that followed: no paper size, no margins, entries cut in half by page boundaries, no boxes — the screen builds them from `box-shadow` and panel fills, none of which print — and the palette printed as-is, so a Villain sheet was a full-bleed near-black page.

- **The palette is forced light for both modes**, as a third block of token overrides in `theme.css`. No rule anywhere else needs to know it is printing. `--primary` is a fill, so on paper it becomes white and the banner takes its weight from a doubled rule instead of a wash of ink; the derived tokens are restated rather than left as colour-mixes, because a mix of black into white is grey and grey prints as a smear.
- **A4, 14mm margins, ruled boxes, break control** on `.power-entry`, `.stat-block`, table rows, list items and the section boxes. `break-inside: avoid` is a request, not a guarantee — a box too tall for any page is broken rather than clipped, which is exactly the fallback a long Powers group needs — so it is safe to ask for on every box, and it stops a four-line Gear box straddling a page for nothing.
- **A running footer was tried and does not work.** `position: fixed` is not repeated per page by Chrome's print output; it renders once, at the top of page two, over the content. What does carry the character's name across every page is the **document title**, which the browser prints in its own header, so the review page leads its title with the name. A colophon prints once at the end.

**The judging was done from the PDF, not the screen**, which is the only way this is checkable: the sheet markup was captured from the running app, rendered against the live stylesheets with headless Chrome's `--print-to-pdf`, and the pages rasterised and read back. A three-sheet document forced breaks through every kind of block. Computed styles cannot tell you whether a break lands mid-entry.

**The copy stopped talking to developers.** The GM review step no longer says its exports are "built by `CharacterSheetRenderer` in the shared sheets layer… byte-for-byte what `dotnet run` produces"; the derived-stats page no longer credits `DerivedStatsCalculator`; validation findings no longer print `NO_TIER_SELECTED` at the reader. All of it stays in the `@* *@` comments and `@code` blocks, where it belongs. The machine codes are still in the `.json` export, because they are genuinely useful in a bug report, and the page says so. Rulebook references were left alone on purpose — chapters, page numbers and rule names are what a player wants.

**32 new tests hold all three in place**, because none of them is visible to a compiler: no colour named outside `theme.css` (by hex, by keyword, and by `rgb()`/`hsl()` function syntax, which a hex grep misses); every palette token restated for print, or one mode's value survives through the cascade; nothing developer-facing in the visible markup, *and* the rulebook references still present, since deleting those would pass a naive version of the same test; one owner per repeated class; and the printed sheet's own rules, including no `position: fixed` in the print block — a regression guard, because that looks correct and lands on the wrong page.

Also fixed, all small and all on the same surface: the page heading opened with a focus ring drawn round it on every navigation (`FocusOnNavigate` is worth keeping for screen readers, but a heading is not a control and takes no ring); gear rows were set flush right, because spanning both columns also made them the last cell; radii and durations became tokens so `prefers-reduced-motion` can switch every animation off in one place; and Rider's `.idea/` directory is no longer tracked.

**What was not done: a rendering test.** Everything above is asserted against the source, which is honest for rules that *are* statements about the source, but it cannot catch the next "Armor8d" — a bug in what the components actually render. That needs bUnit and a test project that references `web/`, which is a bigger change than the rules it would check and pulls Blazor WebAssembly into a console test executable. Worth doing before the components grow much further.

### Two sample characters, for previewing a sheet — [#23](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/23)

`SampleCharacters.Hero()` and `.Villain()`, offered on the tier page. An empty sheet previews nothing — no Source headings, no Pros and Cons, no gear line, every derived stat zero — so judging a layout or a palette change meant building a character first. These fill every section a printed sheet has.

They are this project's own characters rather than the published Ch.8 Heroes, which stay in the test suite where they verify the engine against printed numbers.

**`SampleCharacterTests` holds them to the same rules a player's character is held to** — legal, inside budget, fully priceable, every section filled, at least one Source heading, both exports rendering. That was not ceremony: writing them produced three genuine errors on the first run, all of which the tests named.

- Ranks bought on Powers that have none. `invisibility` and `lightning_reflexes` are `max_rank: 0`, priced flat.
- Danger Sense pushed to 15d against a 12d cap, and Resistance to 16d. Both take a baseline **equal to** an Ability rather than half it, so purchased ranks stack on 9 and 8 rather than on 4.

The lesson worth keeping: check `rank_type` and `prerequisite` before giving a sample any ranks. The Hero lands at 105 of 125 HP and the Villain at 119.

The Villain deliberately leaves one Power without a Source, so the sheet prints the plain `POWERS` fallback heading and the review step shows a warning beside a legal character. Both are worth exercising in a preview, and a test would otherwise be the only thing that ever saw them.

### The first real deploy, and the trap it walked into — [#21](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/21)

The site is up and the engine runs from Cloudflare: all six tiers render from the fetched rules, the Superhero Package costs 50 of 125, Armor at 4 purchased ranks with Burnout settles on **2 HP** rather than 0 — the rulebook floor, live — and both exports build with no CSP violations.

**`--branch` is a label Cloudflare compares against the project's configured production branch, not a branch it reads.** New projects default to `main`; we deploy `master`. The mismatch does not fail anything: the upload succeeds, wrangler prints a `master.<project>.pages.dev` alias, the workflow goes green — and the production URL and any custom domain answer 404, because no production deployment exists. Nothing in the logs says so.

The setup instructions omitted this, which is how it was found. Fixed three ways: the README makes the production branch its own numbered step and explains what going wrong looks like, the deploy step carries the same warning where someone editing `--branch` would read it, and the workflow now **checks the production hostname after deploying** and fails with the remedy in the error. A deploy step that passes while the site is 404 is worse than one that fails.

### A README audit, and the Roadmap section deleted for the second time — [#20](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/20)

Checked every factual claim in the README against the tree and the data. The counts all held — 141 Powers, the 71/62/3/2/1/2 split by `cost_type`, the 63/46/27/5 by `rank_type`, and every file's entry count. Five things did not:

- **The minimum-cost floor was documented as the reading that was already known to be wrong.** The README said "no power costs less than 1 HP per rank, or 1 HP per 2 ranks once a rate-reducing con applies". The floor is 1 HP per 2 ranks *always* — `MinimumRankedCost` has been that since [#7](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/7), and reading it the other way is what silently voided every Con on a 1 HP/rank Power. The code was right and the document described the bug.
- **The Iconic tier was described as flagged `needs_review`.** Nothing in `data/rules/` is flagged, and two other paragraphs of the same README said so. The open end is GM discretion, reported as an `ICONIC_TIER_OPEN_BUDGET` notice.
- **Qodana's code-scanning upload was described as "attempted but non-fatal".** It is skipped outright while the repository is private, deliberately — the workflow comment explains that a swallowed failure left a red annotation on every run.
- The project tree omitted `scripts/`, `Directory.Build.props`, `.github/workflows/` and `qodana.yaml`, three of which the README already referenced by name elsewhere.
- Two sections still described CLI-only behaviour as though it were the whole story.

**The Roadmap section had regrown a numbered summary of `PROGRESS.md`, and it had drifted again** — still advertising the Blazor front end and the Hero/Villain printable sheet as future work after both had shipped. That is the second time; the section says so now and carries only the pointer. A short version is not cheaper than one list, it is a second list nobody remembers to update.

### The two real Qodana findings in the browser front end — [#19](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/19)

**`Router.NotFound` was deprecated in .NET 10, and the build could not see it.** A `.razor` file sets a component parameter by string key rather than by referencing the property, so the C# compiler never encounters the `[Obsolete]` attribute — `dotnet build` reported zero warnings with warnings-as-errors on. Qodana's Razor-aware inspection is currently the only thing in this repository that would catch the next one, which is worth knowing before treating a green build as a clean bill of health for the components. Replaced with `NotFoundPage` and a real `Pages/NotFoundPage.razor`, verified against a published build: an unknown path renders it and keeps the address rather than redirecting, which is what the `200`-not-`302` fallback in `_redirects` exists to allow. Also five redundant empty statements in the characteristics tab switch.

**The other 243 findings were not fixed, and that is item 3's problem rather than this one's.** See it for the breakdown; the short version is that Qodana inspects only changed files, so moving `engine/` and `sheets/` re-reported all of them.

### Hosted on Cloudflare Pages — [#18](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/18)

`pp.softwaresamurai.net`, deployed by GitHub Actions on every push to `master` that touches the app, the engine, the rules or the deploy itself. Direct upload rather than Cloudflare's Git integration, so there is one deploy path rather than two that can disagree. Setup and the token scoping are in the README.

**The Content-Security-Policy is generated, and that is the part worth remembering.** Blazor emits an inline `<script type="importmap">` into `index.html` naming the fingerprinted framework assets, so its contents change whenever those are rebuilt. Under `script-src 'self'` an inline script is blocked and the app never boots — and the easy way out, `'unsafe-inline'`, gives up most of what the policy is for. `scripts/write-cloudflare-headers.sh` hashes the inline scripts of the `index.html` that was actually published, and **exits non-zero if it finds none**, because a hard-coded hash would rot silently and take the site down on some later deploy. CI runs the same script, so a policy that would break the app fails on the pull request instead.

`style-src` still carries `'unsafe-inline'`: the budget bar's width is a live number and arrives as an inline style attribute. That is the one concession, and it is scoped to styles.

The policy was verified by serving the published output through a host that applies `_headers`, not by reading it: the app boots with no violations, deep links resolve through `_redirects`, the mode switch works through JS interop, and — the one genuinely uncertain case — the `blob:` URL the `.txt`/`.json` download builds is not blocked.

Two security choices behind the arrangement, both about blast radius rather than the site itself, which is static and holds nothing:

- **The workflow never triggers on `pull_request`.** That trigger runs a contributor's workflow changes with the base repository's secrets in scope, which would put the Cloudflare token one PR away from anyone.
- **A subdomain and a token scoped to Pages on one account.** A leaked token can redeploy this one site and nothing else, and a mistake in the Pages config cannot reach the apex domain.

What it left open is payload size — see item 5.

### A browser front end, on the same engine — [#17](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/17)

A character can now be created end to end in a browser and exported, with the terminal wizard unchanged. This also closes what was item 7, the printable sheet with Hero and Villain styling — it belongs to a front end, and now there is one to put it in.

**The engine and the sheet exports are their own projects now, and that was the substance of the change.** Both used to be compiled into the root executable. A Blazor WebAssembly project cannot reference that — it would drag in Spectre.Console — and referencing the CLI would have inverted the one dependency rule this architecture has. So `engine/` and `sheets/` became class libraries, and `data → engine → sheets → host` is a fact of the build rather than a convention. `web/` has no calculator of its own and no way to reach one it does not reference, which is the guarantee the whole slice existed to test.

`sheets/` is new and is the less obvious half. `CharacterSheetExporter` built the two export documents and wrote them to disk in one method; the browser needs the same two documents but hands them to a download. The string-building moved out and the file-writing stayed, so both hosts emit byte-identical exports because there is only one copy of the code. It is a separate project because neither host may own it and `engine/` must stay free of presentation.

**Nothing in `engine/` changed.** No presentation code, no duplicated rules logic, no Hero/Villain flag on `CharacterSheet` — the mode is a palette and the only mechanical difference, that a Villain has no Hero Point budget (Ch.9), is handled by hiding the bar and filtering `HP_BUDGET_EXCEEDED` from the display. The validator is never told which mode is active, so the JSON export still records every issue.

Some things the build found:

- **`Content Include="..\data\rules\*.json" LinkBase="wwwroot\data\rules"` looks right and silently is not.** The asset gets registered with a content root of `wwwroot/` while the file stays outside it, so every request answers `200` with an empty body and the engine reports the rulebook as malformed JSON. The csproj copies the files into `wwwroot/data/rules/` before static-asset discovery instead, and errors if it finds none — the failure it guards against is a site that loads and then cannot start.
- **Trimming is off on publish.** `RulesRepository` deserializes with reflection-based `System.Text.Json`, so the trimmer may remove model properties it can only see through reflection, and the failure is not a build error but a silently empty rules set at runtime. Rooting the engine assembly would keep the smaller payload, but the local toolchain cannot run the trimmer at all — the ILLink task host crashes without the `wasm-tools` workload, on the stock template too — so that is a change nobody could verify here. Recorded in item 5.
- **Pros and Cons on Abilities offer Cons only, and that is the rulebook's answer rather than a shortcut.** Each option's entry states what it may be applied to; of 23 Pros and 28 Cons, exactly two name Abilities and both are Cons. The picker filters on that field, so the list follows the data.
- **Blazor's `#blazor-error-ui` needs a `display: none` rule of its own.** Without one it shows from the first paint and reports a failure that never happened — which it duly did, twice, before being noticed.

The palettes live entirely in `web/wwwroot/css/theme.css` as CSS custom properties on `:root[data-mode="hero"]` and `[data-mode="villain"]`. No component names a colour: a grep for hex literals and colour keywords across `app.css` and every `.razor` file returns nothing, which is what keeps the switch a one-attribute change. The role split matters more than the values — `--primary` is a fill and `--heading` is text, and they are kept apart even in the Hero theme where they coincide, because Villain `--primary` measures 2.0:1 on its surface and would be unreadable as type.

### The rules loader is decoupled from the filesystem — [#16](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/16)

`RulesRepository` called `File.ReadAllText` itself. A browser has no filesystem, so a Blazor WebAssembly build could not have run the engine at all — and the alternative, reimplementing cost and validation in JavaScript, is the one thing the architecture exists to prevent. `IRulesSource` is the seam, with a file-backed implementation for the CLI and an in-memory one for hosts that load the data themselves.

**The interface is deliberately synchronous.** Making it async would push `await` through every lazy collection on the repository and from there into `CostCalculator` and `CharacterValidator`, turning a pure instantly-callable engine into an async one for nothing. A host that can only load asynchronously does so once at startup and hands over strings. Fetching is the host's problem; answering questions about the rules is the engine's.

Both existing entry points are untouched, so no call site moved. `RulesRepository.DataFileNames` is new and is the contract a self-loading host works from — it cannot glob a directory that isn't there — with a test asserting it matches what actually ships, since a rules file added and not listed would leave a browser build silently running on an incomplete set. A missing file now throws naming the file and where it looked, rather than surfacing later as a null somewhere unrelated.

The tests hold the seam open rather than merely covering it: one builds a repository with no disk access whatsoever and checks it costs a character identically to the disk-backed one. That is the Blazor path, proven before the front end exists.

### Sources, and Powers grouped by them on every sheet — [#15](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/15)

**Six Sources** (Ch.2 p.15): Innate, Magic, Psychic, Super, Tech, Trained. Each names the Ability that stands in as a rankless Power's rank whenever Powers act on other Powers — Drain, Nullify, Dispel, Power Absorption, Power Mimicry. The split is even but not intuitive: Innate, Super and Tech use Toughness; **Trained uses Willpower**, not Toughness.

`GetRankAgainstPowers` is deliberately separate from `GetEffectiveRank`, which still answers 0 for a rankless Power. The default rank stands in *only* against other Powers; it is not the Power's rank. Folding it into the effective rank would feed Edge and Resolve figures the published sheets contradict, and a test pins that distinction.

**It is a rendering change too, and that was the point.** The `.txt` sheet, the JSON export and the wizard's GM review all listed Powers flat; they now print Source headings the way a published sheet does. The JSON gains `source`, `source_heading` and `rank_against_powers` — that last one is otherwise invisible, and is where the rule shows: Tech-Source Communications exports `effective_rank: 0` alongside `rank_against_powers: 5`.

All twenty published sheets have their grouping transcribed and a test asserts the engine reproduces each one's printed headings — Psidearm carries three groups, Alabama Slammer two, Talon one. A Power with no Source still prints, under a plain heading at the end, rather than being dropped from its own sheet.

**A correction to what this file said before.** It recorded that Abilities are printed with no Source marking. That is true of the Abilities block, but incomplete: the sheets record an Ability's Source as an `Abilities (…)` entry inside a Power group — Stronghold's four armoured Abilities sit under `TECH POWERS`. See item 2, which is now scoped to exactly that.

### Pro/Con applicability is derived, not guessed — [#14](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/14)

Every Power carried hand-written `available_pros` / `available_cons` lists, and `ProConSelector` filtered on them absolutely — an option not on the list could not be selected at all. Those lists were this project's invention, and they were badly wrong: **68 of the 141 Powers offered no generic Pro whatsoever**, six Self-range Powers offered the Ranged Pro (which raises a Touch Power to Distant Range, and has nothing to raise on a Power that affects only you), and Self-range Teleportation offered the Touch Con for the same reason.

The rulebook never states applicability per Power. It states it inside each generic option — *"This Pro applies to Zone Powers"*, *"applies to Powers that only affect you"*, *"applies to Power Rank Powers and Baseline Rank Powers"*. So the 141 lists are deleted and the answer is derived from the option instead, by `ProConApplicability`.

**Ten entries constrain on something the rulebook prints for every Power** — its Range (Ch.2 p.19) or its Rank type. Those are enforced, each transcribed in a test naming the sentence it comes from. Every Power now offers Pros and Cons, and the counts move with Range as they should: 16 Pros on a Self Power, 18 on Zone, 19 on Touch and Ranged, and all 23 on the four Special-range Powers, where the book says the Power "works in some unique way discussed in the description" and so rules nothing out.

**The rest are deliberately not enforced.** See item 1b: they would need about a thousand fresh per-Power judgements, which is the same mistake in a new shape. They travel as a caveat displayed beside the option, and a test asserts a caveat never acts as a silent filter.

### Toxin Pros/Cons, custom gear, and two more Heroes closed — [#13](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/13)

Four pieces of work, two of which found real cost bugs.

**The three toxin Pros/Cons (Ch.7, p.108).** The original extraction was scoped to Chapter 2, so it missed Caustic (−2) and Non-Lethal Disease (+2) on Stun, and Lethal Disease (+6) on Slay. A sweep of the whole book for a PRO/CON Hero Point marker returns exactly these three outside Ch.2 and nothing else, so Pros and Cons are now complete. Note Non-Lethal Disease is Stun, not Slay, despite being printed under Lethal Disease.

**Custom gear features (Ch.6, p.92).** Twelve features at 1–2 HP each, ten flat and two graded, plus ordinary Pros and Cons applied to a piece of gear. Gear has its own floor: *"no piece of gear can cost less than 0 Hero Points"*, where a Power floors at 1. The Item Con is deliberately **not** credited — Ch.6 says every piece of gear has it as a statement of what gear *is*, and Item is absent from the list of Cons the same page calls common on gear; crediting it would make every 1 HP feature free. Free-text mundane gear stays the wizard's default, since nearly all gear is free. Gear is the first thing to spend HP outside `TotalCost`'s four existing categories.

**Super Senses is one Power, and it was being overcharged.** Ch.2 says so outright: *"Regardless of the options you select, Super Senses is always considered a single Power."* Each option is a separate entry here only because each carries its own price — a storage decision that was leaking into the arithmetic. Cons and the minimum-cost floor are both written per Power, so both apply once to the group. The floor is what bit: most options cost 1 HP flat, so an Item Con recorded against a gear-mounted sense was swallowed by that option's own floor and worth nothing. The handover proposed a different fix for the same symptom — apply the Con to every option — which reaches the same numbers but multiplies a Con the sheet wrote once; rejected on the rules rather than the result. **Talon** closes exactly, Shadow moves +2 → +1, and Psidearm and Vigilant have one-option groups and correctly do not move. Super Senses is the only such group: Transformation says *"Regardless of which Transformation Power you possess"*, plural, and there is a test so this is not over-generalised.

**Vector's −6, the largest gap left, was Deflection.** Its entry says you pick physical *or* energy, and *"you can double the cost of this Power and spend 2 Hero Points per rank to be able to deflect both."* His sheet reads `Deflection (Physical and Energy) 10d`, so the parenthesis was buying that for free — worth +10. The other 4 was his starting package: packages are never printed and are inferred as whichever lands the rebuild on 125, and his Superhero attribution was a closest fit made while Deflection was underpriced. With it corrected the Hero Package is the only one that fits. To keep that honest, a new test re-runs the inference for every exact Hero and asserts exactly one package works — it passes for all fifteen, so no Hero rests on a package chosen because it helped.

**15 of 20 Heroes now rebuild to exactly 125**, and nothing left is more than 2 HP out, so that test's bound tightened from 6 to 2. Writing the gear validator also surfaced an ordering bug: gear that cannot be priced threw instead of reporting the gap, which the validator already guards against for Power selections. Fixed with the same pattern.

### Pros and Cons on Abilities, and what gear actually costs — [#9](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/9)

**Abilities can carry Pros and Cons.** The rulebook's Brute Option is Overkill applied to Might, and Stronghold buys four Abilities through his powered armour, so his sheet reads `Abilities (Agility, Might, Perception, Toughness) (Item: armor)`. Nothing modelled that. `CharacterSheet.AbilityModifiers` and `CostCalculator.AbilityCost` now do, including the Brute Option's half price and a floor of zero. Stronghold's Item Con on four Abilities is worth exactly −4, which is exactly what he was over by: **13 of 20 Heroes now rebuild to exactly 125**.

**Gear turned out to be a wrong assumption, not a missing feature.** This file previously listed gear as an unpriced cost contributing to the Hero Point gap. Chapter 6 says mundane gear is free and explicitly not tracked, and a Gear Limit is a cap on the Trait rank you can apply while using it, not a budget. So the wizard's free-text gear step was right all along, and the residuals recorded against "has gear" were misattributed — they are now corrected. What genuinely remains is custom *features* on mundane gear at 1–6 HP each, which is a much smaller and better-defined gap.

### Hero Pros/Cons transcription, and the package double-charge — [#8](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/8)

Transcribed the Pros and Cons each published Hero sheet carries, which turned the Hero Point reconstruction from a rough check into an exact one for most of them.

**It found a second cost bug, and a bigger one than the last.** With the Pros and Cons in, seven Heroes came out over budget by exactly 4 — including Citizen Soldier, who has no Pros or Cons at all, so it could not have been the new data. 4 is exactly what the Superhero Package saves: it costs 50 Hero Points for 3d in six Abilities and twelve Talents, which is 54 bought separately. `TotalCost` had been adding the package price **on top of** every rank at full price, charging twice for the ranks the package grants. That made taking a package strictly worse than not taking one, which cannot be right for something the rulebook sells "at a small discount".

With packages paying for what they grant, **12 of the 20 Heroes rebuild to exactly 125** — seven on the Superhero Package, four on the Hero Package, one on the Civilian. The sheets never print which package was taken, but for those twelve exactly one package lands the total on the point, so the inference is safe.

That is the whole engine end to end against numbers the authors published: package-aware ability and talent costs, baseline ranks, every cost type, and both generic and Power-specific Pros and Cons.

### Power-specific Pros and Cons — [#7](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/7)

Extracted the 102 Pros and Cons the rulebook prints inside individual Power entries, across 61 Powers. Completeness was checked by counting every PRO/CON marker in the chapter against the entries parsed: 102 markers, 102 entries, none unaccounted for.

These are not simply more generic Pros. Every generic one is a flat Hero Point change, but 23 of these are not: ten change the Power's cost **per rank** (Constructs' *Devices* is +2 per rank, so on a 6-rank Constructs it is +12, not +2), five are graded, five scale with how many extra Sources the Power reaches, and Alternate Form's *Independent Forms* scales per power level. `PowerProConModel` and `CostCalculator` now separate flat modifiers from rate modifiers to handle that.

**This found a real bug in the previous change.** The minimum-cost floor had been read as "1 Hero Point per rank", but the rulebook's parenthesis — "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons" — is the ranked form of the same rule, so the floor is 1 per *2* ranks. Read the old way, the floor sat exactly at the undiscounted cost of any 1 HP/rank Power, which silently made every Con on such a Power worth nothing. It went unnoticed until a test applied a Con to Armor and got no discount.

The wizard now offers a Power's own Pros and Cons first, marked as belonging to that Power, and prompts for a variant or quantity where one is needed.

### Chapter 1–2 rules verification and test suite — [#5](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/5)

Started as a README correctness check and turned into a full verification pass.

**Data.** Every power entry had carried `cost_per_rank: 1` with `cost_type: "per_rank"`, which was wrong for 91 of 125 — the rulebook prices Powers six different ways. There was no `range` field at all, and no rank-type distinction, so 46 rankless Powers were modelled as ranked and priced from ranks they cannot have. Buff was missing entirely. `powers.json` was regenerated from Ch.2 as 141 entries with correct range, rank type, costs and baselines, and verification moved from a single `needs_review` boolean to per-field `verified_fields` plus a `source_ref` page reference. All 141 descriptions were rewritten: the originals were invented, and 44 of the 46 rankless Powers described per-rank scaling that does not exist.

**Rules fixes.** Danger Sense *replaces* Perception when computing Edge rather than adding to it. Super Speed sets Edge to rank × 3 and had been missing entirely. Determination is 5 HP per Resolve with no rank, not 1 Resolve per rank — a 5× error. Overkill and Weak reduce the per-rank rate by 1 HP, not to a flat 0.5, which had mispriced every 2 and 3 HP/rank Power. The minimum cost is per rank, not 1 HP per Power.

**Tests.** 2053 tests wired into CI. `CanonicalPowers.cs` holds the Range/Rank/Cost printed for all 141 Powers; `RulesDataTests` holds the tier, ability, talent, pro, con, perk and flaw values; `PrebuiltHeroes.cs` transcribes the 20 published Heroes and asserts their printed Edge, Health and Resolve. Three of those Heroes independently confirmed the Danger Sense, Super Speed and Lightning Reflexes fixes.

**Other.** Pros, cons, perks, flaws, abilities, talents and tiers were all checked and found already correct; their flags are cleared. Four wrong claims in the README were corrected. `.gitignore` now excludes `*.pdf` repository-wide and CI fails if a PDF is ever tracked.

### Earlier

Predates this file, reconstructed from git history:

- **Toolchain, Qodana and README** — [#4](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/4). Qodana Community linter wired into CI, analyzer warnings as errors in CI only, README restored.
- **Back-navigation** between wizard steps, and **JSON export** alongside the `.txt` sheet. Both done — do not re-implement.
- **Initial extraction** of chapters 1–2 into `data/rules/`, and the three-layer `data → engine → cli` architecture.

---

## How to maintain this

When you finish a piece of work:

1. Move it out of **Remaining** and into **Completed** with a short account of what changed and *why* — the reasoning is the part that is expensive to recover.
2. Update **Current state** if the headline numbers moved (test count, entry counts, coverage).
3. If the work revealed new gaps, add them to **Remaining** rather than leaving them in a commit message.
4. Link the PR.

If a task turns out to be partly blocked, say so explicitly in the item and name the blocker. An item that quietly narrows its own scope is worse than one that stays open.
