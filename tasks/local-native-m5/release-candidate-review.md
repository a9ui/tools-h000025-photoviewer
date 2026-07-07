# Local Native M5 Release-Candidate Review Packet

## Review Question

Is the H000025 local-native stack ready to become the release-candidate review
surface, with browser app behavior preserved and a safe merge/close path
identified?

## Scope

In scope:

- PRs #43, #48, #55, and #61.
- Local-native implementation and docs under `local-native/**`,
  `scripts/start-local-native.ps1`, `docs/local-native/**`, and
  `tasks/local-native-*`.
- M5 fixture generation and verification evidence.
- GitHub issue/milestone state for local-native M1-M5.
- SQLite job summaries and Agmsg pointers as local control-plane evidence.

Out of scope:

- `src/**` browser app changes.
- H000033.
- Linear.
- deployment.
- automatic enhancement workers.
- deleting `.cache/thumbs`, `.cache/display`, `.cache/enhance`, favorites,
  albums, settings, or native SQLite state.
- browser profile or Chrome localStorage scraping.

## Browser Baseline Gate

The browser app remains the product baseline. M5 changed no `src/**` files,
but the existing browser E2E suite is narrow: it currently covers only landing
page folder workflow and restoring the last folder set. That is not enough to
declare the full browser app regression-checked.

The required browser sweep is now captured in
`tasks/local-native-m5/browser-regression-matrix.md`. M6 must complete or
explicitly classify every row before merging the local-native stack.

## Current PR Stack

| PR | State | Base | Head | Head SHA | Latest verify |
| --- | --- | --- | --- | --- | --- |
| #43 `Build local native viewing MVP` | draft/open, CLEAN | `main` | `codex/h25-local-native-m1` | `a85b2287adf63cc42e7001129cb5dec090a2adfa` | success, run `28867872288` |
| #48 `Build local native browser workflow drop-in` | draft/open, CLEAN | `codex/h25-local-native-m1` | `codex/h25-local-native-m2` | `d2428db45a4f2dd2f0723b2889181e5f8fa08ea1` | success, run `28869670313` |
| #55 `Build local native M3 performance acceleration` | draft/open, CLEAN | `codex/h25-local-native-m2` | `codex/h25-local-native-m3` | `43caeef1e94a816a55f28c1b5990eef30bda94e9` | success, run `28872084793` |
| #61 `Build local native M4 parity and cache reuse` | draft/open, CLEAN | `codex/h25-local-native-m3` | `codex/h25-local-native-m4` | `69cb93c1f9d90024e03f328674ce63aaf01f7e6f` | success, run `28881130949` |
| #66 `Prepare local native M5 release candidate readiness` | draft/open | `codex/h25-local-native-m4` | `codex/h25-local-native-m5` | verify live | initial success, run `28882218377`; re-check latest before closeout |

PR diffs were checked with `gh pr diff <n> --name-only`; PRs #43, #48, #55,
#61, and #66 include no `src/**`.

## Local Verification Evidence

See `docs/local-native/m1-verification.md`,
`docs/local-native/m2-verification.md`,
`docs/local-native/m3-verification.md`,
`docs/local-native/m4-verification.md`, and
`docs/local-native/m5-verification.md`.

M5 added a deterministic fixture preparation command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
```

The fixture generation result in this worktree:

- 3 PNG source images under `.cache/native-fixture`
- 1 favorite row source
- 2 albums with 3 album membership rows
- 5 explicit browser `pvu_*` export keys
- deterministic thumbnail/display compatibility classes:
  - thumbnail compatible/missing/incompatible: `1 / 1 / 1`
  - display compatible/missing/incompatible: `1 / 2 / 0`

The fixture command does not overwrite existing browser state files when they
already exist.

Full project verification also passed locally:

- `powershell -ExecutionPolicy Bypass -File .\System\scripts\verify-project.ps1 -Full`
- checks: required files, `pnpm test:unit`, `pnpm lint`,
  `pnpm audit --audit-level moderate`, `pnpm typecheck`, `pnpm build`,
  `pnpm test:e2e`
- unit result: 16 files, 94 tests passed
- browser E2E result: 2 tests passed
- lint result: 0 errors, 2 existing `<img>` warnings in
  `src/components/CachedImage.tsx`

## Parity Coverage

Covered:

- native virtual list/grid browsing shell
- folder scan and incremental scan
- file watcher refresh path
- direct image preview and previous/current/next ring buffer
- search with FTS and substring fallback
- favorite import, filter, and mutation
- album summary and membership import
- settings/key-binding compatibility summary
- explicit browser `pvu_*` JSON export import
- read-only thumbnail/display cache compatibility measurement
- Recycle Bin delete behavior with no hard-delete fallback
- open file and Explorer folder selection actions

Not covered:

- direct Chrome profile localStorage reads; intentionally out of scope
- full feature-by-feature browser regression sweep; required by
  `browser-regression-matrix.md` before stack merge
- automatic native reuse of browser thumbnail/display assets; M4 only measures
  compatibility
- browser export helper in `src/**`; deferred unless explicitly approved
- deployment or public-share access
- H000033 integration

## Merge And Close Decision

Recommended decision: `GO_WITH_ORDERED_STACK_MERGE`.

Do not merge upper PRs while their base remains another feature branch. Use
ordered stack merge with one verify gate at each step:

1. Keep all local-native PRs draft through M5 review.
2. Merge #43 first.
3. Retarget #48 to `main`, update/rebase if duplicated lower-stack commits
   appear, wait for Actions, then merge.
4. Retarget #55 to `main`, update/rebase if needed, wait for Actions, then
   merge.
5. Retarget #61 to `main`, update/rebase if needed, wait for Actions, then
   merge.
6. Retarget and merge PR #66 last, after its fixture/review docs and CI
   pass against the integrated stack.
7. Do not mark the stack merge-ready until the browser regression matrix is
   completed or each remaining row has an explicit blocked/deferred decision.

Merge commits are the lowest-friction path for this stack because the
repository allows merge commits and the upper branches already contain lower
branch commits. If squash is used for a lower PR, the next upper branch must be
rebased or otherwise checked to prevent duplicate lower-stack changes in the
diff.

## Issue And Milestone State

- M2 issues #44-#47 are closed.
- M3 issues #49, #50, #52, #53, and #54 are closed.
- M4 issues #56, #57, #59, and #60 are closed.
- M1 issues #37-#42 were still open at M5 startup despite M1 verification
  passing; M5 closed them with verification comments and closed milestone #6.
- M5 issues are #62-#65 under milestone #10.

## Advice Checkpoint

M4 closeout Agmsg correlation id: `h25-m4-closeout-20260708`.

Trace result at M5 startup:

- `cursor_impl`: sent, `required_reply=none`, no reply found.
- `claude_ui`: sent, `required_reply=none`, no reply found.
- `grok_consult`: sent, `required_reply=none`, no reply found.

Adoption classification: no actionable advice to adopt, reject, defer, or
escalate at M5 startup.

## Remaining Risks

- Stack merge mechanics remain the main risk. Retargeting after each merge and
  rerunning Actions is required.
- Fixture state is intentionally ignored local data. It proves repeatability in
  a clean worktree, not production-user state migration.
- Browser export helper remains deferred; adding one would touch `src/**` and
  needs explicit approval and focused verification.
- Browser regression remains the main product-risk gap. Existing automated
  E2E proves only landing-level smoke, not the full feature set.
- Cache compatibility is still measurement-only. Native runtime reuse should
  remain off until compatibility, invalidation, and fallback behavior are
  reviewed separately.
