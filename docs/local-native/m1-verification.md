# Local Native M1 Verification

Date: 2026-07-07

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\display
sqlite3 .\.cache\native\photoviewer-native.sqlite "select 'images', count(*) from images union all select 'favorites', count(*) from favorites union all select 'scan_roots', count(*) from scan_roots union all select 'import_runs', count(*) from import_runs;"
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

## Results

- Native build passed with 0 warnings and 0 errors.
- Headless scan completed 3,704 image rows from `.cache\display` in 110 ms.
- Existing `.cache\favorites.json` imported 26,866 favorite rows.
- SQLite counts:
  - `images`: 3,704
  - `favorites`: 26,866
  - `scan_roots`: 1
  - `import_runs`: 1
- TypeScript typecheck passed.
- Unit tests passed: 17 files, 103 tests.
- No `src/` files changed.

## Notes

The native app still intentionally avoids thumbnail generation. M1 proves the
fast local path: direct filesystem scan, SQLite persistence, virtual list, and
direct preview. Browser `localStorage` state is not read directly.
