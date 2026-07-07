# Local Native M5 Handoff - Release Candidate Readiness

## Next Goal

Create a Goal for `Local Native M5 - Release Candidate Readiness` before
planning or editing.

Objective: turn the stacked local-native lane into a release-candidate review
surface without changing the browser app: verify the M1-M4 PR stack, decide the
safe merge/close order, harden repeatable fixture generation, and prepare a
bounded native parity review packet.

## Source Packet To Read First

Read these files in full before planning or editing:

- `AGENTS.md`
- `PROJECT.md`
- `DESIGN.md`
- `project.toml`
- `local-native/README.md`
- `docs/local-native/state-migration-map.md`
- `docs/local-native/m1-verification.md`
- `docs/local-native/m2-verification.md`
- `docs/local-native/m3-verification.md`
- `docs/local-native/m4-verification.md`
- `tasks/local-native-m1/task.md`
- `tasks/local-native-m2/task.md`
- `tasks/local-native-m3/handoff.md`
- `tasks/local-native-m3/task.md`
- `tasks/local-native-m3/m4-handoff.md`
- `tasks/local-native-m4/task.md`
- this file

Then inspect GitHub PRs #43, #48, #55, and #61, GitHub Actions, SQLite jobs
summary, Agmsg trace for `h25-m4-closeout-20260708`, and local `git status`.

## Current State

- M1 draft PR: #43 `Build local native viewing MVP`
- M2 draft PR: #48 `Build local native browser workflow drop-in`
- M3 draft PR: #55 `Build local native M3 performance acceleration`
- M4 draft PR: #61 `Build local native M4 parity and cache reuse`
- M4 base: `codex/h25-local-native-m3`
- M4 head: `codex/h25-local-native-m4`
- M4 changed no `src/**` browser-app files.
- H33 remains untouched.
- Linear remains unused.
- Direct `git push` was blocked by local approval policy. M4 branch was
  published through the documented GitHub API fallback after creating the
  remote branch ref from the M3 head.

## M4 Verification Evidence

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- Headless import/scan/incremental/search/performance/cache compatibility
  checks passed against ignored local fixture `.cache/native-fixture`.
- Headless import:
  - favorites: 1
  - albums: 2
  - album image membership rows: 3
  - explicit browser `pvu_*` state keys: 5
- Headless indexed search:
  - query `fixture`: 3 matches, `indexed=true`
  - substring fallback query `xture`: 3 matches, `indexed=false`
- Headless performance, 20 iterations:
  - search p95: 0.65 ms
  - navigation p95: 14.79 ms
  - cache hit rate: 95.0%
  - header coverage: 100.0%
  - mutation probe: added 1, updated 1, removed 1
  - watcher events observed: 3
- Headless cache compatibility:
  - images checked: 3
  - thumbnail compatible/missing/incompatible: 1 / 1 / 1
  - display compatible/missing/incompatible: 1 / 2 / 0
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `git diff --name-only -- src` returned no files.
- GitHub Actions for PR #61 must be rechecked live by the next thread because
  this handoff may not be the final PR head at read time.

## Agmsg Checkpoint

Correlation id: `h25-m4-closeout-20260708`

Send required-reply-none pointers at M4 closeout:

- `cursor_impl`: M4 PR #61 is stacked on PR #55, local verification passed, no
  `src/` changes, and M5 should focus on stack/merge readiness rather than new
  product scope.
- `claude_ui`: optional human-surface/docs review pointer for M4 native
  parity wording and state-summary clarity.
- `grok_consult`: M5 risk focus pointer for stacked PR merge order, cache reuse
  assumptions, fixture repeatability, and avoiding accidental browser/native
  coupling.

No replies are required to start M5. If replies appear later, classify them as
`ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or `NEEDS_HUMAN` before changing
code, docs, GitHub, or SQLite.

## M5 Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m5/**`
- small updates under `tasks/local-native-m4/**` if needed for closeout
- GitHub PR/issue/milestone/Actions review
- SQLite job summary updates and Agmsg pointers

Avoid:

- `src/**` unless a browser export helper is explicitly approved, minimal, and
  verified
- H000033
- Linear
- deployment
- automatic enhancement workers
- deleting `.cache/thumbs`, `.cache/display`, `.cache/enhance`, favorites,
  albums, settings, or native SQLite state

## M5 Verification Minimum

At minimum:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder <fixture-or-real-folder>
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder <fixture-or-real-folder> -Search <query> -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder <fixture-or-real-folder>
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

Add M5-specific checks for PR stack merge readiness and repeatable fixture
generation before calling M5 complete.

## Closeout Rule For M5

M5 completion requires:

- local native build,
- headless parity/cache checks,
- pnpm typecheck,
- pnpm test:unit,
- no accidental `src/**` changes unless explicitly justified and verified,
- GitHub PR stack and issue/milestone state updated,
- short Agmsg pointers sent and traced,
- advice classified if any arrives,
- a next milestone handoff packet written,
- actual Codex thread-tool discovery and next thread create/handoff before the
  M5 Goal is closed.
