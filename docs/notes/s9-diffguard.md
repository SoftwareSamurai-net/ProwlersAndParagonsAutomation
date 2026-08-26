# s9-diffguard: closing the two holes in the visual-regression comparator

`scripts/visual/diff.mjs` is the pixel comparator behind `scripts/visual-regression.sh`, the
repository's only pixel-level check. An adversarial audit found its tolerance had two independent
holes. This note records the "before" reproduction, the fix, the reasoning behind the new
defaults, and the mutation table proving each new check actually catches what it claims to.

## The two holes, reproduced against the unmodified file

Fixture: a 1280×900 (the manifest's own viewport) mid-grey (120,120,120) "golden", compared
against two "actual" images, using the code as it stood before this change (`channelThreshold`
defaulting to 24, `maxDiffPercent` to 0.05).

**Hole 1 — a uniform +20-per-channel shift.** Every pixel of "actual" is (140,140,140) — the whole
page nudged the same amount, no single pixel differing from any of its neighbours. The exact
stdout from the unmodified `diff.mjs`:

```
OK <path>/shifted20.png: pixel-identical (tolerance 0.05%)
```

Exit code `0`. No pixel's delta (20) ever exceeded `channelThreshold` (24), so the differing-pixel
count was zero and the check read the two images as identical — despite every pixel in the image
being wrong.

**Hole 2 — a fully-opaque 24×24 block.** "actual" is the same golden with a 24×24 square (top-left
corner) repainted solid red. 24×24 = 576 pixels; 1280×900 = 1,152,000 total; 576 / 1,152,000 × 100
= exactly 0.05% — the old default's own boundary. The exact stdout from the unmodified `diff.mjs`:

```
OK <path>/block24.png: 576/1152000 pixels differ (0.050%), bounding box (0,0)-(23,23) (tolerance 0.05%)
```

Exit code `0`. The comparison used `<=`, so a block landing exactly on the boundary passed.

## What was added

`diff.mjs` now checks **two independent measures** and fails if either is exceeded:

1. **The existing count measure** — the fraction of pixels whose per-channel delta exceeds
   `channelThreshold` — unchanged in shape, tightened in default (below).
2. **A new mean measure** — the mean absolute channel difference over every pixel in the image,
   computed over red/green/blue only (alpha excluded — see below), regardless of
   `channelThreshold`. A uniform shift moves this measure by (almost) exactly the size of the
   shift, because nothing about it is gated by a per-pixel cutoff.

A third figure — the single largest per-channel delta seen anywhere in the image (alpha included)
— is computed for free alongside the other two and printed in every failure/summary line for a
human to read, but gates nothing: it is exactly what `channelThreshold` already gates per pixel, so
making it a third pass/fail condition would just be the first measure under a different name.

**Alpha and the mean measure.** The per-pixel count measure already folds alpha into its
`max(dr, dg, db, da)`, so a genuine alpha regression is still caught there. The mean measure
excludes alpha: every screenshot this script compares is fully opaque (`png.mjs` forces alpha to
255 for an RGB source, and a real screenshot of a rendered page carries no transparent region
either), so alpha's delta is 0 for every real comparison this script has ever run, and folding an
always-zero channel into the mean's denominator only dilutes it — dividing by 4 instead of 3 makes
a real colour shift read as 3/4 its true size for no corresponding benefit.

## Chosen defaults and the arithmetic behind each

All three live in `scripts/visual/diff.mjs`'s own header comment, reproduced here:

- **`channelThreshold = 3`** (was 24). 24 was sized for *cross-renderer* jitter — a golden painted
  by one Chrome build's antialiasing compared against a different Chrome's. That premise stops
  applying once goldens are generated in CI by the same Chrome, launched with the same flags, that
  later compares them (a separate, later change). The honest jitter floor for two runs of *one*
  binary against *one* static page is then at or near zero, not "however far two different
  browsers' antialiasing can disagree" — this number is a trade against renderer jitter, not a
  constant of nature, and should shrink as that repeatability is actually measured. 3 is a small,
  defensible margin above zero: enough to absorb a couple of integer values' worth of
  floating-point rounding wobble in the rasteriser's own arithmetic, not enough to hide a real
  colour difference.
- **`maxDiffPercent = 0.02`** (was 0.05). Must be tight enough that a 24×24 block fails at
  1280×900: 1,152,000 pixels total; a 24×24 block is 576 of them, 0.05% — exactly the old default,
  so it landed on the boundary and passed. 0.02% of 1,152,000 is 230.4 pixels (√230 ≈ 15.2, so in
  practice a solid block up to roughly 15×15 survives as noise, anything bigger fails) — a 24×24
  block (576 px) clears that bar by more than 2×, not marginally.
- **`meanDiffThreshold = 0.5`** (new, 0–255 scale). A uniform +20 shift with alpha untouched has a
  mean absolute RGB delta of exactly 20, regardless of image size — forty times over this default.
  A real screenshot's honest mean is dominated by the vast majority of pixels that render
  identically run to run, so even genuine antialiasing wobble at every text edge moves the
  *whole-viewport* mean by a small fraction of one channel value. 0.5 sits comfortably above that
  kind of honest, whole-image-diluted noise while catching a global shift with a wide margin.

These are argued from the arithmetic above, not measured against this repository's actual CI
Chrome rendering itself — that measurement is explicitly left to the change that regenerates the
goldens from CI's own Chrome, per the brief for this work. Adjust from a measurement, not by
padding "to be safe" — that is exactly how the original 24 and 0.05% went stale.

**A single changed pixel was decided to PASS, and pinned.** At the new defaults, one pixel changed
to the most different colour available (pure red against mid-grey) moves the count measure by
1/1,152,000 (nowhere near 0.02%) and the mean measure by a few ten-thousandths (nowhere near 0.5).
This is deliberate: the whole reason both tolerances are percentages/means rather than "zero
pixels may differ" is to let exactly this kind of single-pixel antialiasing noise through, so the
test pins that it actually does. `tests/visual/diff.test.mjs` asserts on this by name.

## `scripts/visual-regression.sh`

The manifest (the list of pages compared) is untouched, per the brief. Two things were changed:

- `tolerance=0.05` → `tolerance=0.02`, to match `diff.mjs`'s own new `--max-diff-percent` default
  — otherwise the script would keep passing the *old*, exploitable 0.05% explicitly on every
  invocation, silently undoing the fix wherever this script is actually used. The mean measure
  needed no equivalent change: the script never overrode it, so `diff.mjs`'s own default (0.5)
  already applies.
- The header comment gained a short section pointing at `diff.mjs`'s own header for the
  two-measure reasoning, so the tolerance story is not duplicated in two places that can drift.

## CI and local wiring

- `.github/workflows/build.yml` gained a **"Test the visual-regression comparator"** step,
  modelled on the existing "Test the accounts API" step's count assertion — `node --test` against
  an empty glob exits 0 and reports zero tests, which reads exactly like a passing suite.
- **One difference from that model, found by testing rather than assumed:** the existing step
  relies on `node --test`'s reporter defaulting to `tap` when stdout is not a TTY. Reproducing that
  locally (Node 24, output piped through `tee`) it did not hold — the default reporter was `spec`
  (`✔ name (123ms)` lines), which matches `grep -c '^ok '` **zero times regardless of how many
  tests passed**. That is the exact "reads like a passing suite" failure the count assertion
  exists to catch, just relocated from an empty glob to a reporter guess. The new CI step and
  `scripts/test-visual.sh` both pass `--test-reporter=tap` explicitly rather than repeat that
  assumption. (The existing accounts-API step was left untouched — out of scope for this change —
  but the same risk applies to it on whatever Node/OS combination does not default to `tap`.)
- `scripts/test-visual.sh` is a new sibling to `scripts/test-worker.sh`, mirroring its
  Node-22-or-Docker-fallback shape and its `MSYS_NO_PATHCONV` handling for Git Bash on Windows.
  Both paths (local Node 24, and the `node:22-alpine` Docker fallback) were run directly and
  produced the same 14/14 passing count.

## Tests

`tests/visual/diff.test.mjs` (9 tests) and `tests/visual/png.test.mjs` (5 tests), 14 total, all
via `node --test` with no npm dependency — images are synthesised in-process with `png.mjs`'s own
`encodePng`, no committed fixtures. `diff.mjs` is driven as a real subprocess (it calls
`process.exit` directly, so it cannot safely be imported into a running test runner) and asserted
on exit code and stdout content, matching the only contract `scripts/visual-regression.sh` relies
on.

Covered: identical images (pixel-identical, exit 0); the uniform +20 shift at the *old*
`channelThreshold=24` in isolation (proves the mean measure alone catches what the count measure
cannot — the headline case, reproducing the reported hole exactly); the uniform +20 shift at this
file's own new defaults; a 24×24 opaque block (fails, names the count measure); a single changed
pixel (passes, pinned); dimension mismatch (exit 2); a missing file (exit 2); diffCount and
bounding-box exactness on a small hand-verified rectangle; `--out` only writing a diff PNG on a
real mismatch; and a PNG round trip for both RGB and RGBA, including a probe that a broken encoder
(always writing solid red) is **not** hidden by the round-trip shape — proving the round-trip test
is actually reading pixel content rather than agreeing with whatever the encoder wrote.

## Mutation table

Every row below was actually run, not reasoned about. Each mutation was applied on top of a
**committed** working tree (this fix + tests were committed first, per CLAUDE.md's stash/commit
discipline), the affected test was watched go red, and the mutation was then reverted before the
next row.

| # | Mutation | Expected | Observed |
|---|---|---|---|
| 1 | Removed the mean measure from `diff.mjs` (deleted the `meanWithin` check from `within`, so only the count measure gates) | The uniform-shift tests go red | Both uniform-shift tests failed: `THE HEADLINE CASE` failed because exit code was 0 instead of 1; the `channel-threshold 24` isolation test failed the same way. All other tests still passed. |
| 2 | Widened `maxDiffPercent`'s default back to `0.05` | The 24×24-block test goes red | The block test failed: exit code was 0 instead of 1 (576/1,152,000 = 0.050% now again `<=` 0.05%). All other tests still passed. |
| 3 | Made `png.mjs`'s `encodePng` write a constant (every pixel forced to solid red, ignoring the pixels argument) | The round-trip tests go red | `encodePng then decodePng returns the original RGBA pixels unchanged` and `decodePng recovers RGB pixels with alpha forced opaque` (RGB fixture goes through the real `encodePng`... — see note) both failed on the `deepEqual` pixel comparison, confirming the round-trip assertions are reading real pixel content and not merely agreeing with the encoder. |

Row 3 note: the RGB-decode test in `png.test.mjs` builds its own RGB fixture by hand
(`encodeRgbPng`) rather than going through `encodePng` (which only ever writes RGBA), so mutating
`encodePng` alone does not touch it directly — it was the RGBA round-trip test, plus the dedicated
"broken encoder" positive-control test in the same file, that went red, which is the pair the
positive-control test exists to prove: a round-trip test alone can pass against a broken encoder
by construction, and the positive control demonstrates that this file's actually does not.

After each row, the mutation was reverted and the full suite re-run green (14/14) before moving to
the next row.
