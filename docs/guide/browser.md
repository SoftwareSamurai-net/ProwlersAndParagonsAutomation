# The browser front end

Read before editing anything under `web/`: the four areas, the screens, the four presentation rules, and the four palettes on two axes.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## The browser front end

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


## Four areas, and the address decides which

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


## The front door

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


## The rules reference

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


## The sheet beside the editors

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


## A broken rule says so on the row that broke it

The engine answers continuously and the findings used to surface only at GM review, so a Trait over
the Trait Cap was silent on its own row until the end. `web/Services/SheetFindings.cs` routes a
`ValidationResult` to the row that owns each issue and `RowFinding.razor` draws it.

- **It reads `SubjectKind`, `SubjectId` and `OwnerId`, and computes nothing.** Those fields exist on
  `ValidationIssue` precisely so a consumer does not parse the message back into the facts it was
  built from — this is the first thing to actually use them that way. No arithmetic in the routing,
  no engine change, and the message is the engine's own string unaltered.
- **It is visible, never hover-only.** Every row grew an `aria-describedby` target during the
  hover-description work and that is the tempting wrong place to put this: WCAG is explicit that
  anything carried only by a tooltip is information some readers do not get. A description is
  optional and a broken rule is not.
- **Error and warning differ by a printed word *and* a border style**, not by colour alone. No new
  token was needed — `--danger` and `--heading` on `--panel` are already held to 4.5:1 in all four
  palettes by `EveryScreenPairInUseHoldsItsContrastFloor`. Adding one would have needed a fresh
  measurement, since the screen palette has no luminance test.
- **`HP_BUDGET_EXCEEDED` and the tier findings are deliberately unrouted.** They belong to no row,
  and the budget strip already exists for the first of them. A finding pinned to an arbitrary row
  would be worse than one shown where it belongs.


## Three controls could destroy twenty minutes; now seven act at once and can be undone

- **The plan's "three buttons on the tier page" no longer described anything.** The samples had
  moved to `/admin/portfolio` and the confirm logic into `CharacterManager`. The real family is
  five: "Start a new character", the current row's "Discard", the portfolio's two sample buttons,
  and the replay's "Open in the editor" — plus **"Import a character", which had no confirm at all
  *and* never called `NotifyChanged()`**, so an imported character was not autosaved until a later
  edit happened to touch it. That was a real defect and no audit found it.
- **The buffer is a JSON snapshot, never the live `CharacterSheet`.** The replay carries a recorded
  bug of exactly the shared-reference shape — handing over the instance let the first edit rewrite
  the original — and an undo buffer holding the same object would be that bug again.
- **The window closes by construction rather than by remembering to clear it.** `CanUndo` requires
  the session's `Version` to still equal what it was when the buffer was armed, so the first edit to
  the replacing character ends it and nothing has to know to call a `Clear`.
- **It is a fact about a screen, so it is not on `CharacterSheet`** — same rule as the budget
  breakdown's open/shut state, and for the same reason: that type is what gets exported, stored and
  restored.
- **No `Ctrl-Z`.** There is no general shortcut manager to extend, `palette.js` says in as many
  words to resist growing it, and taking `Ctrl-Z` off the finishing step's four text inputs is real
  untested risk for one shortcut. Announced through `MainLayout`'s existing `.save-status` live
  region rather than a second one.
- **Discarding a *different*, non-current saved character is the seventh, and it is undoable too.**
  This bullet used to say it was deliberately still a confirm because undoing it is a
  restore-into-store rather than the sheet buffer — which was the separate piece of work, not the
  decision. The reasoning that took the confirms off — *switching away from it already left it saved
  under its own id and this cannot touch that copy* — is true of `Open` and false of `Delete`, which
  is exactly what destroys that copy, so the row a reader is least likely to be weighing carefully
  had the least behind it. `web/Services/DiscardedCharacter.cs` is the missing half of the same
  mechanism rather than a second one: same `IsWorthKeeping` predicate, same JSON-snapshot rule, same
  `Version` match closing the window, same `.save-status` region. Three things are its own —
  **read with `ReadAsync`, never `OpenAsync`**, because the latter moves the current-character
  pointer on its way past, so remembering a row would switch the app to the character being
  discarded and the next autosave would write over the id just restored; **a refusal is reported in
  words**, because the account cap is a refusal that actually happens and an undo that silently did
  nothing leaves the reader believing their character is back; and **the identity key is captured
  and compared**, or an offer left standing across a sign-out would write an account's character
  into the anonymous slot. The two buffers can never be armed by one click — deleting a background
  row raises no change event, so it cannot move the session's `Version` — and the session's is
  checked first, because its window closes on the very next edit.


## An option or a Trait says what it is, on hover and on focus

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


## The browser front end's four presentation rules

All four are asserted by `WebPresentationTests`, which reads the source because none of them is visible to a compiler.

1. **No component names a colour.** Checked by hex, by keyword, *and* by `rgb()`/`hsl()`/`oklch()` function syntax — that last one is the loophole a hex grep leaves open. `transparent` is allowed; it is the absence of a colour, and `currentColor` is allowed for the same reason: it is a reference to whatever ink already applies, not a hue chosen here.

   **`light-dark(white, black)` passed all three detectors, and this is the rule the four palettes rest on.** It is CSS Color 5, so it was in none of the six function names the scan knew, and its arguments follow `(` and `,` rather than the `:` the keyword regex anchored on — so every detector missed it at once. Two changes: the keyword's position anchor is **gone** rather than widened, because a colour is equally a colour in `border: 1px solid black`, bounded by `(?<![\w-])`/`(?![\w-])` rather than `\b` since a plain word boundary treats the hyphen in `white-space` as one; and the function list gained `color`, `light-dark`, `color-contrast` and `device-cmyk`. **It is still a denylist and it will rot again when the spec grows another one.** An allowlist of the functions this codebase uses was tried and rejected: the razor scan runs over files whose `@code` blocks are full of unrelated calls, which is the same denylist problem one level up. Re-run the function census in the comment if it rots. `///` doc comments are stripped from the razor scan for the same reason `@* *@` comments always were — one `<summary>` in `ChooseTier.razor` is prose about a screenshot that mentions "dead white". Radii and durations are tokens for the same reason, and `prefers-reduced-motion` turns every animation off by setting three duration tokens to `0.01ms` — not `0`, which makes some engines skip `transitionend` entirely.

   **Nor a typeface.** `--font-display` (Oswald) and `--font-body` (Public Sans) are declared in `theme.css` and nothing else names a family; `font:` shorthand is checked as well as `font-family`, because the shorthand carries a family too and `font: inherit` is everywhere. **Both faces are self-hosted under `web/wwwroot/fonts/` and both are SIL OFL, so the licence text ships beside them** — this repository redistributes them on every deploy and every fork, which is a condition rather than a courtesy, and there is a test. **A missing font file fails silently**: the stacks name system fallbacks on purpose, so a renamed file degrades the whole app to them with every other test green — which is why one test reads the bytes on disk. They are `.ttf` and would be ~40% smaller as `.woff2`; converting them is a one-line change per face.
2. **Nothing on screen names an internal type or a build command.** Asserted on the *prose*, which `VisibleText` derives by stripping `@* *@` comments, the `@code` block, every tag (and so every attribute) and every Razor expression — so `@PowerFormatter.StatLine(p)` is fine and the same characters in a paragraph are not. The rule is general: no compound PascalCase type declared in `engine/` or `sheets/` may appear. The reverse is asserted too — `Ch.6`, `Ch.9`, `Trait Cap` and `Hero Point` must still appear *in the prose*, since deleting the rulebook references would satisfy a naive reading of this rule and ruin the app. (Asserted against the raw file, that test passed while `Ch.6` survived only in a comment.)

   **And the rule is one step wider than "no jargon": copy answers what the reader came to do, and anything explaining *why the app is built this way* belongs in a `@* *@` comment.** Four places broke that and the owner found all four by reading the app — the sign-in page explaining that it will not say whether an address has an account (noise to somebody signing in, and an advertisement of the defence), the replay page accounting for who would pay for the model in a sentence that had also stopped being true, a sample character vouched for by "there is a test that says so", and "nothing was pre-computed". `NoPageExplainsItselfToADeveloper` is a denylist and cannot be anything else — no pattern separates a sentence about a character from a sentence about the program — so it grows when somebody reads the app. `NoPagePointsAtAFileInThisRepository` is the structural half: any `.md`/`.json`/`.cs`/`.razor`/`.css` path in visible prose fails, whatever it is called. That one would have caught the worst instance on its own, which sent a reader wanting the live version to `docs/MCP-SETUP.md`.

   **The validator's messages are the other half of this surface**, and `web/`'s tests cannot see them — they are engine strings, printed verbatim on the GM review step and in both exports. `ValidationMessageTests` provokes them from real sheets and holds them to the same rule: no file name, no internal flag, no bare id where the rulebook has a name, no `flaw(s)`, and every message a sentence.
3. **One component owns each repeated class.** `Panel`, `Field`, `SheetSection`, `StatBlock`, `DerivedStatBlocks`, `OptionList`/`OptionRow`, `ChosenList`/`ChosenRow`, `Tooltip`. Writing `class="panel"` by hand anywhere else fails a test.

   **The other half of that pair — that the owner still writes its own class — was a substring match and held nothing.** `Assert.Contains($"\"{cssClass}", source)` is satisfied by `"panelish"`, so renaming `panel` in `Panel.razor` passed all fifteen cases: the exact "an owner that satisfies the test by writing nothing at all" failure the check exists to prevent, wearing the check's own clothes. It tokenizes now — the same `class="…"` splitter the sibling test twenty lines above uses, for the eight classes written as markup, and a small lexer over the named member for the seven built in C# (`ClassName` on five components, `OptionRow`'s `RowClass`, and `RuledLines`' `Lines`, which reaches the page through `RenderTreeBuilder.AddAttribute` and never appears as `class="…"` at all). **A whole-file literal scan is not the shortcut it looks like**: `OptionRow` writes `role="@(Navigable ? "option" : null)"`, so the literal `"option"` is in that file whatever `RowClass` says.

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


## Hero and Villain are one app with four palettes, on two independent axes

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


