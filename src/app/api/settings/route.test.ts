import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { DEFAULT_KEY_BINDINGS } from '@/lib/types';
import { GET, PUT } from './route';

let root = '';
let target = '';
const previousOverride = process.env.PVU_SETTINGS_PATH;

beforeEach(async () => {
  root = await fs.mkdtemp(path.join(os.tmpdir(), 'pvu-settings-route-'));
  target = path.join(root, 'settings.json');
  process.env.PVU_SETTINGS_PATH = target;
});

afterEach(async () => {
  if (previousOverride === undefined) delete process.env.PVU_SETTINGS_PATH;
  else process.env.PVU_SETTINGS_PATH = previousOverride;
  await fs.rm(root, { recursive: true, force: true });
});

function putRequest(body: string) {
  return new Request('http://127.0.0.1/api/settings', {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body,
  });
}

describe('settings route write safety', () => {
  it('returns explicit defaults without creating a missing file', async () => {
    const response = await GET();
    const body = await response.json();

    expect(body).toMatchObject({
      keyBindings: DEFAULT_KEY_BINDINGS,
      confirmBeforeDelete: true,
      malformed: false,
    });
    await expect(fs.stat(target)).rejects.toMatchObject({ code: 'ENOENT' });
  });

  it('atomically merges a partial update while preserving unknown future fields', async () => {
    await fs.writeFile(target, JSON.stringify({
      futureSetting: { enabled: true },
      confirmBeforeDelete: true,
      keyBindings: { nextImage: 'n', futureAction: 'q' },
    }), 'utf8');

    const response = await PUT(putRequest(JSON.stringify({
      confirmBeforeDelete: false,
      keyBindings: { prevImage: 'p' },
    })));
    const stored = JSON.parse(await fs.readFile(target, 'utf8'));

    expect(response.status).toBe(200);
    expect(await response.json()).toMatchObject({
      ok: true,
      confirmBeforeDelete: false,
      keyBindings: { nextImage: 'n', prevImage: 'p' },
      malformed: false,
    });
    expect(stored).toMatchObject({
      futureSetting: { enabled: true },
      confirmBeforeDelete: false,
      keyBindings: { nextImage: 'n', prevImage: 'p', futureAction: 'q' },
    });
    expect((await fs.readdir(root)).filter((name) => name.endsWith('.tmp'))).toEqual([]);
    await expect(fs.stat(`${target}.lock`)).rejects.toMatchObject({ code: 'ENOENT' });
  });

  it('defaults a missing filmstrip binding and preserves a legacy T assignment with a conflict-free fallback', async () => {
    await fs.writeFile(target, JSON.stringify({
      keyBindings: { nextImage: 't' },
    }), 'utf8');

    const response = await GET();
    const body = await response.json();

    expect(response.status).toBe(200);
    expect(body).toMatchObject({
      malformed: false,
      keyBindings: {
        nextImage: 't',
        toggleFilmstrip: 'b',
      },
    });
    expect(JSON.parse(await fs.readFile(target, 'utf8'))).toEqual({
      keyBindings: { nextImage: 't' },
    });
  });

  it.each([
    ['invalid JSON', '{'],
    ['array body', JSON.stringify([])],
    ['empty body', JSON.stringify({})],
    ['invalid confirmation', JSON.stringify({ confirmBeforeDelete: 'no' })],
    ['invalid bindings map', JSON.stringify({ keyBindings: [] })],
    ['invalid binding value', JSON.stringify({ keyBindings: { nextImage: 7 } })],
    ['empty binding value', JSON.stringify({ keyBindings: { nextImage: '' } })],
  ])('rejects %s without replacing the existing file', async (_name, body) => {
    const original = '{"confirmBeforeDelete":true}\n';
    await fs.writeFile(target, original, 'utf8');

    const response = await PUT(putRequest(body));

    expect(response.status).toBe(400);
    expect(await fs.readFile(target, 'utf8')).toBe(original);
  });

  it('reports and preserves an existing malformed shared file', async () => {
    const malformed = '{not-json';
    await fs.writeFile(target, malformed, 'utf8');

    const getResponse = await GET();
    expect(await getResponse.json()).toMatchObject({
      keyBindings: DEFAULT_KEY_BINDINGS,
      confirmBeforeDelete: true,
      malformed: true,
    });

    const putResponse = await PUT(putRequest(JSON.stringify({ confirmBeforeDelete: false })));
    expect(putResponse.status).toBe(409);
    expect(await fs.readFile(target, 'utf8')).toBe(malformed);
  });

  it('treats invalid stored known fields as malformed instead of silently replacing them', async () => {
    const original = JSON.stringify({ confirmBeforeDelete: 'sometimes' });
    await fs.writeFile(target, original, 'utf8');

    const response = await PUT(putRequest(JSON.stringify({ confirmBeforeDelete: true })));

    expect(response.status).toBe(409);
    expect(await fs.readFile(target, 'utf8')).toBe(original);
  });

  it('refuses a normalized key collision without replacing existing bindings', async () => {
    const original = JSON.stringify({
      confirmBeforeDelete: true,
      keyBindings: { nextImage: 'ArrowRight', toggleFavorite: 'f' },
    });
    await fs.writeFile(target, original, 'utf8');

    const response = await PUT(putRequest(JSON.stringify({
      keyBindings: { nextImage: 'F' },
    })));

    expect(response.status).toBe(409);
    expect(await response.json()).toMatchObject({
      ok: false,
      error: 'Key bindings must not assign the same key to multiple actions.',
    });
    expect(await fs.readFile(target, 'utf8')).toBe(original);
  });

  it('preserves an existing collision until an explicit valid replacement resolves it', async () => {
    const original = JSON.stringify({
      keyBindings: { nextImage: 'f', toggleFavorite: 'F' },
    });
    await fs.writeFile(target, original, 'utf8');

    const unrelated = await PUT(putRequest(JSON.stringify({ confirmBeforeDelete: false })));
    expect(unrelated.status).toBe(200);
    expect(JSON.parse(await fs.readFile(target, 'utf8'))).toMatchObject({
      keyBindings: { nextImage: 'f', toggleFavorite: 'F' },
      confirmBeforeDelete: false,
    });

    const replacement = await PUT(putRequest(JSON.stringify({
      keyBindings: { toggleFavorite: 'g' },
    })));
    expect(replacement.status).toBe(200);
    expect(JSON.parse(await fs.readFile(target, 'utf8'))).toMatchObject({
      keyBindings: { nextImage: 'f', toggleFavorite: 'g' },
    });
  });

  it('serializes concurrent partial updates without dropping either field', async () => {
    await fs.writeFile(target, JSON.stringify({
      confirmBeforeDelete: true,
      keyBindings: {},
    }), 'utf8');

    const [first, second] = await Promise.all([
      PUT(putRequest(JSON.stringify({ confirmBeforeDelete: false }))),
      PUT(putRequest(JSON.stringify({ keyBindings: { nextImage: 'n' } }))),
    ]);
    const stored = JSON.parse(await fs.readFile(target, 'utf8'));

    expect(first.status).toBe(200);
    expect(second.status).toBe(200);
    expect(stored).toMatchObject({
      confirmBeforeDelete: false,
      keyBindings: { nextImage: 'n' },
    });
  });
});
