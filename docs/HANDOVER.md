# Handover

**`CLAUDE.md` is now an index of 286 lines, and ten guides under [`docs/guide/`](guide/) carry
the rest.** Read [`CLAUDE.md`](../CLAUDE.md) first and follow its routing table to the guide for
whatever you are about to touch, then [`PROGRESS.md`](../PROGRESS.md).

**This round printed the command palette's chord on the screen** — the one part of
`PROGRESS.md` item 12 the owner named as a defect about today rather than a design for later. The
round before it closed all three things the owner reported from using the deployed app, rebuilt the
character manager's layout to a pitched plan, and took a long product conversation that is written
down as `PROGRESS.md` items 11, 12 and 13.

**Item 12's remaining half is the corpus, not the surface.** The banner now carries a `Search`
button with `Ctrl`/`Cmd`+`K` beside it; putting the *rulebook* behind that control means growing
the palette onto a second body of text that sits behind an account gate, and `palette.js` says in
as many words to resist growing it. That argument is untouched.

**Read item 11 before choosing the next slice.** The owner spent a session reading a competitor and
brought back eight ideas; they are not eight decisions, they are one, and the entry says which. The
short version is that three things already in this repository answer it without anybody having
decided: the tier and Trait Cap are campaign facts stored per character, `UnlimitedBudget` is
documented as "a GM building to whatever a scene needs", and Adversity — a settled, GM-scoped
currency — appears in **no code at all**.

**The lesson of the round is one sentence, and it is `PROGRESS.md` item 10's argument.** A feature
was built, tested, adversarially reviewed by two independent agents and shipped, while nothing in
the application ever put a character into the store it read from. Every check passed, and passed
honestly, because every check called the store directly — and **a test that reaches the machinery by
hand cannot notice that nothing else reaches it.** Nothing in this repository asks whether a feature
is reachable by an ordinary person doing an ordinary thing.

**Read the adversarial-review entries in `PROGRESS.md` before touching character storage.** There
are two of them now and both are worth the ten minutes: the earlier one shipped three defects into
review, one of which silently destroyed a reader's draft on sign-out; this one found three more in
the fix above, including a check that was dead code and read exactly like a guard.

---

## Where things stand

**4,831 tests across four suites** — 4,015 engine, 636 bUnit, 166 accounts, 14 pixel comparator.
All four were re-run on this round's branch. Re-measure on the integration branch rather than
copying this line:

```bash
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
./scripts/test-visual.sh
```

**`dotnet test` prints one `Passed!` line per project, and there are two.** If you see one, a
project failed to **build** and its result is simply missing. Count the lines, and grep for
`Catastrophic` — a crashed process still prints `Passed! - Failed: 0`.

**A whole-tree Qodana scan reported 0** via `./scripts/qodana-scan.sh` (needs Docker Desktop) — on
each of this round's **five** branches and then **on `master` after the merges**, which is the
figure worth having, because separately-clean branches are not the same claim as a clean merge of
them. Do not repeat that zero without re-running it.

**And expect a new test file to put findings there.** Every branch this round scanned dirty the
first time, on five rules between them: `UseAwaitUsing` for an `IAsyncDisposable` context in an `async`
test, `MethodHasAsyncOverload` for `Click()` where `ClickAsync` exists, `RedundantUsingDirective`
for a `using` a global one already covers, `RedundantNameQualifier`, and `InvalidXmlDocComment` for
a `<para>` left unclosed. **Run the scan before the push rather than after the PR** — and run the CI
build, not just `dotnet test`, because the analyzers that catch `Assert.NotNull` on a value type are
errors only under `--configuration Release -p:ContinuousIntegrationBuild=true`.

---

## The split, and the one thing that would tell you it was wrong

`CLAUDE.md` went from 1,431 lines to 286. The rule that decided what moved:

> A rule stays in the index if breaking it costs work regardless of what you were doing. It moves
> to a guide if you can only break it while working on that area.

**Nothing was cut** — the partition was proved to tile the 1,446-line baseline exactly: 1,446 rows
covered counting duplicates, 1,446 distinct, zero gaps and zero overlaps, with a positive control
(dropping one 100-line range reports exactly 100 gaps). `RepositoryGuideTests` now holds the index
and the guide set to each other, and holds the index to a 400-line budget.

**Four fresh no-context agents were given trap tasks and all four routed correctly** — each quoted
the routing table as the thing that sent it to the right guide, and each then found the rule that
made its task wrong (the `title`-attribute ban, the Item Con, the MCP stdout discipline, the
four presentation rules). That is the measurement item 7 asked for, and it is better evidence than
the eleven-agent data point that motivated the item. **A control arm was run with the routing table
stripped out** — see the completed entry in `PROGRESS.md` for what it showed.

**What would tell you the split was wrong:** an agent breaking a rule that is now in a guide. If
that happens, suspect the routing table before you suspect the reader — and say so in the entry.

**Adding something? Put it in the guide for its area.** The way `CLAUDE.md` got to 1,431 lines was
one reasonable paragraph at a time, and the cost was not aesthetic: PR #79 shipped four features
and `CLAUDE.md` recorded one of them, while a bullet describing a confirm that same PR had removed
sat there being wrong through three more merges.

---

## Documentation rot found by doing this, which is the point

Three separate stale claims were found and fixed, none of which any test could have caught:

- `CLAUDE.md`'s undo section said discarding a non-current saved character "is still a confirm,
  deliberately". PR #79 removed that confirm and shipped `web/Services/DiscardedCharacter.cs`.
- `CLAUDE.md` was **silent on three of PR #79's four features** — `RulebookProse`/`BookText`, the
  explained sheet, and `DiscardedCharacter`.
- **This file** said `.shell` "also holds the sticky budget strip, so it needs proofing on every
  route." It does not — the strip is a sibling of `<main class="shell">`
  (`MainLayout.razor:157`), which is why the `.shell` gap fix needed no proofing against it at
  all. Two bullets in `docs/guide/browser.md` inherited the same staleness and described a
  negative-margin bleed that `app.css` records as removed along with its guard.

**Read a claim against the code before you act on it.** All three read as true.

---

## The owner's three reports are closed

All three came from using the deployed app and all three are done. **Two of them were one defect**,
and it is the more serious kind: a feature built, tested, adversarially reviewed and shipped while
nothing in the application ever put a character into the store it read from.

### 1 and 2 — the switcher had nothing to list

`CharacterManager.StartNew()` was `StartAgain()` plus `ClearAsync()`: it emptied the slot the
character was in rather than leaving it there and pointing somewhere else, which is the "blows away
my old one" the owner hit. Import overwrote whatever the pointer was aimed at. And because nothing
ever added a character to `SavedCharacters`'s index, the list, the switcher and both undo buffers —
all of which read that index, all of which had tests — could never have held more than one row. On
an account it was worse: `ApiCharacterStore.ClearAsync` is an HTTP `DELETE`, so "Start a new
character" removed the row from the server.

Both controls now go through `AccountCharacterStore.StartAnotherAsync`. **The order is the whole of
the correctness**: write the character down under its own id, move the pointer, and only then empty
the session — the reverse races the fire-and-forget autosave and puts the empty sheet over what was
being kept.

### 3 — the sheet explains itself

`SheetView.Explain` defaults to `true`, and `/build/sheet` and its link are retired. **The question
the handover said to decide — whether `Term` should do anything on paper — turned out to be already
answered in the print stylesheet**: `.tip-wrap` is hidden, a tip is shut unless hovered, `.term-name`
gives up its underline and cursor, and the description's other copy is `.sr-only`. The old default
was off specifically to protect the printed page, and the printed page never needed protecting. That
was true before the change and **nothing tested it** — one stylesheet edit from being false on every
sheet the tool produces.

### And then the manager was redesigned rather than adjusted

The owner asked for a plan first, which is in `PROGRESS.md`'s completed entry with the argument
intact. Three faults — actions stranded at the panel's right edge, two peer controls drawn unequal
("skinnier and adjacent but also floating"), and no way to tell which character you were in — and
three moves: the row becomes the button, the open character gets its own block carrying its spend,
and the two ways to make a character become one bar under a rule.

**One constraint decided the rest and is worth carrying forward:** `SavedCharacters` holds labels
and timestamps in its index and each payload under its own key, so a row can carry a **time** for
free and a **cost** never — except the open character, which the session is already holding. That
asymmetry is not a flourish; it falls straight out of where the data lives.

---

## What is left, in the order I would take it

0. **The product question, `PROGRESS.md` item 11 — and it outranks everything below it, because
   the answer changes what "left" even means.** *Is this a tool for a player building a character,
   or a table aid for a GM running a game?* Item 11 lays out the eight things waiting on it, in the
   order their dependencies force (campaign → headquarters → dice → log → analytics → combat → GM
   screen), and the evidence already in the codebase that the answer is "GM".

   **Item 12 needs none of that and could be done tomorrow** — three doors on the front door, the
   rules search into the banner on `Ctrl`+`K` (which *already opens the command palette*, so it is
   an extension of an existing surface and has to be argued with `js/palette.js`, which says in as
   many words to resist growing it), account and settings to the right, and the Hero/Villain switch
   into that settings menu.

   **The defect half of item 12 is closed** — the chord is printed in the banner, on every route,
   with the modifier chosen at render time from the platform. What is left of that bullet is the
   rulebook moving behind the same control, which needs the palette argued onto a second corpus.
   Plus two small ones, both untouched: the Hero Point limit as its own two-card group rather than
   a full-width panel for one button, and `/rules`' inert "What is here" list.

   **Item 13 is the owner's branding and the sign-in email**, which they rate below a competitor's.
   The kit exists outside this repository. Anything sent is outward-facing and costs the hourly
   allowance to test, so it is proofed with `scripts/probe-mail.mjs` and not against a real inbox.

1. **`PROGRESS.md` item 10 still needs the owner's decision, and this round sharpened its
   argument.** Stage one needs no permission; stage two — the development-only session seam — is
   the owner's call, with a zero-risk alternative written up beside it. Nothing was implemented.
   **Read the new subsection in that item before proposing anything**, because it corrects the
   item's own framing: the defect above was not an *assembly* fault, which is what item 10 argues
   about. Every unit test passed honestly because every one of them called the store directly, and a
   test that reaches the machinery by hand cannot notice that nothing else reaches it.

   **One row of item 10's table is now partly closed in-process.** `RenderContext(storesForReal:
   true)` swaps bUnit's recorder — which answers null to every interop read — for a storage that
   actually holds what is written. It closes `ppStore` and nothing else: no boot, no routing, no
   Functions, nothing behind sign-in, and `theme.js`/`palette.js`/`motion.js` still answered by a
   recorder.

2. **The codebase half of item 7**, untouched: dead code, engine hot paths, payload waste, the token
   side. `PROGRESS.md` is now over 5,600 lines and is read at the start of every slice by
   instruction. Nobody has costed it.
3. **The four published Heroes 1 HP out** — item 1. A confirmed negative from two independent
   instruments. What is left is an interaction, not a mispriced element. Do not tune an ambiguous
   variant to force a zero.
4. **`search_powers` measures 60 of 72.** `cloud_minds`, `buff`, `power_absorption`, `psi_screen`,
   `elemental_control` and `form_gaseous` are named candidates for the next pass.
5. **Durable telemetry**, deferred by the owner — item 9.
6. **Item 5, the 27 MiB payload.** The one open item where a mistake ships live and quiet: it cannot
   be verified on this machine, and its failure mode is a silently empty rules set at runtime.

---

## What this round learned, that the next one needs

- **An exemption in a test is a claim with a shelf life, and four of them had expired.**
  `UppercasedTextTests` names selectors it cannot reach on any rendered page, and four read
  *"MainLayout, which needs a Body fragment and a router"* — which was never true: `BannerTests`
  has rendered the layout on its own since the day it was written. A new uppercased class tripped
  the test's own refusal, which is the guard working, and the honest fix was to render the layout
  in the sweep and delete all four. **Check an exemption still holds before adding a sixth beside
  it.**
- **A screenshot at `--window-size=375` is not a 375px viewport.** Headless Chrome clamps the
  window to about 485px, so the render is cropped and reads as horizontal overflow that is not
  there — a pill sliced by the right edge on a page whose narrow harness reports `clientWidth 360,
  scrollWidth 360`. The harness measures a real 375px **iframe** for exactly this reason and says
  so in its own header. Screenshot `proof-narrow-shell.html` itself, not the shell at a small
  window.
- **`git remote -v` points at the old owner, and `gh` can fail in a way that looks like billing.**
  This repository is `SoftwareSamurai-net/ProwlersAndParagonsAutomation` now; the remote still
  says `DorianSheiles/…` and redirects. `gh workflow run --ref …` resolved from that remote came
  back *"the job was not started because recent account payments have failed or your spending
  limit needs to be increased"* — which reads exactly like the account's Actions being switched
  off, and is not: the same dispatch with `--repo SoftwareSamurai-net/…` ran in four minutes. Two
  runs on `master` that failed in seconds after the #90 merge have the same explanation. **Pass
  `--repo` explicitly, and do not conclude anything about billing from that message.**
- **A guard that has never had an answer to be wrong about is a different guard once it does.**
  Every call in `Shortcuts` was fire-and-forget, and its own remarks said "no caller reads a result
  back, so there is no answer to be wrong". The one that reads an answer had to break that
  sentence rather than inherit it: a swallowed failure answers `null`, not a default, because a
  default is a claim about the reader's keyboard made by a script that never ran.

- **A check can be dead code and read exactly like a guard.** `SavedCharacters.SaveAsync` returned
  the id it was passed whether or not the write landed, so both callers weighing it compared a
  string against itself. One of them was the undo behind a discarded row — the one method documented
  as answering whether the write landed, "because an undo that silently did nothing is the worst
  possible outcome". **Ask what the failure branch returns**, not only whether there is one.
- **A disciplined suite can have a hole shaped exactly like the thing it was written about.** Every
  account test in the new file calls `Settle` before clicking, deliberately, so the fire-and-forget
  write finishes first. That is right for determinism and it removes the exact race the check under
  test existed to close. The fix is to assert the *order of the requests* rather than to race a
  timer: `FakeApi.Asked` records each with its method, so "the `PUT` comes before the `GET`" is a
  plain assertion and does not test this machine's scheduler.
- **Breaking a guard and watching it fail is not enough if you break something the guard was never
  about.** The print-safety test was broken by removing `.tip-wrap` from the print block and it went
  red, which looked like proof. `.tip-wrap` belongs to `Tooltip`; a `Term`'s tip is `.row-tip`, kept
  off paper by its own base `display: none`, which the test never touched. A reviewer set that line
  to `display: block` and put every description on the printed page with the suite green.
  **Ask what the mechanism is before choosing what to break** — this is the null mutation
  `CLAUDE.md` records, wearing a disguise.
- **An adversarial review and an end-to-end harness are not substitutes.** Three defects were found
  by a reader told only to look for data loss; a harness would have caught at most one of them, and
  the race not at all reliably. A harness answers "is this reachable"; a hostile reader answers
  "what does this do when something goes wrong".
- **Trace a review finding before applying it.** A fourth finding — write the index before the
  payload — was demonstrated-sounding and wrong: `WriteIndexAsync` swallows its own failures, so it
  does not abort the pair and both orders end identically. The paragraph the reviewer was reading
  *had* gone stale, so the fix was to the comment. Applying the reorder would have been churn that
  no mutation could have justified.
- **`git checkout <branch>` carries uncommitted changes across with it.** Two branches were in play
  this round and a switch took one branch's edits onto the other, silently and with no warning —
  including a staged `git rm`. `git stash push -u` then switch then `git stash pop` moved them back
  with nothing lost, which is the rule at the top of `CLAUDE.md` earning its place a fourth time.
  **`git status` immediately after every switch.**
- **A heredoc is not an editing tool, in a third spelling.** `python - <<'PY'` with a long payload
  failed twice with `unexpected EOF while looking for matching` — the content never reached Python.
  Writing the script to a file with the editing tool and running `python <file>` worked every time
  and is also re-runnable when a match fails. The existing note about heredocs hanging on a machine
  with no Python is a different failure with the same lesson.
- **A fixture can be a state no running app can reach, and only a redesign will tell you.**
  `CharacterManagerTests.OpenRow` planted a current-character pointer at a saved character and never
  put that character into the session — but opening one does both, as the app's own boot does. It
  went unnoticed for as long as the open character was an ordinary row. **Ask what else is true
  whenever a fixture plants one half of a state.**
- **A stub that answers something the real server never would is worse than no stub — and
  `FakeApi` was one.** Its clock handed out 1, 2, 3: timestamps a millisecond after the epoch, where
  the real server writes `Date.now()`. Nothing asserted on the value, so it was invisible until a
  panel printed a time and every proof page read "over a year ago". That class's own remarks state
  the rule it was breaking.
- **When a mutation leaves the suite green, the answer is usually that every fixture happens to
  satisfy the rule.** "Show a time only with two characters" survived being set to always-on,
  because every fixture in that file holds two. Not a null mutation — an untested rule.
- **`dotnet test` on its own is not the CI build, and the difference is analyzers.** Two xUnit
  analyzer faults in new test code — `Assert.NotNull` on a value tuple, which is always true, and a
  `Where` before `Assert.Single` — passed a plain `dotnet test` and failed
  `--configuration Release -p:ContinuousIntegrationBuild=true`. `CLAUDE.md` already lists that
  command; run it before the push, not after CI says so.
- **When a mutation leaves the suite green, find out why before recording a hole.** Dropping the
  index-add from the autosave was green — not because the guard was fine, but because the *kept*
  character is indexed by the explicit save that keeps it, so nothing noticed that the character
  being **built** in the new slot was never listed at all. A real hole, one slot further along, and
  it is now closed.

---

## Still open from before, unchanged

- **Screen-reader testing is deferred by the owner, and this is a decision rather than a backlog
  item.** *"I don't care about accessibility / screen reader stuff. So defer until the application
  is finished. Which it is far from."* — 2026-08-27. **Do not spend a slice on it, and do not offer
  it as the next thing to do.** It stays recorded because the debt is real and the app will
  eventually be finished: it is owed on the command palette, the pips, the sign-in page, the
  light/dark control, the row descriptions, the rules search, the row findings and the undo
  announcement, and `aria-pressed` asserted as the string `"true"` is not the same as having been
  listened to.

  **The structural half is done and does not need revisiting**: `AriaReferenceTests` sweeps twelve
  surfaces and resolves every `aria-describedby`, `aria-labelledby` and `aria-controls` token, so a
  dangling IDREF cannot reach whoever eventually does the real work. Keep writing components to the
  rules in `docs/guide/browser.md` — the `title`-attribute ban, string-valued ARIA booleans,
  conditional `aria-controls` — because those are cheap at the time and expensive to retrofit. That
  is the whole of what is expected here for now.

  **That sweep carries a count as its positive control**, because every assertion in it is an
  absence and a sweep that rendered nothing satisfies all of them; it was broken both ways and
  watched to fail. **It cannot hear an announcement**, and it is not progress against the eight
  surfaces above.
- **The browser payload is ~27 MiB** because trimming is off — `PROGRESS.md` item 5.

---

## From the round before, and all of it still applies

- **A stream's report is not a verdict — and this round the re-run paid twice.** The `.shell`
  stream claimed the budget strip was no longer a child of `.shell`, contradicting what this file
  said; the stream was right and this file was stale. Separately, the search stream's baseline was
  quoted as 24 in `PROGRESS.md` prose and 25 in the test file's own constant. Re-run, then read.
- **A mutation can be null because you removed only half of what carries the behaviour.** Breaking
  the new Powers vocabulary by deleting Phasing's `walls` tag changed nothing, because the same
  entry also carries `walk` and the query "walks through walls" matches it by shared prefix. The
  test passed and looked like a guard with a hole; it was a null mutation. Removing the whole set
  dropped the score 33 → 31 and named `phasing` in the failure. **Check the mutation bit before
  you conclude anything about the guard.**
- **A set comparison cannot see reordering**, which is how the extractor audit once rotated 1,492
  section bodies onto the wrong headings with the suite green. When the question is "did this move
  lose anything", prove the partition *tiles* the source — count rows with duplicates, count them
  distinct, and compare both against the total.
- **`git add -A <dir>` will sweep up untracked files that are not yours.** It caught an unrelated
  `.docx` sitting in `docs/`. `git rm --cached` undid it without touching the file, but the commit
  had already been made. Stage by path.
- **A heredoc is not an editing tool**, again, in a new spelling: a `python - <<EOF` heredoc on a
  machine with no Python hangs for the full timeout waiting on stdin. Use the editing tool.
