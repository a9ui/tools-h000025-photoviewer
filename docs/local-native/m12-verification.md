# Local Native M12 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M12 resolves the deferred folder range-selection decision and implements a
small native gallery-state restore slice. It does not claim full native parity.

Decision: `NATIVE_FOLDER_RANGE_DEFERRED_WITH_SELECTION_BACKED_GALLERY_STATE_RESTORE`.

Meaning:

- Normal native UI still uses WinForms directly.
- The implementation does not use Node, Next.js, Chrome/browser runtime,
  React/DOM, HTTP image/API routes, Blob/Object URL image loading, webview
  wrapping, or a local server.
- The browser app remains preserved; M12 does not require `src/**` changes.
- Folder range selection remains deferred because the current WinForms
  `CheckedListBox` does not support `SelectionMode.MultiExtended`; replacing
  or custom-wrapping that control needs a future product/UI decision.
- The M12 native slice persists `last_selected_image` and
  `last_visible_index`, restores the saved selection after filter refresh, and
  calls `EnsureVisible` for the restored item.
- Rows that still need date sections, seen/unseen state, large-fixture scroll
  proof, preview tabs, bulk destructive actions, explicit enhancement UI,
  malformed export UX, full browser API/error equivalents, or screenshot
  polish stay `DEFERRED` in
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
  - native settings rows after import: 24
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
  - search p50: 0.20 ms
  - search p95: 0.39 ms
  - indexed samples: 20 / 20
  - navigation p50: 0.00 ms
  - navigation p95: 15.29 ms
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
  - galleryStateRestore: true
  - multiSelection: true
  - backgroundClear: true
  - folderBuckets: 2
  - folderShowSelected/folderHideSelected/folderClearSelection: true
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

## Native Rows Advanced By M12 First Slice

M12 adds native acceptance evidence for:

- explicit folder range-selection product decision with current-control
  evidence;
- persisted native `last_selected_image`;
- persisted native `last_visible_index`;
- saved selection restore after filter refresh;
- `EnsureVisible` on restored selection so the saved item returns to view;
- ordinary native browsing/search/filter/preview/detail smoke not changing
  enhancement job state.

Still deferred:

- folder range selection, pending custom control or revised product/UI
  semantics;
- date sections, seen/unseen state, and large-fixture virtualized scroll proof;
- preview tabs, pin/unpin, close/restore closed tab, and hover/quick preview;
- bulk favorite/open/recycle confirmation and destructive delete UI tests;
- explicit enhancement queue/settings/cancel/retry/open/delete output/source
  UI and enhanced-only filter;
- malformed browser-state export UX;
- full browser API/error equivalents;
- native desktop screenshot polish.
