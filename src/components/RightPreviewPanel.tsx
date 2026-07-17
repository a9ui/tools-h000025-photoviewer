'use client';

import React, { useEffect, useRef, useState } from 'react';
import { useImageStore } from '../store/ImageContext';
import CachedImage from './CachedImage';
import { EnhanceSettingsControls, createEnhancementJob, getEnhancementSettings } from './EnhanceQueuePanel';
import { useDialogFocus } from '../lib/useDialogFocus';

function formatDateTime(value?: number): string {
  if (!value) return '-';
  return new Date(value).toLocaleString();
}

export default function RightPreviewPanel() {
  const {
    previewTabIds,
    activePreviewId,
    previewById,
    cycleFavoriteLevel,
    decreaseFavoriteLevel,
    favorites,
    openExternal,
    selectedIds,
    clearSelection,
    deleteImage,
    view,
    setView,
    confirmBeforeDelete,
    setConfirmBeforeDelete,
  } = useImageStore();

  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false);
  const confirmPanelRef = useRef<HTMLDivElement>(null);
  const confirmCancelButtonRef = useRef<HTMLButtonElement>(null);
  const [bulkMessage, setBulkMessage] = useState('');
  const [enhanceMessage, setEnhanceMessage] = useState('');
  const [showDetails, setShowDetails] = useState(false);
  const selectedCount = selectedIds.length;

  useDialogFocus({
    open: confirmBulkDelete,
    dialogRef: confirmPanelRef,
    initialFocusRef: confirmCancelButtonRef,
    onEscape: () => setConfirmBulkDelete(false),
  });

  const [panelWidth, setPanelWidth] = useState(view.rightPanelWidth ?? 320);
  const isResizing = useRef(false);
  const dragStartX = useRef(0);
  const dragStartWidth = useRef(0);
  const widthRef = useRef(panelWidth);

  useEffect(() => {
    widthRef.current = panelWidth;
  }, [panelWidth]);

  const onHandleMouseDown = (e: React.MouseEvent) => {
    isResizing.current = true;
    dragStartX.current = e.clientX;
    dragStartWidth.current = widthRef.current;
    e.preventDefault();
  };

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!isResizing.current) return;
      const delta = dragStartX.current - e.clientX;
      const next = Math.max(240, Math.min(900, dragStartWidth.current + delta));
      setPanelWidth(next);
    };

    const onUp = () => {
      if (!isResizing.current) return;
      isResizing.current = false;
      setView({ rightPanelWidth: widthRef.current });
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
    return () => {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
    };
  }, [setView]);

  const handleFavoriteSelected = () => {
    if (selectedCount === 0) return;
    for (const id of selectedIds) cycleFavoriteLevel(id);
    setBulkMessage(`Raised favorite level for ${selectedCount} image(s).`);
  };

  const handleOpenSelected = () => {
    if (selectedCount === 0) return;
    for (const id of selectedIds) openExternal(id);
    setBulkMessage(`Opened ${selectedCount} image(s) in external viewer.`);
  };

  const enhanceOne = async (id: string) => {
    try {
      setView({ enhanceQueueOpen: true });
      const job = await createEnhancementJob(id, getEnhancementSettings());
      setEnhanceMessage(`Enhance queued: ${job.id.slice(0, 8)}`);
    } catch (err) {
      setEnhanceMessage(err instanceof Error ? err.message : String(err));
    }
  };

  const enhanceSelected = async () => {
    if (selectedCount === 0) return;
    let queued = 0;
    setView({ enhanceQueueOpen: true });
    for (const id of selectedIds) {
      try {
        await createEnhancementJob(id, getEnhancementSettings());
        queued += 1;
      } catch (err) {
        setEnhanceMessage(`Queued ${queued}/${selectedCount}. ${err instanceof Error ? err.message : String(err)}`);
        return;
      }
    }
    setEnhanceMessage(`Enhance queued for ${queued} image(s).`);
  };

  const executeBulkDelete = async () => {
    if (selectedCount === 0) return;
    const targets = [...selectedIds];
    let success = 0;
    for (const id of targets) {
      const ok = await deleteImage(id);
      if (ok) success++;
    }
    const failed = targets.length - success;
    setConfirmBulkDelete(false);
    clearSelection();
    setBulkMessage(
      failed > 0
        ? `Moved ${success}/${targets.length} to Recycle Bin. Failed: ${failed}.`
        : `Moved ${success} image(s) to Recycle Bin.`
    );
  };

  const handleBulkDeleteRequest = () => {
    if (selectedCount === 0) return;
    if (confirmBeforeDelete) setConfirmBulkDelete(true);
    else void executeBulkDelete();
  };

  const panelStyle = { width: panelWidth, minWidth: panelWidth, maxWidth: panelWidth };

  const activeId = activePreviewId;
  const active = activeId ? previewById[activeId] : null;
  const previewSrc = active ? (active.displayUrl || active.fileUrl) : null;

  if (!view.rightPanelOpen) return null;

  if (previewTabIds.length === 0 && !activePreviewId) {
    return (
      <aside className="preview-panel empty" style={panelStyle}>
        <div className="preview-resize-handle" onMouseDown={onHandleMouseDown} />
        <div className="preview-empty">
          <p>Click an image to open preview.</p>
          <p>Use Ctrl/Shift for multi-select.</p>
        </div>
      </aside>
    );
  }

  return (
    <>
      <aside className="preview-panel" style={panelStyle}>
        <div className="preview-resize-handle" onMouseDown={onHandleMouseDown} />

        <div className="bulk-toolbar">
          <span className="bulk-count">{selectedCount > 0 ? `${selectedCount} selected` : 'No selection'}</span>
          <button className="pill" onClick={handleFavoriteSelected} disabled={selectedCount === 0}>Favorite</button>
          <button className="pill" onClick={handleOpenSelected} disabled={selectedCount === 0}>Open</button>
          <button className="pill" onClick={() => void enhanceSelected()} disabled={selectedCount === 0}>Enhance selected</button>
          <button className="pill danger" onClick={handleBulkDeleteRequest} disabled={selectedCount === 0}>Recycle</button>
        </div>
        {bulkMessage && <div className="bulk-message">{bulkMessage}</div>}
        {enhanceMessage && <div className="bulk-message">{enhanceMessage}</div>}
        <EnhanceSettingsControls />

        {!active ? (
          <div className="preview-empty">
            <p>Click an image to preview it, or choose a saved tab below.</p>
          </div>
        ) : (
          <div className="preview-content">
            <div className="preview-main">
              <div className="preview-image-wrap">
                <CachedImage
                  key={active.id}
                  src={previewSrc ?? active.fileUrl}
                  requestSrc={`${previewSrc ?? active.fileUrl}${(previewSrc ?? active.fileUrl).includes('?') ? '&' : '?'}priority=visible`}
                  fallbackSrc={active.fullUrl}
                  cacheKind="display"
                  alt={active.filename}
                  className="preview-image"
                  loading="eager"
                  decoding="async"
                  fetchPriority="high"
                />
              </div>

              <div className="preview-actions">
                <button className={`pill ${(favorites[active.id] ?? 0) > 0 ? 'active' : ''}`} onClick={() => cycleFavoriteLevel(active.id)}>
                  Favorite +1 {favorites[active.id] ? `(${favorites[active.id]})` : ''}
                </button>
                <button className="pill" onClick={() => decreaseFavoriteLevel(active.id)}>Favorite -1</button>
                <button className="pill" onClick={() => openExternal(active.id)}>Open External</button>
                <button className="pill" onClick={() => void enhanceOne(active.id)}>Enhance</button>
                <button className={`pill ${showDetails ? 'active' : ''}`} onClick={() => setShowDetails((v) => !v)}>
                  {showDetails ? 'Hide Details' : 'Show Details'}
                </button>
              </div>
            </div>

            {showDetails && (
              <div className="preview-details-pane">
                <div className="preview-meta">
                  <div className="preview-meta-row">
                    <span className="preview-meta-label">Path</span>
                    <span className="preview-meta-value" title={active.id}>{active.id}</span>
                  </div>
                  <div className="preview-meta-row">
                    <span className="preview-meta-label">Created</span>
                    <span className="preview-meta-value">{formatDateTime(active.createdAt)}</span>
                  </div>
                  <div className="preview-meta-row">
                    <span className="preview-meta-label">Modified</span>
                    <span className="preview-meta-value">{formatDateTime(active.mtime)}</span>
                  </div>
                  <div className="preview-meta-block">
                    <div className="preview-meta-label">Prompt</div>
                    <div className="preview-meta-text">{active.metadata?.prompt || '-'}</div>
                  </div>
                  <div className="preview-meta-block">
                    <div className="preview-meta-label">Negative Prompt</div>
                    <div className="preview-meta-text">{active.metadata?.negativePrompt || '-'}</div>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}
      </aside>

      {confirmBulkDelete && (
        <div className="confirm-overlay">
          <div className="confirm-backdrop" aria-hidden="true" onClick={() => setConfirmBulkDelete(false)} />
          <div ref={confirmPanelRef} className="confirm-panel" role="alertdialog" aria-modal="true" aria-labelledby="preview-bulk-delete-title" tabIndex={-1}>
            <h3 id="preview-bulk-delete-title">Move selected images to Recycle Bin?</h3>
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
              <button ref={confirmCancelButtonRef} className="btn-cancel" onClick={() => setConfirmBulkDelete(false)}>Cancel</button>
              <button className="btn-danger" onClick={() => void executeBulkDelete()}>Move to Recycle Bin</button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
