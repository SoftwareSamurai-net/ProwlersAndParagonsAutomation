# Slice 11: single-level undo

Phase 3 of `docs/FRONT-END-PLAN.md`: "Three buttons on the tier page can destroy twenty
minutes and are guarded by a confirm; a single-level undo is friendlier and less
interrupting than a dialogue." This is the account of what changed, what deliberately did
not, and why.

## What "the three buttons" turned into

The plan was written against an older `ChooseTier.razor` that carried "Load a Hero", "Load
a Villain" and "Start a new character" directly, each guarded by an in-page confirm. Since
then the samples moved to `/admin/portfolio` and the confirm-and-discard logic moved into
`CharacterManager` (rendered on the tier page). `CharacterSession`'s own doc comment already
named the real family before this slice touched anything:

> "the tier page's discard, the portfolio's samples and the replay's 'open this character'
> all replace the character outright... and all decide whether to ask first by answering
> [`HasSomethingToLose`]"

That is the actual "same footing" group, and it is four controls once you count the two
sample buttons separately:

1. **`CharacterManager.razor` — "Start a new character"** (`Session.StartAgain()`).
2. **`CharacterManager.razor` — per-row "Discard", when the row is the one on screen**
   (reaches the same `StartAgain()` path).
3. **`Portfolio.razor` — "Load a Hero" / "Load a Villain"** (`Session.LoadSample(mode)`).
4. **`ReplayConversation.razor` — "Open @DisplayName in the editor"**
   (`Session.Restore(...)`, now `Session.ReplaceWithUndo(...)`).

A fifth turned up while working: **`CharacterManager.razor`'s "Import a character"**
(`ImportCharacter` → `Imported` handler). It replaces `Session.Sheet` exactly the way the
other four do — and it had *no* protection at all, not even a confirm, and (a real bug)
never called `Session.NotifyChanged()`, so an imported character was not write-through
saved until some unrelated later edit touched it. It is on the same footing and now gets the
same undo.

All five now act immediately — no confirm, no "are you sure" — and `CharacterSession`
buffers what they replaced so a banner offer can bring it back.

## Why these five and not others

Several other places in the app also swap `Session.Sheet`, and the reason they were left
alone is the same reason in each case: **the character being left behind already has a safe
home of its own**, because the write-through autosave writes to whichever id the
*current-character pointer* names, and these calls move the pointer along with the sheet:

- Opening a *different* saved character from the manager (`Open(id)`) — `SavedCharacters`
  moves the pointer to the opened id, so the one you left is exactly as saved as it was.
- Signing in and signing out (`SignIn.razor`) — the anonymous browser slot and the account's
  slot are two separate stores; switching between them never overwrites either.
- The app's own boot restore (`Program.cs`) — there is nothing on screen yet to lose.

Import and "open a recording" are different: both keep the pointer exactly where it was, so
the very next autosave overwrites the only stored copy of what was on screen. That is what
makes them destructive, and it is the property `Buffer`'s doc comment names as the reason it
exists.

## Controls deliberately left as a confirm (or as nothing)

**Per-row "Discard" in `CharacterManager`, for a row that is *not* the one on screen.** This
was already unguarded — no confirm — because switching away from a character already leaves
it saved under its own id; deleting it from the list is a real, standalone delete, not a
"replace what's in front of you" action. I left this exactly as it was rather than folding
it into the new undo mechanism: restoring a deleted *list row* would mean writing back into
`AccountCharacterStore`/`ICharacterStore` under its original id, which is a different, larger
mechanism than the in-memory sheet buffer this slice built, and it was not what the plan's
"three buttons" were about. I flagged the pre-existing asymmetry (a row you're not looking at
was never asked about, even when it isn't empty) as a separate follow-up rather than folding
a fix for it into this diff — see the spawned task.

**"Sign out" (`SignIn.razor`).** Explicitly non-destructive by design — the page already says
"Signing out leaves the character this browser was building exactly where it was" — so
neither a confirm nor an undo has anything to guard.

**"Withdraw" an invitation (`Admin.razor`).** A different footing entirely: it is a remote,
server-side action against *another person's* access, not the current user's own
twenty-minutes-of-work. `CLAUDE.md` already records that withdrawing keeps the person's
characters and only ends their sessions, which is its own, gentler safety net. Building undo
for it would mean a second HTTP round trip to re-invite, which is a different feature; a
confirm was never even present here to begin with, and adding one is outside what this task
asked for.

## Where the buffer lives, and why

`CharacterSession` (scoped), as four private fields (`_undoSnapshot` — a JSON string, not a
`CharacterSheet` — plus `_undoMode`, `_undoLabel`, `_undoArmedAtVersion`) and three members:
`CanUndo`, `UndoLabel`, `Undo()`. Reasoning, matching the constraints in the brief:

- **Not on `CharacterSheet`.** It is a fact about this screen — never exported, stored, or
  read back — the same reason the budget breakdown's open/shut flag lives on a component
  rather than on the sheet.
- **Not arithmetic**, so it is allowed in `CharacterSession` under "anything resembling
  arithmetic in this file is a bug" — buffering a snapshot and comparing a version counter
  is state, not a calculation of a Hero Point or a derived stat.
- **JSON, not the live `CharacterSheet` instance.** Holding the object itself would repeat
  the replay's own recorded bug verbatim — "handing over a shared instance let the first
  edit rewrite the recording" — just with the direction reversed: the *next* character's
  first edit would silently rewrite the *buffered* one. `CharacterSheetJson.Write`/`Read` is
  the same round trip `ICharacterStore` already trusts for local storage, so nothing new is
  being trusted.
- **Single-level, closed on the first edit — no explicit clear needed.** `CanUndo` requires
  `Version == _undoArmedAtVersion` exactly. `Undo()` itself sets `_undoSnapshot = null`
  before returning, so a second call is a no-op (no redo, no second step back). Any ordinary
  edit to the character that replaced the buffered one bumps `Version` on its own — every
  edit path in the app already calls `NotifyChanged()` — so the window closes by itself
  without a second thing to remember to call. This was the one place I did *not* wire an
  explicit "clear the buffer" call at each of the many edit call sites across the app,
  because there are dozens of them and a version comparison makes that enumeration
  unnecessary — and, not incidentally, unbreakable by a future call site nobody remembered
  to add it to.
- **`CharacterStore`'s guard is untouched.** The buffer never goes through it — `Undo()`
  reads its own snapshot back with `CharacterSheetJson.Read(_undoSnapshot, strict: false)`,
  the same call `Restore` and the replay's hand-off already make, and then calls the
  ordinary `Restore` + `NotifyChanged()` path. From the store's point of view, an undo is
  just another edit; it gets validated exactly as any other write-through would.

## The banner, and why it reuses the existing live region

`MainLayout`'s `.save-status` span (`aria-live="polite"`) already existed for "Saved". It now
also carries the undo offer, in preference to "Saved" when both are true for the same
version — which happens on every one of these five controls, because the very change that
arms undo also fires the autosave that would otherwise show "Saved" and bury the one thing
worth announcing. I did not add a second live region: the brief says to find the existing one
and reuse it, and this is the only one rendered on every route these five controls can leave
you on (the tier page, the portfolio, and — critically — the review step the replay
navigates to, none of which share an area with `HpBudgetBar`'s own live region, which is
Play-only).

The offer names what would come back (`Session.UndoLabel`, falling back to "Your character"
for a sheet with no name) and a `.btn.small` button that calls `Session.Undo()`. No new CSS,
no new colour, radius, duration or raw length — `.btn`/`.btn.small` and `.save-status`'s
existing rules cover it, and buttons already render directly in this banner (`.mode-switch`,
`.theme-switch`).

## The keyboard: no `Ctrl-Z` binding, and why

Considered and rejected for this slice. Three reasons:

1. **The app has no general shortcut manager to extend.** The only document-level key
   listener is in `wwwroot/js/palette.js`, and its own comment is explicit: *"the whole of
   it is one listener and two focus calls. Resist growing it."* and *"this is the only key
   this file claims."* Adding `Ctrl-Z` there fights the file's own stated purpose; adding a
   second document listener in a new file is a materially bigger change than the feature
   itself for a single binding.
2. **A focused text input is the wrong place to steal `Ctrl-Z` from**, and the finishing
   step alone has four of them (name, appearance, motivation, quote) plus free-text
   connections. The browser's own per-field undo has to keep working there; getting that
   right needs the listener to inspect `document.activeElement` on every keypress and would
   be real, untested surface area added for one shortcut.
3. **`Ctrl-Z` carries a much stronger, universal expectation — "undo my last text edit" —
   than what this feature does**, which is "bring back the character I just replaced," an
   event that happens rarely (five specific buttons) rather than on every keystroke.
   Conflating the two is more likely to surprise somebody mid-sentence in the Motivation box
   than to help them.

The banner's button is reachable by keyboard (an ordinary tab stop) and announced to a
screen reader through the live region it already sits in, which covers the accessibility
half without the new interop surface.

## Mutation table

Every mutation below was applied to committed work (`git checkout -- <path>` used to revert,
per `CLAUDE.md`'s own discipline — safe because nothing was uncommitted first).

| # | Mutation | What it changed | Watched red | Reverted, re-ran green |
|---|---|---|---|---|
| 1 | `Buffer`/`Undo` hold the live `CharacterSheet` instance instead of a JSON snapshot | `web/Services/CharacterSession.cs` | `UndoTests.UndoRestoresACopyNotTheLiveInstance` — `Assert.NotSame() Failure: Values are the same instance` | Yes |
| 2 | `Undo()` never clears `_undoSnapshot` (repeatable / "two-deep") | `web/Services/CharacterSession.cs` | 3 failures: `UndoRestoresACopyNotTheLiveInstance`, `ASecondUndoDoesNotGoBackFurther`, `TheBannerOffersUndoAndRestoresOnClick` | Yes |
| 3 | `CanUndo` drops the `Version == _undoArmedAtVersion` check (never closes) | `web/Services/CharacterSession.cs` | `UndoTests.TheWindowClosesOnTheFirstEditToTheReplacement` — `Assert.False() Failure: Actual: True` | Yes |
| 4 | `StartAgain()` buffers `Sheet` (the new, empty one) instead of `previous` — "restore the wrong snapshot" | `web/Services/CharacterSession.cs` | 7 failures across `UndoTests` and `StartAgainTests` (`ReplacingACharacterWithSomethingInItArmsUndo`, `TheCharacterReplacedReallyHadSomethingInIt`, `UndoingBringsBackTheCharacterThatWasReplaced`, etc.) | Yes |
| 5 | The undo offer is removed from `MainLayout`'s live region — dropped announcement | `web/Layout/MainLayout.razor` | 2 failures: `UndoTests.TheBannerOffersUndoAndRestoresOnClick`, `UndoOutranksASimultaneousSavedAnnouncement` | Yes |

`git status` was clean after every revert; the full suite (`dotnet test --configuration
Release -p:ContinuousIntegrationBuild=true`) was re-run green after the last one, both
projects, no `Catastrophic` in the output.

## Files touched

- `web/Services/CharacterSession.cs` — the buffer, `CanUndo`, `UndoLabel`, `Undo()`,
  `ReplaceWithUndo`; `StartAgain()` and `LoadSample()` now arm it.
- `web/Layout/MainLayout.razor` — the banner offer, sharing `.save-status`.
- `web/Components/CharacterManager.razor` — "Start a new character" and per-row "Discard"
  act immediately; "Import a character" now goes through `ReplaceWithUndo`.
- `web/Pages/Portfolio.razor` — the two sample buttons act immediately.
- `web/Pages/ReplayConversation.razor` — "Open ... in the editor" acts immediately, through
  `ReplaceWithUndo`; the stale "there is no undo" sentence is gone.
- `tests/ProwlersAndParagons.Web.Tests/UndoTests.cs` — new: the mechanism itself.
- `tests/ProwlersAndParagons.Web.Tests/StartAgainTests.cs`,
  `tests/ProwlersAndParagons.Web.Tests/ReplayRenderTests.cs` — rewritten for the new,
  confirm-free behaviour (old ones asserted a dialogue that no longer exists).

**Not touched:** `engine/`, `sheets/`, `mcp/`, `worker/`, `data/`, option and Trait row
components, or anything else `CharacterSession` beyond the block above — the diff to that
file is additive (two new methods, two methods gaining a `Buffer` call at the end) and
should read cleanly against whatever the validation-on-the-row stream lands separately.
