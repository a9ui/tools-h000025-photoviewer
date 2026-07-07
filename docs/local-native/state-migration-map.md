# Local Native State Migration Map

The native lane must preserve H000025 user state without forcing the browser
app to change shape.

## Disk State Already Reusable

| Source | Current Shape | Native Handling |
| --- | --- | --- |
| `.cache/favorites.json` | `Record<absolutePath, level>` | Read immediately; later write through native store and optional JSON sync. |
| `.cache/albums.json` | `{ albums: Album[] }` | M2 imports album id/name/image-count summary rows into SQLite `albums`. Full album image membership remains M4 parity work. |
| `.cache/settings.json` | app settings JSON | M2 stores browser settings presence/raw JSON marker and native key-binding defaults in SQLite `native_settings`. |
| `.cache/index_*.json` | per-root scan cache | Treat as optional seed data; do not rely on whole-file JSON long term. |
| `.cache/thumbs` | WebP thumbnails | Reuse only after cache key compatibility is proven. |
| `.cache/display` | WebP display variants | Reuse as a warm display cache when source/version matches. |

## Browser-Only State

These keys live in browser `localStorage` and are not directly visible to a
desktop executable:

- `pvu_view`
- `pvu_pinned_tabs`
- `pvu_perf_enabled`
- `pvu_fav_only`
- `pvu_unfav_only`
- `pvu_fav_levels`
- `pvu_enhanced_only`
- `pvu_scroll_memory`
- `pvu_seen_images`
- `pvu_recent_albums`
- `pvu_recent_dirs`
- `pvu_last_dir_set`
- `pvu_enhance_settings`

Native import should use an explicit export file from the browser app or a
small compatibility route. Do not read Chrome profile storage directly.

## M1 Native Store

M1 creates `.cache/native/photoviewer-native.sqlite` with:

- `images`
- `scan_roots`
- `favorites`
- `import_runs`

Favorites import is active. Albums, settings, and browser-only `pvu_*` state
remain compatibility targets for later milestones.

## M2 Native Store

M2 extends `.cache/native/photoviewer-native.sqlite` with:

- `albums`
- `native_settings`

Native settings currently include recent folder, view mode, search text,
favorite-only filter, default key-binding JSON, and browser settings import
markers. Favorite level mutation writes to SQLite immediately. Delete removes
the image/favorite rows only after the Windows Recycle Bin operation succeeds.

## M3 Native Store

M3 extends the same SQLite database with:

- `images.width` and `images.height` for header-first dimensions
- `image_search_fts` FTS5 table for indexed filename/folder/path search, with
  LIKE fallback for substring compatibility

Incremental scan compares size/mtime against existing rows, removes deleted
paths, and only upserts changed files. Full scans still replace the root slice
when no prior rows exist.

## Compatibility Rules

- Absolute path remains the primary image id until a stronger file identity
  layer exists.
- Favorites levels stay `1..5`.
- Optional enhancement jobs remain explicit user actions.
- Delete/open behavior should call local OS APIs, not browser APIs.
- Browser and native state should be syncable, but either app must stay usable
  if the other is never opened again.
