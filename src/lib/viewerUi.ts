export type FolderSortBy = 'name-asc' | 'name-desc' | 'count-desc' | 'count-asc';

export interface FolderBucket {
  key: string;
  label: string;
  count: number;
}

export interface ResultCountLabelArgs {
  searchQuery: string;
  searchTotal: number;
  totalIndexed: number;
  dateFrom?: string;
  dateTo?: string;
  hiddenFolders?: string[];
}

export interface GridMetricsSnapshot {
  scrollTop: number;
  viewportHeight: number;
  rowHeight: number;
  gridColumns: number;
  fullCount: number;
  totalHeight: number;
}

export interface ArrowSelectionArgs {
  key: string;
  viewMode: 'grid' | 'list';
  gridColumns: number;
  currentIndex: number;
  itemCount: number;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export function sortFolderBuckets(
  buckets: FolderBucket[],
  sortBy: FolderSortBy
): FolderBucket[] {
  return [...buckets].sort((left, right) => {
    if (sortBy === 'name-asc') {
      return left.label.localeCompare(right.label, undefined, { sensitivity: 'base' });
    }
    if (sortBy === 'name-desc') {
      return right.label.localeCompare(left.label, undefined, { sensitivity: 'base' });
    }
    if (sortBy === 'count-asc') {
      if (left.count !== right.count) return left.count - right.count;
      return left.label.localeCompare(right.label, undefined, { sensitivity: 'base' });
    }
    if (left.count !== right.count) return right.count - left.count;
    return left.label.localeCompare(right.label, undefined, { sensitivity: 'base' });
  });
}

export function getResultCountLabel({
  searchQuery,
  searchTotal,
  totalIndexed,
  dateFrom,
  dateTo,
  hiddenFolders = [],
}: ResultCountLabelArgs): string {
  const hasServerFilters = Boolean(
    searchQuery.trim() ||
    dateFrom ||
    dateTo ||
    hiddenFolders.length > 0
  );

  return hasServerFilters
    ? `${searchTotal.toLocaleString()} filtered / ${totalIndexed.toLocaleString()} indexed`
    : `${totalIndexed.toLocaleString()} indexed`;
}

export function getZoomCenteredScrollTop(
  previous: GridMetricsSnapshot,
  next: GridMetricsSnapshot
): number {
  if (
    previous.fullCount <= 0 ||
    previous.gridColumns <= 0 ||
    next.gridColumns <= 0 ||
    previous.rowHeight <= 0 ||
    next.rowHeight <= 0
  ) {
    return previous.scrollTop;
  }

  const previousCenterY = previous.scrollTop + previous.viewportHeight / 2;
  const previousRow = clamp(
    Math.floor(previousCenterY / previous.rowHeight),
    0,
    Math.max(0, Math.ceil(previous.fullCount / previous.gridColumns) - 1)
  );
  const rowOffsetRatio = clamp(
    (previousCenterY - previousRow * previous.rowHeight) / previous.rowHeight,
    0,
    1
  );
  const anchorColumn = clamp(
    Math.floor(previous.gridColumns / 2),
    0,
    Math.max(0, previous.gridColumns - 1)
  );
  const anchorIndex = clamp(
    previousRow * previous.gridColumns + anchorColumn,
    0,
    Math.max(0, previous.fullCount - 1)
  );
  const nextRow = Math.floor(anchorIndex / next.gridColumns);
  const nextCenterY = nextRow * next.rowHeight + next.rowHeight * rowOffsetRatio;
  const maxScrollTop = Math.max(0, next.totalHeight - next.viewportHeight);
  return clamp(nextCenterY - next.viewportHeight / 2, 0, maxScrollTop);
}

export function getArrowSelectionIndex({
  key,
  viewMode,
  gridColumns,
  currentIndex,
  itemCount,
}: ArrowSelectionArgs): number | null {
  if (itemCount <= 0) return null;

  if (currentIndex < 0 || currentIndex >= itemCount) {
    return key === 'ArrowLeft' || key === 'ArrowUp' ? itemCount - 1 : 0;
  }

  const rowDelta = viewMode === 'grid' ? Math.max(1, gridColumns) : 1;
  const delta =
    key === 'ArrowLeft' ? -1 :
    key === 'ArrowRight' ? 1 :
    key === 'ArrowUp' ? -rowDelta :
    key === 'ArrowDown' ? rowDelta :
    0;

  if (delta === 0) return null;
  return clamp(currentIndex + delta, 0, itemCount - 1);
}
