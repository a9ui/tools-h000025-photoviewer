# PRO Review Response: H000025 M0 Plan

Source conversation: private ChatGPT Project conversation; URL intentionally
omitted from the public repository.

Model setting reported by Chrome subagent: Pro extended.

## Conclusion

The roadmap is approved with changes. M0 should not be closed until the latest
main SHA has a verified successful GitHub Actions run in the authenticated
environment.

## Adopted Guidance

- Keep launch and scan relative improvement targets, but allow a future absolute
  budget to satisfy the same goal.
- Replace vague "feels immediate" language with cached modal p95
  input-to-displayed-image <= 100ms.
- Require uncached modal loading state <= 100ms and no blank or wrong-image
  flash.
- Use p95 as the tail-latency gate. Treat max/slowest values as diagnostics,
  not acceptance gates.
- Require minimum sample sizes before p95 claims:
  - Launch: 10 cold and 10 warm observations.
  - Scan: 5 observations.
  - Modal, thumbnail, and API scenarios: 30 observations.
- Count a relative improvement only if it is at least 10% and at least twice
  observed noise.
- If variance exceeds 10%, fix the harness before claiming optimization.
- Mandatory baseline evidence must include commit SHA, Windows version,
  CPU/RAM/storage/power mode, Node/pnpm versions, production build config,
  deterministic fixture manifest, cache state, scenario steps, correctness
  evidence, raw observations, and frozen budgets.

## Recommended Issue Order

1. Expand Issue #3 into repeatable Windows baseline and instrumentation.
2. Complete Issue #4 to enforce the explicit enhancement boundary.
3. Split Issue #5 into bounded visible-first thumbnail scheduling.

## Stop Condition

- Current main SHA is green for unit, typecheck, build, E2E,
  heavy-isolation, and benchmark checks.
- Preserved workflow matrix passes on Windows.
- Launch reaches an accepted absolute budget or 30% improvement.
- Scan reaches an accepted absolute budget or 25% faster first visible result.
- Scan progress gaps are <= 500ms and controls remain responsive.
- Thumbnail queues are bounded, visible-first behavior is proven, stale work
  cancels, and viewport-fill budgets pass.
- Cached modal p95 is <= 100ms.
- Uncached modal loading appears within <= 100ms with no stale flash.
- Ordinary browsing has 0 enhancement enqueues and 0 worker starts.
- No critical API p95, CPU, peak memory, or disk regression exceeds 10%.
- No sustained idle work or unbounded memory/queue growth.
- Three consecutive benchmark batches pass with raw before/after evidence.
