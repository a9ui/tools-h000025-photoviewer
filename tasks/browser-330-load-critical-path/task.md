<!-- codex-idempotency: browser-load-root-cause-20260722 -->

# Measure and optimize the Browser folder-open critical path

## Outcome

Measure the current Browser-only folder-open pipeline on a representative TEMP
fixture, identify the dominant latency source, and land one small reversible
optimization with before/after evidence.

## Scope

- Current Next.js Browser runtime only.
- Folder selection through scan, first usable search result, and first visible
  thumbnail.
- Cold and warm production-mode measurements with phase evidence.

## Acceptance criteria

- [x] All measurement images/cache/state are isolated under TEMP; no user
  image, cache, state, or history is read or mutated.
- [x] Fixture shape, cache condition, source SHA, runtime mode, and observation
  counts are recorded; the old four-image artifact is not used as proof.
- [x] The primary cause is classified from measured phase timing.
- [x] Exactly one small reversible optimization targets the measured dominant
  cause.
- [x] Focused tests, Browser regression tests, lint, production build, and
  before/after measurements complete.
- [x] Draft PR #331 includes rollback notes and does not deploy.

## Non-goals and safety

- No WPF/native code or verification.
- No shared-state schema/root/lock-contract change.
- No real user image, enhancement output, cache, history, Favorites, Seen,
  Albums, Settings, or Recent folders.
- No broad rewrite, deployment, visibility, permission, credential, license,
  or history operation.
- Ordinary browsing must not enqueue enhancement work.

## Verification budget

- One representative baseline matrix and one candidate matrix on the same TEMP
  fixture set, plus one order-rotated concurrency control because the original
  baseline variance exceeded the policy gate.
- Focused changed-path tests once per code state, one Browser regression suite,
  one lint run, and one final production build.

Full evidence: `tasks/browser-330-load-critical-path/report.md`.
