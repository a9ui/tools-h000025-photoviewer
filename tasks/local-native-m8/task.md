# Local Native M8 - Deferred Parity Implementation

## Goal

Implement a small, verifiable slice of the highest-value native parity gaps
left after M7, while preserving the browser app and keeping non-implemented
rows explicitly deferred.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains a fast native local PhotoViewer, not a webview, local
server, browser wrapper, or packaged Next.js app.

## GitHub Route

- Milestone: #13 `Local Native M8 - Deferred Parity Implementation`
- Issue: #71 `M8-001 Implement deferred native parity gaps`
- Branch: `codex/h25-local-native-m8-deferred-parity`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m8/**`

Implemented in this first slice:

- native folder visibility buckets with all/none/invert controls,
- sort modes for modified, created, name, folder, size, favorite, and random,
- random reshuffle and thumbnail-size display control,
- preview panel show/hide and detail show/hide controls,
- keybinding metadata for preview/details/reshuffle shortcuts,
- extended native WinForms UI smoke evidence for the rows above.

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

- Issue #71 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
