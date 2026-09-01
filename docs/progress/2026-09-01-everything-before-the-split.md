# Completed work, up to the split

The account of every slice finished before `PROGRESS.md` stopped carrying them — **92
entries**, moved here byte for byte on 2026-09-01 and not edited since. Newest first, as they were.

**This file does not change.** Work finished after the split gets a file of its own in this
directory; see [`README.md`](README.md) for why, and
[`../../PROGRESS.md`](../../PROGRESS.md) for what is still open.

---

### A roster of twenty-nine, and the three fields that let a row say what it is

**The owner's report was one line — "need a better way to manage LOTS of characters (especially as
a GM)" — with a screenshot of twenty-nine of them.** What it is a report about is a panel designed
for the two or three characters a player keeps, meeting the job [item 16](#16-the-tool-costs-one-character-and-a-campaign-is-a-roster)
names: statting a campaign's worth of NPCs. That entry is about the CLI half and stays open; this
is the browser half of the same finding, and it was pitched before it was built — the design, the
faults and four questions each with a recommendation, all four taken.

**Six faults, and every one of them measurable rather than aesthetic:**

1. **No filter box**, on the only list-of-many in this application without one. Six pick-lists and
   the command palette already share `OptionFilter`; 141 Powers got a search field and 29 characters
   did not.
2. **The one disambiguating column was exhausted.** Rows carry a relative time and roughly twenty of
   them read *3 minutes ago*. The list is most-recently-touched-first, so after a bulk import the
   order is arbitrary to the eye and the only thing telling two rows apart says the same on both.
3. **Variants of one character were twenty-nine peers.** Three Cael Hugheses, two Emir Hugheses, and
   *Lena (true capability — GM eyes only)* beside *Lena (as observed)*.
4. **Discard on every row, over a one-deep undo.** `DiscardedCharacter` holds exactly one.
5. **The roster was a panel on step one of a wizard**, and the tallest thing on it — a screen of
   other people's characters between a visitor and the six tier cards.
6. **The grouping was already in the data and was not drawn.** `campaign_id` has been on every row
   since 0005.

#### Three moves, in the order their dependencies forced

**One — the roster gets a room.** `/build/characters`, under the builder's own prefix so `Areas.Of`
needs no case saying "this one is the builder too" — the step band, the budget strip and the
switcher come with the address by construction. The tier page keeps the character on screen and the
two ways to make another, and links to the rest with the count on the door. `CharacterManager` draws
both shapes: **`ListsEveryCharacter` turns off a list and never a behaviour**, because starting
another character and importing one both go through `Keep`, and two components would be two chances
for one of them to forget to report a refusal.

**Two — find, and group by game.** The filter is `OptionFilter.Matches`, the rule the pick-lists and
the palette already answer to, over the label, the game's name, the kind and the tier's name.
Headings per campaign with the ones in no game last, three orders, and a count that says what a
filter is hiding. **The decisions are in `web/Services/Roster.cs` rather than in the component** —
the same split `Commands` makes for the palette — so most of `RosterTests` renders nothing at all.

**Three — a row that says what the character is.** Migration `0008` and three defaulted fields on
`SavedCharacterSummary`: `Kind`, `TierId`, `Spent`. A row reads `Villain · 164 HP · High Level`
with no payload fetched, deserialised, costed or validated for it.

**It was written as `0007` and renumbered on the rebase**, because the campaign slice below took
that number while this branch was open.

**And the thing this entry said to watch has now been watched: `0008` applied to the live database
on the merge deploy of 2026-09-01, and it is the deploy's own reading rather than a claim.**
`wrangler d1 migrations list --remote` named exactly one pending file, `apply-migrations.sh` ran the
apply path, and `0008_character_index_fields.sql` came back ✅ against `prowlers-and-paragons` —
*Executed 4 commands in 1.16ms*. It was one migration and not the two this paragraph predicted,
because `0007` had already gone out on the deploy before it.

**And it establishes nothing new about D1 *Edit*, which is worth saying rather than claiming
otherwise**: the Hosting row already records `0007` as the migration that proved it, one deploy
earlier. This is the second real apply and it corroborates the first — which is all it is, and the
useful half of that is that the mechanism worked twice in a row on two different slices' migrations
rather than once on a favourable one.

#### The constraint that decided all three, and how it was paid rather than argued with

`SavedCharacters` keeps an index of labels and timestamps and holds each payload under its own key,
so a Hero Point figure on an ordinary row is **a read, a cost and a validate per row** — twenty-nine
requests to draw one list. That is why the open character was the only row that could carry a
figure, and it is why the fix is a wider index rather than a richer row: the fields travel *beside*
the payload, supplied by the client, because the server never parses a character and cannot derive
them.

- **The server validates a shape and never a value out of the rules.** `kind` and `tierId` are
  bounded strings and `spent` is a whole number in a range; a tier this server has never heard of is
  accepted, because it has never read `data/rules/tiers.json` and must not start. A list of legal
  values there would be a copy of a rules file kept in a language that cannot read one — stale the
  first time the data moved, and wrong about whose job it is.
- **`spent` is nullable and null is an answer.** The engine throws rather than guessing on an
  incomplete selection, and the autosave fires on the very change that makes a sheet unpriceable, so
  `TryCost` is asked and null is written. A row then shows its tier and no figure. A column
  defaulting to `0` would report that a half-built character costs nothing.
- **Nothing backfills, and every existing row lists.** A character written before `0008` has none of
  the three and picks them up on its next save. The defaults are load-bearing in exactly the way
  `CampaignId`'s are, and a literal four-field index is checked in to pin it.
- **One spelling of what an index records**, `SavedCharacters.IndexFieldsFor`, shared by both
  autosave paths — `LabelFor` had two copies once, and a character described one way in this browser
  and another on the account is a list that disagrees with itself depending on who is signed in.

#### Three things now drawn only where they say something

- **The Hero/Villain chip, only where the list holds both.** On a player's roster it is one word
  repeated down every row; on a GM's it is the fastest thing to read.
- **The time, only where it explains something.** It was drawn from two characters up as the one
  thing telling rows apart, and fault 2 above is what that became. It is kept for lists too short to
  have an order control and for the `Recent` order, where it *is* what the order means.
- **A group heading, only where there is more than one group** — and how many groups there are is
  decided from the *unfiltered* list, so a heading cannot change identity under somebody's typing.

#### What was deliberately not built, so it is not rediscovered as an omission

- **Bulk discard.** A multi-select over a one-deep undo buffer is a way to lose eleven characters
  with one click and get one back. If bulk anything, it is *move to a game*, which destroys nothing.
- **Folders or tags.** A fourth grouping mechanism beside campaigns, games and names. Worth
  revisiting if the filter does not hold at a hundred characters; not worth inventing before then.
- **A mechanism for variants** — fault 3. The owner's answer was *"perhaps a thing to think about"*,
  so it is [item 21](#21-variants-of-one-character-are-a-naming-convention-doing-a-structures-job)
  rather than a decision. Typing `hughes` already gathers all four, which is the cheap half.

#### Two findings from the testing that are worth more than the feature

- **`FakeApi` had been dropping `campaignId` on the floor since 0005.** The stub stored a label, a
  payload and a timestamp; the real server's list has answered `campaign_id` for two slices. So
  every browser-side test of "which game is this character in" was asserting against a fake that
  could never have said — the exact failure mode that class's own remarks name: *a stub that answers
  something the real server never would is worse than no stub*, and answering **less** is the same
  fault wearing a quieter coat. It now carries all four fields beside the payload.
- **A guard's proxy stopped being one, and said so.** `ATimeIsShownOnlyOnceThereAreTwoCharactersToTellApart`
  asserted on "any `.meta` on a row", which was a faithful proxy for "a time" while a time was the
  only `.meta` there could be. The spend arrived in a `.meta` and the test went red — correctly,
  about the wrong thing. Sniffing the text for "ago" was tried next and is also wrong: `Ages`
  answers "just now" inside two minutes, which is every character a test has only just saved. The
  time now has its own class, `.when`, so the test asks for the thing rather than for a symptom of
  it.

#### Broken and watched to fail

**Twenty-four mutations across the three moves, each applied to committed work and reverted after**,
with a positive control asserting the suite was green first — because a harness that cannot see a
green run cannot be believed about a red one, and this repository has shipped three guards that were
a feature not running mistaken for a feature that worked.

The worker sweep found its own harness broken before it found anything else: the first version
invoked `scripts/test-worker.sh` through `shell=True` from Python, which never ran, and reported six
consecutive **GREEN — THE GUARD HAS A HOLE** verdicts. Every one was the script failing to start.
That is the fourth time in this repository's history that "the check did not run" has presented as
"the check passed", and the control is what caught it.
### The Qodana scan left its export behind, and on Windows that orphaned a worktree

**`scripts/qodana-scan.sh` deleted its staging directory at the *start* of a run and never at the
end.** That was invisible until it was not: Qodana *builds* the project it is given, so
`.qodana-scan/project/` does not stay the clean tree `git archive` wrote — it grows
`obj/Release/net10.0/…` under every test project, about half a gigabyte, and every worktree that had
ever run a scan was still holding one.

**The failure it caused is worse than the disk.** A worktree root under `.claude/worktrees/` is
already ~125 characters; the deepest path in that export lands near 288, and Windows' 260-character
limit starts refusing operations on it. `git worktree remove` then fails with **"Filename too
long" *after* deregistering the worktree** — so the worktree is gone from `git worktree list`, the
files are still on disk, and git can no longer help delete them. That is what happened cleaning up
after the leave/notify/view slice.

**`results/` and `qodana.log` survive and the export does not**, because the first two are what the
run is for and what a failure is read from, and the third is `git archive <commit>` — one command
reproduces it exactly.

**It is a `trap … EXIT`, not a line at the end of the happy path.** The SARIF-missing bail-out and a
Ctrl-C are the runs most likely to leave a mess, and a trap that only fires when nothing went wrong
never fires on them. Watched by planting an `exit 1` immediately after the trap: the script exited
1, `project/` was gone and `results/` was not.

**Related and not fixed here:** `core.longpaths` is unset in this repository and globally, so git is
capped at 260 characters whatever the OS allows; `git config --global core.longpaths true` is one
command and covers every clone. The Windows `LongPathsEnabled` policy is the general fix and needs
administrator rights. Neither is necessary now that the export goes.

### Swapping the character in the banner left the sheet on the old one

**The owner reported the pill and the sheet disagreeing, and the sheet was right** — it was drawing
exactly what the session had told it, which was nothing.

`CharacterSession.Restore` assigned the character and rang no bell. That is correct for exactly one
caller: `Program.cs`, wiring the session up before the first render, where there is nothing to
redraw and somebody should find their character where they left it rather than watch it arrive. Its
doc comment said so. **Five call sites that run long after the first render reached for it anyway**
— the banner's switcher, `CharacterManager.Open`, and three paths through `SignIn`.

#### Why it survived, and why the fix is a name

**The failure does not show up where the mistake is made.** Whoever swaps the character is a
component handling a click, so Blazor re-renders *that* component whatever the session did — the
pill follows perfectly, and everything subscribed to `CharacterSession.Changed` goes on drawing what
was replaced. From the one control a reader is looking at, a swap that notified nobody looks
completely correct.

**The knowledge was already in the file twice and neither copy stopped it.**
`CharacterManager.Imported` had been patched with a bare `NotifyChanged()` after its `Restore` —
one call site fixed, five left — and `ReplaceWithUndo`'s own doc comment says in as many words that
*every caller that reaches this wants the redraw*. A remark on one method does not reach somebody
picking a method name in another file.

So the two are told apart by their **names**: `Open` restores and notifies and is what everything
reaches for; `RestoreBeforeFirstRender` keeps the silence and carries its constraint where it cannot
be missed, so the trap cannot be fallen into by picking the shorter name. `ReplaceWithUndo` and the
undo path go through `Open` as well, which retired two more hand-written `NotifyChanged()` calls.

#### In place, not a navigation — and that was the owner's call

Swapping updates what is on screen where the reader is standing. It does **not** navigate to
`/sheet/{id}`, and the reason is the switcher's own: it exists precisely because swapping used to
mean walking back to step one, and sending somebody to a document page is that same fault pointed
the other way. It would also blur *showing* and *opening*, which [`browser.md`](docs/guide/browser.md)
keeps one word apart — the switcher performs the second, and `/sheet/{id}` is the first.

#### Two guards, both watched to fail

- **`SwappingRedrawsTheSheetAndNotOnlyThePill`** renders the shell and a subscribing `SheetView`
  against one scoped session and asserts both move. The swap test that already existed asserts
  `Session.Sheet` changed — **which is the hole**: assigning the field is not telling anybody, and
  that assertion passes against the defect.
- **`NothingDrawnCallsTheSilentRestore`** reads the source of everything under `Components`, `Pages`
  and `Layout`. `Program.cs` still calling the silent method is the positive control, so a rename
  cannot leave the scan hunting a spelling nothing uses — which is how this repository has shipped a
  guard measuring nothing before.

Four mutations, all red: the swap going silent again, `Open` losing its notify, a page reaching for
the silent one, and the boot no longer calling it.

### A player can leave, a GM can remove and read any sheet, and a rejection stops being silent

**Three gaps a player would hit in the first session, all of them in the half that was shipped
deliberately incomplete.** The campaign slice's own "out of this slice" list names two of them, so
none of this is a surprise — what changed is that they turned out to be the ones somebody meets
first.

#### The GM could only see a sheet while a decision was outstanding

`MembershipDetail.Approved` was already on the wire and already parsed into a `CharacterSheet`, and
the approval page never touched it. The full-sheet render sat inside
`@if (_reading == one.Id && _diff is { } diff)` and was bound to the *pending* snapshot, so a
character approved in March was unreadable on the screen that exists to hold the roster, for as
long as nobody changed it. **No server change, no wire change, no migration** — the payload was
always being sent.

`Look` had two bare `return`s. An unreachable server and an empty pending slot both left the panel
blank, which is the same blank a member with nothing to show produces. The open row now carries an
explicit `Showing` and every path sets one:

    Asking · Unreachable · NothingSent · PendingUnreadable · CloneUnreadable · Settled · Changes

**Four of those are ways of having nothing to draw and they are four different sentences.** Two are
finding 8's fault in a second place: a payload from a later version of the app answers `null` from
`StoredCharacter.Read` exactly as an empty slot does, and saying *there is nothing here* about a
request a player is waiting on is the same wrong sentence on the read path that was already caught
on the decision path. The list row is what tells them apart. A final `else` arm means no state can
open an empty box, and the panel is titled for what it always held — a roster, not a queue.

#### Nobody could leave, and nothing about a character was what kept them in

`DELETE /api/memberships/{id}` is a player walking out **and** a GM removing somebody, and nothing
in the request says which: two statements run in turn, `player_user_id = ?` then `gm_user_id = ?`,
so the column that matches is what the request means and a third account matches neither. That
keeps the two-owners rule without inventing a role the browser could claim.

**Deleting the character did not do it, and that is worth writing down** — `character_id` is a
stored string never joined to `characters`, so the membership, the GM's clone and the player's own
list row all outlived it. The list then showed a row for a character that no longer existed.
Deleting the *account* was the only thing that had ever cleared a membership.

**The row goes and the clone with it**, which is deliberately the opposite of deleting a campaign.
That keeps its memberships precisely so writing it back is a complete undo; there is no undo behind
this, and a campaign holding the sheet of somebody who has left is a roster nothing could correct.
Neither side's own character is touched.

The asymmetry runs the same way as every other statement here. The player's is a bare column, so
somebody can walk out of a game the GM threw away — the row most worth being rid of. The GM's
carries the `EXISTS`, so a deleted campaign's surviving rows stay survivable, and a removal from
one is refused with the 409 both decisions give rather than reported as a removal that did not
happen. **204 whether or not a row matched**, for the reason `join` answers an existing membership
rather than a conflict, and because a 404-or-204 split would say whether an id exists.

**Both controls ask twice**, and every test asserts the *first* press did not end it — which is the
only assertion that tells a confirm from a control labelled like one.

#### Approving and rejecting were the same event from the player's side

`0007_decision_recorded.sql`. Approve moves the pending payload into the approved one; Reject clears
the pending slot and leaves the clone, which is what rejecting *means* — so both left the two
booleans the standing was derived from in a state the player had already seen. A rejection reverted
their standing to the identical sentence it showed before they sent anything: *Approved for
Nightfall*, or *Not submitted*. **The only way a player ever learned of a decision was a first
approval**, `Not submitted → Approved`. Every decision after that, either way, was silent and
shapeless.

`decision` is the fact. `CampaignStanding.ChangesTurnedDown` reads it, ordered after
`ChangesPending` so a resubmission shadows it with no clearing write, and ahead of `Approved`
because a clone is exactly what a rejection leaves untouched. A refused compare-and-swap writes no
decision at all — a refusal that recorded one would tell a player their change was turned down by a
GM who never got to decide.

**`decided_at` is stored and deliberately off the wire.** A decision without a time is a fact half
recorded and an `ALTER` cannot invent one later; but nothing draws a time yet, and
`AccountsContractTests` holds this server to sending nothing the browser binds nothing to. The open
item about a submission's age is unchanged and now has the column it would need.

#### A fourth defect, found by following the new standing to where it prints

**Two screens passed a character's own label to `Standings.Say` as the campaign's name.** That
argument is what the *game* is called; `MembershipSummary.Label` is what the *character* goes by.
So a character in two games rendered *"Approved for Ninefold · Changes pending for Ninefold"* — the
same name against every row, on screens whose stated reason for naming a campaign at all is that
one game's answer is not the other's. `CharacterManager`'s own doc comment said exactly that while
the code beneath it did the opposite.

**It had no coverage of any kind**, which is why the whole suite stayed green when it was fixed. It
survived because no rendering test ever had two campaigns in it and because each half of the
sentence reads plausibly on its own.

**Nothing can name the game today, and that is the underlying gap.** A `MembershipSummary` carries
`CampaignId` and no label, and a player belongs to campaigns they do not own, so `ApiCampaignStore`
cannot look one up either — it lists what the account *runs*. Putting `campaigns.label` on the
player's list row is a join the server can do and a wire field with a screen behind it, which is a
small slice of its own. Until then both screens say the standing and claim nothing.

`WebPresentationTests.NoScreenPassesACharactersLabelAsTheCampaignsName` is the guard, and it is a
**source** guard on purpose: the defect is a wrong argument, and markup with one campaign in it
looks correct either way — the split `testing.md` describes. It asserts its extraction is non-empty
before asserting nothing matches, and it was watched to fail, naming the offending call site.

#### Two things found by breaking a guard rather than by reading it

1. **A sentence written and thrown away unrendered — finding 7, verbatim, in a new place.** The
   removal's 409 handler set a status line the page could never draw: a GM reaches that refusal
   only by having deleted the game, at which point `Refresh` has already moved the page to
   `NotYours`. Caught because the test asserting the sentence went red. `Remove` sets its status
   *after* the refresh and only while the campaign is still on screen, and the case now asserts the
   page is right rather than that it apologises.
2. **A fake can be too restrictive, which is the same fault as finding 9 and reads nothing like
   it.** `FakeApi` answered the read's 404 gates for `DELETE` too, so a third account and a GM with
   a deleted game were both unreachable — states the server really produces, made untestable by a
   stub that was *stricter* than the real thing. The `DELETE` arm now sits ahead of those gates and
   mirrors both statements and the probe.

#### Fifteen mutations, all red

Six on the sheet view, nine on leaving and removing, each run against committed work and reverted
after — collapsing each `Showing` arm into another, the button label, the settled sheet not being
drawn, each statement's owner column, the `EXISTS`, the 204, the 409 probe, the origin check, the
`DELETE` route, both confirms, and the fake keeping the row. Each named the case it should.

#### Open, and the one thing that was worth watching

- **`0007` was the first migration `apply-migrations.sh` ever actually applied, and it applied.**
  `0006` was put in by hand and every gate run since had skipped, so the apply path had only ever
  been driven against a stub wrangler. On the deploy of `a978806` the gate read one pending file,
  classified it additive, applied it, **and then asked the database again** — the script's own
  positive control, which is what makes this "the schema moved" rather than "wrangler exited 0".
  It also settled the D1 **Edit** question that had been open since #104; see the Hosting row and
  [`docs/guide/hosting.md`](docs/guide/hosting.md). Closed, and recorded rather than deleted
  because the watch was the right call at the time.
- **A rejection still carries no reason**, because there is nowhere a GM types one. The standing
  says which way it went and does not promise more than the row holds.
- **A player's screens cannot name the game a standing is about** — see the fourth defect above.
  Two standings side by side now say what each is without saying which campaign each belongs to,
  which is honest and is not the answer. The fix is `campaigns.label` on the player's list row.
- **Notifications proper are still out.** No mail, no badge outside the campaign screens, nothing
  that interrupts. What changed is that looking now answers the question; it did not before.

### Six Dependabot pull requests, and the four things in them that were not version numbers

**Four merged, two closed, and one pull request opened that Dependabot had nothing to do with.** The first Dependabot batch this repository has ever had — raised 2026-08-28 by `a4ec6ef`, all six behind a `main` that had since taken three more merges, so every one was rebased before its green meant anything. What follows is the part worth keeping: **on a repository this heavily commented, a version bump's real cost is the prose it silently falsifies**, and four of the six had one.

| | | |
|---|---|---|
| [#107](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/107) | `cloudflare/wrangler-action` 3 → 4 | merged; deploy green |
| [#108](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/108) | `actions/setup-node` 4 → 7 | merged |
| [#109](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/109) | `actions/upload-artifact` 4 → 7 | merged |
| [#110](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/110) | eight NuGet packages | merged; deploy green |
| [#111](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/111) | `xunit.runner.visualstudio` 3.1.5 → 4.0.0 | **closed** — see item 20 |
| [#112](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/112) | `xunit.v3` 3.2.2 → 4.0.0 | **closed** — same |
| [#116](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/116) | not a bump | merged — the scan that found it is below |

#### The bump that would have unpinned production, silently

**`wrangler-action@v3` hard-codes `DEFAULT_WRANGLER_VERSION = "3.90.0"`. `@v4`'s is `"4"`** — a *range*, read out of both published `dist` bundles rather than inferred from release notes. So leaving `wranglerVersion` unset was a pin under v3 and is a floating dependency under v4: `npm i wrangler@4` resolves to whatever the newest 4.x is at the moment the runner asks. Two deploys minutes apart could bundle two wranglers. **The merge's own deploy log is the proof it was worth catching** — it reads `npm i wrangler@4.127.0` and then `⛅️ wrangler 4.127.0 (update available 4.127.1)`, which is the drift, named by wrangler itself, in the first run after the pin was stated.

`a4ec6ef` had already half-seen this. It taught `build.yml`'s dry-run `sed` to match any major so a bump could not silently leave the dry-run behind, and then ended by warning that the version *inside* the `# wrangler=` comment was still hand-maintained. **That warning is now `WranglerIsPinnedToOneVersion`**, which reads all three places a wrangler version is written — the comment `build.yml` parses, the `wranglerVersion:` input the upload runs, and `scripts/apply-migrations.sh`'s own constant — and fails when they disagree *or when any one is missing*. Watched to fail four ways: each version changed alone, and the input deleted.

**And the two pins became one.** `apply-migrations.sh` pinned 4.127.0 separately because 3.90.0 cannot parse `--cwd` and answers with a usage dump the gate correctly refuses on. That reasoning has expired, and its header says so rather than describing a `@v3` that is gone.

**The dry-run's `[ -n "$version" ] || version=3.90.0` fallback is gone too**, because it was the same hazard wearing a belt: a `sed` that stopped matching would have bundled against a hardcoded version rather than saying so, under a comment reading *"same wrangler version as the deploy action, or the point is lost"*. It fails and names the line now.

#### A comment that had been right about v4 and would have been wrong about v7

`upload-artifact`'s step carries the repository's most-cited failure: `include-hidden-files` is set because without it the step **had never once uploaded anything** — the path is `.visual-regression/`, dot-prefixed paths are excluded by default, `if-no-files-found` defaults to `warn`, so it logged "No files were found" and went green on every failing run for as long as it existed. The comment saying so named **v4**, and three majors landed in between.

Re-read out of `v7`'s own `action.yml`, both defaults survive. **Then proved rather than left at that**: a temporary commit copied one golden over another so the comparison genuinely failed and the step actually ran, beside a twin byte-identical to it but for the one line under test. The twin failed with `##[error]No files were found with the provided path: .visual-regression/`; the real step uploaded **9 files, 867,576 bytes**. The artifact was downloaded and listed rather than trusted to the log — eight captures under `actual/` and one diff PNG, for `shell-villain-light`, the single page whose golden had been corrupted, which is the positive control that the harness measured what it was aimed at.

#### A package bump moved a static-analysis finding in a file it does not touch

**A whole-tree Qodana scan goes 2 → 5 on #110.** `AdminPageTests.cs` calls `.Input(...)` at three sites inside `async Task` tests and ReSharper now reports `MethodHasAsyncOverload` on each; bunit has shipped `InputAsync` with an identical signature since 2.0.66, so this is the analyzer resolving something it did not resolve before rather than a new API. **Which package moved it is not the interesting part — that CI could not have seen it is.** Qodana runs on `main` and weekly, and in pull-request mode over changed files even then; this bump changes six `.csproj` files and nothing under `tests/ProwlersAndParagons.Web.Tests/`. The local whole-tree scan `CLAUDE.md` requires was the only thing that was ever going to report it. Fixed by awaiting the overload, and proved load-bearing: pointing one site at a different address turns the test red (768/769) and restoring it turns it green.

**Spectre.Console 0.49.1 → 0.57.2 is eight minor versions inside a group named "minor and patch", and the front end it renders has no harness.** What could be measured, was. 0.55.0 names three breaking changes; the clean CI-strict build rules out the two compile-time ones. The third is behavioural — ANSI output disabled when stdout or stderr is redirected — and a probe against both versions in an identically redirected environment reproduces it: 0.49.1 reports `Ansi=True Links=True ColorSystem=TrueColor`, 0.57.2 reports `Ansi=False Links=False ColorSystem=EightBit`. **Both report `Interactive=False`, which is the half that matters**: `InteractiveTerminal.IsAvailable` reads `Capabilities.Interactive` and nothing else, so the wizard's entry gate is unmoved, and the capability that moved belongs to a mode the wizard refuses to start in. The wizard rendering in a real terminal remains unmeasured — that is the standing CLI gap, not something this bump introduced.

#### The scan that was skipped, and what it was hiding

**`main`'s whole-tree Qodana scan reported 2, not the 0 the Static analysis row claimed** — both `InvalidXmlDocComment`, one unclosed `<para>` in `EveryScriptAWorkflowRunsDirectlyIsExecutable`'s doc comment, which arrived with that guard in #114 and had been reported by nothing for four days. Fixed in #116, which is not a Dependabot pull request at all and exists only because the first thing this slice did was re-measure a figure instead of quoting it. Qodana came off pull requests deliberately, on the reasoning that the local scan `CLAUDE.md` requires runs first — **this is what one skip of that step looks like**, and the answer is to run it, not to put the workflow back. A sweep of every `.cs` and `.razor` file for an unbalanced `<para>` count finds no other file.

#### The one that was not a chore

#111 and #112 are closed, not merged and not shelved silently: the measurements are in item 20 with a working recipe, including the one genuinely good piece of news — under Microsoft.Testing.Platform a crashed test process no longer prints `Passed! - Failed: 0`, so the trap `docs/guide/testing.md` is built around is closed rather than reworded.

**Also merged:** `setup-node` 4 → 7, which was not cosmetic — the previous production deploy carried *"Node.js 20 is deprecated … actions/setup-node@v4"*. Read out of the run log rather than assumed: `node: v22.23.2`, and the annotation is gone.

Five suites: **4042 / 769 / 231 / 14 / 19 = 5075**, the one new test being `WranglerIsPinnedToOneVersion`. `dotnet build --configuration Release -p:ContinuousIntegrationBuild=true` at 0 warnings; whole-tree Qodana at 0. Both production deploys green.

### A campaign holds a clone of a character, and a player's edits arrive as an approval request

**Item 11's first of eight is closed, screens and all** — and the shape it closed with is not the
one that entry describes. That entry says *single-user first, no sharing, because that needs nothing
new from the server*. The owner has since decided the shape: fork and pull request, for characters.
A player builds freely in their own rows and needs nobody's permission to do it; when they want a
change to count at the table they *send it for approval*, which writes a snapshot into the campaign;
the GM reads a field-level diff and accepts or rejects the whole snapshot; accepting replaces the
campaign's clone. Both sides keep a copy. So sharing is in, the server did need something new, and
the entry above is superseded rather than merely extended.

**What made the previous half worth nothing until now is worth naming once more.** The storage half
— `Campaign`, `CampaignJoin`, a `campaigns` table, four routes — shipped complete, tested, and
**unreachable**: no screen in the application could create or see one. That is the fault this
repository keeps hitting, and it is why the entry above said "no screen draws one yet" as though it
were a status rather than a defect.

#### The three screens

`/campaign` is the third avenue, behind `Run`. It lists the games somebody runs and the ones their
characters are in, names a campaign, shows and rotates its join code, and joins one by a code read
out at a table. `/campaign/{id}` is the approval screen: the diff, Approve, Reject, and the full
sheet under it. The character manager and `/sheet` carry a **standing** — `Approved for Nightfall` /
`Changes pending` / `Not submitted` — which is the answer to "which sheet do I print at the table".
One line on the tier page says which campaign and what was inherited, and it is deliberately not a
second budget strip.

`MainLayout`'s own note had said a third avenue would cost one `NavLink` and that a door onto an
empty room is worse than a wall. It cost one `NavLink`.

#### Storage: `0006_campaign_membership.sql`, and why the clones needed a table

**The clones are not in `characters`.** That table's cap is
`SELECT COUNT(*) FROM characters WHERE user_id = ?`, so a clone stored there would count against the
GM's own `character_limit`: a GM with six players would hit their five-character cap before building
a single NPC. `campaign_members` holds the clone, the snapshot waiting for a decision, and the
version those decisions name. There is a test that six approved clones leave the GM's five slots
untouched, with the cap's own 409 asserted beside it as the control.

**The approval slot is version-checked, and skipping that is a defect rather than a missing
nicety.** The GM reads snapshot A, the player resubmits B while the diff is on screen, the GM
presses Approve — and B, which nobody has looked at, becomes the clone. So a submission increments a
version, a decision sends that number back, it is in the `UPDATE`'s `WHERE`, and a mismatch is
refused **with the newer snapshot attached** so the screen can redraw. It is a compare-and-swap and
not a history: one slot per character per campaign, resubmitting overwrites, no rollback.

#### The rules this slice settled, so nobody re-litigates them

- **Campaigns are account-only, and the local half is deleted.** A campaign kept in one browser can
  never receive a submission, hold a clone, or be joined by the code it would advertise — so an
  anonymous campaign is a promise to somebody who can never be told. `SavedCampaigns` is gone and
  `pp.campaign.v1` is unused; **nothing was lost, because no screen had ever created one.** A
  *character* still has a browser half and always will. This reverses the storage slice's own
  "the character trio again, one level up".
- **Whole snapshots, never one field.** Partial application is a merge algorithm for characters — a
  second engine, capable of producing a sheet neither person authored. `CampaignDiff` has no method
  that could apply a row and a test asserts there is none.
- **The diff says how much it compared, on the screen.** A diff showing nothing and a diff that
  failed to run are indistinguishable, and "nothing changed" is the commonest honest answer this
  screen gives. `CharacterDiff.Compared` counts fields *examined*; a comparison that has stopped
  comparing reports zero, which is a red test and a visibly wrong sentence rather than a reassuring
  empty list.
- **Every diff row is in the book's words** — `Tier: Standard → High Level`, `Might 6d → 8d`,
  `added Flight 4`. The guard is stated as a shape rather than a list of ids: no underscore reaches a
  screen. An id the data does not know is printed as itself, deliberately, because a diff full of
  rows called "Unnamed" tells a GM nothing.
- **A tier disagreement is ordinary traffic and reads as information.** The owner's campaigns climb
  tiers in play. Nothing is repaired either way: raising the tier turns an illegal character legal in
  silence, and lowering it moves Resolve.
- **The Trait Cap is still reported and never enforced**, and there is still no `TraitCapOverride`.
- **Deleting a campaign leaves its memberships**, no cascade — a cascade would delete the clone the
  GM accepted on the strength of one click. Deleting an *account* does cascade, on both columns.
- **Nothing bumped `StoredCharacter.CurrentVersion`.** It is 1.
- **The server still never parses a character.** Both payloads round-trip byte for byte, including
  `{}`, `[]`, `123`, `null`, a bogus tier and no tier at all. **Nothing compares two, either** — the
  diff is the browser's, computed by the engine.
- **Every statement stays scoped to whoever is asking, with one exception that is stated rather than
  buried.** A membership names two accounts and each side's queries carry its own column. The
  exception is the join-code lookup, which reads a row the caller does not own — **because that is
  what a join code is**: a secret the GM minted and handed out, holding it being the whole of the
  authorisation, the same shape as holding a sign-in link. It answers the campaign's settings and
  nothing else — no account id, no character, no other member — is rate limited per account, and
  answers an unknown code and a replaced one byte-identically. Joining by a shared code cannot be
  built any other way; see `docs/CHARACTERS-API.md`, which records it.

#### Out of this slice, deliberately

Notifications beyond the waiting count, a GM editing the clone directly, approval history or
rollback, removing a player, transferring a campaign. Adversity still appears in no code — that is
still a loose end of item 11 and this slice did not touch it.

**Two of those are no longer out, and one is narrower** — see the entry above this one. Leaving and
removing are built; a decision is now a fact on the row rather than only its consequence, so a
rejection is visible to the player waiting on it. **That is not the same as notifications**, and
the sentence above still holds for the rest of what it meant: there is no mail, no badge outside
the campaign screens, and nothing interrupts anybody. **Approval history and rollback stay
deliberately out**, unchanged by `0007`, which records the *last* decision and not a log of them
for the same reason the pending slot is one slot. A GM editing the clone directly and transferring
a campaign are untouched.

#### Three findings worth carrying, all from breaking a guard and watching it

1. **A seam in the wrong place is a test that races nothing.** The render test for the stale-approval
   refusal first put the resubmission between the click and the request, through a hook on the API
   stub. Mutating the page to send the *current* version rather than the one it drew — the exact
   defect — walked straight through it, because the broken page's extra read happens *before* such a
   seam can fire. The real race is between the diff being drawn and the button being pressed, which a
   test drives with no seam at all. The hook was deleted: one nothing races reads as a guarantee.
2. **A fixture can hide a mutation three ways in one test.** `NoRowNamesAFieldOfAStoredCharacter`
   used a made-up Flaw id (so the diff correctly printed it back), then a real one whose printed name
   is the same word (so a case-insensitive check could not tell a leak from a lookup), then a tier
   whose id has no underscore (so a mutation printing ids walked through the general rule). Each time
   the fault was the fixture, not the guard.
3. **`RedundantJumpStatement` found a real bug no test did.** The approval page's overtake check sat
   *after* the write, where it does nothing — a second navigation mid-read left the wrong campaign
   under the right address, verbatim the fault `SheetPage` records. Qodana saw it; nothing else did.

#### An audit of the slice found three more, and two are fixed here

1. **`campaign_members_one_per_character` was missing `gm_user_id`, and that was a cross-account
   defect rather than a tidiness point — fixed.** `campaigns` is `PRIMARY KEY (user_id, id)`, so a
   `g_…` is unique *per account* and two GMs may hold the same one; the id is the client's, it is
   handed to every member in the join response, and it travels in an exported character's own
   `CampaignId`, so knowing one takes no work. Without the column in the key, a player redeeming a
   second GM's code conflicted with their row in the *first* campaign, `ON CONFLICT … DO UPDATE`
   handed that row back, and the join answered 200 with the second GM's label and payload — so the
   player built to the right tier and every snapshot they sent afterwards was delivered to a GM they
   never joined, while the GM whose code they redeemed saw an empty inbox. Driven against real
   SQLite before the fix and it reproduced exactly. The key now leads with `gm_user_id`, and
   `memberships.join` also *checks* the row it got back belongs to the campaign whose code was
   redeemed — the index is what makes it true, the check is the half a mutation can break. Both were
   mutated independently and each fails on its own; with both reverted the guard reproduces the
   original defect by name.

2. **The join code's hyphen never reached a reader — fixed.** `crypto.js` said the hyphen "travels
   with the code, because it is what makes ten characters readable". It did not: `normaliseJoinCode`
   takes the punctuation out on the way in, the list answered the bare ten, and the screen printed
   them — so a code minted as `Q4TWX-NPRKM` was only ever shown as `Q4TWXNPRKM`, which is exactly
   the unbroken run somebody misreads over a phone. Worse, `rotateCode` answered the *hyphenated*
   form, so the two addresses disagreed about one value and a screen redrawing from the list after
   minting showed a different string from the one the mint had handed it. The wire is now the bare
   ten everywhere and `SavedCampaignSummary.Spoken` puts the hyphen back at the point of display.

   **And `FakeApi` had been minting `AAAA1-BBBB1` all along** — a shape the server never sends. The
   fake and the server disagreed and nothing caught it, which is the drift `AccountsContractTests`
   exists for and could not see, because a code's *form* is not a field name.

3. **The diff reported "nothing changed" while the spend moved — fixed, and the guard that should
   have caught it did not exist.** `CampaignDiff` compared a Power on `PowerId` and
   `PurchasedRanks` and nothing else: not its `Pros`, `Cons`, `CostVariantKey`, `Units`,
   `BaselineTraitId` or `SourceId`, not a Perk's or Flaw's narrative detail, not
   `AbilityModifiers`, and gear only by its name — when a custom feature is the only thing gear
   costs Hero Points for. Immunity moved from one unit to six produced `spend 7 → 22`, **no rows at
   all**, and a healthy `Compared` of 7.

   **`Compared` was never going to catch it.** It counts fields *examined*, which is the right
   guard against a comparison that has stopped running and worth nothing against one that runs and
   looks at the wrong half of a field — the Power's key *was* examined. That is this repository's
   own recurring fault in a new place: a check measuring the wrong thing reads as a guarantee.
   `NoRowNamesAFieldOfAStoredCharacter` reflects over `CharacterSheet`, but for **naming**, not
   coverage.

   Each list entry now carries a detail line built from the app's own labels — `SheetView`'s
   `Name (Variant)`, `Labels.Humanise`, the rules data's own `CostUnitLabel` — so a row cannot
   print a spelling the sheet would not. And the invariant is in the type rather than in a
   reviewer's memory: `CharacterDiff.Explained` holds the engine's two figures against the rows,
   and the screen says *the total changed but this comparison cannot say what* instead of drawing
   the empty list that reads as agreement. `EveryChangeThatMovesTheSpendMovesARow` drives ten
   cost-bearing fields and `EveryChangeAGmDecidesAboutMovesARow` the seven free-but-reportable
   ones. **All four mutations go red** — a Power's detail back to its rank, gear's detail nulled,
   an Ability's modifiers hidden, and the tier.

4. **A fixture hid a mutation for the fourth time in this file's history, and the same way.** The
   gear cases *added* a customised item, so nulling `GearDetail` entirely — exactly how the slice
   shipped — left them green: an item that was not there and now is produces an Added row whatever
   its detail says, and the behaviour under test was never reached. Changing an item already on the
   fixture is the only mutation that can tell a detail comparison from no comparison. Caught by
   re-running the mutation rather than by reading the test.

5. **A deleted campaign left its memberships live — fixed, asymmetrically.** The GM's inbox still
   showed a request waiting on a game they had thrown away, with an Approve button under it, the
   detail read answered 200, and the player could go on submitting into it and be told it had been
   sent. There is still no cascade and there is not going to be one — the player's row is theirs,
   and keeping it is what makes restoring a campaign a complete undo — so the fix is an `EXISTS`
   on the GM's half alone: the inbox, the detail read, and both decisions. The player keeps their
   standing and gets a 409 naming the actual reason instead of a 404 that would read as *you have
   no such membership*. The guard asserts the restore too.

6. **`apply-migrations.sh` trusted an exit code on the one path that had no test.** Every refusal
   path is driven by `migration-gate.test.mjs`; the apply itself had nothing, so a `wrangler d1
   migrations apply` that exited 0 without applying anything was read as success — the same trust
   that produced the outage the script exists to prevent. It now re-lists and requires the gate's
   own `proceed`, asked through `gate.mjs` rather than by grepping wrangler's prose in a second
   place. Driven with a stub wrangler both ways: the honest one passes, the one that exits 0
   without applying fails the job — and the script as it was exits 0 and prints "Applied."

#### A second review, and what it found that the first did not

Three no-context reviewers were then run over the server half, the browser half and the docs, each
told the constraints and nothing about the work. Five more findings, all reproduced by running:

7. **The GM's status line was written and thrown away unrendered.** `_said` sat *inside*
   `@if (_reading == one.Id && _diff is { } diff)` on the approval page — and approving, turning
   down, and both refusals all set `_reading` to null. So a GM pressed Approve, the panel
   collapsed, and **nothing said it had worked**; the only sentence anybody could ever read was
   the stale-snapshot one, which is the single branch that keeps the request open. That is exactly
   why the stale-approval test passed while every other message on the screen was invisible. Moved
   out to the panel, where a `role="status"` live region also stops being destroyed and rebuilt
   around each announcement.

8. **A snapshot this build cannot open was called "nothing waiting".** `DecideAsync` reads the
   newer payload through `StoredCharacter`, which answers null for an envelope a later version of
   the app wrote — an ordinary thing to meet, by that class's own notes. The outcome is `Stale`
   with nothing to draw, the page's switch had no arm for it, and it fell into `NothingWaiting`:
   the GM was told the queue was empty about a request the player was waiting on.

9. **`FakeApi` was more permissive than the server, which made both of the above untestable.** The
   fake never checked whether a campaign still existed, so it kept answering 200 with the clone and
   the waiting snapshot for a game the GM had deleted, and let the player submit into it. **A fake
   that is more permissive than the server does not make tests fail; it makes them pass about a
   state the server never produces** — the same class as the `AAAA1-BBBB1` join code the fake was
   minting. Both halves of the real `EXISTS` are in it now.

10. **The diff was still blind to six fields, and three guards could not see three more.**
    `Appearance`, `Motivation`, `Quote`, `Connections`, `AbilitySources` and `TalentSources` were
    compared nowhere at all — every one of them prints on the sheet, and a Trait's Source is not
    derivable from its rank, decides which Pros an option allows, and prints inside a Power group.
    And the fixture could not reach `PerkDetail`'s narrative half, `GearDetail`'s matched pair or
    `ProConLabel`'s unit count, because the cases **added** the element rather than changing one:
    all three mutations survived. `ACostedSheet` carries all three now. **Nine mutations, all
    red.**

11. **Two API documents described a `WHERE` clause the server does not have.**
    `docs/CHARACTERS-API.md` and `docs/guide/accounts-server.md` both wrote `getMembership`'s
    scoping as a symmetric `(gm_user_id = ? OR player_user_id = ?)`. It is not symmetric: the GM's
    half additionally requires the campaign to exist and the player's deliberately does not, which
    is the whole of finding 5 above. Both now write it out, and say why. `.editorconfig`'s
    rationale for the same file said "five records" against ten and claimed nothing read them by
    name, which is false of five of them.

#### What is open in it, recorded rather than left in a pull request

- **`ApiMembershipStore.LastJoinRefusal` is mutable state read out of band** after `JoinAsync`
  returns null. Single-user WebAssembly, so it works; returning the refusal *in* the result is the
  smaller surface. **`SubmitAsync` has the same shape and one real consequence**: it answers `int?`,
  so a permanent 409 — *that campaign is no longer here* — is indistinguishable from a dropped
  connection, and `Campaigns.razor` tells the player *"That could not be sent just now. Try again in
  a moment"* about a state that will never succeed on retry. One change fixes both: a refusal on the
  result of each.
- **A GM cannot be told a submission's age**, because the two timestamps were removed from the
  detail read rather than bound — see finding 3 above; they are in the two lists, where a "sent
  three hours ago" belongs, and nothing prints one yet.

### Three sentences of the handout became a mechanism, and one described a state that cannot happen

**The handout was the stopgap and it said so.** Every paragraph on it existed because the app is
silent at that moment — a code retyped rather than carried, a Join button disabled with no reason
beside it, a rule about "the character on screen" printed where the character's name could go. This
closes the three that were mechanisable.

- **A GM sends a link.** The settings panel carries the whole URL beside the code it could
  previously only read aloud. **One click selects all of it** — `user-select: all` — because the
  owner's condition was *"text is fine as long as pasting is perfect"*, and a double-click that
  takes two thirds of a URL is not perfect. Proven in a real browser rather than asserted: a
  synthetic page carrying the shipped stylesheet, selection emptied as the positive control, one
  click, and `getSelection()` reads back exactly the 64 characters with no whitespace. Absolute
  from `NavigationManager.BaseUri`, so the same string works pasted from `localhost:5000` and from
  the deployed site; and the hyphenless code, because the hyphen exists to survive being said down
  a phone and a panel showing two spellings invites somebody to type the wrong one.
- **`?join=` fills the box and then leaves the address bar.** The removal is the point, not
  tidiness: a join code is a shared secret, and one left in the bar is in history, in a screenshot
  of the window, and in whatever the reader pastes next. A `replace` navigation drops it without
  leaving an entry to go back to. **It does not join anything by itself** — joining writes to a
  character, and a link that did that on open would be a link anybody could send to anybody.
- **The join box names the character rather than restating the rule.**

**The finding, and it is a defect rather than a polish item: the state that sentence described
cannot be reached.** `SavedCharacters.CurrentIdAsync` falls back to `LegacyId` rather than answering
null, and the join box only renders in the branch that assigns `_openId` — so a character is always
open, `disabled="@(_openId is null)"` on Join never fires, and the null guard in `Join` is inert.
The handout had a whole row explaining that disabled button. Both are gone; the row is now the tier
disagreement, which is a message the app really sends. The inert guards are kept and commented,
because the cost of a defensive null check is nothing and the cost of a sentence promising an
unreachable state is a reader looking for it.

**And a guard earned its keep on the way through.**
`NoPageSetsAnythingInMonospaceForThePlayer` bans `<code>` on any page a player reads — monospace
there has always turned out to be an internal name — and it fired on the `<code>` this link was
first written into. A URL is not code and the element was never load-bearing, so it is a `span`
now. The rule was right; nothing about it was widened.

### The handout is a static file at `/join.html`, and the scans that would have missed it

**A GM had nothing to send a player.** The three campaign screens explain themselves to somebody
already signed in, and every step before that — being invited, that an uninvited address is refused
in silence, that the character on screen is the one a code joins — was known only to whoever built
it. `web/wwwroot/join.html` is that page: invitation, sign-in, build, join by code, send for
approval, plus what a standing means and what each join refusal says.

- **A static file rather than a Blazor route, deliberately.** All of it is true before WebAssembly
  has landed and none of it should sit behind the thing it explains — a route would mean
  downloading the application to be told how to sign in to it. `_redirects` sends every unknown
  path to `index.html` with a 200, and a file that exists on disk is served ahead of that rule, so
  `/join.html` is the page. It needs no policy change either: `default-src 'self'` already covers a
  same-origin stylesheet, the self-hosted faces and `js/theme.js`, which is included so the handout
  arrives in whichever palette the reader already chose in the app.
- **It names no colour, face or size of its own**, so it is in all four palettes and prints on the
  print one. `join.css` is a second stylesheet rather than a section of `app.css`, which is the
  application's: linking `app.css` would make every visitor download the wizard's rules to read a
  paragraph, and make the handout wait on the largest stylesheet on the site.
- **The presentation scans enumerate every stylesheet under `wwwroot` now, instead of reading
  `app.css` by name.** `join.css` was outside `NoComponentNamesAColour` and
  `NoComponentNamesATypeface` from the moment it landed, with the whole suite green — a list of
  filenames goes stale on exactly the change that most needs checking, which is the failure
  `RepositoryGuideTests` already exists to prevent for the guide set. `join.html` is named
  alongside `index.html` for the same reason. Proven by mutation: a hex in `join.css` is reported
  as *"join.css names a colour by hex value"*, and the first attempt at that mutation missed its
  anchor and passed — a no-op, recorded here because it is the failure mode this discipline is for.
- **`--heading` on `--surface` joins the measured contrast pairs.** Every `h1` and `h2` in the
  application was already that pair and it was unlisted; the handout is entirely that pair, being a
  page of headings with no panel under them. Green in all four palettes on the first run, so this
  documents a gap in the list rather than a fault in the palettes.
- **`EveryTokenTheHandoutAsksForIsDefinedInThemeCss`** is the guard the enumerated scans cannot
  give. A misspelt custom property is the quietest failure in CSS — `var(--panel-sunken)` does not
  warn, leaves the declaration with no value, and draws a card with no ground, which is
  indistinguishable from a design that wanted none.

- **Merging cleanly is not the same as still being true.** `main` shipped leaving a campaign, a GM
  removing a member, and `ChangesTurnedDown` while this branch was open. Three files conflicted and
  none of them was the handout — it merged without a mark and was *wrong*: it listed three standings
  where there are now four, and told a player nothing about a control that had appeared on the
  screen it describes. Both are in it now, in the app's own words (**Leave** then **Leave for
  good**, *"Changes turned down"*). A page that describes a screen has to be re-read against that
  screen on every merge, and git will never say so.

**What is open: nothing in the application links to it.** A page nobody links to is a page nobody
finds, and the two places that want it are the campaign settings panel — where *"share its code
with your players"* is exactly the moment a GM needs something to send — and `/signin`, for a
player who arrives at the sign-in form without knowing they must be invited first. Both are one
anchor; neither is in this change, because the copy on those screens is the owner's to decide.

### The gate came back, and its own script was not executable

**The first deploy after the campaign slice merged failed at `Apply pending D1 migrations`, and
not on the credential everybody was watching.** `scripts/apply-migrations.sh` was committed
`100644`, so the runner answered

    ./scripts/apply-migrations.sh: Permission denied
    ##[error]Process completed with exit code 126

before a line of `gate.mjs` ran.

**The ordering held, and that is the thing to take from it.** The apply step sits ahead of the
Pages upload, so its failure skipped `Deploy to Cloudflare Pages` and both post-deploy checks, and
production went on serving the deployment from before the merge. A deploy that cannot ship is the
designed failure of this mechanism; a deploy that ships past an unapplied migration is the outage
the entry below records. The mechanism did its job on the first real run, for the wrong reason.

**It also means the D1 permission question is still open.** The gate never got as far as asking
Cloudflare anything, so the 7403 that reverted PR #104 has been neither reproduced nor cleared. And
the permission needed is **D1 Edit**, not the Read this file used to say: true of #104's step,
which only listed, and not of the one that came back, which also runs `wrangler d1 migrations
apply`. Cloudflare's reference carries both one line apart, and a token holding only Read refuses
on every deploy while looking identical to a token holding neither.

**Nothing on the machine it was written on could have caught it.** Git for Windows does not honour
the mode bit in the working tree, so `./scripts/apply-migrations.sh` runs perfectly here and fails
on every Linux runner. That is the same shape as the credential lesson in the entry below — *what
answers on the developer's machine says nothing about what CI holds* — arriving a second time in
the same file, one section apart, on the same script.

`WorkflowFilterTests.EveryScriptAWorkflowRunsDirectlyIsExecutable` collects every `run: ./….sh` out
of the workflows themselves, so a script added later is covered without anybody remembering the
test exists, and reads the mode out of `git ls-files --stage` rather than off the filesystem —
the only place the answer is the same on both platforms. Of the twelve scripts, exactly two were
`755`, and they are exactly the two a workflow already ran this way.

**Watched to fail three ways**: the real defect restored, the same bit cleared on
`write-cloudflare-headers.sh` which was already right, and the scan mutated to match nothing — the
last because an empty set would otherwise pass, which is how four guards in this repository have
shipped hollow.

#### And then it went green, which answered one of the two questions

The deploy after the fix ran every step: the gate read the live database, found nothing pending,
skipped the apply, uploaded to Pages, and both post-deploy checks passed —
`prowlers-and-paragons-chargen.pages.dev` live and `/api/me` answering `401 application/json`.

    ✅ No migrations to apply!
    Every migration in d1/migrations is already applied to the remote database.
    Nothing pending; skipping 'wrangler d1 migrations apply'.

**Two things to read out of that, and only one of them is what it looks like.**

- **The token can reach D1**, so the 7403 that reverted PR #104 is cleared. But **nothing was
  pending, so the apply path never ran** — this establishes *Read*, and a token holding only Read
  produces exactly this log before failing on the first migration that actually has to be applied.
  The Edit half was untested at the time, and was tested by the next migration rather than by
  anything written here — `0007_decision_recorded.sql`, applied on the deploy of `a978806`.
- **`0006` was already applied**, by hand, by the owner. So the Accounts row's "0006 is pending —
  measured against the live database" was *right when it was written* and stale by the time it
  merged. **That is worth recording rather than quietly correcting**, because it is the honest
  shape of a measured figure in this file: it did not rot through carelessness, it rotted because
  somebody did the thing the gate exists to automate. The row now carries the deploy's own reading,
  which is the one figure that re-measures itself on every push.

### A migration that was merged but never applied took character saving down in production

**The owner reported it from the live error log**, which is the only instrument that could have.

`0005_campaigns.sql` added `characters.campaign_id`. The migration was written, reviewed, tested and
merged; the deploy shipped the Worker; **nothing in this project applies migrations to D1.** So the
code went live asking for a column that existed only in a file, and production answered
`D1_ERROR: no such column: campaign_id` on `/api/characters` and
`table characters has no column named campaign_id` on `/api/characters/{id}`.

**The blast radius is the part worth remembering.** `campaign_id` went into the `SELECT` and the
`INSERT` for *ordinary character saving*, so a feature nobody was using yet broke the feature
everybody uses, for every signed-in reader. **29 failures**, not the 11 first reported — the counts
kept climbing while it was being diagnosed.

#### Every check passed while it happened, and none of them could have failed

- **`./scripts/test-worker.sh` runs 192 tests against real SQLite — and builds its schema by running
  the migrations.** It cannot notice that production's schema was never built. **A suite that
  constructs the world it tests cannot tell you the real world differs.** Same shape as the defect
  recorded below where every unit test called the store directly, so none could notice that nothing
  else did.
- **The post-deploy smoke check passed and was right to.** It asks `/api/me`, which touches no table
  this migration changed. It proves the server is wired up; it says nothing about whether the schema
  matches the code just deployed.

#### The fix, and the guard that could not be kept

The migration was applied by hand — additive only, a nullable column and a new table, no data
touched — and the error counters froze at 12 and 17 with identical `last_at` across two checks
minutes apart. **Confirmed stopped by measurement rather than by the absence of new reports.**

A step that **refuses to deploy** while a migration is unapplied was then built, driven through all
four of its branches, and merged. **It was reverted on its first real run, and the reason is a
finding rather than a mistake in the step.**

The deploy's `CLOUDFLARE_API_TOKEN` has no D1 permission — wrangler answered *"The given account is
not valid or is not authorized to access this service [code: 7403]"*. The step could not tell
whether migrations were pending, so it refused, **exactly as designed**. But refusing meant `master`
could not deploy at all, which is a worse failure than the one it prevents, so it came out.

**It worked locally and could never have worked in CI, and that gap is the lesson.** The token this
machine holds is the owner's own, with access to everything; the deploy's is scoped to Pages. *A
credential that answers on the developer's machine says nothing about the one CI holds* — the same
shape as a test that builds the world it tests, one layer out.

- **It returns when the deploy token can reach D1.** The step is in PR #104's history and needs no
  redesign, only credentials. **This said D1 *Read*, and Read is not enough** — true of the step as
  #104 shipped it, which only *listed*; the version that came back also runs
  `wrangler d1 migrations apply`, so it is **D1 Edit**, alongside what the token has for Pages.
  Cloudflare's permission reference has both: *D1 Read — grants read access*, *D1 Edit — grants
  write access*. A token given only the first refuses on every deploy, correctly, and looks exactly
  like a token given neither.
- **It must not be "fixed" by treating an unauthorised answer as all-clear**, which would turn the
  guard into the thing it exists to prevent.
- **Two properties worth keeping when it comes back.** An unrecognised answer fails, because the
  step reads wrangler's prose and a reworded release must not read as clear — driven with four
  canned answers, clean passing and pending, unfamiliar and *empty* all failing. And it is pinned to
  a wrangler whose output was read: `3.90.0`, what the deploy bundled with at the time, answers this command
  with a **usage dump**, which would have been an unrecognised answer on every single run.

**Until then the discipline is manual**: after merging anything that adds a file under
`d1/migrations`, run the apply. Nothing reminds you.

**Also done while in there, at the owner's request:** their account's `character_limit` raised from
5 to 1,000,000. There is no unlimited sentinel — the column is `INTEGER NOT NULL` and the check is
`count < character_limit` — so this is a number nobody reaches rather than an infinity.

### A chapter of the book can be searched on its own, and the rules index stopped being inert

**[Item 12](#12-the-interface-the-owner-asked-for-which-needed-none-of-item-11s-answer)'s second
smaller thing, and the half that was left open because it needed the server.** `/rules`' "What is
here" panel listed ten chapters, looked like a list of links, and was not one. Each row now runs the
box's query scoped to that chapter, and the passage counts are gone.

**The narrowing is on the server because it cannot honestly be anywhere else, which is the argument
the open bullet already carried and which held up.** `MOST_RESULTS` is 30 and no caller may raise
it, so filtering the response would be filtering what survived a cap — a chapter with real matches
outside the best thirty comes back empty, and `found` becomes a count of the whole book presented as
a count of the chapter. **That is not hypothetical and a test names a measured instance**: forty
passages in Ch.8 use "vehicle" and the unscoped thirty rows are filled out of Ch.2 and Ch.6 before
reaching one of them. The pair was chosen by running twenty ordinary queries against the real corpus
and asking which chapters had matches and appeared nowhere in the top thirty; it had the widest
margin of those it found.

- **`chapter=N` is a filter on the input, not a parameter threaded through the ranking.**
  `search(chapters, query, limit)` already took the chapters it ranks as its first argument, so a
  scoped search is the same search over a smaller book and nothing inside the matching rule knows
  the option exists. **That is what makes `found` and `nothingMatchedByHeading` describe the scoped
  set for free** — they are computed over the whole result and only then is the list cut, the
  ordering `search.js` insists on for its own recorded reason, and that ordering was not disturbed.
  A test asks for one row of a scoped search and asserts the count and the flag do not move.
- **A bad chapter is a status code, never an empty result, and that is the decision worth keeping.**
  `found: 0` is this API's one way of saying *the book is silent on this*, which the page prints as
  a sentence about the rulebook. Answering it for `chapter=99` would make that sentence a claim
  about the text told on the strength of a typo — the same "a search that always answers reads as
  an answer" fault the page was designed against. Not a number is `400`, a number the book has no
  chapter under is `404`: the pair `/api/rulebook/passage` already gives, for the reason it already
  records.
- **The per-chapter array is held, and keyed on the corpus as well as on the number.** `search.js`
  keys its index on the *identity* of the array it is handed, so a fresh `filter()` per request
  would walk that chapter's prose every request; and a cache keyed on the number alone would answer
  a second corpus out of the first one's entry — the exact fault `corpusIndex` one file over carries
  a comment about. Driven in a test through the route rather than through the ranking, since that is
  where the cache is.
- **The counts are dropped from that panel only, and the ban is scoped to it deliberately.** The
  instruction as first written was "no count of passages appears anywhere on `/rules`", and that
  would have killed `Summary()`'s "12 passages, best 5 first" — a different number doing a real job,
  found against shown, which is the honesty the whole page is built on. The guard reads that panel
  and asserts the results panel still prints its figure, which is also the positive control: a page
  whose panels had all gone would satisfy the absence.
- **A row cannot be pressed with an empty box.** The server answers an empty query `found: 0`, so a
  row that ran on one would tell a reader Ch.4 is silent because they had not typed yet. The panel's
  aside says what the rows want. **Searching the chapter's own title instead stays rejected** for
  the reason the open bullet gave: a row labelled with a chapter that answers with hits from three
  other chapters is the original complaint in a new spelling.
- **A scoped answer says where it looked, and the box is the way back out.** The results panel is
  headed "What Ch.4 says", a scoped miss names the chapter and points at Search, and submitting the
  form always clears the scope — a narrowing that survived the next query would answer a new
  question out of a chapter chosen for the old one, with nothing on screen looking wrong.
- **Ten mutations, each watched red.** Scoping made a no-op (5 worker tests), `found` taken from the
  whole book (2), the 404 turned into an empty result (1), the sign-in gate narrowed off the prefix
  (2, including the new scoped one), the page dropping the chapter from its request (2 bUnit), the
  passage count put back in the panel (1), the scope surviving a fresh search (2), the rows enabled
  on an empty box (1), the results heading never naming the chapter (2), and the scoped miss
  over-claiming about the whole book (1).

**`rules-reference.png` moves** — the panel is a different shape. It is deliberately not regenerated
here; that is `visual-goldens.yml` on the runner, and a golden updated as a side effect of something
else is a regression signed off by nobody.

### The Hero Point limit is two cards, and the label that flipped is gone

**[Item 12](#12-the-interface-the-owner-asked-for-which-needed-none-of-item-11s-answer)'s first
smaller thing, built as the owner designed it.** The limit was one button inside a full-width
panel, and the chrome — a heading, a rule and an "Optional" aside — outweighed the control about
four to one. It is now **its own two-option group under a rule**, drawn in the tier grid's idiom,
on the same page.

**They are not tiers 7 and 8, and the separation is the design rather than a detail.** A tier is a
pick-one-of-six; the budget limit is an orthogonal boolean, and **you still pick a tier in sandbox
mode because the Trait Cap still applies**. Dropping the pair into the grid above would say the
opposite in one move, so the rule and the heading are what carry "this is a different question" —
the same `border-top` idiom `.make-another` uses to separate the two ways to make a character from
the list of the ones that exist. There is a test that the six tiers are still there without a
limit, that the step still refuses to go on until one is chosen, and that `TRAIT_ABOVE_CAP` still
fires in the sandbox.

**And it fixed a real defect for free, which is the part worth keeping.** The single button's label
flipped between naming the *action* and naming the *state*: off it read "Hold me to the tier's
budget", which is what pressing it would do, and on it read "Building without a limit", which is
what was already happening — so a glance could not tell which of the two it was reporting. Two
cards, both always visible and exactly one selected, cannot be ambiguous that way. Both labels now
name a state — *Building to the tier's budget* and *Building without a limit* — and there is a test
on the parallel construction, because the shape of the words is the fix and not a matter of taste.

- **`OptionRow` grew an optional `Pressed`, written out as a string.** Blazor drops a false bool
  attribute and renders a true one as `aria-pressed=""`, which assistive technology reads as *not*
  pressed, so the natural spelling announces the opposite of the state in both directions. Both
  mutations were watched: dropping the `"false"` spelling and binding the bool directly each turn
  four assertions red. It is refused inside a listbox, where `aria-selected` already says which row
  is the answer — two answers to one question is what a row carrying both would be.
- **The tip is gone and its sentence is printed on the card.** That is the rule a row which says
  its own piece already follows, and what the tip answered — whether switching the budget off stops
  the tool checking the character at all — is now read without hovering for it.
- **Nothing else about the character moves.** Asserted as the whole exported sheet before and after
  the click rather than field by field, because "nothing else moved" is not a list of fields to
  keep up to date. The engine is never told about the flag, so its findings do not move either, and
  `PresentationFlagsTests` still holds `UnlimitedBudget` out of `engine/` and `sheets/` — a
  mutation making the Trait Cap check read it turned that test *and* the new one red.
- **Choosing the card already chosen is not an edit.** A pair of cards is not a toggle wearing two
  hats: pressing the state you are already in is not a way to leave it. Read off the session's
  version counter, which the autosave and both undo buffers key on — an edit recorded there would
  close an undo window somebody was still inside.
- **`StartAnotherTests` now reads the tier cards rather than scanning the whole page for
  "Selected".** One of the new pair is selected at all times by design, so a markup scan finds its
  word and calls that test red for a reason that has nothing to do with tiers. It kept its positive
  control.

**The four `shell-*` visual goldens move**, because the shell proofs render this page inside
`MainLayout`. They are deliberately not regenerated here — that is `visual-goldens.yml` on the
runner, and a golden updated as a side effect of something else is a regression signed off by
nobody.
### One bar, two sides, one baseline — and the alignment was the symptom

> *"can we get a more uniform looking header bar? Search is vertically elevated"* — the owner

**Both halves of that report were true, and only one of them was arithmetic.** This is
[item 12](#12-the-interface-the-owner-asked-for-which-needed-none-of-item-11s-answer)'s third and
fourth bullets — *"account and settings move to the right of the banner"* and *"the Hero/Villain
switch moves into that settings menu"* — closed, plus the alignment defect that prompted them.

#### The measurement, which said the obvious fix was not the fix

In the shipped banner the three plain `.banner-link`s — Build, Rules, the account — had their text
line centred at **29.13px**. The SEARCH label sat at **27.88px**, 1.25px above, and was the single
most elevated item in the row.

**`align-items: center` was doing exactly what it says.** Every item's *box* was centred on
30.30px, correctly, to within a hundredth of a pixel. What differed was how far each item's *text*
sat from its own box centre — and that is a number no rule states and no source scan can compute:

- an ordinary link is skewed **1.17px up** by the underline hanging below its text;
- `.palette-open` is skewed **2.42px up** because its own `align-items: baseline` pins the label
  flush to the button's top edge while the `.key` boxes' border and padding hang below the shared
  baseline.

Four candidate fixes were measured. **`align-items: baseline` on `.banner-inner` lands the spread
at 0.00px** and is what shipped. `align-items: center` on the palette button moves the error to
+1.00px, a wash. A tuned `line-height` on `.key` reaches +0.01px and was **rejected**: it is a magic
number depending on three tokens, so it would rot the first time any of them moved, silently, in
exactly the direction that is hard to see.

And one thing the measurement ruled out before anybody built on it: `.mode-switch`, `.theme-switch`
and the character pill were **all 30.34px tall, identically**. They did not differ in height. What
differed was horizontal padding — `--space-3` on one, `--space-4` on the other.

#### The cause, which is the larger half

Nothing in the row lined up because **nothing in the row was the same kind of thing**: a wordmark,
two underlined links, a bare button carrying two key boxes, and two bordered pills with different
horizontal padding — five ways of drawing a control, inside about eleven centimetres. **Tuning the
alignment of five idioms leaves five idioms.** So:

- **Two sides.** Left is what the site is and where you can go — the wordmark, then the avenues.
  Right is the tools. **The character's own two things sit between them, attached to each other**,
  separated from the tools by a wider gap and a hairline. The bar used to be seven controls in a
  flat list with one `margin-right: auto` after the wordmark, which said only that six of them
  were "not the title".
- **The Hero/Villain and Light/Dark/Auto switches moved off the bar into a settings menu.** This is
  the move that does most of the work: it takes the idiom count from five to three and **deletes**
  the mismatched-padding problem rather than tuning it. Off the band there is nothing to fit
  around, so they are the same control twice. Neither is something a reader reaches for often — an
  identity once per character, a theme once per person — and both were sitting permanently in the
  most valuable strip on the page at the size of a primary control.
- **Search, the account and Settings are one idiom**: same face, size, tracking and optical line,
  **none of them underlined, because none of them is a destination**. Search opens an overlay,
  Settings drops a menu, and an account is an identity rather than a place. A caret — drawn in CSS,
  never a glyph — marks the only one that opens a menu.
- **"Saved" moved next to the character switcher.** It reports a write of the *document*; beside
  the account link it read as a comment on whoever was signed in, which on a shared machine is the
  reading that matters.

**It completes a decision `docs/guide/browser.md` already records rather than reversing one.** "A
rules search is not a Hero or a Villain" is why the subtitle names the palette in the builder and
nowhere else — while the switch that *sets* it sat on every route regardless, which is the same
fault one band up.

#### Two things the design was checked against and one it was corrected by

- **The live region is *not* gated on the builder, though the switcher beside it is**, and the
  approved design would have moved both. `.save-status` carries the undo offer for four acts that
  replace the character **wherever the reader is standing** — the tier page, the portfolio's two
  sample buttons, a recording that then navigates away from itself — so it has to live somewhere
  every one of those routes draws, and only this band does. Two of those routes are outside the
  builder. Gating it with the switcher would have taken the offer off the two that raise it most.
  `TheSaveRegionSitsWithTheCharacterAndSurvivesLeavingTheBuilder` pins the asymmetry, asserted on a
  route where the switcher is absent — or the claim is untested.
- **No third door.** `.avenue-nav` is a flex list of links and `Areas.Of` already reads the first
  path segment, so the avenue item 12 reserves for running a game is one `NavLink`. It is not here,
  because there is nothing behind it yet and a door onto an empty room is worse than a wall.
- **The switches are on `--panel` now, not on `--primary`**, which is a real change rather than a
  re-skin: `--on-primary` is the only ink that reads on the banner's fill and is invisible on the
  menu's. Unpressed is `--ink` on `--panel`, hover is `--panel-sunk`, pressed is `--on-primary` on
  `--primary` — all three already in `EveryScreenPairInUseHoldsItsContrastFloor`, so no unmeasured
  pair was introduced in any of the four palettes.

#### The guard is a browser, because nothing else can answer this

`proof-align.html` measures the text **baseline** of every one-line item in the band and requires
the spread under 0.5px. Three things about it are the point:

- **The baseline, not the line-box centre.** A line box's height follows its font size, so two
  items genuinely sharing a baseline in two sizes measure several tenths of a pixel apart — and the
  banner has two faces and two sizes in it, so that error is not hypothetical. A zero-sized
  `inline-block` probe resolves its own baseline to its single edge, which is the line's baseline,
  exactly and independently of the face. No computed style exposes that number and no range box
  gives it.
- **The positive control is the count, not the spread.** A spread over an empty list fails, but
  over a *single* found item it is 0.00 and passes — so a banner that had lost five of its six
  controls would report a perfectly aligned row. Mutation M2 below is that exact state, and the
  harness said `FAIL` at `items 5 of 6` with a spread of 0.00.
- **A twin, driving the byte-identical script**, against a shell with `align-items: center`
  injected — the owner's reported defect, reproduced. CI requires the real page to say `PASS` and
  the twin to say `FAIL`. **Measured: PASS at 0.00px, twin FAIL at 1.00px.** Nineteen browser
  verdicts became twenty-one.

**Two exclusions, named on the page itself so a reader of the dump knows what was not measured.**
The wordmark is two lines — the name and the subtitle — so it has no single text line to be on, and
`app.css` gives it `align-self: center` for that reason; putting a two-line block into a spread of
one-line baselines compares two different things. The save region renders no text between saves, so
on this page it has no baseline rather than a wrong one.

#### Guards, and the mutation that broke each one

Every mutation was applied to **committed** work, confirmed to have matched with
`git diff --numstat`, watched to go red, reverted, and the suites re-run after the revert.

| # | Mutation | Guard | Observed |
|---|---|---|---|
| M1 | `.banner-inner` back to `align-items: center` | `proof-align.html` | **`ALIGN: FAIL`**, spread 1.00px, three distinct baseline groups. The C# suites stayed **green** — 4026 + 664 — which is the whole reason this harness exists |
| M2 | `<SettingsMenu />` deleted from the bar | `proof-align.html`'s positive control | **`ALIGN: FAIL`** at `items 5 of 6` with the spread a perfect **0.00px** |
| M3 | `Pressed` always `"true"` | `ExactlyOneModeButtonAnnouncesItselfAsPressed` | 2 failed |
| M4 | `.settings-menu` removed from `@media print` | `TheSettingsMenusControlsDoNotPrint`, `ThePrintedSheetLeavesOutTheToolAroundIt` | 1 failed in each suite |
| M5 | `<SettingsMenu />` deleted from the bar | `TheBarOffersBothAvenues…OnEveryRoute` | 13 failed, all four routes among them |
| M6 | `banner-link` put back on the account | `TheAccountIsATooAndNotAnAvenue` | 1 failed |
| M7 | the live region gated on the builder | `TheSaveRegionSitsWithTheCharacter…` | 1 failed |
| M8 | `Toggle()` made a no-op | `WithSettingsOpen`, `OpenedSettings` | **42 failed.** The switch assertions do not pass vacuously on a menu that never opened |
| M9 | `aria-controls` written unconditionally | `AriaReferenceTests` | 1 failed, naming the dangling IDREF on the shut menu |

**M8 and M9 are the two worth keeping in mind.** M8 is the check on every other check in this list:
a disclosure that rendered nothing satisfies "exactly one is pressed" completely, by having no
buttons — this repository's commonest guard fault in its exact shape. M9 needed
`AriaReferenceTests` to render `MainLayout` at all, which it never had: the character switcher's
conditional `aria-controls` had been correct and **unswept** since the day it was written, and so
would this one have been.

#### What has to happen next, and I have not done it

**This change moves the visual goldens** — the banner's contents, its height and its right-hand
edge all changed, on all four `proof-shell-*` pages. They are **not** regenerated here, deliberately:
a golden updated as a side effect of something else is a regression signed off by nobody, and
`--update-goldens` from a Windows machine puts the Docker-versus-runner Chrome gap straight back.

```bash
gh workflow run visual-goldens.yml --repo SoftwareSamurai-net/ProwlersAndParagonsAutomation
```

Four pages will move: `shell-hero-light`, `shell-hero-dark`, `shell-villain-light`,
`shell-villain-dark`. Look at the images before committing them.

### A hung CI step spent six hours of the org's allowance, and nothing was stopping it

**Found by reading a failure rather than re-running it.** `Build` on the sheet-page branch reported
`fail` after **6h 0m 17s** — which is GitHub's own ceiling, not a test result. Every test step had
succeeded; the visual comparator printed `pixel-identical` for six of seven proof pages, printed
**no line at all** for `shell-villain-dark`, and sat there until the platform killed it.

**A re-run over byte-identical inputs passed**, which is what makes this worth writing down rather
than fixing quietly: it rules out a deterministic loop in the decoder and rules in very little else.
The honest position is that the cause is open and the bleeding is stopped.

- **`timeout-minutes: 30` on the job**, not on the step that hung. Capping that one step fixes the
  instance and not the class. **The default is unlimited**, so any hang anywhere — Chrome not
  exiting, a deadlocked test, a stalled runner — costs six hours before anybody is told, and looks
  exactly like a job that is merely slow.
- **`scripts/visual-regression.sh` caps each comparison too**, and that is not redundant: a
  job-level timeout says *the build hung*; this one says **which page**. `timeout`'s exit code 124
  is reported as its own finding rather than folded into a mismatch — a picture that changed and a
  comparison that never finished are different facts, and only one of them is a regression.
- **Broken and watched to fail**, with a `while (true) {}` at the top of `diff.mjs`: all seven pages
  reported the deadline by name instead of the run hanging. Restored, and the real comparator then
  passed all seven against the committed goldens, `shell-villain-dark` included.
- **The deadline is one variable** used by both the `timeout` and the message that quotes it. The
  first version hard-coded "120s" in the message beside a separate literal — a claim with a shelf
  life, which is the shape this file records being bitten by repeatedly.

**Still open:** why one comparison hung. `scripts/visual/png.mjs` is a hand-written PNG decoder that
had no tests at all until recently, and it is the prime suspect; it is now bounded rather than
understood.

### The sheet gets its own address, and it is not the one that was retired

> *"Id like to add a pure 'Sheet' view page to the application"* — the owner, 2026-08-27

**`/sheet` is the character being built; `/sheet/{id}` is any saved one.** Both draw the banner and
then the sheet, and nothing else: no step band, no Hero Point strip, no character switcher, no
findings panel. It is the character as a **document** rather than as a job in progress — the thing
somebody reads at the table or hands to a printer, which is a surface [item
11](#11-answered-it-is-a-tool-for-running-and-playing-pp)'s answer makes sense of and which a tool
for building alone would not need.

#### It had to be argued against the address that was deleted three rounds ago

`/build/sheet` existed and went, and the completed entry below says why: it was **the review step
with `Explain` flipped**, so once explanations became the default it offered a route to the page you
were already on. That is a real objection to putting a second sheet address back, and it is answered
rather than ignored: **this page differs by what it omits, never by a setting.**

That is not a sentence anybody has to remember. `TheDocumentIsAloneOnThePage` refuses a `.panel`, a
`.nav-buttons` and the review step's own heading on this route — **with the sheet's presence asserted
first**, because every one of those is an absence and a page that rendered nothing satisfies all
three. Break it by putting a panel above the sheet and it goes red; that was done.

#### The chrome is dropped by the address, not by the page

**A fifth area.** `Areas.Of` answers `Area.Sheet` on the first segment, and `MainLayout` already
draws the step band, the budget strip and the switcher in `Area.Play` alone — so the page inherits
none of them **and cannot forget to**. An address under `/build` would have inherited all three by
construction, which is the whole reason this is not one.

The switcher mattering here is not incidental. On `/sheet/{id}` the sheet may be somebody else's,
and a banner naming *this* visitor's character over it is exactly the fault the budget strip was
pulled off three areas to fix. The subtitle is `Character sheet` for the same reason — deliberately
not Hero or Villain, unlike the builder's.

Mutating `Areas.Of` to answer `Area.Play` for the segment turns **nine** tests red across two files.

#### Showing is not opening, which is the whole of the correctness

**`/sheet/{id}` reads a saved character through `AccountCharacterStore.ReadAsync`, which has no side
effect.** `OpenAsync` beside it moves the current-character pointer and overwrites this browser's
anonymous slot — so a page built on it would mean that **glancing at an old character quietly
switched the app to it, and the next autosave wrote the sheet on screen over whatever was actually
open.** That is not hypothetical; it is the shape of the defect recorded in the switcher entry
below, which cost somebody a character.

`ShowingIsNotOpening` asserts the session's character and the pointer are both untouched, with the
named character's presence asserted first as the positive control. **Swapping `ReadAsync` for
`OpenAsync` turns exactly that one test red** — measured, not reasoned about.

#### Two nulls that had to stay apart

"No character was read" and "no id was asked for" both arrive as a null character. Collapsing them
renders **the reader's own sheet at somebody else's dead link**, which reads as their character
having been renamed. `Missing` is a separate flag for that reason, and deleting it turns
`AnAddressWithNothingBehindItSaysSo` red.

#### Two guards in the existing suite caught real gaps, which is the part worth recording

Neither was anticipated, and both were right:

- **`EveryRoutedPageIsReachableFromAnotherPage` failed.** Nothing linked to `/sheet`. The page was
  a working feature nobody could reach — the exact class of fault [item
  10](#10-nothing-drives-the-assembled-app--a-plan-awaiting-the-owners-approval) argues about, and
  here a test already covers it. The fix is two links in the character manager: the open character's
  block gets one, and **every other row gets `sheet/{id}`**, which is the read-without-opening the
  second address exists for. The pitch had guessed those placements; the test is what made them
  non-optional.
- **`NoParagraphOnScreenIsAnEssay` failed** at 37 words. The empty state was explaining *why* a
  character might be missing — thrown away, or a link from another browser — which is a guess the
  page cannot check, printed as though it were a finding. Cut to the reader's actual question.

#### Decided while building, so it is not re-litigated

- **`ShowBudget` is read off the character being shown, not off the visitor.** The first version
  wrote `Mode == Hero && !UnlimitedBudget`, which conflates two things that were deliberately
  split: "a Villain has no Hero Point budget" stopped being a fact about Villains when
  `UnlimitedBudget` came out of `IsVillain`, and either kind of character can carry it. It is the
  same expression `CharacterSession.ShowBudget` uses, applied to the sheet on screen.
- **`SheetView` subscribes only for the session's own character**, which is its existing rule and is
  correct here rather than incidental: a saved character is fixed for the life of the render, and
  tying it to the visitor's edits is the influence the replay renders two pages to forbid. The page
  itself subscribes, because the page *title* reads the same sheet.
- **The read is keyed on the id, not guarded by a bool.** Blazor reuses the component when only the
  route parameter changes, so a one-shot flag would leave the first character on screen under the
  second one's address — the same trap `ReplayConversation` already records.
- **The controls sit under the sheet**, so the first thing on the screen and the first thing on the
  paper are the same thing. They carry `no-print`, which the print block already hides.

#### Two adversarial reviews found six things, and the best of them was in a test

Two agents were told nothing about the change — one asked only "can this lose somebody's
character", one asked only "do these guards guard". **They agreed on one defect neither was told
about and each found things the other did not.**

- **A read that lost a race wrote its answer anyway, permanently.** The address was claimed
  *before* the await and never re-checked, so a second navigation arriving mid-read let the losing
  read overwrite the winner — and because the key already held the new id, nothing would ever
  re-read to correct it. It stuck until the tab was reloaded. The reviewer built a probe and
  demonstrated three shapes; the worst is `/sheet` announcing *"nothing is saved at that address"*
  over a character somebody was actively building. **It is a signed-in fault above all**, because
  there the read is an HTTP round trip rather than one synchronous storage call.
- **"Could not be read" was printed as "does not exist".** `ReadAsync` answers null for a discarded
  character *and* for a 401 whose session expired while the tab sat open, a network failure, and an
  unreadable payload. Its own remarks say to treat that as **unknown rather than as nothing** — and
  this was the first caller to do the opposite, in a sentence shown to somebody whose character was
  sitting safe on the server.
- **The identity switch acted on a character that was not on screen.** Those buttons always write
  to the character that is *open*; `/sheet/{id}` draws one that is not it, so pressing "Villain"
  recoloured the foreign sheet while silently flipping and saving `IsVillain` on somebody else's.
  The reviewer's phrasing is the fair one: **the page's own subtitle is deliberately not "Hero" or
  "Villain" because the sheet may be somebody else's, and then the two controls acting on exactly
  that conflation were left in the band above it.** It is now the builder's, like the step band.
- **`SheetView`'s subscription was decided twice and could disagree with itself.** It branched on
  `Character is null` at initialisation *and* at disposal, and this page is the first host where
  that flips on a live instance. Measured, both directions: going to a named character leaked a
  handler for the life of the session; **coming back left a component that had never subscribed, so
  `/sheet` silently stopped following the character being built** — the one thing it is for.
  `@key` on the id fixes all of it by making each address its own instance.
- **The new subtitle was guarded by nothing.** Deleting its whole arm left both suites green,
  because the test asserted only the *absence* of "Villain" — which the front door's fallback
  satisfies just as well. `OnlyTheBuilderNamesThePalette` pairs a positive with its negative for
  precisely this reason and this copy of it had kept only the negative.
- **A doc comment overclaimed which assertion fires.** It said the `OpenAsync` mutation reddens
  `ShowingIsNotOpening` "on both assertions"; only the pointer one fails, because `OpenAsync` never
  touches the session. **A claim about which assertion catches a bug is worth as little as any
  other untested claim**, and both reviewers checked it independently.

#### And the first attempt at the race guard passed for the wrong reason

Worth recording because it is the failure this repository keeps having, in a new spelling. The
obvious test — hold the response, release it, assert — **stayed green under the mutation that
removes the fix.** Not because the fix was unnecessary: because nothing made the stale continuation
run before the assertion, so the test was racing a scheduler and winning by accident.

A second attempt navigated *inside* the gate to force the ordering; bUnit refuses a re-entrant
render. What works is **waiting for the wrong state and requiring that it never arrives**. The time
budget there is deliberately one-sided: a page that writes the stale answer does so as soon as the
continuation is scheduled, so the mutation is caught in about a second, and the budget is spent only
on the path where nothing goes wrong. That is the opposite trade from racing a timer for a
*positive* result, which is the mistake this file already records.

**Every one of the four fixes has a guard that was broken and watched to fail**: removing the
re-check reddens the race guard, removing `@key` reddens the follow-the-character guard, deleting
the subtitle arm reddens three tests, and drawing the identity switch everywhere reddens two.

**Not done, and deliberately:** no banner door. Three doors are build, run a game and look something
up; a sheet is a view of a character, not a fourth avenue.

**Left open, and named so it is not lost:** the command palette gives step six the alias `"sheet"`,
so typing that word still goes to `/build/review`. Not wrong — this entry declines a banner door on
purpose — but the word now names two destinations and one of them is a page called the sheet.

### CI cost three times what it needed to, and the measurement is the interesting part

**Asked for after the owner hit an Actions limit.** The answer began with numbers rather than
guesses, and the numbers pointed somewhere unexpected.

Over the last 100 runs — roughly one busy session — **403 minutes across 98 runs**:

| Workflow | Runs | Minutes | Avg |
|---|---|---|---|
| **Qodana** | 39 | **214** | 5.5 |
| **Build** | 40 | **150** | 3.7 |
| Deploy | 11 | 25 | 2.3 |
| Regenerate visual goldens | 8 | 14 | 1.8 |

Re-measure rather than repeating those figures:

```bash
gh run list --limit 100 --json name,status,createdAt,updatedAt --jq '
[.[] | select(.status=="completed") | {name, secs: ((.updatedAt|fromdateiso8601) - (.createdAt|fromdateiso8601))}]
| group_by(.name) | map({workflow: .[0].name, runs: length, total_min: ((map(.secs)|add)/60|round)})
| sort_by(-.total_min)[]'
```

**And the cache was worse than the minutes.** 10.08 GB across 24 entries, **every one of them
Qodana** — roughly 420 MB per branch against a 10 GB per-repository ceiling, so the caches were
evicting one another and every run started cold. They were deleted; the authoritative list
(`actions/caches`) reports `total_count: 0`. The `actions/cache/usage` aggregate went on reporting
the old figure for hours afterwards and **is not the endpoint to check** — it lags, and believing it
would have looked like the deletion had failed.

#### Three changes

- **Superseded runs are cancelled** — `concurrency` keyed on workflow and ref, on Build and Qodana.
  A force-push used to leave the previous run burning to completion on a commit nobody would merge.
  **`deploy.yml` keeps the opposite setting deliberately**: cancelling a half-finished deploy is how
  a site ends up serving a partial upload.
- **Two documentation files are skipped by the build, and only two** — `PROGRESS.md` and
  `docs/HANDOVER.md`. **"It is only docs" is false here far more often than it looks**: `CLAUDE.md`
  and `docs/guide/*.md` are read by `RepositoryGuideTests`, `docs/ACCOUNTS-SETUP.md` by
  `AccountsContractTests`, `docs/MCP-SETUP.md` and `README.md` by `McpSetupDocumentationTests`,
  `mcp/QUESTION-POLICY.md` by `McpQuestionPolicyTests`. Editing the index past its line budget, or
  adding a guide the routing table does not name, is a red build.
- **Qodana came off every pull request** and runs on `main` and weekly. **What makes that safe is
  that the pull request was never where the check first ran**: `CLAUDE.md`'s process already requires
  `./scripts/qodana-scan.sh` locally, reading zero, before one is opened. What is kept is the part a
  local run cannot give — a scan of `main` **as merged**, which is a different claim from a scan of
  the branches that went into it — plus a weekly backstop for a skipped local step. If that weekly
  run starts finding things, the answer is that the local step is being skipped, not that this
  should go back on every push.

**Measured immediately: a pull request that would have cost about 9.5 minutes across three checks
now costs 4 across one.**

#### The trap that did not apply here, checked rather than assumed

A path-filtered job reports **no status at all**, so on a repository with required status checks it
leaves a pull request permanently unmergeable. That is the usual reason not to do this.

**It does not apply**: branch protection is unavailable on this plan, so nothing is required —
verified by asking, not by reasoning. `docs/guide/hosting.md` records it, because it would not be
true of a repository on a different plan and the pattern is the sort of thing that gets copied.

#### `WorkflowFilterTests` holds the filter to its claim, and is honest about which half it can prove

- **Provable, and the direction whose failure costs a missed regression:** no file a test opens by
  name is skipped. Collected by scanning the test sources themselves, so no list is kept by hand.
- **Not provable, so an allowlist that says so:** a file being *unread* cannot be shown — a test
  could compose a path no scan sees. Growing `paths-ignore` therefore has to be deliberate, and this
  is what makes it one.
- Plus: the guide directory is never skipped wholesale, both triggers carry the same list, `deploy`
  still refuses to cancel, and Qodana still watches `main` and still has a schedule.

**Five mutations, all red**: skipping the guide set, skipping `CLAUDE.md`, dropping the cancellation,
filtering one trigger and not the other, and taking Qodana off `master`.

**Two of the nine tests exist because the first run failed honestly.** The scan found its own
allowlist — the file that vouches for a path necessarily names it — and the cancellation check
matched the words inside the comment explaining why `deploy.yml` differs. Both are guards that would
have passed for the wrong reason.

#### Where the minutes were actually going, which was not where the trimming happened

**The account that ran out was the owner's personal one, and the repository had already moved.**
Actions bill to whoever owned the repository at the time, so the session measured above billed to
`DorianSheiles`; the organisation's own meter read **77 minutes, net $0** — about 4% of its
allowance. The move to `SoftwareSamurai-net` was made deliberately for that fresh meter.

So the trimming was worth doing and was **not** what unblocked anything, and the entry says so
rather than claiming a rescue. Two things it is worth for on their own: a pull request that
finishes in four minutes instead of nine and a half, and a cache that has stopped evicting itself.

**Read usage from the organisation, not the user** — and note the endpoint moved:

```bash
gh api "organizations/SoftwareSamurai-net/settings/billing/usage" --jq '
[.usageItems[] | select(.product=="actions")] | {minutes: (map(.quantity)|add), net_usd: (map(.netAmount)|add)}'
```

`orgs/…/settings/billing/actions` answers `410 Gone`, and the equivalent user endpoint needs a
`user` scope this token does not carry.

---

### The chord is printed on the screen now, which is the whole of the defect

**`Ctrl`/`⌘`+`K` has opened the command palette since the palette shipped, and the only place the
chord was written down was inside the palette itself** — on the row of keys along its own foot,
which is visible to somebody who has already pressed it. The owner named it in item 12 as a defect
about today rather than a note about a future design, and it is: a keyboard shortcut nobody is
told about is a shortcut for whoever wrote it, and the palette's own file already carried that
sentence as a comment over the row of keys nobody could reach.

The banner carries the way in and the key beside it: **Search**, then the chord in two key boxes.

#### The four things that decided the shape

- **On every route.** The chord works on every route, and the step band and the budget strip — the
  two things drawn in the builder alone — are precedents for *builder-scoped* chrome, not for this.
  A button that appeared only inside the builder would say the key stops at its edge, which is
  worse than saying nothing at all. Four routes are pinned by a theory.
- **The modifier is the reader's, not the developer's**, which is the owner's own amendment to
  item 12 and the half that is easy to get wrong quietly. `palette.js` listens for `ctrlKey` *or*
  `metaKey` precisely because it is `Ctrl` on Windows and Linux and Command on a Mac. So the
  script answers one boolean about the platform — `ppPalette.onAMac` — and `Shortcuts.ReadModifier`
  decides the word. **A hard-coded `Ctrl` is wrong for half the readers in the way that costs the
  affordance**: somebody who presses the key they were told about and gets nothing stops reaching
  for it.
- **`Cmd`, and not the looped-square glyph the Mac convention actually uses.** That glyph is in
  neither of the two typefaces this app names, so it would fall back to a system face — silently,
  on one platform. That is precisely the failure the "no component names a typeface" rule exists
  to prevent, arriving as a character rather than as a declaration.
- **A missing script prints no chord at all.** That is the deployment where the key does nothing,
  so the reading call answers `null` on a swallowed failure rather than a default: **a default
  there is a claim about the reader's keyboard made by a script that never ran.** The button still
  opens the palette, because opening it is a click Blazor handles — the same bargain every guarded
  interop call in this app makes, in a case where the guard has an answer to be wrong about for
  the first time.

#### What it is not, and why

**A button and not a search box**, though item 12 puts a rules search in this spot eventually. A
box that looked like a search field while searching Powers and step names would be the wrong
promise twice over: the rulebook is a different corpus and it is behind an account. Growing the
palette onto it is the part of item 12 that still has to be argued, and `palette.js` says in as
many words to resist growing it. **That argument is untouched here** — this closes the
discoverability half and nothing else in item 12.

**"Search" rather than "Go to"**, which is what the palette calls itself inside. The label has to
survive being read at a glance in a strip of six other controls, and the first version — "Go to",
sitting between two underlined links — read as a third link with no destination. Looked at, not
reasoned about: the shell proof page was screenshotted in both palettes before and after.

#### Five mutations, five reds

Every one against the committed change, and each named the test that should have caught it:

| Mutation | What went red |
|---|---|
| `Cmd` → `Ctrl` in `ReadModifier` | the Mac row of `TheChordIsPrintedForTheKeyboardTheReaderHas` |
| the swallow answers `false` rather than `null` | `ShortcutsSwallowsAMissingScript` |
| the key boxes deleted from the banner | seven rows across three tests |
| the button drawn in `Area.Play` only | eight rows — and the `build/tier` row **passed**, which is the positive control |
| `Commands.Open` → `Commands.Close` | the two tests that press the button |

**The nineteen browser verdicts were re-driven locally against real Chrome**, including all four
375px narrow proofs, and the twins still say `FAIL`. The banner gained a control and nothing
overflows at 375px — `clientWidth 360, scrollWidth 360`, measured in the iframe the harness uses
rather than in a Chrome window, which headless clamps to ~485px and which reads as overflow that
is not there. That trap is written down in the harness's own header and was nearly walked into
from a screenshot.

#### And four exemptions that had stopped being true

`UppercasedTextTests` refuses a selector it cannot find on any rendered page, which is the
assertion added after **13 of 25 uppercased selectors were found to be matching nothing**. Four of
its exemptions read *"MainLayout, which needs a Body fragment and a router"* — and that was never
true: `BannerTests` has rendered the layout on its own since the day it was written, `Body` left
null and every band drawn. The new label tripped the refusal, which is the guard working; the
honest fix was to render the layout in the sweep and delete the four, so five uppercased banner
selectors are now actually checked instead of standing behind a reason nobody re-read.

**When a test exempts a subject, the exemption is a claim with a shelf life.** Check it still
holds before adding a sixth beside it.

### The character manager, rebuilt around what it actually holds

**The owner asked for a redesign and asked to see a plan first**, which is why this entry has an
argument in it rather than a list of CSS changes. The panel holds one kind of thing and offers four
actions on it, and its layout encoded neither.

> *"'import a character' being skinnier and adjacent but also floating is just awkward."*

#### What the layout was getting wrong

Three faults, and each had exactly one answer:

| Fault | What it cost |
|---|---|
| **Stranded actions** | A row was a name at the left edge and two buttons at the right, across a gap that grew with the window. The commonest act in the panel — opening a character — was the smallest thing on the row and the furthest away |
| **Mismatched peers** | *Start a new character* and *Import a character* both make a character that does not exist yet, and one was drawn `.small`. Nothing contained either |
| **No "you are here"** | A character reaches the list only once it is worth keeping, so straight after *Start a new character* the list showed the one you kept and **nothing marked open at all** |

#### The weights were upside down, measured against the actions themselves

| Action | Reversible | How often | Was drawn as |
|---|---|---|---|
| Open | yes — open the other back | most often | a small button, far right |
| Discard | yes — one level of undo | rarely | **danger red, on every row** |
| Start a new character | nothing is lost | sometimes | a normal button |
| Import a character | nothing is lost | rarely | **a smaller button, floating beside it** |

The loudest thing in the panel was its rarest and most reversible action, repeated per row. The
red had a reason — a destructive control should look as serious as what it does — but **the confirm
was removed *because* undo makes discarding cheap**, and the colour was never revisited to match.

#### The three moves

- **The row is the control.** A character's name is a real, full-width button that opens it, the
  same idiom a Trait row's name already uses. Discard is a quiet trailing button. The large easy
  target is the safe act and the small distant one is the destructive act, which is the way round
  Fitts's law should be pointed. **No new tab stops** — two controls per row before, two after; what
  changed is which one is big.
- **The open character has its own block, above the list, always.** It carries the spend.
- **The two ways to make a character are one bar**, equal weight, under a rule. `ImportCharacter`
  loses `.small`: the demotion was itself a fix — for the operating system's raw file chip, which
  "competed with" its neighbour — and it worked by making one of two peers visibly lesser.
  Separating them from the list is a container's job, not a font size's.

#### One constraint decided the data on a row, and it is worth stating

**A row carries a time and never a cost.** `SavedCharacters` keeps an index of labels and timestamps
and holds each character's payload under its own key; its own remarks refuse a list that
"deserialize[s] and cost[s] every one of them just to print a label and a timestamp". So `UpdatedAt`
is free and a Hero Point figure is a read, a cost and a validate **per row**.

**The open character is the one exception, because the session is already holding it** — which is
the whole reason its block can carry a figure when no row can. That asymmetry is not a design flourish;
it falls straight out of where the data lives.

`Ages.Since` turns the stamp into a phrase, and **answers null for a stamp of zero**: the legacy slot
is synthesised into the list with no timestamp, because nothing ever recorded one, and formatted
naively that reads "over a year ago" beside somebody's oldest character — a confident answer to a
question nobody can answer. Times appear only once there are **two** characters to tell apart.

#### Four things the build found that the pitch did not

- **The planted-pointer fixture was a state no running app can be in.** `OpenRow` moved the pointer
  and saved a character but never put it into the session — and opening a character does both, as
  the app's own boot does. It went unnoticed while the open character was an ordinary row; the
  redesign reads its name from the session, live, because somebody may be typing it on the finishing
  step, so the fixture drew "Unnamed character" over a store holding "Ninth Precinct".
- **`FakeApi` was answering 1970.** Its clock was a bare counter handing out 1, 2, 3 — timestamps a
  millisecond after the epoch — where the real server writes `Date.now()`. Nothing asserted on the
  value, so it went unnoticed until a panel started printing a time and **every proof page read
  "over a year ago"**. That class's own remarks name the rule it was breaking: *a stub that answers
  something the real server never would is worse than no stub.*
- **The manager's proof page needed `storesForReal`.** bUnit's interop answers null to every read, so
  the current-character pointer never round-tripped — and the proof drew the open character in its
  own block *and* again in the list, because the panel could not tell they were the same one. A
  picture of a bug the app does not have.
- **`UppercasedTextTests` caught the new caption with its own positive control**, refusing rather
  than exempting: *"'.others-head' is set in capitals and this test found it on no page."* Showing it
  the surface meant giving the sweep a manager holding two characters — and **the fixture had to sign
  in before loading the sample**, because `Accounts` resolves who is here once and anything touching
  the session first settles that question as "nobody".

#### Six mutations, five red on the first pass, and the sixth was a real hole

Setting "show times always" left the whole suite green — **not a null mutation**: every other fixture
in that file happens to hold two characters, so the rule was never asserted at all.
`ATimeIsShownOnlyOnceThereAreTwoCharactersToTellApart` closes it and the mutation now goes red. The
other five: not excluding the open character from the list, removing the *Open now* mark, offering a
Discard beside an empty slot, demoting the import again, and giving the legacy slot a confident age.

#### What was pitched and deliberately not built

The pitch put four questions to the owner with a recommendation on each, and all four were taken.
Nothing was built beyond them — in particular **there is still no rename**, so a character is listed
under whatever its sheet's name says. That is the right default and it is not the same as being able
to file two characters under labels of their own.

---

### The explained sheet is how the sheet renders, and the second address is gone

> *"the 'explain this character sheet' button is still present, instead of that just being the
> default way the sheet renders."*

**`SheetView.Explain` defaults to `true`.** Every call site already drew `<SheetView />` with no
argument — the review step, the preview beside the editors, every replayed recording — so one
default moved all of them. `/build/sheet` and the *Explain this sheet* link on the review step are
gone with it: with the sheet below already explained, the link offered a way to the page you were
already on.

#### The argument the old default rested on, and why it does not hold

`SheetView`'s own comment said it plainly: *"Off by default, and the default is the one that
matters. The printed sheet is what this whole tool produces and it is one page by a margin; a sheet
that gained forty controls would be a different document."*

**The premise is right and the conclusion does not follow, because the printed sheet gains
nothing.** The print block already takes every control back off:

- `.tip-wrap` is in the `display: none` list, and the tip itself is `display: none` unless hovered —
  which paper cannot be.
- `.term-name` gives up `text-decoration` and `cursor`, so the word prints as a word with nothing to
  say it was ever a button.
- The description's other copy — the one `aria-describedby` names, which cannot be `display: none`
  or it leaves the accessibility tree — is `.sr-only`, a clipped 1px box.

So the page out of the printer is identical whether the explanations are on or off. **That was true
before this change and nothing tested it**: it was one stylesheet edit away from being false on
every sheet the tool produces, and nobody would have found out until a sheet came out of a printer
with forty dotted underlines on it. `PrintingASheetIsUnchangedByTheExplanations` reads the cascade
out of `app.css` and pins all three, and both halves were broken and watched to fail.

**So `Term` needs to do nothing on paper, and already does nothing.** That was the second question
this item said to decide rather than assume, and the answer was already in the stylesheet with a
comment explaining itself.

#### What the flip actually cost, which was not the printer

**Seventeen tests across four files failed at once, and all of them were asking the same question.**
A term's cell holds the name *and* two copies of its description, so `TextContent` on a Trait cell
reads `"PresenceHow forceful…How forceful…"`. Every one of those tests was using `TextContent` as a
stand-in for "what the sheet says", which it had been while exactly one address drew terms.

`ExplainedSheetTests` already had the right helper — a `Visible` that drops `.sr-only` and
`.row-tip` and inserts a separator only where a browser would, carrying its own recorded trap
(*"this repository has shipped a test for that bug that was beaten by its own helper"*, which
replaced every tag with a newline and so read the broken markup as correct). **It is shared as
`SheetText` rather than copied**, which is the whole of the fix for sixteen of the seventeen.

**The seventeenth was different and is worth recording.** `PreviewColumnTests` compares the
preview's markup against the review step's, character for character, and terms carry
`blazor:onkeydown="N"` where N is a per-renderer counter — so two renders of an identical component
disagree on a number no reader could ever see. Blazor's handler ids are stripped and nothing else
is, with a positive control asserting something really was stripped, so a change that stopped the
sheet rendering terms at all could not pass by making both sides trivially equal. **This is the same
trap `Term` already documents for its own ids** and solves by deriving them from the name; these are
Blazor's and cannot be derived from anything.

#### Off is still a real setting, and is asserted as hard as on

Nothing in the app passes `Explain="false"` any more, which is exactly why it is tested. It selects
`Term`'s bare-name fallback — **the same path a Power with no description in the rules data takes**,
which is not hypothetical — and that path has to keep rendering what the sheet rendered before
explanations existed. `ExplainingTheSheetChangesNoneOfItsWords` compares off against the default
rather than the default against `Explain="true"`; the latter would be comparing a render with itself
and would pass by construction.

#### Measured, and the one number worth knowing

The explained render is **34,250 bytes against 8,835** for the same character — a Hero sheet's 33
terms, each adding a button and two copies of its description. **No change to what is downloaded**:
`Term`, `Tooltip` and their CSS already shipped in the bundle, because `/build/sheet` already used
them. What grows is the rendered DOM, and the place that matters is the preview column beside the
editors, which redraws on every rank, Power and Perk change above 1500px. Render time was not
measured; if it ever reads as slow, that is a measurement to take rather than a reason to give the
preview a second, quieter sheet.

#### The adversarial review broke the print guard and watched it stay green

**The test that was the whole evidence for "the printed sheet gains nothing" did not test the
mechanism.** It checked that `.tip-wrap` was in the print block's hide list, that `.term-name` gave
up its underline and cursor, and that `clip-path` appeared *somewhere* in `app.css`. All three are
true. Only the second is about a `Term` at all: **`.tip-wrap` belongs to `Tooltip`, and a `Term`'s
tip has no such ancestor.** What keeps a description off paper is `.row-tip`'s own base rule,
`display: none`, opened only by `:hover` and `:focus-visible` — and the test never looked at it.

A reviewer set that one line to `display: block`, ran the suite, and got 581 green. Then rendered
the markup against the mutated stylesheet, printed it with headless Chrome and read the PDF back:
**the description was on the page**, and the file had doubled in size. That is the exact regression
the test exists to prevent.

**This was broken and watched to fail, and that was not enough.** The mutation removed `.tip-wrap`
from the print block and the test went red — so it looked like a working guard. It was a null
mutation wearing a disguise: the guard reacted, to a change that could not have affected a sheet.
`CLAUDE.md` already says a semantically null mutation does not count; the sharper form is that
**breaking something the guard was never about is the same failure.** Ask what the mechanism is
before choosing what to break.

The one test is now three, each naming its own mechanism, and a `Rules()` helper reads `app.css` as
selectors and bodies rather than as a string to search — *"a guard that cannot name the rule it is
about cannot notice that rule changing."*

**And the new guard had a hole of its own, found the same way.** It asked whether the selector
contained `:hover`; a selector *list* is not one selector, so
`.option:hover .row-tip, .sheet .row-tip { display: block }` opens the tip unconditionally through
its second branch while the string is still present in the first. That mutation came back green,
was traced rather than recorded as a null result, and the guard now splits on commas. All four
mutations go red.

#### Two findings recorded rather than fixed, and why

**Duplicate ids are reachable, and the only reachable case is the harmless one.**
`Term`'s id is derived from its name, so two things called the same word put one id on two elements
— and before this change `Review` carried no ids at all, because it drew the sheet unexplained. The
reviewer demonstrated it by selecting one Power twice, which `CharacterValidator` allows with a
warning.

**Traced, and the harm depends entirely on whether the two sentences differ.** They do not, in any
reachable case: the five categories the sheet draws terms for — Abilities, Talents, Powers, Perks,
Flaws — **collide on no name at all** in the shipped data. Two collisions do exist (`Collapsible`,
`Repair`) and neither reaches a term: both are between a Power's own Con and an entry in another
file, and Pros and Cons print as a stat line. So the only duplicate anybody can produce is the same
Power twice, where both terms carry the identical sentence and whichever one `aria-describedby`
resolves to is right — the collision `Term`'s remarks already call the one worth having.

**What was missing was any guarantee that it stays that way**, and that is what was added:
`NoTwoThingsTheSheetExplainsShareAName` fails on the entry that would put two *different*
descriptions under one id. Changing the id scheme was considered and rejected — a generated id
breaks the replay guard that requires two renders of one character to be identical, which is a trap
`Term` already records, and the benefit while both copies say the same sentence is nil.

**The preview column gained 33 tab stops, and that is a decision rather than an oversight.**
`Characteristics` draws the same sheet beside the editors, so above 1500px the preview now holds 33
focusable buttons where it held none — against 19 real controls for the open tab and the step
buttons. `docs/guide/browser.md` records the project's stance on exactly this cost for a different
component: *"141 extra tab stops would undo `OptionList`'s one-tab-stop keyboard model."*

It is accepted, and the alternative is why: turning the explanations off for the preview alone is a
second, quieter sheet — the shape the owner's report was against — and the preview is the same
document, not a summary of it. **The number is recorded here because it was not measured before and
because the owner may want it back**: the change is one parameter on one call site. Keyboard cost is
not the same question as the screen-reader work the owner has deferred, and it is stated plainly
rather than folded into that deferral.

#### What moved

`ExplainedSheet.razor` deleted; the link removed from `Review.razor`. Two `ExplainedSheetTests`
assertions inverted, two deleted with the page, one repointed so it is not vacuous, two added.
`ProofPages.TheExplainedSheet` renders `SheetView` directly and keeps its one forced-open tip, which
is still the half no golden and no assertion can settle. The `explained-sheet` golden moved and was
regenerated on CI's own Chrome.

**A stale address is not left behind and that follows precedent rather than setting it.** `App.razor`
renders its own "no such page" for anything unrouted, `_redirects` names no specific path, and the
sample characters moved from the tier page to `/portfolio` to `/admin/portfolio` with no redirect
stub kept at any of the old addresses.
### The tier page did not redraw when a control inside it emptied the sheet

**Found by opening the deployed site and pressing the button**, immediately after the change above
went live. The character was correctly kept and the sheet correctly emptied — and the Standard card
still read *Selected*, until the page was reloaded.

`CharacterManager` is a child of `ChooseTier`. Pressing its *Start a new character* empties the
session; the child's own `StateHasChanged` redraws the list of characters, and every tier card above
it is the parent's. **Nothing on that page subscribed to the session at all**, so the cards were a
render from before the click. Importing reaches the same gap from the other side: it gives the sheet
a tier, and no card would have shown it.

**It predates this round** — the control this replaced also emptied the sheet, and the card would
have gone on saying *Selected* then too. It is fixed now because this is the button it is visible
on, and because **a page saying a tier is chosen while the engine holds none is the one thing on
that screen a reader would act on.**

One subscription and a `Dispose`. Every handler on the page already calls `NotifyChanged` for the
shell's sake, so there are no new call sites, and the page reads its cards straight off the sheet —
a redraw is all it needs.

**Subscribing changes what a test may do**, and this is the durable part: raising `Changed`
off-dispatcher now throws on this page rather than redrawing, which is the constraint
`DiscardedCharacterTests` already records for the layout. `StartAnotherTests`' `Build` helper goes
through `InvokeAsync`, **awaited and never blocked on** — blocking on a renderer task can deadlock
against that same dispatcher, and that arrives as a hung CI run rather than a red test.

#### And the reason this entry exists at all: nothing in the suites could have found it

Every check in this repository that touches the tier page renders it and reads its markup. **None of
them changes the session from inside a child component and then looks at the parent**, because until
this round no control on that page did anything the parent had to notice. This is
`PROGRESS.md` item 10's argument again in its smallest form: it took a person opening the deployed
site and pressing the button. The new test does the same thing in-process, and was watched to fail.

#### `git checkout -- <file>` destroyed the fix, for the fourth recorded time

The guard above was written, the fix was written, the suite went green, and then the mutation
battery ran `git checkout -- web/Pages/ChooseTier.razor` to undo its own mutation — **taking the
uncommitted fix with it**, because the fix had never been committed. `git commit` then succeeded
against a staged test file alone and produced exactly the failure `CLAUDE.md` describes: *"a commit
message that describes a change the commit does not contain."*

**Both habits `CLAUDE.md` names caught it and neither is a judgement call.** `git status --short`
after the commit listed one file where two were expected, and `git show --stat HEAD` said the same
in one line. Nothing was lost, because re-applying a twenty-line change is cheap — but the deeper
rule is the one that was broken: **a mutation belongs against committed work.** The earlier
batteries this round were run after a commit, deliberately, and this one was not.

---

### The character switcher had nothing to list, because nothing ever named a character into the index

**The owner reported two things and they are one defect.**

> *"I don't see the web UI character switcher working yet? If I import Lynchpin, my character,
> there is no option to start a new character that doesn't blow away my old one?"*

`SavedCharacters` had an index, `CharacterManager` drew rows off it, the banner's switcher listed
it, `DiscardedCharacter` restored into it and the undo buffers closed around it. All of it worked.
**Nothing in the application ever put a character into it.** `RestoreAsync` — the only labelled-save
path — had no caller outside `web/Services/`, and everything else autosaved into the *current* slot,
which for an anonymous visitor is the single legacy slot this browser has held since before there
was a list. So:

- `CharacterManager.StartNew()` was `Session.StartAgain()` plus `Store.ClearAsync()`. It **emptied
  the slot the character was in** rather than leaving it there and pointing somewhere else. That is
  the "blows away my old one" — the owner imported a character, pressed it, and lost it.
- Import went through `ReplaceWithUndo`, which overwrites whatever the pointer is aimed at.
- `ListAsync` therefore returned at most one row, so the switcher always said *"Nothing else saved
  yet."* **It was not broken. It had nothing to list**, and that was read as correct-for-an-empty-
  browser when #84 was verified live.

**On an account the same control was worse.** `ApiCharacterStore.ClearAsync` is
`DeleteAsync(currentId)` — an HTTP `DELETE`. "Start a new character" did not empty a local slot
there, it removed the row from the server.

#### What was built

`AccountCharacterStore.StartAnotherAsync(sheet, mode)` — one operation, wired to both controls:

1. Write the character on screen down under its own id, with the label taken from its own name.
2. Move the current-character pointer to a freshly minted id.
3. Only then may the caller empty the session.

**The order is the whole of the correctness.** Emptying the session raises its change event, which
starts a write nobody awaits; doing it first races that write against the move and puts the empty
sheet over the character being kept. Both steps are awaited, so the autosave reads a pointer that
has already moved. This is the same ordering property the old control had, the other way up: it used
to be "the clear lands *after* the save", because the last thing that had to happen was the slot
being emptied. The last thing that happens now is the empty sheet landing in a *different* slot.

Three supporting changes, each of which is a defect in its own right:

- **`SavedCharacters.SaveCurrentAsync` now puts the open character into the index** on its first
  worth-keeping autosave, instead of only bumping an entry that was already there. The account's
  store has always worked this way — its `PUT` creates the row — and **the two sides disagreeing is
  what hid this**. Without it, a character built in the slot the keep just opened has a payload and
  no index entry, and the list is discovered *through* the index: it would be a character nobody
  could get back to.
- **The label follows the sheet's own name**, which reverses the older note that "an ordinary edit
  is not a rename". That was written when the only way into the index was an explicit labelled save,
  and it made the label a thing you could set once and never change. `ApiCharacterStore` already
  derived it from the sheet on every autosave; there were two copies of that one-line rule and there
  is now one, `SavedCharacters.LabelFor`.
- **`ListAsync` reads the legacy slot's payload rather than synthesising a row from the bare fact
  that one exists.** Two things fall out. A named character is listed under its name instead of as
  *"Unnamed character"* — which is what the owner's imported Lynchpin was being called in the one
  row the list could draw. And an *empty* sheet in that slot is no longer listed at all: switching
  the palette autosaves an otherwise untouched sheet, so a visitor who had done nothing but that was
  shown a row for a character who did not exist.

**The account's cap is asked about before anything moves, and a refusal is said out loud.** A
character created lazily by its first autosave is refused with a `409` that path has nowhere to
report — so on a full account everything typed into the new character would go quietly nowhere. A
cap that could not be read counts as no room, the same direction `AccountCharacters.IsFull` already
takes. Both refusals — no room, and a server that could not be reached — put a sentence under the
button, because a control that keeps rather than overwrites does *nothing* when it cannot proceed,
and doing nothing is indistinguishable from a control that is not wired up.

**Import no longer arms an undo**, and that is not an oversight. `ReplaceWithUndo` exists for the
two things that really do replace the character on screen without moving the pointer — loading a
sample, opening a recording. An import moves the pointer now, so nothing is destroyed, and an undo
would put a *duplicate* of the kept character into the imported one's slot: a rescue offered from a
character that was never in danger.

#### The tests, and why they are shaped this way

**This is item 10's argument with a name on it.** A feature was built, tested, adversarially reviewed
by two independent agents and shipped, while nothing in the application ever wrote to the store it
read from. Every check passed because every check called the store directly — so no test could have
noticed that nothing else did, and no test could ever have had two characters in it.

So `RenderContext` gained `storesForReal: true`, which registers a local storage that actually holds
what is written to it in place of bUnit's recorder (which answers null to every read). Every test in
`StartAnotherTests` presses a control a person presses and then asks what is actually stored.
`FakeLocalStorage` gained a call log, because bUnit's `JSInterop.Invocations` is not recording once
its runtime has been replaced — and because **order is what several of these are about and cannot be
read off the end state**: a write and a clear of one key leave the same dictionary whichever way
round they happened, and which way round they happened is the difference between keeping a character
and losing one.

**Seven mutations, six red on the first pass, and the seventh was the finding.** Dropping the
index-add from `SaveCurrentAsync` left the whole suite green. It is *not* a null mutation: the kept
character is indexed by the explicit save that keeps it, so nothing noticed that the character
actually being **built** in the newly minted slot was never listed at all — the same defect this
entry is about, one slot further along.
`ACharacterBuiltInTheNewSlotIsListedWithoutBeingKeptAgain` and
`SwitchingAwayFromTheNewCharacterLeavesItWhereItWas` close it, and the mutation was re-run against
them and goes red. The other six: reverting `StartNew` to empty the slot (9 red), never moving the
pointer (5), listing an empty sheet (3), never asking the account's cap (1), import back to
`ReplaceWithUndo` (2), and synthesising the legacy row from bare existence (3).

**One test was deleted rather than kept, and the reason is worth recording.**
`StartAgainTests.TheClearLandsAfterTheSaveThatEmptyingTheSheetFires` became an ordering claim its own
context cannot see: with storage answering null to every read, the pointer move it records is
invisible to the very next call that reads the pointer, so a test written there would watch the
autosave land on the old key and would have to either bless that or assert nothing. A comment now
sits where it was, naming where the property is actually proved.

#### Then an adversarial review found three data-loss defects in the fix, and a fourth that was not one

**All three were demonstrated rather than argued**, by a reviewer given the diff and told nothing
about how it was built. Every one of them is a sequence an ordinary person can perform.

- **`SavedCharacters.SaveAsync` returned the id it was passed whether or not the write landed.**
  Both callers weighing the result compared it against the id they had just handed in — which, for a
  non-null id, is the same string either way. A dead check that read exactly like a guard. On a
  browser that refuses storage, "keep this one and start another" reported success over a character
  that had gone nowhere and then emptied the sheet. **The undo behind a discarded row had the same
  bug and predates this slice** — `AccountCharacterStore.RestoreAsync` is the one method in that
  class documented as answering whether the write landed, "because an undo that silently did nothing
  is the worst possible outcome", and it could not. It now answers `(Id, Stored)`.

- **The account's cap was read before the character was written, which raced the very thing the
  check exists to prevent.** The ordinary autosave is fire-and-forget over HTTP, so a list read
  straight after an edit can answer from before that edit's row existed: an account one short of its
  cap reads as having room, a fresh slot opens, and everything typed into it is refused by a `409`
  the autosave path has nowhere to report. Write first, then read — the write is awaited, so the
  list cannot be answering from before the character existed. **A fourth outcome,
  `NotStarted`**, keeps the sentence honest for the case that now exists: kept, but the cap could not
  be read. "Your character could not be saved" over a character that *was* saved is the kind of false
  alarm that teaches somebody to distrust every message the app gives them.

- **"Start a new character" still armed the undo buffer.** `Undo` restores into the sheet and never
  moves the current-character pointer — which by then is on the fresh slot — so undoing wrote a
  second copy of the kept character there and the reader found it listed twice. **This is the exact
  failure the import path had already been changed to avoid**, and the same fix was simply not
  carried across: "an undo would put a duplicate of the kept character into the imported one's slot".
  `StartAgain` now takes `offerUndo`, false here and true for discarding the row that is open, which
  really does throw the character away.

**The fourth finding was traced and rejected, and that matters as much as the three.** It argued
for writing the index before the payload, on the grounds that a half-failed pair should leave an
entry naming nothing (which `ListAsync` drops) rather than a character nothing names (which is
unreachable). The mechanism is real and the conclusion does not follow: `WriteIndexAsync` swallows
its own failures, so an index write that fails does not abort the pair and **both orders end in
exactly the same state**. The one case where they differ favours the existing order — a payload
write that throws has then touched nothing at all, where the reverse would already have added an
entry `ListAsync` must drop on every future visit. The order is unchanged and the class remarks are
corrected instead, because the *paragraph* the reviewer was reading really had gone stale: "a
character that exists in storage but is missing from the index can only ever be the legacy slot" was
true while nothing but an explicit labelled save added an entry, and stopped being true the moment
the autosave started adding one.

**Three mutations, each reverting one fix, and each goes red on the tests written for it** —
`AStorageRefusalKeepsTheCharacterOnScreenAndSaysSo`,
`TheAccountsCharacterIsWrittenBeforeItsCapIsRead`, and
`StartingAnotherLeavesNoUndoThatWouldDuplicateTheKeptCharacter`, plus a second test each.

**The cap-ordering test asserts the order of the requests rather than racing a timer**, which is the
only honest way to test it here: a test that slept would be testing this machine's scheduler.
`FakeApi.Asked` records each request with its method, so "the `PUT` comes before the `GET`" is a
plain assertion. **And the shipped account tests could not have caught this**, for a reason worth
recording: every one of them calls `Settle` before clicking, deliberately, so the fire-and-forget
write finishes first — which is right for determinism and removes the race the check existed to
close. A suite can be disciplined, have positive controls throughout, and still have a hole shaped
exactly like the thing it was written about.

**Importing shares the keep and therefore shares the refusals, and only "Start a new character" had
tests for them.** Both refusal branches are now exercised on both controls.

#### What this does not do

- **Loading a sample still overwrites the character on screen**, with the one-level undo it has
  always had. That is a demonstration replacing what you are looking at, not a second character, and
  it was outside what was reported.
- **There is no rename.** A character is listed under whatever its sheet's name says, which is the
  right default and is not the same as being able to file two characters under labels of their own.

---

### Swapping characters from anywhere, and three defects an adversarial review found in the half beneath it

**Two pieces of work, and the second is the one worth reading.**

**The switcher.** `CharacterManager` is a panel on the tier page, so swapping meant navigating to
step one of six — which reads as starting over. `CharacterSwitcher` names the open character in the
banner and lists the others, on every builder route and nowhere else, the same rule the step list
and the budget strip follow. The manager keeps discard, import and start-new; this is the one act
worth having from everywhere.

**Two of its faults were found by looking at it, and neither was visible to any test.**

- **The disclosure painted behind the step band.** `view-transition-name` on `.banner` creates a
  stacking context, so a `z-index` on the menu resolves *inside* the banner — and the banner is a
  static earlier sibling of `.steps`. Fixed on `.banner`, not on the menu.
- **At 375px it was clipped off the left edge**, because the banner wraps and a right-anchored menu
  grows leftwards off the window. Anchored left instead.

A third came from the repository's own guard: the name was set in capitals and
`UppercasedTextTests` objected because the selector appeared on no page it renders. The rule it
guards is the reason to keep it that way — **a character's name is the person's own words**, and it
is free text that can hold anything the rulebook writes in mixed case.

---

**The browser cache follows what is open — and the first version of it destroyed people's work.**

The owner asked for the anonymous slot to track the character being worked on, and chose
clear-on-sign-out over the alternatives when the shared-machine consequence was put to them. What
shipped into review wrote the copy through the anonymous *current* pointer and cleared
unconditionally. **Two independent reviews, each given the diff and told nothing else, found the
same three faults with running evidence:**

| Fault | What it cost |
|---|---|
| The clear was unconditional | A draft built before signing in was destroyed by a later sign-out, although no account character was ever opened. Silent, no undo |
| The clear ran only from the Sign-out **button** | Closing the tab or letting the session expire — how people actually leave a shared machine — left the last account character readable to the next visitor. The leak the clear exists to close, open in the ordinary case |
| The copy went through the *current* pointer | It overwrote whichever named local character was open, which kept its own label while holding somebody else's data |

**One cause, and the fix is structural rather than defensive.** The copy has a reserved id,
`SavedCharacters.AccountCopyId`, and is **written at that id rather than through the pointer** —
the first attempt moved the pointer and trusted the next write to see it, and it did not, which the
test harness surfaced immediately. The clear removes only that id. `Program.cs` clears it at boot
when nobody is signed in, which is what covers an expired or revoked session. The reader's own
characters are never written to, never cleared, and the copy stays out of their index so it never
appears in their list.

**A guard that asserted nothing, proved by mutation.**
`OpeningACharacterSignedOutDoesNotDisturbTheAnonymousSlot` read the anonymous slot back after
opening a character *from that same slot* — a self-write. Removing the `who.IsSignedIn` guard from
the copy-down left all 581 tests green. It asserts the reserved slot now, and the same mutation
fails it.

**And a null mutation nearly produced a false finding here too.** Re-breaking the unconditional
clear appeared to leave the suite green — until `git diff --numstat` showed the pattern had never
matched and the file was unchanged. Applied properly, **two tests fail**. *Check the mutation bit
before believing anything about the guard*; this is the second time in two slices.

**What this does not close.** Every assertion about storage here is on which key is written and
which is cleared, because bUnit answers null to every interop read — three attempts to write these
tests against a working store failed on their own preconditions. That is the narrowest honest claim
available, and it is the argument for item 10.

### The anonymous slot now tracks what a signed-in reader has open, and sign-out clears it

The browser's anonymous local-storage slot and an account's server-side characters used to be two
worlds that never touched: signing in left whatever the browser was holding alone, and opening an
account character never wrote to it either. The owner asked for two changes together, deliberately
paired — do one without the other and the pairing is unsafe in one direction or the other:

1. **`AccountCharacterStore.OpenAsync` now copies a signed-in reader's opened account character
   down into the browser's anonymous slot**, replacing whatever was there.
2. **Signing out empties the anonymous slot.**

**(2) exists because of (1), and only because of it.** Before this, signing out never touched the
anonymous slot at all — there was nothing of anybody else's in it to worry about. Once opening an
account character starts writing it there, leaving it alone on sign-out would let a shared machine
hand an ex-user's account character to whoever opens that browser next, signed in or not. The
clear is unconditional — it empties whatever the slot holds, not only a copy this feature itself
put there — because nothing at that point can tell the two apart, and the owner was shown the
narrower alternative and chose the simpler, safer rule instead.

- **The write-through lives in `OpenAsync`, not in `LoadAsync` or anywhere sign-in itself runs.**
  Signing in still does not copy anything by itself — the sign-in page's offer to keep what the
  browser was holding, only when the account has none, is unchanged and still the only way an
  anonymous character moves *up*. What changed is the other direction: looking at one of the
  account's own characters is now also holding it in this browser, the way it always was for an
  anonymous visitor.
- **The clear cannot live inside `Accounts.SignOutAsync` itself.** `Accounts` is the
  `IIdentitySource` both stores are built on (`CharacterStore` and `AccountCharacterStore`, and the
  `SavedCharacters` beneath them, all take one), so having it depend on either store back would be
  a constructor cycle. The call is on `SignIn.razor`'s `SignOut`, right after asking the server to
  sign out — the same page that already makes the one other explicit copy in this design, the
  keep-the-anonymous-character offer.
- **The two internal seams this needed were already half-built.** `CharacterStore.LoadAsync(Identity)`
  existed for reading the anonymous slot specifically (the sign-in page's offer needs it); this
  added the write-side twin, `CharacterStore.SaveAsync(Identity, sheet, mode)`, and made the
  existing private `ClearAsync(Identity)` `internal` so `AccountCharacterStore.ClearAnonymousAsync()`
  could call it. Nothing about `ChosenAsync()` — the per-call routing every ordinary save and load
  goes through — changed at all.
- **The sign-in page's copy needed fixing.** "Kept on your account. The one in this browser is
  untouched." was true the instant it was shown — the keep action itself still does not touch the
  anonymous slot — but it read as a durable promise, and it no longer is one: the same slot will be
  emptied the moment this reader signs out, by design. Trimmed to "Kept on your account.", which
  claims only what stays true.
- **Proved by breaking each half and watching it go red, not by reasoning about it.** Turning the
  `OpenAsync` write-through into dead code (`if (false && …)`) put `"Old anonymous work"` where the
  test asserted `"Their account character"` — the mutation changed the answer, not merely the
  verdict. Turning `ClearAnonymousAsync` into a no-op left a full `CharacterSheet` where the test
  asserted `null`. Both restored and rerun green.
- Five new tests in `tests/ProwlersAndParagons.Web.Tests/AccountTests.cs`: opening an account
  character replaces whatever the anonymous slot held; opening one while signed out does not
  disturb it (nothing to copy — it is already where the open went); signing out empties it
  whatever it holds; ordinary anonymous saving still works right after a sign-out; and the
  sign-in carry-over offer still only applies while the account has none, unaffected by any of
  the above. `worker/` needed no change — this is entirely browser-side, and `AccountsContractTests`
  and the worker suite both stayed green untouched.

Nothing outstanding. `dotnet test --configuration Release -p:ContinuousIntegrationBuild=true` prints
two `Passed!` lines (4015 engine, 565 web) with no `Catastrophic`; `./scripts/test-worker.sh` still
reports 166 passing; a whole-tree Qodana scan still reports 0.

### The optimisation half of the pre-1.0 audit — dead code, hot paths, payload, the token side

The last open bullet of item 7. Worked in the order the task set: dead code first (highest
confidence), then hot paths, then payload, then a costing of this file's own size — landing real
changes where the evidence supported one, and reporting a finding where it did not.

**Dead code.** Two genuinely unused exports found and removed, `worker/db.js`'s `userByEmail` and
`worker/search.js`'s `corpusIndex` — each called only from within its own file, never imported
elsewhere, never referenced by a test. Both are now private functions rather than deleted, since
each is still a real internal caller's dependency. `./scripts/test-worker.sh` reports 166 passed,
0 failed both before and after.

Everything else checked came back clean, and each check is a method that could have found
something and did not, not merely a look that did not:

- **Unused private members, unused parameters, "can be static", "never instantiated".**
  `.editorconfig`'s `dotnet_diagnostic.IDE0051/IDE0052/IDE0060/CA1801/CA1812/CA1852` were bumped
  to `warning` for a scratch `dotnet build --configuration Release -t:Rebuild`, then reverted (the
  repository was clean before and after — `git status --porcelain` confirmed both). Result: 8
  warnings, all `CA1812` ("apparently never instantiated") on types the `[engine/Models/*.cs]` /
  `[engine/TranscriptLibrary.cs]` exemptions already document as constructed only by
  `System.Text.Json` reflection — test-transcription `Chapter`/`Section` helper records and
  `TranscriptLibrary`'s `Envelope`/`TurnEnvelope`. No true positive.
- **CSS.** All 180 distinct class selectors in `web/wwwroot/css/app.css` trace to a real writer —
  `web/**/*.razor`, `web/**/*.cs` (for the five components that write a class from C#, per the
  browser guide's own warning), or `web/wwwroot/index.html`. That last one is the interesting
  miss: `.boot`, `.boot-title` and `.boot-sub` looked unreferenced against `*.razor`/`*.cs` alone
  because they are written into the static boot screen in `index.html`, which loads before Blazor
  does. A "what references this" sweep in this repository has to include the static HTML, not
  just the component tree.
- **JS.** All five `web/wwwroot/js/*.js` files' exported entry points (`ppMotion`, `ppCount`,
  `ppLand`, `ppSetMode`, `ppStore`, `ppDownload`, `ppPalette`, `ppTheme`, `ppSlider`) are called
  from `web/Services/*.cs` or a `.razor` file.
- **Razor components.** All 35 files under `web/Components/` are used as a tag somewhere else in
  the tree — no orphaned component.
- **Public engine/sheets types.** Spot-checked the ones that looked like candidates —
  `GearFormatter`, `RulebookStatLine`/`RulebookOption` (used structurally through the parent
  `RulebookProse` record in `BookText.razor`, not by their own type names, which is why a naive
  name-grep flagged them first), `ValidationSubject`, `CreationRulesModel`'s nested records. Every
  one traces to a real caller in a host project or a test that exercises it end to end.

**Hot paths.** `RulesRepository`'s ten `GetX(id)` lookups are already lazily-built,
cached-after-first-call dictionaries; nothing in `CostCalculator` or `DerivedStatsCalculator`
rebuilds a dictionary or re-scans a full rules collection per call on the paths a character build
exercises. Measured with a `Stopwatch` over the 20 published Heroes from `PrebuiltHeroes.cs`, 2000
iterations each (warmed up first so the lazy caches were already built):

| Call | Total (40,000 calls) | Per call |
|---|---|---|
| `CostCalculator.TotalCost` | 526.6 ms | 13.16 µs |
| `CalculateEdge` + `CalculateHealth` + `CalculateResolve` | 99.5 ms | 2.49 µs |
| `CharacterValidator.Validate` | 1001.0 ms | 25.03 µs |

All three are microseconds against a UI driven by human keystrokes and a test suite that already
runs in seconds. No optimisation was landed — the task's own rule was not to land one that could
not be shown faster, and nothing here is slow enough to be worth trading the clarity this codebase
spends deliberately (see `CLAUDE.md` on `CA1822` and explicit constructors). The benchmark itself
was a scratch xunit test, deleted before this commit — not part of the committed suite.

**Payload.** The one candidate that looked like dead weight — `PublicSans-Italic-Variable.ttf` —
is reached by `.power-entry.trait-sources` and `.sheet .quote`, both set `font-style: italic` in
`app.css`. Nothing else in `web/wwwroot/` (outside the generated `data/` the csproj copies, and
the 27 MiB payload item 5 already covers and puts out of scope) looked unreferenced.

**The token side.** Costed, not implemented, per the task's own instruction that the shape is the
owner's call. `PROGRESS.md` is 5,069 lines / 446,711 characters / 71,769 words — roughly
**90–110K tokens** to read whole (at the usual ~4 characters or ~1.3 tokens per word for English
prose), against **~15K tokens** for `Current state` + `Remaining work` + `How to maintain this`
alone (60,829 of those characters, lines 11–556 plus the header and footer). `Completed work` —
lines 592 onward at time of writing, 68 entries prepended newest-first — is the other ~89% of the
file. Proposed shape: keep this file's `Current state`, `Remaining work` and `How to maintain
this` where they are; move `Completed work` verbatim, same newest-first order, into a second file;
leave a one-line-per-entry newest-first index here in its place, linking into the archive. That
is an ~85% cut to the mandatory-read cost. Not built, because the risk is real and already named
in this item before this audit ran: a reader who does not follow the link loses the reasoning that
is this file's whole point, which is exactly what `RepositoryGuideTests` was built to catch for
the `CLAUDE.md` split — a tiling check that the split covers the original exactly, so a pointer
cannot rot silently. The same kind of check would need to exist for this split before it shipped,
which is a slice of its own rather than a side effect of an audit.

**What was deliberately left alone.** `cli/` was not swept for dead code with the same confidence
as the rest: it is the one area with no test harness (`WizardOrchestrator` and the six
`IWizardStep`s), so "nothing calls this" there rests on reading rather than on a suite that would
fail if the reading were wrong, and this audit did not remove anything it could not prove dead by
a green-then-red build or test run. `mcp/`'s public surface was checked structurally (all six
tools wired into `CharacterTools`, all reachable from `McpStdioTests`) but not swept file-by-file
the way `worker/` was.

### `CLAUDE.md` becomes an index, and ten guides carry the rest

**1,431 lines, and the cost was never that it was long.** The cost is visible in the pull request
immediately before this one. #79 shipped four features and `CLAUDE.md` records **one** of them: it
is silent on `RulebookProse`/`BookText`, silent on the explained sheet, and silent on
`DiscardedCharacter` — while a bullet at line 425 said discarding a non-current saved character
"is still a confirm, deliberately… a separate piece of work", which #79 had removed, and which
then sat there being wrong through three more merges. Nothing caught either, because nothing
could: a markdown paragraph that has stopped being true breaks no build.

Both halves of that are one failure. **A file nobody finishes is a file whose last two hundred
lines do not fire** — so the rules stop being read, and then they stop being written, because the
place to write them down is no longer a place anybody goes.

**The rule that decided the split, and it is the whole design:**

> A rule stays in `CLAUDE.md` if breaking it costs work regardless of what you were doing. It
> moves to a guide if you can only break it while working on that area.

So the stash rule and break-it-and-watch-it-fail are in the index — you can lose a day to either
while editing a JSON file — and "no component names a colour" is not, because you cannot break it
without opening `web/`. **290 lines, from 1,431.** Ten files under `docs/guide/`: the rules engine,
the browser front end, the printed sheet, the replay, the two assisted-creation surfaces, the
accounts server, the rulebook corpus, tests and static analysis, hosting, and the terminal wizard.

**Nothing was cut. The partition was proved to tile the source exactly**, which is the check worth
keeping rather than the outcome:

- Every one of the 1,446 baseline lines is covered by exactly one destination range — 1,446 rows
  counted with duplicates, 1,446 distinct, **zero gaps and zero overlaps**. Dropping one 100-line
  range from the cover reports exactly 100 gaps, which is the positive control: the checker fires.
- A set comparison over the content confirms it independently, and **it was broken and watched to
  fail** — deleting the Item Con bullet from `rules-engine.md` made the checker name that exact
  line, and restoring it returned zero. Twelve lines differ from the baseline in the end, and all
  twelve are cross-references deliberately repointed at their new files.

That mattered because a set comparison is precisely the shape this repository has been fooled by
before: the extractor audit rotated 1,492 section bodies onto the wrong headings with the suite
green. The tiling check is the one that cannot be satisfied that way.

**The split buys a smaller always-loaded file and costs a failure mode the single file did not
have: a pointer can rot independently of the thing it points at.** A guide nobody names is a guide
nobody reads; an index naming a renamed file sends a reader hunting for rules still in force
somewhere else. Both are silent. `RepositoryGuideTests` is the only thing that would notice — five
tests, each broken and watched to fail:

| Mutation | What went red |
|---|---|
| `replay.md` renamed to `recordings.md` | three at once: an orphaned guide, an unrouted guide, and a dead pointer naming `replay.md` |
| 120 lines appended to `CLAUDE.md` | the budget test, naming 410 against 400 |
| one table row rewritten as a prose sentence | the routing test, naming `cli-wizard.md` — **the prose mention survived and the test still failed**, which is the property it exists for |
| a guide's `PROGRESS.md` back-link removed | the back-link test, naming `hosting.md` |

Two of them carry positive controls on the instrument rather than on the subject: an empty guide
directory and an extraction that has stopped matching both satisfy every "no orphans" assertion
completely while proving nothing, which is the failure shape this repository has shipped four
times.

**The budget test is the load-bearing one and it is deliberately awkward.** Without it the file
regrows and the split has bought a year rather than a fix. 400 lines, with headroom over the 290
actually produced — a budget that fails on the next honest sentence teaches people to raise the
budget, which is the one outcome that makes it worthless. It is there to catch a *section*.

**The evidence item 7 recorded was weak, and it has been replaced rather than repeated.** That was
eleven agents given two or three sections each, none going wrong for want of the rest — weak
because somebody who had read the whole file chose the sections. The honest test is a fresh session
that has to find a rule it was not handed, and it was run. If an agent later breaks a rule that is
now in a guide, the routing table is the suspect before the reader is.

**The measurement, and it does not say what the split's author hoped.** Four fresh no-context
agents were each given a task that is a trap for a rule that had just moved into a guide — a panel
border (raw lengths, no colours), crediting the Item Con (forbidden outright), a `title`-attribute
tooltip (banned by name), and an MCP log line (breaks the JSON-RPC stream). **All four routed
correctly**, each quoting the routing table as the thing that sent it, and each found the rule that
made its task wrong. One went further than asked and reported that half its task — an MCP startup
banner — already existed.

**Then the control arm was run, and it is the useful half.** The same tooltip task, against a
checkout of this same tree with every mention of `docs/guide/` stripped out of `CLAUDE.md` — the
guides still on disk, nothing naming them. **It found both load-bearing rules anyway.** So the
routing table is not what makes the rules findable, and any claim that it is would be false.

What it changed is the cost and the confidence:

| | routed | control |
|---|---|---|
| files read | 5–9 | 17 |
| tool calls | 12–19 | 48 |
| found the guide at | step 2 | step 13, by accident |
| its own verdict | quoted the table | *"Nothing told me to look at `docs/`… Everything else was hunting"* |

The control reached `docs/guide/browser.md` only after a code comment had already told it the
answer, and it closed with *"I am **not** confident I found everything."* The routed agents did not
say that. **What actually saved the control was this repository's redundancy, not its own
searching** — it said so itself: both rules are *"stated in at least three independent places each
(code comment, prose doc, and a named regression test), which is this repo's pattern for anything
it considers settled."*

So the honest finding is narrower than the hypothesis and worth more than it: **the guides are
findable without the table; the table makes finding them cheap and makes the reader confident they
are done.** Triple-stating a settled rule is doing more work than either.

**And the control found a real defect while it was hunting**, which is the strongest argument for
the exercise. `RowDescriptionTests.ARowThatAlreadyPrintsItsDescriptionHasNoTip` asserts no Perks or
Flaws row carries a tip, and its doc comment claims a rule about *"the row's own caveat"* — but the
fixture set the tier and nothing else, so both tabs rendered an **empty chosen list** and the
assertion only ever reached the pickable `OptionRow`s. Demonstrated rather than argued: wrapping a
chosen Perk's name in `<Term>` — a tip on every chosen row — left the old fixture **green**, and
fails the tightened one on `Assert.Empty() Failure: Collection was not empty`. It now chooses a
Perk and a Flaw and carries a positive control that the chosen rows actually rendered, without
which every assertion is satisfied by a list that drew nothing. That is the same fault this file
records in four other spellings, found by an agent that was not looking for it.

### The shell spaces its own children with `gap`, closing the last item in "Still open from before"

`docs/HANDOVER.md` had carried this since before the pre-1.0 audit: `.shell` had no spacing
mechanism of its own, so a column of panels was evenly spaced only because `.panel` carried
`margin-bottom: var(--space-5)` — which meant any *non*-panel direct child got none at all. The
tier-cards grid was the one instance in the app, and it had already been patched with a
margin-bottom of its own to match, which is the workaround the "real fix" note was written
against.

**The hazard the note named no longer exists.** It said the fix wanted proofing on every route
because `.shell` also held the sticky budget strip and its negative-margin bleed. An unrelated,
earlier refactor already pulled the strip out to be a sibling of `<main class="shell">` in
`MainLayout.razor` — `web/wwwroot/css/app.css`'s own comment on `.budget` records it — so there
was nothing left to proof against the strip specifically.

**`.shell` is now a flex column with `gap: var(--space-5)`, and `.panel`'s margin-bottom was not
deleted.** It is relied on everywhere a panel is nested more than one level under a routed
page — inside `.editing` on the characteristics step, inside a tab, inside a dialog — none of
which is a direct child of the shell and none of which a `gap` on the shell can reach. Deleting
it globally would have collapsed every one of those onto its neighbour. Instead `.shell > *`
cancels every direct child's own vertical margin, placed after every rule that sets one so it
wins by source order rather than a specificity fight — margins do not collapse between flex
items the way they did in the shell's old block flow, so without the cancellation a panel
following a panel would get its own margin-bottom *and* the new gap, stacked rather than
replaced. The now-redundant margin-bottom on `.options.cards` (the tier-cards patch) was
removed with it. Print explicitly resets `.shell` to `display: block`, since a rem-based `gap`
has no business surviving onto paper even though every visible sibling but the sheet is already
hidden there.

**Measured, not assumed, and the two things worth recording from the measurement:** direct-child
gaps everywhere in the app were already `var(--space-5)` (16px) except two — `h1`→`p` and
`p`→first-panel were 12px (the headings' own smaller margin), and the gap before `StepButtons`
("Back"/"Continue") was 24px (`.nav-buttons`'s own, wider `margin-top`, which still carries its
extra separation — only the *cancellation* is scoped to the shell's direct children, not the
value). Both are now the uniform 16px. On every route that ends in `StepButtons` the two
changes cancel exactly (+4, +4, −8 = net 0 by the bottom of the page — confirmed with a
`getBoundingClientRect` harness against the actual rendered proof pages, before and after), so
the only visible difference is a handful of panels sitting 8px lower than before, for the height
of that one page. Routes with no `StepButtons` (`/rules`) keep the small, constant +4px from the
heading change and nothing more.

**The visual-regression pixel diff moves on all eight proof pages, as expected, and the deltas
are confined to this.** Confirmed by direct measurement (not by eyeballing the diff PNGs, which
read as far more alarming than the numbers): every gap in the rendered proof pages resolved to
exactly `16.0px`, no doubling anywhere, and the cumulative drift never exceeded the 8px/12px
figures above. The goldens are CI-rendered and cannot be regenerated from this machine —
`scripts/visual-regression.sh` was run locally (Docker) only to observe the deltas, and the
committed goldens were left untouched. **They need regenerating on CI
(`visual-goldens.yml`) after this merges**, or every run of the ordinary check fails on a page
nobody broke.

Guarded by `WebPresentationTests.TheShellSpacesItsOwnChildrenWithGapNotWithAMargin`, against the
parsed rule rather than a substring — `EffectiveValue`, exact-selector, per the file's own
warning about suffix matching. Broken by hand (the `.shell > *` cancellation emptied) and watched
red before being restored.

### Two scripts Cloudflare injects at the edge, one allowed and one deliberately not

Reported from the live site's console, not by a test: `static.cloudflareinsights.com/beacon.min.js`
refused, and an inline script on `/build` refused with a hash the policy does not carry.

**Neither could ever have been covered by a hash.** `scripts/write-cloudflare-headers.sh` hashes the
inline scripts of the `index.html` that was actually published — and both of these are injected by
the edge *afterwards*, into the response. The generator was working exactly as designed; there was
nothing wrong with the import-map hash, which still matched.

**The Web Analytics beacon is now allowed by host** — `https://static.cloudflareinsights.com` in
`script-src`. `connect-src` deliberately stays `'self'`: with automatic setup on a proxied domain the
beacon reports to this site's own `/cdn-cgi/rum` rather than to `cloudflareinsights.com`, so widening
it would have added an exfiltration destination in exchange for nothing.

**Cloudflare's Precursor bot script is left blocked, and that is the part worth remembering.** Its
inline body carries a per-request token, so its hash differs on every single response — two loads of
`/build` gave `sha256-kqhq0c…` and `sha256-C8a1Bx…`, which is what proves no generated hash can ever
catch up with it. Cloudflare's own answer is a CSP **nonce**, which it propagates into the tags it
injects by parsing the response header; a static `_headers` file cannot mint one per request, so that
route is closed here. And `'unsafe-inline'` is not the escape hatch it looks like: with a hash present
browsers ignore `'unsafe-inline'` entirely, so it would do nothing at all unless the import-map hash
were deleted alongside it — which is the whole policy.

**So one console error is still there, and closing it is a dashboard setting rather than repository
work**: Security → Settings → Precursor. Check Bot Fight Mode beside it, which force-enables
JavaScript Detections and injects the same way, and which the documentation says cannot then be
turned off separately.

Nothing was broken by either refusal — Blazor booted and the app rendered throughout. What was lost
was the analytics beacon and the precursor bot signal.

Verified by publishing the web project and running the generator against the real output, then
matching the emitted host-source against the origin of the blocked URL. `_headers` is generated and
not tracked, so there was no fixture to update; the policy is only genuinely exercised on the deploy.
[PR #80](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/80)

### The copy answered questions nobody asked

The owner read the app and said so: *"so much commentary on EVERY button click and EVERY step. Just
cut it to raw process and unclear actions."* The measurement, before touching anything: **1,943
words** of visible prose across **53 paragraphs of twelve words or more**, the worst of them 88
words above a recording and 81 above a list of findings. Four separate paragraphs were
three-sentence accounts of what a button would do, printed above the button.

It is **442 words in 27 paragraphs** now, and the longest is 27.

**What was kept is the test of the rule.** "Turn *Headers and footers* off under *More settings*" is
an instruction nobody could guess and it stays; "Printing gives you the sheet below on one page, or
as a PDF if you pick Save as PDF as the destination…" was fifty words of it and is gone. "**Download
to keep** is the only one that loads back in" replaces fifty-seven words distinguishing four
buttons. The derived-stats step's three-gotcha paragraph went entirely — it was a **verbatim
duplicate** of the `Formula` lines already printed under each figure.

**`NoParagraphOnScreenIsAnEssay` is what stops it coming back**, at 28 words per paragraph. A word
count is crude and it is the only thing enforceable: no pattern separates a real instruction from a
paragraph restating the heading above it, which is why `NoPageExplainsItselfToADeveloper` next to it
is a denylist. What a ceiling does catch is the shape the drift actually takes, which is one more
qualifying sentence. **Razor control flow splits a paragraph** rather than being counted through it —
the character list's closing sentence is an `@if` over two eleven-word branches, which read as
twenty-two words to a naive scan and as eleven to every reader. Both halves watched to fail: a long
paragraph restored (red, naming the file and the count) and the scan pointed at nothing (red on the
positive control, which is not optional — every other assertion in it is an absence and an app with
all its copy deleted would satisfy them all).

**One test's positive control had to move rather than the copy.**
`AskingForALinkSaysNothingAboutWhetherTheAddressHasAnAccount` asserted the page says "on its way",
which was the confirmation's old wording; the property it guards is the three absences beside it, so
it now looks for "Check your inbox". Restoring the sentence to keep a test green would have been the
tail wagging the dog.

### A passage of the book is set as the book sets it

The owner sent a screenshot of the LUCK entry on `/rules`: a stat line, a description and two Pros,
run together as **one 200-word paragraph**. Both surfaces that show the book's own words had a
`Paragraphs` helper splitting on `\n` under a comment claiming *"the extractor keeps them"* — and
for a Power's entry **the corpus holds no newline at all**, so the helper yielded exactly one
paragraph and the comment was describing a thing that does not happen.

**`sheets/RulebookProse` finds the structure the page prints, and only that.** Three markers, each
measured across all 1,523 passages before a line was written:

| marker | count | what it is |
|---|---|---|
| `Range • RankType • Cost` | **141** | a Power's stat line |
| `PRO Name (price):` | **102** | a Pro or Con printed inside an entry |
| `PRO +1 Hero Point` at the start | **51** | a generic option's own section |

**The 141 is the finding worth keeping**: it is exactly `powers.json`'s entry count, and the first
version of the test asserted 116 — the entries whose corpus heading matches a name in the rules
data. The other 25 are the Form, Transformation and Super Senses options, each printed with a stat
line of its own and each its own entry in `powers.json`, filed under headings the rules data spells
differently. The test reads the number off `RulesRepository` now, so the two stores are witnesses to
each other rather than one of them being a figure somebody typed.

**What it deliberately does not do is invent a paragraph break** — see [item 1c](#1c-the-extractor-loses-the-books-paragraph-breaks--closed),
closed in a later slice by reading the ones the page actually has off its own vertical spacing.
Splitting the remaining description on "For example," or on sentence count would have been a
presentation layer deciding where the author's paragraphs were.

**Six sections share a heading with a Power and are not that Power's entry** — Ch.6's ARMOR gear row
and COMMUNICATIONS base points, Ch.7's SWIMMING and LEAPING, Ch.4's HEALING, Ch.9's TIME TRAVEL — and
each is named in a theory, because a count on its own is satisfied by a pattern that fires
everywhere. `EveryWordOfEveryPassageSurvivesTheSplit` is the other half: 1,523 passages rebuilt from
their parts and compared word for word, which is the one property counting cannot check and the
failure hardest to see — a regular expression that swallowed a sentence leaves a passage reading
perfectly well and saying something else. Three mutations, each caught: a narrowed option marker, an
over-eager stat line, and a dropped first sentence.

**`BookText` owns how a passage is set**, replacing two hand-written copies of `.book-text` — one of
which carried a comment promising *"one idiom for the book's own words, not a second one for the same
text on another page"*, which is a promise a comment cannot keep. It is in `OwnedClasses` now.

**Two things the proof caught that no test could.** The stub's search snippet was the *whole*
passage, so the rules proof drew a 200-word wall directly above the panel that sets the same text
out properly — a fixture lying about the server's shape, now windowed. And an option's price was
`.hp`, which sets its text in capitals, so "+1 per rank" rendered **"+1 PER RANK"**; a price the book
writes in words is a phrase, not a Hero Point figure.

### The sheet says what every name on it means

> **Superseded in part, and the address is gone.** This shipped as a page of its own at
> `/build/sheet`; the owner then reported the link to it as the defect — explaining the sheet was
> never something to ask for — so `SheetView.Explain` defaults to on and that address is retired. See
> the entry above. Everything below about `Term` itself still holds; only where it is drawn changed.

`/build/sheet` drew the sheet with every name carrying its `data/rules` description, on hover and on
focus. The descriptions have been there the whole time and **only the editors ever showed one**: a
printed sheet says "Presence 6d" and "Plot Hook" and "TECH POWERS" and left a reader to know.

- **`Term` is `RankRow`'s trigger extracted** — a real button, a dotted underline in `--muted`, the
  description in a separate `sr-only` copy that `aria-describedby` names, and the visible tip
  `display: none` when shut. Never a `title` attribute. The id is derived from the name, or the
  replay's strongest guard — two renders of one character being identical — fails on a random one.
- **Its own address rather than a mode on the review step**, because the printed sheet is the
  deliverable and turning forty names on it into controls would change the document. `Explain` is off
  by default and `TheOrdinarySheetGainsNoControls` plus `TheReviewStepsSheetIsNotTheExplainedOne`
  hold it there; `ExplainingTheSheetChangesNoneOfItsWords` holds the two sheets word-for-word equal.
- **On paper a term is a word.** The tip never reaches a printer — hovering does not happen there —
  but the underline and the help cursor would, so the print block drops both, asserted through the
  resolved cascade rather than off the print block's own declarations. `@media` adds no specificity.
- **The test helper was nearly the bug.** `Visible` had to model how a browser renders: it drops the
  two hidden copies, inserts a separator **only at a block boundary**, and collapses whitespace that
  is already there. A helper that joined children with a space would turn `Armor8d` into "Armor 8d"
  and report it fixed — which this repository has shipped, twice. Watched: reintroducing the missing
  space failed the new test *and* the three pre-existing `SheetRenderTests` ones.
- Five mutations, each caught by exactly the intended test: `Armor8d` back, `Explain` defaulting on,
  a description wired to the name, a Source heading losing its term, and print keeping the underline.

**The proof page is where the two judgements that matter were made**, neither of them checkable by
assertion: whether a dotted underline under forty names reads as marking or as noise, and whether a
tip hanging off a word inside a three-column sheet lands somewhere readable rather than as a sliver
down one column. It is a golden now, at 1280×1700 because a 900px frame cuts the sheet
mid-Powers and neither judgement survives a crop.

**Every golden in this slice came from `.github/workflows/visual-goldens.yml`, not from a local
run.** Master moved generation into CI so both sides of a comparison are the runner's own Chrome and
says plainly not to regenerate from a developer machine; a first attempt here did exactly that and
the PNGs were thrown away and re-made properly.

**All seven moved, and each is accounted for** — an unexplained golden is a regression signed off by
nobody. The explained sheet is new. Both front doors and the four shell captures moved because the
shell proof renders the tier page and this slice trimmed three of its paragraphs, so everything below
shifts up about 24px (18.6% of pixels, in one band from y≈266 down). The rules reference moved
because a passage now comes apart into a stat line and its options.

**The first CI attempt failed on `shell-villain-light` at 27.6%, and that was not this branch.** It
is the knife-edge master's own comment describes, and the fix — `--run-all-compositor-stages-before-draw`
— landed on master (PR #78) while this work was in progress. Merging master again was what made the
capture deterministic; nothing here needed its own workaround.

### A discarded row you were not looking at had nothing behind it

**The confirmations came off every row when undo arrived** (`da9f249`), on the reasoning that the
one on screen has `CharacterSession`'s buffer behind it and a different row *"was never asked about
either, because switching away from it already left it saved under its own id and this cannot touch
that copy"*.

**The first half of that is true of `Open`. The second half is not true of `Delete`, which is
precisely what destroys that copy.** `CharacterSession` holds the sheet being edited and never the
others, so a background row — twenty minutes of work switched away from — went on one click with no
question and nothing to bring it back. The row a reader is least likely to be weighing carefully had
the least behind it.

**`DiscardedCharacter` is the missing half of the same mechanism, not a second one.** It reads the
row back *before* the delete and can write it to the same id again, and it borrows every rule the
session's buffer already follows:

| | session's buffer | `DiscardedCharacter` |
|---|---|---|
| holds | the sheet on screen | one deleted row, by id |
| armed by | `StartAgain`, `LoadSample`, `ReplaceWithUndo` | the manager's `Delete`, for a row that is not open |
| skipped when | `IsWorthKeeping` is false | the same predicate |
| closes on | `Version` moving | the same |
| offered from | the banner's `.save-status` | the same region, checked second |

**They can never be armed by one click**, which is what makes one region safe for both: deleting a
background row raises no change event, so it cannot touch the session's `Version`. The session's is
checked first anyway — it is the one whose window closes on the very next edit, so it is the one more
likely to be about what just happened.

**Read with `ReadAsync`, never `OpenAsync`.** The latter moves the current-character pointer on its
way past, so reading a row in order to remember it would switch the app to the character being
discarded — and the next autosave would write the sheet on screen over the id just restored.

**A refusal is reported in words, because an undo that silently did nothing is the worst outcome
available** — the reader believes their character is back. The account cap is the refusal that
actually happens: discard a row from a full account, build something in its place, and there is
nowhere to put the old one. `RestoreAsync` is the only method on that store that answers whether the
write landed; every other one is an autosave, where failure is not worth interrupting somebody over.

**An offer does not survive a change of who is here.** The store picks the account or this browser
per call from whoever is signed in *now*, so an offer left standing across a sign-out would write an
account's character into the anonymous slot. The identity key is captured and compared rather than an
event being listened for — the same decision `RulebookReader` made, for the same reason.

**The open-row branch was covered by nothing, and could not be.** Which row is open resolves through
`ppStore`, and bUnit's loose interop answers null to every read, so `_currentId` was always `legacy`
and no account row ever matched it — every test in `CharacterManagerTests` exercised the not-open
half. Planting the pointer with `JSInterop.Setup` fixes it and also closes the "open now" caveat this
file has carried since the manager shipped. Three mutations, each caught: nothing remembered before
the delete, a restore reporting success regardless, and the identity guard removed.

**One thing recorded rather than changed.** `StartAgain` raises `Changed` *before* it fills the
buffer, so the redraw that event triggers still sees `CanUndo` false; in the app the completed
write-through fires `Saved` a moment later and the offer appears then. A render test has to ask for
the second pass explicitly, which `TheSessionsOwnUndoWinsTheRegion` says in a comment rather than
papering over.

### The pre-1.0 audit's forty-eight survivors, and the two front-end items that were left

A twelve-agent adversarial audit applied **126 mutations to the three suites and 48 were not
caught**. This is what was done about them, plus the two Phase 3 items `docs/FRONT-END-PLAN.md`
had carried as the largest remaining front-end work. Eleven streams, worked in isolated worktrees
and merged one at a time onto a single integration branch so that eleven streams cost **one**
deploy; every merge was verified by re-running the suites rather than by trusting the stream's own
report, and the highest-value guards were re-broken by hand afterwards.

**Per-stream mutation tables are in [`docs/notes/`](docs/notes/README.md)** — evidence rather than
status, kept out of this file because eleven of them would bury the record they are meant to be.

#### Two things the audit called production bugs that were not, and one it missed that was

The audit reported "a negative purchased rank makes a character free and legal". **It does not**:
`CheckQuantities` catches it, and `SelectedPower("blast", -400)` binds to `PurchasedRanks`, which
that guard reads. What was true is the sentence underneath — weakening the guard leaves the suite
green, because five of the six quantity branches had no isolating test. The framing is worth
recording, because it would have been easy to "fix" a validator that was already right.

**Why five branches were invisible**: they were reachable only through one sheet carrying all six
fields negative at once, so a meta-check asking whether *some* `NEGATIVE_RANK` or `NEGATIVE_UNITS`
issue exists stayed satisfied whichever single branch was disabled. Each now has a test on an
otherwise-legal sheet asserting the specific issue that branch constructs. One detail from the
mutation runs is worth keeping: for the Ability and Talent branches, `IsValid == false` alone would
**not** have caught the mutation, because `TRAIT_BELOW_MINIMUM` independently invalidates any rank
below 1d. Only the assertion on the code itself bites there.

**The one the audit missed was real.** Importing a character called `CharacterSession.Restore`,
which does not call `NotifyChanged()` — so an imported character was not autosaved until some later
edit happened to touch it. Found while replacing the confirm dialogues with undo.

#### Four guards that did not guard what their docstring claimed

- **`light-dark(white, black)` passed `NoComponentNamesAColour`** — the one rule the four-palette
  architecture rests on. Three detectors and all three missed it: `light-dark` is CSS Color 5 and
  was in none of the six function names the scan knew, and its arguments follow `(` and `,` rather
  than the `:` the keyword regex required. The keyword position anchor is gone rather than widened
  (a colour is equally a colour in `border: 1px solid black`), bounded by `(?<![\w-])`/`(?![\w-])`
  rather than `\b`, because a plain word boundary treats the hyphen in `white-space` as one. The
  function list gained `color`, `light-dark`, `color-contrast` and `device-cmyk` and **is still a
  denylist that will rot again** — an allowlist was tried and rejected, because the razor scan runs
  over files whose `@code` blocks are full of unrelated calls. Verified by hand with the strongest
  form of the mutation: `light-dark(var(--surface), var(--ink))`, whose arguments are legitimate
  tokens, so only the function path can see it. It fails on the function path.
- **The engine had no filesystem-isolation test at all**, though `CLAUDE.md` asserts the property
  in two places, one of them *inside `TheEngineHasNoNetwork`'s own doc comment*. A reachable,
  non-throwing `File.Exists(...)` in `CostCalculator.AbilityCost` left all 3,734 tests green. The
  guard bans `File.`, `Directory.`, `FileStream`, `StreamReader`/`StreamWriter` and a written-out
  `using System.IO;`, with comments blanked first (three files name these APIs as history) and
  `FileSystemRulesSource.cs` excluded by name as the sanctioned seam. `Path.Combine` stays legal.
- **`EveryAddressTheBrowserAsksForIsOneTheServerAnswers` was substring-matching every worker file
  concatenated together**, so renaming the real routing condition passed because the literal
  survives in `worker/errors.js`'s `KNOWN_ROUTES` — a list built for an unrelated purpose that
  happens to name the same addresses. It now reads `worker/index.js` alone and extracts the routing
  *conditions* structurally: thirteen exact `path === '…'` routes and three `path.startsWith('…')`
  prefixes, which is also what correctly resolves `/api/characters/{id}`.
- **`EachOwnerActuallyWritesTheClassItOwns` was `Assert.Contains($"\"{cssClass}", source)`**, so
  renaming `panel` to `panelish` passed all fifteen cases — the exact "satisfied by writing
  nothing" shape the test exists to prevent, in disguise. It reuses the attribute tokenizer twenty
  lines above it for the eight classes written as markup, and a small lexer for the seven built in
  C#. A whole-file literal scan was tried and rejected because `OptionRow`'s
  `role="@(Navigable ? "option" : null)"` would have kept masking a broken `RowClass`.

#### A denylist of spellings cannot make a verdict honest — so every harness got a twin

`MustNotShow` banned `say(true` in the sticky harness. **`|| true` is textually distinct and walks
straight through it**, and the weakened harnesses reported `PASS` against a sticky strip that moved
the full 483px it should have stayed pinned against, and against a real View Transition opening for
a visitor who asked for none. Reproduced exactly before anything was written.

The answer is a negative control rather than a longer denylist. Each of the seven parameterised
harnesses now also writes a **deliberately-broken twin**, driven by the byte-identical harness
script — an injected `<style>` defect for the geometry ones, a derived broken copy of the shipped
`.js` for the script-driven ones, with `WithDefect` throwing if the line it substitutes has moved,
so a twin cannot silently stop reproducing its defect. CI requires nineteen verdicts: every real
page `PASS` **and** every twin `FAIL`.

**Every twin says `FAIL` and not the resting `measuring` text**, which is the distinction three of
this repository's four historical guard failures turned on: a feature that did not run mistaken for
a feature that worked. Driven through Docker Chrome by hand after the merge, and again after both
front-end streams landed.

A structural "the verdict is derived from the measurements" check was considered and rejected: it
would be a second unbounded-spelling denylist of the same shape that just failed, and the twin
subsumes it by proving behaviour rather than guessing at structure.

#### The pixel check could not see a whole-page colour change

Two independent holes in `scripts/visual/diff.mjs`, both reproduced before being closed:

```
uniform +20 per channel:  OK  0/1152000 pixels differ (0.000%)      exit 0
24×24 opaque block:       OK  576/1152000 pixels differ (0.050%)    exit 0
```

The first is the serious one — a `channelThreshold` of 24 means a per-pixel count can never see a
uniform sub-threshold shift, **and halving the threshold just moves the exploit to +11**. So there
is a second, independent measure: mean absolute channel difference over the whole image, RGB only,
failing if either measure is exceeded, with the summary line naming which fired. `channelThreshold`
is 3, `maxDiffPercent` 0.02 (230px at 1280×900, so a 24×24 block fails by more than twice), and the
new mean threshold 0.5 against a uniform +20's mean of 20.

`diff.mjs` and the hand-written `png.mjs` codec beneath it had no tests at all; there are now
fourteen, synthesising images rather than committing fixtures, run in CI with the same
count-assertion the accounts step uses.

#### The goldens are generated by CI now, and the four palette captures are back

The four `proof-shell-*` captures had been removed, recorded in the script as *"a retreat rather
than a decision"*: they compared a golden written by a developer machine's digest-pinned Docker
Chrome against CI's own Chrome, and `shell-villain-light` disagreed by exactly **32,462 pixels**
across repeated CI runs while the three beside it came back identical.

The removal named the fix and this is it. `.github/workflows/visual-goldens.yml` regenerates them
on `ubuntu-latest`, from the Chrome that compares them. It is **`workflow_dispatch` only** — a
golden regenerated as a side effect of an unrelated change is a regression signed off by nobody —
and it **commits nothing**, uploading the PNGs for a person to look at first. It also re-runs the
ordinary comparison against what it just wrote: two captures seconds apart from one Chrome must be
pixel-identical, and if they are not, the pages are non-deterministic and committing them would
install a permanently flaky check by another route.

The missing-golden message now points at that workflow rather than at `--update-goldens`, which off
Linux writes back exactly the cross-renderer golden that caused this.

**All seven pages are checked and all seven come back pixel-identical in CI.** Three things had to
be found on the way, and two of them were only findable because the comparator had been tightened
first.

**The step that uploads the diff images had never uploaded anything.** `upload-artifact@v4` excludes
dot-prefixed paths by default, the path is `.visual-regression/`, and `if-no-files-found` defaults
to `warn` — so on every failing run it logged *"No files were found with the provided path"* and
went green. The diff PNGs it exists to hand you have never once been reachable. Found by trying to
use it. `include-hidden-files: true` fixes it and `if-no-files-found: error` stops the same silence
recurring.

**The rules reference had been flaky the whole time and nothing could say so.** Two CI runs, on
commits that changed nothing that page renders, disagreed on **59.6%** of its pixels with a mean
channel difference of 5.622 — and the diff image is unambiguous: three panel interiors solid, the
headings outside them untouched. `.panel`'s entrance animation, caught at two different moments.
`--virtual-time-budget` is a *wait*, and a wait is a race settled only as reliably as the page is
fast; that is the page with the most content on the site. **At `--channel-threshold 24` most of
those deltas did not count as differing at all**, so it read as identical while being flaky. Fixed
by capturing with `--force-prefers-reduced-motion` rather than by guessing a longer budget: the app
zeroes its three duration tokens under that media feature, the motion harness proves it every CI
run, and `both` means the resting frame is the same frame either way — so the capture is the settled
page by construction rather than by arriving late enough.

**And the 32,462 pixels turned out to be the entrance animation, not a renderer difference at all.**

This paragraph originally recorded a confident and wrong answer, and the correction is worth more
than the answer was. Sampling the band at the bottom of `shell-villain-light` showed CI painting
`--surface` (255,253,249) where the Docker Chrome painted `--bg` (248,243,236), and that was read as
the last panel's bottom edge landing a few pixels apart in the two renderers. The measurement was
right; the conclusion was not. **Those are the painted and unpainted states of the same panel.**
`.panel` carries `animation: rise … both`, and a `both` fill is backwards as well as forwards — the
element holds the keyframe's starting `opacity: 0` from layout until the animation *begins*, so
whichever capture landed in that window showed the page ground straight through the panel.

It surfaced properly when master went red on `shell-hero-light` immediately after the merge, on an
**identical tree hash** to the run that had just passed: both `.panel` regions at opacity 0 while the
`.card` elements between them matched exactly, which is what named the cause — only `rise` was
involved. Unreproducible across four local captures, so a load-dependent race.

**`--force-prefers-reduced-motion` on the capture was not enough, and could not have been**, because
`theme.css` collapsing `--enter` to `0.01ms` shortens the *run* and the problem is the window
*before the start*. The fix is `animation: none` under reduced motion for the four rules that carry
`rise` — which the print block has carried for this exact reason for far longer ("an animation with
`both` fill can leave an element at its starting opacity if print runs before it completes"), and
which is a real accessibility improvement besides: a reader who asked for no motion was still being
shown a panel arriving.

**That closed the renderer gap completely.** This script's Docker Chrome and the CI runner's own
Chrome now produce byte-for-byte identical PNGs for all seven pages, `shell-villain-light` included,
so a developer can run the check locally and believe the answer. The Windows-versus-Linux rule is
untouched and still absolute; what is gone is the Linux-versus-Linux difference, which was never a
difference.

Guarded by `ReducedMotionStopsTheEntranceRatherThanShorteningIt`, which reads the animated selectors
out of the stylesheet rather than listing them, so a fifth cannot be added uncovered. Broken three
ways. **The third mutation was null on the first attempt** and is recorded because it looked like a
hole: renaming the keyframe to `risen` still matches a prefix scan for `rise`.

#### Rules data that read as transcribed and was not

Four areas had no transcription pinning them to the book — talents' `linked_ability`, 49 of 53 flaw
types, Power baseline prerequisites, and the graded Cons' variant→value mapping (only the multiset
of prices was checked, so swapping two grades passed). All four are now transcribed from
`data/rulebook/` with page citations, in the `CanonicalPowers.cs` shape, each with a positive
control that the transcription covers **every** entry in its rules file.

**No disagreement was found** in three of the four: all 53 flaw types, all 27 baseline prerequisites
and all 4 graded-Con mappings matched. The fourth is the finding.

**The rulebook prints no Talent→Ability table at all.** The only explicit pairing anywhere in
Ch.1–2 is Covert : Agility on p.17, via the half-Agility substitution rule. The other eleven
`linked_ability` values are **this project's own editorial grouping**, present unsourced since the
first rules-data commit, used only to group Talents under an Ability heading in the CLI and browser
displays. Nothing is wrong — unlike flaw `type`, which feeds the Resolve formula, this has no
mechanical consequence — but `data/rules/` was carrying eleven values that read as transcribed and
were not. They are recorded as what they are and pinned as a regression snapshot rather than given
invented page citations.

The flaw types are additionally pinned to their **consequence**: retyping `obligation` from
`plot_hook` moves the computed Resolve from 25 to 24, and a test asserts that as well as the string.
Verified by hand after the merge; both tests fire.

#### `PageReader` had no tests, and now regenerating the corpus is a check

Disabling `CrossesGutter` reproduced the historical two-column corruption byte for byte with the
suite green, because nothing regenerated the corpus from the PDF. PdfPig's `Page` has no public
constructor, so `Read(Page)` became a one-line adapter onto a `RawLetter` record and the logic runs
against an `internal` overload — the same shape `ColumnLayout` already used. Fifteen tests over
made-up pages, since the committed corpus cannot show you a layout the book happens not to contain.

**The seam was verified by regenerating the whole corpus from the real PDF and comparing blob
hashes: all ten chapters, 1,523 sections, byte-identical.** `CLAUDE.md` says nothing does this; it
is now a one-command check, and it is the thing that actually closes this item.

One of the ten mutations survived first time. Rather than being reported as covered, it was traced
to a fixture whose gap sizes accidentally satisfied the *other* splitting mechanism, the fixture was
tightened, and it was re-run to red. One named behaviour — a heading with no body qualifying the
headings beneath it — turned out to live in `Program.cs`'s outline stack rather than in
`PageReader`, and is recorded as out of scope rather than quietly dropped.

#### `GearFormatter` and the JSON export

Twelve tests on the formatter, asserting whole strings rather than fragments, per the `Armor8d`
lesson. Twenty-five on the export, parsed with `JsonNode` and pinned key set by key set rather than
as a whole-document snapshot — the reasoning parallels the goldens warning above, that a snapshot is
regenerated thoughtlessly the first time it fails. It caught a real asymmetry: gear's `pros`/`cons`
are bare id strings while a Power's are `{id, variant_key}` objects.

#### `search_powers`: 24 of 33 to 25 of 33

The stopword list already existed to drop words carrying no information about a Power, and stopped
short of ordinary prepositions and pronouns — **"through" alone tied twenty of the twenty-two
candidates** for "walks through walls". Widening it shrank those coincidence clusters. One
expectation moved unmet→met; none moved the other way.

Two other approaches were measured and reverted: symmetric stemming (fixed "mind"/"minds" for
Telepathy, strengthened Cloud Minds' coincidental name match on the same word, **net −1**), and
tie-breaking by matched-term breadth (**net 0**, and it inverts the documented
name>id>tag>category>description hierarchy for every tied pair rather than only this set's).

The ratchet is at 25, and that is what was achieved rather than a padded figure — raising it to 26
reports *"meets only 25 of 33 … down from the recorded baseline of 26"*.

**The two cases item 4 names by hand stay unmet, and the reason has changed.** They are not
reachable by any word-matching change at all: Phasing's description says "solid matter" and never
"walls"; Blast's says "a damaging ranged attack", with the damage type left to the player, so
nothing in it is about fire. Closing those needs vocabulary in the data, which is a different and
better-scoped slice than tuning the scorer.

**That slice landed — see item 4, now closed at 33 of 33.** The vocabulary is a wider `tags`
field, reusing the mechanism the browser already calls `Keywords`; the scorer above is untouched.

#### Phase 3: a broken rule says so on the row that broke it

`web/Services/SheetFindings.cs` routes a `ValidationResult` to the row that owns each issue, using
the `SubjectKind`/`SubjectId`/`OwnerId` fields `CharacterValidator` already fills in — which is why
those fields exist, so a consumer does not parse the message back into the facts it was built from.
No arithmetic and no engine change. `RowFinding.razor` renders them **visibly, never hover-only**:
WCAG is explicit that anything carried only by a tooltip is information some readers do not get, and
the `aria-describedby` target the hover-description work left on every row was the tempting wrong
place to put this.

Error and warning are told apart by a printed word **and** a border style, not by colour alone. No
new colour token and no new contrast measurement were needed — `--danger` and `--heading` on
`--panel` are already held to 4.5:1 across all four palettes.

`HP_BUDGET_EXCEEDED` and the tier findings are deliberately left unrouted: they belong to no row,
and the budget strip already exists for the first of them.

#### Phase 3: single-level undo, and what "the three buttons" had become

The plan was written against an older `ChooseTier.razor`; the samples had moved to
`/admin/portfolio` and the confirm logic into `CharacterManager`. The real family is five: the tier
page's "Start a new character", the per-row "Discard" of the current row, the portfolio's two sample
buttons, the replay's "Open in the editor" — and "Import a character", which had no confirm at all
and is where the autosave bug above was found. All five now act immediately and buffer what they
replaced.

The buffer is a **JSON snapshot** in `CharacterSession`, never the live sheet — the replay has a
recorded bug of exactly the shared-reference shape — plus a version counter. `CanUndo` requires the
session's `Version` to still match the moment it was armed, so the window closes on the first edit
with nothing needing to remember to clear it. It is announced through `MainLayout`'s existing
`.save-status` live region rather than a second one.

**No `Ctrl-Z` binding**, deliberately: there is no general shortcut manager to extend, `palette.js`
says in as many words to resist growing it, and taking `Ctrl-Z` from the finishing step's four text
fields is real untested risk for one shortcut.

Discarding a *different*, non-current saved character is deliberately left as it was — deleting it
does not touch the live session, so restoring it is a different mechanism than the sheet buffer, and
it is a separate item rather than folded in.

### Assisted builds default to full strength, and a Villain has no Resolve

Two pieces of guidance the owner gave while building the first campaign Villain, plus three
claims in `mcp/QUESTION-POLICY.md` that were found stale on the way through. The document is
embedded and served verbatim as the `creation_guide` tool, so a wrong sentence there is a wrong
sentence in every conversation the MCP server has; the skill got the same changes because the two
teach the same loop to different readers and are exactly the kind of pair that drifts.

#### Build at full strength first, and trade down out loud

The default had been to build *tastefully* — a modest-sounding concept got a modest sheet, and
the leftover points were never mentioned. That is a decision made on somebody's behalf and hidden
behind prose about the character being unassuming. **Trading down is a decision somebody makes out
loud; trading up is a correction they have to notice they need**, and a sheet six points weaker
than it could be looks exactly like one that is not.

"Strongest" needed defining, because the rulebook has no single power axis. Six levers are named:
spend to `remaining: 0`, always take a package, headline Trait to the Trait Cap, prefer a Power
that arrives with a baseline rank, use only the Cons the character would genuinely suffer, and
know the three derived-stat levers. Question 3 of the question policy — *what are they
deliberately ordinary at* — now defaults **mechanically** when the person shrugs, rather than
guessing a modest-sounding weakness on their behalf.

Four limits go with it, because the principle without them is a licence to rebuild somebody's
character into a better one: it does not overrule a stated weakness, drop a Flaw or Con they asked
for, exceed the budget, or make the character *cheaper* rather than stronger.

#### Only Heroes have Resolve, and three purchases turn on it

The owner said a Villain does not get Resolve. He is right, and the book says so twice in Ch.2 —
*"Only Heroes have Resolve"* — with Ch.5 giving the GM **Adversity** instead, spendable "on behalf
of any NPC whether they're Villains, Foes, Minions, or Extras". Ch.9's *"the only difference
between Foes and Villains is that Foes have less Health"* is not a contradiction: it compares two
kinds of antagonist, and Resolve is not in scope of that comparison.

The engine builds Heroes, so it returns a Resolve figure for a Villain regardless. **That figure
is noise, and the guide now says not to quote it.** Three consequences change what gets built:

- **Determination is a dead buy on a Villain** — Hero Points spent on Resolve. It is the one
  purchase that goes from good to worthless on this distinction alone.
- **Plot Hook and Condition Flaws grant nothing mechanical**, since what they grant is starting
  Resolve.
- **The Trait Cap trade does not exist.** Resolve comes off the gap between the cap and the
  highest relevant rank, so it is what a *Hero* pays for a rank at the cap. A Villain pays
  nothing, which makes capping strictly correct rather than a trade-off.

That last one had already been shipped as advice in this session and was wrong: a Villain build
was offered against a thematic alternative on the strength of "capping costs you Resolve", a trade
that does not exist for the character in question.

#### A Villain's Flaws are the players' handles

Following from the above rather than standing beside it. For a Hero a Flaw is a bargain — a
drawback bought with the Resolve it pays out. **With the payout gone, the drawback is all that is
left**, which makes the Flaw slots the one place on a Villain's sheet where the GM decides how the
character can be beaten. Creation allows one to three and no more, so a slot spent on colour is a
handle the party does not get.

The test is whether the Flaw bites without a Resolve payout to notice it. Most do not —
Absentminded, Clumsy, Quirk, Decorum, Notoriety, Creepy, Unusual Looks, Broke and Illiterate are
pure flavour on a Villain. The guide names three categories and says to take one from each, so a
party can win by fighting, outthinking or exposing them rather than only the way the GM imagined:

- **In the fight** — **Vulnerability** is the strongest in the book, halving active *and* passive
  defence against one attack, effect or weapon as a printed rule rather than a Resolve trigger.
  Severe Reaction, Severe Requirement, Power Limits, Finite Power, Light Sensitive, Night Blind,
  Impaired Sense.
- **In their behaviour** — Code, Severe Compulsion, Frenzy, Hidden Agenda; Flashbacks/Guilt is the
  rare behavioural Flaw with printed numbers.
- **Outside the fight** — Secret, Secret Identity, Relationship, Wanted, Obligation.

#### Three stale claims in the served document

Found by following the Resolve question through, and all three would have misled a conversation:

- **"A Villain has no Hero Point budget"** — retired when `UnlimitedBudget` became a toggle either
  kind of character can carry. `CLAUDE.md` already recorded that "no budget" was never a fact about
  Villains; the served document had not caught up.
- **"There is no Villain flag to set"** — `IsVillain` is a real field. A model following that
  sentence omits it and the sheet draws in the wrong palette.
- **Nothing about Resolve at all** — the gap above.

`IsVillain` is now documented in both field lists as **presentation only**, proved rather than
asserted: flipping it on a real character and diffing the reports gives byte-identical output, and
a test asserts equal cost, equal Resolve and an equal issue list across the flag.

#### Everything watched to fail

Five new tests, each broken before being trusted, per the discipline in `CLAUDE.md`:

- Stripping the limits from the optimise-first section while keeping the direction — **red**.
- Inverting the direction while keeping the limits — **red**, so neither half satisfies the guard.
- Stating the Resolve rule while dropping all three consequences — **red**.
- Restoring each retired claim, separately — **red** both times.
- Collapsing the three Flaw categories, and naming a Flaw id the rules do not have — **red** both.

`SkillDocumentationTests` gained the `Flowed` helper the MCP tests already had, since the file is
hard-wrapped and a raw substring assertion passes or fails on where the wrap landed. The handles
test also carries a positive control: every Flaw id the guide recommends is looked up in the rules,
so the advice cannot quietly name one that would be refused on submit.

**Not covered, and not coverable:** whether a recommendation is *good*. The tests hold the document
to naming the categories and to naming real ids; that a GM would actually want Vulnerability on a
given Villain is a judgement no regular expression reaches.

### The manager panel stopped creating empty characters, and got a hierarchy

The owner's report — "the start a new character section is awkward, and often has empty
characters to discard" — turned out to be two different problems, and the second was hiding
behind the first.

**The cause: `ApiCharacterStore`'s autosave PUT unconditionally, and `PUT` creates the row if the
id is new.** There is no separate "create a character" step for an account — the first
write-through *is* the create. So switching the Hero/Villain palette or the sandbox budget toggle
before choosing a tier fired `CharacterSession.NotifyChanged` exactly the way choosing a tier
does, and this autosave wrote the untouched sheet straight to the server: a real, listed, empty
character, from one click nowhere near "Start a new character." `CharacterSession.HasSomethingToLose`
already answered "is this worth keeping" for the manager's own delete/discard confirmations; it is
now `CharacterSession.IsWorthKeeping(CharacterSheet)`, a static so the autosave path — which only
ever has the sheet, never a session — can ask the identical question before writing through.
Nothing is deleted or repaired; the guard only stops a worthless sheet from being written in the
first place, and the very next substantive edit saves everything, including whatever preference
was set first. `AccountTests` pins both directions, and the mutation was watched to fail (removing
the guard reintroduces the exact server row).

**The panel itself, once the cause was fixed:**

- **The count is stated once**, in the panel's `Aside`, not printed again in the closing sentence.
- **The empty state names the action and the right place a character lands** — `EmptyState`, the
  same component every other empty list in this app uses, and it no longer tells a signed-in
  account "remembered in this browser," which was simply false: nothing here has ever put an
  account's character in browser storage.
- **The file picker is a real, styled `<label class="btn small">` beside a visually-hidden
  `<input>`**, not the operating system's own "Choose File" chrome sized the same as "Start a new
  character" next to it. One label — `Field` is not used here, so there is nothing to double it —
  and its own focus ring is drawn on the sibling label with a plain adjacent-sibling selector,
  since `for`/`id` association (deliberately not nesting) means `:focus-within` cannot see it.

`CharacterManagerTests` (new) renders both identities in both states and holds the count, the
wording, and the control's classes and `for`/`id` pairing to all of the above; each guard was
watched to fail before landing. `scripts/visual-regression.sh --update-goldens` was re-run because
the panel sits inside all four `proof-shell-*` goldens — the other three (front door × 2, rules
reference) came back pixel-identical, confirming the change is contained to the one component.

### A signed-in visitor can change what they are called

The name a fresh sign-in gets is the email's own local part, and there was no way to change it —
the owner's own account was stuck reading "tabletop". `PUT /api/me/display-name` changes the
caller's own row and nobody else's, scoped by the id off the session rather than anything the
request names; trimmed, refused for not being a usable string, a control character, or more than
60 characters, and reset to the email's local part rather than refused when it comes in blank
after trimming — the same value a fresh sign-in already gets, so a cleared name looks like one
never set rather than a banner naming nobody while `Identity.IsSignedIn` still read true.

**Uniqueness is deliberately not checked**, on either side of the wire — a name is free text
shown in a banner, never a permission or a claim of identity, and asking "is this name taken" is
the same oracle the invitation list exists to keep this site from answering about addresses. The
box is on the account panel already on `/signin`, and the banner and the panel both pick the new
name up from `Accounts.Changed` without a reload, the same event a sign-in already raises.

Every new guard was broken and watched fail: the control-character check, the length cap, the
blank-resets-to-email-local-part rule, the same-origin check, the signed-in check, the
own-row-only scoping in the SQL, and the two client-side syncs that keep the name box and the
banner from going stale. `docs/ACCOUNTS-SETUP.md` also had `d1/migrations/0004_error_log.sql`'s
worked example pointed at a database named `prowlers-accounts`, which does not exist — the real
one is `prowlers-and-paragons`; fixed in the same change since it was found while this was open.

### The recordings move behind the gate, and out of every visitor's startup fetch

**The account gate on the replay pages was a front door rather than a lock, and this closes it.**
The recordings and the pages that play them back moved behind `AdminOnly` in an earlier slice, and
that slice said plainly what it had not done: the four transcripts were still ordinary files under
`wwwroot/data/transcripts`, so anybody who knew a filename could fetch one straight through, and
every visitor's browser fetched all four before its first render whether or not that visitor could
ever reach a page that shows them. Both are fixed the same way the rulebook corpus already was.

- **Bundled into the worker, not staged into `wwwroot`.** `worker/transcripts-corpus.js` bakes
  `data/transcripts/*.json` into an object literal at build time, the way `worker/corpus.js` bakes
  the rulebook — `scripts/inline-transcripts.mjs` mirrors `scripts/inline-rulebook.mjs` line for
  line, including reading the directory rather than naming files, so a fifth recording needs no
  edit to the bake script. A guard in `tests/worker/transcripts.test.mjs` refuses a bake that has
  drifted from disk, the same shape as the rulebook's own guard.
- **`api/transcripts` answers a signed-in caller and refuses everybody else**, gated in the same
  block as the rulebook routes in `worker/index.js` — "signed in", not "administrator", which
  matches how the rulebook routes are gated and is deliberate: the page above it is
  administrator-only, but the route underneath asks the same question every other gated route
  does. The refusal is a plain 401 whose body carries none of the recorded text — asserted with a
  positive control, since an absence assertion is satisfied by a route that has stopped answering
  at all.
- **`ReplayLoader` fetches once, on demand, and caches it for the session.** `web/Program.cs` used
  to fetch every transcript before the first render and register a `ReplayLibrary` singleton; that
  line is gone, and nothing replaces it there. The two replay pages and the portfolio page each ask
  `ReplayLoader` for the library in their own `OnInitializedAsync`, and because Blazor WebAssembly
  has one DI scope for the whole app, the first ask is the only ask — opening a second recording
  does not fetch again. `ReplayLibrary.LoadAsync` still owns the guarantee that a failed fetch
  leaves the app running rather than the page: it fetches the whole bundle in one request now
  instead of one request per file, parses it, and hands the result to
  `engine/TranscriptLibrary.ReadAll` exactly as before.
- **Nothing fetches the recordings until somebody actually opens one.**
  `WebPresentationTests.TheBrowserDoesNotFetchTheReplayLibraryAtStartup` holds `Program.cs` to
  never naming `ReplayLibrary` or mentioning transcripts at all, and
  `ReplayRenderTests.NothingFetchesTheRecordingsUntilOneIsOpened` is the behavioural half, with a
  positive control: rendering the shell asks nothing, and opening a recording does.
- **Every new guard was broken and watched fail**, not merely reasoned about: the gate bypassed and
  the refusal turned to 200, the bake mutated one word and the byte-for-byte guard caught it, the
  eager-startup-fetch line put back in `Program.cs` and the source guard caught it, an eager fetch
  reachable from `MainLayout` and the behavioural guard caught it, the cross-language route name
  changed in both `worker/index.js` and `worker/errors.js` and `AccountsContractTests` caught it,
  and the outer catch in `ReplayLibrary.LoadAsync` narrowed from `Exception` to `JsonException` and
  the "one recording missing" test caught the resulting unhandled exception. Each was reverted
  after.
- **This reverses the specific sentence CLAUDE.md used to carry twice** — that the portfolio gate
  is "a front door rather than a lock" and that the transcripts are "still ordinary files under
  `wwwroot`" — corrected rather than appended to, in "The replay" and in "The accounts server".

Tests: engine + bUnit 4180 (3730 + 450, both unchanged in total shape apart from this slice's own
additions and renames), accounts 131 (124 + 7 new in `tests/worker/transcripts.test.mjs`). All
green, `dotnet build -p:ContinuousIntegrationBuild=true` at zero warnings.

### Adding an address to the invitation list now actually tells them, with a one-click link

`worker/invitations.js`'s `add()` wrote the row and returned — nothing was ever mailed. An address
the owner added had no way of knowing it could sign in unless he told them himself, and the admin
page's own "*can sign in now*" read as though something had been done about that. Fixed:

- **A real, one-click sign-in link, not a bare pointer at the sign-in page.** The first version of
  this deliberately carried no token — the reasoning being that a token is minted only when
  somebody *asks*, and minting one unrequested puts a live credential in a mailbox nobody asked
  anything of. **The owner reversed that mid-slice**: every invited address has already been
  chosen deliberately, so a longer-lived credential in that inbox is an acceptable trade for the
  first sign-in being one click rather than three. `worker/auth.js`'s
  `INVITATION_TOKEN_LIFETIME_MS` is three days against the public request path's fifteen minutes,
  and it needed no new migration and no second kind of token: `login_tokens.expires_at` is already
  per-row, and `used_at` already burns a token on first spend. `worker/tokens.js` is the one place
  that mints and hashes a token and the one place that builds its URL, so the public request path
  and the invitation path cannot drift into two ways of doing either.
- **A mail failure never costs the invitation.** The row is written first; the mail is attempted
  second and caught in `worker/invitations.js`, so a dead provider still leaves the address able
  to ask for an ordinary link, and the admin page is told honestly rather than shown a 500 that
  would read as nothing having happened. **Deliberately still written to `error_log`**, `mail`
  category, `route = '/api/admin/invitations'` — the same outage breaks every ordinary sign-in
  too, and the owner should be able to find it from either failure, not only from a visitor who
  complained.
- **The admin page now says which of three things happened**: mailed and can sign in now; added,
  but the mail did not go, so tell them another way; or already on the list, nothing sent.
  `web/Services/Invitations.cs`'s `AddAsync` used to return a bare `bool` for "was the HTTP status
  a success", which could not tell "just invited" from "already allowed" apart — it now reads the
  two booleans the server sends back.
- **16 new tests on the server, 2 on the page**, on top of the existing 124 and 449: the token is
  minted and hashed through the shared function, the mail carries it and the database keeps only
  the hash, it signs the invited address in and burns on first use exactly like a requested link,
  it survives the public path's fifteen minutes and expires after its own three days,
  `sweepExpired` actually removes an expired one rather than merely refusing it, a provoked mail
  failure keeps the row and writes exactly one `error_log` row, and the admin page's three
  sentences are told apart from each other rather than merely from a refusal. Every one of these
  guards was broken by hand and watched fail before being restored, per this file's own standing
  rule about checks that have never been seen to fail — see the git history on
  `tests/worker/invitations.test.mjs`, `tests/worker/tokens.test.mjs` and
  `tests/ProwlersAndParagons.Web.Tests/AdminPageTests.cs` for exactly what was mutated each time.

**Not done, and deliberately left for later:** `scripts/probe-mail.mjs` still only diagnoses the
sign-in message; it was not extended to send a test invitation. Both messages now go through the
same `send`/refusal-handling function in `worker/mail.js`, so the diagnosis the probe already
gives — the provider's status and its own machine code — is the same fault either message would
hit, which is most of why this was left alone rather than because it would be hard.

### A front door with two avenues, the whole book searchable, and the sheet while you build

**The open half of the visual redesign, and it turned into an information-architecture change
rather than one widget.** The handover named three candidates for what a first screen should
demonstrate — a dice roller, a live cost, a verdict — and asked for the choice to be taken with the
owner before anything was built. It was, and the answer was none of the three: **present the
avenues.** A rules reference and a character builder, with the portfolio moved out of the way, and
the working assumption that somebody arriving is here to build a character rather than to look a
rule up.

**One thing the handover did not name, and it shaped the whole slice.** `/` was both the first
screen *and* step one of the wizard, so a visitor who had not decided what they came for met step
one of a job they had not chosen. `ChooseTier.razor` had already recorded exactly that objection
when the two sample characters were moved off it — *a demonstration is not a step in making your own
character* — so parking a demo on `/` would have re-opened a decision already taken. The builder
moved to `/build` instead and `/` became the chooser.

#### What landed

- **Four areas, decided from the first path segment.** `""` is the front door, `build` the six
  creation steps, `rules` the reference, `admin` (with `signin`) the account pages. An unrouted
  address falls to the front door rather than to the builder: the default is what the not-found page
  gets, and a numbered step list with one step marked current, above "no such address", offers to
  continue something that never started.
- **Both avenues offered from everywhere**, replacing one banner link that flipped its own label to
  name whichever half you were not in. That works for two rooms and fails for three — it identifies
  a destination only while there is exactly one elsewhere.
- **The front door carries figures and none of them is written into the page.** 141 Powers off the
  rules the app is running; the spend beside a character in progress from the same `TryCost` call
  the budget strip makes, and shown only when the engine can give one. A figure typed into
  `Home.razor` would be the one number on the site nobody had checked, on the page whose whole claim
  is that the numbers are real.
- **`/rules` searches all ten chapters and cites the printed page.** 725KB baked into the worker,
  224KB gzipped, well inside the limit. The matching rule is `Mentions` from the MCP server, ported.
- **Hovering an option or a Trait says what it is.** The lists priced things and never said what
  they were; the descriptions were in `data/rules` the whole time with nothing showing them.
- **Phase 4: the sheet drawn beside the editors** on the characteristics step, with `--column`
  widened on the token so all five bands follow it.
- **The portfolio — recordings and both samples — moved behind the account pages.**

#### The entitlement decision, and what it lifted

Broadening the corpus past Chapter 2 was recorded as the owner's call and a blocker. He took it:
**an account may read the book**, and the older restrictions on shipping the rulebook text and the
published characters are lifted. `scripts/inline-rulebook.mjs` globs `data/rulebook/` rather than
naming files, and the test that used to assert it named exactly one chapter now asserts it names
**none** — a filename in that script is a list that goes stale the first time a chapter is added,
and the failure would be a chapter silently missing from the search rather than anything visibly
broken.

#### What the search rule actually buys here, which is narrower than it buys in the MCP server

The ported rule matches word by word with a shared-prefix allowance, never by substring, because
substring matching once answered *"she bakes bread in the city"* with **Plasticity** — and a wrong
match that looks plausible costs more than a miss.

**The first version of the comment in `worker/search.js` claimed the wider property, and it is
false.** It said the baker's sentence has to stay at nothing found. It does not and cannot: over
there the haystack is 141 short Power entries, here it is the whole book, and "city" is a word the
text genuinely uses — *City of Heroes* in the introduction, "a city, forest, jungle" in Attuned.
**Twenty-one real matches, measured.** What is worth pinning is that the sentence must not reach
Plasticity, with the positive control beside it, since a search that has stopped working satisfies
every absence. Watched to fail: swapping the word test for `String.includes` turns four tests red.

#### Three faults that only a screenshot could find

A green suite is not a working app — three visible defects survived 4,133 tests here and four pieces
of developer jargon survived 4,186. Proof pages were written for all three new screens and driven
rather than rendered at rest, and looking at them found:

- **The Trait name's dotted underline was drawn in `--rule`**, the hairline token, which under a
  word is invisible. It was the only marking on the control, so the control had no marking.
  `--muted` now.
- **A CSS comment claimed the preview keeps the sheet's three columns.** It does not:
  `.sheet-columns` is `auto-fit, minmax(280px, …)`, so a ~560px column fits one or two. The comment
  is corrected rather than the layout — three at 180px each is worse, and the three-column
  arrangement is a fact about the paper.
- **The search results sat unframed between two panels**, reading as an unfinished section rather
  than as the answer to the box above.

#### SheetView did not redraw, and nothing could have told us

It reads the session and takes no parameter that changes, so Blazor has nothing to compare and skips
it when the parent re-renders. Measured, with the tab strip above it reporting one Power beside a
sheet still drawing twelve blank rules. **It was invisible while the only sheet on screen was the
review step's**, where the character is finished before anybody looks. It subscribes now, and only
when it is showing the session's own character — a recording is handed over as a parameter, and
tying it to the visitor's edits is the influence the replay renders two pages to forbid. Both
directions are asserted.

#### Three guards were shaped by the code rather than by the claim

- **The contract scanner** required `searchParams.get('field')` on the expression, so hoisting the
  search parameters into a local — which a handler reading two parameters wants to do — reported a
  field the server plainly reads as unread. A guard that dictates the shape of the code it inspects
  is a guard that gets worked around rather than fixed.
- **`NoScreenCalcNamesARawLength`** refused `100vh`, which names the container exactly as `100%`
  does and can agree with no token. Widened — **pinned to the figure 100 rather than to the unit**,
  because exempting the unit is the mistake this file already records twice: an allow-list let `9pt`
  through and its thirty-unit replacement let `9dvmin` and `4PX` through. Watched: `37svh` is still
  refused.
- **The corpus sync test** asserted exactly one chapter.

#### Everything watched to fail

Every new guard was broken and seen to go red, and in three cases the rendered tests stayed green
through the mutation, which is the whole reason the stylesheet guards exist:

| Mutation | What went red | What stayed green |
|---|---|---|
| Word matching → `String.includes` | 4 accounts tests | — |
| `:focus-visible` removed from the row tip | 1 CSS guard | all 9 rendered tests |
| `position: static` on the preview | 1 CSS guard | all 7 rendered tests |
| `--column` widening removed | 1 CSS guard | all 7 rendered tests |
| `calc(37svh - …)` | the widened calc guard | — |

#### What this did not do, and is honest about

- **The portfolio gate was a front door rather than a lock — closed, see below.** This bullet used
  to say the transcripts were still ordinary files under `wwwroot`, reachable by anybody who knew a
  filename, and that making it real would need a refactor of `ReplayLibrary.LoadAsync` and of
  `Program.cs`. That refactor is [the completed item at the top of this section](#the-recordings-move-behind-the-gate-and-out-of-every-visitors-startup-fetch).
- **The old `/portfolio` and `/replay` addresses now 404.** Deliberate: the content is
  account-gated, so a public link that still worked would be the wrong answer, and one that arrives
  wearing the wrong chrome is worse than one that breaks.
- **The preview is on the characteristics step alone**, because the finishing step is where the free
  text is and a whole sheet behind every keypress is the render cost the front-end plan warns about.
  Asserted, with a control.
- ~~**Still no visual regression testing**~~ — **closed by the follow-up fan-out**; at the time of this entry it was open, and the gap was worse for three new screens and four
  palettes, every screenshot judged by eye. **Closed in a later slice** — see item 9 below and
  `docs/HANDOVER.md`.

#### Merged and deployed

[#73](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/73) went into `master` as
**`9ff148e`**, with Build, Deploy and Qodana green on the merge commit.

**The new server routes were then checked in production rather than inferred from a green deploy.**
`/api/rulebook/contents`, `/api/rulebook/search` and `/api/me` all answer `401` with
`application/json`. That is the check worth making rather than fetching a page: `_redirects` serves
every unmatched path as `index.html` with a **200**, so a route that never shipped comes back
looking like a working page and only the body tells you. A JSON refusal proves the address is routed
*and* that the gate is on it.

**4,303 tests** — 3,730 engine, 449 bUnit, 124 accounts — and a whole-tree Qodana scan at **0**,
both re-measured on `master` after the merge rather than carried across from the branch. That is
the discipline this file's own Tests row exists to enforce, and the merge is exactly where it has
been broken before.


### Three branches reconciled into one, and the four things that only collided

`#67` (invitation list, and the send that was never made), `#68` (four palettes on two axes) and
`#69` (categorised error reporting) were built in parallel off the same commit. Each was green on
its own and all three reported `MERGEABLE` against `master`, which is a statement about *text*
and says nothing about whether they agree. Four things only existed once they were in one tree.

**Two migrations both numbered `0003`.** Different file names, so git merged them silently and the
schema had two. The invitation table keeps `0003` and the error log became `0004_error_log.sql`,
with `db.js`'s "has migration 0004 been applied?" and the harness's explicit list following it —
appended rather than inserted, because `migration.test.mjs` indexes that list by position. Nothing
would have failed; the two would simply have applied in alphabetical order for ever.

**A misconfigured deployment named who was on the invitation list.** The gate was written above
the `SITE_URL` check, so a missing setting answered an invited address with a 500 and a stranger
with `204` — an oracle for list membership, available to anybody, on exactly the failure this site
has actually had. It is the same property `#69` refuses for account existence, on the axis `#67`
introduced, and neither branch could see it because neither contained both halves. The deployment
check goes first now; `a broken deployment answers an invited and an uninvited address
identically` pins it with byte-identical bodies and two positive controls. **Two channels stay
open and are recorded rather than papered over**: an uninvited address does not wait on the mail
provider, and while that provider refuses everything an invited address gets a 500 where a
stranger still gets `204`. Closing either means mailing strangers or padding every refusal to the
length of a send.

**A test that passed for the wrong reason.** `an error log that cannot be pruned does not stop
anybody signing in` asks for a link and asserts one was *sent*. It builds an `env` of its own, so
it calls `handle` directly and misses the harness scaffolding that quietly invites the address a
request names — and the new gate then answered `204` with no mail, which is the exact shape of the
pass it was looking for. It invites by hand now.

**The sign-in copy became false in both directions.** `#68` tightened "if that address *can have*
an account here" to "*has* an account here" while `#67` made the site invitation-only. An invited
address that has never signed in has no account row and still gets a link; an uninvited one gets
the same sentence and no mail. "Can have" is the word that covers both, and the reason is in a
comment rather than on screen.

Beyond the collisions: a whole-tree Qodana scan of the merged tree found **six**, all in `#67`'s
new files and none of them ever reported — that PR's Qodana check came back `NEUTRAL`, and CI runs
Qodana in PR mode regardless, so no whole-tree number for the merged tree existed. Three were real
and are fixed, two are the reflection-bound-DTO objection this repository already has a scoped
name for, and the scan is back to a measured zero. `AddingAnAddressPutsItOnTheList` turned out not
to check the list — deleting `await Reload()` left it green, because the page's own confirmation
sentence satisfied an assertion against the whole markup; it reads the rows now, and the weakness
predated the merge. Three documented claims had gone false: the settled list still said "two
palettes", the setup guide listed six tables while naming only one of the two new ones, and the
handover's error-reporting section named account existence as the only axis a category must not
betray. And `publish/` is gitignored, which it should have been before — both workflows publish
there, so reproducing the CI step that checks the Content-Security-Policy leaves 717 files of
build output in the tree.

**What was not done:** none of the three slices' own work was revisited or re-reviewed. Each was
reviewed on its own PR; this reconciled only where they met.

### Four palettes on two axes, and the print bug that would have shipped with them

**Light/dark is now independent of Hero/Villain.** The two used to be one switch — Hero was a
light theme, Villain a dark one — so somebody who wanted a dark screen had to make their Hero a
Villain to get it. There are four token sets now: hero-light and villain-dark are the two that
always existed, with their values unchanged, and hero-dark and villain-light are new.

**Villain-light was the one with a real risk in it** — a crimson-and-gold identity on white that
does not just become Hero in different hues — and what made it tractable is that a villain-on-white
already existed and nobody had noticed: the *print* palette, whose crimson and brass had been
measured as ink on paper years of commits ago. It is those, on a warm oyster ground rather than
Hero's cool near-white, keeping villain-dark's 2px rules and tight heading tracking. Identity
survives the change of ground by weight as much as by hue. Both candidates — warm paper and cool
— were built as proof pages and looked at side by side before one was chosen.

**The slice's real finding is a print bug that every existing guard would have missed.** The
handover prescribed `:root[data-theme="dark"][data-mode="x"]` blocks so an explicit choice beats
the system. That is specificity (0,3,0); the print block is (0,2,0), and `@media` contributes
nothing to specificity. So a reader in dark mode would have printed the full-bleed near-black
page the print block exists to prevent — with `PrintKeepsThePaperWhiteAndTheInkReadable` green,
because it read the print block's own declarations rather than resolving the cascade against the
screen blocks. Measured in a browser before any CSS was written, not reasoned about. The fix is
`@media screen` on the dark half: they do not apply on paper at all.

**And the guard built to catch it did not, at first.** Its replacement resolves the whole
stylesheet for a given state — but the first version applied admitted rules in **source order**,
which is not the cascade, and passed with `screen` deleted from the OS-dark media query. That was
the mutation that mattered: an earlier, wider mutation (`@media all`) had *appeared* to be caught
and was not — it tripped the resolver's refusal to model an unknown at-rule, which is an honest
refusal and not the catch it looked like. Weighing specificity, the same mutation fails on exactly
the three states where a dark system reaches paper, and names it.

One defect found in the contrast instrument itself, inherited from #65: its token-name regex was
`--[a-z-]+`, which does not match `--shadow-1`. Every shadow, space and type token was silently
dropped from every palette it resolved.

**The preference is per-browser and attached to nothing else** — `pp.theme.v1` in local storage,
never sent to the server, not on `CharacterSheet` and not on the account. A theme on the sheet
would travel through an export and change the screen of whoever imported somebody else's
character; one on the account would let a signed-in reader on a shared machine impose it on the
next. `js/theme.js` is loaded from `<head>` and is the only render-blocking script in the app,
because the payload is ~27 MiB and a theme applied from C# lands seconds after the reader has
already seen the wrong one.

**Persistence turned out to have no guard at all, and could not have a C# one.** Deleting the
`localStorage.setItem` — so a choice applies for the visit and is forgotten on reload — left all
4,115 tests green: the C# side checks that the right word goes out and that a stored value is read
back, and both are true of a script that stores nothing. `proof-theme.html` drives the shipped
file in a browser and re-executes the module, which is what a reload does; it is in the build
workflow beside the other harnesses. Its own positive control was wrong first — re-executing the
module re-declares `ppThemeStats`, so the counter *resets* rather than going up, and asserting the
reset is what makes it a control.

**Separately, four places on screen explained the app to a developer**, all found by the owner
reading it. The sign-in page explained that it would not say whether an address has an account —
noise to somebody signing in, and an advertisement of the defence. The replay page accounted for
who would pay for the model, in a sentence that had also stopped being true ("no accounts, no
server"), and sent a reader wanting the live version to `docs/MCP-SETUP.md`. A sample character
was vouched for by "there is a test that says so". A panel said "nothing was pre-computed". All
four behave identically; the reasoning moved into `@* *@` comments. Two guards hold it:
`NoPageExplainsItselfToADeveloper` grew eight phrases, and `NoPagePointsAtAFileInThisRepository`
is structural.

**Still open from the redesign brief, and deliberately not in this slice:** making the substance
visible rather than described — numbers as design material, the Hero Point budget as the hero
moment, and a first screen that demonstrates the mechanic instead of listing features. That is the
larger half of the brief and it is easier to build against four settled palettes than alongside
them.

### The Hero Point budget becomes the hero moment — a first step, not the whole brief

**The handover named the Hero Point budget as the cheapest, strongest starting point for "make
the substance visible", and this slice is that step alone** — not the first-screen dice-style
demonstration, not Phase 3's validation-on-the-row or undo, not Phase 4's live sheet preview.
Those stay open below.

**The number a player watches continuously used to be a full step smaller than the numbers they
see occasionally.** `DerivedStatBlocks` already sets Edge, Health, Resolve and the Hero Point
total at `--text-3xl` on the derived-stats step and on the sheet; the sticky strip printed the
same total at `--text-xl` in the one place it changes every few seconds while a character is
being built. Raised to match — the app's largest numeral, not a caption beside one — and set in
`--heading` rather than plain ink, the same role the tier cards and the active step already
carry. Both are text roles already held to their 4.5:1 floor on `--panel` in all four palettes,
so nothing new needed measuring.

**The breakdown disclosure became a small bar chart, not only a row of numbers.** The six
categories `TotalCost` sums — Package, Abilities, Talents, Powers, Perks, Gear — now each draw a
meter sized to their own share of the spend, using the same `--accent` fill on `--panel-sunk`
track the sticky rail above them already uses: one visual idiom applied twice, not a second one
invented. Trait Cap is not a spend and carries no meter; it sits below the six as a rule, set
apart the same way the sheet sets a rule apart from a figure. The meter is decoration — the
numeral beside it already carries the same figure in words, so the track is `aria-hidden`.

**Proved by breaking, on both the new engine-adjacent logic and the CSS no bUnit test can see.**
`HpBudgetBar.Share` forced to return 0 failed `TheBreakdownShowsEachCategorysShareOfTheSpend`'s
width assertions (`width:0%` where `width:38%` was expected) — restored, and the whole suite
re-run green afterwards, not only before. `.budget-figure strong`'s `font-size` reverted to
`--text-xl` failed `TheWatchedFigureIsTheAppsLargestNumeral` the same way, which is the test that
exists precisely because a stylesheet-only regression is invisible to every rendered-markup
assertion in the project.

**A stale doc comment in the file was corrected in passing.** `HpBudgetBar.razor`'s own opening
comment still claimed the strip was "Hidden entirely in Villain mode" — true before the sandbox
toggle existed, and contradicted three paragraphs later in the same file and by
`AVillainIsStillHeldToTheTiersBudget`. Left as found, it is exactly the kind of thing `CLAUDE.md`
warns a stale note becomes: something the next reader trusts because it is close to the code.

**Still open, and larger than this slice:** *(all but two of these are closed by the entry above —
see "A front door with two avenues".)*

- ~~**The first screen that demonstrates rather than describes.**~~ **Done, and not as any of the
  three candidates.** The choice was taken with the owner and the answer was to present the
  avenues: `/` is a chooser, the builder moved to `/build`, and the front door's figures are the
  engine's. What the entry above adds is the reason none of the three fitted — the tier page was
  step one as well as the first screen.
- **Phase 3's validation-on-the-row and undo**, from `docs/FRONT-END-PLAN.md`. **Still open.**
- ~~**Phase 4, the sheet as a live preview column.**~~ **Done**, with `--column` widened on the
  token at 1500px so all five bands follow it.
- ~~**No visual regression testing**~~ **Done.** See item 9 in "Remaining work" and
  `scripts/visual-regression.sh`.

### Only invited addresses, and a page that says which

The site could mail a sign-in link to any address anybody typed into it. That is the ordinary
shape for a public sign-up and it is not what this site is — an account is what puts the
rulebook's own text on screen, and who may read that belongs to the owner and to people he has
named. So there is a list, and a page that manages it.

**The gate is silent, and it has to be.** An address that is not on the list gets the same `204`
a sent link gets, for the same reason a rate-limited request does: any other answer makes the
endpoint a way of asking who is on the list, one address at a time. It is checked again when a
link is spent, because fifteen minutes is long enough to be withdrawn in.

**The first invitation cannot come from the list**, since managing it needs an account and an
account needs an invitation. `ADMIN_EMAIL` breaks that circle: the address in it is always
allowed, always an administrator, and has no row, so no click can remove it. **Nothing is seeded
into the database** — a committed address would be this repository owner's own on every fork, and
a deployment with neither the variable nor a row allows nobody, which is the safe direction.

**Withdrawing ends the sessions that address is holding and keeps its characters.** Deleting the
row alone is a gesture against somebody holding a month-long cookie; deleting their work would
make one button on an administration page the most dangerous control here.

**The page holds no claim about who is reading it.** `Identity` still carries a key and a name
and no role — the decision recorded when accounts were built — so the server answers an ordinary
account with the same `404` an unrouted address gets, and the page is reached by its address
rather than by a button that appears for some people. `/admin` is a third `Area` for the reason
the recordings are the second: the six creation steps and a running Hero Point total mean nothing
above a list of email addresses.

**Sixteen tests on the server and nine on the page, and two things were found by mutating them.**
Five deletions in the server — the gate, the gate on spending a link, who may reach the page,
ending a withdrawn address's sessions, and the refusal to withdraw your own — each turn a named
test red. The browser's five found one guard that was not guarding: the fixture's administrator
was *also* the deployment's address, whose row has no id, so a page offering a withdrawal on every
row with an id passed. The fixture now has four rows and tells the two cases apart. The other is
recorded in the harness: nearly every test in the accounts suite predates the list, so the harness
invites the address a request names — and there is a test that this scaffolding is really doing
something, because a bypass that had stopped working would leave the whole suite passing for the
wrong reason.

**What is still not proven is a link arriving.** The provider refusal above is unchanged by any of
this, and no invitation is worth anything until somebody can receive one.

### A refused send spent the allowance that would have reported it

The first person to try signing in to the live site got "a sign-in link is on its way to it", no
mail, and nothing in either dashboard to say why. Both halves of that were this repository's doing.

**The rate limit counted attempts, not messages.** `requestLink` counts against the address and
against the source before it calls the provider, and a refused send left the count spent. The
limit is five an hour, and the two answers this endpoint gives are deliberately identical — a
rate-limited request and a sent link are both `204`, so that nobody can use it to ask whether an
address has an account. So the sixth attempt stopped reporting the failure and started reporting
success, for the rest of the hour. **The shape hides itself**: somebody retries *because* no mail
arrived, and retrying is the one action that silences the error naming the fault.

The fix is `db.refundAttempt`, called on the failure path only: an attempt is spent on a message
rather than on a request, so the limit still bounds the mail one address or one machine can cause.
What it no longer bounds is requests against a provider that is refusing all of them — which is
the trade, and it buys back the only signal there is that something at this end is broken.

Two tests, one per bucket. The second is not redundant: nothing in that suite sets
`CF-Connecting-IP` unless a test says so, so a refund written for the address alone would pass
every assertion about the address. Both were confirmed by deleting the two refund calls from the
committed fix and watching them go red, and each carries the positive control that the limit still
bites on mail that was actually sent — a refund that had broken the counting outright would
otherwise look like a pass.

**It did not fix sign-in**, and the open item above says what is still wrong: the provider is
still refusing. What it fixed is that the site now says so every time instead of five times.

### Error reporting: four categories, a recorded row, and a message with the addresses out

**One failure, two audiences that want opposite things.** A visitor needs to know whether to
retry, wait or report — and nothing else, because an internal message is both meaningless to them
and a disclosure. The owner needs to know what threw. Before this the visitor got one flat
sentence and the owner got a live tail: close it and the error was gone, so any failure nobody
happened to be watching for was unrecoverable. [#66](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/66)
did the cheap half — a 500 stopped being reported as an unreachable site, and gained a reference.
This is the rest, scoped in `docs/HANDOVER.md` before it was built.

**A closed set of four — `mail`, `storage`, `configuration`, `unknown` — in `worker/errors.js`.**
Each side renders the same category its own way: the 500 body carries `{ error, reference,
category }` and `SignIn.razor` maps the category to a sentence, replacing the single
`LinkRequest.Failed` message with one per category.

- **The category is assigned where a failure is caught, never at a throw site.** `handle()` wraps
  the two subsystems on the way in — `taggedStorage` round the D1 binding, `taggedMail` round the
  send — so `db.js` and `mail.js` know nothing about categories and one file says how a failure is
  classified. A category per throw site would be a description of the internals by enumeration,
  which is the disclosure the design exists to avoid. The first tag wins, so a storage failure
  raised *inside* the mail call stays `storage`.
- **`configuration` never advises retrying**, because retrying cannot set an environment variable.
  That is the category the sign-in failure that prompted all of this would have landed in — and
  `SITE_URL` missing now throws rather than answering with its own bare 500, so the one failure
  this site has actually had is the one a visitor could not report and the owner could not find
  afterwards. It can be both now.
- **`unknown` stays reachable and is the default at both ends**, including for a category the
  client does not recognise. A taxonomy with no default grows a category for every new failure,
  and the pressure is then to classify by guessing.

**The owner's half was one D1 table read by hand, and there was no admin endpoint — superseded
below**, once the invitation list made "am I an admin" a question the server could already
answer. `docs/ACCOUNTS-SETUP.md` still carries the `wrangler d1 execute` command and the table of
what each category means, for a deployment with nobody set up as an administrator yet.

**Bounded by construction rather than by a cap somebody remembers to enforce.** The primary key is
`(category, route)` and `route` is a *pattern* from a closed list, so `/api/characters/{id}` is
one row however many ids a caller invents — otherwise the error log is a table anybody passing by
can fill, with a caller-chosen string in it. Occurrences count against the one row: **the count is
the record of what was dropped**, because a silently truncated log reads as a quiet period. The
retention window rolls inside the write statement, the same shape as `countAttempt`, so a stale
row starts a fresh count rather than continuing last month's into this morning's outage; a prune
written as a separate pass is a prune that does not happen.

**On redaction, what it buys and what it does not.** `users.email` is in that database in the
clear already, by necessity, so an error row is not a new exposure *boundary*. What it protects is
that the error log — the artefact most likely to be read aloud, pasted into an issue or
screenshotted — does not carry somebody's address. It over-redacts on purpose: any run of twenty
or more token-alphabet characters goes, with no test for whether it looks random, because a
session secret is 43 base64url characters and a hash is 64 hex ones and neither is guaranteed to
contain a digit. **The table is still never safe to publish.**

**The three tests that were the point, and one property that is security rather than style:**

| Pinned | How |
|---|---|
| Nothing anybody should read twice reaches the row | An exception quoting an address and a token-shaped string, provoked through the real mail boundary — **with a positive control that a row was written at all**, and a second that the message still says what happened, since a `redact` returning `""` satisfies every absence while destroying the column |
| The public body carries a category and a reference and no exception text | The same provoked failure, asserting the body has exactly the three keys and none of `Resend`, `422`, the address or the token |
| **The category never varies with account existence** | The same subsystem failure for a registered and an unregistered address, requiring **byte-identical** bodies. Asking for a link always answers 204 precisely so the endpoint cannot be used to ask whether an address is registered, and a category that appeared only for known addresses would put that oracle back through the error path |

That last one is why the failure reference is injected through `deps` like the clock: a random one
per failure makes every body differ for a reason that has nothing to do with the question.

**Every guard was broken and watched go red — seventeen mutations, all seventeen red**, and the
suites re-run after the reverts rather than only before. The ones worth naming: a logger that
silently writes nothing (11 red — the shape this repository has shipped four times), redaction
that keeps addresses (4), that keeps long random strings (3), and that returns the empty string
(2); `unknown` defaulting to `storage`; the route stored as the arrived path; occurrences frozen at
one; the retention cutoff never firing; nothing pruning; the logger rethrowing out of the catch;
the category dropped from the body; **the category made to depend on whether the address had an
account** (11); a client wire name renamed off the server's; the configuration sentence advising a
retry; *no* sentence advising a retry, which fires the positive control rather than the assertion;
a rendered category deleted; and the server growing a fifth category.

Five of the first twelve came out **inert** on the first pass — a multi-line `perl` substitution
that matched nothing — and were rewritten line-based until they bit. An inert mutation reads
exactly like a guard that held; it is worth checking that the file actually changed before
believing a green run.

Not done, and deliberately: **no third-party error service** — nothing about who somebody is
currently leaves the Cloudflare account, and that is worth more than a nicer dashboard — and **no
stack traces to the client in any environment**, since there is no debug build of a deployed site
and a flag that turns them on is a flag one mistake from being on.

### The error log gets an admin endpoint after all, reversing the decision above

The decision two entries up — "there is no admin endpoint and there is not going to be one" — was
sound when it was written and is superseded now, on purpose rather than by drift. What changed
underneath it is the invitation list, built after that decision: the *server* now answers "am I
an admin" on every request, via `invitations.isAdministrator(env, user)`, to gate
`/api/admin/invitations`. A read-only `/api/admin/error-log`, gated by the identical check, adds
no role to `Identity` and no new concept to the client — it is the same question asked once more.
Both `d1/migrations/0004_error_log.sql` and `docs/ACCOUNTS-SETUP.md` now say so, instead of
repeating the old refusal.

**Read-only, and deliberately narrow.** There is no route that deletes or clears a row — the
table is already bounded by its own primary key, so there is nothing to reclaim, and a control
that could erase a row would be a control that could erase the evidence of the thing it is for.
If a clear is ever wanted, that is a new decision, not a gap this slice left open.

**A panel on `/admin`, beside who can sign in**, because both are the same gate and a second page
would only be a second address for the same account to reach. The one row this site has ever
produced was a resolved outage, so the panel does not just print a count next to a timestamp:
a row whose most recent failure is more than a day old says plainly that it has not happened
again, and an empty table reads as "nothing has failed" rather than as a blank section — the same
discipline the budget bar's "None yet." already follows for an empty list that is empty for its
own reasons.

**`console.error` in the catch became one JSON object instead of a formatted sentence**, so
`wrangler pages deployment tail` can filter and read it. It shares the exact object `error_log`
is written from — category, route pattern, exception kind, redacted detail, reference — computed
once in `worker/index.js`'s catch and passed to both the log line and the row, so a tail and the
table cannot redact the same failure two different ways. The exception's raw message is never in
it, same reason it was never in the row.

**Tests, and what was broken to prove them:** the new route's three-state gate (401 signed out,
the invitation list's own 404 for a signed-in non-administrator, 200 with rows otherwise) in
`tests/worker/invitations.test.mjs`, with the gate removed and watched to answer 200 to an
ordinary account; the structured log's redaction and its agreement with the row in
`tests/worker/errors.test.mjs`, with the redaction call deleted and watched to leak the planted
address and token into the tail; and, on the browser side, that an account which may not manage
the list is never even asked — `Asked` catches this though the markup cannot, since the page's
own refusal already hides every panel regardless of whether the fetch's own guard is doing
anything. Test totals moved: the accounts suite from 124 to 130, the bUnit suite from 449 to 455.

### Characters, plural: a manager, imports, and the export the app was not writing

**The accounts slice deliberately stopped at one character**, and this closes it. Up to five per
account (25 for a GM, `users.character_limit`), a list with per-row Open/Discard, and an import
affordance folded into the same panel. Both halves the old panel was tested for came across: it
asks before discarding, only when there is something to lose, and the clear still lands after the
save that emptying the sheet fires. **The first of those was narrower than this sentence said** —
it asked only about the character that was open, and every other row went on one click. Closed
later; see the entry above.

**"Download to keep" is the reason the whole slice is not smaller than it looked.** The importer
was written against the strict inputs shape — which is what `build --from` reads and what a
character actually is — and the app's existing "Download as data" writes the *report*: derived
stats, costs, findings. The strict reader refuses it, correctly, because reading it back would
rebuild a character from its own conclusions. Both halves were right on their own and the pair was
useless: a player could download their character and had nowhere to take it. Found by the import
agent, which noticed there was nothing for it to import.

**Which character is open is a local pointer, and that had to be, for a reason a test caught after
it went wrong.** A fresh browser signing in has no such pointer, and minting a new id there asked
the server for a character that could not exist — the visitor started on an empty sheet while
theirs sat on the server under an id this browser had never heard of, which is the feature broken
in the case it exists for. `ApiCharacterStore` now lists first and adopts the most recent one.

**The identity/role tension held.** `Identity` is still a key and a name — no claims, no token, no
role — so "am I a GM" is not a question the client can ask. What the UI needs is a *number*, and
that rides on the list response as `limit`. The cap is set by hand in SQL, with no self-service
endpoint, on the reasoning that a cap you can raise on yourself is not one.

**The fan-out earned itself three times on things no single agent could see**, and this is worth
recording because the pattern is expensive if wielded badly:

- The character silently stopped following you to another browser — the local-pointer trap above,
  caught by `ACharacterFollowsItsAccountToAnotherBrowser`.
- The importer had nothing to import — the export gap above, caught by
  `ExportImportRoundTripTests`.
- `RenderContext` claimed to wire the same services as `Program.cs` and was false twice in one
  afternoon. That fails as *every* render test at once on "Unable to resolve service", loud about
  everything and silent about the one missing line. `RenderContextWiringTests` now compares the
  two, and its own positive control caught my first exemption list being wrong.

**`AccountsContractTests` earned its keep for the third time**: it went red the moment the server
dropped `/api/character` while the browser still asked for it, with both language suites green.
Nothing but that one test can see across the seam.

**One honest caveat.** The manager's "open now" row marking resolves through `ppStore`, and
bUnit's loose interop answers null, so a static render can't show it. It works in a real browser;
the proof carried a note saying so rather than claiming I saw it.

**One deliberate blast radius the fan-out cost, recorded rather than glossed.** Three build
agents at 24–37 minutes each; the fix-audit reviewer 30 more. The mutation-verification demand in
each brief drove most of that — "break every guard and re-run the full suite" is about 50s per
mutation, and one agent did 19 of them. That is the discipline that catches six-of-nine theatre
in the fix pass, but the demand has to be *scoped*: mutate the security and ordering guards,
filter tests to affected classes, and let landing come before verification rather than block on
it. See the memory note about it.

### #57: bake the rulebook, bundle on CI

Master's deploy went red after #55 landed — the wrangler pinned in `deploy.yml` predates JSON
import attributes. The corpus is baked into `worker/corpus.js` by `scripts/inline-rulebook.mjs`,
and CI now runs `wrangler pages functions build` at the pinned version so a bundler difference
fails the PR. Details and both guards are in `CLAUDE.md`'s accounts-server section.

### Accounts: one character, one account, and the book behind a sign-in

**The character used to live in one browser and nowhere else.** Close it on another machine and it
was gone; the only way to share one was to download a file; and the rulebook's own text had no way
to know who was reading it. This closes all three.

**Cloudflare Pages Functions over D1, in the account the site already deploys to** — chosen over a
hosted identity provider and over a "sync key" that would not have been an account at all. The
deciding argument was the session cookie: an API on `workers.dev` is a different origin, so its
cookie is a third-party cookie and Safari and Chrome's partitioning drop it. Same-origin Functions
cost one directory and no new bill. **If the API is ever moved to its own hostname, sign-in stops
working and nothing else does.**

**A magic link, so there is no password anywhere.** Proving you can read the address is the whole
of the check, so there is nothing to store, nothing to leak and nothing to reset. Two secrets are
minted — the link's token and the session — and **neither is ever stored in the clear**: the
database holds SHA-256 of each, so a dump of it lets nobody sign in as anybody. The session is an
`HttpOnly` cookie, which means the WebAssembly app never holds a credential and an injected script
cannot read one. That keeps true the claim the MCP server already made for this project: it handles
no credentials.

**Four things the server refuses, each because the alternative is a silent hole:**

- **A link works once**, enforced by `UPDATE … WHERE used_at IS NULL … RETURNING` — one statement,
  because read-then-write lets two requests both redeem the same link.
- **Every sign-in refusal says the same thing**, whether the token was never issued, has expired or
  is spent. Nothing legitimate needs the difference.
- **Asking for a link always answers 204**, so the endpoint cannot be used to ask whether an
  address has an account here, one address at a time. The rate limit is silent for the same reason,
  and its window rolls inside the statement rather than in a read-modify-write — the alternative is
  a limit that stops counting exactly when it is under load.
- **A state-changing request must carry this site's own `Origin`**, and one with no `Origin` at all
  is refused rather than allowed. `SameSite=Lax` already blocks the cross-site form post; this does
  not depend on the visitor's browser having got that right.

**The server never parses a character.** The payload arrives as JSON, is checked for being JSON and
being under a quarter-megabyte, and is written down verbatim; a read hands the same bytes back. The
engine is the authority on what a character costs and whether it is legal, it runs in the browser,
and a second place that understood the shape would be a second place to keep in step. A test sends
key order and spacing no serialiser would reproduce and requires them back unchanged.

**One character per account, and `characters.user_id` is the primary key rather than a convention.**
A list is a different interface and a different set of screens; it is much easier to get right once
one character round-trips, and the handover said so explicitly. *(The next slice does that widening
— see the entry above.)*

**The book is bundled into the server rather than copied into `wwwroot`, and that placement is the
entire access control.** A file under `wwwroot` is a public URL, and no amount of checking sessions
in the browser would make it not be one. Chapter 2 only — where the Powers are — because adding
chapters is a decision about what an account is entitled to read and should not happen by a glob.
*(**That decision has since been taken and this is now all ten chapters** — see "A front door with
two avenues" below. The placement is unchanged and is still the whole access control.)*
The lookup needs no table of its own: it joins on the heading, which is the join
`RulebookCorpusTests` already holds the corpus to across a hundred and sixteen entries.

**`Tooltip` was the wrong container and is not used.** A Power's entry is several paragraphs to
read, not a sentence to glance at, so it is a disclosure — and it renders **nothing at all** when
there is nothing to show. That last part is most of the design: about a fifth of the 141 Powers
have no printed entry of their own, because Super Senses' sixteen options share one between them,
so a row of apologies would appear under Powers that are perfectly fine.

**A missing server is a missing feature, never a blank page.** Identity is asked for before the
first render, so every failure — no network, a 401, a five-second timeout — answers with the
anonymous visitor. **Including the one that looks like success:** `_redirects` serves every
unmatched path as `index.html` with a 200, so a deploy without its Functions answers `/api/me` with
a page of HTML. The client parses the body rather than believing the status.

**That property is also why the deploy is the only place the mistake is ever visible**, and it now
checks: the routed function must exist before uploading, and `/api/me` must answer 401 *carrying
JSON* afterwards. Without that second check, a site with no accounts API looks completely healthy
and signs nobody in for ever.

**`StoredCharacter` was extracted so the two stores cannot disagree.** Local storage and the server
keep the character in very different places, and the temptation is to let each own its envelope —
at which point a version bump or the null-repair lands in one of them and a character saved on a
laptop restores wrongly on a phone.

**Nothing about the rules learns any of this**, and `AccountsContractTests` enforces it the way
`PresentationFlagsTests` enforces the Hero/Villain flag — with a positive control, because a scan
for eight names is satisfied completely by eight names that no longer exist. It also asserts
`engine/` and `sheets/` make no HTTP call at all, which is the form that would catch an account
arriving under a name the scan does not know.

**The two halves are written in different languages and both suites stay green while they
disagree.** The server is JavaScript and the client is C#; each is tested thoroughly alone, and
nothing but `AccountsContractTests` reads both. It compares the addresses the browser asks for
against the ones the server routes, and the keys of the object `identityOf` actually returns
against the names the client actually binds. **That last one was a `Contains` first and a mutation
walked straight through it:** renaming the server's `displayName` to `display_name` left it green,
because the word still occurred in `db.js` as a parameter name. Searching concatenated files for a
word says nothing about where the word is.

**Tested against real SQLite running the real migration**, through a D1-shaped shim — D1 *is*
SQLite, so the two statements that close a race by being one statement are executed rather than
described. A hand-written fake would have passed for either. **Thirteen mutations were applied and
all thirteen caught**, but two of the first results were worthless: removing an `expires_at > ?`
from a query left three parameters bound to two placeholders, so what went red was a broken
statement rather than the missing check. Redone by binding `0` for the timestamp instead, which
keeps the arity and changes only the answer.

**And one "survivor" was a hole in the harness, not in the code.** The mutation script ran
`dotnet test` and not the accounts suite, so a rename the Node tests caught was reported as
surviving. A mutation harness that does not run every suite reports the wrong answer confidently.

**Two faults were found by looking at a rendered page, and neither was visible to any test:**

- The signed-in proof rendered the **signed-out form** under a heading saying "Signed in". Loading
  a sample raises the session's change event, which saves the character, which asks who is here and
  *remembers the answer* — so the harness had to sign in before loading. `SignInPageTests` exists
  because of it.
- The entry's left edge was `var(--rule-weight) solid var(--accent)`, which is **1px in Hero and
  2px in Villain**, and gold on the light sunk ground **measured 1.57:1** — no edge at all. It
  looked entirely deliberate in the Villain proof, where the same declaration measured 5.62:1. It
  is now `3px solid var(--heading)`: 6.76:1 and 5.62:1, both measured, and 3px in both.

**The contrast probe was wrong before it was right**, and the way it was wrong is worth recording:
computed colours come back as `rgb(0–255)` *or* `color(srgb 0–1)`, and reading both on one scale
measured everything against black and reported 1.00 for a pair that is plainly legible. It carries
a white-on-black positive control now, which must read 21.

**`CouldOverride` in `WebPresentationTests` had a real over-broad rule**, found by this slice
needing it: `border-radius` starts with `border-`, so the helper reported that a radius was what
the cascade resolved for a `border-left` and refused to answer about correct CSS. Four names are
now excluded — radius, collapse, spacing and image are separate properties sharing a prefix. This
*narrows* a guard, so the three real overrides were re-checked afterwards and all three still
refuse.

**Two reviewers who knew nothing about this found eight things, and one was a real leak.**

- **The rulebook reader cached the book's text across a sign-out**, in the same tab. Its own
  comment claimed the cache was "per visit and per scope, so signing out and back in re-asks" —
  and **Blazor WebAssembly has one DI scope for the life of the app**, so a scoped service is a
  singleton and signing out is SPA state with no reload. A signed-in visitor on a shared machine
  could open a Power's entry, sign out, open the same Power, and be handed the book's own text
  out of the dictionary with the server — which would have refused — never asked. Reproduced
  first, then fixed by comparing the identity key rather than trusting an event to be raised: a
  guarantee that depends on an event is one somebody can remove by editing another file.
- **The magic link's domain came from the request's own host.** The CSRF check compares the
  `Origin` header *against that host* rather than validating the host, so where more than one
  hostname routes to the Function a caller who could influence it received a link minted for it —
  carrying the raw token. `SITE_URL` is now required and the server refuses to send without it,
  the same way `RulesLocation.Find` refuses rather than guessing.
- **The body cap counted UTF-16 code units while its comment said bytes**, so a body padded with
  astral-plane characters reached about twice the limit. The existing test padded with ASCII,
  where the two measures agree, which is exactly why it passed.
- **The per-client rate limit had no test at all** — deleting half the guard left all 36 passing,
  because nothing in the suite set the header it keys on, so every call counted as one `unknown`
  source. It has two tests now, and the second asserts `X-Forwarded-For` is *not* read: anybody
  may write that header, and reading it would be a limit somebody steps around with a string.
- **`login_attempts` was never swept** — one permanent row per address and per source ever seen.
  Nothing looked wrong, because the counting stayed correct; what grew was the table.
- **The banner's two controls were untested.** Hardcoding the account link to "Sign in" and
  making `Pressed` always return `"true"` — both buttons announcing pressed, which is invalid
  ARIA — each left all 333 tests green. The cause is worth recording: `Find(".banner-link")`
  returns the **first** match, and `AreaTests` uses it for the link immediately before this one.
- **`aria-controls` had no guard**, so making it unconditional — naming an element not in the
  document while closed — passed. Now asserted absent when closed and resolvable when open.
- **The address-contract regex had a character-class hole**: `[a-z/]` meant renaming a route to
  `api/auth/verify-token` matched nothing, so it was dropped from the list and the test passed
  while the two halves genuinely disagreed. A pattern that answers "not an address" when it means
  "I cannot read this" is worse than none.

**And one fault was in the test stub rather than the code**: `FakeApi`'s verify route answered a
hardcoded identity whatever it was asked, so a test about two accounts on one machine was quietly
a test about one. All seven new guards were then mutated and all seven bite.

**Then a third reviewer was pointed at the fixes rather than the code, and six of the nine did not
hold.** This has been the highest-yield reviewer for six sessions running and it earned it again:
every one of the six caught only the mutation it had been shown, and a *variant* reaching the same
end state walked past it with every suite green. What was wrong was the same thing each time — the
guard asserted the absence of one spelling instead of the property.

| The fix | The variant that got past it | What it is now |
|---|---|---|
| `SITE_URL` required | trust `X-Forwarded-Host` *as well*, leaving the refusal intact — mailed a link to `evil.attacker.test` | the link's origin must **equal** `SITE_URL`, with six hostile host headers set |
| the cap counts bytes | "correct" the count by +2 per surrogate pair — right for emoji, wrong for CJK — stored 307 KB | asserted with a three-byte character as well as a four-byte one |
| `X-Forwarded-For` not trusted | trust `X-Real-IP` too — 26 links against a cap of 20 | nine spoofable headers varied at once |
| `login_attempts` swept | restrict the `DELETE` to `key LIKE 'email:%'` — 51 stale `ip:` rows | both kinds of key asserted by name |
| the identity contract | return `key: null` — same field names, so the name-comparison passed | the key must be the account's own id, and match `/api/me` |
| `CouldOverride` narrowed | `all: unset` after the rule — the box lost background, padding and edge in any browser | `all` overrides everything, checked first |

**The `key: null` one is the worst-shaped of the six**: the server would establish the session and
set the cookie while the client read a null key as "nobody is signed in" — a live session its owner
is told they do not have. A contract on field names is not a contract.

**It also found a second lie in the stub, live and uncovered:** `FakeApi` had one character slot
shared by every account, so any test of two accounts against the character store would have been a
test of one — and would have passed against a server with no notion of ownership at all. The real
server's own tests do cover ownership, so nothing in production was wrong; what was missing was the
ability to tell. `TwoAccountsOnOneMachineDoNotShareACharacter` is that ability.

All six variants were then re-run against the strengthened guards and all six now go red.

**What is not done, and needs the account owner rather than a commit:** none of it runs until a D1
database, a binding named `DB`, a Resend key and the DNS records for a sending domain exist.
[`docs/ACCOUNTS-SETUP.md`](docs/ACCOUNTS-SETUP.md) is the five steps, and a test asserts it names
every environment variable the server actually reads — documentation of a configuration nobody in
this repository can try out rots silently otherwise.

**One cost, stated rather than hidden:** a failed save is silent. Local storage effectively cannot
fail; a network can, and the character then exists only in that tab.

> **"Saved" feedback — done, on a branch not yet merged.** `MainLayout` shows the word beside the
> account link once `CharacterSession.Saved` reports a write-through completed, and nothing before
> that — there is deliberately no "saving…" state, since nobody outside the store knows how long a
> write takes. It does not solve the *failed* save this paragraph is actually about: `SaveAsync`
> still never throws, by design (see `ICharacterStore`'s own doc comment), so a save that genuinely
> fails still says nothing. What it buys is the more common gap — nobody could tell a *successful*
> save had happened either, which read as no feedback at all rather than as silence about failure
> specifically.
>
> **The race worth recording:** two saves can be in flight together (a slow account save from one
> edit, a fast one from the next) and finish in either order. `CharacterSession.Version` — bumped on
> every `NotifyChanged` — is what a completed save is checked against, rather than a bare
> `bool` latched by whichever event happens to run last; a stale completion cannot un-confirm a
> newer one. `SaveStatusTests.AStaleCompletionCannotUnconfirmANewerSave` pins it, though the
> harness environment resolves the local-storage path synchronously, so what it actually exercises
> is the version comparison rather than a truly overlapping pair of writes — the closest anything
> here gets to a real out-of-order race without a controllable double for `ICharacterStore`.


### One site, two areas: the play aide and the portfolio

**The first piece of actual reorganisation rather than polish**, asked for by the repository's
owner, who observed — correctly — that Phases 0–3 improve what is on screen without ever asking
whether the right things are on screen.

The tool and the demonstrations *of* the tool were the same screens. Somebody who came to build a
character walked past a recording of a stranger's conversation and a pair of pre-made characters to
reach the tier list, and the chrome above every page was the character generator's: six numbered
steps, one of them marked as the step you are on, offered to a portfolio visitor who is not taking
any of them.

- **`Areas.Of` answers which half an address is in**, from the first path segment, once, and
  case-insensitively because Blazor's routing is. `MainLayout` draws the step band and the budget
  strip only in the tool. The banner's cross-link points at the half you are *not* in — it used to
  say "Watch one being built" from everywhere, so the only cross-link a player ever saw pointed
  away from what they were doing.
- **The two samples moved to `/portfolio`; "Start a new character" stayed**, because throwing away
  the character you are building belongs to building. The recordings moved under
  `/portfolio/replay`.
- **The old `/replay` addresses are still portfolio addresses, and that is a fix rather than a
  courtesy.** Moving the route made them Play, so the budget strip — the visitor's *own* character
  — came back over somebody else's recorded one with nothing saying whose was whose, which is the
  exact fault it was hidden there to prevent. A shared link that still works but arrives wearing
  the wrong chrome is worse than one that breaks. It was caught by a test, not by looking.
- **`HasSomethingToLose` moved onto the session.** Three pages now ask it before replacing a
  character and two copies had already drifted apart by a field.

**Three mutations, three caught** — dropping the legacy prefix, matching by `StartsWith` so
"portfolios" would be captured, and an ordinal comparison so a capitalised link wears the wrong
chrome. One first attempt was a no-op that passed and had to be re-applied properly.

### The storage and identity seam

**No accounts yet and no behaviour change** — every visitor is anonymous and the character is in
this browser exactly as before. What changed is that the shape is now the one accounts need, which
was the owner's explicit choice over building the split first and retrofitting later.

`ICharacterStore` is the smallest thing every caller uses, so a server-backed store is a
registration change. `IIdentitySource` answers who the character belongs to, asynchronously
because a real one has to ask something — making it synchronous now would mean changing every
caller later, which is the whole point of the seam. It carries a key and a name and deliberately
no claims, token or expiry: **the wrong authentication model is harder to remove than none.**

**The anonymous key stays `pp.character.v1` exactly**, which is the compatibility promise —
suffixing it for consistency would empty every returning visitor's browser, silently, looking like
storage cleared rather than a bug. An account's characters land beside it, so signing in on a
shared browser cannot overwrite what the anonymous visitor was building, and clearing one slot
leaves the other. Five tests pin it, with a positive control that the shipped identity really is
anonymous.

**The larger half is now done** — see [accounts](#accounts-one-character-one-account-and-the-book-behind-a-sign-in)
below. The seam held: the two interfaces did not change shape, and the app's behaviour for a
visitor with no account is byte-identical to what it was.


### Phase 3, second slice: the pips become the control

They were `aria-hidden` decoration beside a `+`/`−` stepper, so the only way from 2d to 9d was
seven clicks. Clicking the fifth pip sets 5d, the arrows move by one, Home and End go to the ends.

**`role="slider"` rather than a radio group.** A rank is a value on a bounded, ordered range, and
one focusable element beats twelve — eighteen Traits would otherwise add 216 tab stops. The stepper
stays: it is discoverable, it is a bigger touch target, and a slider beside its own buttons is an
ordinary pairing. The individual pips stay `aria-hidden`, because they are the slider's own
rendering and a reader told "4d Noteworthy, slider" does not also want twelve unlabelled children.

**The announced minimum is the package floor where there is one**, not the Trait's own 1d, because
a package's granted ranks cannot be lowered below the package rank. A slider announcing a bound it
will not go to tells a screen-reader user something untrue about the control in front of them, and
clicking a pip below that floor clamps up rather than asking for a rank the validator would then
report.

**Nothing suppresses the browser's default on those keys through Blazor, and that is not a gap —
it is the wrong layer for it.** Blazor fixes `preventDefault` at render time rather than per event,
so suppressing it on this element would also swallow Tab and trap focus inside a rank row — much
worse than what it would fix. Left and Right need no such thing: the pips are horizontal and a CI
harness holds this app to no horizontal overflow, so those two scroll nothing regardless.

> **Home and End — done, on a branch not yet merged.** `Sliders`/`wwwroot/js/slider.js` is the
> small interop shim this paragraph said would close it: one native `keydown` listener per rank
> row, attached once on first render, answering to exactly Home and End and nothing else. It
> follows the same guarded-interop shape as `Motion`, `Shortcuts` and `Theme` — a `try`/`catch`
> swallowing a missing script rather than throwing out of every rank's render. bUnit cannot see a
> real `preventDefault`, so the meaningful proof is a browser harness, `proof-slider.html`, driven
> in the build workflow the same way `proof-motion.html` and `proof-shortcut.html` are; a mutation
> that suppressed every key (not only Home/End) was applied and watched the harness fail on
> exactly the Tab-trapping case this note has warned about for three sessions.

**Five mutations, five caught** — the minimum ignoring the package floor, the keys bypassing the
clamp, a click off by one, the pips exposed as twelve children, and a handler answering every key.

**And one defect found by measuring rather than by looking.** The same slice enlarged the click
target by giving each pip padding, with `background-clip: content-box` intended to leave the fill
as drawn. A probe reading `getBoundingClientRect` against the computed padding and border showed it
had not: these are `border-box`, so the pip's box went 7px to 11px and its painted fill went 7px to
5px, with the border no longer hugging it. That is a deliberate design quietly altered to fix a
secondary concern, invisible to every test here and about two pixels to the eye. Reverted. If it is
revisited, **measure the painted width rather than reasoning about the box model.**


### A skip link, and the shell's landmarks — done, on a branch not yet merged

There was neither before this. A reader tabbing from the address bar met the banner's two links and
five buttons, then the step band, then the sticky budget strip, on every single route, before
reaching anything the page was actually about.

- **`<a class="skip-link">` is the first thing `MainLayout` writes**, before the banner — order is
  the whole of what makes it a skip link rather than a link with the right words in the wrong
  place. Off-screen by `transform`, not `display: none`, so it stays in the accessibility tree and
  reachable by keyboard while invisible; `:focus` brings it on screen. `<main>` carries
  `id="main-content"` and `tabindex="-1"` so the jump actually moves focus rather than only
  scrolling — a plain anchor jump to a non-focusable element moves the viewport and leaves the
  caret wherever it already was.
- **The landmarks were already mostly right** — `<header>`, two `<nav>`s each with their own
  `aria-label`, one `<main>` — this only added the id/tabindex and a test that pins the count and
  the naming on more than one route, since the step band's own `<nav>` only exists once a tier is
  being built.
- **`LandmarkTests` and `WebPresentationTests.TheSkipLinkIsOffscreenUntilFocused`** cover the
  markup and the CSS separately, for the reason this file states everywhere else: a rendering test
  cannot see whether a rule actually hides or reveals the link, and a source-reading test cannot
  see where an element landed in the render order. Mutations applied and watched fail: moving the
  skip link after the banner, removing a `<nav>`'s `aria-label`, and deleting the `:focus` rule.


### Tooltips, and the attribute that is not one

**A `title` attribute is not a tooltip, and that is the whole reason this is a component.** It
never appears on a touch screen, is unreliable for keyboard users, cannot be styled, cannot be
dismissed, and is announced inconsistently by screen readers — and none of that is visible to a
compiler, to a rendering test, or to somebody reading the markup and finding it perfectly
reasonable. It is the easiest way to undo this work because it is the obvious thing to write, so
there is a guard refusing the attribute outright across every component.

**The trigger is a real button**, which is what makes the tip reachable without a mouse at all: a
tap focuses it and focus opens it. A `<span>` with a mouse handler is a tooltip only for people
using a mouse.

**The handlers are on the wrapper, not the button.** WCAG 1.4.13 asks for hoverable, dismissable
and persistent; a tip that vanishes when you move the pointer towards it fails the first. Escape
covers the second — a tip that can only be closed by moving a pointer is not dismissable by
somebody who is not using one.

**The tip is always in the document, hidden by a class — deliberately the opposite of the budget
breakdown.** There, `aria-controls` is written only while the target exists, because it genuinely
does not. Here an `aria-describedby` pointing at nothing whenever the tip was closed would dangle
for all but a moment, and a description a reader has to *hover* for is one a screen-reader user
never gets. Hidden by `visibility` and `opacity`, never `display: none`, which would take the
accessible description with it while leaving every rendering test green.

**The id is derived from the term rather than generated.** A fresh one per render would break the
replay's strongest guard, which renders one character twice and requires the two pages to be
identical — it would report a difference on every run that is not one.

**Both call sites are supplementary and a test says so.** The Trait Cap in the budget breakdown and
the no-limit toggle on the tier page; the Trait Cap's *figure* is asserted still printed beside its
tip, because the failure mode of adding a tooltip is quietly moving something into it, at which
point the readers who cannot open it have lost something that used to be on the page.

**Five mutations, five caught — after one false pass that exposed a real hole.** Moving the hover
handlers onto the button reported as a survivor, because the mutation had only *added* them and
left the wrapper's in place; `git diff --numstat` showed additions only. Applied properly it was
caught — but only after the test was strengthened, because the original asserted enter and leave
and never the structural fact that delivers the behaviour: `mouseenter` does not bubble, so what
keeps the tip open under the pointer is that the tip is *inside* the element carrying the
handlers. The test now asserts that containment.

**And one defect no test could have found: the tip opened upward.** That is the conventional shape
and it is wrong here — the budget breakdown hangs off a strip stuck to `top: 0`, so an upward tip
is clipped by the window edge exactly where it is most likely to be opened. It opens downward now.
Found by rendering one and looking at it.

**Deliberately not built: a Power's rulebook text on hover.** `data/rulebook/` has the prose and is
deliberately not in the browser payload, so that wants the reference surface rather than a tooltip
parameter. See the handover.


### The palette becomes the character's, and the Hero Point limit becomes its own toggle

**Asked for by the repository's owner, and it reverses an entry in the settled list.** Villain used
to mean two things at once — a colour scheme *and* no Hero Point budget — and `CLAUDE.md` refused a
mode field on `CharacterSheet` for exactly that reason: the second half is mechanical, and a
mechanical flag on the sheet is the browser deciding a rule. Splitting them is what makes the field
defensible, and the split is the substance of this change rather than a side effect of it.

**`IsVillain` and `UnlimitedBudget` are on the character, and nothing in the rules can see them.**
The first is why: an exported sheet should still be a Villain when it is read back, which a palette
held beside the character could never manage because it was never in the file. `PresentationFlagsTests`
asserts that no file under `engine/` or `sheets/` so much as names either field — **with a positive
control, which is not optional**, because a scan for two names is satisfied completely by two names
that no longer exist. Both were proved by mutation: a `Lenient(sheet) => sheet.IsVillain` dropped
into `CharacterValidator` was named and refused, and renaming a flag in the guard's own list failed
the control.

**Ch.9 builds Villains by exactly the Hero rules**, so "no budget" was never a fact about Villains —
it is a GM building to whatever the scene needs, which a Hero campaign does too. The sandbox is an
independent toggle on the tier page, where the budget is introduced. A Villain can be held to a
tier's points; a Hero need not be. Three tests encoded the old conflation and were rewritten rather
than deleted, including one whose *name* asserted the opposite of the new behaviour.

**Without a limit the strip is a running total rather than absent.** Absent was the old behaviour
and it took the breakdown with it, so somebody building without a limit lost the one panel that says
where the points went. No cap, no remaining figure, and no rail — a `progressbar` needs a maximum to
be a proportion of, and one drawn against the tier's points would put back on screen the limit that
was just switched off, while announcing a figure to a screen reader that nothing is measured
against.

**The validator is still never told, and still reports the finding.** The browser shows a total and
`build --from` reports everything the engine returns: a report that dropped a finding on the
strength of a flag in its own input would be worth less than no report. The engine answers; hosts
present.

**One route puts the palette on the document, and the guard is what found that.** Three call sites
used to push `ppSetMode` themselves — the tier page's samples, the replay hand-off, and the switch.
The palette follows the character now, so the layout applies it on the render after any change of
character, and the other three are gone. That made the call *render-reached*, at which point
`TheAppsOwnScriptsAreCalledOnlyThroughMotion` failed: `ppSetMode` had been on its by-hand allow-list
as a call only ever reached by a click, and that claim had just stopped being true. It goes through
`Theme` now, guarded like `Motion` and `Shortcuts`; unguarded it would have thrown out of every
render of the shell. **The allow-list shrinking is the point** — an entry on it is a claim, not a
permission.

**One mutation reported a false pass and had to be re-run.** The first attempt at the validator
mutation used `perl -0pi`, which on this machine exits 0 and edits nothing; `git diff --numstat`
printed no change and the guard "passed". `CLAUDE.md` records that exact trap. Check the numstat
every time.


### Phase 3 of the front-end plan, first slice: the command palette

`Ctrl-K` from any route opens a box that offers the six creation steps and, once something has
been typed, the Powers. It is the plan's own "single highest-leverage affordance" and the first
of Phase 3's two slices; the pips-as-control, keyboard navigation inside the option lists,
validation on the row where the mistake is made, and undo are the second.

**The matching rule is the lists' rule.** `OptionFilter` grew a static `Matches` that its
instance `Admits` now calls, so the palette and the five pickable lists cannot answer the same
query differently — a reader who has learnt that "plast" finds Plasticity in the Powers list has
learnt something about this app, not about one list. The tallying wrapper stays where it was,
because a list counts what it drew and a palette does not.

**The six steps moved out of the step band and into `Commands`, which both draw.** Two lists
would drift, and a palette offering a step the band does not have — or missing one it does — is
worse than no palette. The test compares what each actually renders rather than reading the
source.

**Choosing a Power requests it; it never adds it.** A Power needs ranks, variants and its Pros
and Cons chosen, and the editor is the one component that knows how to price them, so the palette
hands the Power over and changes nothing about the character. The request is **read once**: the
step holding the editor re-renders on every keystroke elsewhere on it, and a request that stayed
set would reopen the editor over whatever the reader had moved on to, repeatedly, with nothing on
screen explaining why. The step above it *peeks* to decide which section to show and the section
itself takes and clears — consuming it in the step would leave the reader on the right tab with
nothing open, which is the failure that looks most like the feature working.

**Interop goes through `Shortcuts`, the same bargain `Motion` makes.** All three calls are
reached from a render, so a `palette.js` that 404s or fails to parse would otherwise throw out of
`OnAfterRenderAsync` on every render of the layout, which is every page. A missing keyboard
shortcut must not take the app with it: the six steps are still one click away in the band and
every Power is still in the list on its own step.

**The component dispatches its whole event handler, not just the redraw.** The key listener
arrives from the browser rather than from Blazor, and Blazor refuses off-dispatcher state changes
outright when it can tell — which is how this was found, as a real fragility rather than a test
artefact.

**Nine mutations, nine caught, and one defect found only by looking.** Six against the rendered
component and its service — Powers offered to an empty box, arrows clamping instead of wrapping,
`aria-selected` frozen while the ring still moved, the request peeked instead of taken, the band
growing a step list of its own that had drifted, and the palette matching by a rule of its own —
and three against the script, driven in Chrome: the chord no longer taking the key from the
browser, the listener firing on any `k`, and focus dropped on the body instead of restored.

The tenth finding had no test at all. **The current-row marking was a `--rule-weight` hairline,
legible in Hero and very nearly invisible in Villain**, where `--accent` sits on a near-black
ground — and that edge is the only thing on screen saying what Enter is about to do. It is now
the 3px edge `.option` already marks a chosen row with. Found by screenshotting both palettes and
opening both, which is the rule this project keeps re-learning.

**A sixth browser harness, `proof-shortcut.html`, and it exists because bUnit cannot reach the
door.** Ctrl-K is heard by a listener on the document, which no render tree contains, so every
assertion about what the arrow keys do sat on top of an opening chord nothing checked — the exact
shape of gap this repository has shipped four times. It drives the real `wwwroot/js/palette.js`
with synthetic key events, asserts the negative cases (a bare `k`, a `Ctrl-J`, a second
registration) because without them a listener that fires on every key passes the lot, and checks
focus in both directions, since a palette that takes focus and does not give it back is a
keyboard trap. `ppPaletteStats` is the positive control, asserted before anything that depends on
it. It runs in CI beside the other five.

**What it deliberately does not do.** No preventDefault inside the component: Blazor decides that
at render time rather than per event, so a blanket suppression on the box would stop the letters
reaching it and the palette could not be typed in. Nothing here computes a Hero Point, and the
palette is a way to reach a control rather than a second place a character can be changed.


### Qodana's 32 findings on #46 — and two were real defects, not style

All 32 were in test files, none in production code, and Qodana runs in PR mode so several were
pre-existing rather than new. Most were spelling: seven redundant `using` directives, six redundant
verbatim prefixes, a redundant name qualifier, a redundant default argument.

**Two were worth the scan on their own.**

- **`EffectiveValue` carried two `<summary>` blocks.** The doc for `ScreenHalfOfAppCss` — eighteen
  lines explaining why the `@page` box is stripped, and recording that an earlier version of the
  note cited a compensating check that did not compensate — had been **stranded 175 lines from its
  method** by an insertion, leaving `ScreenHalfOfAppCss` with no documentation at all and
  `EffectiveValue` with two summaries and an unclosed `<para>`. This is the exact defect
  `CLAUDE.md` already records from an earlier slice, repeated by the same mechanism: line-based
  splicing. **The compiler sees none of it**, and neither does any test in this repository.
- **A lambda parameter named `rule` shadowed a `Match` named `rule`** eight lines above it, in the
  same method, with different types. Renamed.

The seven `AccessToDisposedClosure` were fixed by removing the capture rather than suppressing the
inspection — the fill actions take the sheet as a parameter now, the Power is resolved before the
render that used it, and the interop counter is a local function over `ctx.JSInterop` rather than a
delegate over `ctx`. `CLAUDE.md` asks for a rationale beside any deliberate exception; none was
needed, because none of these needed an exception.

### Phase 2 of the front-end plan: motion that carries meaning — **in progress**

**The first thing found was that the guard this phase must not break did not exist.** The handover
names `proof-sticky.html` as the measured check on the budget strip — the strip stays put only
because its containing block is the document, and View Transitions is precisely the change that
would wrap it. The file was on disk and **had never been committed**: no generator in `ProofPages`,
`web/wwwroot/proof-*.html` is gitignored, and regenerating the proofs deleted it. So did
`proof-measure.html`, `proof-narrow.html` and `proof-narrow-shell.html`, the three harnesses
prerequisite 8 leans on for the 375px measurements. Four measured checks the handover treats as
standing were one `PP_PROOF=1` run from gone, and absent entirely in a fresh worktree.

`TheStickyStrip` is now a generator beside the others, so it survives a clean checkout, and
`TheStickyHarnessMeasuresRatherThanAsserts` runs on every build — not under `PP_PROOF`, which is the
mistake Phase 1's fix-audit found in the marker checks and would have reproduced exactly.

**The harness states a verdict token rather than leaving it to the eye**, so the check is read out of
a dumped DOM instead of a screenshot. The baseline measures `.budget` top at 117.0 before a scroll
and **0.0** after, with `.steps` bottom at −483.0 — stuck, against a page that genuinely scrolled.

**And the instruction for reading it was itself defeatable, which only looking at the dump showed.**
The first version said to assert on `STICKY: PASS` in the page. That string appears **twice** in a
dumped DOM — once as the verdict and once inside the harness's own script source — so the assertion
passes on a harness whose script never fired, which is the failure mode being guarded against. The
verdict is written to `document.title` as well, which the script alone writes and whose resting value
is neither verdict, so the three states are distinguishable. `MustNotShow` refuses a hard-coded
`say(true, …)`; a proof that cannot fail is worse than no proof, because it is read as evidence.

**Item 1, continuity across steps, is in.** The three chrome bands carry a `view-transition-name`,
so the browser matches each to itself either side of a navigation and interpolates rather than
cross-fading the whole window; what is left in the `root` group is the content of `main`, which is
the only thing that changed. `wwwroot/js/motion.js` is 55 lines and the only script added — no
animation library, for the reasons in the plan. The CSP is untouched: `script-src 'self'` already
allows a same-origin file.

**`LocationChanged` is the wrong hook and it is the one already in the layout.** The API animates
between two snapshots and the first has to be taken while the old page is still on screen; by the
time `LocationChanged` fires there is nothing left to capture. `RegisterLocationChangingHandler`
runs before the navigation, so `begin()` snapshots and holds the transition open on a promise and
`OnAfterRenderAsync` resolves it once the new step has rendered. The failure mode of getting this
wrong is silent — no error, just no animation, indistinguishable from an unsupported browser.

**Two of the four new guards were theatre, and a variant is what showed it.** Both assert that
`motion.js` *mentions* something — `still()`, `setTimeout` — and both pass against a script doing
the opposite of what it says:

- `if (… || !still()) return` — one character — serves the animation to exactly the people who
  asked for none, and every string assertion still passes.
- `setTimeout(() => {}, 1000)` is a safety net that catches nothing. That one matters more than it
  reads: while a transition is open the live DOM sits behind a snapshot, so a release that never
  arrives leaves a frozen picture of the app with no way back.

**The fix is not two more string assertions.** The property is behavioural, so the instrument has
to run the code — the same class of mistake as `Contains`, one level up. `proof-motion.html` loads
the shipped `motion.js`, stubs `startViewTransition` to observe it, and asks three questions: does
`begin()` open a transition, does `end()` release it, does an unreleased one free itself. It is run
twice, the second under Chrome's `--force-prefers-reduced-motion`, **and every expectation inverts**
— which is the half no source scan can reach. Re-run against both variants it catches both, and
discriminates: the inverted gate fails checks 1 and 2, the dead timer fails only check 3.

**Item 2 is in, on a clock a test can seek — and the first explanation of why was wrong.** The
counting figure was written on `requestAnimationFrame`, found to be unverifiable, and parked with
the note that "rAF does not fire under `--headless=new --dump-dom`". **That named the wrong cause
and would have misdirected the next session**, because it points at the dump mode. The cause is
`--virtual-time-budget`: it suppresses frame production, so neither rAF nor the document timeline
advances, while `setTimeout` continues to fire. Measured both ways —

| flags | result |
|---|---|
| `--screenshot --dump-dom`, no virtual time | `RAF-FIRED-1` |
| `--virtual-time-budget=8000` + any of `--dump-dom`, `--screenshot`, `--run-all-compositor-stages-before-draw` | `NO-FRAME`, and `waapi=running@0` |

**That matters beyond this feature**: every screenshot in this repository needs
`--virtual-time-budget`, because `.panel` animates from `opacity: 0` and a bare capture photographs
it mid-animation. So the flag this project cannot work without is the flag that makes frame-driven
animation unobservable. Anything animated here has to be checkable by *seeking* rather than by
waiting.

Which is why the counting figure is `element.animate()`. Not because rAF is impossible — it runs
perfectly in a real browser and still pumps the redraw — but because a WAAPI animation's
`currentTime` is **settable**, and `draw()` is a pure function of it. Seeking the clock and calling
the same `draw` a visitor's frame calls is a test of the shipped path. Seeked at t = 0, .25, .5,
.75, 1 the figure reads **10, 16, 19, 20, 20**: in bounds, monotonic, and resting on the engine's
number.

**Every harness now carries a positive control, and the reason is a tally rather than a
principle.** Three separate checks in this slice passed because the feature under test never ran —
an inverted gate meant no transition opened, an unlinked stylesheet meant no count started, and an
iframe that fails to load reports `clientWidth === scrollWidth` over an empty document, which is a
clean "nothing overflows". So each harness asserts the work happened — `ppMotionStats.transitions`,
`ppMotionStats.counts`, an element count, a scroll position that actually moved — before asking
whether the outcome was right.

**All five were then deliberately broken, and the exercise paid for itself immediately.**

| break | caught by | result |
|---|---|---|
| `.budget { position: static }` | sticky | `STICKY: FAIL`, others unaffected |
| `.panel { min-width: 460px }` | both narrow harnesses | `NARROW: FAIL` ×2 |
| `.budget-strip { padding-left: 60px }` | insets | **survived at first** |
| the reduced-motion gate inverted | motion, both modes | `MOTION: FAIL` |
| the count rests one short | motion | `at t=1 showed 19`, and the monotonic check too |

**The third one is the finding.** `getBoundingClientRect()` returns the *border* box, so padding
moves the content on the page without moving the number the harness read: one band sat 60px out of
line and the spread still reported `0.00px`. It measures the content edge now. Nothing but breaking
it would have found that — it had been passing, against a real layout, the whole time.

A sixth mutation — deleting the assigned resting frame so the last value is interpolated — was
**not** caught, and that is correct rather than a hole: the easing reaches exactly 1 at `t=1`, so
`Math.round(from + (to - from) * 1)` is already `to`. The mutation changes nothing observable. It
is recorded because "a guard missed this" and "this mutation was a no-op" look identical in a



**The adversarial round: two reviewers, ten findings between them, every one demonstrated by
mutation rather than argued.** Three were bugs in shipped code, not in the guards — the rate this
project records for new guards ("a third to a half are theatre") held, and understated it.

**What was wrong with the code:**

- **`ppCount` leaked one live `Animation` per count.** `fill: "forwards"` keeps a finished
  animation *relevant*, so it stayed attached to the single `<strong>` in the budget strip —
  measured growing 1, 2, 3 … 10 over ten counts, for the life of a session. The cleanup now hangs
  off `clock.finished` rather than the rAF pump, which also makes it *drivable*: frames are
  exactly what `--virtual-time-budget` suppresses, so a tidy-up tied to the pump could not be
  tested at all.
- **An interrupted count could come to rest on a figure the engine no longer returns.** The
  cancel sat below the early returns, so a call taking the immediate path left an older count
  pumping — and a probe showed the abandoned count writing **100** after the newer one had settled
  on **42**. That is the shape `CLAUDE.md` forbids in as many words. The cancel is above every
  early return now.
- **`motion.js` had quietly become load-bearing for navigation itself.** `OpenTransition` awaits
  interop on *every* internal navigation and `ChosenList` on every render; a 404 or a parse error
  would have thrown out of both. `Motion` swallows the script's failure — **a service rather than
  a `try` at each call site, for the same reason `ReplayLibrary.LoadAsync` is a method**: a block
  inside a component is where nothing can reach it.
- **`HpBudgetBar` held an `@ref` to an element Villain mode does not render** and called interop
  against it on every change. Blazor never clears an `@ref` when its element stops rendering, and
  the call was absorbed by `ppCount`'s null guard — invisible, and load-bearing without anybody
  having written that down.

**What was wrong with the guards — eight of them:**

| guard | it passed while… |
|---|---|
| the `LocationChanging` hook | the snapshot was taken from `LocationChanged` instead — **no cover at all**, suite and all six harnesses green |
| `view-transition-name` uniqueness | one name sat on `.panel`; driven Chrome returns `InvalidStateError` and abandons every transition |
| the reduced-motion gate | a new ungated entry point was added; its helper anchored on the first `animate(`, **which is in the file's header comment** |
| the safety-timer ordering | it compared indices against that same comment |
| the narrow harnesses | `overflow-x: clip` hid 352px of unreachable content; and leftward overflow is invisible to both measurements |
| the insets harness | one band ran 96px short, printed in its own evidence |
| the sticky harness | `position: fixed` reported as `sticky`, because `before` was measured and discarded |
| every proof page | the app's own `<div id="app">` wrapper was missing, so ancestor-borne faults could not be seen |

**The bUnit test written to close the worst of those passed against the mutation on its first
attempt**, because it recorded the address from its own handler rather than correlating with the
moment `Begin` ran. It counts interop calls already made when a `LocationChanging` handler fires,
which is the only thing that separates the two hooks.

**Two findings are recorded rather than fixed, and deliberately.**

- **A held-open transition swallows pointer input.** Measured: `elementFromPoint` over a button
  returns the `::view-transition` overlay rather than the button, for ~260ms normally and up to
  1000ms if `end()` never arrives. `pointer-events: none` on the pseudo would let the click
  through — **to the new page, while the visitor is still looking at a snapshot of the old one**,
  which trades a dead click for a wrong one. 260ms of inert overlay is what every implementation
  of this API does. Recorded with the numbers so the next session can weigh it rather than
  rediscover it.
- **There is no `aria-live` anywhere**, so the counting figure spams nothing — but crossing into
  over-budget is announced to nobody either.

  > **Done, on a branch not yet merged.** A `sr-only` sibling of `.budget-figure`, never inside it
  > — exactly the placement this bullet asked for. Its text is computed by a method that compares
  > the current over-budget state against what it was last time and only writes new words on an
  > actual flip, so an ordinary change in spend that leaves the character on the same side of the
  > line says nothing twice, and loading an already-over-budget character announces nothing at
  > all (there is no crossing to describe — it arrived that way). `BudgetStripTests` pins both
  > halves, and a mutation that inverted the flip check — announcing on *no* change instead of on
  > a real one — was applied and watched the crossing test fail.

One latent defect is also recorded: `_midTransition` is released by any render of `MainLayout`,
not specifically the navigation's, so a render batch flushing in between would close the
transition early and snapshot the old page twice. Not reachable today — no step-navigation path
writes to the session before navigating — and it becomes live the first time one does.

**The fix-audit — a reviewer pointed at the fixes rather than the code — was the most valuable of
the three, and its first finding was a bug the previous round had *introduced*.** Of the ten fixes
audited, three held, one held while shipping something worse, and six did not hold. That is close
to the rate this project has recorded four sessions running, and it was found by asking for a
*variant* rather than a re-run.

**Severity 1: the counting figure came to rest on the previous Hero Point total.** Moving the
tidy-up onto `clock.finished` calls `clock.cancel()`, after which `currentTime` is `null` — and
`draw` read that through a nullish default as `t = 0`, so a pump frame still scheduled at
completion wrote the *old* figure back over the answer and rescheduled itself for ever. Worse than
the leak it replaced, live on the branch, and **found with no mutation applied at all**.

The reason nothing saw it is the reason the fix was made in the first place:
`--virtual-time-budget` produces no frames, so no pump was ever pending in any driven check. The
property that made the cleanup testable is the property that hid the regression. It is fixed four
ways, each sufficient alone — a null `currentTime` reads as the end rather than the beginning, the
pump stops on it, the pump checks it still owns the element, and the finished handler cancels the
pending frame — and the check that catches it needs no frames: capture the record's `draw`, finish
the clock, call `draw` once more. Against the buggy version it reports *"settled on 60, then a late
frame showed 40"*.

**What else did not hold, and the variant that showed it:**

| fix | the variant that walked past it |
|---|---|
| `view-transition-name` on a singleton selector | the rule put in `theme.css`, which was never read — and the same rule spelled `VIEW-TRANSITION-NAME`, which CSS treats as identical and a case-sensitive regex does not |
| the cancel above the early returns | moving it *below* them: the harness's interrupt used 78→42, which never takes an early return, while `HpBudgetBar` produces `from === to` routinely |
| every animation gated on `still()` | a module-level helper — `EnclosingBlock` still fell back to returning the whole file, which contains `still()`; and only `motion.js` was scanned |
| `Motion` guarding the interop | calling `Js.InvokeVoidAsync("ppLand", …)` directly again: the fix guarded two call sites, not the property |
| the `ShowBudget` guard | correct code with **zero cover** — deleting the line left everything green |
| the insets harness | a margin on a band's first child: the band's own box and padding are untouched, so both spreads still read `0.00` |
| the narrow harnesses | a 300px `::before` at `left: -320px` — `querySelectorAll` returns no pseudo-elements |
| the structural hook guard | a *comment* naming `Motion.Begin()` left in the handler while the real call moved. Its bUnit half caught it; the structural half was worth nothing alone |

**And one the audit found outside the ten:** `Begin(); End();` in the changing handler passes every
check — a transition opened, from the right hook, and released — while animating nothing, because
the second snapshot is taken before Blazor renders. The release belongs to the render, and that is
asserted now.

**Three fixes held under attack**, and the audit said what it tried: the `#app` wrapper (also
confirming that `transform` and `contain` on that wrapper genuinely do *not* unstick the strip, so
the harness's own docstring overstates them), the sticky harness's `before > 20`, and the overlap
check.

**My own new guard was theatre once in this round too.** The release-ordering check read its
counter synchronously after `begin()`, and a release resolves a promise — so it reported zero
whether or not `begin()` had released, and passed the exact variant it was written for. It settles
first now.
**Item 2's last part: a row arriving in a chosen list lands, and `--ease-emphasised` arrives with
it.** Phase 0 withheld that token deliberately — an overshoot curve wants something that should
read as *landing*, and until now nothing did. A row moving from the picker into the character is
the one thing that does, and it is the token's only user; an overshoot on a state change reads as
a wobble.

**Which row is new is decided by a `data-landed` mark in `motion.js`, not by a key in the
component.** Blazor reuses DOM nodes, so the render tree does not answer that cheaply — and a mark
survives something a key does not: a filter re-ordering the list is not twelve arrivals.
`firstRender` is passed *through* rather than used to skip the call, so restoring a saved character
marks its rows without playing a dozen animations at once, and the next genuine addition still
lands alone. Both halves are asserted, and both were broken to prove it:

| break | result |
|---|---|
| land on first render too | `a first render marks rows without landing them` fails, and so does `rows already present do not land again` — 2 animations where 0 belong |
| hard-code the curve instead of reading the token | `effect easing "ease-in-out" vs token "cubic-bezier(0.34, 1.56, 0.64, 1)"` |

The second is the one no CSS test could have caught: the animation is built in script, so a curve
that drifts from the token is invisible to every stylesheet scan. The harness reads the easing back
off the running effect and compares it against the computed token.

`getAnimations()` is the positive control throughout — it asks the browser what is actually
running rather than trusting a counter this code also owns.
**The two source guards on `motion.js` are theatre and stay theatre, so a browser runs in CI.**
They assert the script *mentions* `still()` and `setTimeout`, and both pass against
`|| !still()) return`. `ubuntu-latest` ships Chrome, so the build workflow drives all five
harnesses and requires each to *say* PASS in its `<title>` — asserted on the positive, because a
harness whose script never ran leaves resting text that is neither verdict, and grepping for FAIL
would call a broken harness green. The source guards are kept beside it: they run where the
browser does not, and they now claim only what they can support.

results table and are not the same fact.
**Four harnesses were missing, not one.** `proof-sticky`, `proof-measure`, `proof-narrow` and
`proof-narrow-shell` were all uncommitted scratch. All four are generators now. The restored
measurements: nothing overflows at 375px on either page, and all four chrome bands sit on the same
column to 0.00px.

The source guards are kept beside it. They are cheap, they run in CI where the browser does not, and
what they now claim is only what they can support.

### Phase 1 of the front-end plan: density and hierarchy, which was mostly deletion

Three of the plan's four items in full, the fourth split — see the end of this entry, which says
what was left and why.

**One chrome band, in place of three.** The banner ran full width, then a step list with its own
bottom rule inside the 1100px column, then the budget as a shadowed white card inset from the
window: **about 215px of furniture before the page heading, on every step**, reading as four
stacked pieces. It is **163px** now and reads as one — banner, steps, strip, rail, contiguous and
all full width.

- **The step list and the strip are siblings of `main` rather than children of it**, and that is
  load-bearing twice over. Full width without a bleed: the negative-margin hack that pulled the
  strip out of the shell's padding is gone from three sites, along with the pair of
  narrow-viewport rules that had to be kept in step with it — **so the 8px overflow they caused
  at 375px is now unreachable rather than guarded.** An invariant is better deleted than guarded
  when the thing it constrains can be removed.
- **And a wrapper around both rows would have broken the sticky strip.** `position: sticky` is
  bounded by its parent, so a short chrome `div` holding both would unstick it the moment the band
  scrolled past — the whole span it exists to survive. Measured through an iframe: the strip sits
  at 116 before a scroll and at **0** after scrolling 600, with the step list at −484. Joined the
  band and scrolled away, which is what the plan asked for.
- What replaces the deleted guard is a real requirement in the direction that cannot overflow: the
  shell and all three chrome columns cap on `--column` and reserve the same padding, **discovered
  per media query rather than listed**. It failed on its first run and was right to — the
  narrow-viewport rule pads the three bands in one grouped rule, and the padding reader filtered
  on the whole selector string being equal, which is the **same comma-list weakness a fix-audit
  had found in the sticky-strip guard two commits earlier.**

**Two boxes that were drawn around boxes.** The options scroller carried its own border inside a
panel that is already a ruled box with a heading strip, so a list of rows with their own
separators sat three nested edges deep; only the top rule survives, which does a different job —
separating the list from the filter box, which is a control and not a row. And the derived step
wrapped four ruled figures in a bare untitled panel, a box round four boxes separating nothing.

**Six empty states that said nothing.** "None yet.", "None.", "Nothing yet." — a full stop
restating a fact the reader can already see, on the one screen where a tool is least useful and
best placed to help. Each now names the next action, and where a rule stands behind it, the rule:
the Flaws tab says the rules ask for a minimum at creation and **reads the figure from the same
place its own heading reads it**, and the Gear step says ordinary gear is free so the only thing
that spends Hero Points is a custom feature. They are an `EmptyState` component rather than a
class applied six times — one owner per repeated class, and the guard gets one element to find
rather than an enumeration that goes stale.

**The tab strip marks what is untouched, and only three sections can be marked.** That is a rules
matter rather than a convenience: Ch.2 floors every Ability and Talent at 1d, so a character has
all eighteen and **cannot be without them** — those sections are never empty, and their 0 HP means
a package covered the cost rather than that nobody has been there. Powers, Perks and Flaws are
genuinely collections. The marker is a ring rather than a colour.

**The Sources editor becomes a grid.** Eighteen fields between the two pickers, each a short label
over a 200px control, stacked one per row — the right-hand two thirds of the panel spent on
nothing, and the Talents picker taller than a laptop viewport. Six rows become two, twelve become
three. The rank rows on the tabs are deliberately left alone: those are a table read down.

**Two guards were written after a mutation showed they were needed, not before.** The picker test
rendered `AbilitiesTab`, which is where the app puts the component — and that tab passes
`IsPro="false"` and nothing else, so **the Pro half of the wording was never rendered** and putting
"this Power does" into it passed. And the untouched marker **shipped with no guard at all**; the
negative half of the test that now covers it is the load-bearing one, since marking a Trait section
would report eighteen Traits a character cannot be without as missing.

**Two process failures of mine, both traps this repository had already written down.**

- **The mutation harness reverts with `git checkout -- <file>`, which restores the last
  *committed* state** — and the empty-state edits were not committed when I mutated those two
  files, so the revert discarded them. `docs/HANDOVER.md` says "Commit before letting anything
  mutate files… That has cost rework twice." It is three times now, by somebody who had just
  finished reading it.
- **And I committed the damage, because the command was `dotnet test | grep … && git commit`** —
  which gates the commit on grep finding lines, not on the tests passing. What caught it was the
  new guard **refusing to pass when it could find no `.empty-state` element at all**, rather than
  asserting nothing over an empty set: the "refuse a subject you never found" rule doing its job
  one commit after being written. Every run since captures the output and asserts on the absence
  of `Failed!` before committing.
- A third, smaller: `git diff --numstat` is **blind to an untracked file**, so mutating a
  brand-new component read as "never applied" *and* could not be reverted — the mutation was
  silently left in the working tree, which is worse than either failure alone. Both harnesses
  refuse an untracked target now.

**Verified by looking as well as by testing**, since every visual bug in this project's history was
found that way: both palettes at 1400px, the chrome band and the empty editors read on a rendered
page, 375px measured at `overflow 0px` with the bands correctly edge-to-edge, the sheet still
**three pages in both palettes**, and every one of 75 section children still inset symmetrically.

**A proof of the shell, and a proof of the editors holding nothing** — the two states no page this
harness wrote had ever shown. Every other proof renders components into a bare `.shell` div, so
the three chrome bands never appeared together and "how much does the chrome cost" had no answer;
and every other proof loads a sample, so an empty list was invisible. Part of why six empty states
stayed full stops for so long is that nobody could see them.

**What was left, and why.** The plan's fourth item is "a real grid on wide screens", and it names
**Phase 4** in its own text: above ~1400px the editors and *a live sheet preview* sit side by side.
The preview is Phase 4's, and without it a second column has nothing in it — while at today's
1100px column two editor panels would be ~530px each, too narrow for a Power list with a stat line
under every name. Widening `--column` globally would also widen the sheet and the replay, which is
a design decision Phase 1 has no business making as a side effect. So the half that stands alone
was done (the Sources grid) and the half that needs Phase 4 waits for it. **Item 4 is not
finished; it is split, and the remaining half is listed under Phase 4 in the plan.**

**An adversarial reviewer then found five real defects and demonstrated that six of six of the new
guards held nothing.** The worst of the five was visible in a proof page this change added, and
which I generated and never opened.

- **The chrome had no bottom edge at all on three whole classes of screen.** `.steps` lost its
  bottom rule on the argument that the budget strip beneath carries the edge for both — and the
  strip renders nothing in **Villain mode** (Ch.9 gives Villains no budget), on **the tier page
  before a tier is chosen**, which is the first screen a new visitor sees, and on **every
  `/replay` route**. On all three the step chips sat on the page ground with the heading following
  on the shell's padding alone. `proof-shell-villain.html` showed it plainly; I screenshotted the
  Hero one and wrote "verified by looking… both palettes". **Generating a proof is not looking at
  it.**
- **"Untouched" was inverted for the default path, and my test pinned the mistake.** The claim was
  that Abilities and Talents can never be marked, because Ch.2 floors every Trait at 1d and 0 HP
  there means a package covered the cost. The first half is true of a *finished* character and is
  exactly why a fresh one needs telling; the second is false, because `AbilityCost` walks
  `AbilityRanks`, which is empty on a new sheet — **so no package also costs 0 HP.** The two
  sections that most needed marking were the two forbidden from saying so, on a character the
  engine reports eighteen `TRAIT_BELOW_MINIMUM` errors deep. The predicate is now whether a rank
  is *recorded*, which tells the cases apart properly: choosing a package writes its granted ranks
  into the sheet, so a packaged character reads as touched at 0 HP.
- **The banner was the one band not on `--column`**, while the note in `MainLayout` offered it as
  the example the others follow. Fixed with an inner column rather than a `padding-inline: max(…
  calc((100% − …) / 2))`: the percentage is a raw length the stylesheet's own rule refuses, and the
  wrapper makes the banner structurally identical to the other three bands, so **one guard covers
  four instead of three plus a special case.** The guard then immediately caught that the
  narrow-viewport rule had not been told about it — 24px against 16px, the same figure as the
  bleed bug.
- `.field`'s own margin **doubled the row gap** in the new Sources grid, 32px against 16px; and
  `.options` lost the bottom rule that **marks where 141 Powers are clipped**, so a row cut through
  its own stat line read as a rendering fault rather than as a scroller.

**The six guard survivors, each fixed as a property rather than as a case.** A new breakpoint
widening `.shell` alone passed, because `max-width` was read in the base rules only while the doc
claimed queries were discovered. Centring was not read at all, so a strip with `margin: 0` sat
flush against the window edge with the labels above and the heading below still on the column. The
band elements were outside the guard entirely, so padding on `.budget` shifted the column inside it
and stopped the rail running edge to edge. **Deleting the whole `.empty-state` rule left all eight
`EmptyStateTests` green plus both ownership tests** — the class is still on the element and every
assertion reads markup, which is the `.hp` trap this file records verbatim, reproduced by the change
that cites it. Pinning Powers to permanently untouched passed, because the test filled Perks alone.
Naming the *Ability* in the picker passed, because the ban listed one of three subjects. And
**misstating the creation minimum to the player passed** — the one empty state that quotes a rules
figure had nothing checking the figure.

**Then one more, of my own making and worse than any of them: I fixed the missing edge and wrote no
guard, so a mutation put it straight back.** That is the same failure as the six, one level up —
fixing the defect rather than the class of defect — on the most severe finding of the round. It is
guarded now, together with the three conditions that are the *reason* for it, because a guard whose
premise has quietly gone is worse than none.

**The two proof harnesses genuinely cannot be guarded much, and that is stated rather than papered
over**: they are generators gated on `PP_PROOF`, so with it unset they are no-ops and a reviewer
duly commented out five of six sections with the suite at full count. What is checkable is that a
page which *is* written shows what it claims to — including a negative that catches the dangerous
shape, since wrapping the shell proof in a column defeats its whole purpose and every positive
marker survives it.

**Then a fix-audit, and its central finding is that this was one mechanism rather than twelve
problems.** Of the twelve claims: two held, two did not, six held only against the mutation shown to
them, and two fixes were correct while the defect they repaired reverted green. **`EffectiveValue`
and `HorizontalPaddingTokenOf` each read a single CSS spelling of the property they were asked
about**, so four separate guards fell the same way — `border-bottom-color: transparent` beat a
`border-bottom` check, `border-left-width: 0` beat a `border-left` check, `margin-left: 0` beat a
`margin` check, and `padding-inline` beat a padding check that knew only the physical pair. Closing
the one helper converted four near-misses at once.

- **`EffectiveValue` reads every declaration that decides a property** — the property, its
  longhands, and its logical equivalents — in source order, and **refuses to answer when the last of
  them is a spelling it does not model.** An unreadable answer is a red test, which is the safe
  direction; modelling the whole cascade is a bigger job than any of these guards needs. **Order is
  what makes that correct rather than merely strict**: `.budget-toggle` writes `border: none` and
  then `border-bottom: …`, which the cascade resolves as the author meant, so a check refusing any
  related spelling would fail on correct CSS. It caught exactly that on its first run.
- **A zero width is not a visible edge.** `border-left: 0 solid var(--rule)` contains no `none`,
  names the right token, and draws nothing — and got past both edge guards. Refused in any unit now,
  along with a transparent ink.
- **Two guards were not passing `exact: true`**, so a rule matching no element in this app supplied
  the value they read. That is verbatim the defeat recorded on the `exact` parameter itself from the
  previous audit, reached again by guards written after it.
- **The media-query scan ended at the first newline-brace**, so a query written on one line was not
  found at all and its contents were swallowed into whichever block did end that way — which is how
  a band-only breakpoint evaded a guard whose own comment says the queries are discovered. It
  brace-matches now. **CSS formatting is not a property a guard may depend on.**
- **Four defects reverted green because they were fixed and not guarded**: the options scroller's
  clip mark, the grid-cell margin reset, and the untouched ring's whole CSS rule — **the `.hp` trap
  again, on the sibling of the feature this round had just closed it for.**
- **Two sentence guards were satisfiable with the words wrong.** The picker's ban read the `Target`
  enum, which is better than one word and still not the property wanted: "what this **Trait** does"
  names no enum member and is false of a piece of Gear. And the Flaws figure was checked without the
  claim around it, so *"the rules make them optional, though at least 1 buys extra Resolve"* passed
  while the rules require 1–3. Both pin the clause now.
- **Deleting the `.banner-inner` element while its CSS stayed passed everything**, and the end state
  is worse than the defect it fixed — the banner's contents then have no padding at all. A CSS guard
  cannot see a missing element, so the markup is asserted where the markup is built.
- **And the proof-page markers were near-theatre for a reason I had not seen: they sat after
  `if (!Asked) return`**, so the one mutation the negative half exists for was invisible to CI and to
  every ordinary run — including the run whose count the commit quoted. The page builders are
  extracted and asserted by a test that runs always, the mode is checked against the filename (the
  Villain proof could be made a copy of the Hero one), and the empty-editor page carries a marker per
  section rather than two the tab strip supplied on its own.

Fourteen mutations were re-run after the fixes and all fourteen are caught.

3854 tests to **3877**. Zero warnings at CI strictness. **Payload: unchanged** — one new component
file, no new asset.

### Phase 0 of the front-end plan: the scales nothing after them can be consistent without

[`docs/FRONT-END-PLAN.md`](docs/FRONT-END-PLAN.md) puts this first because it is invisible on
its own and every later phase is cheaper for it. **Nothing here changes what the app does.**

**The counts in the plan were an undercount, and the measured ones are the reason this was
worth doing.** The plan named "twenty separately-chosen spacing values and eleven font sizes".
Measured off the screen half of `app.css`: **twenty-seven** distinct lengths on padding, margin
and gap, and **twenty** font sizes — nineteen in rem plus the body's own 15.5px — of which
**ten sat between 0.68rem and 0.9rem**, a range no reader can resolve into ten steps. That is
not a design, it is a history of individual decisions, and the reason nothing could have told
you so is that every one of them was locally reasonable.

**Nine spacing rungs and seven type rungs replace them**, plus three elevation steps and a
second easing. 169 lengths rewritten; every rung is used by at least one rule and no rule names
a length outside the scales.

- **Steps of 2px at the bottom and 4px above it, not a strict 4px base.** Four of the old values
  sat between 4.8px and 7.2px — tag padding, pip gaps, the gap in a row of controls — and a
  4px-only scale collapses that whole range onto either 4px or 8px, which is a factor of two on
  the tightest spacing in the app. The half-steps stop above 8px, where 2px is invisible anyway.
- **Two type rungs are anchored to existing values rather than to the ratio, and both for a
  measured reason rather than a taste one.** `--text-xs` is exactly **0.72rem** because that is
  the size `--muted` was measured against: the note on it holds it to 4.5:1 rather than 3:1
  *because* it carries explanatory prose at this size, and a scale that rounded the bottom rung
  down to 0.67rem to fit a ratio would have invalidated that measurement silently — the colour
  would still pass its own test, at a size nobody had checked. `--text-3xl` is exactly 2.15rem
  because it is the masthead and nothing sits above it for a ratio to answer to. The base is
  0.97rem, the 15.5px the body has always been, so prose does not reflow for a round number.
- **Elevation was one `--shadow` carrying the banner, every panel, the sheet, the tier cards and
  the sticky strip.** The consequence was not that the page looked wrong — it is that nothing on
  it had a *height*. `--shadow-3` is claimed by the budget strip alone, which is the only element
  that moves independently of the document, and that is asserted **by count**: spreading the top
  step back across the page would undo the distinction without changing a single value.
- **There is deliberately no `--ease-emphasised`**, though the plan named one. An overshoot curve
  wants something that should read as *landing*, and the only candidate is a row arriving in a
  list, which is Phase 2. Declaring it now ships a token no rule asks for — and this app has
  already shipped a `.label-line` class applied to nothing, found by looking at a rendered page
  rather than by any test. `--ease-out` is added and used, on the four things that travel.

**Held by the same rule as colour and typeface, and the rule refuses both spellings.**
`NoScreenRuleNamesARawSpacingOrTypeLength` bans a raw length in padding, margin, gap or
font-size — **in px as well as rem**, because px is what somebody reaching for a value rather
than a rung would naturally write, and a rem-only check leaves that door open. The print block is
out of scope by design: it is mm and pt, a different medium with its own scale and its own tests.
Three literals are exempt, each **paired with the selector it belongs to** and each asserted to
still exist, because an exemption whose selector was renamed away permits its declaration
everywhere and says nothing.

**Three things were found by doing this rather than by planning it, and the first was mine.**

- **The scripted rewrite produced `-var(--space-6)`, which is not valid CSS.** A minus sign in
  front of a `var()` invalidates the whole declaration, so the browser drops it — the budget
  strip would have quietly stopped bleeding to the shell's edges, and the `margin-bottom` on the
  same line would have gone with it. **Nothing about the page would have looked broken**; it
  would have looked as though the bleed had never been written. Four sites, all `calc(-1 * …)`
  now, and the comment beside the first says why.
- **Three existing typographic guards read their font size out of the declaration with a regex,
  and stopped working the moment the sizes became tokens.** The obvious repair — accept a
  `var()` and skip the range check — turns three *measured* assertions into three assertions
  that a property is present, which is the exact weakness all three of their doc comments record
  being hardened against. They **resolve the scale** instead, so they are stronger than before:
  `--text-sm: 2rem` in theme.css now fails the trait-Source-line guard, which no literal read
  could ever have seen.
- **"The bleed has to follow the shell's padding" was a comment asking to be remembered.** It
  had to be: two unrelated literals have no relationship to assert, which is why the 8px overflow
  at 375px got in. Two references to one token do, so it is
  `TheBudgetStripsBleedMatchesTheShellsPadding` now — asserted at every breakpoint, with the
  media queries **discovered rather than listed**, so a third breakpoint is covered the day it is
  added rather than the day somebody remembers it.

**Eighteen mutations were run against the new guards before any reviewer saw them; seventeen
applied and all seventeen were caught.** The other two are the finding worth keeping: **a
mutation aimed by line number at a file that had since gained four lines of comment deleted a
comment instead, passed, and reported as a survivor.** The harness now asserts the file actually
moved and prints the numstat, so "the mutation never applied" and "the guard held" stop looking
identical — which is the same failure as reading `Passed!` off a crashed run, one level down.

**Verified by looking, not only by testing.** Both palettes at 1400px and the narrow viewport
through an iframe, which is **measured rather than eyeballed**: `clientWidth 360, scrollWidth
360, overflow 0px`, against the 368/360 that was the bug this replaces. The printed sheet was
rasterised and read — three sheets still three pages in both palettes, ink on white paper,
heading bars still a tint, Notes and Origin still ruled at a writable 4mm, gear still flush left.

**Two reviewers that knew nothing about it then found three code defects and eight guards that
held nothing.** Every one of the eight was demonstrated by mutation there and re-demonstrated
here after the fix.

**The three defects.** Two were found independently by both reviewers, which is worth noting: the
overlap was not redundancy, it was corroboration on the two that mattered.

- **`.sheet-section > table` read `width: calc(100% - 1.2rem)` and was a matched pair with the
  0.6rem inset on its siblings.** Phase 0 moved the inset onto the scale and left the width
  behind, so a table's right edge fell 3.2px short of every other child of its box *and* of the
  heading bar above it — measured at 8.00px of inset on the left against 11.20px on the right,
  five boxes a sheet, both palettes. **The change written to abolish paired literals left one
  standing one property name outside its own scope**, and no test could see it because `width` is
  not padding, margin, gap or font-size. `NoScreenCalcNamesARawLength` closes the class rather
  than adding `width` to a list: what makes the bug possible is not the property, it is a number
  that has to agree with a token and has no way of doing so. Now 75 of 75 children of every
  section measure symmetric.
- **`--text-3xl` was declared 2.1rem while four documents called it "exactly 2.15rem… not to be
  tidied onto a ratio".** It had been tidied onto the ratio. So the one rung the notes single out
  as unpinnable was the one already off its stated anchor — and unlike `--text-xs` it had no
  test. It is 2.15rem and pinned, and the note now states what the anchor *costs*: a 1.26 top
  step rather than ~1.2, which is the honest version of "anchored, not derived".
- **`.replay-figures.spent-on` became a rule identical to its base**, 0.8rem against 0.85rem with
  both snapping to one rung. It was the modifier's only declaration, so the class did nothing
  anywhere while a component still emitted it — **the `.label-line`-applied-to-nothing shape this
  very entry cites as a lesson, reintroduced in the same commit.** The distinction was 6% and
  below perception, so the rule and the class go rather than inventing a new size difference:
  that is Phase 1's hierarchy work, not Phase 0's mechanical pass.

**The eight guards, and the transferable part of each.**

| Held nothing because | Now |
|---|---|
| The scale check read token **names** and never a value, and nothing else in the suite pinned any `--space-*`. `--space-4: 4rem` re-padded most of the app, the narrow shell and the strip's bleed from 12px to 64px, green | Every rung's value is recorded and asserted, and the rungs must be strictly increasing. Same pattern as the server instructions: where the value *is* the deliverable, the value is the assertion, and the duplicated literal buys a change having to be deliberate and visible in a diff |
| The declared set was computed from **theme.css alone**, so a `--space-9` declared in a `:root` block inside app.css joined the scale invisibly and the raw-length scan waved through every `var()` using it | Both stylesheets and `index.html` are checked to declare no rung at all. theme.css is the only file allowed to |
| The unit list was `px\|rem\|em\|ch\|vh\|vw\|%`, so `margin-top: 9pt` walked through — and so would mm, cm, in, pc, ex, lh, vmin, dvh and the container units. **An allow-list of units is the wrong shape for a ban** | Every CSS length unit, longest-first |
| **`@page` sits *above* `@media print`**, so it was inside the region this file calls the screen half, and the guard's own doc claim that print is out of scope was false for it. It passed only because `mm` was missing — closing that gap would have turned a live print declaration red | Excluded by name, and **asserted present before being removed**, so a moved or renamed page box fails rather than silently un-excluding itself |
| `BleedTokenOf` took the first negative token found **anywhere** in the shorthand and assigned it to both sides, so it could not tell a horizontal bleed from a vertical one. A margin pulling the strip 24px *up* over the step nav, with positive side margins and the rail left 48px wider than the strip, satisfied the pairing | Both the padding and the margin readers parse the four sides properly. Splitting on whitespace was the cause: `calc(-1 * var(--space-6))` contains three spaces |
| `Contains("position:sticky")` is satisfied by a declaration a **later one in the same block** overrides, so `position: static` un-stuck the strip while it kept the top elevation step — with the guard's own comment claiming that could not happen. **The identical shadowing trick the `.hp` guard in this file was already hardened against; the new guard did not inherit the fix** | The last `position` declaration wins, as the cascade does |
| The rank-word band is **absolute**, and the rank it glosses is a rung of the same scale, so setting the gloss to that rung left it exactly level with the figure it sits behind | The relationship is asserted. Before Phase 0 the two were unrelated literals in two rules and this could not be expressed at all — **the scales made a describable claim into a checkable one**, which is the clearest thing Phase 0 bought |
| The 1px exemptions are justified **entirely** by the `border-bottom` they sit against. Replace it with `text-decoration: underline` and the padding is dead decoration with the stated reason false, and a guard checking the declaration string passed. Its own doc calls a stale exemption "worse than a missing one" | Each exemption carries its precondition, looked for across every rule targeting the element — `.hp` supplies the `.power-entry .head .hp` case by inheritance, so requiring it in the same block asserted something never true |

Three elevation steps may also no longer be three copies of one shadow, which a name-only check
could not have told apart either.

**And a method finding, which is the one to carry forward.** **Two reviewers running concurrently
in one worktree poison each other** — both mutate files and revert with `git checkout`, so one
caught the other's `--text-sm: 2rem` and read it as a finding, both lost runs to `index.lock`,
and one's cleanup deleted the other's harness. Give each its own worktree. Relatedly, **a numstat
check taken *before* the test run does not catch a mutation reverted mid-run**; the harness checks
after as well now, which is the same lesson as reading `Passed!` off a crashed run, one level down.

The headline count also read 3849 against a tree of 3850 for one commit, copied from a run taken
before the last test was added. Both reviewers spent a finding on it, which is a waste of a
reviewer: **take the number from the run.**

**Then a fix-audit — a reviewer pointed at the fixes rather than at the code — and it found that
nine of the eleven caught only the mutation demonstrated to them.** That is the fourth session
running this reviewer has been worth more than the passes before it, and the second time it has
found most of a round of fixes to be narrower than claimed. Both figures it re-measured from the
documents checked out (75 of 75 children symmetric, `overflow 0px`, 3852 tests), which is the
other half of its job.

**Its central finding is one root cause behind three of the nine, and the correct pattern was
already in this file twice: a later declaration of the same thing beats a `Contains`.** Each of
the three reached the bad end state by declaring the thing *again* rather than by editing what the
guard was reading — a duplicate `--space-4: 4rem` under `--space-8` (workhorse rung at 64px, full
suite green), `.budget, .breakdown { position: static }` later in the file (strip un-stuck, top
elevation step kept), and `border-bottom: none` instead of deleting the line. It is fixed once, as
`EffectiveValue`: comma lists split, suffix-matched, last declaration wins.

**Two doors needed no scale token at all, and that is the more useful lesson.**
`--table-inset: 1.2rem` beside `width: calc(100% - var(--table-inset))` restored the table
misalignment byte for byte — 3.20px on all fifteen tables — and `--pad-lg: 4rem` re-padded an
element with a raw length under a name no rule about the scales could match. **Narrowing the
earlier check to `--space-*` and `--text-*` defended the names of the scales rather than the
property that makes a scale mean anything**, which is that there is one place lengths are decided.
`app.css` may now declare no custom property at all — free, because it declares none.

**And the unit list was the wrong shape twice, the second time knowingly.** The fix's own doc
comment said "an allow-list of units is the wrong shape for a ban" and then shipped a longer
allow-list, which `9dvmin`, `3svb`, `2lvi` and `4PX` walked through — twelve viewport units
missing and the match case-sensitive besides. It is inverted now: a digit followed by letters or a
percent is a length, whatever the letters are, so a unit from a future specification is caught the
day it ships.

Three more where the fix asserted more than it checked, which is the shape this project keeps
being bitten by:

- **`ScreenHalfOfAppCss` strips every `@page` block while `ThePageIsA4WithMargins` read only the
  first** — and the stripping cited that test as the compensating check. A second `@page` after
  the A4 block printed the sheet A5 landscape at margin 0, seen by nothing.
  `ThereIsExactlyOnePrintBlockInEachStylesheet` exists for this failure one at-rule over; the page
  box now has its equivalent, pseudo-pages included.
- **`TokenIn`/`NegativeTokenIn` still took the first token found anywhere inside a side**, which is
  the first-match weakness the four-side parser was written to remove, surviving one level down.
  `calc(-1 * calc(-1 * var(--space-6)))` computes to **plus** 24px and read as a bleed;
  `calc(var(--space-6) * 3)` is 72px of padding read as matching a 24px bleed. Both anchored to
  the whole side, so a side doing arithmetic fails safe rather than being guessed at.
- **The rank-word guard named `.trait-table td` as "the rank it glosses", and the two never render
  on the same surface** — `.rank-word` comes from `RankRow.razor` and `.trait-table` from
  `SheetView.razor`. The claim was about a pair nobody can see together, and it passed only by
  being accidentally conservative. Shrinking `.stepper .value`, the figure it actually sits beside,
  put the gloss exactly level with it, green. Compared against that now, with an assertion that
  the two really do render together.

**One route needed a second round.** Reading the exemption's precondition as a *value* closed
`border-bottom: none` and did not close `.sheet .budget-toggle { border-bottom: … }` — a selector
matching nothing in this app, supplying the reason while the real rule lost its border. **Suffix
matching is right for "what applies to this element" and wrong for "does this rule still say
this",** and a source-reading test cannot know which selectors match real elements. So each
exemption records the selector that must carry its reason, matched exactly — which for `.hp` is
the base rule rather than the exempt selector, because the letter-spacing is inherited.

**Two of the fixes were mine to break again in the same sitting**, both caught by running rather
than by reading: the inverted unit regex allowed whitespace between number and unit, so
`margin: 0 auto` read as the length "0 auto"; and the precondition field carried a trailing colon
into a pattern that appends its own, demanding two and matching nothing, which reported every
exemption's reason as missing. And `CA1875` — an analyzer error **only the
`ContinuousIntegrationBuild` flag reports**, which a plain `dotnet test` was green over.

**One of my own re-runs was a misaimed mutation reported as a survivor, for the second time this
slice.** The comma-list rule was inserted at `.boot-sub`, line 84, which is *earlier* in the file
than `.budget` at 239 — so the cascade genuinely resolved to sticky and the mutation never reached
the state it was testing for. Re-aimed after the rule it had to override, both it and a
more-specific-selector variant are caught. **Placement in the cascade is part of aiming a
mutation, not a detail of it.**

3841 tests to **3854**. Zero warnings at CI strictness. **Payload: unchanged** — no file added,
no byte of CSS beyond the token declarations.

### The visual redesign: two faces, and the filter box somebody actually asked for

All six items of Slice B, chosen after looking at [pnpready.com](https://www.pnpready.com/) —
a companion app for this game whose scope is not worth chasing and whose presentation is.

**1. Two typefaces with distinct jobs**, which was the single biggest difference. **Oswald** for
display and **Public Sans** for body, both self-hosted under `web/wwwroot/fonts/`, both SIL Open
Font License with the licence text shipped beside them — a condition of redistributing them, not
a courtesy, and this repository redistributes them on every deploy and every fork. Public Sans
over Source Sans 3 on payload: 103 KB variable against 642 KB for the same job. Both variable, so
one file covers every weight.

**The tokens are the point, not the faces.** `--font-display` and `--font-body` live in
`theme.css` and nothing else names a face, which is the same discipline already holding for
colour, radius and duration — so the house style is one edit and no component can drift.
`NoComponentNamesATypeface` checks both spellings, because `font:` shorthand carries a family
too and `font: inherit` is all over `app.css`.

**Two guards, both demonstrated by mutation.** A font file that goes missing degrades the whole
app to the system fallbacks *silently* — the stacks name fallbacks deliberately, so a failed load
still leaves a readable page, which means nothing but the bytes on disk can catch a renamed file.
And a family may not ship without its licence.

**2. Labels carry the structure of the long forms** on screen, rather than borders alone. Written
first as a `.label-line` class that was applied to nothing — dead CSS, found by looking at the
rendered page — and now on `label` itself, which is where a long form needs it: the Sources
editor is eighteen fields in a column. `SheetSection`'s centred bar heading is untouched, because
it is right on *paper* and the published Hero Sheet prints it that way.

**3. The tier choice is a card grid**, six cards each carrying its consequence on a line of its
own, built as a `cards` modifier on `OptionList` rather than as new markup — which also deleted
the hand-rolled Panel grid the page used to carry.

**4. Every derived figure shows the rule it came out of.** **A statement of the rule and never a
working of it**: the engine returns a number, not the terms it added up, and reconstructing them
in the browser would be the one thing this front end exists not to do. Off on the sheet, because
the published sheet prints no formulas and one page is a margin four blocks of small print would
spend. **It overlaps the "Where they come from" panel on that page and was left overlapping** —
the panel's value is the live working with the character's own numbers, which the rule under the
figure cannot be; if one of the two goes, it is the panel's rule paragraphs and not the workings.

**5. The rulebook's word beside each rank** — `4d Noteworthy`, `4d Proficient`. Presentation only:
`rank_guide` has been on every entry in `abilities.json` and `talents.json` the whole time, read
into the models the whole time, and shown by nothing. **Both tables stop at 6d and above that
there is deliberately no word**, so a 7d Trait shows an empty cell rather than the nearest one,
which would be this program inventing a rung the book does not have.

**6. One filter box, in the component all five pickable lists share.** The only item here that
came from somebody using the thing: scrolling 141 Powers. **The Powers tab had the only search
box in the app** and it now has none of its own — the box moved into `OptionList`, so Pros, Cons,
Perks, Flaws and gear features got one by being lists of options. It reads a row's tags as well as
its text, because the Powers box did and tags are never printed, so moving it would otherwise
have quietly narrowed the one list that already worked.

**Two bugs found by looking at the rendered page, neither of which any test would have caught:**

- **The count read `282 of 282` where the rulebook has 141 Powers.** A row inside a
  `CascadingValue` is reached from *both* directions when the value changes — the parent
  re-renders the fragment holding it, and the cascading value notifies its subscribers — so
  `OnParametersSet` runs twice per pass and every row counted itself twice. **It read as a
  plausible number beside a list nobody counts.** The row now counts itself once per pass however
  often it is asked, rather than the list assuming how often Blazor will ask; the test asserts the
  count against the rows underneath it rather than against a figure from the rules.
- **A tier card printed `TRAIT CAP 12D`.** Every label in this redesign is uppercased and that
  line carries a *rank*, which the rulebook writes `12d`. The card is now the one label that is
  not uppercased, for a rules reason rather than a taste one.

**And then the same bug a second time, which is what turned it into a guard.** Uppercasing every
form `label` made "Custom features (Ch.6, p.93)" read `CH.6, P.93` — a rulebook citation in a
notation the rulebook does not use. Several labels are whole sentences besides: a Pro's narrative
constraint reads "Player must define the specific condition when purchasing." So `label` keeps
the face, the tracking and the muted ink, and drops the capitals.

**`UppercasedTextTests` guards the class, in two halves, and one half is not enough.**

- The **rendered** half takes its selectors from whatever `app.css` actually uppercases today —
  never a list somebody remembered to update — and asserts that no such element on a rendered
  page carries a rank or a citation. Reinstating the capitals on the tier card's cost line fails
  it, quoting `Trait Cap 8d`.
- The **source** half exists because the rendered half **could not see the case that caused it**.
  The gear labels live several interactions deep, and rendering that page with a character
  loaded produces *zero* labels — so the mutation left the rendered theory green. That is exactly
  "a runtime test is only worth the paths it drives", found by mutating rather than by trusting a
  new test because it was new. It is conditional on the stylesheet, so the constraint lifts if
  the capitals ever go.

**The printed sheet was re-proofed on paper, not on screen**, through the bUnit-plus-headless-
Chrome route with `--print-to-pdf` and `--no-pdf-header-footer`, and rasterised with Docnet plus
ImageSharp pinned below 4.0. **Three sheets print as three pages in both palettes** — the new
metrics did not cost the one-page property — white paper, navy or crimson ink, heading bars still
a tint. That harness is now `ProofPages`, which writes nothing unless `PP_PROOF` is set.

**What this did not close.** The fonts ship as `.ttf` and would be roughly 40% smaller as
`.woff2`; there is no converter and no network on this machine, and it is a one-line change per
face when there is. It also adds ~372 KB to a payload item 5 already calls large.

**The fonts were verified as far as the published output**, not merely the build: `.NET` static
web assets do not copy into `bin/wwwroot`, so a build tells you nothing about what ships. A
`dotnet publish` puts all three faces and both licences in `wwwroot/fonts/`.

**An adversarial review then found eight holes, one of which reverted the slice.** Every one was
demonstrated by mutation there and re-demonstrated here after the fix.

- **Both `@font-face` families could be pointed at the same file.** Every heading, label, figure
  and section bar rendered in the body face — with both tokens declared, both different, both
  asked for, every file present and licensed, and four font guards green. **Nothing correlated a
  family to its own file**, so the headline item of this slice silently reverted and the app
  looked exactly as it had before. `EachFamilyIsServedItsOwnFile` is three lines in a test that
  already computed both halves.
- **The rank word had no rendering coverage at all** — its only guard was the CSS rule check,
  which passes while the word is wrong, invented or hidden. Three mutations went through: reading
  the 6d word for every rank above it (*the exact failure the component's own comment says is
  prevented*), an off-by-one printing the 5d word beside a 4d Trait, and `display: none` on the
  class. `RankWordTests` renders it against the rules' own guide with two anchors from the printed
  tables, and the CSS half now refuses `display: none` over every rule targeting the class.
- **The licence guard was `File.Exists`**, so a 23-byte stub reading "Oswald is a nice font."
  satisfied it. **This is the one green in the slice that carries a legal claim.** It reads the
  licence text and the reserved font name now, so one family's licence cannot stand in for the
  other's.
- **Four of the five lists could drop their filter box silently**, and matching by *description*
  was untested while four placeholders promise it. Both were one test each — and **both had to
  drive the page rather than render it**: the gear features are two clicks in and the Pro/Con list
  is behind a toggle, so the first version of that test passed against a page with no options on
  it at all, which is the same blind spot in a new coat.
- **`index.html` was scanned by neither the colour nor the typeface rule**, and it is the other
  file in the payload that can carry CSS — `style-src 'unsafe-inline'` means an inline block there
  applies rather than being blocked.
- And a comment cited **Elasticity**, which is not a Power in this rulebook. `CLAUDE.md` records
  that exact slip being made once before from a stale note; this is the second time.

**One finding was about method rather than code, and it is in the traps list now.** A crashed test
process still prints `Passed!  -  Failed: 0`: the endless render loop this component's guard
prevents ends in a stack overflow, 31 of 153 tests never run, and the summary line reads as green.
The exit code is 1 so CI catches it — a person grepping for `Passed!` does not.

**A second review measured rather than read**, resolving the `color-mix()` tokens itself and
validating them against Chrome's own resolution, so its numbers are figures rather than
impressions. It found a **real WCAG failure**: Villain `--heading` on `--accent-soft` is
**4.08:1**, and 1.4.3 applies to a hover state. It also found the print-specificity trap for
the **third time in this file**, `PointsRule` unreachable, item 4's formulas duplicating the
panel below them on the only page they appear, no cache header on 381 KB of fonts, and the
Iconic tier card printing its one sentence twice — which, because the cards are an
equal-height grid row, cost the two cards beside it five lines of dead white.

**Then a fix-audit — a reviewer pointed at the fixes rather than the code — and it was worth
more than either review before it.** Three of the fixes did not hold:

- **`EachFamilyIsServedItsOwnFile` caught the mutation it was written for and missed two
  routes to the same regression.** `FontFaces()` filtered out any face whose `src` it could not
  parse, so an **absolute** `url("/fonts/PublicSans-Variable.ttf")` — live under
  `<base href="/">` — dropped the Oswald face from the family check *and* from the licence
  check, and the app served Public Sans for every heading with all 96 tests green. **The filter
  written to make the guard robust widened the hole it was closing.** The second route was
  overwriting one font file with the other's bytes, which no name correlation can see; the
  check now reads the TrueType `name` table.
- **The licence guard's three strings all appear in an OFL file's first nine lines**, so
  `head -9` — 383 bytes, permission grant and warranty disclaimer deleted — passed.
- **`UppercasedTextTests` closed with `Assert.True(seen >= 0)`**, a tautology, and **13 of its
  25 selectors matched nothing on any rendered page**. Setting `.verdict` to
  "Trait Cap 12d (Ch.9, p.150)" rendered `TRAIT CAP 12D (CH.9, P.150)` with the suite green —
  the exact bug class the file exists for. It renders fourteen components now and **refuses a
  selector it never found**, with six named exemptions.

**And the fix for the contrast finding was itself half a fix**: moving the tier card off
`--accent-soft` left `.btn:hover` on the identical failing pair and `.btn.danger:hover` on a
worse one (3.94:1 Villain, 4.55:1 Hero). There are far more buttons than tier cards, so the
stylesheet carried a comment calling the pair a fault eleven lines above two live instances.

3772 tests to **3841** — 3645 on the engine, 196 in bUnit. Zero warnings at CI strictness.

### The budget bar becomes chrome, and the plan for what the front end could be

The budget was a bordered panel costing about **110px above every step** — roughly a quarter of
a laptop viewport, six times over, most of it a seven-part table consulted occasionally while
occupying the space continuously. A persistent budget is *ambient status*, and status belongs
where the banner is rather than in the column the reader scrolls.

It is a **sticky strip** now: the spend set large against its budget as a denominator, what is
left beside it, a **3px rail** bled to the width of the shell along its own bottom edge, and the
breakdown disclosed on request. About 40px, and it stays put. Whether the disclosure is open is
a field on the component — deliberately **not** on `CharacterSheet`, which is what gets exported
and restored.

**It shipped unreviewed and had three defects**, all found by the fix-audit and all now covered
by `BudgetStripTests`: the negative-margin bleed is fixed while the shell's padding is not, so it
overflowed 8px at 375px; the `progressbar` announced `valuenow=132` against `valuemax=125` when
over budget — invalid, and disagreeing with its own clamped fill — and had no accessible name;
and `aria-controls` pointed at an element that only exists while open.

[`docs/FRONT-END-PLAN.md`](docs/FRONT-END-PLAN.md) is the plan for the rest, in six phases. **Its
one load-bearing decision is that there is no animation library**: `element.animate()` does
everything on the list, the payload is already item 5 below, and the View Transitions API does a
thing no library can. The shortlist is named if that is overruled.

### A1, A2 and A3 reconciled onto one branch, and the arithmetic that says nothing was dropped

The three sub-slices of the mutation audit were worked **concurrently, one branch each**, all
three forked from `5867340`. That is why the three entries below each read as though they were
the last session: none of them could see the other two.

**They collide on four files and nothing else** — `PROGRESS.md`, `CLAUDE.md`, `docs/HANDOVER.md`
and `ValidationIssueStructureTests.cs`. The first three are documents and were resolved by hand;
each branch had rewritten the same counts and the same "the last session did X" paragraph, so the
conflict was real but its resolution is prose. **The fourth resolved itself and was checked rather
than trusted**: A1 made `CaseNames` and `Build` `internal` so `McpServerTests` could drive the same
sheets, A3 added 592 lines of new tests elsewhere in the file, and the two never touch the same
declaration — so the clean auto-merge is clean for the right reason, not by luck. No test and no
source file was resolved by hand.

**The check that the merge lost nothing is the test count, and it reconciles exactly.** Base
`5867340` was 3322 engine + 124 bUnit = 3446. A1 added 79 engine, A2 6 engine and 19 bUnit, A3 222
engine. Predicted 3629 + 143; measured **3629 + 143 = 3772**, zero warnings, at
`ContinuousIntegrationBuild=true`. A merge that silently dropped a test file would land under that
number, and a merge that duplicated one would land over it.

**The count is necessary and not sufficient, so each slice's flagship guard was re-run by mutation
*on the merged tree*.** A test can survive a merge and stop biting: the count only says the method
is still there, not that the assertion inside it still fails when it should. Five mutations, each
confirmed applied with `git diff --numstat` before the suite was believed — A2's tier substitution
in `SheetView` (4 red), A2's `Find` made case-sensitive (3 red), A2's `print-color-adjust: economy`
(1 red), A1's substring matching restored in `Mentions` (8 red), A1's `ReadEverything` no longer
reading the embedded guide (1 red), and A3's `DUPLICATE_PRO` reporting `ValidationSubject.Ability`
(2 red). All still bite on the merged tree.

**One near-miss worth recording, because the search for it is what should be copied.** Three
`git stash` commits were left dangling on the A1 branch and one of them holds a test —
`TheStartupCheckReadsTheEmbeddedGuideAndNotOnlyTheRules` — whose name appears nowhere in the merged
tree. It had been **renamed**, not lost: it is `TheStartupCheckReadsTheGuideAndNotOnlyTheRules`, and
deleting `_ = _guide().Length;` from `ReadEverything` turns it red. But a name comparison against
dangling work is a cheap check that found the one thing worth checking, and `git fsck
--lost-found` after a parallel-branch merge costs a minute.

**Each entry below still quotes the count measured on its own branch**, deliberately — rewriting
them to the reconciled figure would make three true statements into three false ones. The figure
for this tree is the one in the table at the top of this file.

### The MCP server's twelve guards that held nothing — slice A1 of the mutation audit

**Verified on Linux at CI strictness as well as on Windows**, from a `git archive` export of the
commit rather than in place: 3401 + 124 green, zero warnings. That step is not ceremony here — one
of the fixes in this slice *was* a Windows-shaped assertion that passed locally and would have
failed the container (`Path.IsPathRooted(@"C:\Users\…")` is `false` on Linux, because a backslash
is not a separator there and `C:` is not a root). It was caught by reading rather than by running,
and the run is what confirms nothing else of the shape is left.

Twelve tests claimed to pin behaviour and did not. Each is recorded with the mutation that
defeated it, because the mutation is the evidence and an argument is not. Every one was confirmed
to survive the suite *before* being fixed, and confirmed to fail it after — with the production
code reverted in between, so no fix is a claim.

**The two that mattered.**

**A `Console.WriteLine` inside a tool body reached the client's standard output with both guards
green.** `NothingWritesToStandardOutput` scans each line for the token `Console.`, so `Console`
and `.WriteLine(…)` on two lines contains it on neither.
`TheBuiltProgramSpeaksNothingButTheProtocol` sent `initialize`, `notifications/initialized` and
`tools/list` and stopped — so it never entered a tool body at all, and covered the startup path
and the handshake and nothing else. Confirmed live: line 2 of the JSON-RPC stream was
`searching for turns invisible`. The same write in `ListOptions` *was* caught, and only because
`ReadEverything` calls it at startup.

The fix is that the runtime test now **calls all six tools**, and each answer has to carry
something only the far end of that body produces — a call answered "unknown tool" would otherwise
satisfy it while running no code at all. **Not another regex**, which would only move the hole:
the spelling after a multi-line one is a helper in another file, or a library. `CLAUDE.md` claimed
the two halves were complementary and is corrected; so is the scan's own doc comment. Its
deadline is 30 seconds and it says which read ran out, because "no greeting at all" and "a request
that never came back" are both real mutations and mean different things.

**`search_powers` could be widened back to substring matching by one line** — the failure
`Mentions`' own summary calls "worse than no match", since it arrives looking exactly like a real
answer. Confirmed live: `search_powers("she bakes bread in the city")` returns **Plasticity**.
Every existing search test was a positive assertion or a negative on a query whose words happen
not to be substrings of anything. It is pinned now by four fragments that occur inside a Power's
name and nowhere in the rules files as a word — `city`/Plasticity, `ration`/Regeneration,
`art`/Martial Arts, `kinesis`/Telekinesis — each with the positive control beside it, plus the
baker's sentence held at `found: 0`. **Both `CLAUDE.md` and the method's own summary named
*Elasticity*, which is not a Power in this rulebook**; a reproduction would have searched for an
entry that is not there. There turned out to be a **third** copy, in this file — the correction
missed it, and a reviewer pointed at it rather than a test, because nothing greps prose for the
name of a Power that does not exist. All three now say Plasticity.

**The rest, where one payload assertion replaced the fields somebody remembered.** This is the
shape of the whole slice: twelve separate field assertions is what produced the gaps, because a
field added later is a field nobody wrote an assertion for.

| Was free | Now |
|---|---|
| `power_detail`'s `cost_variants` → `null`, plus `category`, `stat_line`, `rank_type`, `cost_type`, `max_rank`, `unit`, `description`, `source_ref` each replaceable with a constant | Every field of all 141, against the model, **with the key set asserted** so a new field fails until it is read. `cost_variants` was the sharp one: it is the only place a caller learns the accepted keys, and `check_character` refuses a `per_rank_variable` Power for lacking one — the tool taught a dead end the judge then closed |
| `list_options`' gear-feature `cost_type` and `grades` (nulling the second kills the two graded features' keys), flaw `flaw_type`, perk `unit`, talent `ordinary_human_rank` | Every field of every entry of all ten catalogues, plus the entry count and the report's own key set. `TheCatalogueNumbersAreTheRulesOwn` stays — it reads the numbers pairwise and says why each matters; this is the completeness half |
| An issue could drop `owner_id` and `options` — the two a repair loop cannot work without | Every field of every issue, **driven from `ValidationIssueStructureTests.Cases`**, which is itself held to the validator's source, so the codes covered are the ones the validator can construct rather than a list that goes stale. `CaseNames` and `Build` are `internal` for that reason: a second copy would be the stale one. A companion test asserts the sheets really do reach all six optional fields, since two nulls compared to two nulls is how this gap arose — and it checked **five** when this sentence first claimed six, which is the "three of four" shape a reviewer has caught here before. The sixth is asserted now |
| `QUESTION-POLICY.md`'s schema **prose and comments** — only the fenced block goes through the strict reader, and with its comments stripped. Rewriting the sentence for a Pro to `ProId`/`Count` passed | Every quoted PascalCase name anywhere in the document must be a property in the character's object graph, by reflection rather than against a list here. Reading is strict, so a proposer following the prose gets an unreadable character |
| **The whole Claude Desktop half of `docs/MCP-SETUP.md`** — breaking both blocks' `command` paths passed, because every other test regexes `claude mcp add` | Both blocks must parse as JSON, key the server under the same name the Code commands use, and give an **absolute** path ending in the binary this repository builds. One block keeps its doubled backslashes and one has none, which is the difference the pair exists to show |
| The startup diagnostic the guide's first troubleshooting bullet sends a stuck reader to find | Read off standard error by the runtime test, and required to name the rules directory |
| `CharacterServer.Instructions` reduced to `"creation_guide check_character the engine decides"` | A length floor, the "never state a figure" clause, and a sentence count. Note the asymmetry that made it worth fixing: every tool description had a 60-character floor and the instructions — the model's only guidance *before* it picks a tool — had none |
| `ReadEverything` no longer reading the embedded guide | Asserted, and **honestly caveated**: a resource cannot be un-embedded from a loaded assembly, so there is no runtime arrangement in which the guide is absent. The line that reads it is what is checked, and the test says that is a limitation rather than a preference |
| The search limit's upper bound: `Math.Clamp(limit, 1, 25)` → `(limit, 1, 400)` passed, because every row searched "armor", which matches fewer than 25 | Wide rows on a query that overflows the ceiling, and a test asserting that it really does — so the clamp rows prove something about the clamp |

**What the two adversarial reviews then found, which is the part worth reading.** Fourteen further
findings across two passes, **most of them inside the fixes above**, then six more from a third
review pointed at those. All are closed and each was demonstrated the same way. (An earlier version
of this sentence said "nine of them"; the figure is not reconcilable from the list either way,
depending on how the two `ReadEverything` findings are grouped, so it is not stated as one.) Four
patterns, and they are the transferable part:

1. **A marker that proves a code path ran must be something only that path can produce.**
   `character_sheet`'s marker was the character's name — which the test sends as the argument and
   `check_character` echoes back as `character.name`. So wiring the `character_sheet` wire name to
   `CheckCharacter` passed, and a client asking for the printed sheet would have got JSON. It was
   the only test that drives that wiring at all, since every other one calls the method in process.
   It is the sheet's masthead now.
2. **A driven test covers the arguments it sends and nothing else.** Every tool's *refusal* branch
   was undriven, so a two-line `Console.WriteLine` in `TryReadCharacter` — reached by a misspelled
   field, the commonest first mistake there is — passed the source scan and the runtime test both.
   Every tool is now called twice, once to its answer and once to its refusal.
3. **A source-reading guard is worth what its instrument can see, and mine could see very little.**
   `ReadEverything` could go back to warming one catalogue (`ListOptions(Categories[0])`) with the
   grep green, because the token `Categories` survives; and the read of the embedded guide could be
   deleted and replaced with a *comment* mentioning it, which is what somebody removing that line
   would actually write. The first is now a runtime theory over `RulesRepository.DataFileNames` —
   a directory missing any one rules file must be refused, which is the property, and the two older
   tests both used a directory holding `tiers.json` alone that throws whatever else is broken. The
   second was defeated twice more — a comment mentioning the token, then `nameof(QuestionPolicy)` —
   before being made a real runtime test: the guide is handed to `CharacterTools` the way its clock
   already is, so a guide that throws is an arrangement a test can build and deleting the read
   fails. **A token is not a read, and no amount of text-matching makes it one.**
4. **A phrase assertion cannot survive a "not" in front of the phrase.** The baseline note and the
   server instructions were each pinned by required phrases and each inverted while keeping every
   one of them — "do **not** stack on top of this baseline, and the Trait Cap applies to the total,
   which is the baseline alone", and "It is a **myth** that the arithmetic is not guessable". Both
   are asserted verbatim now. Where the content of a sentence *is* the deliverable, the sentence is
   the assertion, and the duplicated literal buys the only thing that matters: a change to it has
   to be deliberate and visible in a diff. The question policy's two confusable shapes needed the
   same treatment for a different reason — swapping the two groups whole leaves both field sets
   valid and only the English inverted, which no structural rule can see.

The rest, briefly: `grades` could be *narrowed* rather than nulled, dropping the −4 grades of the
two Cons the guide calls mandatory (key sets are exact both ways on every payload asserted whole;
three one-sided `Except` checks remain, each documented in situ where the field set is genuinely
open); `pros.own` and
`cons.own` were not read at all, so three fields could go constant and a new one appear unnoticed;
`Zip` truncates in silence, so an emptied array ran every loop zero times and fired no key check;
the baseline `note` — the one string in `power_detail` that is not the engine's answer — could be
inverted to say purchased ranks *replace* the baseline, across all 27 baseline Powers; the question
policy could name a real field on the *wrong type*, which reflection over a flat name set cannot
see, so each shape it writes out is now matched against the type that has those fields; the Desktop
`command` paths could point somewhere no publish command produces, and `(\.exe)?$` accepted `.exe`
on the macOS block — and then, once the extension rule existed, it asked `StartsWith("C:")`, so a
`D:` path dropped the `.exe` and passed; `force_field`'s `area_burst` grades were pinned nowhere, so
losing `burst` from the only place a caller learns it was invisible, and all three of its own-text
allowances are pinned by key now; `Judgement`'s guarded engine calls were still unentered, because
the unpriceable character went only to `character_sheet`, which never reaches them; a *corrupt*
rules file, as against a missing one, was covered nowhere though `Program.cs` catches
`JsonException` for exactly it; and one *opposite* failure — an extra, entirely legitimate line on
standard error broke the runtime test, which reads as a stdout regression and is nothing of the
kind, so it drains until a line names the rules directory.

**What is still not closed, stated rather than left to be found.** The stdout pair covers the
startup path, the handshake, and both branches of every tool; a write reachable only from an
argument shape nothing sends is still seen by neither guard. A `list_options` field that is null
for every entry in its catalogue would be caught now that key sets are exact, but the general point
stands: these tests are worth the payloads they ask for.


Newest first. Link the PR so the reasoning stays findable.

### Thirteen guards on the browser and the replay that were not guarding anything

The audit backlog in `docs/HANDOVER.md` listed 38 surviving mutations across the whole tree,
grouped by subsystem. This closes the browser-and-replay group — **all thirteen, each
demonstrated by re-applying the mutation, confirming red, reverting and confirming green.**

**The first five are one bug class, and it is the one `ReplayRenderTests` was written for.** The
four `.stat-block` figures and the Power ranks were genuinely pinned to the recorded character;
nothing else on the page was. The tier in the masthead and the colophon, the budget sub-line, the
Quote, the Motivation, the Description, the Connections and a rankless Power's stand-in rank could
all be moved from the printed character to the visitor's own with the suite green — so a replay
would print Vera Nunn's name over somebody else's tier, budget, words and connections.

Five more named assertions would have moved the boundary rather than closed it: the eighth field
somebody adds is unguarded again the day it is added. So the test compares **two renderings of the
same character** — one where the session holds it, one where the session holds somebody else
entirely and it arrives as a parameter — and asserts they are the same page. Every read of
`Session.Sheet` that should have been a read of the parameter is a difference between them,
whatever field it lives in. Three cases it cannot reach have their own tests: the stand-in rank
(no recorded character has a rankless Power, so it is built from the Hero sample), the budget flag
the replay passes for a Villain, and a capitalised address, which Blazor routes happily and
`ReplayLibrary.Find` then refused.

**The transcript honesty rules were narrower than they read.** The figure scan read `Title`,
`Blurb` and the recorded lines — never the character, which is what the sheet at the end is
printed from, so a Hero Point total in a Motivation, a Quote, a Description, a Connection or a
Flaw's narrative detail passed. It walks the character by reflection rather than naming six
fields, because a list of field names is exactly what goes stale here. Its word set was the
engine's vocabulary and not the page's: `ReplayVerdict` labels the gap `Over by` and `Left`, and
"nineteen over … three to spare" matched none of `HP|hero points|points|edge|health|resolve|
budget`. Those positional words now count, at a **one-word window** rather than three — at three,
Vera Nunn's "an apron over a cardigan" is a quoted figure.

And questions were counted by `'?'`, so seven imperative demands asked nothing at all — a
questionnaire, in the one file whose job is to demonstrate the opposite. The counter recognises a
demand now, and is honest in its own doc comment about being a heuristic; a second test counts the
**person's replies**, which no phrasing can get round.

**Three CSS rules were asserted by presence rather than by value**, which is the same mistake in
three places: `print-color-adjust: exact` could become `economy` (every heading bar prints white —
the failure the rule's own comment describes), `.hp` could be set at 2.4rem/800 (a Hero Point cost
three times the size of the rank beside it), and the 7pt print floor matched only sizes already
written in `pt`, so the two densest blocks on the sheet could go to `0.3rem` and `4px` unseen. The
floor now requires every printed size to be in points, which it has to be to mean anything: `rem`
is relative to a root size the print block resets.

**"A failed transcript fetch must not stop the app" had no test**, because the guarantee was a
`try`/`catch` in top-level statements that nothing can reach. Deleting it was green, and one 404
then took the whole character generator to a blank page. It is `ReplayLibrary.LoadAsync` now,
which takes the fetch as an argument and can therefore be handed one that fails — four tests,
including that one file short leaves **no** recordings rather than most of them, plus a source
test that `Program.cs` actually goes through it. Both halves are needed: a guarded loader nobody
calls guarantees nothing.

**And the strip-tags trap was in the helper `SheetRenderTests` uses to catch it.** `Rendered`
replaced every tag with a newline and one test then collapsed all whitespace, so
`<b>Armor</b><span>8d</span>` read as "Armor 8d" — the string the assertions look for, produced by
the bug they exist to find. Demonstrated both ways: splitting a Pros line into per-word elements
is red against the concatenated text nodes and green against the old helper.

**Then two reviewers found fourteen ways round the fixes, and thirteen are closed.** That is the
most useful number in this entry: the first pass at closing a finding is roughly half right, and
the review that goes looking for the *hole in the fix* has now been the most valuable one three
sessions running.

**Nine were one shape wearing different clothes: a check that reads one place while the thing it
guards is decided somewhere else.** Three CSS guards read the first matching rule, or one rule by
its exact selector, while the cascade reads the last — so a second `.hp` rule further down, or a
second `font-size` inside the same block, or `print-color-adjust: economy` written after `exact`,
all did what the guard forbade with the suite green. They read every declaration of every rule
that targets the class now. The 7pt floor read `font-size` and never the `font` shorthand, which
sets a size without writing the property; and it reads declared sizes, so `zoom: 0.55` on the
printed sheet left every declaration legal and printed the stat lines at about 4pt. The shorthand
and page-scaling are both refused outright. The source check on `Program.cs` asserted the guarded
loader was *called*, which stays true if the fetch is hoisted back outside it — so `LoadAsync`
takes the `HttpClient` and there is nothing left to hoist. And the path it fetches from was
pinned by a `Contains`, which `data/transcript` satisfies against `data/transcripts`: the
one-character mistake the test existed to catch, passing it.

**Two were preconditions that had quietly stopped being true.** The sheet-equality pool crossed
tiers on one row of four, because Vera Nunn is the only recorded character who is not Standard —
so the masthead, the Trait Cap and the budget sub-line would have stopped being covered the day
she changed, with nothing failing to say so. And the verdict panel's Perks and Gear rows were
asserted against the engine while every character in play had zero of both, which cannot tell two
characters apart; that panel now gets the same two-visitors treatment the sheet does. The
Villain's budget finding was asserted to be *not called illegal* and never asserted to be
**shown**, though showing it is what that recording is about.

**One was not closeable by vocabulary, and needed a different kind of rule.** "She lands on 75
exactly, and the tier hands her 75 to spend" quotes her spend and her budget in one sentence and
matches no word list anybody could write. So a second rule asks the engine what the figures are
and refuses those numerals outright, in digits and spelled out — which is what `CLAUDE.md` says
the rule is. It sits beside the vocabulary rule rather than replacing it: the vocabulary rule is
about *shape*, and "over by a full nineteen" is a quoted figure whether or not nineteen is the
right answer.

**The one still open is recorded rather than fixed**, because it cannot be fixed by a test.
`IsARequest` recognises the phrasings a demand is normally written in; an unlisted verb —
"Settle the tier. Work out whether she is one Power or several." — scores zero. Its companion
counts the person's *replies*, so seven demands bundled into one turn cost one reply. An earlier
version of that note called the reply count "the half no wording can defeat", which was wrong and
now says so. Four hand-written recordings that change rarely have one real guarantee, and
`CLAUDE.md` already states it for the prose: **read a changed transcript.**

### Slice A3 of the mutation audit: eight guards that did not hold, and the three that were the wrong shape

Eight findings from the mutation audit recorded in `docs/HANDOVER.md`, all in the engine and
validator. **None of them was a bug in the product.** Every one was a test that did not hold what
it claimed to hold, demonstrated by a mutation that left the whole suite green — and every fix
here is demonstrated by the same mutation turning it red, then reverting to green.

**The first three are one bug in three places, and it is worth naming as a class: a lookup that
silently skips what it omits turns its own omissions into exemptions nobody chose.**

- **A validation issue could name the wrong kind of thing.** `EachCodeReportsTheKindOfThingItIsAbout`
  did `TryGetValue(...) continue` over a table missing **eighteen of the validator's forty-five
  codes**, so `DUPLICATE_PRO` could be relabelled an Ability problem — verbatim the failure the
  test's own doc comment exists to prevent, and one a repair loop follows into the wrong
  dictionary. The fix is the shape rather than the eighteen entries: the table is now indexed
  rather than probed, so an unlisted code throws, and `TheKindOfEveryCodeIsWrittenDown` holds it
  to the validator **in both directions** — a new code fails until somebody writes down what it is
  about, and a removed one fails until its line goes.
- **An issue could offer options of the wrong kind entirely.** `EveryOptionOfferedIsOneTheRulesAccept`
  asked only whether each string was an id of *anything*, as a union over ten collections — which
  every id in the rules satisfies. So `POWER_WITHOUT_SOURCE` could offer the six Ability ids: a
  repair loop writes `"agility"` into a Power's Source, gets `UNKNOWN_SOURCE` back, and never
  terminates. What each code offers is now written down per code, and **a code carrying options
  that nothing characterises fails** rather than passing on the union.
- **The "every code is provoked" guarantee was spelling-shaped**, and so was every structural
  invariant downstream of it. The scan was `"([A-Z]+(?:_[A-Z]+)+)"`, which requires an underscore:
  a clean A/B on the same unreachable check had `TOO_MANY_CONNECTIONS` going red and
  `TOOMANYCONNECTIONS` staying green. It now matches by case. **The part that matters is that it
  is driven against a synthetic source**, in `TheCodeScanIsNotDefeatedByHowACodeIsSpelled` —
  reading the shipped validator cannot distinguish a pattern that finds every code from one that
  finds every code somebody happened to spell with an underscore. This is the third time that scan
  has been too narrow, and both earlier escapes were also *how the code was written*.

And the five specific ones:

- **A Power could be deleted from a sample character.** The budget assertion was `spent <= budget`
  and nothing else, and "fills every section" only asks whether a section is non-empty — so
  deleting `stun` from the Hero was green. The budget is now checked at both ends, the Powers each
  sample carries are recorded, and a second test says *why those Powers*: a baseline that is half a
  Trait against one that equals it, a rate below 1 HP per rank, a rankless Power, two Super Senses
  options, a Power carrying a Con, a Source-less Power. Naming the ids catches a deletion; the
  shapes catch a replacement that costs the preview the thing it was previewing.
- **A Power description could be replaced with unrelated prose.** `verified_fields` carries
  `description`, and that flag is a boolean claim about a page somebody read which **survived any
  edit to the text it was made about**: Armor's whole description became "A quiet afternoon in the
  garden, with tea." and every test stayed green, the verified flag included. The descriptions are
  now digested in `CanonicalPowerDescriptions`, so the claim is bound to the text and the failure
  names the page to go and read.

  **Three similarity framings were measured first and none of them is a rule** — recorded so
  nobody re-derives them. A description need not repeat its own Power's name: 48 of the 141 do
  not, and that is good writing. Word overlap against the printed entry in `data/rulebook/` fails
  because these descriptions are deliberately *re-worded* rather than quoted — Blind Fighting's
  shares one distinctive word in nine with the page it came from ("sight" for "vision", "fight"
  for "combat"), which is the policy working. Ranked against all 1439 sections of the book, 130 of
  the 141 match their own entry best, but Blind Fighting comes **215th** and the Super Senses
  options cannot be scored at all, because the book gives the group one entry rather than one per
  option. Every framing needs a threshold plus named exemptions. **What no test can say is whether
  a description is *true*** — a wrong sentence digests like any other. That is the same limit
  `CLAUDE.md` already records for the replay transcripts, and it is closed by reading the page.
- **An applicability caveat could state the opposite of the rulebook.** Penetrating's became
  "Applies to absolutely any Power at all, no conditions." and the suite stayed green, because the
  only test asked whether it was non-blank and ended in a full stop. This matters more than it
  would elsewhere: **the design is that a caveat is shown to the player instead of being enforced**,
  so its wording is the entire deliverable and there is no mechanism behind it to be right when
  the sentence is wrong. The fifteen are now transcribed in `CanonicalCaveats` with the printed
  clause behind each, **and the clause is asserted to appear verbatim under that option's own
  heading in `data/rulebook/ch02-characters.json`** — under its own heading rather than anywhere
  in the chapter, which a reviewer showed is a different thing: Carrier Attack transcribed with
  Ongoing's real printed sentence passed a whole-chapter search. A caveat must also *restrict* —
  every one opens "Only for" or "Not for" — which refuses the inversion even if the record is
  edited to agree with it. And the recorded clause has to say what it constrains: containment
  accepts any fragment, so trimming one to the boilerplate "This Pro applies to" was a correct
  quotation of the right entry that says nothing, and passed. What is left uncaught is a caveat
  that restricts the *wrong* thing, changed in both places at once; the printed clause sits
  beside it so a reader can see.
- **The pickers' documented "rules-file order" had no test**: `.Reverse()` on `ProsFor` was green.
  It is not cosmetic — Ch.2 prints Pros and Cons alphabetically and a player is looking one up by
  name. Asserted as a subsequence of the rules file, so which options a Power is offered stays
  `IsApplicable`'s answer.
- **`GradesFor`'s defensive intersection is behaviourally dead against the shipped data**, and
  that was the honest finding rather than a defect: every grade an allowance records is one the
  option prices, guaranteed by `EveryOwnTextAllowanceResolvesAndCitesItsPrintedText`, so replacing
  it with `return allowance.Grades.ToList();` changes nothing. It was still worth a test, because
  a guard whose only evidence comes from data that cannot exercise it is a guard nobody knows the
  state of — and the test had to be a **synthetic** allowance naming a key the Pro does not price,
  since no reading of the shipped data can reach it.

**One test-file defect, fixed:** `SampleCharacterTests` called `Sample("Hero")` against a
`which == "hero"` comparison — ordinal and case-sensitive — so it silently built the **Villain**
and the Hero's export path went untested under a test named for it. `Sample` now refuses a name
that is not a sample rather than falling through to the other one.

**The lesson from the previous slice held throughout**: a guard test that reads the shipped data
cannot tell you the mechanism reads it too. Three of the fixes here are driven against synthetic
input for exactly that reason — the code scan, the `GradesFor` allowance, and the caveat record's
anchor in the corpus.

**Then two adversarial passes — one told nothing about the work, one pointed at the fixes — found
six more, and three of them were the same defect one table over.** Both reproduced all nine
mutations as red first, so the fixes hold; what they found is what the fixes did not reach.

- **A kind table keyed by code cannot see a swap *within* a code's list**, and seven codes serve
  more than one kind. Changing `CheckTraitSources`' Ability argument to Talent reported
  `UNKNOWN_SOURCE` on `toughness` as a Talent problem with the suite green — the same
  never-terminating repair loop the slice was about. Closed by the rule the table cannot state:
  **the id has to name a thing of the kind claimed.** Deliberately with no exemption list — half
  these findings report an id the rulebook does *not* have, so "resolves against the rules" alone
  would have to excuse them, and excusing them is what let it through. An id resolves against the
  rules **or** against the part of the character the kind names.

  **The first version of that rule skipped `Character`, and this entry claimed it "covers every
  finding without excusing one", which was wrong** — a third review pass demonstrated it. Eleven
  codes are Character-kinded by design and two more may be Character *or* Power, so mutating a
  kind *towards* Character walked through the rule, through the "a subject is named" invariant,
  and through the table, which accepts any entry in a code's list: `PER_UNIT_WITHOUT_UNITS` could
  report a Power id as a fault of "the character". Character is checked now, against the three
  things that belong to a sheet rather than a Trait — a Perk, a package, a Pro or Con.
- **The code scan was still spelling-shaped**, one spelling further on: `"TooManyConnections"` was
  invisible to every guarantee built on it. Widening the pattern again would not have closed it —
  a pattern can always be out-spelled — so the case is now **enforced where a code is written**.
- **And it read one file**, so moving the codes to a constants class or making the validator
  partial voided all three guarantees silently. Both are ordinary refactors. It reads the engine
  now.
- **`MustOfferOptions` had no exhaustiveness check** — the identical hole to `ExpectedKinds`, left
  open on its sibling. A new code with `Options = []` omitted from the list passed both it and the
  option check, the first by not listing it and the second by having nothing to walk. Read off the
  source now, because a code that offers options only *sometimes* is what a behavioural check
  cannot see.
- **The sample budget floor could not see the Powers at all.** A Standard-tier package plus
  eighteen Traits clears half the budget alone, so a Hero with **no Powers whatsoever** passed it
  — and this file's own account of the fix said otherwise. Corrected, with the Powers costed
  separately.
- **The "every shape" test read `powers.json` rather than the sample**, so `Prerequisite` and
  `CostPerRank` stayed true while the preview stopped showing any of it: zeroing Armor, Danger
  Sense and Resistance left every shape "present" and Armor printing its bare baseline instead of
  4 free ranks and 4 bought reaching 8. It asserts on the selections now.
- The picker-order narrowing covered Pros only, so `ConsFor` reduced to `.Take(1)` passed it.

**Two of the round-two assertions were wrong when first written, and measuring corrected them** —
worth recording, because both were plausible: the Hero's Powers cost **20 HP of 125**, not a fifth
of the budget, and `pros.json` is **not** alphabetical (Zone/Nova sits between Area/Burst and
Armor Piercing, following the printed pairing), which is what makes the Pro half of the order
check real where the Con half cannot be.

**A third pass then re-reviewed those fixes. All six held; it found five more**, and the pattern
across all three rounds is worth stating plainly: *the escape is always one indirection past
wherever the rule was written.*

- The `Character` skip above — this file's own overclaim, now corrected.
- **`OwnerId` had no invariant at all**, only three spot tests, so the two sites they miss could
  hold the printed *name* instead of the id. That exact regression is recorded in the validator's
  own comment as having happened once already.
- **`UNKNOWN_TRAIT_SOURCE` was never provoked on the Talent side**, so the Talent arm of its
  option list was dead and could be made to offer perk ids with the suite green — the round-one
  defect alive on a branch no sheet visited. **The "every code is provoked" guarantee is per
  code, not per construction site**, and that gap is now demonstrated rather than theoretical.
  The case list reaches it.
- The spelling convention reads two call shapes, so **a third helper is invisible to it** — the
  same escape one indirection on.
- `MustOfferOptions` catches a code added with `Options = []` but not one written with no
  `Options` line at all and delisted in the same breath. That is a two-file coordinated edit, so
  rather than chase it the Source findings are now asserted outright — which is the case the
  option check existed for.

**A fourth pass re-reviewed those. All five held; it found four more, and two of them were this
file overclaiming again.** Both are recorded rather than quietly corrected, because the shape of
the mistake is the useful part: *a fix that enumerates one spelling of a thing gets out-spelled,
including when the thing being enumerated is the fix.*

- **The helper pin was the same mistake one spelling on.** It matched methods *returning* a
  `ValidationIssue`, so `AddFinding(List<ValidationIssue> into, string code, …)` slipped past on
  the angle bracket — and appending to a passed-in list is this validator's house idiom, not an
  exotic shape. A generic `Finding<T>` got past it too. This entry claimed "the set of methods
  that build a finding is pinned"; it was not. **Construction sites are counted now** — the thing
  that actually makes a finding, which cannot be hidden behind a signature — and the rule is that
  exactly one of them takes its code from a variable.
- **There are five places that offer the six Sources, not four.** This entry said four, and the
  test did too: it counted a line shared by the Ability and Talent arms twice and missed the
  fifth entirely. The one missed was the **blank** Source — a Trait that names a Source and then
  names none, which has its own sentence because printed through the other one it read "names a
  Source, '', that is not one of the six". Its options could be made perk ids with the suite
  green: the same dead-branch defect as the Talent arm, in the same method, one round later.
- **Requiring words after the boilerplate opener was not enough.** "This Pro applies to Powers
  that" cleared it, and twelve of the fifteen entries open "Powers that…" after their opener, so
  nearly all of them could be trimmed to the same empty phrase. It is the *content* word that has
  to survive.
- And that check refused a record holding the substantive clause **alone** — verbatim, under the
  right heading, strictly more informative — telling the reader to go and find a fifth opener in
  a book where nothing was wrong. The opener is optional now.

3446 tests to **3668** — 3544 on the engine, 124 in bUnit. Zero warnings at CI strictness.

### The rulebook corpus was materially wrong, and its tests could not see it

`data/rulebook/` shipped in [#43](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/43)
described as the book's text, verbatim beneath each heading. It was not, and the way it was wrong
is the dangerous way: **the damaged prose still reads as English.**

**The extractor split every page at a fixed midpoint and emitted the left half, then the right.**
That is right for the two-column body and destroys anything set full width — it cuts each line in
two and files the halves in different blocks. **Every chapter opening in the book is set full
width.** Chapter 2's read:

> "run game more Characters include all beings in the game world, from the Heroes the GM. They
> include not only sentient beings but also animals, and so on."

against a page that prints "…from the Heroes **run by the players to the Villains, Foes, Minions,
and Extras run by** the GM. They include not only sentient beings but also animals, **monsters,
mindless undead, unthinking robots, career politicians,** and so on." Two runs gone, the orphans
parked at the front, and nothing about the result looks broken.

Four more defects, all of which the suite passed:

- **135 of 1303 sections had no text at all.** Chapter 9 ended with eighteen sections scraped off
  the blank Hero Sheet form on printed p.189, under headings like `EEDDIITTIIOONN`.
- **83 sections carried a doubled page number mid-sentence** — "…per extra force field. 2299 PRO
  Inviolate…". The display faces are faked bold by drawing the text twice a fraction of a point
  apart, so the two passes interleave.
- **The rotated marginalia was read as prose**, giving `retpahc` — "chapter" reversed — as a
  section heading.
- **Two facing entries on printed p.52 were one section**, headed `OVERKILL PHASE SHIFT`. That is
  the two-column interleave `CLAUDE.md` already warned about, in the shipped data.

**Measured rather than asserted.** A word-adjacency check against the PDF, calibrated so the two
entries the old test vouched for score 2–6%, put **114 of 1095 sections at 10% or more fabricated
adjacencies**. The damage concentrated in chapter openings, tables and Ch.8's stat blocks; the
narrative Power entries were sound throughout, and Force Field's load-bearing sentence was
verbatim correct.

**The extractor no longer exists as a scratch project.** It was one, and by the time the output
was found to be wrong it was gone — so the corpus could be neither audited nor regenerated. It is
now `tools/RulebookExtractor/`, in the solution so it cannot rot:

```bash
dotnet run --project tools/RulebookExtractor -- <rulebook.pdf> data/rulebook
```

It finds the gutter **per page** and decides a line crosses it by whether a **word actually sits
astride it** — not by whether the line's outermost words fall either side, which is equally true
of two facing headings sharing a baseline, and is exactly how p.52 merged. Full-width lines then
break the page into bands, and the columns are read within a band. Furniture goes by rule rather
than by string: the running foot by position, the purchaser watermark **by font** (6pt Helvetica,
780 words — four per page across 195 pages, and nothing else in the book), the vertical chapter
title by text orientation. Doubled glyphs are removed by testing that two glyphs are the same
character *and physically on top of each other*, which "2299" is and a legitimate "aa" is not.

**Words are split on the page's own space glyphs.** Guessing from letter gaps turned "FORCE FIELD"
into "FORCEFIELD" — in the condensed display face a word space is barely wider than the gap
between two letters. A gap break is kept alongside, because two facing headings have no space
glyph between them at all.

**`RulebookCorpusTests` was almost pure shape-checking and now asserts content, every guard
demonstrated by mutation.** All the defect shapes were re-injected and every one goes red:
blanking a section's text, the given name of the watermark, a doubled page number, `retpahc`,
collapsing a chapter's citations onto one page, overlapping two chapters' ranges, stripping the
character names from Ch.8, and **the original scramble itself, pasted back verbatim.**

**Three tests do the heavy lifting, and each exists because a reviewer defeated what was there
before.** An adversarial pass rotated all 1,492 section bodies onto the wrong headings and the
suite stayed green; it deleted 90% of the book and the suite stayed green; it blanked all 33
sections of Chapter 5 and the suite stayed green.

- `TheCorpusStillHoldsTheWholeBook` — a floor on total prose. A null check cannot see truncation.
- `EveryPowerEntryOpensWithItsOwnPrintedStatLine` — **the one that ties a body to its own
  heading**, across 116 entries rather than four spot checks, by cross-checking the Range against
  `data/rules`. It is also the sharpest reading-order check there is: Ch.2 sets one Power after
  another down two columns, so any column mistake shows up at once as an entry opening with its
  neighbour's tail.
- `EveryPublishedCharacterIsNamedInChapterEight` — the names come from `PrebuiltHeroes`, which is
  held to the printed sheets, so it cannot be satisfied by whatever the extractor produced.

**And `ColumnLayout` is now unit-tested against synthetic pages**, because nothing in CI ran a
line of the extractor: the committed corpus can only show you layouts the book happens to
contain, and every failure here has been layout-shaped. Disabling either gutter detector fails
those tests.

**An adversarial review found the first attempt at this was not sound, and the headline defect was
still in the data by a new route.** That reviewer is the reason this entry describes a working
extractor rather than a plausible one, and what it caught is worth recording:

- **Sixteen pages were still column-scrambled**, 10% of the corpus, because the gutter search took
  the strict emptiest point and then measured the run at exactly that count. On printed p.83 the
  minimum sits on a **1pt spur** where two lines happen to end, while the real 20pt gutter beside
  it is crossed by seven — so the spur failed the width test, the page was called single-column,
  and both columns were emitted interleaved. The same defect this change exists to fix, reached
  from the other direction, on the very page used to demonstrate the fix.
- **108 headings the old corpus had were gone**, including every named character in Ch.8 — see
  above; a name is followed straight by its "hero"/"villain" label, so it never got a body and was
  discarded as empty.
- **The tests were close to theatre**, which is what the three content tests above now answer.
- **A claim in every chapter file was false.** The embedded note said the doubled glyphs were
  collapsed; the de-duplicator written for that job turned out to change **not one byte** of the
  output, because every element that doubles lives in the running foot and is dropped by position.
  It is gone, and the note now says what actually happens.

**And fixing the gutter took four attempts, each of which broke something the last one had fixed.**
Worth stating plainly, because the lesson is that this is not one rule: raising the tolerance fixed
p.83 and left the band too wide, so every line on p.81 read as full-width; switching to per-line
gaps fixed p.81 and broke **26 of Chapter 2's Power entries**, because on an ordinary two-column
page almost every line sits in one column and contains no gap at all. **The old extractor, for all
its faults, got all 116 of those right** — measured, not assumed, and that measurement is what
stopped a regression shipping as a fix. The answer is two detectors with a test each.

**Known limits, stated rather than tuned away.** Chapter 8 sets its stat-block headings in small
capitals, which arrive as `aBIlItIes` and `FlaWs`; two rules were tried — point size, then ascender
height — and the second was worse than the disease, uppercasing "Points" to "POINTS" while leaving
the real cases untouched. A table of three or more columns is read across rather than down. Both
are recorded in the extraction note in every chapter file. Tuning a heuristic until it looks right
on the two examples to hand is the thing this repository forbids everywhere else.

Chapter 9 now ends at printed 188. Printed 189 is the blank Hero Sheet form — a form, not prose.

### Two Heroes in the rulebook this tool refused, and the question nobody had asked

Item 1 asked for a per-element cost breakdown of the four Heroes that do not reconcile. Doing
it settled the residuals as far as they go — see that item, where the answer is that no element
is mispriced — and turned up something the ±1 hunt was not looking for: **two published
characters that this tool reports illegal.**

**Nothing in the suite had ever validated the twenty.** The hero tests ask what a Hero costs
and what their Edge, Health and Resolve come to. None asked whether the character the authors
printed is one the validator accepts. Running that once found both faults immediately.

- **Blastwave's six energy types came back as four `DUPLICATE_PRO` errors.** His Energy
  Absorption prints six, so five copies of Also X, and the Pro says so in its own entry: "You
  can absorb one extra type of energy … **each time you select this Pro**" (Ch.2 p.28); Energy
  Form's copy prices it "for every 2 extra Hero Points" (p.30), and the generic Affect
  Inanimate reads "You can apply this Pro multiple times" (p.48). `CostCalculator` has always
  charged every copy — which is exactly what lands Blastwave on his printed 125 — so the two
  halves of the engine were contradicting each other about the same sheet, one pricing it and
  the other refusing it. Repeatability is now `repeatable` on the option rather than a list of
  ids in the validator. **Only three entries carry it.** The other five Also X entries are
  priced per unit, where the extra Sources are a quantity on one selection and a second copy
  really would charge the same thing twice.
- **T-Kay's printed `Force Field 12d (Zone)` came back `PRO_NOT_APPLICABLE`.** Force Field is
  Self range and the Zone Pro applies to Ranged and Touch Powers, so the validator refused it
  and neither editor would offer it — a Hero in the rulebook could not be built here at all.
  The Power's own entry overrides the option in as many words: "Apply the **Zone** Pro to
  shield large areas, the **Ranged** Pro to shield things at a distance, or the **Area** Pro to
  shield large areas at a distance" (p.29). That is the same shape as Deflection covering both
  attack types, which the book also states as prose rather than as a marked PRO.

**`pros_allowed_by_own_text` is not the `available_pros` list coming back, and the distinction
is the whole reason it is safe.** That list was this project's guess at which options suited a
Power and it filtered *absolutely* — 68 Powers offered no generic Pro at all. This one records
a sentence the rulebook prints inside a Power's entry, needs one behind every id, and can only
ever widen. There is a test that no other Power claims it and that the three Pros reach no
other Self-range Power. A sweep of Ch.2 for prose naming a generic Pro found exactly one other
case — Illusions and the Zone Pro — and it is deliberately left alone: no printed character
exercises it, and the rulebook prices Zone by the base Power's range and gives no figure for a
Zone-range one. Force Field has the same gap and the Ranged price is charged, which is recorded
in its entry rather than smoothed over; neither reading closes T-Kay, who is 124 at +2 and 126
at +4.

Both fixes were demonstrated by mutation: removing the `repeatable` flags fails on Blastwave,
disabling the own-text exemption fails on T-Kay. The first attempt at both mutations **silently
did not apply** — `perl -pi` edited nothing and the suite stayed green, which looks exactly
like a fix that holds. `git diff --numstat` is what caught it, which is the check the handover
already insisted on for the reason it gives.

**Three adversarial reviews, by agents told nothing about the work, and none of them could make
the change certify an illegal character.** All three verified every rulebook quotation and page
citation in it, and one re-ran the Ch.2 sweep independently and got the same two hits. What they
found instead was that the containment was looser than this entry's own first draft claimed:

- **Two of the new paths were dead, proven by mutation rather than argued.** Stubbing the
  *generic* half of the repeatable lookup to `return false` left the whole suite green — the
  only repeat test used Also X, which resolves through the Power-specific branch, so Affect
  Inanimate was covered by nothing. And replacing the data lookup with
  `power.Id == "force_field" && …` hard-coded also left it green, so the JSON field was
  behaviourally dead as far as the tests could tell and a second entry added later would have
  done nothing while they said it was fine. A third mutation loosened the Power-specific branch
  to "anything repeats" and stacked three of Flight's Levitation Con into a character 2 HP
  cheaper with an empty error list — the "three Burnouts cancelled a 12d Ability" hole again,
  one lookup over.
- **The grade keys were the real defect, and a comment is not an enforcement.** Zone/Nova and
  Ranged are priced by the base Power's Range; Force Field is Self, which the rulebook does not
  price. `zone_ranged` (+2) and `zone_touch` (+4) were both accepted with no finding, so the
  same printed character costed two ways depending on which key was typed, and the only reason
  T-Kay came out at 124 was that the test fixture happened to pick one. The decision now lives
  in the data as a per-allowance grade list, intersected with what the option actually prices,
  and the validator and both editors read it through one seam.
- **The widening overrode more than it claimed.** It returned early above the rank-type check as
  well as the Range check, and because the applicability method takes the shared interface, an
  id in a field named for Pros would have exempted a Con sharing it. Neither was reachable with
  today's data; neither was prevented. Both are now, with tests that drive the mechanism against
  a synthetic Power rather than observing the shipped data.
- **The citation moved out of a free-text `notes` string** — which no code read and no test
  asserted — into a modelled field a test requires to be present, alongside a check that every
  id claimed resolves to a real Pro and every grade named is one that Pro prices. The standard
  for adding an entry was documentation, and documentation is what drifted last time.
- **The MCP server was serving a document that contradicted itself**: Force Field is
  `"range": "self"` and its Ranged Pro row said `applies_to_ranges: ["touch","zone"]`, with
  nothing to distinguish that from a bug. Rows now carry `allowed_by_this_power_text` with the
  printed sentence, the narrowed grades, and `repeatable` beside `needs_variant` — which was
  missing, so a model had no machine-readable signal that Also X may be listed five times.
- **And the sheet printed `Also X, Also X, Also X, Also X, Also X`.** Legal, and useless. One
  shared formatter now collapses a repeat to `Also X ×5` for the text export and both browser
  surfaces, so it cannot read one way on the sheet and another on the tab.

The reviews also found eleven things wrong with the written record, including two places where
this file contradicted itself within five lines about the 1 HP bound, a claim in `SKILL.md` that
would now teach a model to drop a legal Pro, and a `README.md` bullet still asserting the
unqualified rule. All corrected here.

**A fourth review was pointed at the fixes rather than the code, and three of the eight did not
hold while two held halfway.** That is the same proportion this file records from each of the
last three slices, and the same shape every time: a fix correct on inspection and pinned by
nothing.

- **The MCP fields were an untested claim.** Setting `repeatable` false in both serialisers and
  the printed sentence to null left the whole suite green.
- **The repeat collapse was too.** `PowerFormatter.ModifierLine` had no test, and reverting all
  three call sites to a plain join was invisible. It now has both — unit tests for the function
  and wiring tests for the text export and both browser surfaces, because a formatter test
  exercises the function and not the wiring, which is exactly the distinction that let this
  through.
- **"One shared formatter" was untrue of the terminal**, where two surfaces still joined raw and
  one dropped the variant key, so two grades of Charges read as one option listed twice.
- **The Range-alone claim was pinned by nothing**: the test's rank-type half used Degrades,
  which is a Con, so the Pros-only guard refused it before ordering could matter — two
  assertions that were really one. The rulebook has no Pro carrying a rank-type constraint, so
  the test now builds one.
- **`GradesFor` was covered and none of its three consumers was.** The published Heroes exercise
  the accept path only, since T-Kay is recorded with the grade that is allowed — so the refusal,
  which is the entire point of the narrowing, was never run. The wizard is still the known CLI
  gap; what changed there is that its option label no longer quotes a grade the prompt will not
  offer.

### The other half: four recorded conversations, replayed with the engine run for real

The MCP server serves people who code and can bring their own Claude. A visitor to the site
cannot bring one — that was settled before this slice started and was not re-investigated: a
claude.ai subscription cannot be lent to a third-party site, the API is separate billing with
no dependable free tier, and custom connectors are gated to paid plans. The two options were a
proxy the owner funds, which costs money, invites abuse and breaks the static-site property the
README advertises, or a replay. This is the replay.

**The half worth showing off is the engine deciding, and that half is live.** Four conversations
in `data/transcripts/` are played back at the visitor's pace at `/replay`; every Hero Point
figure, every derived stat and every finding beside them is computed in their browser, from the
character the transcript carries, as they reveal it. At the end the character is handed to the
existing editors so they can change a rank and watch the numbers move.

**No transcript holds a number, and that is the whole design rather than a discipline.** A turn
carries a *character* — the inputs — and never an answer about one. Several turns of one
conversation may carry one, which is what lets the recording about a draft that did not fit show
the draft not fitting rather than merely say so. `TranscriptTests` holds the prose to it: no
recorded line may quote a Hero Point figure, an Edge, a Health or a Resolve. Ranks are
deliberately allowed, because a rank is an input the transcript already carries.

**The transcripts were produced by driving the real server, not written as dialogue**, and the
searching is what shaped two of them. "Punches through time" returns Time Travel, Time Stop,
Precognition and Blink among twenty-one matches, which is the ambiguity rather than the answer,
and is why the policy asks whether it is one Power or several.

**And the cheap one records a mistake rather than a success, because that is what happened.**
Asked for a character who knows when she is being lied to, the search — "knows when someone is
lying, reads intentions" — came back with a mind-shield, an out-of-body Power and a radar sense,
all matched on the word "knows". The conclusion drawn was that the rulebook has no Power for it,
and the transcript said so, and **it was wrong**: `super_senses_lie_detection` does exactly that,
at Perception, for a flat price. What was skipped is the part of the answer that says how many
matched and that the list was cut — the guide's own instruction is to search a more distinctive
word before concluding the rulebook has nothing, and `found: 0` is the only case that means it.
Asking again in the describer's words rather than the model's returns two rows with it first.
That is now what the recording shows, which makes it the more useful of the four: this is the
failure the search's caution fields exist to prevent, made by the person who wrote them.

**The four cover what makes the design visible**: one at Street level where the interesting part
is a search that nearly buried the answer; one whose first draft is over a Standard budget and
has to give something up, with the trade offered and taken; one where four words could be one
Power or three; and a Villain, who has no Hero Point budget at all under Ch.9 — so the replay
shows that finding and explains it rather than hiding it, which is what the GM review step does.
Every character was cross-checked three ways: the MCP server, `dotnet run -- build --from`, and
the rendered page asserted figure by figure against `CostCalculator` in bUnit.

Smaller decisions worth keeping:

- **There is one sheet component and it gained a parameter rather than a twin.** `SheetView` and
  `DerivedStatBlocks` now take an optional character instead of always reading the one being
  built. That was not tidiness: the four big figures come from `DerivedStatBlocks`, so before it
  was given the recorded character to read, a replay printed the *visitor's* Edge, Health and
  Resolve under a recorded character's name. There is a bUnit test that loads a sample first, so
  there is a different character present to be printed by mistake.
- **What is handed off is a copy**, round-tripped through the character's own JSON shape. The
  library is read once at startup and shared by every visit, so handing the instance over would
  let the first edit rewrite the recording — after which the replay plays back a character
  somebody changed, and nothing on the page would say so.
- **The transcripts are read strictly**, the way a submitted file is. They are data that nothing
  recompiles, so the way they rot is a rename: read leniently, a transcript whose `AbilityRanks`
  had been renamed would replay a cheaper character with its abilities silently gone.
- **A failed transcript fetch does not stop the app.** The rules are a broken deployment if they
  are missing; the recordings are a demonstration, and refusing to let somebody build a character
  because a demo file did not arrive is the wrong trade in every direction. The reason travels
  with the empty library and the page prints it, so it is not a silent nothing.
- **The label is the first thing under the heading**, not a note at the bottom, and it says both
  halves: the words are a recording, the figures are not. A notice that only appears at the end
  has been read after it was needed, so the test asserts its position in the rendered text and
  not merely its presence.
- **The shell's budget bar does not render on a replay route.** It is the visitor's own
  character, in the same six-label format as the recorded one directly below it, and nothing on
  the page said whose was whose — worst on the Villain, whose own panel deliberately shows no
  budget, leaving the only budget on screen belonging to somebody else.
- **A failed load is not a bad link.** Both reach the same branch, and the page answered both
  with "that address does not name one of the recorded conversations" — so a deploy that missed
  the transcripts would tell everyone following a good shared link that they had typed it wrong.

**Three adversarial reviews, by agents told nothing about the work, and the worst thing in it was
in the demonstration rather than the code.** None could make a figure on the page disagree with
the calculator, or make the hand-off mutate the recording. What they found:

- **The lie-detection error above**, which is the one that mattered: a recorded line asserting
  something about the rulebook that the rulebook contradicts, on a page whose whole claim is that
  these conversations really happened.
- **A trade the recording offered that would not have worked.** "Take the storm down two ranks
  and she keeps the foresight" saves 6 against an overspend of 12 — the prose invited the visitor
  to make a change and watch, and the change would have left the character still over. Four is
  the true figure and lands it exactly. The guard tests covered the two *stored* drafts and had
  nothing to say about a trade described only in words.
- **Six of seven mutations survived the new tests.** Swapping the two speaker labels credited
  every line in every recording to the wrong side and nothing went red; putting Health and
  Resolve back on the visitor's own character passed under a comment naming all three; a figure
  written into a `Title` or a `Blurb` was unguarded because the honesty scan read `Text` only;
  numbers written as words walked past it; and the budget allowance was blanket, so the cheap
  character pushed to 146 against a 75 budget still passed every test here while the page
  rendered "Over by 71" beside a line saying she comes in under it. All closed, each with the
  mutation named in the test that now catches it.
- **Two unguarded engine calls on the sheet** — `PerkCost` and the gear line — which the replay
  did not introduce but did widen: an unknown id throws during render, and a throw during render
  in the browser takes down the app rather than one box. That reaches a restored character as
  much as a recorded one.
- **A whole-tree Qodana scan run in place reports 1471 findings for a commit that reports 0 from
  a clean export**, `.CSharpErrors` included, on files that build clean. Export before scanning;
  the note is in `CLAUDE.md`.

**A fourth review was pointed at the fixes rather than at the code, and found three more — one
of them inside a fix.** That is the same proportion this file already records from the last two
slices, and the same lesson: a fix without a mutation behind it is a claim.

- **The Hero Point box on a replayed sheet could still print the visitor's own total.** The test
  meant to close this went from asserting one of three boxes to three of four, and the box it
  kept missing is the headline figure a GM checks a character against. It also searched the
  box's whole text, so `105` over a sub-line reading "of 75" satisfied a search for "75". It
  reads the value element now and compares it whole.
- **Powers on a replayed sheet could print the wrong effective rank**, for the same reason and
  with nothing looking. A rank is what a player rolls.
- **The route check was case-sensitive while Blazor's routing is not**, so `/Replay/…` served a
  recording with the budget bar over it.
- The honesty regex did not include the bare word "points", and the Perk and Gear guards added
  in the previous round had no test at all — removing them left the whole suite green.

**Twice in this slice, a mutation pass reverting with `git checkout -- .` took uncommitted work
with it** — both times work written minutes earlier, both times needing to be redone from the
transcript of what had been changed. The repository already recorded this hazard from
[#30](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/30) in its single-file
form; it is written here in the whole-directory form because knowing about it was not enough.
**Commit before letting anything mutate files**, and verify a mutation applied — `git diff
--numstat` non-empty — before believing a green result, because a silently-failed edit and a
passing test look identical.

**And one limit is stated rather than closed: nothing checks whether a recorded sentence about
the rules is true.** The characters are held to the engine and figures are banned from the
prose, but a line claiming "the Trait Cap is a limit on Abilities alone" passes everything here.
The lie-detection error is what that looks like when it happens, and a person caught it. A green
suite says the characters are legal and no figure was quoted; read a changed transcript against
the rulebook before merging it.

### Conversational creation, half of it: the MCP server, and the questions worth asking — [#39](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/39)

`mcp/` is a stdio MCP server wrapping the same engine, so somebody can connect their own Claude,
describe a character out loud, and get a legal costed one back. It handles no credentials and
holds no key — the conversation happens in the client they already pay for. The setup a stranger
needs is in `docs/MCP-SETUP.md`; the dependency arrows hold at compile time, because `mcp/` references
`engine/` and `sheets/` and cannot reference `cli/`.

**The transport was the easy half and the question policy is the deliverable.** A description
under-determines dozens of fields and almost all of them can be defaulted without anybody
caring. Four change the character materially:

| | |
|---|---|
| **Which tier** | It sets the budget and the Trait Cap, and everything else is measured against them. Never guessed |
| **One Power or several** | "Punches through time" is Strike plus Blink, or Omni-Power, or Alternate Form. **The question a model is most tempted to answer silently**, and the one that most changes the build |
| **What they are deliberately ordinary at** | Every character has all eighteen Traits and the points for a 10d come from somewhere. A characterisation question, not an arithmetic one |
| **Where it comes from** | One of six Sources. Costs nothing and changes no rank, so it is inferred and stated rather than asked unless genuinely open |

Everything else — rank spread, which package, which Flaw, gear, Perks — is decided and *shown*.
**Ask at most three questions**: a questionnaire is a worse interface than a wizard, and the
wizard already exists.

That reasoning lives in **`mcp/QUESTION-POLICY.md`**, which is embedded in the assembly and
served verbatim as the `creation_guide` tool, so the document the next person reads and the one
the assistant is taught are the same bytes. `McpQuestionPolicyTests` holds it to the standard
`SkillDocumentationTests` holds the skill to: its example character goes through the strict
reader and the validator, every id in it must exist, and the four questions are asserted by name.

**Six tools, chosen by what a conversation needs rather than by mirroring the engine.**
`cost_character` beside `validate_character` is the engine's API: no turn of a conversation
wants a price without knowing whether the thing priced is allowed, and a separate costing tool
is an invitation to quote a number for a character that breaks a rule. So `check_character`
does both and is the only place the word "legal" is decided. The ten catalogues are one
`list_options` for the mirror-image reason. Powers get `search_powers` and `power_detail`
because 141 entries are searched rather than listed.

**Nothing in it computes a Hero Point**, and the test for that is not a reading of the code:
every figure in the report is asserted equal to the calculator's own answer for the same sheet,
figure by figure rather than by total, over legal and illegal characters alike — because a
front end that always said "legal" would pass a suite run only over legal ones.

Four descriptions were run through the published binary, which is how two of the decisions
above were found rather than reasoned:

- **A cheap character** came back with `TRAIT_BELOW_PACKAGE` on an Intellect of 2d under a
  package that grants 3d — the loop working, on a first draft written by hand.
- **"Superman, but also a detective"** came back at 152 against a 125 budget, `remaining: -27`,
  with the spending breakdown naming where it went. That is the number to quote and the trade
  to offer, never a quietly weaker character presented as what was asked for.
- **"Punches through time"** returned Time Travel, Time Stop, Precognition and Blink — which is
  the ambiguity, not the answer, and is why the policy asks whether it is one Power or several.
- **"He plays the trumpet so beautifully people weep"**, the interesting one, returned four
  unrelated Powers matched on a word inside their descriptions. **A search that always returns
  its five best rows reads as five answers however carefully the caution is worded**, so
  `nothing_matched_by_name` and a note now say it outright. The same run found that substring
  matching answered "she bakes bread in the city" with **Plasticity**; matching is word by word
  with a shared-prefix rule now, because a match like that is worse than none — nothing in it
  looks wrong. (This entry said *Elasticity*, which is not a Power in this rulebook. It was the
  third of three copies of that mistake and the one the first correction missed.)

Smaller things worth keeping: standard output carries the protocol and nothing else, asserted by
reading the source for `Console.` followed by anything but `Error` (the obvious check for
`Console.WriteLine` passes while `Console.Out.Write` ships); the rules are found beside the
binary rather than by walking up for a `.sln`, because a client launches the published program
from a directory of its own choosing; and `RulesLocation.Find` returns null rather than a guess,
since a repository built for a directory that is not there fails on the first tool call instead
of at startup.

**Three adversarial reviews, by agents told nothing about the work, and the search flag above
was the worst thing in it.** None of them could make `check_character` certify a bad character
or quote a figure that was not the calculator's — that ordering held under every hostile shape
they threw at it. What they found instead:

- **The honesty flag lied, in the direction that matters.** `nothing_matched_by_name` came with
  "which usually means the rulebook has no Power for this" — so *"he can fly"* returned Flight
  and then told the assistant there is no Power for flight, because "fly" is not a prefix of
  "Flight" and the entry matched on the word inside its own description. The flag was right and
  the advice was wrong. The three cases are now told apart, and `found: 0` is the only one that
  means the rulebook has nothing. **Two tests straddled this and neither could see it**: one
  asserted `"flying"` finds Flight, the other asserted the flag's meaning over four curated
  queries, and both passed while contradicting each other.
- **It was also computed after the list was cut to `limit`.** With `limit: 1`, a name match at
  position two became "nothing matched by name at all". `limit` is the caller's and no test had
  ever passed one.
- **With no matches at all it still said "name the nearest"** — an invitation to name a Power
  that was never returned, which is the failure this tool exists to prevent.
- **The startup check passed itself.** It warmed one catalogue, so a directory holding nothing
  but `tiers.json` started cleanly and then threw out of five of the six tools — the exact
  failure its own comment claimed to prevent.
- **A mistyped `PROWLERS_RULES_DIR` fell through to the shipped copy**, silently. The setup guide's
  troubleshooting is what sends a stuck user to set that variable.
- **Five guard tests were theatre**, and the mutations were demonstrated rather than argued:
  `AnUnknownPowerIsReportedWithTheNearMisses` never read `did_you_mean`; the Power detail test
  checked own Pros and not own Cons, so serving one in place of the other was invisible across
  106 options; `AWarningDoesNotMakeACharacterIllegal` used the Hero, which has no warnings, and
  compared zero to zero; the guide test compared `QuestionPolicy.Text` with a method returning
  `QuestionPolicy.Text`; and the catalogues asserted only that ids resolved, so reporting every
  Pro as costing nothing passed.
- **`ENGINE_COULD_NOT_ANSWER` was documented as one of three verdicts and produced by no test.**
  It cannot be reached through any character — the validator refuses every sheet the engine
  cannot price — so `Judgement.Report` is now a seam that a test can drive directly, and the
  guarantee keeping the branch dark is asserted where it lives rather than claimed in a comment.
- **Prose:** three claims were wrong, including one of this entry's own ("over legal and
  illegal alike" was true of the verdict test and not the figures test — the figures test now
  covers both), and `CLAUDE.md`'s "nothing may make `TotalCost` negative" describes a floor that
  is not in the code. What actually holds is `NEGATIVE_UNITS` and `checked`.

**A fourth review was pointed at the fixes rather than the code, and two of the eight did not
hold** — which is the same finding this file already records from the last slice, in the same
proportion.

- **The test for the truncation fix did not bite.** Its query's top row matched by name, so
  cutting the list to one still left a name match in it and the buggy and fixed versions
  agreed. Reintroducing the bug left all 94 tests green. The query now ranks a
  description-only row first and every name match below the cut.
- **Nothing asserted that `Program.cs` calls `ReadEverything`.** The unit test covered the
  method; swapping the program back to warming one catalogue left the suite green while the
  binary started cleanly on a one-file rules directory. There is now a test that runs the
  built program.
- **The runtime standard-output test was not the backstop its own comment claimed.** It drove
  the binary through the SDK's client and asserted the session worked — and a real stray line,
  spelled to evade the source scan, left the client perfectly happy. The client skips what it
  cannot parse, which is exactly why the test now reads the stream itself and requires every
  line to be a JSON-RPC message. Verified by mutation, both ways.
- And one of the new tests **hung** rather than failed when its mutation was applied, because
  the failure it looks for is a server that keeps running. It bounds its own wait now.

**A fifth review, of the whole slice, and a whole-tree Qodana scan.** The scan reports zero
again; getting there found that three tool parameters guarded against a `null` while declaring
themselves non-null, so the guards read as dead code — and a client really can send
`{"category": null}`, checked against the built binary. Two of the scan's findings predate this
slice and made the "reports zero" claim untrue: a doc comment pointing at a test renamed in
[#33](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/33), and a redundant
`Cast`.

The review found nothing that certifies a bad character, and four things a stranger would meet:

- **A correctly named field holding the wrong kind of value was reported as a misspelling.**
  `"might": "8d"` is the rank written the way the rulebook writes it — the likeliest first
  mistake there is — and the answer sent a repair loop hunting for a spelling error that did
  not exist. The two are told apart now by reading the same text leniently: lenient reading
  ignores unknown field names and nothing else, so if it succeeds the name was the problem.
- **A blank `PROWLERS_RULES_DIR` still fell through to the shipped copy in silence.** The
  refusal had landed on the argument and not on the variable, which is the one the setup guide tells
  a stuck user to set and the one a client's config writes as `""`.
- **`SKILL.md` said "a minimal legal character is a tier and one flaw"**, which the 1d Trait
  floor made false in
  [#34](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/34): it comes back
  with eighteen errors. Two documents teaching the same JSON shape disagreed, and the wrong one
  was the older and more linked.
- **The tier lookup in `Judgement` was outside its guards** while the class summary said every
  engine call was guarded. Unreachable today only because the validator makes the same lookup
  first, which is an accident of ordering.

**And one finding was left open on purpose, which is the interesting one.** `search_powers`
ranks "walks through walls" by putting twenty-one Powers on two points each — every one of them
matching only the filler word "through", Phasing among them — so which eight a caller sees is
alphabetical, under a caution calling them the closest entries. Weighting each word by how much
of the rulebook uses it was implemented and **reverted**: it fixed that query and broke "reads
minds", which dropped Telepathy out of the first three because four Powers carry "mind" in their
names. A half-tuned scorer is worse than a dull one, and tuning it properly needs its own
evidence rather than two examples. What shipped instead is the truth about each row —
`matched_terms` says which of the caller's words it matched, `more_beyond_these` says the list
was cut, and the caution says rows matching the same words are in no meaningful order. **The
ranking is a known limitation, recorded rather than papered over.**

**What this deliberately did not do** is the browser replay demo — the other half of the
handover's slice, and a slice of its own. A visitor with no Claude account has no way to bring
their own inference, and the recommendation there stands: replay real transcripts with the
engine running for real in WebAssembly, and label the replay as a replay.

### Nine ways an illegal character was reported legal, and the one thing they had in common — [#31](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/31), [#32](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/32), [#33](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/33), [#34](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/34)

The validator had 26 checks and looked thorough. It was thorough about **the shapes a menu can produce**. Both editors pick from lists and count upwards, so a whole class of invalid character was unreachable — and the checks had been written, implicitly, against what the callers could build. The headless command let a *file* in, and the class became reachable all at once:

| | The character that came back legal at exit 0 |
|---|---|
| An unknown tier id | 99d Ability, **zero findings** — the budget and the Trait Cap both hang off the tier, so one typo switched off both limits |
| The same Con twice | Six Abilities at the cap for **0 HP**, empty issue list; scaled up, 216 HP inside a 125 budget |
| A negative quantity on a Perk | *Paid* the character 1000 Hero Points |
| A quantity large enough to wrap | A total of −2,094,967,284 HP passed the budget check |
| Overkill on any Ability | 12d Intellect for 6 HP — the Brute Option is Might, and the id was never checked |
| No 1d Trait floor | Up to 18 HP undercharged, and eighteen Traits at a rank nobody can hold |
| A Trait below its package floor | Free, and therefore silent |
| An unknown starting package | Silently no package at all |
| Unenforced applicability | The Ranged Pro on a Self-range Power |

**Three things explain all of it, and they are worth remembering because they will recur.**

- **Free mistakes are silent mistakes.** A Con past a floor, a Trait under its package rank, a modifier on an unbought Ability: none moves a total, so no test can see one. The mistakes that *did* move a total were all found years ago.
- **Floors launder invalid input into plausible output.** A Power floors at 1 HP per 2 ranks, gear at 0, an Ability at `Math.Max(0, …)`. Each is a real rule. Together they mean nonsense does not error — it rounds up into a believable number. Three Burnouts do not crash; they produce 0 HP and a clean report.
- **The strongest test in the suite is structurally blind to some of this.** Rebuilding the twenty published Heroes to exactly 125 is the best end-to-end check here, and **all twenty take a package**, so every Trait of theirs sits at or above a floor. It cannot see the 1d minimum, the package floor, or anything that only bites a package-less character. A rule can be missing for years while it passes.

**What actually found them**: adversarial reviewers given hostile input, and reading the printed page line by line. Reasoning about the rules found none of them.

Also in this run: **Herald (Airmid) closed** — her sheet prints two Expertise Powers and one was never transcribed, worth exactly the 5 HP her wrongly-attributed package was absorbing, so 16 of 20 Heroes are now exact and the bound is 1 HP. **All twenty Hero page citations were ten pages out**, the error `CLAUDE.md` already warned about, still live because nothing read those numbers. And the rulebook itself turned out to be in `docs/` all along — `*.pdf` is gitignored, so it is absent from a worktree's `docs/`, and checking there reads as "there is no rulebook".

### Assisted character creation, and the three ways a character could be wrong and not be told — [#30](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/30)

This closes the assisted-creation item, which was numbered 5 when it was open — not the item
numbered 5 above, which is newer. `dotnet run -- build --from character.json` costs and validates a
character, writes both exports and exits 0, 1 or 2 — legal, illegal, unreadable — with one
JSON report on standard output for all three. A skill at
`.claude/skills/prowlers-and-paragons-character/` teaches the schema and the
propose/validate/repair loop.

**The ordering is the whole value and nothing in the new code computes a Hero Point.** A model
proposes; `CostCalculator` and `CharacterValidator` decide. Inverted, this would be a random
number generator with good prose — and it is only safe in this direction because the engine is
now trustworthy enough to be the judge.

**It reports and never repairs.** An over-budget character comes back with every issue and the
caller gives something up. Auto-clamping was rejected outright: a player whose concept did not
fit should find that out.

**A command was the right surface rather than an API or an MCP server**, and the reason showed
up immediately: it is testable, and the wizard is not. `HeadlessBuildTests` drives the command
through its writers rather than shelling out, so a failure names a line. That narrows the CLI
gap rather than closing it — the wizard's own steps still have no harness, and the exporter was
already exercised by `SourceTests`, so this is not the first CLI coverage in the repository.

**Three real bugs came out of pointing it at characters a wizard could never produce.** All
three are the same shape — an id nobody checked, and a character reported as legal when it was
not — and all three were invisible to a front end that picks from a list:

- **A misspelled tier turned off both limits.** The Hero Point budget and the Trait Cap both
  hang off the tier, and both were skipped when the id could not be found. A 99d Ability on
  tier `"stanadrd"` came back with no findings at all: the worst answer a validator has, which
  is a confident wrong one. Now `UNKNOWN_TIER`.
- **An unknown starting package was silently no package**, so the character paid full rate for
  ranks the package would have covered. That is the exact shape of the bug that put seven
  published Heroes 4 HP over once already.
- **Ranks bought against a Trait that does not exist were charged and then ignored** — counted
  in the total, checked against the cap, printed nowhere. Found by the skill's own example,
  which named a Talent the rulebook does not have and produced a clean report.

**A `ValidationIssue` now carries the facts as well as the sentence** — subject kind, subject
id, the owning item for a gear feature, value, limit, and the options a choice must come from.
A sentence is enough for a person and not for a repair loop, which would otherwise have to
parse "Intellect is 40d, above the Trait Cap of 12d" back into the three facts it was built
from. Every message is byte-identical; all six properties are optional.

**The tests mostly use the structure rather than asserting it is populated**, because a
non-null check passes with the wrong id in the field, the value and the limit the wrong way
round, or an option the data will not accept. So they read an issue, apply the repair it
implies, re-validate and assert the finding is gone — and there is an invariant for each of
those three failure modes.

**Those invariants were worth only what their case list reached, and the case list was the
thing nobody was holding to anything.** They run over a hand-written set of sheets, and two of
the codes added in the same change were not in it — while one of the invariants would have
failed if they had been, because its list of acceptable options had no abilities or talents in
it. Three tests therefore ran green over a surface that excluded the work they were written
for. The list is now checked against the validator's own source: every code it can construct
has to be provoked by some sheet, with three exempt by name and reason. That check is the
reason the count of cases went from 13 to 20.

Three smaller things, each a trap rather than a decision:

- **The input is the `CharacterSheet` shape, not the JSON export.** The export is a report —
  costs, derived stats, findings, all of them answers — and reading it back would mean
  rebuilding a character out of its own conclusions.
- **A field name that is not part of a character is now refused rather than ignored.** A
  misspelled `AbilityRanks` silently drops every ability, and what arrives is a cheaper, legal
  character nobody notices is wrong. Strict only on the submit path: the browser restoring its
  own storage wants the opposite, since a field removed in a later build should cost it a
  field rather than the character.
- **The deserialization moved into `engine/CharacterSheetJson`** so the browser's local storage
  and the command cannot drift. Its subtleties — `Populate` for the get-only collections, and
  the nulls the deserializer puts where the type system says it cannot — were found by an app
  that would not start, and are worth exactly one copy. Deleting the `Populate` line alone
  fails 26 tests across both projects, which is what that guard is for.

**Four adversarial reviews, by agents told nothing about the work, and they found more than
the slice itself did.** Ten reproduced ways to crash the command, ten shapes of character that
made the validator throw rather than report, fifteen surviving mutations, and thirteen false or
misleading claims in the prose. What is worth carrying forward:

- **A negative `Units` on a per-unit Perk was worth unlimited Hero Points.** `PerkCost`
  multiplies a price by a quantity and the perk total had no floor, so
  `{"PerkId":"contacts","Units":-1000}` paid the character 1000 HP and a sheet with every Trait
  at the cap came back `"ok": true`, exit 0, no issues. **This is the failure the whole slice
  exists to prevent**, reached through the one field nothing bounded — and it was found by
  someone attacking the command, not by anyone reasoning about the rules.
- **`Validate` was the one unguarded engine call, under a comment claiming every call was
  guarded.** Ten shapes of ordinary hand-written JSON came out as a stack trace with nothing on
  standard output and an exit code outside the three: unknown Pro, Con, Perk and nominated-Trait
  ids, variant and grade keys that were present but wrong, and nulls where an id belongs. Each
  is now reported by name with its choices attached, and `CheckHpBudget` carries a `catch` as
  well — the specific checks are what a repair loop acts on, the `catch` only promises the
  validator answers at all. **The new `UNKNOWN_TIER` check had made several of these newly
  reachable**, by fixing the tier so that the budget check ran.
- **Two places priced a character on the strength of the wrong flag**, so a warning about being
  at the cost floor could take the whole validation down with it.
- **The exports overwrote each other.** The file name is the character's plus a timestamp to
  the second, and two runs in one second left one pair of files with both runs reporting them —
  the first caller told its sheet was at a path holding somebody else's character. A repair
  loop runs many times faster than that.
- **A test whose name was the thing it did not check.** `TheExportsAreWrittenWhereTheCallerAsked`
  asserted that the *reported* path existed, which is true of wherever it wrote — so ignoring
  `--out` entirely passed it. Two more had vacuous repair loops: `foreach` over an option list
  with no assertion that the list had anything in it, so emptying it made the test pass by
  doing nothing.
- **`SkillDocumentationTests` validated the document against a second copy of the code.** It
  converted the subject-kind enum to its wire name itself instead of asking the command, so the
  two could disagree and both stay green — `gearfeature` on the wire, `gear_feature` in the
  document. It asks the command now.
- **Thirteen prose claims were wrong**, including two in this entry: "nine tests" for the
  `Populate` guard (26) and "the first coverage the CLI has ever had" (the exporter was already
  covered). Also a doc comment's "4 to 15 Hero Points" (it is 1 to 4), and a claim that 26
  construction sites were unchanged when every one had been edited. The reviewer checked each
  number rather than reading past it, which is the only way this file stays worth anything.

**A second round found more than the first, and the most valuable reviewer was the one asked
to audit the fixes rather than the code.** Four of the six it checked did not hold:

- **The quantity checks looked at five fields and there were six.** A Pro carries a quantity
  too, and a per-rank-per-unit Pro at −1000 drove a Power's rate to −498, which the rulebook
  floor caught at half a point per rank — so a 24 HP Power cost 6 in silence.
- **Making `TotalCost` checked was not enough**, because the wrap happened in the per-unit
  multiplications underneath it. Determination at 500,000,000 units cost **5 HP** and gave
  500,000,016 Resolve, at exit 0.
- **The validator still threw, for an eleventh shape.** The branch handling a Pro or Con printed
  inside a Power's own entry skipped the grade check and went straight past, so Drain carrying
  its own ungraded Only X produced the crash instead of the finding written for it.
- **An export could still leave half of itself behind.** The `.json` path is one character
  longer than the `.txt`, so at one name length the first write succeeded and the second did
  not — an orphan, under a base name that then looked taken, while the report said nothing had
  been written.

**And the worst thing found anywhere in the slice: `--from CON` hung for ever.**
`File.ReadAllText` on a Windows device name opens the console and blocks on a read with no end
— no output, no exit code, no end. `COM1` and `CONIN$` too; `NUL` and `PRN` happened to fail
politely, which is why the whole reserved set is refused by name rather than the three that
were caught. Worse than any crash, and the exact input the item that fixed `--from ""` had
asked about.

**The duplicate exploit is the one to remember.** Nothing rejected the same Con listed twice
and every cost floors at zero, so three Burnouts cancelled a 12d Ability exactly: six Abilities
at the Trait Cap for **0 Hero Points**, exit 0, and an issue list with nothing in it. Scaled up
it bought 216 HP of character inside a 125 HP budget; on Super Senses, where one floor covers
sixteen options, it bought the lot for 1 HP. The same shape appeared twice more — a flaw taken
twice paid Resolve twice for one drawback, and the Brute Option halved *any* Ability because
the id was never checked against Might.

**Two of the fixes were themselves dishonest, and the review said so.** A duplicate id in one
of the rules files throws the same kind of exception as a bad character, and the report blamed
the character — "a null where an id belongs, most likely" — for a fault in this program's own
data, which a repair loop would chase for ever. And a figure the engine could not supply with
no error beside it left a caller told to "fix the errors and try again" with nothing to fix.
Both now say whose fault it is.

**The process lesson, which cost real work.** A reviewer doing mutation testing restores each
file with `git checkout -- <file>`, and it reverted an uncommitted fix of mine in a file we
were both touching — the hazard this file already records for `git checkout -- .`, in its
single-file form. Commit before letting a mutation pass run, or give it its own worktree; the
re-run was given one.

**Also closed: the wizard's crash on a terminal it cannot read.** Recorded by the last health
check, in scope now because there is somewhere to send that caller. Spectre's `SelectionPrompt`
threw `NotSupportedException` out of the first step after correctly rendering the tier table;
it now says the terminal cannot be read and names the command that does not need one.

**What this deliberately did not do** is enforce the semantic Pro/Con constraints. Item 1b
named assisted creation as the one consumer that would justify about a thousand fresh
judgements against the book; the consumer exists now and does not need them, because a caveat
shown to whoever is proposing is what Ch.2 says the list is.

### Sources on Abilities and Talents, and the rank threshold that never existed

This closes what was item 2. `CharacterSheet` gains `AbilitySources` and `TalentSources`, `SourceGrouping` builds the `Abilities (…)` line a published sheet prints, and all four surfaces print it: the `.txt` sheet, the JSON export, the wizard's GM review, and the browser's `SheetView`. Both front ends can set it — a `TraitSourcePicker` on the Abilities and Talents tabs, and a Sources entry in the CLI's two rank menus — because a field no host can reach is the unused data this item was held open to avoid.

**The premise this item was written on was wrong, and finding that out was most of the work.** It cited "Ch.2 p.15" for *"the Sources for your Powers and Abilities with a rank of 7d or greater"*. Two things were wrong with that, and an adversarial review caught the second after the first had been fixed:

- **The sentence is on p.64**, in the Random Hero Generator, where it tells you which Traits to roll Sources for. Still Chapter 2 — an earlier pass "corrected" it to Ch.3, which was also wrong.
- **The Sources rule is on p.16, not p.15.** p.15 is Power Levels and Packages and has no Sources text at all. That error was inherited rather than introduced, and it was in `sources.json` and six other files; all are corrected now. The footers print each page number twice interleaved, which is what made it easy to get wrong — decode one, or use the table of contents.

What p.16 actually says is broader: *every* Ability, Talent and Power has a Source, and the Innate/Trained defaults hold *"at least when dealing with ordinary people. When dealing with supers and characters who aren't human, however, anything goes."* The clause "but these defaults aren't mandatory" appears **once in the book, in the p.64 sentence** — so quoting it as the Ch.2 rule, as this entry did until the review, was the same misattribution the entry was written to complain about.

**Read as a rank threshold it is contradicted by the sheets, in both directions.** Alabama Slammer marks 6d Perception and 6d Toughness; Citizen Soldier leaves 9d Willpower unmarked while marking his two 12s; Stronghold leaves 10d Intellect unmarked and marks 6d Agility. So the printed line is an **exception list** — the Traits whose Source is not the default — and there is no rule that derives it. It has to be stored, which is the whole argument for the field. `PrebuiltHeroTests.ThePrintedTraitSourcesAreNotARankThreshold` names both counterexamples so the derivation cannot be reinvented.

**Ten of the twenty sheets carry such a line and ten carry none**, and both halves are transcribed — a renderer inventing a line for every character would satisfy a test that only checked the ten that do. Three of the ten are printed `Abilities and Talents (All)`: both Heralds and Nano, where every Trait deviates, so the engine collapses a full set to that one line and a single Talent short of it does not collapse.

Three smaller decisions, each from the printed layout rather than from convenience:

- **A Trait on its default prints nothing, and setting one explicitly to its own default prints nothing either.** They are the same Source but not the same statement, so the picker removes the entry rather than storing it. Otherwise an ordinary character prints eighteen lines restating the rulebook at the reader.
- **Abilities on one Source are split by the Pros and Cons they carry.** The marking on a printed line covers the whole line — Stronghold's four Abilities share one `(Item: armor)` — so two Abilities with different Cons are two lines, never one line carrying a Con that applies to half of it. The engine prints `(Item)`: `SelectedProCon` has an id and a variant key and nowhere to keep "armor". That shortfall is recorded beside the transcription rather than tuned away.
- **A Source group can hold no Powers at all** — a Trait bought through powered armour on a character with no Tech Power — so the grouping is no longer gated on there being Powers, and three call sites that were gated on `SelectedPowers.Count` are not any more. The Powers *tab* still skips those groups, because it edits Powers and a heading with nothing under it says less than no heading.

**The persistence test had a blind spot this would have fallen into.** The round trip is deliberately checked against the engine's answers rather than a field list — but a Source costs nothing and changes no rank, so cost, Edge, Health, Resolve and every validation message are blind to it, and dropping `AbilitySources` from storage would have passed all five. The fix keeps the principle: it compares another *answer* — the Source headings and the trait lines under them — rather than adding two field names to a list that will go stale the same way.

A Trait with no Source is **not** reported by the validator, and that is the rule rather than a missing check: the rulebook supplies a default, so silence means "on its default". A Power has no default, which is why `POWER_WITHOUT_SOURCE` exists and no Trait equivalent does. An unknown Source id, or one recorded against a Trait that does not exist, is an error on both.

**Three rounds of adversarial review, and the third found more than the second.** What the reviews caught, beyond the citations above:

- **`(All)` was counted on the Source rather than on the line it appears on.** With a Con splitting the Abilities across two lines, the unmodified line read `Abilities (All)` while naming four of six.
- **A Trait recorded explicitly on its own default printed a line.** The editors strip such an entry, so it could only arrive from stored or hand-edited data — which is exactly the path that reaches the renderers without passing an editor. The filter moved into `SourceGrouping`, so the rule is now true of the engine rather than of two call sites.
- **A blank Source threw `ArgumentNullException` out of the validator** instead of being reported. The storage guard caught it so the app never died, but that is the ordering trap already fixed twice here.
- **Both editors offered the default twice** — seven options for six Sources — and the two spellings did different things: the blank removed the entry, the named one stored it.
- **The `.txt` sheet and the browser sheet both decided "is there a Powers section?" by counting Powers**, so a character built entirely out of powered armour printed `(none)` and lost the only record of where the armour came from. Reverting either left every test green.

**A third round found more than the second, and most of it was in prose rather than code.** A reviewer re-derived the page mapping from scratch and audited all 417 citations in the repository: **55 instances were wrong, in ten distinct errors**, nearly all of them predating this slice. Power Levels is p.15 and was cited as p.17 — including in a validator message a player reads. The twelve custom gear features are on p.93, not p.92, in 22 places including a field label in the browser. The Brute Option and the global "Half" rule were attributed to Ch.1; they are Ch.2 p.17 and the Introduction's Glossary p.7. Cons run to p.54 and Flaws to p.60, not 53 and 59. Four "quotations" were paraphrases or had words elided without an ellipsis. All corrected, and `sources.json`'s page is now asserted by a test, because reverting all six back to p.15 had left the suite green.

Two more code defects from the same round: `EffectiveAbilitySource` read the dictionary directly while everything else went through the default filter, so a stored blank made one JSON document contradict itself; and a Trait naming an unknown Source printed nowhere while a Power in the same state printed under the plain heading — the same principle applied to one and not the other. The CLI's Source menu also handled one Trait per visit, reprinting the whole rank table between each, for the four-Ability case it exists to serve.

**Five tests were theatre and are now not.** One asserted the opposite of its own doc comment and passed by taking only the first line of the answer; one accepted any print font size because it checked for the unit and not the value; one claimed to guard against deriving the line from rank while reading no production code at all (a 7d threshold in the engine left it green); and the `Talents (…)` line's text was unasserted everywhere, so the word and the ids could both have been wrong. `TraitSourcePicker` had no test at all — storing the default, dropping the change notification, and `@if (false)` round the whole control were all green.

Two of those were mine and are worth naming, because both are traps rather than slips. **The picker's tests all rendered the component directly**, so deleting it from both editor tabs left the suite green — the feature could vanish from the UI unnoticed. And they drove it with `Change("tech")`, which supplies the event value directly and never reads the option list, so **swapping every option's value from the Source id to its name also stayed green**; a real user picking "Tech" would have stored `"Tech"`, which is not an id. Both are now driven through the tab and through the values the markup actually offers.

**The remaining known gap is the CLI**: `ChooseTraitSource` and the review step's trait row have no tests, because there is no CLI test harness at all. Building one is a slice of its own, and the flow was read closely by a reviewer instead — which is how the missing loop and an unguarded `First` were found.

A health check over the whole solution afterwards came back clean — zero warnings at CI strictness, 2845 tests, no vulnerable packages, the published site carrying all twelve rules files at full size, and engine, `.txt` and `.json` agreeing on every figure for three characters. It found one thing, which predates this work and is recorded here rather than fixed in a slice it does not belong to: **the wizard crashes with a raw stack trace when its terminal is not interactive** (piped or redirected input, or CI). Spectre's `SelectionPrompt` throws `NotSupportedException` and nothing catches it, so `ChooseTierStep` dumps a stack trace after correctly rendering the tier table. A capability check and a plain message would fix it — but that is CLI behaviour, and there is nothing to verify the fix with until the harness above exists. **Fixed in the assisted-creation slice above**, which is where the caller hitting it finally had somewhere to be sent.

### Qodana reports zero, and the fix was not a baseline — [#25](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/25)

This closes what was item 3, and the conclusion was the opposite of the plan. A whole-tree scan reported **242** problems. 46 were real and were fixed. The remaining ~200 were three structural facts restated, and they are now silenced by name and by path in **`.editorconfig`** with the reason beside each — not baselined, and not by a severity floor, because both hide a finding rather than answer it.

**`qodana.yaml`'s `exclude:` list was the trap.** It accepts an inspection name, looks like it works, and does nothing: the .NET linter is ReSharper, which takes severities from EditorConfig. A named exclusion there is silently ignored and the finding still reports — verified by running the scan both ways, which is the only way to tell. The upside of the real mechanism is that Rider and the ReSharper command-line tools now agree with CI, which a `qodana.yaml` entry would never have given.

What is silenced, in one line each: `engine/Models/*.cs` exists to be deserialized by reflection and must keep its setters (four inspections, ~150 findings); the test transcription records document a rulebook page rather than being read; a `[Theory]` body asserting on its parameter is not a precondition guard; `JsonValue.Create(...)!` is load-bearing and removing it fails the warnings-as-errors build; and this codebase writes explicit constructors and named backing fields on purpose.

Among the 46 that were fixed, two were worth having: `PowerFormatter` compared a `double?` cost rate with `==`, and `CharacterSession` and `FileSystemRulesSource` carried three genuinely dead public members. Note that Qodana in CI runs in **pull-request mode** and inspects only changed files, so its count there is not comparable to a full scan — the command to reproduce one is in `CLAUDE.md`.

### The character survives a refresh — [#28](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/28)

It did not. The sheet lived in a scoped `CharacterSession` and nowhere else, so a reload — or opening a link someone sent, which `_redirects` serves with a 200 *precisely so links can be shared* — silently dropped the character and landed on "Choose a tier first". `CharacterStore` keeps it in the browser's local storage, reads it back in `Program.cs` **before the first render** (restoring in a component's `OnAfterRender` shows an empty sheet first, which reads as "your character is gone"), and writes through on every change.

**What is stored is `CharacterSheet` itself, not the JSON export.** The export is a report — derived stats, costs, validation findings, all of them answers rather than inputs — and reading it back would mean re-deriving a character from its own conclusions. The sheet is the inputs.

Nothing in the path may throw: a character saved by an older build, hand-edited storage, or a browser refusing local storage all mean "no character", and the app starts empty. A tool that will not open because of something it wrote itself is worse than one that forgets.

One engine change, and only one: `SelectedPower` has two constructors, and a deserializer given a choice makes none — it throws. `[method: JsonConstructor]` names the primary. That is the whole of it, and it is what lets a host round-trip a sheet without the engine growing a parallel set of data-transfer types to keep in step.

**The round-trip is asserted by comparing the engine's own answers** — same total cost, same Edge/Health/Resolve, same validation messages — rather than a list of fields, because a field list is exactly the thing that goes stale when somebody adds a field and does not think about persistence. They will not think about the list either.

Asset caching was checked at the same time and needed nothing: `scripts/write-cloudflare-headers.sh` already marks the fingerprinted framework assets `immutable` for a year, holds `/`, `/index.html` and the rules JSON at `no-cache`, and everything else falls to Cloudflare's revalidate-always default.

**The adversarial review of this branch found that the first version of the guard did not hold**, and the way it failed is the general lesson. It stripped nulls exactly one level deep — the four top-level lists, and a Power's missing Pros and Cons. A null one level below that (`"Pros":[null]`, a null `PowerId`, a null inside `AbilityModifiers`, gear with null `Features`) restored cleanly, passed the backstop in `Program.cs`, and then took the app down on the **first frame**, because the budget bar renders on every route and costs the sheet to do it. A blank page, from the class written to prevent one.

So the guard is no longer a list of shapes: the engine is asked to cost and validate the sheet once, and a payload it cannot answer for is not handed to the app — and is removed from storage rather than left to be re-read and re-fail on every visit. `InvalidOperationException` is deliberately **not** caught, because that is what the engine throws for a half-finished character, which is exactly the work this exists to keep. A list of shapes goes stale the first time somebody adds a field, and they will not be thinking about persistence when they do.

Two more from the same review. `Program.cs` threw away a character that had restored perfectly if only the JS call that sets the palette failed — two catches now, because the two halves fail differently and only one of them means "there is no character". And the confirm gate was on the wrong button: **loading a sample overwrites the stored character just as completely**, in one click, while reading as the safe option. All three controls ask now, and only when the sheet holds something to lose.

### The sheet in colour, and a test project that renders it — [#28](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/28)

**`tests/ProwlersAndParagons.Web.Tests` renders components with bUnit**, and it exists because of "Armor8d": Razor swallowed the space in `@name` + `<text> @(rank)d</text>`, the sheet printed the name and rank run together, it was fixed on the sheet, and the identical bug in a second spelling survived on the Powers tab for another whole slice. Every test this repository had read source files, and no source file looks wrong. Anything about what a component *produces* goes here now; anything about how it is *written* stays in `WebPresentationTests`.

It is a separate project because it is the only one that may reference `web/`, and referencing a Blazor WebAssembly project drags the whole component model in — the engine suite has no use for that. bUnit pulls AngleSharp at a version carrying a published advisory, so this project pins AngleSharp forward rather than suppressing NU1902; suppressing an advisory to make a build green is how a vulnerable dependency ships.

**The first version of these tests was audited and five mutations passed it**, which is worth recording because four of the five were things the tests' own comments claimed to guard. Deleting `colspan="2"` from the gear row flushed every piece of equipment against the right margin — the exact bug the file says it exists to catch. Dropping a section's `Title` printed a box with no heading. Putting `None.` back into an empty Perks box restored the regression the sheet was rebuilt to remove. Emptying the `.hp` rule set costs in the same face as ranks. Printing costs at 4pt was invisible to everything.

**The cleverest one is the general lesson.** A test forbids the string `Communications 0d`. The reviewer printed exactly that, visibly, by splitting it across two `<span>`s — because the helper that strips tags out of markup leaves a separator where each tag was. That is fine for a *positive* `Contains("Armor 8d")`, where a newline is not a space and a split element cannot fake a separator; it defeats every `DoesNotContain`. So the negatives read element text now, and the positives assert against **the engine's own answer** for every Power on both surfaces in both modes, rather than three named Powers on one page.

Also from that audit: the run-together guard counted its two sources summed and had exactly zero margin (the Villain sample has four Powers, the sheet seven, against a threshold of eight — one more sample Power and half of it went dark); `PowersTab` said "no rank" for a *ranked* Power sitting at 0d and ran Pros and Cons into one unlabelled list, disagreeing with the sheet about the same Power on both counts; `SheetSection` carried two dead parameters whose doc comment argued for the "None." regression; and `rule-line` was hand-written three times outside `RuledLines`, once inside a `MarkupString` that hand-rolled its own `HtmlEncode`.

**The printed sheet keeps its mode's colours.** The old rule was "both modes print light", which came out of a Villain sheet printing its near-black surface edge to edge. That is the wrong lesson: the fault was a dark *surface*, not colour. So the rule is now white paper and readable ink, and each mode restates its own `--heading`, `--rule`, `--accent` and `--muted` — Hero in navy and gold, Villain in crimson and brass, both on white. Colour appears as ink and as a tint behind a 6mm heading bar; nothing fills an area. `--primary` still prints white, because it is a fill token and the banner used it.

Also, all four from a read of the first coloured proof:

- **An unbought Trait prints `0d`**, not a rule to write on. 0d is a fact about the character; the blank rules are for Alias, Team, Origin, Notes and Details, which the engine has no answer for at all.
- **Hero Point costs are set apart from ranks** — smaller, lighter, letter-spaced, muted. A rank is what you roll; a cost is bookkeeping, and in the same face the sheet read as a receipt.
- **A baseline Power names its Trait the way the rulebook prints it.** `PowerFormatter` swapped underscores for spaces and stopped, so a stat line read "Baseline Rank (½ toughness)" — while the method's own doc comment claimed "(½ Toughness)".
- **A rankless Power now prints the rank that stands in for it**: "Against other Powers: Toughness 8d". It has no rank of its own, but it is not rankless when something Drains it, and that number was nowhere on the sheet.

Four print faults came out of the adversarial read of the stylesheet, and three of them are the same mistake in different places — **a rule that looks like it applies and does not**:

- **The `fill` boxes were opted into `break-inside: avoid`.** They stretch to the height of the tallest column and the Powers column is unbounded, so they are the *other* thing on the page that can exceed a page — and Chrome honours that request by pushing the whole box to the next page, which is precisely what left two thirds of page one white when the Powers box did it. They opt out now, for the same reason `.powers` does. There is a test naming both.
- **The 4mm line gap never reached Notes and Origin**, the two boxes whose entire purpose is being written on. `.sheet-section.fill > .ruled` sets `gap: 0` for the screen at specificity (0,3,0); the print rule was plain `.ruled` at (0,1,0) and lost regardless of source order. On a sparse character those lines could print about 2mm apart, which the rule's own comment calls "decoration".
- **`td[colspan]` beat `td:last-child` on source order alone** — equal specificity — so the fix for flush-right gear would have silently come undone if anyone moved it up the file. It is `tr > td[colspan]` now, and settled on specificity.
- Perks, Gear and Flaws printed at the 10.5pt body size beside 8pt trait tables, because only `.trait-table` carried the print size. That extra height is what tips a borderline character onto a second page.

### The sheet is the published Hero Sheet — [#27](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/27)

The previous pass made the printed sheet *correct* — A4, margins, ruled boxes, no mid-entry breaks, black on white in both modes. It did not make it a **character sheet**. It was a stack of full-width boxes down a page and a half: legible, and obviously the output of a program rather than something you would put on a table.

It is now modelled on the publisher's own **Ultimate Edition Hero Sheet** (`docs/…Hero_Sheet.pdf`, untracked — `*.pdf` is gitignored, get your own copy). A masthead of three boxes, three columns, a foot of free-text boxes, every section ruled with its heading centred in a bar. **The structure only**: no hex pattern, no wordmark, no colour scheme — those are LakeSide Games'.

**The reference is a form, and two things follow.** Every Ability and all twelve Talents print whether bought or not, with a rule where the number goes; and Alias, Team, Origin, Notes and Details — none of which the engine has — print as labelled blank rules rather than being dropped. That also answers the "an empty character prints five boxes saying None." finding from the last round: a blank sheet is now a usable blank *form*.

**One page, and it fills the page.** The three columns are equal height and the box marked `fill` in each absorbs the difference, so a short character does not print a third of a page with white underneath. That is `flex: 1` plus `justify-content: space-between` on the rules, not a tuned line count — the first attempt did count lines, and it tipped onto a second page the moment a character had a long Motivation.

Also: Hero Points joins Edge, Health and Resolve as a fourth big box, as on the published sheet — `105` over a small `of 125`, because `105/125` at that size runs straight out of the box, and a Villain has no budget to compare against so it shows the spend alone.

**On the browser's print header** — the web address, date and page number across the top. No page can remove it: it is the print dialogue's "Headers and footers" setting and it belongs to the person printing. The review step now says which switch to turn off. Chrome supports neither `@page` margin boxes nor page counters, so there is no CSS lever at all.

### The sheet is fit to hand to a player — [#25](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/25)

Mostly presentation. The engine is touched in three places and each is noted below: the validator's user-facing messages, one ordering bug it exposed, and a dead property. Three faults, done in the order that made each one smaller.

**The markup went behind components first.** 22 hand-written `class="panel"`, 21 `panel-head`, 19 `field`, 9 `sheet-section`, 8 `chosen`, 6 `stat-block`, 5 `options`, none of them shared. That is what made the print work expensive rather than the print work itself — ruled boxes and break rules had to reach every one of them. Nine components now: `Panel`, `Field`, `SheetSection`, `StatBlock`, `DerivedStatBlocks`, `OptionList`/`OptionRow`, `ChosenList`/`ChosenRow`, following the `RankRow`/`StepButtons` pattern that was already here.

It found a real display bug on the way: **the sheet printed "Armor8d"**. Razor strips the leading whitespace inside a `<text>` block, so a Power's name and its rank ran together on every ranked entry — and an adversarial pass then found the *same bug* still live on the Powers tab, where the separator sat inside a `<span>` instead. Both are one expression now.

**The printed sheet is the substance of the slice.** The whole print stylesheet was three lines that hid the navigation, and every consequence of that followed: no paper size, no margins, entries cut in half by page boundaries, no boxes — the screen builds them from `box-shadow` and panel fills, none of which print — and the palette printed as-is, so a Villain sheet was a full-bleed near-black page.

- **The palette is forced light for both modes**, as a third block of token overrides in `theme.css`. No rule anywhere else needs to know it is printing. `--primary` is a fill, so on paper it becomes white and the banner takes its weight from a doubled rule instead of a wash of ink; the derived tokens are restated rather than left as colour-mixes, because a mix of black into white is grey and grey prints as a smear.
- **A4, 14mm margins, ruled boxes, break control** on `.power-entry`, `.stat-block`, table rows, list items and the section boxes. `break-inside: avoid` is a request, not a guarantee — a box too tall for any page is broken rather than clipped, which is exactly the fallback a long Powers group needs — so it is safe to ask for on every box, and it stops a four-line Gear box straddling a page for nothing.
- **A running footer was tried and does not work.** `position: fixed` is not repeated per page by Chrome's print output; it renders once, at the top of page two, over the content. What does carry the character's name across every page is the **document title**, which the browser prints in its own header, so the review page leads its title with the name. A colophon prints once at the end. This is a real limitation rather than a solved problem: with the browser's own headers switched off, pages 2..n−1 carry no identification at all, and CSS has no portable answer — Chrome supports neither `@page` margin boxes nor page counters.
- **The sheet gained the Trait Cap and, for a Villain, a point total.** The Trait Cap lived only in the budget bar, which does not print, and it is a number a player consults mid-session. The Villain sheet printed no total at all, because the HP figure was gated on the budget being shown — but "how much character is this" is exactly what a GM wants from an antagonist.

**The judging was done from the PDF, not the screen**, which is the only way this is checkable: the sheet markup was captured from the running app, rendered against the live stylesheets with headless Chrome's `--print-to-pdf`, and the pages rasterised and read back. A three-sheet document forced breaks through every kind of block. Computed styles cannot tell you whether a break lands mid-entry.

**The copy stopped talking to developers.** The GM review step no longer says its exports are "built by `CharacterSheetRenderer` in the shared sheets layer… byte-for-byte what `dotnet run` produces"; the derived-stats page no longer credits `DerivedStatsCalculator`; validation findings no longer print `NO_TIER_SELECTED` at the reader. All of it stays in the `@* *@` comments and `@code` blocks, where it belongs. The machine codes are still in the `.json` export, because they are genuinely useful in a bug report, and the page says so. Rulebook references were left alone on purpose — chapters, page numbers and rule names are what a player wants.

**The tests were adversarially audited, and the first version of them was theatre.** An agent with no context on the work applied *thirteen* violations to `web/` at once — white ink on white paper, every page-break rule flipped to `auto`, the whole working UI un-hidden, a second `@media print` block undoing the first, a `<style>` block carrying `rgb()`, `class="wrapper panel"`, `<code class="tech">`, and the "Armor8d" bug reinstated — and all thirty-two tests passed. It also produced four *false* failures on legitimate edits, one of which was an em-dash entity (`&#8212;`) read as a hex colour.

That is worth recording because the lesson generalises: **a substring check against a whole file is almost always satisfied by something other than the thing being tested.** The rewrite parses instead — the print block is split into rules so a selector and its declaration are checked *together*, tokens are checked by value rather than by presence, and prose is derived by stripping tags, attributes, Razor expressions, comments and the `@code` block so a type name in a paragraph can be told apart from one in an expression. Where a test could not be made honest it was replaced by a general rule: no compound PascalCase type from `engine/` or `sheets/` in visible prose, rather than a denylist of the four phrases that prompted it.

**The validator's messages turned out to be the largest remaining developer-facing surface**, and no test in `web/` could ever have seen them — they are engine strings, printed verbatim on the GM review step and in both exports. They named files (`flaws.json`), printed raw ids at a player holding a book (`Power 'super_senses_thermal_vision'`), used form-field plurals (`flaw(s)`), and one told every Iconic-tier character that its tier "is marked needs_review" — jargon, and **false**: nothing in `data/rules/` carries such a flag and the check fires on the tier id regardless. `ValidationMessageTests` provokes every message from real sheets and holds them all to the rule, which found one more thing on the way: **an unknown Power id crashed the validator** rather than being reported, because the Trait Cap check asked for an effective rank before the unknown-id check had run. The same ordering trap had already been fixed once for gear.

Also fixed, from the same audits: `aria-pressed`/`aria-selected` were rendered as `aria-pressed=""` — Blazor's spelling for a true bool, which is invalid ARIA that reads as *not* pressed, so both controls announced the opposite of their state; a half-built `role="tablist"` with no tabpanel, no `aria-controls` and no roving focus, now plain buttons with `aria-current`; two sibling Pro/Con pickers emitting the same DOM ids, so a label focused its neighbour's control; the review page never redrawing on a mode switch, leaving an over-budget finding on a Villain sheet; `--muted` failing AA at 3.8–4.5:1 where it carries almost all the explanatory prose, now 6.0–7.5:1; a Hero focus ring at 1.8:1 that nobody could see, now its own token; every generic Pro and Con offered as a bare name and a price with its description unused; `"How many Hero Point (= 25 Vehicle Points)s?"`; ids humanised into `super senses thermal vision`; a rankless Power offered as a Boost baseline it could never raise; `color-mix()` with no flat fallback, which would have dropped the banner to unreadable rather than unstyled; gear rows set flush right; and the page heading opening with a focus ring drawn round it on every navigation.

**The rendering-test gap is closed.** See the entry above — `tests/ProwlersAndParagons.Web.Tests` renders components with bUnit, and it exists because of exactly this.

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

**`--branch` is a label Cloudflare compares against the project's configured production branch, not a branch it reads.** New projects default to `main`; when this was found we deployed `master`. The mismatch does not
fail anything: the upload succeeds, wrangler prints a `master.<project>.pages.dev` alias, the workflow goes green — and the production URL and any custom domain answer 404, because no production deployment exists. Nothing in the logs says so.

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

`superheroes.softwaresamurai.net`, deployed by GitHub Actions on every push to `main` that touches the app, the engine, the rules or the deploy itself. Direct upload rather than Cloudflare's Git integration, so there is one deploy path rather than two that can disagree. Setup and the token scoping are in the README.

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

**Six Sources** (Ch.2 p.16): Innate, Magic, Psychic, Super, Tech, Trained. Each names the Ability that stands in as a rankless Power's rank whenever Powers act on other Powers — Drain, Nullify, Dispel, Power Absorption, Power Mimicry. The split is even but not intuitive: Innate, Super and Tech use Toughness; **Trained uses Willpower**, not Toughness.

`GetRankAgainstPowers` is deliberately separate from `GetEffectiveRank`, which still answers 0 for a rankless Power. The default rank stands in *only* against other Powers; it is not the Power's rank. Folding it into the effective rank would feed Edge and Resolve figures the published sheets contradict, and a test pins that distinction.

**It is a rendering change too, and that was the point.** The `.txt` sheet, the JSON export and the wizard's GM review all listed Powers flat; they now print Source headings the way a published sheet does. The JSON gains `source`, `source_heading` and `rank_against_powers` — that last one is otherwise invisible, and is where the rule shows: Tech-Source Communications exports `effective_rank: 0` alongside `rank_against_powers: 5`.

All twenty published sheets have their grouping transcribed and a test asserts the engine reproduces each one's printed headings — Psidearm carries three groups, Alabama Slammer two, Talon one. A Power with no Source still prints, under a plain heading at the end, rather than being dropped from its own sheet.

**A correction to what this file said before.** It recorded that Abilities are printed with no Source marking. That is true of the Abilities block, but incomplete: the sheets record an Ability's Source as an `Abilities (…)` entry inside a Power group — Stronghold's four armoured Abilities sit under `TECH POWERS`. That became its own item, and is now closed — see the entry above.

### Pro/Con applicability is derived, not guessed — [#14](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/14)

Every Power carried hand-written `available_pros` / `available_cons` lists, and `ProConSelector` filtered on them absolutely — an option not on the list could not be selected at all. Those lists were this project's invention, and they were badly wrong: **68 of the 141 Powers offered no generic Pro whatsoever**, six Self-range Powers offered the Ranged Pro (which raises a Touch Power to Distant Range, and has nothing to raise on a Power that affects only you), and Self-range Teleportation offered the Touch Con for the same reason.

The rulebook never states applicability per Power. It states it inside each generic option — *"This Pro applies to Zone Powers"*, *"applies to Powers that only affect you"*, *"applies to Power Rank Powers and Baseline Rank Powers"*. So the 141 lists are deleted and the answer is derived from the option instead, by `ProConApplicability`.

**Ten entries constrain on something the rulebook prints for every Power** — its Range (Ch.2 p.19) or its Rank type. Those are enforced, each transcribed in a test naming the sentence it comes from. Every Power now offers Pros and Cons, and the counts move with Range as they should: 16 Pros on a Self Power, 18 on Zone, 19 on Touch and Ranged, and all 23 on the four Special-range Powers, where the book says such Powers "work in some unique way discussed in their descriptions" and so rules nothing out.

**The rest are deliberately not enforced.** See item 1b: they would need about a thousand fresh per-Power judgements, which is the same mistake in a new shape. They travel as a caveat displayed beside the option, and a test asserts a caveat never acts as a silent filter.

### Toxin Pros/Cons, custom gear, and two more Heroes closed — [#13](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/13)

Four pieces of work, two of which found real cost bugs.

**The three toxin Pros/Cons (Ch.7, pp.108-109).** The original extraction was scoped to Chapter 2, so it missed Caustic (−2) and Non-Lethal Disease (+2) on Stun, and Lethal Disease (+6) on Slay. A sweep of the whole book for a PRO/CON Hero Point marker returns exactly these three outside Ch.2 and nothing else, so Pros and Cons are now complete. Note Non-Lethal Disease is Stun, not Slay, despite being printed under Lethal Disease.

**Custom gear features (Ch.6, p.93).** Twelve features at 1–2 HP each, ten flat and two graded, plus ordinary Pros and Cons applied to a piece of gear. Gear has its own floor: *"no piece of gear can cost less than 0 Hero Points"*, where a Power floors at 1. The Item Con is deliberately **not** credited — Ch.6 says every piece of gear has it as a statement of what gear *is*, and Item is absent from the list of Cons the same page calls common on gear; crediting it would make every 1 HP feature free. Free-text mundane gear stays the wizard's default, since nearly all gear is free. Gear is the first thing to spend HP outside `TotalCost`'s four existing categories.

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

