# Local Native M6 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Scope Decision

M6 does not claim full native parity. The browser-feature/native-parity matrix
has every row classified in
`tasks/local-native-m5/browser-regression-matrix.md`.

Decision: `GO_WITH_ORDERED_STACK_MERGE_FOR_ISOLATED_RC_BASELINE`.

Meaning:

- Merge may proceed for the isolated local-native release-candidate baseline.
- The browser app remains preserved and unchanged.
- Browser E2E evidence is baseline-preservation evidence only.
- Native full parity remains incomplete and must continue in the next
  milestone.

## Live Entry State

- PR #43: draft/open, base `main`, head `codex/h25-local-native-m1`,
  `mergeStateStatus=CLEAN`, verify success run `28867872288`.
- PR #48: draft/open, base `codex/h25-local-native-m1`, head
  `codex/h25-local-native-m2`, `mergeStateStatus=CLEAN`, verify success run
  `28869670313`.
- PR #55: draft/open, base `codex/h25-local-native-m2`, head
  `codex/h25-local-native-m3`, `mergeStateStatus=CLEAN`, verify success run
  `28872084793`.
- PR #61: draft/open, base `codex/h25-local-native-m3`, head
  `codex/h25-local-native-m4`, `mergeStateStatus=CLEAN`, verify success run
  `28881130949`.
- PR #66: draft/open, base `codex/h25-local-native-m4`, head
  `codex/h25-local-native-m5`, head
  `8c1754b0d3959e6e6d1791d2480eebf991d35dda`,
  `mergeStateStatus=CLEAN`, verify success run `28883348901`.
- Issue #65 is closed as completed.
- Milestone #10 `Local Native M5 - Release Candidate Readiness` is closed with
  4 closed issues and 0 open issues.
- Agmsg trace `h25-m5-closeout-20260708` contains the three required
  `required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
  `grok_consult`; no late replies were present in the trace.
- SQLite jobs summary has H000025 local-native rows #214-#217 as `pr_open`
  for M2-M5, with #217 tracking PR #66 and next action M6 matrix completion
  before stack merge.

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
corepack pnpm typecheck
corepack pnpm test:unit
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

## Results

- Native build passed with 0 warnings and 0 errors.
- Fixture preparation passed:
  - images: 3
  - created state:
    `.cache\favorites.json`, `.cache\albums.json`, `.cache\settings.json`,
    `.cache\native\browser-localstorage-export.json`
  - skipped existing state: none in this isolated M6 worktree
  - thumbnail compatible/missing/incompatible: `1 / 1 / 1`
  - display compatible/missing/incompatible: `1 / 2 / 0`
- Headless import passed:
  - favorites: 1
  - albums: 2
  - album membership rows: 3
  - explicit browser `pvu_*` state keys: 5
  - native settings rows after import: 11
  - images at import time: 0
- Headless scan passed:
  - images: 3
  - stored favorites: 1
  - imported favorites: 1
  - elapsed: 4 ms
- Headless performance passed:
  - scan: 3 ms
  - search p50: 0.20 ms
  - search p95: 0.50 ms
  - indexed samples: 20 / 20
  - navigation p95: 14.95 ms
  - cache hit rate: 95.0%
  - header coverage: 100.0%
  - mutation probe: added 1, updated 1, removed 1
  - watcher events observed: 3
- Headless cache compatibility passed:
  - images checked: 3
  - thumbnail compatible/missing/incompatible: `1 / 1 / 1`
  - display compatible/missing/incompatible: `1 / 2 / 0`
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `corepack pnpm test:e2e` passed: 2 Chromium tests.
- `powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full`
  failed because this project checkout has no `System/scripts/verify-project.ps1`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full`
  passed:
  - required files
  - `pnpm test:unit`
  - `pnpm lint`
  - `pnpm audit --audit-level moderate`
  - `pnpm typecheck`
  - `pnpm build`
  - `pnpm test:e2e`
- `pnpm lint` reported 0 errors and 2 existing warnings for `<img>` usage in
  `src/components/CachedImage.tsx`.
- `git diff --name-only -- src` returned no files.

## Verifier Path Correction

M5 handoff and M6 delegation text listed:

```powershell
powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full
```

That path is not present in this H000025 checkout or in the Tools root System
scripts. Project-local guidance and `project.toml` use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```

M6 therefore records the `.\System\...` command as a path/documentation defect
and uses the project verifier as the actual full verification command.

## Native Parity Result

The matrix has no blank rows and no `BLOCKED` rows. All remaining gaps are
explicitly `DEFERRED`.

This is sufficient to merge the isolated native stack as an RC foundation, but
it is not sufficient to call native parity complete. The next milestone should
focus on native UI parity and manually verifiable desktop behavior.
