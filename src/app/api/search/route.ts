import { NextRequest, NextResponse } from 'next/server';
import { searchIndex } from '@/lib/indexer';

type SortBy = 'newest' | 'oldest' | 'created-newest' | 'created-oldest' | 'name' | 'random';

export const dynamic = 'force-dynamic';

/**
 * GET /api/search?q=QUERY&page=0&size=100
 *
 * Server-side full-text search over cached image metadata.
 * Empty query returns the indexed image list for the active filters.
 */
export async function GET(request: NextRequest) {
  const q = request.nextUrl.searchParams.get('q') || '';
  const page = parseInt(request.nextUrl.searchParams.get('page') || '0', 10);
  const size = parseInt(request.nextUrl.searchParams.get('size') || '100', 10);
  const sortByParam = request.nextUrl.searchParams.get('sortBy');
  const randomSeed = request.nextUrl.searchParams.get('randomSeed') || undefined;
  const dateFrom = request.nextUrl.searchParams.get('dateFrom') || undefined;
  const dateTo = request.nextUrl.searchParams.get('dateTo') || undefined;
  const dirPath = request.nextUrl.searchParams.get('dir') || undefined;
  const hiddenFoldersRaw = request.nextUrl.searchParams.get('hiddenFolders');
  let hiddenFolders: string[] | undefined;

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

  const result = searchIndex(
    q,
    page,
    Math.min(size, 200),
    sortBy,
    dateFrom,
    dateTo,
    undefined,
    dirPath,
    hiddenFolders,
    randomSeed
  );

  return NextResponse.json(result);
}
