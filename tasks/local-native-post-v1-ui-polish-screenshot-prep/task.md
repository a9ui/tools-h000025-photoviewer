# Local Native Post-v1 #118 - UI Polish And Screenshot Sweep Prep

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/118

## Objective

Prepare the native UI polish and screenshot sweep without competing with the
active #117 pvu persistence continuation.

The first #118 pass should capture evidence and classify Human Surface
findings. It should not start broad UI rewrites. Any fix should be small,
native-only, and backed by native build/headless/UI evidence.

## Guardrails

- No Linear.
- Do not touch H000033.
- Do not deploy.
- Do not modify `src/**`.
- Do not start automatic enhancement workers.
- Do not delete existing cache/state assets.
- Keep changes under `local-native/**`, `docs/local-native/**`, or `tasks/**`.
- Do not edit `scripts/**` without explicit approval.
- Browser checks are baseline preservation only, not native acceptance.

## Source Packet

Read in full before planning or editing:

- `AGENTS.md`
- `PROJECT.md`
- `DESIGN.md`
- `project.toml`
- `START_HERE.md`
- `docs/operations-log.md`
- `local-native/README.md`
- `docs/local-native/native-intent-source.md`
- `docs/local-native/m20-verification.md`
- `docs/local-native/m19-verification.md`
- `docs/local-native/api-error-parity-matrix.md`
- `docs/local-native/malformed-import-recovery.md`
- `docs/local-native/state-migration-map.md`
- `docs/local-native/pvu-state-persistence-migration.md`
- `tasks/local-native-post-v1-queue-triage/report.md`
- `tasks/local-native-post-v1-pvu-state-persistence/task.md`
- `tasks/local-native-post-v1-malformed-import-recovery/task.md`
- `tasks/local-native-post-v1-api-error-parity/task.md`
- `tasks/local-native-m20/task.md`
- `tasks/local-native-m5/browser-regression-matrix.md`
- GitHub issue #118
- GitHub issue #117, as an active competing lane to avoid
- GitHub milestone #26

## Starting Evidence

- Milestone #26 is open with 20 open / 2 closed issues.
- #117 remains open after PR #123 and is represented by SQLite job #237 for a
  later bounded pvu row. Do not compete with that lane from #118.
- #118 has no issue comments yet.
- Open PRs are currently none.
- Latest confirmed main CI before #118 handoff work began was #28915773995 at
  `7d9b75f3cd7e0d4c60d6e33ecdd8d54204f6672f`; PR #123 and PR #124 also had
  passing PR CI before merge.
- Existing M5 matrix repeatedly defers native responsive/layout parity to
  desktop screenshot or Human Surface review for overlap, text fit, keyboard
  focus, and polish.

## Deliverables

1. Capture or prepare a native desktop screenshot/manual sweep artifact for the
   current `main` state.
2. Record findings by severity with exact surfaces, not broad taste notes.
3. Classify each finding as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or
   `NEEDS_HUMAN`.
4. Implement only adopted, tiny, native-only polish fixes if they are safe and
   verified in the same lane.
5. Leave larger UI redesign, preview-tab behavior, enhancement UI, display
   mode redesign, and keybinding recorder work in their own issues.

## Suggested Checks

```powershell
git status --short --branch
git rev-parse origin/main
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --check
```

If a screenshot helper is added, keep it under `local-native/**` and verify it
does not start browser/runtime/server paths or mutate enhancement state. If no
safe helper is added, use a recorded native manual sweep plus existing
headless UI evidence.

## Closeout Requirements

Before the #118 Goal can close:

1. Record the #118 outcome in GitHub.
2. Update the relevant SQLite job.
3. Send Agmsg pointers and inspect the trace.
4. Classify advice as `ADOPT`, `PARTIAL_ADOPT`, `REJECT`, `DEFER`, or
   `NEEDS_HUMAN`.
5. Create or hand off the next actual Codex thread if more native queue work
   remains.
6. Do not call the Goal complete until GitHub, SQLite, Agmsg, advice
   classification, and thread handoff are reflected.
