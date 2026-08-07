# Handover: make the sheet fit to hand to a player

A task-scoped brief so a fresh session can start without re-deriving anything.
**Delete this file when the slice is done** — [`PROGRESS.md`](../PROGRESS.md) stays the
permanent record.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. Everything
below assumes them.

---

## Where things stand

The browser front end is built, deployed and working:

- Live at **https://prowlers-and-paragons-chargen.pages.dev**, deployed from `master` by
  `.github/workflows/deploy.yml`. `pp.softwaresamurai.net` is not attached yet.
- All six creation steps work, both exports download, Hero and Villain palettes switch.
- Two sample characters load in one click from the tier page — use them for every check
  below, because they fill every section a sheet has. `SampleCharacters.Hero()` is
  *Ninth Precinct* (105/125 HP, one `TECH POWERS` group, customised gear);
  `.Villain()` is *The Quiet Hour* (119/125, two groups including the unsourced
  fallback, a Con on a ranked Power).
- 2588 tests, CI green at full strictness.

## Who this is for

**The owner's friends, at a table.** Not developers, not the author. That framing decides
most of the judgement calls below: if something on screen only makes sense to someone who
has read the source, it is wrong.

The rulebook is the opposite — chapter references, page numbers, rule names and the
reasoning behind a cost are exactly what a player wants, and the app is already good at
that. Keep all of it.

---

## 1. The printed sheet — the big one

This is the deliverable the whole tool exists to produce and it is the least finished
thing in it. Open the site, load a sample, GM review, **Print this sheet**, and look at
the PDF preview. It is bad.

The entire print stylesheet, at the bottom of `web/wwwroot/css/app.css`:

```css
@media print {
    .banner, .steps, .nav-buttons, .budget, .no-print { display: none !important; }
    .sheet { border: none; box-shadow: none; }
}
```

That hides the navigation and stops there. What is missing:

- **No `@page` rule** — no paper size, no margins.
- **No page-break control.** A Power entry or a Source group can split across a page
  boundary mid-entry. `break-inside: avoid` on `.power-entry`, `.stat-block` and each
  `.sheet-section` is the minimum.
- **The Villain palette prints.** `--surface` is `#111114`, so a Villain sheet is a
  full-bleed near-black page. Print must force the light palette regardless of
  `data-mode`, or the sheet is unusable and hostile to a printer.
- **No ruled boxes.** The published sheets put stat blocks in ruled boxes and Powers
  under small-caps Source headings. On screen the app does some of this with
  `box-shadow` and `--panel` fills, none of which survive printing. Print needs real
  borders.
- **Nothing about the page it lands on.** No character name in a running header, no
  sensible column widths at A4, no control over widows.

The layout is already the right shape — `SheetView.razor` renders banner, stat blocks,
grouped Powers, ruled sections. It is the print treatment that is absent. Judge the work
by the **PDF**, not the screen.

Worth deciding while you are here: the "Print this sheet" button currently calls
`window.print()` on the whole page. That is fine if the print stylesheet is good.

## 2. Write the UI for players, not for developers

Concrete offenders, all user-visible:

| Where | What it says |
|---|---|
| `web/Pages/Review.razor:47` | "The same two documents the terminal wizard writes" |
| `web/Pages/Review.razor:55-56` | "built by `CharacterSheetRenderer` in the shared sheets layer, so they are byte-for-byte what `dotnet run` produces" |
| `web/Pages/Review.razor:28` | prints the raw validation code (`NO_TIER_SELECTED`) under each issue |
| `web/Pages/Derived.razor:9` | "by `DerivedStatsCalculator` — the same code the terminal wizard and the tests run" |

Sweep the rest of `web/Pages` and `web/Components` for the same habit rather than fixing
only these four. `@* … *@` Razor comments and `@code` blocks are **not** user-visible and
should keep their engineering detail — that is where it belongs.

On the validation codes: they are genuinely useful when someone reports a problem, so do
not simply delete them. Lead with the human message and demote the code, or drop it from
the display and keep it in the JSON export, which already carries it.

## 3. Reusable components

The markup repeats itself, counted across `web/`:

| Pattern | Occurrences |
|---|---|
| `class="panel"` | 22 |
| `class="panel-head"` | 21 |
| `class="field"` | 19 |
| `class="sheet-section"` | 9 |
| `class="chosen"` | 8 |
| `class="stat-block"` | 6 |
| `class="options"` | 5 |

Nothing shares a component, so the print work above would otherwise be done twenty times.
Do this **first** — it makes 1 and 2 smaller, not larger.

Obvious extractions: a `Panel` with a header slot, a `SheetSection`, a `StatBlock`, a
`Field` wrapper for label-plus-input, and an `OptionList`/`OptionRow` for the pick lists
in `ProConPicker`, `PowersTab`, `PerksTab`, `FlawsTab` and `Gear`. `RankRow`,
`StepButtons` and `HpBudgetBar` are already components and are the pattern to follow.

**The one rule that must not break:** no component names a colour. Everything comes from
the tokens in `web/wwwroot/css/theme.css`, which is what keeps the Hero/Villain switch a
single attribute. There is a grep proving it holds today:

```bash
grep -rnE '#[0-9A-Fa-f]{6}\b|color:\s*(red|blue|white|black)' --include=*.razor web/ ; grep -nE '#[0-9A-Fa-f]{3,8}\b' web/wwwroot/css/app.css
```

Both must stay empty. Print styles will need light values — put them in `theme.css` as a
print-scoped token override, not as literals in a component.

---

## How to check your work

The browser pane in this environment was unreliable for screenshots. What worked:

```bash
dotnet publish web/ProwlersAndParagons.Web.csproj -c Release -o publish
./scripts/write-cloudflare-headers.sh publish/wwwroot
```

then serve `publish/wwwroot` with any static host and drive it. Reading computed styles
via `javascript_tool` proved more reliable than screenshots for verifying the palette —
for instance, confirming Villain `h1` computes to brass `rgb(184, 135, 59)` and not the
crimson fill. **For print you will need to actually look at a PDF**; computed styles will
not tell you whether a page break lands mid-entry.

Do not trust a passing build for anything in the components: Razor sets component
parameters by string key, so `[Obsolete]` is invisible to the compiler and `dotnet build`
reports zero warnings. Qodana is the only thing that catches those.

## Definition of done

1. A Hero and a Villain sheet both print to a PDF you would hand to someone, with ruled
   boxes, sane margins, no mid-entry page breaks, and no dark-mode ink dump.
2. Nothing on screen names an internal type or a build command.
3. The repeated markup above is behind components, and no component names a colour.
4. CI green at full strictness; `dotnet test` still 2588+.
5. `PROGRESS.md` item 0 moved to Completed, this file deleted.

## Ground rules

- Build and test at CI strictness before committing:
  `dotnet build --configuration Release -p:ContinuousIntegrationBuild=true` then
  `dotnet test --no-build --configuration Release -p:ContinuousIntegrationBuild=true`.
- Branch off `master`, conventional commits, **check the PR's base is `master`**.
- A few commits, one PR — components first, then copy, then print.
- Do not change `engine/` or `sheets/`. This slice is presentation only. If the sheet
  needs a piece of data the engine does not expose, that is worth saying out loud in
  `PROGRESS.md` rather than reaching across the layer.
