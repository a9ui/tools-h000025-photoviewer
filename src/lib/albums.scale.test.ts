import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterAll, beforeAll, describe, expect, it } from 'vitest';

import { mutateAlbums, readAlbums } from './albums';

const MEMBER_COUNT = 100_000;
let root = '';
let target = '';

beforeAll(async () => {
  root = await fs.mkdtemp(path.join(os.tmpdir(), 'pvu-albums-scale-'));
  target = path.join(root, 'albums.json');
  const stamp = '2026-07-20T00:00:00.000Z';
  const members = Array.from({ length: MEMBER_COUNT }, (_, index) => ({
    id: `member-${index}`,
    imagePath: path.join(root, 'catalog', `${String(index).padStart(6, '0')}.jpg`),
    addedAtUtc: stamp,
    ...(index === 0 || index === MEMBER_COUNT - 1 ? { futureMember: `edge-${index}` } : {}),
  }));
  await fs.writeFile(target, JSON.stringify({
    version: 1,
    revision: 1,
    updatedAtUtc: stamp,
    futureRoot: { keep: true },
    recentAlbumIds: ['scale'],
    albums: [{
      id: 'scale',
      name: '100k Album',
      pinned: false,
      coverMemberId: 'member-0',
      createdAtUtc: stamp,
      updatedAtUtc: stamp,
      revision: 1,
      members,
    }],
  }), 'utf8');
}, 60_000);

afterAll(async () => {
  const resolved = path.resolve(root);
  const temp = path.resolve(os.tmpdir());
  if (resolved.startsWith(`${temp}${path.sep}pvu-albums-scale-`)) {
    await fs.rm(resolved, { recursive: true, force: true });
  }
});

describe('Album store catalog scale', () => {
  it('reads and atomically mutates 100,000 members without dropping edge or unknown fields', async () => {
    const read = await readAlbums(target);
    expect(read).toMatchObject({ ok: true, document: { revision: 1, albums: [{ members: { length: MEMBER_COUNT } }] } });
    if (!read.ok) throw new Error(read.error);
    expect(read.document.albums[0].members[0].futureMember).toBe('edge-0');
    expect(read.document.albums[0].members[MEMBER_COUNT - 1].futureMember).toBe(`edge-${MEMBER_COUNT - 1}`);

    const updated = await mutateAlbums(target, {
      action: 'update',
      albumId: 'scale',
      pinned: true,
      expectedRevision: 1,
    });
    expect(updated).toMatchObject({ ok: true, changed: true, document: { revision: 2 }, album: { revision: 2, pinned: true } });
    expect(updated.album?.members).toHaveLength(MEMBER_COUNT);

    const stored = JSON.parse(await fs.readFile(target, 'utf8'));
    expect(stored.futureRoot).toEqual({ keep: true });
    expect(stored.albums[0].members[0].futureMember).toBe('edge-0');
    expect(stored.albums[0].members[MEMBER_COUNT - 1].futureMember).toBe(`edge-${MEMBER_COUNT - 1}`);
    expect((await fs.readdir(root)).filter((name) => name.endsWith('.lock') || name.endsWith('.tmp'))).toEqual([]);
  }, 60_000);
});
