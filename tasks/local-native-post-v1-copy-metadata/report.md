# Local Native Post-v1 #108 Copy Metadata Report

## Status

Implemented and locally verified.

## Slice

This first slice adds an explicit main native UI `Copy` action for exactly one
selected image. It builds deterministic plain text containing PNG info,
prompt, negative prompt, generation settings, and raw `parameters` metadata
from the #107 fields.

## Verification

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed and prepared `.cache\native-fixture`.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
  passed with `metadataDisplay=true`, `metadataCopy=true`, and
  `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned no paths.
- `git diff --name-only -- scripts` returned no paths.
- `git diff --check` passed.
- `corepack pnpm typecheck` passed.

## Out Of Scope

- Prompt tag actions (#109)
- Search chips (#110)
- Browser workflow parity
- Automatic enhancement workers
- `src/**` changes
- `scripts/**` changes
