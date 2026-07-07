# Local Native M1 - Viewing MVP

## Goal

Build the isolated local-native PhotoViewer browsing MVP inside H000025 without
touching the existing browser app or H000033.

## GitHub Route

- Milestone: `Local Native M1 - Viewing MVP`
- Label: `local-native`
- Issues:
  - #37 `M1-001 Control route and baseline verification`
  - #38 `M1-002 Native SQLite store and import report`
  - #39 `M1-003 Fast scanner and indexed image rows`
  - #40 `M1-004 Native viewer browsing MVP`
  - #41 `M1-005 Favorites/filter/search minimum parity`
  - #42 `M1-006 Verification pack and next Goal handoff`

Linear is intentionally not used.

## Implemented Shape

- Native app remains under `local-native/PhotoViewer.Native`.
- Parent launcher remains `scripts/start-local-native.ps1`.
- SQLite store lives at `.cache/native/photoviewer-native.sqlite`.
- Existing `.cache/favorites.json` imports to SQLite favorites.
- Scan writes image rows and scan root metadata to SQLite.
- UI supports folder scan, virtual list, direct preview, favorite-only filter,
  and filename/folder/path search.
- Headless verification path:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\display
```

## Current Verification

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- Headless scan of `.cache\display` completed:
  - images: 3,704
  - imported favorites: 26,866
  - elapsed: 110 ms
  - database: `.cache\native\photoviewer-native.sqlite`
- SQLite counts after scan:
  - `images`: 3,704
  - `favorites`: 26,866
  - `scan_roots`: 1
  - `import_runs`: 1
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 17 files, 103 tests.
- `git diff --name-only -- src` returned no files.

## Next Goal Candidate

M2 should focus on UI parity and native browsing ergonomics:

- grid view in addition to list view
- previous/current/next preview preload
- favorite level mutation and persistence
- recent folder restore
- delete/open through Windows APIs
- measured handling for WebP/AVIF preview fallback
