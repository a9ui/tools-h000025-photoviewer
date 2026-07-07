using System.Diagnostics;

namespace PhotoViewer.Native;

internal static class NativeHeadlessRunner
{
    public static int RunImport()
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var store = new NativeImageStore(projectRoot);
        var import = store.ImportProjectState();

        Console.WriteLine(
            $"native-import complete favorites={import.FavoriteCount} albums={store.CountAlbums()} settings={store.CountSettings()} images={import.ImageCount} db=\"{store.DatabasePath}\"");
        return 0;
    }

    public static async Task<int> RunScanAsync(string folder, CancellationToken cancellationToken, bool incremental = false)
    {
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"native-scan error=folder-not-found folder=\"{folder}\"");
            return 2;
        }

        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var store = new NativeImageStore(projectRoot);
        var import = store.ImportProjectState();
        var favorites = store.LoadFavorites();
        var existing = store.LoadImagesForRoot(folder).ToDictionary(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase);

        var stopwatch = Stopwatch.StartNew();
        var progress = new Progress<NativeScanProgress>(item =>
        {
            Console.WriteLine($"native-scan progress count={item.Count} folder=\"{item.CurrentFolder}\"");
        });

        if (incremental && existing.Count > 0)
        {
            var incrementalResult = await NativeIncrementalScanner.ScanAsync(folder, existing, favorites, progress, cancellationToken);
            stopwatch.Stop();
            store.ApplyIncrementalScan(folder, incrementalResult, stopwatch.Elapsed, fullRescan: false);
            var storedImages = store.LoadImagesForRoot(folder);
            var storedFavorites = storedImages.Count(static item => item.FavoriteLevel > 0);
            Console.WriteLine(
                $"native-incremental-scan complete images={storedImages.Count} addedOrUpdated={incrementalResult.AddedOrUpdated.Count} removed={incrementalResult.RemovedPaths.Count} unchanged={incrementalResult.UnchangedCount} favorites={storedFavorites} importedFavorites={import.FavoriteCount} elapsedMs={(long)stopwatch.Elapsed.TotalMilliseconds} db=\"{store.DatabasePath}\"");
            return 0;
        }

        var images = await NativeImageScanner.ScanAsync(folder, favorites, progress, cancellationToken);
        stopwatch.Stop();
        store.SaveScanResult(folder, images, stopwatch.Elapsed);
        var stored = store.LoadImagesForRoot(folder);
        var favCount = stored.Count(static item => item.FavoriteLevel > 0);

        Console.WriteLine(
            $"native-scan complete images={stored.Count} favorites={favCount} importedFavorites={import.FavoriteCount} elapsedMs={(long)stopwatch.Elapsed.TotalMilliseconds} db=\"{store.DatabasePath}\"");
        return 0;
    }

    public static int RunSearch(string folder, string query, bool favoritesOnly)
    {
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"native-search error=folder-not-found folder=\"{folder}\"");
            return 2;
        }

        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var store = new NativeImageStore(projectRoot);
        store.Initialize();
        var matches = store.SearchImagesIndexed(folder, query, favoritesOnly, limit: 25, out var usedIndex);
        var first = matches.Count > 0 ? matches[0].Filename : "";
        Console.WriteLine(
            $"native-search complete query=\"{query}\" favoritesOnly={favoritesOnly.ToString().ToLowerInvariant()} matches={matches.Count} first=\"{first}\" indexed={usedIndex.ToString().ToLowerInvariant()}");
        return 0;
    }

    public static int RunFavorite(string filePath, int level)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.Error.WriteLine("native-favorite error=missing-path");
            return 2;
        }

        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var store = new NativeImageStore(projectRoot);
        store.SetFavoriteLevel(filePath, level);
        var storedLevel = store.LoadFavorites().TryGetValue(Path.GetFullPath(filePath), out var value) ? value : 0;
        Console.WriteLine($"native-favorite complete path=\"{Path.GetFullPath(filePath)}\" level={storedLevel}");
        return 0;
    }

    public static int RunPerformance(string folder, int iterations, string searchQuery)
    {
        return NativeHeadlessPerformance.Run(folder, iterations, searchQuery);
    }
}
