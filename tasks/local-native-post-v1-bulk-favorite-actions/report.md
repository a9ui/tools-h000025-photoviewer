# Local Native Post-v1 #103 Bulk Favorite Actions

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/103

## Decision

Decision:
`ADOPT_NATIVE_MULTI_SELECTION_FAVORITE_LEVEL_APPLICATION`.

Meaning:

- #103 is the next bounded native backlog item after #117 Row25 because it is
  non-destructive, already has native multi-selection and single-favorite
  primitives, and does not compete with #97/#98, #102, #105/#106, or #118.
- The first slice reuses the existing favorite level control and keyboard
  shortcuts for multi-selection instead of adding a new batch workflow surface.
- Browser PhotoViewer remains untouched; `src/**` stays empty in the diff.

## Implemented

- Multi-selected images now receive the same favorite level when the existing
  favorite level control or `Ctrl+Up` / `Ctrl+Down` shortcut changes favorite
  level.
- Single-image favorite behavior and detail-modal favorite callbacks still use
  the same path and continue to select the updated image when visible.
- The native UI smoke now verifies:
  - `bulkFavoriteSet=true`
  - `bulkFavoriteClear=true`
  - existing `multiSelection=true`
  - existing favorite filter and keyboard favorite behavior still pass.

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
```

Observed results:

- `dotnet build` passed with 0 warnings and 0 errors.
- `-PrepareFixture` passed in the worktree and generated only ignored
  `.cache/**` fixture/state files for verification.
- `-HeadlessUiSmoke` passed with `bulkFavoriteSet=true`,
  `bulkFavoriteClear=true`, `multiSelection=true`,
  `keyboardFavorite=true`, `favoriteFilterCounts=true`,
  `favoriteLevelFilter=true`, and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --check` passed.
- `corepack pnpm typecheck` passed.

## Advice Classification

- `ADOPT`: #103 bulk favorite level application through the existing favorite
  control and shortcuts.
- `ADOPT`: native UI smoke coverage for both bulk set and bulk clear.
- `PARTIAL_ADOPT`: keep richer batch workflows out of this first slice.
- `REJECT`: browser app changes, `src/**`, deployment, H000033, automatic
  enhancement workers, cache/state deletion, and broad queue/output UI.
- `DEFER`: #97/#98 enhancement UI, #102 folder range, #105/#106 destructive
  flows, #107 metadata display, and #118 deeper polish to their own issues.
- `NEEDS_HUMAN`: none for this #103 first slice.
