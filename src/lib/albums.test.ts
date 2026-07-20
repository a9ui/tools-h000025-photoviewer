import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { mutateAlbums, readAlbums } from './albums';

let root = '';
let target = '';

beforeEach(async () => {
  root = await fs.mkdtemp(path.join(os.tmpdir(), 'pvu-albums-'));
  target = path.join(root, 'albums.json');
});

afterEach(async () => {
  await fs.rm(root, { recursive: true, force: true });
});

describe('shared Album store', () => {
  it('treats a missing store as empty without publishing a file', async () => {
    const result = await readAlbums(target);
    expect(result).toMatchObject({ ok: true, exists: false, document: { version: 1, revision: 0, albums: [], recentAlbumIds: [] } });
    await expect(fs.stat(target)).rejects.toMatchObject({ code: 'ENOENT' });
  });

  it('supports operation mutations with monotonic revisions and idempotent membership', async () => {
    const first = await mutateAlbums(target, { action: 'create', name: '  Trips  ', albumId: 'album-1', expectedRevision: 0 });
    expect(first).toMatchObject({ ok: true, changed: true, document: { revision: 1 }, album: { id: 'album-1', name: 'Trips', revision: 1 } });

    const imagePath = path.join(root, 'one.jpg');
    const added = await mutateAlbums(target, { action: 'add', albumId: 'album-1', paths: [imagePath, imagePath], expectedRevision: 1 });
    expect(added).toMatchObject({ ok: true, changed: true, document: { revision: 2 }, album: { revision: 2 } });
    expect(added.album?.members).toHaveLength(1);

    const duplicate = await mutateAlbums(target, { action: 'add', albumId: 'album-1', paths: [imagePath], expectedRevision: 2 });
    expect(duplicate).toMatchObject({ ok: true, changed: false, document: { revision: 2 } });

    const memberId = added.album!.members[0].id;
    const updated = await mutateAlbums(target, { action: 'update', albumId: 'album-1', pinned: true, coverMemberId: memberId, expectedRevision: 2 });
    expect(updated).toMatchObject({ ok: true, changed: true, document: { revision: 3 }, album: { revision: 3, pinned: true, coverMemberId: memberId } });

    const removed = await mutateAlbums(target, { action: 'remove', albumId: 'album-1', memberIds: [memberId], expectedRevision: 3 });
    expect(removed).toMatchObject({ ok: true, changed: true, document: { revision: 4 }, album: { revision: 4, coverMemberId: null, members: [] } });

    const deleted = await mutateAlbums(target, { action: 'delete', albumId: 'album-1', expectedRevision: 4 });
    expect(deleted).toMatchObject({ ok: true, changed: true, document: { revision: 5, albums: [], recentAlbumIds: [] } });
    expect((await fs.readdir(root)).filter((name) => name.endsWith('.tmp') || name.endsWith('.lock'))).toEqual([]);
  });

  it('preserves unknown root, Album, and member fields through later operations', async () => {
    const original = {
      version: 1,
      revision: 7,
      updatedAtUtc: '2026-07-20T00:00:00.000Z',
      futureRoot: { keep: true },
      recentAlbumIds: ['album-1'],
      albums: [{
        id: 'album-1',
        name: 'Keep fields',
        pinned: false,
        coverMemberId: 'member-1',
        createdAtUtc: '2026-07-20T00:00:00.000Z',
        updatedAtUtc: '2026-07-20T00:00:00.000Z',
        revision: 3,
        futureAlbum: ['keep'],
        members: [{
          id: 'member-1',
          imagePath: path.join(root, 'one.jpg'),
          addedAtUtc: '2026-07-20T00:00:00.000Z',
          futureMember: 42,
        }],
      }],
    };
    await fs.writeFile(target, JSON.stringify(original), 'utf8');

    const result = await mutateAlbums(target, { action: 'update', albumId: 'album-1', name: 'Renamed', expectedRevision: 7 });
    const stored = JSON.parse(await fs.readFile(target, 'utf8'));
    expect(result).toMatchObject({ ok: true, document: { revision: 8 }, album: { revision: 4 } });
    expect(stored.futureRoot).toEqual({ keep: true });
    expect(stored.albums[0].futureAlbum).toEqual(['keep']);
    expect(stored.albums[0].members[0].futureMember).toBe(42);
  });

  it.each([
    ['malformed JSON', '{not-json', { malformed: true, futureVersion: false }],
    ['future version', JSON.stringify({ version: 2, revision: 0, updatedAtUtc: '2026-07-20T00:00:00Z', albums: [], recentAlbumIds: [] }), { malformed: false, futureVersion: true }],
    ['duplicate Album ids', JSON.stringify({
      version: 1,
      revision: 1,
      updatedAtUtc: '2026-07-20T00:00:00Z',
      recentAlbumIds: [],
      albums: [0, 1].map(() => ({ id: 'same', name: 'Same', pinned: false, coverMemberId: null, createdAtUtc: '2026-07-20T00:00:00Z', updatedAtUtc: '2026-07-20T00:00:00Z', revision: 1, members: [] })),
    }), { malformed: true, futureVersion: false }],
  ])('refuses to overwrite protected state: %s', async (_name, original, expected) => {
    await fs.writeFile(target, original, 'utf8');
    const read = await readAlbums(target);
    expect(read).toMatchObject({ ok: false, ...expected });
    const mutation = await mutateAlbums(target, { action: 'create', name: 'No overwrite' });
    expect(mutation).toMatchObject({ ok: false, ...expected, changed: false });
    expect(await fs.readFile(target, 'utf8')).toBe(original);
  });

  it('rejects stale revisions without mutating the latest document', async () => {
    await mutateAlbums(target, { action: 'create', name: 'First', albumId: 'first' });
    const before = await fs.readFile(target, 'utf8');
    const result = await mutateAlbums(target, { action: 'create', name: 'Stale', expectedRevision: 0 });
    expect(result).toMatchObject({ ok: false, conflict: true, changed: false, document: { revision: 1 } });
    expect(await fs.readFile(target, 'utf8')).toBe(before);
  });

  it('serializes concurrent latest-on-disk operations without lost updates', async () => {
    const results = await Promise.all(Array.from({ length: 20 }, (_, index) => mutateAlbums(target, {
      action: 'create' as const,
      name: `Album ${index}`,
      albumId: `album-${index}`,
    })));
    const stored = await readAlbums(target);
    expect(results.every((result) => result.ok)).toBe(true);
    expect(stored).toMatchObject({ ok: true, document: { revision: 20 } });
    if (!stored.ok) throw new Error(stored.error);
    expect(stored.document.albums.map((album) => album.id).sort()).toEqual(Array.from({ length: 20 }, (_, index) => `album-${index}`).sort());
    await expect(fs.stat(`${target}.lock`)).rejects.toMatchObject({ code: 'ENOENT' });
  });

  it('cleans a recycled source identity from every Album in one document revision', async () => {
    const imagePath = path.join(root, 'shared.jpg');
    await mutateAlbums(target, { action: 'create', name: 'One', albumId: 'one' });
    await mutateAlbums(target, { action: 'create', name: 'Two', albumId: 'two' });
    await mutateAlbums(target, { action: 'add', albumId: 'one', paths: [imagePath] });
    await mutateAlbums(target, { action: 'add', albumId: 'two', paths: [imagePath] });
    const before = await readAlbums(target);
    if (!before.ok) throw new Error(before.error);
    const beforeAlbumRevisions = before.document.albums.map((album) => album.revision);

    const result = await mutateAlbums(target, { action: 'cleanupPaths', paths: [imagePath], expectedRevision: before.document.revision });
    expect(result).toMatchObject({ ok: true, changed: true, document: { revision: before.document.revision + 1 } });
    expect(result.document!.albums.every((album) => album.members.length === 0)).toBe(true);
    expect(result.document!.albums.map((album) => album.revision)).toEqual(beforeAlbumRevisions.map((revision) => revision + 1));
  });

  it('rejects paths that are not supported by both Browser and WPF', async () => {
    await mutateAlbums(target, { action: 'create', name: 'Shared', albumId: 'shared' });
    const result = await mutateAlbums(target, {
      action: 'add',
      albumId: 'shared',
      paths: [path.join(root, 'unsupported.bmp')],
    });
    expect(result).toMatchObject({ ok: false, changed: false, error: 'paths must contain bounded absolute supported image paths.' });
  });
});
