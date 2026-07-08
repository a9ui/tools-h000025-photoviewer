# Local Native Post-v1 #108 Copy PNG Info And Prompt Metadata

## Objective

Implement the first bounded native slice for GitHub issue #108: copy PNG info,
prompt, negative prompt, and generation settings metadata from the selected
native image.

## Scope

- Add one explicit native UI copy action for exactly one selected image.
- Reuse the #107 native metadata fields already parsed from PNG `parameters`.
- Copy PNG info and metadata as deterministic plain text.
- Add headless UI evidence that validates the action path and copied text.

## Guardrails

- Do not touch `src/**`.
- Do not touch `scripts/**`.
- Do not start automatic enhancement workers.
- Do not delete cache/state assets.
- Do not include #109 tag actions, #110 search chips, #118 polish, or browser
  workflow parity.
- Keep clipboard automation out of required CI evidence; verify the UI action
  path and generated copy text deterministically.

## Acceptance Evidence

- `dotnet build local-native/PhotoViewer.Native/PhotoViewer.Native.csproj`
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
- Native UI smoke output includes `metadataDisplay=true`,
  `metadataCopy=true`, and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` is empty.
- `git diff --name-only -- scripts` is empty.
- `git diff --check`
- `corepack pnpm typecheck`
