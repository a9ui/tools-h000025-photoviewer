# Engineering Operations Notes

This file records product-relevant build, test, performance, and runtime lessons.
It intentionally omits personal paths, private workspace routing, credentials,
local database identifiers, process IDs, and private service details.

Historical claims are not proof for current `main`; rerun the relevant gate on
the exact candidate revision.

## Project bootstrap

- H000025 is an independent pnpm workspace. A copied `node_modules` directory or
  stale junction must be removed and restored with
  `pnpm install --frozen-lockfile` rather than repaired manually.
- Verification scripts must throw on a non-zero tool exit code. Producing a
  final JSON object is not evidence of success if an earlier command failed.
- Date-label tests must use an explicit timezone policy or timestamps that do
  not cross a day boundary on a runner in another timezone.
- Run `pnpm typecheck` and `pnpm build` sequentially. Both use `.next/types`, so
  parallel execution can create false missing-file failures.

## Runtime and dependency upgrades

- Next.js 16, React 19, Sharp 0.35, Vitest 4, ESLint 9, and TypeScript 5 are the
  current compatible stack recorded by `package.json` and the lockfile.
- Keep pnpm workspace settings in `pnpm-workspace.yaml` and use an explicit
  build-script allowlist.
- Local dev and production start paths must bind to `127.0.0.1`.
- Turbopack tracing must exclude user cache and optional external enhancement
  tool paths.

## Cache and thumbnail behavior

- Preserve `.cache/thumbs`, `.cache/display`, `.cache/enhance`, and shared state.
  Runtime lightness means better cache use and scheduling, not deleting data
  that makes later browsing faster.
- Visible and modal thumbnail work has priority over speculative warmup.
- Background warmup is bounded and may be superseded by a newer viewport.
- Cache temporary names stay short and in the destination directory so atomic
  publication is not defeated by Windows path-length limits.
- A stale versioned image URL must not publish current bytes under an immutable
  stale cache key.

## Browser build and live runtime ownership

- Do not rebuild the shared `.next` directory behind a live production process.
  An old in-memory manifest can reference static assets replaced by the new
  build even when the root HTML still returns HTTP 200.
- Production verification uses the normal launcher ownership chain, exact source
  revision, a clean-source flag, loopback-only listener evidence, root response,
  and every static asset referenced by the root page.
- An explicit busy port fails. It must not kill, replace, or silently move a
  listener owned by another process.
- Test automation uses an isolated port and does not mutate the normal
  user-owned port 3000.

## WPF large-catalog recovery

- The Grid uses a full-extent `VirtualizingWrapPanel`; it does not publish a
  small moving catalog window.
- The lightweight path catalog is published before dimensions and prompt
  metadata so the viewer becomes usable while metadata continues in the
  background.
- Browser thumbnail-cache compatibility includes precise and legacy mtime keys.
  WPF reads compatible cache entries without taking ownership of Browser cache
  eviction.
- Linked worktrees resolve the common checkout before selecting shared cache and
  state paths.
- Large-catalog evidence is retained as historical performance context, but a
  new candidate needs a rerun when relevant virtualization, materialization,
  metadata-index, or stress-verifier paths change.

## Album v1 and shared state

- Browser and WPF use the same operation-oriented Album v1 store rather than a
  client-supplied full-document replacement.
- Mutations acquire the shared lock, read the latest document, validate an
  optional expected revision, and publish atomically.
- Malformed and newer-version state is preserved rather than overwritten.
- A successful source Recycle and Album membership cleanup are separate steps.
  Cleanup failure leaves a visible pending state and retry must not recycle the
  source again.
- A stale lock is not removed solely because it is old when its structured owner
  is still a live process.

## Browser/WPF parity hardening

- Browser external open uses a validated executable and argument vector. Do not
  restore `cmd.exe`, shell interpolation, or a composed command string.
- The Browser API surface is protected by the loopback Host/Origin/Fetch
  Metadata guard, with defense in depth on side-effecting routes.
- Enhanced thumbnail presentation is a stable solid border. Legacy rainbow
  values may be read and normalized but are not presented again.
- Browser and WPF merge only dirty thumbnail-border preferences into the latest
  shared settings document.
- Modal Filmstrip is vertical on the left, edge navigation uses the full 28%
  zones, and the WPF context menu is scoped to the image area.

## Public repository preparation

- Source publication and application deployment are separate decisions.
- The application remains loopback-only and has no supported LAN or Internet
  deployment.
- Full-history secret scanning, GitHub-surface privacy review, exact-SHA CI, a
  root license, and final owner approval are mandatory before changing
  visibility.
- Repository rename occurs while still private, followed by another full gate.
- A visibility change is not reversible in the security sense; already cloned,
  cached, or forked data cannot be recalled.

See `docs/public-repository-policy.md` and `docs/publication-runbook.md` for the
current publication process.
