# Local Native Post-v1 #104 Bulk Open Actions Report

Date: 2026-07-09 JST

## Scope

Issue: #104 Post-v1 bulk open actions.

Implemented first bounded WinForms native-only slice:

- `Open File(s)` opens all currently selected existing image files through the
  existing OS shell open path.
- `Open Folder(s)` preserves the existing single-selection Explorer
  `/select` behavior.
- For multiple selected images, `Open Folder(s)` opens distinct existing parent
  folders only, so mixed-folder selections are deduped.
- Bulk external opens are capped at 10 targets; larger selections stop with a
  status message instead of launching many OS windows/apps.
- Native UI smoke verifies bulk-open planning without launching external apps.

Not implemented in this slice:

- Bulk destructive delete/recycle (#105/#106).
- Browser app changes or browser open API changes.
- Enhancement queue/output behavior (#97/#98).
- Browser `pvu_*` import/persistence changes.

## Evidence

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`:
  passed with 0 warnings and 0 errors.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`:
  passed; created only ignored fixture/cache state in this worktree.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture`:
  passed with `bulkOpenFiles=true`, `bulkOpenFolders=true`, and existing
  preview-tab/restore/hover flags still true.
- `dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-pvu-state-smoke`:
  passed with `pvuPinnedTabsDeferred=true`, `pinnedTabsMirrorStored=true`,
  and no browser/server/node runtime.

## Classification

- ADOPT: bounded WinForms bulk open file/folder action slice.
- PARTIAL_ADOPT: mixed-folder behavior is deduped parent-folder open; richer
  policies for very large selections stay out of this slice beyond the 10-target
  guard.
- REJECT: `src/**`, `scripts/**`, deployment, H000033, automatic workers,
  cache/state deletion, and browser open API changes.
- DEFER: destructive bulk actions to #105/#106; enhancement UI/output actions
  to #97/#98.
- NEEDS_HUMAN: none for this bounded #104 first slice.
