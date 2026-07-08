# Local Native M19 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M19 implements the smallest native enhancement-filter advance after M18:
enhanced-only filtering mapped to existing browser semantics for succeeded
enhancement jobs with output paths. It does not claim full native parity.

Decision: `NATIVE_ENHANCED_ONLY_FILTER_ADDED_FROM_EXISTING_SUCCEEDED_JOB_STATE`.

Meaning:

- Normal native UI still uses WinForms directly.
- The implementation does not use Node, Next.js, Chrome/browser runtime,
  React/DOM, HTTP image/API routes, Blob/Object URL image loading, webview
  wrapping, or a local server.
- The browser app remains preserved; M19 does not require `src/**` changes.
- Browser behavior evidence:
  - `src/components/Sidebar.tsx` exposes the `Enhanced only` checkbox;
  - `src/store/ImageContext.tsx` persists `pvu_enhanced_only` and refreshes
    enhancement state through `GET /api/enhance/jobs`;
  - `src/components/ImageGrid.tsx` filters images by `enhancedSourceIds`;
  - `src/app/api/enhance/jobs/route.ts` only creates jobs on `POST`; M19
    native code does not call that path or start a worker.
- Native behavior:
  - the native toolbar now includes an `Enhanced` checkbox;
  - native reads `.cache/enhance/jobs.json` read-only and matches only
    `status=succeeded` jobs with non-empty `outputPath`;
  - native matches `sourceId` and `sourcePath` against absolute native image
    paths;
  - the filter composes with search and favorites-only filters;
  - native persists `enhanced_only_filter`;
  - fixture preparation creates minimal enhancement fixture state only when
    no `.cache/enhance/jobs.json` exists;
  - the enhanced-filter smoke verifies that filtering does not mutate
    enhancement state.
- Folder range selection, explicit enhancement queue/settings/cancel/retry/open
  output/source UI, original/enhanced preview toggle, drag/open parity,
  placeholder behavior, native thumbnail warmup UI, preview tabs, bulk
  destructive actions, full browser API/error equivalents, and native desktop
  screenshot polish stay `DEFERRED` in
  `tasks/local-native-m5/browser-regression-matrix.md`.

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessEnhancedFilterSmoke -Folder .\.cache\native-fixture
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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateFilterSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateSectionSmoke
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
  - created enhancement state:
    `.cache\enhance\outputs\native-fixture\m2-fixture-1__enhanced.webp`,
    `.cache\enhance\jobs.json`
  - fixture preparation does not overwrite existing `.cache\enhance\jobs.json`.
- Native enhanced-filter smoke passed:
  - totalImages: 4
  - initialVisible: 4
  - enhancedSources: 1
  - enhancedMatches: 1
  - enhancedSearchMatches: 1
  - enhancedFavoriteMatches: 1
  - clearMatches: 4
  - enhancedOnlyFilter: true
  - enhancedSearchFilter: true
  - enhancedFavoriteFilter: true
  - clearFilter: true
  - enhancedFilterPersisted: true
  - enhancementStateUnchanged: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
- `-HeadlessUiSmoke` passed:
  - scannedImages: 4
  - initialVisible: 4
  - searchMatches: 4
  - favoriteMatches: 1
  - enhancedOnlyFilter: true
  - enhancementStateUnchanged: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
- `-HeadlessDateFilterSmoke` passed:
  - totalImages: 4
  - initialVisible: 4
  - todayMatches: 1
  - last7Matches: 2
  - last30Matches: 3
  - thisYearMatches: 3
  - manualRangeMatches: 2
  - manualFromOnlyMatches: 2
  - manualToOnlyMatches: 2
  - manualSearchMatches: 2
  - manualFavoriteMatches: 1
  - clearMatches: 4
  - enhancementStateUnchanged: true
- `-HeadlessDateSectionSmoke` passed:
  - totalImages: 4
  - initialVisible: 4
  - headerGroups: 4
  - gridHeaderGroups: 4
  - manualRangeHeaderGroups: 2
  - manualRangeGridHeaderGroups: 2
  - enhancementStateUnchanged: true
- `-HeadlessImport` passed:
  - favorites: 1
  - albums: 2
  - albumImages: 4
  - browserStateKeys: 5
  - seenImages: 9 cumulative native DB rows at that point
  - settings: 29
  - images: 12 cumulative native DB rows at that point; fixture scan results
    below remain 4 images for the active fixture
- `-HeadlessScan` passed:
  - images: 4
  - favorites: 1
  - importedFavorites: 1
  - elapsedMs: 4
- `-HeadlessIncrementalScan` passed:
  - images: 4
  - addedOrUpdated: 0
  - removed: 0
  - unchanged: 4
  - elapsedMs: 4
- `-HeadlessSearch -Search fixture` passed:
  - matches: 4
  - indexed: true
- `-HeadlessSearch -Search fixture -FavoritesOnly` passed:
  - matches: 1
  - indexed: true
- `-HeadlessCacheCompat` passed:
  - images: 4
  - thumb compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- `-HeadlessPerf` passed:
  - scanMs: 4
  - searchP50/searchP95: 0.20 ms / 0.44 ms
  - indexed samples: 20 / 20
  - navigationP50/navigationP95: 0.00 ms / 15.36 ms
  - cacheHitRate: 95.0%
  - headerCoverage: 100.0%
  - mutation added/updated/removed: 1 / 1 / 1
  - watcherEvents: 3
- `-HeadlessFolderSetSmoke` passed:
  - roots: 2
  - removedRoots: 1
  - folderBuckets: 4
  - imagesBeforeRemove/imagesAfterRemove: 6 / 2
  - recentSetPersisted/removeFolder/openRecentSet/manualRefresh/watcher:
    true
  - enhancementStateUnchanged: true
- `-HeadlessLargeScrollSmoke` passed:
  - totalImages: 240
  - targetIndex/restoredIndex: 180 / 180
  - topIndexBefore/topIndexAfter: 177 / 177
  - virtualMode: true
  - statePersisted/restoreSelected/ensureVisible/visibleBeforeRestore: true
  - enhancementStateUnchanged: true
- `-HeadlessSeenSmoke` passed:
  - importedSeen: true
  - nativeInitiallyUnseen: true
  - nativeSeenPersisted: true
  - importedStillSeen: true
  - seenSmokeImages: 2
  - totalSeenImages: 14 cumulative native DB rows
  - enhancementStateUnchanged: true
- `git diff --name-only -- src` returned no files.
- `git diff --check` passed.

## Browser Baseline Results

- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed:
  - test files: 16 passed
  - tests: 94 passed
- `corepack pnpm test:e2e` passed:
  - Chromium tests: 2 passed
  - Node printed the existing `NO_COLOR` / `FORCE_COLOR` warning; it did not
    affect the pass result.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full`
  passed:
  - unit: passed
  - lint: passed with the two existing `CachedImage.tsx` `<img>` warnings
  - audit: passed
  - typecheck: passed
  - build: passed
  - e2e: passed

## Native Rows Advanced By M19 First Slice

M19 adds native acceptance evidence for:

- enhanced-only filtering from existing succeeded enhancement job state;
- read-only `.cache/enhance/jobs.json` parsing;
- composition with search and favorites-only filters;
- persisted native `enhanced_only_filter` setting;
- passive enhanced-only filtering not changing enhancement job state.

Still deferred:

- explicit enhancement queue/settings/cancel/retry/open/delete output/source
  UI;
- original/enhanced preview toggle and broader output management;
- folder range selection, pending custom control or revised product/UI
  semantics;
- richer count labels beyond favorite/date/enhanced state;
- drag/open parity, placeholder behavior, and native thumbnail warmup UI;
- preview tabs, pin/unpin, close/restore closed tab, and hover/quick preview;
- bulk favorite/open/recycle confirmation and destructive delete UI tests;
- malformed browser-state export UX;
- full browser API/error equivalents;
- native desktop screenshot polish.
