// Compares two PNGs pixel by pixel and reports where they differ. No image-diff package is
// installed (`npm view pixelmatch version` answers fine from this network, but this repository
// has never had a package.json or a node_modules — the accounts server's whole test suite runs
// on `node --test` with nothing installed), so this is deliberately plain: decode both files
// with png.mjs, walk the buffers, and print what changed.
//
// Usage:
//   node diff.mjs <actual.png> <golden.png> [--out diff.png] [--max-diff-percent 0.05]
//                 [--channel-threshold 24]
//
// Exit 0 within tolerance, 1 over it, 2 on a usage or decoding fault (dimension mismatch, a
// missing file) — a different code because that is not a rendering regression, it is the
// harness being pointed at the wrong thing.

import fs from 'node:fs';
import { decodePng, encodePng } from './png.mjs';

function parseArgs(argv) {
  const positional = [];
  const opts = { channelThreshold: 24, maxDiffPercent: 0.05, out: null };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--out') { opts.out = argv[++i]; continue; }
    if (arg === '--max-diff-percent') { opts.maxDiffPercent = Number(argv[++i]); continue; }
    if (arg === '--channel-threshold') { opts.channelThreshold = Number(argv[++i]); continue; }
    positional.push(arg);
  }

  if (positional.length !== 2) {
    throw new Error('usage: diff.mjs <actual.png> <golden.png> [--out diff.png] '
      + '[--max-diff-percent N] [--channel-threshold N]');
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
    const differs = Math.max(dr, dg, db, da) > opts.channelThreshold;

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
  const within = diffPercent <= opts.maxDiffPercent;

  if (opts.out && diffCount > 0) {
    fs.writeFileSync(opts.out, encodePng({ width, height, pixels: visual }));
  }

  const summary = diffCount === 0
    ? 'pixel-identical'
    : `${diffCount}/${total} pixels differ (${diffPercent.toFixed(3)}%), `
      + `bounding box (${minX},${minY})-(${maxX},${maxY})`
      + (opts.out ? `, diff written to ${opts.out}` : '');

  console.log(`${within ? 'OK' : 'FAIL'} ${opts.actualPath}: ${summary} `
    + `(tolerance ${opts.maxDiffPercent}%)`);

  process.exit(within ? 0 : 1);
}

main();
