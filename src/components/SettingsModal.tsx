'use client';

import React, { useCallback, useState } from 'react';
import { useImageStore } from '../store/ImageContext';
import type { KeyBindings } from '../lib/types';
import { clampModalEdgeRatio } from '../lib/modalNavigation';

const KEY_LABELS: Record<keyof KeyBindings, string> = {
  nextImage: 'Next image',
  prevImage: 'Previous image',
  toggleFavorite: 'Toggle favorite',
  decreaseFavorite: 'Decrease favorite',
  deleteImage: 'Delete image',
  closeModal: 'Close modal',
  flipHorizontal: 'Flip horizontal',
  enhanceImage: 'Enhance image',
  zoomIn: 'Zoom in',
  zoomOut: 'Zoom out',
  zoomReset: 'Reset zoom',
};

export default function SettingsModal() {
  const {
    showSettings,
    setShowSettings,
    keyBindings,
    setKeyBindings,
    confirmBeforeDelete,
    setConfirmBeforeDelete,
    view,
    setView,
  } = useImageStore();

  const [recording, setRecording] = useState<keyof KeyBindings | null>(null);
  const modalEdgePercent = Math.round(clampModalEdgeRatio(view.modalEdgeRatio) * 100);

  const handleKeyCapture = useCallback((e: React.KeyboardEvent, action: keyof KeyBindings) => {
    e.preventDefault();
    e.stopPropagation();
    setKeyBindings({ ...keyBindings, [action]: e.key });
    setRecording(null);
  }, [keyBindings, setKeyBindings]);

  if (!showSettings) return null;

  return (
    <div className="settings-overlay">
      <div className="settings-backdrop" onClick={() => { setShowSettings(false); setRecording(null); }} />
      <div className="settings-panel">
        <div className="settings-header">
          <h2>Settings</h2>
          <button className="icon-btn" onClick={() => { setShowSettings(false); setRecording(null); }} title="Close">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>

        <div className="settings-body">
          <h3 style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginBottom: '1rem' }}>
            Behavior
          </h3>
          <div className="setting-row">
            <span className="setting-label">Confirm before delete</span>
            <label className="sidebar-toggle">
              <input
                aria-label="Confirm before delete"
                type="checkbox"
                checked={confirmBeforeDelete}
                onChange={(e) => setConfirmBeforeDelete(e.target.checked)}
              />
              <span>{confirmBeforeDelete ? 'Enabled' : 'Disabled'}</span>
            </label>
          </div>
          <div className="setting-row">
            <span className="setting-label">Unseen dots</span>
            <label className="sidebar-toggle">
              <input
                aria-label="Show unseen markers"
                type="checkbox"
                checked={view.showUnseenMarkers}
                onChange={(e) => setView({ showUnseenMarkers: e.target.checked })}
              />
              <span>{view.showUnseenMarkers ? 'Enabled' : 'Disabled'}</span>
            </label>
          </div>
          <div className="setting-row">
            <span className="setting-label">Modal edge navigation zone</span>
            <div className="setting-range-control">
              <input
                type="range"
                min="10"
                max="40"
                step="1"
                value={modalEdgePercent}
                onChange={(e) => setView({ modalEdgeRatio: clampModalEdgeRatio(Number(e.target.value) / 100) })}
              />
              <span className="setting-value">{modalEdgePercent}%</span>
            </div>
          </div>

          <h3 style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', margin: '1rem 0' }}>
            Key bindings
          </h3>
          {(Object.keys(KEY_LABELS) as (keyof KeyBindings)[]).map((action) => (
            <div key={action} className="setting-row">
              <span className="setting-label">{KEY_LABELS[action]}</span>
              <button
                className={`setting-key ${recording === action ? 'recording' : ''}`}
                onClick={() => setRecording(action)}
                onKeyDown={recording === action ? (e) => handleKeyCapture(e, action) : undefined}
              >
                {recording === action ? 'Press key...' : formatKey(keyBindings[action])}
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function formatKey(key: string): string {
  const map: Record<string, string> = {
    ArrowLeft: 'Left',
    ArrowRight: 'Right',
    ArrowUp: 'Up',
    ArrowDown: 'Down',
    Escape: 'Esc',
    Delete: 'Del',
    ' ': 'Space',
    Enter: 'Enter',
  };
  return map[key] || key.toUpperCase();
}
