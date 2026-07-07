# Local Native State Migration Map

The native lane must preserve H000025 user state without forcing the browser
app to change shape.

## Disk State Already Reusable

| Source | Current Shape | Native Handling |
| --- | --- | --- |
| `.cache/favorites.json` | `Record<absolutePath, level>` | Read immediately; later write through native store and optional JSON sync. |
| `.cache/albums.json` | `{ albums: Album[] }` | Import after native relation store exists. |
| `.cache/settings.json` | app settings JSON | Read key binding/default confirmation settings after native command map exists. |
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

## Compatibility Rules

- Absolute path remains the primary image id until a stronger file identity
  layer exists.
- Favorites levels stay `1..5`.
- Optional enhancement jobs remain explicit user actions.
- Delete/open behavior should call local OS APIs, not browser APIs.
- Browser and native state should be syncable, but either app must stay usable
  if the other is never opened again.
