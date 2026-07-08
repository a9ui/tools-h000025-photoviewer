# Local Native M17 - Grid Date Sections

## Goal

Advance one small native grid date section/header grouping row after M16
without redesigning the UI, touching the browser app, using Linear, deploying,
touching H000033, deleting cache/state assets, or starting enhancement
workers.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains the existing PhotoViewer workflows ported into fast local
native software. M17 does not invent a new gallery design or visual direction.

## GitHub Route

- Milestone: #22 `Local Native M17 - Grid Date Sections`
- Issue: #89 `M17-001 Map native grid date section headers`
- Branch: `codex/local-native-m17-grid-date-sections`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m17/**`

Implemented in this first slice:

- native Created-sort grid view now marks the first visible image for each
  local created date with the same browser-mapped date header label used by
  M16 list view;
- the native header marker still comes from the filtered visible list, so it
  composes with existing date presets, search, favorite, folder, and sort
  controls;
- `-HeadlessDateSectionSmoke` now proves list headers, grid headers, Created
  sort order, Today-filter regrouping in list and grid, and passive
  enhancement isolation.

Still deferred:

- manual date range inputs;
- folder range selection pending replacement/custom folder bucket control;
- drag/open parity, placeholder behavior, and native thumbnail warmup UI;
- preview tabs and pin/unpin/restore behavior;
- bulk recycle confirmation and destructive delete sweeps;
- explicit enhancement queue UI and enhanced-only filter;
- full browser API/error equivalence;
- native desktop screenshot polish.

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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateFilterSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateSectionSmoke
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

M16 closeout trace `h25-m16-closeout-20260708` had three
`required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
`grok_consult`. Trace search showed no lane replies before M17 code/docs
changes. Classification: no actionable late advice to adopt; native grid date
section headers `ADOPT`; manual date range and folder range selection `DEFER`.

Sidecar risk review in this thread agreed with the low-risk route: reuse the
existing header map and shared `CreateListItem()` text marker for grid view,
avoid a new grid layout or `ListViewGroup` implementation, and keep
`NativeHeadlessRunner.cs` unchanged. Classification: `ADOPT`.

## Closeout Requirements

- Issue #89 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
