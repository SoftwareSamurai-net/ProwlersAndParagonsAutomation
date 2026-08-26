// scripts/visual/png.mjs is ~150 hand-written lines of PNG decode/encode against `node:zlib` —
// no dependency, because this repository has never had one. Nothing tested it until this file.
//
// The property that matters is a round trip: encode a buffer of pixels, decode what came out, and
// get the same pixels back. Both RGB and RGBA are covered because Chrome's `--screenshot` can
// produce either (an opaque page encodes as RGB; the encoder here always writes RGBA, and the
// decoder normalises RGB up to RGBA with alpha forced opaque — see png.mjs's own comment on that).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import zlib from 'node:zlib';

import { decodePng, encodePng } from '../../scripts/visual/png.mjs';

/** A small, deliberately non-uniform RGBA buffer — a flat fill would not catch a transposed byte. */
function samplePixels(width, height) {
  const pixels = Buffer.alloc(width * height * 4);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const o = (y * width + x) * 4;
      pixels[o] = (x * 17 + y * 3) & 0xff;
      pixels[o + 1] = (x * 5 + y * 29) & 0xff;
      pixels[o + 2] = (x * 11 + y * 13) & 0xff;
      pixels[o + 3] = (x + y) % 2 === 0 ? 255 : 200;
    }
  }
  return pixels;
}

/** Encodes an RGB (colour type 2) PNG by hand — encodePng only ever writes RGBA (colour type 6),
 * so an RGB fixture has to be built directly to exercise the decoder's other supported input. */
function encodeRgbPng({ width, height, pixels }) {
  const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

  function chunk(type, data) {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length, 0);
    const typeBuf = Buffer.from(type, 'ascii');
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(zlib.crc32(Buffer.concat([typeBuf, data])) >>> 0, 0);
    return Buffer.concat([length, typeBuf, data, crc]);
  }

  const stride = width * 3;
  const raw = Buffer.alloc(height * (stride + 1));
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0;
    // Drop the alpha byte per pixel: RGBA -> RGB.
    for (let x = 0; x < width; x++) {
      const src = (y * width + x) * 4;
      const dst = y * (stride + 1) + 1 + x * 3;
      raw[dst] = pixels[src];
      raw[dst + 1] = pixels[src + 1];
      raw[dst + 2] = pixels[src + 2];
    }
  }

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr.writeUInt8(8, 8); // bit depth
  ihdr.writeUInt8(2, 9); // color type: RGB
  ihdr.writeUInt8(0, 10);
  ihdr.writeUInt8(0, 11);
  ihdr.writeUInt8(0, 12);

  return Buffer.concat([
    SIGNATURE,
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

test('encodePng then decodePng returns the original RGBA pixels unchanged', () => {
  const width = 13, height = 9; // odd, non-power-of-two dimensions on purpose
  const pixels = samplePixels(width, height);

  const encoded = encodePng({ width, height, pixels });
  const decoded = decodePng(encoded);

  assert.equal(decoded.width, width);
  assert.equal(decoded.height, height);
  assert.deepEqual(Buffer.from(decoded.pixels), Buffer.from(pixels));
});

test('decodePng recovers RGB pixels with alpha forced opaque', () => {
  const width = 11, height = 7;
  const pixels = samplePixels(width, height);

  const rgbPng = encodeRgbPng({ width, height, pixels });
  const decoded = decodePng(rgbPng);

  assert.equal(decoded.width, width);
  assert.equal(decoded.height, height);
  for (let i = 0; i < width * height; i++) {
    const o = i * 4;
    assert.equal(decoded.pixels[o], pixels[o], `red at pixel ${i}`);
    assert.equal(decoded.pixels[o + 1], pixels[o + 1], `green at pixel ${i}`);
    assert.equal(decoded.pixels[o + 2], pixels[o + 2], `blue at pixel ${i}`);
    // The RGB source's own alpha bytes are dropped entirely by encodeRgbPng, not merely ignored —
    // an RGB PNG carries no alpha channel at all, so every decoded pixel must read fully opaque.
    assert.equal(decoded.pixels[o + 3], 255, `alpha at pixel ${i} must be forced opaque`);
  }
});

test('a single-pixel image round-trips (the smallest input the format allows)', () => {
  const pixels = Buffer.from([10, 20, 30, 255]);
  const decoded = decodePng(encodePng({ width: 1, height: 1, pixels }));
  assert.deepEqual(Buffer.from(decoded.pixels), pixels);
});

test('decodePng refuses a file with the wrong signature', () => {
  assert.throws(() => decodePng(Buffer.from('not a png at all')), /bad signature/);
});

// --- The positive control the discipline in CLAUDE.md asks for -------------------------------
//
// A decoder test that only ever runs against this repository's own encoder can pass for the wrong
// reason: if the encoder silently wrote garbage, a decoder that faithfully reproduces that same
// garbage still "round-trips" successfully. This test proves the round-trip is actually checking
// pixel content by breaking the encoder — patching it to always write solid red regardless of the
// pixels it is handed — and confirming the round-trip test above would go red against it. It does
// not modify the committed encoder; it re-derives a broken IDAT the same way encodePng does, so a
// reviewer can see exactly what "the encoder is broken" would look like and that this file's other
// tests would catch it.
test('a broken encoder that always writes solid red is NOT hidden by the round-trip shape', () => {
  const width = 4, height = 4;
  const distinctPixels = samplePixels(width, height);

  // Mimic png.mjs's own chunking so this stays a fair reproduction of "the encoder wrote the
  // wrong bytes", not a different codec.
  const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
  function chunk(type, data) {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length, 0);
    const typeBuf = Buffer.from(type, 'ascii');
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(zlib.crc32(Buffer.concat([typeBuf, data])) >>> 0, 0);
    return Buffer.concat([length, typeBuf, data, crc]);
  }
  const stride = width * 4;
  const raw = Buffer.alloc(height * (stride + 1));
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0;
    for (let x = 0; x < width; x++) {
      const dst = y * (stride + 1) + 1 + x * 4;
      raw[dst] = 255; raw[dst + 1] = 0; raw[dst + 2] = 0; raw[dst + 3] = 255; // constant red
    }
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr.writeUInt8(8, 8);
  ihdr.writeUInt8(6, 9);
  ihdr.writeUInt8(0, 10);
  ihdr.writeUInt8(0, 11);
  ihdr.writeUInt8(0, 12);
  const brokenPng = Buffer.concat([
    SIGNATURE, chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0)),
  ]);

  const decoded = decodePng(brokenPng);
  // The real round-trip test asserts deepEqual against distinctPixels; a constant-red buffer only
  // matches it by coincidence. Assert that mismatch here so this file demonstrably would have
  // caught the broken-encoder case, rather than merely asserting that it currently does not occur.
  assert.notDeepEqual(Buffer.from(decoded.pixels), Buffer.from(distinctPixels));
});
