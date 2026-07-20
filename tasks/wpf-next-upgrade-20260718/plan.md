# Plan: WPF Large Shared-State Responsiveness

Date: 2026-07-18 JST

## Operating Rule

Measure first. A test-only instrumentation slice may be implemented to produce
the baseline, but production Favorite/Seen persistence does not change until a
repeatable RED result exists. Derive latency thresholds from measured control
and large-store distributions; do not choose a convenient number before the
data exists.

Only one product-risk lane is active in this milestone. Bulk Recycle and scan
progress stay queued until this lane is either fixed and verified or closed as
NOT-RED.

Live state: Phases 0-1 and the three-run RED decision in Phase 3 are complete.
The deterministic baseline is recorded in `baseline-results.md`. Phase 2 fault
extensions and Phase 4 production remediation are the next bounded milestone;
they are not replaced by the measurement-only gate.

## Phase 0: Preflight and Safety Envelope

1. Confirm the integrated branch HEAD, local dirty files, active WPF processes,
   and the user's Browser process/port without stopping or adopting any of them.
2. Record hashes and sizes for the user's WPF state, Favorite, Seen, and recent
   files as read-only safety evidence.
3. Read the current Browser/WPF normative contracts and the existing Favorite,
   Seen, cross-runtime, crash-recovery, shutdown, modal-navigation, and aggregate
   verifiers.
4. Confirm all new smoke paths can be overridden into one unique `%TEMP%` root.
5. Stop if the test would require a real source library, project `.cache`, user
   WPF state, port 3000, a real Recycle operation, or an external application.

## Phase 1: Add the Measurement-Only Baseline

1. Add a dedicated WPF smoke entry point and a focused verifier for large
   shared-state latency.
2. Generate both profiles in one isolated invocation:
   - small control with the same real image fixture and minimal shared stores;
   - approximately 2.8 MiB / 100,000-entry Favorite and Seen stores using compact
     synthetic absolute keys plus the real temp-fixture keys.
3. Record actual file bytes, entry counts, machine/runtime identity, and raw
   samples so later results are comparable.
4. Start a 15 ms dispatcher heartbeat and retain tick timestamps.
5. Execute 20 cycles through normal WPF action paths:
   - modal next;
   - selection-triggered Seen mutation;
   - Favorite level mutation;
   - one exact Favorite removal to level `0`;
   - repeated selection of an already-seen image.
6. Capture per-operation p50, p95, maximum, total time, heartbeat count, and
   maximum heartbeat gap separately for small and large profiles.
7. Keep performance reporting informational in this first version. Fail only
   correctness, isolation, process, residue, or malformed result conditions.

## Phase 2: Prove Semantics Around the Measured Boundary

1. Inject external-writer Favorite and Seen keys between WPF mutations and prove
   latest-disk merge preserves both writers.
2. Inject one fresh persistence lock and verify:
   - WPF reports refusal rather than false success;
   - UI/store roll back to the previous exact Favorite value;
   - retry after release commits exactly once;
   - external keys remain present.
3. Close at the final mutation boundary, reopen against the same temp stores,
   and verify exact Favorite, Seen, selection, modal/tab, and viewer state.
4. Assert valid JSON, no `.lock`/`.tmp` residue, source/jobs fingerprints
   unchanged, Enhancement activity zero, clean process exit, and no orphan.
5. Repeat the complete small/large measurement at least three times.

## Phase 3: Make the RED or NOT-RED Decision

1. Compare large-store p50/p95/maximum and maximum heartbeat gap with the
   measured small-control and idle distributions.
2. Inspect raw samples for warmup, filesystem cache, antivirus, and scheduler
   noise; do not hide outliers without an explicit reason.
3. Mark RED only if degradation is repeatable across three runs and localizes to
   Favorite/Seen persistence rather than image decode, modal rendering, or test
   fixture generation.
4. Derive the post-fix threshold from the stable control distribution and
   observed RED margin. Record the formula and measured values in the result;
   include both operation p95 and maximum dispatcher gap.
5. If NOT-RED, commit the evidence, document why no production change is
   justified, and stop this lane.

## Phase 4: Minimal Remediation Only After RED

1. Choose the smallest persistence design supported by the measurements. Start
   by evaluating a single-flight, generation-aware, coalescing background
   writer rather than parallel writers.
2. Keep UI Favorite/Seen state immediate, but serialize shared-store ownership
   so only one merge/replace is in flight for each store.
3. Preserve dirty keys until their generation is durably committed. A stale
   completion must never clear newer dirty work or overwrite newer UI state.
4. Continue using the existing bounded lock, stale-lock recovery, latest-disk
   merge, atomic replace, malformed-file refusal, and retry surface.
5. Implement deterministic shutdown drain/flush. Closing must not deadlock,
   silently abandon the last action, start a second writer, or rewrite unrelated
   shared stores.
6. Keep Favorite exact-level and Seen additive Browser semantics unchanged.
7. Do not alter source files, Enhancement jobs, Browser code, visual design, or
   store schemas.

## Phase 5: Verification Ladder

Run verification from narrowest to broadest:

1. New shared-state latency verifier with three small/large repetitions.
2. Existing focused Favorite and Seen verifiers.
3. Bulk Favorite, modal navigation/interaction, shutdown-state, recent-write
   ownership, crash-lock recovery, and reload-soak verifiers.
4. Browser/WPF shared Favorite/Seen and recent concurrency verifiers.
5. Release build with zero warnings/errors.
6. Full WPF product aggregate including stress and reload soak.
7. Normal WPF launcher provenance/adoption check in isolated automation mode.
8. Final process, port, source, and user-state safety recheck.

If production code changes after a green run, rerun the affected focused gate
and every broader gate whose evidence depends on that revision.

## Acceptance Checklist

- [x] All smoke stores and images are under one unique temp root.
- [x] The large profile records approximately 2.8 MiB and 100,000 entries with
      actual values included in the result.
- [x] Twenty modal-next/Favorite cycles use normal WPF action paths.
- [x] Small and large raw samples include p50, p95, max, heartbeat count, and
      maximum heartbeat gap.
- [x] Three repetitions support an explicit RED or NOT-RED decision.
- [ ] External writer keys survive exact latest-disk merge.
- [ ] Fresh-lock refusal, retry, close, and reopen are deterministic.
- [x] Favorite exact levels/removal and additive Seen semantics are preserved.
- [x] JSON remains valid and `.lock`/`.tmp` residue is zero.
- [x] Source and Enhancement job fingerprints are byte-identical.
- [x] No port, Browser runtime, user cache/state, or real Recycle backend is
      touched.
- [ ] If RED, the production fix passes the derived p95/max-gap threshold and
      the full verification ladder.
- [ ] If NOT-RED, no speculative production refactor is made.

## Deferred Queue

1. Bulk Recycle delayed-backend baseline, then async/progress/cancel only if RED.
2. Enumeration/metadata progress baseline, then throttled live progress only if
   the long zero-percent phase is reproduced.

These are separate milestones. Do not mix them into the shared-state diff.

## Stop Conditions

- Baseline isolation cannot be proven.
- A test path resolves outside `%TEMP%`.
- User state/cache/source or the running Browser/WPF process would be modified.
- The proposed fix weakens exact merge, malformed-file refusal, atomic replace,
  close durability, or Enhancement isolation.
- Evidence does not reproduce RED after three clean runs.
- Work expands into installer, publication, deployment, port 3000, P3
  Enhancement, UI restyling, or external consultation.
