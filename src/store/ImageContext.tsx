'use client';

import React, {
  createContext, useContext, useState, useEffect,
  useCallback, useRef, ReactNode
} from 'react';
import type { ImageFile, KeyBindings, SearchResponse } from '../lib/types';
import { DEFAULT_KEY_BINDINGS } from '../lib/types';
import type { FolderSortBy } from '../lib/viewerUi';
import { removeImageSlot } from '../lib/imageListState';
import { formatDirSet, parseDirSet } from '../lib/pathSet';
import { migrateLegacyPhotoviewerState } from '../lib/localStorageMigration';
import {
  DEFAULT_SHOW_UNSEEN_MARKERS,
  readFavoriteFilterLevelsPreference,
  toggleFavoriteFilterLevel as toggleFavoriteFilterLevelValue,
  type FavoriteFilterLevel,
} from '../lib/browserUiPreferences';
import {
  PREVIEW_TAB_STORAGE_KEY,
  normalizePersistedPreviewTabs,
  serializePersistedPreviewTabs,
  type PersistedPreviewTabs,
} from '../lib/previewTabPersistence';

// ── View settings ──
export type ViewMode = 'grid' | 'list';
export type AspectMode = 'original' | 'square' | 'portrait';
export type DisplayStyle = 'standard' | 'compact' | 'poster';
export type SortBy = 'newest' | 'oldest' | 'created-newest' | 'created-oldest' | 'name' | 'random';

export interface ViewSettings {
  viewMode: ViewMode;
  thumbSize: number;       // px, range 40-600
  aspectMode: AspectMode;
  displayStyle: DisplayStyle;
  columns: number;         // 0 = auto
  sidebarOpen: boolean;
  rightPanelOpen: boolean;
  rightPanelWidth: number; // px
  sortBy: SortBy;
  randomSeed: string;
  folderSortBy: FolderSortBy;
  modalEdgeRatio: number;
  enhanceQueueOpen: boolean;
  dateFrom: string;
  dateTo: string;
  hiddenFolders: string[];
  showUnseenMarkers: boolean;
  foldersExpanded: boolean;
}

const DEFAULT_VIEW: ViewSettings = {
  viewMode: 'grid',
  thumbSize: 200,
  aspectMode: 'original',
  displayStyle: 'standard',
  columns: 0,
  sidebarOpen: true,
  rightPanelOpen: true,
  rightPanelWidth: 320,
  sortBy: 'newest',
  randomSeed: 'default',
  folderSortBy: 'name-asc',
  modalEdgeRatio: 0.28,
  enhanceQueueOpen: true,
  dateFrom: '',
  dateTo: '',
  hiddenFolders: [],
  showUnseenMarkers: DEFAULT_SHOW_UNSEEN_MARKERS,
  foldersExpanded: true,
};

const SCROLL_MEMORY_FLUSH_DELAY_MS = 500;
const SEEN_IMAGES_FLUSH_DELAY_MS = 900;
const FAVORITES_FLUSH_DELAY_MS = 300;
const VIEW_SETTINGS_FLUSH_DELAY_MS = 300;
const AUTO_THUMB_WARM_DELAY_MS = 4200;
const AUTO_THUMB_WARM_LIMIT = 1200;
const MAX_FAVORITE_LEVEL = 5;
const MAX_CLOSED_PREVIEW_TABS = 30;

export function reorderPreviewTabIds(tabIds: string[], id: string, destinationIndex: number): string[] {
  if (!id || !Number.isInteger(destinationIndex)) return tabIds;
  if (new Set(tabIds).size !== tabIds.length) return tabIds;
  const sourceIndex = tabIds.indexOf(id);
  if (sourceIndex < 0 || destinationIndex < 0 || destinationIndex >= tabIds.length) return tabIds;
  if (sourceIndex === destinationIndex) return tabIds;

  const next = tabIds.filter((tabId) => tabId !== id);
  next.splice(destinationIndex, 0, id);
  return next;
}

function normalizeStoredView(value: unknown): ViewSettings {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return { ...DEFAULT_VIEW };
  }

  const stored = value as Partial<ViewSettings>;
  return {
    ...DEFAULT_VIEW,
    ...stored,
    // `columns` belonged to an older UI. The current UI is size-driven and
    // has no way to change or clear a persisted fixed-column value.
    columns: 0,
    // Older snapshots predate this field. An invalid value must not collapse
    // the only folder-management surface after reload.
    foldersExpanded: typeof stored.foldersExpanded === 'boolean'
      ? stored.foldersExpanded
      : DEFAULT_VIEW.foldersExpanded,
  };
}

// ── Context shape ──
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

/**
 * Keep the permissive per-level normalization used by existing local data, but
 * distinguish an invalid document from an intentionally empty favorite map.
 */
function readStoredFavoritesSnapshot(serialized: string): Record<string, number> | null {
  const parsed: unknown = JSON.parse(serialized);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null;
  return normalizeFavorites(parsed);
}

function normalizeSeenImageIds(value: unknown): Record<string, true> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const normalized: Record<string, true> = {};
  for (const [id, marker] of Object.entries(value)) {
    if (id && marker) normalized[id] = true;
  }
  return normalized;
}

function mergeSeenImageIds(
  first: Record<string, true>,
  second: Record<string, true>,
): Record<string, true> {
  return { ...first, ...second };
}

function mergeFavorites(
  first: Record<string, number>,
  second: Record<string, number>,
  exactSecondIds: ReadonlySet<string> = new Set()
): Record<string, number> {
  const merged = { ...first };
  for (const [id, level] of Object.entries(second)) {
    merged[id] = exactSecondIds.has(id) ? level : Math.max(merged[id] ?? 0, level);
  }
  for (const id of exactSecondIds) {
    if (!(id in second)) delete merged[id];
  }
  return merged;
}

function reconcileFavoriteWriteResponse(
  serverFavorites: Record<string, number>,
  requestSnapshot: Record<string, number>,
  currentFavorites: Record<string, number>,
) {
  const reconciled = { ...serverFavorites };
  const candidateIds = new Set([...Object.keys(requestSnapshot), ...Object.keys(currentFavorites)]);
  for (const id of candidateIds) {
    const requestedLevel = requestSnapshot[id] ?? 0;
    const currentLevel = currentFavorites[id] ?? 0;
    if (requestedLevel === currentLevel) continue;
    if (currentLevel > 0) reconciled[id] = currentLevel;
    else delete reconciled[id];
  }

  const currentIds = Object.keys(currentFavorites);
  const reconciledIds = Object.keys(reconciled);
  if (currentIds.length === reconciledIds.length
    && currentIds.every((id) => currentFavorites[id] === reconciled[id])) {
    return currentFavorites;
  }
  return reconciled;
}

function writeJsonLocalStorage(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // ignore storage quota or private-mode failures
  }
}

function withIndexToken(url: string, indexToken: string | null) {
  if (!indexToken || /[?&]indexToken=/.test(url)) return url;
  return `${url}${url.includes('?') ? '&' : '?'}indexToken=${encodeURIComponent(indexToken)}`;
}

type ScanEventStage = 'preparing' | 'scanning' | 'complete';

function normalizeScanEventProgress(data: Record<string, unknown>) {
  const { processed, total, newFiles } = data;
  if (![processed, total, newFiles].every((value) => typeof value === 'number' && Number.isFinite(value) && value >= 0)) {
    return null;
  }
  const stage: ScanEventStage | undefined = data.stage === 'preparing'
    || data.stage === 'scanning'
    || data.stage === 'complete'
    ? data.stage
    : undefined;
  return {
    processed: processed as number,
    total: total as number,
    newFiles: newFiles as number,
    stage,
    message: typeof data.message === 'string' ? data.message : undefined,
  };
}

interface Ctx {
  // App phase
  phase: 'landing' | 'scanning' | 'viewer';
  setPhase: (p: 'landing' | 'scanning' | 'viewer') => void;
  dirPath: string;
  setDirPath: (d: string) => void;
  indexToken: string | null;

  // Scan progress
  scanProgress: { processed: number; total: number; newFiles: number; stage?: 'preparing' | 'scanning' | 'complete'; message?: string } | null;
  scanError: string | null;
  dismissScanError: () => void;

  // Search
  searchQuery: string;
  setSearchQuery: (q: string) => void;
  searchResults: Array<ImageFile | null>;
  searchTotal: number;
  isSearching: boolean;
  searchError: string | null;
  ensureSearchRange: (startIndex: number, endIndex: number) => void;
  retrySearch: () => void;
  dismissSearchError: () => void;

  // Favorites
  favorites: Record<string, number>;
  toggleFavorite: (id: string) => void;
  cycleFavoriteLevel: (id: string) => void;
  decreaseFavoriteLevel: (id: string) => void;
  clearFavorite: (id: string) => void;
  showFavOnly: boolean;
  setShowFavOnly: (v: boolean) => void;
  showUnfavOnly: boolean;
  setShowUnfavOnly: (v: boolean) => void;
  favoriteFilterLevels: FavoriteFilterLevel[];
  toggleFavoriteFilterLevel: (v: FavoriteFilterLevel) => void;
  clearFavoriteFilterLevels: () => void;
  showEnhancedOnly: boolean;
  setShowEnhancedOnly: (v: boolean) => void;
  enhancedSourceIds: Record<string, true>;

  // Modal
  selectedIndex: number | null;
  setSelectedIndex: (i: number | null) => void;
  modalImageIds: string[];
  setModalImageIds: (ids: string[]) => void;
  openModalAtImage: (id: string, fallbackIndex: number | null, orderedIds?: string[]) => void;
  selectedIds: string[];
  selectImage: (
    image: ImageFile,
    orderedIds: string[],
    options?: { range?: boolean; toggle?: boolean }
  ) => void;
  clearSelection: () => void;

  // Right preview panel
  previewTabIds: string[];
  pinnedPreviewIds: string[];
  activePreviewId: string | null;
  previewById: Record<string, ImageFile>;
  showPreviewImage: (image: ImageFile, options?: { makeActive?: boolean }) => void;
  openPreviewTab: (image: ImageFile, options?: { makeActive?: boolean; pin?: boolean }) => void;
  setActivePreviewId: (id: string | null) => void;
  closePreviewTab: (id: string) => void;
  reorderPreviewTab: (id: string, destinationIndex: number) => void;
  togglePinPreviewTab: (id: string) => void;
  closedPreviewTabCount: number;
  restoreLastClosedPreview: () => void;
  closeAllPreviews: () => void;
  seenImageIds: Record<string, true>;
  markImageSeen: (id: string) => void;
  revealImageId: string | null;
  requestRevealImage: (id: string) => void;
  consumeRevealImage: () => void;

  // Settings
  keyBindings: KeyBindings;
  setKeyBindings: (kb: KeyBindings) => void;
  confirmBeforeDelete: boolean;
  setConfirmBeforeDelete: (v: boolean) => void;
  showSettings: boolean;
  setShowSettings: (v: boolean) => void;

  // View settings
  view: ViewSettings;
  setView: (v: Partial<ViewSettings>) => void;
  setSearchScrollPosition: (key: string, value: number) => void;
  getSearchScrollPosition: (key: string) => number | null;

  // lightweight profiler
  perfEnabled: boolean;
  setPerfEnabled: (v: boolean) => void;
  perfStats: { searchCount: number; lastSearchMs: number; avgSearchMs: number };

  // Actions
  startScan: (options?: { full?: boolean; dir?: string; onComplete?: (dir: string) => void }) => void;
  deleteImage: (id: string) => Promise<boolean>;
  openExternal: (id: string) => void;
  totalIndexed: number;
  setTotalIndexed: (n: number) => void;
}

const ImageContext = createContext<Ctx | undefined>(undefined);

export function ImageProvider({ children }: { children: ReactNode }) {
  const [phase, setPhase] = useState<'landing' | 'scanning' | 'viewer'>('landing');
  const [dirPath, setDirPath] = useState('');
  const [indexToken, setIndexToken] = useState<string | null>(null);
  const [scanProgress, setScanProgress] = useState<{ processed: number; total: number; newFiles: number; stage?: 'preparing' | 'scanning' | 'complete'; message?: string } | null>(null);
  const [scanError, setScanError] = useState<string | null>(null);
  const scanRunRef = useRef(0);

  const [searchQuery, setSearchQueryRaw] = useState('');
  const [searchResults, setSearchResults] = useState<Array<ImageFile | null>>([]);
  const [searchTotal, setSearchTotal] = useState(0);
  const [isSearching, setIsSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [favorites, setFavorites] = useState<Record<string, number>>({});
  const [showFavOnlyState, setShowFavOnlyState] = useState(false);
  const [showUnfavOnlyState, setShowUnfavOnlyState] = useState(false);
  const [favoriteFilterLevels, setFavoriteFilterLevels] = useState<FavoriteFilterLevel[]>([]);
  const [showEnhancedOnlyState, setShowEnhancedOnlyState] = useState(false);
  const [enhancedSourceIds, setEnhancedSourceIds] = useState<Record<string, true>>({});
  const [enhanceJobsActive, setEnhanceJobsActive] = useState(false);

  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  const [modalImageIds, setModalImageIdsState] = useState<string[]>([]);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [selectionAnchorId, setSelectionAnchorId] = useState<string | null>(null);
  const [previewTabIds, setPreviewTabIds] = useState<string[]>([]);
  const [pinnedPreviewIds, setPinnedPreviewIds] = useState<string[]>([]);
  const [activePreviewId, setActivePreviewIdState] = useState<string | null>(null);
  const [previewById, setPreviewById] = useState<Record<string, ImageFile>>({});
  const [closedPreviewStack, setClosedPreviewStack] = useState<string[]>([]);
  const [closedPreviewById, setClosedPreviewById] = useState<Record<string, ImageFile>>({});
  const [keyBindings, setKeyBindingsState] = useState<KeyBindings>(DEFAULT_KEY_BINDINGS);
  const [confirmBeforeDelete, setConfirmBeforeDeleteState] = useState(true);
  const [showSettings, setShowSettings] = useState(false);
  const [totalIndexed, setTotalIndexed] = useState(0);
  const [view, setViewState] = useState<ViewSettings>(DEFAULT_VIEW);
  const [perfEnabled, setPerfEnabledState] = useState(false);
  const [perfStats, setPerfStats] = useState({ searchCount: 0, lastSearchMs: 0, avgSearchMs: 0 });
  const [seenImageIds, setSeenImageIds] = useState<Record<string, true>>({});
  const [revealImageId, setRevealImageId] = useState<string | null>(null);
  const [favoritesHydrated, setFavoritesHydrated] = useState(false);
  const [uiPreferencesHydrated, setUiPreferencesHydrated] = useState(false);
  const [previewTabsPersistenceReady, setPreviewTabsPersistenceReady] = useState(false);

  useEffect(() => {
    const retainedIds = new Set(closedPreviewStack);
    setClosedPreviewById((previous) => {
      const keys = Object.keys(previous);
      if (keys.every((id) => retainedIds.has(id))) return previous;
      const next: Record<string, ImageFile> = {};
      for (const id of closedPreviewStack) {
        if (previous[id]) next[id] = previous[id];
      }
      return next;
    });
  }, [closedPreviewStack]);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const scrollMemoryRef = useRef<Record<string, number>>({});
  const scrollMemoryFlushRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const seenImageIdsRef = useRef<Record<string, true>>({});
  const seenImagesFlushRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const favoritesFlushRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const viewSettingsFlushRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pendingViewSettingsRef = useRef<ViewSettings | null>(null);
  const searchQueryRef = useRef('');
  const favoritesRef = useRef<Record<string, number>>({});
  const favoriteLocalStorageReadOnlyRef = useRef(false);
  const favoriteServerBaseRef = useRef<Record<string, number>>({});
  const favoritesHydratedRef = useRef(false);
  const favoriteHydrationDirtyIdsRef = useRef<Set<string>>(new Set());
  const searchMetaRef = useRef<{
    query: string;
    sortBy: SortBy;
    randomSeed: string;
    dateFrom: string;
    dateTo: string;
    hiddenFolders: string[];
    hiddenFoldersKey: string;
    dirPath: string;
    indexToken: string | null;
  }>({
    query: '',
    sortBy: DEFAULT_VIEW.sortBy,
    randomSeed: DEFAULT_VIEW.randomSeed,
    dateFrom: '',
    dateTo: '',
    hiddenFolders: [],
    hiddenFoldersKey: '',
    dirPath: '',
    indexToken: null,
  });
  const searchGenerationRef = useRef(0);
  const committedSearchGenerationRef = useRef(0);
  const lastFailedSearchPageRef = useRef<number | null>(null);
  const searchResultsRef = useRef<Array<ImageFile | null>>([]);
  const searchTotalRef = useRef(0);
  const pendingPreviewTabsRestoreRef = useRef<PersistedPreviewTabs | null>(null);
  const previewTabsHadStoredValueRef = useRef(false);
  const previewTabsUserModifiedRef = useRef(false);
  const loadedPagesRef = useRef<Set<string>>(new Set());
  const pendingPagesRef = useRef<Set<string>>(new Set());
  const pendingSearchControllersRef = useRef<Map<string, AbortController>>(new Map());
  const warmedThumbDirRef = useRef('');
  const PAGE_SIZE = 100;
  const buildHiddenFoldersKey = useCallback(
    (folders: string[]) => folders.slice().sort().join('\u0001'),
    []
  );
  const flushViewSettings = useCallback(() => {
    const snapshot = pendingViewSettingsRef.current;
    pendingViewSettingsRef.current = null;
    if (snapshot) writeJsonLocalStorage('pvu_view', snapshot);
  }, []);
  const scheduleViewSettingsPersist = useCallback((next: ViewSettings) => {
    pendingViewSettingsRef.current = next;
    if (viewSettingsFlushRef.current) {
      clearTimeout(viewSettingsFlushRef.current);
    }
    viewSettingsFlushRef.current = setTimeout(() => {
      viewSettingsFlushRef.current = null;
      flushViewSettings();
    }, VIEW_SETTINGS_FLUSH_DELAY_MS);
  }, [flushViewSettings]);
  const mergeSharedSeen = useCallback((sharedSeen: Record<string, true>) => {
    const merged = mergeSeenImageIds(sharedSeen, seenImageIdsRef.current);
    seenImageIdsRef.current = merged;
    setSeenImageIds((currentSeen) => mergeSeenImageIds(sharedSeen, currentSeen));
    return merged;
  }, []);
  const syncSeenToShared = useCallback((snapshot: Record<string, true>) => {
    if (Object.keys(snapshot).length === 0) return;
    fetch('/api/seen', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ seen: snapshot }),
    })
      .then(async (response) => {
        if (!response.ok) return;
        const data = await response.json();
        if (data?.malformed) return;
        mergeSharedSeen(normalizeSeenImageIds(data?.seen));
      })
      .catch(() => {});
  }, [mergeSharedSeen]);

  // ── Load favorites + view settings from localStorage ──
  useEffect(() => {
    migrateLegacyPhotoviewerState();
    let localFavorites: Record<string, number> = {};
    const stored = localStorage.getItem('pvu_favorites');
    try {
      // A backup is recovery data only when the primary key is absent. An
      // intentionally empty or malformed primary must never be replaced by it.
      const source = stored === null
        ? localStorage.getItem('pvu_favorites_backup')
        : stored;
      if (source !== null) {
        const parsed = readStoredFavoritesSnapshot(source);
        if (parsed === null) {
          favoriteLocalStorageReadOnlyRef.current = true;
        } else {
          localFavorites = parsed;
          setFavorites(localFavorites);
          favoritesRef.current = localFavorites;
        }
      }
    } catch {
      // Preserve corrupt recovery data until a user explicitly changes
      // favorites; automatic hydration must not replace the only evidence.
      favoriteLocalStorageReadOnlyRef.current = true;
    }
    fetch('/api/favorites')
      .then((r) => r.json())
      .then((data) => {
        const serverFavorites = normalizeFavorites(data?.favorites);
        favoriteServerBaseRef.current = serverFavorites;
        const dirtyIds = new Set(favoriteHydrationDirtyIdsRef.current);
        setFavorites((currentFavorites) => mergeFavorites(
          serverFavorites,
          currentFavorites,
          dirtyIds
        ));
      })
      .catch(() => {})
      .finally(() => {
        favoritesHydratedRef.current = true;
        favoriteHydrationDirtyIdsRef.current.clear();
        setFavoritesHydrated(true);
      });
    try {
      const sv = localStorage.getItem('pvu_view');
      if (sv) {
        const normalizedView = normalizeStoredView(JSON.parse(sv));
        setViewState(normalizedView);
        writeJsonLocalStorage('pvu_view', normalizedView);
      }
    } catch { /* ignore */ }
    try {
      const pinned = localStorage.getItem('pvu_pinned_tabs');
      if (pinned) {
        const parsed = JSON.parse(pinned);
        if (Array.isArray(parsed)) {
          setPinnedPreviewIds(parsed.filter((v): v is string => typeof v === 'string'));
        }
      }
    } catch { /* ignore */ }
    try {
      const storedPreviewTabs = localStorage.getItem(PREVIEW_TAB_STORAGE_KEY);
      if (storedPreviewTabs) {
        previewTabsHadStoredValueRef.current = true;
        const restored = normalizePersistedPreviewTabs(JSON.parse(storedPreviewTabs));
        if (restored.tabIds.length > 0) {
          pendingPreviewTabsRestoreRef.current = restored;
        } else {
          setPreviewTabsPersistenceReady(true);
        }
      } else {
        setPreviewTabsPersistenceReady(true);
      }
    } catch {
      // A malformed snapshot intentionally restores no tabs. It is safe to
      // replace only after the user next changes the preview tab state.
      setPreviewTabsPersistenceReady(true);
    }
    try {
      const perf = localStorage.getItem('pvu_perf_enabled');
      if (perf === '1') setPerfEnabledState(true);
    } catch { /* ignore */ }
    try {
      const favOnly = localStorage.getItem('pvu_fav_only');
      if (favOnly === '1') setShowFavOnlyState(true);
      const unfavOnly = localStorage.getItem('pvu_unfav_only');
      if (unfavOnly === '1') setShowUnfavOnlyState(true);
      setFavoriteFilterLevels(readFavoriteFilterLevelsPreference(
        localStorage.getItem('pvu_fav_levels'),
        localStorage.getItem('pvu_fav_level')
      ));
      const enhancedOnly = localStorage.getItem('pvu_enhanced_only');
      if (enhancedOnly === '1') setShowEnhancedOnlyState(true);
    } catch { /* ignore */ }
    try {
      const memory = localStorage.getItem('pvu_scroll_memory');
      if (memory) {
        const parsed = JSON.parse(memory);
        if (parsed && typeof parsed === 'object') {
          scrollMemoryRef.current = parsed as Record<string, number>;
        }
      }
    } catch { /* ignore */ }
    try {
      const seen = localStorage.getItem('pvu_seen_images');
      if (seen) {
        const normalized = normalizeSeenImageIds(JSON.parse(seen));
        seenImageIdsRef.current = normalized;
        setSeenImageIds(normalized);
      }
    } catch { /* ignore */ }
    fetch('/api/seen')
      .then((response) => response.json())
      .then((data) => {
        if (data?.malformed) return;
        const localSeen = seenImageIdsRef.current;
        const merged = mergeSharedSeen(normalizeSeenImageIds(data?.seen));
        if (Object.keys(localSeen).some((id) => !data?.seen?.[id])) {
          syncSeenToShared(merged);
        }
      })
      .catch(() => {});
    setUiPreferencesHydrated(true);
  }, [mergeSharedSeen, syncSeenToShared]);

  // ── Persist favorites ──
  useEffect(() => {
    if (!favoritesHydrated) return;
    favoritesRef.current = favorites;
    if (favoritesFlushRef.current) {
      clearTimeout(favoritesFlushRef.current);
    }
    favoritesFlushRef.current = setTimeout(() => {
      favoritesFlushRef.current = null;
      const snapshot = favoritesRef.current;
      const serialized = JSON.stringify(snapshot);
      try {
        if (!favoriteLocalStorageReadOnlyRef.current) {
          localStorage.setItem('pvu_favorites', serialized);
          if (Object.keys(snapshot).length > 0) {
            localStorage.setItem('pvu_favorites_backup', serialized);
          }
        }
      } catch { /* ignore */ }
      fetch('/api/favorites', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          favorites: snapshot,
          baseFavorites: favoriteServerBaseRef.current,
        }),
      })
        .then(async (response) => {
          if (!response.ok) return;
          const data = await response.json();
          const serverFavorites = normalizeFavorites(data?.favorites);
          favoriteServerBaseRef.current = serverFavorites;
          setFavorites((currentFavorites) => reconcileFavoriteWriteResponse(
            serverFavorites,
            snapshot,
            currentFavorites,
          ));
        })
        .catch(() => {});
    }, FAVORITES_FLUSH_DELAY_MS);
  }, [favorites, favoritesHydrated]);

  useEffect(() => {
    try {
      localStorage.setItem('pvu_pinned_tabs', JSON.stringify(pinnedPreviewIds));
    } catch { /* ignore */ }
  }, [pinnedPreviewIds]);

  useEffect(() => {
    if (!previewTabsHadStoredValueRef.current && !previewTabsUserModifiedRef.current) return;
    if (!previewTabsPersistenceReady && !previewTabsUserModifiedRef.current) return;
    writeJsonLocalStorage(
      PREVIEW_TAB_STORAGE_KEY,
      serializePersistedPreviewTabs(previewTabIds, activePreviewId),
    );
  }, [activePreviewId, previewTabIds, previewTabsPersistenceReady]);

  useEffect(() => {
    try {
      localStorage.setItem('pvu_perf_enabled', perfEnabled ? '1' : '0');
    } catch { /* ignore */ }
  }, [perfEnabled]);

  useEffect(() => {
    if (!uiPreferencesHydrated) return;
    try {
      localStorage.setItem('pvu_fav_only', showFavOnlyState ? '1' : '0');
      localStorage.setItem('pvu_unfav_only', showUnfavOnlyState ? '1' : '0');
      localStorage.setItem('pvu_fav_levels', JSON.stringify(favoriteFilterLevels));
      localStorage.setItem('pvu_enhanced_only', showEnhancedOnlyState ? '1' : '0');
    } catch { /* ignore */ }
  }, [favoriteFilterLevels, showEnhancedOnlyState, showFavOnlyState, showUnfavOnlyState, uiPreferencesHydrated]);

  const refreshEnhancedSources = useCallback(async () => {
    try {
      const res = await fetch('/api/enhance/jobs', { cache: 'no-store' });
      const data = await res.json();
      if (!res.ok || !Array.isArray(data.jobs)) return;
      const next: Record<string, true> = {};
      let hasActiveJobs = false;
      for (const job of data.jobs) {
        if (job?.status === 'queued' || job?.status === 'running') {
          hasActiveJobs = true;
        }
        if (job?.status === 'succeeded' && typeof job.sourceId === 'string' && job.outputPath) {
          next[job.sourceId] = true;
        }
      }
      setEnhancedSourceIds(next);
      setEnhanceJobsActive(hasActiveJobs);
    } catch {
      // Enhancement filtering is best-effort; the viewer remains usable.
    }
  }, []);

  useEffect(() => {
    void refreshEnhancedSources();
    const onChanged = () => void refreshEnhancedSources();
    window.addEventListener('pvu-enhance-jobs-changed', onChanged);
    return () => window.removeEventListener('pvu-enhance-jobs-changed', onChanged);
  }, [refreshEnhancedSources]);

  useEffect(() => {
    if (!enhanceJobsActive && !showEnhancedOnlyState) return;
    const timer = window.setInterval(() => {
      void refreshEnhancedSources();
    }, 1000);
    return () => window.clearInterval(timer);
  }, [enhanceJobsActive, refreshEnhancedSources, showEnhancedOnlyState]);

  useEffect(() => {
    if (showFavOnlyState && showUnfavOnlyState) {
      setShowUnfavOnlyState(false);
    }
  }, [showFavOnlyState, showUnfavOnlyState]);

  useEffect(() => {
    searchResultsRef.current = searchResults;
  }, [searchResults]);

  useEffect(() => {
    searchTotalRef.current = searchTotal;
  }, [searchTotal]);

  useEffect(() => () => {
    if (favoritesFlushRef.current) {
      clearTimeout(favoritesFlushRef.current);
      favoritesFlushRef.current = null;
      const snapshot = favoritesRef.current;
      const serialized = JSON.stringify(snapshot);
      try {
        if (!favoriteLocalStorageReadOnlyRef.current) {
          localStorage.setItem('pvu_favorites', serialized);
          if (Object.keys(snapshot).length > 0) {
            localStorage.setItem('pvu_favorites_backup', serialized);
          }
        }
      } catch { /* ignore */ }
    }
    if (viewSettingsFlushRef.current) {
      clearTimeout(viewSettingsFlushRef.current);
      viewSettingsFlushRef.current = null;
      flushViewSettings();
    }
    if (scrollMemoryFlushRef.current) {
      clearTimeout(scrollMemoryFlushRef.current);
    }
    if (seenImagesFlushRef.current) {
      clearTimeout(seenImagesFlushRef.current);
    }
    writeJsonLocalStorage('pvu_scroll_memory', scrollMemoryRef.current);
    writeJsonLocalStorage('pvu_seen_images', seenImageIdsRef.current);
    syncSeenToShared(seenImageIdsRef.current);
  }, [flushViewSettings, syncSeenToShared]);

  // ── Load key bindings from server ──
  useEffect(() => {
    fetch('/api/settings')
      .then(r => r.json())
      .then(data => {
        if (data.keyBindings) setKeyBindingsState({ ...DEFAULT_KEY_BINDINGS, ...data.keyBindings });
        if (typeof data.confirmBeforeDelete === 'boolean') {
          setConfirmBeforeDeleteState(data.confirmBeforeDelete);
        }
      })
      .catch(() => {});
  }, []);

  const setKeyBindings = useCallback((kb: KeyBindings) => {
    setKeyBindingsState(kb);
    fetch('/api/settings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ keyBindings: kb }),
    }).catch(() => {});
  }, []);

  const setConfirmBeforeDelete = useCallback((v: boolean) => {
    setConfirmBeforeDeleteState(v);
    fetch('/api/settings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ confirmBeforeDelete: v }),
    }).catch(() => {});
  }, []);

  const toggleFavorite = useCallback((id: string) => {
    favoriteLocalStorageReadOnlyRef.current = false;
    if (!favoritesHydratedRef.current) favoriteHydrationDirtyIdsRef.current.add(id);
    setFavorites(prev => {
      const next = { ...prev };
      if ((next[id] ?? 0) > 0) delete next[id];
      else next[id] = 1;
      return next;
    });
  }, []);

  const cycleFavoriteLevel = useCallback((id: string) => {
    favoriteLocalStorageReadOnlyRef.current = false;
    if (!favoritesHydratedRef.current) favoriteHydrationDirtyIdsRef.current.add(id);
    setFavorites((prev) => {
      const next = { ...prev };
      const current = next[id] ?? 0;
      const upcoming = Math.min(MAX_FAVORITE_LEVEL, current + 1);
      next[id] = upcoming;
      return next;
    });
  }, []);

  const clearFavorite = useCallback((id: string) => {
    favoriteLocalStorageReadOnlyRef.current = false;
    if (!favoritesHydratedRef.current) favoriteHydrationDirtyIdsRef.current.add(id);
    setFavorites((prev) => {
      if (!(id in prev)) return prev;
      const next = { ...prev };
      delete next[id];
      return next;
    });
  }, []);

  const decreaseFavoriteLevel = useCallback((id: string) => {
    favoriteLocalStorageReadOnlyRef.current = false;
    if (!favoritesHydratedRef.current) favoriteHydrationDirtyIdsRef.current.add(id);
    setFavorites((prev) => {
      const current = prev[id] ?? 0;
      if (current <= 0) return prev;
      const next = { ...prev };
      if (current <= 1) {
        delete next[id];
      } else {
        next[id] = current - 1;
      }
      return next;
    });
  }, []);

  const setShowFavOnly = useCallback((value: boolean) => {
    setShowFavOnlyState(value);
    if (value) {
      setShowUnfavOnlyState(false);
    }
  }, []);

  const toggleFavoriteFilterLevel = useCallback((value: FavoriteFilterLevel) => {
    setFavoriteFilterLevels((prev) => toggleFavoriteFilterLevelValue(prev, value));
    setShowFavOnlyState(true);
    setShowUnfavOnlyState(false);
  }, []);

  const clearFavoriteFilterLevels = useCallback(() => {
    setFavoriteFilterLevels([]);
  }, []);

  const setShowUnfavOnly = useCallback((value: boolean) => {
    setShowUnfavOnlyState(value);
    if (value) {
      setShowFavOnlyState(false);
    }
  }, []);

  const setShowEnhancedOnly = useCallback((value: boolean) => {
    setShowEnhancedOnlyState(value);
    if (value) void refreshEnhancedSources();
  }, [refreshEnhancedSources]);

  // ── Search (debounced) ──
  const getSearchPageKey = useCallback((generation: number, page: number) => `${generation}:${page}`, []);

  const isSearchPageComplete = useCallback((page: number) => {
    const total = searchTotalRef.current;
    if (total <= 0) return false;
    const start = page * PAGE_SIZE;
    const end = Math.min(total, start + PAGE_SIZE);
    if (start >= end) return true;
    const results = searchResultsRef.current;
    for (let i = start; i < end; i++) {
      if (!results[i]) return false;
    }
    return true;
  }, []);

  const abortPendingSearchRequests = useCallback(() => {
    for (const controller of pendingSearchControllersRef.current.values()) {
      controller.abort();
    }
    pendingSearchControllersRef.current = new Map();
  }, []);

  const doSearchPage = useCallback(async (
    query: string,
    page: number,
    sortBy: SortBy,
    randomSeed: string,
    dateFrom: string,
    dateTo: string,
    hiddenFolders: string[],
    currentDirPath: string,
    currentIndexToken: string | null,
    generation = searchGenerationRef.current
  ) => {
    const pageKey = getSearchPageKey(generation, page);
    if (pendingPagesRef.current.has(pageKey)) return;
    if (loadedPagesRef.current.has(pageKey) && isSearchPageComplete(page)) return;
    loadedPagesRef.current.delete(pageKey);

    pendingPagesRef.current.add(pageKey);
    setIsSearching(true);
    if (generation === searchGenerationRef.current) setSearchError(null);
    const startedAt = performance.now();
    const abortController = new AbortController();
    pendingSearchControllersRef.current.set(pageKey, abortController);
    try {
      const fromParam = dateFrom ? `&dateFrom=${encodeURIComponent(dateFrom)}` : '';
      const toParam = dateTo ? `&dateTo=${encodeURIComponent(dateTo)}` : '';
      const randomSeedParam = sortBy === 'random'
        ? `&randomSeed=${encodeURIComponent(randomSeed || DEFAULT_VIEW.randomSeed)}`
        : '';
      const hiddenParam = hiddenFolders.length > 0
        ? `&hiddenFolders=${encodeURIComponent(JSON.stringify(hiddenFolders))}`
        : '';
      const dirParam = currentDirPath ? `&dir=${encodeURIComponent(currentDirPath)}` : '';
      const indexTokenParam = currentIndexToken ? `&indexToken=${encodeURIComponent(currentIndexToken)}` : '';
      const url = `/api/search?q=${encodeURIComponent(query)}&page=${page}&size=${PAGE_SIZE}&sortBy=${sortBy}${randomSeedParam}${fromParam}${toParam}${hiddenParam}${dirParam}${indexTokenParam}`;
      const res = await fetch(url, { signal: abortController.signal });
      const data = await res.json().catch(() => null) as SearchResponse | { error?: unknown } | null;
      if (!res.ok) {
        const errorData = data as { error?: unknown } | null;
        const message = errorData && typeof errorData.error === 'string'
          ? errorData.error
          : `Search request failed (${res.status}).`;
        throw new Error(message);
      }
      if (!data || !Array.isArray((data as SearchResponse).results)
        || !Number.isFinite((data as SearchResponse).total)) {
        throw new Error('Search returned an invalid response.');
      }
      const searchData = data as SearchResponse;

      // Ignore stale responses from previous query/sort/date state.
      const meta = searchMetaRef.current;
      if (
        generation !== searchGenerationRef.current ||
        meta.query !== query ||
        meta.sortBy !== sortBy ||
        meta.randomSeed !== randomSeed ||
        meta.dateFrom !== dateFrom ||
        meta.dateTo !== dateTo ||
        meta.hiddenFoldersKey !== buildHiddenFoldersKey(hiddenFolders) ||
        meta.dirPath !== currentDirPath ||
        meta.indexToken !== currentIndexToken
      ) {
        return;
      }

      const withFavs = searchData.results.map((img) => ({
        ...img,
        fileUrl: withIndexToken(img.fileUrl, currentIndexToken),
        displayUrl: withIndexToken(img.displayUrl, currentIndexToken),
        fullUrl: withIndexToken(img.fullUrl, currentIndexToken),
        isFavorite: !!favoritesRef.current[img.id],
      }));

      const replacePreviousResults = committedSearchGenerationRef.current !== generation;
      committedSearchGenerationRef.current = generation;
      lastFailedSearchPageRef.current = null;
      searchTotalRef.current = searchData.total;
      setSearchTotal((prevTotal) => (prevTotal === searchData.total ? prevTotal : searchData.total));
      setSearchResults((prev) => {
        const needsResize = replacePreviousResults || prev.length !== searchData.total;
        const next = needsResize ? Array<ImageFile | null>(searchData.total).fill(null) : [...prev];
        if (needsResize && !replacePreviousResults) {
          const keep = Math.min(prev.length, next.length);
          for (let i = 0; i < keep; i++) next[i] = prev[i];
        }
        const base = page * PAGE_SIZE;
        for (let i = 0; i < withFavs.length; i++) {
          const idx = base + i;
          if (idx < next.length) next[idx] = withFavs[i];
        }
        searchResultsRef.current = next;
        return next;
      });
      loadedPagesRef.current.add(pageKey);
      setPerfStats((prev) => {
        const lastSearchMs = Math.round((performance.now() - startedAt) * 10) / 10;
        const searchCount = prev.searchCount + 1;
        const avgSearchMs = Math.round((((prev.avgSearchMs * prev.searchCount) + lastSearchMs) / searchCount) * 10) / 10;
        return { searchCount, lastSearchMs, avgSearchMs };
      });
    } catch (e) {
      if (abortController.signal.aborted) return;
      if (generation !== searchGenerationRef.current) return;
      lastFailedSearchPageRef.current = page;
      const message = e instanceof Error && e.message
        ? e.message
        : 'Search failed. Please retry.';
      setSearchError(message);
      console.error('Search failed', e);
    } finally {
      pendingSearchControllersRef.current.delete(pageKey);
      if (generation === searchGenerationRef.current) {
        pendingPagesRef.current.delete(pageKey);
      }
      if (generation === searchGenerationRef.current && pendingPagesRef.current.size === 0) {
        setIsSearching(false);
      }
    }
  }, [buildHiddenFoldersKey, getSearchPageKey, isSearchPageComplete]);

  const resetSearch = useCallback((
    query: string,
    sortBy: SortBy,
    randomSeed: string,
    dateFrom: string,
    dateTo: string,
    hiddenFolders: string[],
    currentDirPath: string,
    currentIndexToken: string | null,
  ) => {
    const preserveCurrentResults = searchMetaRef.current.dirPath === currentDirPath;
    searchMetaRef.current = {
      query,
      sortBy,
      randomSeed,
      dateFrom,
      dateTo,
      hiddenFolders,
      hiddenFoldersKey: buildHiddenFoldersKey(hiddenFolders),
      dirPath: currentDirPath,
      indexToken: currentIndexToken,
    };
    searchGenerationRef.current += 1;
    abortPendingSearchRequests();
    loadedPagesRef.current = new Set();
    pendingPagesRef.current = new Set();
    lastFailedSearchPageRef.current = null;
    setSearchError(null);
    if (!preserveCurrentResults) {
      searchTotalRef.current = 0;
      searchResultsRef.current = [];
      setSearchTotal(0);
      setSearchResults([]);
    }
    setIsSearching(false);
  }, [abortPendingSearchRequests, buildHiddenFoldersKey]);

  const ensureSearchRange = useCallback((startIndex: number, endIndex: number) => {
    if (startIndex > endIndex) return;
    if (searchTotal <= 0 && searchResults.length === 0) return;

    const safeStart = Math.max(0, startIndex);
    const safeEnd = Math.min(Math.max(0, endIndex), Math.max(0, searchTotal - 1));
    if (safeStart > safeEnd) return;

    const startPage = Math.floor(safeStart / PAGE_SIZE);
    const endPage = Math.floor(safeEnd / PAGE_SIZE);
    const meta = searchMetaRef.current;
    const generation = searchGenerationRef.current;
    for (let page = startPage; page <= endPage; page++) {
      const pageKey = getSearchPageKey(generation, page);
      if (pendingPagesRef.current.has(pageKey)) continue;
      if (loadedPagesRef.current.has(pageKey) && isSearchPageComplete(page)) continue;
      loadedPagesRef.current.delete(pageKey);
      void doSearchPage(
        meta.query,
        page,
        meta.sortBy,
        meta.randomSeed,
        meta.dateFrom,
        meta.dateTo,
        meta.hiddenFolders,
        meta.dirPath,
        meta.indexToken,
        generation
      );
    }
  }, [doSearchPage, getSearchPageKey, isSearchPageComplete, searchResults.length, searchTotal]);

  const retrySearch = useCallback(() => {
    const meta = searchMetaRef.current;
    const generation = searchGenerationRef.current;
    const page = lastFailedSearchPageRef.current ?? 0;
    setSearchError(null);
    void doSearchPage(
      meta.query,
      page,
      meta.sortBy,
      meta.randomSeed,
      meta.dateFrom,
      meta.dateTo,
      meta.hiddenFolders,
      meta.dirPath,
      meta.indexToken,
      generation,
    );
  }, [doSearchPage]);

  const dismissSearchError = useCallback(() => {
    setSearchError(null);
  }, []);

  const setSearchQuery = useCallback((q: string) => {
    setSearchQueryRaw(q);
    searchQueryRef.current = q;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      resetSearch(q, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath, indexToken);
      void doSearchPage(q, 0, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath, indexToken);
    }, 150);
  }, [dirPath, doSearchPage, indexToken, resetSearch, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders]);

  useEffect(() => {
    if (phase !== 'viewer') return;
    const query = searchQueryRef.current;
    resetSearch(query, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath, indexToken);
    void doSearchPage(query, 0, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath, indexToken);
  }, [view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath, indexToken, phase, doSearchPage, resetSearch]);

  useEffect(() => {
    if (phase !== 'viewer' || !dirPath.trim() || totalIndexed <= 0 || searchTotal <= 0) return;
    const key = `${dirPath}\u0001${totalIndexed}`;
    if (warmedThumbDirRef.current === key) return;
    const timer = window.setTimeout(() => {
      if (warmedThumbDirRef.current === key) return;
      if (document.visibilityState !== 'visible') return;
      warmedThumbDirRef.current = key;
      void fetch('/api/thumbs/warm', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          dir: dirPath,
          indexToken,
          priority: 'background',
          limit: Math.min(totalIndexed, AUTO_THUMB_WARM_LIMIT),
        }),
      }).catch(() => {
        // Best-effort cache fill; visible thumbnail requests can still generate files.
      });
    }, AUTO_THUMB_WARM_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [dirPath, indexToken, phase, searchTotal, totalIndexed]);

  // Re-apply favorites flag when favorites change
  useEffect(() => {
    favoritesRef.current = favorites;
  }, [favorites]);

  // A saved preview tab only becomes live once it is present in the current
  // search index. This avoids resurrecting paths from an earlier scan and does
  // not open the modal or start any enhancement work.
  useEffect(() => {
    const persisted = pendingPreviewTabsRestoreRef.current;
    if (!persisted) return;

    const byId = new Map<string, ImageFile>();
    for (const image of searchResults) {
      if (image) byId.set(image.id, image);
    }
    const restoredIds = persisted.tabIds.filter((id) => byId.has(id));

    if (restoredIds.length > 0) {
      setPreviewById((current) => {
        const next = { ...current };
        for (const id of restoredIds) {
          const image = byId.get(id);
          if (image) next[id] = image;
        }
        return next;
      });
      setPreviewTabIds((current) => {
        const next = [
          ...restoredIds,
          ...current.filter((id) => !restoredIds.includes(id)),
        ];
        return next.length === current.length && next.every((id, index) => id === current[index])
          ? current
          : next;
      });
      setActivePreviewIdState((current) => {
        if (persisted.activeId && byId.has(persisted.activeId)) return persisted.activeId;
        if (current) return current;
        return restoredIds[0] ?? null;
      });
    }

    const currentSearchIsComplete = searchTotal > 0
      && searchResults.length === searchTotal
      && searchResults.every(Boolean);
    if (currentSearchIsComplete) {
      pendingPreviewTabsRestoreRef.current = null;
      setPreviewTabsPersistenceReady(true);
    }
  }, [searchResults, searchTotal]);

  useEffect(() => {
    const byId = new Map<string, ImageFile>();
    for (const image of searchResults) {
      if (image) byId.set(image.id, image);
    }

    setPreviewById((prev) => {
      if (Object.keys(prev).length === 0) return prev;
      const next: Record<string, ImageFile> = {};
      let changed = false;
      for (const id of Object.keys(prev)) {
        const matched = byId.get(id);
        if (matched) {
          next[id] = matched;
          if (matched !== prev[id]) changed = true;
        } else {
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [searchResults]);

  // ── Scan ──
  const startScan = useCallback((options: { full?: boolean; dir?: string; onComplete?: (dir: string) => void } = {}) => {
    const scanDir = formatDirSet(parseDirSet(options.dir ?? dirPath));
    if (!scanDir || phase === 'scanning') return;
    if (scanDir !== dirPath) setDirPath(scanDir);
    setIndexToken(null);
    setScanError(null);
    warmedThumbDirRef.current = '';
    setViewState((prev) => {
      if (prev.hiddenFolders.length === 0 && !prev.dateFrom && !prev.dateTo) return prev;
      const next = { ...prev, hiddenFolders: [] as string[], dateFrom: '', dateTo: '' };
      scheduleViewSettingsPersist(next);
      return next;
    });
    setPhase('scanning');
    setScanProgress({ processed: 0, total: 1, newFiles: 0, stage: 'preparing', message: 'Preparing file list...' });
    const scanRunId = scanRunRef.current + 1;
    scanRunRef.current = scanRunId;

    const params = new URLSearchParams({ dir: scanDir });
    if (options.full) params.set('full', '1');
    const es = new EventSource(`/api/scan?${params.toString()}`);
    let settled = false;
    const isCurrentRun = () => scanRunRef.current === scanRunId;
    const failScan = (message: string) => {
      if (settled || !isCurrentRun()) return;
      settled = true;
      console.error('Scan error', message);
      setScanError(message);
      setScanProgress(null);
      es.close();
      setPhase('landing');
    };
    es.onmessage = (event) => {
      if (settled || !isCurrentRun()) return;
      let data: unknown;
      try {
        data = JSON.parse(event.data);
      } catch {
        failScan('The scan stream returned malformed data.');
        return;
      }
      if (!data || typeof data !== 'object' || Array.isArray(data)) {
        failScan('The scan stream returned an unknown event.');
        return;
      }
      const eventData = data as Record<string, unknown>;
      const type = eventData.type;
      if (type !== 'progress' && type !== 'complete' && type !== 'error') {
        failScan('The scan stream returned an unknown event.');
        return;
      }
      if (type === 'progress') {
        const progress = normalizeScanEventProgress(eventData);
        if (!progress) {
          failScan('The scan stream returned invalid progress data.');
          return;
        }
        setScanProgress({
          ...progress,
        });
      } else if (type === 'complete') {
        const progress = normalizeScanEventProgress(eventData);
        if (!progress) {
          failScan('The scan stream returned invalid completion data.');
          return;
        }
        settled = true;
        setScanProgress({
          ...progress,
        });
        setTotalIndexed(progress.processed);
        setIndexToken(typeof eventData.indexToken === 'string' && eventData.indexToken.trim()
          ? eventData.indexToken
          : null);
        es.close();
        options.onComplete?.(scanDir);
        setPhase('viewer');
      } else if (type === 'error') {
        failScan(typeof eventData.message === 'string' && eventData.message.trim()
          ? eventData.message
          : 'The scan could not be completed.');
      }
    };
    es.onerror = () => {
      failScan('Connection lost before the scan completed.');
    };
  }, [dirPath, phase, scheduleViewSettingsPersist]);

  const dismissScanError = useCallback(() => {
    setScanError(null);
  }, []);

  // ── Delete ──
  const deleteImage = useCallback(async (id: string): Promise<boolean> => {
    try {
      const tokenParam = indexToken ? `&indexToken=${encodeURIComponent(indexToken)}` : '';
      const res = await fetch(`/api/delete?path=${encodeURIComponent(id)}${tokenParam}`, { method: 'DELETE' });
      if (res.ok) {
        searchGenerationRef.current += 1;
        const generation = searchGenerationRef.current;
        abortPendingSearchRequests();
        loadedPagesRef.current = new Set();
        pendingPagesRef.current = new Set();
        setIsSearching(false);
        setSearchResults(prev => {
          const next = removeImageSlot(prev, id);
          searchResultsRef.current = next;
          return next;
        });
        setSearchTotal(prev => {
          const next = Math.max(0, prev - 1);
          searchTotalRef.current = next;
          return next;
        });
        setTotalIndexed(prev => Math.max(0, prev - 1));
        setModalImageIdsState(prev => prev.filter(imageId => imageId !== id));
        setSelectedIds(prev => prev.filter(selectedId => selectedId !== id));
        setPreviewTabIds(prev => {
          const remaining = prev.filter(tabId => tabId !== id);
          setActivePreviewIdState(active => {
            if (active !== id) return active;
            return remaining.length > 0 ? remaining[remaining.length - 1] : null;
          });
          return remaining;
        });
        setPreviewById(prev => {
          const next = { ...prev };
          delete next[id];
          return next;
        });
        const meta = searchMetaRef.current;
        void doSearchPage(
          meta.query,
          0,
          meta.sortBy,
          meta.randomSeed,
          meta.dateFrom,
          meta.dateTo,
          meta.hiddenFolders,
          meta.dirPath,
          meta.indexToken,
          generation
        );
        return true;
      }
      const data = await res.json().catch(() => ({}));
      console.error('Delete failed', data?.error || res.statusText);
      return false;
    } catch (error) {
      console.error('Delete failed', error);
      return false;
    }
  }, [abortPendingSearchRequests, doSearchPage, indexToken]);

  // ── Open in external viewer ──
  const openExternal = useCallback((id: string) => {
    const tokenParam = indexToken ? `&indexToken=${encodeURIComponent(indexToken)}` : '';
    fetch(`/api/open?path=${encodeURIComponent(id)}${tokenParam}`, { method: 'POST' }).catch(() => {});
  }, [indexToken]);

  const setView = useCallback((partial: Partial<ViewSettings>) => {
    setViewState(prev => {
      const next = { ...prev, ...partial };
      scheduleViewSettingsPersist(next);
      return next;
    });
  }, [scheduleViewSettingsPersist]);

  const setPerfEnabled = useCallback((v: boolean) => {
    setPerfEnabledState(v);
  }, []);

  const setSearchScrollPosition = useCallback((key: string, value: number) => {
    const next = { ...scrollMemoryRef.current, [key]: value };
    const keys = Object.keys(next);
    if (keys.length > 80) {
      // lightweight cap: keep latest 80 keys
      for (let i = 0; i < keys.length - 80; i += 1) {
        delete next[keys[i]];
      }
    }
    scrollMemoryRef.current = next;
    if (scrollMemoryFlushRef.current) {
      clearTimeout(scrollMemoryFlushRef.current);
    }
    scrollMemoryFlushRef.current = setTimeout(() => {
      scrollMemoryFlushRef.current = null;
      writeJsonLocalStorage('pvu_scroll_memory', scrollMemoryRef.current);
    }, SCROLL_MEMORY_FLUSH_DELAY_MS);
  }, []);

  const getSearchScrollPosition = useCallback((key: string) => {
    const value = scrollMemoryRef.current[key];
    return typeof value === 'number' ? value : null;
  }, []);

  const setActivePreviewId = useCallback((id: string | null) => {
    previewTabsUserModifiedRef.current = true;
    setActivePreviewIdState(id);
  }, []);

  const markImageSeen = useCallback((id: string) => {
    if (seenImageIdsRef.current[id]) return;
    seenImageIdsRef.current = { ...seenImageIdsRef.current, [id]: true };
    if (seenImagesFlushRef.current) {
      clearTimeout(seenImagesFlushRef.current);
    }
    seenImagesFlushRef.current = setTimeout(() => {
      seenImagesFlushRef.current = null;
      const snapshot = seenImageIdsRef.current;
      writeJsonLocalStorage('pvu_seen_images', snapshot);
      syncSeenToShared(snapshot);
    }, SEEN_IMAGES_FLUSH_DELAY_MS);
    setSeenImageIds((prev) => {
      if (prev[id]) return prev;
      return { ...prev, [id]: true };
    });
  }, [syncSeenToShared]);

  const requestRevealImage = useCallback((id: string) => {
    setRevealImageId(id);
  }, []);

  const consumeRevealImage = useCallback(() => {
    setRevealImageId(null);
  }, []);

  const setModalImageIds = useCallback((ids: string[]) => {
    setModalImageIdsState(Array.from(new Set(ids.filter(Boolean))));
  }, []);

  const openModalAtImage = useCallback((
    id: string,
    fallbackIndex: number | null,
    orderedIds?: string[]
  ) => {
    markImageSeen(id);
    setModalImageIdsState(
      orderedIds && orderedIds.length > 0
        ? Array.from(new Set(orderedIds.filter(Boolean)))
        : []
    );
    const resolvedIndex = fallbackIndex !== null && fallbackIndex >= 0
      ? fallbackIndex
      : searchResults.findIndex((image) => image?.id === id);
    setSelectedIndex(resolvedIndex >= 0 ? resolvedIndex : null);
  }, [markImageSeen, searchResults]);

  const showPreviewImage = useCallback((
    image: ImageFile,
    options?: { makeActive?: boolean }
  ) => {
    const { makeActive = true } = options || {};
    setPreviewById((prev) => ({ ...prev, [image.id]: image }));
    if (makeActive) {
      setActivePreviewIdState(image.id);
    }
  }, []);

  const openPreviewTab = useCallback((
    image: ImageFile,
    options?: { makeActive?: boolean; pin?: boolean }
  ) => {
    const { makeActive = true, pin = false } = options || {};
    previewTabsUserModifiedRef.current = true;
    setPreviewById((prev) => ({ ...prev, [image.id]: image }));
    setPreviewTabIds((prev) => (prev.includes(image.id) ? prev : [...prev, image.id]));
    if (makeActive) {
      setActivePreviewIdState(image.id);
    }
    if (pin) {
      setPinnedPreviewIds((prev) => (prev.includes(image.id) ? prev : [...prev, image.id]));
    }
  }, []);

  const togglePinPreviewTab = useCallback((id: string) => {
    setPinnedPreviewIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
    );
  }, []);

  const reorderPreviewTab = useCallback((id: string, destinationIndex: number) => {
    setPreviewTabIds((previous) => {
      const next = reorderPreviewTabIds(previous, id, destinationIndex);
      if (next === previous) return previous;
      previewTabsUserModifiedRef.current = true;
      return next;
    });
  }, []);

  const closePreviewTab = useCallback((id: string) => {
    previewTabsUserModifiedRef.current = true;
    const existing = previewById[id];
    if (existing) {
      setClosedPreviewById((prev) => ({ ...prev, [id]: existing }));
      setClosedPreviewStack((prev) => [id, ...prev.filter((item) => item !== id)].slice(0, MAX_CLOSED_PREVIEW_TABS));
    }
    setPreviewTabIds((prev) => {
      const remaining = prev.filter((tabId) => tabId !== id);
      setActivePreviewIdState((active) => {
        if (active !== id) return active;
        return remaining.length > 0 ? remaining[remaining.length - 1] : null;
      });
      return remaining;
    });
    setSelectedIds((prev) => prev.filter((selectedId) => selectedId !== id));
    setPreviewById((prev) => {
      const next = { ...prev };
      delete next[id];
      return next;
    });
  }, [previewById]);

  const restoreLastClosedPreview = useCallback(() => {
    setClosedPreviewStack((prev) => {
      if (prev.length === 0) return prev;
      const [id, ...rest] = prev;
      const fromSearch = searchResults.find((entry): entry is ImageFile => Boolean(entry?.id === id));
      const fromCache = closedPreviewById[id];
      const image = fromSearch ?? fromCache;
      if (image) {
        previewTabsUserModifiedRef.current = true;
        setPreviewById((current) => ({ ...current, [id]: image }));
        setPreviewTabIds((current) => (current.includes(id) ? current : [id, ...current]));
        setActivePreviewIdState(id);
      }
      return rest;
    });
  }, [closedPreviewById, searchResults]);

  const clearSelection = useCallback(() => {
    setSelectedIds([]);
    setSelectionAnchorId(null);
  }, []);

  const closeAllPreviews = useCallback(() => {
    previewTabsUserModifiedRef.current = true;
    setActivePreviewIdState(null);
    setSelectedIds([]);
    setSelectionAnchorId(null);
    setPreviewById((prev) => {
      if (previewTabIds.length === 0) return {};
      const next: Record<string, ImageFile> = {};
      for (const id of previewTabIds) {
        if (prev[id]) next[id] = prev[id];
      }
      return next;
    });
  }, [previewTabIds]);

  const selectImage = useCallback((
    image: ImageFile,
    orderedIds: string[],
    options?: { range?: boolean; toggle?: boolean }
  ) => {
    const { range = false, toggle = false } = options || {};
    const targetId = image.id;

    showPreviewImage(image, { makeActive: true });

    if (range) {
      const anchor = selectionAnchorId ?? targetId;
      const anchorIndex = orderedIds.indexOf(anchor);
      const targetIndex = orderedIds.indexOf(targetId);
      if (anchorIndex >= 0 && targetIndex >= 0) {
        const [start, end] = anchorIndex < targetIndex
          ? [anchorIndex, targetIndex]
          : [targetIndex, anchorIndex];
        const rangeIds = orderedIds.slice(start, end + 1);
        if (toggle) {
          setSelectedIds((prev) => Array.from(new Set([...prev, ...rangeIds])));
        } else {
          setSelectedIds(rangeIds);
        }
      } else {
        setSelectedIds([targetId]);
      }
      setSelectionAnchorId(anchor);
      return;
    }

    if (toggle) {
      setSelectedIds((prev) =>
        prev.includes(targetId)
          ? prev.filter((id) => id !== targetId)
          : [...prev, targetId]
      );
      setSelectionAnchorId(targetId);
      return;
    }

    setSelectedIds([targetId]);
    setSelectionAnchorId(targetId);
  }, [selectionAnchorId, showPreviewImage]);

  return (
    <ImageContext.Provider value={{
      phase, setPhase, dirPath, setDirPath, indexToken, scanProgress, scanError, dismissScanError,
      searchQuery, setSearchQuery, searchResults, searchTotal,
      isSearching, searchError, ensureSearchRange, retrySearch, dismissSearchError,
      favorites, toggleFavorite, showFavOnly: showFavOnlyState, setShowFavOnly,
      showUnfavOnly: showUnfavOnlyState, setShowUnfavOnly,
      cycleFavoriteLevel, decreaseFavoriteLevel, clearFavorite,
      favoriteFilterLevels, toggleFavoriteFilterLevel, clearFavoriteFilterLevels,
      showEnhancedOnly: showEnhancedOnlyState, setShowEnhancedOnly, enhancedSourceIds,
      selectedIndex, setSelectedIndex,
      modalImageIds, setModalImageIds, openModalAtImage,
      selectedIds, selectImage, clearSelection,
      previewTabIds, activePreviewId, previewById, showPreviewImage, openPreviewTab, setActivePreviewId, closePreviewTab, reorderPreviewTab, closeAllPreviews,
      seenImageIds, markImageSeen, revealImageId, requestRevealImage, consumeRevealImage,
      pinnedPreviewIds, togglePinPreviewTab, closedPreviewTabCount: closedPreviewStack.length, restoreLastClosedPreview,
      keyBindings, setKeyBindings, confirmBeforeDelete, setConfirmBeforeDelete, showSettings, setShowSettings,
      view, setView,
      setSearchScrollPosition, getSearchScrollPosition,
      perfEnabled, setPerfEnabled, perfStats,
      startScan, deleteImage, openExternal,
      totalIndexed, setTotalIndexed,
    }}>
      {children}
    </ImageContext.Provider>
  );
}

export function useImageStore() {
  const ctx = useContext(ImageContext);
  if (!ctx) throw new Error('useImageStore must be inside ImageProvider');
  return ctx;
}
