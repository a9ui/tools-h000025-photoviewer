# Local Native M11 - Multi Root Folder Sets

## Goal

Implement a small, verifiable native folder-set parity slice without touching
the browser app.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains a fast native local PhotoViewer, not a webview, local
server, browser wrapper, or packaged Next.js app.

## GitHub Route

- Milestone: #16 `Local Native M11 - Multi Root Folder Sets`
- Issue: #77 `M11-001 Implement native multi-root folder-set parity`
- Branch: `codex/local-native-m11-folder-sets`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m11/**`
- `scripts/start-local-native.ps1`

Implemented in this first slice:

- semicolon/newline/pipe parsed native folder sets;
- native scan and indexed search across multiple active roots;
- native FileSystemWatcher coverage for all active roots;
- active-set remove-folder behavior without deleting cache/state assets;
- persisted `recent_folder_set` plus open recent folder set behavior;
- explicit manual refresh for the active folder set;
- repeatable second fixture root under `.cache/native-fixture-extra`;
- new native WinForms folder-set smoke evidence.

Still deferred:

- folder range selection, by product decision for this PR;
- preview tabs and pin/unpin/restore behavior;
- bulk recycle confirmation and destructive delete sweeps;
- date sections, scroll memory, seen/unseen state, and large-fixture virtual
  scroll evidence;
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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture
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

- Issue #77 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
