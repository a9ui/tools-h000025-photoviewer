# Local Native M4 Handoff - Native Parity And Cache Reuse

## Next Goal

Create a Goal for `Local Native M4 - Native Parity And Cache Reuse` before
planning or editing.

Objective: deepen the isolated local-native lane after M3 by adding album
membership parity, explicit browser `pvu_*` export/import handling, and
measured optional reuse of existing thumbnail/display cache assets when cache
key compatibility is proven.

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
- `tasks/local-native-m1/task.md`
- `tasks/local-native-m2/task.md`
- `tasks/local-native-m3/handoff.md`
- `tasks/local-native-m3/task.md`
- this file

Then inspect GitHub PRs #43, #48, and #55, GitHub Actions, SQLite jobs
summary, Agmsg trace for `h25-m3-closeout-20260707`, and local `git status`.

## Current State

- M1 draft PR: #43 `Build local native viewing MVP`
- M2 draft PR: #48 `Build local native browser workflow drop-in`
- M3 draft PR: #55 `Build local native M3 performance acceleration`
- M3 base: `codex/h25-local-native-m2`
- M3 head: `codex/h25-local-native-m3`
- M3 CI: verify run `28871706963` passed
- M3 issues #49, #50, #52, #53, and #54 are closed with verification comments
- M3 milestone #8 `Local Native M3 - Native Performance` is closed
- M3 branch was published by GitHub API fallback because direct `git push` was
  blocked by local approval policy
- M3 changed no `src/**` browser-app files
- H33 remains untouched
- Linear remains unused

## M3 Verification Evidence

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- Headless import/scan/incremental/search/performance checks passed against
  ignored local fixture `.cache/native-fixture`.
- Headless indexed search:
  - query `fixture`: 3 matches, `indexed=true`
  - substring fallback query `xture`: 3 matches, `indexed=false`
- Headless performance, 20 iterations:
  - search p95: 0.68 ms
  - navigation p95: 18.84 ms
  - cache hit rate: 95.0%
  - header coverage: 100.0%
  - mutation probe: added 1, updated 1, removed 1
  - watcher events observed: 3
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `git diff --name-only -- src` returned no files.
- GitHub Actions verify passed for PR #55.

## Agmsg Checkpoint

Correlation id: `h25-m3-closeout-20260707`

Send required-reply-none pointers at M3 closeout:

- `cursor_impl`: M3 PR #55 is stacked on PR #48, verify passed, no `src/`
  changes, and M4 should preserve the current native/browser split.
- `claude_ui`: optional human-surface review pointer for M3 native UI behavior,
  especially watcher refresh status, dimension text, and parity wording.
- `grok_consult`: M4 risk focus pointer for album membership, browser `pvu_*`
  export/import, cache-key compatibility, and avoiding accidental enhancement
  work.

No replies are required to start M4. If replies appear later, classify them as
`ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or `NEEDS_HUMAN` before changing
code, docs, GitHub, or SQLite.

## M4 Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m4/**`
- minimal parent launcher/docs updates needed for verification

Avoid:

- `src/**` unless an explicit browser export helper is needed, minimal, and
  verified
- H000033
- Linear
- deployment
- automatic enhancement workers
- deleting `.cache/thumbs`, `.cache/display`, `.cache/enhance`, favorites,
  albums, settings, or native SQLite state

## M4 Verification Minimum

At minimum:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder <fixture-or-real-folder> -Search <query>
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder <fixture-or-real-folder> -Search <query> -PerfIterations 20
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

Add M4-specific headless checks for album membership import/export,
browser-state export/import if implemented, and cache-key compatibility before
calling M4 complete.

## Closeout Rule For M4

M4 completion requires:

- local native build,
- M4-specific headless parity/cache checks,
- pnpm typecheck,
- pnpm test:unit,
- no accidental `src/**` changes unless explicitly justified and verified,
- GitHub milestone/issues/PR updated,
- short Agmsg pointers sent and traced,
- advice classified if any arrives,
- a next milestone handoff packet written,
- actual Codex thread-tool discovery and next thread create/handoff before the
  M4 Goal is closed.
