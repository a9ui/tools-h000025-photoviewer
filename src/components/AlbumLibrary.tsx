'use client';

import React, { useMemo, useRef, useState } from 'react';
import { FolderOpen, Image as ImageIcon, Pin, PinOff, RefreshCw, Trash2, X } from 'lucide-react';

import { useDialogFocus } from '../lib/useDialogFocus';
import { useAlbumStore } from '../store/AlbumContext';
import { useImageStore } from '../store/ImageContext';

export default function AlbumLibrary() {
  const {
    document,
    albums,
    activeSource,
    loading,
    error,
    libraryOpen,
    setLibraryOpen,
    createAlbum,
    updateAlbum,
    deleteAlbum,
    openAlbum,
    refreshAlbums,
  } = useAlbumStore();
  const { selectedIds } = useImageStore();
  const dialogRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const [name, setName] = useState('');
  const [editingId, setEditingId] = useState('');
  const [editingName, setEditingName] = useState('');
  const [confirmDeleteId, setConfirmDeleteId] = useState('');
  const [busy, setBusy] = useState(false);

  useDialogFocus({
    open: libraryOpen,
    dialogRef,
    initialFocusRef: closeRef,
    onEscape: () => setLibraryOpen(false),
  });

  const orderedAlbums = useMemo(() => {
    const recentOrder = new Map((document?.recentAlbumIds ?? []).map((id, index) => [id, index]));
    return [...albums].sort((left, right) => {
      if (left.pinned !== right.pinned) return left.pinned ? -1 : 1;
      const leftRecent = recentOrder.get(left.id) ?? Number.MAX_SAFE_INTEGER;
      const rightRecent = recentOrder.get(right.id) ?? Number.MAX_SAFE_INTEGER;
      if (leftRecent !== rightRecent) return leftRecent - rightRecent;
      return left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
    });
  }, [albums, document?.recentAlbumIds]);

  if (!libraryOpen) return null;

  const run = async (operation: () => Promise<unknown>) => {
    setBusy(true);
    try { await operation(); } finally { setBusy(false); }
  };

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    const normalized = name.trim();
    if (!normalized) return;
    await run(async () => {
      const album = await createAlbum(normalized);
      if (album) setName('');
    });
  };

  return (
    <div className="album-dialog-overlay">
      <button className="album-dialog-backdrop" aria-label="Close Album library" onClick={() => setLibraryOpen(false)} />
      <div ref={dialogRef} className="album-dialog" role="dialog" aria-modal="true" aria-labelledby="album-library-title" tabIndex={-1}>
        <div className="album-dialog-header">
          <div>
            <h2 id="album-library-title">Albums</h2>
            <p>Shared by Browser and WPF</p>
          </div>
          <button ref={closeRef} className="icon-btn" aria-label="Close Album library" onClick={() => setLibraryOpen(false)}><X size={18} /></button>
        </div>

        <form className="album-create-row" onSubmit={create}>
          <input value={name} maxLength={120} onChange={(event) => setName(event.target.value)} placeholder="New Album name" aria-label="New Album name" />
          <button className="btn-primary" disabled={busy || !name.trim()}>Create</button>
          <button type="button" className="btn-secondary" aria-label="Refresh Albums" onClick={() => void run(refreshAlbums)} disabled={busy}>
            <RefreshCw size={15} />
          </button>
        </form>

        {error && <p className="album-error" role="alert">{error}</p>}
        {loading ? <p className="album-empty" role="status">Loading Albums…</p> : orderedAlbums.length === 0 ? (
          <p className="album-empty">No Albums yet. Create one, then add selected images.</p>
        ) : (
          <div className="album-list">
            {orderedAlbums.map((album) => {
              const isActive = activeSource?.album.id === album.id;
              const coverCandidate = isActive
                ? activeSource.members.find((member) => selectedIds.includes(member.imagePath))
                : undefined;
              return (
                <section key={album.id} className={`album-row${isActive ? ' active' : ''}`} aria-label={`Album ${album.name}`}>
                  <button className="album-open" disabled={busy || loading} onClick={() => void run(() => openAlbum(album.id))}>
                    <FolderOpen size={18} />
                    <span><strong>{album.name}</strong><small>{album.members.length} member(s) · revision {album.revision}</small></span>
                  </button>
                  <div className="album-row-actions">
                    <button className="icon-btn" title={album.pinned ? 'Unpin Album' : 'Pin Album'} disabled={busy} onClick={() => void run(() => updateAlbum(album.id, { pinned: !album.pinned }))}>
                      {album.pinned ? <PinOff size={16} /> : <Pin size={16} />}
                    </button>
                    {coverCandidate && (
                      <button className="icon-btn" title="Use selected image as Album cover" disabled={busy} onClick={() => void run(() => updateAlbum(album.id, { coverMemberId: coverCandidate.memberId }))}>
                        <ImageIcon size={16} />
                      </button>
                    )}
                    <button className="btn-link" disabled={busy} onClick={() => {
                      setEditingId(album.id);
                      setEditingName(album.name);
                      setConfirmDeleteId('');
                    }}>Rename</button>
                    <button className="icon-btn danger" title="Delete Album only" disabled={busy} onClick={() => setConfirmDeleteId(album.id)}><Trash2 size={16} /></button>
                  </div>
                  {editingId === album.id && (
                    <form className="album-inline-form" onSubmit={(event) => {
                      event.preventDefault();
                      if (!editingName.trim()) return;
                      void run(async () => {
                        if (await updateAlbum(album.id, { name: editingName.trim() })) setEditingId('');
                      });
                    }}>
                      <input value={editingName} maxLength={120} onChange={(event) => setEditingName(event.target.value)} aria-label={`Rename ${album.name}`} />
                      <button className="btn-primary" disabled={busy || !editingName.trim()}>Save</button>
                      <button type="button" className="btn-secondary" onClick={() => setEditingId('')}>Cancel</button>
                    </form>
                  )}
                  {confirmDeleteId === album.id && (
                    <div className="album-delete-confirm" role="alert">
                      <span>Delete this Album? Source images are not recycled.</span>
                      <button className="btn-danger" disabled={busy} onClick={() => void run(async () => {
                        if (await deleteAlbum(album.id)) setConfirmDeleteId('');
                      })}>Delete Album</button>
                      <button className="btn-secondary" onClick={() => setConfirmDeleteId('')}>Cancel</button>
                    </div>
                  )}
                </section>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
