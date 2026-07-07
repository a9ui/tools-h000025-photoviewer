# Local Native M14 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M14 implements the smallest native date/seen parity advance after M13:
seen/unseen gallery-state persistence. It does not claim full native parity.

Decision: `NATIVE_SEEN_STATE_SLICE_ADDED_WITH_DATE_SECTIONS_DEFERRED`.

Meaning:

- Normal native UI still uses WinForms directly.
- The implementation does not use Node, Next.js, Chrome/browser runtime,
  React/DOM, HTTP image/API routes, Blob/Object URL image loading, webview
  wrapping, or a local server.
- The browser app remains preserved; M14 does not require `src/**` changes.
- Browser behavior evidence:
  - `src/store/ImageContext.tsx` reads and writes `pvu_seen_images`.
  - `src/components/ImageGrid.tsx` calls `markImageSeen` on grid/list click and
    when opening detail, and adds the `is-unseen` class for images absent from
    the seen map.
  - `src/app/globals.css` renders `is-unseen` as a small marker.
- Native behavior:
  - explicit `pvu_seen_images` browser-state export keys import into SQLite
    `seen_images`;
  - native image queries return `IsSeen`;
  - unseen native rows show a minimal `NEW` prefix in the existing list/grid
    item text;
  - selecting a native preview image marks it seen in SQLite.
- Date section UI, date filter controls, folder range selection, drag/open
  parity, placeholder behavior, native thumbnail warmup UI, preview tabs, bulk
  destructive actions, explicit enhancement UI, malformed export UX, and full
  browser API/error equivalents stay `DEFERRED` in
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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke
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
- `-PrepareFixture` passed:
  - images: 4
  - extraImages: 2
  - largeScrollImages: 240
  - browser export: `.cache\native\browser-localstorage-export.json`
- `-HeadlessImport` passed:
  - favorites: 1
  - albums: 2
  - albumImages: 4
  - browserStateKeys: 5
  - seenImages: 3
  - settings: 14
  - images: 4
- `-HeadlessScan` passed:
  - images: 4
  - favorites: 1
  - importedFavorites: 1
- `-HeadlessIncrementalScan` passed:
  - images: 4
  - addedOrUpdated: 0
  - removed: 0
  - unchanged: 4
- `-HeadlessSearch -Search fixture` passed:
  - matches: 4
  - indexed: true
- `-HeadlessSearch -Search fixture -FavoritesOnly` passed:
  - matches: 1
  - indexed: true
- `-HeadlessPerf` passed:
  - scanMs: 4
  - searchP50/searchP95 indexed for 20/20 iterations
  - cacheHitRate: 95.0%
  - headerCoverage: 100.0%
  - mutation added/updated/removed: 1/1/1
  - watcherEvents: 3
- `-HeadlessCacheCompat` passed:
  - images: 4
  - thumb warm/reused/missing: 1/2/1
  - display warm/reused/missing: 1/3/0
- `-HeadlessUiSmoke` passed:
  - scannedImages: 4
  - initialVisible: 4
  - searchMatches: 4
  - favoriteMatches: 1
  - galleryStateRestore: true
  - folder controls, detail modal, settings import, no-results, folder-error,
    and enhancement-state checks: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
- `-HeadlessFolderSetSmoke` passed:
  - roots: 2
  - removedRoots: 1
  - folderBuckets: 4
  - imagesBeforeRemove/imagesAfterRemove: 6/2
  - recentSetPersisted/removeFolder/manualRefresh/watcher/enhancement checks:
    true
- `-HeadlessLargeScrollSmoke` passed:
  - totalImages: 240
  - targetIndex/restoredIndex: 180/180
  - topIndexBefore/topIndexAfter: 177/177
  - virtualMode: true
  - statePersisted/restoreSelected/ensureVisible/visibleBeforeRestore: true
  - enhancementStateUnchanged: true
- Native seen-state smoke passed:
  - importedSeen: true
  - nativeInitiallyUnseen: true
  - nativeSeenPersisted: true
  - importedStillSeen: true
  - seenSmokeImages: 2
  - totalSeenImages: 14 on final rerun; this is the cumulative native DB
    seen-row count, not the per-smoke fixture count
  - enhancementStateUnchanged: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
- `git diff --name-only -- src` was empty.

## Browser Baseline Results

- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed:
  - test files: 16 passed
  - tests: 94 passed
- `corepack pnpm test:e2e` passed:
  - Chromium tests: 2 passed
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full`
  passed:
  - unit: passed
  - lint: passed with the two existing `CachedImage.tsx` `<img>` warnings
  - audit: passed
  - typecheck: passed
  - build: passed
  - e2e: passed

## Native Rows Advanced By M14 First Slice

M14 adds native acceptance evidence for:

- explicit browser `pvu_seen_images` import into native SQLite;
- native `seen_images` persistence independent of browser runtime;
- visible native unseen marker through the existing list/grid item text;
- native selected-preview image becoming seen;
- native scan/search image rows reading the same seen state;
- passive seen-state smoke not changing enhancement job state.

Still deferred:

- date sections and date filter controls;
- folder range selection, pending custom control or revised product/UI
  semantics;
- drag/open parity, placeholder behavior, and native thumbnail warmup UI;
- preview tabs, pin/unpin, close/restore closed tab, and hover/quick preview;
- bulk favorite/open/recycle confirmation and destructive delete UI tests;
- explicit enhancement queue/settings/cancel/retry/open/delete output/source
  UI and enhanced-only filter;
- malformed browser-state export UX;
- full browser API/error equivalents;
- native desktop screenshot polish.
