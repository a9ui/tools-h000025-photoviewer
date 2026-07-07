# Local Native M6 Handoff - Stack Merge And Review Closure

## Next Goal

Create a Goal for `Local Native M6 - Stack Merge And Review Closure` before
planning or editing.

Objective: execute or explicitly close the release-candidate stack after M5 by
checking late advice, retargeting/rebasing stacked PRs safely, rerunning CI,
merging or closing PRs in order, and recording the final native parity
decision without changing the browser app.

## Source Packet To Read First

Read these files in full before planning or editing:

- `AGENTS.md`
- `PROJECT.md`
- `DESIGN.md`
- `project.toml`
- `local-native/README.md`
- `docs/local-native/state-migration-map.md`
- `docs/local-native/m1-verification.md`
- `docs/local-native/m2-verification.md`
- `docs/local-native/m3-verification.md`
- `docs/local-native/m4-verification.md`
- `docs/local-native/m5-verification.md`
- `tasks/local-native-m1/task.md`
- `tasks/local-native-m2/task.md`
- `tasks/local-native-m3/handoff.md`
- `tasks/local-native-m3/task.md`
- `tasks/local-native-m3/m4-handoff.md`
- `tasks/local-native-m4/task.md`
- `tasks/local-native-m4/m5-handoff.md`
- `tasks/local-native-m5/task.md`
- `tasks/local-native-m5/release-candidate-review.md`
- this file

Then inspect GitHub PRs #43, #48, #55, #61, #66, GitHub Actions,
SQLite jobs summary, Agmsg trace for the M5 closeout correlation id, and local
`git status`.

## Current State To Verify Live

- M1 draft PR #43 is stacked on `main`.
- M2 draft PR #48 is stacked on PR #43.
- M3 draft PR #55 is stacked on PR #48.
- M4 draft PR #61 is stacked on PR #55.
- M5 draft PR #66 is `Prepare local native M5 release candidate readiness`.
- M5 branch is `codex/h25-local-native-m5`, stacked on
  `codex/h25-local-native-m4`.
- M5 GitHub issues are #62-#65 under milestone #10.
- M5 added `-PrepareFixture` and release-candidate review docs.
- M5 changed no `src/**` files.
- H33 remains untouched.
- Linear remains unused.

## Merge Rule

Do not merge #48, #55, #61, or #66 while their base is still another
feature branch. Merge #43 first, then retarget and verify the next PR against
`main` before merging it. Repeat for #55, #61, and the M5 PR.

Because squash merges can duplicate lower-stack commits in upper PR diffs, use
merge commits for this stack or rebase/update each upper branch after every
lower merge.

## Verification Minimum

At minimum before calling M6 complete:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

Also verify each retargeted PR's GitHub Actions result before merging it.

## Closeout Rule For M6

M6 completion requires:

- late M5 Agmsg advice checked and classified,
- PR stack retargeted, merged, or explicitly closed in the documented order,
- local native build and headless fixture checks,
- pnpm typecheck and unit tests,
- no accidental `src/**` changes unless explicitly approved and verified,
- GitHub issues and milestones updated,
- SQLite jobs summary updated,
- short Agmsg pointers sent and traced,
- next milestone handoff packet written if more local-native work remains,
- actual Codex thread-tool discovery and next thread create/handoff before the
  M6 Goal is closed.
