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
