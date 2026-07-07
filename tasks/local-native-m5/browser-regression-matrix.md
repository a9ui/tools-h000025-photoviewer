# Browser Feature Baseline And Native Parity Matrix

Date: 2026-07-08

## Purpose

Canonical intent source:
`docs/local-native/native-intent-source.md`, from Codex thread
`019f3c2e-577f-7421-a499-145ece67eb30`.

Before the local-native stack is merged or closed as release-candidate ready,
the existing browser PhotoViewer feature set must be checked feature by
feature. This is not a plan to wrap the browser app in a local server. The
release-candidate target is a fast native local app that does not depend on
Node, a browser runtime, or a local HTTP server for normal use.

M5 did not change `src/**`, but the browser app remains the product baseline.
Each browser feature row below needs one of these outcomes before stack merge:

- native equivalent verified,
- intentionally not applicable to native with a concrete reason,
- deferred with owner, risk, and merge decision.

Current automated browser coverage is not enough: `corepack pnpm test:e2e`
currently runs only the landing/folder-memory checks in `e2e/home.spec.ts`.
That proves the browser baseline is alive, not that native parity is done.
This matrix is a blocking M6 input, not optional follow-up.

## Constraints

- Do not modify `src/**` unless an explicitly approved browser export helper is
  scoped, minimal, and verified.
- Do not use Linear.
- Do not deploy.
- Do not touch H000033.
- Do not start automatic enhancement workers.
- Do not delete `.cache/thumbs`, `.cache/display`, `.cache/enhance`,
  favorites, albums, settings, or native SQLite state.
- Do not treat a webview, browser wrapper, Node server, Next.js dev server, or
  localhost route as satisfying native parity.
- Native verification must use `dotnet`/native headless checks and the native
  UI surface, not browser automation, for the acceptance result.
- Any enhancement test must be triggered by an explicit UI/API action and must
  prove ordinary browsing, modal navigation, and previewing create zero new
  enhancement jobs.

## M6 Classification Summary

M6 classifies every row below. It does not claim full native parity.

Decision: `GO_WITH_ORDERED_STACK_MERGE_FOR_ISOLATED_RC_BASELINE`.

Rationale:

- The local-native stack remains isolated from `src/**` and from H000033.
- Native build and headless fixture checks pass without Node, browser runtime,
  webview wrapping, or localhost HTTP APIs.
- Browser E2E and full project verify pass as browser-baseline preservation
  evidence only.
- No row below is marked complete unless the proof is native acceptance.
- Rows with only partial native coverage are `DEFERRED`, with owner and merge
  decision. The next milestone must continue with a native UI parity sweep.

Verifier path note:

- `powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full`
  was listed in M5/M6 handoff text, but this H000025 repo has no
  `System/scripts/verify-project.ps1` path.
- The project source of truth in `project.toml` is
  `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`.
  M6 ran `.\scripts\verify-project.ps1 -Full` and recorded the missing
  `.\System\...` path as a documentation/path correction, not as a product
  regression.

## M6 Feature Sweep

Status values:

- `VERIFIED`: native acceptance fully satisfies the row.
- `DEFERRED`: some or all of the row is not yet native-accepted; the merge
  decision says whether the isolated stack may still merge.
- `BLOCKED`: cannot be decided without human input or unavailable state.

| Area | M6 status | Evidence type | Evidence | Remaining gap | Merge decision |
| --- | --- | --- | --- | --- | --- |
| Landing and folder set | DEFERRED | Native partial acceptance | `dotnet build`; `-PrepareFixture`; `-HeadlessImport`; `-HeadlessScan`; `MainForm` code inspection shows folder textbox, browse dialog, scan, cancel, import, recent folder restore, and watcher refresh. | Full native UI sweep for pasted multi-root sets, remove-folder behavior, open recent folder set, and manual refresh was not exercised. | Merge isolated RC stack; M7 owner `codex_pm` to verify native folder-set UI before native parity claim. |
| Legacy browser state | DEFERRED | Native partial acceptance | `-HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json` imported 5 `pvu_*` keys without reading Chrome profile storage. | Malformed export handling and user-facing error/empty state not UI-tested. | Merge; M7 owner `codex_pm` to add malformed-export fixture or manual UI test. |
| Search bar | DEFERRED | Native partial acceptance | `-HeadlessPerf -Search fixture -PerfIterations 20` passed with indexed samples `20/20`, search p95 `0.50 ms`; M3/M5 evidence covers substring fallback. | Native UI tag entry, multi-token behavior, clear button, and no-results UI not fully swept. | Merge; M7 owner `codex_pm` to verify native search UI and no-results state. |
| Sidebar quick search and filters | DEFERRED | Native partial acceptance | Native store imports favorites; `MainForm` has favorites-only checkbox and favorite level mutation; M2/M5 evidence covers favorite filtering/mutation. | Unrated, favorite level 1-5 filter chips, enhanced-only, count labels, date presets, and manual date range are not native-accepted. | Merge only as RC baseline; M7 owner `codex_pm` to decide which filters are required for parity. |
| Folder visibility | DEFERRED | Native gap | No native folder bucket/sidebar implementation found in M6 code inspection. | Loading/empty folder buckets, sort modes, show/hide all, invert, multi/range selection, show/hide selected, and clear selection remain unimplemented. | Merge allowed because browser app is unchanged and row is explicitly deferred; M7 owner `codex_pm` or `cursor_impl`. |
| Sorting and display | DEFERRED | Native partial acceptance | `MainForm` supports list/grid mode and `ListView.VirtualMode`; verify commands passed. | Modified/created/name/random sorting, reshuffle, compact/poster-equivalent modes, aspect controls, thumbnail size control, and keyboard/wheel zoom equivalents remain pending. | Merge isolated stack; M7 owner `cursor_impl` likely for implementation. |
| Virtual gallery | DEFERRED | Native partial acceptance | `MainForm` uses virtual list/grid; `-HeadlessCacheCompat` measured cache compatibility; `-HeadlessPerf` measured navigation/cache hit rate. | Date sections, scroll memory, seen/unseen state, drag/open parity, placeholder behavior, and native thumbnail warmup UI are not fully accepted. | Merge; M7 owner `codex_pm` to split UI parity vs cache reuse work. |
| Selection and preview tabs | DEFERRED | Native partial acceptance | `MainForm` supports single selection, previous/next buttons, double-click open, preview panel, and ring-buffer-backed preview loading. | Ctrl/Shift multi-selection, background clear, preview tabs, pin/unpin, close/restore closed tab, and hover/quick preview are not native-accepted. | Merge; M7 owner `cursor_impl` after parity spec is narrowed. |
| Right preview panel | DEFERRED | Native partial acceptance | `MainForm` direct preview, favorite level, open file, Explorer select-folder, and Recycle actions exist; build/headless checks passed. | Show/hide and resize persistence, details toggle, selected count, bulk favorite/open/recycle confirmation are pending. | Merge; M7 owner `codex_pm` to require a native right-panel parity pass. |
| Modal navigation | DEFERRED | Native partial acceptance | Native previous/current/next ring buffer and keyboard/button navigation are present; headless navigation p95 `14.95 ms`. | Separate detail modal open/close/reveal, filtered ordered subset wrap behavior, edge/click/swipe equivalents, and loading-slot UI were not accepted. | Merge; do not claim modal parity. M7 owner `cursor_impl`. |
| Modal image controls | DEFERRED | Native gap | No native zoom/reset/pan/flip/immersive detail controls found in M6 code inspection. | Zoom, reset, pan, horizontal flip, chrome hide/show, favorite +/- inside modal, feedback, and open external remain unimplemented. | Merge only as RC baseline; M7 owner `cursor_impl`. |
| Modal metadata | DEFERRED | Native partial acceptance | Header-first dimensions are stored and shown; `MainForm` displays filename, size, dimensions, and favorite level. | Prompt/negative/settings metadata display, prompt tag actions, copy prompt/negative/PNG info, and no-metadata fallback are not native-accepted. | Merge; M7 owner `cursor_impl` or future metadata milestone. |
| Delete flows | DEFERRED | Native partial acceptance | `MainForm.DeleteSelectedImage` uses `FileSystem.DeleteFile(... SendToRecycleBin ...)` and reports failure without hard-delete fallback; M2 docs record this behavior. | Native confirmation/cancel, do-not-ask setting, bulk delete confirmation, disposable Recycle Bin UI test, and error-equivalent UI paths are pending. | Merge; M7 owner `codex_pm` to verify on disposable copies before parity claim. |
| Settings modal | DEFERRED | Native partial acceptance | `ShowNativeSettings` exposes SQLite path, browser settings import marker, and key-binding JSON; import/headless checks passed. | Confirm-before-delete UI, edge/navigation settings, keybinding recorder, malformed settings fallback, and full browser settings parity are pending. | Merge; M7 owner `cursor_impl` after UI requirements are scoped. |
| Enhancement isolation | DEFERRED | Browser baseline only plus native absence | Browser smoke in M5 saw passive browsing/search/modal create zero jobs. M6 code inspection found no native automatic enhancement worker path. | Native explicit enhancement queue/settings/cancel/retry/open/delete output/source/original-enhanced toggle/enhanced-only filter are not implemented. Browser smoke is not native acceptance. | Merge allowed because no automatic native worker exists and browser app is unchanged; next explicit enhancement milestone must own this. |
| Browser APIs and errors | DEFERRED | Native partial acceptance | Headless native routes cover import, scan, indexed search, performance, and cache compatibility. `NativeHeadlessRunner` returns non-zero for missing folders. | Native equivalents for all browser API error cases, tags/folders/settings/image/thumb/display/open/delete/legacy-state/enhancement operations, malformed requests, and missing image/cache UI remain pending. | Merge; M7 owner `codex_pm` to split API parity into native UI/headless tests. |
| Persistence keys | DEFERRED | Native partial acceptance | Explicit export import recorded 5 `pvu_*` keys; SQLite `browser_state` and `native_settings` are populated by native import. | Full parity for `pvu_favorites`, pinned previews, filters, scroll/seen state, recent dirs, server legacy marker, and enhancement settings is pending. | Merge; M7 owner `codex_pm` to expand export fixture or mark keys intentionally out of scope. |
| Native responsive/layout parity | DEFERRED | Native code inspection only | `MainForm` sets desktop size `1280x820`, minimum `900x560`, dense WinForms toolbar/action row/split preview layout. | No native screenshot/manual layout sweep was captured in M6; overlap and polish are not accepted. | Merge; M7 owner `claude_ui`/`codex_pm` for Human Surface review if Claude smoke is available. |

## M7 Native UI Parity Sweep

M7 adds a WinForms UI smoke and reruns the native headless fixture checks. This
section updates the M6 deferred rows with native acceptance evidence where it
exists. It still does not claim full native parity.

M7 smoke command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
```

M7 smoke result:

- runtime: `winforms`
- scannedImages: 3
- initialVisible: 3
- previewLoaded: true
- navigationButtons: true
- keyboardNavigation: true
- keyboardFavorite: true
- gridToggle: true
- searchMatches: 3
- favoriteMatches: 1
- noResultsState: true
- folderErrorState: true
- albums: 2
- albumImages: 3
- browserStateKeys: 5
- settingsImported: true
- enhancementStateUnchanged: true
- browserRuntime/localHttpServer/nodeRuntime: false/false/false

| Area | M7 status | Native evidence added | Still deferred |
| --- | --- | --- | --- |
| Landing and folder set | DEFERRED | WinForms smoke verifies single folder path scan, imported state summary, initial visible images, and missing-folder status. | Multi-root sets, remove-folder behavior, open recent folder set, and manual refresh. |
| Legacy browser state | DEFERRED | Headless import and UI smoke verify default explicit browser export import: 5 `pvu_*` keys, settings marker, albums 2, albumImages 3. | Malformed export UX and user-facing recovery text. |
| Search bar | DEFERRED | UI smoke verifies query `fixture` returns 3 matches and no-results status works; headless indexed search passes. | Tag entry, multi-token semantics, clear button, and search chip behavior. |
| Sidebar quick search and filters | DEFERRED | UI smoke verifies favorites-only filtering returns 1 match after imported favorite state. | Unrated, favorite level chips 1-5, enhanced-only, date presets, manual date range, and richer count labels. |
| Folder visibility | DEFERRED | None beyond single-folder scan. | Folder buckets/sidebar, show/hide all, invert, multi/range selection, show/hide selected, and clear selection. |
| Sorting and display | DEFERRED | UI smoke verifies list/grid toggle and `Ctrl+G` keyboard path. | Modified/created/name/random sorting, reshuffle, compact/poster equivalents, aspect controls, thumbnail size controls, and wheel zoom equivalents. |
| Virtual gallery | DEFERRED | UI smoke verifies virtual list/grid can show 3 fixture images and direct preview; headless perf verifies navigation p95 15.38 ms and cache hit rate 95.0%. | Date sections, scroll memory, seen/unseen state, drag/open parity, placeholder behavior, and native thumbnail warmup UI. |
| Selection and preview tabs | DEFERRED | UI smoke verifies single selection, previous/next button state, keyboard previous navigation, and preview load. | Ctrl/Shift multi-selection, background clear behavior, preview tabs, pin/unpin, close/restore closed tab, hover/quick preview. |
| Right preview panel | DEFERRED | UI smoke verifies direct preview and favorite-level keyboard mutation. | Show/hide and resize persistence, details toggle, selected count, bulk favorite/open/recycle confirmation. |
| Modal navigation | DEFERRED | UI smoke and headless perf verify native main-view previous/current/next navigation and direct preview. | Separate detail modal parity, filtered subset wrap behavior, edge/click/swipe equivalents, and loading-slot UI. |
| Modal image controls | DEFERRED | None. | Zoom, reset, pan, horizontal flip, chrome hide/show, favorite +/- inside modal, feedback, and open external. |
| Modal metadata | DEFERRED | UI smoke verifies preview loaded; M6 code path shows filename/size/dimensions/favorite label. | Prompt/negative/settings metadata, prompt tag actions, copy prompt/negative/PNG info, and no-metadata fallback. |
| Delete flows | DEFERRED | M6 code inspection keeps Recycle Bin path without hard-delete fallback. M7 smoke intentionally avoids destructive Recycle operations. | Confirmation/cancel, do-not-ask setting, bulk delete confirmation, disposable Recycle Bin UI test, and error-equivalent UI paths. |
| Settings modal | DEFERRED | UI smoke verifies imported settings marker and key-binding data are present in native store. | Non-blocking settings UI automation, confirm-before-delete UI, edge/navigation settings, keybinding recorder, malformed settings fallback, and full browser settings parity. |
| Enhancement isolation | DEFERRED | UI smoke verifies ordinary native browsing/search/preview did not change `.cache/enhance/jobs.json` state and reports no browser/server/node runtime. | Explicit enhancement queue/settings/cancel/retry/open/delete output/source/original-enhanced toggle/enhanced-only filter. |
| Browser APIs and errors | DEFERRED | Headless native routes cover import, scan, incremental scan, indexed search, performance, cache compatibility; UI smoke covers missing-folder status. | Full native equivalents for tags/folders/settings/image/thumb/display/open/delete/legacy-state/enhancement errors. |
| Persistence keys | DEFERRED | Explicit export imports 5 `pvu_*` keys and UI smoke verifies browserStateKeys 5. | Full parity for pinned previews, filters, scroll/seen state, recent dirs, server legacy marker, and enhancement settings. |
| Native responsive/layout parity | DEFERRED | UI smoke creates and exercises the WinForms layout at the native desktop size. | Screenshot/manual layout sweep for overlap, text fit, and polish. |

## M8 Native Deferred Parity Implementation Slice

M8 first slice implements a small set of high-value deferred controls and keeps
the remaining rows explicit. It still does not claim full native parity.

M8 smoke command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
```

M8 smoke result additions:

- folderBuckets: 1
- folderHideAll: true
- sortName: true
- randomReshuffle: true
- thumbnailSize: true
- previewToggle: true
- detailsToggle: true

| Area | M8 status | Native evidence added | Still deferred | Owner | Evidence requirement |
| --- | --- | --- | --- | --- | --- |
| Landing and folder set | DEFERRED | No new landing/multi-root implementation in M8. Existing single-folder scan remains covered by M7/M8 UI smoke. | Multi-root sets, remove-folder behavior, open recent folder set, and manual refresh. | `codex_pm` | Native UI/headless test that pastes or imports a multi-root set and proves remove/open-recent/manual refresh behavior without browser runtime. |
| Legacy browser state | DEFERRED | Existing explicit export import remains covered; no malformed-export UI added in M8. | Malformed export UX and user-facing recovery text. | `codex_pm` | Fixture with malformed export plus native UI/headless error output and recovery state. |
| Search bar | DEFERRED | Existing query/no-results/favorites search remains covered after M8 controls. | Tag entry, multi-token semantics, clear button, and search chip behavior. | `cursor_impl` | Native UI/headless search smoke covering tag-like text, multi-token query, clear action, and no-results copy. |
| Sidebar quick search and filters | DEFERRED | No new favorite-level/date/enhanced filter chips in M8. | Unrated, favorite level chips 1-5, enhanced-only, date presets, manual date range, and richer count labels. | `cursor_impl` | Native UI/headless filter smoke with fixture rows for each filter bucket and count labels. |
| Folder visibility | DEFERRED | Folder bucket list, all/none/invert controls, hidden-folder persistence setting, and hide-all smoke evidence. | Multi-root bucket sets, range selection, show/hide selected only, clear selected, and richer bucket counts across nested folders. | `cursor_impl` | Native UI smoke with nested/multi-root fixture and explicit show/hide selected acceptance. |
| Sorting and display | DEFERRED | Sort modes for modified, created, name, folder, size, favorite, random; random reshuffle; thumbnail-size control; smoke covers name/random/thumbnail paths. | Date sections, scroll memory, seen/unseen state, compact/poster equivalents, aspect controls, and wheel zoom equivalents. | `cursor_impl` | Native UI/headless smoke covering every sort mode, display variant, persisted thumbnail size, and scroll/seen behavior. |
| Virtual gallery | DEFERRED | M8 controls run on the existing virtual list/grid. | Date sections, scroll memory, seen/unseen state, drag/open parity, placeholder behavior, and native thumbnail warmup UI. | `codex_pm` | Native UI/manual sweep plus headless perf evidence for large fixture and virtualized scroll state. |
| Selection and preview tabs | DEFERRED | Preview detail toggle and preview visibility toggle added; single-selection path remains covered. | Ctrl/Shift multi-selection, background clear behavior, preview tabs, pin/unpin, close/restore closed tab, hover/quick preview. | `cursor_impl` | Native UI smoke for multi-selection and preview-tab operations, or explicit product decision to defer tabs. |
| Right preview panel | DEFERRED | Preview panel show/hide and detail show/hide controls added and smoke-verified. | Resize persistence, selected count, bulk favorite/open/recycle confirmation, and richer detail tabs. | `cursor_impl` | Native UI smoke or manual screenshot sweep proving persisted split size, selected count, and bulk actions on disposable files. |
| Modal navigation | DEFERRED | No separate detail modal added in M8; main-view previous/current/next remains covered. | Separate detail modal parity, filtered subset wrap behavior, edge/click/swipe equivalents, and loading-slot UI. | `cursor_impl` | Native detail-modal UI smoke with ordered filtered subset and wrap/edge behavior. |
| Modal image controls | DEFERRED | No native zoom/reset/pan/flip/immersive controls added in M8. | Zoom, reset, pan, horizontal flip, chrome hide/show, favorite +/- inside modal, feedback, and open external. | `cursor_impl` | Native detail-modal UI smoke for zoom/pan/flip/reset/open/favorite controls. |
| Modal metadata | DEFERRED | Existing filename/size/dimensions/favorite preview label remains; no prompt metadata added in M8. | Prompt/negative/settings metadata, prompt tag actions, copy prompt/negative/PNG info, and no-metadata fallback. | `cursor_impl` | Fixture with embedded metadata plus native UI/headless copy/fallback evidence. |
| Delete flows | DEFERRED | M8 does not change Recycle flow and smoke still avoids destructive delete. | Confirmation/cancel, do-not-ask setting, bulk delete confirmation, disposable Recycle Bin UI test, and error-equivalent UI paths. | `codex_pm` | Disposable-copy native UI test proving confirmation/cancel/recycle failure behavior without hard-delete fallback. |
| Settings modal | DEFERRED | Keybinding metadata now includes `Ctrl+P`, `Ctrl+D`, and `Ctrl+R`; existing settings dialog exposes keybinding JSON. | Non-blocking settings UI automation, confirm-before-delete UI, edge/navigation settings, keybinding recorder, malformed settings fallback, and full browser settings parity. | `cursor_impl` | Native settings UI smoke with editable settings/keybinding recorder or explicit read-only decision. |
| Enhancement isolation | DEFERRED | M8 UI smoke again verifies ordinary native browsing/search/preview did not change `.cache/enhance/jobs.json`. | Explicit enhancement queue/settings/cancel/retry/open/delete output/source/original-enhanced toggle/enhanced-only filter. | `codex_pm` | Explicit-action-only native enhancement milestone with zero passive enqueue proof and queue operation smoke. |
| Browser APIs and errors | DEFERRED | No new browser API equivalents in M8. | Full native equivalents for tags/folders/settings/image/thumb/display/open/delete/legacy-state/enhancement errors. | `codex_pm` | Split native headless/API-equivalent error matrix with one fixture per error class. |
| Persistence keys | DEFERRED | M8 persists native-only sort mode, preview visibility, detail visibility, thumbnail size, and hidden folder buckets in SQLite settings. | Full parity for pinned previews, filters, scroll/seen state, recent dirs, server legacy marker, and enhancement settings. | `codex_pm` | Expanded explicit `pvu_*` export fixture and native settings migration acceptance notes. |
| Native responsive/layout parity | DEFERRED | M8 UI smoke exercises the denser toolbar/folder/preview layout at desktop size. | Screenshot/manual layout sweep for overlap, text fit, keyboard focus, and polish. | `claude_ui` / `codex_pm` | Native desktop screenshot or Human Surface review after smoke, with accepted findings reflected before parity claim. |

## Minimum M6 Verification

M6 must run and record:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

The `pnpm`/browser commands are baseline regression checks for the existing
browser app only. They cannot satisfy native acceptance by themselves.

In addition, M6 must perform either:

- native automated checks covering every matrix row that can be safely
  automated, or
- a recorded native UI/manual sweep with screenshots/logs for rows that cannot
  be safely automated without touching `src/**`.

Rows may be marked `VERIFIED`, `BLOCKED`, or `DEFERRED`, but no row may be left
blank before merge. A `BLOCKED` or `DEFERRED` row needs a concrete reason,
owner, and merge decision. M6 has no blank rows and no `BLOCKED` rows; the
remaining gaps are deferred into the next native parity milestone.

## Evidence Required Per Row

Each completed row should record:

- command, native session, or screenshot/log artifact used,
- fixture path or disposable state used,
- pass/fail result,
- whether the evidence is native acceptance or browser-baseline-only evidence,
- whether it changed any ignored cache/state files,
- confirmation that `git diff --name-only -- src` stayed empty unless an
  approved browser helper was intentionally changed.

## M6 Local Evidence

Recorded on 2026-07-08 in branch `codex/h25-local-native-m6`:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed:
  - images: 3
  - created state:
    `.cache\favorites.json`, `.cache\albums.json`, `.cache\settings.json`,
    `.cache\native\browser-localstorage-export.json`
  - skipped existing state: none in this M6 worktree
  - thumbnail compatible/missing/incompatible: `1 / 1 / 1`
  - display compatible/missing/incompatible: `1 / 2 / 0`
- `-HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json`
  passed: favorites 1, albums 2, album images 3, browser state keys 5,
  settings 11, images 0.
- `-HeadlessScan -Folder .\.cache\native-fixture` passed: images 3,
  favorites 1, imported favorites 1, elapsed 4 ms.
- `-HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20`
  passed: scan 3 ms, search p50 0.20 ms, search p95 0.50 ms,
  indexed samples 20/20, navigation p95 14.95 ms, cache hit rate 95.0%,
  header coverage 100.0%, mutation probe added/updated/removed `1 / 1 / 1`,
  watcher events 3.
- `-HeadlessCacheCompat -Folder .\.cache\native-fixture` passed:
  images 3, thumbnail compatible/missing/incompatible `1 / 1 / 1`,
  display compatible/missing/incompatible `1 / 2 / 0`.
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `corepack pnpm test:e2e` passed: 2 Chromium tests.
- `powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full`
  failed because the path does not exist in this project checkout.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full`
  passed: required files, unit, lint, audit, typecheck, build, and E2E. Lint
  kept 0 errors and 2 existing `<img>` warnings in
  `src/components/CachedImage.tsx`.
- `git diff --name-only -- src` returned no files.
