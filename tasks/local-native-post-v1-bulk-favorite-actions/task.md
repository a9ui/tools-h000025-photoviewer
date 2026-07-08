# Local Native Post-v1 #103 - Bulk Favorite Actions

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/103

## Objective

Add the smallest native-only #103 slice after #117 Row25 found no safe
remaining pvu migration key.

The accepted target is bulk favorite level application for the current native
multi-selection. This should reuse the existing favorite level control and
keyboard shortcuts, not add broad batch workflows.

## Guardrails

- No Linear.
- Do not touch H000033.
- Do not deploy.
- Do not modify `src/**`.
- Do not start automatic enhancement workers.
- Do not delete existing cache/state assets.
- Keep implementation under `local-native/**`; use `tasks/**` and
  `docs/local-native/**` only for evidence.
- Keep #97/#98 enhancement queue/output UI, #102 folder range, #105/#106
  destructive flows, and #118 polish separate.

## Source Evidence

- #117 Row25 closeout found no safe unclassified remaining browser `pvu_*`
  key and classified further #117 work as `NEEDS_HUMAN/DEFER`.
- Milestone #26 remains open with #97-#114, #117, and #118.
- #103 states that single-image favorite and multi-selection evidence already
  exist.
- Existing native code already had multi-selection smoke and single-image
  favorite mutation.

## Deliverables

1. Apply favorite level changes to all selected native images.
2. Allow the existing favorite level control and favorite shortcuts to operate
   with multi-selection.
3. Preserve existing single-image favorite and detail-modal favorite behavior.
4. Extend native UI smoke with explicit bulk set and bulk clear evidence.
5. Record the outcome in GitHub, SQLite, Agmsg, and next-thread handoff before
   closing the Goal.

## Verification

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --check
```
