'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ImageProvider, useImageStore } from '../store/ImageContext';
import SearchBar from '../components/SearchBar';
import ImageGrid from '../components/ImageGrid';
import ImageModal from '../components/ImageModal';
import SettingsModal from '../components/SettingsModal';
import Sidebar from '../components/Sidebar';
import RightPreviewPanel from '../components/RightPreviewPanel';
import BottomPreviewTabs from '../components/BottomPreviewTabs';
import EnhanceQueuePanel from '../components/EnhanceQueuePanel';
import { ScanProgressStatus } from '../components/ScanProgressStatus';
import { ScanErrorNotice } from '../components/ScanErrorNotice';
import { getLoadedResultCounts, getResultCountLabel, shouldIgnoreViewerShortcut } from '../lib/viewerUi';
import { appendDirSet, formatDirSet, parseDirSet, removeFromDirSet, summarizeDirSet } from '../lib/pathSet';
import { migrateLegacyPhotoviewerState } from '../lib/localStorageMigration';
import {
  mergeRecentFolderMemories,
  normalizeRecentFolderMemory,
  rememberRecentFolderSet,
  sharedRecentToLocalMemory,
} from '../lib/recentFolders';
import { useDialogFocus } from '../lib/useDialogFocus';
import {
  formatBulkRecycleProgress,
  recycleImagesSequentially,
  snapshotBulkRecycleTargets,
} from '../lib/bulkRecycle';
import { FolderOpen, RefreshCw, Settings, Sparkles, X } from 'lucide-react';

function ViewerApp() {
  const {
    phase, dirPath, setDirPath, startScan, cancelScan, scanProgress, scanError, dismissScanError,
    searchTotal, searchResults, totalIndexed, searchQuery,
    setPhase, view, setView,
    selectedIds, deleteImage,
    cycleFavoriteLevel, decreaseFavoriteLevel, selectedIndex,
    keyBindings, confirmBeforeDelete, setConfirmBeforeDelete, restoreLastClosedPreview, setShowSettings,
    favorites, showFavOnly, showUnfavOnly, favoriteFilterLevels, showEnhancedOnly, enhancedSourceIds,
  } = useImageStore();

  const [browseError, setBrowseError] = useState('');
  const [showBulkDeleteConfirm, setShowBulkDeleteConfirm] = useState(false);
  const bulkDeleteConfirmRef = useRef<HTMLDivElement>(null);
  const bulkDeleteCancelRef = useRef<HTMLButtonElement>(null);
  const bulkDeleteReturnFocusRef = useRef<HTMLButtonElement>(null);
  const bulkDeleteActiveRef = useRef(false);
  const [bulkDeleteTargets, setBulkDeleteTargets] = useState<string[]>([]);
  const [isBulkDeleting, setIsBulkDeleting] = useState(false);
  const [bulkDeleteMessage, setBulkDeleteMessage] = useState('');
  const [recentDirs, setRecentDirs] = useState<string[]>([]);
  const [lastDirSet, setLastDirSet] = useState('');
  const [pasteFolders, setPasteFolders] = useState('');
  const [scanNotice, setScanNotice] = useState('');
  const openFolderSetRef = useRef<HTMLButtonElement>(null);
  const restoreFocusAfterScanCancelRef = useRef(false);
  const selectedFolders = useMemo(() => parseDirSet(dirPath), [dirPath]);
  const selectedCount = selectedIds.length;
  const cancelBulkDelete = useCallback(() => {
    if (bulkDeleteActiveRef.current) return;
    setShowBulkDeleteConfirm(false);
    setBulkDeleteTargets([]);
  }, []);
  useDialogFocus({
    open: showBulkDeleteConfirm,
    dialogRef: bulkDeleteConfirmRef,
    initialFocusRef: bulkDeleteCancelRef,
    onEscape: cancelBulkDelete,
  });
  const loadedResultCounts = useMemo(() => getLoadedResultCounts({
    searchResults,
    favorites,
    showFavOnly,
    showUnfavOnly,
    favoriteFilterLevels,
    showEnhancedOnly,
    enhancedSourceIds,
  }), [
    enhancedSourceIds,
    favoriteFilterLevels,
    favorites,
    searchResults,
    showEnhancedOnly,
    showFavOnly,
    showUnfavOnly,
  ]);
  const resultCountLabel = getResultCountLabel({
    searchQuery,
    searchTotal,
    totalIndexed,
    dateFrom: view.dateFrom,
    dateTo: view.dateTo,
    hiddenFolders: view.hiddenFolders,
    loadedCount: loadedResultCounts.loadedCount,
    shownCount: loadedResultCounts.shownCount,
  });

  const rememberLastDirSet = useCallback((dir: string) => {
    const normalized = normalizeRecentFolderMemory({ lastDirSet: dir }).lastDirSet;
    if (!normalized) return;
    setLastDirSet(normalized);
    try {
      localStorage.setItem('pvu_last_dir_set', normalized);
    } catch {
      // ignore localStorage write errors
    }
  }, []);

  useEffect(() => {
    migrateLegacyPhotoviewerState();
    const { recentDirs: localRecentDirs, lastDirSet: localLastDirSet } = readStoredFolderMemory();
    if (localRecentDirs.length > 0) setRecentDirs(localRecentDirs);
    if (localLastDirSet) setLastDirSet(localLastDirSet);

    const initializeFolderMemory = async () => {
      let activeRecentDirs = localRecentDirs;
      let activeLastDirSet = localLastDirSet;
      const hasLocalFolderMemory = activeRecentDirs.length > 0 || Boolean(activeLastDirSet);

      if (!hasLocalFolderMemory) {
        try {
          const sharedRes = await fetch('/api/recent-folders', { cache: 'no-store' });
          const sharedData = sharedRes.ok ? await sharedRes.json() : null;
          if (sharedData?.ok && !sharedData.malformed && sharedData.recent) {
            const sharedMemory = normalizeRecentFolderMemory(
              sharedRecentToLocalMemory(sharedData.recent)
            );
            if (sharedMemory.recentDirs.length > 0 || sharedMemory.lastDirSet) {
              activeRecentDirs = sharedMemory.recentDirs;
              activeLastDirSet = sharedMemory.lastDirSet;
              setRecentDirs(activeRecentDirs);
              if (activeLastDirSet) setLastDirSet(activeLastDirSet);
              try {
                localStorage.setItem('pvu_recent_dirs', JSON.stringify(activeRecentDirs));
                if (activeLastDirSet) localStorage.setItem('pvu_last_dir_set', activeLastDirSet);
              } catch {
                // ignore localStorage write errors
              }
            }
          }
        } catch {
          // Shared recent folders are best-effort and must not block legacy/local restore.
        }
      }

      let serverLegacyAlreadyImported = false;
      try {
        serverLegacyAlreadyImported = localStorage.getItem('pvu_server_legacy_imported') === '1';
      } catch {
        serverLegacyAlreadyImported = false;
      }
      void fetch('/api/legacy-state', { cache: 'no-store' })
        .then((res) => res.ok ? res.json() : null)
        .then((data) => {
          if (!data) return;
          const legacyMemory = normalizeRecentFolderMemory({
            recentDirs: data.recentDirs,
            lastDirSet: data.lastDirSet,
          });
          if (legacyMemory.recentDirs.length === 0 && !legacyMemory.lastDirSet) return;

          const currentMemory = normalizeRecentFolderMemory({
            recentDirs: activeRecentDirs,
            lastDirSet: activeLastDirSet,
          });
          const mergedMemory = serverLegacyAlreadyImported
            ? mergeRecentFolderMemories(currentMemory, legacyMemory)
            : mergeRecentFolderMemories(legacyMemory, currentMemory);
          const nextRecent = mergedMemory.recentDirs;
          const nextLast = mergedMemory.lastDirSet;

          setRecentDirs(nextRecent);
          if (nextLast) setLastDirSet(nextLast);
          try {
            localStorage.setItem('pvu_recent_dirs', JSON.stringify(nextRecent));
            if (nextLast) localStorage.setItem('pvu_last_dir_set', nextLast);
            localStorage.setItem('pvu_server_legacy_imported', '1');
          } catch {
            // ignore localStorage write errors
          }
        })
        .catch(() => {});
    };

    void initializeFolderMemory();
  }, []);

  useEffect(() => {
    rememberLastDirSet(dirPath);
  }, [dirPath, rememberLastDirSet]);

  const rememberRecentDir = useCallback((dir: string) => {
    const normalized = normalizeRecentFolderMemory({ lastDirSet: dir }).lastDirSet;
    if (!normalized) return;
    rememberLastDirSet(normalized);
    setRecentDirs((prev) => {
      const next = rememberRecentFolderSet({
        recentDirs: prev,
        lastDirSet: normalized,
      }, normalized).recentDirs;
      try {
        localStorage.setItem('pvu_recent_dirs', JSON.stringify(next));
      } catch {
        // ignore localStorage write errors
      }
      void fetch('/api/recent-folders', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ recentDirs: next, lastDirSet: normalized }),
      }).catch(() => {});
      return next;
    });
  }, [rememberLastDirSet]);

  const addFolders = useCallback((folders: string[] | string) => {
    const next = appendDirSet(dirPath, folders);
    setDirPath(next);
    return next;
  }, [dirPath, setDirPath]);

  const removeFolder = useCallback((folder: string) => {
    setDirPath(removeFromDirSet(dirPath, folder));
  }, [dirPath, setDirPath]);

  const handleStartScan = useCallback((
    event?: React.MouseEvent<HTMLButtonElement>,
    overrideDir?: string
  ) => {
    const targetDir = formatDirSet(parseDirSet(overrideDir ?? dirPath));
    if (!targetDir) return;
    setScanNotice('');
    startScan({
      full: Boolean(event?.shiftKey),
      dir: targetDir,
      onComplete: rememberRecentDir,
    });
  }, [dirPath, rememberRecentDir, startScan]);

  const handleCancelScan = useCallback(() => {
    if (phase !== 'scanning') return;
    restoreFocusAfterScanCancelRef.current = true;
    setScanNotice('Scan cancelled. Folder selection preserved.');
    cancelScan();
  }, [cancelScan, phase]);

  useEffect(() => {
    if (phase !== 'landing' || !restoreFocusAfterScanCancelRef.current) return;
    restoreFocusAfterScanCancelRef.current = false;
    openFolderSetRef.current?.focus();
  }, [phase]);

  const handleBrowseFolders = useCallback(async () => {
    try {
      const res = await fetch('/api/browse?multi=1', { method: 'POST' });
      const data = await res.json();
      const selected = Array.isArray(data.paths)
        ? data.paths.filter((item: unknown): item is string => typeof item === 'string')
        : parseDirSet(data.path);
      if (res.ok && selected.length > 0) {
        addFolders(selected);
        setBrowseError('');
      } else if (res.ok) {
        setBrowseError('');
      } else {
        setBrowseError(data.error || 'Failed to open folder dialog.');
      }
    } catch (e) {
      console.error('Browse failed', e);
      setBrowseError('Failed to open folder dialog.');
    }
  }, [addFolders]);

  const handleAddPastedFolders = useCallback(() => {
    const parsed = parseDirSet(pasteFolders);
    if (parsed.length === 0) return;
    addFolders(parsed);
    setPasteFolders('');
    setBrowseError('');
  }, [addFolders, pasteFolders]);

  const markSelectedAsFavorite = useCallback(() => {
    if (selectedCount === 0) return;
    for (const id of selectedIds) {
      cycleFavoriteLevel(id);
    }
  }, [cycleFavoriteLevel, selectedCount, selectedIds]);

  const lowerSelectedFavorite = useCallback(() => {
    if (selectedCount === 0) return;
    for (const id of selectedIds) {
      decreaseFavoriteLevel(id);
    }
  }, [decreaseFavoriteLevel, selectedCount, selectedIds]);

  const deleteSelected = useCallback(async (requestedTargets: readonly string[]) => {
    if (bulkDeleteActiveRef.current) return;
    const targets = snapshotBulkRecycleTargets(requestedTargets);
    if (targets.length === 0) return;

    bulkDeleteActiveRef.current = true;
    setIsBulkDeleting(true);
    setShowBulkDeleteConfirm(false);
    setBulkDeleteMessage(`Moving 0/${targets.length} image(s) to Recycle Bin.`);
    try {
      const result = await recycleImagesSequentially(targets, deleteImage, (progress) => {
        setBulkDeleteMessage(formatBulkRecycleProgress(progress));
      });
      setBulkDeleteMessage(formatBulkRecycleProgress(result));
    } finally {
      bulkDeleteActiveRef.current = false;
      setIsBulkDeleting(false);
      setBulkDeleteTargets([]);
      window.requestAnimationFrame(() => bulkDeleteReturnFocusRef.current?.focus());
    }
  }, [deleteImage]);

  const requestBulkDelete = useCallback(() => {
    if (bulkDeleteActiveRef.current) return;
    const targets = snapshotBulkRecycleTargets(selectedIds);
    if (targets.length === 0) return;
    setBulkDeleteTargets(targets);
    if (confirmBeforeDelete) setShowBulkDeleteConfirm(true);
    else void deleteSelected(targets);
  }, [confirmBeforeDelete, deleteSelected, selectedIds]);

  useEffect(() => {
    if (phase !== 'viewer') return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
      if (shouldIgnoreViewerShortcut(event.target)) return;
      if (selectedIndex !== null) return;
      if (selectedCount === 0) return;

      if (event.key === keyBindings.deleteImage) {
        event.preventDefault();
        requestBulkDelete();
        return;
      }
      if (event.key.toLowerCase() === keyBindings.toggleFavorite.toLowerCase()) {
        event.preventDefault();
        markSelectedAsFavorite();
        return;
      }
      if (event.key.toLowerCase() === keyBindings.decreaseFavorite.toLowerCase()) {
        event.preventDefault();
        lowerSelectedFavorite();
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [
    keyBindings.decreaseFavorite,
    keyBindings.deleteImage,
    keyBindings.toggleFavorite,
    lowerSelectedFavorite,
    markSelectedAsFavorite,
    phase,
    requestBulkDelete,
    selectedCount,
    selectedIndex,
  ]);

  useEffect(() => {
    if (phase !== 'viewer') return;
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.shiftKey && event.key.toLowerCase() === 't') {
        event.preventDefault();
        restoreLastClosedPreview();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [phase, restoreLastClosedPreview]);

  if (phase === 'landing' || phase === 'scanning') {
    return (
      <>
        <div className="landing">
        <button
          className="landing-settings-btn"
          type="button"
          onClick={() => setShowSettings(true)}
          aria-label="Open settings and runtime version"
        >
          <Settings size={17} aria-hidden="true" />
          Settings
        </button>
        <h1 className="landing-title">PhotoViewer</h1>
        <p className="landing-subtitle">Index and search Stable Diffusion PNG metadata locally</p>

        <div className="folder-set-panel">
          <div className="folder-set-actions">
            <button
              className="browse-btn"
              onClick={handleBrowseFolders}
              disabled={phase === 'scanning'}
              title={phase === 'scanning' ? 'Adding folders is unavailable while scanning.' : 'Add folders'}
              aria-label={phase === 'scanning' ? 'Adding folders is unavailable while scanning.' : 'Add folders'}
              type="button"
            >
              <FolderOpen size={18} aria-hidden="true" />
              Add folder
            </button>
            <button
              ref={openFolderSetRef}
              className="scan-btn"
              onClick={(event) => handleStartScan(event)}
              disabled={phase === 'scanning' || selectedFolders.length === 0}
              aria-label={phase === 'scanning'
                ? 'Scanning in progress. Opening a folder set is unavailable until scanning completes.'
                : selectedFolders.length === 0
                  ? 'Open folder set is unavailable because no folders are selected.'
                  : 'Open folder set'}
              type="button"
            >
              {phase === 'scanning' ? 'Scanning...' : 'Open folder set'}
            </button>
          </div>

          <div className="selected-folder-list" aria-label="Selected folders">
            {selectedFolders.length > 0 ? (
              selectedFolders.map((folder) => (
                <div className="selected-folder-row" key={folder} title={folder}>
                  <span className="selected-folder-bullet" aria-hidden="true">・</span>
                  <span className="selected-folder-name">{folder}</span>
                  <button
                    className="selected-folder-remove"
                    type="button"
                    aria-label={`Remove ${folder}`}
                    title="Remove folder"
                    onClick={() => removeFolder(folder)}
                    disabled={phase === 'scanning'}
                  >
                    <X size={14} aria-hidden="true" />
                  </button>
                </div>
              ))
            ) : (
              <div className="selected-folder-empty">No folders selected yet.</div>
            )}
          </div>

          <div className="folder-paste-row">
            <textarea
              className="dir-input folder-paste-input"
              placeholder="Paste one absolute path per line..."
              value={pasteFolders}
              onChange={(e) => setPasteFolders(e.target.value)}
              onKeyDown={(e) => {
                if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') handleAddPastedFolders();
              }}
              disabled={phase === 'scanning'}
            />
            <button
              className="browse-btn folder-paste-add"
              type="button"
              onClick={handleAddPastedFolders}
              disabled={phase === 'scanning' || parseDirSet(pasteFolders).length === 0}
            >
              Add pasted
            </button>
          </div>
        </div>
        {lastDirSet && (
          <div className="last-dir-set">
            <button
              className="last-dir-set-btn"
              type="button"
              title={lastDirSet}
              onClick={() => handleStartScan(undefined, lastDirSet)}
              disabled={phase === 'scanning'}
            >
              Open last folder set
              <span>{summarizeDirSet(lastDirSet)}</span>
            </button>
          </div>
        )}
        {recentDirs.length > 0 && (
          <div className="recent-dirs">
            <div className="recent-dirs-label">Recent folder sets</div>
            <div className="recent-dirs-list">
              {recentDirs.map((dir) => (
                <button
                  key={dir}
                  className="recent-dir-item"
                  title={dir}
                  onClick={() => handleStartScan(undefined, dir)}
                  disabled={phase === 'scanning'}
                >
                  {summarizeDirSet(dir)}
                </button>
              ))}
            </div>
          </div>
        )}
        {browseError && <p className="landing-error" role="alert">{browseError}</p>}
        {scanError && (
          <ScanErrorNotice
            message={scanError}
            canRetry={phase !== 'scanning' && selectedFolders.length > 0}
            onRetry={() => handleStartScan()}
            onDismiss={dismissScanError}
          />
        )}

        {phase === 'landing' && scanNotice && (
          <div className="progress-container">
            <div className="progress-waiting" role="status" aria-live="polite" aria-atomic="true">
              {scanNotice}
            </div>
          </div>
        )}

        {phase === 'scanning' && scanProgress && (
          <ScanProgressStatus progress={scanProgress} onCancel={handleCancelScan} />
        )}
        </div>
        <SettingsModal />
      </>
    );
  }

  return (
    <>
      <div className="viewer">
        <header className="viewer-header">
          <button
            ref={bulkDeleteReturnFocusRef}
            className="icon-btn sidebar-toggle-btn"
            onClick={() => setView({ sidebarOpen: !view.sidebarOpen })}
            title={view.sidebarOpen ? 'Hide sidebar' : 'Show sidebar'}
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
              <line x1="9" y1="3" x2="9" y2="21" />
            </svg>
          </button>
          <button className="viewer-logo" onClick={() => setPhase('landing')} title="Back to folder selection" type="button">
            PhotoViewer
          </button>
          <button
            className="icon-btn sidebar-toggle-btn"
            onClick={handleStartScan}
            title="Refresh active folder (Shift-click: full verify)"
            disabled={!dirPath.trim()}
          >
            <RefreshCw size={18} aria-hidden="true" />
          </button>
          <SearchBar />
          <span className="header-stats" aria-label={`Results: ${resultCountLabel}`}>{resultCountLabel}</span>
          <button
            className="icon-btn sidebar-toggle-btn"
            onClick={() => setView({ rightPanelOpen: !view.rightPanelOpen })}
            title={view.rightPanelOpen ? 'Hide right panel' : 'Show right panel'}
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
              <line x1="15" y1="3" x2="15" y2="21" />
            </svg>
          </button>
          <button
            className="icon-btn sidebar-toggle-btn"
            onClick={() => setView({ enhanceQueueOpen: !view.enhanceQueueOpen })}
            title={view.enhanceQueueOpen ? 'Hide enhance queue' : 'Show enhance queue'}
          >
            <Sparkles size={18} aria-hidden="true" />
          </button>
        </header>

        {bulkDeleteMessage && (
          <div
            className="bulk-message viewer-bulk-message"
            role="status"
            aria-live="polite"
            aria-atomic="true"
            aria-busy={isBulkDeleting}
          >
            {bulkDeleteMessage}
          </div>
        )}

        <div className="viewer-body">
          <Sidebar />
          <main className="viewer-main">
            <ImageGrid />
          </main>
          <RightPreviewPanel />
        </div>
        <BottomPreviewTabs />
        <ImageModal />
        <EnhanceQueuePanel />
        <SettingsModal />
      </div>

      {showBulkDeleteConfirm && (
        <div className="confirm-overlay">
          <div className="confirm-backdrop" aria-hidden="true" onClick={cancelBulkDelete} />
          <div ref={bulkDeleteConfirmRef} className="confirm-panel" role="alertdialog" aria-modal="true" aria-labelledby="bulk-delete-title" tabIndex={-1}>
            <h3 id="bulk-delete-title">Move selected images to Recycle Bin?</h3>
            <p>{bulkDeleteTargets.length} image(s) will be moved to Recycle Bin.</p>
            <label className="sidebar-toggle" style={{ justifyContent: 'center', marginBottom: '1rem' }}>
              <input
                type="checkbox"
                checked={!confirmBeforeDelete}
                onChange={(e) => setConfirmBeforeDelete(!e.target.checked)}
              />
              <span>Do not ask again</span>
            </label>
            <div className="confirm-actions">
              <button ref={bulkDeleteCancelRef} className="btn-cancel" onClick={cancelBulkDelete}>Cancel</button>
              <button
                className="btn-danger"
                onClick={() => void deleteSelected(bulkDeleteTargets)}
                disabled={isBulkDeleting || bulkDeleteTargets.length === 0}
              >
                {isBulkDeleting ? 'Moving...' : 'Move to Recycle Bin'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

function readStoredFolderMemory(): { recentDirs: string[]; lastDirSet: string } {
  if (typeof window === 'undefined') return { recentDirs: [], lastDirSet: '' };
  try {
    const raw = localStorage.getItem('pvu_recent_dirs');
    const last = localStorage.getItem('pvu_last_dir_set');
    return normalizeRecentFolderMemory({
      recentDirs: raw ? JSON.parse(raw) : [],
      lastDirSet: last,
    });
  } catch {
    return { recentDirs: [], lastDirSet: '' };
  }
}

export default function App() {
  return (
    <ImageProvider>
      <ViewerApp />
    </ImageProvider>
  );
}
