import type { ImageFile } from './types';

export const DATE_SECTION_HEADER_HEIGHT = 38;
export const GRID_GAP = 16;
export const LIST_ROW_HEIGHT = 86;
export const DATE_SECTION_LAYOUT_MAX_INCREMENTAL_ITEMS = 2500;

export type DateSectionLayoutEntry =
  | {
      type: 'header';
      key: string;
      top: number;
      height: number;
      dateLabel: string;
    }
  | {
      type: 'item';
      key: string;
      virtualIndex: number;
      top: number;
      left: number;
      width: number;
      height: number;
    };

export type DateSectionLayout = {
  entries: DateSectionLayoutEntry[];
  itemTops: number[];
  totalHeight: number;
};

export type DateSectionLayoutOptions = {
  itemCount: number;
  viewMode: 'grid' | 'list';
  gridColumns: number;
  gridCellWidth: number;
  gridCellHeight: number;
  getImageAt: (index: number) => ImageFile | null | undefined;
};

export type DateSectionLayoutPolicyOptions = {
  showDateSeparators: boolean;
  itemCount: number;
  loadedSearchCount: number;
  searchTotal: number;
  isClientFiltered: boolean;
};

export function shouldUseDateSectionLayout(options: DateSectionLayoutPolicyOptions): boolean {
  const {
    showDateSeparators,
    itemCount,
    loadedSearchCount,
    searchTotal,
    isClientFiltered,
  } = options;

  if (!showDateSeparators || itemCount <= 0) return false;
  const backingSearchComplete = searchTotal <= 0 || loadedSearchCount >= searchTotal;
  if (isClientFiltered) return backingSearchComplete && itemCount <= DATE_SECTION_LAYOUT_MAX_INCREMENTAL_ITEMS;

  return itemCount <= DATE_SECTION_LAYOUT_MAX_INCREMENTAL_ITEMS && backingSearchComplete;
}

export function formatImageDate(value?: number): string {
  if (!value) return '';
  const date = new Date(value);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function formatCompactImageDate(value?: number): string {
  if (!value) return '';
  const date = new Date(value);
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${month}-${day}`;
}

export function formatSectionDate(value?: number): string {
  if (!value) return '';
  const date = new Date(value);
  return `${date.getMonth() + 1}\u6708${date.getDate()}\u65e5`;
}

export function buildDateSectionLayout(options: DateSectionLayoutOptions): DateSectionLayout | null {
  const {
    itemCount,
    viewMode,
    gridColumns,
    gridCellWidth,
    gridCellHeight,
    getImageAt,
  } = options;

  if (itemCount <= 0) return null;

  const entries: DateSectionLayoutEntry[] = [];
  const itemTops = Array<number>(itemCount).fill(0);
  let y = 0;
  let currentDateKey = '';

  if (viewMode === 'list') {
    for (let i = 0; i < itemCount; i++) {
      const image = getImageAt(i) ?? null;
      const dateKey = image ? formatImageDate(image.createdAt) : currentDateKey;
      if (image && dateKey && dateKey !== currentDateKey) {
        currentDateKey = dateKey;
        entries.push({
          type: 'header',
          key: `header-${dateKey}-${i}`,
          top: y,
          height: DATE_SECTION_HEADER_HEIGHT,
          dateLabel: formatSectionDate(image.createdAt),
        });
        y += DATE_SECTION_HEADER_HEIGHT;
      }
      itemTops[i] = y;
      entries.push({
        type: 'item',
        key: image?.id ?? `placeholder-list-${i}`,
        virtualIndex: i,
        top: y,
        left: 0,
        width: 0,
        height: LIST_ROW_HEIGHT,
      });
      y += LIST_ROW_HEIGHT;
    }
    return { entries, itemTops, totalHeight: y };
  }

  const safeColumns = Math.max(1, gridColumns);
  const rowHeight = Math.max(1, gridCellHeight + GRID_GAP);
  let groupItemCount = 0;

  const finishGroup = () => {
    if (groupItemCount <= 0) return;
    y += Math.ceil(groupItemCount / safeColumns) * rowHeight - GRID_GAP;
    groupItemCount = 0;
  };

  for (let i = 0; i < itemCount; i++) {
    const image = getImageAt(i) ?? null;
    const dateKey = image ? formatImageDate(image.createdAt) : currentDateKey;
    if (image && dateKey && dateKey !== currentDateKey) {
      finishGroup();
      if (entries.length > 0) y += GRID_GAP;
      currentDateKey = dateKey;
      entries.push({
        type: 'header',
        key: `header-${dateKey}-${i}`,
        top: y,
        height: DATE_SECTION_HEADER_HEIGHT,
        dateLabel: formatSectionDate(image.createdAt),
      });
      y += DATE_SECTION_HEADER_HEIGHT;
    }

    const row = Math.floor(groupItemCount / safeColumns);
    const col = groupItemCount % safeColumns;
    const top = y + row * rowHeight;
    itemTops[i] = top;
    entries.push({
      type: 'item',
      key: image?.id ?? `placeholder-grid-${i}`,
      virtualIndex: i,
      top,
      left: col * (gridCellWidth + GRID_GAP),
      width: gridCellWidth,
      height: gridCellHeight,
    });
    groupItemCount++;
  }

  finishGroup();
  return { entries, itemTops, totalHeight: Math.max(0, y) };
}

export function findDateSectionItemTop(layout: DateSectionLayout | null, index: number): number | null {
  if (!layout || index < 0 || index >= layout.itemTops.length) return null;
  const top = layout.itemTops[index];
  return Number.isFinite(top) ? top : null;
}
