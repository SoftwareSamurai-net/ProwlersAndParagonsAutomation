// A PNG decoder and encoder for exactly the shape Chrome's `--screenshot` produces: 8-bit,
// non-interlaced, RGB or RGBA. Nothing else in this repository needs a PNG library, and adding
// one as a real dependency would be the first npm package the accounts server has ever needed —
// so this is the ~80-line decoder CLAUDE.md's own notes on this gap anticipated, using only
// `node:zlib` (inflate/deflate and, since Node 22, `crc32`) from the standard library.
//
// What it does not support is everything a general-purpose PNG library would: 16-bit depth,
// palettes, interlacing, ancillary chunks. A screenshot that needed any of those would mean
// Chrome's output shape changed, which is worth failing loudly on rather than silently
// mis-decoding.

import zlib from 'node:zlib';

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}

/**
 * Decodes a PNG file's bytes into `{ width, height, pixels }`, where `pixels` is always RGBA —
 * an RGB source has its alpha channel filled in as opaque, so every caller compares like for
 * like regardless of which one Chrome happened to write.
 */
export function decodePng(buffer) {
  if (!buffer.subarray(0, 8).equals(SIGNATURE)) {
    throw new Error('not a PNG file (bad signature)');
  }

  let offset = 8;
  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = 0;
  const idatChunks = [];

  while (offset < buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString('ascii', offset + 4, offset + 8);
    const data = buffer.subarray(offset + 8, offset + 8 + length);

    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data.readUInt8(8);
      colorType = data.readUInt8(9);
      const interlace = data.readUInt8(12);
      if (bitDepth !== 8) throw new Error(`unsupported PNG bit depth ${bitDepth} (only 8 is)`);
      if (interlace !== 0) throw new Error('unsupported interlaced PNG');
    } else if (type === 'IDAT') {
      idatChunks.push(data);
    }

    offset += 8 + length + 4; // length + type + data + CRC
    if (type === 'IEND') break;
  }

  const channelsIn = colorType === 6 ? 4 : colorType === 2 ? 3 : null;
  if (channelsIn === null) {
    throw new Error(`unsupported PNG color type ${colorType} (only RGB=2 and RGBA=6 are)`);
  }

  const raw = zlib.inflateSync(Buffer.concat(idatChunks));
  const stride = width * channelsIn;
  const unfiltered = Buffer.alloc(height * stride);
  let rawOffset = 0;

  for (let y = 0; y < height; y++) {
    const filterType = raw[rawOffset];
    rawOffset += 1;
    const rowStart = y * stride;
    const prevRowStart = rowStart - stride;

    for (let x = 0; x < stride; x++) {
      const a = x >= channelsIn ? unfiltered[rowStart + x - channelsIn] : 0;
      const b = y > 0 ? unfiltered[prevRowStart + x] : 0;
      const c = y > 0 && x >= channelsIn ? unfiltered[prevRowStart + x - channelsIn] : 0;
      const raw8 = raw[rawOffset + x];

      let value;
      switch (filterType) {
        case 0: value = raw8; break;
        case 1: value = raw8 + a; break;
        case 2: value = raw8 + b; break;
        case 3: value = raw8 + Math.floor((a + b) / 2); break;
        case 4: value = raw8 + paeth(a, b, c); break;
        default: throw new Error(`unsupported PNG filter type ${filterType}`);
      }

      unfiltered[rowStart + x] = value & 0xff;
    }

    rawOffset += stride;
  }

  if (channelsIn === 4) return { width, height, pixels: unfiltered };

  // RGB → RGBA, alpha forced opaque, so every consumer of `pixels` can assume 4 bytes a pixel.
  const pixels = Buffer.alloc(width * height * 4);
  for (let i = 0, o = 0; i < unfiltered.length; i += 3, o += 4) {
    pixels[o] = unfiltered[i];
    pixels[o + 1] = unfiltered[i + 1];
    pixels[o + 2] = unfiltered[i + 2];
    pixels[o + 3] = 255;
  }
  return { width, height, pixels };
}

function chunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);
  const typeBuf = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(zlib.crc32(Buffer.concat([typeBuf, data])) >>> 0, 0);
  return Buffer.concat([length, typeBuf, data, crc]);
}

/**
 * Encodes `{ width, height, pixels }` (RGBA) as an uncompressed-filter, deflate-compressed PNG.
 * Used only for the diff-visualisation image, which is a debugging aid rather than a committed
 * artifact, so there is no pressure to filter scanlines for a smaller file.
 */
export function encodePng({ width, height, pixels }) {
  const stride = width * 4;
  const raw = Buffer.alloc(height * (stride + 1));
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0; // filter type 0 (None) on every scanline
    pixels.copy(raw, y * (stride + 1) + 1, y * stride, y * stride + stride);
  }

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr.writeUInt8(8, 8); // bit depth
  ihdr.writeUInt8(6, 9); // color type: RGBA
  ihdr.writeUInt8(0, 10); // compression
  ihdr.writeUInt8(0, 11); // filter
  ihdr.writeUInt8(0, 12); // interlace

  return Buffer.concat([
    SIGNATURE,
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}
