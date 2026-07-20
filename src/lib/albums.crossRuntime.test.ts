import { execFile } from 'child_process';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import { promisify } from 'util';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { mutateAlbums, readAlbums } from './albums';

const execFileAsync = promisify(execFile);
const wpfExecutable = path.resolve('local-native/PhotoViewer.Wpf/bin/Release/net8.0-windows/PhotoViewer.Wpf.exe');
const canRunWpf = process.platform === 'win32' && await fs.stat(wpfExecutable).then(() => true).catch(() => false);
const crossRuntimeDescribe = canRunWpf ? describe : describe.skip;

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
});
