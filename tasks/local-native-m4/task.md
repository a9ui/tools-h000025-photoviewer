# Local Native M4 - Native Parity And Cache Reuse

## Goal

Deepen the isolated local-native lane after M3 with album membership parity,
explicit browser `pvu_*` export/import handling, and measured optional reuse of
existing thumbnail/display cache assets when cache-key compatibility is proven.

## GitHub Route

- Milestone: `Local Native M4 - Native Parity And Cache Reuse`
- Label: `local-native-m4`
- Issues:
  - #59 `M4-001 Album membership parity`
  - #60 `M4-002 Browser pvu export import handling`
  - #56 `M4-003 Cache key compatibility checks`
  - #57 `M4-004 Verification pack, PR, and next handoff`

Linear is intentionally not used.

## Implemented Shape

- Native app remains under `local-native/PhotoViewer.Native`.
- Parent launcher remains `scripts/start-local-native.ps1`.
- Browser `src/**` files are unchanged.
- Native state source remains `.cache/native/photoviewer-native.sqlite`.
- SQLite now includes:
  - `album_images` for full album membership imported from `.cache/albums.json`,
  - `browser_state` for explicit `pvu_*` localStorage export imports,
  - `cache_compatibility` for measured thumbnail/display compatibility checks.
- Album import accepts common membership shapes:
  - string arrays in `images`, `imageIds`, `paths`, or `items`,
  - object arrays with `absolutePath`, `path`, `filePath`, `id`, `imageId`, or
    `src`.
- Browser localStorage import uses only an explicit JSON export file:
  - default: `.cache/native/browser-localstorage-export.json`,
  - or `-BrowserStateExport <path>` on the launcher.
- Cache compatibility checks use the browser key formulas from
  `src/lib/thumbnailCache.ts`, verify WebP headers, and only record compatible,
  missing, and incompatible counts. They do not delete, regenerate, or rewrite
  cache files.

## Headless Verification

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

Verification details are recorded in
`docs/local-native/m4-verification.md`.

## Next Goal Candidate

M5 should focus on release-candidate readiness for the local-native stack:
stacked PR cleanup/merge order, native parity review, repeatable fixture
generation, and deciding whether a browser export helper is worth adding later
without making the browser app depend on the native lane.
