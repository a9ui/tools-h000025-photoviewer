import fs from 'fs';
import os from 'os';
import path from 'path';
import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  extractSDMetadata,
  MAX_PNG_CHUNKS_BEFORE_IDAT,
  MAX_PNG_METADATA_CHUNK_BYTES,
} from './pngParser';

const PNG_SIGNATURE = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
const createdRoots: string[] = [];

function makeChunk(type: string, data = Buffer.alloc(0), declaredLength = data.length) {
  const header = Buffer.alloc(8);
  header.writeUInt32BE(declaredLength >>> 0, 0);
  header.write(type, 4, 4, 'ascii');
  return Buffer.concat([header, data, Buffer.alloc(4)]); // parser does not need a valid CRC
}

function parametersChunk(raw: string) {
  return makeChunk('tEXt', Buffer.from(`parameters\0${raw}`, 'utf8'));
}

function writePng(chunks: Buffer[]) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-png-parser-'));
  createdRoots.push(root);
  const target = path.join(root, 'fixture.png');
  fs.writeFileSync(target, Buffer.concat([
    PNG_SIGNATURE,
    ...chunks,
    makeChunk('IDAT'),
    makeChunk('IEND'),
  ]));
  return target;
}

afterEach(() => {
  vi.restoreAllMocks();
  for (const root of createdRoots.splice(0)) {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

describe('extractSDMetadata security bounds', () => {
  it('parses a normal parameters chunk without reading image data', () => {
    const target = writePng([
      parametersChunk('portrait, soft light\nNegative prompt: text\nSteps: 28, Sampler: Euler a, Seed: 7'),
    ]);

    expect(extractSDMetadata(target)).toEqual({
      raw: 'portrait, soft light\nNegative prompt: text\nSteps: 28, Sampler: Euler a, Seed: 7',
      prompt: 'portrait, soft light',
      negativePrompt: 'text',
      settings: { Steps: '28', Sampler: 'Euler a', Seed: '7' },
    });
  });

  it('rejects a declared chunk that extends beyond the real file before allocating its payload', () => {
    const target = writePng([
      makeChunk('tEXt', Buffer.alloc(0), 0xffffffff),
    ]);
    const allocation = vi.spyOn(Buffer, 'alloc');

    expect(extractSDMetadata(target)).toBeNull();
    expect(allocation.mock.calls.every(([size]) => Number(size) <= 80)).toBe(true);
  });

  it('rejects an oversized parameters payload without allocating the declared payload', () => {
    const keyword = Buffer.from('parameters\0', 'latin1');
    const data = Buffer.concat([
      keyword,
      Buffer.alloc(MAX_PNG_METADATA_CHUNK_BYTES - keyword.length + 1, 0x61),
    ]);
    const target = writePng([makeChunk('tEXt', data)]);
    const allocation = vi.spyOn(Buffer, 'alloc');

    expect(extractSDMetadata(target)).toBeNull();
    expect(allocation.mock.calls.every(([size]) => Number(size) <= 80)).toBe(true);
  });

  it('reads only a bounded keyword prefix for a large unrelated text chunk', () => {
    const keyword = Buffer.from('comment\0', 'latin1');
    const unrelated = Buffer.concat([
      keyword,
      Buffer.alloc(MAX_PNG_METADATA_CHUNK_BYTES - keyword.length + 1, 0x62),
    ]);
    const target = writePng([
      makeChunk('tEXt', unrelated),
      parametersChunk('kept prompt'),
    ]);
    const allocation = vi.spyOn(Buffer, 'alloc');

    expect(extractSDMetadata(target)?.prompt).toBe('kept prompt');
    expect(allocation.mock.calls.every(([size]) => Number(size) <= 80)).toBe(true);
  });

  it('bounds zero-length chunk traversal before IDAT', () => {
    const chunks = Array.from(
      { length: MAX_PNG_CHUNKS_BEFORE_IDAT + 1 },
      () => makeChunk('tEXt'),
    );
    const target = writePng(chunks);

    expect(extractSDMetadata(target)).toBeNull();
  });

  it('preserves first parameters chunk ownership, including an empty first value', () => {
    const firstWins = writePng([
      parametersChunk('first'),
      parametersChunk('second'),
    ]);
    const emptyFirstWins = writePng([
      parametersChunk(''),
      parametersChunk('later'),
    ]);

    expect(extractSDMetadata(firstWins)?.raw).toBe('first');
    expect(extractSDMetadata(emptyFirstWins)).toMatchObject({ raw: '', prompt: '' });
  });

  it('contains a malformed file and still parses the next image', () => {
    const malformed = writePng([
      makeChunk('tEXt', Buffer.from('parameters\0cut', 'utf8'), 1000),
    ]);
    const valid = writePng([parametersChunk('next image')]);

    expect(extractSDMetadata(malformed)).toBeNull();
    expect(extractSDMetadata(valid)?.prompt).toBe('next image');
  });
});
