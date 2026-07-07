# Local Native M6 - Stack Merge And Review Closure

## Goal

Execute or explicitly close the release-candidate stack after M5 by checking
late advice, completing the browser-feature/native-parity matrix, retargeting
and merging stacked PRs safely, and recording the final native parity decision
without changing the browser app.

## Canonical Intent

Use `docs/local-native/native-intent-source.md` as the product-intent source.
The target is a fast native local PhotoViewer, not a webview, local server,
browser wrapper, or packaged Next.js app.

## GitHub Route

- M1 PR: #43 `Build local native viewing MVP`
- M2 PR: #48 `Build local native browser workflow drop-in`
- M3 PR: #55 `Build local native M3 performance acceleration`
- M4 PR: #61 `Build local native M4 parity and cache reuse`
- M5 PR: #66 `Prepare local native M5 release candidate readiness`
- M6 branch: `codex/h25-local-native-m6`

Linear is intentionally not used.

## M6 Matrix Decision

`tasks/local-native-m5/browser-regression-matrix.md` has been updated with an
M6 row-by-row classification.

Decision: `GO_WITH_ORDERED_STACK_MERGE_FOR_ISOLATED_RC_BASELINE`.

This means the stack can merge as an isolated local-native RC foundation, but
native full parity is not complete. The next milestone must continue the
native UI parity sweep.

## Verification

Details are recorded in `docs/local-native/m6-verification.md`.

Passed:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessScan -Folder .\.cache\native-fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessCacheCompat -Folder .\.cache\native-fixture
corepack pnpm typecheck
corepack pnpm test:unit
corepack pnpm test:e2e
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full
git diff --name-only -- src
```

Path correction:

```powershell
powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full
```

failed because `System/scripts/verify-project.ps1` does not exist in this
project checkout. The project verifier in `project.toml` is
`.\scripts\verify-project.ps1`.

## Merge Order

Use ordered stack merge:

1. Merge #43 into `main`.
2. Retarget #48 to `main`, verify Actions, then merge.
3. Retarget #55 to `main`, verify Actions, then merge.
4. Retarget #61 to `main`, verify Actions, then merge.
5. Retarget #66 to `main`, verify Actions, then merge.
6. Publish/merge M6 record after the M1-M5 stack state is settled, or keep it
   as the final closeout PR if merge mechanics require a separate record.

Prefer merge commits for the existing stack unless an upper branch is rebased
and checked for duplicate lower-stack commits.

## Remaining Native Parity Work

The matrix defers these areas to the next milestone:

- native folder-set UI and recent set behavior,
- malformed browser-state export UX,
- search UI edge states,
- sidebar filters and folder visibility,
- sorting/display controls,
- virtual gallery date/scroll/seen behavior,
- selection, preview tabs, and right preview panel parity,
- modal controls and metadata,
- delete confirmation/bulk flows,
- settings/keybinding UI,
- explicit enhancement UI,
- native API/error coverage,
- full persistence-key parity,
- native desktop layout screenshots/manual sweep.

Do not change `src/**` for those follow-ups unless a browser export helper is
explicitly approved, minimal, and verified.
