# Local Native M5 Verification

Date: 2026-07-08

Product-intent source: `docs/local-native/native-intent-source.md`, based on
Codex thread `019f3c2e-577f-7421-a499-145ece67eb30`.

## Commands

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
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
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

## Results

- Native build passed with 0 warnings and 0 errors.
- Fixture preparation completed:
  - images: 3
  - first isolated run created state files:
    `.cache/favorites.json`, `.cache/albums.json`, `.cache/settings.json`,
    `.cache/native/browser-localstorage-export.json`
  - final repeat run created state files: none
  - final repeat run skipped existing state files:
    `.cache/favorites.json`, `.cache/albums.json`, `.cache/settings.json`,
    `.cache/native/browser-localstorage-export.json`
  - deterministic cache classification target:
    thumbnail compatible/missing/incompatible `1 / 1 / 1`, display
    compatible/missing/incompatible `1 / 2 / 0`
- Headless import completed:
  - favorites: 1
  - albums: 2
  - album image membership rows: 3
  - explicit browser `pvu_*` state keys: 5
  - native settings rows after import: 12
  - images at import time: 3
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
  - scan: 3 ms
  - search p50: 0.18 ms
  - search p95: 0.40 ms
  - indexed samples: 20 / 20
  - navigation p95: 22.15 ms
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
  - `cache_compatibility`: 2
  - `native_settings`: 12
  - `import_runs`: 7
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `corepack pnpm test:e2e` passed: 2 Chromium tests.
- `powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full`
  passed:
  - required files
  - `pnpm test:unit`
  - `pnpm lint`
  - `pnpm audit --audit-level moderate`
  - `pnpm typecheck`
  - `pnpm build`
  - `pnpm test:e2e`
- `pnpm lint` reported 0 errors and 2 existing warnings for `<img>` usage in
  `src/components/CachedImage.tsx`.
- `git diff --name-only -- src` returned no files.

Final post-canonical-intent rerun after the browser-baseline smoke also passed:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `-PrepareFixture` passed and skipped existing state files; no favorites,
  albums, settings, or browser export state was overwritten.
- `-HeadlessImport` passed with favorites 1, albums 2, album membership rows 3,
  browser state keys 5, settings 12, images 3.
- `-HeadlessScan` passed with images 3, favorites 1, imported favorites 1,
  elapsed 4 ms.
- `-HeadlessPerf -PerfIterations 20` passed:
  - scan 3 ms
  - search p50 0.22 ms
  - search p95 0.48 ms
  - navigation p95 8.03 ms
  - cache hit rate 95.0%
  - header coverage 100.0%
  - mutation probe added 1, updated 1, removed 1
  - watcher events 3
- `-HeadlessCacheCompat` passed after the cache had been warmed by the
  browser-baseline smoke:
  - images checked: 3
  - thumbnail compatible: 2
  - thumbnail missing: 0
  - thumbnail incompatible: 1
  - display compatible: 3
  - display missing: 0
  - display incompatible: 0
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `git diff --name-only -- src` returned no files.

## Browser Smoke

M5 also ran a one-off Playwright smoke against a temporary local dev server at
`http://localhost:3001` using the deterministic fixture folder
`.cache/native-fixture`.

This was browser-baseline evidence only. It does not count as native acceptance
and does not change the product direction: the local-native target must remain
a fast native local app with no Node, browser runtime, webview wrapper, or
local HTTP server dependency for normal use.

Observed browser smoke results:

- landing page rendered `PhotoViewer`, `Add folder`, and `Open folder set`
- pasted fixture path was added and removable
- folder scan completed through the browser API path
- viewer showed 3 indexed images and 3 image elements
- sidebar/filter text was present, including favorites, unrated, enhanced,
  date, folder visibility, sort, and display sections
- search for `fixture` returned fixture results with no empty-result state
- modal opened from the first image and exposed counter/action controls
- passive landing, browsing, searching, and modal opening created no enhancement
  jobs: `beforeJobsCount=0`, `afterJobsCount=0`
- browser console errors/warnings captured during the smoke: none

Server log evidence during the smoke included successful calls to:

- `/api/scan`
- `/api/search`
- `/api/folders`
- `/api/thumbs/warm`
- `/api/image`
- `/api/tags`
- `/api/favorites`
- `/api/settings`
- `/api/enhance/jobs`

## Browser Feature Baseline And Native Parity Coverage

The existing browser E2E coverage is only a smoke check for the landing folder
workflow and last-folder restoration. It does not prove the full browser
feature set one by one, and browser checks alone do not prove native parity.

M5 therefore added
`tasks/local-native-m5/browser-regression-matrix.md` as a blocking M6 merge
gate. M6 must verify or explicitly classify every browser-feature/native-parity
row before the local-native stack is marked merge-ready.

## GitHub Stack Check

- PR #43 `Build local native viewing MVP`: draft, open, base `main`, head
  `codex/h25-local-native-m1`, head SHA
  `a85b2287adf63cc42e7001129cb5dec090a2adfa`, verify run
  `28867872288` succeeded, `mergeStateStatus=CLEAN`.
- PR #48 `Build local native browser workflow drop-in`: draft, open, base
  `codex/h25-local-native-m1`, head `codex/h25-local-native-m2`, head SHA
  `d2428db45a4f2dd2f0723b2889181e5f8fa08ea1`, verify run
  `28869670313` succeeded, `mergeStateStatus=CLEAN`.
- PR #55 `Build local native M3 performance acceleration`: draft, open, base
  `codex/h25-local-native-m2`, head `codex/h25-local-native-m3`, head SHA
  `43caeef1e94a816a55f28c1b5990eef30bda94e9`, verify run
  `28872084793` succeeded, `mergeStateStatus=CLEAN`.
- PR #61 `Build local native M4 parity and cache reuse`: draft, open, base
  `codex/h25-local-native-m3`, head `codex/h25-local-native-m4`, head SHA
  `69cb93c1f9d90024e03f328674ce63aaf01f7e6f`, verify run
  `28881130949` succeeded, `mergeStateStatus=CLEAN`.
- `gh pr diff --name-only` for PRs #43, #48, #55, and #61 returned no
  `src/**` files.
- M2, M3, and M4 milestone issues are closed. M1 issues #37-#42 were still
  open at the start of M5 and should be closed with the M1 verification
  comment before closing M5.

## Notes

M5 still does not change the browser app. The fixture command writes ignored
local test assets and avoids existing browser state files when they are
present. It does not read browser profile storage, start enhancement workers,
delete cache assets, use Linear, deploy, or touch H000033.
