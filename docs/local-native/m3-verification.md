# Local Native M3 Verification

Date: 2026-07-07

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search xture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

## Results

- Native build passed with 0 warnings and 0 errors.
- Headless import completed:
  - favorites: 0
  - albums: 0
  - native settings rows: 4
  - images at import time: 3
- Headless scan completed:
  - images: 3
  - elapsed: 5 ms
  - header dimensions stored for all fixture PNG files
- Headless incremental scan completed:
  - images: 3
  - addedOrUpdated: 0
  - removed: 0
  - unchanged: 3
  - elapsed: 5 ms
- Headless indexed search completed:
  - query `fixture`: 3 matches
  - `indexed=true`
  - substring fallback query `xture`: 3 matches
  - fallback query returned `indexed=false`
- Headless performance completed (20 iterations):
  - search p50: 0.18 ms, p95: 0.68 ms
  - indexed samples: 20 / 20
  - navigation p50: 0.00 ms, p95: 18.84 ms
  - cache hit rate: 95.0%
  - header coverage: 100.0%
  - cache-safe mutation probe: added 1, updated 1, removed 1
  - watcher events observed: 3
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed.
- `git diff --name-only -- src` returned no files.

## Notes

M3 adds native-only acceleration without changing the browser app. Incremental
scan, FTS search, preview ring buffer, header-first dimensions, cache
scheduler, mutation handling, watcher signaling, and headless p95 evidence are
all exercised by the commands above.

The local ignored fixture folder `.cache/native-fixture` was generated for this
worktree because `.cache/display` was not present.
