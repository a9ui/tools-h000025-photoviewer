# Local Native Post-v1 #109 Prompt Tag Actions Report

## Status

Implemented and locally verified.

## Advice Classification

`ADOPT`: #109 has clear browser source behavior for a bounded native first
slice. The browser modal splits prompt text into comma-separated tags and adds a
clicked tag to search. Native now mirrors that as a detail-modal prompt tag
action without adopting #110 search chips or broader metadata workflow parity.

## Slice

This first slice adds prompt tag buttons to the native detail modal when the
selected image has prompt metadata. Clicking a tag appends it to the existing
native search field as a comma-separated search token and closes the detail
modal. Native indexed search now includes prompt, negative prompt, generation
settings summary, and raw metadata so the selected tag can filter the image set.

## Verification

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed and prepared `.cache\native-fixture`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `metadataDisplay=true`, `metadataCopy=true`,
  `promptTagAction=true`, and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no paths.
- `git diff --name-only -- scripts` returned no paths.
- `git diff --check` passed.
- `corepack pnpm typecheck` passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`
  passed: 16 test files and 94 tests passed, lint had the existing
  `@next/next/no-img-element` warnings only, and Next build passed.

## Out Of Scope

- Search chips, drag/reorder chips, and tag suggestions (#110)
- Broad browser workflow parity
- Bulk metadata editing
- Automatic enhancement workers
- `src/**` changes
- `scripts/**` changes
