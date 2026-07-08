# Local Native M18 - Manual Date Range Filters

## Goal

Advance one small native manual date range filter parity row after M17 without
redesigning the UI, touching the browser app, using Linear, deploying, touching
H000033, deleting cache/state assets, or starting enhancement workers.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains the existing PhotoViewer workflows ported into fast local
native software. M18 maps the browser `dateFrom` / `dateTo` controls into the
existing native date filter row; it does not introduce a new gallery concept.

## GitHub Route

- Milestone: #23 `Local Native M18 - Manual Date Range Filters`
- Issue: #91 `M18-001 Map native manual date range filters`
- Branch: `codex/local-native-m18-manual-date-range-filters`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m18/**`

Implemented in this first slice:

- native date controls now include checked `From` and `To` date inputs beside
  the existing date preset selector;
- preset selections still map to browser `Today`, `7d`, `30d`, `This year`,
  and `Clear` behavior by setting the same inclusive local date range;
- manual changes select `Custom range` and persist browser-mapped `date_from`
  / `date_to` settings;
- date range filtering composes with search, favorite filters, Created sort,
  and list/grid date section headers;
- non-date native smokes now clear persisted manual date ranges at scenario
  start so saved date state cannot leak between acceptance checks.

Still deferred:

- folder range selection pending replacement/custom folder bucket control;
- enhanced-only filter and explicit enhancement queue UI;
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

M17 closeout trace `h25-m17-closeout-20260708` had three
`required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
`grok_consult`. Trace search showed no lane replies before M18 code/docs
changes. Classification: no actionable late advice to adopt; native manual
date range filters `ADOPT`; folder range selection `DEFER`.

## Closeout Requirements

- Issue #91 updated with evidence.
- PR opened or merged with CI status recorded.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
