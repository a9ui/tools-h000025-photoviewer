# Local Native Post-v1 #117 - Bounded pvu State Persistence Migration

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/117

## Objective

Build the #117 plan first, then advance only the smallest safe `pvu_*`
persistence row under `local-native/**`.

The previous slices migrated explicit browser `pvu_view.viewMode` into native
`view_mode`, explicit browser `pvu_enhanced_only` into native
`enhanced_only_filter`, and explicit browser `pvu_fav_only` /
`pvu_unfav_only` into native `favorite_filter` on first import, while
preserving later native user choices. The fourth slice migrated explicit
browser `pvu_view.dateFrom` / `dateTo` into native `date_filter`, `date_from`,
and `date_to` on first import. The fifth slice migrated explicit browser
`pvu_last_dir_set` / `pvu_recent_dirs` into native `recent_folder_set` and
`recent_folder`, while preserving later native scan/user choices.
The sixth slice migrated explicit browser `pvu_view.rightPanelOpen` /
`rightPanelWidth` into native `preview_visible` and
`preview_splitter_distance` on first import, while preserving later native
right-preview choices. The seventh slice migrated explicit browser
`pvu_view.thumbSize` into native `thumbnail_size` on first import, clamped to
the current native UI range, while preserving later native thumbnail-size
choices.

This continuation migrates explicit browser `pvu_view.sortBy` into native
`sort_mode` on first import only for values with matching native semantics.
Browser ascending sort directions and `randomSeed` remain deferred because the
current native sort surface does not persist equivalent direction or seed
state.

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
- GitHub PR #132
- GitHub PR #130
- GitHub PR #129
- GitHub PR #127
- GitHub PR #123
- GitHub PR #122
- GitHub PR #120
- GitHub milestone #26
- GitHub issues #97-#118 as context only

## Implementation

- Keep `browser_state` raw import behavior unchanged.
- Keep the existing bounded migration for `pvu_view.viewMode`:
  - browser `grid` -> native `view_mode=grid`;
  - browser `list` -> native `view_mode=details`;
  - malformed `pvu_view` JSON is a recoverable warning;
  - existing native `view_mode` is not overwritten.
- Keep the existing bounded migration for `pvu_enhanced_only`:
  - browser truthy values (`1`, `true`) -> native
    `enhanced_only_filter=1`;
  - browser falsy values (`0`, `false`) -> native
    `enhanced_only_filter=0`;
  - malformed boolean values are recoverable warnings;
  - existing native `enhanced_only_filter` is not overwritten.
- Keep the existing bounded migration for `pvu_fav_only` / `pvu_unfav_only`:
  - browser favorite-only truthy values (`1`, `true`) -> native
    `favorite_filter=favorites`;
  - browser unrated-only truthy values with favorite-only off -> native
    `favorite_filter=unrated`;
  - browser cleared values -> native `favorite_filter=all`;
  - if both are truthy, favorite-only wins to match browser conflict behavior;
  - malformed boolean values are recoverable warnings;
  - existing native `favorite_filter` is not overwritten.
- Keep the existing bounded migration for `pvu_view.dateFrom` / `dateTo`:
  - browser `YYYY-MM-DD` strings -> native `date_from` / `date_to`;
  - one-sided browser date ranges are preserved as one-sided native ranges;
  - native `date_filter` is set to `custom` when a non-empty browser date
    range is imported;
  - malformed date strings are recoverable warnings;
  - existing native `date_filter`, `date_from`, or `date_to` is not
    overwritten.
- Keep the existing bounded migration for `pvu_last_dir_set` / `pvu_recent_dirs`:
  - browser `pvu_last_dir_set` folder-set string -> native
    `recent_folder_set` and `recent_folder`;
  - browser `pvu_recent_dirs` JSON array is a fallback when
    `pvu_last_dir_set` is absent or empty;
  - existing native `recent_folder_set`, `recent_folder`, or scan roots are
    not overwritten;
  - malformed recent-dirs values are recoverable warnings.
- Keep the existing bounded migration for `pvu_view.rightPanelOpen` /
  `rightPanelWidth`:
  - browser `rightPanelOpen=true` / `false` -> native
    `preview_visible=1` / `0`;
  - browser `rightPanelWidth=N` -> native `preview_splitter_distance` using
    the existing 1280px desktop layout conversion
    `1280 - clamp(N, 240, 900)`;
  - malformed right-preview values are recoverable warnings;
  - existing native `preview_visible` or `preview_splitter_distance` is not
    overwritten.
- Add a bounded migration for `pvu_view.thumbSize`:
  - browser positive integer `thumbSize` -> native `thumbnail_size`;
  - imported values are clamped to the current native UI range `64..192`;
  - existing native `thumbnail_size` is not overwritten;
  - malformed thumbnail-size values are recoverable warnings;
  - broader display-style, aspect, columns, and pinned-tab state remains
    deferred to the existing post-v1 rows.
- Add a bounded migration for `pvu_view.sortBy`:
  - browser `newest` -> native `sort_mode=Modified`;
  - browser `created-newest` -> native `sort_mode=Created`;
  - browser `name` -> native `sort_mode=Name`;
  - browser `random` -> native `sort_mode=Random`;
  - existing native `sort_mode` is not overwritten;
  - malformed or unsupported sort values are recoverable warnings;
  - browser `oldest`, `created-oldest`, and `randomSeed` remain deferred
    until native has an accepted persisted direction/seed design.
- Extend `--headless-pvu-state-smoke` using a synthetic project root under
  ignored `.cache/native-pvu-state-smoke/**`.
- Keep `NativeFixtureBuilder` browser export fixtures explicit and
  browser-shaped; do not read Chrome profile storage.
- Record the remaining `pvu_*` key split in
  `docs/local-native/pvu-state-persistence-migration.md`.

## Verification Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
corepack pnpm typecheck
git diff --name-only -- src
git diff --name-only -- scripts
git diff --name-only -- H000033
git diff --check
```

Browser commands are baseline preservation only. Native acceptance for #117 is
the dedicated pvu-state smoke plus existing import/UI smoke. The new smoke
must report `browserRuntime=false localHttpServer=false nodeRuntime=false`.

## Current Verification

Recorded on 2026-07-08 in branch `codex/h25-117-pvu-row8-sort-mode`
based on `origin/main` `ffe9c51d6e98066c772e04758100f6bc5d2de204`
after PR #133:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `--headless-pvu-state-smoke` passed with
  `pvuViewModeMigrated=true`, `pvuEnhancedOnlyMigrated=true`,
  `pvuFavoriteFilterMigrated=true`, `pvuDateRangeMigrated=true`,
  `pvuThumbnailSizeMigrated=true`, `pvuThumbnailSizeClamped=true`,
  `pvuSortModeMigrated=true`,
  `pvuRecentFoldersMigrated=true`, `pvuRightPreviewMigrated=true`,
  `migrationRecorded=true`, `browserMirrorStored=true`,
  `enhancedMirrorStored=true`, `favoriteMirrorStored=true`,
  `recentMirrorStored=true`,
  `nativeViewModePreserved=true`, `nativeEnhancedOnlyPreserved=true`,
  `nativeFavoriteFilterPreserved=true`, `nativeDateRangePreserved=true`,
  `nativeThumbnailSizePreserved=true`, `nativeSortModePreserved=true`,
  `nativeRecentFolderSetPreserved=true`,
  `nativeRightPreviewPreserved=true`,
  `malformedEnhancedOnlyWarning=true`, `malformedFavoriteFilterWarning=true`,
  `malformedDateRangeWarning=true`, `malformedThumbnailSizeWarning=true`,
  `unsupportedSortModeWarning=true`,
  `malformedRecentDirsWarning=true`, `malformedRightPreviewWarning=true`,
  `nativeEnhancedOnlyStillPreserved=true`,
  `nativeFavoriteFilterStillPreserved=true`,
  `nativeDateRangeStillPreserved=true`,
  `nativeThumbnailSizeStillPreserved=true`,
  `nativeSortModeStillPreserved=true`,
  `nativeRecentFolderSetStillPreserved=true`,
  `nativeRightPreviewStillPreserved=true`, `browserStateKeys=5`,
  `firstWarnings=0`, `secondWarnings=0`, and `malformedWarnings=7`.
- `-PrepareFixture` passed and created only ignored fixture/cache state in this
  clean worktree.
- `--headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `seenImages=0`, `settings=28`, `images=0`,
  `warnings=0`, and no browser runtime.
- `-HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture` passed
  with `gridToggle=true`, `thumbnailSize=true`, `enhancedOnlyFilter=true`,
  `browserStateKeys=6`, and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `git diff --check` passed.
- `corepack pnpm typecheck` passed.

## Closeout Requirements

Before this Goal can close:

1. Record the #117 outcome in GitHub.
2. Update SQLite job #247 and create/update the next row if more #117 native
   queue work remains.
3. Send Agmsg pointers and inspect the trace.
4. Classify advice as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or
   `NEEDS_HUMAN`.
5. Create or hand off the next actual Codex thread if more native work remains.
6. Do not call the Goal complete until GitHub, SQLite, Agmsg, advice
   classification, and thread handoff are reflected.
