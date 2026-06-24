import fs from 'fs';
import os from 'os';
import path from 'path';
import sharp from 'sharp';
import { describe, expect, it } from 'vitest';
import { ensureDisplayImage, ensureThumbnail, getDisplayPath, getThumbnailPath } from './thumbnailCache';

describe('thumbnail cache', () => {
  it('replaces a broken existing thumbnail cache file', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pvu-thumb-cache-'));
    const sourcePath = path.join(root, 'source.png');
    await sharp({
      create: {
        width: 12,
        height: 12,
        channels: 4,
        background: '#224466ff',
      },
    })
      .png()
      .toFile(sourcePath);

    const thumbPath = await getThumbnailPath(sourcePath);
    fs.mkdirSync(path.dirname(thumbPath), { recursive: true });
    fs.writeFileSync(thumbPath, '');

    await ensureThumbnail(sourcePath, 0);

    const stat = fs.statSync(thumbPath);
    expect(stat.size).toBeGreaterThan(0);
    await expect(sharp(thumbPath, { failOn: 'none' }).metadata()).resolves.toMatchObject({
      format: 'webp',
    });
  });

  it('coalesces concurrent thumbnail generation into one valid cache file', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pvu-thumb-concurrent-'));
    const sourcePath = path.join(root, 'source.png');
    await sharp({
      create: {
        width: 64,
        height: 64,
        channels: 4,
        background: '#aa6633ff',
      },
    })
      .png()
      .toFile(sourcePath);

    const results = await Promise.all(Array.from({ length: 6 }, () => ensureThumbnail(sourcePath, 0)));
    const thumbPath = await getThumbnailPath(sourcePath);

    expect(new Set(results.map((result) => result.thumbPath))).toEqual(new Set([thumbPath]));
    expect(fs.existsSync(thumbPath)).toBe(true);
    await expect(sharp(thumbPath, { failOn: 'none' }).metadata()).resolves.toMatchObject({
      format: 'webp',
    });
  });

  it('coalesces concurrent display-image generation into one valid cache file', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pvu-display-concurrent-'));
    const sourcePath = path.join(root, 'source.png');
    await sharp({
      create: {
        width: 96,
        height: 72,
        channels: 4,
        background: '#3366aaff',
      },
    })
      .png()
      .toFile(sourcePath);

    const results = await Promise.all(Array.from({ length: 6 }, () => ensureDisplayImage(sourcePath, 0)));
    const displayPath = await getDisplayPath(sourcePath);

    expect(new Set(results.map((result) => result.displayPath))).toEqual(new Set([displayPath]));
    expect(fs.existsSync(displayPath)).toBe(true);
    await expect(sharp(displayPath, { failOn: 'none' }).metadata()).resolves.toMatchObject({
      format: 'webp',
    });
  });
});
