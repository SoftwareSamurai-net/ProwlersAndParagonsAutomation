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
- **Putting a character on screen is `CharacterSession.Open`. The silent one is named
  `RestoreBeforeFirstRender` and `Program.cs` is its only caller.** Assigning `Sheet` without
  ringing `Changed` is right exactly once — the boot, where there is nothing rendered to tell.
  Everywhere else it is a defect that **does not show up where you make it**: whoever swaps the
  character is a component handling a click, so Blazor re-renders *that* component either way, and
  the control you are looking at follows perfectly while every subscriber goes on drawing what was
  replaced. **This trap has now been sprung twice.** Import once had no `NotifyChanged()` at all,
  so an imported character was not autosaved until a later edit touched it — recorded further down
  this file. Then five call sites reached for the silent `Restore` and the banner's pill named the
  new character over the old one's sheet. The long name is the fix, and
  `NothingDrawnCallsTheSilentRestore` holds it: nothing under `Components`, `Pages` or `Layout` may
  call it, with `Program.cs` still doing so as the positive control.
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


## The chord is printed where somebody who has never pressed it will see it

`Ctrl`/`⌘`+`K` opened the command palette from the day it shipped, and the only place the chord
was written down was **inside the palette** — on the row of keys along its own foot, visible to
somebody who had already pressed it. That is the whole of what "a shortcut for whoever wrote it"
means, and the owner named it as a defect about today rather than a note about a future design.

The banner carries a `.palette-open` control now: the word **Search**, and the chord beside it in
two `.key` boxes. It is a field — see the reversal four bullets down — and the word is its
placeholder.

- **On every route, because the chord works on every route.** The step band and the budget strip
  are the builder's and are drawn there alone; this is not one of those. A button that appeared
  only inside the builder would say the key stops at its edge, which is worse than saying nothing.
- **The modifier is the reader's, not the developer's.** `palette.js` listens for `ctrlKey` *or*
  `metaKey` precisely because it is `Ctrl` on Windows and Linux and Command on a Mac, so
  `ppPalette.onAMac` answers one boolean about the platform and `Shortcuts.ReadModifier` decides
  the word. A hard-coded `Ctrl` is wrong for half the readers, and wrong in the way that costs
  the affordance: somebody who presses the key they were told about and gets nothing stops
  reaching for it.
- **`Cmd`, not the looped-square glyph.** That glyph is the Mac convention and it is in neither
  typeface this app names, so it would fall back to a system face — silently, on one platform,
  which is exactly what the "no component names a typeface" rule exists to prevent.
- **A missing script prints no chord at all.** That is the deployment where the key does nothing,
  so `Shortcuts`' reading call answers `null` on a swallowed failure rather than a default — a
  default there is a claim about a keyboard made by a script that never ran. The field still
  opens the palette, because a click and a keystroke are things Blazor handles. `RenderContext` answers `false` for
  every test and proof page, so what renders is an ordinary Windows reader rather than a broken
  deployment; `GuardedInteropTests` owns the `null`.
- **A field now, and the reversal is the point of this bullet rather than a correction to it.** It
  read "a button and not a text box" for as long as the box would have been lying: a control that
  says *search* while searching six step names and 141 Powers promises the rulebook and does not
  have it, and the rulebook was behind an account. **The corpus is behind the chord now** — see the
  section below — so the promise the box makes is true, and the field is what was left of the
  decision. Ten things hold it in place:
  - **It searches nothing itself, and that is the line.** Typing hands the word to
    `Commands.Open(query)`; the palette takes it on the way in and matches it exactly as if it had
    been typed into the box — the steps, the Powers, the pause, the race guard, the book. There is
    one palette, one matcher and one corpus reader, and a second search implementation in the
    banner is the thing to refuse. It is asserted **on the wire**, not on screen: one request for
    one keystroke, because a field that also asked on its own behalf would put two requests up and
    look perfectly correct, the second answer landing on the first one's rows.
  - **Who is asking is settled *before* the carried word is asked about, and this was a defect.**
    The palette asks the book from `Refresh`, on the way in, and asks who is here one interop hop
    later in `OnAfterRenderAsync`. For the chord that costs nothing — the box opens empty. For a
    word from the banner it cost the whole feature after a sign-in inside the visit: `Accounts`
    said yes, `Commands.BookIsOffered` still said no, the carried word took the "not offered"
    branch, no request went, the offer flipping a moment later only redrew, and the reader was
    shown *Nothing here matches what you typed* over a rulebook with three entries for their word.
    So the offer turning **on** re-asks whatever `_wanted` already holds. It is fire-and-forget on
    purpose: `NoteWhoIsAskingAsync` is awaited immediately before the caret is moved into the box,
    and awaiting a fifth-of-a-second pause and a round trip there is a box that cannot be typed
    into for as long as the network takes.
  - **The shell is not woken by the book's answers.** The banner's label follows `Commands.Prompt`,
    which moves on a sign-in, a sign-out or a refusal and at no other time — so the layout is on
    `Commands.OfferChanged` and deliberately not on `BookAnswered`, which rings once per burst of
    typing into a box behind the palette's own scrim. It was on `BookAnswered`, and every keystroke
    redrew the banner, the step band, the budget strip and the body. **A render count cannot see
    this**: bUnit's `RenderCount` moves when a descendant re-renders, and the palette is a child of
    the layout and does redraw on every answer, correctly. The guard reads the delegates off the
    running `Commands` instead.
  - **The first keystroke opens it; focus does not.** A field that opened the overlay when the
    caret landed in it is a keyboard trap for everybody tabbing *past* it towards the page — the
    palette takes the screen and leaving it means dismissing something nobody asked for. Typing is
    an intention and arriving is not. Held by a test that also asserts no focus handler is bound,
    because bUnit has no caret and nothing else in the suite could tell.
  - **The letters typed inside that hop are forwarded, not dropped.** The palette opens on the
    first keystroke and takes the caret one interop hop later, so everything pressed in between is
    delivered to the banner's field, which still has focus. `Commands.Open` early-returned while
    the palette was open and every one of those went on the floor: a reader typing at any ordinary
    speed searched their first letter and nothing else. `Open` now raises `Commands.Retyped` with
    the whole of what the field holds, and the palette applies it exactly as it applies its own
    `oninput`. **A replacement, never an append** — the field hands over its whole value each time,
    so applying one twice, or applying a stale one after a newer one, settles on the same text.
    Deliberately not `Changed` and not `_opensWith`: the opening query is read once and cleared, so
    two opens racing one render would leave the second handler taking the blank the first left
    behind, which is the box emptying itself under somebody's hands.
  - **A click on the empty field opens it, which is what the button did.** The field is empty
    whenever it can be clicked: anything typed into it has already opened the palette, and the
    scrim is over this band while it is open.
  - **`aria-label`, and it opens with the visible word.** A placeholder is not a label — it goes
    the moment somebody types — so the label is mandatory here, where the only word on screen is
    printed inside the control. It reads `Search — ` and then `Commands.Prompt`: the visible word
    first, because an accessible name that does not contain the label on screen is a control voice
    control cannot be told to use (WCAG 2.5.3), and the palette's own sentence after it, which
    names the book only to a reader who will be shown it.
  - **`Commands.Prompt` is where that sentence lives, and it moved there for this.** Two controls
    say it now, a second apart, and two spellings is how the app comes to promise the book on one
    surface and not the other to the same person in the same second.
  - **Four things `.banner-tool` gives a button that are wrong on a box, and each is undone by
    name.** The idiom was written for a button, a link and a disclosure, and a text field inherits
    it whole.
    - **`cursor: text`**, not the `pointer` that says "this happens when you press it" over a
      control that gives you a caret.
    - **No hover fade.** Cancelled on the wrapper — `.banner-tool.palette-open:hover` — and not on
      the field, because `opacity` on a parent composites the whole subtree and a child cannot opt
      out of it: a rule setting the input back to `1` would do nothing at all, silently. The
      chord's key boxes go with it; they are a hint printed beside the control, not a second one.
    - **`text-transform: none` on what is typed, `uppercase` on `::placeholder`.** They are two
      pieces of text in one control: `Search` is this band's label and wears its idiom, and what
      somebody types is their own words, which the palette's box shows a second later exactly as
      typed. Inherited, a reader typing `plasticity` here watched it come out `PLASTICITY` in the
      banner and `plasticity` in the palette — the app disagreeing with itself about a reader's own
      words inside one second. Lower-casing both was the other way to settle it and is worse: the
      word would be the only thing in the strip in sentence case.
    - **The width is `8ch` in the stylesheet**, not `size="10"` in the markup — a number with no
      arithmetic behind it, in a file where the type it was sizing is not visible. Measured,
      `SEARCH` under `--label-track` in the shipped face is 6.995ch; eight is that rounded up plus
      one character of slack for the fallback faces, since `ch` is the advance of `0` and its ratio
      to six tracked capitals belongs to whichever face actually loaded. `BannerTests` reads the
      number out of `app.css` and holds the placeholder to a word that fits it.
    - And **WebKit's own clear glyph is reset** — `::-webkit-search-cancel-button` and
      `::-webkit-search-decoration`, `appearance: none; display: none` — because `type="search"`
      draws one on exactly one engine, positioned and sized by that engine, in the row this app
      measures to a half-pixel. **Nothing in CI can see the effect of that rule**: the goldens are
      Linux Chrome, the proofs are headless Chrome, and there is no Safari harness. It is kept
      because the alternative is a defect only the owner's own browser can find.
  - **And it is measured.** The banner's baseline is proved in a browser on every CI run, and an
    `<input>` brings a box model no rule in this repository states — a border, a fill, padding and
    a `line-height` of the browser's choosing, with the shared rule for every text box on the site
    adding a panel ground on top. `.palette-field` undoes all of it, and the proof reads the field
    and the chord as two of seven items — **its place in the row, not its box**, which is a
    distinction the baseline section below records with the measurement behind it, because the
    natural assumption is the other one.
- **The word is "Search" and the palette still calls itself "Go to".** The label has to survive
  being read at a glance beside the other tools; "Go to" between two underlined links read as a
  third link with no destination. What the palette offers is unchanged and its own box says so in
  full. It said "a strip of six other controls" when it was written, and the section below is what
  took that down to two.

**And four exemptions in `UppercasedTextTests` went when this landed.** They read "MainLayout,
which needs a Body fragment and a router", and that was never true — `BannerTests` has rendered
the layout on its own since the day it was written, `Body` left null and every band drawn. Five
uppercased banner selectors were standing behind a reason nobody re-read. **When a test exempts a
selector, check the reason still holds before adding a sixth.**

### The book behind the chord, and why the doorbell did not grow

**The palette offers the rulebook's own passages now, to a reader who is signed in**, as a third
group under "In the book" — the book's heading as the row's label and the printed citation as its
detail, in the spelling `/rules` uses, from `RulebookCitation.For`.

- **`js/palette.js` is unchanged, byte for byte, and there is a test that says so.** That file says
  in as many words to resist growing it, and a corpus is exactly the thing it means: it is still one
  listener, two focus calls and one question about the keyboard. What a second body of text
  actually needs is a request, a pause, a race guard and three more rows, and every one of those is
  a decision about *what the palette offers* — which has always lived in `Commands` and been drawn
  by `CommandPalette`. `TheDoorbellHasNotGrown` hashes the shipped file; changing it deliberately
  means changing that test in the same commit, which is the point.
- **A second corpus behind an account gate is safe here because the palette only ever offers what
  `RulebookReader` answers.** The book is bundled into the worker and never staged into `wwwroot`;
  the server refuses every address under `/api/rulebook/` on the prefix; and for an anonymous
  reader the palette makes **no request at all** and shows no row, no cached prose and no claim
  that the book exists. **And it re-asks who is here on every open**, so signing out leaves the
  next opening of the palette with no rows, no request and the shorter label — the same fault
  `RulebookReader`'s own cache was fixed for, in the place it would reappear. `Commands` also
  clears the rows when the answer moves, and that line is honestly defence rather than the
  mechanism: opening empties the box through the ordinary path, so a mutation removing it
  survives. Its doc comment says so, rather than claiming coverage that is not there.
- **Who is here is settled before the caret is, and an ask re-reads the answer after its pause.**
  Both of those are one window seen from two ends: the box becomes typeable the moment focus lands
  in it, and the pause is a fifth of a second during which somebody can sign out from the account
  page in another tab. So `OnAfterRenderAsync` asks *then* focuses — the other order left an
  interop hop in which a signed-out reader's first keystrokes were asked for an account that was
  gone — and `AskTheBookAsync` checks the offer again on the far side of its pause. A sign-out also
  takes the sequence number past every ask already in the air, so one that is mid-flight is dropped
  rather than answered. Three guards on one window, and that is not redundancy for its own sake:
  the thing being prevented is this app sending a signed-out reader's typing to the address that
  serves the publisher's text.
- **The rows are keyed to the query and dropped the moment it moves.** Left up they are the
  previous question's answer sitting under the current question's text for the pause plus a round
  trip — read as an answer, because that is what rows are — and worse than read wrongly: each row
  carries the query it hands to `/rules`, so Enter on a stale one used to search the book for a
  word the reader had already typed over. `Commands` drops them before its first `await`, and the
  component asks before it counts, so the render that follows a keystroke is already the new
  query's. A late answer for a query nobody is asking is dropped on the same test.
- **The box's `aria-label` promises the book only to somebody who will be shown it.** "Go to a
  step, find a Power, or search the book" for a signed-in reader and the old two-thirds for
  everybody else — a label naming a rulebook to a reader the server will refuse is the wrong
  promise, which is the same objection the banner's control was kept a button for. A placeholder
  is still not a label: both are set and both say the same words. **The sentence is
  `Commands.Prompt` and not a string in this component**, because the banner's field is labelled
  with it too — one promise, made a second earlier, to the same reader.
- **There is no "sign in to search the book" row, deliberately.** An inert row that does nothing is
  the fault `/rules`' "What is here" panel was fixed for, and a row that *did* navigate to sign-in
  would answer a question about Plasticity with an advertisement for an account. `/rules` is where
  that offer belongs and it says it in a panel; the banner links there from every route.
- **Three, 220ms, five — and each figure has a reason rather than a taste.** Three characters
  because `terms()` in `worker/search.js` drops every word of two or fewer, so a shorter query is
  one the server cannot run and would answer `found: 0` for a question it never asked. 220ms
  because this is the **only thing in the app that goes over the network per keystroke** — the
  steps and the 141 Powers are filtered in the browser. Five rows because the palette is a way to
  reach something and `/rules` is the results page.
- **The race guard is not optional and a pause is not one.** Two queries typed a second apart are
  two requests that really were made, and the older one can answer last — the autosave's defect in
  a new place, and silent when it happens, because the rows look like an answer and are just the
  answer to the question before last. Every search takes a number and an answer is dropped if a
  higher one has already landed. The test holds the first response at the wire and releases it
  after the second, with both requests asserted so a dropped answer cannot be mistaken for a
  request that never happened.
- **And "nothing is outstanding" is a claim about the newest question, not about whichever one has
  just answered.** The other ordering is a second fault out of the same two requests: the *older*
  one answers first, and if landing it says the palette has stopped waiting, "nothing here matches
  what you typed" prints in the middle of a search that is still running and is replaced by five
  rows a moment later — the sentence's own failure mode, arrived at from the other side. Both
  orderings are driven, and they are two tests because they fail in opposite directions.
- **A book that could not be asked is not a book with nothing in it.** A search that answered
  `found: 0` means the corpus does not use the word; a 401 from a session that expired
  server-side, a 500, or a laptop off the network mean nothing was learnt at all — and all of them
  came back as the same `null` until `RulebookReader` was made to say which of the three happened.
  Printing the sentence over those tells a reader the rulebook has no entry for a word it may have
  three of, one surface along from where the Powers search shipped exactly that mistake. The state
  is deliberately quiet — no rows, and no sentence either — because "the book could not be reached"
  over an open palette is an error report for something the reader did not ask for, and `/rules` is
  where a search that failed belongs on screen. **A 401 also stops the offer**, since the box's
  label promises the book and the account it was promised for is gone; it does not start offering
  again for the same account key, because `Accounts` answers who is here out of its own memory and
  would go on saying yes. There is no cheaper hook to pull — `IIdentitySource` is one method and
  carries no way to say an answer has gone stale.
- **The rows are appended, never interleaved**, so an answer arriving cannot move the row the
  reader has Enter poised over. One flat list, one index: `aria-activedescendant` names a row by
  its position and the arrow keys move through the same positions, so a second list beside it would
  be a second numbering. The "In the book" heading is `role="presentation"` and carries no id — the
  listbox's children stay options alone — and it is not load-bearing for a screen reader, because
  every row under it carries its own chapter and page. **The arrow keys reach the book's rows and
  Enter chooses one**, which is driven as its own test rather than assumed from the click: a
  palette whose foot prints three key boxes and whose last group could only be clicked would be
  half a feature. `AriaReferenceTests` sweeps `aria-activedescendant` with this list at its
  longest, because it is the one ARIA reference here that names a position in a list that grows and
  shrinks under the reader.
- **"Nothing here matches what you typed" is held back while an answer is outstanding.** Printed
  early it says nothing matches and is then replaced by five rows, which reads as the app changing
  its mind. Nothing is drawn in its place; a spinner for a fifth of a second is worse than a box
  that has not answered yet.
- **Choosing a row is a request, not a URL.** It hands the reader's own query to `/rules` through
  `Commands.RequestSearch` — the idiom that already hands a Power to the editor on another step,
  taken once so it cannot re-run over what somebody has since typed. `/rules?q=…` was the
  alternative and was refused for now: it needs a query string parsed back out of the address by
  hand, and a second way into a page whose one entry point is its own form, for the one thing it
  buys, which is a link somebody could share.

## The sheet as a document, at `/sheet`

`/sheet` is the character being built and `/sheet/{id}` is any saved one. Both draw the banner and
then the sheet — no step band, no budget strip, no switcher, no findings panel. It is the fifth
area, and the area is what does the work: `MainLayout` draws all three of those bands in `Area.Play`
alone, so this page **inherits none of them and cannot forget to**. An address under `/build` would
have inherited all three by construction, which is the whole reason this is not one.

- **It is not the `/build/sheet` that was retired, and the difference has to survive.** That address
  was the review step with `Explain` flipped, so once explanations became the default it offered a
  route to the page you were already on. **This page differs by what it omits, never by a setting.**
  If a panel about *building* the character ever appears above the sheet, the page has become the
  review step again — which is what `TheDocumentIsAloneOnThePage` refuses, with the sheet's presence
  asserted first because the other three assertions are absences.
- **Showing is not opening, and the two methods are one word apart.** `ReadAsync` has no side
  effect; `OpenAsync` moves the current-character pointer and overwrites the anonymous slot. A page
  built on the second would mean glancing at an old character switched the app to it, and the next
  autosave wrote the sheet on screen over what was actually open — the shape of a defect this
  project has already shipped once. **Reach for `ReadAsync` anywhere a character is displayed rather
  than edited.**
- **The subtitle does not name Hero or Villain here**, unlike the builder's. The sheet on screen may
  be somebody else's, so naming *this* visitor's identity over it is the fault the budget strip was
  pulled off three areas to fix. Same reason the switcher stays in `Area.Play`.
- **The read is keyed on the id, not guarded by a bool.** Blazor reuses the component when only the
  route parameter changes, so a one-shot flag leaves the first character on screen under the
  second's address — the trap `ReplayConversation` already records.
- **"Nothing was read" and "no id was asked for" are different states.** Both arrive as a null
  character, and collapsing them renders the reader's own sheet at somebody else's dead link, which
  reads as their character having been renamed.
- **The two controls sit under the sheet** and carry `no-print`, so the first thing on the screen
  and the first thing on the paper are the same thing.
- **A route with nothing linking to it is a feature nobody can reach**, and
  `EveryRoutedPageIsReachableFromAnotherPage` is what says so — it caught this page before it
  shipped. The links are in the character manager: the open character's block, and `sheet/{id}` on
  every other row.

## The banner is two sides and one baseline, and it holds three idioms rather than five

**The owner reported it as "search is vertically elevated" and both halves of that were true.** The
arithmetic half: the three plain `.banner-link`s had their text line at 29.13px and the SEARCH label
at 27.88px, 1.25px above. The other half is why — and it is the one that decided the layout.

**`align-items: center` was not the bug and neither was a stray margin.** Every item's *box* was
centred on 30.30px, correctly. What differed was how far each item's *text* sat from its own box
centre: a link is skewed 1.17px up by the underline hanging below it, and `.palette-open` 2.42px up
because its own `align-items: baseline` pins the label flush to the button's top edge while the
`.key` boxes' border and padding hang below the shared baseline. So `.banner-inner` is
`align-items: baseline` — the thing that was misaligned is what gets aligned. **A tuned
`line-height` on `.key` also reaches 0.01px and was rejected**: a magic number depending on three
tokens rots the first time one of them moves, silently.

- **Three groups on one row, and the middle one is the character's.** Left is what the site is and
  where you can go; right is the tools; the switcher and the save region sit between them. The
  push is `margin-left: auto` on the tools rather than `margin-right: auto` on the title, because
  the middle group is absent on four routes out of five and pushing from the left would leave a
  window-wide gap where it should have been.
- **The wordmark opts out of the baseline and is the only thing that does.** It is two lines, so it
  has no single text line to share with a strip of one-line controls; on the shared baseline its
  first line would sit level with Build and the subtitle would hang below the band's optical
  centre, reading as a heading that has slipped. `align-self: center`, and
  `proof-align.html` excludes it for the same reason and says so on the page.
- **Search, the account and Settings are one idiom, and none of them is underlined.** The underline
  on `.banner-link` is what says "this is a destination". Search opens an overlay, Settings drops a
  menu, and an account is an identity rather than a place — it wore the marking anyway, which was
  the app saying a reader's own name in navigation's voice. `.banner-tool` carries the shared face,
  size, tracking and optical line; `.banner-account` is what tests reach the account by, because
  `Find(".banner-link")` returns the first match and that is an avenue.
- **The caret is drawn in CSS, never written as a glyph.** A triangle character is in neither
  typeface this app names, so it would fall back to a system face on some platforms and not others.
  Same rule as `Cmd` rather than the looped-square glyph, and the same silent, single-platform
  failure it exists to prevent. Borders name no family.
- **The hairline is `color-mix` against `--on-primary`, not `--rule`.** It sits on `--primary`,
  where the token used between sections of a panel is invisible — the same reason `.key`'s border is
  `currentColor`. It is as tall as the tools' own text, because an item on a baseline-aligned row is
  its content's height, which is what makes it read as a separator between two runs of type rather
  than as a border on a box.
- **A third avenue cost one `NavLink`, exactly as this note said it would.** It read "deliberately
  absent … nothing behind it until the campaign exists"; the campaign exists, and `Run` is that
  link. `BannerTests` asserts three avenues now and says why in the source — the number is pinned
  because the point of that control is that the avenues are a closed set with a marking of their
  own, so a *tool* that grew the underline would arrive as a fourth avenue.

### The settings menu, and why it is the character switcher's mechanism

**The Hero/Villain and Light/Dark/Auto switches live behind `SettingsMenu` now.** That took the
banner from five idioms to three and **deleted** the mismatched-padding problem rather than tuning
it: on the band the light/dark control took `--space-3` where the identity one took `--space-4`, so
that three buttons and two would fit in a strip holding four other things. Off the band there is
nothing to fit around and they are the same control twice.

It **completes** the decision recorded above — "a rules search is not a Hero or a Villain" — rather
than reversing it: the subtitle stopped naming the palette outside the builder while the switch that
*sets* it stayed on every route.

- **The disclosure is the character switcher's, and reusing it is the whole reason this was cheap.**
  That control already solved the one thing about hanging a menu off this band that no rendering
  test can see: `view-transition-name` on `.banner` creates a stacking context, so a `z-index` here
  is resolved *inside* the banner, and the banner is a static earlier sibling of `.steps`. The menu
  painted behind the step band — visible, unusable, reading as a control that does nothing. It is
  `.banner`'s own `z-index: 30` that fixes it, for both menus at once.
- **Right-anchored, unlike the switcher's list**, and for the mirror of that control's reason: this
  sits at the right end of the bar, so a menu growing rightwards would leave the window.
- **The switches are drawn on `--panel` now and that is not a re-skin.** `--on-primary` is the only
  ink that reads on the banner's fill and is invisible on the menu's. Unpressed `--ink` on
  `--panel`, hover `--panel-sunk`, pressed `--on-primary` on `--primary` — all three already in
  `EveryScreenPairInUseHoldsItsContrastFloor`, so no unmeasured pair was introduced. Hover is
  **not** a tint of the accent: `--heading` on `--accent-soft` measures 4.08:1 and WCAG 1.4.3
  applies to a hover state.
- **`Theme.ReadChoice()` moved out of `MainLayout` and into the menu.** The buttons are not in the
  document until somebody opens it, so the component that owns them asks the question. Two readers
  of one value is one more than can be kept in step.
- **`.mode-switch` still names the control and not the shape.** The light/dark pill briefly carried
  the same class, which made `.mode-switch button` match five buttons and the identity switch report
  three pressed states at once. The shape is shared by selector list. `BannerTests` caught it in
  under a minute.
- **`.settings-menu` is named in `@media print` beside both switches, and all three names stay.**
  `.banner` already covers them — the menu hangs off the band rather than out of the document — but
  a selector that is only correct because of another selector is one rearrangement away from being
  wrong, and hiding a control on paper is not a thing anybody re-checks.
- **`.save-status` is *not* gated on the builder, though the switcher beside it is**, and the
  asymmetry is load-bearing. That region carries the undo offer for four acts that replace the
  character wherever the reader is standing — including the portfolio's two sample buttons and a
  recording that navigates away from itself, both outside the builder. Gating it with the switcher
  takes the offer off the two routes that raise it most.

### Only a browser can see whether the bar lines up

`proof-align.html` measures the text **baseline** of every one-line item in the band and requires
the spread under 0.5px. Twenty-one browser verdicts now, not nineteen.

- **The baseline, not the line-box centre.** A line box's height follows its font size, so two items
  genuinely sharing a baseline in two sizes measure several tenths of a pixel apart — and this
  banner has two faces and two sizes in it. A zero-sized `inline-block` probe appended to each item
  resolves its own baseline to its single edge, which *is* the line's baseline, exactly and
  independently of the face. No computed style exposes that number and a range box gives the line
  box instead.
- **The positive control is the count.** A spread over one found item is 0.00 and passes, so a
  banner that had lost six of its seven controls would report a perfectly aligned row. Proved by
  mutation: dropping the `.key` class from the chord's two spans gave `FAIL` at `items 6 of 7`
  with a spread of 0.00 — the six that were still found genuinely did share a line, which is
  exactly the reading the count exists to refuse.
- **Seven items, because the search control is a field and is measured as two of them.** The
  `<input>` takes no children, so its baseline is read off `.palette-open` — the probe joins that
  flex line — and the chord is read separately in a `.key` box, which holds text of its own.
  **Each row was measured catching a defect the other reports as a tidy band**, which is why there
  are two: `.palette-open { align-items: center }` puts the control on 28.17 against the band's
  32.00 while the chord stays within 0.25px (chord row alone: PASS), and a defect confined to the
  key boxes leaves the control on 32.00 with everything else while the chord goes to 37.00
  (control row alone: PASS at 0.00px, over a chord 5px off the line).
- **What this page does *not* hold is the field's own box model.** Restoring the UA border and
  padding `.palette-field` strips moves all seven items from 32.00 to 33.00 *together* and leaves
  the spread at 0.00px, still `PASS`: `align-items: baseline` re-aligns the band to the field's new
  baseline, and a spread cannot see a band that moved as one. Recorded because the opposite is the
  natural assumption and this page is read as evidence — those declarations are held by the pixel
  goldens, not here.
- **And it does not see x-position either.** Every measurement on this page is a `top`; the field's
  width, the gaps between the tools and where the cluster sits in the row are invisible to it. A
  band with its seven items on one baseline and the search box twice as wide as it should be is a
  `PASS` here. **So a change to the banner's geometry is a change nothing in the ordinary CI run
  will catch, and it has to go through the pixel goldens.** `tests/visual-goldens/*.png` includes
  the four `proof-shell-*` pages, which draw this whole band; regenerating them is
  `.github/workflows/visual-goldens.yml`, which is `workflow_dispatch` only and **commits nothing** —
  it uploads the PNGs for somebody to look at and commit, because a golden updated as a side effect
  of an unrelated change is a regression signed off by nobody:

  ```bash
  gh workflow run visual-goldens.yml --ref <branch>
  gh run download <run-id> --name visual-goldens --dir tests/visual-goldens
  # look at the PNGs — they are real images — then commit them
  ```

  It has to be that workflow and not a local run: the goldens and the check must come from the same
  Chrome, and `scripts/visual-regression.sh` drives a Docker Chrome off Linux and the runner's own
  Chrome on it. Changes that need this: the field's `width`, its `margin`, the tracking or transform
  that decide what a `ch` measures, anything about the `.key` boxes, and adding or removing a tool.
- **A twin reproduces `align-items: center`** — the owner's reported defect — driving the
  byte-identical script, and CI requires it to say `FAIL`. Measured: PASS at 0.00px, twin FAIL at
  2.00px. **The C# suites stay green against that mutation**, which is the whole reason the harness
  exists: a CSS guard asserting `align-items: baseline` would pass the day somebody adds a taller
  child the baseline no longer saves.

**And `AriaReferenceTests` renders `MainLayout` now, which it never had.** Both banner disclosures
render their list inside an `@if`, so an unconditional `aria-controls` on either dangles whenever it
is shut — the budget disclosure's shipped bug in two more places. The switcher's had been correct
and *unswept* since the day it was written.


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


## Two cards are how this app asks a yes-or-no question

**The Hero Point limit is a pair of `OptionRow` cards under a rule on the tier page, and it used to
be one button in a panel.** What made the pair worth the extra markup is not the chrome it saved —
it is that a single toggle has to label itself with either the action or the state, and the one
here did both by turns: off it read "Hold me to the tier's budget", which is what pressing it would
do, and on it read "Building without a limit", which is what was already happening. A glance could
not tell which of the two it was reporting. **Reach for two cards whenever a boolean is a state
somebody will read at a glance rather than an action they are about to take.**

- **Both labels name the same kind of thing as each other** — two states or two choices, never one
  of each. That is the whole fix, so it is asserted rather than left to taste: the two are written
  as a parallel construction a test can read.
- **Both cards are always visible and exactly one is pressed.** Hiding the unselected one is the
  flipping label again in another spelling.
- **`OptionRow.Pressed` is a `bool?` and renders `"true"`/`"false"` as a string.** Blazor drops a
  false bool attribute and renders a true one as `aria-pressed=""`, which is invalid ARIA that
  assistive technology reads as *not* pressed — the trap `MainLayout`'s switches already document.
  It draws nothing at all inside a listbox, where `aria-selected` is what says which row is the
  answer; a row carrying both would be two answers to one question.
- **A card group under a rule is not the grid above it.** The tier cards and these are drawn in one
  idiom deliberately — they are the same kind of thing to look at — but a tier is a
  pick-one-of-six and the limit is an orthogonal boolean. `.budget-choice` is `border-top` and
  padding, the same separation `.make-another` uses, and it is load-bearing: dropped into the grid
  the pair would read as two more tiers, and **you still pick a tier without a limit, because the
  Trait Cap still applies**.
- **Choosing the state already in force is not an edit.** The session's setter returns early, so
  the `Version` counter does not move — and an edit recorded there would close an undo window
  somebody was still inside.


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
- **"What is here" is a list of controls, and it prints no passage counts.** Each chapter row runs
  a search of the box's words scoped to that chapter, through `chapter=N` on the search route — see
  [`accounts-server.md`](accounts-server.md) for why the narrowing has to be the server's. "742
  passages" was a statistic about how the extractor split the text: the app describing its own
  internals to somebody who asked about a rulebook. The chapter's name and its printed page range
  stay, because they answer whether a thing is in the book and where to find it in a paper copy.

  **The ban on that count is scoped to that panel and must not be widened.** `Summary()` prints "12
  passages, best 5 first" beside the results, and that is a different number doing a real job — how
  many matched against how many are shown, which is the honesty the whole page is built on. A guard
  reading "no passage count anywhere on `/rules`" would kill it.

  **A row is disabled until the box holds something**, because the server answers an empty query
  with `found: 0` and this page prints that as a sentence about the book — a row that ran on an
  empty box would tell a reader Ch.4 is silent on the strength of their not having typed yet. The
  panel's aside says what the rows are waiting for. Searching the chapter's *own title* instead was
  rejected in `PROGRESS.md` and stays rejected: a row labelled with a chapter that answers with hits
  from three other chapters is the original "looks like a list of links and is not one" complaint in
  a new spelling.
- **This page can be arrived at with the question already asked — or be sitting here when it is.**
  Choosing a passage in the command palette hands the reader's own query over through
  `Commands.RequestSearch`, and this page *takes* it: once, so it cannot re-run over whatever has
  since been typed into the box, the same read-once rule a requested Power follows. It is taken
  even when the book is refused, or it would sit waiting to fire on some later visit; nothing is
  searched in that case, because a search this account cannot make answers null and would draw
  nothing beside a panel already saying to sign in. See the palette's own section above for why the
  query travels this way rather than as `?q=`.
- **Taken on `Commands.Changed` and not only in `OnInitializedAsync`, and that is a fix rather than
  a flourish.** Reading it on initialisation alone worked for every reader except the likeliest
  one: somebody already on `/rules` who opens the palette and picks a passage got *nothing*, because
  `NavigateTo("rules")` from `/rules` is a no-op, Blazor reuses this component rather than
  initialising a second one, and the request then sat in the service until some later visit
  answered a question asked minutes earlier. The handler dispatches through `InvokeAsync` — the
  event can be raised by the key listener, which arrives from the browser rather than from Blazor —
  and the page unsubscribes on dispose. **The test renders this page first and asserts on that same
  instance**; the version that rendered a fresh one afterwards passed against the live defect, which
  is a thing the app never does and the test always did.
- **A scoped answer says which chapter it came out of, and the box is the way back.** The results
  panel is headed "What Ch.4 says" rather than "What the book says", a scoped miss names the chapter
  and points at the Search button, and submitting the form always clears the scope — a narrowing
  that survived the next query would answer a new question out of a chapter chosen for the old one,
  with nothing on screen looking wrong.


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
- **A cap tighter than a row's own floor is drawn, not enforced, and above all not thrown over.**
  `RankRow.Ceiling` is `max(Max, Min, Rank)` and `Max` is the Trait Cap in force. Two states reach
  it and both are ordinary once a house cap exists: a campaign capped at 2d over a package that
  grants 3d made `Min > Max`, and `Math.Clamp` throws `ArgumentException` on exactly that — the
  first pip click took out the whole step, on a character the validator already had
  `TRAIT_CAP_BELOW_MINIMUM` and `TRAIT_ABOVE_CAP` to say something about. And an 8d Trait under a
  6d cap drew six pips announcing `aria-valuenow=8` against `aria-valuemax=6`, a `slider` outside
  its own range — the fault the budget strip records for `progressbar`, one component over. The
  ceiling gives way to what is on the sheet, so the rank comes down and cannot climb; nothing is
  repaired and the findings under the row do the talking. There is no "over the cap" ink, and a
  second way of saying what the finding says is not worth a token.
- **`HP_BUDGET_EXCEEDED` and the tier findings are deliberately unrouted.** They belong to no row,
  and the budget strip already exists for the first of them. A finding pinned to an arbitrary row
  would be worse than one shown where it belongs.


## Many characters: what actually puts one into the list

**The index is the list. A payload nothing indexed is a character nobody can get back to** —
`SavedCharacters.ListAsync` discovers every character *through* the index, because a `localStorage`
that can be enumerated is a new interop surface and the fake in the tests is a flat dictionary with
no way to list keys. The one exception is the legacy slot, checked directly every visit.

- **`SaveCurrentAsync` adds the open character to the index, and that is not an optimisation.** It
  used to bump an existing entry's timestamp and do nothing at all when there was none, so the only
  way into the index was an explicit labelled save — which nothing outside `web/Services/` called.
  The list, the banner's switcher, `DiscardedCharacter` and both undo buffers all read that index,
  all were tested, and none of them could ever have had two characters to work with. **The account's
  store never had this bug**: its `PUT` creates the row on the first autosave. The two sides
  disagreeing is what hid it for a whole slice.
- **Nothing empty is ever listed**, on either side — `CharacterSession.IsWorthKeeping`, one
  predicate. The payload is still written when the sheet is empty, because emptying the current slot
  is how starting over leaves it; what is guarded is the row a person sees. Without it, minting a
  fresh id and opening it creates a listed, empty character the instant the palette is switched.
- **The label is the sheet's own name, refreshed on every autosave**, and there is one spelling of
  that rule (`SavedCharacters.LabelFor`) because there were two and they would have drifted. The
  older note here said "an ordinary edit is not a rename" — true only while the index could be
  reached by an explicit save alone, which made the label a thing you set once and never changed.
- **The legacy row is read off its payload, not synthesised from the fact that one exists.** That
  slot predates the index, so it cannot be discovered through it; but drawing a row for any payload
  at all listed a character called "Unnamed character" whatever it was really named, and listed one
  for an empty sheet that autosaved because somebody switched the palette. It is one read of one
  payload, which is the only place in this class where reading a payload to draw a row is worth it.

## The character manager's layout, and the one constraint that decided it

**The panel holds one kind of thing and offers four actions on it, and its first layout encoded
neither.** A row was a name at the left edge with two buttons at the right across a gap that grew
with the window, and the two ways to make a character floated beneath it unequal and uncontained.
Three rules came out of rebuilding it:

- **The row is the control.** A character's name is a real, full-width `<button>` that opens it —
  the same idiom a Trait row's name uses — and Discard is a quiet trailing button. The large easy
  target is the safe act and the small distant one is the destructive act. It costs no extra tab
  stops: two controls per row before, two after.
- **The character on screen has its own block above the list, and it is the only row that may carry
  a figure.** `SavedCharacters` keeps labels and timestamps in the index and each payload under its
  own key, so a Hero Point figure on an ordinary row is a read, a cost and a validate *per row* —
  which that class's remarks refuse. The open character is free because the session is already
  holding it. **That has since been paid for rather than argued with** — see the roster section
  below: the *index* carries the spend now, written when the character was saved, so a row states a
  figure without anything being read to draw it. The constraint is unchanged and is what shaped the
  fix. **A row carries a time as well** (`Ages.Since`, null for the legacy slot's absent stamp
  rather than "over a year ago"), and that rule has narrowed too.
- **Nothing on this panel is drawn in `--danger`.** The red was there so a destructive control looked
  as serious as what it does, and then the confirm was removed *because* undo makes discarding
  cheap — leaving the loudest thing in the panel attached to its rarest and most reversible action,
  repeated per row. Red is kept for what cannot be undone.

**And the reason `ImportCharacter` is no longer `.small`:** the demotion was itself a fix, for the
operating system's raw file chip competing with its neighbour, and it worked by making one of two
peers visibly lesser. Both make a character that does not exist yet. Separating them from the list is
a container's job — `.make-another` draws the rule and splits the bar — not a font size's.

**The block reads the session, so the panel subscribes to it.** The name is live on the finishing
step where somebody is typing it, which is the same reason the banner's switcher reads the session
rather than the list. The list itself is *not* re-read on every change — that would be a storage read
per letter typed.

## The roster: two shapes of one panel, and what a row is allowed to know

**`/build/characters` is the list; the tier page keeps the character on screen and a link to the
rest.** At twenty-nine characters the panel was the tallest thing on the page a visitor meets
first, so choosing a tier meant scrolling a screen of other people's characters to reach the six
cards — and a GM sorting NPCs is not choosing a tier at all.

- **It is under `/build` rather than beside it, and that is the whole of the routing decision.**
  `Areas.Of` reads the first segment, so the address is `Area.Play` by construction: the step band,
  the budget strip and the banner's switcher come with it and cannot be forgotten. A top-level
  `/characters` would have needed a case in `Areas.Of` saying "this one is the builder too", which
  is the special case the prefix scheme exists to avoid. The step band draws with **no step
  marked** — every step's `NavLink` is `NavLinkMatch.All` — which is correct rather than tolerated.
- **One component, two shapes, and `ListsEveryCharacter` turns off a list and never a behaviour.**
  Both shapes name the character on screen, both offer the two ways to make one, and both report a
  refusal through `Keep` in the same words. Two components would be two chances for one of them to
  forget to say anything.
- **Below `ToolsFrom` rows the panel is exactly what it was.** A search box, three order buttons and
  a set of headings over four characters are furniture on a list a reader can see the whole of —
  the argument `_showTimes` makes at two, one threshold up.

### What a row may know, and the index that pays for it

**`SavedCharacterSummary` carries `Kind`, `TierId` and `Spent` as well as the campaign, and every
one is a duplicate of something inside the payload.** The server cannot derive any of them — it
never parses a character — so the client sends them alongside, exactly as it sends `label`. See
`docs/CHARACTERS-API.md` and migration `0008`.

- **One spelling of what an index records**, `SavedCharacters.IndexFieldsFor`, shared by both
  autosave paths. `LabelFor` had two copies once and that is how they would have drifted; a
  character described one way in this browser and another on the account is a list that disagrees
  with itself depending on who is signed in.
- **`Spent` is `int?` and null is an answer.** The engine throws rather than guessing on an
  incomplete selection, and the autosave fires on the very change that makes a sheet unpriceable —
  so `CharacterSession.TryCost` is asked and null is written. A row then shows its tier and no
  figure, which is the front door's rule. Zero would be a cost nobody computed.
- **The defaults on those three parameters are load-bearing in exactly the way `CampaignId`'s
  are.** An index or an account row written before them must still list; a `required` member there
  empties a returning visitor's list in silence. A literal four-field index is checked in.
- **A tier id the rules do not know is printed as itself**, the same choice `CampaignDiff` makes: a
  row reading "Unnamed" tells a GM nothing, and the id at least says what to look up.

### Three things drawn only where they say something

- **The Hero/Villain chip, only where the list holds both.** On a player's roster it is one word
  down every row. It is `.open-target .kind`, an outline and not a fill: `--accent` is a fill token,
  and a Villain chip tinted toward the Villain palette's own crimson is invisible on that palette's
  ground. What distinguishes it from the `.meta` phrases beside it is the box.
- **The time, only where it explains something.** It was drawn from two characters up, as the one
  thing telling two rows apart; on twenty-nine imported in one sitting it read "3 minutes ago" on
  every row. It is kept for lists too short to have an order control, and for the `Recent` order,
  where it *is* what the order means. The element is `.when` — named so a test can ask for the time
  rather than sniffing a row for the word "ago", which `Ages` does not always use ("just now") and
  which the spend beside it could one day contain.
- **A group heading, only where there is more than one group.** `Roster.Group` decides how many
  groups exist from the **unfiltered** list, so a heading cannot change identity under somebody's
  typing; a group with nothing matching is dropped, and the survivors read "4 of 11" while a filter
  is on. One group that is a real game keeps the game's name; one that is "In no game" or "A game
  that is not here" falls back to the sentence the panel has always carried about where these
  characters live.

### The decisions are in `Roster`, not in the component

Which rows survive a query, which heading they land under and what the count beside it says are all
answerable without a browser — the same split `Commands` makes for the palette, and the reason most
of `RosterTests` renders nothing. The matching rule is **`OptionFilter.Matches`**, the one the six
pick-lists and the palette already share: a reader who has learnt that "plast" finds Plasticity has
learnt something about this app, and a roster that matched differently would be teaching them it was
about one list. `RosterNames` carries the game names and the tier names together rather than as two
parameters of the same type, which could be passed the wrong way round in silence — the failure
being a roster grouped by tier name, which is neither a compile error nor obviously wrong on screen.

**`.options-filter`, `.options-count` and `.others-head` are reused rather than reinvented.**
`UppercasedTextTests` reads every `text-transform: uppercase` rule out of `app.css` and requires
each selector to be found on a page in its own hand-maintained list, so a new uppercase class costs
either a page in that list or an exemption. Reusing a selector already found there costs neither —
and `.open-target .kind`, which is new and is uppercase, is found because the manager fixture in
that file already holds one Hero and one Villain.

## Campaigns: stored here, resolved here, and never resolved in the engine

`ApiCampaignStore` / `AccountCampaignStore` / `ApiMembershipStore`, and `CampaignJoin` is the only
thing that decides anything. **Three screens draw them now** — see the section below; this one is
the storage half, which shipped a slice earlier.

- **There is no local campaign store, and `SavedCampaigns` is deleted.** It kept campaigns under
  `pp.campaign.v1` beside the characters, written before there was a screen. A campaign is the thing
  two accounts hand a snapshot between: one kept in a single browser can never receive a submission,
  hold a clone, or be joined by the code it would advertise. **Nothing was lost** — no screen had
  ever created one, so nobody could be holding one under that key — and `pp.character.v1` still
  means exactly what it always did, which was the half of the old rule that mattered.
- **The engine may not resolve a campaign, and `CharacterSheet.CampaignId` is barred for a
  different reason from the two presentation flags.** They are a palette and a way of working; this
  is an *indirection* — resolving it means asking storage, storage in a browser is asynchronous, and
  `IRulesSource` is synchronous precisely to forbid that. `PresentationFlagsTests` carries all
  three and says which bar each is under. `AccountCampaignStore.ForAsync` is the one place a
  campaign id becomes a campaign, and **a null id resolves to null, never to a default** —
  `ACharacterInNoCampaignIsUnchanged` is the guard, over both samples and every tier, on the
  rendered sheet in full and the ordered finding codes.
- **Inherit into an empty field; offer into a full one.** Joining copies the campaign's tier and
  sandbox setting only when the character has no tier. When they disagree, **nothing is written at
  all** and a `CAMPAIGN_TIER_MISMATCH` finding is handed back. Repair is worse than usual in both
  directions: raising the tier turns an illegal character legal in silence, and lowering it moves
  Resolve, which is `(TraitCap − highestRelevantRank) × 2`. `AnEmptyTierIsInherited` is the positive
  control that keeps the mismatch assertion from being an absence satisfied by a join that does
  nothing.
- **Joining copies a campaign's Trait Cap into a character that has none, and never over one that
  has.** That is the tier rule one field at a time: an empty field is filled — alongside the tier
  where the tier was empty, and on its own where it was not — and a character already built to a
  cap keeps it, with `CAMPAIGN_TRAIT_CAP_MISMATCH` handed back by `Inspect`. Writing over one would
  move Resolve on somebody's finished character in the course of typing a join code, which is the
  same objection as lowering a tier. **A cap mismatch does not block the join and a tier mismatch
  does**: a character at the wrong power level is at the wrong table, and one whose table caps
  tighter than it does is a character with a finding on it. The finding carries the two ranks as
  fields rather than in its sentence, the way the tier finding carries the two ids.
  `AnEmptyTraitCapInheritsTheCampaigns` is the positive control and asserts the Resolve it moves,
  because a join that wrote a field nothing read would satisfy an assertion about the field alone.
- **A join says exactly what it took, because the outcome could not.** `Apply` returns
  `CampaignJoinResult` — the outcome plus `TookTier` and `TookTraitCap` — and each sentence states
  that and nothing more: "Its 6d Trait Cap is now yours, and Resolve is measured from it." One
  outcome covers four different things having happened (both, tier only, cap only, neither), and
  the page guessed from the outcome alone. It guessed wrong, claiming "Its tier and its Trait Cap
  are now yours" over a join that took the tier and left a cap the character already had — the one
  thing joining most carefully does not do, announced out loud. The write is `??=` and is silent by
  construction, so the flags are read *before* it. **A message claiming a change nobody made is
  worse than no message**, which is the same rule the three kept-but-no-room refusals follow.
- **`CampaignJoin.Inspect` is drawn at the head of "Games you are in" on `/campaign`, and nowhere
  else.** All three of its findings — `UNKNOWN_CAMPAIGN`, `CAMPAIGN_TIER_MISMATCH`,
  `CAMPAIGN_TRAIT_CAP_MISMATCH` — reach a reader there and only there. **It shipped reaching
  nobody**: the method was called by tests alone for a whole slice, so a character at 8d in a 6d
  game was told on no screen, which is the fault this repository keeps hitting. The page resolves
  the character's campaign **once per campaign id** and asks `Inspect` **every render** — the tier
  and the cap move without the id moving, and re-resolving per keystroke is the read-per-letter
  `ChooseTier` already refuses. The screen looks the two tier ids and the two ranks up and says
  them, which is the other half of the finding carrying them as fields rather than in its sentence.
- **`CharacterSession.TraitCap` is the cap in force, not the tier's**, and it is
  `DerivedStatsCalculator.EffectiveTraitCap` rather than a second spelling of the coalesce. Every
  rank field on every step is bounded by it, and the number the browser bounds by has to be the
  number the validator judges by and the number Resolve was measured from. **Wherever the cap is
  printed — the budget strip, the printed sheet's meta line, the Resolve breakdown, a replay's
  verdict — the tier's is named beside it when the two differ**, because "Trait Cap 6d" at the
  Standard tier looks like a mistake to anybody who knows the tier allows 12d. **All four are
  tested, and for a while only the strip was** — `TraitCapOnScreenTests` covers the other three,
  each with the control that a character on its tier's own cap is told one figure and not two.
- **No screen claims which *way* the cap moved, because nothing on a screen can tell.**
  `CharacterSession.TraitCapIsNotTheTiers` is a difference, not a direction, and a cap *above* the
  tier's is reachable: the GM's form takes 1 to 30 whatever tier is chosen, and the validator
  reports `TRAIT_CAP_ABOVE_TIER` and **still uses the number as written**. The Resolve breakdown
  said "This game caps tighter than its tier's 12d" and would have said it over a 20d cap, between
  the two figures that contradict it. It reads "Not the tier's 12d" now, which is true either way.
- **The GM's form says when a cap is above the tier it chose, and does not refuse it.** A house cap
  tightens a tier's ceiling and never loosens it, and the box accepted 20d at Standard in silence
  under a hint reading "Tighter than the tier's" — so every character joining with no cap of its
  own inherited an error on a screen the GM never opens. **Refusing was the alternative and this is
  one of the few places it would have been defensible** — a form checking its own input against a
  tier the same form chose is not the engine judging somebody's character. It reports because a GM
  may type the cap before picking the tier, and a Save that silently does nothing is a control that
  looks broken; reporting is also the answer the engine gives the same mistake one level down, so
  the two cannot disagree about what a bad cap means.
- **A campaign's Trait Cap is read from the character, never from the campaign.** That deferral is over: the owner settled on 2026-09-05 that a house cap *substitutes*
  for the tier's, so it moves Resolve — see [`rules-engine.md`](rules-engine.md) for the
  arithmetic and why gating was not an honest alternative. What survives unchanged is the route.
  `CharacterSheet.TraitCapRank` is a field on the character; `Campaign.TraitCapRank` is still read
  by nothing that computes anything, and `CampaignTests.ACampaignsTraitCapIsNotReadFromTheCampaign`
  pins that at three caps including none. **If a reader for `CampaignId` is ever written, that is
  the test that fails.**
- **`CampaignId` is not in `CharacterSession.IsWorthKeeping`, and must not be.** Adding it would
  make picking a campaign create a real, listed, empty character the moment it happened — verbatim
  the defect that predicate was added to fix.
- **A finding carries the two tier ids rather than writing them into its sentence**, because an id
  is not what a tier is called and every other finding here names things the way the book does.

## Keeping a character while starting another

**"Start a new character" and "Import a character" keep what is on screen. They used to destroy it.**
`StartNew` was `StartAgain` plus `ClearAsync` — it emptied the slot the character was in — and on an
account `ClearAsync` is an HTTP `DELETE`, so it removed the row from the server. Import overwrote
whatever the current-character pointer was aimed at. Both now go through
`AccountCharacterStore.StartAnotherAsync`.

- **The order is the whole of the correctness.** Write the character down under its own id, move the
  pointer, and only then let the caller empty the session. Emptying raises the session's change
  event, which starts a write nobody awaits; doing it first races that write against the move and
  puts the empty sheet over the character being kept. Both steps are awaited, so the autosave reads
  a pointer that has already moved. This is the old ordering rule the other way up — it used to be
  "the clear lands *after* the save", because the last thing that had to happen was the slot being
  emptied.
- **The caller passes the sheet in.** The store has never known a session exists, and naming the
  sheet is what lets the keep run *before* the session is emptied.
- **The character is written down before the cap is asked about, and the reverse raced.** A list
  read straight after an edit can answer from before that edit's row existed, because the ordinary
  autosave is fire-and-forget over HTTP — so an account one short of its cap reads as having room, a
  slot opens, and everything typed into it is refused by a `409` the autosave path has nowhere to
  report. The write is awaited, so the list after it cannot be stale. A cap that could not be read
  counts as no room, the same direction `AccountCharacters.IsFull` takes.
- **A refusal is said out loud, under the button.** A control that keeps rather than overwrites does
  *nothing* when it cannot proceed, and doing nothing is indistinguishable from a control that is
  not wired up. This is the same rule `DiscardedCharacter`'s refusal follows. There are three
  refusals and they are three sentences, not one: kept-but-no-room, kept-but-the-cap-is-unreadable,
  and not-kept-at-all. "Your character could not be saved" over a character that *was* saved is the
  false alarm that teaches somebody to distrust every message the app gives them.
- **Whether a write landed is a thing the store has to answer, not something a caller can infer.**
  `SavedCharacters.SaveAsync` returns `(Id, Stored)`. It used to return the id alone, and both
  callers weighing it compared that against the id they had just passed in — the same string either
  way, so the check was dead code that read like a guard. On a browser refusing storage it reported
  success over a character that had gone nowhere.
- **Nothing here arms an undo, and `Undo` is the reason.** It restores into the sheet and never
  moves the current-character pointer, so an undo offered after the pointer has moved writes a
  second copy of the kept character into the fresh slot. `CharacterSession.StartAgain` takes
  `offerUndo` for exactly this; true only for discarding the row that is open, which really does
  empty the slot.
- **Import arms no undo, and that is not an oversight.** `ReplaceWithUndo` is for the two things
  that really do replace the character on screen without moving the pointer — a sample, a recording.
  An import moves the pointer, so nothing is destroyed, and an undo would put a *duplicate* of the
  kept character into the imported one's slot.
- **Loading a sample still overwrites**, with its one-level undo. A demonstration replacing what you
  are looking at is not a second character.

**`RenderContext(storesForReal: true)` exists because of all of the above.** bUnit's `IJSRuntime`
answers null to every read, so every storage test in this project had to assert on *which key was
written* — and a feature can satisfy every one of those while being unreachable by anybody using the
app, which is exactly what happened. It swaps in a storage that actually holds what is written, at
the cost of `JSInterop.Invocations`; `FakeLocalStorage.Calls` is the replacement recorder, and order
is what several of these tests are about and cannot be read off the end state. Reach for it whenever
the question is "does pressing this actually reach the store", and leave the default alone for
everything else.
## Reading a rendered sheet in a test

**`SheetText.Visible`, never `TextContent`.** Every name on the sheet is a `Term`, so its cell holds
the name *and* two copies of the description — the `sr-only` one `aria-describedby` names, and the
tip. `TextContent` on a Trait cell reads `"PresenceHow forceful…How forceful…"`. Seventeen tests
across four files failed on the day `Explain` defaulted to on, all of them using `TextContent` as a
stand-in for what the sheet says, which it had been while one address drew terms.

- **It inserts a separator only where a browser would**, which is a block boundary. This repository
  has shipped a test for the `Armor8d` bug that was beaten by its own helper, because the helper
  replaced every tag with a newline and read the broken markup as correct.
- **Comparing raw markup needs Blazor's handler ids stripped.** A term's button carries
  `blazor:onkeydown="N"`, a per-renderer counter, so two renders of an identical component disagree
  on a number no reader can see. `PreviewColumnTests` strips exactly that and nothing else, with a
  positive control asserting something really was stripped. Same trap `Term` documents for its own
  ids and solves by deriving them from the name; these cannot be derived from anything.
- **`Explain="false"` is still exercised even though nothing in the app passes it**, because it
  selects `Term`'s bare-name fallback — the same path a Power with no description in the rules data
  takes. A setting nothing uses and nothing tests is a setting that rots.

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

**A link to a static file needs `target="_blank"`, or the router swallows it.** Blazor intercepts
clicks on same-origin anchors and hands the path to its own router, so `<a href="join.html">` makes
the app answer its own "No such page" and the file is never fetched. The failure is invisible to
every test that checks the anchor is present with the right `href` — both are true and the link is
broken — so `EveryLinkToTheHandoutLeavesTheRouter` reads the attribute instead, over every such
anchor in `web/Pages`. The alternative is `NavigateTo(url, forceLoad: true)` behind a button; the
attribute is cheaper and a handout is a thing you keep open beside the app anyway.

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

   **A `-var(…)` is not a negative length.** A minus sign in front of a `var()` invalidates the whole declaration and the browser drops it — nothing looks broken, the rule simply is not there. Write `calc(-1 * var(--space-1))`. This was found on the budget strip's negative-margin bleed, which no longer exists (see the budget-strip note below); the CSS fact is unchanged and still applies to every remaining negative length, of which `outline-offset` is now the live one.

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

- **There is no negative-margin bleed any more, and this bullet used to say there must be one.** The strip *was* pulled out of `.shell`'s padding by `calc(-1 * var(--space-6))` in three places, with a matching pair in the ≤620px query that had to be kept in step or the band ran 8px past the page — measured once as `scrollWidth` 368 against `clientWidth` 360. **The strip is a sibling of `<main class="shell">` now**, not a child of it, so it is already the width of the window and there is nothing to escape; the mechanism is gone and the guard that watched it went with it. `app.css` records this above `.budget`. Do not reintroduce a pull to "line the strip up" — check where it sits in `MainLayout` first.
- **`aria-valuenow` is clamped to `aria-valuemax` and `aria-valuetext` carries the truth.** An over-budget character spends more than the budget, and a `progressbar` reporting 132 of 125 is out of range; the fill was already clamped in the same block while the announced value was not. The bar also carries its own `aria-label` — the one on the enclosing `<section>` names the section, not the bar.
- **`aria-controls` only while the target exists.** The breakdown renders inside an `@if`, so naming it unconditionally leaves a dangling IDREF. `aria-expanded` is what carries the state.
- Whether the disclosure is open is a field on the component, **never on `CharacterSheet`** — that is a fact about a screen, and the sheet is what gets exported and restored.

**`--accent-soft` and `--danger-soft` are grounds for tints, not for text.** Villain `--heading` on `--accent-soft` measures **4.08:1** and `--danger` on `--danger-soft` **3.94:1**, both under the 4.5:1 text needs — and WCAG 1.4.3 applies to a **hover state**, which is where all three instances were. Hover grounds are `--panel-sunk`. This was found twice: the first fix moved the tier card and left `.btn:hover` and `.btn.danger:hover` on the same pairs, so the file carried a comment naming the fault eleven lines above two live instances of it. **Re-measure; the screen palette has no luminance test, unlike print.**

**The wizard's disabled Next is legible, not merely WCAG-exempt — owner's ruling 2026-09-06 (`PROGRESS.md` item 10).** `.btn.disabled` used to fade the primary fill with `opacity: 0.45`, which measured 2.23:1-3.28:1 across the four palettes: WCAG 1.4.3 exempts an inactive control's text, but a disabled Next is exactly what a reader looks at to work out why they cannot go on, and 2.23:1 is close to invisible. It now paints `--muted` on `--panel-sunk` instead — the same recessed, secondary-text pairing `.btn.quiet` already uses on `--panel`, one step further sunk so a disabled control and an active quiet one are not the same shape at a glance — which measures 6.01:1-7.26:1, clear of 4.5:1 in every palette. That is why `tests/e2e/Checks/Accessibility.cs` no longer carries a per-node exemption for it: the pair conforms outright, so a future regression there is an ordinary `color-contrast` finding rather than something that has to be re-exempted first.

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
- **`.mode-switch` names the Hero/Villain control, not the pill shape.** The light/dark control briefly carried the same class, which made `.mode-switch button` match five buttons and the identity switch report three pressed states at once. The shape is shared by selector list; `BannerTests` caught it in under a minute. **Both controls are inside `SettingsMenu` now** — see the banner section above, including why they are drawn on `--panel` rather than on `--primary`.
- Only the palette differs. If a layout change seems necessary for one mode, the layout is wrong for both.

## The accounts panel on `/admin`, and why it introduces no idiom of its own

**One more `Panel` beside the invitation list and the failure log**, listing the players in the
reader's own campaigns with an editable cap and a disclosure onto their sheets. The server half —
the scope, the key, the single-statement write — is in
[`accounts-server.md`](accounts-server.md); what is here is what the screen does.

- **No new class and no new component.** `Panel`, `ChosenList`/`ChosenRow` and `Field` already draw
  a list of things with an action on each, which is what this is. A fourth panel that reached for
  its own markup would be the twenty-two hand-written panels this app spent a slice removing.
- **The cap is a `<input type="number">` with a real `<label>`, not a placeholder.** A placeholder
  is not a label — it goes when somebody types and it is not what a screen reader announces.
- **The Save button is dead until the number differs from the server's and while a write is in
  flight**, and the comparison is against the server's value rather than against whether anybody
  has touched the box. Typing the original number back is not a change.
- **The range is the server's, and the box's copy of it is pinned rather than absent.** `min`/`max`
  on the input are a browser affordance and the page decides nothing with them — there is no range
  check in C#, because a second copy there would be a rule the page made up and the refusal it
  produced would be a sentence about it. But `max="500"` *is* a copy, and this bullet used to claim
  it was not, so `AccountsContractTests` reads `MAX_CHARACTER_LIMIT` out of `worker/adminAccounts.js`
  and compares it with the attribute: raise the server's bound and the razor fails until it agrees.
  What the page owns is turning each of the server's three answers into one thing to say — the
  number, the account, or nothing said why.
- **Save is dead while the box holds something that is not a number**, and this is a defect it
  shipped with rather than a nicety. The draft was a parsed `int`, so clearing the box or typing
  `1e5` left the last number that parsed sitting behind a live button, and pressing it wrote a
  figure nothing on screen was showing. The draft is the *text* now and `Number` is where it
  becomes an integer or does not.
- **Each row's cap carries the player's address in its label, `sr-only`.** Twenty boxes all
  accessibly named "Characters they may keep" are twenty controls that cannot be told apart by
  the only thing announced on arriving at one; the address is on screen above the box and nothing
  linked the two.
- **The sheets are read when the disclosure is opened, not with the list.** Twenty players would
  otherwise be twenty requests for something nobody has asked to look at. `aria-controls` names the
  list **only while it is rendered** — the same conditional the banner's two disclosures carry, and
  the same dangling IDREF the budget disclosure shipped once. `aria-expanded` is a `"true"`/
  `"false"` string, for the reason every switch in this app spells it out.
- **There is one open disclosure and one `_sheets`, so a late answer is dropped by sequence
  number.** Opening a second row while the first is still in flight put the first row's characters
  under the second row's name — a GM reading one player's sheets attributed to another, which is
  worse than either row failing. `_asked` moves on every open and every shut and a reply that does
  not carry the current value is stale by construction; comparing the address instead would not do,
  because opening a row, shutting it and opening it again is the same address twice. The reply is
  what gets dropped, never the second click: the row somebody just asked for has to open now.
- **A player who holds nothing and a list that could not be read are two sentences.** Saying
  "nothing saved yet" for the second tells a GM their player has built nothing on the strength of a
  request that failed — the same rule the standing follows for `MineAsync` returning null.
- **The empty panel reads as reassurance**: *"Nobody is in one of your campaigns yet."* Same rule
  as the failure log's "nothing has failed here" — **and it is guarded, which it was not.** The
  panel is only allowed that sentence when the list actually loaded. `ListAsync` folded every
  non-2xx into `NotForYou` and the page turned everything that was not `Loaded` into an empty list,
  so one 500 from storage told a GM their games were empty; a 404 is the gate's answer and is the
  only one that still means that, and everything else — 500, 502, 405, a 200 carrying the app's own
  `index.html` — is `Unavailable` and gets *"The players could not be read just now."* This is the
  same distinction the bullet above draws one level down, which the panel itself did not keep.
- **`AriaReferenceTests` renders this page now, in both states of the disclosure**, with the
  positive control that the click really opened the list. A reference resolves for the wrong reason
  when the element it names happens to be there.

## Campaigns: three screens, a diff, and a standing

**A campaign holds a clone of a character; a player's edits arrive as an approval request.** Fork
and pull request, for characters. `/campaign` is the third avenue, `/campaign/{id}` is the approval
screen, and the character manager and `/sheet` carry a standing. The storage half shipped a slice
earlier with no screen at all, which is the fault this repository keeps hitting — a feature that
works and nobody can reach.

- **`Run` is the third avenue, and `MainLayout`'s own note said what it would cost.** One
  `NavLink`; `.avenue-nav` is a flex list and `Areas.Of` reads the first segment. That note said
  "not here, because a door onto an empty room is worse than a wall" — the room is `/campaign`.
  `Area.Campaign` draws neither the step band nor the budget strip, for the reason every other area
  drops them: **the spend on that screen belongs to somebody else's character**, which is the exact
  confusion the strip was pulled off three areas to fix. `BannerTests` asserts three avenues now,
  not two, and says why in the source.
- **Campaigns are account-only, and `SavedCampaigns` is deleted.** A campaign kept in one browser
  can never receive a submission, hold a clone, or be joined by the code it advertises — so an
  anonymous campaign is a promise to somebody who can never be told. `AccountCampaignStore` answers
  nothing for a signed-out visitor and the screen refuses before the store is asked. **Nothing was
  lost**: no screen had ever created one, so nobody could have been holding one under
  `pp.campaign.v1`. That class's two static helpers (`NewId`, `LabelFor`) are on `StoredCampaign`,
  where the envelope is. **A character still has a browser half and always will** — that is the
  asymmetry, and it is not an oversight.
- **The diff is `web/Services/CampaignDiff.cs`, and it is a report rather than a merge.** There is
  no method that could apply one row, and a test asserts there is none: partial application is a
  merge algorithm for characters — a second engine, capable of producing a sheet neither person
  authored. Whole snapshots are accepted or rejected.
- **It is led by the spend, and both figures are the engine's answers for the two sheets.** Not a
  difference worked out in the diff and not a number read out of a payload. A snapshot the engine
  refuses to price gets no figure at all, the same rule the front door follows.
- **Every row is named the way the book names it** — `Tier: Standard → High Level`, `Might 6d → 8d`,
  `added Flight 4`. Never a field from a stored payload, and never an id: `CampaignDiff` looks tiers,
  Abilities, Talents, Powers, Perks and Flaws up. **The guard is stated as a shape rather than as a
  list** — no underscore reaches a screen — because a list of ids goes stale and because two of the
  obvious fixture ids (`code`, `flight`) are written exactly the way their printed names are, so a
  case-insensitive check on those cannot tell a leak from a correct lookup. That took three goes at
  one fixture; the test records all three.
- **The Trait Cap row is the cap in force, not the field.** `Trait Cap 12d → 6d`, through
  `EffectiveTraitCap`, so the ceiling a GM decides about is the one the validator judged the
  submission by and the one its Resolve was measured from. A house cap written at exactly the
  tier's own moves nothing and says nothing — the same rule the 0d rank follows. **Without this row
  the screen said "nothing changed" over a real change**: the cap costs no Hero Points, so the
  spend could not be the trigger, and a player who set one between submissions moved their Resolve
  and could turn a legal Ability illegal while the GM's list stayed empty. A tier change moves this
  row as well as the Tier row, which is two facts and not a duplicate.
- **An id the rules data does not know is printed as itself, deliberately.** A payload can name a
  Power from a build these rules do not have, and a diff full of rows called "Unnamed" tells a GM
  nothing.
- **`CharacterDiff.Compared` is the positive control and it is on the screen, not only in a test.**
  A diff showing nothing and a diff that failed to run are indistinguishable, and "nothing changed"
  is the commonest honest answer this screen gives — so the page prints how many fields were
  *examined*. A comparison that has stopped comparing reports zero, which is a red test and a
  visibly wrong sentence rather than a reassuring empty list.
- **A rank stepped back to zero reads as removed, not as `0d`.** The editors leave a 0 behind when
  somebody steps a Trait down, so a row saying `removed Might 0d` would be the app reporting its own
  bookkeeping. And a list reordered is not a list changed: everything is compared by key, never by
  position.
- **The decision carries the version the screen drew, and this is the one thing here whose failure
  is a defect.** Read a snapshot, have the player resubmit while it is on screen, press Approve, and
  a page that sent the *current* version would approve a character nobody had looked at. A mismatch
  is refused and the refusal brings the newer snapshot back, so the diff redraws rather than sending
  somebody to look again.

  **Two mutations were needed to get that guard honest, and the second finding is the transferable
  one.** The first version of the test put the resubmission between the click and the request,
  through a seam on `FakeApi`; the mutation walked straight through it, because the broken page's
  extra read happens *before* such a seam can fire. **A seam in the wrong place is a test that races
  nothing.** The real race is between the diff being drawn and the button being pressed, which a
  test drives with no seam at all — so the seam was deleted, because one nothing races reads as a
  guarantee and is not one. `FakeApi` records that where the seam used to be.
- **A submission's label and its payload come from one source: the character the row names, read
  by id.** `Submit` used to send `Session.Sheet` — the character on screen — into a membership
  keyed on `AccountCharacterStore.CurrentIdAsync()`, the browser's current-character pointer.
  **Nothing holds those two to the same character.** `SignIn.razor` calls `Session.StartAgain()` on
  both its paths whenever `Store.LoadAsync()` answers null — a read that 404s, times out, or comes
  back as the site's own `index.html` — and `Program.cs`'s boot restore does the same for one that
  throws; neither moves the pointer, because the account's character is still there and still the
  one this browser has open. From that state the row for a real character still offered Send for
  approval, and sent an empty sheet under its name: the owner's GM opened an approved clone and was
  shown an unnamed character with every Trait at 0d, a 379-byte payload carrying nothing but the
  tier, campaign id and house Trait Cap that `CampaignJoin.Apply` copies in. It is `ReadAsync` and
  not `OpenAsync`, because sending is not switching to a character and opening one moves the
  pointer on the way past.
- **A campaign act needs the sheet on screen to be the character the pointer names, and the loss
  it stops is a character.** Reading the submission back by id fixed what was *sent*; it did not
  touch what joining *writes*. `CampaignJoin.Apply` puts the campaign's tier onto
  `Session.Sheet` and rings `NotifyChanged`, and that write-through lands under
  `AccountCharacterStore.CurrentIdAsync()` — so from the emptied-session state a join wrote the
  379-byte envelope straight over a fully statted character, and `IsWorthKeeping` could not stop
  it because the join had just given the sheet a tier. `Campaigns.razor`'s
  `TheCharacterOnScreenIsThePointers` runs before both acts: **asked only of a sheet with nothing
  on it**, it re-adopts the pointer's character with `OpenAsync` and says so in the sentence, and
  refuses only where the read was `Unreachable`. `ApiCharacterStore.LastReadRefusal` is what makes
  that possible — `NotThere` is a fresh empty slot, where joining with an empty character is the
  ordinary first move and there is nothing to lose, and it may not arrive as the same null as a
  dropped connection. **The strict form — "the session's id must equal the pointer's" — was
  rejected**: an account with no characters yet mints an id for its first save and loads nothing
  into the session, so it would refuse the first join every new account makes.
- **An empty sheet is refused on the page with a sentence, and never repaired.** `CharacterSession.
  HasNothingOnIt` is the question, and **"nothing on it" means literally nothing**: no Talent, no
  Power, no Perk, no Flaw, no Gear, no Name, and every Ability below the rulebook's floor. It
  asked the Abilities question alone at first, and that was wrong in the direction that costs
  somebody their work — **a powers-only sheet is a character**, a Talents-only sheet is a
  character, and both were refused with "this character has nothing on it yet" and captioned
  "empty" on the GM's screen. The Abilities half is still the engine's answer rather than a count
  of a dictionary, and that is load-bearing: the editors leave a 0 behind when a Trait is stepped
  down, so six zeroed entries are an untouched sheet and a dictionary count would call it built.
  **`Rules.Abilities.Count > 0` is guarded**, because `0 == 0` would otherwise turn a rules file
  that failed to load into "nobody has a character". **`IsWorthKeeping` cannot answer this and must
  not be reused for it** — a tier alone counts there, deliberately, and joining writes a tier onto
  an empty sheet in the course of typing a code, so the submission this refuses passes it by
  construction. A sheet the engine cannot *price* is not empty and is not refused: that is a
  half-finished character, and refusing it would be repairing rather than reporting.
- **The account's write-through will not put an empty sheet over a character its own list says is
  real, and it says so in `.save-status`.** The belt beside the campaigns page's guard, and it is
  needed because the loss does not need that page: *any* edit from the emptied-session state —
  picking a tier, switching palette — fires the autosave, and it lands under the pointer.
  `ApiCharacterStore.WouldEmptyACharacter` asks the account's index rather than reading the
  character, because a row says whether there is one and what it is called without a payload being
  fetched; a row is worth protecting when it is **priced or named**, which the 379-byte envelope is
  neither. **A list that could not be read refuses too**, the direction `AccountCharacters.IsFull`
  already takes. **Nothing is read on the ordinary path** — the list is asked for only once the
  sheet has answered "nothing on it", which is false from the first Power, Ability or letter of a
  name. **And it is never silent**: `WriteRefused` reaches `MainLayout`, which prints it in the
  same live region as "Saved" and above it, because a refusal under the word "Saved" would be the
  app reporting a write it had just declined to make.
- **The GM's screen says when a campaign is holding an empty submission, on both slots.** A clone
  with nothing on it was drawn as the character the row is named after, which is the same fault the
  two unreadable arms already have their own sentences for: a state that reads as emptiness is not
  the same as nothing being there, and the reader is owed which one it is. The sheet is still drawn
  beneath the sentence — a GM has to see what they are being told about — and Approve is still on
  the screen, because a decision about somebody's character is theirs to take. **The remedy for a
  row already like this is the player resubmitting**, which is the only way a clone has ever
  changed; nothing anywhere rewrites one.
- **The standing answers "which sheet do I print at the table", and it is silent three ways.** The
  standings could not be read; this character is in no campaign; or there is no id to match against.
  In every one of those a printed standing would answer a question nobody asked — and the first is
  the one that matters: `MineAsync` returns **null, never an empty list**, when it cannot ask, because
  an empty list says "in no campaign" and saying that wrongly tells somebody their character is out
  of a game it is still in. Same fault `SheetPage` already records for a character that could not be
  read.
- **A character in two games says both**, joined by a middle dot. One campaign's answer is not the
  other's.
- **One line on the tier page, and deliberately not a second budget strip.** The running total stays
  on the strip; what the line says is where the tier came from, which the cards cannot say for
  themselves. It resolves the campaign once in `OnInitializedAsync` rather than on every change —
  that page redraws on every step of a rank, and re-reading per keystroke is the read-per-letter the
  character manager's own split exists to avoid.
- **A tier disagreement reads as information, not as an error**, on every screen that mentions one.
  The owner's campaigns climb tiers in play, so it is ordinary traffic — and nothing is repaired
  either way, because raising the tier turns an illegal character legal in silence and lowering it
  moves Resolve.
- **The join code is drawn in `--font-display`, never a monospace keyword.** No component may name a
  typeface and the two faces this app declares are the two it has; `monospace` would fall back to
  whatever the platform decides, silently, on some machines and not others. Same rule as `Cmd`
  rather than the looped-square glyph. Wide tracking and tabular figures are what make ten
  characters dictatable.
- **`.campaign-list` is the character list's idiom in a second class, and the difference is
  layout.** A campaign row can open a block beneath itself, so the `<li>` is a column and the row
  inside it is the flex line. Reusing `.character-list` would have needed that structure imposed on
  the character manager too.
- **`.others-head` is reused for the section labels rather than a new uppercase class being
  added.** `UppercasedTextTests` reads every `text-transform: uppercase` rule out of `app.css` and
  requires each selector to be found on a page in its own hand-maintained list — so a new uppercase
  class on a page that list does not render fails, and the fix would be either adding the page or
  adding an exemption. Reusing a selector already found there costs neither.
