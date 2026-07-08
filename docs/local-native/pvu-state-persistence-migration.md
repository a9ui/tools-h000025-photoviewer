# Local Native Post-v1 #117 pvu State Persistence Migration

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/117

## Decision

Decision:
`ADOPT_BOUNDED_PVU_VIEW_MODE_MIGRATION_FIRST`.

Meaning:

- #117 is broad by default, so this slice advances only one safe row:
  `pvu_view.viewMode` from an explicit browser localStorage export.
- Native still reads browser `pvu_*` state only from an explicit JSON export
  file. It never reads Chrome profile storage directly.
- The migration writes native `view_mode` only when no native `view_mode`
  setting exists yet, so later native user choices are not overwritten on every
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

The raw browser key is still stored under `browser_state` and mirrored as
`native_settings.browser_pvu_view` for traceability.

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
| `pvu_fav_only` / `pvu_unfav_only` / `pvu_fav_levels` | `DEFER` | Native favorite filters exist; import mapping needs a separate filter-state smoke. |
| `pvu_enhanced_only` | `PARTIAL_ADOPT` | Native enhanced-only state exists from M19; import mapping can be a later safe row. |
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
- `migrationRecorded=true`
- `browserMirrorStored=true`
- `nativeViewModePreserved=true`
- `firstWarnings=0`
- `secondWarnings=0`
- `browserRuntime=false localHttpServer=false nodeRuntime=false`

The smoke uses a synthetic project root under ignored
`.cache/native-pvu-state-smoke/**` and does not overwrite real user state.

## Follow-Up Classification

- `ADOPT`: bounded `pvu_view.viewMode` migration.
- `PARTIAL_ADOPT`: use #117 as a key-by-key migration lane, not a broad
  one-pass state rewrite.
- `REJECT`: direct Chrome profile reads, browser HTTP compatibility, and
  browser marker-only keys as native migration targets.
- `DEFER`: pinned tabs, enhancement settings, folder/filter/date/display
  details to their existing post-v1 issue rows.
- `NEEDS_HUMAN`: none for this slice.
