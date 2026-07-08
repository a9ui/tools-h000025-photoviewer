# Local Native Post-v1 #112 Aspect Ratio Controls

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/112

## Objective

Implement the first bounded native-only #112 slice after #111 display modes.

The accepted target is a native aspect selector that uses clear browser source
evidence while staying inside the existing native grid/thumbnail surface. This
does not take over #113 gallery wheel/keyboard zoom, #114 keybinding recorder,
#118 polish, #97/#98 enhancement UI, #102 folder range selection, #104 bulk
open, or #105/#106 destructive flows.

## Source Evidence

- Browser `src/store/ImageContext.tsx` defines `AspectMode` as `original`,
  `square`, and `portrait`, with default `original`.
- Browser `src/components/Sidebar.tsx` exposes aspect buttons labelled
  `Original`, `1:1`, and `2:3`.
- Browser Compact sets `aspectMode=square`; browser Poster sets
  `aspectMode=portrait`.
- Browser `src/components/ImageGrid.tsx` uses a square grid frame for `square`
  and a portrait 2:3 frame for `original` / `portrait`. `original` differs
  from `portrait` by image fit/crop behavior, not by the current grid frame
  ratio.
- Native #111 already has accepted `Display` and `Thumb` controls on the
  local-native display row.

## Decision

`ADOPT`: add a native `Aspect` selector with `Original`, `1:1`, and `2:3`.

`PARTIAL_ADOPT`: map `1:1` to square native grid image frames, and map
`Original` / `2:3` to a portrait native grid frame in the existing WinForms
`ListView` image list. Compact now sets `1:1`; Poster now sets `2:3`.

`DEFER`: exact browser `object-fit` / crop parity for actual thumbnails,
fixed column controls, browser `pvu_view.aspectMode` migration, richer visual
polish, and any broader display model rewrite.

`REJECT`: browser app changes under `src/**`, script changes under
`scripts/**`, automatic enhancement workers, deployment, H000033 work, and
cache/state deletion.

## Acceptance Evidence

Run:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke
git diff --name-only -- src
git diff --name-only -- scripts
git diff --check
corepack pnpm typecheck
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```

Native UI smoke must include:

```text
displayModes=true aspectModes=true searchChips=true searchSuggestion=true metadataDisplay=true metadataCopy=true enhancementStateUnchanged=true
```
