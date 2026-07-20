# Code-ready design: generation-aware shared-state writers

Date: 2026-07-18 JST
Status: reviewed design only; production code is unchanged

## Why `Task.Run(SaveFavorites)` is forbidden

`SaveFavorites` and `SaveSeenState` read and replace UI-owned
`_favorites`, `_seenPaths`, `_favoriteDirtyPaths`, and `_seenDirtyPaths`.
Calling either method on a worker would race the dispatcher, clear newer dirty
keys, apply an old merged map over newer UI state, and risk touching a closed
window. The existing synchronous methods also select a zero-timeout lock when
called from the dispatcher, so merely wrapping them changes lock semantics.

The safe seam is an immutable delta captured on the dispatcher, a pure
background disk kernel, and a generation-checked dispatcher completion.

## Production types

Add these internal records near the existing persistence helpers:

```csharp
internal readonly record struct FavoriteDelta(
    string Path,
    int? DurableBefore,
    int DesiredLevel,
    long Generation);

internal readonly record struct SeenDelta(
    string Path,
    bool WasUnseen,
    bool ShowedUnseenDot,
    long Generation);

internal enum SharedWriteStatus { Succeeded, Busy, Protected, Failed }

internal sealed record SharedWriteResult(
    SharedWriteStatus Status,
    long ThroughGeneration,
    IReadOnlyList<string> Keys,
    string? Error = null);
```

Create one `SharedStoreWriter<TDelta>` instance for Favorite and one
for Seen. It owns a private gate, a `Dictionary<string,TDelta>` keyed with
`OrdinalIgnoreCase`, one running task, and a monotonic `throughGeneration`.
Enqueue replaces a pending delta for the same path only when its generation is
newer. There is never more than one disk kernel in flight per store.

The writer API is deliberately small:

```csharp
void Enqueue(TDelta delta);
Task<SharedWriteResult> DrainOneBatchAsync();
Task<SharedWriteResult> DrainAsync(CancellationToken token);
bool HasPending { get; }
```

Do not give the actor a `Tile`, `Window`, dispatcher collection, or mutable UI
dictionary. The actor sees only normalized strings, primitive values, immutable
deltas, and the resolved target path.

## Disk kernels

Extract the current guarded I/O into static worker-safe methods:

```csharp
WriteFavoriteBatch(string path, IReadOnlyList<FavoriteDelta> deltas)
WriteSeenBatch(string path, IReadOnlyList<SeenDelta> deltas)
```

Each method must:

1. call `TryAcquirePersistenceLock(path, 2_000)` off the dispatcher;
2. read the latest disk value under that lock with the existing strict parser;
3. apply only the immutable batch (`1..5` set, `0` remove; Seen add only);
4. order with `OrdinalIgnoreCase` and use the existing atomic temp + move;
5. return status, committed generation, and committed keys, not the 100,000-entry
   merged map; copying that map back on the dispatcher would recreate a stall;
6. classify a lock timeout as `Busy`, invalid/newer JSON as `Protected`, and
   access/atomic-write failure as `Failed`;
7. retain existing stale-lock and target-specific temp-residue behavior.

Never run two full-map writers in parallel. Never read before acquiring the
lock. Never serialize a snapshot captured before the lock.

## Dispatcher state and generation rules

Add monotonic `_favoriteMutationGeneration` and `_seenMutationGeneration` plus
pending UI records keyed by normalized path. The first mutation for a key
captures its durable baseline. Later coalesced mutations retain that baseline,
replace only the desired value, and advance the key generation.

Favorite interaction order:

1. normalize path and increment the global generation;
2. capture durable baseline on the first pending mutation;
3. update `_favorites`, `tile.Fav`, filters, modal, selection surface, and
   header immediately;
4. enqueue `FavoriteDelta` and schedule the single writer loop;
5. do not show a durable-success toast until its generation commits.

Seen interaction order:

1. preserve `tile.Unseen` and `tile.ShowUnseenDot` in the first pending record;
2. set `_seenPaths`, `tile.Unseen=false`, and `tile.ShowUnseenDot=false` on the
   dispatcher;
3. enqueue one additive `SeenDelta`; repeated selection while the same key is
   pending is a no-op;
4. update the header immediately, but when `UnseenOnly` is active defer removal
   from the filtered gallery until durable success; a refused write must not
   make the selected image disappear and then jump back. No Enhancement work is
   allowed.

On success, a dispatcher completion may clear a pending key only when its
current generation is at or below `ThroughGeneration`. Advance the durable
baseline only for committed keys and retain every newer pending overlay. An old
completion must never replace or clear newer UI work. Unrelated external keys
are preserved on disk by latest-under-lock merge and become UI-visible on the
next normal reload; do not copy the complete disk map through the dispatcher.

On `Busy` or `Failed`, roll back only keys whose current generation equals the
failed batch generation. Favorite returns to its first durable level; Seen
restores `_seenPaths`, `Unseen`, and `ShowUnseenDot` together. Keep an immutable
failed-batch snapshot solely for the Retry action. Retry allocates new
generations, reapplies the optimistic UI state, and enqueues exactly once.

On `Protected`, perform the same generation-safe rollback, set the existing
`_favoritesWriteBlocked` or `_seenWriteBlocked` flag, preserve the original
bytes, and require repair/reload rather than automatic retry.

## Adoption boundary

The first production slice may route only an existing Favorite or Seen store
of at least 1 MiB through the new actor. Missing/small stores, explicit import,
and current fault smokes may remain on the proven synchronous path until the
actor fault matrix is green. This keeps the diff reviewable while fixing the
user's 2.78 MiB Favorite profile. Once an actor has pending/in-flight work, all
later mutations for that store stay on that actor even if file size changes.
Record which path was selected in smoke-only diagnostics; do not expose a user
setting or change the JSON schema.

After both small and large actor matrices are green, prefer one actor behavior
for every interactive mutation and remove the temporary size branch. Do not
leave two permanent semantic implementations.

## Close contract

The current `MainWindow_Closing` writes viewer state only. Preserve that fast
path exactly when both actors have no pending work.

When pending work exists:

1. set `e.Cancel=true` once and enter a `ClosingDrain` state;
2. disable new shared-state actions and announce a polite live status;
3. await both actors with one bounded 2.5-second deadline;
4. on success, set an allow-close flag and invoke `Close()` once on the
   dispatcher; the second Closing event performs only the existing state flush;
5. on busy/failure/timeout, keep the window open, restore interaction, show the
   existing Retry surface, and do not claim the application closed or discard
   an optimistic mutation silently.

No-pending close must not open or rewrite Favorite, Seen, Recent, or Enhancement
stores. Pending close must write each affected shared store at most once per
coalesced generation and leave zero lock/temp residue.

`LoadFolderAsync`/Refresh must drain pending shared writes before calling the
current Favorite/Seen loaders; an old reload may not erase optimistic work.
Explicit import must either drain first or refuse with the existing Retry
surface. It must not run concurrently with the actor.

## Required verifier ladder

Extend the current temp-only latency smoke before switching production:

1. **Coalescing:** 100 rapid changes to one Favorite path produce one final
   exact level and substantially fewer disk writes; changes to distinct paths
   are all present.
2. **Stale completion:** delay generation N, enqueue N+1, then release N. N may
   not clear/replace N+1 UI state or dirty work.
3. **External writer:** insert distinct Favorite and Seen keys after enqueue but
   before lock acquisition; latest-disk merge preserves both owners.
4. **Fresh lock:** bytes and UI durable baseline remain exact, Retry is visible,
   release + one Retry commits once.
5. **Malformed/future JSON:** original bytes remain identical, protected flag is
   set, optimistic Favorite/Seen and blue dot roll back together.
6. **Access/atomic failure:** same rollback and retry guarantees with no residue.
7. **Pending close:** close during the last batch, drain, exit, and reopen with
   exact Favorite/Seen/selection/modal/tab/viewer state.
8. **No-pending close:** current state-only flush and shared-store byte identity
   remain unchanged.
9. **Two WPF + Browser/third writer:** existing 20-iteration cross-runtime and
   crash-lock gates remain green.
10. **Performance:** three deterministic 100,000-entry runs must meet modal
    p95 <= 50 ms, Favorite p95 <= 65 ms, dispatcher max gap <= 110 ms, and every
    large/control ratio <= 2.5x.

Then run the focused Favorite/Seen/P1B/modal/shutdown/crash/cross-runtime gates,
Release build, and full `verify-wpf-product.ps1 -IncludeReloadSoak`. The fix is
not accepted from a latency win alone.
