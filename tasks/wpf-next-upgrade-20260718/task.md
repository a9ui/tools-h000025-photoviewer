# WPF Next Upgrade: Large Shared-State Responsiveness

Date: 2026-07-18 JST

## Goal

Use the Browser-authoritative WPF product contract to measure and, only after a
repeatable RED result, remove UI-thread stalls caused by large shared Favorite
and Seen JSON stores.

The first deliverable is a deterministic, temp-only baseline gate. It must
exercise a synthetic shared-state profile of approximately 2.8 MiB and 100,000
entries without reading or changing the user's real state, cache, source
library, or running PhotoViewer processes. Production behavior must not change
until that gate reproduces the latency problem and records enough evidence to
derive a meaningful threshold.

## Current Evidence

### Live baseline decision

The measurement-only gate is now implemented and RED in three deterministic
repetitions. See `baseline-results.md`. With exact 100,000-entry stores, modal
next p95 was 186-204 ms, Favorite p95 was 233-251 ms, and the 15 ms dispatcher
heartbeat stalled for 442-460 ms. Paired small controls remained at 5-6 ms,
29-31 ms, and 38-53 ms respectively. All exactness/isolation checks were green.

This is now a confirmed P1, not an unverified candidate. Production persistence
remains synchronous and correctness-first until the generation-aware writer,
external-writer/lock/refusal tests, close drain, reopen proof, and derived
50/65/110 ms thresholds are green.

- `MainWindow.SaveFavorites` reads, merges, orders, serializes, and atomically
  replaces the complete Favorite map on the UI thread for each Favorite action.
- `MainWindow.SaveSeenState` performs the same full-store operation for Seen.
- `MainWindow.ApplyPrimarySelection` calls `MarkTileSeen` for each newly
  selected real image, so modal navigation and gallery selection can enter the
  Seen write path repeatedly.
- Existing bulk, cross-runtime, crash-recovery, reload, and rapid-UI gates prove
  correctness and ownership with small stores. They do not measure Favorite or
  Seen mutation latency, dispatcher starvation, or close behavior with a
  production-scale shared store.
- The current implementation is correct-first and synchronous. This task must
  preserve exact merge, rollback, recovery, and Browser meaning while improving
  responsiveness; replacing a proven synchronous path with an unproven async
  path is not acceptable.

## Review Framework

Every proposed change is reviewed on these eight axes. A milestone is green
only when all applicable rows have explicit evidence.

| Axis | Required evidence |
| --- | --- |
| Non-destructive safety | Dedicated `%TEMP%` root and environment overrides; source fixture, Favorite, Seen, recent, state, and Enhancement job fingerprints are scoped and compared; no user path is opened or rewritten. |
| Correctness | Final Favorite levels are exact, Favorite level `0` removes the entry, Seen remains additive, selected/modal state is coherent, JSON is valid, and failed writes do not leave false UI success. |
| Regression resistance | A focused verifier is added first, then the related Favorite/Seen, persistence, modal, shutdown, cross-runtime, and full WPF product gates pass. |
| Performance | Record per-action wall time, p50, p95, maximum, dispatcher heartbeat count, and maximum heartbeat gap for small-control and large-store profiles. Do not use a wall-clock-only claim. |
| Recovery | Inject lock contention/write refusal and an external writer; preserve external keys, retry safely, reload exact state, and drain or explicitly refuse pending work on close without corrupting the store. |
| Accessibility | Long work must not strand keyboard focus; recoverable status is exposed through the existing live region, and modal navigation remains keyboard-operable throughout the test. |
| Launch and shutdown | Use only the isolated WPF verifier process; prove one clean exit, no orphan process, one final state flush, no `.lock`/`.tmp` residue, and a reopen that observes the committed state. |
| Browser meaning parity | Favorite is path-to-exact-level `1..5` with `0` absent; Seen is path-to-true and additive; latest-disk merge preserves another writer; browsing never writes Enhancement jobs or starts a worker. |

Prioritize only reproducible work using:

```text
priority = impact (1..5) x likelihood (1..5) x evidence gap (1..5)
```

Current ranking:

1. Large Favorite/Seen UI-thread persistence: `5 x 5 x 5 = 125`.
2. Bulk Recycle synchronous per-file execution: `5 x 3 x 5 = 75`.
3. Enumeration/metadata progress visibility: `3 x 4 x 5 = 60`.

## Baseline Gate Contract

Create one focused verifier, provisionally named
`verify-wpf-shared-state-latency.ps1`, backed by a dedicated WPF smoke entry
point. The verifier must meet all of the following before it can justify a
production change.

### Isolation and fixture

- Reject result and store paths outside `%TEMP%`.
- Create a unique root such as
  `%TEMP%\photoviewer-wpf-shared-state-latency-<guid>`.
- Route WPF state, Favorite, Seen, recent, and Enhancement job paths into that
  root with the existing environment overrides.
- Generate a compact, valid 100,000-entry Favorite/Seen payload whose serialized
  profile is approximately 2.8 MiB. Record actual bytes and entry counts rather
  than assuming the generator hit the target.
- Keep the actual image fixture small and fully inside the same temp root. Large
  shared-state keys that are not selected may be synthetic absolute Windows
  paths and do not need corresponding source files.
- Record source and Enhancement job fingerprints before interaction and prove
  them byte-identical afterward.
- Do not bind a port, call Browser HTTP APIs, touch port 3000, use the project
  cache, or invoke a real external application or Recycle Bin operation.

### Workload

- Run a small-store control and the large-store profile through the same code
  path and on the same verifier invocation.
- Start a `DispatcherTimer` heartbeat before the workload and record every tick
  timestamp so maximum heartbeat gap can be calculated.
- Perform 20 deterministic interaction cycles. Each cycle must navigate the
  modal to the next real image and apply a Favorite change through the normal
  WPF action path. Record modal-next and Favorite durations separately.
- Include Seen mutations for newly selected images and repeated selection of an
  already-seen image, proving the latter remains a no-op at the store level.
- Report raw duration samples plus p50, p95, maximum, heartbeat count, maximum
  heartbeat gap, bytes read/written if exposed without production behavior
  changes, and total elapsed time.
- Run at least three clean repetitions before declaring the issue reproducible.

### Exactness, concurrency, and close

- Insert a distinct external-writer Favorite key and Seen key between WPF
  mutations. The final latest-disk merge must preserve those keys and every WPF
  mutation.
- Prove exact Favorite levels for all 20 target actions, removal semantics for a
  level changed to `0`, and additive Seen values for every navigated image.
- Inject one fresh-lock refusal. The UI must not claim success, the previous
  persisted value must survive, and the retry route must commit exactly once
  after contention is released.
- Close while the final mutation is pending in the future remediated design,
  then reopen against the same temp stores. The committed final Favorite, Seen,
  selection, modal/tab, and viewer state must be exact. Closing must not rewrite
  unrelated shared stores.
- Require valid JSON, no lost external key, no `.lock`/`.tmp` residue, one clean
  WPF process exit, no orphan, source fingerprint unchanged, Enhancement jobs
  byte-identical, and zero Enhancement enqueue/worker activity.

### RED decision and thresholds

The first implementation of the verifier is measurement-only for performance:
correctness, isolation, residue, and lifecycle violations may fail the gate,
but no latency number is invented in advance.

After three small-control and three large-store samples:

1. Record the small-control distribution and idle heartbeat distribution.
2. Record the large-store p50/p95/maximum and maximum heartbeat-gap ratios
   against those controls.
3. Declare RED only when the large-store degradation is repeatable and clearly
   attributable to the Favorite/Seen persistence boundary.
4. Derive and document the post-fix performance threshold from those measured
   distributions, with enough margin for machine noise. The threshold must
   cover p95 and maximum heartbeat gap, not only total elapsed time.
5. If the result is not RED, stop with the evidence. Do not refactor persistence
   speculatively.

## Production Change Boundary After RED

If and only if the baseline is repeatably RED, design the smallest change that
keeps UI state immediate while moving full-store merge/serialize/replace work
off the dispatcher. A likely direction is a single-flight, generation-aware,
coalescing persistence queue, but the measured evidence chooses the design.

Any adopted design must retain:

- bounded create-new lock ownership and stale-lock recovery;
- latest-disk merge for Browser/WPF concurrency;
- exact Favorite dirty-key semantics and additive Seen semantics;
- malformed/future-file refusal without overwrite;
- visible retry on write refusal;
- deterministic close drain and reopen proof;
- no deadlock, stale completion, lost last action, duplicate write, residue, or
  Enhancement activity.

Do not combine the persistence fix with UI redesign, new visual tokens, cache
format migration, source editing, or Enhancement work.

## Follow-up Candidates Kept Out of This Slice

### 2. Bulk Recycle async/cancel

`ExecuteBulkDelete` currently invokes the Recycle backend serially on the UI
thread. A later milestone should first reproduce dispatcher starvation with a
temp-only delayed fake backend, then consider sequential background execution,
bounded progress, double-submit prevention, and cancellation of only work that
has not started. Successful moves remain committed, failures remain selected,
and Windows Recycle Bin remains the only real destructive backend.

### 3. Scan progress

Enumeration and metadata currently show a phase label but numerical progress
remains at zero until thumbnail work. A later milestone should measure a delayed
temp-only scan and, if reproduced, add throttled monotonic phase/count updates
with polite live status, cancellation under the existing target, stale-
generation suppression, and no catalog/order/state semantic changes.

## Out of Scope

- User state/cache deletion or migration.
- User source-library writes.
- Browser production launcher, port 3000, deployment, publication, installer,
  signing, or auto-update work.
- P3 Enhancement write ownership, cache quota, or worker behavior.
- UI restyling or new product semantics.
- External AI consultation, Agmsg consultation, Cursor, Claude, Grok, Fable, or
  PRO review.

## Completion Evidence

This milestone can close only with:

- a committed temp-only baseline verifier and raw small/large measurements;
- an explicit RED or NOT-RED decision;
- if RED, a separately reviewable minimal production fix and a green derived
  latency/heartbeat threshold;
- focused exactness/concurrency/recovery/close evidence;
- related and full WPF product verification;
- unchanged user state/cache/source fingerprints and no product/server orphan;
- a concise recap that leaves Bulk Recycle and scan progress as separate,
  bounded follow-up choices.
