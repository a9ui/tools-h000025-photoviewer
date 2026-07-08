# Local Native M20 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M20 implements the smallest native filter-count-label advance after M19:
the browser sidebar result-count label maps into native as `indexed` or
`filtered / indexed` when the existing browser behavior and existing native
state line up cleanly. It does not claim full native parity.

Decision: `NATIVE_BROWSER_MAPPED_RESULT_COUNT_LABEL_ADDED_WITH_BROADER_COUNT_LABELS_DEFERRED`.

Meaning:

- Normal native UI still uses WinForms directly.
- The implementation does not use Node, Next.js, Chrome/browser runtime,
  React/DOM, HTTP image/API routes, Blob/Object URL image loading, webview
  wrapping, or a local server.
- The browser app remains preserved; M20 does not require `src/**` changes.
- Browser behavior evidence:
  - `src/lib/viewerUi.ts` renders `<count> indexed` when no search/date/hidden
    folder count filter is active;
  - `src/lib/viewerUi.ts` renders `<visible> filtered / <indexed> indexed`
    when search text, date filters, or hidden folders are active;
  - `src/components/Sidebar.tsx` renders that label as sidebar metadata;
  - `src/lib/viewerUi.test.ts` covers unfiltered, search-filtered, and
    date-filtered labels.
- Native behavior:
  - the existing native top status label now renders `<count> indexed` or
    `<visible> filtered / <indexed> indexed`;
  - the label is derived from existing `_allImages`, `_visibleImages`, search
    text, date range, and folder bucket visibility;
  - `-HeadlessUiSmoke` verifies indexed, hidden-folder-filtered, restored, and
    search-filtered labels;
  - `-HeadlessDateFilterSmoke` verifies Today date-filtered and cleared labels.
- Search tag suggestion counts, richer per-filter counts beyond the browser
  result-count label, folder range selection, explicit enhancement
  queue/settings/cancel/retry/open/delete output/source UI,
  original/enhanced preview toggle, drag/open parity, placeholder behavior,
  native thumbnail warmup UI, preview tabs, bulk destructive actions, full
  browser API/error equivalents, and native desktop screenshot polish stay
  `DEFERRED` in `tasks/local-native-m5/browser-regression-matrix.md`.

## Initial Live State

- `origin/main` is M19 merge commit
  `dfcdb12fa5fb23254b6d0d8589952b713b15a530`.
- PR #94 is merged at
  `dfcdb12fa5fb23254b6d0d8589952b713b15a530`.
- PR CI run #28911783114 passed.
- Final main CI run #28911883489 passed.
- Issue #93 and milestone #24 are closed.
- Issue #95 and milestone #25 are open.
- SQLite job 231 is `merged`.
- SQLite job 232 is `queued` and references Codex thread
  `019f3f76-e5bb-7cb2-80c2-3740e10440b8`.

## Initial Advice Classification

M19 closeout trace `h25-m19-closeout-20260708` had three
`required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
`grok_consult`. Trace search showed no lane replies before M20 code/docs
changes.

Classification:

- late sidecar advice: no actionable replies before implementation;
- browser-mapped result-count label: `ADOPT`;
- broader per-filter/tag/enhancement-output count labels: `DEFER`;
- explicit enhancement queue/output UI: `DEFER`;
- folder range selection: `DEFER`;
- `REJECT` / `NEEDS_HUMAN`: none at implementation start.

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateFilterSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessEnhancedFilterSmoke -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture -FavoritesOnly
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateSectionSmoke
git diff --name-only -- src
git diff --check
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
    `.cache\native\browser-localstorage-export.json`,
    `.cache\enhance\outputs\native-fixture\m2-fixture-1__enhanced.webp`,
    `.cache\enhance\jobs.json`
  - skipped existing state: none
  - thumbnail compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- `-HeadlessUiSmoke` passed:
  - scannedImages: 4
  - initialVisible: 4
  - folderBuckets: 2
  - favoriteFilterCounts: true
  - filterCountLabel: true
  - enhancedOnlyFilter: true
  - searchMatches: 4
  - favoriteMatches: 1
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
  - filterCountLabel: true
  - dateFilterPersisted: true
  - enhancementStateUnchanged: true
- `-HeadlessEnhancedFilterSmoke` passed:
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
- `-HeadlessImport` passed:
  - favorites: 1
  - albums: 2
  - albumImages: 4
  - browserStateKeys: 5
  - seenImages: 7 cumulative native DB rows at that point
  - settings: 29
  - images: 8 cumulative native DB rows at that point; fixture scan results
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
- `-HeadlessPerf` passed:
  - scanMs: 5
  - searchP50/searchP95: 0.21 ms / 0.46 ms
  - indexed samples: 20 / 20
  - navigationP50/navigationP95: 0.00 ms / 17.51 ms
  - cacheHitRate: 95.0%
  - headerCoverage: 100.0%
  - mutation added/updated/removed: 1 / 1 / 1
  - watcherEvents: 3
- `-HeadlessCacheCompat` passed:
  - images: 4
  - thumb compatible/missing/incompatible: `1 / 2 / 1`
  - display compatible/missing/incompatible: `1 / 3 / 0`
- `-HeadlessFolderSetSmoke` passed:
  - roots: 2
  - removedRoots: 1
  - folderBuckets: 4
  - imagesBeforeRemove/imagesAfterRemove: 6 / 2
  - searchMatches: 6
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
  - totalSeenImages: 12 cumulative native DB rows
  - enhancementStateUnchanged: true
- `-HeadlessDateSectionSmoke` passed:
  - totalImages: 4
  - initialVisible: 4
  - headerGroups: 4
  - filteredGroups: 1
  - gridHeaderGroups: 4
  - manualRangeHeaderGroups: 2
  - manualRangeGridHeaderGroups: 2
  - enhancementStateUnchanged: true
  - note: PowerShell rendered the Japanese `firstHeader` text as mojibake in
    terminal output; the command exited 0 and the UTF-8 file/content policy is
    unchanged.
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

## Native Rows Advanced By M20 First Slice

M20 adds native acceptance evidence for:

- browser-mapped result-count label behavior;
- `<count> indexed` when no browser-mapped count filter is active;
- `<visible> filtered / <indexed> indexed` for search, hidden folder buckets,
  and date filter state;
- reuse of existing native image/filter state without new browser helpers;
- passive count-label updates not changing enhancement job state.

Still deferred:

- search tag suggestion count parity;
- richer per-filter counts beyond the browser result-count label;
- folder range selection, pending custom control or revised product/UI
  semantics;
- explicit enhancement queue/settings/cancel/retry/open/delete output/source
  UI;
- original/enhanced preview toggle and broader output management;
- drag/open parity, placeholder behavior, and native thumbnail warmup UI;
- preview tabs, pin/unpin, close/restore closed tab, and hover/quick preview;
- bulk favorite/open/recycle confirmation and destructive delete UI tests;
- malformed browser-state export UX;
- full browser API/error equivalents;
- native desktop screenshot polish.
