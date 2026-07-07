# Browser Feature Baseline And Native Parity Matrix

Date: 2026-07-08

## Purpose

Canonical intent source:
`docs/local-native/native-intent-source.md`, from Codex thread
`019f3c2e-577f-7421-a499-145ece67eb30`.

Before the local-native stack is merged or closed as release-candidate ready,
the existing browser PhotoViewer feature set must be checked feature by
feature. This is not a plan to wrap the browser app in a local server. The
release-candidate target is a fast native local app that does not depend on
Node, a browser runtime, or a local HTTP server for normal use.

M5 did not change `src/**`, but the browser app remains the product baseline.
Each browser feature row below needs one of these outcomes before stack merge:

- native equivalent verified,
- intentionally not applicable to native with a concrete reason,
- deferred with owner, risk, and merge decision.

Current automated browser coverage is not enough: `corepack pnpm test:e2e`
currently runs only the landing/folder-memory checks in `e2e/home.spec.ts`.
That proves the browser baseline is alive, not that native parity is done.
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
- Do not treat a webview, browser wrapper, Node server, Next.js dev server, or
  localhost route as satisfying native parity.
- Native verification must use `dotnet`/native headless checks and the native
  UI surface, not browser automation, for the acceptance result.
- Any enhancement test must be triggered by an explicit UI/API action and must
  prove ordinary browsing, modal navigation, and previewing create zero new
  enhancement jobs.

## Required Feature Sweep

| Area | Browser baseline source | Native parity requirement | M5 status |
| --- | --- | --- | --- |
| Landing and folder set | `src/app/page.tsx`, `src/lib/pathSet.ts` | Native folder selection, pasted/typed absolute path intake, multi-root set, remove folder, open last/recent folder set, scan progress, refresh, and full verify path without HTTP routes. | Partially covered by native scan/import commands; full native UI sweep pending. |
| Legacy browser state | `src/app/page.tsx`, `src/lib/localStorageMigration.ts`, `/api/legacy-state` | Native import path for explicit browser-state export and safe fallback for missing/malformed exported keys. No direct Chrome profile scraping required. | Headless explicit export import covered; malformed/error UI pending. |
| Search bar | `src/components/SearchBar.tsx`, `/api/tags`, `/api/search` | Native tag/query entry, multi-token search, clear search, no-results state, FTS indexed search, and substring fallback at native speed. | Headless FTS/fallback covered; native UI controls pending. |
| Sidebar quick search and filters | `src/components/Sidebar.tsx` | Native quick presets, clear, favorites only, unrated only, favorite level 1-5, enhanced only, count labels, date presets, and manual date range. | Favorite import/filter covered; rest pending. |
| Folder visibility | `src/components/Sidebar.tsx`, `/api/folders` | Native folder buckets, loading/empty states, A-Z/Z-A/count sorting, show/hide all, invert, single/multi/range selection, show/hide selected, clear selection. | Pending. |
| Sorting and display | `src/components/Sidebar.tsx`, `src/components/ImageGrid.tsx` | Native modified/created/name/random sorting, reshuffle, grid/list mode, compact/poster-equivalent display, aspect controls, thumbnail sizing, and keyboard/wheel zoom equivalents. | Virtual list/grid shell covered; detailed controls pending. |
| Virtual gallery | `src/components/ImageGrid.tsx`, `src/components/CachedImage.tsx` | Native virtualized large-list browsing, placeholders/loading, date sections, scroll memory, seen/unseen state, drag/open behavior, thumbnail warmup/cache fallback. | Virtual browsing and cache compatibility partially covered; UI sweep pending. |
| Selection and preview tabs | `src/components/ImageGrid.tsx`, `src/components/BottomPreviewTabs.tsx` | Native click selection, Ctrl/Shift selection, background clear, double-click detail, preview tabs, pin/unpin, close/restore closed tab, hover/quick preview equivalent. | Preview ring buffer covered; tabs/selection pending. |
| Right preview panel | `src/components/RightPreviewPanel.tsx` | Native preview panel show/hide, resize persistence, empty state, active preview, favorite +/- actions, open external, details toggle, selected count, bulk favorite/open/recycle confirmation. | Direct preview and open/delete primitives covered; panel UI pending. |
| Modal navigation | `src/components/ImageModal.tsx`, `src/lib/modalNavigation.ts` | Native detail viewer open/close/reveal, previous/next wrap, filtered ordered subset navigation, edge/click/swipe or keyboard equivalents, loading slot. | Previous/current/next ring covered; UI interactions pending. |
| Modal image controls | `src/components/ImageModal.tsx` | Native zoom, reset, pan, horizontal flip, chrome hide/show or immersive equivalent, favorite +/-, feedback, open external. | Pending. |
| Modal metadata | `src/components/ImageModal.tsx`, `src/lib/pngMetadataRows.ts` | Native prompt/negative/settings display, prompt tag actions, copy prompt/negative/PNG info, no-metadata fallback. | Metadata parsing/import partially covered; UI actions pending. |
| Delete flows | `src/app/page.tsx`, `src/components/ImageModal.tsx`, `src/components/RightPreviewPanel.tsx`, `/api/delete` | Native single/bulk Recycle Bin delete with confirmation/cancel, do-not-ask toggle, API/error-equivalent handling, no hard-delete fallback. Use disposable fixture copies. | Recycle primitive covered; UI confirmation paths pending. |
| Settings modal | `src/components/SettingsModal.tsx`, `/api/settings` | Native settings surface for confirm-before-delete, modal edge/navigation behavior where applicable, keybinding persistence/recording, malformed settings fallback. | Settings import covered; UI recording pending. |
| Enhancement isolation | `src/components/EnhanceQueuePanel.tsx`, `src/components/ImageModal.tsx`, `src/components/RightPreviewPanel.tsx`, `/api/enhance/*` | Native explicit-only enhancement entry points, queue view, settings, cancel/retry/open/delete output/source, original/enhanced toggle, enhanced-only filter, and proof passive browsing creates no jobs/workers. | Passive no-worker rule covered by policy and smoke; native enhancement UI pending/deferred decision needed. |
| Browser APIs and errors | `src/app/api/**` | Native equivalents for browse, scan, search, tags, folders, favorites, settings, image/thumb/display, open, delete, legacy-state import, and enhancement job operations. Include invalid path, missing image/cache, malformed request, and no external dependency cases. | Headless scan/search/import/cache covered; error UI pending. |
| Persistence keys | `src/store/ImageContext.tsx`, `src/components/EnhanceQueuePanel.tsx` | Native settings/state migration for `pvu_favorites`, `pvu_view`, pinned previews, filters, scroll/seen state, recent dirs, server legacy marker, and enhancement settings where applicable. | Explicit browser export import covered for 5 keys; full key parity pending. |
| Native responsive/layout parity | `src/app/globals.css` | Native desktop layout must be usable and dense like the browser app, with no overlapping controls and no browser/webview dependency. | Pending. |

## Minimum M6 Verification

M6 must run and record:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

The `pnpm`/browser commands are baseline regression checks for the existing
browser app only. They cannot satisfy native acceptance by themselves.

In addition, M6 must perform either:

- native automated checks covering every matrix row that can be safely
  automated, or
- a recorded native UI/manual sweep with screenshots/logs for rows that cannot
  be safely automated without touching `src/**`.

Rows may be marked `verified`, `blocked`, or `deferred`, but no row may be left
blank before merge. A `blocked` or `deferred` row needs a concrete reason,
owner, and merge decision.

## Evidence Required Per Row

Each completed row should record:

- command, native session, or screenshot/log artifact used,
- fixture path or disposable state used,
- pass/fail result,
- whether the evidence is native acceptance or browser-baseline-only evidence,
- whether it changed any ignored cache/state files,
- confirmation that `git diff --name-only -- src` stayed empty unless an
  approved browser helper was intentionally changed.
