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
import { getResultCountLabel, shouldIgnoreViewerShortcut } from '../lib/viewerUi';
import { appendDirSet, formatDirSet, parseDirSet, removeFromDirSet, summarizeDirSet } from '../lib/pathSet';
import { migrateLegacyPhotoviewerState } from '../lib/localStorageMigration';
import { sharedRecentToLocalMemory } from '../lib/recentFolders';
import { useDialogFocus } from '../lib/useDialogFocus';
import { FolderOpen, RefreshCw, Sparkles } from 'lucide-react';

function ViewerApp() {
  const {
    phase, dirPath, setDirPath, startScan, scanProgress, scanError, dismissScanError,
    searchTotal, totalIndexed, searchQuery,
    setPhase, view, setView,
    selectedIds, clearSelection, deleteImage,
    cycleFavoriteLevel, decreaseFavoriteLevel, selectedIndex,
    keyBindings, confirmBeforeDelete, setConfirmBeforeDelete, restoreLastClosedPreview,
  } = useImageStore();

  const [browseError, setBrowseError] = useState('');
  const [showBulkDeleteConfirm, setShowBulkDeleteConfirm] = useState(false);
  const bulkDeleteConfirmRef = useRef<HTMLDivElement>(null);
  const bulkDeleteCancelRef = useRef<HTMLButtonElement>(null);
  const [recentDirs, setRecentDirs] = useState<string[]>([]);
  const [lastDirSet, setLastDirSet] = useState('');
  const [pasteFolders, setPasteFolders] = useState('');
  const selectedFolders = useMemo(() => parseDirSet(dirPath), [dirPath]);
  const selectedCount = selectedIds.length;
  useDialogFocus({
    open: showBulkDeleteConfirm,
    dialogRef: bulkDeleteConfirmRef,
    initialFocusRef: bulkDeleteCancelRef,
    onEscape: () => setShowBulkDeleteConfirm(false),
  });
  const resultCountLabel = getResultCountLabel({
    searchQuery,
    searchTotal,
    totalIndexed,
    dateFrom: view.dateFrom,
    dateTo: view.dateTo,
    hiddenFolders: view.hiddenFolders,
  });

  const rememberLastDirSet = useCallback((dir: string) => {
    const normalized = formatDirSet(parseDirSet(dir));
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
            const sharedMemory = sharedRecentToLocalMemory(sharedData.recent);
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
          const legacyRecent = Array.isArray(data.recentDirs)
            ? data.recentDirs
              .filter((v: unknown): v is string => typeof v === 'string')
              .map((v: string) => formatDirSet(parseDirSet(v)))
              .filter(Boolean)
            : [];
          const legacyLast = typeof data.lastDirSet === 'string'
            ? formatDirSet(parseDirSet(data.lastDirSet))
            : '';
          if (legacyRecent.length === 0 && !legacyLast) return;

          const combined = [
            ...(serverLegacyAlreadyImported ? activeRecentDirs : legacyRecent),
            ...(serverLegacyAlreadyImported ? legacyRecent : activeRecentDirs),
          ].filter(Boolean);
          const seen = new Set<string>();
          const nextRecent = combined.filter((value) => {
            const key = value.toLowerCase();
            if (seen.has(key)) return false;
            seen.add(key);
            return true;
          }).slice(0, 8);
          const nextLast = serverLegacyAlreadyImported
            ? (activeLastDirSet || legacyLast)
            : (legacyLast || activeLastDirSet);

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
    const normalized = formatDirSet(parseDirSet(dir));
    if (!normalized) return;
    rememberLastDirSet(normalized);
    setRecentDirs((prev) => {
      const next = [normalized, ...prev.filter((v) => v !== normalized)].slice(0, 8);
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
    startScan({
      full: Boolean(event?.shiftKey),
      dir: targetDir,
      onComplete: rememberRecentDir,
    });
  }, [dirPath, rememberRecentDir, startScan]);

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

  const deleteSelected = useCallback(async () => {
    if (selectedCount === 0) return;
    const targets = [...selectedIds];
    for (const id of targets) {
      await deleteImage(id);
    }
    clearSelection();
  }, [clearSelection, deleteImage, selectedCount, selectedIds]);

  useEffect(() => {
    if (phase !== 'viewer') return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
      if (shouldIgnoreViewerShortcut(event.target)) return;
      if (selectedIndex !== null) return;
      if (selectedCount === 0) return;

      if (event.key === keyBindings.deleteImage) {
        event.preventDefault();
        if (confirmBeforeDelete) setShowBulkDeleteConfirm(true);
        else void deleteSelected();
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
    confirmBeforeDelete,
    deleteSelected,
    keyBindings.decreaseFavorite,
    keyBindings.deleteImage,
    keyBindings.toggleFavorite,
    lowerSelectedFavorite,
    markSelectedAsFavorite,
    phase,
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
      <div className="landing">
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
                    ×
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

        {phase === 'scanning' && scanProgress && (
          <ScanProgressStatus progress={scanProgress} />
        )}
      </div>
    );
  }

  return (
    <>
      <div className="viewer">
        <header className="viewer-header">
          <button
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
          <span className="header-stats">{resultCountLabel}</span>
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
          <div className="confirm-backdrop" aria-hidden="true" onClick={() => setShowBulkDeleteConfirm(false)} />
          <div ref={bulkDeleteConfirmRef} className="confirm-panel" role="alertdialog" aria-modal="true" aria-labelledby="bulk-delete-title" tabIndex={-1}>
            <h3 id="bulk-delete-title">Move selected images to Recycle Bin?</h3>
            <p>{selectedCount} image(s) will be moved to Recycle Bin.</p>
            <label className="sidebar-toggle" style={{ justifyContent: 'center', marginBottom: '1rem' }}>
              <input
                type="checkbox"
                checked={!confirmBeforeDelete}
                onChange={(e) => setConfirmBeforeDelete(!e.target.checked)}
              />
              <span>Do not ask again</span>
            </label>
            <div className="confirm-actions">
              <button ref={bulkDeleteCancelRef} className="btn-cancel" onClick={() => setShowBulkDeleteConfirm(false)}>Cancel</button>
              <button
                className="btn-danger"
                onClick={async () => {
                  setShowBulkDeleteConfirm(false);
                  await deleteSelected();
                }}
              >
                Move to Recycle Bin
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
    const recentDirs = raw
      ? JSON.parse(raw)
        .filter((v: unknown): v is string => typeof v === 'string')
        .map((v: string) => formatDirSet(parseDirSet(v)))
        .filter(Boolean)
        .slice(0, 8)
      : [];
    const last = localStorage.getItem('pvu_last_dir_set');
    return {
      recentDirs,
      lastDirSet: last ? formatDirSet(parseDirSet(last)) : '',
    };
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
