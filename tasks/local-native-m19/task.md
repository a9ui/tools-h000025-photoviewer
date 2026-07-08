# Local Native M19 - Enhanced-Only Filter Decision

## Goal

Advance one small native parity row after M18 by deciding whether the browser
enhanced-only filter can cleanly map to native without redesigning enhancement
UI, touching the browser app, using Linear, deploying, touching H000033,
deleting cache/state assets, or starting enhancement workers.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target remains the existing PhotoViewer workflows ported into fast local
native software. M19 maps only the browser enhanced-only filter semantics that
can be proven from existing succeeded enhancement job state.

## GitHub Route

- Milestone: #24 `Local Native M19 - Enhanced-Only Filter Decision`
- Issue: #93 `M19-001 Decide native enhanced-only filter mapping`
- Branch: `codex/local-native-m19-enhanced-filter`

Linear is intentionally not used.

## First PR Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m5/browser-regression-matrix.md`
- `tasks/local-native-m19/**`

Implemented in this first slice:

- native toolbar now has an `Enhanced` checkbox that filters visible images to
  sources with succeeded enhancement jobs and non-empty output paths;
- native reads `.cache/enhance/jobs.json` read-only and never posts jobs,
  starts workers, rewrites queue state, or opens output management UI;
- the filter composes with search and favorites-only filters;
- native SQLite persists `enhanced_only_filter`;
- fixture preparation creates a minimal succeeded enhancement job only when
  `.cache/enhance/jobs.json` is absent, preserving existing queue state;
- `-HeadlessEnhancedFilterSmoke` proves the accepted behavior and verifies
  the enhancement state fingerprint is unchanged.

Still deferred:

- explicit enhancement queue/settings/cancel/retry/open/delete output/source
  UI;
- original/enhanced preview toggle and broader output management;
- folder range selection pending a replacement/custom folder bucket control;
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
- new enhancement UI concepts that do not map back to existing PhotoViewer
  behavior

## Verification

Native evidence:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessEnhancedFilterSmoke -Folder .\.cache\native-fixture
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

## Initial Live State

- `origin/main` includes M18 merge commit
  `122097dbb41afc20bb758c1d9f2a6cfaf80f04cf`.
- PR #92 is merged.
- PR CI run #28910713588 passed.
- Final main CI run #28910818584 passed.
- Issue #91 and milestone #23 are closed.
- Issue #93 and milestone #24 are open.
- SQLite job 230 is `merged`.
- SQLite job 231 is `queued` and references the queued M19 thread handoff.

## Initial Advice Classification

M18 closeout trace `h25-m18-closeout-20260708` had three
`required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
`grok_consult`. Trace search showed no lane replies before M19 code/docs
changes.

Classification:

- late sidecar advice: no actionable replies before implementation;
- enhanced-only filter from existing succeeded job state: `ADOPT`;
- explicit enhancement queue UI and output management: `DEFER`;
- folder range selection: `DEFER`.

## Closeout Requirements

- Issue #93 updated with evidence.
- PR opened or merged with CI status recorded.
- Milestone #24 closed when the PR lands.
- SQLite jobs summary updated.
- Agmsg pointers sent and traced to `cursor_impl`, `claude_ui`, and
  `grok_consult`.
- Advice or missing replies classified as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`,
  `DEFER`, or `NEEDS_HUMAN`.
- If more parity work remains, write a next milestone handoff and create or
  hand off a fresh Codex thread before closing the Goal.
