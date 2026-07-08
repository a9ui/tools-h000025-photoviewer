# Local Native #115 Malformed Import Recovery

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/115

## Decision

Decision:
`ADOPT_NATIVE_IMPORT_WARNING_RECOVERY_WITHOUT_BROWSER_APP_OR_SCRIPT_CHANGES`.

Meaning:

- Native import remains tolerant of malformed state.
- Malformed favorites, albums, settings, and explicit browser localStorage
  export JSON no longer fail silently.
- Recovery guidance is visible in native headless output, the main native state
  summary, the Import status text, and native Settings.
- Existing browser PhotoViewer workflows remain untouched.
- No `src/**`, `scripts/**`, deployment, H000033, automatic enhancement worker,
  or cache/state deletion is part of this slice.

## User-Facing Behavior

When a malformed import source is found:

- the native app continues running;
- the malformed source is skipped or falls back to defaults;
- the user sees which source failed and what to repair or regenerate;
- valid sources from the same import still load;
- malformed browser localStorage export does not clear prior valid
  `browser_state` rows.

Warnings are stored in native SQLite settings:

- `import_warning_count`
- `import_warnings_json`
- `import_recovery_summary`
- `browser_settings_imported`
- `browser_state_export_imported`

## Native Evidence

The dedicated smoke uses a synthetic project root under ignored
`.cache/native-malformed-import-smoke/**` so it does not overwrite real user
state.

Command:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-malformed-import-smoke
```

Expected result:

- warnings for `favorites`, `albums`, `settings`, and `browser-state-export`;
- `recoveryStored=true`;
- `warningsStored=true`;
- `settingsFallback=true`;
- `browserExportSkipped=true`;
- `browserStatePreserved=true`;
- `safeEmptyFallback=true`;
- `browserRuntime=false localHttpServer=false nodeRuntime=false`.

## Verification On 2026-07-08

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `--headless-malformed-import-smoke` passed with 4 warnings and all recovery
  checks true, including `browserStatePreserved=true`.
- `-PrepareFixture` passed and created only missing ignored fixture/cache state.
- Valid `--headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `browserStateKeys=5` and `warnings=0`.
- `-HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture` passed
  with existing UI parity checks and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no files.

## Follow-Up Classification

- `ADOPT`: native import warning recovery for #115.
- `ADOPT`: synthetic malformed-import smoke under ignored `.cache/**` to avoid
  mutating real user state.
- `REJECT`: changing the browser app or adding a browser HTTP compatibility
  layer for this issue.
- `DEFER`: complete `pvu_*` persistence migration to #117.
- `DEFER`: native UI screenshot/polish review to #118.
- `NEEDS_HUMAN`: none.
