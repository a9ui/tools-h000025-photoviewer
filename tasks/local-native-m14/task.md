# Local Native M14 - Date And Seen Parity

## Goal

Advance one small native gallery-state parity row after M13 without redesigning
the UI, touching the browser app, using Linear, deploying, touching H000033, or
starting enhancement workers.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains the existing PhotoViewer workflows ported into fast local
native software. M14 does not invent a new gallery design or visual direction.

## GitHub Route

- Milestone: #19 `Local Native M14 - Date And Seen Parity`
- Issue: #83 `M14-001 Map native date grouping or seen-state parity`
- Branch: `codex/local-native-m14-date-seen`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m14/**`

Implemented in this first slice:

- native SQLite `seen_images` table;
- explicit browser `pvu_seen_images` export import into native seen state;
- `NativeImageRecord.IsSeen` query mapping for scan/search/gallery rows;
- minimal native unseen row marker through the existing list/grid item text;
- native selected-preview image marks seen and persists that state;
- `-HeadlessSeenSmoke` proving imported browser seen state and native
  persistence without browser runtime or local HTTP server.

Still deferred:

- date sections and date filter controls;
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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke
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

M13 closeout trace `h25-m13-closeout-20260708` had three
`required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
`grok_consult`. No late replies were present before M14 code/docs/GitHub/SQLite
changes. Classification: no actionable late advice to adopt; seen-state slice
`ADOPT`; date sections and folder range selection `DEFER`.

## Closeout Requirements

- Issue #83 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
