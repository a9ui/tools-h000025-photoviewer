# Local Native M5 - Release Candidate Readiness

## Goal

Turn the stacked local-native lane into a release-candidate review surface
without changing the browser app.

## GitHub Route

- Milestone: #10 `Local Native M5 - Release Candidate Readiness`
- Label: `local-native-m5`
- Issues:
  - #62 `M5-001 PR stack merge readiness review`
  - #63 `M5-002 Repeatable local-native fixture generation`
  - #64 `M5-003 Native parity RC review packet`
  - #65 `M5-004 Verification, closeout, and next handoff`

Linear is intentionally not used.

## Implemented Shape

- Native app remains under `local-native/PhotoViewer.Native`.
- Parent launcher remains `scripts/start-local-native.ps1`.
- Browser `src/**` files remain unchanged.
- Added `-PrepareFixture` to create deterministic ignored local-native fixture
  assets for headless checks.
- Fixture preparation writes generated test images and cache compatibility
  probes under `.cache/native-fixture`, `.cache/native`, `.cache/thumbs`, and
  `.cache/display`.
- Existing `.cache/favorites.json`, `.cache/albums.json`,
  `.cache/settings.json`, and `.cache/native/browser-localstorage-export.json`
  are not overwritten when present.
- The release-candidate review packet is
  `tasks/local-native-m5/release-candidate-review.md`.

## Safe Merge Decision

Do not merge upper stacked PRs while their base remains another feature branch.
The safe order is:

1. Keep PRs #43, #48, #55, #61, and the M5 PR draft until M5 verification and
   review are complete.
2. Merge PR #43 first.
3. Retarget PR #48 to `main`, update/rebase if GitHub shows duplicated lower
   stack commits, rerun Actions, then merge.
4. Retarget PR #55 to `main`, update/rebase if needed, rerun Actions, then
   merge.
5. Retarget PR #61 to `main`, update/rebase if needed, rerun Actions, then
   merge.
6. Retarget the M5 PR to `main`, rerun Actions, then merge.
7. Only delete stacked branches after every upper PR has been retargeted,
   merged, or explicitly closed.

Because the repository allows merge commits, squash, and rebase merges, do not
squash a lower stacked PR unless the next upper branch is rebased or otherwise
confirmed clean against `main`.

## Headless Verification

Details are recorded in `docs/local-native/m5-verification.md`.

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessIncrementalScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessSearch -Folder .\.cache\native-fixture -Search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
corepack pnpm typecheck
corepack pnpm test:unit
git diff --name-only -- src
```

## Next Goal Candidate

M6 should focus on stack merge execution after review: confirm no late Agmsg
advice changes the decision, mark draft PRs ready only when appropriate,
retarget/rebase in order, rerun CI at each step, merge or close the stack, and
record the final native parity decision.
