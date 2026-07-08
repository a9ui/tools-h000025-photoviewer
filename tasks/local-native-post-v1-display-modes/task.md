# Local Native Post-v1 #111 Compact And Poster Display Modes

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/111

## Objective

Implement the first bounded native-only #111 slice after #110 search chips.

The accepted target is a native display preset control that maps the clear
browser display-style actions without taking over #112 aspect controls,
columns, gallery zoom, preview tabs, destructive flows, or enhancement UI.

## Source Evidence

- Browser `src/components/Sidebar.tsx` has display styles `standard`,
  `compact`, and `poster`.
- Browser Compact sets `displayStyle=compact`, `viewMode=grid`,
  `aspectMode=square`, and `thumbSize=140`.
- Browser Poster sets `displayStyle=poster`, `viewMode=grid`,
  `aspectMode=portrait`, and `thumbSize=240`.
- Native already has list/grid view mode and a persisted thumbnail-size
  control with current native range 64-192.
- #117 explicitly deferred browser `pvu_view.aspectMode`, `displayStyle`, and
  `columns` persistence until #111/#112 accepted a native display/aspect
  contract.

## Decision

`ADOPT`: add a native Display preset control with `Standard`, `Compact`, and
`Poster`.

`PARTIAL_ADOPT`: map Compact to existing native grid mode plus 64px thumbnails,
and Poster to existing native grid mode plus 192px thumbnails. This gives the
first native display-mode switch using current accepted controls.

`DEFER`: poster crop/portrait aspect, square/original aspect controls, fixed
columns, browser `pvu_view` display persistence migration, wheel/keyboard zoom,
and richer visual parity. Those remain #112/#113/#117 or later polish.

`REJECT`: browser app changes under `src/**`, script changes under
`scripts/**`, automatic enhancement workers, deployment, H000033 work, and
cache/state deletion.

## Acceptance Evidence

Run:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --name-only -- scripts
git diff --check
corepack pnpm typecheck
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```

Native UI smoke must include:

```text
displayModes=true searchChips=true searchSuggestion=true metadataDisplay=true metadataCopy=true enhancementStateUnchanged=true
```
