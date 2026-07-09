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

## Files

| File | Role |
| --- | --- |
| `PhotoViewer.Wpf.csproj` | net8.0-windows WPF project |
| `App.xaml` | design tokens, control styles, card/list templates |
| `App.xaml.cs` | startup and `--shot` / `--query` / `--perf-log` / `--state-smoke` capture path |
| `MainWindow.xaml` | custom chrome, sidebar, grouped grid/list, preview, modal, overlays |
| `MainWindow.xaml.cs` | folder scan, image thumbnail decode, load/modal timing, search/filter, state, selection wiring |
| `Converters.cs` | simple WPF value converters |

## Current Limits

- Folder scan is bounded to the first 1,200 images sorted by modified time.
- The grid still uses the shell `WrapPanel`; virtualized layout is the next
  performance step.
- Favorites are currently read-only counts on imported/sample tiles; album
  mutation, delete, and browser-state import are not wired in this WPF surface yet.
- Additional speed work should stay in measured WPF-only follow-up lanes.
- Existing WinForms `PhotoViewer.Native` remains separate and is not modified by
  this WPF lane.

## Design Evidence

Design source of truth:

- `../ui-mockup/photoviewer-ui-mockup.html`
- rendered WPF previews in `../ui-mockup/wpf-*.png`
