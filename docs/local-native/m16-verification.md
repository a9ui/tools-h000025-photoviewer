# Local Native M16 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M16 implements the smallest native date-section advance after M15: Created-sort
date header markers in the native list view. It does not claim full native
parity.

Decision: `NATIVE_CREATED_LIST_DATE_HEADERS_ADDED_WITH_GRID_DATE_SECTIONS_DEFERRED`.

Meaning:

- Normal native UI still uses WinForms directly.
- The implementation does not use Node, Next.js, Chrome/browser runtime,
  React/DOM, HTTP image/API routes, Blob/Object URL image loading, webview
  wrapping, or a local server.
- The browser app remains preserved; M16 does not require `src/**` changes.
- Browser behavior evidence:
  - `src/lib/dateSectionLayout.ts` groups by local `createdAt` day and formats
    section headers as `M月D日`;
  - `src/components/ImageGrid.tsx` enables date separators only for created
    date sort modes;
  - browser fallback list/grid separators mark the first item for each date
    when full section layout is unavailable.
- Native behavior:
  - Created-sort native list view marks the first visible image for each local
    `CreatedAtUtc` date with a browser-mapped `M月D日` header label;
  - the header map is rebuilt from the filtered visible list, so it follows
    date presets, search, favorite, folder, and sort changes;
  - native grid date headers are intentionally suppressed in this slice;
  - `-HeadlessDateSectionSmoke` verifies the header labels, Created sort
    order, Today-filter regrouping, grid suppression, and enhancement
    isolation on relative-date fixture images.
- Native grid date section layout, manual date range inputs, folder range
  selection, drag/open parity, placeholder behavior, native thumbnail warmup
  UI, preview tabs, bulk destructive actions, explicit enhancement UI,
  malformed export UX, and full browser API/error equivalents stay `DEFERRED`
  in `tasks/local-native-m5/browser-regression-matrix.md`.

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
  - created state:
    `.cache\favorites.json`, `.cache\albums.json`, `.cache\settings.json`,
    `.cache\native\browser-localstorage-export.json`
  - skipped existing state: none in this M16 worktree
  - thumbnail compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- `-HeadlessImport` passed:
  - favorites: 1
  - albums: 2
  - albumImages: 4
  - browserStateKeys: 5
  - seenImages: 2
  - settings: 23
  - images: 32 cumulative native DB rows at that point; fixture scan results
    below remain 4 images for the active fixture
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
  - searchP50/searchP95: 0.19 ms / 0.49 ms
  - indexed samples: 20 / 20
  - navigationP50/navigationP95: 0.00 ms / 16.10 ms
  - cacheHitRate: 95.0%
  - headerCoverage: 100.0%
  - mutation added/updated/removed: 1 / 1 / 1
  - watcherEvents: 3
- `-HeadlessCacheCompat` passed:
  - images: 4
  - thumb compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- `-HeadlessUiSmoke` passed:
  - scannedImages: 4
  - initialVisible: 4
  - searchMatches: 4
  - favoriteMatches: 1
  - folder controls, detail modal, settings import, gallery restore,
    no-results, folder-error, and enhancement-state checks: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
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
  - totalSeenImages: 11 on final rerun; this is the cumulative native DB
    seen-row count, not the per-smoke fixture count
  - enhancementStateUnchanged: true
- Native date-filter smoke passed:
  - totalImages: 4
  - initialVisible: 4
  - todayMatches: 1
  - last7Matches: 2
  - last30Matches: 3
  - thisYearMatches: 3
  - clearMatches: 4
  - todayFilter / last7Filter / last30Filter / thisYearFilter / clearFilter:
    true
  - dateFilterPersisted: true
  - enhancementStateUnchanged: true
- Native date-section smoke passed:
  - totalImages: 4
  - initialVisible: 4
  - headerGroups: 4
  - firstHeader: `7月8日`
  - showDateHeaders: true
  - firstItemGrouped: true
  - createdSortOrder: true
  - filteredGroups: 1
  - todaySingleGroup: true
  - gridGroupsSuppressed: true
  - enhancementStateUnchanged: true
  - browserRuntime/localHttpServer/nodeRuntime: false/false/false
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

## Native Rows Advanced By M16 First Slice

M16 adds native acceptance evidence for:

- browser-mapped `M月D日` date header labels in native Created-sort list view;
- first visible item per local created date carrying the header marker;
- date headers following the native Today filter;
- native grid date headers staying intentionally suppressed for this slice;
- passive date-section smoke not changing enhancement job state.

Still deferred:

- native grid date section layout/header behavior;
- manual date range inputs;
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
