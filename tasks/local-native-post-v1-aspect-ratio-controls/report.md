# Local Native Post-v1 #112 Aspect Ratio Controls Report

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/112

## Outcome

Implemented and locally verified.

## Implemented Slice

- Added a native `Aspect` selector beside the existing `Display` and `Thumb`
  controls.
- The selector supports `Original`, `1:1`, and `2:3`, persisted as
  `aspect_mode`.
- `1:1` uses square native grid image frames.
- `Original` and `2:3` use portrait native grid image frames on the existing
  WinForms `ListView` grid surface.
- Compact display mode now selects `1:1`; Poster display mode now selects
  `2:3`.
- Native UI smoke verifies the selector and display-mode interaction with
  `aspectModes=true`.

## Advice Classification

- `ADOPT`: bounded native aspect selector for #112.
- `PARTIAL_ADOPT`: use existing native grid image-list sizing for the first
  frame-ratio slice.
- `DEFER`: exact thumbnail crop/object-fit parity, fixed columns, browser
  `pvu_view.aspectMode` migration, wider display-model persistence, and visual
  polish.
- `REJECT`: `src/**`, `scripts/**`, deployment, H000033, automatic enhancement
  workers, and cache/state deletion.

## Verification

Passed:

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

Native UI smoke emitted:

```text
displayModes=true aspectModes=true metadataDisplay=true metadataCopy=true promptTagAction=true searchChips=true searchSuggestion=true enhancementStateUnchanged=true
```

`-PrepareFixture` reported `createdState=none`, so existing fixture/cache/state
assets were not overwritten.

The pvu-state smoke kept `pvuDisplayDetailsDeferred=true`, confirming this
slice did not import browser `pvu_view.aspectMode` into native migration state.

`git diff --name-only -- src` and `git diff --name-only -- scripts` were empty.

`scripts/verify-project.ps1` passed with 16 test files and 94 tests, plus
successful typecheck and Next build. ESLint reported the existing two
`<img>` warnings in `src/components/CachedImage.tsx` and no errors. E2E was
skipped by this non-`-Full` verifier run.
