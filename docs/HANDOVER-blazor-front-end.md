# Handover: Blazor WebAssembly front end

A task-scoped brief so a fresh session can start without re-deriving anything.
**Delete this file when the slice is done** — [`PROGRESS.md`](../PROGRESS.md) stays the
permanent record.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. Everything below
assumes them.

---

## What this slice is

The wizard is CLI-only. This adds a browser front end at `softwaresamurai.net` that does the
same job, plus the Hero/Villain styling that PROGRESS.md item 7 has been waiting for.

**It is a vertical slice, not a rewrite.** The goal is one character created end to end in a
browser and exported, with the CLI still working unchanged. Breadth of UI polish matters less
than proving the architecture holds.

## The decision that is already made

**Blazor WebAssembly.** Not an HTTP API with a JavaScript SPA. The reason is not preference:

- `engine/` is pure C# with zero Spectre references and, since [#16], no filesystem coupling.
  It compiles to WASM and runs `CostCalculator` and `CharacterValidator` **as the same code**.
- That makes the one rule the architecture exists to protect — *never reimplement cost or
  validation in the browser* — true by construction rather than by discipline. With an API
  and a JS SPA, every DTO is a place the two can drift, and the tests only guard one side.
- It ships as a static site. No server to run, no per-request cost, trivial to host.

Do not revisit this without a concrete reason. If one appears, write it in `PROGRESS.md`.

## The seams that already exist — build against these

Do **not** add rules logic to the web layer. Everything needed is in `engine/`:

| Need | Use |
|---|---|
| Load the rules with no filesystem | `RulesRepository(new InMemoryRulesSource(files))` |
| Which files to fetch | `RulesRepository.DataFileNames` — 11 names |
| Cost anything | `CostCalculator` (`PowerCost`, `GearCost`, `TotalCost`) |
| Legality and warnings | `CharacterValidator.Validate` → `ValidationResult.Issues` |
| Edge / Health / Resolve | `DerivedStatsCalculator` |
| Which Pros/Cons a Power allows | `ProConApplicability.ProsFor` / `ConsFor` |
| Powers grouped for the sheet | `SourceGrouping.GroupPowers` |

### Startup, concretely

```csharp
var http  = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var files = new Dictionary<string, string>(StringComparer.Ordinal);

foreach (var name in RulesRepository.DataFileNames)
    files[name] = await http.GetStringAsync($"data/rules/{name}");

var rules = new RulesRepository(new InMemoryRulesSource(files));
```

Fetch all eleven **before** the first render — the engine is synchronous by design and a
half-loaded repository throws `FileNotFoundException` naming the missing file. Copy
`data/rules/*.json` into the web project's `wwwroot/data/rules/` at build time; do not
duplicate the files in the repository.

`RulesSourceTests.TheEngineRunsWithNoFilesystemAtAll` already proves this path works, so if
loading fails the fault is in the fetching, not the engine.

## Suggested shape

```
web/                     ← new, alongside cli/
  Program.cs             ← startup + rules fetch above
  wwwroot/
    data/rules/*.json    ← copied by the csproj, never committed here
    css/theme.css        ← the palettes below
  Pages/                 ← one page per creation step, mirroring the CLI's six
  Components/            ← HpBudgetBar, PowerBrowser, ProConPicker, CharacterSheet
  Services/
    CharacterSession.cs  ← holds the CharacterSheet, wraps the calculators
```

`CharacterSheet` is already a mutable state object the whole wizard mutates — it is a
scoped service, essentially unchanged.

Add `web/` to the solution so Qodana still sees the whole thing (`dotnet.solution` in
`qodana.yaml` is load-bearing — see CLAUDE.md).

## Theming: Heroes and Villains

Ch.9 is explicit that Villains are built exactly like Heroes, just without a Hero Point
budget, and prints no separate stat-block format. So this is **one app with two palettes**,
switched by a single mode flag — not two layouts, and nothing in `engine/` changes.

The published sheets are the reference: a bold banner strip, stat blocks in ruled boxes,
Powers under Source headings in small caps.

**The roles matter more than the hex values.** `--primary` is a *fill* colour — banner and
bar backgrounds, with `--on-primary` text sitting on it. It is **not** a text colour;
`--heading` is. Getting that wrong is exactly the trap described under "measured contrast"
below, so keep the two separate even though in the Hero theme they happen to coincide.

### Hero — "four-colour daylight"

Bright, optimistic, high-contrast. Think newsprint primaries.

| Token | Value | Use |
|---|---|---|
| `--surface` | `#F7F9FC` | Page background, near-white with a cool cast |
| `--panel` | `#FFFFFF` | Stat blocks, cards |
| `--ink` | `#0F1B2D` | Body text — navy-black, never pure black |
| `--heading` | `#1B4F9C` | Headings, active nav — **text** |
| `--primary` | `#1B4F9C` | Banner and bar **fill** |
| `--on-primary` | `#FFFFFF` | Text on `--primary` |
| `--accent` | `#E8B923` | Rank pips, budget bar fill, focus rings — **never text** |
| `--danger` | `#C0392B` | Over-budget, validation errors |
| `--rule` | `#C9D6E8` | Hairlines between sections |

### Villain — "midnight and blood"

Same geometry, inverted weight. Heavier, tighter, a little oppressive.

| Token | Value | Use |
|---|---|---|
| `--surface` | `#111114` | Page background, near-black |
| `--panel` | `#1C1C22` | Stat blocks, cards |
| `--ink` | `#EDE8E4` | Body text — bone, never pure white |
| `--heading` | `#B8873B` | Headings, active nav — tarnished brass, **text** |
| `--primary` | `#8B0F1D` | Banner and bar **fill** only |
| `--on-primary` | `#EDE8E4` | Text on `--primary` |
| `--accent` | `#B8873B` | Rank pips, bar fill, focus rings |
| `--danger` | `#E4572E` | Over-budget, validation errors |
| `--rule` | `#3A3A44` | Hairlines |

The Villain identity comes from the **red banner and rules** against near-black, with brass
for type. Do not reach for red as heading text to make it feel more villainous — see below.

### Measured contrast

Computed, not estimated. WCAG AA wants 4.5:1 for body text, 3:1 for large text and UI edges.

| Pairing | Ratio | Verdict |
|---|---|---|
| Hero `--ink` on `--surface` | 16.4 | ✅ |
| Hero `--heading` on `--surface` | 7.5 | ✅ |
| Hero `--on-primary` on `--primary` | 7.9 | ✅ |
| Hero `--danger` on `--surface` | 5.2 | ✅ |
| Hero `--accent` on `--surface` | **1.8** | ❌ as text — fills and pips only |
| Villain `--ink` on `--surface` | 15.5 | ✅ |
| Villain `--heading` on `--surface` | 5.9 | ✅ |
| Villain `--on-primary` on `--primary` | 7.9 | ✅ |
| Villain `--danger` on `--surface` | 5.1 | ✅ |
| Villain `--primary` on `--surface` | **2.0** | ❌ — this is why it is fill-only |

That last row is the trap, and this brief originally fell into it: `#8B0F1D` reads fine as a
banner behind bone text, and is almost unreadable *as* text on near-black. If you restyle,
re-measure — do not eyeball it.

### How to implement it

Define both as CSS custom properties on `:root[data-mode="hero"]` and
`:root[data-mode="villain"]`, and write **every** component against the tokens. No component
should ever name a colour directly — that is what makes the switch a one-line change and
stops the two themes drifting.

Notes worth honouring:

- **Do not just invert.** Dark mode with the same weights looks washed out. The Villain theme
  wants slightly heavier rules, tighter letter-spacing on headings, and a touch more panel
  contrast than a naive inversion gives.
- Only the palette differs. If a layout change seems necessary for one mode, that is a signal
  the layout is wrong for both.
- The mode is a **presentation** choice, not a rules one. Do not add a Hero/Villain flag to
  `CharacterSheet` — the only real difference is that a Villain has no Hero Point budget, so
  the mode can simply hide the budget bar and skip the `HP_BUDGET_EXCEEDED` display.

## Definition of done

1. A character can be created end to end in the browser and exported (`.txt` and `.json`,
   matching the CLI's output — `CharacterSheetExporter` writes files, so the web layer needs
   a download rather than a rewrite; extract the string-building if that is cleanest).
2. Both palettes work and switch cleanly.
3. The CLI still works, unchanged.
4. `engine/` has gained **no** presentation code and **no** duplicated rules logic.
5. CI is green at full strictness, with the web project in the solution.
6. `PROGRESS.md` updated, this file deleted.

## Ground rules

- Build and test at CI strictness before committing:
  `dotnet build --configuration Release -p:ContinuousIntegrationBuild=true` then
  `dotnet test --no-build --configuration Release -p:ContinuousIntegrationBuild=true`.
- Branch off `master`, conventional commits, open a PR, **check the PR's base is `master`**
  before merging — two early PRs were stacked and stranded.
- Do not weaken a rules test to make the UI easier. If the engine's shape is awkward for the
  browser, change the engine deliberately and say why in `PROGRESS.md`.
- Item 2 (Sources on Abilities) is still open and the printable sheet wants it. It is not a
  blocker for this slice — Powers already group correctly — but if you reach the sheet layout
  and it looks wrong without it, that is the reason.
