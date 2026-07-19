import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { DELETE, GET, PUT } from './route';

let root = '';
let target = '';
let previousOverride: string | undefined;

function request(method: 'PUT' | 'DELETE', body: unknown) {
  return new Request('http://isolated.test/api/search-history', {
    method,
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
}

async function storedDocument() {
  return JSON.parse(await fs.readFile(target, 'utf8')) as {
    version: number;
    entries: string[];
    updatedAtUtc: string;
    [key: string]: unknown;
  };
}

beforeEach(async () => {
  previousOverride = process.env.PVU_SEARCH_HISTORY_PATH;
  root = await fs.mkdtemp(path.join(os.tmpdir(), 'photoviewer-search-history-route-'));
  target = path.join(root, 'search-history.json');
  process.env.PVU_SEARCH_HISTORY_PATH = target;
});

afterEach(async () => {
  if (previousOverride === undefined) delete process.env.PVU_SEARCH_HISTORY_PATH;
  else process.env.PVU_SEARCH_HISTORY_PATH = previousOverride;
  await fs.rm(root, { recursive: true, force: true });
});

describe('search history shared route', () => {
  it('normalizes complete queries, removes empty tokens, deduplicates NFKC case-insensitively, and promotes repeats', async () => {
    expect((await PUT(request('PUT', { query: '  ＣＡＴ ,, Dog  ' }))).status).toBe(200);
    expect((await PUT(request('PUT', { query: 'landscape, night' }))).status).toBe(200);
    expect((await PUT(request('PUT', { query: 'cat, dog' }))).status).toBe(200);

    const response = await GET();
    expect(response.status).toBe(200);
    expect(await response.json()).toMatchObject({
      ok: true,
      entries: ['cat, dog', 'landscape, night'],
      malformed: false,
      futureVersion: false,
    });
    expect(await storedDocument()).toMatchObject({
      version: 1,
      entries: ['cat, dog', 'landscape, night'],
    });
  });

  it('keeps only the newest 50 entries', async () => {
    for (let index = 0; index < 55; index += 1) {
      const response = await PUT(request('PUT', { query: `query ${index}` }));
      expect(response.status).toBe(200);
    }

    const document = await storedDocument();
    expect(document.entries).toHaveLength(50);
    expect(document.entries[0]).toBe('query 54');
    expect(document.entries.at(-1)).toBe('query 5');
  });

  it('deletes one normalized identity or clears all while preserving unknown root fields', async () => {
    await fs.writeFile(target, `${JSON.stringify({
      version: 1,
      entries: ['ＣＡＴ, Dog', 'landscape'],
      updatedAtUtc: '2026-07-19T00:00:00.000Z',
      ownerMarker: { keep: true },
    })}\n`, 'utf8');

    const deleted = await DELETE(request('DELETE', { query: 'cat, dog' }));
    expect(deleted.status).toBe(200);
    expect(await deleted.json()).toMatchObject({ ok: true, entries: ['landscape'] });
    expect(await storedDocument()).toMatchObject({ ownerMarker: { keep: true } });

    const cleared = await DELETE(request('DELETE', { clear: true }));
    expect(cleared.status).toBe(200);
    expect(await cleared.json()).toMatchObject({ ok: true, entries: [] });
    expect(await storedDocument()).toMatchObject({ ownerMarker: { keep: true } });
  });

  it.each([
    ['malformed', '{ definitely-not-json', false, true],
    ['future', JSON.stringify({ version: 2, entries: ['future'] }), true, false],
  ])('shows empty and protects a %s document from every mutation', async (_name, content, futureVersion, malformed) => {
    await fs.writeFile(target, content, 'utf8');
    const before = await fs.readFile(target, 'utf8');

    const read = await GET();
    expect(read.status).toBe(200);
    expect(await read.json()).toMatchObject({
      ok: false,
      entries: [],
      malformed,
      futureVersion,
    });

    expect((await PUT(request('PUT', { query: 'must not replace' }))).status).toBe(409);
    expect((await DELETE(request('DELETE', { clear: true }))).status).toBe(409);
    expect(await fs.readFile(target, 'utf8')).toBe(before);
    expect(await fs.readdir(root)).toEqual(['search-history.json']);
  });

  it('rereads under the shared lock so concurrent commits are not lost and leaves no atomic residue', async () => {
    const responses = await Promise.all(Array.from({ length: 30 }, (_, index) => (
      PUT(request('PUT', { query: `parallel ${index}` }))
    )));
    expect(responses.every((response) => response.status === 200)).toBe(true);

    const document = await storedDocument();
    expect(new Set(document.entries)).toEqual(new Set(Array.from({ length: 30 }, (_, index) => `parallel ${index}`)));
    expect((await fs.readdir(root)).sort()).toEqual(['search-history.json']);
  });

  it('rejects empty and oversized incoming queries without creating shared state', async () => {
    expect((await PUT(request('PUT', { query: ' , , ' }))).status).toBe(400);
    expect((await PUT(request('PUT', { query: 'x'.repeat(32_769) }))).status).toBe(400);
    await expect(fs.stat(target)).rejects.toMatchObject({ code: 'ENOENT' });
  });
});
