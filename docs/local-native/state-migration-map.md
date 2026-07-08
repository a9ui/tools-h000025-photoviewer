# Local Native State Migration Map

The native lane must preserve H000025 user state without forcing the browser
app to change shape.

## Disk State Already Reusable

| Source | Current Shape | Native Handling |
| --- | --- | --- |
| `.cache/favorites.json` | `Record<absolutePath, level>` | Read immediately; later write through native store and optional JSON sync. |
| `.cache/albums.json` | `{ albums: Album[] }` | M4 imports album id/name/image-count summary rows into SQLite `albums` and full membership into `album_images`. |
| `.cache/settings.json` | app settings JSON | M2 stores browser settings presence/raw JSON marker and native key-binding defaults in SQLite `native_settings`. |
| `.cache/index_*.json` | per-root scan cache | Treat as optional seed data; do not rely on whole-file JSON long term. |
| `.cache/thumbs` | WebP thumbnails | M4 checks the browser key formula and WebP header compatibility before native reuse. It does not delete or regenerate assets. |
| `.cache/display` | WebP display variants | M4 checks the browser display key formula and WebP header compatibility before native reuse. It does not delete or regenerate assets. |

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

Native import uses an explicit export file such as
`.cache/native/browser-localstorage-export.json`, or a path supplied to the
headless import command. Do not read Chrome profile storage directly.

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

## M4 Native Store

M4 extends the same SQLite database with:

- `album_images` for full album-to-image membership imported from
  `.cache/albums.json`
- `browser_state` for explicit `pvu_*` localStorage export imports
- `cache_compatibility` for measured thumbnail/display cache-key checks

Browser `pvu_*` state is imported only from an explicit JSON export file. The
native app records raw imported keys under `browser_state` and mirrors them as
`native_settings` entries prefixed with `browser_`.

## Post-v1 #117 Native Store

#117 starts the full `pvu_*` migration as a key-by-key post-v1 lane. The first
accepted row maps explicit browser `pvu_view.viewMode` into native
`native_settings.view_mode`; the next accepted row maps explicit browser
`pvu_enhanced_only` into native `native_settings.enhanced_only_filter`.
The third accepted row maps explicit browser `pvu_fav_only` /
`pvu_unfav_only` into native `native_settings.favorite_filter` as
`favorites`, `unrated`, or `all`. The fourth accepted row maps explicit
browser `pvu_view.dateFrom` / `dateTo` into native
`native_settings.date_filter`, `native_settings.date_from`, and
`native_settings.date_to`. The fifth accepted row maps explicit browser
`pvu_last_dir_set` / `pvu_recent_dirs` into
`native_settings.recent_folder_set` and `native_settings.recent_folder`.
The sixth accepted row maps explicit browser `pvu_view.rightPanelOpen` /
`rightPanelWidth` into native
`native_settings.preview_visible` and
`native_settings.preview_splitter_distance`. These migrations write only when
the target native state does not exist yet. This gives first-import continuity
without clobbering later native user choices on every startup or Import action.
The seventh accepted row maps explicit browser `pvu_view.thumbSize` into
`native_settings.thumbnail_size`, clamped to the current native UI range of
64-192. These migrations write only when the target native state does not exist
yet. This gives first-import continuity without clobbering later native user
choices on every startup or Import action.
The eighth accepted row maps explicit browser `pvu_view.sortBy` into
`native_settings.sort_mode` only for browser values with matching native sort
semantics: `newest` to `Modified`, `created-newest` to `Created`, `name` to
`Name`, and `random` to `Random`. Browser ascending directions
`oldest` / `created-oldest` and browser `randomSeed` remain deferred because
the current native sort surface does not persist equivalent direction or seed
state.

The dedicated smoke is:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke
```

Thumbnail/display reuse is read-only until compatibility is proven. The native
check uses the browser formula from `src/lib/thumbnailCache.ts`:

- thumbnail key: `base64url(absolutePath + "|" + sourceMtimeMs)`
- display key: `base64url(absolutePath + "|" + sourceMtimeMs + "|display:2200")`

The native check classifies compatible, missing, and incompatible cache files
and records the counts; it does not create, rewrite, or delete cache assets.

## Compatibility Rules

- Absolute path remains the primary image id until a stronger file identity
  layer exists.
- Favorites levels stay `1..5`.
- Optional enhancement jobs remain explicit user actions.
- Delete/open behavior should call local OS APIs, not browser APIs.
- Browser and native state should be syncable, but either app must stay usable
  if the other is never opened again.
