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
choices. The eighth slice migrated explicit browser `pvu_view.sortBy` into
native `sort_mode` on first import only for values with matching native
semantics. Browser ascending sort directions and `randomSeed` remain deferred
because the current native sort surface does not persist equivalent direction
or seed state. The ninth slice migrated explicit browser
`pvu_view.hiddenFolders` into native `hidden_folder_buckets` on first import,
mapping browser folder keys through the exported `pvu_last_dir_set` /
`pvu_recent_dirs` roots while preserving later native hidden-folder choices.
The tenth slice recorded explicit browser `pvu_seen_images` in #117: native
import upserts browser seen-image rows into `seen_images`, and the row added
pvu-state smoke/migration trace coverage while preserving existing native seen
rows.

The eleventh slice migrated explicit browser `pvu_view.folderSortBy` into
native `folder_sort_mode` on first import for matching folder-bucket sort
semantics, while preserving later native folder-sort choices. #102 folder
range-selection behavior stays separate.

The twelfth slice formally recorded browser marker-only keys:
`pvu_legacy_imported` and `pvu_server_legacy_imported` are raw-mirrored for
traceability but rejected as native migration targets.

This continuation formally records Row 13 for the browser-only performance
overlay flag: `pvu_perf_enabled` is raw-mirrored for traceability, but it is
deferred as a native migration target because the native app has no accepted
user-facing performance-overlay setting yet.

This continuation formally records Row 14 for browser `pvu_scroll_memory`:
it is raw-mirrored for traceability but deferred as a native migration target
because the browser per-view scroll map is not equivalent to native
`last_selected_image` / `last_visible_index` restore.

This Row15 continuation formally records the non-evidenced browser favorite
levels key: `pvu_fav_levels` is raw-mirrored for traceability when an explicit
export contains it, but it is deferred as a native migration target because
current browser code does not persist this key and there is no accepted native
conflict policy.

This continuation formally records Row 16 for browser `pvu_pinned_tabs`: it is
raw-mirrored for traceability if an explicit export contains it, but it is
deferred as a native migration target because current browser pinned preview
tabs belong to #99/#100 and native has no accepted tab/pin/restore state
contract yet.

This continuation formally records Row 17 for browser `pvu_recent_albums`: it
is raw-mirrored for traceability if an explicit export contains it, but it is
deferred as a native migration target because album import exists while native
has no accepted recent-album UI selection/restore contract yet.

This Row18 continuation formally records browser display details inside
`pvu_view`: `aspectMode`, `displayStyle`, and `columns` are raw-mirrored for
traceability in `browser_pvu_view`, but they are deferred as native migration
targets because compact/poster/aspect/fixed-column display behavior belongs to
#111/#112 until native has an accepted display/aspect persistence contract.

This Row19 continuation formally records browser `pvu_enhance_settings`: it is
raw-mirrored for traceability if an explicit export contains it, but it is
deferred as a native migration target because explicit native enhancement
queue/settings UI remains owned by #97/#98 and ordinary native browsing must
not start enhancement workers.

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
- `tasks/local-native-post-v1-pvu-state-persistence/task.md`
- `tasks/local-native-m20/task.md`
- `tasks/local-native-m5/browser-regression-matrix.md`
- GitHub issue #117
- GitHub PR #144
- GitHub PR #142
- GitHub PR #141
- GitHub PR #140
- GitHub PR #139
- GitHub PR #138
- GitHub PR #137
- GitHub issue #115
- GitHub issue #116
- GitHub PR #134
- GitHub PR #133
- GitHub PR #131
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
- Keep the existing bounded migration for `pvu_view.thumbSize`:
  - browser positive integer `thumbSize` -> native `thumbnail_size`;
  - imported values are clamped to the current native UI range `64..192`;
  - existing native `thumbnail_size` is not overwritten;
  - malformed thumbnail-size values are recoverable warnings;
  - broader display-style, aspect, columns, and pinned-tab state remains
    deferred to the existing post-v1 rows.
- Keep the existing bounded migration for `pvu_view.sortBy`:
  - browser `newest` -> native `sort_mode=Modified`;
  - browser `created-newest` -> native `sort_mode=Created`;
  - browser `name` -> native `sort_mode=Name`;
  - browser `random` -> native `sort_mode=Random`;
  - existing native `sort_mode` is not overwritten;
  - malformed or unsupported sort values are recoverable warnings;
  - browser `oldest`, `created-oldest`, and `randomSeed` remain deferred
    until native has an accepted persisted direction/seed design.
- Keep the existing bounded migration for `pvu_view.hiddenFolders`:
  - browser folder keys -> native `hidden_folder_buckets` absolute folder
    paths;
  - folder keys are mapped only through exported browser roots from
    `pvu_last_dir_set` / `pvu_recent_dirs`;
  - existing native `hidden_folder_buckets` is not overwritten;
  - malformed folder keys, non-array values, and hidden-folder exports without
    roots are recoverable warnings;
- Keep the existing bounded import for `pvu_seen_images` in #117:
  - explicit browser seen-image entries are upserted into native `seen_images`
    with source `browser_export`;
  - existing native `seen_images` rows are preserved;
  - malformed seen-image JSON is a recoverable warning;
  - no broader scroll-memory, pinned-tab, enhancement, or display state is
    adopted in this row.
- Add a bounded migration for `pvu_view.folderSortBy`:
  - browser `name-asc` -> native `folder_sort_mode=NameAsc`;
  - browser `name-desc` -> native `folder_sort_mode=NameDesc`;
  - browser `count-desc` -> native `folder_sort_mode=CountDesc`;
  - browser `count-asc` -> native `folder_sort_mode=CountAsc`;
  - existing native `folder_sort_mode` is not overwritten;
  - malformed or unsupported folder sort values are recoverable warnings;
  - #102 folder range-selection behavior remains separate.
- Formally reject marker-only browser migration keys in #117:
  - `pvu_legacy_imported` and `pvu_server_legacy_imported` are kept as raw
    browser mirrors under `browser_pvu_legacy_imported` and
    `browser_pvu_server_legacy_imported`;
  - they do not create native `legacy_imported` settings;
  - they are not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count.
- Formally defer browser performance instrumentation in #117:
  - `pvu_perf_enabled` is kept as a raw browser mirror under
    `browser_pvu_perf_enabled`;
  - it does not create a native `perf_enabled` setting;
  - it is not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count.
- Formally defer browser scroll memory in #117:
  - `pvu_scroll_memory` is kept as a raw browser mirror under
    `browser_pvu_scroll_memory`;
  - it does not create a native `scroll_memory` setting;
  - it is not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count;
  - native gallery-state restore through `last_selected_image` and
    `last_visible_index` remains separate from browser scroll-memory import.
- Formally defer non-evidenced browser favorite-level state in #117:
  - `pvu_fav_levels` is kept as a raw browser mirror under
    `browser_pvu_fav_levels` when an explicit export contains it;
  - it does not create native `fav_levels` or `favorite_filter_level`
    settings;
  - it is not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count.
- Formally defer browser pinned-tab state in #117:
  - `pvu_pinned_tabs` is kept as a raw browser mirror under
    `browser_pvu_pinned_tabs` when an explicit export contains it;
  - it does not create native `pinned_tabs`, `pinned_preview_tabs`, or
    `preview_tabs` settings;
  - it is not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count.
- Formally defer browser recent-album state in #117:
  - `pvu_recent_albums` is kept as a raw browser mirror under
    `browser_pvu_recent_albums` when an explicit export contains it;
  - it does not create native `recent_albums`, `recent_album`, or
    `recent_album_ids` settings;
  - it is not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count.
- Formally defer browser display details in #117:
  - `pvu_view.aspectMode`, `pvu_view.displayStyle`, and `pvu_view.columns`
    are kept only inside the raw `browser_pvu_view` mirror when an explicit
    export contains them;
  - native does not create `aspect_mode`, `display_style`, `columns`, or
    `display_columns` settings;
  - they are not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count;
  - #111 compact/poster display modes and #112 aspect controls remain
    separate.
- Formally defer browser enhancement settings in #117:
  - `pvu_enhance_settings` is kept as a raw browser mirror under
    `browser_pvu_enhance_settings` when an explicit export contains it;
  - it does not create native `enhance_settings`, `enhancement_settings`, or
    `enhancement_queue_settings` settings;
  - it is not recorded in `pvu_state_migrations`, so
    `pvu_state_migration_count` remains at the Row 11 count;
  - explicit native enhancement queue/settings UI remains in #97/#98 and this
    row must not start automatic workers.
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
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
corepack pnpm typecheck
git diff --name-only -- src
git diff --name-only -- scripts
git diff --name-only -- H000033
git diff --check
```

Browser commands are baseline preservation only. Native acceptance for #117 is
the dedicated pvu-state smoke plus existing import/UI smoke. The new smoke must
report `pvuSeenImagesMigrated=true`, `pvuLegacyMarkersRejected=true`,
`seenMirrorStored=true`, `markerMirrorStored=true`,
`pvuPerfFlagDeferred=true`, `perfMirrorStored=true`,
`pvuScrollMemoryDeferred=true`, `scrollMemoryMirrorStored=true`,
`pvuFavLevelsDeferred=true`, `favLevelsMirrorStored=true`,
`pvuPinnedTabsDeferred=true`, `pinnedTabsMirrorStored=true`,
`pvuRecentAlbumsDeferred=true`, `recentAlbumsMirrorStored=true`,
`pvuDisplayDetailsDeferred=true`, `displayDetailsMirrorStored=true`,
`pvuEnhanceSettingsDeferred=true`, `enhanceSettingsMirrorStored=true`,
`nativeSeenImagesPreserved=true`, `malformedSeenImagesWarning=true`,
`nativeSeenImagesStillPreserved=true`, `pvuFolderSortModeMigrated=true`,
`nativeFolderSortModePreserved=true`, `unsupportedFolderSortWarning=true`,
`nativeFolderSortModeStillPreserved=true`, `browserStateKeys=11`,
`malformedWarnings=10`, and
`browserRuntime=false localHttpServer=false nodeRuntime=false`.

## Current Verification

Recorded on 2026-07-09 in branch
`codex/h25-117-row18-pvu-enhance-settings` rebased by merge onto
`origin/main` after PR #149:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `--headless-pvu-state-smoke` passed with
  `pvuDisplayDetailsDeferred=true`, `displayDetailsMirrorStored=true`,
  `pvuEnhanceSettingsDeferred=true`, `enhanceSettingsMirrorStored=true`,
  `pvuRecentAlbumsDeferred=true`, `recentAlbumsMirrorStored=true`,
  `pvuPinnedTabsDeferred=true`, `pinnedTabsMirrorStored=true`,
  `pvuFavLevelsDeferred=true`, `favLevelsMirrorStored=true`,
  `pvuScrollMemoryDeferred=true`, `scrollMemoryMirrorStored=true`,
  `pvuPerfFlagDeferred=true`, `perfMirrorStored=true`,
  `pvuLegacyMarkersRejected=true`, `markerMirrorStored=true`,
  `migrationRecorded=true`, `pvu_state_migration_count=11` by smoke
  contract,
  `pvuFolderSortModeMigrated=true`, `pvuSeenImagesMigrated=true`,
  `seenMirrorStored=true`,
  `nativeFolderSortModePreserved=true`, `nativeSeenImagesPreserved=true`,
  `nativeFolderSortModeStillPreserved=true`,
  `nativeSeenImagesStillPreserved=true`, `browserStateKeys=11`,
  `firstWarnings=0`, `secondWarnings=0`, `malformedWarnings=10`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `-PrepareFixture` passed using ignored fixture/cache state while preserving
  existing cache/state assets.
- `--headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=7`, `seenImages=0`, `settings=30`, `images=0`, and
  `warnings=0`.
- `-HeadlessSeenSmoke` passed with `importedSeen=true`,
  `nativeInitiallyUnseen=true`, `nativeSeenPersisted=true`,
  `importedStillSeen=true`, `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `-HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture` passed
  with `gridToggle=true`, `folderSortMode=true`, `thumbnailSize=true`,
  `enhancedOnlyFilter=true`, `favoriteLevelFilter=true`, `sortName=true`,
  `randomReshuffle=true`, `browserStateKeys=7`, `settingsImported=true`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" . -g '!node_modules/**' -g '!.next/**' -g '!.cache/**'`
  returned no conflict markers.
- `git diff --check` passed.

## Closeout Requirements

Before this Goal can close:

1. Record the #117 outcome in GitHub.
2. Update SQLite job #257 and create/update the next row if more #117 native
   queue work remains.
3. Send Agmsg pointers and inspect the trace.
4. Classify advice as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or
   `NEEDS_HUMAN`.
5. Create or hand off the next actual Codex thread if more native work remains.
6. Do not call the Goal complete until GitHub, SQLite, Agmsg, advice
   classification, and thread handoff are reflected.
