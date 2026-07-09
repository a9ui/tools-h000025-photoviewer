# PhotoViewer.Wpf - local-native WPF surface

Native WPF (.NET 8) PhotoViewer surface. It uses native controls only: no server,
Node, Chrome, localhost, webview, or WebView2.

This project started as the Claude WPF UI shell and now includes the first real
browse and practical viewer slice:

- folder picker
- recursive image file scan
- decode-to-size thumbnails
- grid/list display using real image thumbnails
- right preview using the selected real image
- modal preview using the selected real image
- search across filename, path, prompt, group, size, and modified date
- favorites-only and unseen-only filters
- refresh active folder
- open the selected real image with the OS default app
- lightweight WPF state for last folder, search query, selected image, and card size
- `--shot` UI smoke capture
- `--shot --folder <path>` real-folder smoke capture
- `--shot --query <text>` filtered search smoke capture
- `--shot --perf-log <path>` load timing capture for WPF performance evidence
- `--modal-nav-smoke <path>` modal previous/next selected-path sync smoke
- `--grid-realization-smoke <path>` grid initial-realization and batch-append smoke
- `--scroll-realization-smoke <path>` repeated grid scroll/advance realization guard smoke
- `--favorite-smoke <path>` selected-image favorite toggle/filter/reload smoke
- `--favorite-level-smoke <path>` selected-image favorite level adjustment/reload smoke
- `--favorite-import-smoke <path>` bounded `pvu_fav_levels` import policy smoke
- `--seen-smoke <path>` real-folder seen/unseen filter and reload smoke
- `--seen-import-smoke <path>` bounded `pvu_seen_images` import policy smoke

It still preserves the shell-only guardrail for enhancement: browsing, preview,
modal, settings, album picker, and enhance drawer do not start enhancement jobs
or background enhancement workers.

## Build and Run

```powershell
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj
```

Headless UI-smoke evidence renders the window to a PNG and exits. `--screen`
selects the state to capture: `viewer` (default), `landing`, `list`, `modal`,
`settings`, `album`, `enhance`, `confirm`.

```powershell
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot .\local-native\ui-mockup\wpf-preview.png --screen viewer
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot .\local-native\ui-mockup\wpf-landing.png --screen landing
dotnet run --project .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -- --shot .\local-native\ui-mockup\wpf-settings.png --screen settings
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

## Files

| File | Role |
| --- | --- |
| `PhotoViewer.Wpf.csproj` | net8.0-windows WPF project |
| `App.xaml` | design tokens, control styles, card/list templates |
| `App.xaml.cs` | startup and `--shot` / `--query` / `--perf-log` / WPF smoke capture paths |
| `MainWindow.xaml` | custom chrome, sidebar, grouped grid/list, preview, modal, overlays |
| `MainWindow.xaml.cs` | folder scan, image thumbnail decode, load/modal timing, search/filter, state, selection wiring |
| `Converters.cs` | simple WPF value converters |

## Current Limits

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
- Seen state uses the WPF-only `.cache/wpf-seen.json` absolute-path map for
  selected-image seen persistence and real-folder `Unseen only` filtering.
  `pvu_seen_images` import is supported only through the separately scoped
  explicit-file smoke path, not through browser/profile reads.
- Additional speed work should stay in measured WPF-only follow-up lanes.
- Existing WinForms `PhotoViewer.Native` remains separate and is not modified by
  this WPF lane.

## Design Evidence

Design source of truth:

- `../ui-mockup/photoviewer-ui-mockup.html`
- rendered WPF previews in `../ui-mockup/wpf-*.png`
