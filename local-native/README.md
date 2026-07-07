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
