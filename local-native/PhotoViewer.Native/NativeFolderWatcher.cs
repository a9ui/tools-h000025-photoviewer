namespace PhotoViewer.Native;

internal sealed class NativeFolderWatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _debounces = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<string>? ChangesDetected;

    public int WatchedRootCount
    {
        get
        {
            lock (_gate)
            {
                return _watchers.Count;
            }
        }
    }

    public void Watch(string root)
    {
        Watch([root]);
    }

    public void Watch(IEnumerable<string> roots)
    {
        var resolvedRoots = NativeFolderSet.NormalizeDistinct(roots)
            .Where(Directory.Exists)
            .ToList();
        lock (_gate)
        {
            if (SameRootsLocked(resolvedRoots))
            {
                return;
            }

            StopWatchersLocked();
            foreach (var resolvedRoot in resolvedRoots)
            {
                var watcher = new FileSystemWatcher(resolvedRoot)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                watcher.Created += (_, args) => OnChanged(resolvedRoot, args);
                watcher.Changed += (_, args) => OnChanged(resolvedRoot, args);
                watcher.Deleted += (_, args) => OnChanged(resolvedRoot, args);
                watcher.Renamed += (_, args) => OnRenamed(resolvedRoot, args);
                _watchers[resolvedRoot] = watcher;
            }
        }
    }

    private void OnChanged(string root, FileSystemEventArgs args)
    {
        if (!IsSupportedPath(args.FullPath))
        {
            return;
        }

        ScheduleNotify(root);
    }

    private void OnRenamed(string root, RenamedEventArgs args)
    {
        if (!IsSupportedPath(args.FullPath) && !IsSupportedPath(args.OldFullPath))
        {
            return;
        }

        ScheduleNotify(root);
    }

    private void ScheduleNotify(string root)
    {
        lock (_gate)
        {
            if (_debounces.Remove(root, out var previous))
            {
                previous.Cancel();
                previous.Dispose();
            }

            var debounce = new CancellationTokenSource();
            _debounces[root] = debounce;
            var token = debounce.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(350, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                    {
                        ChangesDetected?.Invoke(this, root);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounce reset.
                }
            }, token);
        }
    }

    private static bool IsSupportedPath(string path)
    {
        if (Directory.Exists(path))
        {
            return true;
        }

        return NativeImageScanner.SupportedExtensions.Contains(Path.GetExtension(path));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            StopWatchersLocked();
        }
    }

    private bool SameRootsLocked(IReadOnlyCollection<string> roots)
    {
        return roots.Count == _watchers.Count && roots.All(root => _watchers.ContainsKey(root));
    }

    private void StopWatchersLocked()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        foreach (var debounce in _debounces.Values)
        {
            debounce.Cancel();
            debounce.Dispose();
        }

        _debounces.Clear();
    }
}
