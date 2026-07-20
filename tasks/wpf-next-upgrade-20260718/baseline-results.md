# WPF Shared Favorite / Seen Latency Baseline

Date: 2026-07-18 JST
Decision: **RED (3 / 3 reproducible)**

## Scope and safety

`scripts/verify-wpf-shared-state-latency.ps1` launches one isolated WPF
automation process. `ConfigureAutomationStorage` assigns a unique `%TEMP%`
root before `MainWindow` is constructed. The smoke then checks every state,
Favorite, Seen, recent, Enhancement-job, source-fixture, and result path again.

Each profile uses the same 22-image fixture and 20 normal modal-next + Favorite
cycles. Image timestamps are fixed so initial selection is deterministic.
Every cycle proves that Seen count increased by exactly one; fixture/JIT/decode
warm-up is outside the timed region. The final assertions require exact
Favorite removal/levels, additive Seen values, valid 100,000-entry seeds,
unchanged source and Enhancement jobs, no persistence residue, no Enhancement
activity, and removal of the complete temp root.

No user state/cache/source, Browser process, port 3000, Recycle backend, or
external application is read or changed.

## Repeated measurements

Runtime: .NET 8.0.28, Windows 10.0.26200, x64, 28 logical processors.
Large seeds were exactly 100,000 entries: Favorite 2,900,099 bytes and Seen
3,200,099 bytes. The timed Seen store started at 3,700,237 bytes in every run.

For scale only, a final read-only count of the untouched user stores reported
28,792 Favorite entries / 2,784,842 bytes and 3 Seen entries / 345 bytes. No
path or value was copied into the verifier. The synthetic gate therefore
matches the real Favorite byte scale while deliberately stressing a higher key
count; it is evidence of the synchronous algorithmic boundary, not a claim that
the user's exact file was mutated or timed.

| Run | Profile | Modal next p50 / p95 / max ms | Favorite p50 / p95 / max ms | 15 ms heartbeat max gap ms |
| ---: | --- | ---: | ---: | ---: |
| 1 | small | 4.356 / 5.939 / 6.560 | 23.803 / 30.553 / 42.820 | 52.651 |
| 1 | 100k | 147.794 / 204.389 / 220.341 | 156.686 / 239.545 / 248.542 | 449.053 |
| 2 | small | 4.168 / 4.908 / 5.072 | 19.754 / 29.108 / 35.349 | 43.354 |
| 2 | 100k | 124.646 / 186.232 / 198.670 | 171.218 / 251.245 / 271.627 | 441.835 |
| 3 | small | 3.726 / 5.158 / 5.760 | 17.985 / 28.545 / 30.023 | 37.766 |
| 3 | 100k | 146.192 / 203.575 / 218.441 | 159.398 / 233.028 / 241.059 | 459.759 |

All six profiles were semantically GREEN. The large profile was performance
RED in all three runs:

- Modal-next p95 was 31.3x to 39.5x the paired control.
- Favorite p95 was 7.8x to 8.6x the paired control.
- Maximum dispatcher gap was 8.5x to 12.2x the paired control and reached
  459.759 ms.

The measured gap matches one synchronous Seen full-store operation followed by
one synchronous Favorite full-store operation. Fixture generation, store
generation, first load, modal open, and warm-up are excluded. The bottleneck is
therefore localized to the UI-thread read/parse/latest-disk merge/order/
serialize/atomic-replace paths in `SaveSeenState` and `SaveFavorites`, not image
decode or modal rendering.

## Post-fix performance gate

The first remediated design must pass three repetitions with the same exactness
and isolation assertions and all of these limits:

- modal-next p95 at or below 50 ms;
- Favorite action p95 at or below 65 ms;
- maximum dispatcher heartbeat gap at or below 110 ms;
- no large/control p95 or max-gap ratio above 2.5x.

The limits are the rounded maximum of a practical interaction ceiling and two
times the worst observed small-control result. They retain machine-noise margin
without accepting the reproduced 186-251 ms operations or 442-460 ms freezes.

## Required remediation boundary

Do not parallelize whole-file writers. Use one generation-aware, single-flight,
coalescing writer per shared store. The UI may update immediately, while one
background owner performs the existing bounded lock, latest-disk merge, atomic
replace, malformed/future refusal, and residue cleanup. A completion may clear
only the dirty generations it committed; newer UI mutations remain overlaid.

The next implementation must add deterministic external-writer, fresh-lock,
write-refusal rollback, retry, pending-close drain, and reopen tests before the
production path is accepted. Seen remains additive. Favorite level `0` remains
absence and levels `1..5` remain exact. Failure rollback must restore both the
Unseen value and its visible blue-dot state.

The async writer is deliberately not rushed into this closeout: it changes
multi-process durability and shutdown semantics and requires those fault gates
before it can replace the currently correct synchronous implementation.
