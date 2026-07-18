namespace PhotoViewer.Wpf;

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

/// <summary>
/// Owns one generation-aware full-file writer. Enqueue only records immutable
/// deltas; the caller starts the pump after the current dispatcher input turn
/// so rapid UI mutations can coalesce without an arbitrary timer.
/// </summary>
internal sealed class SharedStoreWriter<TDelta>
    where TDelta : ISharedStoreDelta
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TDelta> _queued = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<IReadOnlyList<TDelta>, SharedWriteResult<TDelta>> _writeBatch;
    private readonly Func<SharedWriteResult<TDelta>, Task> _applyCompletion;
    private Task<SharedWriteStatus>? _pumpTask;
    private int _batchWriteCount;

    public SharedStoreWriter(
        Func<IReadOnlyList<TDelta>, SharedWriteResult<TDelta>> writeBatch,
        Func<SharedWriteResult<TDelta>, Task> applyCompletion)
    {
        _writeBatch = writeBatch ?? throw new ArgumentNullException(nameof(writeBatch));
        _applyCompletion = applyCompletion ?? throw new ArgumentNullException(nameof(applyCompletion));
    }

    public void Enqueue(TDelta delta)
    {
        if (string.IsNullOrWhiteSpace(delta.Path))
            throw new ArgumentException("A shared-store delta requires a path.", nameof(delta));

        lock (_gate)
        {
            if (!_queued.TryGetValue(delta.Path, out TDelta? existing)
                || delta.Generation > existing.Generation)
            {
                _queued[delta.Path] = delta;
            }
        }
    }

    public Task<SharedWriteStatus> StartOrJoinPumpAsync()
    {
        lock (_gate)
        {
            if (_pumpTask is { IsCompleted: false })
                return _pumpTask;

            _pumpTask = Task.Run(PumpAsync);
            return _pumpTask;
        }
    }

    public async Task<SharedWriteStatus> DrainAsync(CancellationToken token)
    {
        while (true)
        {
            SharedWriteStatus status = await StartOrJoinPumpAsync().WaitAsync(token).ConfigureAwait(false);
            if (status != SharedWriteStatus.Succeeded)
                return status;

            lock (_gate)
            {
                if (_queued.Count == 0)
                    return SharedWriteStatus.Succeeded;
            }
        }
    }

    public bool HasPendingOrInFlight
    {
        get
        {
            lock (_gate)
                return _queued.Count > 0 || _pumpTask is { IsCompleted: false };
        }
    }

    public int BatchWriteCount => Volatile.Read(ref _batchWriteCount);

    private async Task<SharedWriteStatus> PumpAsync()
    {
        while (true)
        {
            List<TDelta> batch;
            lock (_gate)
            {
                if (_queued.Count == 0)
                    return SharedWriteStatus.Succeeded;

                batch = _queued.Values
                    .OrderBy(static delta => delta.Generation)
                    .ToList();
                _queued.Clear();
            }

            SharedWriteResult<TDelta> result;
            try
            {
                Interlocked.Increment(ref _batchWriteCount);
                result = _writeBatch(batch);
            }
            catch (Exception ex)
            {
                result = new SharedWriteResult<TDelta>(SharedWriteStatus.Failed, batch, ex.Message);
            }

            await _applyCompletion(result).ConfigureAwait(false);
            if (result.Status != SharedWriteStatus.Succeeded)
                return result.Status;
        }
    }
}
