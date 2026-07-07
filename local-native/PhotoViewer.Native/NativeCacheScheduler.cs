namespace PhotoViewer.Native;

internal enum NativeCacheJobKind
{
    PreviewDecode,
    NeighborDecode,
    HeaderDimensions,
    MetadataWarm,
}

internal sealed class NativeCacheScheduler : IDisposable
{
    private readonly object _gate = new();
    private readonly PriorityQueue<(int Priority, long Sequence, string Key, Func<CancellationToken, object?> Work, TaskCompletionSource<object?> Completion), (int Priority, long Sequence)> _queue = new();
    private readonly Dictionary<string, TaskCompletionSource<object?>> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;
    private readonly int _maxQueued;
    private long _sequence;
    private bool _disposed;

    public NativeCacheScheduler(int maxQueued = 64)
    {
        _maxQueued = Math.Max(8, maxQueued);
        _worker = Task.Run(ProcessQueueAsync);
    }

    public async Task<T> ScheduleAsync<T>(NativeCacheJobKind kind, string path, Func<CancellationToken, T> work, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = $"{(int)kind}:{path}";
        TaskCompletionSource<object?> completion;
        TaskCompletionSource<object?>? existingCompletion = null;

        lock (_gate)
        {
            if (_pending.TryGetValue(key, out completion!))
            {
                existingCompletion = completion;
            }
            else
            {
                completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

                if (_queue.Count >= _maxQueued && kind != NativeCacheJobKind.PreviewDecode)
                {
                    completion.TrySetResult(default(T));
                }
                else
                {
                    var priority = kind switch
                    {
                        NativeCacheJobKind.PreviewDecode => 0,
                        NativeCacheJobKind.NeighborDecode => 1,
                        NativeCacheJobKind.HeaderDimensions => 2,
                        NativeCacheJobKind.MetadataWarm => 3,
                        _ => 4,
                    };

                    var sequence = Interlocked.Increment(ref _sequence);
                    _queue.Enqueue((priority, sequence, key, ct => work(ct)!, completion), (priority, sequence));
                    _pending[key] = completion;
                }
            }
        }

        return (T)(await (existingCompletion ?? completion).Task.ConfigureAwait(false))!;
    }

    public Task ScheduleAsync(NativeCacheJobKind kind, string path, Action<CancellationToken> work, CancellationToken cancellationToken = default)
    {
        return ScheduleAsync<object?>(kind, path, ct =>
        {
            work(ct);
            return null;
        }, cancellationToken);
    }

    private async Task ProcessQueueAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            (int Priority, long Sequence, string Key, Func<CancellationToken, object?> Work, TaskCompletionSource<object?> Completion)? job = null;
            lock (_gate)
            {
                if (_queue.TryDequeue(out var next, out _))
                {
                    job = next;
                }
            }

            if (job is null)
            {
                await Task.Delay(8, _shutdown.Token).ConfigureAwait(false);
                continue;
            }

            try
            {
                var result = job.Value.Work(_shutdown.Token);
                job.Value.Completion.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                job.Value.Completion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                job.Value.Completion.TrySetException(ex);
            }
            finally
            {
                lock (_gate)
                {
                    _pending.Remove(job.Value.Key);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort shutdown.
        }

        _shutdown.Dispose();
    }
}
