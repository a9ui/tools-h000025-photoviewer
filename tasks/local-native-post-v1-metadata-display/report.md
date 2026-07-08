# Local Native Post-v1 #107 Metadata Display First Slice

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/107

## Decision

Decision:
`ADOPT_NATIVE_PNG_PARAMETERS_METADATA_DISPLAY_FIRST_SLICE`.

Meaning:

- #107 can proceed as a bounded native-only display slice because browser source
  already defines the PNG `parameters` format and the native fixture path can
  carry deterministic metadata evidence.
- The first slice displays prompt, negative prompt, and compact settings
  summary. Copy metadata remains #108, prompt tag actions remain #109, and
  search chip/tag-style UI remains #110.
- Browser PhotoViewer remains untouched; `src/**` stays empty in the diff.
- `scripts/**`, H000033, deployment, automatic enhancement workers, and
  cache/state deletion remain out of scope.

## Implemented

- Added a bounded native PNG metadata reader that scans PNG chunks before IDAT
  and extracts `tEXt` keyword `parameters`.
- Extended native image rows with prompt, negative prompt, settings summary,
  raw parameters, and metadata-checked state.
- Added deterministic `parameters` metadata to `m2-fixture-1.png` during
  native fixture preparation.
- Displayed metadata in:
  - right-preview details label;
  - detail modal metadata label.
- Extended native UI smoke with `metadataDisplay=true`.

## Verification

Commands run:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --name-only -- scripts
git diff --check
corepack pnpm typecheck
sqlite3 .\.cache\native\photoviewer-native.sqlite "SELECT filename, prompt, negative_prompt, metadata_settings_summary, metadata_checked FROM images WHERE filename='m2-fixture-1.png';"
```

Observed results so far:

- `dotnet build` passed with 0 warnings and 0 errors.
- `-PrepareFixture` passed and generated only ignored `.cache/**`
  fixture/state files in this worktree.
- `-HeadlessUiSmoke` passed with `metadataDisplay=true`,
  `previewLoaded=true`, `detailModal=true`, `detailOpenExternal=true`,
  `bulkFavoriteSet=true`, `bulkFavoriteClear=true`, and
  `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --check` passed.
- `corepack pnpm typecheck` passed.
- The native SQLite fixture row for `m2-fixture-1.png` stored the deterministic
  prompt, negative prompt, settings summary, and `metadata_checked=1`.

## Advice Classification

- `ADOPT`: bounded native PNG `parameters` reader for display metadata.
- `ADOPT`: deterministic metadata fixture and UI smoke evidence.
- `ADOPT`: right-preview and detail-modal display for prompt, negative prompt,
  and compact settings summary.
- `PARTIAL_ADOPT`: settings are displayed as a compact summary; full structured
  copy/export remains #108.
- `REJECT`: browser app changes, `src/**`, `scripts/**`, deployment, H000033,
  automatic enhancement workers, cache/state deletion, and broad metadata/tag
  workflows in this lane.
- `DEFER`: #108 copy metadata, #109 prompt tag actions, #110 search chips,
  #97/#98 enhancement UI, #102 folder range, #104 bulk open, #105/#106
  destructive flows, and #118 deeper polish.
- `NEEDS_HUMAN`: none for this #107 first slice.
