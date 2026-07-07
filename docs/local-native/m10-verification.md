# Local Native M10 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M10 implements a native selection, filters, and folder visibility parity slice.
It does not claim full native parity.

Decision: `NATIVE_SELECTION_FILTER_FOLDER_SLICE_ADDED_WITH_REMAINING_GAPS_DEFERRED`.

Meaning:

- Normal native UI still uses WinForms directly.
- The implementation does not use Node, Next.js, Chrome/browser runtime,
  React/DOM, HTTP image/API routes, Blob/Object URL image loading, webview
  wrapping, or a local server.
- The browser app remains preserved; M10 does not require `src/**` changes.
- Rows that still need multi-root scan/search/watch behavior, folder range
  selection, preview tabs, bulk destructive actions, explicit enhancement UI,
  screenshot polish, or explicit product decisions stay `DEFERRED` in
  `tasks/local-native-m5/browser-regression-matrix.md`.

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture -FavoritesOnly
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
```

Browser-baseline and full project verification:

```powershell
corepack pnpm typecheck
corepack pnpm test:unit
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full
```

## Current Native Results

- Native build passed with 0 warnings and 0 errors.
- Fixture preparation passed:
  - images: 4
  - created state: none in the final run
  - skipped existing state:
    `.cache\favorites.json`, `.cache\albums.json`, `.cache\settings.json`,
    `.cache\native\browser-localstorage-export.json`
  - thumbnail compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- Headless import passed:
  - favorites: 1
  - albums: 2
  - album image membership rows: 3
  - explicit browser `pvu_*` state keys: 5
  - native settings rows after import: 21
  - images at import time: 4
- Headless scan passed:
  - images: 4
  - stored favorites: 1
  - imported favorites: 1
  - elapsed: 4 ms
- Headless incremental scan passed:
  - images: 4
  - addedOrUpdated: 0
  - removed: 0
  - unchanged: 4
  - elapsed: 4 ms
- Headless indexed search passed:
  - query `fixture`: 4 matches, `indexed=true`
  - query `fixture` with favorites only: 1 match, `indexed=true`
- Headless performance passed:
  - scan: 4 ms
  - search p50: 0.17 ms
  - search p95: 0.37 ms
  - indexed samples: 20 / 20
  - navigation p50: 0.00 ms
  - navigation p95: 15.75 ms
  - cache hit rate: 95.0%
  - header coverage: 100.0%
  - mutation probe: added 1, updated 1, removed 1
  - watcher events observed: 3
- Headless cache compatibility passed:
  - images checked: 4
  - thumbnail compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- Native WinForms UI smoke passed:
  - runtime: `winforms`
  - scannedImages: 4
  - initialVisible: 4
  - previewLoaded: true
  - navigationButtons: true
  - keyboardNavigation: true
  - keyboardFavorite: true
  - gridToggle: true
  - folderBuckets: 2
  - folderHideAll: true
  - folderShowSelected: true
  - folderHideSelected: true
  - folderClearSelection: true
  - sortName: true
  - randomReshuffle: true
  - thumbnailSize: true
  - previewToggle: true
  - detailsToggle: true
  - previewSplitter: true
  - selectedCount: true
  - multiSelection: true
  - backgroundClear: true
  - favoriteFilterCounts: true
  - favoriteLevelFilter: true
  - unratedFilter: true
  - clearSearch: true
  - detailModal: true
  - detailNavigation: true
  - detailZoom: true
  - detailReset: true
  - detailPan: true
  - detailFlip: true
  - detailFavorite: true
  - detailOpenExternal: true
  - settingsReadOnly: true
  - searchMatches: 4
  - favoriteMatches: 1
  - noResultsState: true
  - folderErrorState: true
  - albums: 2
  - albumImages: 3
  - browserStateKeys: 5
  - settingsImported: true
  - enhancementStateUnchanged: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
- `git diff --name-only -- src` returned no files.

## Browser Baseline And Full Verify Results

- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `corepack pnpm test:e2e` passed: 2 Chromium tests.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full`
  passed:
  - required files
  - `pnpm test:unit`
  - `pnpm lint`
  - `pnpm audit --audit-level moderate`
  - `pnpm typecheck`
  - `pnpm build`
  - `pnpm test:e2e`
- `pnpm lint` reported 0 errors and 2 existing `<img>` warnings in
  `src/components/CachedImage.tsx`.
- Browser E2E is recorded as browser-baseline preservation evidence only. It
  does not satisfy native parity by itself.

## Native Rows Advanced By M10 First Slice

M10 adds native acceptance evidence for:

- nested folder fixture and two folder buckets;
- selected folder bucket show/hide/clear controls;
- ListView multi-selection and background selection clear;
- selected-count label with multi-selection;
- search clear button;
- favorite filter count labels for all/favorites/unrated/levels 1-5;
- favorite-level and unrated native filters;
- persisted native `favorite_filter` setting;
- ordinary native browsing/search/filter/preview/detail smoke not changing
  enhancement job state.

Still deferred:

- multi-root folder sets, remove-folder behavior, open recent folder set,
  manual refresh, folder range selection, date sections, scroll memory,
  seen/unseen state, preview tabs, hover/quick preview, bulk destructive
  actions, explicit enhancement UI/enhanced-only filter, full browser API error
  equivalents, full `pvu_*` persistence parity, and native desktop screenshot
  polish.
