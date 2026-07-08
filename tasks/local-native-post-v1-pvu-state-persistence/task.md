# Local Native Post-v1 #117 - Bounded pvu State Persistence Migration

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/117

## Objective

Build the #117 plan first, then advance only the smallest safe `pvu_*`
persistence row under `local-native/**`.

This slice migrates explicit browser `pvu_view.viewMode` into native
`view_mode` on first import, while preserving later native user choices.

## Guardrails

- No Linear.
- Do not touch H000033.
- Do not deploy.
- Do not modify `src/**`.
- Do not start automatic enhancement workers.
- Do not delete existing cache/state assets.
- Keep implementation under `local-native/**` and documentation under
  `docs/local-native/**` / `tasks/**`.
- Do not edit `scripts/**` in this slice.
- Native reads browser `pvu_*` only from an explicit export JSON file, never
  from Chrome profile storage.

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
- `docs/local-native/api-error-parity-matrix.md`
- `docs/local-native/malformed-import-recovery.md`
- `docs/local-native/state-migration-map.md`
- `tasks/local-native-post-v1-malformed-import-recovery/task.md`
- `tasks/local-native-post-v1-api-error-parity/task.md`
- `tasks/local-native-m20/task.md`
- `tasks/local-native-m5/browser-regression-matrix.md`
- GitHub issue #117
- GitHub issue #115
- GitHub issue #116
- GitHub PR #121
- GitHub PR #120
- GitHub milestone #26
- GitHub issues #97-#118 as context only

## Implementation

- Keep `browser_state` raw import behavior unchanged.
- Add a bounded migration for `pvu_view.viewMode`:
  - browser `grid` -> native `view_mode=grid`;
  - browser `list` -> native `view_mode=details`;
  - malformed `pvu_view` JSON is a recoverable warning;
  - existing native `view_mode` is not overwritten.
- Add `--headless-pvu-state-smoke` using a synthetic project root under
  ignored `.cache/native-pvu-state-smoke/**`.
- Update `NativeFixtureBuilder` so newly generated browser export fixtures use
  a browser-shaped `pvu_view` object.
- Record the remaining `pvu_*` key split in
  `docs/local-native/pvu-state-persistence-migration.md`.

## Verification Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --check
```

Browser commands are baseline preservation only. Native acceptance for #117 is
the dedicated pvu-state smoke plus existing import/UI smoke. The new smoke
must report `browserRuntime=false localHttpServer=false nodeRuntime=false`.

## Closeout Requirements

Before this Goal can close:

1. Record the #117 outcome in GitHub.
2. Update SQLite job #235.
3. Send Agmsg pointers and inspect the trace.
4. Classify advice as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or
   `NEEDS_HUMAN`.
5. Create or hand off the next actual Codex thread if more native work remains.
6. Do not call the Goal complete until GitHub, SQLite, Agmsg, advice
   classification, and thread handoff are reflected.
