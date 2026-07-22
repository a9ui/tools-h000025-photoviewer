import { NextRequest, NextResponse } from 'next/server';
import { hasIndexSession, searchIndex, type SearchTiming } from '@/lib/indexer';
import { formatServerTiming, isPerfTraceEnabled } from '@/lib/perfTrace';

type SortBy = 'newest' | 'oldest' | 'created-newest' | 'created-oldest' | 'name' | 'random';

export const dynamic = 'force-dynamic';

function parseNonNegativeInt(value: string | null, fallback: number) {
  const parsed = Number.parseInt(value || '', 10);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
}

function parsePageSize(value: string | null, fallback: number) {
  const parsed = Number.parseInt(value || '', 10);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(1, Math.min(200, parsed));
}

/**
 * GET /api/search?q=QUERY&page=0&size=100
 *
 * Server-side full-text search over cached image metadata.
 * Empty query returns the indexed image list for the active filters.
 */
export async function GET(request: NextRequest) {
  const traceEnabled = isPerfTraceEnabled();
  const q = request.nextUrl.searchParams.get('q') || '';
  const page = parseNonNegativeInt(request.nextUrl.searchParams.get('page'), 0);
  const size = parsePageSize(request.nextUrl.searchParams.get('size'), 100);
  const sortByParam = request.nextUrl.searchParams.get('sortBy');
  const randomSeed = request.nextUrl.searchParams.get('randomSeed') || undefined;
  const indexToken = request.nextUrl.searchParams.get('indexToken') || undefined;
  const dateFrom = request.nextUrl.searchParams.get('dateFrom') || undefined;
  const dateTo = request.nextUrl.searchParams.get('dateTo') || undefined;
  const dirPath = request.nextUrl.searchParams.get('dir') || undefined;
  const hiddenFoldersRaw = request.nextUrl.searchParams.get('hiddenFolders');
  let hiddenFolders: string[] | undefined;

  if (indexToken && !hasIndexSession(indexToken)) {
    return NextResponse.json(
      { error: 'This viewer session expired. Scan the folder set again to refresh it.' },
      { status: 410 }
    );
  }

  if (hiddenFoldersRaw) {
    try {
      const parsed = JSON.parse(hiddenFoldersRaw);
      if (Array.isArray(parsed)) {
        hiddenFolders = parsed.filter((item): item is string => typeof item === 'string');
      }
    } catch {
      hiddenFolders = undefined;
    }
  }

  const sortBy: SortBy =
    sortByParam === 'oldest' ||
    sortByParam === 'created-newest' ||
    sortByParam === 'created-oldest' ||
    sortByParam === 'name' ||
    sortByParam === 'random'
      ? sortByParam
      : 'newest';

  const timing: SearchTiming | undefined = traceEnabled ? {
    cacheHit: false,
    filterSortMs: 0,
    pageSliceMs: 0,
    totalMs: 0,
  } : undefined;
  const result = searchIndex(
    q,
    page,
    size,
    sortBy,
    dateFrom,
    dateTo,
    undefined,
    dirPath,
    hiddenFolders,
    randomSeed,
    indexToken,
    timing,
  );

  if (timing) {
    const serializeStartedAt = performance.now();
    const body = JSON.stringify(result);
    const serializeMs = performance.now() - serializeStartedAt;
    return new NextResponse(body, {
      headers: {
        'Content-Type': 'application/json',
        'Server-Timing': formatServerTiming([
          ['filter', timing.filterSortMs],
          ['page', timing.pageSliceMs],
          ['search', timing.totalMs],
          ['serialize', serializeMs],
          ['total', timing.totalMs + serializeMs],
        ]),
        'X-PV-Search-Cache': timing.cacheHit ? 'hit' : 'miss',
      },
    });
  }
  return NextResponse.json(result);
}
