namespace PhotoViewer.Native;

internal sealed class NativeFolderWatcher : IDisposable
{
    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounce;
    private string? _root;

    public event EventHandler<string>? ChangesDetected;

    public void Watch(string root)
    {
        var resolvedRoot = Path.GetFullPath(root);
        lock (_gate)
        {
            if (string.Equals(_root, resolvedRoot, StringComparison.OrdinalIgnoreCase) && _watcher is not null)
            {
                return;
            }

            StopWatcherLocked();
            _root = resolvedRoot;
            _watcher = new FileSystemWatcher(resolvedRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnChanged;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnRenamed;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        if (!IsSupportedPath(args.FullPath))
        {
            return;
        }

        ScheduleNotify();
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        if (!IsSupportedPath(args.FullPath) && !IsSupportedPath(args.OldFullPath))
        {
            return;
        }

        ScheduleNotify();
    }

    private void ScheduleNotify()
    {
        lock (_gate)
        {
            _debounce?.Cancel();
            _debounce?.Dispose();
            _debounce = new CancellationTokenSource();
            var token = _debounce.Token;
            var root = _root;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(350, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested && root is not null)
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
            StopWatcherLocked();
        }
    }

    private void StopWatcherLocked()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = null;
        _root = null;
    }
}
