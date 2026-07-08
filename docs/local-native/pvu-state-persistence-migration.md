# Local Native Post-v1 #117 pvu State Persistence Migration

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/117

## Decision

Decision:
`DEFER_BROWSER_RECENT_ALBUMS_AFTER_PINNED_TABS`.

Meaning:

- #117 is broad by default, so this slice advances only one safe row:
  explicit classification of browser `pvu_recent_albums` after the
  pinned-tabs row.
- The previous accepted rows remain `pvu_view.viewMode` into native
  `view_mode`, `pvu_enhanced_only` into native `enhanced_only_filter`, and
  `pvu_fav_only` / `pvu_unfav_only` into native `favorite_filter`, and
  `pvu_view.dateFrom` / `dateTo` into native date settings, and
  `pvu_last_dir_set` / `pvu_recent_dirs` into native recent-folder settings,
  and `pvu_view.rightPanelOpen` / `rightPanelWidth` into native right-preview
  settings, `pvu_view.thumbSize` into native `thumbnail_size`,
  `pvu_view.sortBy` into native `sort_mode`, `pvu_view.hiddenFolders` into
  native `hidden_folder_buckets`, explicit browser `pvu_seen_images` into
  native `seen_images`, and `pvu_view.folderSortBy` into native
  `folder_sort_mode`.
- Native still reads browser `pvu_*` state only from an explicit JSON export
  file. It never reads Chrome profile storage directly.
- The migrations write native settings only when the target native setting
  does not exist yet, so later native user choices are not overwritten on every
  startup/import.
- Browser sort values are imported only where the current native sort surface
  has matching semantics: `newest` -> `Modified`, `created-newest` ->
  `Created`, `name` -> `Name`, and `random` -> `Random`.
- Browser ascending directions `oldest` / `created-oldest` and browser
  `randomSeed` remain deferred because the current native sort surface does
  not persist equivalent direction or seed state.
- Browser hidden folders are stored as browser folder keys, not native
  absolute folder paths. The previous accepted hidden-folders row converts only
  keys that can be mapped through the explicit exported `pvu_last_dir_set` /
  `pvu_recent_dirs` roots.
- Browser `pvu_seen_images` is additive. Explicit browser-export rows are
  upserted into native `seen_images` with source `browser_export`; existing
  native seen rows are preserved by the previous accepted row.
- Malformed `pvu_seen_images` is warning-only and does not overwrite or delete
  native seen state.
- Browser `folderSortBy` is now imported only for the existing folder-bucket
  sort semantics: `name-asc`, `name-desc`, `count-desc`, and `count-asc`.
  Folder range-selection semantics remain owned by #102.
- Browser `pvu_legacy_imported` and `pvu_server_legacy_imported` are
  one-time browser migration markers. They are raw-mirrored for traceability
  as `browser_pvu_legacy_imported` and
  `browser_pvu_server_legacy_imported`, but they do not create native
  `legacy_imported` settings and are not recorded in `pvu_state_migrations`.
- Browser `pvu_perf_enabled` is a browser performance instrumentation flag. It
  is raw-mirrored for traceability as `browser_pvu_perf_enabled`, but it does
  not create a native `perf_enabled` setting and is not recorded in
  `pvu_state_migrations`.
- Browser `pvu_scroll_memory` is a browser virtual-scroll memory map keyed by
  browser search/view state. Native already persists `last_selected_image` and
  `last_visible_index`, but that is not equivalent to importing the browser
  scroll map. `pvu_scroll_memory` is raw-mirrored for traceability as
  `browser_pvu_scroll_memory`, but native does not create a `scroll_memory`
  setting and does not record it in `pvu_state_migrations`.
- Browser `pvu_fav_levels` is listed as a possible browser-only key in older
  state maps, but current browser code does not persist this key. It is
  raw-mirrored for traceability as `browser_pvu_fav_levels` if an explicit
  export contains it, but it does not create native `fav_levels` or
  `favorite_filter_level` settings and is not recorded in
  `pvu_state_migrations`.
- Browser `pvu_pinned_tabs` is persisted by the current browser preview-tab
  UI, but native has no accepted preview-tab, pinned-tab, or restore-tab
  state contract yet. It is raw-mirrored for traceability as
  `browser_pvu_pinned_tabs` if an explicit export contains it, but it does not
  create native `pinned_tabs`, `pinned_preview_tabs`, or `preview_tabs`
  settings and is not recorded in `pvu_state_migrations`.
- Browser `pvu_recent_albums` is listed as a possible browser-only key in
  older state maps. Native imports album cache files, but there is no accepted
  native recent-album UI/state contract for this browser key. It is
  raw-mirrored for traceability as `browser_pvu_recent_albums` if an explicit
  export contains it, but it does not create native `recent_albums` or
  `recent_album` settings and is not recorded in `pvu_state_migrations`.
- Existing browser PhotoViewer workflows remain untouched.
- No `src/**`, `scripts/**`, deployment, H000033, automatic enhancement worker,
  or cache/state deletion is part of this slice.

## Implemented Row

| Browser state | Native target | Current result |
| --- | --- | --- |
| `pvu_view.viewMode=grid` | `native_settings.view_mode=grid` | `ADOPT`: imported on first native import when `view_mode` is absent. |
| `pvu_view.viewMode=list` | `native_settings.view_mode=details` | `ADOPT`: browser list maps to the native WinForms details/list view. |
| Existing native `view_mode` | Preserve native setting | `ADOPT`: import does not clobber a native user choice. |
| `pvu_enhanced_only=1` / `true` | `native_settings.enhanced_only_filter=1` | `ADOPT`: imported on first native import when `enhanced_only_filter` is absent. |
| `pvu_enhanced_only=0` / `false` | `native_settings.enhanced_only_filter=0` | `ADOPT`: browser cleared state maps to the native enhanced checkbox state. |
| Existing native `enhanced_only_filter` | Preserve native setting | `ADOPT`: import does not clobber a native user choice. |
| `pvu_fav_only=1` / `true` | `native_settings.favorite_filter=favorites` | `ADOPT`: imported on first native import when `favorite_filter` is absent. |
| `pvu_unfav_only=1` / `true` with favorite-only off | `native_settings.favorite_filter=unrated` | `ADOPT`: browser unrated-only maps to the native unrated filter. |
| `pvu_fav_only=0` and `pvu_unfav_only=0` | `native_settings.favorite_filter=all` | `ADOPT`: browser cleared state maps to no favorite filter. |
| Both favorite-only and unrated-only truthy | `native_settings.favorite_filter=favorites` | `ADOPT`: matches browser conflict behavior where favorite-only wins and unrated-only is cleared. |
| Existing native `favorite_filter` | Preserve native setting | `ADOPT`: import does not clobber a native user choice. |
| `pvu_view.dateFrom=YYYY-MM-DD` / `dateTo=YYYY-MM-DD` | `native_settings.date_filter=custom`, `date_from`, `date_to` | `ADOPT`: imported on first native import when native date settings are absent. |
| `pvu_view.dateFrom` only or `dateTo` only | matching one-sided native `date_from` / `date_to` with `date_filter=custom` | `ADOPT`: native manual date range already supports from-only and to-only filtering. |
| Existing native `date_filter` / `date_from` / `date_to` | Preserve native date settings | `ADOPT`: import does not clobber a native user choice. |
| Malformed `pvu_view.dateFrom` / `dateTo` | recoverable warning, no native date overwrite | `ADOPT`: invalid date strings are skipped with recovery guidance. |
| `pvu_last_dir_set` folder set | `native_settings.recent_folder_set`, `recent_folder` | `ADOPT`: imported on first native import when native recent folder state is absent. |
| `pvu_recent_dirs` JSON array | fallback source for `recent_folder_set` / `recent_folder` | `ADOPT`: first valid browser recent folder set is imported when `pvu_last_dir_set` is absent or empty. |
| Existing native `recent_folder_set` / `recent_folder` or scan roots | Preserve native recent state | `ADOPT`: import does not clobber later native scan/user choices. |
| Malformed `pvu_recent_dirs` | recoverable warning, no native recent overwrite | `ADOPT`: invalid recent-dirs state is skipped with recovery guidance. |
| `pvu_view.rightPanelOpen=true` / `false` | `native_settings.preview_visible=1` / `0` | `ADOPT`: imported on first native import when native preview visibility is absent. |
| `pvu_view.rightPanelWidth=N` | `native_settings.preview_splitter_distance=1280 - clamp(N, 240, 900)` | `ADOPT`: browser right-panel width maps to the native split container's left-panel distance for the 1280px desktop layout. |
| Existing native `preview_visible` / `preview_splitter_distance` | Preserve native right-preview settings | `ADOPT`: import does not clobber a native user choice. |
| Malformed `pvu_view.rightPanelOpen` / `rightPanelWidth` | recoverable warning, no native right-preview overwrite | `ADOPT`: invalid right-preview values are skipped with recovery guidance. |
| `pvu_view.thumbSize` positive integer | `native_settings.thumbnail_size` clamped to native 64-192 | `ADOPT`: imported on first native import when `thumbnail_size` is absent. |
| Existing native `thumbnail_size` | Preserve native thumbnail size | `ADOPT`: import does not clobber a native user choice. |
| Malformed `pvu_view.thumbSize` | recoverable warning, no native thumbnail overwrite | `ADOPT`: invalid thumbnail-size values are skipped with recovery guidance. |
| `pvu_view.sortBy=newest` | `native_settings.sort_mode=Modified` | `ADOPT`: imported on first native import when `sort_mode` is absent. |
| `pvu_view.sortBy=created-newest` | `native_settings.sort_mode=Created` | `ADOPT`: imported on first native import when `sort_mode` is absent. |
| `pvu_view.sortBy=name` | `native_settings.sort_mode=Name` | `ADOPT`: imported on first native import when `sort_mode` is absent. |
| `pvu_view.sortBy=random` | `native_settings.sort_mode=Random` | `ADOPT`: imports only random mode; browser `randomSeed` remains deferred. |
| Existing native `sort_mode` | Preserve native sort mode | `ADOPT`: import does not clobber a native user choice. |
| `pvu_view.sortBy=oldest` / `created-oldest` | warning-only, no native sort overwrite | `DEFER`: current native sort surface has no persisted ascending direction. |
| Malformed / unsupported `pvu_view.sortBy` | warning-only, no native sort overwrite | `ADOPT`: invalid sort values are skipped with recovery guidance. |
| `pvu_view.hiddenFolders` browser folder keys with exported roots | `native_settings.hidden_folder_buckets` absolute native folder paths | `ADOPT`: imported on first native import when hidden-folder state is absent. |
| Existing native `hidden_folder_buckets` | Preserve native hidden-folder state | `ADOPT`: import does not clobber a native user choice. |
| Malformed/rootless `pvu_view.hiddenFolders` | recoverable warning, no native hidden-folder overwrite | `ADOPT`: invalid folder keys, non-array values, and exports without roots are skipped with recovery guidance. |
| `pvu_seen_images` truthy entries | `seen_images` rows with source `browser_export` | `ADOPT`: explicit browser seen rows are imported additively. |
| Existing native `seen_images` | Preserve native seen rows | `ADOPT`: import does not delete or replace native seen state. |
| Malformed `pvu_seen_images` | recoverable warning, no native seen-row deletion | `ADOPT`: invalid seen-image JSON is skipped with recovery guidance. |
| `pvu_view.folderSortBy=name-asc` | `native_settings.folder_sort_mode=NameAsc` | `ADOPT`: imported on first native import when folder-sort state is absent. |
| `pvu_view.folderSortBy=name-desc` | `native_settings.folder_sort_mode=NameDesc` | `ADOPT`: imported on first native import when folder-sort state is absent. |
| `pvu_view.folderSortBy=count-desc` | `native_settings.folder_sort_mode=CountDesc` | `ADOPT`: imported on first native import when folder-sort state is absent. |
| `pvu_view.folderSortBy=count-asc` | `native_settings.folder_sort_mode=CountAsc` | `ADOPT`: imported on first native import when folder-sort state is absent. |
| Existing native `folder_sort_mode` | Preserve native folder-sort state | `ADOPT`: import does not clobber a native user choice. |
| Malformed / unsupported `pvu_view.folderSortBy` | recoverable warning, no native folder-sort overwrite | `ADOPT`: invalid folder-sort values are skipped with recovery guidance. |
| `pvu_legacy_imported=1` | `native_settings.browser_pvu_legacy_imported=1` raw mirror only | `REJECT`: marker-only key is preserved for traceability but has no native migration target. |
| `pvu_server_legacy_imported=1` | `native_settings.browser_pvu_server_legacy_imported=1` raw mirror only | `REJECT`: marker-only key is preserved for traceability but has no native migration target. |
| `pvu_perf_enabled=1` | `native_settings.browser_pvu_perf_enabled=1` raw mirror only | `DEFER`: browser instrumentation flag is preserved for traceability but has no accepted native user-facing migration target. |
| `pvu_scroll_memory={...}` | `native_settings.browser_pvu_scroll_memory` raw mirror only | `DEFER`: browser scroll-memory map is preserved for traceability, but the native selected-image/index restore is not the same state contract. |
| `pvu_fav_levels` if present in explicit export | `native_settings.browser_pvu_fav_levels` raw mirror only | `DEFER`: current browser code does not persist this key, so there is no evidenced native migration target or conflict policy. |
| `pvu_pinned_tabs` if present in explicit export | `native_settings.browser_pvu_pinned_tabs` raw mirror only | `DEFER`: browser pinned preview tabs belong to #99/#100; native has no accepted tab/pin/restore state contract yet. |
| `pvu_recent_albums` if present in explicit export | `native_settings.browser_pvu_recent_albums` raw mirror only | `DEFER`: album cache import exists, but there is no accepted native recent-album UI/state contract for this browser key. |

The raw browser keys are still stored under `browser_state` and mirrored as
`native_settings.browser_pvu_view` /
`native_settings.browser_pvu_enhanced_only` /
`native_settings.browser_pvu_fav_only` /
`native_settings.browser_pvu_unfav_only` /
`native_settings.browser_pvu_last_dir_set` /
`native_settings.browser_pvu_recent_dirs` /
`native_settings.browser_pvu_seen_images` /
`native_settings.browser_pvu_scroll_memory` /
`native_settings.browser_pvu_recent_albums` /
`native_settings.browser_pvu_fav_levels` /
`native_settings.browser_pvu_pinned_tabs` /
`native_settings.browser_pvu_perf_enabled` /
`native_settings.browser_pvu_legacy_imported` /
`native_settings.browser_pvu_server_legacy_imported` for traceability.

## Split Plan For Remaining pvu Keys

| Key | #117 classification | Reason / next evidence |
| --- | --- | --- |
| `pvu_view.thumbSize` | `ADOPT` | Native has `thumbnail_size`; this row imports only the scalar size, clamped to native 64-192, without adopting broader display-mode work. |
| `pvu_view.sortBy` | `ADOPT` | Native has `sort_mode`; import only browser values with matching native semantics (`newest`, `created-newest`, `name`, `random`) and preserve existing native choices. |
| `pvu_view.sortBy=oldest` / `created-oldest` and `randomSeed` | `DEFER` | Native currently lacks persisted ascending sort direction and browser random seed parity; adopting them needs a separate sort-surface decision. |
| `pvu_view.aspectMode` / `displayStyle` / `columns` | `DEFER` | These map to compact/poster/aspect controls in #111/#112, not this tiny persistence row. |
| `pvu_view.rightPanelOpen` / `rightPanelWidth` | `ADOPT` | Native preview visibility/splitter exists; M8/M9 UI smoke covers preview toggle and splitter persistence, so this row now maps first-import browser right-preview state without overwriting native choices. |
| `pvu_view.dateFrom` / `dateTo` | `ADOPT` | Native manual date settings already persist; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_view.hiddenFolders` | `ADOPT` | Native already persists `hidden_folder_buckets`; this row maps browser folder keys through the exported browser folder roots and preserves existing native hidden-folder choices. |
| `pvu_view.folderSortBy` | `ADOPT` | Native now has `folder_sort_mode`; import only browser values with matching folder-bucket semantics (`name-asc`, `name-desc`, `count-desc`, `count-asc`) and preserve existing native choices. |
| #102 folder range-selection semantics | `DEFER` | This row only persists bucket sort mode; range selection and broader folder workflows remain separate. |
| `pvu_pinned_tabs` | `DEFER` | Formally covered by Row 16; raw mirror is retained, but preview tab/pinned/restore state remains owned by #99/#100. |
| `pvu_perf_enabled` | `DEFER` | Formally covered by Row 13; raw mirror is retained, but there is no accepted native `perf_enabled` user setting or migration target. |
| `pvu_fav_only` / `pvu_unfav_only` | `ADOPT` | Native favorite filters exist; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_fav_levels` | `DEFER` | Formally covered by Row 15; current browser code does not persist this key, so explicit exports are raw-mirrored only until source evidence and conflict policy exist. |
| `pvu_enhanced_only` | `ADOPT` | Native enhanced-only state exists from M19; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_scroll_memory` | `DEFER` | Formally covered by Row 14; native has selected-image/index restore, not browser scroll-memory parity, so the browser map is raw-mirrored only. |
| `pvu_seen_images` | `ADOPT` | Formally covered by Row 10 pvu-state smoke/migration trace; explicit browser export imports additively into native `seen_images` and preserves native seen rows. |
| `pvu_recent_albums` | `DEFER` | Formally covered by Row 17; album cache import exists, but current `src/**` evidence has no browser recent-album persistence source and no accepted native recent-album UI/state contract, so explicit exports are raw-mirrored only. |
| `pvu_recent_dirs` / `pvu_last_dir_set` | `ADOPT` | Native folder-set persistence exists; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_enhance_settings` | `DEFER` | Owned by #97/#98 explicit enhancement UI; no automatic workers. |
| `pvu_server_legacy_imported` / `pvu_legacy_imported` | `REJECT` | Formally covered by Row 12 marker-key smoke evidence; raw mirrors are retained, but there is no native user workflow or migration target. |
| `pvu_favorites` / `pvu_favorites_backup` | `PARTIAL_ADOPT` | Native imports disk `.cache/favorites.json`; browser localStorage favorites need a separate conflict policy before import. |

## Native Evidence

Dedicated smoke:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke
```

Expected result:

- `pvuViewModeMigrated=true`
- `pvuEnhancedOnlyMigrated=true`
- `pvuFavoriteFilterMigrated=true`
- `pvuDateRangeMigrated=true`
- `pvuThumbnailSizeMigrated=true`
- `pvuThumbnailSizeClamped=true`
- `pvuSortModeMigrated=true`
- `pvuRecentFoldersMigrated=true`
- `pvuRightPreviewMigrated=true`
- `pvuHiddenFoldersMigrated=true`
- `pvuSeenImagesMigrated=true`
- `pvuFolderSortModeMigrated=true`
- `pvuPerfFlagDeferred=true`
- `pvuScrollMemoryDeferred=true`
- `pvuFavLevelsDeferred=true`
- `pvuPinnedTabsDeferred=true`
- `pvuRecentAlbumsDeferred=true`
- `pvuLegacyMarkersRejected=true`
- `migrationRecorded=true`
- `browserMirrorStored=true`
- `enhancedMirrorStored=true`
- `favoriteMirrorStored=true`
- `recentMirrorStored=true`
- `recentAlbumsMirrorStored=true`
- `seenMirrorStored=true`
- `scrollMemoryMirrorStored=true`
- `favLevelsMirrorStored=true`
- `pinnedTabsMirrorStored=true`
- `perfMirrorStored=true`
- `markerMirrorStored=true`
- `nativeViewModePreserved=true`
- `nativeEnhancedOnlyPreserved=true`
- `nativeFavoriteFilterPreserved=true`
- `nativeDateRangePreserved=true`
- `nativeThumbnailSizePreserved=true`
- `nativeSortModePreserved=true`
- `nativeRecentFolderSetPreserved=true`
- `nativeRightPreviewPreserved=true`
- `nativeHiddenFoldersPreserved=true`
- `nativeSeenImagesPreserved=true`
- `nativeFolderSortModePreserved=true`
- `malformedEnhancedOnlyWarning=true`
- `malformedFavoriteFilterWarning=true`
- `malformedDateRangeWarning=true`
- `malformedThumbnailSizeWarning=true`
- `unsupportedSortModeWarning=true`
- `malformedRecentDirsWarning=true`
- `malformedRightPreviewWarning=true`
- `malformedHiddenFoldersWarning=true`
- `malformedSeenImagesWarning=true`
- `unsupportedFolderSortWarning=true`
- `nativeEnhancedOnlyStillPreserved=true`
- `nativeFavoriteFilterStillPreserved=true`
- `nativeDateRangeStillPreserved=true`
- `nativeThumbnailSizeStillPreserved=true`
- `nativeSortModeStillPreserved=true`
- `nativeRecentFolderSetStillPreserved=true`
- `nativeRightPreviewStillPreserved=true`
- `nativeHiddenFoldersStillPreserved=true`
- `nativeSeenImagesStillPreserved=true`
- `nativeFolderSortModeStillPreserved=true`
- `browserStateKeys=10`
- `firstWarnings=0`
- `secondWarnings=0`
- `malformedWarnings=10`
- `browserRuntime=false localHttpServer=false nodeRuntime=false`

`migrationRecorded=true` keeps `pvu_state_migration_count=11`; Row 12 does
not add marker-only keys to `pvu_state_migrations`, Row 13 does not add the
browser performance flag, Row 14 does not add browser scroll memory, Row 15
does not add non-evidenced browser favorite levels, and Row 16 does not add
browser pinned preview tabs, and Row 17 does not add browser recent-album
state.
`markerMirrorStored=true`, `perfMirrorStored=true`,
`scrollMemoryMirrorStored=true`, `favLevelsMirrorStored=true`,
`pinnedTabsMirrorStored=true`, and `recentAlbumsMirrorStored=true` are the
raw-mirror evidence. The final `browserStateKeys=10` count is measured after
the malformed follow-up import, where PR #141 keeps `pvu_perf_enabled` in the
malformed recovery path while Row 14 keeps `pvu_scroll_memory`, Row 15 keeps
`pvu_fav_levels`, Row 16 keeps `pvu_pinned_tabs`, and Row 17 keeps
`pvu_recent_albums` as native settings raw mirrors only.

The smoke uses a synthetic project root under ignored
`.cache/native-pvu-state-smoke/**` and does not overwrite real user state.

## Current Row 16 Pinned-Tabs Verification

Recorded on 2026-07-08 in branch
`codex/h25-117-row16-pvu-state` based on `origin/main`
`d7348766f950575f987e2606fab78b240bc94e9e` after PR #144 and the Row15
verification commit:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuPinnedTabsDeferred=true`,
  `pinnedTabsMirrorStored=true`, `pvuFavLevelsDeferred=true`,
  `favLevelsMirrorStored=true`, `pvuScrollMemoryDeferred=true`,
  `scrollMemoryMirrorStored=true`, `pvuPerfFlagDeferred=true`,
  `perfMirrorStored=true`, `pvuLegacyMarkersRejected=true`,
  `markerMirrorStored=true`, `migrationRecorded=true`,
  `pvu_state_migration_count=11` by smoke contract,
  `browserStateKeys=9`, `firstWarnings=0`, `secondWarnings=0`,
  `malformedWarnings=10`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed using ignored fixture/cache state while preserving existing
  cache/state assets.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=7`, `warnings=0`, and no browser runtime.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke`
  passed with `importedSeen=true`, `nativeInitiallyUnseen=true`,
  `nativeSeenPersisted=true`, `importedStillSeen=true`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `folderSortMode=true`, `thumbnailSize=true`,
  `enhancedOnlyFilter=true`, `favoriteLevelFilter=true`,
  `browserStateKeys=7`, `settingsImported=true`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" .` returned no conflict markers.
- `git diff --check` passed.

## Current Row 17 Recent-Albums Verification

Recorded on 2026-07-08 in branch
`codex/h25-117-row16-pvu-recent-albums` based on `origin/main`
`4cd9f84fe11be576e905959069ca838db814a5da` after PR #146:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuRecentAlbumsDeferred=true`,
  `recentAlbumsMirrorStored=true`, `pvuPinnedTabsDeferred=true`,
  `pinnedTabsMirrorStored=true`, `pvuFavLevelsDeferred=true`,
  `favLevelsMirrorStored=true`, `pvuScrollMemoryDeferred=true`,
  `scrollMemoryMirrorStored=true`, `pvuPerfFlagDeferred=true`,
  `perfMirrorStored=true`, `pvuLegacyMarkersRejected=true`,
  `markerMirrorStored=true`, `migrationRecorded=true`,
  `pvu_state_migration_count=11` by smoke contract,
  `pvuFolderSortModeMigrated=true`, `pvuSeenImagesMigrated=true`,
  `seenMirrorStored=true`, `nativeFolderSortModePreserved=true`,
  `nativeSeenImagesPreserved=true`,
  `nativeFolderSortModeStillPreserved=true`,
  `nativeSeenImagesStillPreserved=true`, `browserStateKeys=10`,
  `firstWarnings=0`, `secondWarnings=0`, `malformedWarnings=10`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed using ignored fixture/cache state while preserving existing cache/state
  assets.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `warnings=0`, and no browser runtime. Persisted
  `seenImages` / `settings` / `images` counts reflect existing ignored cache
  state and may increase across repeated smoke runs.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large`
  passed with `totalImages=240`, `targetIndex=180`,
  `restoredIndex=180`, `statePersisted=true`, `restoreSelected=true`,
  `ensureVisible=true`, `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke`
  passed with `importedSeen=true`, `nativeInitiallyUnseen=true`,
  `nativeSeenPersisted=true`, `importedStillSeen=true`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `gridToggle=true`, `folderSortMode=true`,
  `thumbnailSize=true`, `enhancedOnlyFilter=true`, `sortName=true`,
  `randomReshuffle=true`, `settingsImported=true`, `browserStateKeys=6`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" .` returned no conflict markers.
- `git diff --check` passed.

## Current Row 15 Favorite-Levels Verification

Recorded on 2026-07-08 in branch
`codex/h25-117-row14-pvu-fav-levels` rebased on `origin/main`
`4433182a7fd1d84142eac920187fea3f88410c55` after PR #142:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuFavLevelsDeferred=true`,
  `favLevelsMirrorStored=true`, `pvuScrollMemoryDeferred=true`,
  `scrollMemoryMirrorStored=true`, `pvuPerfFlagDeferred=true`,
  `perfMirrorStored=true`, `pvuLegacyMarkersRejected=true`,
  `markerMirrorStored=true`, `migrationRecorded=true`,
  `pvu_state_migration_count=11` by smoke contract,
  `pvuFolderSortModeMigrated=true`, `pvuSeenImagesMigrated=true`,
  `seenMirrorStored=true`, `nativeFolderSortModePreserved=true`,
  `nativeSeenImagesPreserved=true`,
  `nativeFolderSortModeStillPreserved=true`,
  `nativeSeenImagesStillPreserved=true`, `browserStateKeys=8`,
  `firstWarnings=0`, `secondWarnings=0`, `malformedWarnings=10`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed using ignored fixture/cache state while preserving existing cache/state
  assets.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `warnings=0`, and no browser runtime. Persisted
  `seenImages` / `settings` / `images` counts reflect existing ignored cache
  state and may increase across repeated smoke runs.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large`
  passed with `totalImages=240`, `targetIndex=180`,
  `restoredIndex=180`, `statePersisted=true`, `restoreSelected=true`,
  `ensureVisible=true`, `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke`
  passed with `importedSeen=true`, `nativeInitiallyUnseen=true`,
  `nativeSeenPersisted=true`, `importedStillSeen=true`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `gridToggle=true`, `folderSortMode=true`,
  `thumbnailSize=true`, `enhancedOnlyFilter=true`, `sortName=true`,
  `randomReshuffle=true`, `settingsImported=true`, `browserStateKeys=6`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" .` returned no conflict markers.
- `git diff --check` passed.

## Current Row 14 Scroll-Memory Verification

Recorded on 2026-07-08 in branch
`codex/h25-117-row14-pvu-scroll-memory` based on `origin/main`
`272d5c576d18b86ea1fa7342bb1a317f2018bab1` after PR #141:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuScrollMemoryDeferred=true`,
  `scrollMemoryMirrorStored=true`, `pvuPerfFlagDeferred=true`,
  `perfMirrorStored=true`, `pvuLegacyMarkersRejected=true`,
  `markerMirrorStored=true`, `migrationRecorded=true`,
  `pvu_state_migration_count=11` by smoke contract,
  `pvuFolderSortModeMigrated=true`, `pvuSeenImagesMigrated=true`,
  `seenMirrorStored=true`, `nativeFolderSortModePreserved=true`,
  `nativeSeenImagesPreserved=true`,
  `nativeFolderSortModeStillPreserved=true`,
  `nativeSeenImagesStillPreserved=true`, `browserStateKeys=7`,
  `firstWarnings=0`, `secondWarnings=0`, `malformedWarnings=10`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed using ignored fixture/cache state while preserving existing cache/state
  assets.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `warnings=0`, and no browser runtime. Persisted
  `seenImages` / `settings` / `images` counts reflect existing ignored cache
  state and may increase across repeated smoke runs.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large`
  passed with `totalImages=240`, `targetIndex=180`,
  `restoredIndex=180`, `statePersisted=true`, `restoreSelected=true`,
  `ensureVisible=true`, `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke`
  passed with `importedSeen=true`, `nativeInitiallyUnseen=true`,
  `nativeSeenPersisted=true`, `importedStillSeen=true`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `folderSortMode=true`, `sortName=true`,
  `randomReshuffle=true`, `thumbnailSize=true`, `settingsImported=true`,
  `browserStateKeys=6`, `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" .` returned no conflict markers.
- `git diff --check` passed.

## Current Row 13 Perf-Flag Verification

Recorded on 2026-07-08 in branch
`codex/h25-117-row13-pvu-perf-flag-50c1` based on `origin/main`
`8522939ec8d3da673e3e51d066c73eed956e378f` after PR #140:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuPerfFlagDeferred=true`, `perfMirrorStored=true`,
  `pvuLegacyMarkersRejected=true`, `markerMirrorStored=true`,
  `migrationRecorded=true`, `pvu_state_migration_count=11` by smoke
  contract, `browserStateKeys=7`, `firstWarnings=0`, `secondWarnings=0`,
  `malformedWarnings=10`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed using ignored fixture/cache state while preserving existing
  cache/state assets.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `warnings=0`, and no browser runtime. Persisted
  `seenImages` / `settings` / `images` counts reflect existing ignored cache
  state and may increase across repeated smoke runs.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke`
  passed with `importedSeen=true`, `nativeInitiallyUnseen=true`,
  `nativeSeenPersisted=true`, `importedStillSeen=true`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `folderSortMode=true`, `enhancedOnlyFilter=true`,
  `settingsImported=true`, `browserStateKeys=6`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" .` returned no conflict markers.
- `git diff --check` passed.

## Verification On 2026-07-08

Recorded in branch `codex/h25-117-row5-pvu-state` after rebasing PR #131 onto
PR #132 / `origin/main` `13710ebc86d3247f54027c190b30e3b77eab9e1b`:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuViewModeMigrated=true`,
  `pvuEnhancedOnlyMigrated=true`, `pvuFavoriteFilterMigrated=true`,
  `pvuDateRangeMigrated=true`, `pvuRecentFoldersMigrated=true`,
  `pvuRightPreviewMigrated=true`,
  `migrationRecorded=true`, `browserMirrorStored=true`,
  `enhancedMirrorStored=true`, `favoriteMirrorStored=true`,
  `recentMirrorStored=true`, `nativeViewModePreserved=true`,
  `nativeEnhancedOnlyPreserved=true`,
  `nativeFavoriteFilterPreserved=true`, `nativeDateRangePreserved=true`,
  `nativeRecentFolderSetPreserved=true`, `nativeRightPreviewPreserved=true`,
  `malformedEnhancedOnlyWarning=true`,
  `malformedFavoriteFilterWarning=true`, `malformedDateRangeWarning=true`,
  `malformedRecentDirsWarning=true`,
  `malformedRightPreviewWarning=true`,
  `nativeEnhancedOnlyStillPreserved=true`,
  `nativeFavoriteFilterStillPreserved=true`,
  `nativeDateRangeStillPreserved=true`,
  `nativeRecentFolderSetStillPreserved=true`,
  `nativeRightPreviewStillPreserved=true`, `browserStateKeys=5`,
  `firstWarnings=0`, `secondWarnings=0`, and `malformedWarnings=5`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed using ignored fixture/cache state and preserved existing cache/state
  assets.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `seenImages=4`, `settings=37`, `images=4`,
  `warnings=0`, and no browser runtime.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `gridToggle=true`, `enhancedOnlyFilter=true`,
  `browserStateKeys=6`, and `enhancementStateUnchanged=true`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture`
  passed with `recentSetPersisted=true`, `openRecentSet=true`,
  `watcherRoots=true`, and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `git diff --check` passed.
- `corepack pnpm typecheck` passed.

## Current Row 12 Marker-Key Verification

Recorded on 2026-07-08 in branch
`codex/h25-117-row12-marker-keys` based on `origin/main`
`abdeb59581f0fa5d1a7658e0a8fae7ccb914f35a` after PR #138:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuSeenImagesMigrated=true`,
  `pvuFolderSortModeMigrated=true`, `pvuLegacyMarkersRejected=true`,
  `migrationRecorded=true`, `seenMirrorStored=true`,
  `markerMirrorStored=true`, `nativeSeenImagesPreserved=true`,
  `nativeFolderSortModePreserved=true`, `malformedSeenImagesWarning=true`,
  `unsupportedFolderSortWarning=true`, `nativeSeenImagesStillPreserved=true`,
  `nativeFolderSortModeStillPreserved=true`, `browserStateKeys=6`,
  `firstWarnings=0`, `secondWarnings=0`, `malformedWarnings=10`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed using ignored fixture/cache state while preserving existing cache/state
  assets.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `seenImages=6`, `settings=38`, `images=6`,
  `warnings=0`, and no browser runtime.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke`
  passed with `importedSeen=true`, `nativeInitiallyUnseen=true`,
  `nativeSeenPersisted=true`, `importedStillSeen=true`,
  `totalSeenImages=8`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `folderSortMode=true`, `sortName=true`,
  `randomReshuffle=true`, `thumbnailSize=true`, `settingsImported=true`,
  `browserStateKeys=6`, `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `rg -n "<<<<<<<|=======|>>>>>>>" .` returned no conflict markers.
- `git diff --check` passed.

## Current Row 7 Verification

Recorded on 2026-07-08 in branch `codex/h25-117-pvu-row6-thumb-size`
rebased onto `origin/main` `10dbf1245a40e35509516e7f26ffb7a254f05d70`
after PR #131:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuThumbnailSizeMigrated=true`,
  `pvuThumbnailSizeClamped=true`,
  `nativeThumbnailSizePreserved=true`,
  `malformedThumbnailSizeWarning=true`,
  `nativeThumbnailSizeStillPreserved=true`, `pvuRightPreviewMigrated=true`,
  `nativeRightPreviewPreserved=true`, `malformedRightPreviewWarning=true`,
  `migrationRecorded=true`, `firstWarnings=0`, `secondWarnings=0`, and
  `malformedWarnings=6`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed and created only ignored fixture/cache state in this clean worktree.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `browserStateKeys=6`, `seenImages=4`, `settings=37`,
  `images=4`, `warnings=0`, and no browser runtime.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `thumbnailSize=true`, `browserStateKeys=6`, and
  `enhancementStateUnchanged=true`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
- `git diff --check` passed.

## Follow-Up Classification

- `ADOPT`: bounded `pvu_view.viewMode` migration.
- `ADOPT`: bounded `pvu_enhanced_only` migration.
- `ADOPT`: bounded `pvu_fav_only` / `pvu_unfav_only` migration.
- `ADOPT`: bounded `pvu_view.dateFrom` / `dateTo` migration.
- `ADOPT`: bounded `pvu_last_dir_set` / `pvu_recent_dirs` migration.
- `ADOPT`: bounded `pvu_view.rightPanelOpen` / `rightPanelWidth` migration.
- `ADOPT`: bounded `pvu_view.thumbSize` migration into native
  `thumbnail_size`, clamped to the current native UI range.
- `ADOPT`: bounded `pvu_view.sortBy` migration into native `sort_mode` for
  browser values with matching native semantics.
- `ADOPT`: bounded `pvu_view.hiddenFolders` migration into native
  `hidden_folder_buckets`, mapped through explicit browser folder roots.
- `ADOPT`: bounded `pvu_seen_images` import trace into native `seen_images`,
  preserving existing native seen rows.
- `ADOPT`: bounded `pvu_view.folderSortBy` migration into native
  `folder_sort_mode` for matching folder-bucket sort semantics.
- `PARTIAL_ADOPT`: use #117 as a key-by-key migration lane, not a broad
  one-pass state rewrite.
- `REJECT`: direct Chrome profile reads, browser HTTP compatibility, and
  browser marker-only keys as native migration targets. Row 12 records
  `pvu_legacy_imported` / `pvu_server_legacy_imported` as raw mirrors only.
- `DEFER`: browser `pvu_perf_enabled` as a native migration target. Row 13
  records `pvu_perf_enabled` as a raw mirror only and keeps the native
  migration count unchanged.
- `DEFER`: browser `pvu_scroll_memory` as a native migration target. Row 14
  records `pvu_scroll_memory` as a raw mirror only and keeps native
  selected-image/index restore separate from browser scroll-map import.
- `DEFER`: browser `pvu_fav_levels` as a native migration target. Row 15
  records explicit exports as raw mirrors only because current browser code
  does not persist that key.
- `DEFER`: browser `pvu_pinned_tabs` as a native migration target. Row 16
  records explicit exports as raw mirrors only and keeps preview tab/pinned/
  restore semantics in #99/#100.
- `DEFER`: browser `pvu_recent_albums` as a native migration target. Row 17
  records `pvu_recent_albums` as a raw mirror only and keeps album cache import
  separate from any future native recent-album UI/state contract.
- `DEFER`: browser ascending sort directions, `randomSeed`, #102 folder range
  selection, enhancement settings, broader display details, and browser
  localStorage favorites to their existing post-v1 issue rows.
- `NEEDS_HUMAN`: none for this slice.
