using System.Diagnostics;
using System.Drawing.Imaging;

namespace PhotoViewer.Native;

internal static class NativeHeadlessPerformance
{
    public static int Run(string folder, int iterations, string searchQuery)
    {
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"native-perf error=folder-not-found folder=\"{folder}\"");
            return 2;
        }

        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var store = new NativeImageStore(projectRoot);
        store.ImportProjectState();
        var favorites = store.LoadFavorites();
        var existing = store.LoadImagesForRoot(folder).ToDictionary(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase);

        var scanStopwatch = Stopwatch.StartNew();
        var incremental = NativeIncrementalScanner.ScanAsync(
            folder,
            existing,
            favorites,
            progress: null,
            CancellationToken.None).GetAwaiter().GetResult();
        scanStopwatch.Stop();
        store.ApplyIncrementalScan(folder, incremental, scanStopwatch.Elapsed, fullRescan: existing.Count == 0);
        var images = store.LoadImagesForRoot(folder);
        if (images.Count == 0)
        {
            Console.Error.WriteLine("native-perf error=no-images");
            return 2;
        }

        var ring = new NativePreviewRingBuffer(capacity: 5);
        var scheduler = new NativeCacheScheduler();
        var searchSamples = new List<long>();
        var navigationSamples = new List<long>();
        var cacheHits = 0;
        var cacheMisses = 0;
        var headerHits = 0;
        var indexedSearches = 0;

        for (var i = 0; i < iterations; i++)
        {
            var searchWatch = Stopwatch.StartNew();
            _ = store.SearchImagesIndexed(folder, searchQuery, favoritesOnly: false, limit: 200, out var usedIndex);
            searchWatch.Stop();
            searchSamples.Add(searchWatch.ElapsedTicks);
            if (usedIndex)
            {
                indexedSearches++;
            }

            var index = i % images.Count;
            var current = images[index];
            var previous = images[Math.Max(0, index - 1)];
            var next = images[Math.Min(images.Count - 1, index + 1)];

            var navWatch = Stopwatch.StartNew();
            if (ring.TryGet(current.AbsolutePath, out _))
            {
                cacheHits++;
            }
            else
            {
                cacheMisses++;
                scheduler.ScheduleAsync(
                    NativeCacheJobKind.PreviewDecode,
                    current.AbsolutePath,
                    _ => ring.Store(current.AbsolutePath, LoadImageCopy(current.AbsolutePath))).GetAwaiter().GetResult();
            }

            foreach (var neighbor in new[] { previous.AbsolutePath, next.AbsolutePath })
            {
                if (!ring.TryGet(neighbor, out _))
                {
                    scheduler.ScheduleAsync(
                        NativeCacheJobKind.NeighborDecode,
                        neighbor,
                        _ => ring.Store(neighbor, LoadImageCopy(neighbor))).GetAwaiter().GetResult();
                }
            }

            navWatch.Stop();
            navigationSamples.Add(navWatch.ElapsedTicks);

            if (current.Width is > 0 && current.Height is > 0)
            {
                headerHits++;
            }
        }

        scheduler.Dispose();
        ring.Dispose();

        var mutation = RunMutationProbe(projectRoot, folder, store, favorites);

        var searchP95 = Percentile(searchSamples, 0.95);
        var navigationP95 = Percentile(navigationSamples, 0.95);
        var cacheTotal = cacheHits + cacheMisses;
        var cacheHitRate = cacheTotal == 0 ? 0 : (double)cacheHits / cacheTotal;
        var headerCoverage = images.Count == 0 ? 0 : (double)images.Count(item => item.Width is > 0 && item.Height is > 0) / images.Count;

        Console.WriteLine(
            $"native-perf complete folder=\"{Path.GetFullPath(folder)}\" images={images.Count} iterations={iterations} incrementalAdded={incremental.AddedOrUpdated.Count} incrementalRemoved={incremental.RemovedPaths.Count} incrementalUnchanged={incremental.UnchangedCount} scanMs={(long)scanStopwatch.Elapsed.TotalMilliseconds}");
        Console.WriteLine(
            $"native-perf search query=\"{searchQuery}\" p50Ms={TicksToMs(Percentile(searchSamples, 0.50)):F2} p95Ms={TicksToMs(searchP95):F2} indexedSamples={indexedSearches}/{iterations}");
        Console.WriteLine(
            $"native-perf navigation p50Ms={TicksToMs(Percentile(navigationSamples, 0.50)):F2} p95Ms={TicksToMs(navigationP95):F2} cacheHitRate={cacheHitRate:P1} headerCoverage={headerCoverage:P1} headerHits={headerHits}");
        Console.WriteLine(
            $"native-perf mutation skipped={mutation.Skipped.ToString().ToLowerInvariant()} added={mutation.Added} updated={mutation.Updated} removed={mutation.Removed} watcherEvents={mutation.WatcherEvents}");
        return 0;
    }

    private static NativeMutationProbeResult RunMutationProbe(
        string projectRoot,
        string folder,
        NativeImageStore store,
        IReadOnlyDictionary<string, int> favorites)
    {
        var resolvedFolder = Path.GetFullPath(folder);
        var cacheRoot = Path.GetFullPath(Path.Combine(projectRoot, ".cache"));
        if (!resolvedFolder.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new NativeMutationProbeResult(Skipped: true, Added: 0, Updated: 0, Removed: 0, WatcherEvents: 0);
        }

        var probePath = Path.Combine(resolvedFolder, "m3-mutation-probe.png");
        var watcherEvents = 0;
        using var watcher = new NativeFolderWatcher();
        watcher.ChangesDetected += (_, _) => Interlocked.Increment(ref watcherEvents);
        watcher.Watch(resolvedFolder);

        try
        {
            WriteProbePng(probePath, Color.DarkCyan);
            WaitForWatcher();
            var add = ApplyIncremental(store, resolvedFolder, favorites);

            File.SetLastWriteTimeUtc(probePath, DateTime.UtcNow.AddSeconds(1));
            WriteProbePng(probePath, Color.DarkMagenta);
            WaitForWatcher();
            var update = ApplyIncremental(store, resolvedFolder, favorites);

            File.Delete(probePath);
            WaitForWatcher();
            var remove = ApplyIncremental(store, resolvedFolder, favorites);

            return new NativeMutationProbeResult(
                Skipped: false,
                Added: add.AddedOrUpdated.Count,
                Updated: update.AddedOrUpdated.Count,
                Removed: remove.RemovedPaths.Count,
                WatcherEvents: watcherEvents);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch
            {
                // Probe cleanup is best-effort.
            }
        }
    }

    private static NativeIncrementalScanResult ApplyIncremental(
        NativeImageStore store,
        string folder,
        IReadOnlyDictionary<string, int> favorites)
    {
        var existing = store.LoadImagesForRoot(folder).ToDictionary(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase);
        var stopwatch = Stopwatch.StartNew();
        var result = NativeIncrementalScanner.ScanAsync(folder, existing, favorites, progress: null, CancellationToken.None).GetAwaiter().GetResult();
        stopwatch.Stop();
        store.ApplyIncrementalScan(folder, result, stopwatch.Elapsed, fullRescan: false);
        return result;
    }

    private static void WriteProbePng(string path, Color color)
    {
        using var bitmap = new Bitmap(18, 18);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(color);
        }

        bitmap.Save(path, ImageFormat.Png);
    }

    private static void WaitForWatcher()
    {
        Thread.Sleep(500);
    }

    private static long Percentile(IReadOnlyList<long> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        var ordered = samples.OrderBy(static item => item).ToArray();
        var index = (int)Math.Clamp(Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static Image LoadImageCopy(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        using var source = Image.FromStream(memory);
        return new Bitmap(source);
    }

    private readonly record struct NativeMutationProbeResult(
        bool Skipped,
        int Added,
        int Updated,
        int Removed,
        int WatcherEvents);
}
