# Local Native Post-v1 #118 UI Polish And Screenshot Sweep Prep

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/118

## Decision

Decision:
`ADOPT_NATIVE_UI_SCREENSHOT_HELPER_AND_STATE_SUMMARY_WRAP_FIX`.

Meaning:

- #118 starts as evidence/prep, not a broad native UI rewrite.
- Native acceptance is backed by native build, screenshot helper output, and
  existing native UI smoke.
- Browser PhotoViewer remains untouched; `src/**` stays empty in the diff.
- `scripts/**`, H000033, deployment, automatic enhancement workers, and
  cache/state deletion remain out of scope.
- Larger preview-tab, enhancement UI, display-mode, keybinding recorder,
  destructive-flow, and metadata work stays in separate issues.

## Live Starting State

Verified at start of this #118 lane:

- `origin/main`: `992a14fe63d40e06dba377d5d2650c1d36896976`.
- #118 is open in milestone #26.
- #117 is also open again for remaining `pvu_*` migration rows after PR #127
  merged; #118 must not compete with that implementation lane.
- Milestone #26 is open with 20 open issues and 2 closed issues.
- Open PRs: none.
- Latest main Actions run: #28921460659 passed for `main@992a14f`.
- SQLite job #238 was dispatched for #118.
- Parallel performance survey thread created #128 and SQLite job #239 as a
  proposal-only speedup lane; it is not an implementation dispatch for #118.
- Agmsg trace `h25-post-v1-queue-triage-final-20260708` has three
  `required_reply=none` pointers to `cursor_impl`, `claude_ui`, and
  `grok_consult`. The older
  `h25-post-v1-queue-triage-20260708` trace was not found in the last 1000
  history lines.

## Implemented

- Added `--headless-ui-screenshot` under
  `local-native/PhotoViewer.Native`.
- The helper opens the native WinForms form, imports current project state,
  scans `.cache/native-fixture`, applies the supplied search, selects an image,
  loads preview content for capture, focuses the search textbox, saves a PNG,
  and reports:
  - screenshot path;
  - window size;
  - scanned/visible counts;
  - preview-loaded state;
  - conservative text-fit and sibling-overlap counters;
  - focused control type;
  - passive enhancement state unchanged;
  - no browser runtime, local HTTP server, or Node runtime.
- Shortened the top-right native state summary from the long slash-separated
  form to a compact one-line form:
  `db 4 fav 1 seen 4 alb 2/4 pvu 6`.

## Screenshot Evidence

Artifact:

- `tasks/local-native-post-v1-ui-polish-screenshot-prep/artifacts/native-ui-screenshot.png`

Command:

```powershell
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-ui-screenshot .\.cache\native-fixture .\tasks\local-native-post-v1-ui-polish-screenshot-prep\artifacts\native-ui-screenshot.png --search fixture
```

Result:

```text
native-ui-screenshot complete runtime=winforms ... width=1280 height=820 scannedImages=4 visibleImages=4 previewLoaded=true textFitWarnings=1 overlapWarnings=0 focusControl=TextBox enhancementStateUnchanged=true browserRuntime=false localHttpServer=false nodeRuntime=false
```

Manual sweep from the captured image:

| Surface | Result | Classification |
| --- | --- | --- |
| Overall overlap | No visible control overlap at 1280x820 desktop capture; helper reports `overlapWarnings=0`. | `ADOPT` |
| Top-right state summary | Before fix it wrapped `pvu` and `6` onto separate visual lines. Compact text now stays on one line. | `ADOPT` |
| Text fit | Captured button/checkbox text is visually readable. Helper still reports `textFitWarnings=1`; this is treated as a conservative metric, not a visible blocker in the captured desktop image. | `PARTIAL_ADOPT` |
| Focus | Helper focuses the search textbox and reports `focusControl=TextBox`. | `ADOPT` |
| Keyboard operation | Existing native UI smoke verifies `keyboardNavigation=true`, `keyboardFavorite=true`, and `gridToggle=true`. | `ADOPT` |
| Passive enhancement isolation | Screenshot helper and UI smoke both report `enhancementStateUnchanged=true`; no browser/server/node runtime. | `ADOPT` |

## Verification

Commands run in this lane:

```powershell
git status --short --branch
git rev-parse origin/main
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
dotnet run --no-build --project .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj -- --headless-ui-screenshot .\.cache\native-fixture .\tasks\local-native-post-v1-ui-polish-screenshot-prep\artifacts\native-ui-screenshot.png --search fixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --check
```

Observed results so far:

- `dotnet build` passed with 0 warnings and 0 errors.
- `-PrepareFixture` passed and created only ignored `.cache/**` fixture/state
  in this clean worktree; existing cache/state files were skipped, not
  deleted.
- `--headless-ui-screenshot` passed and wrote the PNG artifact.
- `-HeadlessUiSmoke` passed with existing keyboard/detail/modal/filter/folder
  checks and `enhancementStateUnchanged=true`.
- `git diff --name-only -- src` returned empty.
- `git diff --check` passed.
- Final desktop image review at 1280x820 found no visible overlap; top-right
  state summary stays on one line after the compact text fix.

## Advice Classification

- `ADOPT`: add native screenshot helper as #118 evidence infrastructure.
- `ADOPT`: shorten native state summary to prevent the visible one-line wrap.
- `ADOPT`: use existing native UI smoke for keyboard operation and passive
  enhancement isolation.
- `PARTIAL_ADOPT`: keep the text-fit warning counter as conservative evidence;
  the first captured desktop image has no visible text cutoff after the state
  summary fix, but smaller viewport/manual sweeps remain useful.
- `REJECT`: broad UI rewrite, browser app changes, `scripts/**` edits,
  deployment, H000033 work, automatic enhancement workers, and cache/state
  deletion in this lane.
- `DEFER`: preview tabs/pinned previews, enhancement queue/output UI,
  compact/poster/aspect display modes, gallery zoom, editable keybinding
  recorder, destructive flows, and metadata work to their existing post-v1
  issues.
- `NEEDS_HUMAN`: none for this #118 first slice. Existing #102 folder range
  control semantics still need human/product direction before implementation.
