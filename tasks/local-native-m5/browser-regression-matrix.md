# Browser Regression Matrix For Local Native Stack Closure

Date: 2026-07-08

## Purpose

Before the local-native stack is merged or closed as release-candidate ready,
the existing browser PhotoViewer must be checked feature by feature. M5 did not
change `src/**`, but the browser app remains the product baseline, so native
work must not regress it.

Current automated browser coverage is not enough: `corepack pnpm test:e2e`
currently runs only the landing/folder-memory checks in `e2e/home.spec.ts`.
This matrix is a blocking M6 input, not optional follow-up.

## Constraints

- Do not modify `src/**` unless an explicitly approved browser export helper is
  scoped, minimal, and verified.
- Do not use Linear.
- Do not deploy.
- Do not touch H000033.
- Do not start automatic enhancement workers.
- Do not delete `.cache/thumbs`, `.cache/display`, `.cache/enhance`,
  favorites, albums, settings, or native SQLite state.
- Any enhancement test must be triggered by an explicit UI/API action and must
  prove ordinary browsing, modal navigation, and previewing create zero new
  enhancement jobs.

## Required Browser Sweep

| Area | Source surface | Required checks | M5 status |
| --- | --- | --- | --- |
| Landing and folder set | `src/app/page.tsx`, `src/lib/pathSet.ts` | Paste absolute folders, add pasted, remove folder, add folder dialog error path, open folder set, open last folder set, recent folder sets, scan progress and refresh/full verify path. | Partially automated by existing E2E; full sweep pending. |
| Legacy browser state | `src/app/page.tsx`, `src/lib/localStorageMigration.ts`, `/api/legacy-state` | `pvu_recent_dirs`, `pvu_last_dir_set`, legacy import marker, localStorage migration, invalid localStorage JSON fallback. | Pending. |
| Search bar | `src/components/SearchBar.tsx`, `/api/tags`, `/api/search` | Type query, comma/Enter tag commit, autocomplete mouse/keyboard, Backspace chip removal, clear search, drag reorder chips, multi-tag search, empty search results. | Pending. |
| Sidebar quick search and filters | `src/components/Sidebar.tsx` | Quick search presets, clear, favorites only, unrated only, favorite level 1-5, enhanced only, filter count labels, date presets and manual date range. | Pending. |
| Folder visibility | `src/components/Sidebar.tsx`, `/api/folders` | Folder list loading/empty states, A-Z/Z-A/count sorting, show all, hide all, invert, single/multi/range selection, show selected, hide selected, clear selection. | Pending. |
| Sorting and display | `src/components/Sidebar.tsx`, `src/components/ImageGrid.tsx` | Modified/created/name/random sort, reshuffle, grid/list mode, standard/compact/poster style, original/square/portrait aspect, thumbnail size slider, Ctrl/Alt wheel zoom, Ctrl +/-/0. | Pending. |
| Virtual gallery | `src/components/ImageGrid.tsx`, `src/components/CachedImage.tsx` | Initial placeholders, lazy page loading, date section headers, scroll memory, seen/unseen state, drag image data, thumbnail warmup requests, full-size display fallback. | Pending. |
| Selection and preview tabs | `src/components/ImageGrid.tsx`, `src/components/BottomPreviewTabs.tsx` | Click select, Ctrl toggle, Shift range, clear selection by background click, double-click modal open, preview tab open, pin/unpin, close tab, restore closed tab with Ctrl+Shift+T, hover thumb. | Pending. |
| Right preview panel | `src/components/RightPreviewPanel.tsx` | Show/hide panel, resize persistence, empty state, active preview rendering, favorite +/- actions, open external, show/hide details, selected count, bulk favorite/open/recycle confirmation. | Pending. |
| Modal navigation | `src/components/ImageModal.tsx`, `src/lib/modalNavigation.ts` | Open/close/reveal to grid, previous/next wrap, modal ordered subset under filters, edge-zone click behavior, image/empty click behavior, swipe, keyboard next/prev/close, loading slot. | Pending. |
| Modal image controls | `src/components/ImageModal.tsx` | Wheel zoom, reset zoom, pan while zoomed, horizontal flip, chrome hide/show, favorite +/-, hidden chrome favorite feedback, open external. | Pending. |
| Modal metadata | `src/components/ImageModal.tsx`, `src/lib/pngMetadataRows.ts` | Prompt/negative/settings tabs, prompt tag list, add prompt tag to search, copy prompt/negative/PNG info, no-metadata fallback. | Pending. |
| Delete flows | `src/app/page.tsx`, `src/components/ImageModal.tsx`, `src/components/RightPreviewPanel.tsx`, `/api/delete` | Single delete confirmation/cancel, do-not-ask toggle, bulk delete confirmation/cancel, API failure handling, Recycle Bin behavior only. Use disposable fixture copies. | Pending. |
| Settings modal | `src/components/SettingsModal.tsx`, `/api/settings` | Open/close/backdrop, confirm-before-delete persistence, modal edge navigation zone slider, every keybinding recording path, malformed settings fallback. | Pending. |
| Enhancement isolation | `src/components/EnhanceQueuePanel.tsx`, `src/components/ImageModal.tsx`, `src/components/RightPreviewPanel.tsx`, `/api/enhance/*` | Queue panel show/hide/refresh, settings persistence, adapter/model/scale/format/sliders, explicit enhance from modal/preview/bulk, large-job confirmation, cancel, retry, open output, delete output, source open, original/enhanced toggle, enhanced-only filter. | Pending; must also prove passive browsing creates no jobs. |
| APIs and errors | `src/app/api/**` | Browse, scan, search, tags, folders, favorites, settings, image, thumbs warm, open, delete, legacy-state, enhance isolation/jobs/output/presets. Include invalid path, missing image, malformed request, and no external dependency cases. | Pending. |
| Persistence keys | `src/store/ImageContext.tsx`, `src/components/EnhanceQueuePanel.tsx` | `pvu_favorites`, `pvu_favorites_backup`, `pvu_view`, `pvu_pinned_tabs`, `pvu_perf_enabled`, `pvu_fav_only`, `pvu_unfav_only`, `pvu_enhanced_only`, `pvu_scroll_memory`, `pvu_seen_images`, `pvu_recent_dirs`, `pvu_last_dir_set`, `pvu_server_legacy_imported`, `pvu_enhance_settings`. | Pending. |
| Responsive/browser smoke | `src/app/globals.css` | Desktop and narrow viewport layout, no overlapping controls, modal usable at narrow width, right panel hidden behavior, queue panel position, no console errors beyond known warnings. | Pending. |

## Minimum M6 Browser Verification

M6 must run and record:

```powershell
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

In addition, M6 must perform either:

- expanded Playwright E2E tests covering every matrix row that can be safely
  automated, or
- a recorded Browser/manual sweep with screenshots/logs for rows that cannot be
  safely automated without touching `src/**`.

Rows may be marked `verified`, `blocked`, or `deferred`, but no row may be left
blank before merge. A `blocked` or `deferred` row needs a concrete reason,
owner, and merge decision.

## Evidence Required Per Row

Each completed row should record:

- command, Browser session, or screenshot/log artifact used,
- fixture path or disposable state used,
- pass/fail result,
- whether it changed any ignored cache/state files,
- confirmation that `git diff --name-only -- src` stayed empty unless an
  approved browser helper was intentionally changed.
