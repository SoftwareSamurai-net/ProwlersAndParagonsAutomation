# Handover

**`CLAUDE.md` is now an index of 286 lines, and ten guides under [`docs/guide/`](guide/) carry
the rest.** Read [`CLAUDE.md`](../CLAUDE.md) first and follow its routing table to the guide for
whatever you are about to touch, then [`PROGRESS.md`](../PROGRESS.md).

This round closed `PROGRESS.md` item 7's snapshotability half, item 4 (`search_powers`), item 1c
(the extractor's paragraph breaks), item 2's route-back half, and the `.shell` spacing item that had
sat in this file's "still open" list since before the pre-1.0 audit — and re-verified item 1 (the
four Heroes) with a second independent instrument. Then the owner asked for character management,
which is [#84](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/84).

**Read the adversarial-review entry in `PROGRESS.md` before touching the anonymous slot.** That
round shipped three defects into review — one of which silently destroyed a reader's draft on
sign-out — and two independent reviewers found all three. The fix is structural and the entry says
why; the shape to keep is that the account copy has a slot of its own and nothing else is ever
written to or cleared.

---

## Where things stand

**4,775 tests across four suites** — 4,015 engine, 580 bUnit, 166 accounts, 14 pixel comparator.
Measured after the last merge, not carried across from any stream:

```bash
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
./scripts/test-visual.sh
```

**`dotnet test` prints one `Passed!` line per project, and there are two.** If you see one, a
project failed to **build** and its result is simply missing. Count the lines, and grep for
`Catastrophic` — a crashed process still prints `Passed! - Failed: 0`.

**A whole-tree Qodana scan reported 0** via `./scripts/qodana-scan.sh` (needs Docker Desktop).
Do not repeat that zero without re-running it.

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

## What is left, in the order I would take it

1. **`PROGRESS.md` item 10 needs a decision, and it is the highest-value thing on this list.**
   Nothing drives the assembled application: every visual proof is bUnit markup rendered against
   the real stylesheets and screenshotted, which is *not* the running app — no interop, no routing,
   no Functions, and nothing behind sign-in. **Two defects in the last slice were found by
   screenshotting and none by the suites**, and three storage tests had to assert on *which key is
   written* because bUnit answers null to every interop read. Item 10 splits it: the anonymous half
   needs no permission; the signed-in half needs a development-only session seam, which is the most
   dangerous thing that could be added here, so it is the owner's call and there is a zero-risk
   alternative written up beside it.

2. **The codebase half of item 7**, untouched: dead code, engine hot paths, payload waste, and the
   token side. `PROGRESS.md` is now over 5,400 lines and is read at the start of every slice by
   instruction — the same argument that motivated the `CLAUDE.md` split applies to it, and the
   same answer probably does not, because it is chronological by design. Nobody has costed it.
3. **The four published Heroes 1 HP out** — item 1. Now a *confirmed* negative from two
   independent instruments: the residual is not a mispriced element in any of the four, and no
   alternate starting package lands any of them on 125. What is left is an interaction — a floor,
   a baseline or a grouping applied where the authors did something else. Do not tune an ambiguous
   variant to force a zero.
4. **`search_powers` measures again: 60 of 72**, widened from a saturated 33 of 33 and ratcheted
   there. The twelve misses are real gaps, reported rather than tuned away — three are negatives
   that find weak coincidental hits, and the rest are Powers with no vocabulary written for them
   yet. `cloud_minds`, `buff`, `power_absorption`, `psi_screen`, `elemental_control` and
   `form_gaseous` are named, concrete candidates for the next pass. The scorer itself is untouched:
   description-only matches still tie flat.
5. **Durable telemetry**, deferred by the owner — `PROGRESS.md` item 9.
6. **Item 5, the 27 MiB payload**, deliberately not started this round and worth saying why: it
   cannot be verified on this machine (the trimmer needs the `wasm-tools` workload, which needs
   elevation), its failure mode is a *silently empty rules set at runtime* rather than a build
   error, and deploy fires on merge. It is the one open item where a mistake ships live and quiet.
   `PROGRESS.md` records the source-generation attempt that already failed and the test that caught
   it; whatever is done needs a check that loads the published site and reads a rule out of it.

---

## Still open from before, unchanged

- **Screen-reader testing is owed** on the command palette, the pips, the sign-in page, the
  light/dark control, the row descriptions, the rules search, the row findings and the undo
  announcement. `aria-pressed` asserted as the string `"true"` is not the same as having been
  listened to. **No agent in this repository can close this** — it needs a person with a screen
  reader, and nothing automated is a substitute. Saying otherwise would be the kind of claim this
  file exists to prevent.

  What *was* added is one structural check that clears the plumbing out of the way first:
  `AriaReferenceTests` sweeps twelve surfaces and resolves every token of `aria-describedby`,
  `aria-labelledby` and `aria-controls`, so a dangling IDREF cannot reach a person doing the real
  work. It catches the class rather than an instance — `RowDescriptionTests` already resolved one
  row's target — and it carries a count as its positive control, because every assertion in it is
  an absence and a sweep that rendered nothing satisfies all of them. Broken both ways and watched
  to fail: a dangled reference names the id, and an empty sweep reports `Only 0`. **It cannot hear
  an announcement**, and it is not progress against the eight surfaces above.
- **The browser payload is ~27 MiB** because trimming is off — `PROGRESS.md` item 5.

---

## What this round learned, that the next one needs

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
