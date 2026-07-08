# Local Native Post-v1 #115 - Malformed Import Recovery UI

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/115

## Objective

Add the smallest native recovery surface for malformed imported state:
favorites, albums, settings, and explicit browser localStorage export JSON.

The native app should keep running, fall back to safe empty/default state, and
show enough recovery guidance for a user or headless verifier to understand
what to repair or regenerate.

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
- `tasks/local-native-post-v1-api-error-parity/task.md`
- `tasks/local-native-m20/task.md`
- `tasks/local-native-m5/browser-regression-matrix.md`
- GitHub issue #115
- GitHub issue #116
- GitHub PR #120
- GitHub milestone #26
- GitHub issues #97-#118 as context only

## Implementation

- `NativeStateBridge` records recoverable warnings instead of silently hiding
  malformed JSON.
- `NativeImageStore.ImportProjectState` stores warnings in native SQLite
  settings:
  - `import_warning_count`
  - `import_warnings_json`
  - `import_recovery_summary`
  - `browser_settings_imported`
  - `browser_state_export_imported`
- Malformed browser export import skips that source without clearing any
  previous valid `browser_state` rows.
- `NativeHeadlessRunner.RunImport` prints warning lines and a `warnings=`
  count.
- `--headless-malformed-import-smoke` verifies malformed favorites, albums,
  settings, and browser export using a synthetic project root under ignored
  `.cache/native-malformed-import-smoke/**`.
- `MainForm` shows warning count in the state label and recovery text after
  Import; Settings also shows warning count and recovery summary.

## Verification Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-malformed-import-smoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --check
```

Browser commands are baseline preservation only. Native acceptance for #115 is
the native malformed-import smoke plus existing native import/UI smoke. The
malformed smoke first imports one valid browser-state export, then verifies the
malformed export path reports `browserStatePreserved=true`.

## Closeout Requirements

Before this Goal can close:

1. Record the #115 outcome in GitHub.
2. Update SQLite job #234.
3. Send Agmsg pointers and inspect the trace.
4. Classify advice as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or
   `NEEDS_HUMAN`.
5. Create or hand off the next actual Codex thread if more native work remains.
6. Do not call the Goal complete until GitHub, SQLite, Agmsg, advice
   classification, and thread handoff are reflected.
