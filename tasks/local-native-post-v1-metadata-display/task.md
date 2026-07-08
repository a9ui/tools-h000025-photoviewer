# Local Native Post-v1 #107 - Metadata Display First Slice

Date: 2026-07-09

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/107

## Objective

Add the smallest native-only #107 metadata display slice after #103 bulk
favorite closeout.

The accepted target is display of PNG `parameters` prompt metadata in the
native right-preview details label and detail modal metadata label. This first
slice should parse and show prompt, negative prompt, and a compact settings
summary from a deterministic fixture. Copy metadata (#108), prompt tag actions
(#109), search chips (#110), and broader metadata workflows stay separate.

## Guardrails

- No Linear.
- Do not touch H000033.
- Do not deploy.
- Do not modify `src/**`.
- Do not modify `scripts/**`.
- Do not start automatic enhancement workers.
- Do not delete existing cache/state assets.
- Keep implementation under `local-native/**`; use `tasks/**` and
  `local-native/README.md` only for evidence.
- Keep #97/#98 enhancement queue/output UI explicit-action-only, #102 folder
  range separate, #104 bulk open separate, #105/#106 destructive flows
  separate, #108 copy metadata separate, #109 tag actions separate, and #118
  polish separate.

## Source Evidence

- Browser code already extracts PNG `tEXt` keyword `parameters` in
  `src/lib/pngParser.ts` and displays rows from `src/lib/pngMetadataRows.ts`.
- Native scan/store/display only covered filename, size, dimensions, favorite,
  and seen state before this slice.
- #103 is closed by PR #161, with PR verify run `28963688258` and main verify
  run `28963907394` passing.
- Live GitHub verification on 2026-07-09 found #117 closed by PR #162, not open
  as the older handoff text stated. Row25 remains a final inventory with no next
  bounded #117 pvu-state row.

## Deliverables

1. Add bounded native PNG `parameters` parsing without adding external
   dependencies.
2. Store prompt, negative prompt, compact settings summary, raw parameters, and
   metadata checked state in native SQLite image rows.
3. Add deterministic metadata to one ignored native fixture image.
4. Display metadata from the right-preview details label and detail modal label.
5. Extend native UI smoke with `metadataDisplay=true`.
6. Record the outcome in GitHub, SQLite, Agmsg, and next-thread handoff before
   closing the Goal.

## Verification

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --name-only -- scripts
git diff --check
corepack pnpm typecheck
```
