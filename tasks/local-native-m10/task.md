# Local Native M10 - Selection Filters And Folder Parity

## Goal

Implement a small, verifiable native parity slice for selection behavior,
sidebar-style filters, and folder visibility controls without touching the
browser app.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains a fast native local PhotoViewer, not a webview, local
server, browser wrapper, or packaged Next.js app.

## GitHub Route

- Milestone: #15 `Local Native M10 - Selection Filters And Folder Parity`
- Issue: #75 `M10-001 Implement native selection, filters, and folder parity gaps`
- Branch: `codex/local-native-m10-selection-filters`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m10/**`

Implemented in this first slice:

- native ListView multi-selection and background selection clear;
- selected-count label for multi-selection;
- search clear button;
- favorite filter choices with count labels for all/favorites/unrated/levels
  1-5;
- persisted native favorite filter key in SQLite `native_settings`;
- selected folder-bucket show/hide/clear controls;
- nested fixture image for repeatable folder-bucket acceptance evidence;
- extended native WinForms UI smoke evidence for the rows above.

Still deferred:

- multi-root folder-set scan/search/watch behavior;
- remove-folder and open-recent-folder-set flows;
- folder range selection;
- preview tabs and pin/unpin/restore behavior;
- bulk recycle confirmation and destructive delete sweeps;
- explicit enhancement queue UI and enhanced-only filter;
- full browser API/error equivalence.

Forbidden unless explicitly approved:

- `src/**`
- H000033
- deployment
- automatic enhancement workers
- deletion of `.cache/thumbs`, `.cache/display`, `.cache/enhance`,
  favorites, albums, settings, or native SQLite state
- Chrome profile reads or browser-state scraping

## Verification

Native evidence:

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
```

Browser-baseline preservation:

```powershell
corepack pnpm typecheck
corepack pnpm test:unit
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

## Closeout Requirements

- Issue #75 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
