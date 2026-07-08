# Local Native M20 - Migration v1 Closeout Gate

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/95

## Objective

M20 is the `Local Native Migration v1 Closeout Gate`.

The finish line is not full browser parity and not a narrow filter-count-label
addition. First decide what is required to honestly say the native viewer has
been migrated enough for local use, then implement only true blockers for that
v1 line.

User direction:

> ケツを先に決めないと終わらない。まずローカル版への移行を済ませて、それから最善化

## Guardrails

- No Linear.
- Do not touch H000033.
- Do not deploy.
- Do not modify `src/**` unless a minimal browser evidence/export helper is
  explicitly approved and verified.
- Do not start automatic enhancement workers.
- Do not delete existing cache/state assets.
- Preserve the existing browser PhotoViewer app and its workflows.

## Required M20 Work

1. Fix the minimum conditions for `local-native migration v1 complete`.
2. Inventory M1-M19 completed native work in user-facing Japanese.
3. Classify remaining rows into:
   - `v1 closeout 必須`
   - `v1 後の最善化`
   - `明示的に延期`
4. Implement only true v1 closeout blockers discovered by that classification.
5. Decide whether count labels, folder range, explicit enhancement UI,
   API/error parity, UI polish, and related rows are v1 blockers.
6. At close, update GitHub issue/PR/milestone, SQLite jobs, Agmsg
   pointers/trace, advice classification, and create/handoff the next actual
   Codex thread if post-v1 native work remains.

## M20 Decision

Current M1-M19 evidence already covers the core local viewer path:

- local native launch/build and fixture verification;
- local folder and folder-set scan/import/search;
- native SQLite state and browser-state explicit export import;
- favorites, albums, folder visibility, sorting/display, selection, preview,
  detail modal, gallery state restore, large virtual scroll, seen/unseen,
  date filters/date headers, and enhanced-only read-only filtering;
- passive browsing/search/preview/detail/enhanced filtering that does not
  enqueue or mutate enhancement jobs;
- browser app preservation with no `src/**` changes.

Therefore M20 does not accept a new implementation slice unless verification
finds a real v1 closeout blocker. Remaining count-label richness, folder range
selection, broad explicit enhancement UI, API/error parity, and polish are not
v1 blockers unless separately approved or proven necessary.

GitHub milestone #26 `Local Native Post-v1 Backlog` now tracks the post-v1
implementation list in issues #97-#118. Those issues are after-v1 by default
and must not block M20 unless explicitly reclassified as v1-required with
evidence.

## Verification Plan

Minimum local verification for closeout:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateFilterSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateSectionSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessEnhancedFilterSmoke -Folder .\.cache\native-fixture
corepack pnpm typecheck
corepack pnpm test:unit
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

Browser commands are baseline-preservation evidence only. Native acceptance
must come from native build/headless/UI smoke evidence.
