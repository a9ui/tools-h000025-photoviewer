import { execFile } from 'child_process';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import { promisify } from 'util';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { mutateAlbums, readAlbums } from './albums';

const execFileAsync = promisify(execFile);
const wpfExecutable = path.resolve(
  process.env.PVU_WPF_EXECUTABLE
    ?? 'local-native/PhotoViewer.Wpf/bin/Release/net8.0-windows/PhotoViewer.Wpf.exe',
);
const canRunWpf = process.platform === 'win32' && await fs.stat(wpfExecutable).then(() => true).catch(() => false);
const crossRuntimeDescribe = canRunWpf ? describe : describe.skip;
const wpfScaleIt = canRunWpf && process.env.PVU_RUN_WPF_ALBUM_SCALE === '1' ? it : it.skip;

async function waitForFile(target: string, timeoutMs = 10_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await fs.stat(target).then(() => true).catch(() => false)) return;
    await new Promise((resolve) => setTimeout(resolve, 10));
  }
  throw new Error(`Timed out waiting for ${target}`);
}

let root = '';

beforeEach(async () => {
  root = await fs.mkdtemp(path.join(os.tmpdir(), 'pvu-albums-cross-runtime-'));
});

afterEach(async () => {
  await fs.rm(root, { recursive: true, force: true });
});

crossRuntimeDescribe('Browser/WPF Album store compatibility', () => {
  wpfScaleIt('keeps WPF Album reads and operation publishes bounded at 1,000 Albums / 100,000 memberships', async () => {
    const target = path.join(root, 'albums.json');
    const resultPath = path.join(root, 'wpf-scale-result.json');
    const memberRoot = path.join(root, 'catalog');
    const timestamp = '2026-07-20T00:00:00.000Z';
    const albums = Array.from({ length: 1_000 }, (_, albumIndex) => {
      const albumId = `scale-${String(albumIndex).padStart(4, '0')}`;
      const members = Array.from({ length: 100 }, (_, memberIndex) => ({
        id: `${albumId}-member-${String(memberIndex).padStart(3, '0')}`,
        imagePath: path.join(memberRoot, String(albumIndex), `${memberIndex}.jpg`),
        addedAtUtc: timestamp,
        ...(albumIndex === 0 && memberIndex === 0 ? { futureMember: { keep: true } } : {}),
      }));
      return {
        id: albumId,
        name: `Scale Album ${albumIndex}`,
        pinned: albumIndex < 10,
        coverMemberId: members[0].id,
        createdAtUtc: timestamp,
        updatedAtUtc: timestamp,
        revision: 1,
        members,
        ...(albumIndex === 0 ? { futureAlbum: ['keep'] } : {}),
      };
    });
    await fs.writeFile(target, JSON.stringify({
      version: 1,
      revision: 5,
      updatedAtUtc: timestamp,
      albums,
      recentAlbumIds: albums.slice(0, 10).map((album) => album.id),
      futureRoot: { keep: true },
    }), 'utf8');

    const startedAt = Date.now();
    await execFileAsync(wpfExecutable, [
      '--album-store-smoke', resultPath,
      '--album-path', target,
      '--member-path', path.join(root, 'wpf-scale-new.jpg'),
    ], { windowsHide: true, timeout: 120_000, maxBuffer: 4 * 1024 * 1024 });
    const elapsedMs = Date.now() - startedAt;
    const wpfResult = JSON.parse(await fs.readFile(resultPath, 'utf8'));
    expect(wpfResult).toMatchObject({
      ok: true,
      initialRevision: 5,
      finalRevision: 9,
      unknownRootPreserved: true,
      liveOwnerLockPreserved: true,
      noResidue: true,
      albumCount: 1_001,
    });
    expect(elapsedMs).toBeLessThan(60_000);

    const stored = JSON.parse(await fs.readFile(target, 'utf8'));
    expect(stored.futureRoot).toEqual({ keep: true });
    expect(stored.albums[0].futureAlbum).toEqual(['keep']);
    expect(stored.albums[0].members[0].futureMember).toEqual({ keep: true });
  }, 125_000);

  it('lets WPF upgrade the known versionless empty store without a read-time rewrite', async () => {
    const target = path.join(root, 'albums.json');
    const resultPath = path.join(root, 'wpf-legacy-result.json');
    const wpfImage = path.join(root, 'wpf-legacy.jpg');
    const legacy = JSON.stringify({ albums: [], futureRoot: { keep: true } });
    await fs.writeFile(target, legacy, 'utf8');

    const browserRead = await readAlbums(target);
    expect(browserRead).toMatchObject({ ok: true, document: { version: 1, revision: 0, albums: [] } });
    expect(await fs.readFile(target, 'utf8')).toBe(legacy);

    await execFileAsync(wpfExecutable, [
      '--album-store-smoke', resultPath,
      '--album-path', target,
      '--member-path', wpfImage,
    ], { windowsHide: true, timeout: 30_000 });
    const wpfResult = JSON.parse(await fs.readFile(resultPath, 'utf8'));
    expect(wpfResult).toMatchObject({
      ok: true,
      initialRevision: 0,
      finalRevision: 4,
      unknownRootPreserved: true,
      legacyMigration: { Ok: true },
    });
    const stored = JSON.parse(await fs.readFile(target, 'utf8'));
    expect(stored).toMatchObject({ version: 1, revision: 4, futureRoot: { keep: true } });
  });

  it('interleaves both runtimes without losing fields or revisions', async () => {
    const target = path.join(root, 'albums.json');
    const resultPath = path.join(root, 'wpf-result.json');
    const browserImage = path.join(root, 'browser.jpg');
    const wpfImage = path.join(root, 'wpf.jpg');

    const browserCreated = await mutateAlbums(target, {
      action: 'create',
      name: 'Browser before WPF',
      albumId: 'browser-before',
      expectedRevision: 0,
    });
    await mutateAlbums(target, {
      action: 'add',
      albumId: 'browser-before',
      paths: [browserImage],
      expectedRevision: browserCreated.document!.revision,
    });
    const seeded = JSON.parse(await fs.readFile(target, 'utf8'));
    seeded.futureRoot = { keep: true };
    seeded.albums[0].futureAlbum = ['keep'];
    seeded.albums[0].members[0].futureMember = 42;
    await fs.writeFile(target, `${JSON.stringify(seeded, null, 2)}\n`, 'utf8');

    try {
      await execFileAsync(wpfExecutable, [
        '--album-store-smoke', resultPath,
        '--album-path', target,
        '--member-path', wpfImage,
      ], { windowsHide: true, timeout: 30_000 });
    } catch (error) {
      const report = await fs.readFile(resultPath, 'utf8').catch(() => '<result missing>');
      throw new Error(`${error instanceof Error ? error.message : String(error)}\n${report}`);
    }
    const wpfResult = JSON.parse(await fs.readFile(resultPath, 'utf8'));
    expect(wpfResult).toMatchObject({
      ok: true,
      initialRevision: 2,
      finalRevision: 6,
      created: 'Succeeded',
      added: 'Succeeded',
      updated: 'Succeeded',
      cleaned: 'Succeeded',
      stale: 'Conflict',
      unknownRootPreserved: true,
      noResidue: true,
      albumCount: 2,
    });

    const browserAfter = await mutateAlbums(target, {
      action: 'create',
      name: 'Browser after WPF',
      albumId: 'browser-after',
      expectedRevision: 6,
    });
    expect(browserAfter).toMatchObject({ ok: true, document: { revision: 7 } });
    const final = await readAlbums(target);
    expect(final).toMatchObject({ ok: true, document: { revision: 7 } });
    if (!final.ok) throw new Error(final.error);
    expect(final.document.albums.map((album) => album.id)).toEqual(expect.arrayContaining([
      'browser-before',
      wpfResult.albumId,
      'browser-after',
    ]));
    const stored = JSON.parse(await fs.readFile(target, 'utf8'));
    expect(stored.futureRoot).toEqual({ keep: true });
    expect(stored.albums.find((album: { id: string }) => album.id === 'browser-before').futureAlbum).toEqual(['keep']);
    expect(stored.albums.find((album: { id: string }) => album.id === 'browser-before').members[0].futureMember).toBe(42);
    expect((await fs.readdir(root)).filter((name) => name.endsWith('.lock') || name.endsWith('.tmp'))).toEqual([]);
  });

  it('serializes simultaneous Browser and WPF writers without lost updates', async () => {
    const target = path.join(root, 'albums.json');
    const resultPath = path.join(root, 'wpf-concurrent-result.json');
    const readyPath = path.join(root, 'wpf-ready');
    const goPath = path.join(root, 'start');
    const countPerRuntime = 16;

    const wpfRun = execFileAsync(wpfExecutable, [
      '--album-concurrent-writer-smoke', resultPath,
      '--album-path', target,
      '--count', String(countPerRuntime),
      '--prefix', 'wpf',
      '--ready-path', readyPath,
      '--go-path', goPath,
    ], { windowsHide: true, timeout: 30_000 });
    await waitForFile(readyPath);
    const browserRun = Promise.all(Array.from({ length: countPerRuntime }, (_, index) => mutateAlbums(target, {
      action: 'create' as const,
      name: `Browser concurrent ${index}`,
      albumId: `browser-${String(index).padStart(3, '0')}`,
    })));
    await fs.writeFile(goPath, 'go', 'utf8');

    const [browserResults] = await Promise.all([browserRun, wpfRun]);
    expect(browserResults.every((result) => result.ok)).toBe(true);
    const wpfResult = JSON.parse(await fs.readFile(resultPath, 'utf8'));
    expect(wpfResult).toMatchObject({ ok: true, count: countPerRuntime });

    const final = await readAlbums(target);
    expect(final).toMatchObject({ ok: true, document: { revision: countPerRuntime * 2 } });
    if (!final.ok) throw new Error(final.error);
    expect(final.document.albums).toHaveLength(countPerRuntime * 2);
    expect(final.document.albums.filter((album) => album.id.startsWith('browser-'))).toHaveLength(countPerRuntime);
    expect(final.document.albums.filter((album) => album.id.startsWith('wpf-'))).toHaveLength(countPerRuntime);
    expect((await fs.readdir(root)).filter((name) => name.endsWith('.lock') || name.endsWith('.tmp'))).toEqual([]);
  });

  it('serializes Browser add and WPF remove on the same Album without losing either intent', async () => {
    const target = path.join(root, 'albums.json');
    const resultPath = path.join(root, 'wpf-member-result.json');
    const readyPath = path.join(root, 'wpf-member-ready');
    const goPath = path.join(root, 'wpf-member-go');
    const removedPath = path.join(root, 'remove-by-wpf.jpg');
    const addedPath = path.join(root, 'add-by-browser.jpg');
    await mutateAlbums(target, { action: 'create', name: 'Shared race', albumId: 'shared-race' });
    await mutateAlbums(target, { action: 'add', albumId: 'shared-race', paths: [removedPath] });

    const wpfRun = execFileAsync(wpfExecutable, [
      '--album-member-writer-smoke', resultPath,
      '--album-path', target,
      '--album-id', 'shared-race',
      '--member-path', removedPath,
      '--ready-path', readyPath,
      '--go-path', goPath,
    ], { windowsHide: true, timeout: 30_000 });
    await waitForFile(readyPath);

    const [browserResult] = await Promise.all([
      mutateAlbums(target, { action: 'add', albumId: 'shared-race', paths: [addedPath] }),
      (async () => {
        await fs.writeFile(goPath, 'go', 'utf8');
        await wpfRun;
      })(),
    ]);
    expect(browserResult).toMatchObject({ ok: true, changed: true });
    const wpfResult = JSON.parse(await fs.readFile(resultPath, 'utf8'));
    expect(wpfResult).toMatchObject({ ok: true, status: 'Succeeded', removed: true });

    const final = await readAlbums(target);
    expect(final).toMatchObject({ ok: true, document: { revision: 4 } });
    if (!final.ok) throw new Error(final.error);
    const members = final.document.albums.find((album) => album.id === 'shared-race')!.members;
    expect(members.map((member) => member.imagePath)).toContain(path.resolve(addedPath));
    expect(members.map((member) => member.imagePath)).not.toContain(path.resolve(removedPath));
    expect((await fs.readdir(root)).filter((name) => name.endsWith('.lock') || name.endsWith('.tmp'))).toEqual([]);
  }, 30_000);
});
