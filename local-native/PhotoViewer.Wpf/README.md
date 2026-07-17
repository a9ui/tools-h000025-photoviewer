# PhotoViewer.Wpf - local-native WPF surface

Native WPF (.NET 8) PhotoViewer surface. It uses native controls only: no server,
Node, Chrome, localhost, webview, or WebView2.

This project started as a WPF UI shell and is now the native PhotoViewer
implementation. The normative current behavior is documented in
`../../docs/wpf-product-spec.md`; this README focuses on launch and verification:

- landing folder-set picker with multi-folder selection and pasted folder paths
- recursive image file scan
- decode-to-size thumbnails
- grid/list display using real image thumbnails
- right preview using the selected real image, including lazy PNG `tEXt/parameters` metadata and explicit current-selection PNG Info, Prompt, and Negative Copy actions when present
- modal preview using the selected real image
- browser-aligned modal favorite `+1` / `-1` controls with the current `0..5` level visible
- debounced background search across filename and indexed PNG prompt, with comma-AND grammar
- independent exact Favorite Lv1-Lv5 filters, All/Unrated, bulk Favorite, and unseen-only filters
- manual Created/Birth From/To date filter; runtime/write state is only `none|manual`, while legacy relative tokens migrate once to a fixed manual range; no Quick Search or Today/7d/30d/year presets
- persisted collapsible Folders section, Ctrl/Shift bucket selection, and Show/Hide selected/all/invert controls
- full catalog with bounded/recycling Grid and List realization (no 1,200-image product cap)
- anchored 40..600 gallery zoom that leaves sidebar/text/List dimensions unchanged
- guarded Windows Recycle Bin-only source Delete with confirmation and adjacent-image continuation
- resizable/persisted right preview panel and multi-selection actions
- guarded native FileDrop Copy from Grid, List, and right preview to Explorer, preserving display-order multi-selection
- guarded Show in folder actions in Right Preview and Modal; they select the canonical active source in Explorer without modifying it
- preview tabs with pin/close/reopen/close-all, drag/Alt+Shift reorder, middle-close, reload restoration, external delete/rename reconciliation, and cancellable hover decode
- modal first/last wrap, chrome toggle, accessible edge/swipe navigation, transient feedback, zoom/pan/flip, metadata Prompt chips that append to search, and Original/Enhanced display
- refresh active folder
- open the selected real image with the OS default app
- versioned, normalized, unknown-field-preserving WPF state written through a bounded process lock and atomic replace
- `--shot` UI smoke capture
- `--shot --folder <path>` real-folder smoke capture
- `--shot --query <text>` filtered search smoke capture
- `--shot --perf-log <path>` load timing capture for WPF performance evidence
- `--shot-width <px> --shot-height <px>` exact content viewport capture; `--folders-collapsed`, `--show-unseen-dots`, and `--clear-selection` reproduce Browser comparison states
- `--modal-nav-smoke <path>` modal previous/next selected-path sync smoke
- `--grid-realization-smoke <path>` grid initial-realization and batch-append smoke
- `--scroll-realization-smoke <path>` repeated grid scroll/advance realization guard smoke
- `--favorite-smoke <path>` selected-image favorite toggle/filter/reload smoke
- `--favorite-level-smoke <path>` selected-image favorite level adjustment/reload smoke
- `--favorite-filter-smoke <path>` independent exact Favorite levels and unrated-only filter smoke
- `--favorite-import-smoke <path>` bounded `pvu_fav_levels` import policy smoke
- `--preview-tabs-smoke <path>` preview tab open/pin/hover/activate/close/restore/reload smoke
- `--preview-tab-hover-smoke <path>` cancellable/stale-safe/corrupt-safe preview tab hover decode smoke
- `--preview-tab-reorder-smoke <path>` drag/keyboard reorder, middle-close, focus/Automation, and reload-order smoke
- `--preview-decode-smoke <path>` latest-selection async preview decode smoke
- `--png-metadata-smoke <path>` lazy active-preview and read-only modal Prompt / Negative / Settings tab plus PNG `parameters` metadata smoke
- `--explorer-reveal-smoke <path>` temp-only injected Explorer `/select,` action, guard, focus, and isolation smoke
- `--shortcut-typing-smoke <path>` editable-text shortcut guard smoke
- `--seen-smoke <path>` real-folder seen/unseen filter and reload smoke
- `--seen-import-smoke <path>` bounded `pvu_seen_images` import policy smoke
- `--shared-seen-smoke <path>` shared `.cache/seen.json` and legacy merge smoke
- `--shared-recent-smoke <path>` shared `.cache/recent-folders.json` import/write-through smoke
- `--folder-set-smoke <path>` landing folder-set and shared recent smoke
- `--diagnostics-smoke <path>` temp-only App Settings About / Diagnostics privacy and clipboard-failure smoke
- `--settings-unseen-dots-smoke <path>` temp-only sidebar/App Settings Unseen dots synchronization, persistence, accessibility, and Seen/cache isolation smoke
- `--scan-cancel-smoke <path>` temp-only enumeration/metadata Cancel scan, generation, focus, immediate-rescan, and no-partial-publish smoke
- `--cross-runtime-recent-smoke <path>` temp-only WPF participant for the Browser/WPF/third-writer shared-recent stress
- `--recent-write-ownership-smoke <path>` temp-only explicit folder-set commit ownership, retry, and byte-isolation smoke
- `--folder-bucket-smoke <path>` folder bucket range selection, show/hide, collapse, migration, and reload smoke
- `--grid-zoom-smoke <path>` thumbnail size zoom smoke
- `--p0b-smoke <path>` 1,201-image catalog, bounded-grid, exact search/modal, zoom-anchor, and recycling-list smoke
- `--p0c-smoke <path>` guarded source Recycle Bin workflow with injected temp-only backend smoke
- `--delete-correctness-smoke <path>` temp-only protected project/app-root, single/bulk ownership reconciliation, retained Favorite/Seen/Enhancement history, reload, and partial-failure smoke
- `--delete-race-smoke <path>` temp-only preview/modal decode, sparse rapid Delete, bulk partial failure, concurrent Refresh tombstone, and same-name regeneration smoke
- `--external-stale-source-smoke <path>` temp-only external delete/rename/corrupt-source Refresh, tab/pin/modal/focus/state recovery, and shared-history isolation smoke
- `--p0d-smoke <path>` 5,000-image integrated P0 gate with temp-only persistence and enhancement sentinel
- `--catalog-stress-smoke <path> --count 20000` exact/bounded 20,000-image tail-search/modal/heartbeat observation gate
- `--aspect-smoke <path>` browser-aligned aspect mode smoke
- `--date-filter-smoke <path>` browser-aligned manual Created/Birth From/To smoke
- `--search-stall-smoke <path>` 5,000-image rapid-query dispatcher responsiveness smoke
- `--rapid-ui-state-smoke <path>` temp-only rapid search/selection/layout/panel/filter/tab final-state and heartbeat stress
- `--focus-filter-race-smoke <path>` temp-only 20-run search/filter/focus/Grid-List/selection/tab/modal dispatcher reconciliation stress
- `--shutdown-state-smoke <path>` sub-debounce close flush, stale-work cancellation, refusal, and atomic-residue smoke
- `scripts/verify-wpf-modal-wrap.ps1` first/last modal navigation wrap verifier
- `scripts/verify-wpf-modal-interaction.ps1` chrome/edge/swipe/feedback and gesture-conflict verifier
- `scripts/verify-wpf-prompt-tag-search.ps1` temp-only Prompt chip/search/focus/persistence/isolation verifier
- `scripts/verify-wpf-file-drag-out.ps1` temp-only threshold/payload/path-guard/selection/isolation verifier without invoking an OS drag
- `scripts/verify-wpf-explorer-reveal.ps1` temp-only Right Preview/Modal Explorer reveal verifier without starting Explorer
- `scripts/verify-wpf-rapid-ui-state.ps1` medium-catalog stale-result/final-state/reload/enhancement-isolation stress
- `scripts/verify-wpf-shutdown-state.ps1` temp-only exactly-once close persistence and protected/contended-state verifier
- `scripts/verify-wpf-crash-lock-recovery.ps1` actual-process fresh/live lock protection, abrupt-exit stale recovery, atomic-temp cleanup, schema protection, and Browser/WPF concurrency verifier
- `scripts/verify-wpf-recent-write-ownership.ps1` temp-only shared Recent ownership/latest-merge/retry verifier
- `scripts/verify-wpf-partial-scan.ps1` temp-only missing/disconnected multi-root publication, retry ownership, and cancel/stale isolation verifier
- `scripts/verify-wpf-scan-boundary.ps1` temp-only outside/cyclic junction boundary and source-isolation verifier
- `scripts/verify-wpf-monitor-work-area.ps1` current-monitor maximize, exact unchanged restore, disconnected/downsized/DPI-equivalent bounded restore, and fallback verifier
- `scripts/verify-wpf-external-stale-source.ps1` temp-only external source lifecycle, recoverable decode, focus, state, and history-isolation verifier
- `scripts/verify-wpf-delete-race.ps1` temp-only destructive-workflow race, stale-publish rejection, failed/cancel non-tombstone, and fresh same-name regeneration verifier
- `scripts/verify-wpf-folder-buckets.ps1` isolated Folder selection/collapse persistence verifier
- `scripts/verify-wpf-preview-tab-reorder.ps1` isolated preview-tab reorder/focus verifier
- `scripts/verify-wpf-catalog-stress.ps1 -Count 20000` temp-only large-catalog structural and metric verifier
- `scripts/verify-wpf-product.ps1` aggregate every focused WPF verifier (`-SkipStress` for the short loop)
- `--bulk-favorite-smoke <path>` atomic multi-selection Favorite transaction smoke
- `--bulk-recycle-smoke <path>` temp-only cancel/partial-failure/neighbor/empty Recycle workflow smoke

It preserves the enhancement guardrail: browsing, preview, and modal actions do
not start enhancement jobs or background enhancement workers.

## Build and Run

```powershell
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj
```

Fast direct launch from the project root uses the Release executable instead of
`dotnet run`:

```powershell
.\start_wpf.bat
```

The normal launcher compares WPF source/project timestamps with the Release
executable and rebuilds automatically when the executable is missing or stale;
an unchanged build starts directly. To force a rebuild before launch:

```powershell
$env:PHOTOVIEWER_WPF_REBUILD = "1"
.\start_wpf.bat
```

For development, the old `dotnet run` path remains available:

```powershell
$env:PHOTOVIEWER_WPF_DOTNET_RUN = "1"
.\start_wpf.bat
```

Headless UI-smoke evidence renders the window to a PNG and exits. `--screen`
selects the state to capture: `viewer` (default), `landing`, `list`, or `modal`.

```powershell
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot .\local-native\ui-mockup\wpf-preview.png --screen viewer
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot .\local-native\ui-mockup\wpf-landing.png --screen landing
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot .\local-native\ui-mockup\wpf-modal-preview.png --screen modal
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot "$env:TEMP\photoviewer-wpf-modal-metadata.png" --screen modal --show-modal-metadata
```

Real-folder smoke:

```powershell
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot "$env:TEMP\photoviewer-wpf-folder-smoke.png" --screen viewer --folder .\local-native\ui-mockup
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot "$env:TEMP\photoviewer-wpf-query-smoke.png" --screen viewer --folder .\local-native\ui-mockup --query wpf-preview
```

Performance-log smoke:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot "$env:TEMP\photoviewer-wpf-perf-after.png" --screen viewer --folder "$env:TEMP\photoviewer-wpf-perf-fixture" --perf-log "$env:TEMP\photoviewer-wpf-perf-after.json"
```

The perf log separates `MetadataMs`, `MetadataWorkers`, and
`MetadataCompleted` from UI-thread `MaterializeMs`, so large-folder header
reads remain measurable without hiding them inside tile creation time.

Preview decode smoke selects one fixture image and immediately selects another,
then verifies that the deferred decode applies only to the latest selection:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --preview-decode-smoke "$env:TEMP\photoviewer-wpf-preview-decode-smoke.json" --folder .\local-native\ui-mockup --select-name wpf-settings.png
```

PNG metadata smoke creates temporary real PNG fixtures. It verifies a bounded
pre-IDAT `tEXt` keyword `parameters` read for the active preview only, prompt
and negative prompt/settings parsing, metadata-free and unrelated-text fallback,
latest-selection cancellation, and clipboard-free PNG Info/Prompt/Negative Copy text and disabled fallbacks
without touching user state or the OS clipboard:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --png-metadata-smoke "$env:TEMP\photoviewer-wpf-png-metadata-smoke.json"
```

For a metadata display screenshot, `--wait-preview-metadata` waits only for the
selected preview's already-started lazy read before rendering; normal runtime
and standard `--shot` behavior remain non-blocking.

State-restore smoke uses a caller-provided bounded state file and never writes
browser or WinForms state:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --state-smoke "$env:TEMP\photoviewer-wpf-state-smoke.json" --state-path "$env:TEMP\photoviewer-wpf-state.json" --folder .\local-native\ui-mockup --query wpf --select-name wpf-settings.png
```

Modal navigation smoke uses the same bounded WPF state path and verifies that
modal next/previous navigation keeps selection and `SelectedPath` in sync:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --modal-nav-smoke "$env:TEMP\photoviewer-wpf-modal-nav-smoke.json" --state-path "$env:TEMP\photoviewer-wpf-modal-nav-state.json" --folder .\local-native\ui-mockup --query wpf --select-index 1
```

Grid realization smoke verifies that larger filtered folders render an initial
bounded grid batch and append another batch on demand:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --grid-realization-smoke "$env:TEMP\photoviewer-wpf-grid-realization-smoke.json" --folder "$env:TEMP\photoviewer-wpf-perf-fixture"
```

Scroll realization smoke verifies that repeated grid advances keep the realized
card window bounded while still moving through a large filtered folder:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --scroll-realization-smoke "$env:TEMP\photoviewer-wpf-scroll-realization-smoke.json" --folder "$env:TEMP\photoviewer-wpf-perf-fixture" --advance-count 16
```

Favorite workflow smoke uses a caller-provided bounded favorites JSON path so
verification does not modify real user favorites:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --favorite-smoke "$env:TEMP\photoviewer-wpf-favorite-smoke.json" --folder .\local-native\ui-mockup --favorites-path "$env:TEMP\photoviewer-wpf-favorites.json"
```

Favorite level smoke uses the same bounded favorites JSON override and verifies
level up/down, clear, final persistence, reload, and favorites-only filtering:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --favorite-level-smoke "$env:TEMP\photoviewer-wpf-favorite-level-smoke.json" --folder .\local-native\ui-mockup --favorites-path "$env:TEMP\photoviewer-wpf-favorite-levels.json"
```

P0A sidebar contract smoke uses only temporary state/favorites/seen files. It verifies exact independent favorite levels (including All and Unrated), expanded/collapsible Folders, and default-off unseen dots without altering seen state:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --p0a-smoke "$env:TEMP\photoviewer-wpf-p0a-smoke.json"
```

The same `ShowUnseenDots` state is available from both the sidebar and App
Settings. Either checkbox updates the other immediately; the setting defaults
OFF, persists across reload, and changes only dot visibility. The focused smoke
keeps all persistence under a temporary root and proves the Seen JSON, source
fixture, favorites, recent history after folder-open setup, and enhancement jobs
stay byte-identical while the two controls are exercised:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-settings-unseen-dots.ps1
```

P0C adds the App Settings `Confirm before delete` default (persisted in the WPF
state), a Cancel-first confirmation dialog, and one guarded source-delete command
used by Grid/List selection, right preview, modal, and `Delete`. Production sends
only to the Windows Recycle Bin and never hard-deletes on failure. Its smoke creates
only a temporary fixture and injects a fake backend which moves files to a temporary
fake recycle folder; it does not exercise the user's actual Windows Recycle Bin:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --p0c-smoke "$env:TEMP\photoviewer-wpf-p0c-smoke.json"
```

Delete validation rejects a source when either its lexical or canonical path is
inside the repository/project root or the running app root, even if that root was
opened as an active source folder. Successful single and bulk Recycle operations
share one reconciliation path: only catalog/selection/tab/pin/closed-history/
preview/modal and persisted UI references are purged. Favorite, Seen, Recent, and
Enhancement history remain under their own ownership. The focused verifier uses
only temporary sources, protected-root injection, and a fake Recycle backend:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-delete-correctness.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-delete-race.ps1
```

P0D is the integrated local-native gate. It builds and removes a temporary
5,000-image fixture and verifies catalog completeness; bounded, recycling List
realization at the first, middle, and final scroll positions; favorites/dots/folder
controls; anchored zoom; fake Recycle Bin continuation; reload state; malformed
field protection; future JSON preservation; cross-process temp+rename lock merges;
and a byte-identical enhancement-jobs sentinel. It never uses browser, port 3000,
the real Recycle Bin, or user cache:

```powershell
.\scripts\verify-wpf-p0.ps1
```

The shared persistence lock is `<target>.lock`: create-new JSON ownership, a
2-second/25-ms bounded background retry, and a conservative 30-second stale-file
recovery. Interactive WPF actions make one attempt and yield on contention so the
UI does not wait behind another writer. If that attempt removes a lock older than
30 seconds, it performs one immediate create-new retry so the first user action
recovers the crash. A successfully acquired lock removes only target-specific WPF
and Browser atomic-temp orphans; fresh/live locks and unrelated temp files remain
authoritative and untouched. Delete still has an unavoidable filesystem
TOCTOU window between canonical-path validation and the Windows Recycle Bin API;
the command revalidates immediately before that call, but adversarial reparse-point
swaps remain a documented post-P1S hardening item.

Favorite import smoke writes a temporary explicit browser-state export and
imports only bounded favorite fields into the accepted WPF favorites JSON:
`browserLocalStorage.pvu_fav_levels` object-map entries and
`browserLocalStorage.pvu_favorites` object-map or string-list entries. It proves
import, preserve-existing, invalid/zero/missing/unmatched ignore, persistence,
reload, and favorites-only filtering:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --favorite-import-smoke "$env:TEMP\photoviewer-wpf-favorite-import-smoke.json" --folder .\local-native\ui-mockup --favorites-path "$env:TEMP\photoviewer-wpf-favorite-import-favorites.json" --browser-state-path "$env:TEMP\photoviewer-wpf-favorite-import-browser-state.json"
```

Seen-state smoke uses caller-provided bounded seen and favorites JSON paths so
verification does not modify real WPF state:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --seen-smoke "$env:TEMP\photoviewer-wpf-seen-smoke.json" --folder .\local-native\ui-mockup --seen-path "$env:TEMP\photoviewer-wpf-seen.json" --favorites-path "$env:TEMP\photoviewer-wpf-seen-favorites.json"
```

Seen import smoke writes a temporary explicit browser-state export and imports
only `browserLocalStorage.pvu_seen_images` into the accepted WPF seen-state JSON:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --seen-import-smoke "$env:TEMP\photoviewer-wpf-seen-import-smoke.json" --folder .\local-native\ui-mockup --seen-path "$env:TEMP\photoviewer-wpf-seen-import.json" --favorites-path "$env:TEMP\photoviewer-wpf-seen-import-favorites.json" --browser-state-path "$env:TEMP\photoviewer-wpf-seen-import-browser-state.json"
```

Shared seen smoke creates a temporary project root and verifies default
`.cache/seen.json` writes, additive legacy `.cache/wpf-seen.json` reads, reload,
and malformed-file fail-safe behavior without touching real user state:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shared-seen-smoke "$env:TEMP\photoviewer-wpf-shared-seen-smoke.json" --folder .\local-native\ui-mockup
```

Shared recent-folders smoke creates a temporary project root and verifies the
cross-surface `.cache/recent-folders.json` contract: import when WPF local
`state.json` has no last folder, one-entry WPF write-through after folder load,
additive preservation of existing folder sets, malformed-file fail-safe behavior,
and favorites/seen store isolation:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shared-recent-smoke "$env:TEMP\photoviewer-wpf-shared-recent-smoke.json" --folder .\local-native\ui-mockup
```

Folder-set smoke verifies the M15 landing workflow: shared recent folder-set
import, pasted folder append, multi-folder scan, shared recent write-through,
local WPF state persistence, and favorites/seen isolation:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --folder-set-smoke "$env:TEMP\photoviewer-wpf-folder-set-smoke.json" --folder .\local-native\ui-mockup
```

While recursive enumeration or metadata loading is active, Landing exposes a
single `Cancel scan` action. Cancellation invalidates only that load generation,
keeps the ordered folder draft, clears progress with a polite status, and returns
focus to `Open folder set`. A canceled run cannot publish a partial catalog,
current folder, state, recent entry, or Seen entry, and a delayed completion
cannot overwrite an immediate newer rescan. The verifier injects delayed worker
completion under a temporary root and covers both phases, double cancel, focus /
Automation, source/cache isolation, and final newer-run ownership:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-scan-cancel.ps1
```

Grid zoom smoke verifies thumbnail size controls and the same card-width helper
used by zoom shortcut and wheel paths:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --grid-zoom-smoke "$env:TEMP\photoviewer-wpf-grid-zoom-smoke.json" --folder .\local-native\ui-mockup
```

P0B creates a temporary offscreen 1,201-image fixture. It proves that the complete catalog is searchable and modal-addressable beyond the old 1,200 boundary while Grid stays bounded, List uses recycling virtualization, and Grid captures the actual ScrollViewer center-near card before 200→300→80→200 zoom. The smoke reports anchor identity and pixel drift (target ≤8px), plus sidebar/right-panel invariance; List thumbnail size remains unchanged.

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --p0b-smoke "$env:TEMP\photoviewer-wpf-p0b-smoke.json"
```

Aspect smoke creates a temporary three-image real-folder fixture and verifies
Original / 1:1 / 2:3 grid and list thumbnail dimensions, selection/order
stability, zoom composition, and WPF state persistence:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --aspect-smoke "$env:TEMP\photoviewer-wpf-aspect-smoke.json"
```

Startup smoke opens the WPF shell to dispatcher-idle readiness, writes timing
evidence, and exits. It can be run through either launch path:

```powershell
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --startup-smoke "$env:TEMP\photoviewer-wpf-startup-dotnet-run.json" --startup-mode dotnet-run
.\local-native\PhotoViewer.Wpf\bin\Release\net8.0-windows\PhotoViewer.Wpf.exe --startup-smoke "$env:TEMP\photoviewer-wpf-startup-release-exe.json" --startup-mode release-exe
```

## Historical milestone notes (non-normative)

The remaining `M*`, issue-number, old `Current Limits`, and old `Design Evidence`
sections preserve how the WPF surface evolved. Their claims are point-in-time
evidence and may describe controls or gaps that were later removed or completed.
They must not override `../../docs/wpf-product-spec.md` or its live completion
ledger. In particular, the M21 Today/7d/30d/year UI is retired; only manual
Created/Birth From/To is current.

## WPF M2 First Performance Slice

The first #177 slice keeps the WPF surface isolated and adds measured load
timing plus bounded parallel thumbnail decode. Folders under 32 images stay on
the sequential path to avoid parallel overhead on small launches.

| Fixture | Baseline wall clock | #177 wall clock | #177 internal total | #177 thumbnail decode | Workers | Images |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `../ui-mockup` small smoke | 1,353.6 ms | 1,162.1 ms | 143 ms | 78 ms | 1 | 8 |
| temp copied larger smoke | 2,852.8 ms | 1,806.8 ms | 484 ms | 416 ms | 4 | 160 |

## WPF M3 Follow-Up Improvement Slice

The first #182 slice improves selection-to-modal responsiveness. List/grid
selection synchronization now avoids duplicate preview updates, and modal open
uses the currently decoded preview or thumbnail immediately while the larger
modal bitmap decodes on a cancellable background task.

| Fixture | Baseline modal wall clock | #182 modal wall clock | Modal immediate source | Modal deferred decode |
| --- | ---: | ---: | --- | --- |
| temp 3000px PNG modal smoke, 4 images | 1,589.1 ms | 1,330.6 ms | true | true |

The `--perf-log` output now includes preview and modal fields:
`PreviewMs`, `PreviewUpdates`, `ModalOpenMs`, `ModalImmediateSource`, and
`ModalDeferredDecode`.

## WPF M4 State And Navigation Polish Slice

The first #186 slice restores the previously selected image when the same WPF
folder/search state is opened again. The state file now records `SelectedPath`,
and `LoadFolderAsync` prefers that tile before falling back to the first
visible result.

The dedicated state smoke uses `--state-path` to keep verification inside a
bounded local WPF state file. It proved `wpf-settings.png` restored with the
search query `wpf` on the second window instance.

## WPF M5 Modal Navigation Polish Slice

The #190 slice wires the modal Previous / Next buttons and Left / Right keys
to the current filtered tile list. Navigation updates the selected tile, right
preview, modal image, and persisted `SelectedPath` together.

The dedicated modal navigation smoke starts from a filtered real-image fixture,
moves to the next image, returns to the previous image, and verifies that the
bounded WPF state file records the final selected image.

## WPF M6 Grid Virtualization Performance Slice

The #193 slice keeps the existing WPF shell grid design but avoids realizing the
entire filtered card set on initial display. `CardsList` now binds to a bounded
grid realization collection, while the full filtered list remains available for
list mode, selection, preview, and modal previous/next navigation. The grid
starts with 96 realized cards and appends 96-card batches as the user scrolls.

The `--perf-log` output now includes `GridTotalItems`, `GridRealizedItems`,
`GridDeferredItems`, `GridInitialRealizationLimit`, and
`GridRealizationBatchSize`.

| Fixture | Before wall clock | #193 wall clock | #193 internal total | Initial grid realized | Deferred |
| --- | ---: | ---: | ---: | ---: | ---: |
| temp copied larger smoke | 2,736.6 ms | 1,634.7 ms | 476 ms | 96 / 320 | 224 |
| temp copied max-loaded smoke | 6,709.3 ms | 3,146.4 ms | 1,805 ms | 96 / 1,200 | 1,104 |

The dedicated grid realization smoke verified a 320-image fixture starts at 96
realized cards and advances to 192 after the next batch request.

## WPF M7 Scroll Realization Guard Slice

The #196 slice keeps the M6 manual grid-realization strategy but prevents
scroll/advance from growing the realized card collection without bound. The grid
still starts at 96 realized cards, expands in 96-card batches, and is capped at
384 realized cards. Further forward/back scroll advances slide a bounded window
through the filtered list instead of appending every card.

The `--perf-log` output now also includes `GridMaxRealizationCount`,
`GridWindowStartIndex`, and `GridWindowEndIndex`.

| Fixture | Before scroll evidence | #196 scroll evidence |
| --- | --- | --- |
| 1,200-image temp smoke | one advance grew `96 -> 192` realized and the M6 path had no repeated-advance cap | 16 advances kept `MaxObservedRealized=384`, final window `816..1200`, `FinalDeferred=816`, back advance succeeded |

The 1,200-image perf smoke after #196 recorded initial `GridRealizedItems=96`,
`GridDeferredItems=1104`, `GridMaxRealizationCount=384`, and window `0..96`.

## WPF M8 Favorite Workflow Slice

The #199 slice keeps favorite mutation non-destructive and WPF-only. Real-file
tiles read favorite levels from the accepted native/browser disk state path,
`.cache/favorites.json`, whose shape is `Record<absolutePath, level>`. Runtime
toggle writes a merged copy of that same map and does not create a parallel WPF
favorite store. The smoke path requires `--favorites-path` and uses a temporary
JSON file for bounded verification.

The dedicated favorite smoke toggles the selected real image from level `0` to
`5`, enables the existing `Favorites only` filter, verifies exactly one visible
favorite, reloads a second WPF window from the same favorites JSON, and verifies
the selected image still has level `5` under the favorites-only filter.

## WPF M9 Favorite Level Polish Slice

The #203 slice keeps the #199 favorite storage path unchanged and adds explicit
headless evidence for level adjustment. The WPF preview stepper already mutates
the selected real image in the accepted `0..5` model; `--favorite-level-smoke`
now proves that path by moving `0 -> 1 -> 2 -> 1 -> 0 -> 4`, verifying the clear
removes the store entry, enabling `Favorites only`, and reloading a second WPF
window from the same temporary favorites JSON with level `4` preserved.

## WPF M23 Favorite Filter Polish Slice

The #254 slice wires the browser-aligned read-only favorite filter controls:
`Favorites only`, `Unrated only`, and `Lv 1+` through `Lv 5+`. Favorite-only
uses the selected threshold, unrated-only shows level `0` images, and those two
filter modes are mutually exclusive. The threshold and active mode persist
through the existing WPF state JSON while favorite levels continue to live only
in the accepted shared favorites JSON map. `--favorite-filter-smoke` creates a
temporary four-image fixture, assigns levels `0/1/3/5`, verifies `Lv 1+`,
`Lv 3+`, `Lv 5+`, unrated-only, clear, selection fallback, and reload
persistence without touching user favorites or seen state.

## WPF M24 Preview Tabs First Slice

The #256 slice wires the existing bottom preview tab shell to real selected
images. The right preview action can open the selected image as a tab; bottom
tabs can activate and close; `Reopen` restores the last closed tab; `Close all`
clears the current runtime tab set. Activating or restoring a tab selects the
matching visible tile so the right preview and modal source stay coherent.
`--preview-tabs-smoke` verifies opening two tabs, activation, close, restore,
close-all, restore-after-close-all, and selection sync with temporary state,
seen, and favorites paths.

The #260 follow-up adds browser-aligned hover previews for bottom preview tabs.
Hovering a tab shows a small non-mutating preview card with thumbnail, filename,
and path; leaving the tab hides it. The same smoke verifies hover show/hide and
that hover does not mutate active tab or selected tile. Pinned tabs, drag/reorder,
grid/list tile hover preview, and preview tab persistence remain deferred.

## WPF M10 Favorite Import Policy Slice

The #206 slice keeps browser/profile access out of WPF and accepts only an
explicit browser-state JSON object-map shape:
`browserLocalStorage.pvu_fav_levels = Record<path-or-file-name, level>`.
Matching entries import into the accepted WPF `.cache/favorites.json`
absolute-path map only when the existing WPF level is absent or zero. Imported
levels clamp to `1..5`; zero, invalid, empty-key, and unmatched entries are
ignored. Broad browser-state import, `pvu_favorites`, albums, delete/recycle,
and browser `src/**` work remain out of scope.

## WPF M11 Favorite Boolean Import Slice

The #210 slice keeps the same explicit browser-state route and adds
`browserLocalStorage.pvu_favorites` import into the accepted WPF favorites JSON
store. Supported shapes are `Record<path-or-file-name, level-or-bool>` and a
string list of path-or-file-name entries. Numeric levels clamp to `1..5`;
boolean `true` and list entries import as level `5`; zero, `false`, invalid,
empty-key, and unmatched entries are ignored. Existing WPF levels, including
earlier `pvu_fav_levels` imports, are preserved and not overwritten.

## WPF M12 Seen State Slice

The #214 slice keeps seen/unseen state WPF-only and non-destructive. Real-file
tiles read seen flags from `.cache/wpf-seen.json`, whose shape is
`Record<absolutePath, true>`. Runtime selection/preview of a real image marks
that image seen, persists the map, clears the tile's `Unseen` flag, and lets the
existing `Unseen only` filter work against real folders.

The dedicated seen smoke uses `--seen-path` and
`PHOTOVIEWER_WPF_SEEN_PATH` to keep verification in a temporary JSON file. It
proves initial real-folder unseen count, selected-image seen persistence,
unseen-only count change, reload behavior, and favorite-store isolation.
`pvu_seen_images` import, direct browser/profile reads, delete/recycle, album
mutation, and broad browser-state import remain out of scope.

## WPF M13 Seen Import Policy Slice

The #216 slice keeps the M12 seen-state store and adds only an explicit-file
`browserLocalStorage.pvu_seen_images` import path. Supported shapes are
`Record<path-or-file-name, truthy>` and a string list of path-or-file-name
entries. Matching truthy entries are added to `.cache/wpf-seen.json`; existing
WPF seen entries are preserved; `false`, zero, invalid, empty-key, and
unmatched entries are ignored.

The dedicated seen import smoke requires `--seen-path`, `--favorites-path`, and
`--browser-state-path`, all intended for temporary verification files. It proves
explicit import, preserve-existing behavior, invalid/missing/unmatched ignore,
persistence/reload, unseen-only filtering, and favorite-store isolation. Direct
Chrome/profile reads, broad browser-state migration, WinForms/browser changes,
delete/recycle, album mutation, cache/state deletion, and automatic workers
remain out of scope.

## WPF #222 Shared Seen Prep

The #222 WPF prep accepts PM's shared seen contract for WPF only. Without
`PHOTOVIEWER_WPF_SEEN_PATH`, WPF now writes selected-image seen state to the
canonical shared `.cache/seen.json` file, whose shape is
`Record<absolutePath, true>`. If legacy `.cache/wpf-seen.json` exists, WPF reads
it additively into memory, writes the union to `.cache/seen.json`, and preserves
the legacy file without deleting or rewriting it.

`PHOTOVIEWER_WPF_SEEN_PATH` remains exact for tests and smokes: when it is set,
WPF reads and writes only that path and does not touch real `.cache/seen.json` or
legacy `.cache/wpf-seen.json`. Malformed shared or legacy seen JSON blocks writes
for that load, preserves existing files, and avoids mass seen/unseen mutation.

## WPF #230 Shared Recent-Folders Prep

The #230 WPF prep accepts the browser M1 shared recent-folder contract for WPF
only. Without changing the existing WPF `state.json` UX, WPF now imports the
canonical `.cache/recent-folders.json` only when local WPF state has no
`LastFolder`. The shared file shape is the browser-accepted versioned positive
folder-set JSON with `lastFolderSet`, `recentFolderSets`, and `updatedAtUtc`.

WPF writes a folder set only after an explicit open/change has committed. Search,
layout/settings saves, active-folder refresh, and window close never rewrite the
shared Recent file. A successful set is written at most once per window until a
different set commits; a failed lock/write is not memoized, so the next explicit
commit retries. Under the lock WPF rereads the latest file, prepends the new set,
caps distinct history at 12, and preserves unknown fields. Malformed shared recent
JSON is not overwritten; local WPF `state.json` still saves so browsing is not
blocked.

## Cross-runtime recent stress

`scripts/verify-cross-runtime-recent.ps1` starts no browser server and uses a
fresh temp root. It races 20 Browser route writes, 20 real WPF writes, and 20
independent protocol-compatible writes against one `recent-folders.json`. The
verifier requires valid JSON, schema/unknown-field preservation, no `.lock` or
`.tmp` residue, all three newest owner markers in the bounded 12-set history,
and documents `lastFolderSet` as last-successful-lock-holder wins.

## WPF M14 Performance Final Gate

The #218 slice keeps the existing viewer contract and accelerates the measured
thumbnail pipeline without changing grid/list/modal behavior. Thumbnail decode
still completes for the loaded real-file set, but decoded thumbnails are applied
to WPF bindings in small dispatcher batches instead of one dispatcher hop per
image. Parallel decode is now capped by both CPU count and a 12-worker maximum;
small folders still stay on the sequential path below 32 images.

Representative temp fixture measurements:

| Fixture | Before wall | #218 wall | Before internal total | #218 internal total | Before thumbnail | #218 thumbnail | Workers | Grid / scroll evidence |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 320 hardlinked PNGs | 2,453.1 ms | 1,506.6 ms repeat median | 711 ms | 394 ms repeat median | 425 ms | 153 ms repeat median | 12 | grid 96 realized / 224 deferred; scroll cap not applicable below 384 |
| 1,200 hardlinked PNGs | 2,893.6 ms | 2,854.3 ms | 1,643 ms | 1,189 ms | 1,346 ms | 875 ms | 12 | grid 96 realized / 1,104 deferred; scroll max 384, final 816 deferred |
| 2,400 hardlinked PNGs | 3,161.3 ms | 2,186.3 ms | 1,659 ms | 856 ms | 1,320 ms | 541 ms | 12 | load cap 1,200; grid 96 realized / 1,104 deferred; scroll max 384 |

Candidate classification from the final gate:

| Candidate | Classification | Evidence / reason |
| --- | --- | --- |
| Batch decoded-thumbnail UI application | ADOPT | Removes per-image dispatcher churn while preserving all thumbnail completion semantics. |
| Raise thumbnail workers from 4 to CPU-capped max 12 | ADOPT | 1,200-image internal thumbnail time improved from 1,346 ms to 875 ms in the full final run; 12-worker repeats also recorded 544-548 ms. 16 workers regressed and was rejected. |
| File scan / metadata materialization | NO-OP | Scan and materialize stayed small compared with thumbnail decode: final 2,400 fixture scan/materialize was 53/77 ms. |
| Grid realization / scroll object growth | NO-OP | Existing M6/M7 guards still held: initial 96 realized, repeated scroll max 384, deferred count remained bounded. |
| Modal immediate responsiveness | NO-OP | Modal open remained immediate (`ModalOpenMs=0`, `ModalImmediateSource=true`, deferred decode true). |
| Custom virtualizing wrap panel or broad lazy thumbnail redesign | DEFER | It would change the current shell/layout and smoke contract; keep it as a separately measured WPF issue only if future evidence needs it. |
| WinForms/browser/scripts/cache deletion/destructive work | REJECT | Out of scope for this WPF-only performance gate. |

## WPF M15 Parity Rebaseline First Slice

The #238 slice restores the browser-like landing folder-set workflow in WPF
without touching browser or WinForms code. The landing page now displays the
selected folder set, supports multi-folder picker results, accepts pasted
absolute folder paths, opens all selected folders as one scan bounded by the
existing 1,200-image cap, and reads/writes the shared
`.cache/recent-folders.json` folder-set contract additively.

The grid exposes thumbnail zoom buttons plus the same helper path used by
Ctrl/Windows key and wheel zoom handling. The old size slider remains the source
of truth for clamping and persisted card width.

Obvious WPF shell controls without accepted behavior in this lane are disabled
instead of remaining clickable no-ops: album mutation, delete/recycle, and
unfinished settings controls.

The current `start_wpf.bat` / `dotnet run` path still works, but this slice does
not replace it with a faster packaged launch route. Faster startup packaging or
a release-mode launcher should be measured in a follow-up WPF slice.

## WPF M16 Fast Launch Slice

The #240 slice changes `start_wpf.bat` from the previous `dotnet run` path to a
direct Release executable launch. The script builds
`local-native\PhotoViewer.Wpf\bin\Release\net8.0-windows\PhotoViewer.Wpf.exe`
only when that target is missing or when `PHOTOVIEWER_WPF_REBUILD=1` is set.
The old development route remains available with
`PHOTOVIEWER_WPF_DOTNET_RUN=1`.

The dedicated `--startup-smoke` route records shell readiness timing without
writing user state. It is intended for comparing process wall time for
`dotnet run` against the direct executable route.

Measured #240 evidence on the project fixture shell:

| Route | Evidence command | Wall clock |
| --- | --- | ---: |
| Before change, `dotnet run` landing shot | 3-run median, `--shot --screen landing` | 1,796.1 ms |
| Before change, Release exe landing shot | 3-run median, same shot route | 1,114.3 ms |
| After change, `dotnet run` startup smoke | 5-run median, `--startup-smoke` | 1,454.8 ms |
| After change, Release exe startup smoke | 5-run median, `--startup-smoke` | 654.6 ms |
| After change, `start_wpf.bat` direct route | single `--startup-smoke` with piped pause input | 1,040.3 ms |

## WPF M18 Enhanced Read-only Filter Slice

The #244 slice enables the existing `Enhanced only` checkbox as a read-only
filter. WPF reads `.cache/enhance/jobs.json` without starting enhancement work
and marks only real-file tiles whose succeeded job maps an existing
`sourcePath`/`sourceId` to an existing `outputPath`.

Malformed jobs, failed jobs, missing sources, and stale or missing outputs are
ignored. WPF does not create, retry, cancel, delete, regenerate, overwrite, or
otherwise mutate enhancement jobs or enhancement cache state in this slice.

Dedicated smoke coverage:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --enhanced-filter-smoke $env:TEMP\wpf-enhanced-filter.json --folder .\local-native\ui-mockup
```

## WPF M19 Browser Sort Parity First Slice

The #246 slice replaces the static Sort row with working WPF controls for the
safe browser-aligned first set:

- Modified new
- Modified old
- Name

The sort choice applies after the current search/favorite/enhanced/unseen
filters, so grid, list, modal navigation, and selection all follow the same
filtered order. The choice is persisted through the existing WPF `state.json`
path as `SortBy`.

Dedicated smoke coverage:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --sort-smoke $env:TEMP\wpf-sort-smoke.json --folder .\local-native\ui-mockup
```

## WPF M20 Browser Aspect Mode First Slice

The #248 slice adds browser-aligned Display > Aspect controls to the WPF
surface:

- Original
- 1:1
- 2:3

Aspect mode composes with the existing Standard/Compact/Poster display style
and thumbnail zoom size. Display style still controls density/width, zoom still
controls base card size, and aspect mode controls the card/list thumbnail
height ratio. The selected aspect is persisted through the existing WPF
`state.json` path as `AspectMode`; missing older state falls back to Original.

Dedicated smoke coverage:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --aspect-smoke $env:TEMP\wpf-aspect-smoke.json
```

## WPF M21 Browser Date Filter Presets First Slice

The #250 slice replaces the static date placeholders in the Filters section
with browser-aligned preset controls:

- Today
- 7d
- 30d
- This year
- Clear

The filter applies to real-folder tiles using the WPF modified timestamp data.
When the selected image remains visible, selection is preserved; otherwise WPF
uses the existing first-visible fallback. The active preset and concrete
`yyyy-MM-dd` range persist through the existing WPF `state.json` path as
`DatePreset`, `DateFrom`, and `DateTo`.

The #258 follow-up adds browser-aligned manual Date from / Date to inputs. A
manual range is stored as `DatePreset = manual` plus the same `DateFrom` and
`DateTo` fields. Clear resets both manual inputs and preset state. From-only
and to-only ranges are supported against the same modified timestamp data.

Dedicated smoke coverage creates a temporary real-folder fixture with
deterministic file modified dates and verifies preset counts/order, manual
bounded/from-only/to-only ranges, clear behavior, selection
preservation/fallback, and reload persistence:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --date-filter-smoke $env:TEMP\wpf-date-filter-smoke.json
```

## WPF M22 Browser Folder Bucket Controls First Slice

The #252 slice adds browser-aligned folder bucket visibility controls to the
WPF Folders section. WPF builds buckets from the active loaded folder set,
assigns each real-file tile to the longest matching root folder, and supports:

- per-folder Shown / Hidden toggles
- Show all
- Hide all
- Invert

Hidden folder buckets filter the current real-file tile list. Selection is
preserved when the selected image remains visible and otherwise falls back to
the existing first-visible behavior. Hidden bucket keys are persisted through
the existing WPF `state.json` path as `HiddenFolderBuckets`.

Dedicated smoke coverage creates a temporary two-folder fixture and verifies
bucket counts, hide/show/invert behavior, selection fallback/preservation,
hidden bucket reload persistence, and temp-state isolation:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --folder-bucket-smoke $env:TEMP\wpf-folder-bucket-smoke.json
```

## WPF M27 Input Safety

Global viewer shortcuts now leave editable WPF text inputs alone. Search terms
can include `F` and `X` without mutating the selected image favorite; the same
shortcuts still work after focus leaves the input.

Dedicated smoke coverage uses temporary state and favorite paths to prove the
text-input guard and the outside-input shortcuts together:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shortcut-typing-smoke $env:TEMP\wpf-shortcut-typing-smoke.json --folder .\local-native\ui-mockup
```

## WPF Native Folder Drag-In

Explorer folder drops now work on both Landing and the Viewer gallery. The WPF
surface accepts only existing absolute folders, resolves canonical paths,
deduplicates case-insensitively, and appends by reference: Landing updates the
draft folder set and Viewer rescans the merged set. Files and folders are never
copied, moved, or deleted. File payloads, missing folders, and relative paths
are refused with a status message; existing image drag-out remains Copy-only.

The focused verifier uses only temporary source fixtures and isolated
state/favorites/seen/recent/jobs paths:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-folder-drag-in.ps1
```

## WPF M50 Modal Metadata Tabs

The #306 slice aligns the existing read-only modal metadata sidebar with the
browser's `Prompt` / `Negative` / `Settings` tabs. Tab changes only switch the
visible WPF panel; they do not re-read PNG metadata or change search, favorite,
enhancement, delete, album, cache, or persisted state. PNG Info remains visible
below every tab, and the existing PNG Info, Prompt, and Negative copy actions
stay current even while their panel is hidden.

The existing `--png-metadata-smoke` verifies all three tab states and then
rechecks the M47-M49 topbar and image-double-click sidebar cycles with current
metadata and copy state preserved.

## Files

| File | Role |
| --- | --- |
| `PhotoViewer.Wpf.csproj` | net8.0-windows WPF project |
| `App.xaml` | design tokens, control styles, card/list templates |
| `App.xaml.cs` | startup and `--shot` / `--query` / `--perf-log` / WPF smoke capture paths |
| `MainWindow.xaml` | custom chrome, sidebar, grouped grid/list, preview, modal, overlays |
| `MainWindow.xaml.cs` | folder scan, image thumbnail decode, load/modal timing, search/filter, state, selection wiring |
| `Converters.cs` | simple WPF value converters |

## Historical limits at the time of those milestones (non-normative)

- Folder scan is bounded to the first 1,200 images sorted by modified time.
- The grid still uses the shell `WrapPanel`, but initial card realization is
  bounded and expands in capped sliding-window batches. A custom true
  virtualizing wrap panel remains deferred until a separate measured slice
  proves it is needed.
- Favorites use the accepted `.cache/favorites.json` absolute-path map for
  selected-image toggle, level adjustment, favorites-only filtering, and the
  bounded `pvu_fav_levels` / `pvu_favorites` explicit-import smoke. Album
  mutation, delete, and broad browser-state import are not wired in this WPF
  surface yet.
- Seen state writes the shared `.cache/seen.json` absolute-path map by default
  and reads legacy `.cache/wpf-seen.json` additively. The
  `PHOTOVIEWER_WPF_SEEN_PATH` override remains exact for bounded smokes.
  `pvu_seen_images` import is supported only through the separately scoped
  explicit-file smoke path, not through browser/profile reads.
- Recent folders import/write through `.cache/recent-folders.json` using the
  browser-accepted shared folder-set shape. WPF exposes current, last, and
  recent folder sets on the landing page; richer history management remains
  deferred.
- `start_wpf.bat` now launches the Release executable directly after a first
  build. A richer packaged installer or self-contained distribution remains
  deferred.
- Enhanced-only filtering is read-only: it consumes succeeded jobs from
  `.cache/enhance/jobs.json` when source and output files are both usable.
  Enhancement generation, queue management, retry/cancel, output ownership, and
  cache mutation remain deferred.
- Sort supports modified-newest, modified-oldest, and name ordering. Browser
  created-date sorting, random sorting, and reshuffle remain deferred until WPF
  has reliable created timestamp and deterministic random-seed contracts.
- Aspect controls support Original, 1:1, and 2:3. Browser display-style default
  aspect shortcuts, fixed columns, exact object-fit/crop parity, and
  `pvu_view.aspectMode` browser-state import remain deferred until separately
  contracted.
- Date filters support the browser preset controls Today, 7d, 30d, This year,
  Clear, and manual Date from / Date to inputs against modified timestamps.
  Date-range browser-state import and created-date filtering remain deferred
  until separately contracted.
- Folder bucket controls support per-folder visibility, Show all, Hide all, and
  Invert for the active loaded folder set. Browser-exact multi-select/range
  bucket operations, folder bucket sort controls, and server/search
  `hiddenFolders` integration remain deferred until separately contracted.
- Additional speed work should stay in measured WPF-only follow-up lanes.
- Existing WinForms `PhotoViewer.Native` remains separate and is not modified by
  this WPF lane.

## Historical design evidence (non-normative)

These files explain the early shell only and are not the current source of truth:

- `../ui-mockup/photoviewer-ui-mockup.html`
- rendered WPF previews in `../ui-mockup/wpf-*.png`

Current visual/product truth is the live Browser contract plus the real WPF
`--shot` output described in `../../docs/wpf-product-spec.md`.

`scripts/verify-wpf-visual-layout.ps1` renders Landing, Viewer, Settings,
Folders collapsed, and Unseen dots at exact 1280x820 and 1024x700 content
viewports. The audit pairs the 1280x820 outputs with the matching live Browser
references; WPF Landing exposes the same App Settings entry, and a cleared
selection shows only the empty preview guidance rather than stale actions.

Recursive scan does not descend into nested junctions, symbolic links, or
other reparse-point directories. The scan-boundary verifier proves that only
in-root images are indexed and that outside/cyclic targets and source files
remain untouched.

A partial multi-root scan keeps indexing every available root when another
selected root is missing or becomes unavailable. The complete ordered folder
set remains in WPF state and shared Recent so Refresh can retry it; only a
successful current generation may commit those stores. Cancelled or stale runs
publish neither catalog nor ownership changes.
