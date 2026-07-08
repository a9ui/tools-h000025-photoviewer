# Local Native Post-v1 #116 - API/Error Parity Matrix

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/116

## Objective

Build a bounded matrix that maps existing browser API/error surfaces to native
UI/headless equivalents, then split actual implementation into the smallest
safe follow-up rows.

## Guardrails

- No Linear.
- Do not touch H000033.
- Do not deploy.
- Do not modify `src/**`.
- Do not start automatic enhancement workers.
- Do not delete existing cache/state assets.
- Keep changes under `local-native/**`, `docs/local-native/**`, or `tasks/**`.
- `scripts/**` edits require explicit approval in this thread because they are
  outside the requested isolation boundary.

## Source Packet

Read in full before planning or editing:

- `AGENTS.md`
- `PROJECT.md`
- `DESIGN.md`
- `project.toml`
- `START_HERE.md`
- `docs/operations-log.md`
- `local-native/README.md`
- `docs/local-native/native-intent-source.md`
- `docs/local-native/m20-verification.md`
- `docs/local-native/m19-verification.md`
- `tasks/local-native-m20/task.md`
- `tasks/local-native-m5/browser-regression-matrix.md`
- GitHub issue #116
- GitHub PR #119
- GitHub milestones #25 and #26
- GitHub issues #97-#118 as context only

## Deliverable

Primary deliverable:

- `docs/local-native/api-error-parity-matrix.md`

Optional direct implementation:

- None in this slice unless the matrix finds a tiny `local-native/**` error
  equivalent that is both required for #116 and safe to verify without touching
  `src/**`, `scripts/**`, enhancement workers, cache/state deletion, or
  deployment.

## Verification Commands

Baseline:

```powershell
git status --short --branch
git rev-parse origin/main
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
git diff --name-only -- src
git diff --check
```

Existing native negative evidence:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-scan .\.cache\native-missing-folder-for-116
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-search .\.cache\native-missing-folder-for-116 fixture
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-cache-compat .\.cache\native-missing-folder-for-116
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-ui-smoke .\.cache\native-missing-folder-for-116 --search fixture
```

Do not run multiple native headless commands in parallel against the same
worktree unless the project has already been built and no command can trigger a
concurrent `dotnet` build. Parallel `dotnet run` can contend on `obj/Debug`.

## Known Findings From The Matrix

- Native scan/search/cache/UI headless missing-folder checks already emit
  explicit `error=folder-not-found` messages and direct `dotnet run --no-build`
  returns non-zero.
- The documented wrapper `scripts/start-local-native.ps1` currently prints the
  same native error but returns 0 because it does not propagate the native exit
  code. This is a verifier-routing gap, but `scripts/**` is outside this
  issue's allowed edit scope.
- Native external open currently calls `Process.Start` without a UI status
  catch. This is a small `local-native/**` fix candidate, but it is not required
  to complete the matrix-first #116 slice.
- Malformed import recovery UI, delete confirmation, bulk destructive flows,
  enhancement queue/output management, complete `pvu_*` migration, and UI
  screenshot polish are already represented by #97/#98/#105/#106/#115/#117/#118.

## Closeout Requirements

Before this Goal can close:

1. Record the #116 outcome in GitHub.
2. Update SQLite job #233.
3. Send Agmsg pointers and inspect the trace.
4. Classify advice as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or
   `NEEDS_HUMAN`.
5. Create or hand off the next actual Codex thread if more native work remains.
6. Do not call the Goal complete until GitHub, SQLite, Agmsg, advice
   classification, and thread handoff are reflected.

