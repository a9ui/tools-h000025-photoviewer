# PhotoViewer

## Big Goal

Keep the existing PhotoViewer functions intact while making the app feel as
light as possible during local browsing, scanning, thumbnail loading, modal
navigation, search, favorites, deletion, and optional explicit enhancement
jobs.

## Success

PhotoViewer is successful when a large local image library can be opened,
scanned, browsed, filtered, previewed, and navigated with low waiting time, low
UI jank, and no accidental heavy background work.

Target outcomes:

- First usable screen appears quickly after launch.
- Folder scan reports progress and avoids blocking normal UI controls.
- Thumbnail work is bounded, cancellable, and prioritized around visible items.
- Modal navigation feels immediate after the first image loads.
- Search, favorites, tags, date sections, and delete/open actions keep their
  existing behavior.
- Optional enhancement work starts only from explicit user actions and never
  competes silently with browsing.
- Performance changes are measured with repeatable local tests before and after
  each milestone.

## Users

The primary user is a local Windows operator browsing large illustration or
photo folders. The user wants a fast viewer, not a cloud product or a heavy
gallery manager.

## Scope

- Preserve the current Next.js local-first PhotoViewer app.
- Measure startup, scan, thumbnail, modal, and API costs.
- Reduce redundant state updates, repeated filesystem reads, unnecessary image
  decoding, and avoidable client re-renders.
- Add small instrumentation where needed to prove improvements.
- Keep GitHub as the source of truth for milestones, issues, PRs, and CI.
- GitHub is the official source of truth for code, issues, PRs, CI, and
  milestone state.
- SQLite is a local ledger/cache for Codex jobs and service state, not a source
  of truth.
- Use Cursor for normal implementation tasks and Codex for control, review,
  small fixes, and integration.
- Large artifacts follow the workspace context-budget policy: do not load raw
  local DBs, generated reports, caches, `.codex`, `.agents`, or large logs into
  model context by default.

## Non-goals

- Do not rename the product as an upscaler.
- Do not remove existing user-facing PhotoViewer features for speed.
- Do not add paid APIs or cloud storage.
- Do not make Linear part of the workflow.
- Do not start background AI or GPU work automatically.

## Constraints

- No secrets in files or commits.
- No paid API, billing change, or production deploy without explicit approval.
- H projects deploy to Vercel when deployment is needed.
- All Japanese docs and reports are UTF-8; terminal mojibake is not file
  corruption unless UTF-8 decoding fails.

## Roadmap

### M0 - Project Spine And Baseline

Exit criteria:

- Local folder, Git repository, GitHub repository, milestone, and issues exist.
- Existing PhotoViewer code is preserved in the new project.
- `pnpm test:unit`, `pnpm typecheck`, and `pnpm build` status is known.
- Baseline performance plan and first measurement issue exist.
- ChatGPT Project URL is recorded in `project.toml`.
- M0 bootstrap lessons are recorded so repeated setup steps can become a
  reusable skill if they recur.

### M1 - Performance Baseline

Exit criteria:

- Define repeatable test folders and measurement commands.
- Measure launch, scan, thumbnail, modal navigation, and API timings.
- Add lightweight logs or test helpers only where the numbers are otherwise
  invisible.
- Create a prioritized bottleneck list.

Measurement targets:

- Launch: record cold and warm process start to shell interactive, first
  visible image, and viewport ready. Pass by meeting a later absolute budget or
  improving at least 30% from the first reliable baseline.
- Scan: record time to first correct result, progress-update gaps, full
  completion, result count, cancellation, and UI responsiveness. Pass by
  meeting a later absolute budget or improving first visible result at least
  25%.
- Thumbnails: record first visible thumbnail, viewport fill, max queued and
  in-flight work, stale cancellation, cache state, and eventual completion.
- Modal navigation: record cached input-to-displayed-image p95, uncached
  input-to-loading-state, and uncached input-to-correct-image. Cached movement
  target is p95 <= 100ms; uncached loading state target is <= 100ms with no
  blank or wrong-image flash.
- Local APIs: record route p50, p95, max diagnostic, error count, result count,
  and payload size for browse, scan, search, image, thumbnail warm, settings,
  favorites, tags, delete, and open routes.
- Optional enhancement jobs: prove ordinary browsing, preview, and modal
  navigation produce 0 enhancement enqueues and 0 worker starts.
- Runtime lightness: record CPU time, peak working set, repeated-navigation
  memory, disk activity, and UI long tasks.

### M2 - Viewer Lightness Pass

Exit criteria:

- Reduce UI jank and redundant renders in the grid, sidebar, preview panels,
  and modal.
- Bound thumbnail warming and visible-item prioritization.
- Keep all current viewer workflows intact.
- Compare before/after metrics.

Stop condition:

- Continue one bottleneck per issue until current main is green for unit,
  typecheck, build, E2E, heavy-isolation, and benchmark checks; preserved
  workflow matrix passes on Windows; launch/scan/thumbnail/modal/API budgets or
  accepted relative improvements pass; ordinary browsing has 0 enhancement
  enqueues and 0 worker starts; no critical p95, CPU, memory, or disk regression
  exceeds 10%; and three consecutive benchmark batches pass with raw evidence.

### M3 - Scan And Local API Lightness Pass

Exit criteria:

- Improve scan progress, cancellation, metadata extraction, and cache behavior.
- Keep folder, tag, search, favorite, delete, and open APIs compatible.
- Compare before/after metrics.

### M4 - Release Candidate

Exit criteria:

- All planned issues are closed.
- PRs are merged.
- CI passes.
- Vercel preview or production URL is recorded when deployment is requested.
- A milestone review pack is prepared for the ChatGPT Project / PRO review.
