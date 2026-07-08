# Local Native Post-v1 Restore Preview Tabs Report

Date: 2026-07-09 JST

## Scope

Issue: #100 Post-v1 restore recently closed preview tabs.

Implemented first bounded native-only slice:

- Closing a native preview tab records it in an in-session recently closed stack.
- The new `Restore` preview-tab action restores the most recently closed visible tab.
- Restored tabs preserve the session-only pinned state they had when closed.
- Headless UI smoke reports `previewTabRestore=true`.

Not implemented in this slice:

- Browser `pvu_pinned_tabs` import or native pinned-tab persistence.
- Broad preview-tab persistence across app restarts.
- Hover quick preview (#101).
- Automatic enhancement workers, browser app changes, script changes, deployment, H000033 work, or cache/state deletion.

## Evidence

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`: passed with 0 warnings and 0 errors.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`: passed with `previewTabs=true previewTabPin=true previewTabClose=true previewTabRestore=true`.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`: passed with `pvuPinnedTabsDeferred=true` and `pinnedTabsMirrorStored=true`.
- `git diff --name-only -- src`: no output.
- `git diff --name-only -- scripts`: no output.
- `git diff --check`: passed.
- `corepack pnpm typecheck`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`: passed; existing lint warnings only for `src/components/CachedImage.tsx` `<img>` usage.

## Classification

- ADOPT: #100 native in-session recently closed preview-tab restore and smoke flag.
- PARTIAL_ADOPT: restore is session-only and current-visible-list only; no browser `pvu_pinned_tabs` import or broad persistence.
- REJECT: changes to `src/**`, `scripts/**`, deployment, H000033, automatic workers, cache/state deletion, and browser pinned-tab migration in this slice.
- DEFER: #101 hover quick preview and broad preview-tab/pinned-tab persistence.
- NEEDS_HUMAN: none for this bounded #100 first slice.
