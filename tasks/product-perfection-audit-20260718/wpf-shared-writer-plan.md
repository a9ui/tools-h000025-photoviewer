# WPF shared Favorite / Seen writer: bounded implementation plan

Date: 2026-07-18 JST
Status: ready to implement after the current catalog/performance edit in
`MainWindow.xaml.cs` and `App.xaml.cs` is stabilized
Scope: WPF interactive Favorite and Seen persistence only. No Browser runtime,
port 3000, user state/cache, source image, Recycle Bin, Enhancement worker, or
deployment operation is part of this slice.

## Decision and acceptance boundary

The current WPF behavior is semantically correct but performs the complete
read/merge/sort/serialize/atomic-replace cycle synchronously on the dispatcher.
The repeated 100,000-entry profile measured:

- Modal-next p95: 186-204 ms;
- Favorite p95: 233-251 ms;
- dispatcher heartbeat max gap: 442-460 ms.

The bounded fix is a generation-aware, per-store, single-flight writer. UI
mutation remains immediate. Only immutable per-key deltas cross to the worker.
The worker remains compatible with the Browser lock and JSON format by reading
the latest disk value *after* it owns `<target>.lock`, merging the batch, then
using the existing atomic replacement protocol.

The first adoption step routes an interactive store through the actor when its
resolved file is at least `1_048_576` bytes. Missing/smaller stores retain the
current synchronous path while the new fault matrix is proved. Once a store is
adopted in a window, every later interactive mutation for that store stays on
the actor even if the file size changes. A smoke-only force switch covers small
actor cases; there is no user setting or environment feature flag.

This slice is accepted only when all correctness/fault gates and all three
performance repetitions pass. A latency improvement alone is not acceptance.

## Invariants that may not change

1. Favorite `0` is absence; Favorite `1..5` is exact.
2. Seen is additive. A repeated mark-seen is a no-op and does not rewrite disk.
3. Every write owns `<target>.lock`, reads latest disk under that lock, preserves
   unrelated external keys, writes sorted JSON, atomically replaces the target,
   and removes only target-specific lock/temp residue.
4. Malformed or unsupported data is never overwritten. It sets the existing
   protected-write flag and preserves original bytes.
5. At most one full-map writer per store may run. Favorite and Seen may run in
   parallel with each other, but two Favorite batches or two Seen batches may
   not.
6. No `Tile`, `Window`, dispatcher collection, or mutable `_favorites` /
   `_seenPaths` object crosses to a worker.
7. An older completion may not clear, overwrite, toast success for, or roll back
   a newer generation.
8. Ordinary browsing, modal navigation, preview, and close do not enqueue
   Enhancement work.
9. The JSON schema and Browser interoperability remain unchanged; no migration
   of the user's files is needed.

## Exact production file map

### 1. New `local-native/PhotoViewer.Wpf/SharedStoreWriter.cs`

Add the worker-only types in a separate file so the current large-catalog work
does not have to share its main edit surface.

```csharp
internal interface ISharedStoreDelta
{
    string Path { get; }
    long Generation { get; }
}

internal readonly record struct FavoriteDelta(
    string Path,
    int DurableBefore,
    int DesiredLevel,
    long Generation) : ISharedStoreDelta;

internal readonly record struct SeenDelta(
    string Path,
    bool DurableSeenBefore,
    bool WasUnseen,
    bool ShowedUnseenDot,
    long Generation) : ISharedStoreDelta;

internal enum SharedWriteStatus
{
    Succeeded,
    Busy,
    Protected,
    Failed,
}

internal sealed record SharedWriteResult<TDelta>(
    SharedWriteStatus Status,
    IReadOnlyList<TDelta> Batch,
    string? Error = null)
    where TDelta : ISharedStoreDelta;
```

Add `SharedStoreWriter<TDelta>` with this API:

```csharp
void Enqueue(TDelta delta);
Task<SharedWriteStatus> StartOrJoinPumpAsync();
Task<SharedWriteStatus> DrainAsync(CancellationToken token);
bool HasPendingOrInFlight { get; }
int BatchWriteCount { get; }
```

Constructor dependencies are:

```csharp
Func<IReadOnlyList<TDelta>, SharedWriteResult<TDelta>> writeBatch
Func<SharedWriteResult<TDelta>, Task> applyCompletionOnDispatcher
Func<TDelta, string> keySelector
Func<TDelta, long> generationSelector
```

Implementation rules:

- protect a case-insensitive queued dictionary and pump task with one private
  gate;
- `Enqueue` replaces the same key only when the incoming generation is newer;
- `Enqueue` does **not** start the pump itself;
- a dispatcher-background schedule in `MainWindow` starts the pump after the
  current input turn, so a deterministic rapid burst coalesces before snapshot;
- the pump snapshots and removes the queued dictionary under the gate, orders
  by generation, and calls `writeBatch` with `Task.Run`;
- invoke the completion callback exactly once for that immutable result; do not
  rely on an ambient synchronization context;
- after `Succeeded`, immediately take the next queued snapshot;
- after `Busy`, `Protected`, or `Failed`, stop the pump and retain any deltas
  queued while the failed batch was in flight;
- `DrainAsync` joins the one pump with `Task.WaitAsync(token)`. It never starts a
  second kernel and never cancels a kernel during an atomic replace;
- convert an unexpected kernel exception into `Failed`, preserving the batch
  for the dispatcher rollback path.

No NuGet dependency is needed; SDK-style compilation picks up the new file.

### 2. `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs`

#### Fields and UI-owned pending records

Add beside the current Favorite/Seen fields:

```csharp
private const long AsyncSharedStoreThresholdBytes = 1_048_576;
private long _favoriteMutationGeneration;
private long _seenMutationGeneration;
private readonly Dictionary<string, FavoritePendingMutation> _pendingFavoriteMutations =
    new(StringComparer.OrdinalIgnoreCase);
private readonly Dictionary<string, SeenPendingMutation> _pendingSeenMutations =
    new(StringComparer.OrdinalIgnoreCase);
private IReadOnlyList<FavoriteDelta>? _failedFavoriteBatch;
private IReadOnlyList<SeenDelta>? _failedSeenBatch;
private SharedStoreWriter<FavoriteDelta>? _favoriteWriter;
private SharedStoreWriter<SeenDelta>? _seenWriter;
private bool _favoriteWriterAdopted;
private bool _seenWriterAdopted;
private bool _favoritePumpScheduled;
private bool _seenPumpScheduled;
private bool _forceSharedWritersForSmoke;
private bool _closingDrainInProgress;
private bool _allowCloseAfterSharedDrain;
private bool _sharedActionsDisabled;
```

The UI pending records are immutable replacements:

```csharp
private sealed record FavoritePendingMutation(
    int DurableLevel,
    int DesiredLevel,
    long Generation);

private sealed record SeenPendingMutation(
    bool DurableSeen,
    bool WasUnseen,
    bool ShowedUnseenDot,
    long Generation);
```

`DurableLevel` / `DurableSeen` are the rollback baselines displayed when the
first pending mutation for a key was accepted. They are not a copy of the full
disk map.

#### Actor selection and construction

Add:

- `ShouldUseFavoriteWriter()`;
- `ShouldUseSeenWriter()`;
- `EnsureFavoriteWriter()`;
- `EnsureSeenWriter()`;
- `SafeStoreLength(string path)`.

`ShouldUse*` returns true when the smoke force is set, the store was already
adopted, or the current target length is at least the threshold. Reading length
failure selects the existing synchronous path; it must not silently mark the
store adopted.

The completion delegate explicitly posts
`ApplyFavoriteWriteResult` / `ApplySeenWriteResult` with
`Dispatcher.InvokeAsync(...).Task`.

#### Disk kernels

Keep the existing `TryLoadFavoritesFile`, `TryLoadSeenFile`,
`TryAcquirePersistenceLock`, `TryWriteAtomicText`, stale-lock handling, and
temp-residue cleanup as the one protocol implementation. Add near the current
`SaveFavorites` and `SaveSeenState` methods:

```csharp
private static SharedWriteResult<FavoriteDelta> WriteFavoriteBatch(
    string path,
    IReadOnlyList<FavoriteDelta> batch);

private static SharedWriteResult<SeenDelta> WriteSeenBatch(
    string path,
    IReadOnlyList<SeenDelta> batch);
```

Each kernel performs exactly this sequence off the dispatcher:

1. `TryAcquirePersistenceLock(path, PersistenceLockTimeoutMilliseconds)`;
2. if no lease: `Busy` when `path + ".lock"` still exists, otherwise `Failed`;
3. latest-disk parse with the existing strict parser; invalid shape returns
   `Protected` without changing bytes;
4. apply only the immutable batch (`DesiredLevel == 0` removes; Seen adds);
5. ordinal-ignore-case order and current indented JSON shape;
6. `TryWriteAtomicText`; false returns `Failed`;
7. success returns the same batch, not the merged full map.

Do not assign a 100,000-entry merged dictionary back on the dispatcher. External
unrelated keys become visible on the next normal reload.

The current `SaveFavorites` and `SaveSeenState` remain the synchronous small-file
and import fallback for this first slice. Once an actor is adopted, no
interactive mutation for that store may call the synchronous method.

#### Dispatcher scheduling

Add `ScheduleFavoriteWriterPump` and `ScheduleSeenWriterPump`. The first enqueue
posts one `DispatcherPriority.Background` callback. That callback clears the
scheduled flag and starts/joins the matching pump. This gives 100 mutations
performed in one dispatcher turn one deterministic coalescing boundary without
adding a timer or an arbitrary debounce delay.

#### Favorite interaction changes

Keep the existing synchronous branch intact. In `SetFavoriteLevel` and
`MutateSelectedFavorites`, select the branch before calling `SaveFavorites`.

Actor branch, per path:

1. reject while `_sharedActionsDisabled` or `_favoritesWriteBlocked`;
2. normalize path and increment `_favoriteMutationGeneration`;
3. on the first pending mutation capture the displayed durable level; later
   coalescing retains that baseline;
4. replace the pending record with the new desired level and generation;
5. update `_favorites` and every matching `Tile.Fav` immediately;
6. apply filters, selection/action surfaces, modal Favorite value, header, and
   viewer state immediately;
7. enqueue `FavoriteDelta` and schedule the pump;
8. return true to mean “accepted”, not “already durable”; do not show the current
   durable-success toast yet.

For a bulk action, build every pending record and tile update first, then enqueue
all deltas and schedule once. Track the requested path/generation set and the
existing success message as a small pending UI operation. Show that success only
when every generation committed. If any key was superseded, suppress the stale
operation toast instead of claiming the old level won.

`ApplyFavoriteWriteResult`:

- on success, for each key whose current generation equals the completed
  generation, remove the pending record;
- when a newer pending generation exists for the key, keep it but advance its
  `DurableLevel` to the completed delta's `DesiredLevel`. This is required for:
  gen N writes level 5, gen N+1 requests level 4, then N+1 fails; rollback must
  restore 5, not the pre-N value;
- on `Busy`/`Failed`, roll back only records whose generation exactly equals the
  failed delta, update `_favorites`, all matching tiles, filters, selection,
  modal, and header once, and save that exact failed batch for one Retry action;
- Retry allocates new generations, reapplies desired levels, enqueues once, and
  clears the saved failed batch before scheduling;
- on `Protected`, perform the same generation-safe rollback, set
  `_favoritesWriteBlocked`, preserve bytes, and expose repair/reload guidance
  without automatic Retry.

#### Seen interaction changes

Keep the current synchronous branch intact. The actor branch in `MarkTileSeen`:

1. rejects non-files, protected state, and `_sharedActionsDisabled`;
2. a key already seen with no pending mutation remains a no-op;
3. increment `_seenMutationGeneration` and capture `_seenPaths.Contains(key)`,
   `tile.Unseen`, and `tile.ShowUnseenDot` in the first pending record;
4. add `_seenPaths`, set `Unseen=false`, set `ShowUnseenDot=false`, update header,
   enqueue one additive `SeenDelta`, and schedule;
5. while `UnseenOnly` is active, do **not** call `ApplyFilters` until durable
   success. The card may not disappear and navigate elsewhere before a write
   that can still be refused;
6. repeated selection of the same pending Seen key is a no-op and enqueues no
   second delta.

`ApplySeenWriteResult`:

- success clears only the exact current generation, then applies the
  `UnseenOnly` filter/removal once and preserves the existing selection-null
  behavior if the selected tile leaves the result;
- a newer generation is retained and its durable baseline is advanced;
- `Busy`/`Failed` rolls back only exact generations, restoring `_seenPaths`,
  `Tile.Unseen`, and `Tile.ShowUnseenDot` together, then exposes one Retry;
- Retry allocates a new generation, reapplies the optimistic Seen state, and
  enqueues once;
- `Protected` also sets `_seenWriteBlocked` and has no automatic retry.

#### Reload and import boundary

> 2026-07-18 implementation closeout: the real reload boundary is included in
> P1 and is covered through both drains and async catalog publication. The
> `ImportPvu*ForSmoke` methods are smoke-only helpers rather than a product UI
> entry; their actor-pending refusal/retry case is explicitly deferred to P2
> test-boundary hardening. No product import command is exposed by this slice.

Add:

```csharp
private Task<SharedWriteStatus> DrainFavoriteWriterAsync(CancellationToken token);
private Task<SharedWriteStatus> DrainSeenWriterAsync(CancellationToken token);
private Task<bool> DrainSharedWritersAsync(TimeSpan timeout);
```

Immediately before the existing `LoadFavorites()` / `LoadSeenState()` calls in
`LoadFolderAsync`, drain any adopted writer. Recheck the load generation after
the await. Call the loaders only when that store drained successfully; on a
retryable/protected result, retain the current UI overlay and continue catalog
publication with a recoverable status. An older reload may never clear pending
optimistic state.

The synchronous `ImportPvuFavoriteLevelsForSmoke`,
`ImportPvuFavoritesForSmoke`, and `ImportPvuSeenImagesForSmoke` entry paths must
refuse while their actor has pending/in-flight work and offer Retry after drain.
They may not import concurrently or fall back to an unguarded whole-file write.

#### Close drain

Extract lines currently owned by `MainWindow_Closing` into
`FlushViewerStateForCloseOnce()`. It retains the existing exactly-once timer,
CTS, decode/hover cancellation and `SaveState` behavior.

`MainWindow_Closing` becomes:

- if `_allowCloseAfterSharedDrain` or no actor has pending/in-flight work, call
  `FlushViewerStateForCloseOnce()` and allow close immediately;
- otherwise set `e.Cancel = true` and start `DrainThenCloseAsync()` only once;
- while draining, set `_sharedActionsDisabled`, disable the main viewer/landing
  input surface, and announce “Saving Favorite and Seen changes before
  closing...”;
- await both actors with one 2.5-second deadline;
- success: set `_allowCloseAfterSharedDrain`, restore the UI flag, and invoke
  `Close()` once on the dispatcher. The second Closing event performs only the
  existing viewer-state flush;
- busy/failure/timeout: keep the window open, re-enable interaction, apply the
  generation-safe rollback if a result arrived, and expose Retry. Do not claim
  the app closed and do not discard an optimistic mutation silently.

The 2.5-second token cancels only the *wait*. It must never abort a kernel in the
middle of atomic replacement. If a kernel finishes after timeout, apply its
normal dispatcher completion and require another explicit Close.

No-pending close must remain the current fast path and must not open or rewrite
Favorite, Seen, Recent, or Enhancement files.

#### Smoke-only accessors

Add only the bounded hooks needed by `App.xaml.cs`:

- `ForceSharedStoreWritersForSmoke()`;
- `DrainSharedStoreWritersForSmokeAsync()`;
- Favorite/Seen actor adopted, pending, batch-count and generation accessors;
- a deterministic “before lock” await gate and “fail before atomic replace”
  one-shot injector held in immutable smoke-only delegates;
- `CloseAndWaitForSmokeAsync()` using the real Closing/Closed events.

The hooks receive only temp paths and primitive results. Production behavior is
unchanged when hooks are null.

### 3. `local-native/PhotoViewer.Wpf/MainWindow.xaml`

Add one name to the body root (`x:Name="ViewerBody"`) so the close drain can
visibly disable input without disabling the status toast or window caption.
`LandingPanel.IsEnabled` is toggled in parallel. No layout/style change is part
of this slice.

### 4. `local-native/PhotoViewer.Wpf/App.xaml.cs`

Add dispatch for `--shared-state-writer-smoke <temp-result.json>` and implement
`CaptureSharedStateWriterSmoke`. Reuse `ConfigureAutomationStorage`, hidden real
windows, real dispatcher turns, real JSON files, and existing PNG helpers.

Modify `CaptureSharedStateLatencySmoke` so the large actor path is measured
correctly:

- action samples end when the dispatcher handler returns;
- keep the 15 ms heartbeat running during worker serialization;
- explicitly await the writer drain before reading final files;
- use `CloseAndWaitForSmokeAsync` for pending-close scenarios rather than
  assuming the first `Close()` returns after final durability;
- report actual Favorite/Seen batch counts and `pendingAtClose`.

Do not add sleeps to make correctness pass. Use dispatcher turn boundaries and
smoke gates for deterministic ordering.

### 5. New `scripts/verify-wpf-shared-state-writer.ps1`

The script must require all inputs/results under a unique `%TEMP%` root, build
Release unless `-NoBuild`, launch only the isolated WPF smoke, assert every
boolean/count, show the JSON result, and remove the complete root. It may not
inspect or fingerprint the user's stores.

### 6. `scripts/verify-wpf-shared-state-latency.ps1`

Add `-Repetitions 3` (default 3), launch separate isolated profiles, and gate
each repetition rather than publishing measurement-only output.

Per repetition:

- Modal-next p95 `<= 50 ms`;
- Favorite action p95 `<= 65 ms`;
- dispatcher workload max gap `<= 110 ms`;
- large/control ratio for either p95 and the max gap `<= 2.5x`;
- all semantic/isolation assertions remain green.

### 7. `scripts/verify-wpf-product.ps1`

Add `verify-wpf-shared-state-writer.ps1` and the gating latency verifier to the
sequential Release ladder. Do not run simultaneous `dotnet build` processes
against the same output tree.

### 8. Documentation after implementation

Update `local-native/PhotoViewer.Wpf/README.md`, `docs/wpf-product-spec.md`, and
the milestone findings/recap with the actor threshold, optimistic/durable
semantics, close drain, fault recovery, test commands, and measured result. Do
not describe the actor as universal until the small-file branch is removed.

## State machine

### Per-store lifecycle

| State | Entry | Allowed exit | UI meaning |
| --- | --- | --- | --- |
| `Synchronous` | file below 1 MiB and never adopted | `AdoptedIdle` when threshold/force selects actor | current behavior |
| `AdoptedIdle` | actor exists, no queued/in-flight work | `Scheduled` on enqueue | UI equals durable state |
| `Scheduled` | delta queued; dispatcher-background pump posted | `Writing` | optimistic UI visible |
| `Writing` | immutable batch is under worker ownership | `Writing` for next successful batch, `AdoptedIdle`, `RetryableFailure`, or `Protected` | UI remains responsive |
| `RetryableFailure` | `Busy` or `Failed` | `Scheduled` through one Retry; `AdoptedIdle` after exact rollback with no newer queue | failed exact generations rolled back |
| `Protected` | latest disk is malformed/unsupported | reload/repair only | bytes preserved; actions refused |
| `ClosingDrain` | close requested with queued/in-flight work | actual close on success; previous actor state on failure/timeout | input briefly disabled, status visible |

Once a store leaves `Synchronous`, it never returns there in that window.
There is no sync fallback after an actor failure.

### Per-key generation rules

1. `None -> Pending(N)`: capture displayed durable baseline once.
2. `Pending(N) -> Pending(N+1)` before snapshot: replace desired only; one disk
   delta remains.
3. `InFlight(N) + Pending(N+1)`: N completion may advance N+1's durable
   baseline but may not clear or replace N+1.
4. `Succeeded(N)` with no newer generation: clear pending N.
5. `Busy/Failed(N)` with exact current N: rollback to N's durable baseline and
   retain one retry snapshot.
6. Any completion N with current generation greater than N: never roll back,
   clear, toast, or overwrite the newer UI value.
7. Retry always receives fresh generations; a result from the failed attempt
   cannot complete the retry.

## Deterministic verifier matrix

`CaptureSharedStateWriterSmoke` and
`verify-wpf-shared-state-writer.ps1` must cover all of these with real temp JSON:

1. **Coalescing:** 100 Favorite mutations to one path in one dispatcher turn
   produce exactly one Favorite disk batch and the final exact level. A
   multi-path batch preserves every distinct path.
2. **Stale completion/baseline advance:** gate generation N, enqueue N+1,
   release N, then fail N+1. N never clears N+1, and N+1 rollback restores N's
   committed value. Retry commits N+1 once.
3. **External writer:** after enqueue but before actor lock acquisition, insert
   distinct Favorite and Seen keys with the existing shared writer helper;
   release the actor and prove all owners remain.
4. **Fresh lock:** hold a real fresh lock, assert exact target bytes and visible
   rollback, Retry presence, then release and prove one Retry commits once.
5. **Malformed/unsupported JSON:** fingerprint original bytes, attempt Favorite
   and Seen writes, assert protected flags, byte identity, Favorite rollback,
   Seen `_seenPaths`/`Unseen`/blue-dot rollback, and no automatic Retry.
6. **Atomic/access failure:** inject one deterministic pre-replace `Failed`,
   assert byte identity, exact UI rollback, zero residue, then disable injection
   and prove one Retry.
7. **Unseen-only failure:** select an unseen tile under `UnseenOnly`, fail the
   write, and prove it never disappears/navigates before rollback. A subsequent
   successful Retry removes it exactly once.
8. **Pending close:** gate the last Favorite and Seen batches, request close,
   prove the window remains open and input disabled, release, then prove one
   actual close and exact reopen of Favorite, Seen, selection, modal/tab, and
   viewer state.
9. **Close failure/timeout:** a busy or timed-out pending close remains open,
   re-enables input, preserves a recoverable status, and never reports success.
10. **No-pending close:** retain existing `<300 ms`, exactly-one viewer-state
    flush, and byte identity for Favorite/Seen/Recent/Enhancement stores.
11. **Isolation:** source fixture and Enhancement jobs are byte-identical; no
    lock/temp residue; complete temp root removed; no Browser port used.

After that focused smoke passes, run sequentially:

1. `scripts/verify-wpf-shared-state-latency.ps1 -Repetitions 3`;
2. `scripts/verify-wpf-bulk-favorite.ps1`;
3. existing Favorite, Seen/shared-Seen, settings-unseen-dots, P1B/modal gates;
4. `scripts/verify-wpf-shutdown-state.ps1`;
5. `scripts/verify-wpf-crash-lock-recovery.ps1`;
6. `scripts/verify-cross-runtime-shared-state.ps1 -Iterations 20`;
7. Release build;
8. `scripts/verify-wpf-product.ps1 -IncludeReloadSoak`.

Every fault test must assert original-byte identity where refusal is expected,
zero `.lock`/`.tmp` residue, and exact temp-root cleanup.

## Migration and rollback

- Migration: none. Existing `favorites.json` and `seen.json` remain directly
  readable by both Browser and WPF. No backup/delete/rewrite occurs merely by
  upgrading.
- First-run adoption: lazy on the first interactive mutation at/above 1 MiB.
  No background rewrite occurs at startup.
- Failure: never fall back to synchronous writing after adoption; that would
  create competing whole-file writers and ambiguous generations.
- Code rollback: the actor commit can be reverted without data conversion
  because schema and lock protocol are unchanged. Any already committed actor
  write is an ordinary valid shared-state file.
- Roll-forward after the matrix is stable: remove the 1 MiB branch and route
  all interactive mutations through the same actor. Do not maintain two
  permanent semantic implementations.

## Current uncommitted-edit conflict audit

The shared worktree currently has active WPF catalog/correctness edits. This
plan file is conflict-free, but production implementation must wait for or build
on those edits rather than restoring `HEAD` versions.

Known overlap:

- `MainWindow.xaml.cs` field declarations: `_tiles` changed to
  `ResettableObservableCollection<Tile>`; add writer fields beside the final
  Favorite/Seen declarations without replacing that change.
- `LoadFolderAsync`: active work moved/cancelled generations, split catalog
  preparation/publication, and still calls `LoadFavorites` / `LoadSeenState`.
  Insert the drain immediately around the final loader location after that
  work settles; do not reapply an old function body.
- `ApplyPrimarySelection`: active work times `MarkTileSeen` through
  `_lastSeenSelectionMs`. Preserve the timing wrapper; the measured call should
  now cover only the accepted UI mutation/enqueue latency.
- `ApplyFilters` / `_tiles`: active work replaced Clear/Add with `ReplaceAll`.
  Failure/success reconciliation must call the final `ApplyFilters` and must not
  restore collection churn.
- smoke accessors near `LastSeenSelectionMsForSmoke` are actively edited; append
  writer accessors after the finalized block.
- `App.xaml.cs` has active catalog/scan/delete smoke additions and command
  dispatch changes. Add the new switch and result types against the final file,
  not with a large stale patch.
- `scripts/verify-wpf-catalog-stress.ps1`,
  `verify-wpf-delete-race.ps1`, and `verify-wpf-scan-cancel.ps1` are active but
  do not overlap the new writer script. `verify-wpf-shared-state-latency.ps1`
  is currently clean and is the safe existing-script edit surface.

Recommended implementation order after the active WPF agent is done:

1. add `SharedStoreWriter.cs` and compile it unused;
2. add kernels and actor state to the finalized `MainWindow.xaml.cs`;
3. route Favorite only, add its focused generations/fault tests, and pass them;
4. route Seen, add blue-dot/Unseen-only tests, and pass them;
5. add reload/import and close drain;
6. gate latency for three repetitions;
7. run crash/cross-runtime/full product ladders;
8. only then update product spec/recap and mark the P1 complete.
