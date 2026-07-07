# Local Native M12 - Folder Range And Gallery State

## Goal

Resolve the deferred native folder range-selection product decision first, then
land the smallest verified native gallery-state slice that fits without
touching the browser app.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains a fast native local PhotoViewer, not a webview, local
server, browser wrapper, or packaged Next.js app.

## GitHub Route

- Milestone: #17 `Local Native M12 - Folder Range And Gallery State`
- Issue: #79 `M12-001 Decide folder range behavior and land native gallery-state slice`
- Branch: `codex/local-native-m12-gallery-state`

Linear is intentionally not used.

## Product Decision

Decision: defer native folder range selection for M12.

Evidence:

- The current folder bucket UI uses WinForms `CheckedListBox`.
- `CheckedListBox` rejects `SelectionMode.MultiExtended` with
  `ArgumentException: Multi-selection is not supported on CheckedListBox`.
- A safe range-selection implementation therefore needs a product/UI decision
  for replacing or custom-wrapping the folder bucket control, instead of a
  small configuration change.

M12 keeps the existing single selected folder-bucket behavior. `Show Sel`,
`Hide Sel`, and `Clear Sel` remain valid for the currently selected bucket.
Range behavior stays `DEFERRED` until a future slice defines the custom control,
keyboard/mouse semantics, and native smoke evidence.

## First PR Scope

Allowed:

- `local-native/**`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m12/**`

Implemented in this first slice:

- native gallery state persistence for the last selected image path;
- native gallery state persistence for the last visible index;
- restore of the saved image or index after scan/filter refresh;
- `ListView.EnsureVisible` on restored selection so the selected row is brought
  back into view;
- native WinForms UI smoke evidence through `galleryStateRestore=true`.

Still deferred:

- folder range selection, by M12 product decision;
- date sections;
- seen/unseen state;
- large-fixture virtualized scroll-state proof beyond selection-backed restore;
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

- Issue #79 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
