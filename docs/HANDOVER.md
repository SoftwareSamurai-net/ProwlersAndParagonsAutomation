# Handover

**`CLAUDE.md` is now an index of 286 lines, and ten guides under [`docs/guide/`](guide/) carry
the rest.** Read [`CLAUDE.md`](../CLAUDE.md) first and follow its routing table to the guide for
whatever you are about to touch, then [`PROGRESS.md`](../PROGRESS.md).

**This round closed all three things the owner reported from using the deployed app** — the two
that were one defect, and the explained sheet. See the section below for what each was and what
replaced it.

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

**4,800 tests across four suites** — 4,015 engine, 605 bUnit, 166 accounts, 14 pixel comparator.
Measured after the last merge, not carried across from any stream:

```bash
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
./scripts/test-visual.sh
```

**`dotnet test` prints one `Passed!` line per project, and there are two.** If you see one, a
project failed to **build** and its result is simply missing. Count the lines, and grep for
`Catastrophic` — a crashed process still prints `Passed! - Failed: 0`.

**A whole-tree Qodana scan reported 0** via `./scripts/qodana-scan.sh` (needs Docker Desktop), on
both of this round's branches. Do not repeat that zero without re-running it — **and expect the new
test files to put findings there.** Both branches scanned dirty the first time, on the same three
rules every time: `UseAwaitUsing` for an `IAsyncDisposable` context in an `async` test,
`MethodHasAsyncOverload` for `Click()` where `ClickAsync` exists, and a `using` a global one already
covers. Run the scan before the push rather than after the PR.

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

---

## What is left, in the order I would take it

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
