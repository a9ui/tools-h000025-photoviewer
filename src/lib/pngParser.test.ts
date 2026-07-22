import fs from 'fs';
import os from 'os';
import path from 'path';
import { afterEach, describe, expect, it } from 'vitest';
import { extractSDMetadata, extractSDMetadataAsync, PNG_METADATA_PREFIX_BYTES } from './pngParser';

const createdRoots: string[] = [];
const PNG_SIGNATURE = Buffer.from('89504e470d0a1a0a', 'hex');

function chunk(type: string, data: Buffer) {
  const output = Buffer.alloc(12 + data.length);
  output.writeUInt32BE(data.length, 0);
  output.write(type, 4, 4, 'ascii');
  data.copy(output, 8);
  return output;
}

function writeFixture(chunks: Buffer[]) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-png-parser-'));
  createdRoots.push(root);
  const filePath = path.join(root, 'fixture.png');
  fs.writeFileSync(filePath, Buffer.concat([PNG_SIGNATURE, ...chunks]));
  return filePath;
}

afterEach(() => {
  for (const root of createdRoots.splice(0)) fs.rmSync(root, { recursive: true, force: true });
});

describe('extractSDMetadata', () => {
  it('extracts parameters from the common prefix-only PNG layout', () => {
    const text = Buffer.from(
      'parameters\0portrait, blue sky\nNegative prompt: blur\nSteps: 20, Sampler: Euler',
      'utf8',
    );
    const filePath = writeFixture([chunk('tEXt', text), chunk('IDAT', Buffer.alloc(0))]);

    expect(extractSDMetadata(filePath)).toMatchObject({
      prompt: 'portrait, blue sky',
      negativePrompt: 'blur',
      settings: { Steps: '20', Sampler: 'Euler' },
    });
    return expect(extractSDMetadataAsync(filePath, Buffer.allocUnsafe(PNG_METADATA_PREFIX_BYTES))).resolves.toMatchObject({
      prompt: 'portrait, blue sky',
      negativePrompt: 'blur',
      settings: { Steps: '20', Sampler: 'Euler' },
    });
  });

  it('preserves async extraction after a chunk crosses the prefix boundary', async () => {
    const largeAncillaryChunk = Buffer.alloc(70 * 1024, 1);
    const text = Buffer.from('parameters\0fallback prompt\nSteps: 12, Seed: 7', 'utf8');
    const filePath = writeFixture([
      chunk('iCCP', largeAncillaryChunk),
      chunk('tEXt', text),
      chunk('IDAT', Buffer.alloc(0)),
    ]);

    expect(extractSDMetadata(filePath)).toMatchObject({
      prompt: 'fallback prompt',
      settings: { Steps: '12', Seed: '7' },
    });
    await expect(extractSDMetadataAsync(filePath, Buffer.allocUnsafe(PNG_METADATA_PREFIX_BYTES))).resolves.toMatchObject({
      prompt: 'fallback prompt',
      settings: { Steps: '12', Seed: '7' },
    });
  });

  it('returns null for non-PNG input and PNGs without parameters', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-png-parser-'));
    createdRoots.push(root);
    const nonPng = path.join(root, 'photo.jpg');
    fs.writeFileSync(nonPng, 'not a png');
    const pngWithoutParameters = writeFixture([
      chunk('tEXt', Buffer.from('Comment\0hello', 'utf8')),
      chunk('IDAT', Buffer.alloc(0)),
    ]);

    expect(extractSDMetadata(nonPng)).toBeNull();
    expect(extractSDMetadata(pngWithoutParameters)).toBeNull();
  });
});
