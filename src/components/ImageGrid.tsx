'use client';

import React, { useMemo, useRef, useEffect, useState } from 'react';
import type { ImageFile } from '../lib/types';
import { useImageStore } from '../store/ImageContext';
import { getZoomCenteredScrollTop, type GridMetricsSnapshot } from '../lib/viewerUi';
import { reconcileModalOrderAfterFilterChange } from '../lib/modalNavigation';
import { createThumbnailWarmupBatcher, type ThumbnailWarmupPriority } from '../lib/thumbnailWarmupBatcher';
import {
  buildDateSectionLayout,
  findDateSectionItemTop,
  formatCompactImageDate,
  formatImageDate,
  GRID_GAP,
  LIST_ROW_HEIGHT,
  shouldUseDateSectionLayout,
  type DateSectionLayoutEntry,
} from '../lib/dateSectionLayout';
import CachedImage from './CachedImage';

const OVERSCAN_ROWS = 4;
const SEARCH_PAGE_SIZE = 100;
const COMPACT_ACTIONS_MAX_WIDTH = 150;
const MODAL_WARMUP_DELAY_MS = 1400;
const MODAL_WARMUP_INTERVAL_MS = 2200;
const MODAL_WARMUP_THUMB_BUDGET = 18;
const MODAL_WARMUP_SEARCH_PAGES = 1;
const BACKGROUND_SEARCH_PAGE_DELAY_MS = 90;
const BACKGROUND_SEARCH_PAGES = 2;
const SCROLL_MEMORY_WRITE_DELAY_MS = 180;

function withThumbPriorityParams(fileUrl: string, priority: ThumbnailWarmupPriority = 'visible') {
  const separator = fileUrl.includes('?') ? '&' : '?';
  return `${fileUrl}${separator}priority=${priority}`;
}

function dispatchThumbnailWarmup(paths: string[], dirPath: string, priority: ThumbnailWarmupPriority = 'nearby') {
  if (paths.length === 0) return;
  void fetch('/api/thumbs/warm', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ paths, dir: dirPath, priority }),
  }).catch(() => {
    // Best-effort only; rendered image requests can still generate thumbs.
  });
}

function getDragImageContentType(filename: string) {
  const extension = filename.split('.').pop()?.toLowerCase();
  if (extension === 'jpg' || extension === 'jpeg') return 'image/jpeg';
  if (extension === 'webp') return 'image/webp';
  if (extension === 'avif') return 'image/avif';
  if (extension === 'gif') return 'image/gif';
  return 'image/png';
}

function formatSectionDate(value?: number): string {
  if (!value) return '';
  const date = new Date(value);
  return `${date.getMonth() + 1}月${date.getDate()}日`;
}

type SectionLayoutEntry =
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

export default function ImageGrid() {
  const {
    searchQuery, searchResults, searchTotal, isSearching, ensureSearchRange,
    selectImage, openPreviewTab, cycleFavoriteLevel, decreaseFavoriteLevel, favorites, view, setView, selectedIds, showFavOnly, showUnfavOnly, favoriteFilterLevel,
    showEnhancedOnly, enhancedSourceIds,
    closeAllPreviews, setSearchScrollPosition, getSearchScrollPosition,
    seenImageIds, markImageSeen, revealImageId, consumeRevealImage, openModalAtImage,
    modalImageIds, setModalImageIds, selectedIndex, setSelectedIndex,
    dirPath,
  } = useImageStore();

  const containerRef = useRef<HTMLDivElement>(null);
  const thumbSizeRef = useRef(view.thumbSize);
  const [scrollTop, setScrollTop] = useState(0);
  const [viewportHeight, setViewportHeight] = useState(0);
  const [viewportWidth, setViewportWidth] = useState(0);
  const [horizontalPadding, setHorizontalPadding] = useState(0);
  const preloadRef = useRef<Set<string>>(new Set());
  const visibleWarmRef = useRef<Set<string>>(new Set());
  const modalWarmCursorRef = useRef(0);
  const restoredScrollKeyRef = useRef<string | null>(null);
  const previousThumbSizeRef = useRef(view.thumbSize);
  const previousGridMetricsRef = useRef<GridMetricsSnapshot | null>(null);
  const warmupBatcherRef = useRef<ReturnType<typeof createThumbnailWarmupBatcher> | null>(null);

  if (warmupBatcherRef.current === null) {
    warmupBatcherRef.current = createThumbnailWarmupBatcher({
      dispatch: dispatchThumbnailWarmup,
    });
  }

  useEffect(() => {
    const batcher = warmupBatcherRef.current;
    return () => batcher?.clear();
  }, []);

  useEffect(() => {
    thumbSizeRef.current = view.thumbSize;
  }, [view.thumbSize]);

  useEffect(() => {
    const container = containerRef.current;
    const scrollEl = container?.closest('.viewer-main') as HTMLElement | null;
    if (!container || !scrollEl) return;

    let metricsFrameId: number | null = null;

    const updateMetrics = () => {
      metricsFrameId = null;
      const style = window.getComputedStyle(container);
      const leftPad = Number.parseFloat(style.paddingLeft || '0') || 0;
      const rightPad = Number.parseFloat(style.paddingRight || '0') || 0;
      setHorizontalPadding(leftPad + rightPad);
      setViewportHeight(scrollEl.clientHeight);
      setViewportWidth(container.clientWidth);
      setScrollTop(scrollEl.scrollTop);
    };

    const scheduleMetricsUpdate = () => {
      if (metricsFrameId !== null) return;
      metricsFrameId = window.requestAnimationFrame(updateMetrics);
    };

    updateMetrics();
    scrollEl.addEventListener('scroll', scheduleMetricsUpdate, { passive: true });
    const resizeObserver = new ResizeObserver(scheduleMetricsUpdate);
    resizeObserver.observe(scrollEl);
    resizeObserver.observe(container);
    window.addEventListener('resize', scheduleMetricsUpdate);

    return () => {
      scrollEl.removeEventListener('scroll', scheduleMetricsUpdate);
      resizeObserver.disconnect();
      window.removeEventListener('resize', scheduleMetricsUpdate);
      if (metricsFrameId !== null) {
        window.cancelAnimationFrame(metricsFrameId);
      }
    };
  }, []);

  const scrollMemoryKey = useMemo(
    () => JSON.stringify({
      dir: dirPath,
      q: searchQuery,
      sortBy: view.sortBy,
      randomSeed: view.sortBy === 'random' ? view.randomSeed : '',
      dateFrom: view.dateFrom,
      dateTo: view.dateTo,
      mode: view.viewMode,
      style: view.displayStyle,
      fav: showFavOnly ? 1 : 0,
      unfav: showUnfavOnly ? 1 : 0,
      favLevel: favoriteFilterLevel,
      enhanced: showEnhancedOnly ? 1 : 0,
      hiddenFolders: view.hiddenFolders,
    }),
    [
      favoriteFilterLevel,
      dirPath,
      searchQuery,
      showFavOnly,
      showEnhancedOnly,
      showUnfavOnly,
      view.dateFrom,
      view.dateTo,
      view.displayStyle,
      view.hiddenFolders,
      view.randomSeed,
      view.sortBy,
      view.viewMode,
    ]
  );

  useEffect(() => {
    const container = containerRef.current;
    const scrollEl = container?.closest('.viewer-main') as HTMLElement | null;
    if (!scrollEl) return;
    const remembered = getSearchScrollPosition(scrollMemoryKey);
    if (remembered === null) return;
    if (restoredScrollKeyRef.current === scrollMemoryKey) return;
    restoredScrollKeyRef.current = scrollMemoryKey;
    requestAnimationFrame(() => {
      scrollEl.scrollTop = remembered;
      setScrollTop(remembered);
    });
  }, [getSearchScrollPosition, scrollMemoryKey]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setSearchScrollPosition(scrollMemoryKey, scrollTop);
    }, SCROLL_MEMORY_WRITE_DELAY_MS);
    return () => window.clearTimeout(timeoutId);
  }, [scrollMemoryKey, scrollTop, setSearchScrollPosition]);

  useEffect(() => {
    const container = containerRef.current;
    const scrollEl = container?.closest('.viewer-main') as HTMLElement | null;
    if (!scrollEl) return;

    const onWheel = (event: WheelEvent) => {
      if (view.viewMode !== 'grid') return;
      if (!event.ctrlKey && !event.metaKey && !event.altKey) return;
      event.preventDefault();
      const next = Math.max(40, Math.min(600, thumbSizeRef.current + (event.deltaY > 0 ? -20 : 20)));
      if (next !== thumbSizeRef.current) setView({ thumbSize: next });
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (view.viewMode !== 'grid') return;
      if (!event.ctrlKey && !event.metaKey) return;
      const key = event.key;
      if (key === '+' || key === '=' || key === '-') {
        event.preventDefault();
        const delta = key === '-' ? -20 : 20;
        const next = Math.max(40, Math.min(600, thumbSizeRef.current + delta));
        if (next !== thumbSizeRef.current) setView({ thumbSize: next });
      } else if (key === '0') {
        event.preventDefault();
        setView({ thumbSize: 200 });
      }
    };

    scrollEl.addEventListener('wheel', onWheel, { passive: false });
    window.addEventListener('keydown', onKeyDown, { passive: false });
    window.addEventListener('wheel', onWheel, { passive: false, capture: true });
    return () => {
      scrollEl.removeEventListener('wheel', onWheel);
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('wheel', onWheel, { capture: true } as EventListenerOptions);
    };
  }, [setView, view.viewMode]);

  const loadedOrderedIds = useMemo(
    () => searchResults.filter((img): img is ImageFile => Boolean(img)).map((img) => img.id),
    [searchResults]
  );

  const isClientFiltered = showFavOnly || showUnfavOnly || showEnhancedOnly;

  const clientFilteredVisible = useMemo(() => {
    if (!isClientFiltered) return [] as Array<{ image: ImageFile; sourceIndex: number }>;
    const items: Array<{ image: ImageFile; sourceIndex: number }> = [];
    for (let i = 0; i < searchResults.length; i++) {
      const img = searchResults[i];
      if (!img) continue;
      const level = favorites[img.id] ?? 0;
      const matchesFavorite = showFavOnly
        ? level >= favoriteFilterLevel
        : showUnfavOnly
          ? level === 0
          : true;
      const matchesEnhanced = showEnhancedOnly ? Boolean(enhancedSourceIds[img.id]) : true;
      if (matchesFavorite && matchesEnhanced) {
        items.push({ image: img, sourceIndex: i });
      }
    }
    return items;
  }, [enhancedSourceIds, favoriteFilterLevel, isClientFiltered, showEnhancedOnly, showFavOnly, showUnfavOnly, searchResults, favorites]);

  const filteredOrderedIds = useMemo(
    () => clientFilteredVisible.map((item) => item.image.id),
    [clientFilteredVisible]
  );

  const selectedIdSet = useMemo(() => new Set(selectedIds), [selectedIds]);
  const modalImageIdKey = modalImageIds.join('\u0001');

  useEffect(() => {
    if (!isClientFiltered || modalImageIds.length === 0) return;
    const currentId = selectedIndex !== null ? searchResults[selectedIndex]?.id ?? null : null;
    const nextState = reconcileModalOrderAfterFilterChange(
      currentId,
      modalImageIds,
      filteredOrderedIds
    );
    const nextKey = nextState.orderedIds.join('\u0001');
    if (modalImageIdKey !== nextKey) {
      setModalImageIds(nextState.orderedIds);
    }

    const nextIndex = nextState.selectedId
      ? searchResults.findIndex((image) => image?.id === nextState.selectedId)
      : null;
    const resolvedNextIndex = nextIndex !== null && nextIndex >= 0 ? nextIndex : null;
    if (selectedIndex !== resolvedNextIndex) {
      setSelectedIndex(resolvedNextIndex);
    }
  }, [
    filteredOrderedIds,
    isClientFiltered,
    modalImageIdKey,
    modalImageIds,
    searchResults,
    selectedIndex,
    setModalImageIds,
    setSelectedIndex,
  ]);

  const handleThumbWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    const wantsThumbZoom = event.altKey || event.ctrlKey || event.metaKey;
    if (!wantsThumbZoom || view.viewMode !== 'grid') return;
    event.preventDefault();
    const next = Math.max(40, Math.min(600, thumbSizeRef.current + (event.deltaY > 0 ? -20 : 20)));
    if (next !== thumbSizeRef.current) {
      setView({ thumbSize: next });
    }
  };

  const handleImageDragStart = (event: React.DragEvent, imageId: string, filename: string, fullUrl: string) => {
    const absoluteUrl = new URL(fullUrl, window.location.origin).toString();
    event.dataTransfer.effectAllowed = 'copy';
    event.dataTransfer.setData('text/uri-list', absoluteUrl);
    event.dataTransfer.setData('text/plain', imageId);
    event.dataTransfer.setData('DownloadURL', `${getDragImageContentType(filename)}:${filename}:${absoluteUrl}`);
  };

  const openImageDetail = (image: ImageFile, sourceIndex: number) => {
    markImageSeen(image.id);
    openPreviewTab(image, { makeActive: true, pin: true });
    openModalAtImage(
      image.id,
      sourceIndex >= 0 ? sourceIndex : null,
      isClientFiltered ? filteredOrderedIds : []
    );
  };

  const aspectRatioValue = view.aspectMode === 'square' ? 1 : 2 / 3;

  const gridColumns = useMemo(() => {
    if (view.viewMode !== 'grid') return 1;
    if (view.columns > 0) return view.columns;
    const available = Math.max(1, viewportWidth - horizontalPadding);
    return Math.max(1, Math.floor((available + GRID_GAP) / (Math.max(40, view.thumbSize) + GRID_GAP)));
  }, [horizontalPadding, view.columns, view.thumbSize, view.viewMode, viewportWidth]);

  const gridCellWidth = useMemo(() => {
    if (view.viewMode !== 'grid') return 0;
    const available = Math.max(1, viewportWidth - horizontalPadding);
    return Math.max(40, Math.floor((available - (gridColumns - 1) * GRID_GAP) / gridColumns));
  }, [gridColumns, horizontalPadding, view.viewMode, viewportWidth]);

  const gridCellHeight = useMemo(() => {
    if (view.viewMode !== 'grid') return 0;
    if (view.aspectMode === 'square') return gridCellWidth;
    return Math.round(gridCellWidth / aspectRatioValue);
  }, [aspectRatioValue, gridCellWidth, view.aspectMode, view.viewMode]);
  const compactGridActions = view.viewMode === 'grid' && gridCellWidth > 0 && gridCellWidth <= COMPACT_ACTIONS_MAX_WIDTH;
  const showDateSeparators = view.sortBy === 'created-newest' || view.sortBy === 'created-oldest';
  const imageObjectStyle: React.CSSProperties = view.displayStyle === 'poster'
    ? {}
    : view.aspectMode === 'original'
      ? { objectFit: 'contain', background: 'var(--bg-tertiary)' }
      : {};

  const fullCount = isClientFiltered ? clientFilteredVisible.length : searchTotal;
  const loadedSearchCount = useMemo(
    () => searchResults.reduce((count, image) => count + (image ? 1 : 0), 0),
    [searchResults]
  );
  const canUseSectionLayout = shouldUseDateSectionLayout({
    showDateSeparators,
    itemCount: fullCount,
    loadedSearchCount,
    searchTotal,
    isClientFiltered,
  });

  const sectionLayout = useMemo(() => {
    if (!canUseSectionLayout) return null;

    return buildDateSectionLayout({
      itemCount: fullCount,
      viewMode: view.viewMode,
      gridColumns,
      gridCellWidth,
      gridCellHeight,
      getImageAt: (index) => (
        isClientFiltered
          ? clientFilteredVisible[index]?.image ?? null
          : searchResults[index] ?? null
      ),
    });
  }, [
    clientFilteredVisible,
    fullCount,
    gridCellHeight,
    gridCellWidth,
    gridColumns,
    searchResults,
    canUseSectionLayout,
    isClientFiltered,
    view.viewMode,
  ]);

  const virtualRange = useMemo(() => {
    if (fullCount <= 0) return { start: 0, end: -1, totalHeight: 0 };

    if (sectionLayout) {
      const top = Math.max(0, scrollTop - viewportHeight * 0.75);
      const bottom = scrollTop + viewportHeight * 1.75;
      let start = Number.POSITIVE_INFINITY;
      let end = -1;
      for (const entry of sectionLayout.entries) {
        if (entry.top + entry.height < top || entry.top > bottom) continue;
        if (entry.type !== 'item') continue;
        start = Math.min(start, entry.virtualIndex);
        end = Math.max(end, entry.virtualIndex);
      }
      return {
        start: Number.isFinite(start) ? start : 0,
        end,
        totalHeight: sectionLayout.totalHeight,
      };
    }

    if (view.viewMode === 'list') {
      const start = Math.max(0, Math.floor(scrollTop / LIST_ROW_HEIGHT) - OVERSCAN_ROWS);
      const end = Math.min(
        fullCount - 1,
        Math.ceil((scrollTop + viewportHeight) / LIST_ROW_HEIGHT) + OVERSCAN_ROWS
      );
      return {
        start,
        end,
        totalHeight: fullCount * LIST_ROW_HEIGHT,
      };
    }

    const rowHeight = Math.max(1, gridCellHeight + GRID_GAP);
    const totalRows = Math.ceil(fullCount / gridColumns);
    const startRow = Math.max(0, Math.floor(scrollTop / rowHeight) - OVERSCAN_ROWS);
    const endRow = Math.min(
      totalRows - 1,
      Math.ceil((scrollTop + viewportHeight) / rowHeight) + OVERSCAN_ROWS
    );

    return {
      start: startRow * gridColumns,
      end: Math.min(fullCount - 1, (endRow + 1) * gridColumns - 1),
      totalHeight: Math.max(1, totalRows * rowHeight - GRID_GAP),
    };
  }, [fullCount, gridCellHeight, gridColumns, scrollTop, sectionLayout, view.viewMode, viewportHeight]);

  const visiblePriorityRange = useMemo(() => {
    if (fullCount <= 0) return { start: 0, end: -1 };

    if (sectionLayout) {
      let start = Number.POSITIVE_INFINITY;
      let end = -1;
      for (const entry of sectionLayout.entries) {
        if (entry.top + entry.height < scrollTop || entry.top > scrollTop + viewportHeight) continue;
        if (entry.type !== 'item') continue;
        start = Math.min(start, entry.virtualIndex);
        end = Math.max(end, entry.virtualIndex);
      }
      return {
        start: Number.isFinite(start) ? start : 0,
        end,
      };
    }

    if (view.viewMode === 'list') {
      return {
        start: Math.max(0, Math.floor(scrollTop / LIST_ROW_HEIGHT)),
        end: Math.min(fullCount - 1, Math.ceil((scrollTop + viewportHeight) / LIST_ROW_HEIGHT)),
      };
    }

    const rowHeight = Math.max(1, gridCellHeight + GRID_GAP);
    const startRow = Math.max(0, Math.floor(scrollTop / rowHeight));
    const endRow = Math.min(
      Math.ceil(fullCount / gridColumns) - 1,
      Math.ceil((scrollTop + viewportHeight) / rowHeight)
    );
    return {
      start: startRow * gridColumns,
      end: Math.min(fullCount - 1, (endRow + 1) * gridColumns - 1),
    };
  }, [fullCount, gridCellHeight, gridColumns, scrollTop, sectionLayout, view.viewMode, viewportHeight]);

  const getThumbPriority = (virtualIndex: number): ThumbnailWarmupPriority => (
    virtualIndex >= visiblePriorityRange.start && virtualIndex <= visiblePriorityRange.end
      ? 'visible'
      : 'nearby'
  );

  useEffect(() => {
    if (view.viewMode !== 'grid') {
      previousThumbSizeRef.current = view.thumbSize;
      previousGridMetricsRef.current = null;
      return;
    }

    const nextMetrics: GridMetricsSnapshot = {
      scrollTop,
      viewportHeight,
      rowHeight: Math.max(1, gridCellHeight + GRID_GAP),
      gridColumns,
      fullCount,
      totalHeight: virtualRange.totalHeight,
    };
    const previousMetrics = previousGridMetricsRef.current;
    const previousThumbSize = previousThumbSizeRef.current;

    if (
      previousMetrics &&
      previousThumbSize !== view.thumbSize &&
      previousMetrics.fullCount > 0
    ) {
      const container = containerRef.current;
      const scrollEl = container?.closest('.viewer-main') as HTMLElement | null;
      if (scrollEl) {
        const nextScrollTop = getZoomCenteredScrollTop(previousMetrics, nextMetrics);
        scrollEl.scrollTop = nextScrollTop;
        setScrollTop(nextScrollTop);
      }
    }

    previousThumbSizeRef.current = view.thumbSize;
    previousGridMetricsRef.current = nextMetrics;
  }, [
    fullCount,
    gridCellHeight,
    gridColumns,
    scrollTop,
    view.thumbSize,
    view.viewMode,
    viewportHeight,
    virtualRange.totalHeight,
  ]);

  useEffect(() => {
    if (isClientFiltered || searchTotal === 0) return;
    if (virtualRange.end < virtualRange.start) return;
    ensureSearchRange(virtualRange.start, virtualRange.end);
  }, [ensureSearchRange, isClientFiltered, searchTotal, virtualRange.end, virtualRange.start]);

  useEffect(() => {
    if (isClientFiltered) return;
    if (searchTotal <= 0) return;
    if (loadedSearchCount >= searchTotal) return;

    const firstMissing = searchResults.findIndex((image) => image === null);
    if (firstMissing < 0) return;

    const timeoutId = window.setTimeout(() => {
      ensureSearchRange(
        firstMissing,
        Math.min(searchTotal - 1, firstMissing + SEARCH_PAGE_SIZE * BACKGROUND_SEARCH_PAGES - 1)
      );
    }, isSearching ? BACKGROUND_SEARCH_PAGE_DELAY_MS * 2 : BACKGROUND_SEARCH_PAGE_DELAY_MS);

    return () => window.clearTimeout(timeoutId);
  }, [
    ensureSearchRange,
    isSearching,
    loadedSearchCount,
    searchResults,
    searchTotal,
    isClientFiltered,
  ]);

  useEffect(() => {
    if (!isClientFiltered) return;
    if (searchTotal <= 0) return;

    if (loadedSearchCount >= searchTotal) return;

    const rowHeight = view.viewMode === 'list'
      ? LIST_ROW_HEIGHT
      : Math.max(1, gridCellHeight + GRID_GAP);
    const visibleRows = Math.max(1, Math.ceil(Math.max(1, viewportHeight) / rowHeight));
    const targetMatches = view.viewMode === 'list'
      ? visibleRows + OVERSCAN_ROWS * 6
      : gridColumns * (visibleRows + OVERSCAN_ROWS * 6);
    const needsMoreMatches = clientFilteredVisible.length < Math.max(24, targetMatches);
    const isNearFilteredEnd = virtualRange.end >= Math.max(0, clientFilteredVisible.length - 1);
    if (!needsMoreMatches && !isNearFilteredEnd) return;

    const firstMissing = searchResults.findIndex((image) => image === null);
    if (firstMissing < 0) return;
    ensureSearchRange(firstMissing, Math.min(searchTotal - 1, firstMissing + SEARCH_PAGE_SIZE * 2 - 1));
  }, [
    ensureSearchRange,
    clientFilteredVisible.length,
    gridCellHeight,
    gridColumns,
    loadedSearchCount,
    searchResults,
    searchTotal,
    isClientFiltered,
    view.viewMode,
    viewportHeight,
    virtualRange.end,
  ]);

  useEffect(() => {
    if (searchTotal <= 0) return;
    if (fullCount <= 0) return;
    const start = Math.max(0, virtualRange.start - gridColumns * 2);
    const end = Math.min(fullCount - 1, virtualRange.end + gridColumns * 8);
    const budget = 80;
    let queued = 0;
    const warmPaths: string[] = [];
    for (let i = start; i <= end && queued < budget; i++) {
      const image = isClientFiltered
        ? clientFilteredVisible[i]?.image
        : searchResults[i];
      if (!image) continue;
      if (preloadRef.current.has(image.id)) continue;
      preloadRef.current.add(image.id);
      warmPaths.push(image.id);
      queued++;
    }
    warmupBatcherRef.current?.enqueue(warmPaths, {
      dirPath,
      contextKey: scrollMemoryKey,
      priority: 'nearby',
    });
    if (preloadRef.current.size > 400) {
      const trimmed = Array.from(preloadRef.current).slice(-300);
      preloadRef.current = new Set(trimmed);
    }
  }, [clientFilteredVisible, dirPath, fullCount, gridColumns, isClientFiltered, scrollMemoryKey, searchResults, searchTotal, virtualRange.end, virtualRange.start]);

  useEffect(() => {
    if (searchTotal <= 0) return;
    if (fullCount <= 0) return;
    if (visiblePriorityRange.end < visiblePriorityRange.start) return;

    const warmPaths: string[] = [];
    for (let i = visiblePriorityRange.start; i <= visiblePriorityRange.end; i++) {
      const image = isClientFiltered
        ? clientFilteredVisible[i]?.image
        : searchResults[i];
      if (!image) continue;
      const key = `${scrollMemoryKey}\u0001${image.id}`;
      if (visibleWarmRef.current.has(key)) continue;
      visibleWarmRef.current.add(key);
      warmPaths.push(image.id);
    }

    warmupBatcherRef.current?.enqueue(warmPaths, {
      dirPath,
      contextKey: scrollMemoryKey,
      priority: 'visible',
    });
    if (visibleWarmRef.current.size > 1200) {
      const trimmed = Array.from(visibleWarmRef.current).slice(-800);
      visibleWarmRef.current = new Set(trimmed);
    }
  }, [
    clientFilteredVisible,
    dirPath,
    fullCount,
    isClientFiltered,
    scrollMemoryKey,
    searchResults,
    searchTotal,
    visiblePriorityRange.end,
    visiblePriorityRange.start,
  ]);

  useEffect(() => {
    if (selectedIndex === null || searchTotal <= 0) {
      modalWarmCursorRef.current = 0;
      return;
    }

    const warmModalBackground = () => {
      const firstMissing = searchResults.findIndex((image) => image === null);
      if (firstMissing >= 0) {
        ensureSearchRange(
          firstMissing,
          Math.min(searchTotal - 1, firstMissing + SEARCH_PAGE_SIZE * MODAL_WARMUP_SEARCH_PAGES - 1)
        );
      }

      const totalSlots = searchResults.length;
      if (totalSlots <= 0) return;
      let cursor = modalWarmCursorRef.current % totalSlots;
      let inspected = 0;
      let queued = 0;
      const budget = MODAL_WARMUP_THUMB_BUDGET;
      const warmPaths: string[] = [];

      while (inspected < totalSlots && queued < budget) {
        const index = (cursor + inspected) % totalSlots;
        const image = searchResults[index];
        if (image && !preloadRef.current.has(image.id)) {
          preloadRef.current.add(image.id);
          warmPaths.push(image.id);
          queued++;
        }
        inspected++;
      }
      warmupBatcherRef.current?.enqueue(warmPaths, {
        dirPath,
        contextKey: scrollMemoryKey,
        priority: 'nearby',
      });

      modalWarmCursorRef.current = (cursor + inspected) % totalSlots;
      if (preloadRef.current.size > 800) {
        const trimmed = Array.from(preloadRef.current).slice(-600);
        preloadRef.current = new Set(trimmed);
      }
    };

    const timeoutId = window.setTimeout(warmModalBackground, MODAL_WARMUP_DELAY_MS);
    const intervalId = window.setInterval(warmModalBackground, MODAL_WARMUP_INTERVAL_MS);
    return () => {
      window.clearTimeout(timeoutId);
      window.clearInterval(intervalId);
    };
  }, [dirPath, ensureSearchRange, scrollMemoryKey, searchResults, searchTotal, selectedIndex]);

  useEffect(() => {
    if (!revealImageId) return;
    const container = containerRef.current;
    const scrollEl = container?.closest('.viewer-main') as HTMLElement | null;
    if (!scrollEl) return;

    const visibleIndex = isClientFiltered
      ? filteredOrderedIds.indexOf(revealImageId)
      : searchResults.findIndex((image) => image?.id === revealImageId);

    if (visibleIndex < 0) {
      consumeRevealImage();
      return;
    }

    const sectionItemTop = findDateSectionItemTop(sectionLayout, visibleIndex);
    const targetTop = sectionItemTop !== null
      ? Math.max(0, sectionItemTop - viewportHeight * 0.35)
      : view.viewMode === 'list'
        ? Math.max(0, visibleIndex * LIST_ROW_HEIGHT - viewportHeight * 0.35)
        : Math.max(
          0,
          Math.floor(visibleIndex / gridColumns) * Math.max(1, gridCellHeight + GRID_GAP) - viewportHeight * 0.35
        );

    requestAnimationFrame(() => {
      scrollEl.scrollTop = targetTop;
      setScrollTop(targetTop);
      consumeRevealImage();
    });
  }, [
    consumeRevealImage,
    filteredOrderedIds,
    gridCellHeight,
    gridColumns,
    revealImageId,
    searchResults,
    sectionLayout,
    isClientFiltered,
    view.viewMode,
    viewportHeight,
  ]);

  // Empty state: no results after searching
  if (!isSearching && fullCount === 0 && searchQuery.trim()) {
    return (
      <div className="empty-state">
        <svg width="64" height="64" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" strokeWidth="1">
          <circle cx="12" cy="12" r="10" />
          <line x1="15" y1="9" x2="9" y2="15" />
          <line x1="9" y1="9" x2="15" y2="15" />
        </svg>
        <p>No images found for query: {searchQuery}</p>
      </div>
    );
  }

  const renderListItem = (virtualIndex: number, layoutEntry?: Extract<DateSectionLayoutEntry, { type: 'item' }>) => {
    const sourceIndex = isClientFiltered ? clientFilteredVisible[virtualIndex]?.sourceIndex ?? -1 : virtualIndex;
    const image = isClientFiltered ? clientFilteredVisible[virtualIndex]?.image ?? null : searchResults[sourceIndex];
    const top = layoutEntry?.top ?? virtualIndex * LIST_ROW_HEIGHT;
    const height = layoutEntry?.height ?? LIST_ROW_HEIGHT;

    if (!image) {
      return (
        <div
          key={`slot-list-${virtualIndex}`}
          className="list-item virtual-list-item placeholder"
          style={{ top, height }}
        >
          <div className="list-thumb list-thumb-placeholder" />
          <div className="list-info">
            <span className="list-filename placeholder-line" />
            <span className="list-path placeholder-line short" />
            <span className="list-prompt placeholder-line" />
          </div>
        </div>
      );
    }

    const favLevel = favorites[image.id] ?? 0;
    const isFav = favLevel > 0;
    const isSelected = selectedIdSet.has(image.id);
    const isUnseen = !seenImageIds[image.id];
    const thumbPriority = getThumbPriority(virtualIndex);
    const previousImage = isClientFiltered
      ? clientFilteredVisible[virtualIndex - 1]?.image ?? null
      : searchResults[sourceIndex - 1] ?? null;
    const dateLabel = formatImageDate(image.createdAt);
    const showDateSeparator = showDateSeparators && !sectionLayout && dateLabel !== formatImageDate(previousImage?.createdAt);
    return (
      <div
        key={`slot-list-${virtualIndex}`}
        className={`list-item virtual-list-item ${isSelected ? 'is-selected' : ''} ${isUnseen ? 'is-unseen' : ''} ${showDateSeparator ? 'has-date-separator' : ''}`}
        style={{ top, height }}
        draggable
        onDragStart={(event) => handleImageDragStart(event, image.id, image.filename, image.fullUrl)}
        onClick={(event) => {
          markImageSeen(image.id);
          selectImage(
            image,
            isClientFiltered ? filteredOrderedIds : loadedOrderedIds,
            { range: event.shiftKey, toggle: event.ctrlKey || event.metaKey }
          );
        }}
        onDoubleClick={(event) => {
          event.stopPropagation();
          openImageDetail(image, sourceIndex);
        }}
      >
        {showDateSeparator && <div className="date-separator list-date-separator">{dateLabel}</div>}
        <div className="list-thumb">
          <CachedImage
            src={image.fileUrl}
            requestSrc={withThumbPriorityParams(image.fileUrl, thumbPriority)}
            fallbackSrc={image.fullUrl}
            cacheKind="thumb"
            alt={image.filename}
            loading={thumbPriority === 'visible' ? 'eager' : 'lazy'}
            decoding="async"
            fetchPriority={thumbPriority === 'visible' ? 'high' : 'auto'}
          />
        </div>
        <div className="list-info">
          <span className="list-filename">{image.filename}</span>
          <span className="list-path" title={image.id}>{image.id}</span>
          <span className="list-prompt">
            {image.metadata?.prompt?.substring(0, 150) || '(no metadata)'}
          </span>
        </div>
        <div className="list-actions">
          <button
            className={`card-fav-step ${isFav ? 'active' : ''}`}
            disabled={!isFav}
            onClick={(event) => {
              event.stopPropagation();
              decreaseFavoriteLevel(image.id);
            }}
            style={{ position: 'static' }}
            title="Decrease favorite level"
          >
            -
          </button>
          <button
            className={`card-fav ${isFav ? 'active' : ''}`}
            onClick={(event) => { event.stopPropagation(); cycleFavoriteLevel(image.id); }}
            style={{ opacity: 1, position: 'static' }}
            title="Increase favorite level"
          >
            <svg width="16" height="16" viewBox="0 0 24 24"
              fill={isFav ? 'var(--favorite)' : 'none'}
              stroke={isFav ? 'var(--favorite)' : 'currentColor'} strokeWidth="2">
              <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
            </svg>
            {favLevel > 0 && <span style={{ marginLeft: 4, fontSize: 11, fontWeight: 700 }}>{favLevel}</span>}
          </button>
        </div>
      </div>
    );
  };

  const renderGridItem = (virtualIndex: number, layoutEntry?: Extract<DateSectionLayoutEntry, { type: 'item' }>) => {
    const sourceIndex = isClientFiltered ? clientFilteredVisible[virtualIndex]?.sourceIndex ?? -1 : virtualIndex;
    const image = isClientFiltered ? clientFilteredVisible[virtualIndex]?.image ?? null : searchResults[sourceIndex];
    const row = Math.floor(virtualIndex / gridColumns);
    const col = virtualIndex % gridColumns;
    const top = layoutEntry?.top ?? row * (gridCellHeight + GRID_GAP);
    const left = layoutEntry?.left ?? col * (gridCellWidth + GRID_GAP);
    const width = layoutEntry?.width ?? gridCellWidth;
    const height = layoutEntry?.height ?? gridCellHeight;

    if (!image) {
      return (
        <div
          key={`slot-grid-${virtualIndex}`}
          className="image-card virtual-grid-item placeholder"
          style={{ top, left, width, height }}
        />
      );
    }

    const favLevel = favorites[image.id] ?? 0;
    const isFav = favLevel > 0;
    const isSelected = selectedIdSet.has(image.id);
    const isUnseen = !seenImageIds[image.id];
    const thumbPriority = getThumbPriority(virtualIndex);
    const previousImage = isClientFiltered
      ? clientFilteredVisible[virtualIndex - 1]?.image ?? null
      : searchResults[sourceIndex - 1] ?? null;
    const dateLabel = formatImageDate(image.createdAt);
    const compactDateLabel = formatCompactImageDate(image.createdAt);
    const showDateSeparator = showDateSeparators && !sectionLayout && dateLabel !== formatImageDate(previousImage?.createdAt);
    return (
      <div
        key={`slot-grid-${virtualIndex}`}
        className={`image-card virtual-grid-item ${isSelected ? 'is-selected' : ''} ${isUnseen ? 'is-unseen' : ''} ${showDateSeparator ? 'has-date-separator' : ''}`}
        style={{ top, left, width, height }}
        draggable
        onDragStart={(event) => handleImageDragStart(event, image.id, image.filename, image.fullUrl)}
        onClick={(event) => {
          markImageSeen(image.id);
          selectImage(
            image,
            isClientFiltered ? filteredOrderedIds : loadedOrderedIds,
            { range: event.shiftKey, toggle: event.ctrlKey || event.metaKey }
          );
        }}
        onDoubleClick={(event) => {
          event.stopPropagation();
          openImageDetail(image, sourceIndex);
        }}
      >
        {showDateSeparator && (
          <div className="date-separator card-date-separator">
            {compactGridActions ? compactDateLabel : dateLabel}
          </div>
        )}
        <CachedImage
          src={image.fileUrl}
          requestSrc={withThumbPriorityParams(image.fileUrl, thumbPriority)}
          fallbackSrc={image.fullUrl}
          cacheKind="thumb"
          alt={image.filename}
          loading={thumbPriority === 'visible' ? 'eager' : 'lazy'}
          decoding="async"
          fetchPriority={thumbPriority === 'visible' ? 'high' : 'auto'}
          style={imageObjectStyle}
        />
        {compactGridActions ? (
          favLevel > 0 && (
            <div className="card-fav-count" title={`Favorite level ${favLevel}`}>
              <svg width="12" height="12" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78z" />
              </svg>
              <span>{favLevel}</span>
            </div>
          )
        ) : (
          <div className="card-fav-stack">
            <button
              className={`card-fav-step ${isFav ? 'active' : ''}`}
              disabled={!isFav}
              onClick={(event) => {
                event.stopPropagation();
                decreaseFavoriteLevel(image.id);
              }}
              title="Decrease favorite level"
            >
              -
            </button>
            <button
              className={`card-fav ${isFav ? 'active' : ''}`}
              onClick={(event) => {
                event.stopPropagation();
                cycleFavoriteLevel(image.id);
              }}
              title="Increase favorite level"
            >
              <svg width="16" height="16" viewBox="0 0 24 24"
                fill={isFav ? 'var(--favorite)' : 'none'}
                stroke={isFav ? 'var(--favorite)' : 'currentColor'} strokeWidth="2">
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
              </svg>
              {favLevel > 0 && <span style={{ marginLeft: 4, fontSize: 11, fontWeight: 700 }}>{favLevel}</span>}
            </button>
          </div>
        )}
        <div className="card-overlay">
          <span className="card-filename">{image.filename}</span>
        </div>
      </div>
    );
  };

  const items: React.ReactNode[] = [];
  if (sectionLayout) {
    const top = Math.max(0, scrollTop - viewportHeight * 0.75);
    const bottom = scrollTop + viewportHeight * 1.75;
    for (const entry of sectionLayout.entries) {
      if (entry.top + entry.height < top || entry.top > bottom) continue;
      if (entry.type === 'header') {
        items.push(
          <div
            key={entry.key}
            className="date-section-header"
            style={{ top: entry.top, height: entry.height }}
          >
            <span>{entry.dateLabel}</span>
            <i aria-hidden="true" />
          </div>
        );
      } else {
        items.push(view.viewMode === 'list'
          ? renderListItem(entry.virtualIndex, entry)
          : renderGridItem(entry.virtualIndex, entry));
      }
    }
  } else {
    for (let i = virtualRange.start; i <= virtualRange.end; i++) {
      items.push(view.viewMode === 'list' ? renderListItem(i) : renderGridItem(i));
    }
  }

  return (
    <div className="grid-container" ref={containerRef}
      onWheel={handleThumbWheel}
      onClick={(e) => { if (e.target === e.currentTarget) closeAllPreviews(); }}>
      <div
        className={`virtual-canvas ${view.viewMode === 'list' ? 'is-list' : 'is-grid'} display-style-${view.displayStyle}`}
        style={{ height: virtualRange.totalHeight }}
      >
        {items}
      </div>
      {isSearching && <div className="virtual-loading-indicator" />}
    </div>
  );
}
