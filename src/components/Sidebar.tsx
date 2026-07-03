'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { useImageStore } from '../store/ImageContext';
import { getResultCountLabel, sortFolderBuckets, type FolderBucket } from '../lib/viewerUi';
import { appendDirSet, summarizeDirSet } from '../lib/pathSet';

const FAVORITE_FILTER_LEVELS = [1, 2, 3, 4, 5] as const;

function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function createRandomSeed() {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

export default function Sidebar() {
  const {
    view,
    setView,
    setSearchQuery,
    dirPath,
    setDirPath,
    startScan,
    totalIndexed,
    searchTotal,
    searchQuery,
    showFavOnly,
    setShowFavOnly,
    showUnfavOnly,
    setShowUnfavOnly,
    favoriteFilterLevel,
    setFavoriteFilterLevel,
    showEnhancedOnly,
    setShowEnhancedOnly,
    setShowSettings,
    setPhase,
    perfEnabled,
    setPerfEnabled,
    perfStats,
  } = useImageStore();

  const [folderBuckets, setFolderBuckets] = useState<FolderBucket[]>([]);
  const [loadingFolders, setLoadingFolders] = useState(false);
  const [selectedFolderKeys, setSelectedFolderKeys] = useState<string[]>([]);
  const [lastSelectedFolderKey, setLastSelectedFolderKey] = useState<string | null>(null);
  const [addingFolder, setAddingFolder] = useState(false);
  const [folderActionError, setFolderActionError] = useState('');

  const hiddenFolderSet = useMemo(() => new Set(view.hiddenFolders), [view.hiddenFolders]);
  const selectedFolderSet = useMemo(() => new Set(selectedFolderKeys), [selectedFolderKeys]);
  const sortedFolderBuckets = useMemo(
    () => sortFolderBuckets(folderBuckets, view.folderSortBy),
    [folderBuckets, view.folderSortBy]
  );
  const resultCountLabel = useMemo(() => getResultCountLabel({
    searchQuery,
    searchTotal,
    totalIndexed,
    dateFrom: view.dateFrom,
    dateTo: view.dateTo,
    hiddenFolders: view.hiddenFolders,
  }), [searchQuery, searchTotal, totalIndexed, view.dateFrom, view.dateTo, view.hiddenFolders]);

  useEffect(() => {
    if (!dirPath) {
      setFolderBuckets([]);
      return;
    }

    let cancelled = false;
    setLoadingFolders(true);

    fetch(`/api/folders?dir=${encodeURIComponent(dirPath)}`)
      .then((response) => response.json())
      .then((data) => {
        if (cancelled) return;
        if (Array.isArray(data.folders)) {
          setFolderBuckets(
            data.folders.filter(
              (folder: unknown): folder is FolderBucket =>
                !!folder &&
                typeof folder === 'object' &&
                typeof (folder as FolderBucket).key === 'string' &&
                typeof (folder as FolderBucket).label === 'string' &&
                typeof (folder as FolderBucket).count === 'number'
            )
          );
          return;
        }
        setFolderBuckets([]);
      })
      .catch(() => {
        if (!cancelled) setFolderBuckets([]);
      })
      .finally(() => {
        if (!cancelled) setLoadingFolders(false);
      });

    return () => {
      cancelled = true;
    };
  }, [dirPath, totalIndexed]);

  useEffect(() => {
    const available = new Set(folderBuckets.map((folder) => folder.key));
    setSelectedFolderKeys((prev) => prev.filter((key) => available.has(key)));
    setLastSelectedFolderKey((prev) => (prev && available.has(prev) ? prev : null));
  }, [folderBuckets]);

  if (!view.sidebarOpen) return null;

  const applyDatePreset = (preset: 'today' | '7d' | '30d' | 'year' | 'clear') => {
    const today = new Date();
    const todayYmd = toDateInputValue(today);

    if (preset === 'clear') {
      setView({ dateFrom: '', dateTo: '' });
      return;
    }

    if (preset === 'today') {
      setView({ dateFrom: todayYmd, dateTo: todayYmd });
      return;
    }

    if (preset === 'year') {
      const from = `${today.getFullYear()}-01-01`;
      setView({ dateFrom: from, dateTo: todayYmd });
      return;
    }

    const days = preset === '7d' ? 7 : 30;
    const fromDate = new Date(today);
    fromDate.setDate(today.getDate() - (days - 1));
    setView({ dateFrom: toDateInputValue(fromDate), dateTo: todayYmd });
  };

  const toggleFolderVisibility = (folderKey: string) => {
    const isHidden = view.hiddenFolders.includes(folderKey);
    const next = isHidden
      ? view.hiddenFolders.filter((item) => item !== folderKey)
      : [...view.hiddenFolders, folderKey];
    setView({ hiddenFolders: next });
  };

  const selectFolder = (folderKey: string, event: React.MouseEvent) => {
    const additive = event.ctrlKey || event.metaKey;
    const range = event.shiftKey && lastSelectedFolderKey;
    const sortedKeys = sortedFolderBuckets.map((folder) => folder.key);

    if (range) {
      const from = sortedKeys.indexOf(lastSelectedFolderKey);
      const to = sortedKeys.indexOf(folderKey);
      if (from >= 0 && to >= 0) {
        const [start, end] = from < to ? [from, to] : [to, from];
        const rangeKeys = sortedKeys.slice(start, end + 1);
        setSelectedFolderKeys((prev) => additive
          ? Array.from(new Set([...prev, ...rangeKeys]))
          : rangeKeys);
        setLastSelectedFolderKey(folderKey);
        return;
      }
    }

    setSelectedFolderKeys((prev) => {
      if (additive) {
        return prev.includes(folderKey)
          ? prev.filter((key) => key !== folderKey)
          : [...prev, folderKey];
      }
      return [folderKey];
    });
    setLastSelectedFolderKey(folderKey);
  };

  const showSelectedFolders = () => {
    const selected = new Set(selectedFolderKeys);
    if (selected.size === 0) return;
    setView({ hiddenFolders: view.hiddenFolders.filter((key) => !selected.has(key)) });
  };

  const hideSelectedFolders = () => {
    if (selectedFolderKeys.length === 0) return;
    setView({ hiddenFolders: Array.from(new Set([...view.hiddenFolders, ...selectedFolderKeys])) });
  };

  const clearSelectedFolders = () => {
    setSelectedFolderKeys([]);
    setLastSelectedFolderKey(null);
  };

  const showAllFolders = () => {
    setView({ hiddenFolders: [] });
  };

  const hideAllFolders = () => {
    setView({ hiddenFolders: sortedFolderBuckets.map((folder) => folder.key) });
  };

  const invertFolderVisibility = () => {
    const hidden = new Set(view.hiddenFolders);
    setView({
      hiddenFolders: sortedFolderBuckets
        .filter((folder) => !hidden.has(folder.key))
        .map((folder) => folder.key),
    });
  };

  const addFolderFromSidebar = async () => {
    if (addingFolder) return;
    setAddingFolder(true);
    setFolderActionError('');
    try {
      const res = await fetch('/api/browse?multi=1', { method: 'POST' });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data.error || 'Failed to open folder dialog.');
      const folders = Array.isArray(data.paths)
        ? data.paths.filter((item: unknown): item is string => typeof item === 'string' && item.trim().length > 0)
        : typeof data.path === 'string' && data.path.trim()
          ? [data.path]
          : [];
      if (folders.length === 0) return;
      const nextDirSet = appendDirSet(dirPath, folders);
      setDirPath(nextDirSet);
      startScan({ dir: nextDirSet });
    } catch (error) {
      setFolderActionError(error instanceof Error ? error.message : String(error));
    } finally {
      setAddingFolder(false);
    }
  };

  const applyDisplayStyle = (displayStyle: typeof view.displayStyle) => {
    if (displayStyle === 'compact') {
      setView({ displayStyle, viewMode: 'grid', aspectMode: 'square', thumbSize: 140 });
      return;
    }
    if (displayStyle === 'poster') {
      setView({ displayStyle, viewMode: 'grid', aspectMode: 'portrait', thumbSize: 240 });
      return;
    }
    setView({ displayStyle });
  };

  return (
    <aside className="sidebar">
      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
          </svg>
          <span>Folder</span>
        </div>

        <p className="sidebar-path" title={dirPath}>{summarizeDirSet(dirPath) || dirPath}</p>
        <p className="sidebar-meta">{resultCountLabel}</p>
        <button className="sidebar-link" onClick={() => void addFolderFromSidebar()} disabled={addingFolder}>
          {addingFolder ? 'Adding folder...' : 'Add folder'}
        </button>
        <button className="sidebar-link" onClick={() => setPhase('landing')}>Change folder</button>
        {folderActionError && <p className="sidebar-error">{folderActionError}</p>}
      </div>

      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <span>Quick Search</span>
        </div>
        <div className="sidebar-pills">
          {[
            { label: 'Portrait', q: '1girl, portrait' },
            { label: 'Landscape', q: 'landscape' },
            { label: 'Anime', q: 'anime style' },
            { label: 'Photoreal', q: 'photorealistic' },
          ].map((preset) => (
            <button key={preset.label} className="pill" onClick={() => setSearchQuery(preset.q)}>
              {preset.label}
            </button>
          ))}
          <button className="pill" onClick={() => setSearchQuery('')}>Clear</button>
        </div>
      </div>

      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
          </svg>
          <span>Filter</span>
        </div>

        <label className="sidebar-toggle">
          <input type="checkbox" checked={showFavOnly} onChange={(e) => setShowFavOnly(e.target.checked)} />
          <span>Favorites only</span>
        </label>

        <label className="sidebar-toggle" style={{ marginTop: '0.35rem' }}>
          <input type="checkbox" checked={showUnfavOnly} onChange={(e) => setShowUnfavOnly(e.target.checked)} />
          <span>Unrated only</span>
        </label>

        <label className="sidebar-toggle" style={{ marginTop: '0.35rem' }}>
          <input type="checkbox" checked={showEnhancedOnly} onChange={(e) => setShowEnhancedOnly(e.target.checked)} />
          <span>Enhanced only</span>
        </label>

        {showFavOnly && (
          <div className="sidebar-pills" style={{ marginTop: '0.55rem' }}>
            {FAVORITE_FILTER_LEVELS.map((level) => (
              <button
                key={level}
                className={`pill ${favoriteFilterLevel === level ? 'active' : ''}`}
                onClick={() => setFavoriteFilterLevel(level)}
              >
                Lv {level}+
              </button>
            ))}
          </div>
        )}

        <div className="sidebar-date-range">
          <div className="sidebar-pills">
            <button className="pill" onClick={() => applyDatePreset('today')}>Today</button>
            <button className="pill" onClick={() => applyDatePreset('7d')}>7d</button>
            <button className="pill" onClick={() => applyDatePreset('30d')}>30d</button>
            <button className="pill" onClick={() => applyDatePreset('year')}>This year</button>
            <button className="pill" onClick={() => applyDatePreset('clear')}>Clear</button>
          </div>

          <label className="sidebar-date-label">
            <span>Date from</span>
            <input
              type="date"
              className="sidebar-date-input"
              value={view.dateFrom}
              onChange={(e) => setView({ dateFrom: e.target.value })}
            />
          </label>

          <label className="sidebar-date-label">
            <span>Date to</span>
            <input
              type="date"
              className="sidebar-date-input"
              value={view.dateTo}
              onChange={(e) => setView({ dateTo: e.target.value })}
            />
          </label>

          {(view.dateFrom || view.dateTo) && (
            <button className="sidebar-link" onClick={() => setView({ dateFrom: '', dateTo: '' })}>
              Clear date filter
            </button>
          )}
        </div>
      </div>

      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M3 4h18M3 12h18M3 20h18" />
          </svg>
          <span>Folders</span>
        </div>

        {loadingFolders && <p className="sidebar-meta">Loading folder list...</p>}

        {!loadingFolders && folderBuckets.length === 0 && (
          <p className="sidebar-meta">No folders found under this root.</p>
        )}

        {folderBuckets.length > 0 && (
          <>
            <div className="sidebar-pills" style={{ marginBottom: '0.5rem' }}>
              <button
                className={`pill ${view.folderSortBy === 'name-asc' ? 'active' : ''}`}
                onClick={() => setView({ folderSortBy: 'name-asc' })}
              >
                A-Z
              </button>
              <button
                className={`pill ${view.folderSortBy === 'name-desc' ? 'active' : ''}`}
                onClick={() => setView({ folderSortBy: 'name-desc' })}
              >
                Z-A
              </button>
              <button
                className={`pill ${view.folderSortBy === 'count-desc' ? 'active' : ''}`}
                onClick={() => setView({ folderSortBy: 'count-desc' })}
              >
                Count
              </button>
            </div>

            <div className="sidebar-pills" style={{ marginBottom: '0.5rem' }}>
              <button className="pill" onClick={showAllFolders}>Show all</button>
              <button className="pill" onClick={hideAllFolders}>Hide all</button>
              <button className="pill" onClick={invertFolderVisibility}>Invert</button>
            </div>

            <div className="sidebar-pills" style={{ marginBottom: '0.5rem' }}>
              <button className="pill" onClick={showSelectedFolders} disabled={selectedFolderKeys.length === 0}>
                Show selected
              </button>
              <button className="pill" onClick={hideSelectedFolders} disabled={selectedFolderKeys.length === 0}>
                Hide selected
              </button>
              <button className="pill" onClick={clearSelectedFolders} disabled={selectedFolderKeys.length === 0}>
                Clear selection
              </button>
            </div>

            <div className="sidebar-folder-list">
              {sortedFolderBuckets.map((folder) => {
                const isVisible = !hiddenFolderSet.has(folder.key);
                const isSelected = selectedFolderSet.has(folder.key);
                return (
                  <div
                    key={folder.key}
                    className={`sidebar-folder-item ${isVisible ? 'is-active' : ''} ${isSelected ? 'is-selected' : ''}`}
                    title={`${folder.label} (${folder.count})`}
                    onClick={(event) => selectFolder(folder.key, event)}
                  >
                    <input
                      type="checkbox"
                      className="sidebar-folder-select"
                      checked={isSelected}
                      onChange={() => {}}
                      onClick={(event) => {
                        event.stopPropagation();
                        selectFolder(folder.key, event);
                      }}
                      aria-label={`Select ${folder.label}`}
                    />
                    <span className="sidebar-folder-label">{folder.label}</span>
                    <span className="sidebar-folder-count">{folder.count.toLocaleString()}</span>
                    <button
                      type="button"
                      className={`sidebar-folder-visibility ${isVisible ? 'is-visible' : ''}`}
                      onClick={(event) => {
                        event.stopPropagation();
                        toggleFolderVisibility(folder.key);
                      }}
                      title={isVisible ? 'Hide folder' : 'Show folder'}
                    >
                      {isVisible ? 'Shown' : 'Hidden'}
                    </button>
                  </div>
                );
              })}
            </div>
          </>
        )}

        {view.hiddenFolders.length > 0 && (
          <div className="sidebar-folder-actions">
            <button className="sidebar-link" style={{ marginTop: '0.55rem' }} onClick={showAllFolders}>
              Show all folders
            </button>
          </div>
        )}
      </div>

      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <line x1="12" y1="5" x2="12" y2="19" />
            <polyline points="19 12 12 19 5 12" />
          </svg>
          <span>Sort</span>
        </div>

        <div className="sidebar-pills">
          {(['newest', 'oldest', 'created-newest', 'created-oldest', 'name', 'random'] as const).map((s) => (
            <button key={s} className={`pill ${view.sortBy === s ? 'active' : ''}`} onClick={() => setView({ sortBy: s })}>
              {s === 'newest'
                ? 'Modified new'
                : s === 'oldest'
                  ? 'Modified old'
                  : s === 'created-newest'
                    ? 'Created new'
                    : s === 'created-oldest'
                      ? 'Created old'
                      : s === 'random'
                        ? 'Random'
                        : 'Name'}
            </button>
          ))}
        </div>
        {view.sortBy === 'random' && (
          <button className="sidebar-link" style={{ marginTop: '0.55rem' }} onClick={() => setView({ randomSeed: createRandomSeed() })}>
            Reshuffle
          </button>
        )}
      </div>

      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <rect x="3" y="3" width="7" height="7" />
            <rect x="14" y="3" width="7" height="7" />
            <rect x="14" y="14" width="7" height="7" />
            <rect x="3" y="14" width="7" height="7" />
          </svg>
          <span>Display</span>
        </div>

        <div className="sidebar-row">
          <span className="sidebar-row-label">Mode</span>
          <div className="sidebar-pills">
            <button className={`pill ${view.viewMode === 'grid' ? 'active' : ''}`} onClick={() => setView({ viewMode: 'grid' })}>Grid</button>
            <button className={`pill ${view.viewMode === 'list' ? 'active' : ''}`} onClick={() => setView({ viewMode: 'list' })}>List</button>
          </div>
        </div>

        <div className="sidebar-row">
          <span className="sidebar-row-label">Style</span>
          <div className="sidebar-pills">
            {(['standard', 'compact', 'poster'] as const).map((style) => (
              <button
                key={style}
                className={`pill ${view.displayStyle === style ? 'active' : ''}`}
                onClick={() => applyDisplayStyle(style)}
              >
                {style === 'standard' ? 'Standard' : style === 'compact' ? 'Compact' : 'Poster'}
              </button>
            ))}
          </div>
        </div>

        <div className="sidebar-row">
          <span className="sidebar-row-label">Aspect</span>
          <div className="sidebar-pills">
            {(['original', 'square', 'portrait'] as const).map((a) => (
              <button key={a} className={`pill ${view.aspectMode === a ? 'active' : ''}`} onClick={() => setView({ aspectMode: a })}>
                {a === 'original' ? 'Original' : a === 'square' ? '1:1' : '2:3'}
              </button>
            ))}
          </div>
        </div>

        <div className="sidebar-row">
          <span className="sidebar-row-label">Size</span>
          <input
            type="range"
            className="sidebar-slider"
            min={40}
            max={600}
            step={20}
            value={view.thumbSize}
            onChange={(e) => setView({ thumbSize: parseInt(e.target.value, 10) })}
          />
          <span className="sidebar-row-value">{view.thumbSize}px</span>
        </div>
      </div>

      <div className="sidebar-bottom">
        <label className="sidebar-toggle" style={{ marginBottom: '0.5rem' }}>
          <input type="checkbox" checked={perfEnabled} onChange={(e) => setPerfEnabled(e.target.checked)} />
          <span>Perf overlay</span>
        </label>
        {perfEnabled && (
          <div className="sidebar-meta" style={{ marginBottom: '0.5rem' }}>
            Search avg: {perfStats.avgSearchMs}ms / last: {perfStats.lastSearchMs}ms / count: {perfStats.searchCount}
          </div>
        )}
        <button className="sidebar-link" onClick={() => setShowSettings(true)}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
          </svg>
          Settings
        </button>
      </div>
    </aside>
  );
}
