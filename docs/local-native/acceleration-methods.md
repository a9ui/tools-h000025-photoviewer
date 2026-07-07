# Local Native Acceleration Methods

This document records concrete speedups available when PhotoViewer runs as a
local desktop app while the current browser version remains intact.

## Confirmed Current Costs In H000025

- Image display goes through `/api/image`, then browser `fetch`, `Blob`, and
  object URL management in `src/lib/clientImageCache.ts`.
- Thumbnail and display variants are generated through Sharp in
  `src/lib/thumbnailCache.ts`, then served through the Next route.
- The main scan cache is large JSON under `.cache/index_*.json`, loaded and
  saved as whole files by `src/lib/indexer.ts`.
- Favorites are a JSON map in `.cache/favorites.json`; browser state also
  writes `pvu_*` keys into `localStorage`.
- The grid is already virtualized, but it still pays React/browser layout,
  hydration, fetch, and image element costs.

## Native Speedup Inventory

1. Remove the local HTTP image route for native viewing and decode files by
   absolute path.
2. Remove browser `fetch`/`Blob`/`URL.createObjectURL` from the preview path.
3. Stream folder enumeration directly into the UI instead of waiting for a
   complete scan response.
4. Use a native virtual list/grid so large libraries do not create one DOM node
   per image.
5. Keep metadata extraction off the first visible path; show path/stat rows
   first, then enrich later.
6. Replace whole-file JSON indexes with SQLite or a binary append/update store.
7. Store favorites as indexed rows rather than rewriting a multi-megabyte JSON
   object on every meaningful update.
8. Store albums as relations keyed by absolute path/image id.
9. Use `FileSystemWatcher` for known folders so unchanged scans become cheap
   refreshes.
10. Use file IDs or normalized absolute paths to preserve identity across state
    imports.
11. Decode the selected image on a worker thread and only publish the newest
    selection back to the UI.
12. Keep a small modal ring buffer for previous/current/next decoded images.
13. Use embedded image dimensions and headers for layout before full decode.
14. Prefer source display for first preview; generate thumbnails only for grid
    density or repeated navigation.
15. Split caches into hot preview, warm viewport, and cold archive tiers.
16. Store thumbnail cache metadata in SQLite so cache hits avoid repeated
    source stat calls.
17. Batch UI state writes and scroll memory writes.
18. Push scan progress into the UI instead of polling API endpoints.
19. Use native recycle-bin APIs for delete instead of an HTTP route.
20. Keep enhancement workers fully explicit and outside the scan/preview
    scheduler.
21. Use one priority scheduler across scan, preview, thumbnail, and metadata
    work.
22. Use fixed-size viewport jobs so offscreen cache work cannot starve visible
    items.
23. Keep stable sorted key arrays for search and favorites filters.
24. Use incremental search tables or token indexes instead of repeated JS
    string scans over large arrays.
25. Persist a startup snapshot of last folder and visible rows.
26. Load `.cache/favorites.json`, `.cache/albums.json`, and `.cache/settings.json`
    once into native state and then write through the native store.
27. Import browser-only `pvu_*` state through an explicit export/import step
    instead of depending on Chrome origin storage.
28. Use native image surfaces for panning/zooming instead of browser image
    layout.
29. Avoid Next.js build/dev server startup for the native executable path.
30. Keep H000025 browser code and local native code in the same repo so UI
    behavior and migration rules can be compared without losing context.

## First Implementation Slice

The first local-native implementation slice covers items 1, 2, 3, 4, 6, 7, 11,
and 26 for favorites and scan rows. It deliberately does not create a new
thumbnail pipeline yet.
