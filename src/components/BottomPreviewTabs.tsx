'use client';

import React, { useState } from 'react';
import { useImageStore } from '../store/ImageContext';
import CachedImage from './CachedImage';

interface HoverPreviewState {
  id: string;
  left: number;
  top: number;
}

export default function BottomPreviewTabs() {
  const {
    previewTabIds,
    activePreviewId,
    previewById,
    searchResults,
    pinnedPreviewIds,
    setActivePreviewId,
    setSelectedIndex,
    closePreviewTab,
    togglePinPreviewTab,
    restoreLastClosedPreview,
  } = useImageStore();

  const [hoverPreview, setHoverPreview] = useState<HoverPreviewState | null>(null);

  if (previewTabIds.length === 0) return null;

  const activeId = activePreviewId;
  const hoveredImage = hoverPreview ? previewById[hoverPreview.id] : null;

  const showHoverPreview = (
    event: React.MouseEvent<HTMLButtonElement>,
    id: string
  ) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const width = 220;
    const height = 280;
    const left = Math.min(
      Math.max(12, rect.left),
      Math.max(12, window.innerWidth - width - 12)
    );
    const top = Math.max(12, rect.top - height - 12);
    setHoverPreview({ id, left, top });
  };

  return (
    <div className="bottom-preview-tabs">
      <div className="bottom-preview-actions">
        <button className="bottom-preview-action-btn" onClick={restoreLastClosedPreview} title="Restore recently closed tab">
          Restore
        </button>
      </div>
      <div className="bottom-preview-tabs-scroll">
        {previewTabIds.map((id) => {
          const img = previewById[id];
          const label = img?.filename ?? id.split(/[/\\]/).pop() ?? id;
          const isActive = id === activeId;
          const isPinned = pinnedPreviewIds.includes(id);
          const openFullView = () => {
            setActivePreviewId(id);
            const idx = searchResults.findIndex((entry) => entry?.id === id);
            if (idx >= 0) setSelectedIndex(idx);
          };
          const closeByMiddleClick = (event: { button: number; preventDefault: () => void; stopPropagation: () => void }) => {
            if (event.button !== 1) return;
            event.preventDefault();
            event.stopPropagation();
            closePreviewTab(id);
          };

          return (
            <button
              key={id}
              className={`bottom-preview-tab ${isActive ? 'active' : ''}`}
              onClick={openFullView}
              onMouseDown={closeByMiddleClick}
              onMouseUp={closeByMiddleClick}
              onAuxClick={closeByMiddleClick}
              onPointerDownCapture={(event) => closeByMiddleClick(event)}
              onMouseEnter={(event) => showHoverPreview(event, id)}
              onMouseMove={(event) => showHoverPreview(event, id)}
              onMouseLeave={() => setHoverPreview((prev) => (prev?.id === id ? null : prev))}
              title={label}
            >
              <span className="bottom-preview-tab-label">{label}</span>
              <span
                className={`bottom-preview-tab-pin ${isPinned ? 'active' : ''}`}
                onClick={(event) => {
                  event.stopPropagation();
                  togglePinPreviewTab(id);
                }}
                title={isPinned ? 'Unpin tab' : 'Pin tab'}
              >
                {isPinned ? 'P' : 'p'}
              </span>
              <span
                className="bottom-preview-tab-close"
                onClick={(event) => {
                  event.stopPropagation();
                  closePreviewTab(id);
                }}
              >
                x
              </span>
            </button>
          );
        })}
      </div>
      {hoveredImage && hoverPreview && (
        <div
          className="bottom-preview-floating-hover"
          style={{ left: hoverPreview.left, top: hoverPreview.top }}
        >
          <div className="bottom-preview-floating-thumb">
            <CachedImage
              src={hoveredImage.fileUrl}
              requestSrc={`${hoveredImage.fileUrl}${hoveredImage.fileUrl.includes('?') ? '&' : '?'}priority=visible`}
              fallbackSrc={hoveredImage.fullUrl}
              cacheKind="thumb"
              alt={hoveredImage.filename}
            />
          </div>
          <div className="bottom-preview-floating-meta">
            <div className="bottom-preview-floating-name">{hoveredImage.filename}</div>
            <div className="bottom-preview-floating-path" title={hoveredImage.id}>{hoveredImage.id}</div>
          </div>
        </div>
      )}
    </div>
  );
}
