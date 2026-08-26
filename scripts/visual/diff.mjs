// Compares two PNGs pixel by pixel and reports where they differ. No image-diff package is
// installed (`npm view pixelmatch version` answers fine from this network, but this repository
// has never had a package.json or a node_modules — the accounts server's whole test suite runs
// on `node --test` with nothing installed), so this is deliberately plain: decode both files
// with png.mjs, walk the buffers, and print what changed.
//
// Usage:
//   node diff.mjs <actual.png> <golden.png> [--out diff.png] [--max-diff-percent 0.02]
//                 [--channel-threshold 3] [--mean-diff-threshold 0.5]
//
// Exit 0 within tolerance, 1 over it, 2 on a usage or decoding fault (dimension mismatch, a
// missing file) — a different code because that is not a rendering regression, it is the
// harness being pointed at the wrong thing.
//
// ------------------------------------------------------------------------------------------------
// TWO INDEPENDENT MEASURES, BECAUSE ONE CANNOT SEE A UNIFORM SHIFT.
//
// The original version of this file had exactly one measure: count how many pixels differ by
// more than `channelThreshold` on any channel, and fail if more than `maxDiffPercent` of them do.
// An adversarial audit found that a **uniform shift of every pixel in the image by the same small
// amount** — every colour on the page nudged +20 per channel — produces a `differs` count of
// *zero*, because no single pixel's delta ever crosses the per-pixel threshold. That is not a
// hypothetical: this app renders four whole-page palettes, and "the whole palette went the wrong
// colour" is precisely the regression a pixel check exists to catch. No per-pixel threshold fixes
// this — halving it to 12 just moves the exploit to +11 per channel, uniformly.
//
// So there is a second measure that a uniform shift cannot escape: the **mean absolute channel
// difference over the entire image**. A shift too small to trip any single pixel's threshold
// still moves this measure by (almost) exactly the size of the shift, because it is not gated by
// a per-pixel cutoff at all — every pixel's contribution counts, however small. The two measures
// answer different questions and neither can stand in for the other: the count measure finds a
// *localised* change (a moved element, a missing icon, a block of the wrong colour) that is too
// small a fraction of the image to move the mean; the mean measure finds a *global* change (a
// wrong palette, a filter applied to the whole page) that is everywhere but nowhere over any
// single pixel's threshold. A page can fail either without failing the other, and both are
// checked: **fail if either is exceeded.**
//
// Whether alpha belongs in the mean: no. The per-pixel count measure already folds alpha into its
// `max(dr, dg, db, da)` — a real alpha regression is still caught there. But every screenshot this
// script ever compares is fully opaque: png.mjs forces alpha to 255 for an RGB source, and an
// RGBA screenshot of a rendered page carries no transparent region either (Chrome's `--screenshot`
// output is the whole viewport painted over the page's own background). So `da` is 0 for every
// pixel of every real comparison this script has ever been run against, and folding a channel that
// always contributes zero into the mean's denominator only dilutes it — dividing by 4 instead of 3
// makes a real colour shift read as 3/4 its true size for no corresponding benefit, since the
// alpha regression this would exist to catch is already caught by the count measure. The mean is
// computed over red, green and blue only.
//
// A third figure — the single largest per-channel delta seen anywhere in the image, alpha
// included — is cheap to track alongside the other two (it falls out of values already computed
// per pixel) and is reported for a human reading a failure, but it gates nothing: it is exactly
// what `channelThreshold` already gates at the per-pixel level, so making it a third pass/fail
// condition would just be the first measure with a different name.
//
// ------------------------------------------------------------------------------------------------
// THE DEFAULTS, AND THE ARITHMETIC BEHIND EACH.
//
// `channelThreshold` (default 3): the old default of 24 was sized for **cross-renderer** jitter —
// a golden painted by one Chrome build's antialiasing and hinting compared against a different
// Chrome's. That premise stops applying once goldens are generated in CI by the same Chrome,
// launched with the same flags, that later compares them: same font rasteriser, same subpixel
// rules, same software rasteriser path (`--disable-gpu` on both sides), so the honest jitter floor
// for two runs of *one* binary against *one* static page is at or near zero, not "however far two
// different browsers' antialiasing can disagree". This number is a trade against renderer jitter,
// not a constant of nature — it says how much per-pixel noise a rendering pipeline is allowed
// before a run counts as different from the one before it, and it should shrink as that pipeline's
// own repeatability is actually measured. 3 is chosen as a small, defensible margin above zero: it
// absorbs a couple of integer values' worth of floating-point rounding wobble in the rasteriser's
// own arithmetic (order-of-summation noise, not a rendering difference a person could see) without
// being wide enough to hide anything a person would call "the same colour rendered differently".
// It is deliberately not padded further "to be safe" — a padded default is the defect this file
// exists to fix, just moved to a new number.
//
// `maxDiffPercent` (default 0.02): must be tight enough that a fully-opaque 24×24 block — the
// audit's other adversarial case — fails at the viewport size this repository actually screenshots
// (1280×900, from scripts/visual-regression.sh's manifest). 1280×900 = 1,152,000 pixels; a 24×24
// block is 576 of them, or 576 / 1,152,000 × 100 = 0.05%. The *old* default was exactly 0.05%, so
// a 576-pixel block landed exactly on the boundary and passed (`<=`). 0.02% of 1,152,000 is 230.4
// pixels — √230 ≈ 15.2, so in practical terms this tolerates a solid block up to roughly 15×15
// (225 px) surviving as jitter but fails anything bigger, and a 24×24 block (576 px) clears that
// bar by better than 2×, not marginally. It still leaves room for a handful of stray antialiased
// pixels scattered across a page (nowhere near 230 of them) without the check having to be perfect
// about the count-measure/mean-measure split above.
//
// `meanDiffThreshold` (default 0.5, on the same 0–255 scale as a channel value): a uniform +20
// shift with alpha untouched has a mean absolute RGB delta of exactly 20 (every one of the three
// channels moved by 20, every pixel, so the mean is 20 regardless of image size) — forty times over
// this default, not a near miss. A real screenshot's honest mean, by contrast, is dominated by the
// vast majority of pixels that render identically run to run; even a page with a full line of text
// whose antialiasing genuinely wobbled a little at every edge would move the *mean over the whole
// viewport* by a small fraction of a single channel value, because most of a 1280×900 screenshot is
// flat background and untouched by any text edge at all. 0.5 is chosen to sit comfortably above
// that kind of honest, whole-image-diluted noise while catching a global shift with a wide margin.
//
// **These are starting points, not a claim of having measured this repository's actual CI Chrome
// against itself.** They are argued from the arithmetic above; the person landing the goldens this
// compares against has said they will re-measure against real CI-generated goldens and may adjust
// them from real numbers rather than from this reasoning alone. Adjust with a measurement, not by
// widening "to be safe" — that is exactly how the original 24 and 0.05% went stale.

import fs from 'node:fs';
import { decodePng, encodePng } from './png.mjs';

function parseArgs(argv) {
  const positional = [];
  const opts = { channelThreshold: 3, maxDiffPercent: 0.02, meanDiffThreshold: 0.5, out: null };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--out') { opts.out = argv[++i]; continue; }
    if (arg === '--max-diff-percent') { opts.maxDiffPercent = Number(argv[++i]); continue; }
    if (arg === '--channel-threshold') { opts.channelThreshold = Number(argv[++i]); continue; }
    if (arg === '--mean-diff-threshold') { opts.meanDiffThreshold = Number(argv[++i]); continue; }
    positional.push(arg);
  }

  if (positional.length !== 2) {
    throw new Error('usage: diff.mjs <actual.png> <golden.png> [--out diff.png] '
      + '[--max-diff-percent N] [--channel-threshold N] [--mean-diff-threshold N]');
  }

  return { actualPath: positional[0], goldenPath: positional[1], ...opts };
}

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(2);
}

function main() {
  let opts;
  try {
    opts = parseArgs(process.argv.slice(2));
  } catch (e) {
    fail(e.message);
    return;
  }

  if (!fs.existsSync(opts.actualPath)) fail(`no such file: ${opts.actualPath}`);
  if (!fs.existsSync(opts.goldenPath)) fail(`no such file: ${opts.goldenPath}`);

  const actual = decodePng(fs.readFileSync(opts.actualPath));
  const golden = decodePng(fs.readFileSync(opts.goldenPath));

  if (actual.width !== golden.width || actual.height !== golden.height) {
    fail(`dimension mismatch: actual is ${actual.width}x${actual.height}, `
      + `golden is ${golden.width}x${golden.height} — the harness changed the viewport, `
      + 'not (necessarily) the page');
  }

  const { width, height } = actual;
  const total = width * height;
  let diffCount = 0;
  let minX = width, minY = height, maxX = -1, maxY = -1;

  // The mean measure: summed over every pixel regardless of `channelThreshold`, which is exactly
  // what lets it see a shift the threshold-gated count cannot. Alpha is excluded — see the header
  // comment for why.
  let sumR = 0, sumG = 0, sumB = 0;
  // Reported only, per the header comment — this is what `channelThreshold` already gates.
  let maxChannelDiff = 0;

  // Built only when an --out path is asked for: unchanged pixels are dimmed to a third of their
  // brightness so the page's own structure stays visible as a backdrop, and every differing
  // pixel is painted solid magenta — a colour nothing in either palette in this app uses, so it
  // cannot be mistaken for real content.
  const visual = opts.out ? Buffer.alloc(actual.pixels.length) : null;

  for (let i = 0; i < total; i++) {
    const o = i * 4;
    const dr = Math.abs(actual.pixels[o] - golden.pixels[o]);
    const dg = Math.abs(actual.pixels[o + 1] - golden.pixels[o + 1]);
    const db = Math.abs(actual.pixels[o + 2] - golden.pixels[o + 2]);
    const da = Math.abs(actual.pixels[o + 3] - golden.pixels[o + 3]);
    const pixelMax = Math.max(dr, dg, db, da);
    const differs = pixelMax > opts.channelThreshold;

    sumR += dr;
    sumG += dg;
    sumB += db;
    if (pixelMax > maxChannelDiff) maxChannelDiff = pixelMax;

    if (differs) {
      diffCount++;
      const x = i % width;
      const y = Math.floor(i / width);
      if (x < minX) minX = x;
      if (y < minY) minY = y;
      if (x > maxX) maxX = x;
      if (y > maxY) maxY = y;
    }

    if (visual) {
      if (differs) {
        visual[o] = 255; visual[o + 1] = 0; visual[o + 2] = 255; visual[o + 3] = 255;
      } else {
        visual[o] = golden.pixels[o] / 3 | 0;
        visual[o + 1] = golden.pixels[o + 1] / 3 | 0;
        visual[o + 2] = golden.pixels[o + 2] / 3 | 0;
        visual[o + 3] = 255;
      }
    }
  }

  const diffPercent = (diffCount / total) * 100;
  const meanChannelDiff = (sumR + sumG + sumB) / (total * 3);
  const countWithin = diffPercent <= opts.maxDiffPercent;
  const meanWithin = meanChannelDiff <= opts.meanDiffThreshold;
  const within = countWithin && meanWithin;

  if (opts.out && diffCount > 0) {
    fs.writeFileSync(opts.out, encodePng({ width, height, pixels: visual }));
  }

  const reasons = [];
  if (!countWithin) {
    reasons.push(`count measure exceeded: ${diffPercent.toFixed(3)}% > ${opts.maxDiffPercent}%`);
  }
  if (!meanWithin) {
    reasons.push(`mean measure exceeded: ${meanChannelDiff.toFixed(3)} > ${opts.meanDiffThreshold}`);
  }

  const boxPart = diffCount > 0 ? `, bounding box (${minX},${minY})-(${maxX},${maxY})` : '';
  const outPart = (opts.out && diffCount > 0) ? `, diff written to ${opts.out}` : '';
  const summary = (diffCount === 0 && meanChannelDiff === 0)
    ? 'pixel-identical'
    : `${diffCount}/${total} pixels differ (${diffPercent.toFixed(3)}%)${boxPart}, `
      + `mean channel diff ${meanChannelDiff.toFixed(3)}/255 (max channel diff seen: `
      + `${maxChannelDiff})${outPart}`;

  const verdictPart = reasons.length ? ` [${reasons.join('; ')}]` : '';

  console.log(`${within ? 'OK' : 'FAIL'} ${opts.actualPath}: ${summary}${verdictPart} `
    + `(tolerance: count ${opts.maxDiffPercent}%, mean ${opts.meanDiffThreshold})`);

  process.exit(within ? 0 : 1);
}

main();
