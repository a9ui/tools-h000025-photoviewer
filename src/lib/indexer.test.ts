import fs from 'fs';
import os from 'os';
import path from 'path';
import crypto from 'crypto';
import { afterEach, describe, expect, it } from 'vitest';
import { getFolderBuckets, scanDirectory, searchIndex, setIndex } from './indexer';

const ONE_BY_ONE_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=',
  'base64'
);

const createdRoots: string[] = [];
const cacheFiles: string[] = [];

function cacheHash(dirPath: string) {
  return crypto.createHash('md5').update(path.resolve(dirPath).toLowerCase()).digest('hex');
}

function rememberCacheFiles(dirPath: string) {
  const hash = cacheHash(dirPath);
  cacheFiles.push(
    path.join(process.cwd(), '.cache', `index_${hash}.json`),
    path.join(process.cwd(), '.cache', `folders_${hash}.json`)
  );
}

function writePng(filePath: string) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, ONE_BY_ONE_PNG);
}

function writeImagePlaceholder(filePath: string) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, Buffer.from('placeholder image bytes'));
}

afterEach(() => {
  setIndex([]);
  for (const filePath of cacheFiles.splice(0)) {
    fs.rmSync(filePath, { force: true });
  }
  for (const root of createdRoots.splice(0)) {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

describe('scanDirectory', () => {
  it('detects new PNGs added inside nested folders after folder-signature caching', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    const nested = path.join(root, '2026-05-18', 'nested');
    writePng(path.join(nested, 'first.png'));

    expect(await scanDirectory(root)).toHaveLength(1);
    expect(await scanDirectory(root)).toHaveLength(1);

    writePng(path.join(nested, 'second.png'));
    const future = new Date(Date.now() + 2000);
    fs.utimesSync(nested, future, future);

    const afterNestedAdd = await scanDirectory(root);
    expect(afterNestedAdd.map((image) => image.filename).sort()).toEqual(['first.png', 'second.png']);
  });

  it('can force a full verification scan for same-name overwrites', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    const imageDir = path.join(root, '2026-05-18');
    const imagePath = path.join(imageDir, 'overwrite.png');
    const inactiveFolderTime = new Date(Date.now() - 72 * 60 * 60 * 1000);
    writePng(imagePath);
    fs.utimesSync(imageDir, inactiveFolderTime, inactiveFolderTime);

    const initial = await scanDirectory(root);
    expect(initial).toHaveLength(1);
    expect(await scanDirectory(root)).toHaveLength(1);

    const future = new Date(Date.now() + 3000);
    fs.utimesSync(imagePath, future, future);
    fs.utimesSync(imageDir, inactiveFolderTime, inactiveFolderTime);

    const fastRefresh = await scanDirectory(root);
    expect(fastRefresh[0].mtime).toBe(initial[0].mtime);

    const verifiedRefresh = await scanDirectory(root, undefined, { forceFull: true });
    expect(verifiedRefresh[0].mtime).toBeGreaterThan(initial[0].mtime);
  });

  it('indexes newly added non-PNG image files', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    writePng(path.join(root, 'initial.png'));
    expect(await scanDirectory(root)).toHaveLength(1);

    writeImagePlaceholder(path.join(root, 'new-photo.jpg'));
    writeImagePlaceholder(path.join(root, 'new-render.webp'));
    const future = new Date(Date.now() + 2000);
    fs.utimesSync(root, future, future);

    const afterAdd = await scanDirectory(root);
    expect(afterAdd.map((image) => image.filename).sort()).toEqual([
      'initial.png',
      'new-photo.jpg',
      'new-render.webp',
    ]);
  });

  it('scans newly added top-level folders even when their folder mtime predates the last scan', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    writePng(path.join(root, 'existing', 'first.png'));
    expect(await scanDirectory(root)).toHaveLength(1);
    expect(await scanDirectory(root)).toHaveLength(1);

    const imported = path.join(root, 'imported-old');
    writePng(path.join(imported, 'second.png'));
    const past = new Date(Date.now() - 60_000);
    fs.utimesSync(imported, past, past);

    const afterImport = await scanDirectory(root);
    expect(afterImport.map((image) => image.filename).sort()).toEqual(['first.png', 'second.png']);
  });

  it('does not let a signature-only empty folder skip later uncached child images', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    const olderFolder = path.join(root, '2026-05-17');
    fs.mkdirSync(olderFolder, { recursive: true });
    const stablePast = new Date(Date.now() - 72 * 60 * 60 * 1000);
    fs.utimesSync(olderFolder, stablePast, stablePast);

    expect(await scanDirectory(root)).toHaveLength(0);

    writePng(path.join(olderFolder, 'late-child.png'));
    fs.utimesSync(olderFolder, stablePast, stablePast);

    const afterChildAdd = await scanDirectory(root);
    expect(afterChildAdd.map((image) => image.filename)).toEqual(['late-child.png']);
  });

  it('does not let a signature-only nested folder skip later uncached deeper images', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    const nested = path.join(root, '2026-05-17', 'child');
    fs.mkdirSync(nested, { recursive: true });
    const stablePast = new Date(Date.now() - 72 * 60 * 60 * 1000);
    fs.utimesSync(nested, stablePast, stablePast);
    fs.utimesSync(path.dirname(nested), stablePast, stablePast);

    expect(await scanDirectory(root)).toHaveLength(0);

    writePng(path.join(nested, 'deep.png'));
    fs.utimesSync(nested, stablePast, stablePast);
    fs.utimesSync(path.dirname(nested), stablePast, stablePast);

    const afterDeepAdd = await scanDirectory(root);
    expect(afterDeepAdd.map((image) => image.filename)).toEqual(['deep.png']);
  });

  it('rescans recently active existing folders even when the folder signature looks unchanged', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    const today = path.join(root, '2026-05-19');
    const stableTimestamp = new Date();
    writePng(path.join(today, 'first.png'));
    fs.utimesSync(today, stableTimestamp, stableTimestamp);

    expect(await scanDirectory(root)).toHaveLength(1);
    expect(await scanDirectory(root)).toHaveLength(1);

    writePng(path.join(today, 'second.png'));
    fs.utimesSync(today, stableTimestamp, stableTimestamp);

    const afterAdd = await scanDirectory(root);
    expect(afterAdd.map((image) => image.filename).sort()).toEqual(['first.png', 'second.png']);
  });

  it('treats an unreadable scan root as a failed scan instead of a zero-result success', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-'));
    createdRoots.push(root);
    rememberCacheFiles(root);

    writePng(path.join(root, '2026-05-17', 'old.png'));
    expect(await scanDirectory(root)).toHaveLength(1);

    const cachePath = path.join(process.cwd(), '.cache', `index_${cacheHash(root)}.json`);
    const before = fs.readFileSync(cachePath, 'utf-8');

    fs.renameSync(root, `${root}-offline`);
    createdRoots.push(`${root}-offline`);

    await expect(scanDirectory(root)).rejects.toThrow();
    expect(fs.readFileSync(cachePath, 'utf-8')).toBe(before);
  });

  it('filters search and folder buckets across newline-separated folder sets', async () => {
    const rootA = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-a-'));
    const rootB = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-b-'));
    const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-indexer-out-'));
    createdRoots.push(rootA, rootB, outside);
    rememberCacheFiles(rootA);
    rememberCacheFiles(rootB);
    rememberCacheFiles(outside);

    writePng(path.join(rootA, 'day-a', 'a.png'));
    writePng(path.join(rootB, 'day-b', 'b.png'));
    writePng(path.join(outside, 'day-out', 'out.png'));

    const images = [
      ...(await scanDirectory(rootA)),
      ...(await scanDirectory(rootB)),
      ...(await scanDirectory(outside)),
    ];
    setIndex(images);

    const dirSet = `${rootA}\n${rootB}`;
    const result = searchIndex('', 0, 20, 'name', undefined, undefined, undefined, dirSet);
    expect(result.total).toBe(2);
    expect(result.results.map((image) => image.filename).sort()).toEqual(['a.png', 'b.png']);

    const folders = getFolderBuckets(dirSet);
    expect(folders.map((folder) => folder.key).sort()).toEqual(['0:day-a', '1:day-b']);
  });
});
