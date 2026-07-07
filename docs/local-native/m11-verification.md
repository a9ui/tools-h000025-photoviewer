# Local Native M11 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M11 implements a native multi-root folder-set parity slice. It does not claim
full native parity.

Decision: `NATIVE_MULTI_ROOT_FOLDER_SET_SLICE_ADDED_WITH_RANGE_SELECTION_DEFERRED`.

Meaning:

- Normal native UI still uses WinForms directly.
- The implementation does not use Node, Next.js, Chrome/browser runtime,
  React/DOM, HTTP image/API routes, Blob/Object URL image loading, webview
  wrapping, or a local server.
- The browser app remains preserved; M11 does not require `src/**` changes.
- Folder range selection is explicitly deferred for product/UI decision before
  implementation.
- Rows that still need preview tabs, date sections, scroll memory, seen/unseen
  state, bulk destructive actions, explicit enhancement UI, malformed export
  UX, full browser API/error equivalents, or screenshot polish stay
  `DEFERRED` in `tasks/local-native-m5/browser-regression-matrix.md`.

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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture
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
  - extraFolder: `.cache\native-fixture-extra`
  - extraImages: 2
  - created state:
    `.cache\favorites.json`, `.cache\albums.json`, `.cache\settings.json`,
    `.cache\native\browser-localstorage-export.json`
  - thumbnail compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- Headless import passed:
  - favorites: 1
  - albums: 2
  - album image membership rows: 4
  - explicit browser `pvu_*` state keys: 5
  - native settings rows after import: 22
  - images at import time: 6
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
  - search p95: 0.54 ms
  - indexed samples: 20 / 20
  - navigation p50: 0.00 ms
  - navigation p95: 11.52 ms
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
  - folderBuckets: 2
  - multiSelection: true
  - backgroundClear: true
  - favoriteFilterCounts: true
  - favoriteLevelFilter: true
  - unratedFilter: true
  - clearSearch: true
  - detailModal/detailNavigation/detailZoom/detailReset/detailPan/detailFlip:
    true
  - settingsReadOnly: true
  - searchMatches: 4
  - favoriteMatches: 1
  - noResultsState: true
  - folderErrorState: true
  - albums: 2
  - albumImages: 4
  - browserStateKeys: 5
  - settingsImported: true
  - enhancementStateUnchanged: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
- Native WinForms folder-set smoke passed:
  - runtime: `winforms`
  - roots: 2
  - removedRoots: 1
  - folderBuckets: 4
  - imagesBeforeRemove: 6
  - imagesAfterRemove: 2
  - searchMatches: 6
  - recentSetPersisted: true
  - removeFolder: true
  - openRecentSet: true
  - manualRefreshAdded: true
  - manualRefreshRemoved: true
  - watcherRoots: true
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

## Native Rows Advanced By M11 First Slice

M11 adds native acceptance evidence for:

- multi-root folder-set scan across two fixture roots;
- indexed search across the active folder set;
- FileSystemWatcher coverage for all active roots;
- active-set remove-folder behavior;
- persisted `recent_folder_set` and open recent folder set behavior;
- explicit manual refresh adding and removing a disposable probe image;
- multi-root folder bucket construction;
- passive native folder-set scan/search/preview/detail smoke not changing
  enhancement job state.

Still deferred:

- folder range selection, by explicit M11 product decision;
- preview tabs, pin/unpin, close/restore closed tab, and hover/quick preview;
- date sections, scroll memory, seen/unseen state, large-fixture virtualized
  scroll state, drag/open parity, placeholder behavior, and native thumbnail
  warmup UI;
- bulk favorite/open/recycle confirmation and destructive delete UI tests;
- explicit enhancement queue/settings/cancel/retry/open/delete output/source
  UI and enhanced-only filter;
- malformed browser-state export UX;
- full browser API/error equivalents;
- native desktop screenshot polish.
