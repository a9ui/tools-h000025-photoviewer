# Local Native M4 Verification

Date: 2026-07-08

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search xture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
sqlite3 .\.cache\native\photoviewer-native.sqlite "select 'images', count(*) from images union all select 'favorites', count(*) from favorites union all select 'albums', count(*) from albums union all select 'album_images', count(*) from album_images union all select 'browser_state', count(*) from browser_state union all select 'cache_checks', count(*) from cache_compatibility union all select 'settings', count(*) from native_settings union all select 'import_runs', count(*) from import_runs;"
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

## Results

- Native build passed with 0 warnings and 0 errors.
- Headless import completed:
  - favorites: 1
  - albums: 2
  - album image membership rows: 3
  - explicit browser `pvu_*` state keys: 5
  - native settings rows after import: 11
  - images at import time: 0
- Headless scan completed:
  - images: 3
  - stored favorites: 1
  - elapsed: 4 ms
- Headless incremental scan completed:
  - images: 3
  - addedOrUpdated: 0
  - removed: 0
  - unchanged: 3
  - elapsed: 4 ms
- Headless indexed search completed:
  - query `fixture`: 3 matches, `indexed=true`
  - substring fallback query `xture`: 3 matches, `indexed=false`
- Headless performance completed (20 iterations):
  - search p95: 0.65 ms
  - navigation p95: 14.79 ms
  - cache hit rate: 95.0%
  - header coverage: 100.0%
  - mutation probe: added 1, updated 1, removed 1
  - watcher events observed: 3
- Headless cache compatibility completed:
  - images checked: 3
  - thumbnail compatible: 1
  - thumbnail missing: 1
  - thumbnail incompatible: 1
  - display compatible: 1
  - display missing: 2
  - display incompatible: 0
- SQLite counts after checks:
  - `images`: 3
  - `favorites`: 1
  - `albums`: 2
  - `album_images`: 3
  - `browser_state`: 5
  - `cache_compatibility`: 1
  - `native_settings`: 12
  - `import_runs`: 4
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `git diff --name-only -- src` returned no files.

## Notes

The isolated worktree did not contain an existing `.cache` folder. The M4
headless checks used ignored fixture files under `.cache/native-fixture`,
`.cache/native`, `.cache/thumbs`, and `.cache/display`. No `src/**` browser app
files changed, no deployment was attempted, Linear was not used, and no
enhancement workers were started.
