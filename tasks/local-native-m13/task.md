# Local Native M13 - Date Seen And Scroll Parity

## Goal

Advance one small native gallery-state parity row after M12 without redesigning
the UI, touching the browser app, using Linear, deploying, touching H000033, or
starting enhancement workers.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains the existing PhotoViewer workflows ported into fast local
native software. M13 does not invent a new gallery design or visual direction.

## GitHub Route

- Milestone: #18 `Local Native M13 - Date Seen And Scroll Parity`
- Issue: #81 `M13-001 Advance native date/seen/scroll gallery-state parity`
- Branch: `codex/local-native-m13-scroll-proof`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m13/**`

Implemented in this first slice:

- deterministic `.cache/native-fixture-large` with 240 generated local images;
- `-HeadlessLargeScrollSmoke` native UI/headless path;
- verification that the existing WinForms `ListView.VirtualMode` path holds
  240 visible items;
- verification that item 180 persists `last_selected_image` and
  `last_visible_index`;
- verification that filter refresh restores the same item and `EnsureVisible`
  keeps it visible;
- a small UI-smoke harness wait so preview verification follows the existing
  native UI behavior without racing the async selection event.

Still deferred:

- date sections and date-group UI semantics;
- seen/unseen state;
- folder range selection pending replacement/custom folder bucket control;
- drag/open parity, placeholder behavior, and native thumbnail warmup UI;
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
- new UI concepts that do not map back to the existing PhotoViewer behavior

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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large
```

Browser-baseline preservation:

```powershell
corepack pnpm typecheck
corepack pnpm test:unit
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

## Initial Advice Classification

M12 closeout trace `h25-m12-closeout-20260708` had three
`required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
`grok_consult`. No late replies were present before M13 code/docs/GitHub/SQLite
changes. Classification: no actionable advice to adopt; continue with the
small large-scroll proof slice, keep date/seen/folder-range rows deferred.

## Closeout Requirements

- Issue #81 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
