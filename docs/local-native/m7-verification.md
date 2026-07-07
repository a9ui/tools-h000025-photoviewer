# Local Native M7 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M7 is a native UI parity evidence sweep. It does not claim full native parity.
Browser E2E remains browser-baseline evidence only.

Decision: `NATIVE_UI_SMOKE_ADDED_WITH_REMAINING_PARITY_GAPS_DEFERRED`.

Meaning:

- Normal native UI smoke uses WinForms directly.
- The smoke does not use Node, Next.js, Chrome/browser runtime, React/DOM,
  HTTP image/API routes, Blob/Object URL image loading, webview wrapping, or a
  local server.
- The browser app remains preserved; M7 does not require `src/**` changes.
- Rows that still need product implementation or manual visual acceptance stay
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
  - images: 3
  - created state:
    `.cache\favorites.json`, `.cache\albums.json`, `.cache\settings.json`,
    `.cache\native\browser-localstorage-export.json`
  - skipped existing state: none in this isolated M7 worktree
  - thumbnail compatible/missing/incompatible: `1 / 1 / 1`
  - display compatible/missing/incompatible: `1 / 2 / 0`
- Headless import passed:
  - favorites: 1
  - albums: 2
  - album image membership rows: 3
  - explicit browser `pvu_*` state keys: 5
  - native settings rows after import: 11
  - images at import time: 0
- Headless scan passed:
  - images: 3
  - stored favorites: 1
  - imported favorites: 1
  - elapsed: 4 ms
- Headless incremental scan passed:
  - images: 3
  - addedOrUpdated: 0
  - removed: 0
  - unchanged: 3
  - elapsed: 4 ms
- Headless indexed search passed:
  - query `fixture`: 3 matches, `indexed=true`
  - query `fixture` with favorites only: 1 match, `indexed=true`
- Headless performance passed:
  - scan: 3 ms
  - search p50: 0.16 ms
  - search p95: 0.35 ms
  - indexed samples: 20 / 20
  - navigation p50: 0.00 ms
  - navigation p95: 15.38 ms
  - cache hit rate: 95.0%
  - header coverage: 100.0%
  - mutation probe: added 1, updated 1, removed 1
  - watcher events observed: 3
- Headless cache compatibility passed:
  - images checked: 3
  - thumbnail compatible/missing/incompatible: `1 / 1 / 1`
  - display compatible/missing/incompatible: `1 / 2 / 0`
- Native WinForms UI smoke passed:
  - runtime: `winforms`
  - scannedImages: 3
  - initialVisible: 3
  - previewLoaded: true
  - navigationButtons: true
  - keyboardNavigation: true
  - keyboardFavorite: true
  - gridToggle: true
  - searchMatches: 3
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

## Native UI Rows Advanced By M7

M7 adds native acceptance evidence for:

- single-folder path scan and imported state summary,
- direct preview loading from local file paths,
- list/grid native browsing,
- previous/next navigation and keyboard navigation,
- favorite-level keyboard mutation and favorites-only filtering,
- indexed search and no-results UI status,
- missing-folder error status,
- album/browser-state/settings import counts,
- performance-sensitive navigation,
- cache compatibility measurement,
- ordinary native browsing not changing enhancement job state.

Still deferred:

- multi-root folder sets, folder visibility buckets, sort/date/seen/scroll
  parity, preview tabs, right-panel resizing/bulk actions, separate modal zoom
  and pan controls, prompt metadata controls, Recycle confirmation UI, full
  keybinding recorder, explicit enhancement UI, all browser API error
  equivalents, full `pvu_*` persistence parity, and native desktop screenshot
  polish.
