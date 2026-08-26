// scripts/visual/diff.mjs is the comparator behind this repository's only pixel check
// (scripts/visual-regression.sh). An adversarial audit found two independent ways its old,
// single-measure tolerance let a real regression through:
//
//   1. A per-pixel channel threshold cannot see a shift uniform across every pixel, however the
//      threshold is tuned — it only ever sees a *difference*, and a uniform shift produces the
//      same difference everywhere, none of it concentrated enough to cross any one pixel's cutoff.
//   2. A percentage-of-pixels tolerance, sized against the 1280x900 viewport this repository
//      actually screenshots, let a fully-opaque 24x24 block (exactly the old default's boundary)
//      through.
//
// This file drives the script as a real subprocess — exit code and stdout are the only public
// contract `scripts/visual-regression.sh` depends on — rather than importing its internals, since
// `main()` calls `process.exit` directly and cannot be safely imported into a running test runner.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { encodePng } from '../../scripts/visual/png.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const diffScript = path.join(__dirname, '..', '..', 'scripts', 'visual', 'diff.mjs');

// The manifest's own viewport (scripts/visual-regression.sh) — the size the "24x24 block passes"
// exploit and the maxDiffPercent arithmetic in diff.mjs's header comment are both stated against.
const WIDTH = 1280;
const HEIGHT = 900;

function solid(width, height, r, g, b, a = 255) {
  const pixels = Buffer.alloc(width * height * 4);
  for (let i = 0; i < width * height; i++) {
    pixels[i * 4] = r; pixels[i * 4 + 1] = g; pixels[i * 4 + 2] = b; pixels[i * 4 + 3] = a;
  }
  return { width, height, pixels };
}

function writeTempPng(dir, name, image) {
  const file = path.join(dir, name);
  fs.writeFileSync(file, encodePng(image));
  return file;
}

function runDiff(args) {
  const result = spawnSync(process.execPath, [diffScript, ...args], { encoding: 'utf8' });
  return { status: result.status, stdout: result.stdout, stderr: result.stderr };
}

function withTempDir(fn) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'pp-diff-test-'));
  try {
    return fn(dir);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

test('identical images: OK, "pixel-identical", exit 0', () => {
  withTempDir((dir) => {
    const image = solid(WIDTH, HEIGHT, 120, 120, 120);
    const a = writeTempPng(dir, 'a.png', image);
    const b = writeTempPng(dir, 'b.png', image);

    const { status, stdout } = runDiff([a, b]);
    assert.equal(status, 0);
    assert.match(stdout, /^OK /);
    assert.match(stdout, /pixel-identical/);
  });
});

test('a uniform +20-per-channel shift is invisible to the count measure alone, at the reported '
  + 'default (channel-threshold 24), and is caught only by the mean measure', () => {
  // This reproduces the exact adversarial case verbatim, with the *old* channelThreshold default
  // explicitly restored via the flag, to isolate what the mean measure alone contributes — with
  // this repository's tightened default (3) the count measure would also fire on a +20 shift,
  // which would prove nothing about whether the mean measure is doing any work on its own.
  withTempDir((dir) => {
    const golden = solid(WIDTH, HEIGHT, 120, 120, 120);
    const shifted = solid(WIDTH, HEIGHT, 140, 140, 140);
    const a = writeTempPng(dir, 'shifted.png', shifted);
    const b = writeTempPng(dir, 'golden.png', golden);

    const { status, stdout } = runDiff([a, b, '--channel-threshold', '24']);
    assert.equal(status, 1);
    assert.match(stdout, /^FAIL /);
    // The count measure must NOT be why this failed — that is the whole point of restoring the
    // old threshold — so the differing-pixel count must read zero.
    assert.match(stdout, /0\/1152000 pixels differ/);
    assert.match(stdout, /mean measure exceeded/);
  });
});

test('THE HEADLINE CASE: a uniform +20-per-channel shift fails at this file\'s own defaults', () => {
  withTempDir((dir) => {
    const golden = solid(WIDTH, HEIGHT, 120, 120, 120);
    const shifted = solid(WIDTH, HEIGHT, 140, 140, 140);
    const a = writeTempPng(dir, 'shifted.png', shifted);
    const b = writeTempPng(dir, 'golden.png', golden);

    const { status, stdout } = runDiff([a, b]);
    assert.equal(status, 1, 'a whole-page colour shift must fail the build');
    assert.match(stdout, /^FAIL /);
    assert.match(stdout, /mean measure exceeded/);
  });
});

test('a fully-opaque 24x24 block at 1280x900 fails (576/1,152,000 = 0.05%, over the 0.02% default)', () => {
  withTempDir((dir) => {
    const golden = solid(WIDTH, HEIGHT, 120, 120, 120);
    const block = solid(WIDTH, HEIGHT, 120, 120, 120);
    for (let y = 0; y < 24; y++) {
      for (let x = 0; x < 24; x++) {
        const o = (y * WIDTH + x) * 4;
        block.pixels[o] = 255; block.pixels[o + 1] = 0; block.pixels[o + 2] = 0; block.pixels[o + 3] = 255;
      }
    }
    const a = writeTempPng(dir, 'block.png', block);
    const b = writeTempPng(dir, 'golden.png', golden);

    const { status, stdout } = runDiff([a, b]);
    assert.equal(status, 1);
    assert.match(stdout, /^FAIL /);
    assert.match(stdout, /576\/1152000 pixels differ \(0\.050%\)/);
    assert.match(stdout, /count measure exceeded/);
  });
});

test('a single changed pixel passes — decided and pinned, not merely observed', () => {
  // A lone antialiasing-jitter pixel is exactly the kind of noise both tolerances exist to allow:
  // it moves the count measure by 1 out of 1,152,000 pixels (nowhere near the 0.02% cutoff) and
  // the mean measure by a few ten-thousandths (nowhere near 0.5), even at maximum severity — this
  // pixel is changed to the most different colour available, pure red against mid-grey. This is a
  // deliberate choice pinned by this test, not an accident of the arithmetic: the whole reason
  // both tolerances exist as *percentages*/*means* rather than "zero pixels may differ" is to let
  // exactly this kind of single-pixel noise through, so it must actually do that.
  withTempDir((dir) => {
    const golden = solid(WIDTH, HEIGHT, 120, 120, 120);
    const onePixel = solid(WIDTH, HEIGHT, 120, 120, 120);
    const o = 0; // pixel (0,0)
    onePixel.pixels[o] = 255; onePixel.pixels[o + 1] = 0; onePixel.pixels[o + 2] = 0;

    const a = writeTempPng(dir, 'one-pixel.png', onePixel);
    const b = writeTempPng(dir, 'golden.png', golden);

    const { status, stdout } = runDiff([a, b]);
    assert.equal(status, 0, `expected a single stray pixel to pass; got: ${stdout}`);
    assert.match(stdout, /^OK /);
    assert.match(stdout, /1\/1152000 pixels differ/);
  });
});

test('dimension mismatch exits 2, not 1 — a viewport change is a harness fault, not a regression', () => {
  withTempDir((dir) => {
    const a = writeTempPng(dir, 'a.png', solid(100, 50, 0, 0, 0));
    const b = writeTempPng(dir, 'b.png', solid(100, 60, 0, 0, 0));

    const { status, stderr } = runDiff([a, b]);
    assert.equal(status, 2);
    assert.match(stderr, /dimension mismatch/);
  });
});

test('a missing file exits 2', () => {
  withTempDir((dir) => {
    const b = writeTempPng(dir, 'b.png', solid(10, 10, 0, 0, 0));
    const { status, stderr } = runDiff([path.join(dir, 'does-not-exist.png'), b]);
    assert.equal(status, 2);
    assert.match(stderr, /no such file/);
  });
});

test('diffCount and the bounding box are exact on a known synthetic input', () => {
  // A 3x2 rectangle of changed pixels at a known offset in a small image — small enough that the
  // exact bounding box and count can be hand-verified rather than merely plausible.
  withTempDir((dir) => {
    const width = 20, height = 20;
    const golden = solid(width, height, 10, 10, 10);
    const actual = solid(width, height, 10, 10, 10);

    // Rectangle spanning x in [5,7], y in [8,9] — 3 columns x 2 rows = 6 pixels.
    for (let y = 8; y <= 9; y++) {
      for (let x = 5; x <= 7; x++) {
        const o = (y * width + x) * 4;
        actual.pixels[o] = 250; // far enough from 10 to clear any channel threshold
      }
    }

    const a = writeTempPng(dir, 'actual.png', actual);
    const b = writeTempPng(dir, 'golden.png', golden);

    const { status, stdout } = runDiff([a, b, '--max-diff-percent', '100', '--mean-diff-threshold', '255']);
    assert.equal(status, 0); // tolerances forced wide open so only the reported numbers are checked
    assert.match(stdout, /6\/400 pixels differ/);
    assert.match(stdout, /bounding box \(5,8\)-\(7,9\)/);
  });
});

test('--out writes a diff visualisation only when at least one pixel differs', () => {
  withTempDir((dir) => {
    const golden = solid(10, 10, 50, 50, 50);
    const identical = solid(10, 10, 50, 50, 50);
    const a = writeTempPng(dir, 'a.png', identical);
    const b = writeTempPng(dir, 'b.png', golden);
    const out = path.join(dir, 'diff.png');

    const { status } = runDiff([a, b, '--out', out]);
    assert.equal(status, 0);
    assert.equal(fs.existsSync(out), false, 'no diff image should be written when nothing differs');

    const different = solid(10, 10, 200, 50, 50);
    const c = writeTempPng(dir, 'c.png', different);
    const { status: status2 } = runDiff([c, b, '--out', out]);
    assert.equal(status2, 1);
    assert.equal(fs.existsSync(out), true, 'a diff image must be written on a real mismatch');
  });
});
