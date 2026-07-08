# PhotoViewer Local Native Lane

This lane is an isolated Windows desktop prototype inside H000025. It does not
replace the existing Next.js browser app, and it does not depend on H000033.

## Current Scope

- Keep the browser version untouched.
- Reuse H000025 state where it is already on disk:
  - `.cache/favorites.json`
  - `.cache/albums.json`
  - `.cache/settings.json`
- Avoid a local HTTP server and browser image route for native browsing.
- Start with fast filesystem enumeration, a virtual list, and direct image
  preview.
- Defer thumbnail-cache generation until measurements prove where it helps.

## Run

```powershell
dotnet run --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
```

From the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1
```

Headless scan/import verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder "C:\path\to\images"
```

M2 headless state checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder "C:\path\to\images" -Search "name" -FavoritesOnly
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -FavoritePath "C:\path\to\image.png" -FavoriteLevel 4
```

## M0 Notes

This first app is intentionally small. It proves the local shape:

- `Directory.EnumerateFiles` streams image paths instead of building a browser
  API response first.
- `ListView.VirtualMode` keeps large folders from creating one control per
  image.
- Selection preview decodes directly from the source file without
  `/api/image`, `fetch`, `Blob`, or `URL.createObjectURL`.
- Favorites are read from the current H000025 disk cache by absolute path, so
  existing favorite levels carry over.

Expected limitations:

- Format support initially follows Windows/GDI+ decoding. PNG/JPEG/GIF are the
  reliable baseline; WebP/AVIF need a measured decoder decision.
- Albums and browser-only `localStorage` view state are mapped in docs first and
  should be imported after the native store is chosen.
- Enhancement jobs stay out of the hot path.

## M2 Notes

- List/grid browsing is native WinForms state, backed by SQLite settings.
- Previous/next navigation, favorite level mutation, search/favorite filters,
  recent folder restore, open file/folder, and Recycle Bin delete are present.
- `.cache/albums.json` and `.cache/settings.json` are imported as compatibility
  state summaries. Browser `pvu_*` localStorage is still not read directly.
- Recycle delete has no hard-delete fallback; failure leaves the file in place.

## M3 Notes

- Incremental scan reuses unchanged rows by size/mtime and only upserts deltas.
- `FileSystemWatcher` debounces folder changes into incremental rescans.
- Search uses SQLite FTS5 (`image_search_fts`) with a LIKE fallback for
  substring compatibility.
- Preview navigation keeps a small ring buffer for previous/current/next images.
- Header-first dimensions are read from PNG/JPEG/GIF headers during scan.
- A native cache scheduler prioritizes preview decode over neighbor warmup.
- Headless performance checks report search/navigation p95 and cache hit rate.
- When the performance target is under this project's `.cache`, the headless
  check also runs a safe add/update/delete mutation probe and records watcher
  events.

M3 headless performance verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder "C:\path\to\images" -Search "name" -PerfIterations 40
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder "C:\path\to\images"
```

## M4 Notes

- `.cache/albums.json` imports full image membership into SQLite `album_images`
  in addition to the existing `albums` summary rows.
- Browser `pvu_*` localStorage imports use an explicit JSON export file only.
  The default path is `.cache/native/browser-localstorage-export.json`; Chrome
  profile storage is never read.
- Thumbnail/display cache reuse is measured before use. The native check uses
  the browser cache-key formula and verifies a WebP header, then records
  compatible, missing, and incompatible counts without deleting or regenerating
  cache assets.

M4 headless parity and cache checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder "C:\path\to\images"
```

## M5 Notes

M5 adds a repeatable ignored fixture generator for release-candidate checks.
It writes deterministic files under `.cache/native-fixture`, `.cache/native`,
`.cache/native-fixture-large`, `.cache/thumbs`, and `.cache/display`. It
creates `.cache/favorites.json`, `.cache/albums.json`, `.cache/settings.json`,
and the default browser localStorage export only when those files are absent;
existing user state files are left in place.

Prepare the fixture:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
```

The generated fixture is intended for the existing M4 headless import, scan,
performance, and cache compatibility commands. It is not a browser export
helper and does not read Chrome profile storage.

## M7 Notes

M7 adds an automated WinForms UI smoke for native acceptance evidence. It starts
the native desktop form, scans the fixture folder, loads direct image preview,
uses previous/next and keyboard navigation, changes favorite level through the
keyboard shortcut, toggles grid mode, checks search/favorites/no-results,
checks missing-folder error status, and confirms the enhancement jobs state did
not change.

It remains a native UI smoke, not browser E2E, not a webview, and not a local
HTTP server check.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
```

## M8 Notes

M8 first slice adds native folder visibility buckets, sort/display controls,
preview panel toggles, detail toggles, thumbnail-size control, and keybinding
metadata for the new preview/details/reshuffle shortcuts. The native UI smoke
now verifies those controls in addition to the M7 scan, navigation, search,
favorite, missing-folder, import, and enhancement-isolation checks.

This is still a parity slice, not a full native parity claim. Remaining rows
stay deferred in `tasks/local-native-m5/browser-regression-matrix.md`.

## M9 Notes

M9 adds a separate native detail modal and right-preview/settings parity slice.
The browser app remains untouched. Native detail is opened from the main viewer
with the `Detail` button or `Ctrl+M`; `Open File` and existing external-open
paths remain available separately.

The detail modal supports previous/next over the current filtered order, zoom
in/out, reset, mouse/scrollbar pan, horizontal flip, favorite up/down, and an
open-external command path. The right preview panel records selected count and
persists splitter distance. Native settings now uses a read-only settings
dialog that records the M9 decision: editable keybinding recording is deferred,
but the keybinding metadata includes the new detail controls.

The native UI smoke verifies the M9 controls with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
```

This is still a parity slice, not a full native parity claim. Remaining rows
stay deferred in `tasks/local-native-m5/browser-regression-matrix.md`.

## M10 Notes

M10 adds a small native selection/filter/folder parity slice. The browser app
remains untouched. Native browsing now supports ListView multi-selection,
background selection clear, a search clear button, favorite filter choices with
counts for all/favorites/unrated/levels 1-5, and selected folder-bucket
show/hide/clear controls. The repeatable fixture now includes one nested
folder image so folder bucket controls have native acceptance evidence.

The native UI smoke verifies the M10 controls with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
```

This is still a parity slice, not a full native parity claim. Multi-root
folder sets, folder range selection, preview tabs, bulk destructive actions,
and explicit enhancement UI remain deferred in
`tasks/local-native-m5/browser-regression-matrix.md`.

## M11 Notes

M11 adds native multi-root folder-set behavior without touching the browser app.
The native UI can scan a semicolon-separated folder set, search across roots,
watch all active roots, remove a root from the active set, reopen the persisted
recent folder set, and manually refresh the current set. The repeatable fixture
now also creates `.cache/native-fixture-extra` so acceptance can cover at least
two roots.

The new folder-set smoke verifies the M11 path with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture
```

This remains a parity slice, not full native parity. Folder range selection is
explicitly deferred for a later product/UI decision; preview tabs, bulk
destructive actions, date/seen/scroll parity, and explicit enhancement UI also
remain deferred in `tasks/local-native-m5/browser-regression-matrix.md`.

## M12 Notes

M12 resolves the deferred folder range-selection decision and adds a small
native gallery-state restore slice. Folder range selection stays deferred:
WinForms `CheckedListBox` rejects `SelectionMode.MultiExtended`, so safe range
behavior needs a future product/UI decision for replacing or custom-wrapping the
folder bucket control.

Native gallery state now persists `last_selected_image` and
`last_visible_index` in SQLite settings. After scan or filter refresh, the UI
restores the saved image when it is still visible, falls back to the saved
visible index, and calls `EnsureVisible` for the restored item. The native UI
smoke verifies this with `galleryStateRestore=true`.

This remains a parity slice, not full native parity. Date sections, seen/unseen
state, large-fixture virtualized scroll proof, preview tabs, bulk destructive
actions, explicit enhancement UI, and folder range selection remain deferred in
`tasks/local-native-m5/browser-regression-matrix.md`.

## M13 Notes

M13 adds evidence for the existing native virtual gallery behavior on a larger
fixture. It does not redesign the UI, add a new visual concept, touch the
browser app, or rename the product. The native lane remains a local-software
port of the existing PhotoViewer workflows.

`-PrepareFixture` now also writes `.cache/native-fixture-large` with 240
deterministic images. The new large-scroll smoke selects item 180 in the
virtualized WinForms list, persists `last_selected_image` and
`last_visible_index`, clears selection, reapplies the current filter, restores
the same selected item, and verifies that `EnsureVisible` leaves the restored
item in view.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large
```

This remains a parity slice, not full native parity. Date sections, seen/unseen
state, drag/open parity, placeholder behavior, native thumbnail warmup UI,
preview tabs, bulk destructive actions, explicit enhancement UI, and folder
range selection remain deferred in
`tasks/local-native-m5/browser-regression-matrix.md`.

## M14 Notes

M14 first slice adds native seen/unseen gallery-state parity without touching
the browser app. Browser evidence maps from `pvu_seen_images`: the browser
marks an image seen when it is selected in the grid/list or opened in the
detail modal, stores the state in localStorage, and renders unseen images with a
small marker.

Native now imports explicit `pvu_seen_images` browser-state exports into a
SQLite `seen_images` table, shows unseen rows with a minimal `NEW` prefix in
the existing list/grid text, and marks the selected preview image as seen in
native SQLite. The state is read through the same native image queries used by
scan, search, and UI smoke.

The M14 seen-state smoke verifies browser-export import and native persistence:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSeenSmoke
```

This remains a parity slice, not full native parity. Date sections/date filter
controls, folder range selection, drag/open parity, placeholder behavior,
native thumbnail warmup UI, preview tabs, bulk destructive actions, and
explicit enhancement UI remain deferred in
`tasks/local-native-m5/browser-regression-matrix.md`.

## M15 Notes

M15 first slice adds a browser-mapped native date filter preset row without
touching the browser app. Browser evidence maps from the existing Sidebar date
filter controls: `Today`, `7d`, `30d`, `This year`, and `Clear` update
`dateFrom` / `dateTo` and the search route filters by image `createdAt`.

Native now adds a `Date` preset selector with `All dates`, `Today`, `7d`,
`30d`, and `This year`. The filter uses `NativeImageRecord.CreatedAtUtc` as a
local calendar date, composes with the existing search, favorite, folder, and
sort controls, and persists `date_filter` in native SQLite settings. The M15
date-filter smoke creates relative-date fixture images and verifies each
browser-mapped preset without browser runtime, local HTTP server, or Node:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateFilterSmoke
```

This remains a parity slice, not full native parity. Date section headers,
manual date range inputs, folder range selection, drag/open parity, placeholder
behavior, native thumbnail warmup UI, preview tabs, bulk destructive actions,
and explicit enhancement UI remain deferred in
`tasks/local-native-m5/browser-regression-matrix.md`.

## M16 Notes

M16 first slice adds browser-mapped native date header behavior for the Created
sort list view without touching the browser app. Browser evidence maps from
the existing date section layout and fallback date separators: date groups are
based on local `createdAt` day, labels render as `M月D日`, and date separators
are tied to created-date sorting.

Native now marks the first visible image for each local `CreatedAtUtc` date in
Created-sort list view with a `M月D日` header label. The header map is rebuilt
from the same filtered visible list used by search, favorite, folder, sort,
and date preset controls. Native grid date headers remain explicitly deferred
for a later layout decision.

The M16 date-section smoke verifies header labels, Created sort order,
Today-filter regrouping, grid suppression, and passive enhancement isolation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateSectionSmoke
```

This remains a parity slice, not full native parity. Native grid date section
layout, manual date range inputs, folder range selection, drag/open parity,
placeholder behavior, native thumbnail warmup UI, preview tabs, bulk
destructive actions, and explicit enhancement UI remain deferred in
`tasks/local-native-m5/browser-regression-matrix.md`.

## M17 Notes

M17 first slice extends the M16 browser-mapped date header behavior to the
native grid view without touching the browser app. It keeps the existing
WinForms virtual `ListView` grid surface and uses the same filtered visible
list, Created sort, local `CreatedAtUtc` day grouping, and first-item date
header marker that M16 introduced for list view.

The M17 date-section smoke verifies both list and grid date headers. It checks
that grid headers are present after clearing the date filter and that the
native grid still collapses to one date group after the Today preset filter:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateSectionSmoke
```

This remains a parity slice, not full native parity. Manual date range inputs,
folder range selection, drag/open parity, placeholder behavior, native
thumbnail warmup UI, preview tabs, bulk destructive actions, explicit
enhancement UI, and screenshot polish remain deferred in
`tasks/local-native-m5/browser-regression-matrix.md`.

## M18 Notes

M18 first slice maps the browser manual `dateFrom` / `dateTo` controls into
the native date filter row without touching the browser app. Native now shows
checked `From` and `To` date inputs beside the existing date preset selector.
Preset choices still populate the same inclusive local date ranges, while
manual edits select `Custom range` and persist `date_from` / `date_to` in
native SQLite settings.

The date-filter smoke now verifies manual range, from-only, to-only, search
composition, favorite-filter composition, and persisted range state. The
date-section smoke verifies that manual ranges also rebuild Created-sort list
and grid header groups:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateFilterSmoke
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessDateSectionSmoke
```

This remains a parity slice, not full native parity. Folder range selection
stays deferred pending a replacement/custom folder bucket control and
browser-mapped UI semantics. Enhanced-only filtering, drag/open parity,
placeholder behavior, native thumbnail warmup UI, preview tabs, bulk
destructive actions, explicit enhancement UI, and screenshot polish also remain
deferred in `tasks/local-native-m5/browser-regression-matrix.md`.

## M19 Notes

M19 maps the browser enhanced-only filter into native only where the existing
browser behavior has a clean local source: succeeded enhancement jobs with
non-empty output paths in `.cache/enhance/jobs.json`. Native now shows an
`Enhanced` checkbox in the toolbar, reads that job file read-only, filters by
`sourceId` / `sourcePath`, and persists `enhanced_only_filter` in native
SQLite settings.

M19 does not add broad explicit enhancement UI. It does not enqueue jobs,
start workers, call browser enhancement APIs, manage outputs, or modify the
browser app. Fixture preparation writes a minimal succeeded enhancement job
only when no `.cache/enhance/jobs.json` exists.

The enhanced-filter smoke verifies accepted behavior and passive isolation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessEnhancedFilterSmoke -Folder .\.cache\native-fixture
```

This remains a parity slice, not full native parity. Folder range selection
stays deferred pending a replacement/custom folder bucket control and
browser-mapped UI semantics. Explicit enhancement queue/settings/cancel/retry/
open/delete output/source UI, original/enhanced preview toggles, drag/open
parity, placeholder behavior, native thumbnail warmup UI, preview tabs, bulk
destructive actions, full browser API/error parity, and screenshot polish also
remain deferred in `tasks/local-native-m5/browser-regression-matrix.md`.

## M20 Notes

M20 changes the milestone shape from another small filter-count-label slice to
`Local Native Migration v1 Closeout Gate`. The local-native v1 finish line is:
the native viewer is usable for normal local browsing without Node, browser
runtime, webview, localhost server, automatic enhancement workers, deployment,
H000033, or `src/**` changes.

M1-M19 already cover the core local viewer path: folder/folder-set scan,
SQLite state, explicit browser-state import, search, favorites, albums,
folder visibility, sorting/display controls, selection, preview, detail modal,
gallery restore, large virtual scroll, seen/unseen state, date filters/date
headers, and read-only enhanced-only filtering from succeeded jobs.

M20 therefore does not add a new implementation slice unless verification
finds a true v1 blocker. Remaining richer count labels, API/error parity,
desktop UI polish, drag/open/placeholder/thumb-warmup details, preview tabs,
bulk destructive confirmations, and full `pvu_*` coverage move to post-v1
optimization. Folder range selection and explicit enhancement queue/output UI
are also tracked in milestone #26 `Local Native Post-v1 Backlog` and do not
block M20 unless explicitly reclassified as v1-required. Automatic enhancement
workers, deployment, H000033 work, and full browser parity as the M20 finish
line remain out of scope.

See `docs/local-native/m20-verification.md` and
`tasks/local-native-m20/task.md` for the v1 closeout gate, Japanese M1-M19
inventory, and remaining-row classification.

## Post-v1 #116 Notes

#116 records the native browser API/error parity matrix after v1. It does not
make the native app an HTTP API clone of the browser app. The accepted target is
native UI/headless error equivalence, with broader implementation split into
post-v1 issues #97-#118.

See `docs/local-native/api-error-parity-matrix.md` and
`tasks/local-native-post-v1-api-error-parity/task.md`.

## Post-v1 #115 Notes

#115 adds bounded native recovery for malformed imported state. Favorites,
albums, settings, and explicit browser localStorage export JSON still fall back
safely, but native now records recoverable warnings, stores a recovery summary
in SQLite settings, prints headless warning lines, and shows warning/recovery
text from the native Import/Settings surfaces.

The dedicated smoke uses a synthetic project root under ignored `.cache/**` and
does not overwrite real user state:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-malformed-import-smoke
```

See `docs/local-native/malformed-import-recovery.md` and
`tasks/local-native-post-v1-malformed-import-recovery/task.md`.

## Post-v1 #117 Notes

#117 starts complete `pvu_*` state persistence migration as a bounded
key-by-key lane. The first accepted row imports explicit browser
`pvu_view.viewMode` into native `view_mode` when native has no saved view mode
yet, while preserving later native choices. The next accepted row imports
explicit browser `pvu_enhanced_only` into native `enhanced_only_filter` under
the same first-import-only rule. The third accepted row imports explicit
browser `pvu_fav_only` / `pvu_unfav_only` into native `favorite_filter` as
`favorites`, `unrated`, or `all`, also only when native has no saved favorite
filter yet. The fourth accepted row imports explicit browser
`pvu_view.dateFrom` / `dateTo` into native `date_filter`, `date_from`, and
`date_to` under the same first-import-only rule. The fifth accepted row imports
explicit browser `pvu_last_dir_set` / `pvu_recent_dirs` into native
`recent_folder_set` / `recent_folder`, while preserving existing native recent
folder state after scan or user changes.

The dedicated smoke uses a synthetic project root under ignored `.cache/**`:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke
```

See `docs/local-native/pvu-state-persistence-migration.md` and
`tasks/local-native-post-v1-pvu-state-persistence/task.md`.

## Post-v1 #118 Notes

#118 starts native desktop UI polish and screenshot sweep evidence as a
post-v1 prep lane. It does not start a broad UI rewrite, does not touch the
browser app, and does not add preview-tab, enhancement-queue, display-mode,
destructive-flow, metadata, or keybinding-recorder work.

The native screenshot helper captures the current WinForms desktop layout after
fixture scan/search/preview setup and reports conservative layout counters:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-ui-screenshot .\.cache\native-fixture .\tasks\local-native-post-v1-ui-polish-screenshot-prep\artifacts\native-ui-screenshot.png --search fixture
```

The #118 first slice also shortens the top-right state summary so it stays on
one line in the 1280x820 desktop capture. Existing native UI smoke remains the
acceptance evidence for keyboard operation, detail modal controls, focusable
controls, and passive enhancement isolation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
```
