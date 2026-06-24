# PRO Review Request: H000025 PhotoViewer M0 Plan

## Goal

Improve PhotoViewer so local image browsing feels lighter and faster without
removing existing user-facing behavior.

## Current Evidence

- Repository: https://github.com/a9ui/tools-h000025-photoviewer
- Visibility: private.
- Project: H000025 PhotoViewer.
- Local workflow: GitHub is the source of truth; SQLite is Codex's local job
  ledger; Cursor Composer 2.5 handles normal implementation; Codex handles
  control, review, CI, SQLite, and small fixes.
- ChatGPT Project: Codex用（PhotoViewer）.
- Local verify on 2026-06-24 passed:
  - `pnpm test:unit`: 57 tests passed.
  - `pnpm typecheck`: passed.
  - `pnpm build`: passed.
- GitHub Actions initially failed twice:
  - pnpm version was specified both in workflow and package.json.
  - a date-label test depended on runner timezone.
  Both fixes are pushed to `main`; latest CI run should be checked before
  accepting M0.
- M0 issues exist:
  - #1 M0: Project spine and baseline.
  - #2 M0: Ask PRO to review the current plan and success metrics.
  - #3 M1: Define repeatable PhotoViewer performance baseline.
  - #4 M1: Prove optional heavy jobs are isolated.
  - #5 M2: Reduce visible browsing jank.
  - #6 M3: Lighten scan and local API path.

## Constraints

- Preserve existing PhotoViewer functions.
- Do not rename the product as an upscaler.
- Do not remove search, tags, favorites, date sections, delete/open, preview,
  modal navigation, or optional explicit enhancement jobs.
- Heavy enhancement work must never start without explicit user action.
- No paid APIs, billing changes, cloud storage, or production deploy without
  explicit approval.
- H projects deploy to Vercel only when deployment is needed.

## Proposed Measurement Targets

- Launch to first usable screen: record cold and warm values, then target at
  least 30% improvement from the first reliable baseline.
- Scan: record time to first visible result, progress cadence, and full
  completion; target non-blocking controls and at least 25% faster time to first
  visible result.
- Thumbnails: record visible viewport fill time and queue size; prioritize
  visible thumbnails before offscreen warming and prevent unbounded queue growth.
- Modal navigation: record previous/next latency for cached and uncached images;
  target cached movement that feels immediate and uncached movement with clear
  loading state.
- Local APIs: record p50 and slowest observed timings for browse, scan, search,
  image, thumbnail warm, settings, favorites, tags, delete, and open routes.
- Optional enhancement jobs: prove normal browsing, preview, and modal
  navigation never enqueue heavy work without explicit user action.

## Question

Please review this roadmap and tell Codex:

1. Are the proposed measurement targets appropriate for making this local
   Windows PhotoViewer feel meaningfully lighter?
2. Which baseline metrics should be mandatory before any optimization PR?
3. What should the first 3 implementation issues be after M0?
4. What risks could cause accidental feature regression or misleading
   performance results?
5. What objective stop condition should define "good enough / complete" before
   asking PRO for final confirmation again?

## Desired Output

Return a concise implementation policy with:

- accepted metrics,
- changed or rejected metrics,
- recommended M1 issue order,
- verification commands,
- risks and guardrails,
- final stop condition.
