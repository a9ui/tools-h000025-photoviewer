# Local Native M20 - Filter Count Label Decision

## Goal

Advance one small native parity row after M19 by deciding whether remaining
browser sidebar/filter count labels can cleanly map to native without touching
the browser app, redesigning filters, using Linear, deploying, touching
H000033, deleting cache/state assets, or starting enhancement workers.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
M20 maps only the browser result-count label behavior that is already explicit
in the existing browser app and already backed by native state.

## GitHub Route

- Milestone: #25 `Local Native M20 - Filter Count Label Decision`
- Issue: #95 `M20-001 Decide native filter count label parity`
- Branch: `codex/local-native-m20-filter-count`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m20/**`

Implemented in this first slice:

- native top status now maps the browser result-count label:
  - no browser-mapped count filter: `<count> indexed`;
  - search, date range, or hidden folder buckets: `<visible> filtered /
    <indexed> indexed`;
- the label uses existing native `_allImages`, `_visibleImages`, search text,
  date range, and folder-bucket state;
- `-HeadlessUiSmoke` verifies indexed, folder-filtered, restored, and
  search-filtered count labels;
- `-HeadlessDateFilterSmoke` verifies date-filtered and cleared count labels.

Still deferred:

- search tag suggestion count parity;
- richer per-filter count labels beyond the browser result-count mapping;
- folder range selection pending a replacement/custom folder bucket control;
- explicit enhancement queue/settings/cancel/retry/open/delete output/source
  UI;
- original/enhanced preview toggle and broader output management;
- drag/open parity, placeholder behavior, and native thumbnail warmup UI;
- preview tabs and pin/unpin/restore behavior;
- bulk recycle confirmation and destructive delete sweeps;
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
- folder range replacement or broad enhancement UI

## Verification

Native evidence:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateFilterSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessEnhancedFilterSmoke -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture -FavoritesOnly
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke
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

## Initial Live State

- `origin/main` is M19 merge commit
  `dfcdb12fa5fb23254b6d0d8589952b713b15a530`.
- PR #94 is merged.
- PR CI run #28911783114 passed.
- Final main CI run #28911883489 passed.
- Issue #93 and milestone #24 are closed.
- Issue #95 and milestone #25 are open.
- SQLite job 231 is `merged`.
- SQLite job 232 is `queued` and now references Codex thread
  `019f3f76-e5bb-7cb2-80c2-3740e10440b8`.

## Initial Advice Classification

M19 closeout trace `h25-m19-closeout-20260708` had three
`required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
`grok_consult`. Trace search showed no lane replies before M20 code/docs
changes.

Classification:

- late sidecar advice: no actionable replies before implementation;
- browser-mapped result-count label: `ADOPT`;
- broader per-filter/tag/enhancement-output count labels: `DEFER`;
- explicit enhancement queue/output UI: `DEFER`;
- folder range selection: `DEFER`.

## Closeout Requirements

- Issue #95 updated with evidence.
- PR opened or merged with CI status recorded.
- Milestone #25 closed when the PR lands.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
