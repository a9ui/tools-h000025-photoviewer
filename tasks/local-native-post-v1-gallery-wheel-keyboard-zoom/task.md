# Local Native Post-v1 #113 Gallery Wheel and Keyboard Zoom

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/113

## Objective

Implement the first bounded native-only #113 slice after #112 aspect controls.

The accepted target is gallery-level wheel and keyboard zoom interaction backed
by browser source evidence while staying inside the existing native `Thumb`
control and WinForms grid surface. This does not take over #114 keybinding
recorder, #118 polish, #97/#98 enhancement UI, #102 folder range selection,
#104 bulk open, #105/#106 destructive flows, or remaining #112 crop/object-fit,
fixed-column, and `pvu_view` migration decisions.

## Source Evidence

- Browser `src/store/ImageContext.tsx` defines `thumbSize` with default `200`
  and range `40..600`.
- Browser `src/components/ImageGrid.tsx` handles modified wheel input only in
  grid mode and changes `thumbSize` by `20`.
- Browser `src/components/ImageGrid.tsx` handles `Ctrl`/`Meta` + `+` / `=`,
  `-`, and `0` in grid mode for gallery thumbnail zoom and reset.
- Browser `src/lib/viewerUi.ts` provides centered-scroll math for browser grid
  zoom; this first native slice does not adopt that scroll model.
- Native #111/#112 already has accepted `Display`, `Aspect`, and `Thumb`
  controls on the local-native display row.

## Decision

`ADOPT`: add native gallery wheel and keyboard zoom entry points for Grid view.

`PARTIAL_ADOPT`: map browser gallery zoom to the existing native `Thumb`
control and accepted native `64..192` / `16` step size. Browser default reset
maps to the native maximum accepted equivalent, `192`, instead of introducing a
new `40..600` range.

`DEFER`: browser centered-scroll parity, exact `thumbSize` range parity,
list/details wheel semantics, editable keybindings (#114), visual polish
(#118), and broader `pvu_view` display migration.

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
galleryWheelZoom=true galleryKeyboardZoom=true displayModes=true aspectModes=true searchChips=true searchSuggestion=true metadataDisplay=true metadataCopy=true enhancementStateUnchanged=true
```
