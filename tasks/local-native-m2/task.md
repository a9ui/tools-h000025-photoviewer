# Local Native M2 - Browser Feature Drop-in

## Goal

Recreate the important browser PhotoViewer workflows inside the isolated native
lane while preserving the existing browser app.

## GitHub Route

- Milestone: `Local Native M2 - Browser Feature Drop-in`
- Label: `local-native-m2`
- Issues:
  - #44 `M2-001 Native browser workflow shell and SQLite state`
  - #45 `M2-002 Native favorite mutation, open, and trash-safe delete`
  - #46 `M2-003 Browser cache compatibility import map`
  - #47 `M2-004 Headless verification pack and PR strategy`

Linear is intentionally not used.

## Implemented Shape

- Native app remains under `local-native/PhotoViewer.Native`.
- Parent launcher remains `scripts/start-local-native.ps1`.
- Native state source remains `.cache/native/photoviewer-native.sqlite`.
- UI now includes:
  - list/grid mode toggle,
  - current/previous/next selection navigation,
  - search and favorite-only filtering,
  - favorite level mutation and persistence,
  - recent folder restore,
  - open file and Explorer select-folder actions,
  - Recycle Bin delete using Windows shell-safe API with no hard-delete fallback,
  - native settings/key-binding state display.
- Compatibility imports now store:
  - `.cache/favorites.json` into `favorites`,
  - `.cache/albums.json` summary rows into `albums`,
  - `.cache/settings.json` raw JSON marker into `native_settings`.

## Not Yet Imported

Browser `pvu_*` localStorage keys are still not read directly. They need an
explicit browser export file or a small compatibility helper in a later
milestone. Chrome profile storage remains out of scope.

## Headless Verification

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

## PR Strategy

Stack M2 on PR #43 by pushing branch `codex/h25-local-native-m2` after PR #43.
This avoids mixing the unrelated unpublished local `main` browser-app commits
visible in the desktop checkout. No `src/` changes are part of M2.

## Next Goal Candidate

M3 should focus on native acceleration:

- incremental scan/watchers,
- indexed search,
- modal/preview ring buffer,
- header-first dimensions,
- native cache scheduler,
- measured p95 navigation.
