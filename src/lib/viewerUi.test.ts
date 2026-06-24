import { describe, expect, it } from 'vitest';
import {
  getResultCountLabel,
  getZoomCenteredScrollTop,
  sortFolderBuckets,
  type FolderBucket,
} from './viewerUi';

describe('sortFolderBuckets', () => {
  const buckets: FolderBucket[] = [
    { key: 'zeta', label: 'Zeta', count: 3 },
    { key: 'alpha', label: 'Alpha', count: 9 },
    { key: 'beta', label: 'Beta', count: 9 },
  ];

  it('sorts folders by name ascending', () => {
    expect(sortFolderBuckets(buckets, 'name-asc').map((bucket) => bucket.label)).toEqual([
      'Alpha',
      'Beta',
      'Zeta',
    ]);
  });

  it('sorts folders by count descending and breaks ties by name', () => {
    expect(sortFolderBuckets(buckets, 'count-desc').map((bucket) => bucket.label)).toEqual([
      'Alpha',
      'Beta',
      'Zeta',
    ]);
  });
});

describe('getResultCountLabel', () => {
  it('shows indexed total when no server-side filters are active', () => {
    expect(getResultCountLabel({
      searchQuery: '',
      searchTotal: 42,
      totalIndexed: 900,
      hiddenFolders: [],
    })).toBe('900 indexed');
  });

  it('shows filtered result total when a search filter is active', () => {
    expect(getResultCountLabel({
      searchQuery: 'portrait',
      searchTotal: 42,
      totalIndexed: 900,
      hiddenFolders: [],
    })).toBe('42 filtered / 900 indexed');
  });

  it('makes date-filtered counts explicit', () => {
    expect(getResultCountLabel({
      searchQuery: '',
      searchTotal: 5807,
      totalIndexed: 71773,
      dateFrom: '2026-04-26',
      dateTo: '2026-05-25',
      hiddenFolders: [],
    })).toBe('5,807 filtered / 71,773 indexed');
  });
});

describe('getZoomCenteredScrollTop', () => {
  it('keeps the same center row visible after zooming in', () => {
    const nextScrollTop = getZoomCenteredScrollTop(
      {
        scrollTop: 1200,
        viewportHeight: 800,
        rowHeight: 200,
        gridColumns: 5,
        fullCount: 500,
        totalHeight: 20000,
      },
      {
        scrollTop: 1200,
        viewportHeight: 800,
        rowHeight: 320,
        gridColumns: 3,
        fullCount: 500,
        totalHeight: 54000,
      }
    );

    expect(nextScrollTop).toBeGreaterThan(1200);
    expect(nextScrollTop).toBeLessThan(54000);
  });

  it('clamps to valid bounds when the new content is shorter', () => {
    const nextScrollTop = getZoomCenteredScrollTop(
      {
        scrollTop: 1500,
        viewportHeight: 900,
        rowHeight: 240,
        gridColumns: 4,
        fullCount: 24,
        totalHeight: 2400,
      },
      {
        scrollTop: 1500,
        viewportHeight: 900,
        rowHeight: 180,
        gridColumns: 6,
        fullCount: 24,
        totalHeight: 900,
      }
    );

    expect(nextScrollTop).toBe(0);
  });
});
