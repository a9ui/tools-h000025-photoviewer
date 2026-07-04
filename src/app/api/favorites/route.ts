import { NextResponse } from 'next/server';
import { promises as fs } from 'fs';
import path from 'path';

const FAVORITES_PATH = path.join(/*turbopackIgnore: true*/ process.cwd(), '.cache', 'favorites.json');
const MAX_FAVORITE_LEVEL = 5;

function normalizeFavorites(value: unknown): Record<string, number> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
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

async function readFavorites(): Promise<Record<string, number>> {
  try {
    const raw = await fs.readFile(FAVORITES_PATH, 'utf8');
    return normalizeFavorites(JSON.parse(raw));
  } catch {
    return {};
  }
}

async function writeFavorites(favorites: Record<string, number>) {
  await fs.mkdir(path.dirname(FAVORITES_PATH), { recursive: true });
  await fs.writeFile(FAVORITES_PATH, JSON.stringify(favorites, null, 2), 'utf8');
}

export async function GET() {
  return NextResponse.json({ favorites: await readFavorites() });
}

export async function PUT(req: Request) {
  const body = await req.json().catch(() => ({}));
  const favorites = normalizeFavorites((body as { favorites?: unknown }).favorites);
  await writeFavorites(favorites);
  return NextResponse.json({ ok: true, favorites });
}
