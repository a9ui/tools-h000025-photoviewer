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
};

const SCROLL_MEMORY_FLUSH_DELAY_MS = 500;
const SEEN_IMAGES_FLUSH_DELAY_MS = 900;
const FAVORITES_FLUSH_DELAY_MS = 300;
const VIEW_SETTINGS_FLUSH_DELAY_MS = 300;
const AUTO_THUMB_WARM_DELAY_MS = 4200;
const AUTO_THUMB_WARM_LIMIT = 1200;
const MAX_FAVORITE_LEVEL = 5;

function normalizeStoredView(value: unknown): ViewSettings {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return { ...DEFAULT_VIEW };
  }

  return {
    ...DEFAULT_VIEW,
    ...(value as Partial<ViewSettings>),
    // `columns` belonged to an older UI. The current UI is size-driven and
    // has no way to change or clear a persisted fixed-column value.
    columns: 0,
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

interface Ctx {
  // App phase
  phase: 'landing' | 'scanning' | 'viewer';
  setPhase: (p: 'landing' | 'scanning' | 'viewer') => void;
  dirPath: string;
  setDirPath: (d: string) => void;

  // Scan progress
  scanProgress: { processed: number; total: number; newFiles: number; stage?: 'preparing' | 'scanning' | 'complete'; message?: string } | null;

  // Search
  searchQuery: string;
  setSearchQuery: (q: string) => void;
  searchResults: Array<ImageFile | null>;
  searchTotal: number;
  isSearching: boolean;
  ensureSearchRange: (startIndex: number, endIndex: number) => void;

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
  togglePinPreviewTab: (id: string) => void;
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
  const [scanProgress, setScanProgress] = useState<{ processed: number; total: number; newFiles: number; stage?: 'preparing' | 'scanning' | 'complete'; message?: string } | null>(null);

  const [searchQuery, setSearchQueryRaw] = useState('');
  const [searchResults, setSearchResults] = useState<Array<ImageFile | null>>([]);
  const [searchTotal, setSearchTotal] = useState(0);
  const [isSearching, setIsSearching] = useState(false);

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
  const [, setClosedPreviewStack] = useState<string[]>([]);
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
  }>({
    query: '',
    sortBy: DEFAULT_VIEW.sortBy,
    randomSeed: DEFAULT_VIEW.randomSeed,
    dateFrom: '',
    dateTo: '',
    hiddenFolders: [],
    hiddenFoldersKey: '',
    dirPath: '',
  });
  const searchGenerationRef = useRef(0);
  const searchResultsRef = useRef<Array<ImageFile | null>>([]);
  const searchTotalRef = useRef(0);
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

  // ── Load favorites + view settings from localStorage ──
  useEffect(() => {
    migrateLegacyPhotoviewerState();
    let localFavorites: Record<string, number> = {};
    try {
      const stored = localStorage.getItem('pvu_favorites');
      if (stored) {
        localFavorites = normalizeFavorites(JSON.parse(stored));
        if (Object.keys(localFavorites).length === 0) {
          const backup = localStorage.getItem('pvu_favorites_backup');
          if (backup) localFavorites = normalizeFavorites(JSON.parse(backup));
        }
        setFavorites(localFavorites);
        favoritesRef.current = localFavorites;
      }
    } catch { /* ignore */ }
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
        const parsed = JSON.parse(seen);
        if (parsed && typeof parsed === 'object') {
          const normalized: Record<string, true> = {};
          for (const [id, value] of Object.entries(parsed)) {
            if (typeof id === 'string' && value) normalized[id] = true;
          }
          seenImageIdsRef.current = normalized;
          setSeenImageIds(normalized);
        }
      }
    } catch { /* ignore */ }
    setUiPreferencesHydrated(true);
  }, []);

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
        localStorage.setItem('pvu_favorites', serialized);
        if (Object.keys(snapshot).length > 0) {
          localStorage.setItem('pvu_favorites_backup', serialized);
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
        localStorage.setItem('pvu_favorites', serialized);
        if (Object.keys(snapshot).length > 0) {
          localStorage.setItem('pvu_favorites_backup', serialized);
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
  }, [flushViewSettings]);

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
    if (!favoritesHydratedRef.current) favoriteHydrationDirtyIdsRef.current.add(id);
    setFavorites(prev => {
      const next = { ...prev };
      if ((next[id] ?? 0) > 0) delete next[id];
      else next[id] = 1;
      return next;
    });
  }, []);

  const cycleFavoriteLevel = useCallback((id: string) => {
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
    if (!favoritesHydratedRef.current) favoriteHydrationDirtyIdsRef.current.add(id);
    setFavorites((prev) => {
      if (!(id in prev)) return prev;
      const next = { ...prev };
      delete next[id];
      return next;
    });
  }, []);

  const decreaseFavoriteLevel = useCallback((id: string) => {
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
    generation = searchGenerationRef.current
  ) => {
    const pageKey = getSearchPageKey(generation, page);
    if (pendingPagesRef.current.has(pageKey)) return;
    if (loadedPagesRef.current.has(pageKey) && isSearchPageComplete(page)) return;
    loadedPagesRef.current.delete(pageKey);

    pendingPagesRef.current.add(pageKey);
    setIsSearching(true);
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
      const url = `/api/search?q=${encodeURIComponent(query)}&page=${page}&size=${PAGE_SIZE}&sortBy=${sortBy}${randomSeedParam}${fromParam}${toParam}${hiddenParam}${dirParam}`;
      const res = await fetch(url, { signal: abortController.signal });
      const data: SearchResponse = await res.json();

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
        meta.dirPath !== currentDirPath
      ) {
        return;
      }

      const withFavs = data.results.map((img) => ({
        ...img,
        isFavorite: !!favoritesRef.current[img.id],
      }));

      searchTotalRef.current = data.total;
      setSearchTotal((prevTotal) => (prevTotal === data.total ? prevTotal : data.total));
      setSearchResults((prev) => {
        const needsResize = prev.length !== data.total;
        const next = needsResize ? Array<ImageFile | null>(data.total).fill(null) : [...prev];
        if (needsResize) {
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
    currentDirPath: string
  ) => {
    searchMetaRef.current = {
      query,
      sortBy,
      randomSeed,
      dateFrom,
      dateTo,
      hiddenFolders,
      hiddenFoldersKey: buildHiddenFoldersKey(hiddenFolders),
      dirPath: currentDirPath,
    };
    searchGenerationRef.current += 1;
    abortPendingSearchRequests();
    loadedPagesRef.current = new Set();
    pendingPagesRef.current = new Set();
    searchTotalRef.current = 0;
    searchResultsRef.current = [];
    setIsSearching(false);
    setSearchTotal(0);
    setSearchResults([]);
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
        generation
      );
    }
  }, [doSearchPage, getSearchPageKey, isSearchPageComplete, searchResults.length, searchTotal]);

  const setSearchQuery = useCallback((q: string) => {
    setSearchQueryRaw(q);
    searchQueryRef.current = q;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      resetSearch(q, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath);
      void doSearchPage(q, 0, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath);
    }, 150);
  }, [dirPath, doSearchPage, resetSearch, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders]);

  useEffect(() => {
    if (phase !== 'viewer') return;
    const query = searchQueryRef.current;
    resetSearch(query, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath);
    void doSearchPage(query, 0, view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath);
  }, [view.sortBy, view.randomSeed, view.dateFrom, view.dateTo, view.hiddenFolders, dirPath, phase, doSearchPage, resetSearch]);

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
          priority: 'background',
          limit: Math.min(totalIndexed, AUTO_THUMB_WARM_LIMIT),
        }),
      }).catch(() => {
        // Best-effort cache fill; visible thumbnail requests can still generate files.
      });
    }, AUTO_THUMB_WARM_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [dirPath, phase, searchTotal, totalIndexed]);

  // Re-apply favorites flag when favorites change
  useEffect(() => {
    favoritesRef.current = favorites;
  }, [favorites]);

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
    if (!scanDir) return;
    if (scanDir !== dirPath) setDirPath(scanDir);
    warmedThumbDirRef.current = '';
    setViewState((prev) => {
      if (prev.hiddenFolders.length === 0 && !prev.dateFrom && !prev.dateTo) return prev;
      const next = { ...prev, hiddenFolders: [] as string[], dateFrom: '', dateTo: '' };
      scheduleViewSettingsPersist(next);
      return next;
    });
    setPhase('scanning');
    setScanProgress({ processed: 0, total: 1, newFiles: 0, stage: 'preparing', message: 'Preparing file list...' });

    const params = new URLSearchParams({ dir: scanDir });
    if (options.full) params.set('full', '1');
    const es = new EventSource(`/api/scan?${params.toString()}`);
    es.onmessage = (event) => {
      const data = JSON.parse(event.data);
      if (data.type === 'progress') {
        setScanProgress({
          processed: data.processed,
          total: data.total,
          newFiles: data.newFiles,
          stage: data.stage,
          message: data.message,
        });
      } else if (data.type === 'complete') {
        setScanProgress({
          processed: data.processed,
          total: data.total,
          newFiles: data.newFiles,
          stage: data.stage,
          message: data.message,
        });
        setTotalIndexed(data.processed);
        es.close();
        options.onComplete?.(scanDir);
        setPhase('viewer');
      } else if (data.type === 'error') {
        console.error('Scan error', data.message);
        alert('Scan error: ' + data.message);
        es.close();
        setPhase('landing');
      }
    };
    es.onerror = () => {
      console.error('Scan event stream failed');
      es.close();
      alert('Scan error: connection lost before the scan completed.');
      setPhase('landing');
    };
  }, [dirPath, scheduleViewSettingsPersist]);

  // ── Delete ──
  const deleteImage = useCallback(async (id: string): Promise<boolean> => {
    try {
      const res = await fetch(`/api/delete?path=${encodeURIComponent(id)}`, { method: 'DELETE' });
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
  }, [abortPendingSearchRequests, doSearchPage]);

  // ── Open in external viewer ──
  const openExternal = useCallback((id: string) => {
    fetch(`/api/open?path=${encodeURIComponent(id)}`, { method: 'POST' }).catch(() => {});
  }, []);

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
      writeJsonLocalStorage('pvu_seen_images', seenImageIdsRef.current);
    }, SEEN_IMAGES_FLUSH_DELAY_MS);
    setSeenImageIds((prev) => {
      if (prev[id]) return prev;
      return { ...prev, [id]: true };
    });
  }, []);

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

  const closePreviewTab = useCallback((id: string) => {
    const existing = previewById[id];
    if (existing) {
      setClosedPreviewById((prev) => ({ ...prev, [id]: existing }));
      setClosedPreviewStack((prev) => [id, ...prev.filter((item) => item !== id)].slice(0, 30));
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
      phase, setPhase, dirPath, setDirPath, scanProgress,
      searchQuery, setSearchQuery, searchResults, searchTotal,
      isSearching, ensureSearchRange,
      favorites, toggleFavorite, showFavOnly: showFavOnlyState, setShowFavOnly,
      showUnfavOnly: showUnfavOnlyState, setShowUnfavOnly,
      cycleFavoriteLevel, decreaseFavoriteLevel, clearFavorite,
      favoriteFilterLevels, toggleFavoriteFilterLevel, clearFavoriteFilterLevels,
      showEnhancedOnly: showEnhancedOnlyState, setShowEnhancedOnly, enhancedSourceIds,
      selectedIndex, setSelectedIndex,
      modalImageIds, setModalImageIds, openModalAtImage,
      selectedIds, selectImage, clearSelection,
      previewTabIds, activePreviewId, previewById, showPreviewImage, openPreviewTab, setActivePreviewId, closePreviewTab, closeAllPreviews,
      seenImageIds, markImageSeen, revealImageId, requestRevealImage, consumeRevealImage,
      pinnedPreviewIds, togglePinPreviewTab, restoreLastClosedPreview,
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
