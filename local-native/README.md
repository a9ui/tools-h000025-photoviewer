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
`.cache/thumbs`, and `.cache/display`. It creates `.cache/favorites.json`,
`.cache/albums.json`, `.cache/settings.json`, and the default browser
localStorage export only when those files are absent; existing user state files
are left in place.

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
