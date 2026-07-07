# Local Native M9 - Modal And Settings Parity

## Goal

Implement a small, verifiable native parity slice for detail/modal controls,
settings/keybinding treatment, and right preview panel state without touching
the browser app.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains a fast native local PhotoViewer, not a webview, local
server, browser wrapper, or packaged Next.js app.

## GitHub Route

- Milestone: #14 `Local Native M9 - Modal And Settings Parity`
- Issue: #73 `M9-001 Implement native modal and settings parity gaps`
- Branch: `codex/h25-local-native-m9-modal-settings`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1` if a new native smoke switch is required
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m9/**`

Implemented in this first slice:

- native detail modal entry from the main viewer through a `Detail` button and
  `Ctrl+M`;
- detail modal previous/next navigation over the current filtered order;
- detail modal zoom in/out, reset, mouse/scrollbar pan, horizontal flip,
  favorite up/down, and open-external command path;
- right preview selected-count label;
- right preview splitter distance persistence;
- native settings dialog with explicit read-only keybinding recorder decision;
- expanded keybinding metadata for the new detail controls;
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

- Issue #73 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
