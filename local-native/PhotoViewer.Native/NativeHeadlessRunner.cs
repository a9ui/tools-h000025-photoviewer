using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;

namespace PhotoViewer.Native;

internal static class NativeHeadlessRunner
{
    public static int RunImport(string? browserStateExportPath = null)
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var store = new NativeImageStore(projectRoot);
        var import = store.ImportProjectState(browserStateExportPath);

        WriteImportWarnings(import.Warnings);
        Console.WriteLine(
            $"native-import complete favorites={import.FavoriteCount} albums={store.CountAlbums()} albumImages={store.CountAlbumImages()} browserStateKeys={store.CountBrowserStateKeys()} seenImages={import.SeenImageCount} settings={store.CountSettings()} images={import.ImageCount} warnings={import.WarningCount} recovery=\"{EscapeConsoleValue(import.RecoverySummary)}\" db=\"{store.DatabasePath}\"");
        return 0;
    }

    public static int RunMalformedImportSmoke()
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var runId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Environment.ProcessId}";
        var smokeRoot = Path.Combine(projectRoot, ".cache", "native-malformed-import-smoke", runId);
        var smokeCache = Path.Combine(smokeRoot, ".cache");
        var smokeNative = Path.Combine(smokeCache, "native");
        Directory.CreateDirectory(Path.Combine(smokeRoot, "src"));
        Directory.CreateDirectory(smokeNative);
        File.WriteAllText(Path.Combine(smokeRoot, "PROJECT.md"), "# Native malformed import smoke" + Environment.NewLine, Encoding.UTF8);

        var store = new NativeImageStore(smokeRoot);
        var validExportPath = Path.Combine(smokeNative, "valid-browser-localstorage-export.json");
        File.WriteAllText(
            validExportPath,
            "{\"localStorage\":{\"pvu_view\":\"grid\"}}" + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _ = store.ImportProjectState(validExportPath);
        var previousBrowserStateKeys = store.CountBrowserStateKeys();

        WriteMalformedJson(Path.Combine(smokeCache, "favorites.json"));
        WriteMalformedJson(Path.Combine(smokeCache, "albums.json"));
        WriteMalformedJson(Path.Combine(smokeCache, "settings.json"));
        var exportPath = Path.Combine(smokeNative, "malformed-browser-localstorage-export.json");
        WriteMalformedJson(exportPath);

        var import = store.ImportProjectState(exportPath);
        var warningSources = import.Warnings
            .Select(static warning => warning.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static source => source, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hasExpectedWarnings =
            warningSources.Contains("favorites", StringComparer.OrdinalIgnoreCase) &&
            warningSources.Contains("albums", StringComparer.OrdinalIgnoreCase) &&
            warningSources.Contains("settings", StringComparer.OrdinalIgnoreCase) &&
            warningSources.Contains("browser-state-export", StringComparer.OrdinalIgnoreCase);
        var recoveryStored = !string.IsNullOrWhiteSpace(store.GetSetting("import_recovery_summary", ""));
        var warningsStored = store.GetSetting("import_warning_count", "0") == import.WarningCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var settingsFallback = store.GetSetting("browser_settings_found", "0") == "1" &&
            store.GetSetting("browser_settings_imported", "1") == "0";
        var browserExportSkipped = store.GetSetting("browser_state_export_found", "0") == "1" &&
            store.GetSetting("browser_state_export_imported", "1") == "0";
        var browserStatePreserved = previousBrowserStateKeys > 0 &&
            store.CountBrowserStateKeys() == previousBrowserStateKeys;
        var safeEmptyFallback = import.FavoriteCount == 0 &&
            store.CountAlbums() == 0 &&
            store.CountAlbumImages() == 0;
        var passed = hasExpectedWarnings && recoveryStored && warningsStored && settingsFallback && browserExportSkipped && browserStatePreserved && safeEmptyFallback;

        WriteImportWarnings(import.Warnings);
        Console.WriteLine(
            $"native-malformed-import-smoke complete warnings={import.WarningCount} sources=\"{string.Join(",", warningSources)}\" recoveryStored={BoolText(recoveryStored)} warningsStored={BoolText(warningsStored)} settingsFallback={BoolText(settingsFallback)} browserExportSkipped={BoolText(browserExportSkipped)} browserStatePreserved={BoolText(browserStatePreserved)} safeEmptyFallback={BoolText(safeEmptyFallback)} smokeRoot=\"{smokeRoot}\" browserRuntime=false localHttpServer=false nodeRuntime=false");
        return passed ? 0 : 2;
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

    public static int RunSeenStateSmoke(string? folder)
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var smokeFolder = ResolveSeenSmokeFolder(projectRoot, folder);
        var beforeEnhancementState = EnhancementStateFingerprint(projectRoot);
        var importedSeenPath = Path.Combine(smokeFolder, "m14-seen-imported.png");
        var nativeSeenPath = Path.Combine(smokeFolder, "m14-seen-native.png");

        Directory.CreateDirectory(smokeFolder);
        WriteSmokePng(importedSeenPath, Color.DeepSkyBlue);
        WriteSmokePng(nativeSeenPath, Color.DarkOrange);

        var store = new NativeImageStore(projectRoot);
        store.Initialize();
        var favorites = store.LoadFavorites();
        var images = NativeImageScanner.ScanAsync(smokeFolder, favorites, progress: null, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        store.SaveScanResult(smokeFolder, images, TimeSpan.Zero);

        var exportPath = WriteSeenSmokeBrowserExport(projectRoot, importedSeenPath);
        var import = store.ImportProjectState(exportPath);
        var afterImport = store.LoadImagesForRoot(smokeFolder);
        var importedSeen = IsSeen(afterImport, importedSeenPath);
        var nativeInitiallySeen = IsSeen(afterImport, nativeSeenPath);

        store.MarkImageSeen(nativeSeenPath, "native_smoke");
        var afterNativeMark = store.LoadImagesForRoot(smokeFolder);
        var nativeSeenPersisted = IsSeen(afterNativeMark, nativeSeenPath);
        var importedStillSeen = IsSeen(afterNativeMark, importedSeenPath);
        var smokeSeenCount = afterNativeMark.Count(static item => item.IsSeen);
        var afterEnhancementState = EnhancementStateFingerprint(projectRoot);

        if (!importedSeen || nativeInitiallySeen || !nativeSeenPersisted || !importedStillSeen)
        {
            Console.Error.WriteLine(
                $"native-seen-smoke error importedSeen={BoolText(importedSeen)} nativeInitiallyUnseen={BoolText(!nativeInitiallySeen)} nativeSeenPersisted={BoolText(nativeSeenPersisted)} importedStillSeen={BoolText(importedStillSeen)}");
            return 2;
        }

        Console.WriteLine(
            $"native-seen-smoke complete folder=\"{smokeFolder}\" images={afterNativeMark.Count} browserStateKeys={import.BrowserStateKeyCount} importedSeen={BoolText(importedSeen)} nativeInitiallyUnseen={BoolText(!nativeInitiallySeen)} nativeSeenPersisted={BoolText(nativeSeenPersisted)} importedStillSeen={BoolText(importedStillSeen)} seenSmokeImages={smokeSeenCount} totalSeenImages={store.CountSeenImages()} enhancementStateUnchanged={BoolText(beforeEnhancementState == afterEnhancementState)} browserRuntime=false localHttpServer=false nodeRuntime=false");
        return beforeEnhancementState == afterEnhancementState ? 0 : 2;
    }

    public static int RunPerformance(string folder, int iterations, string searchQuery)
    {
        return NativeHeadlessPerformance.Run(folder, iterations, searchQuery);
    }

    public static int RunCacheCompatibility(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"native-cache-compat error=folder-not-found folder=\"{folder}\"");
            return 2;
        }

        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var store = new NativeImageStore(projectRoot);
        store.Initialize();
        if (store.LoadImagesForRoot(folder).Count == 0)
        {
            var favorites = store.LoadFavorites();
            var images = NativeImageScanner.ScanAsync(folder, favorites, progress: null, CancellationToken.None).GetAwaiter().GetResult();
            store.SaveScanResult(folder, images, TimeSpan.Zero);
        }

        var report = store.CheckCacheCompatibility(folder);
        Console.WriteLine(
            $"native-cache-compat complete folder=\"{Path.GetFullPath(folder)}\" images={report.ImagesChecked} thumbCompatible={report.ThumbnailCompatible} thumbMissing={report.ThumbnailMissing} thumbIncompatible={report.ThumbnailIncompatible} displayCompatible={report.DisplayCompatible} displayMissing={report.DisplayMissing} displayIncompatible={report.DisplayIncompatible}");
        return 0;
    }

    private static string ResolveSeenSmokeFolder(string projectRoot, string? folder)
    {
        if (!string.IsNullOrWhiteSpace(folder))
        {
            return Path.GetFullPath(folder);
        }

        var runId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Environment.ProcessId}";
        return Path.Combine(projectRoot, ".cache", "native-seen-smoke", runId);
    }

    private static string WriteSeenSmokeBrowserExport(string projectRoot, string importedSeenPath)
    {
        var exportPath = Path.Combine(projectRoot, ".cache", "native", "seen-smoke-browser-localstorage-export.json");
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
        var payload = new
        {
            localStorage = new Dictionary<string, object>
            {
                ["pvu_seen_images"] = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    [Path.GetFullPath(importedSeenPath)] = true,
                },
            },
        };
        File.WriteAllText(
            exportPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return exportPath;
    }

    private static void WriteSmokePng(string path, Color color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new Bitmap(28, 28);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        bitmap.Save(path, ImageFormat.Png);
        var timestamp = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);
        try
        {
            File.SetCreationTimeUtc(path, timestamp);
            File.SetLastWriteTimeUtc(path, timestamp);
            File.SetLastAccessTimeUtc(path, timestamp);
        }
        catch
        {
            // Timestamp normalization is best-effort for smoke fixtures.
        }
    }

    private static void WriteMalformedJson(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ malformed", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static bool IsSeen(IEnumerable<NativeImageRecord> images, string path)
    {
        var normalized = Path.GetFullPath(path);
        return images.Any(item => string.Equals(item.AbsolutePath, normalized, StringComparison.OrdinalIgnoreCase) && item.IsSeen);
    }

    private static string EnhancementStateFingerprint(string projectRoot)
    {
        var path = Path.Combine(projectRoot, ".cache", "enhance", "jobs.json");
        if (!File.Exists(path))
        {
            return "missing";
        }

        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private static string BoolText(bool value)
    {
        return value.ToString().ToLowerInvariant();
    }

    private static void WriteImportWarnings(IEnumerable<NativeImportWarning> warnings)
    {
        foreach (var warning in warnings)
        {
            Console.WriteLine(
                $"native-import warning source={warning.Source} code={warning.Code} path=\"{EscapeConsoleValue(warning.Path)}\" recovery=\"{EscapeConsoleValue(warning.RecoveryAction)}\"");
        }
    }

    private static string EscapeConsoleValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
