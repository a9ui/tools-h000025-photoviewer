# Local Native Post-v1 #117 pvu State Persistence Migration

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/117

## Decision

Decision:
`ADOPT_BOUNDED_PVU_FAVORITE_FILTER_MIGRATION_AFTER_VIEW_AND_ENHANCED_ROWS`.

Meaning:

- #117 is broad by default, so this slice advances only one safe row:
  `pvu_fav_only` / `pvu_unfav_only` from an explicit browser localStorage
  export.
- The previous accepted rows remain `pvu_view.viewMode` into native
  `view_mode` and `pvu_enhanced_only` into native `enhanced_only_filter`.
- Native still reads browser `pvu_*` state only from an explicit JSON export
  file. It never reads Chrome profile storage directly.
- The migrations write native settings only when the target native setting
  does not exist yet, so later native user choices are not overwritten on every
  startup/import.
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

The raw browser keys are still stored under `browser_state` and mirrored as
`native_settings.browser_pvu_view` /
`native_settings.browser_pvu_enhanced_only` /
`native_settings.browser_pvu_fav_only` /
`native_settings.browser_pvu_unfav_only` for traceability.

## Split Plan For Remaining pvu Keys

| Key | #117 classification | Reason / next evidence |
| --- | --- | --- |
| `pvu_view.thumbSize` | `DEFER` | Native has `thumbnail_size`, but broader display mapping belongs with #111/#112 display-mode work. |
| `pvu_view.aspectMode` / `displayStyle` / `columns` | `DEFER` | These map to compact/poster/aspect controls in #111/#112, not this tiny persistence row. |
| `pvu_view.rightPanelOpen` / `rightPanelWidth` | `DEFER` | Native preview visibility/splitter exists; migrate with UI polish or right-panel parity evidence. |
| `pvu_view.dateFrom` / `dateTo` | `PARTIAL_ADOPT` | Native manual date settings already persist; browser import mapping needs a dedicated fixture if adopted. |
| `pvu_view.hiddenFolders` / `folderSortBy` | `DEFER` | Folder bucket behavior and range semantics remain tied to #102/folder UI decisions. |
| `pvu_pinned_tabs` | `DEFER` | Owned by #99/#100 preview tab/pinned/restore work. |
| `pvu_perf_enabled` | `DEFER` | Browser performance instrumentation flag has no native user-facing equivalent yet. |
| `pvu_fav_only` / `pvu_unfav_only` | `ADOPT` | Native favorite filters exist; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_fav_levels` | `DEFER` | Listed as possible browser-only state, but current browser code does not persist this key; keep deferred until there is source evidence and conflict policy. |
| `pvu_enhanced_only` | `ADOPT` | Native enhanced-only state exists from M19; explicit browser import now maps first-import state without overwriting native choices. |
| `pvu_scroll_memory` | `DEFER` | Native has selected-image/index restore, not browser scroll-memory parity. |
| `pvu_seen_images` | `ADOPT` | Already imported into native `seen_images` by M14. |
| `pvu_recent_albums` | `DEFER` | Albums import exists, but recent-album UI semantics are not native-accepted. |
| `pvu_recent_dirs` / `pvu_last_dir_set` | `PARTIAL_ADOPT` | Native folder-set persistence exists; browser import mapping should be split from this view-mode row. |
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
- `migrationRecorded=true`
- `browserMirrorStored=true`
- `enhancedMirrorStored=true`
- `favoriteMirrorStored=true`
- `nativeViewModePreserved=true`
- `nativeEnhancedOnlyPreserved=true`
- `nativeFavoriteFilterPreserved=true`
- `malformedEnhancedOnlyWarning=true`
- `malformedFavoriteFilterWarning=true`
- `nativeEnhancedOnlyStillPreserved=true`
- `nativeFavoriteFilterStillPreserved=true`
- `firstWarnings=0`
- `secondWarnings=0`
- `malformedWarnings=2`
- `browserRuntime=false localHttpServer=false nodeRuntime=false`

The smoke uses a synthetic project root under ignored
`.cache/native-pvu-state-smoke/**` and does not overwrite real user state.

## Verification On 2026-07-08

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`
  passed with `pvuViewModeMigrated=true`,
  `pvuEnhancedOnlyMigrated=true`, `pvuFavoriteFilterMigrated=true`,
  `migrationRecorded=true`, `browserMirrorStored=true`,
  `enhancedMirrorStored=true`, `favoriteMirrorStored=true`,
  `nativeViewModePreserved=true`, `nativeEnhancedOnlyPreserved=true`,
  `nativeFavoriteFilterPreserved=true`, `malformedEnhancedOnlyWarning=true`,
  `malformedFavoriteFilterWarning=true`,
  `nativeEnhancedOnlyStillPreserved=true`,
  `nativeFavoriteFilterStillPreserved=true`, `browserStateKeys=4`,
  `firstWarnings=0`, `secondWarnings=0`, and `malformedWarnings=2`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed and created only ignored fixture/cache state in this clean worktree.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-import --browser-state-export .\.cache\native\browser-localstorage-export.json`
  passed with `browserStateKeys=6`, `warnings=0`, and no browser runtime.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `gridToggle=true`, `enhancedOnlyFilter=true`,
  `browserStateKeys=6`, and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no files.
- `git diff --check` passed.
- `corepack pnpm typecheck` passed.

## Follow-Up Classification

- `ADOPT`: bounded `pvu_view.viewMode` migration.
- `ADOPT`: bounded `pvu_enhanced_only` migration.
- `ADOPT`: bounded `pvu_fav_only` / `pvu_unfav_only` migration.
- `PARTIAL_ADOPT`: use #117 as a key-by-key migration lane, not a broad
  one-pass state rewrite.
- `REJECT`: direct Chrome profile reads, browser HTTP compatibility, and
  browser marker-only keys as native migration targets.
- `DEFER`: `pvu_fav_levels`, pinned tabs, enhancement settings, date/display
  details to their existing post-v1 issue rows.
- `NEEDS_HUMAN`: none for this slice.
