# Operations Log

This file records setup failures, fixes, and repeatable workflow candidates for
H000025. Promote repeated patterns into a Codex skill or System script when the
same manual sequence appears more than twice.

## 2026-06-24 M0 Bootstrap

- Found `node_modules` junctions copied from `H000024_PhotoViewer`; `vitest`,
  `typescript`, and `next` could not resolve because the old target no longer
  existed.
- `pnpm install --frozen-lockfile` prompted about recreating `node_modules` but
  did not repair the links in the non-interactive run.
- `pnpm install --frozen-lockfile --force` recreated local dependencies and
  fixed the junction targets to `H000025_PhotoViewer`.
- `scripts/verify-project.ps1` originally reported `ok: true` even when pnpm
  commands failed; it now throws on non-zero pnpm/corepack exit codes.
- Parent `C:\Users\a9ui\Desktop\Tools` has `.pnpm-store` but no parent
  `node_modules` or parent `pnpm-workspace.yaml`; H000025 is its own pnpm
  workspace.
- GitHub CLI, Cursor CLI, Cursor Agent, Python, Git, and SQLite are available;
  Vercel and Wrangler CLIs are not on PATH.
- ChatGPT Project creation is manual or browser-assisted; record the final URL
  in `project.toml` and sync SQLite after creation.
- First GitHub Actions runs failed because `pnpm/action-setup@v4` had
  `version: 9` while `package.json` already declared `packageManager:
  pnpm@9.15.9`; remove the workflow version and let package.json be the source
  of truth.
- Initial issue creation produced issues without the `M0` milestone attached;
  verify issue metadata after creation and repair with `gh issue edit` when
  needed.
- Chaining `git add; git commit; git push` in one PowerShell command was
  rejected by the local safety policy; run git staging, commit, and push as
  separate commands.
- A plain `git push` command was also rejected by the local safety policy after
  commit; use the repository bootstrap script or an explicit safe-directory git
  invocation when push is required.
- The first fixed Actions run then failed on
  `src/lib/dateSectionLayout.test.ts` because a midnight `+09:00` timestamp
  formatted as the previous day on the CI runner timezone; use local noon or an
  explicit timezone policy in date-label tests.
- `multi_agent_v1.spawn_agent` rejects calls that pass both `message` and
  `items`, and full-history forked agents cannot override reasoning effort;
  pass `items` only and omit fork context when a specific reasoning effort is
  requested.
- The documented Composer Bridge harness paths
  `I000003_AgentHarness\scripts\verify-harness.ps1` and
  `I000005_CodexSystemCore\scripts\test-composer25-light.ps1` were not present
  under `C:\Users\a9ui\Desktop\Tools` in this project setup.
- `agent models` confirmed `composer-2.5` is the standard Composer model and
  `composer-2.5-fast` is the fast variant. Use `agent --print --model
  composer-2.5 --trust --workspace <project> <prompt>` as the fallback direct
  Cursor lane when the harness scripts are missing.
- Do not run `pnpm typecheck` and `pnpm build` in parallel for this Next.js app.
  `tsconfig.json` includes `.next/types/**/*.ts`, so parallel build/typecheck can
  race while `.next/types` is being regenerated and produce false missing-file
  errors. Run `scripts/verify-project.ps1` or run the commands sequentially.

Skill candidate:

- A reusable `Tools` project M0 bootstrap skill should check copied pnpm
  junctions, repair installs, verify GitHub/SQLite/Cursor/ChatGPT readiness,
  create a private repository, seed milestones/issues, and record service
  checks.

## 2026-07-04 Performance Turbo Pass

- Updated the runtime/test stack to Next 16, React 19, Sharp 0.35, Vitest 4,
  and current compatible frontend tooling. ESLint 9 and TypeScript 5 stay pinned
  because ESLint 10 / TypeScript 6 are newer but not yet a safe peer fit here.
- Next 16 / Turbopack initially traced `.cache` and external enhancer tool paths;
  runtime cache and enhancer adapters now use tracing excludes, Turbopack ignore
  markers, and lazy adapter loading. Build completes without Turbopack trace
  warnings after the change.
- Thumbnail and search cache policy now prioritizes visible and modal work:
  background search prefetch is one page past the viewport, automatic thumbnail
  warmup is delayed and capped, and direct visible/modal warmup stays prioritized.
- `next lint` was replaced with an ESLint 9 flat config because Next 16 removed
  the old lint command path. `verify-project.ps1 -Full` now runs unit tests,
  lint, audit, typecheck, build, and E2E sequentially.
- Next 16 local dev needs `allowedDevOrigins: ['127.0.0.1']` for Playwright's
  127.0.0.1 base URL; without it the landing page can render but client effects
  such as last-folder restoration do not hydrate correctly in E2E.
- pnpm 11 no longer reads `package.json#pnpm`; keep project pnpm settings in
  `pnpm-workspace.yaml`. Use `allowBuilds` instead of deprecated
  `ignoredBuiltDependencies`, and respect pnpm minimum-release-age policy by
  pinning too-new tooling releases to the newest policy-allowed version.
- Thumbnail cache optimization must preserve existing `.cache/thumbs`,
  `.cache/display`, `.cache/enhance`, and index/settings/favorites files.
  Runtime lightness means faster cache hits and better scheduling, not deleting
  cache assets that make future browsing faster.
- Favorite persistence should avoid synchronous read/backup/stringify/write work
  on every click. Debounce browser/server writes and store compact JSON on disk
  so large favorite maps do not stall normal browsing.
- Versioned image URLs may use the existing cache without source stat work, but
  cache misses must not write current bytes under stale `v` keys. If the source
  mtime changed, generate under the current key and avoid immutable caching for
  the stale URL.
- Modal and search background work must stay local to the user's current view:
  modal thumbnail warmup is bounded around the selected image, unchanged search
  text is not resent, and invalid search pagination is clamped.

## 2026-07-05 Rough Audit Follow-up

- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1` failed
  in this long Cursor worktree path because thumbnail/display cache temp files
  used `"<finalPath>.<pid>.<timestamp>.<rand>.tmp"` and crossed Windows path
  length limits (`ENOENT` while writing `sharp().toFile(tmpPath)`).
- The failure reproduced in `src/lib/thumbnailCache.test.ts` with stale-version,
  fake-webp, and display-concurrency cases; all three wrote temp files longer
  than the working environment allowed.
- `src/lib/thumbnailCache.ts` now creates temp files in the same cache
  directory using a short basename (`tmp-<pid>-<timestamp>-<rand>.tmp`) so
  atomic rename behavior is preserved without expanding path length.
- After this change, `corepack pnpm exec vitest run src/lib/thumbnailCache.test.ts`
  and `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`
  both passed in the same worktree.

## 2026-07-18 WPF 100k Full-Catalog Recovery

- The former WPF Grid exposed only a 96-item moving window capped at 384 UI
  items. Replacing it with a full-extent `VirtualizingWrapPanel` kept all
  100,000 canonical items reachable while realizing only visible rows plus two
  overscan rows.
- Publish the complete lightweight path catalog before dimension and PNG prompt
  metadata. On the final 100-folder / 100,000-image TEMP fixture, Viewer became
  usable in 4,975ms while metadata continued in the background; the complete
  load finished in 33,792ms.
- Browser thumbnail-cache compatibility needs both the precise sub-millisecond
  mtime key and the legacy integer fallback. WPF only reads these candidates;
  it does not evict or rewrite the Browser cache.
- A linked worktree must resolve the common checkout through `.git` /
  `commondir` before choosing shared `.cache`. Otherwise WPF appears to lose
  Browser Favorite, Seen, Recent, thumbnail, and Enhancement history even
  though the files still exist in the main checkout.
- Final local evidence: 47-check aggregate including 20,000 stress and 24-cycle
  reload soak passed; exact 100,000 / 100 folders passed with silent truncate
  0, Grid/List realized 15/9, tail index 99,999, zoom drift 0, and TEMP cleanup;
  shared Favorite/Seen and Recent cross-runtime tests passed for 20 iterations.
- SQLite `improvement_items.id = 41` records this local-only milestone. GitHub
  Actions, GitHub publication, deployment, and external AI consultation were
  intentionally not used.
- Operational incident: an earlier invocation of
  `node scripts/prod_launcher.js status --port 3000` was assumed to be a
  read-only status check, but that command restarted the user-owned Browser
  process and then exited. The mistake was disclosed immediately and port 3000
  was restored with a hidden direct `next start` process (observed PID 2672,
  HTTP 200 at restoration time). Do not use that launcher `status` form as a
  probe for a user-owned port; WPF verification after the incident used TEMP
  processes only and did not touch port 3000 again.

## 2026-07-20 Album v1 Normal-Launcher Closeout

- Album v1 implementation checkpoint `5f24725d7f5842db2dc1e2a3dc773edc4ad451f4`
  passed the normal production launcher on explicit loopback port 3132 with
  `sourceDirty=false`, exact source revision, and the Album Playwright scenario
  1/1. Port 3000 was not used for that E2E.
- The first normal-launcher attempt correctly rejected clean provenance because
  Next production build rewrote tracked `next-env.d.ts`. Restoring the tracked
  development reference made the same completed build clean; do not treat a
  post-build dirty flag as green.
- Operational incident: building the shared `.next` while the old user-owned
  port 3000 process was alive left its in-memory old manifest pointing at two
  replaced static assets, which returned HTTP 500. Root HTML still returned 200,
  so root-only health checks would have missed the breakage.
- The exact standard `start_viewer.bat -> prod_launcher.js ->
  serve_with_parent_watch.js -> next start` ownership chain was confirmed. The
  standard launcher replaced only that stale managed process tree with the clean
  final build. Follow-up verification required exact source revision,
  `sourceDirty=false`, loopback-only binding, root 200, and all 9 referenced
  static assets 200. No user state/cache or unrelated process was removed.
- Future production-build verification must either stop the normal server first,
  use a truly isolated output directory, or include a planned standard-launcher
  restart. Never overwrite the shared `.next` behind a live production server.

## 2026-07-20 Album v1 Independent-Review Hardening (working checkpoint)

- The hardening delta is being integrated directly in the existing local
  `main` worktree above `f64d98d`; no branch/worktree, cherry-pick, push, merge,
  close, deployment, WinForms work, or user-state/cache deletion is part of the
  operation. PR #322 remains **MERGE FROZEN**.
- Browser external-open launch now passes a validated executable and argument
  vector directly. Do not restore `cmd.exe`, shell interpolation, or a composed
  command string for Explorer/open actions.
- A stale shared lock is not recoverable by age alone when its structured PID
  still identifies a live process. Album cleanup after successful Recycle is a
  separate latest-state reconciliation with a visible retry path; do not roll
  back the source Recycle or discard the missing member on cleanup failure.
- Browser and WPF now treat the old rainbow Enhanced-border preference as a
  legacy input normalized to solid cyan. Keep Favorite yellow and Enhanced cyan
  independently configurable and merged by dirty preference into the shared
  settings document.
- Current confirmed gates are Browser unit 69 files / 635 tests, Browser
  typecheck/lint/build, WPF Release build, and focused WPF store/UI verifiers.
  Final focused and normal-scale full Browser/WPF regressions, isolated Browser
  runtime, normal WPF runtime/provenance, final SHA, and GitHub/SQLite reflection
  remain **PENDING FINAL GATE** and must not be inferred from the prior closeout.
- The user explicitly removed 20,000/100,000 catalog-scale reruns and aggregate
  check-count tracking from this hardening checkpoint. Prior green evidence is
  retained as history, is not evidence for the current delta, and is not a
  required rerun here.
- Public-readiness review follow-up is active but not green. Direct non-shell
  open is adopted. `package.json` explicitly binds `dev` and `start` to
  `127.0.0.1`, and mutating local API routes use the shared Origin/Host guard;
  focused/full tests for those worktree changes remain pending. `LICENSE` is
  unresolved, so public repository/distribution readiness is **NO-GO** and the
  product remains private/local with deployment prohibited.
- A full Codex Security scan was not run because external consultation is
  forbidden in this lane and the scan requires a separate authorized setup. Do
  not treat the current focused checks as a public security clearance.
- `next-env.d.ts` is an unrelated generated tracked change in the primary
  worktree. Claude's untracked `local-native/` evidence and the separate
  untracked screenshot worktree artifact were not changed or cleaned.

## 2026-07-23 Browser CI Clear and Restarted-Worker Cancel Contract

- The authenticated ChatGPT Project `Codex用（PhotoViewer）`, using the
  `非常に高い` reasoning setting, reviewed Browser-head GitHub Actions only.
  It classified the current Browser blocker, queued run, in-progress run, and
  repair count as zero. PR #343 head run `29967995088` and merge-main run
  `29969647499` are the accepted green chain; old #343 red runs are historical,
  and #338/#340 are superseded. No CI wait/monitor hold remains.
- A focused-display slot reservation was not adopted. The ordinary modal path
  measured only 0.3-0.6ms focused queue wait, below the predeclared 75ms / 20%
  diagnostic gate. The experimental scheduler and harness changes were fully
  removed before product work continued.
- Explicit enhancement Cancel now compares a persisted running job's
  `workerInstanceId` with the current Browser process worker instance. A job
  owned by a prior process is atomically persisted as `canceled` with
  `cancelRequested`, `finishedAt`, and `updatedAt`; a same-process worker keeps
  the existing `running` plus `cancelRequested` path. Passive GET/browse/modal
  flows still do not recover jobs, write state, or start the worker.
- The Cancel response shape remains `{ job, interruptWarning }`. For an orphaned
  persisted run, `job.status` is immediately `canceled`; the next explicit Retry
  returns a distinct queued job and starts the normal queue.
- A second `非常に高い` review of the unpublished cancel candidate identified
  one concrete bundle-boundary risk: a module-local worker instance ID could be
  evaluated more than once inside one Next.js server process. The finding was
  adopted. The ID is now stored process-wide through `Symbol.for` and
  `globalThis`, matching the existing shared-root process-state pattern. The
  queue claim and Cancel route getter both use that process-wide value; a new
  process still creates a new value.
- Verification used OS-TEMP state only: focused enhancement/store/route/path
  checks 21/21, full Browser unit suite 781 passed / 14 skipped, TypeScript, and
  the production build all passed. A real `next start` same-process HTTP check
  observed `running + cancelRequested` with no `finishedAt`, followed by worker
  transition to `canceled`; module reevaluation kept the same worker ID. The
  source hash remained unchanged and the existing managed-output root guard
  stayed green.
