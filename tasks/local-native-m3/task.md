# Local Native M3 - Native Performance

## Goal

Add native-only acceleration on top of the M2 browser workflow drop-in without
touching the existing Next.js browser app.

## Implemented Shape

- Incremental scan compares existing SQLite rows by size/mtime and only upserts
  changed files while removing deleted paths.
- `FileSystemWatcher` debounces folder changes into incremental rescans in the
  WinForms UI.
- SQLite FTS5 (`image_search_fts`) backs indexed search for filename, folder,
  and absolute path tokens, with LIKE fallback for substring compatibility.
- Preview navigation uses a small ring buffer plus a priority cache scheduler.
- PNG/JPEG/GIF header parsing stores width/height before full decode.
- Headless commands report search/navigation p95, cache hit rate, and header
  coverage.
- The headless performance command runs a safe add/update/delete mutation probe
  with watcher-event evidence when the target folder is under project `.cache`.

## Headless Verification

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

## Allowed Paths

- `local-native/**`
- `scripts/start-local-native.ps1`
- `docs/local-native/**`
- `tasks/local-native-m3/**`

## Next Goal Candidate

M4 should focus on deeper browser parity: album membership, browser export
import for `pvu_*` state, and optional thumbnail/display cache reuse when key
compatibility is proven.
