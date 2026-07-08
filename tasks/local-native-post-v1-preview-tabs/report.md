# Local Native Post-v1 Preview Tabs Report

Date: 2026-07-09 JST

## Scope

Issue: #99 Post-v1 preview tabs and pinned previews.

Implemented first bounded native-only slice:

- Selecting an image opens or activates an in-session native preview tab.
- The active preview tab can be pinned or unpinned in session.
- The active preview tab can be closed without adding a restore stack.
- Headless UI smoke reports `previewTabs`, `previewTabPin`, and `previewTabClose`.

Not implemented in this slice:

- Browser `pvu_pinned_tabs` import or native pinned-tab persistence.
- Restore recently closed tabs (#100).
- Hover quick preview (#101).
- Automatic enhancement workers, browser app changes, or script changes.

## Evidence

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`: passed with `previewTabs=true previewTabPin=true previewTabClose=true`.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`: passed with `pvuPinnedTabsDeferred=true` and `pinnedTabsMirrorStored=true`.
- `git diff --name-only -- src`: no output.
- `git diff --name-only -- scripts`: no output.
- `git diff --check`: passed.
- `corepack pnpm typecheck`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`: passed; existing lint warnings only for `src/components/CachedImage.tsx` `<img>` usage.

## Classification

- ADOPT: #99 native in-session preview tabs, pin/unpin, close, and smoke flags.
- PARTIAL_ADOPT: pinned previews are native session state only; no browser `pvu_pinned_tabs` import or persistence.
- REJECT: changes to `src/**`, `scripts/**`, deployment, H000033, automatic workers, cache/state deletion.
- DEFER: #100 restore closed tabs, #101 hover quick preview, and browser pinned-tab persistence migration.
- NEEDS_HUMAN: none for this bounded #99 first slice.
