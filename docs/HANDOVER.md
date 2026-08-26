# Handover

**The pre-1.0 audit's test half is done, both remaining Phase 3 front-end items are built, and
[#77](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/77) is green on both
checks.** Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) after this file.

A twelve-agent adversarial audit had applied **126 mutations to the three suites and 48 were not
caught**. Eleven streams closed them, worked in isolated git worktrees and merged one at a time onto
a single integration branch so that eleven streams cost **one** deploy. Every merge was verified by
re-running the suites rather than by trusting the stream's own report, and the highest-value guards
were re-broken by hand afterwards. The full account is the completed entry in `PROGRESS.md`; the
per-stream mutation tables are in [`docs/notes/`](notes/README.md).

The shape worth reusing: **fan out on file-collision boundaries, merge serially, verify each merge
yourself.** Two streams reported results I could not reproduce as stated and both mattered — see
"What this round learned" below.

---

## Where things stand

**4,653 tests across four suites** — 3,964 engine, 509 bUnit, 166 accounts, and **14 on the pixel
comparator, which is new**: `scripts/visual/diff.mjs` and the hand-written PNG codec beneath it had
no tests at all while being the only thing standing between four palettes and nobody looking.

Measured on the integration branch after the last merge, not carried across from any stream:

```bash
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
./scripts/test-visual.sh
```

**`dotnet test` prints one `Passed!` line per project, and there are two.** If you see one, a
project failed to **build** and its result is simply missing — the same trap as `Catastrophic` in
another spelling. Count the lines.

**The browser harnesses now assert nineteen verdicts, not eleven.** Every behavioural harness has a
**deliberately-broken twin** that CI requires to say `FAIL`, driven by the byte-identical harness
script. That is the answer to a denylist of hard-coded-verdict spellings, which `|| true` walked
straight through while reporting `PASS` against a sticky strip that moved 483px.

**A whole-tree Qodana scan reports 0.** `./scripts/qodana-scan.sh`; it needs Docker Desktop running.
It found 2 on this work and both were fixed. Do not repeat the zero without re-running it.

**Regenerating the rulebook corpus is now a check**, and `CLAUDE.md` used to say nothing did it:

```bash
dotnet run --project tools/RulebookExtractor -- "docs/Prowlers_&_Paragons_Ultimate_Edition.pdf" data/rulebook
```

Compare `git hash-object` against `git rev-parse HEAD:<file>`, **not** `git status` — regenerating
rewrites every file, and on Windows the raw bytes differ by line ending even when the blob does not.
All ten chapters came back byte-identical, which is what verifies the `PageReader` seam.

---

## The visual check, which is where most of the surprises were

**All seven pages are checked again and all seven come back pixel-identical in CI.** The goldens are
CI-rendered and committed. Three things had to be found on the way, and two were only findable
because the comparator had been tightened first — which is the argument for tightening it.

- **The step that uploads the diff images had never uploaded anything.** `upload-artifact@v4`
  excludes dot-prefixed paths by default, the path is `.visual-regression/`, and
  `if-no-files-found` defaults to `warn`. Every failing run logged *"No files were found with the
  provided path"* and went green. Found by trying to use it.
- **The rules reference was flaky the whole time.** Two runs on commits that changed nothing it
  renders disagreed on 59.6% of its pixels — `.panel`'s entrance animation caught mid-flight, which
  `--virtual-time-budget` cannot prevent because a wait is a race. Captures now force
  `prefers-reduced-motion`, so the frame is settled by construction.
- **The historic 32,462-pixel disagreement was never a renderer difference.** It was the same
  entrance animation, and the first diagnosis written for it here was wrong — the band where CI
  painted `--surface` and Docker painted `--bg` is the painted and unpainted state of one panel,
  not two renderers disagreeing about layout. `animation: none` under reduced motion closed it:
  the Docker Chrome and the runner's Chrome now produce **byte-for-byte identical PNGs for all
  seven pages**. The Windows-versus-Linux rule is untouched and still absolute; the
  Linux-versus-Linux gap is gone because it was never a gap.

**A local run now passes all seven.** Still do not use `--update-goldens` off Linux — the
Windows/Linux rasteriser difference is real and unrelated.

**To regenerate goldens deliberately**, once this is on `master`:

```bash
gh workflow run visual-goldens.yml --ref <branch>
gh run download <run-id> --name visual-goldens --dir tests/visual-goldens
```

**A `workflow_dispatch` workflow cannot be dispatched until it exists on the default branch** — a
GitHub rule, not a repository one. Before then the same PNGs come out of the ordinary build's
`visual-regression-diffs` artifact, whose `actual/` holds every page as CI rendered it.

Look at them before committing them. They are real images, and this is the one check in the
repository whose whole subject is what something looks like.

---

## What is left, in the order I would take it

1. **The snapshotability half of the pre-1.0 audit** — `PROGRESS.md` item 7. Untouched, and the
   argument for it got stronger: `CLAUDE.md` is over 1,300 lines and `PROGRESS.md` over 4,400, and
   this slice added to both. One weak data point in favour of the pointer-file answer: eleven agents
   were each given the two or three `CLAUDE.md` sections their task depended on rather than the
   whole file, and none went wrong for want of the rest — weak because the sections were chosen by
   somebody who had read all of it.
2. **`search_powers` vocabulary, not scoring.** The scorer is at 25 of 33 with a ratchet. The two
   cases `PROGRESS.md` item 4 names by hand are now known to be unreachable by *any* word-matching
   change — Phasing's description says "solid matter" and never "walls", Blast's says "a damaging
   ranged attack" and nothing about fire. That is a data slice with a different shape.
3. **The four published Heroes 1 HP out.** Every cheap explanation is spent.
4. **Discarding a non-current saved character** still uses a confirm; undoing it is a
   restore-into-store rather than the sheet buffer, so it was left rather than folded in.
5. **Durable telemetry**, deferred by the owner — `PROGRESS.md` item 9 has the research and the trap
   that would break the site silently if anybody migrates.

---

## Still open from before, unchanged

- **Screen-reader testing is owed** on the command palette, the pips, the sign-in page, the
  light/dark control, the row descriptions, the rules search — and now the row findings and the undo
  announcement. `aria-pressed` asserted as the string `"true"` is not the same as having been
  listened to.
- **The browser payload is ~27 MiB** because trimming is off — `PROGRESS.md` item 5.

---

## What this round learned, that the next one needs

- **An audit's finding can be right about the symptom and wrong about the cause.** "A negative
  purchased rank makes a character free and legal" was reported as a production bug. It is not —
  `CheckQuantities` catches it. What was true was the sentence underneath: the guard has no
  dedicated coverage. The fix was the same either way, and acting on the headline would have meant
  "fixing" a validator that was already right. **Reproduce the claim before you act on it.**
- **A stream's report is not a verdict.** Two of eleven reported figures I could not reproduce as
  stated (one mis-stated its own check count, one had arithmetic wrong in a doc comment it had just
  written), and one reported a mutation as "predicted" before correcting itself after actually
  running it. None was dishonest and all were caught by re-running. Re-run.
- **A seam introduced to make something testable needs its own proof.** `PageReader` gained one
  because PdfPig's `Page` has no public constructor. Regenerating all ten chapters from the real PDF
  and comparing blob hashes is what proves it changed nothing — far stronger than the fifteen tests
  the seam exists to enable.
- **Halving a threshold does not close a threshold hole.** `channelThreshold: 24` let a uniform +20
  shift read as pixel-identical; at 12 the exploit is +11. A per-pixel count cannot see a uniform
  shift at any threshold, so it takes a second, independent measure.
- **A denylist of spellings cannot make a verdict honest.** Build the broken twin and require it to
  fail. And require it to say `FAIL` rather than merely not say `PASS` — a harness whose script never
  ran leaves its resting text, which is neither.
- **A guard's own doc comment is not evidence the guard exists.** `CLAUDE.md` asserted the engine has
  no filesystem access in two places, one of them *inside another guard's summary*, and nothing
  checked it.
- **Raw bytes are not the blob.** Regenerating the corpus on Windows produces CRLF where the blob is
  LF, which reads as "the extractor is platform-dependent" and nearly earned a fix. `.gitattributes`
  already handles it and the blobs are identical. Check the hash before you change the tool.
- **A shell heredoc is not an editing tool**, which this file has said before in another spelling.
  A long document containing backticks and quotes broke the heredoc that was writing it. Use the
  editing tool.
