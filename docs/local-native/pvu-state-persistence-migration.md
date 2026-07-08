# Local Native Post-v1 #117 pvu State Persistence Migration

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/117

## Decision

Decision:
`ADOPT_BOUNDED_PVU_SEEN_IMAGES_TRACE_AFTER_HIDDEN_FOLDERS_ROW`.

Meaning:

- #117 is broad by default, so this slice advances only one safe row:
  explicit browser `pvu_seen_images` from a localStorage export.
- The previous accepted rows remain `pvu_view.viewMode` into native
  `view_mode`, `pvu_enhanced_only` into native `enhanced_only_filter`, and
  `pvu_fav_only` / `pvu_unfav_only` into native `favorite_filter`, and
  `pvu_view.dateFrom` / `dateTo` into native date settings, and
  `pvu_last_dir_set` / `pvu_recent_dirs` into native recent-folder settings,
  and `pvu_view.rightPanelOpen` / `rightPanelWidth` into native right-preview
  settings, `pvu_view.thumbSize` into native `thumbnail_size`,
  `pvu_view.sortBy` into native `sort_mode`, and `pvu_view.hiddenFolders` into
  native `hidden_folder_buckets`.
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
  absolute folder paths. This row converts only keys that can be mapped through
  the explicit exported `pvu_last_dir_set` / `pvu_recent_dirs` roots.
- Browser `pvu_seen_images` is additive. Explicit browser-export rows are
  upserted into native `seen_images` with source `browser_export`; existing
  native seen rows are preserved.
- Malformed `pvu_seen_images` is warning-only and does not overwrite or delete
  native seen state.
- Browser `folderSortBy` remains deferred because native folder bucket sorting
  is not a separate accepted setting yet, and folder range-selection semantics
  remain owned by #102.
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

The raw browser keys are still stored under `browser_state` and mirrored as
`native_settings.browser_pvu_view` /
`native_settings.browser_pvu_enhanced_only` /
`native_settings.browser_pvu_fav_only` /
`native_settings.browser_pvu_unfav_only` /
`native_settings.browser_pvu_last_dir_set` /
`native_settings.browser_pvu_recent_dirs` /
`native_settings.browser_pvu_seen_images` for traceability.

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
| `pvu_view.folderSortBy` | `DEFER` | Native folder bucket sorting is currently fixed by label and range semantics remain tied to #102/folder UI decisions. |
| `pvu_pinned_tabs` | `DEFER` | Owned by #99/#100 preview tab/pinned/restore work. |
| `pvu_perf_enabled` | `DEFER` | Browser performance instrumentation flag has no native user-facing equivalent yet. |
| `pvu_fav_only` / `pvu_unfav_only` | `ADOPT` | Native favorite filters exist; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_fav_levels` | `DEFER` | Listed as possible browser-only state, but current browser code does not persist this key; keep deferred until there is source evidence and conflict policy. |
| `pvu_enhanced_only` | `ADOPT` | Native enhanced-only state exists from M19; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_scroll_memory` | `DEFER` | Native has selected-image/index restore, not browser scroll-memory parity. |
| `pvu_seen_images` | `ADOPT` | Formally covered by Row 10 pvu-state smoke/migration trace; explicit browser export imports additively into native `seen_images` and preserves native seen rows. |
| `pvu_recent_albums` | `DEFER` | Albums import exists, but recent-album UI semantics are not native-accepted. |
| `pvu_recent_dirs` / `pvu_last_dir_set` | `ADOPT` | Native folder-set persistence exists; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_enhance_settings` | `DEFER` | Owned by #97/#98 explicit enhancement UI; no automatic workers. |
| `pvu_server_legacy_imported` / `pvu_legacy_imported` | `REJECT` | Browser migration marker only; no native user workflow to migrate. |
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
- `migrationRecorded=true`
- `browserMirrorStored=true`
- `enhancedMirrorStored=true`
- `favoriteMirrorStored=true`
- `recentMirrorStored=true`
- `seenMirrorStored=true`
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
- `malformedEnhancedOnlyWarning=true`
- `malformedFavoriteFilterWarning=true`
- `malformedDateRangeWarning=true`
- `malformedThumbnailSizeWarning=true`
- `unsupportedSortModeWarning=true`
- `malformedRecentDirsWarning=true`
- `malformedRightPreviewWarning=true`
- `malformedHiddenFoldersWarning=true`
- `malformedSeenImagesWarning=true`
- `nativeEnhancedOnlyStillPreserved=true`
- `nativeFavoriteFilterStillPreserved=true`
- `nativeDateRangeStillPreserved=true`
- `nativeThumbnailSizeStillPreserved=true`
- `nativeSortModeStillPreserved=true`
- `nativeRecentFolderSetStillPreserved=true`
- `nativeRightPreviewStillPreserved=true`
- `nativeHiddenFoldersStillPreserved=true`
- `nativeSeenImagesStillPreserved=true`
- `firstWarnings=0`
- `secondWarnings=0`
- `malformedWarnings=9`
- `browserRuntime=false localHttpServer=false nodeRuntime=false`

The smoke uses a synthetic project root under ignored
`.cache/native-pvu-state-smoke/**` and does not overwrite real user state.

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

## Current Row 10 Seen-Images Verification

Recorded on 2026-07-08 in branch
`codex/h25-117-row10-state-persistence` based on `origin/main`
`61b01837ba6a73238cbc33ffe983e829cc4e4ea4` after PR #136:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuSeenImagesMigrated=true`,
  `migrationRecorded=true`, `seenMirrorStored=true`,
  `nativeSeenImagesPreserved=true`, `malformedSeenImagesWarning=true`,
  `nativeSeenImagesStillPreserved=true`, `browserStateKeys=6`,
  `firstWarnings=0`, `secondWarnings=0`, `malformedWarnings=9`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed and created only ignored fixture/cache state in this worktree.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `favorites=1`, `albums=2`, `albumImages=4`,
  `browserStateKeys=6`, `seenImages=0`, `settings=28`, `images=0`,
  `warnings=0`, and no browser runtime.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke`
  passed with `importedSeen=true`, `nativeInitiallyUnseen=true`,
  `nativeSeenPersisted=true`, `importedStillSeen=true`,
  `totalSeenImages=2`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `sortName=true`, `randomReshuffle=true`,
  `thumbnailSize=true`, `settingsImported=true`, `browserStateKeys=6`,
  `enhancementStateUnchanged=true`, and
  `browserRuntime=false localHttpServer=false nodeRuntime=false`.
- `corepack pnpm typecheck` passed.
- `git diff --name-only -- src` returned no files.
- `git diff --name-only -- scripts` returned no files.
- `git diff --name-only -- H000033` returned no files.
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
- `PARTIAL_ADOPT`: use #117 as a key-by-key migration lane, not a broad
  one-pass state rewrite.
- `REJECT`: direct Chrome profile reads, browser HTTP compatibility, and
  browser marker-only keys as native migration targets.
- `DEFER`: browser ascending sort directions, `randomSeed`,
  `pvu_view.folderSortBy`, `pvu_fav_levels`, pinned tabs, enhancement
  settings, broader display details, `pvu_recent_albums`, scroll memory, and
  browser localStorage favorites to their existing post-v1 issue rows.
- `NEEDS_HUMAN`: none for this slice.
