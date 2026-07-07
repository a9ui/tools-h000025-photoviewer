# Local Native M2 Verification

Date: 2026-07-07

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture -FavoritesOnly
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -FavoritePath .\.cache\native-fixture\m2-fixture-2.png -FavoriteLevel 4
sqlite3 .\.cache\native\photoviewer-native.sqlite "select 'images', count(*) from images union all select 'favorites', count(*) from favorites union all select 'albums', count(*) from albums union all select 'settings', count(*) from native_settings union all select 'scan_roots', count(*) from scan_roots union all select 'import_runs', count(*) from import_runs;"
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

## Results

- Native build passed with 0 warnings and 0 errors.
- `.cache/display` was not present in this isolated worktree, so a local ignored
  fixture folder `.cache/native-fixture` was generated for headless checks.
- Headless import completed:
  - favorites: 1
  - albums: 1
  - native settings rows: 5
  - images at import time: 3
- Headless scan completed:
  - images: 3
  - stored favorites before mutation: 1
  - elapsed: 4 ms
- Headless search completed:
  - query `fixture`: 3 matches
  - query `fixture` with favorite-only before mutation: 1 match
  - favorite mutation set `m2-fixture-2.png` to level 4
  - query `fixture` with favorite-only after mutation: 2 matches
- SQLite counts after checks:
  - `images`: 3
  - `favorites`: 2
  - `albums`: 1
  - `native_settings`: 5
  - `scan_roots`: 1
  - `import_runs`: 4
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `git diff --name-only -- src` returned no files.

## Notes

M2 uses `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` with
`RecycleOption.SendToRecycleBin` for delete. If the shell-safe recycle call
fails, the native app reports the failure and does not perform a hard delete.

Browser `pvu_*` localStorage remains documented but not imported. A future
browser export helper can add that path without reading Chrome profile storage.
