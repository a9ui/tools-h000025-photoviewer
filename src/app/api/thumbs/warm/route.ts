import { NextRequest, NextResponse } from 'next/server';
import path from 'path';
import { getIndex } from '@/lib/indexer';
import { parseDirSet } from '@/lib/pathSet';
import { enqueueThumbnails, getThumbnailWarmupState, startThumbnailWarmup } from '@/lib/thumbnailCache';

export const dynamic = 'force-dynamic';

function normalizeDirPaths(value: unknown) {
  if (typeof value !== 'string' || !value.trim()) return [];
  const paths: string[] = [];
  for (const item of parseDirSet(value)) {
    try {
      paths.push(path.resolve(item));
    } catch {
      // ignore invalid path fragments
    }
  }
  return paths;
}

function isInsideDir(filePath: string, dirPath: string) {
  const rel = path.relative(dirPath, filePath);
  return !rel.startsWith('..') && !path.isAbsolute(rel);
}

function isInsideAnyDir(filePath: string, dirPaths: string[]) {
  return dirPaths.length === 0 || dirPaths.some((dirPath) => isInsideDir(filePath, dirPath));
}

export async function GET() {
  return NextResponse.json({ warmup: getThumbnailWarmupState() });
}

export async function POST(request: NextRequest) {
  const body = await request.json().catch(() => ({}));
  const dirs = normalizeDirPaths((body as { dir?: unknown }).dir);
  const directPaths = Array.isArray((body as { paths?: unknown }).paths)
    ? (body as { paths: unknown[] }).paths
      .filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
      .map((item) => path.resolve(item))
    : [];
  const priorityRaw = String((body as { priority?: unknown }).priority ?? '').toLowerCase();
  const priority = priorityRaw === 'focused' || priorityRaw === 'current' || priorityRaw === 'modal'
    ? -1
    : priorityRaw === 'visible' || priorityRaw === 'high'
      ? 0
      : priorityRaw === 'nearby'
        ? 1
        : 2;
  const limitValue = Number((body as { limit?: unknown }).limit);
  const limit = Number.isFinite(limitValue) && limitValue > 0
    ? Math.min(Math.trunc(limitValue), 200000)
    : 200000;

  if (directPaths.length > 0) {
    const paths = directPaths.filter((imagePath) => isInsideAnyDir(imagePath, dirs));
    return NextResponse.json({
      ok: true,
      warmup: enqueueThumbnails(paths.slice(0, Math.min(limit, 500)), priority),
    });
  }

  const images = getIndex();
  const paths = images
    .filter((image) => isInsideAnyDir(image.id, dirs))
    .slice(0, limit)
    .map((image) => image.id);

  return NextResponse.json({
    ok: true,
    warmup: startThumbnailWarmup(paths, dirs.join('\n') || 'all-indexed-images'),
  });
}
