'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useImageStore } from '../store/ImageContext';
import { DEFAULT_KEY_BINDINGS, type KeyBindings } from '../lib/types';
import { clampModalEdgeRatio } from '../lib/modalNavigation';
import { useDialogFocus } from '../lib/useDialogFocus';
import { getKeyBindingConflicts } from '../lib/keyBindings';

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
  const [draftKeyBindings, setDraftKeyBindings] = useState<KeyBindings>(keyBindings);
  const modalEdgePercent = Math.round(clampModalEdgeRatio(view.modalEdgeRatio) * 100);
  const panelRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const close = useCallback(() => {
    setShowSettings(false);
    setRecording(null);
    setDraftKeyBindings(keyBindings);
  }, [keyBindings, setShowSettings]);

  useEffect(() => {
    if (!showSettings) return;
    setDraftKeyBindings(keyBindings);
    setRecording(null);
  }, [keyBindings, showSettings]);

  useDialogFocus({
    open: showSettings,
    dialogRef: panelRef,
    initialFocusRef: closeButtonRef,
    onEscape: close,
  });

  const conflicts = useMemo(
    () => getKeyBindingConflicts(draftKeyBindings),
    [draftKeyBindings]
  );
  const conflictByAction = useMemo(() => {
    const next = new Map<keyof KeyBindings, Array<keyof KeyBindings>>();
    for (const conflict of conflicts) {
      for (const action of conflict.actions) {
        next.set(action, conflict.actions.filter((item) => item !== action));
      }
    }
    return next;
  }, [conflicts]);
  const hasBindingConflicts = conflicts.length > 0;

  const handleKeyCapture = useCallback((e: React.KeyboardEvent, action: keyof KeyBindings) => {
    e.preventDefault();
    e.stopPropagation();
    setDraftKeyBindings((current) => ({ ...current, [action]: e.key }));
    setRecording(null);
  }, []);
  const saveKeyBindings = useCallback(() => {
    if (hasBindingConflicts) return;
    setKeyBindings(draftKeyBindings);
    close();
  }, [close, draftKeyBindings, hasBindingConflicts, setKeyBindings]);
  const resetKeyBindings = useCallback(() => {
    setDraftKeyBindings(DEFAULT_KEY_BINDINGS);
    setRecording(null);
  }, []);

  if (!showSettings) return null;

  return (
    <div className="settings-overlay">
      <div className="settings-backdrop" aria-hidden="true" onClick={close} />
      <div ref={panelRef} className="settings-panel" role="dialog" aria-modal="true" aria-labelledby="settings-title" tabIndex={-1}>
        <div className="settings-header">
          <h2 id="settings-title">Settings</h2>
          <button ref={closeButtonRef} className="icon-btn" onClick={close} title="Close" aria-label="Close settings">
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
          {(Object.keys(KEY_LABELS) as (keyof KeyBindings)[]).map((action) => {
            const conflictsWith = conflictByAction.get(action) ?? [];
            const errorId = `key-binding-error-${action}`;
            return (
            <div key={action} className="setting-row">
              <span className="setting-label">{KEY_LABELS[action]}</span>
              <button
                type="button"
                className={`setting-key ${recording === action ? 'recording' : ''}`}
                onClick={() => setRecording(action)}
                onKeyDown={recording === action ? (e) => handleKeyCapture(e, action) : undefined}
                aria-label={`${KEY_LABELS[action]} binding`}
                aria-invalid={conflictsWith.length > 0 || undefined}
                aria-describedby={conflictsWith.length > 0 ? errorId : undefined}
              >
                {recording === action ? 'Press key...' : formatKey(draftKeyBindings[action])}
              </button>
              {conflictsWith.length > 0 && (
                <p id={errorId} className="sidebar-error" role="alert">
                  Also assigned to {conflictsWith.map((item) => KEY_LABELS[item]).join(', ')}.
                </p>
              )}
            </div>
            );
          })}
          <div className="sidebar-actions" style={{ marginTop: '0.75rem' }}>
            <button type="button" className="sidebar-link" onClick={resetKeyBindings}>
              Reset to defaults
            </button>
            <button
              type="button"
              className="sidebar-link"
              onClick={saveKeyBindings}
              disabled={hasBindingConflicts}
            >
              Save key bindings
            </button>
          </div>
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
