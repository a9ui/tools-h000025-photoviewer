# Local Native M3 Handoff - Native Performance

## Next Goal

Create a Goal for `Local Native M3 - Native Performance` before planning or
editing.

Objective: add native-only acceleration on top of the M2 browser workflow
drop-in: incremental scan/watchers, indexed search, modal/preview ring buffer,
header-first dimensions, native cache scheduler, and measured p95 navigation.

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
- `tasks/local-native-m1/task.md`
- `tasks/local-native-m2/task.md`
- this file

Then inspect GitHub PRs #43 and #48, GitHub Actions, SQLite jobs summary, and
local git status.

## Current State

- M1 draft PR: #43 `Build local native viewing MVP`
- M2 draft PR: #48 `Build local native browser workflow drop-in`
- M2 base: `codex/h25-local-native-m1`
- M2 head: `codex/h25-local-native-m2`
- M2 CI: verify run `28869254676` passed
- M2 issues #44-#47 are closed with verification comments
- M2 branch was published by GitHub API fallback because direct `git push` was
  blocked by local approval policy
- M2 changed no `src/**` browser-app files
- H33 remains untouched
- Linear remains unused

## M2 Verification Evidence

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- Headless import/scan/search/favorite mutation passed against ignored local
  fixture `.cache/native-fixture`.
- SQLite counts after checks:
  - `images`: 3
  - `favorites`: 2
  - `albums`: 1
  - `native_settings`: 5
  - `scan_roots`: 1
  - `import_runs`: 4
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `git diff --name-only -- src` returned no files.
- GitHub Actions verify passed for PR #48.

## Agmsg Checkpoint

Correlation id: `h25-m2-closeout-20260707`

Sent required-reply-none pointers:

- `cursor_impl`: M2 PR #48 is stacked on PR #43, verify passed, no `src/`
  changes.
- `claude_ui`: optional human-surface review pointer for M2 native UI parity.
- `grok_consult`: M3 risk focus pointer for watchers, indexed search, ring
  buffer, and cache scheduler.

No replies are required to start M3. If replies appear later, classify them as
`ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or `NEEDS_HUMAN` before changing
code, docs, GitHub, or SQLite.

## M3 Scope

Allowed:

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m3/**`
- minimal parent launcher/docs updates needed for verification

Avoid:

- `src/**` unless an explicit compatibility/export helper is needed and
  verified
- H000033
- Linear
- deployment
- automatic enhancement workers

## M3 Verification Commands

At minimum:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder <fixture-or-real-folder>
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder <fixture-or-real-folder> -Search <query>
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

Add M3-specific headless measurements for p95 navigation/search/cache behavior
before calling M3 complete.

## Closeout Rule For M3

M3 completion requires:

- local native build,
- headless performance checks,
- pnpm typecheck,
- pnpm test:unit,
- no accidental `src/**` changes,
- GitHub milestone/issues/PR updated,
- short Agmsg pointers sent and traced,
- advice classified if any arrives,
- a next milestone handoff packet written,
- actual Codex thread-tool discovery and next thread create/handoff before the
  M3 Goal is closed.
