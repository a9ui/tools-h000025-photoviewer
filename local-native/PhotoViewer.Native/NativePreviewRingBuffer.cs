namespace PhotoViewer.Native;

internal sealed class NativePreviewRingBuffer : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Image> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _order = new();
    private readonly int _capacity;

    public NativePreviewRingBuffer(int capacity = 5)
    {
        _capacity = Math.Max(3, capacity);
    }

    public bool TryGet(string path, out Image? image)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(path, out var cached))
            {
                image = cached;
                return true;
            }
        }

        image = null;
        return false;
    }

    public void Store(string path, Image image)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(path, out var existing))
            {
                existing.Dispose();
                _entries[path] = image;
                return;
            }

            _entries[path] = image;
            _order.Enqueue(path);
            while (_order.Count > _capacity)
            {
                var evicted = _order.Dequeue();
                if (_entries.Remove(evicted, out var removed))
                {
                    removed.Dispose();
                }
            }
        }
    }

    public void Warm(IEnumerable<string> paths, Func<string, Image> loader)
    {
        foreach (var path in paths)
        {
            if (TryGet(path, out _))
            {
                continue;
            }

            try
            {
                Store(path, loader(path));
            }
            catch
            {
                // Warmup is best-effort.
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            foreach (var image in _entries.Values)
            {
                image.Dispose();
            }

            _entries.Clear();
            _order.Clear();
        }
    }

    public void Dispose()
    {
        Clear();
    }
}
