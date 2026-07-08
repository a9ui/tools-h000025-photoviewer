# Local Native Post-v1 #109 Prompt Tag Actions

## Objective

Implement the first bounded native slice for GitHub issue #109: expose prompt
tags from existing #107 metadata and let a user add one prompt tag to the native
search field from the detail view.

## Scope

- Reuse the browser prompt-tag semantics: comma-separated prompt split, bracket
  and whitespace trimming, duplicate suppression, and existing search text as a
  comma-separated list.
- Add prompt tag buttons only near the native detail modal metadata display.
- Add prompt, negative prompt, settings summary, and raw metadata to the native
  search index so a selected prompt tag actually filters images.
- Add headless UI evidence that validates the action and preserves passive
  enhancement isolation.

## Guardrails

- Do not touch `src/**`.
- Do not touch `scripts/**`.
- Do not start automatic enhancement workers.
- Do not delete cache/state assets.
- Do not include #110 search chips, drag/reorder chips, or tag suggestion UI.
- Do not include broad metadata workflow parity, bulk metadata editing, or
  destructive flows.

## Acceptance Evidence

- `dotnet build local-native/PhotoViewer.Native/PhotoViewer.Native.csproj`
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`
- Native UI smoke output includes `metadataDisplay=true`,
  `metadataCopy=true`, `promptTagAction=true`, and
  `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` is empty.
- `git diff --name-only -- scripts` is empty.
- `git diff --check`
- `corepack pnpm typecheck`
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`
