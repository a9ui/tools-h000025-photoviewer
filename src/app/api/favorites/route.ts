import { NextResponse } from 'next/server';
import { promises as fs } from 'fs';
import path from 'path';

const MAX_FAVORITE_LEVEL = 5;

function favoritesPath() {
  return process.env.PVU_FAVORITES_PATH
    ? path.resolve(process.env.PVU_FAVORITES_PATH)
    : path.join(/*turbopackIgnore: true*/ process.cwd(), '.cache', 'favorites.json');
}

function isObjectMap(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function normalizeStoredFavorites(value: unknown): Record<string, number> | null {
  if (!isObjectMap(value)) return null;
  const normalized: Record<string, number> = {};
  for (const [id, levelValue] of Object.entries(value)) {
    if (!id) continue;
    const level = typeof levelValue === 'number'
      ? Math.max(0, Math.min(MAX_FAVORITE_LEVEL, Math.trunc(levelValue)))
      : levelValue
        ? 1
        : 0;
    if (level > 0) normalized[id] = level;
  }
  return normalized;
}

function normalizeIncomingFavorites(value: unknown):
  { ok: true; favorites: Record<string, number> } |
  { ok: false; error: string } {
  if (!isObjectMap(value)) {
    return { ok: false, error: 'favorites must be an object map.' };
  }

  const normalized: Record<string, number> = {};
  for (const [id, rawLevel] of Object.entries(value)) {
    if (!id) return { ok: false, error: 'Favorite paths must not be empty.' };
    if (typeof rawLevel !== 'number' && typeof rawLevel !== 'boolean') {
      return { ok: false, error: `Favorite level for ${id} must be a number or boolean.` };
    }
    if (typeof rawLevel === 'number' && !Number.isFinite(rawLevel)) {
      return { ok: false, error: `Favorite level for ${id} must be finite.` };
    }
    const level = typeof rawLevel === 'boolean'
      ? (rawLevel ? 1 : 0)
      : Math.max(0, Math.min(MAX_FAVORITE_LEVEL, Math.trunc(rawLevel)));
    if (level > 0) normalized[id] = level;
  }
  return { ok: true, favorites: normalized };
}

async function readFavorites(): Promise<
  { ok: true; favorites: Record<string, number>; malformed: false } |
  { ok: false; favorites: Record<string, number>; malformed: true; error: string }
> {
  const target = favoritesPath();
  try {
    const raw = await fs.readFile(target, 'utf8');
    const favorites = normalizeStoredFavorites(JSON.parse(raw));
    if (favorites === null) {
      return { ok: false, favorites: {}, malformed: true, error: 'favorites.json is not an object map.' };
    }
    return { ok: true, favorites, malformed: false };
  } catch (error) {
    if ((error as NodeJS.ErrnoException)?.code === 'ENOENT') {
      return { ok: true, favorites: {}, malformed: false };
    }
    return {
      ok: false,
      favorites: {},
      malformed: true,
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

async function writeFavorites(favorites: Record<string, number>) {
  const target = favoritesPath();
  const dir = path.dirname(target);
  const temp = path.join(dir, `favorites-${process.pid}-${Date.now()}.tmp`);
  await fs.mkdir(dir, { recursive: true });
  try {
    await fs.writeFile(temp, `${JSON.stringify(favorites)}\n`, 'utf8');
    await fs.rename(temp, target);
  } finally {
    await fs.unlink(temp).catch(() => {});
  }
}

export async function GET() {
  const result = await readFavorites();
  return NextResponse.json({
    favorites: result.favorites,
    malformed: result.malformed,
    error: result.ok ? undefined : result.error,
  });
}

export async function PUT(req: Request) {
  const current = await readFavorites();
  if (!current.ok) {
    return NextResponse.json({
      ok: false,
      error: 'Shared favorites JSON is malformed; refusing to overwrite it.',
      favorites: current.favorites,
      malformed: true,
    }, { status: 409 });
  }

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ ok: false, error: 'Request body must be valid JSON.' }, { status: 400 });
  }
  if (!isObjectMap(body) || !Object.hasOwn(body, 'favorites')) {
    return NextResponse.json({ ok: false, error: 'Request body must include favorites.' }, { status: 400 });
  }

  const incoming = normalizeIncomingFavorites(body.favorites);
  if (!incoming.ok) {
    return NextResponse.json({ ok: false, error: incoming.error }, { status: 400 });
  }

  await writeFavorites(incoming.favorites);
  return NextResponse.json({ ok: true, favorites: incoming.favorites, malformed: false });
}
