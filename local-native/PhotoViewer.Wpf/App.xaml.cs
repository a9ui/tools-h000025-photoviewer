using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoViewer.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var startupWatch = Stopwatch.StartNew();
        base.OnStartup(e);

        int startupSmokeIdx = Array.IndexOf(e.Args, "--startup-smoke");
        if (startupSmokeIdx >= 0 && startupSmokeIdx + 1 < e.Args.Length)
        {
            CaptureStartupSmoke(e.Args[startupSmokeIdx + 1], e.Args, startupWatch);
            return;
        }

        int shotIdx = Array.IndexOf(e.Args, "--shot");
        if (shotIdx >= 0 && shotIdx + 1 < e.Args.Length)
        {
            CaptureShot(e.Args[shotIdx + 1]);
            return;
        }

        int modalNavSmokeIdx = Array.IndexOf(e.Args, "--modal-nav-smoke");
        if (modalNavSmokeIdx >= 0 && modalNavSmokeIdx + 1 < e.Args.Length)
        {
            CaptureModalNavigationSmoke(e.Args[modalNavSmokeIdx + 1], e.Args);
            return;
        }

        int gridRealizationSmokeIdx = Array.IndexOf(e.Args, "--grid-realization-smoke");
        if (gridRealizationSmokeIdx >= 0 && gridRealizationSmokeIdx + 1 < e.Args.Length)
        {
            CaptureGridRealizationSmoke(e.Args[gridRealizationSmokeIdx + 1], e.Args);
            return;
        }

        int scrollRealizationSmokeIdx = Array.IndexOf(e.Args, "--scroll-realization-smoke");
        if (scrollRealizationSmokeIdx >= 0 && scrollRealizationSmokeIdx + 1 < e.Args.Length)
        {
            CaptureScrollRealizationSmoke(e.Args[scrollRealizationSmokeIdx + 1], e.Args);
            return;
        }

        int favoriteSmokeIdx = Array.IndexOf(e.Args, "--favorite-smoke");
        if (favoriteSmokeIdx >= 0 && favoriteSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFavoriteSmoke(e.Args[favoriteSmokeIdx + 1], e.Args);
            return;
        }

        int favoriteLevelSmokeIdx = Array.IndexOf(e.Args, "--favorite-level-smoke");
        if (favoriteLevelSmokeIdx >= 0 && favoriteLevelSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFavoriteLevelSmoke(e.Args[favoriteLevelSmokeIdx + 1], e.Args);
            return;
        }

        int favoriteImportSmokeIdx = Array.IndexOf(e.Args, "--favorite-import-smoke");
        if (favoriteImportSmokeIdx >= 0 && favoriteImportSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFavoriteImportSmoke(e.Args[favoriteImportSmokeIdx + 1], e.Args);
            return;
        }

        int seenSmokeIdx = Array.IndexOf(e.Args, "--seen-smoke");
        if (seenSmokeIdx >= 0 && seenSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSeenSmoke(e.Args[seenSmokeIdx + 1], e.Args);
            return;
        }

        int seenImportSmokeIdx = Array.IndexOf(e.Args, "--seen-import-smoke");
        if (seenImportSmokeIdx >= 0 && seenImportSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSeenImportSmoke(e.Args[seenImportSmokeIdx + 1], e.Args);
            return;
        }

        int sharedSeenSmokeIdx = Array.IndexOf(e.Args, "--shared-seen-smoke");
        if (sharedSeenSmokeIdx >= 0 && sharedSeenSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSharedSeenSmoke(e.Args[sharedSeenSmokeIdx + 1], e.Args);
            return;
        }

        int sharedRecentSmokeIdx = Array.IndexOf(e.Args, "--shared-recent-smoke");
        if (sharedRecentSmokeIdx >= 0 && sharedRecentSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSharedRecentSmoke(e.Args[sharedRecentSmokeIdx + 1], e.Args);
            return;
        }

        int folderSetSmokeIdx = Array.IndexOf(e.Args, "--folder-set-smoke");
        if (folderSetSmokeIdx >= 0 && folderSetSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFolderSetSmoke(e.Args[folderSetSmokeIdx + 1], e.Args);
            return;
        }

        int gridZoomSmokeIdx = Array.IndexOf(e.Args, "--grid-zoom-smoke");
        if (gridZoomSmokeIdx >= 0 && gridZoomSmokeIdx + 1 < e.Args.Length)
        {
            CaptureGridZoomSmoke(e.Args[gridZoomSmokeIdx + 1], e.Args);
            return;
        }

        int displayStyleSmokeIdx = Array.IndexOf(e.Args, "--display-style-smoke");
        if (displayStyleSmokeIdx >= 0 && displayStyleSmokeIdx + 1 < e.Args.Length)
        {
            CaptureDisplayStyleSmoke(e.Args[displayStyleSmokeIdx + 1], e.Args);
            return;
        }

        int sortSmokeIdx = Array.IndexOf(e.Args, "--sort-smoke");
        if (sortSmokeIdx >= 0 && sortSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSortSmoke(e.Args[sortSmokeIdx + 1], e.Args);
            return;
        }

        int enhancedFilterSmokeIdx = Array.IndexOf(e.Args, "--enhanced-filter-smoke");
        if (enhancedFilterSmokeIdx >= 0 && enhancedFilterSmokeIdx + 1 < e.Args.Length)
        {
            CaptureEnhancedFilterSmoke(e.Args[enhancedFilterSmokeIdx + 1], e.Args);
            return;
        }

        int stateSmokeIdx = Array.IndexOf(e.Args, "--state-smoke");
        if (stateSmokeIdx >= 0 && stateSmokeIdx + 1 < e.Args.Length)
        {
            CaptureStateSmoke(e.Args[stateSmokeIdx + 1], e.Args);
            return;
        }

        new MainWindow().Show();
    }

    /// <summary>Open the shell to dispatcher-idle readiness, write timing evidence, and exit.</summary>
    private void CaptureStartupSmoke(string path, string[] args, Stopwatch startupWatch)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        string resultFullPath = Path.GetFullPath(path);
        string mode = ArgValue(args, "--startup-mode") ?? "unspecified";

        long beforeWindowMs = startupWatch.ElapsedMilliseconds;
        var constructWatch = Stopwatch.StartNew();
        var win = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            Width = 1280,
            Height = 820,
        };
        win.SuppressStatePersistence();
        constructWatch.Stop();

        var showWatch = Stopwatch.StartNew();
        win.Show();
        showWatch.Stop();

        win.Dispatcher.InvokeAsync(() =>
        {
            startupWatch.Stop();
            var result = new StartupSmokeResult(
                Ok: true,
                Message: "startup shell reached dispatcher idle",
                Mode: mode,
                BeforeWindowMs: beforeWindowMs,
                WindowConstructMs: constructWatch.ElapsedMilliseconds,
                ShowMs: showWatch.ElapsedMilliseconds,
                ReadyMs: startupWatch.ElapsedMilliseconds,
                CompletedAtUtc: DateTime.UtcNow);
            WriteStartupSmokeResult(resultFullPath, result);
            Shutdown(0);
        }, DispatcherPriority.ContextIdle);
    }

    /// <summary>Render the main window offscreen to a PNG and exit (UI smoke evidence).</summary>
    private void CaptureShot(string path)
    {
        var win = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            Width = 1280,
            Height = 820,
        };
        win.Show();
        win.SuppressStatePersistence();

        var args = Environment.GetCommandLineArgs();
        int sIdx = Array.IndexOf(args, "--screen");
        string screen = sIdx >= 0 && sIdx + 1 < args.Length ? args[sIdx + 1]
            : args.Contains("--modal") ? "modal" : "viewer";

        win.Dispatcher.InvokeAsync(async () =>
        {
            int folderIdx = Array.IndexOf(args, "--folder");
            if (folderIdx >= 0 && folderIdx + 1 < args.Length)
                await win.LoadFolderAsync(args[folderIdx + 1]);

            int queryIdx = Array.IndexOf(args, "--query");
            if (queryIdx >= 0 && queryIdx + 1 < args.Length)
                win.SetSearchQuery(args[queryIdx + 1], persist: false);
            else
                win.SetSearchQuery("", persist: false);

            win.ShowScreen(screen);
            win.UpdateLayout();
            int perfIdx = Array.IndexOf(args, "--perf-log");
            if (perfIdx >= 0 && perfIdx + 1 < args.Length && win.LastLoadMetrics is not null)
            {
                string perfPath = args[perfIdx + 1];
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(perfPath))!);
                var json = System.Text.Json.JsonSerializer.Serialize(win.LastLoadMetrics, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(perfPath, json);
            }

            var root = (FrameworkElement)win.Content;
            int w = (int)Math.Ceiling(root.ActualWidth);
            int h = (int)Math.Ceiling(root.ActualHeight);
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using (var fs = File.Create(path))
                encoder.Save(fs);

            win.Close();
            Shutdown();
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureFavoriteSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? favoritesPath = ArgValue(args, "--favorites-path");
        int selectIndex = ArgInt(args, "--select-index", 0);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(favoritesPath))
        {
            WriteFavoriteSmokeResult(
                resultPath,
                new FavoriteSmokeResult(false, "missing required --folder or --favorites-path", folder, favoritesPath, selectIndex, false, null, null, 0, false, 0, 0, 0, 0, false, 0, 0));
            Shutdown(1);
            return;
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();
        first.SuppressStatePersistence();

        first.Dispatcher.InvokeAsync(async () =>
        {
            FavoriteSmokeResult result;
            try
            {
                await first.LoadFolderAsync(folder);
                bool selected = first.SelectIndexForSmoke(selectIndex);
                string? selectedName = first.SelectedFileNameForSmoke;
                string? selectedPath = first.SelectedPathForSmoke;
                int beforeLevel = first.SelectedFavoriteLevelForSmoke;
                bool toggled = first.ToggleSelectedFavoriteForSmoke();
                int afterLevel = first.SelectedFavoriteLevelForSmoke;

                first.SetFavoriteOnlyFilterForSmoke(true);
                int filteredAfterToggle = first.FilteredCountForSmoke;
                int storeCountAfterToggle = first.FavoriteStoreCountForSmoke;
                first.Close();

                int persistedLevel = selectedPath is null ? 0 : ReadFavoriteLevel(favoritesPath, selectedPath);

                var second = HiddenWindow();
                second.Show();
                second.SuppressStatePersistence();
                await second.LoadFolderAsync(folder);
                bool reloadSelected = !string.IsNullOrWhiteSpace(selectedName) && second.SelectFileNameForSmoke(selectedName);
                int reloadedLevel = second.SelectedFavoriteLevelForSmoke;
                second.SetFavoriteOnlyFilterForSmoke(true);
                int reloadedFilteredCount = second.FilteredCountForSmoke;
                second.Close();

                bool ok = selected
                    && beforeLevel == 0
                    && toggled
                    && afterLevel == 5
                    && filteredAfterToggle == 1
                    && storeCountAfterToggle == 1
                    && persistedLevel == 5
                    && reloadSelected
                    && reloadedLevel == 5
                    && reloadedFilteredCount == 1;

                result = new FavoriteSmokeResult(
                    ok,
                    ok ? "favorite toggle, favorites-only filter, and reload persistence passed" : "favorite workflow did not toggle, filter, or reload as expected",
                    folder,
                    favoritesPath,
                    selectIndex,
                    selected,
                    selectedName,
                    selectedPath,
                    beforeLevel,
                    toggled,
                    afterLevel,
                    filteredAfterToggle,
                    storeCountAfterToggle,
                    persistedLevel,
                    reloadSelected,
                    reloadedLevel,
                    reloadedFilteredCount);
            }
            catch (Exception ex)
            {
                first.Close();
                result = new FavoriteSmokeResult(false, ex.Message, folder, favoritesPath, selectIndex, false, null, null, 0, false, 0, 0, 0, 0, false, 0, 0);
            }

            WriteFavoriteSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureFavoriteLevelSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? favoritesPath = ArgValue(args, "--favorites-path");
        int selectIndex = ArgInt(args, "--select-index", 0);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(favoritesPath))
        {
            WriteFavoriteLevelSmokeResult(
                resultPath,
                new FavoriteLevelSmokeResult(
                    false,
                    "missing required --folder or --favorites-path",
                    folder,
                    favoritesPath,
                    selectIndex,
                    false,
                    null,
                    null,
                    0,
                    false,
                    0,
                    false,
                    0,
                    false,
                    0,
                    false,
                    0,
                    0,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    0,
                    0));
            Shutdown(1);
            return;
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();
        first.SuppressStatePersistence();

        first.Dispatcher.InvokeAsync(async () =>
        {
            FavoriteLevelSmokeResult result;
            try
            {
                await first.LoadFolderAsync(folder);
                bool selected = first.SelectIndexForSmoke(selectIndex);
                string? selectedName = first.SelectedFileNameForSmoke;
                string? selectedPath = first.SelectedPathForSmoke;
                int beforeLevel = first.SelectedFavoriteLevelForSmoke;

                bool increasedOnce = first.AdjustSelectedFavoriteForSmoke(1);
                int afterIncreaseOnce = first.SelectedFavoriteLevelForSmoke;
                bool increasedTwice = first.AdjustSelectedFavoriteForSmoke(1);
                int afterIncreaseTwice = first.SelectedFavoriteLevelForSmoke;
                bool decreasedOnce = first.AdjustSelectedFavoriteForSmoke(-1);
                int afterDecreaseOnce = first.SelectedFavoriteLevelForSmoke;
                bool cleared = first.SetSelectedFavoriteLevelForSmoke(0);
                int afterClear = first.SelectedFavoriteLevelForSmoke;
                int storeCountAfterClear = first.FavoriteStoreCountForSmoke;

                bool adjustedToFinalLevel = true;
                int increaseStepsAfterClear = 0;
                for (int i = 0; i < 4; i++)
                {
                    adjustedToFinalLevel &= first.AdjustSelectedFavoriteForSmoke(1);
                    increaseStepsAfterClear++;
                }

                int finalLevel = first.SelectedFavoriteLevelForSmoke;
                first.SetFavoriteOnlyFilterForSmoke(true);
                int filteredAfterFinal = first.FilteredCountForSmoke;
                int storeCountAfterFinal = first.FavoriteStoreCountForSmoke;
                first.Close();

                int persistedLevel = selectedPath is null ? 0 : ReadFavoriteLevel(favoritesPath, selectedPath);

                var second = HiddenWindow();
                second.Show();
                second.SuppressStatePersistence();
                await second.LoadFolderAsync(folder);
                bool reloadSelected = !string.IsNullOrWhiteSpace(selectedName) && second.SelectFileNameForSmoke(selectedName);
                int reloadedLevel = second.SelectedFavoriteLevelForSmoke;
                second.SetFavoriteOnlyFilterForSmoke(true);
                int reloadedFilteredCount = second.FilteredCountForSmoke;
                second.Close();

                bool ok = selected
                    && beforeLevel == 0
                    && increasedOnce
                    && afterIncreaseOnce == 1
                    && increasedTwice
                    && afterIncreaseTwice == 2
                    && decreasedOnce
                    && afterDecreaseOnce == 1
                    && cleared
                    && afterClear == 0
                    && storeCountAfterClear == 0
                    && adjustedToFinalLevel
                    && increaseStepsAfterClear == 4
                    && finalLevel == 4
                    && filteredAfterFinal == 1
                    && storeCountAfterFinal == 1
                    && persistedLevel == 4
                    && reloadSelected
                    && reloadedLevel == 4
                    && reloadedFilteredCount == 1;

                result = new FavoriteLevelSmokeResult(
                    ok,
                    ok ? "favorite level adjustment, clear, filter, and reload persistence passed" : "favorite level adjustment did not match expected 0..5 behavior",
                    folder,
                    favoritesPath,
                    selectIndex,
                    selected,
                    selectedName,
                    selectedPath,
                    beforeLevel,
                    increasedOnce,
                    afterIncreaseOnce,
                    increasedTwice,
                    afterIncreaseTwice,
                    decreasedOnce,
                    afterDecreaseOnce,
                    cleared,
                    afterClear,
                    storeCountAfterClear,
                    adjustedToFinalLevel,
                    increaseStepsAfterClear,
                    finalLevel,
                    filteredAfterFinal,
                    storeCountAfterFinal,
                    persistedLevel,
                    reloadSelected,
                    reloadedLevel,
                    reloadedFilteredCount);
            }
            catch (Exception ex)
            {
                first.Close();
                result = new FavoriteLevelSmokeResult(
                    false,
                    ex.Message,
                    folder,
                    favoritesPath,
                    selectIndex,
                    false,
                    null,
                    null,
                    0,
                    false,
                    0,
                    false,
                    0,
                    false,
                    0,
                    false,
                    0,
                    0,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    0,
                    0);
            }

            WriteFavoriteLevelSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureFavoriteImportSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? favoritesPath = ArgValue(args, "--favorites-path");
        string? browserStatePath = ArgValue(args, "--browser-state-path");
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(favoritesPath) || string.IsNullOrWhiteSpace(browserStatePath))
        {
            WriteFavoriteImportSmokeResult(
                resultPath,
                new FavoriteImportSmokeResult(
                    false,
                    "missing required --folder, --favorites-path, or --browser-state-path",
                    folder,
                    favoritesPath,
                    browserStatePath,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    new Dictionary<string, int>(),
                    new Dictionary<string, int>()));
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string importedPath = Path.Combine(fullFolder, "wpf-preview.png");
        string preservedPath = Path.Combine(fullFolder, "wpf-settings.png");
        string clampedName = "wpf-list.png";
        string clampedPath = Path.Combine(fullFolder, clampedName);
        string zeroPath = Path.Combine(fullFolder, "wpf-modal-preview.png");
        string unmatchedPath = Path.Combine(fullFolder, "missing-pvu-fav-level.png");
        string pvuFavoriteDefaultName = "wpf-enhance.png";
        string pvuFavoriteDefaultPath = Path.Combine(fullFolder, pvuFavoriteDefaultName);
        string pvuFavoriteExplicitName = "wpf-confirm.png";
        string pvuFavoriteExplicitPath = Path.Combine(fullFolder, pvuFavoriteExplicitName);
        string pvuFavoriteUnmatchedPath = Path.Combine(fullFolder, "missing-pvu-favorite.png");
        string pvuFavoriteListName = "wpf-album.png";
        string pvuFavoriteListPath = Path.Combine(fullFolder, pvuFavoriteListName);
        string pvuFavoritesListStatePath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(browserStatePath)) ?? Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(browserStatePath) + "-list.json");
        bool fixtureFilesExist = File.Exists(importedPath) &&
            File.Exists(preservedPath) &&
            File.Exists(clampedPath) &&
            File.Exists(zeroPath) &&
            File.Exists(pvuFavoriteDefaultPath) &&
            File.Exists(pvuFavoriteExplicitPath) &&
            File.Exists(pvuFavoriteListPath);

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (fixtureFilesExist)
        {
            WriteFavoriteSeed(favoritesPath, preservedPath, 2);
            WritePvuFavoriteImportFixture(
                browserStatePath,
                importedPath,
                preservedPath,
                clampedName,
                zeroPath,
                unmatchedPath,
                pvuFavoriteDefaultPath,
                pvuFavoriteExplicitName,
                pvuFavoriteUnmatchedPath);
            WritePvuFavoritesListImportFixture(
                pvuFavoritesListStatePath,
                pvuFavoriteListPath,
                pvuFavoriteDefaultPath,
                pvuFavoriteUnmatchedPath);
        }

        var first = HiddenWindow();
        first.Show();
        first.SuppressStatePersistence();

        first.Dispatcher.InvokeAsync(async () =>
        {
            FavoriteImportSmokeResult result;
            try
            {
                await first.LoadFolderAsync(folder);
                first.SetSearchQuery("", persist: false);

                FavoriteImportSummary? importSummary = fixtureFilesExist
                    ? first.ImportPvuFavoriteLevelsForSmoke(browserStatePath)
                    : FavoriteImportSummary.Failed(browserStatePath, "fixture files missing");
                FavoriteImportSummary? pvuFavoritesImportSummary = fixtureFilesExist
                    ? first.ImportPvuFavoritesForSmoke(browserStatePath)
                    : FavoriteImportSummary.Failed(browserStatePath, "fixture files missing");
                FavoriteImportSummary? pvuFavoritesListImportSummary = fixtureFilesExist
                    ? first.ImportPvuFavoritesForSmoke(pvuFavoritesListStatePath)
                    : FavoriteImportSummary.Failed(pvuFavoritesListStatePath, "fixture files missing");

                int importedLevel = SelectFavoriteLevel(first, "wpf-preview.png");
                int preservedLevel = SelectFavoriteLevel(first, "wpf-settings.png");
                int clampedLevel = SelectFavoriteLevel(first, clampedName);
                int zeroLevel = SelectFavoriteLevel(first, "wpf-modal-preview.png");
                int pvuFavoriteDefaultLevel = SelectFavoriteLevel(first, pvuFavoriteDefaultName);
                int pvuFavoriteExplicitLevel = SelectFavoriteLevel(first, pvuFavoriteExplicitName);
                int pvuFavoriteListLevel = SelectFavoriteLevel(first, pvuFavoriteListName);
                first.SetFavoriteOnlyFilterForSmoke(true);
                int filteredAfterImport = first.FilteredCountForSmoke;
                int storeCountAfterImport = first.FavoriteStoreCountForSmoke;
                first.Close();

                var persistedLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["wpf-preview.png"] = ReadFavoriteLevel(favoritesPath, importedPath),
                    ["wpf-settings.png"] = ReadFavoriteLevel(favoritesPath, preservedPath),
                    ["wpf-list.png"] = ReadFavoriteLevel(favoritesPath, clampedPath),
                    ["wpf-modal-preview.png"] = ReadFavoriteLevel(favoritesPath, zeroPath),
                    [pvuFavoriteDefaultName] = ReadFavoriteLevel(favoritesPath, pvuFavoriteDefaultPath),
                    [pvuFavoriteExplicitName] = ReadFavoriteLevel(favoritesPath, pvuFavoriteExplicitPath),
                    [pvuFavoriteListName] = ReadFavoriteLevel(favoritesPath, pvuFavoriteListPath),
                };

                var second = HiddenWindow();
                second.Show();
                second.SuppressStatePersistence();
                await second.LoadFolderAsync(folder);
                second.SetSearchQuery("", persist: false);
                bool reloadSelected = second.SelectFileNameForSmoke("wpf-preview.png");
                int reloadedImportedLevel = second.SelectedFavoriteLevelForSmoke;
                int reloadedPreservedLevel = SelectFavoriteLevel(second, "wpf-settings.png");
                int reloadedClampedLevel = SelectFavoriteLevel(second, clampedName);
                int reloadedZeroLevel = SelectFavoriteLevel(second, "wpf-modal-preview.png");
                int reloadedPvuFavoriteDefaultLevel = SelectFavoriteLevel(second, pvuFavoriteDefaultName);
                int reloadedPvuFavoriteExplicitLevel = SelectFavoriteLevel(second, pvuFavoriteExplicitName);
                int reloadedPvuFavoriteListLevel = SelectFavoriteLevel(second, pvuFavoriteListName);
                second.SetFavoriteOnlyFilterForSmoke(true);
                int reloadedFilteredCount = second.FilteredCountForSmoke;
                second.Close();

                var reloadedLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["wpf-preview.png"] = reloadedImportedLevel,
                    ["wpf-settings.png"] = reloadedPreservedLevel,
                    ["wpf-list.png"] = reloadedClampedLevel,
                    ["wpf-modal-preview.png"] = reloadedZeroLevel,
                    [pvuFavoriteDefaultName] = reloadedPvuFavoriteDefaultLevel,
                    [pvuFavoriteExplicitName] = reloadedPvuFavoriteExplicitLevel,
                    [pvuFavoriteListName] = reloadedPvuFavoriteListLevel,
                };

                bool ok = fixtureFilesExist
                    && importSummary.Ok
                    && importSummary.SourceShape == "browserLocalStorage.pvu_fav_levels"
                    && importSummary.TotalEntries == 7
                    && importSummary.ImportedCount == 2
                    && importSummary.PreservedCount == 1
                    && importSummary.IgnoredZeroCount == 1
                    && importSummary.IgnoredInvalidCount == 1
                    && importSummary.MissingCount == 1
                    && importSummary.UnmatchedCount == 1
                    && pvuFavoritesImportSummary.Ok
                    && pvuFavoritesImportSummary.SourceShape == "browserLocalStorage.pvu_favorites"
                    && pvuFavoritesImportSummary.TotalEntries == 8
                    && pvuFavoritesImportSummary.ImportedCount == 2
                    && pvuFavoritesImportSummary.PreservedCount == 2
                    && pvuFavoritesImportSummary.IgnoredZeroCount == 1
                    && pvuFavoritesImportSummary.IgnoredInvalidCount == 1
                    && pvuFavoritesImportSummary.MissingCount == 1
                    && pvuFavoritesImportSummary.UnmatchedCount == 1
                    && pvuFavoritesListImportSummary.Ok
                    && pvuFavoritesListImportSummary.SourceShape == "browserLocalStorage.pvu_favorites"
                    && pvuFavoritesListImportSummary.TotalEntries == 5
                    && pvuFavoritesListImportSummary.ImportedCount == 1
                    && pvuFavoritesListImportSummary.PreservedCount == 1
                    && pvuFavoritesListImportSummary.IgnoredZeroCount == 0
                    && pvuFavoritesListImportSummary.IgnoredInvalidCount == 1
                    && pvuFavoritesListImportSummary.MissingCount == 1
                    && pvuFavoritesListImportSummary.UnmatchedCount == 1
                    && importedLevel == 4
                    && preservedLevel == 2
                    && clampedLevel == 5
                    && zeroLevel == 0
                    && pvuFavoriteDefaultLevel == 5
                    && pvuFavoriteExplicitLevel == 2
                    && pvuFavoriteListLevel == 5
                    && filteredAfterImport == 6
                    && storeCountAfterImport == 6
                    && persistedLevels["wpf-preview.png"] == 4
                    && persistedLevels["wpf-settings.png"] == 2
                    && persistedLevels["wpf-list.png"] == 5
                    && persistedLevels["wpf-modal-preview.png"] == 0
                    && persistedLevels[pvuFavoriteDefaultName] == 5
                    && persistedLevels[pvuFavoriteExplicitName] == 2
                    && persistedLevels[pvuFavoriteListName] == 5
                    && reloadSelected
                    && reloadedImportedLevel == 4
                    && reloadedPreservedLevel == 2
                    && reloadedClampedLevel == 5
                    && reloadedZeroLevel == 0
                    && reloadedPvuFavoriteDefaultLevel == 5
                    && reloadedPvuFavoriteExplicitLevel == 2
                    && reloadedPvuFavoriteListLevel == 5
                    && reloadedFilteredCount == 6;

                result = new FavoriteImportSmokeResult(
                    ok,
                    ok ? "pvu_fav_levels and pvu_favorites import, preserve-existing, ignore cases, persistence, reload, and filter passed" : "favorite import smoke did not meet expected policy",
                    folder,
                    favoritesPath,
                    browserStatePath,
                    importedPath,
                    preservedPath,
                    clampedPath,
                    zeroPath,
                    importSummary,
                    pvuFavoritesImportSummary,
                    pvuFavoritesListImportSummary,
                    importedLevel,
                    preservedLevel,
                    clampedLevel,
                    zeroLevel,
                    filteredAfterImport,
                    storeCountAfterImport,
                    persistedLevels["wpf-preview.png"],
                    persistedLevels["wpf-settings.png"],
                    persistedLevels["wpf-list.png"],
                    persistedLevels["wpf-modal-preview.png"],
                    reloadSelected,
                    reloadedImportedLevel,
                    reloadedPreservedLevel,
                    reloadedClampedLevel,
                    reloadedZeroLevel,
                    reloadedFilteredCount,
                    persistedLevels,
                    reloadedLevels);
            }
            catch (Exception ex)
            {
                first.Close();
                result = new FavoriteImportSmokeResult(false, ex.Message, folder, favoritesPath, browserStatePath, importedPath, preservedPath, clampedPath, zeroPath, null, null, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, new Dictionary<string, int>(), new Dictionary<string, int>());
            }

            WriteFavoriteImportSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureSeenSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? seenPath = ArgValue(args, "--seen-path");
        string? favoritesPath = ArgValue(args, "--favorites-path");
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(seenPath))
        {
            WriteSeenSmokeResult(
                resultPath,
                new SeenSmokeResult(
                    false,
                    "missing required --folder or --seen-path",
                    folder,
                    seenPath,
                    favoritesPath,
                    null,
                    null,
                    0,
                    0,
                    false,
                    false,
                    0,
                    0,
                    0,
                    false,
                    0,
                    0,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0));
            Shutdown(1);
            return;
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        if (!string.IsNullOrWhiteSpace(favoritesPath))
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();
        first.SuppressStatePersistence();

        first.Dispatcher.InvokeAsync(async () =>
        {
            SeenSmokeResult result;
            try
            {
                await first.LoadFolderAsync(folder);
                first.SetSearchQuery("", persist: false);

                string? selectedName = first.SelectedFileNameForSmoke;
                string? selectedPath = first.SelectedPathForSmoke;
                int initialUnseen = first.LastInitialUnseenCountForSmoke;
                int unseenAfterSelection = first.UnseenCountForSmoke;
                bool selectedMarkedSeen = selectedPath is not null && !first.SelectedUnseenForSmoke && ReadSeenFlag(seenPath, selectedPath);
                int seenStoreAfterSelection = first.SeenStoreCountForSmoke;
                int favoriteCountAfterSelection = first.FavoriteStoreCountForSmoke;
                long firstLoadTotalMs = first.LastLoadMetrics?.TotalMs ?? 0;
                int firstThumbnailsCompleted = first.LastLoadMetrics?.ThumbnailsCompleted ?? 0;

                first.SetUnseenOnlyFilterForSmoke(true);
                int unseenOnlyAfterSelection = first.FilteredCountForSmoke;
                first.Close();

                bool persistedSeen = selectedPath is not null && ReadSeenFlag(seenPath, selectedPath);

                var second = HiddenWindow();
                second.Show();
                second.SuppressStatePersistence();
                await second.LoadFolderAsync(folder);
                second.SetSearchQuery("", persist: false);
                bool reloadedSelected = !string.IsNullOrWhiteSpace(selectedName) && second.SelectFileNameForSmoke(selectedName);
                bool reloadedSelectedSeen = selectedPath is not null && ReadSeenFlag(seenPath, selectedPath) && !second.SelectedUnseenForSmoke;
                int reloadInitialUnseen = second.LastInitialUnseenCountForSmoke;
                int reloadUnseenAfterSelection = second.UnseenCountForSmoke;
                int reloadSeenStoreCount = second.SeenStoreCountForSmoke;
                int reloadFavoriteCount = second.FavoriteStoreCountForSmoke;
                second.SetUnseenOnlyFilterForSmoke(true);
                int reloadUnseenOnlyCount = second.FilteredCountForSmoke;
                second.Close();

                bool ok = initialUnseen > 0
                    && selectedPath is not null
                    && selectedMarkedSeen
                    && unseenAfterSelection == initialUnseen - 1
                    && unseenOnlyAfterSelection == unseenAfterSelection
                    && persistedSeen
                    && reloadedSelected
                    && reloadedSelectedSeen
                    && reloadInitialUnseen == unseenAfterSelection
                    && reloadUnseenAfterSelection == unseenAfterSelection
                    && reloadUnseenOnlyCount == unseenAfterSelection
                    && reloadSeenStoreCount == seenStoreAfterSelection
                    && reloadFavoriteCount == favoriteCountAfterSelection
                    && firstLoadTotalMs > 0
                    && firstThumbnailsCompleted == initialUnseen;

                result = new SeenSmokeResult(
                    ok,
                    ok ? "real-folder seen/unseen state, filter, persistence, reload, and favorite-store isolation passed" : "seen/unseen smoke did not meet expected policy",
                    folder,
                    seenPath,
                    favoritesPath,
                    selectedName,
                    selectedPath,
                    initialUnseen,
                    unseenAfterSelection,
                    selectedMarkedSeen,
                    persistedSeen,
                    unseenOnlyAfterSelection,
                    seenStoreAfterSelection,
                    favoriteCountAfterSelection,
                    reloadedSelected,
                    reloadInitialUnseen,
                    reloadUnseenAfterSelection,
                    reloadedSelectedSeen,
                    reloadUnseenOnlyCount,
                    reloadSeenStoreCount,
                    reloadFavoriteCount,
                    firstLoadTotalMs,
                    firstThumbnailsCompleted);
            }
            catch (Exception ex)
            {
                first.Close();
                result = new SeenSmokeResult(false, ex.Message, folder, seenPath, favoritesPath, null, null, 0, 0, false, false, 0, 0, 0, false, 0, 0, false, 0, 0, 0, 0, 0);
            }

            WriteSeenSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureSeenImportSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? seenPath = ArgValue(args, "--seen-path");
        string? favoritesPath = ArgValue(args, "--favorites-path");
        string? browserStatePath = ArgValue(args, "--browser-state-path");
        if (string.IsNullOrWhiteSpace(folder) ||
            string.IsNullOrWhiteSpace(seenPath) ||
            string.IsNullOrWhiteSpace(favoritesPath) ||
            string.IsNullOrWhiteSpace(browserStatePath))
        {
            WriteSeenImportSmokeResult(
                resultPath,
                new SeenImportSmokeResult
                {
                    Ok = false,
                    Message = "missing required --folder, --seen-path, --favorites-path, or --browser-state-path",
                    Folder = folder,
                    SeenPath = seenPath,
                    FavoritesPath = favoritesPath,
                    BrowserStatePath = browserStatePath,
                });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string favoriteSeedName = "wpf-confirm.png";
        string favoriteSeedPath = Path.Combine(fullFolder, favoriteSeedName);
        string statePath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(resultPath)) ?? Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(resultPath) + "-state.json");

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (File.Exists(favoriteSeedPath))
            WriteFavoriteSeed(favoritesPath, favoriteSeedPath, 3);

        var first = HiddenWindow();
        first.Show();
        first.SuppressStatePersistence();

        first.Dispatcher.InvokeAsync(async () =>
        {
            SeenImportSmokeResult result;
            try
            {
                await first.LoadFolderAsync(folder);
                first.SetSearchQuery("", persist: false);

                string? selectedName = first.SelectedFileNameForSmoke;
                string? selectedPath = first.SelectedPathForSmoke;
                var candidateNames = new[]
                    {
                        "wpf-list.png",
                        "wpf-modal-preview.png",
                        "wpf-enhance.png",
                        "wpf-album.png",
                        "wpf-settings.png",
                        "wpf-landing.png",
                        "wpf-confirm.png",
                    }
                    .Where(name => !string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase))
                    .Where(name => first.PathForFileNameForSmoke(name) is not null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                bool fixtureFilesExist = selectedPath is not null &&
                    candidateNames.Count >= 3 &&
                    File.Exists(favoriteSeedPath);
                string importedName = fixtureFilesExist ? candidateNames[0] : "";
                string importedByName = fixtureFilesExist ? candidateNames[1] : "";
                string zeroName = fixtureFilesExist ? candidateNames[2] : "";
                string? importedPath = fixtureFilesExist ? first.PathForFileNameForSmoke(importedName) : null;
                string? importedByNamePath = fixtureFilesExist ? first.PathForFileNameForSmoke(importedByName) : null;
                string? zeroPath = fixtureFilesExist ? first.PathForFileNameForSmoke(zeroName) : null;
                string unmatchedPath = Path.Combine(fullFolder, "missing-pvu-seen.png");

                if (fixtureFilesExist && importedPath is not null && importedByNamePath is not null && zeroPath is not null)
                {
                    WritePvuSeenImagesImportFixture(
                        browserStatePath,
                        selectedPath!,
                        importedPath,
                        importedByName,
                        zeroPath,
                        unmatchedPath);
                }

                int preImportSeenCount = first.SeenStoreCountForSmoke;
                int preImportUnseenCount = first.UnseenCountForSmoke;
                int favoriteCountBefore = first.FavoriteStoreCountForSmoke;
                SeenImportSummary importSummary = fixtureFilesExist
                    ? first.ImportPvuSeenImagesForSmoke(browserStatePath)
                    : SeenImportSummary.Failed(browserStatePath, "fixture files missing");

                bool importedSeen = !string.IsNullOrWhiteSpace(importedName) && first.IsFileUnseenForSmoke(importedName) == false;
                bool importedByNameSeen = !string.IsNullOrWhiteSpace(importedByName) && first.IsFileUnseenForSmoke(importedByName) == false;
                bool zeroStillUnseen = !string.IsNullOrWhiteSpace(zeroName) && first.IsFileUnseenForSmoke(zeroName) == true;
                int seenStoreAfterImport = first.SeenStoreCountForSmoke;
                int unseenAfterImport = first.UnseenCountForSmoke;
                int favoriteCountAfter = first.FavoriteStoreCountForSmoke;
                first.SetUnseenOnlyFilterForSmoke(true);
                int filteredAfterImport = first.FilteredCountForSmoke;
                first.Close();

                bool preservedPersistedSeen = selectedPath is not null && ReadSeenFlag(seenPath, selectedPath);
                bool importedPersistedSeen = importedPath is not null && ReadSeenFlag(seenPath, importedPath);
                bool importedByNamePersistedSeen = importedByNamePath is not null && ReadSeenFlag(seenPath, importedByNamePath);
                bool zeroPersistedSeen = zeroPath is not null && ReadSeenFlag(seenPath, zeroPath);

                if (selectedPath is not null)
                    WriteViewerStateSeed(statePath, fullFolder, selectedPath);

                var second = HiddenWindow();
                second.Show();
                second.SuppressStatePersistence();
                await second.LoadFolderAsync(folder);
                second.SetSearchQuery("", persist: false);
                bool reloadImportedSeen = !string.IsNullOrWhiteSpace(importedName) && second.IsFileUnseenForSmoke(importedName) == false;
                bool reloadImportedByNameSeen = !string.IsNullOrWhiteSpace(importedByName) && second.IsFileUnseenForSmoke(importedByName) == false;
                bool reloadZeroStillUnseen = !string.IsNullOrWhiteSpace(zeroName) && second.IsFileUnseenForSmoke(zeroName) == true;
                int reloadSeenStoreCount = second.SeenStoreCountForSmoke;
                int reloadUnseenCount = second.UnseenCountForSmoke;
                int reloadFavoriteCount = second.FavoriteStoreCountForSmoke;
                second.SetUnseenOnlyFilterForSmoke(true);
                int reloadFilteredCount = second.FilteredCountForSmoke;
                second.Close();

                bool ok = fixtureFilesExist
                    && importSummary.Ok
                    && importSummary.SourceShape == "browserLocalStorage.pvu_seen_images"
                    && importSummary.TotalEntries == 7
                    && importSummary.ImportedCount == 2
                    && importSummary.PreservedCount == 1
                    && importSummary.IgnoredZeroCount == 1
                    && importSummary.IgnoredInvalidCount == 1
                    && importSummary.MissingCount == 1
                    && importSummary.UnmatchedCount == 1
                    && seenStoreAfterImport == preImportSeenCount + 2
                    && unseenAfterImport == preImportUnseenCount - 2
                    && filteredAfterImport == unseenAfterImport
                    && favoriteCountBefore == favoriteCountAfter
                    && favoriteCountAfter == reloadFavoriteCount
                    && preservedPersistedSeen
                    && importedPersistedSeen
                    && importedByNamePersistedSeen
                    && !zeroPersistedSeen
                    && importedSeen
                    && importedByNameSeen
                    && zeroStillUnseen
                    && reloadSeenStoreCount == seenStoreAfterImport
                    && reloadUnseenCount == unseenAfterImport
                    && reloadFilteredCount == unseenAfterImport
                    && reloadImportedSeen
                    && reloadImportedByNameSeen
                    && reloadZeroStillUnseen;

                result = new SeenImportSmokeResult
                {
                    Ok = ok,
                    Message = ok ? "pvu_seen_images explicit-file import, preserve-existing, ignore cases, persistence, reload, and favorite-store isolation passed" : "seen import smoke did not meet expected policy",
                    Folder = folder,
                    SeenPath = seenPath,
                    FavoritesPath = favoritesPath,
                    BrowserStatePath = browserStatePath,
                    PreservedName = selectedName,
                    ImportedName = importedName,
                    ImportedByName = importedByName,
                    ZeroName = zeroName,
                    ImportSummary = importSummary,
                    PreImportSeenCount = preImportSeenCount,
                    SeenStoreCountAfterImport = seenStoreAfterImport,
                    PreImportUnseenCount = preImportUnseenCount,
                    UnseenAfterImport = unseenAfterImport,
                    FilteredAfterImport = filteredAfterImport,
                    FavoriteCountBefore = favoriteCountBefore,
                    FavoriteCountAfter = favoriteCountAfter,
                    ReloadSeenStoreCount = reloadSeenStoreCount,
                    ReloadUnseenCount = reloadUnseenCount,
                    ReloadFilteredCount = reloadFilteredCount,
                    ReloadFavoriteCount = reloadFavoriteCount,
                    PreservedPersistedSeen = preservedPersistedSeen,
                    ImportedPersistedSeen = importedPersistedSeen,
                    ImportedByNamePersistedSeen = importedByNamePersistedSeen,
                    ZeroStillUnseen = zeroStillUnseen && !zeroPersistedSeen,
                    ReloadImportedSeen = reloadImportedSeen,
                    ReloadImportedByNameSeen = reloadImportedByNameSeen,
                    ReloadZeroStillUnseen = reloadZeroStillUnseen,
                };
            }
            catch (Exception ex)
            {
                first.Close();
                result = new SeenImportSmokeResult
                {
                    Ok = false,
                    Message = ex.Message,
                    Folder = folder,
                    SeenPath = seenPath,
                    FavoritesPath = favoritesPath,
                    BrowserStatePath = browserStatePath,
                };
            }

            WriteSeenImportSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureSharedSeenSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteSharedSeenSmokeResult(
                resultPath,
                new SharedSeenSmokeResult
                {
                    Ok = false,
                    Message = "missing required --folder",
                    Folder = folder,
                });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string[] fixtureNames = GetSmokeImageFileNames(fullFolder).Take(4).ToArray();
        if (fixtureNames.Length < 3)
        {
            WriteSharedSeenSmokeResult(
                resultPath,
                new SharedSeenSmokeResult
                {
                    Ok = false,
                    Message = "shared seen smoke requires at least three fixture images",
                    Folder = folder,
                });
            Shutdown(1);
            return;
        }

        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();
        first.SuppressStatePersistence();

        first.Dispatcher.InvokeAsync(async () =>
        {
            SharedSeenSmokeResult result;
            try
            {
                result = await RunSharedSeenSmokeAsync(first, fullFolder, smokeRoot, fixtureNames);
            }
            catch (Exception ex)
            {
                first.Close();
                result = new SharedSeenSmokeResult
                {
                    Ok = false,
                    Message = ex.Message,
                    Folder = folder,
                    ProjectRoot = smokeRoot,
                };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteSharedSeenSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureSharedRecentSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteSharedRecentSmokeResult(
                resultPath,
                new SharedRecentSmokeResult
                {
                    Ok = false,
                    Message = "missing required --folder",
                    Folder = folder,
                });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            SharedRecentSmokeResult result;
            try
            {
                result = await RunSharedRecentSmokeAsync(first, fullFolder, smokeRoot);
            }
            catch (Exception ex)
            {
                first.Close();
                result = new SharedRecentSmokeResult
                {
                    Ok = false,
                    Message = ex.Message,
                    Folder = folder,
                    ProjectRoot = smokeRoot,
                };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteSharedRecentSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private static async Task<SharedRecentSmokeResult> RunSharedRecentSmokeAsync(
        MainWindow first,
        string fullFolder,
        string smokeRoot)
    {
        string cacheRoot = Path.Combine(smokeRoot, ".cache");
        string sharedRecentPath = Path.Combine(cacheRoot, "recent-folders.json");
        string favoritesPath = Path.Combine(cacheRoot, "favorites.json");
        string seenPath = Path.Combine(cacheRoot, "seen.json");
        string statePath = Path.Combine(cacheRoot, "state.json");
        string writeFolder = Path.Combine(smokeRoot, "write-folder");
        string preservedFolder = Path.Combine(smokeRoot, "preserved-folder");
        string malformedFolder = Path.Combine(smokeRoot, "malformed-folder");
        Directory.CreateDirectory(writeFolder);
        Directory.CreateDirectory(preservedFolder);
        Directory.CreateDirectory(malformedFolder);

        string favoriteSeedPath = Path.Combine(fullFolder, "shared-recent-favorite-seed.png");
        string seenSeedPath = Path.Combine(fullFolder, "shared-recent-seen-seed.png");
        WriteFavoriteSeed(favoritesPath, favoriteSeedPath, 4);
        WriteSeenSeed(seenPath, seenSeedPath);
        string favoritesBefore = File.ReadAllText(favoritesPath);
        string seenBefore = File.ReadAllText(seenPath);
        WriteSharedRecentSeed(sharedRecentPath, fullFolder, preservedFolder);

        var importWindow = HiddenWindow();
        importWindow.Show();
        string? importedCurrentFolder = importWindow.CurrentFolderForSmoke;
        string resolvedRecentPath = importWindow.SharedRecentPathForSmoke;
        importWindow.Close();

        await first.LoadFolderAsync(writeFolder);
        string? writeCurrentFolder = first.CurrentFolderForSmoke;
        first.Close();

        SharedRecentSmokeSnapshot afterWrite = ReadSharedRecentSnapshot(sharedRecentPath);
        ViewerState? stateAfterWrite = ReadPersistedState(statePath);
        bool importedSharedLastFolder = string.Equals(
            NormalizeFavoritePath(fullFolder),
            NormalizeFavoritePath(importedCurrentFolder ?? ""),
            StringComparison.OrdinalIgnoreCase);
        bool wroteLastFolder = afterWrite.Ok
            && afterWrite.LastFolderSet.Count == 1
            && string.Equals(afterWrite.LastFolderSet[0], NormalizeFavoritePath(writeFolder), StringComparison.OrdinalIgnoreCase);
        bool additivePreserved = afterWrite.Ok && afterWrite.ContainsFolderSet(preservedFolder);
        bool writeFolderInRecent = afterWrite.Ok && afterWrite.ContainsFolderSet(writeFolder);
        bool statePreserved = stateAfterWrite is not null
            && string.Equals(
                NormalizeFavoritePath(stateAfterWrite.LastFolder ?? ""),
                NormalizeFavoritePath(writeFolder),
                StringComparison.OrdinalIgnoreCase);
        bool favoritesUnchangedAfterWrite = string.Equals(favoritesBefore, File.ReadAllText(favoritesPath), StringComparison.Ordinal);
        bool seenUnchangedAfterWrite = string.Equals(seenBefore, File.ReadAllText(seenPath), StringComparison.Ordinal);

        var reloadWindow = HiddenWindow();
        reloadWindow.Show();
        string? reloadedCurrentFolder = reloadWindow.CurrentFolderForSmoke;
        reloadWindow.Close();
        bool reloadedLocalStateWins = string.Equals(
            NormalizeFavoritePath(reloadedCurrentFolder ?? ""),
            NormalizeFavoritePath(writeFolder),
            StringComparison.OrdinalIgnoreCase);

        const string malformedJson = "[";
        File.WriteAllText(sharedRecentPath, malformedJson);
        var malformedWindow = HiddenWindow();
        malformedWindow.Show();
        await malformedWindow.LoadFolderAsync(malformedFolder);
        malformedWindow.Close();

        string malformedAfter = File.ReadAllText(sharedRecentPath);
        ViewerState? stateAfterMalformed = ReadPersistedState(statePath);
        bool malformedPreserved = string.Equals(malformedJson, malformedAfter, StringComparison.Ordinal);
        bool localStateStillSavedAfterMalformed = stateAfterMalformed is not null
            && string.Equals(
                NormalizeFavoritePath(stateAfterMalformed.LastFolder ?? ""),
                NormalizeFavoritePath(malformedFolder),
                StringComparison.OrdinalIgnoreCase);
        bool favoritesUnchangedAfterMalformed = string.Equals(favoritesBefore, File.ReadAllText(favoritesPath), StringComparison.Ordinal);
        bool seenUnchangedAfterMalformed = string.Equals(seenBefore, File.ReadAllText(seenPath), StringComparison.Ordinal);

        bool ok = string.Equals(Path.GetFullPath(resolvedRecentPath), Path.GetFullPath(sharedRecentPath), StringComparison.OrdinalIgnoreCase)
            && importedSharedLastFolder
            && string.Equals(NormalizeFavoritePath(writeCurrentFolder ?? ""), NormalizeFavoritePath(writeFolder), StringComparison.OrdinalIgnoreCase)
            && wroteLastFolder
            && writeFolderInRecent
            && additivePreserved
            && statePreserved
            && reloadedLocalStateWins
            && malformedPreserved
            && localStateStillSavedAfterMalformed
            && favoritesUnchangedAfterWrite
            && seenUnchangedAfterWrite
            && favoritesUnchangedAfterMalformed
            && seenUnchangedAfterMalformed;

        return new SharedRecentSmokeResult
        {
            Ok = ok,
            Message = ok ? "shared recent import, write-through, additive preservation, malformed fail-safe, and favorites/seen isolation passed" : "shared recent smoke did not meet expected policy",
            Folder = fullFolder,
            ProjectRoot = smokeRoot,
            SharedRecentPath = sharedRecentPath,
            ResolvedRecentPath = resolvedRecentPath,
            ImportedCurrentFolder = importedCurrentFolder,
            WriteFolder = writeFolder,
            PreservedFolder = preservedFolder,
            MalformedFolder = malformedFolder,
            WriteCurrentFolder = writeCurrentFolder,
            LastFolderAfterWrite = afterWrite.LastFolderSet,
            RecentFolderSetCountAfterWrite = afterWrite.RecentFolderSets.Count,
            ImportedSharedLastFolder = importedSharedLastFolder,
            WroteLastFolder = wroteLastFolder,
            WriteFolderInRecent = writeFolderInRecent,
            AdditivePreserved = additivePreserved,
            StatePreserved = statePreserved,
            ReloadedCurrentFolder = reloadedCurrentFolder,
            ReloadedLocalStateWins = reloadedLocalStateWins,
            MalformedPreserved = malformedPreserved,
            LocalStateStillSavedAfterMalformed = localStateStillSavedAfterMalformed,
            FavoritesUnchangedAfterWrite = favoritesUnchangedAfterWrite,
            SeenUnchangedAfterWrite = seenUnchangedAfterWrite,
            FavoritesUnchangedAfterMalformed = favoritesUnchangedAfterMalformed,
            SeenUnchangedAfterMalformed = seenUnchangedAfterMalformed,
        };
    }

    private void CaptureFolderSetSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteFolderSetSmokeResult(resultPath, new FolderSetSmokeResult { Ok = false, Message = "missing required --folder", Folder = folder });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            FolderSetSmokeResult result;
            try
            {
                result = await RunFolderSetSmokeAsync(win, fullFolder, smokeRoot);
            }
            catch (Exception ex)
            {
                win.Close();
                result = new FolderSetSmokeResult { Ok = false, Message = ex.Message, Folder = folder, ProjectRoot = smokeRoot };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteFolderSetSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private static async Task<FolderSetSmokeResult> RunFolderSetSmokeAsync(MainWindow win, string fullFolder, string smokeRoot)
    {
        string cacheRoot = Path.Combine(smokeRoot, ".cache");
        string sharedRecentPath = Path.Combine(cacheRoot, "recent-folders.json");
        string favoritesPath = Path.Combine(cacheRoot, "favorites.json");
        string seenPath = Path.Combine(cacheRoot, "seen.json");
        string statePath = Path.Combine(cacheRoot, "state.json");
        string secondFolder = PrepareSecondFolderFixture(fullFolder, smokeRoot);
        string preservedFolder = Path.Combine(smokeRoot, "preserved-folder");
        string missingFolder = Path.Combine(smokeRoot, "missing-folder");
        Directory.CreateDirectory(preservedFolder);

        WriteFavoriteSeed(favoritesPath, Path.Combine(fullFolder, "folder-set-favorite-seed.png"), 4);
        WriteSeenSeed(seenPath, Path.Combine(fullFolder, "folder-set-seen-seed.png"));
        string favoritesBefore = File.ReadAllText(favoritesPath);
        var seenBeforeMap = ReadSeenMap(seenPath);
        WriteSharedRecentSetSeed(sharedRecentPath, [fullFolder, secondFolder], [preservedFolder]);

        var importWindow = HiddenWindow();
        importWindow.Show();
        string resolvedRecentPath = importWindow.SharedRecentPathForSmoke;
        var importedLanding = importWindow.LandingFolderSetForSmoke;
        int recentCountOnStartup = importWindow.RecentFolderSetCountForSmoke;
        importWindow.Close();

        win.SetLandingFolderSetForSmoke([fullFolder]);
        int pastedAdded = win.AppendPastedFoldersForSmoke(string.Join(Environment.NewLine, [secondFolder, missingFolder, fullFolder]));
        var landingAfterPaste = win.LandingFolderSetForSmoke;
        await win.LoadFolderSetAsync(landingAfterPaste);
        var currentFolderSet = win.CurrentFolderSetForSmoke;
        int filteredAfterLoad = win.FilteredCountForSmoke;
        string? currentFolder = win.CurrentFolderForSmoke;
        win.Close();

        SharedRecentSmokeSnapshot afterWrite = ReadSharedRecentSnapshot(sharedRecentPath);
        ViewerState? stateAfterWrite = ReadPersistedState(statePath);
        int expectedMinImages = CountDirectSmokeImages(fullFolder) + CountDirectSmokeImages(secondFolder);
        bool importedSharedLastFolderSet = SameFolderSet(importedLanding, [fullFolder, secondFolder]);
        bool pastedSecondFolder = pastedAdded == 1 && SameFolderSet(landingAfterPaste, [fullFolder, secondFolder]);
        bool loadedCurrentFolderSet = SameFolderSet(currentFolderSet, [fullFolder, secondFolder]);
        bool stateSavedFolderSet = stateAfterWrite?.LastFolderSet is not null && SameFolderSet(stateAfterWrite.LastFolderSet, [fullFolder, secondFolder]);
        bool wroteSharedLastFolderSet = afterWrite.Ok && SameFolderSet(afterWrite.LastFolderSet, [fullFolder, secondFolder]);
        bool writeFolderSetInRecent = afterWrite.Ok && SnapshotContainsFolderSet(afterWrite, [fullFolder, secondFolder]);
        bool additivePreserved = afterWrite.Ok && SnapshotContainsFolderSet(afterWrite, [preservedFolder]);
        bool favoritesUnchanged = string.Equals(favoritesBefore, File.ReadAllText(favoritesPath), StringComparison.Ordinal);
        var seenAfterMap = ReadSeenMap(seenPath);
        bool seenPreserved = seenBeforeMap.Keys.All(path => seenAfterMap.ContainsKey(path));

        bool ok = string.Equals(Path.GetFullPath(resolvedRecentPath), Path.GetFullPath(sharedRecentPath), StringComparison.OrdinalIgnoreCase)
            && importedSharedLastFolderSet
            && recentCountOnStartup > 0
            && pastedSecondFolder
            && loadedCurrentFolderSet
            && string.Equals(NormalizeFavoritePath(currentFolder ?? ""), NormalizeFavoritePath(fullFolder), StringComparison.OrdinalIgnoreCase)
            && filteredAfterLoad >= expectedMinImages
            && stateSavedFolderSet
            && wroteSharedLastFolderSet
            && writeFolderSetInRecent
            && additivePreserved
            && favoritesUnchanged
            && seenPreserved;

        return new FolderSetSmokeResult
        {
            Ok = ok,
            Message = ok ? "folder-set landing, paste, multi-folder load, shared recent write-through, and state persistence passed" : "folder-set smoke did not meet expected policy",
            Folder = fullFolder,
            ProjectRoot = smokeRoot,
            SharedRecentPath = sharedRecentPath,
            ResolvedRecentPath = resolvedRecentPath,
            SecondFolder = secondFolder,
            MissingFolder = missingFolder,
            ImportedLandingFolderSet = importedLanding,
            LandingAfterPaste = landingAfterPaste,
            CurrentFolderSet = currentFolderSet,
            CurrentFolder = currentFolder,
            PastedAdded = pastedAdded,
            RecentCountOnStartup = recentCountOnStartup,
            FilteredAfterLoad = filteredAfterLoad,
            ExpectedMinImages = expectedMinImages,
            StateLastFolderSet = stateAfterWrite?.LastFolderSet ?? [],
            SharedLastFolderSet = afterWrite.LastFolderSet,
            RecentFolderSetCountAfterWrite = afterWrite.RecentFolderSets.Count,
            ImportedSharedLastFolderSet = importedSharedLastFolderSet,
            PastedSecondFolder = pastedSecondFolder,
            LoadedCurrentFolderSet = loadedCurrentFolderSet,
            StateSavedFolderSet = stateSavedFolderSet,
            WroteSharedLastFolderSet = wroteSharedLastFolderSet,
            WriteFolderSetInRecent = writeFolderSetInRecent,
            AdditivePreserved = additivePreserved,
            FavoritesUnchanged = favoritesUnchanged,
            SeenPreserved = seenPreserved,
        };
    }

    private void CaptureGridZoomSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteGridZoomSmokeResult(resultPath, new GridZoomSmokeResult(false, "missing required --folder", folder, null, 0, 0, 0, 0, 0, 0, 0, false, false, false, false, false, false));
            Shutdown(1);
            return;
        }

        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string statePath = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-state.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.SuppressStatePersistence();

        win.Dispatcher.InvokeAsync(async () =>
        {
            GridZoomSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                double initial = win.CardWidthForSmoke;
                bool buttonIn = win.ZoomInForSmoke();
                double afterButtonIn = win.CardWidthForSmoke;
                bool buttonOut = win.ZoomOutForSmoke();
                double afterButtonOut = win.CardWidthForSmoke;
                bool shortcutIn = win.ZoomShortcutForSmoke("plus");
                double afterShortcutIn = win.CardWidthForSmoke;
                bool shortcutReset = win.ZoomShortcutForSmoke("0");
                double afterShortcutReset = win.CardWidthForSmoke;
                bool wheelIn = win.ZoomWheelForSmoke(120);
                double afterWheelIn = win.CardWidthForSmoke;
                bool wheelOut = win.ZoomWheelForSmoke(-120);
                double afterWheelOut = win.CardWidthForSmoke;
                bool allWidthsMatch = win.AllCardWidthsMatchForSmoke(afterWheelOut);
                int filtered = win.FilteredCountForSmoke;

                bool ok = filtered > 0
                    && buttonIn
                    && afterButtonIn > initial
                    && buttonOut
                    && Math.Abs(afterButtonOut - initial) < 0.01
                    && shortcutIn
                    && afterShortcutIn > afterButtonOut
                    && shortcutReset
                    && Math.Abs(afterShortcutReset - 190) < 0.01
                    && wheelIn
                    && afterWheelIn > afterShortcutReset
                    && wheelOut
                    && Math.Abs(afterWheelOut - afterShortcutReset) < 0.01
                    && allWidthsMatch;

                result = new GridZoomSmokeResult(ok, ok ? "grid zoom buttons, shortcut helper, wheel helper, and tile card-width sync passed" : "grid zoom smoke did not match expected size changes", folder, statePath, filtered, initial, afterButtonIn, afterButtonOut, afterShortcutIn, afterShortcutReset, afterWheelIn, buttonIn, buttonOut, shortcutIn, shortcutReset, wheelIn, wheelOut && allWidthsMatch);
            }
            catch (Exception ex)
            {
                result = new GridZoomSmokeResult(false, ex.Message, folder, statePath, 0, 0, 0, 0, 0, 0, 0, false, false, false, false, false, false);
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteGridZoomSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureDisplayStyleSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteDisplayStyleSmokeResult(resultPath, new DisplayStyleSmokeResult(false, "missing required --folder", folder, null, null, 0, null, null, null, null, false, false, false, false));
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            DisplayStyleSmokeResult result;
            try
            {
                await first.LoadFolderAsync(fullFolder);
                DisplayStyleMetrics standard = first.DisplayStyleMetricsForSmoke();
                bool compactChanged = first.SetDisplayStyleForSmoke("compact");
                DisplayStyleMetrics compact = first.DisplayStyleMetricsForSmoke();
                bool posterChanged = first.SetDisplayStyleForSmoke("poster");
                DisplayStyleMetrics poster = first.DisplayStyleMetricsForSmoke();
                bool zoomed = first.ZoomInForSmoke();
                DisplayStyleMetrics posterZoomed = first.DisplayStyleMetricsForSmoke();
                string statePath = first.StatePathForSmoke;
                int filtered = first.FilteredCountForSmoke;
                first.Close();

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(fullFolder);
                DisplayStyleMetrics restored = second.DisplayStyleMetricsForSmoke();
                second.Close();

                ViewerState? persisted = ReadPersistedState(statePath);
                bool compactDense = compact.CardWidth < standard.CardWidth
                    && compact.CardHeight < standard.CardHeight
                    && compact.ListThumbnailSize < standard.ListThumbnailSize
                    && Area(compact) < Area(standard);
                bool posterPortrait = poster.CardHeight > poster.CardWidth
                    && poster.CardHeight > standard.CardHeight
                    && poster.ListThumbnailSize > standard.ListThumbnailSize
                    && Area(poster) > Area(standard);
                bool zoomComposes = zoomed
                    && posterZoomed.CardWidth > poster.CardWidth
                    && posterZoomed.CardHeight > poster.CardHeight;
                bool persistedPoster = string.Equals(persisted?.DisplayStyle, "poster", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(restored.Style, "poster", StringComparison.OrdinalIgnoreCase);

                bool ok = filtered > 0
                    && string.Equals(standard.Style, "standard", StringComparison.OrdinalIgnoreCase)
                    && compactChanged
                    && posterChanged
                    && compactDense
                    && posterPortrait
                    && zoomComposes
                    && persistedPoster;

                result = new DisplayStyleSmokeResult(
                    ok,
                    ok ? "display styles changed grid density/aspect, composed with zoom, and restored from WPF state" : "display style smoke did not meet density/aspect/persistence expectations",
                    fullFolder,
                    smokeRoot,
                    statePath,
                    filtered,
                    standard,
                    compact,
                    poster,
                    posterZoomed,
                    compactChanged,
                    posterChanged,
                    zoomed,
                    persistedPoster);
            }
            catch (Exception ex)
            {
                first.Close();
                result = new DisplayStyleSmokeResult(false, ex.Message, fullFolder, smokeRoot, null, 0, null, null, null, null, false, false, false, false);
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteDisplayStyleSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private static double Area(DisplayStyleMetrics metrics)
        => metrics.CardWidth * metrics.CardHeight;

    private void CaptureSortSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteSortSmokeResult(resultPath, new SortSmokeResult { Ok = false, Message = "missing required --folder", Folder = folder });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string[] fixtureNames = GetSmokeImageFileNames(fullFolder);
        if (fixtureNames.Length < 3)
        {
            WriteSortSmokeResult(resultPath, new SortSmokeResult { Ok = false, Message = "sort smoke requires at least 3 fixture images", Folder = fullFolder });
            Shutdown(1);
            return;
        }

        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        string sortFolder = PrepareSortSmokeFolder(fullFolder, fixtureNames, smokeRoot);
        string alphaName = "alpha-sort" + Path.GetExtension(fixtureNames[0]).ToLowerInvariant();
        string bravoName = "bravo-sort" + Path.GetExtension(fixtureNames[1]).ToLowerInvariant();
        string charlieName = "charlie-sort" + Path.GetExtension(fixtureNames[2]).ToLowerInvariant();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            SortSmokeResult result;
            try
            {
                await first.LoadFolderAsync(sortFolder);
                string statePath = first.StatePathForSmoke;
                List<string> newestOrder = first.FilteredFileNamesForSmoke(3);
                bool selectedAlpha = first.SelectFileNameForSmoke(alphaName);
                bool changedOldest = first.SetSortByForSmoke("modified-oldest");
                List<string> oldestOrder = first.FilteredFileNamesForSmoke(3);
                bool selectionKeptAfterOldest = string.Equals(first.SelectedFileNameForSmoke, alphaName, StringComparison.OrdinalIgnoreCase);
                bool changedName = first.SetSortByForSmoke("name");
                List<string> nameOrder = first.FilteredFileNamesForSmoke(3);
                bool selectionKeptAfterName = string.Equals(first.SelectedFileNameForSmoke, alphaName, StringComparison.OrdinalIgnoreCase);

                first.SetSearchQuery("alpha", persist: false);
                List<string> filteredAlphaOrder = first.FilteredFileNamesForSmoke(3);
                bool filterPreserved = first.FilteredCountForSmoke == 1
                    && filteredAlphaOrder.Count == 1
                    && string.Equals(filteredAlphaOrder[0], alphaName, StringComparison.OrdinalIgnoreCase);

                first.SetSearchQuery("", persist: false);
                bool modalOpened = first.OpenModalForSmoke();
                bool modalMovedNext = first.NavigateModalForSmoke(1);
                string? modalNextName = first.SelectedFileNameForSmoke;
                bool modalUsesSortedNameOrder = modalOpened
                    && modalMovedNext
                    && string.Equals(modalNextName, bravoName, StringComparison.OrdinalIgnoreCase);

                bool changedNewest = first.SetSortByForSmoke("modified-newest");
                ViewerState? persisted = ReadPersistedState(statePath);
                first.Close();

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(sortFolder);
                string restoredSort = second.SortByForSmoke;
                List<string> restoredOrder = second.FilteredFileNamesForSmoke(3);
                second.Close();

                List<string> expectedNewest = [bravoName, alphaName, charlieName];
                List<string> expectedOldest = [charlieName, alphaName, bravoName];
                List<string> expectedName = [alphaName, bravoName, charlieName];
                bool newestOk = SameNameOrder(newestOrder, expectedNewest);
                bool oldestOk = SameNameOrder(oldestOrder, expectedOldest);
                bool nameOk = SameNameOrder(nameOrder, expectedName);
                bool persistedNewest = string.Equals(persisted?.SortBy, "modified-newest", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(restoredSort, "modified-newest", StringComparison.OrdinalIgnoreCase)
                    && SameNameOrder(restoredOrder, expectedNewest);
                bool ok = newestOk
                    && changedOldest
                    && oldestOk
                    && selectionKeptAfterOldest
                    && changedName
                    && nameOk
                    && selectionKeptAfterName
                    && filterPreserved
                    && modalUsesSortedNameOrder
                    && changedNewest
                    && persistedNewest;

                result = new SortSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "modified newest, modified oldest, name sort, filter, selection, modal navigation, and persistence checks passed"
                        : "sort smoke did not meet order/filter/selection/modal/persistence expectations",
                    Folder = sortFolder,
                    ProjectRoot = smokeRoot,
                    StatePath = statePath,
                    ExpectedNewest = expectedNewest,
                    ExpectedOldest = expectedOldest,
                    ExpectedName = expectedName,
                    NewestOrder = newestOrder,
                    OldestOrder = oldestOrder,
                    NameOrder = nameOrder,
                    RestoredOrder = restoredOrder,
                    SelectedAlpha = selectedAlpha,
                    SelectionKeptAfterOldest = selectionKeptAfterOldest,
                    SelectionKeptAfterName = selectionKeptAfterName,
                    FilterPreserved = filterPreserved,
                    ModalOpened = modalOpened,
                    ModalMovedNext = modalMovedNext,
                    ModalNextName = modalNextName,
                    PersistedSort = persisted?.SortBy,
                    RestoredSort = restoredSort,
                };
            }
            catch (Exception ex)
            {
                first.Close();
                result = new SortSmokeResult { Ok = false, Message = ex.Message, Folder = sortFolder, ProjectRoot = smokeRoot };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteSortSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureEnhancedFilterSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteEnhancedFilterSmokeResult(resultPath, new EnhancedFilterSmokeResult { Ok = false, Message = "missing required --folder", Folder = folder });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string[] fixtureNames = GetSmokeImageFileNames(fullFolder);
        if (fixtureNames.Length < 3)
        {
            WriteEnhancedFilterSmokeResult(resultPath, new EnhancedFilterSmokeResult { Ok = false, Message = "enhanced filter smoke requires at least 3 fixture images", Folder = fullFolder });
            Shutdown(1);
            return;
        }

        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        string jobsPath = Path.Combine(smokeRoot, ".cache", "enhance", "jobs.json");
        string outputRoot = Path.Combine(smokeRoot, "enhanced-output");
        Directory.CreateDirectory(outputRoot);

        string validSource = Path.Combine(fullFolder, fixtureNames[0]);
        string staleSource = Path.Combine(fullFolder, fixtureNames[1]);
        string failedSource = Path.Combine(fullFolder, fixtureNames[2]);
        string validOutput = Path.Combine(outputRoot, "enhanced-" + Path.GetFileName(validSource));
        string missingOutput = Path.Combine(outputRoot, "missing-output.png");
        string missingSource = Path.Combine(smokeRoot, "missing-source.png");
        File.Copy(validSource, validOutput, overwrite: true);
        WriteEnhancedJobsFixture(jobsPath, validSource, validOutput, staleSource, missingOutput, failedSource, missingSource);
        string beforeJobsJson = File.ReadAllText(jobsPath);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            EnhancedFilterSmokeResult result;
            try
            {
                await win.LoadFolderAsync(fullFolder);
                string resolvedJobsPath = win.EnhancementJobsPathForSmoke;
                int allCount = win.FilteredCountForSmoke;
                bool selectedValid = win.SelectFileNameForSmoke(Path.GetFileName(validSource));
                bool selectedValidEnhanced = win.SelectedEnhancedForSmoke;
                string? selectedOutput = win.SelectedEnhancedOutputPathForSmoke;
                bool outputMatches = string.Equals(selectedOutput, validOutput, StringComparison.OrdinalIgnoreCase);

                win.SetEnhancedOnlyFilterForSmoke(true);
                int enhancedFilteredCount = win.FilteredCountForSmoke;
                bool validVisible = win.SelectFileNameForSmoke(Path.GetFileName(validSource));
                bool staleVisible = win.SelectFileNameForSmoke(Path.GetFileName(staleSource));
                bool failedVisible = win.SelectFileNameForSmoke(Path.GetFileName(failedSource));
                bool selectedStillEnhanced = win.SelectedEnhancedForSmoke;

                win.SetEnhancedOnlyFilterForSmoke(false);
                int afterClearCount = win.FilteredCountForSmoke;
                win.Close();

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(fullFolder);
                second.SetEnhancedOnlyFilterForSmoke(true);
                int reloadFilteredCount = second.FilteredCountForSmoke;
                bool reloadValidVisible = second.SelectFileNameForSmoke(Path.GetFileName(validSource));
                second.Close();

                string afterJobsJson = File.ReadAllText(jobsPath);
                bool enhancementStateUnchanged = string.Equals(beforeJobsJson, afterJobsJson, StringComparison.Ordinal);

                bool ok = string.Equals(Path.GetFullPath(resolvedJobsPath), Path.GetFullPath(jobsPath), StringComparison.OrdinalIgnoreCase)
                    && win.EnhancementReadOkForSmoke
                    && win.EnhancementJobsReadForSmoke >= 5
                    && win.EnhancedCandidateCountForSmoke == 1
                    && win.EnhancedStoreCountForSmoke == 1
                    && allCount >= 3
                    && selectedValid
                    && selectedValidEnhanced
                    && outputMatches
                    && enhancedFilteredCount == 1
                    && validVisible
                    && !staleVisible
                    && !failedVisible
                    && selectedStillEnhanced
                    && afterClearCount == allCount
                    && reloadFilteredCount == 1
                    && reloadValidVisible
                    && enhancementStateUnchanged;

                result = new EnhancedFilterSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "read-only enhanced metadata import, enhanced-only filtering, stale fallback, reload, and state unchanged checks passed"
                        : "enhanced filter smoke did not meet read/filter/reload/state expectations",
                    Folder = fullFolder,
                    ProjectRoot = smokeRoot,
                    JobsPath = jobsPath,
                    ResolvedJobsPath = resolvedJobsPath,
                    ValidSourceName = Path.GetFileName(validSource),
                    StaleSourceName = Path.GetFileName(staleSource),
                    FailedSourceName = Path.GetFileName(failedSource),
                    ValidOutputPath = validOutput,
                    MissingOutputPath = missingOutput,
                    AllCount = allCount,
                    JobsRead = win.EnhancementJobsReadForSmoke,
                    CandidateCount = win.EnhancedCandidateCountForSmoke,
                    EnhancedStoreCount = win.EnhancedStoreCountForSmoke,
                    SelectedValid = selectedValid,
                    SelectedValidEnhanced = selectedValidEnhanced,
                    SelectedOutputPath = selectedOutput,
                    EnhancedFilteredCount = enhancedFilteredCount,
                    ValidVisible = validVisible,
                    StaleVisible = staleVisible,
                    FailedVisible = failedVisible,
                    AfterClearCount = afterClearCount,
                    ReloadFilteredCount = reloadFilteredCount,
                    ReloadValidVisible = reloadValidVisible,
                    EnhancementStateUnchanged = enhancementStateUnchanged,
                    ReadOk = win.EnhancementReadOkForSmoke,
                    ReadError = win.EnhancementReadErrorForSmoke,
                };
            }
            catch (Exception ex)
            {
                win.Close();
                result = new EnhancedFilterSmokeResult { Ok = false, Message = ex.Message, Folder = fullFolder, ProjectRoot = smokeRoot, JobsPath = jobsPath };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteEnhancedFilterSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private static async Task<SharedSeenSmokeResult> RunSharedSeenSmokeAsync(
        MainWindow first,
        string fullFolder,
        string smokeRoot,
        string[] fixtureNames)
    {
        string sharedSeenPath = Path.Combine(smokeRoot, ".cache", "seen.json");
        string legacySeenPath = Path.Combine(smokeRoot, ".cache", "wpf-seen.json");
        string sharedSeedName = fixtureNames[0];
        string legacySeedName = fixtureNames[1];
        string targetName = fixtureNames[2];
        string sharedSeedPath = Path.Combine(fullFolder, sharedSeedName);
        string legacySeedPath = Path.Combine(fullFolder, legacySeedName);
        string targetPath = Path.Combine(fullFolder, targetName);

        WriteSeenSeed(sharedSeenPath, sharedSeedPath);
        WriteSeenSeed(legacySeenPath, legacySeedPath);
        string legacyBefore = File.ReadAllText(legacySeenPath);

        await first.LoadFolderAsync(fullFolder);
        first.SetSearchQuery("", persist: false);
        string resolvedSeenPath = first.SeenPathForSmoke;
        int initialSeenStoreCount = first.SeenStoreCountForSmoke;
        bool selectedTarget = first.SelectFileNameForSmoke(targetName);
        int seenStoreAfterSelection = first.SeenStoreCountForSmoke;
        bool targetSelectedSeen = first.IsFileUnseenForSmoke(targetName) == false;
        first.SetUnseenOnlyFilterForSmoke(true);
        int filteredAfterSelection = first.FilteredCountForSmoke;
        first.Close();

        var sharedMap = ReadSeenMap(sharedSeenPath);
        var legacyMap = ReadSeenMap(legacySeenPath);
        string legacyAfter = File.ReadAllText(legacySeenPath);
        bool sharedHasSharedSeed = sharedMap.ContainsKey(NormalizeFavoritePath(sharedSeedPath));
        bool sharedHasLegacySeed = sharedMap.ContainsKey(NormalizeFavoritePath(legacySeedPath));
        bool sharedHasTarget = sharedMap.ContainsKey(NormalizeFavoritePath(targetPath));
        bool legacyPreserved = string.Equals(legacyBefore, legacyAfter, StringComparison.Ordinal)
            && legacyMap.Count == 1
            && legacyMap.ContainsKey(NormalizeFavoritePath(legacySeedPath))
            && !legacyMap.ContainsKey(NormalizeFavoritePath(sharedSeedPath))
            && !legacyMap.ContainsKey(NormalizeFavoritePath(targetPath));

        var second = HiddenWindow();
        second.Show();
        second.SuppressStatePersistence();
        await second.LoadFolderAsync(fullFolder);
        second.SetSearchQuery("", persist: false);
        bool reloadSharedSeedSeen = second.IsFileUnseenForSmoke(sharedSeedName) == false;
        bool reloadLegacySeedSeen = second.IsFileUnseenForSmoke(legacySeedName) == false;
        bool reloadTargetSeen = second.IsFileUnseenForSmoke(targetName) == false;
        int reloadSeenStoreCount = second.SeenStoreCountForSmoke;
        second.Close();

        MalformedSeenSmokeCaseResult malformedLegacy = await RunMalformedSeenSmokeCaseAsync(
            fullFolder,
            smokeRoot,
            "malformed-legacy",
            fixtureNames,
            malformedShared: false);
        MalformedSeenSmokeCaseResult malformedShared = await RunMalformedSeenSmokeCaseAsync(
            fullFolder,
            smokeRoot,
            "malformed-shared",
            fixtureNames,
            malformedShared: true);

        bool ok = string.Equals(Path.GetFullPath(resolvedSeenPath), Path.GetFullPath(sharedSeenPath), StringComparison.OrdinalIgnoreCase)
            && selectedTarget
            && targetSelectedSeen
            && initialSeenStoreCount >= 2
            && seenStoreAfterSelection >= initialSeenStoreCount
            && filteredAfterSelection >= 0
            && sharedHasSharedSeed
            && sharedHasLegacySeed
            && sharedHasTarget
            && legacyPreserved
            && reloadSharedSeedSeen
            && reloadLegacySeedSeen
            && reloadTargetSeen
            && reloadSeenStoreCount >= sharedMap.Count
            && malformedLegacy.Ok
            && malformedShared.Ok;

        return new SharedSeenSmokeResult
        {
            Ok = ok,
            Message = ok ? "shared seen default path, legacy additive merge, shared-only write, reload, and malformed fail-safe passed" : "shared seen smoke did not meet expected policy",
            Folder = fullFolder,
            ProjectRoot = smokeRoot,
            SharedSeenPath = sharedSeenPath,
            LegacySeenPath = legacySeenPath,
            ResolvedSeenPath = resolvedSeenPath,
            SharedSeedName = sharedSeedName,
            LegacySeedName = legacySeedName,
            TargetName = targetName,
            InitialSeenStoreCount = initialSeenStoreCount,
            SeenStoreCountAfterSelection = seenStoreAfterSelection,
            SharedMapCountAfterSelection = sharedMap.Count,
            LegacyMapCountAfterSelection = legacyMap.Count,
            SharedHasSharedSeed = sharedHasSharedSeed,
            SharedHasLegacySeed = sharedHasLegacySeed,
            SharedHasTarget = sharedHasTarget,
            LegacyPreserved = legacyPreserved,
            ReloadSharedSeedSeen = reloadSharedSeedSeen,
            ReloadLegacySeedSeen = reloadLegacySeedSeen,
            ReloadTargetSeen = reloadTargetSeen,
            ReloadSeenStoreCount = reloadSeenStoreCount,
            MalformedLegacy = malformedLegacy,
            MalformedShared = malformedShared,
        };
    }

    private static async Task<MalformedSeenSmokeCaseResult> RunMalformedSeenSmokeCaseAsync(
        string fullFolder,
        string parentRoot,
        string caseName,
        string[] fixtureNames,
        bool malformedShared)
    {
        string caseRoot = Path.Combine(parentRoot, caseName);
        PrepareSharedSeenSmokeEnvironment(caseRoot);

        string sharedSeenPath = Path.Combine(caseRoot, ".cache", "seen.json");
        string legacySeenPath = Path.Combine(caseRoot, ".cache", "wpf-seen.json");
        string sharedSeedName = fixtureNames[0];
        string legacySeedName = fixtureNames[1];
        string targetName = fixtureNames[2];
        string sharedSeedPath = Path.Combine(fullFolder, sharedSeedName);
        string legacySeedPath = Path.Combine(fullFolder, legacySeedName);
        string targetPath = Path.Combine(fullFolder, targetName);
        const string malformedJson = "[";

        if (malformedShared)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sharedSeenPath)!);
            File.WriteAllText(sharedSeenPath, malformedJson);
            WriteSeenSeed(legacySeenPath, legacySeedPath);
        }
        else
        {
            WriteSeenSeed(sharedSeenPath, sharedSeedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(legacySeenPath)!);
            File.WriteAllText(legacySeenPath, malformedJson);
        }

        string sharedBefore = File.ReadAllText(sharedSeenPath);
        string legacyBefore = File.ReadAllText(legacySeenPath);

        var window = HiddenWindow();
        window.Show();
        window.SuppressStatePersistence();
        await window.LoadFolderAsync(fullFolder);
        window.SetSearchQuery("", persist: false);
        string resolvedSeenPath = window.SeenPathForSmoke;
        bool selectedTarget = window.SelectFileNameForSmoke(targetName);
        bool targetStillUnseen = window.IsFileUnseenForSmoke(targetName) == true;
        bool validSeedLoaded = malformedShared
            ? window.IsFileUnseenForSmoke(legacySeedName) == false
            : window.IsFileUnseenForSmoke(sharedSeedName) == false;
        int seenStoreCount = window.SeenStoreCountForSmoke;
        window.Close();

        string sharedAfter = File.ReadAllText(sharedSeenPath);
        string legacyAfter = File.ReadAllText(legacySeenPath);
        bool sharedPreserved = string.Equals(sharedBefore, sharedAfter, StringComparison.Ordinal);
        bool legacyPreserved = string.Equals(legacyBefore, legacyAfter, StringComparison.Ordinal);
        bool targetNotPersisted = malformedShared
            ? !ReadSeenFlag(legacySeenPath, targetPath)
            : !ReadSeenFlag(sharedSeenPath, targetPath);
        bool ok = string.Equals(Path.GetFullPath(resolvedSeenPath), Path.GetFullPath(sharedSeenPath), StringComparison.OrdinalIgnoreCase)
            && selectedTarget
            && targetStillUnseen
            && targetNotPersisted
            && validSeedLoaded
            && sharedPreserved
            && legacyPreserved;

        return new MalformedSeenSmokeCaseResult(
            ok,
            caseName,
            malformedShared,
            resolvedSeenPath,
            selectedTarget,
            targetStillUnseen,
            targetNotPersisted,
            validSeedLoaded,
            sharedPreserved,
            legacyPreserved,
            seenStoreCount);
    }

    private void CaptureScrollRealizationSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string query = ArgValue(args, "--query") ?? "";
        int advanceCount = ArgInt(args, "--advance-count", 16);
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteScrollRealizationSmokeResult(
                resultPath,
                new ScrollRealizationSmokeResult(false, "missing required --folder", folder, query, advanceCount, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, false));
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.SuppressStatePersistence();

        win.Dispatcher.InvokeAsync(async () =>
        {
            ScrollRealizationSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSearchQuery(query, persist: false);

                int total = win.FilteredCountForSmoke;
                int initialRealized = win.GridRealizedCountForSmoke;
                int initialDeferred = win.GridDeferredCountForSmoke;
                int initialStart = win.GridWindowStartIndexForSmoke;
                int initialEnd = win.GridWindowEndIndexForSmoke;
                int maxAllowed = win.GridMaxRealizationCountForSmoke;
                int maxObserved = initialRealized;
                int advancesMoved = 0;

                for (int i = 0; i < advanceCount; i++)
                {
                    if (win.RealizeNextGridBatchForSmoke())
                        advancesMoved++;
                    maxObserved = Math.Max(maxObserved, win.GridRealizedCountForSmoke);
                }

                int finalRealized = win.GridRealizedCountForSmoke;
                int finalDeferred = win.GridDeferredCountForSmoke;
                int finalStart = win.GridWindowStartIndexForSmoke;
                int finalEnd = win.GridWindowEndIndexForSmoke;
                bool movedBack = win.RealizePreviousGridBatchForSmoke();
                int afterBackStart = win.GridWindowStartIndexForSmoke;
                int afterBackRealized = win.GridRealizedCountForSmoke;

                bool bounded = maxObserved <= maxAllowed && finalRealized <= maxAllowed && afterBackRealized <= maxAllowed;
                bool ok = total > maxAllowed
                    && initialRealized < total
                    && advancesMoved > 0
                    && finalStart > initialStart
                    && finalEnd <= total
                    && finalDeferred > 0
                    && bounded
                    && movedBack
                    && afterBackStart < finalStart;

                result = new ScrollRealizationSmokeResult(
                    ok,
                    ok ? "scroll advance keeps grid realization bounded and moves the window forward/back" : "scroll realization guard did not stay bounded or did not move as expected",
                    folder,
                    query,
                    advanceCount,
                    total,
                    initialRealized,
                    initialDeferred,
                    initialStart,
                    initialEnd,
                    advancesMoved,
                    maxObserved,
                    bounded,
                    finalRealized,
                    finalDeferred,
                    finalStart,
                    finalEnd,
                    maxAllowed,
                    movedBack);
            }
            catch (Exception ex)
            {
                result = new ScrollRealizationSmokeResult(false, ex.Message, folder, query, advanceCount, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, false);
            }

            win.Close();
            WriteScrollRealizationSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureGridRealizationSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string query = ArgValue(args, "--query") ?? "";
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteGridRealizationSmokeResult(
                resultPath,
                new GridRealizationSmokeResult(false, "missing required --folder", folder, query, 0, 0, 0, false, 0, 0));
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.SuppressStatePersistence();

        win.Dispatcher.InvokeAsync(async () =>
        {
            GridRealizationSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSearchQuery(query, persist: false);

                int total = win.FilteredCountForSmoke;
                int initial = win.GridRealizedCountForSmoke;
                int initialDeferred = win.GridDeferredCountForSmoke;
                bool moved = win.RealizeNextGridBatchForSmoke();
                int afterBatch = win.GridRealizedCountForSmoke;
                int afterDeferred = win.GridDeferredCountForSmoke;

                bool ok = total > initial
                    && initialDeferred > 0
                    && moved
                    && afterBatch > initial
                    && afterBatch <= total
                    && afterDeferred < initialDeferred;

                result = new GridRealizationSmokeResult(
                    ok,
                    ok ? "grid starts with a bounded realized batch and appends the next batch on demand" : "grid realization batch did not advance as expected",
                    folder,
                    query,
                    total,
                    initial,
                    initialDeferred,
                    moved,
                    afterBatch,
                    afterDeferred);
            }
            catch (Exception ex)
            {
                result = new GridRealizationSmokeResult(false, ex.Message, folder, query, 0, 0, 0, false, 0, 0);
            }

            win.Close();
            WriteGridRealizationSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureModalNavigationSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? statePath = ArgValue(args, "--state-path");
        string selectName = ArgValue(args, "--select-name") ?? "";
        string query = ArgValue(args, "--query") ?? "";
        int selectIndex = ArgInt(args, "--select-index", 1);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(statePath))
        {
            WriteModalNavigationSmokeResult(
                resultPath,
                new ModalNavigationSmokeResult(false, "missing required --folder or --state-path", statePath, folder, query, selectName, selectIndex, false, null, null, false, null, null, false, null, null, false, null, null));
            Shutdown(1);
            return;
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            ModalNavigationSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                if (!string.IsNullOrWhiteSpace(query))
                    win.SetSearchQuery(query);

                bool selected = !string.IsNullOrWhiteSpace(selectName)
                    ? win.SelectFileNameForSmoke(selectName)
                    : win.SelectIndexForSmoke(selectIndex);
                win.ShowModalForShot();
                string? startName = win.SelectedFileNameForSmoke;
                string? startPath = win.SelectedPathForSmoke;
                bool movedNext = win.NavigateModalForSmoke(1);
                string? nextName = win.SelectedFileNameForSmoke;
                string? nextPath = win.SelectedPathForSmoke;
                bool movedPrevious = win.NavigateModalForSmoke(-1);
                string? previousName = win.SelectedFileNameForSmoke;
                string? previousPath = win.SelectedPathForSmoke;
                var persisted = ReadPersistedState(statePath);
                string? persistedName = persisted?.SelectedPath is null ? null : Path.GetFileName(persisted.SelectedPath);

                bool ok = selected
                    && win.ModalVisibleForSmoke
                    && movedNext
                    && movedPrevious
                    && !string.IsNullOrWhiteSpace(startPath)
                    && !string.Equals(startPath, nextPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(startPath, previousPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(previousPath, persisted?.SelectedPath, StringComparison.OrdinalIgnoreCase);

                result = new ModalNavigationSmokeResult(
                    ok,
                    ok ? "modal navigation moved next, returned previous, and persisted selected path" : "modal navigation did not keep selection/state in sync",
                    statePath,
                    folder,
                    query,
                    selectName,
                    selectIndex,
                    selected,
                    startName,
                    startPath,
                    movedNext,
                    nextName,
                    nextPath,
                    movedPrevious,
                    previousName,
                    previousPath,
                    win.ModalVisibleForSmoke,
                    persistedName,
                    persisted?.SelectedPath);
            }
            catch (Exception ex)
            {
                result = new ModalNavigationSmokeResult(false, ex.Message, statePath, folder, query, selectName, selectIndex, false, null, null, false, null, null, false, null, null, false, null, null);
            }

            win.Close();
            WriteModalNavigationSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureStateSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? statePath = ArgValue(args, "--state-path");
        string selectName = ArgValue(args, "--select-name") ?? "";
        string query = ArgValue(args, "--query") ?? "";
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(statePath))
        {
            WriteStateSmokeResult(
                resultPath,
                new StateSmokeResult(false, "missing required --folder or --state-path", statePath, selectName, null, null, null, null, query, null));
            Shutdown(1);
            return;
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            StateSmokeResult result;
            try
            {
                await first.LoadFolderAsync(folder);
                if (!string.IsNullOrWhiteSpace(query))
                    first.SetSearchQuery(query);

                bool selected = !string.IsNullOrWhiteSpace(selectName) && first.SelectFileNameForSmoke(selectName);
                string? expectedPath = first.SelectedPathForSmoke;
                string? expectedName = first.SelectedFileNameForSmoke;
                first.Close();

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(folder);
                string? restoredPath = second.SelectedPathForSmoke;
                string? restoredName = second.SelectedFileNameForSmoke;
                string restoredQuery = second.SearchQueryForSmoke;

                bool restored = selected
                    && !string.IsNullOrWhiteSpace(expectedPath)
                    && string.Equals(expectedPath, restoredPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(query, restoredQuery, StringComparison.Ordinal);

                result = new StateSmokeResult(
                    restored,
                    restored ? "restored selected path and search query" : "selection or search did not restore",
                    second.StatePathForSmoke,
                    selectName,
                    expectedName,
                    expectedPath,
                    restoredName,
                    restoredPath,
                    query,
                    restoredQuery);
                second.Close();
            }
            catch (Exception ex)
            {
                result = new StateSmokeResult(false, ex.Message, statePath, selectName, null, null, null, null, query, null);
            }

            WriteStateSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private static MainWindow HiddenWindow()
        => new()
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            Width = 1280,
            Height = 820,
        };

    private static string? ArgValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int ArgInt(string[] args, string name, int fallback)
    {
        var value = ArgValue(args, name);
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }

    private static int SelectFavoriteLevel(MainWindow window, string fileName)
        => window.SelectFileNameForSmoke(fileName) ? window.SelectedFavoriteLevelForSmoke : -1;

    private static string[] GetSmokeImageFileNames(string folder)
    {
        string[] extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];
        return Directory.Exists(folder)
            ? Directory
                .EnumerateFiles(folder)
                .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
    }

    private static void PrepareSharedSeenSmokeEnvironment(string projectRoot)
    {
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "local-native"));
        Directory.CreateDirectory(Path.Combine(projectRoot, ".cache"));
        File.WriteAllText(Path.Combine(projectRoot, "project.toml"), "# shared seen smoke root");
        Environment.CurrentDirectory = projectRoot;
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", null);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", Path.Combine(projectRoot, ".cache", "favorites.json"));
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", Path.Combine(projectRoot, ".cache", "state.json"));
    }

    private static void WriteFavoriteSeed(string favoritesPath, string preservedPath, int level)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(favoritesPath))!);
        var favorites = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeFavoritePath(preservedPath)] = Math.Clamp(level, 1, 5),
        };
        var json = System.Text.Json.JsonSerializer.Serialize(favorites, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(favoritesPath, json);
    }

    private static void WritePvuFavoriteImportFixture(
        string browserStatePath,
        string importedPath,
        string preservedPath,
        string clampedName,
        string zeroPath,
        string unmatchedPath,
        string pvuFavoriteDefaultPath,
        string pvuFavoriteExplicitName,
        string pvuFavoriteUnmatchedPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(browserStatePath))!);
        var pvuFavLevels = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeFavoritePath(importedPath)] = 4,
            [NormalizeFavoritePath(preservedPath)] = 5,
            [clampedName] = 8,
            [NormalizeFavoritePath(zeroPath)] = 0,
            [NormalizeFavoritePath(unmatchedPath)] = 3,
            ["invalid-level"] = "bad",
            [""] = 2,
        };
        var pvuFavorites = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeFavoritePath(pvuFavoriteDefaultPath)] = true,
            [NormalizeFavoritePath(importedPath)] = true,
            [NormalizeFavoritePath(preservedPath)] = true,
            [pvuFavoriteExplicitName] = 2,
            [NormalizeFavoritePath(zeroPath)] = false,
            [NormalizeFavoritePath(pvuFavoriteUnmatchedPath)] = true,
            ["invalid-favorite"] = new { unsupported = true },
            [""] = true,
        };
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["browserLocalStorage"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["pvu_fav_levels"] = pvuFavLevels,
                ["pvu_favorites"] = pvuFavorites,
            },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(browserStatePath, json);
    }

    private static void WritePvuFavoritesListImportFixture(
        string browserStatePath,
        string importedPath,
        string preservedPath,
        string unmatchedPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(browserStatePath))!);
        object?[] pvuFavorites =
        [
            NormalizeFavoritePath(importedPath),
            NormalizeFavoritePath(preservedPath),
            NormalizeFavoritePath(unmatchedPath),
            "",
            new { unsupported = true },
        ];
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["browserLocalStorage"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["pvu_favorites"] = pvuFavorites,
            },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(browserStatePath, json);
    }

    private static void WritePvuSeenImagesImportFixture(
        string browserStatePath,
        string preservedPath,
        string importedPath,
        string importedByName,
        string zeroPath,
        string unmatchedPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(browserStatePath))!);
        var pvuSeenImages = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeFavoritePath(preservedPath)] = true,
            [NormalizeFavoritePath(importedPath)] = true,
            [importedByName] = 1,
            [NormalizeFavoritePath(zeroPath)] = false,
            [NormalizeFavoritePath(unmatchedPath)] = true,
            ["invalid-seen"] = new { unsupported = true },
            [""] = true,
        };
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["browserLocalStorage"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["pvu_seen_images"] = pvuSeenImages,
            },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(browserStatePath, json);
    }

    private static void WriteEnhancedJobsFixture(
        string jobsPath,
        string validSourcePath,
        string validOutputPath,
        string staleSourcePath,
        string missingOutputPath,
        string failedSourcePath,
        string missingSourcePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jobsPath))!);
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["version"] = 1,
            ["jobs"] = new object?[]
            {
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "enhanced-ok",
                    ["sourceId"] = validSourcePath,
                    ["sourcePath"] = validSourcePath,
                    ["status"] = "succeeded",
                    ["progress"] = 100,
                    ["outputPath"] = validOutputPath,
                    ["createdAt"] = DateTime.UtcNow.ToString("o"),
                    ["updatedAt"] = DateTime.UtcNow.ToString("o"),
                },
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "stale-output",
                    ["sourceId"] = staleSourcePath,
                    ["sourcePath"] = staleSourcePath,
                    ["status"] = "succeeded",
                    ["progress"] = 100,
                    ["outputPath"] = missingOutputPath,
                },
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "failed-job",
                    ["sourceId"] = failedSourcePath,
                    ["sourcePath"] = failedSourcePath,
                    ["status"] = "failed",
                    ["progress"] = 100,
                    ["outputPath"] = validOutputPath,
                },
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "missing-source",
                    ["sourceId"] = missingSourcePath,
                    ["sourcePath"] = missingSourcePath,
                    ["status"] = "succeeded",
                    ["progress"] = 100,
                    ["outputPath"] = validOutputPath,
                },
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "missing-output-field",
                    ["sourceId"] = failedSourcePath,
                    ["sourcePath"] = failedSourcePath,
                    ["status"] = "succeeded",
                    ["progress"] = 100,
                },
                "unsupported-entry",
            },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jobsPath, json);
    }

    private static void WriteViewerStateSeed(string statePath, string folder, string selectedPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(statePath))!);
        var state = new ViewerState
        {
            LastFolder = folder,
            SearchQuery = "",
            SelectedPath = selectedPath,
            CardWidth = 190,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(statePath, json);
    }

    private static ViewerState? ReadPersistedState(string path)
    {
        try
        {
            return File.Exists(path)
                ? System.Text.Json.JsonSerializer.Deserialize<ViewerState>(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static int ReadFavoriteLevel(string favoritesPath, string selectedPath)
    {
        try
        {
            if (!File.Exists(favoritesPath))
                return 0;

            string normalizedSelected = NormalizeFavoritePath(selectedPath);
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(favoritesPath));
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return 0;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(NormalizeFavoritePath(property.Name), normalizedSelected, StringComparison.OrdinalIgnoreCase))
                    continue;

                int level = 0;
                if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                    property.Value.TryGetInt32(out level);
                else if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    int.TryParse(property.Value.GetString(), out level);

                return Math.Clamp(level, 0, 5);
            }
        }
        catch
        {
        }

        return 0;
    }

    private static bool ReadSeenFlag(string seenPath, string selectedPath)
    {
        try
        {
            if (!File.Exists(seenPath))
                return false;

            string normalizedSelected = NormalizeFavoritePath(selectedPath);
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(seenPath));
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(NormalizeFavoritePath(property.Name), normalizedSelected, StringComparison.OrdinalIgnoreCase))
                    continue;

                return property.Value.ValueKind == System.Text.Json.JsonValueKind.True
                    || (property.Value.ValueKind == System.Text.Json.JsonValueKind.Number && property.Value.TryGetInt32(out int numeric) && numeric != 0)
                    || (property.Value.ValueKind == System.Text.Json.JsonValueKind.String && bool.TryParse(property.Value.GetString(), out bool parsed) && parsed);
            }
        }
        catch
        {
        }

        return false;
    }

    private static Dictionary<string, bool> ReadSeenMap(string seenPath)
    {
        var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(seenPath))
                return seen;

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(seenPath));
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return seen;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                bool truthy = property.Value.ValueKind == System.Text.Json.JsonValueKind.True
                    || (property.Value.ValueKind == System.Text.Json.JsonValueKind.Number && property.Value.TryGetInt32(out int numeric) && numeric != 0)
                    || (property.Value.ValueKind == System.Text.Json.JsonValueKind.String && bool.TryParse(property.Value.GetString(), out bool parsed) && parsed);
                if (truthy)
                    seen[NormalizeFavoritePath(property.Name)] = true;
            }
        }
        catch
        {
        }

        return seen;
    }

    private static void WriteSeenSeed(string seenPath, params string[] paths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(seenPath))!);
        var seen = paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeFavoritePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static path => path, static _ => true, StringComparer.OrdinalIgnoreCase);
        var json = System.Text.Json.JsonSerializer.Serialize(seen, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(seenPath, json);
    }

    private static void WriteSharedRecentSeed(string recentPath, string lastFolder, string preservedFolder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(recentPath))!);
        var seed = new
        {
            version = 1,
            lastFolderSet = new[] { NormalizeFavoritePath(lastFolder) },
            recentFolderSets = new[]
            {
                new[] { NormalizeFavoritePath(lastFolder) },
                new[] { NormalizeFavoritePath(preservedFolder) },
            },
            updatedAtUtc = "2026-07-09T00:00:00Z",
        };
        var json = System.Text.Json.JsonSerializer.Serialize(seed, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(recentPath, json);
    }

    private static void WriteSharedRecentSetSeed(string recentPath, IReadOnlyList<string> lastFolderSet, IReadOnlyList<string> preservedFolderSet)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(recentPath))!);
        var seed = new
        {
            version = 1,
            lastFolderSet = NormalizeFolderSetForSmoke(lastFolderSet),
            recentFolderSets = new[]
            {
                NormalizeFolderSetForSmoke(lastFolderSet),
                NormalizeFolderSetForSmoke(preservedFolderSet),
            },
            updatedAtUtc = "2026-07-10T00:00:00Z",
        };
        var json = System.Text.Json.JsonSerializer.Serialize(seed, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(recentPath, json);
    }

    private static string PrepareSecondFolderFixture(string sourceFolder, string smokeRoot)
    {
        string target = Path.Combine(smokeRoot, "pasted-folder");
        Directory.CreateDirectory(target);
        int index = 0;
        foreach (string file in Directory.EnumerateFiles(sourceFolder).Where(IsSmokeImageFile).Take(2))
        {
            string targetFile = Path.Combine(target, $"copy-{index:00}-{Path.GetFileName(file)}");
            File.Copy(file, targetFile, overwrite: true);
            index++;
        }
        return target;
    }

    private static string PrepareSortSmokeFolder(string sourceFolder, string[] fixtureNames, string smokeRoot)
    {
        string target = Path.Combine(smokeRoot, "sort-folder");
        Directory.CreateDirectory(target);
        var inputs = new[]
        {
            new
            {
                SourceName = fixtureNames[0],
                TargetName = "alpha-sort" + Path.GetExtension(fixtureNames[0]).ToLowerInvariant(),
                ModifiedUtc = new DateTime(2026, 7, 10, 11, 0, 0, DateTimeKind.Utc),
            },
            new
            {
                SourceName = fixtureNames[1],
                TargetName = "bravo-sort" + Path.GetExtension(fixtureNames[1]).ToLowerInvariant(),
                ModifiedUtc = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc),
            },
            new
            {
                SourceName = fixtureNames[2],
                TargetName = "charlie-sort" + Path.GetExtension(fixtureNames[2]).ToLowerInvariant(),
                ModifiedUtc = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc),
            },
        };

        foreach (var input in inputs)
        {
            string source = Path.Combine(sourceFolder, input.SourceName);
            string destination = Path.Combine(target, input.TargetName);
            File.Copy(source, destination, overwrite: true);
            File.SetLastWriteTimeUtc(destination, input.ModifiedUtc);
        }

        return target;
    }

    private static int CountDirectSmokeImages(string folder)
        => Directory.Exists(folder) ? Directory.EnumerateFiles(folder).Count(IsSmokeImageFile) : 0;

    private static bool IsSmokeImageFile(string file)
    {
        string[] extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];
        return extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
    }

    private static bool SameFolderSet(IEnumerable<string> actual, IEnumerable<string> expected)
        => string.Equals(FormatFolderSetForSmoke(actual), FormatFolderSetForSmoke(expected), StringComparison.OrdinalIgnoreCase);

    private static bool SameNameOrder(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
        => actual.Count >= expected.Count && expected
            .Select((name, index) => string.Equals(actual[index], name, StringComparison.OrdinalIgnoreCase))
            .All(static matches => matches);

    private static bool SnapshotContainsFolderSet(SharedRecentSmokeSnapshot snapshot, IEnumerable<string> folders)
    {
        string expected = FormatFolderSetForSmoke(folders);
        return snapshot.RecentFolderSets.Any(set => string.Equals(FormatFolderSetForSmoke(set), expected, StringComparison.OrdinalIgnoreCase));
    }

    private static SharedRecentSmokeSnapshot ReadSharedRecentSnapshot(string recentPath)
    {
        try
        {
            if (!File.Exists(recentPath))
                return new SharedRecentSmokeSnapshot();

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(recentPath));
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return new SharedRecentSmokeSnapshot();

            var lastFolderSet = document.RootElement.TryGetProperty("lastFolderSet", out var lastElement)
                ? NormalizeFolderSetForSmoke(lastElement)
                : [];
            var recentFolderSets = new List<List<string>>();
            if (document.RootElement.TryGetProperty("recentFolderSets", out var recentElement) &&
                recentElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in recentElement.EnumerateArray())
                {
                    var folderSet = NormalizeFolderSetForSmoke(item);
                    if (folderSet.Count > 0)
                        recentFolderSets.Add(folderSet);
                }
            }

            return new SharedRecentSmokeSnapshot
            {
                Ok = true,
                LastFolderSet = lastFolderSet,
                RecentFolderSets = recentFolderSets,
            };
        }
        catch
        {
            return new SharedRecentSmokeSnapshot();
        }
    }

    private static List<string> NormalizeFolderSetForSmoke(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            return NormalizeFolderSetForSmoke(element.EnumerateArray()
                .Where(static item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(static item => item.GetString() ?? ""));

        if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            return NormalizeFolderSetForSmoke(
                (element.GetString() ?? "").Split(["\r\n", "\n"], StringSplitOptions.None));

        return [];
    }

    private static List<string> NormalizeFolderSetForSmoke(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();
        foreach (string raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string path = NormalizeFavoritePath(raw.Trim());
            if (seen.Add(path))
                normalized.Add(path);
        }

        return normalized;
    }

    private static string NormalizeFavoritePath(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : path;
        }
        catch
        {
            return path;
        }
    }

    private static void WriteStateSmokeResult(string path, StateSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteModalNavigationSmokeResult(string path, ModalNavigationSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteGridRealizationSmokeResult(string path, GridRealizationSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteFavoriteSmokeResult(string path, FavoriteSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteFavoriteLevelSmokeResult(string path, FavoriteLevelSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteFavoriteImportSmokeResult(string path, FavoriteImportSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteSeenSmokeResult(string path, SeenSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteSeenImportSmokeResult(string path, SeenImportSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteSharedSeenSmokeResult(string path, SharedSeenSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteSharedRecentSmokeResult(string path, SharedRecentSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteFolderSetSmokeResult(string path, FolderSetSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteGridZoomSmokeResult(string path, GridZoomSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteDisplayStyleSmokeResult(string path, DisplayStyleSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteSortSmokeResult(string path, SortSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteEnhancedFilterSmokeResult(string path, EnhancedFilterSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteStartupSmokeResult(string path, StartupSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteScrollRealizationSmokeResult(string path, ScrollRealizationSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private sealed record StateSmokeResult(
        bool Ok,
        string Message,
        string? StatePath,
        string SelectName,
        string? ExpectedName,
        string? ExpectedPath,
        string? RestoredName,
        string? RestoredPath,
        string Query,
        string? RestoredQuery);

    private sealed record ModalNavigationSmokeResult(
        bool Ok,
        string Message,
        string? StatePath,
        string? Folder,
        string Query,
        string SelectName,
        int SelectIndex,
        bool Selected,
        string? StartName,
        string? StartPath,
        bool MovedNext,
        string? NextName,
        string? NextPath,
        bool MovedPrevious,
        string? PreviousName,
        string? PreviousPath,
        bool ModalVisible,
        string? PersistedName,
        string? PersistedPath);

    private sealed record FavoriteSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string? FavoritesPath,
        int SelectIndex,
        bool Selected,
        string? SelectedName,
        string? SelectedPath,
        int BeforeLevel,
        bool Toggled,
        int AfterLevel,
        int FilteredAfterToggle,
        int StoreCountAfterToggle,
        int PersistedLevel,
        bool ReloadSelected,
        int ReloadedLevel,
        int ReloadedFilteredCount);

    private sealed record FavoriteLevelSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string? FavoritesPath,
        int SelectIndex,
        bool Selected,
        string? SelectedName,
        string? SelectedPath,
        int BeforeLevel,
        bool IncreasedOnce,
        int AfterIncreaseOnce,
        bool IncreasedTwice,
        int AfterIncreaseTwice,
        bool DecreasedOnce,
        int AfterDecreaseOnce,
        bool Cleared,
        int AfterClear,
        int StoreCountAfterClear,
        bool AdjustedToFinalLevel,
        int IncreaseStepsAfterClear,
        int FinalLevel,
        int FilteredAfterFinal,
        int StoreCountAfterFinal,
        int PersistedLevel,
        bool ReloadSelected,
        int ReloadedLevel,
        int ReloadedFilteredCount);

    private sealed record FavoriteImportSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string? FavoritesPath,
        string? BrowserStatePath,
        string? ImportedPath,
        string? PreservedPath,
        string? ClampedPath,
        string? ZeroPath,
        FavoriteImportSummary? ImportSummary,
        FavoriteImportSummary? PvuFavoritesImportSummary,
        FavoriteImportSummary? PvuFavoritesListImportSummary,
        int ImportedLevel,
        int PreservedLevel,
        int ClampedLevel,
        int ZeroLevel,
        int FilteredAfterImport,
        int StoreCountAfterImport,
        int PersistedImportedLevel,
        int PersistedPreservedLevel,
        int PersistedClampedLevel,
        int PersistedZeroLevel,
        bool ReloadSelected,
        int ReloadedImportedLevel,
        int ReloadedPreservedLevel,
        int ReloadedClampedLevel,
        int ReloadedZeroLevel,
        int ReloadedFilteredCount,
        Dictionary<string, int> PersistedLevels,
        Dictionary<string, int> ReloadedLevels);

    private sealed record SeenSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string? SeenPath,
        string? FavoritesPath,
        string? SelectedName,
        string? SelectedPath,
        int InitialUnseen,
        int UnseenAfterSelection,
        bool SelectedMarkedSeen,
        bool PersistedSeen,
        int UnseenOnlyAfterSelection,
        int SeenStoreCountAfterSelection,
        int FavoriteStoreCountAfterSelection,
        bool ReloadedSelected,
        int ReloadInitialUnseen,
        int ReloadUnseenAfterSelection,
        bool ReloadedSelectedSeen,
        int ReloadUnseenOnlyCount,
        int ReloadSeenStoreCount,
        int ReloadFavoriteStoreCount,
        long FirstLoadTotalMs,
        int FirstThumbnailsCompleted);

    private sealed class SeenImportSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? SeenPath { get; init; }
        public string? FavoritesPath { get; init; }
        public string? BrowserStatePath { get; init; }
        public string? PreservedName { get; init; }
        public string? ImportedName { get; init; }
        public string? ImportedByName { get; init; }
        public string? ZeroName { get; init; }
        public SeenImportSummary? ImportSummary { get; init; }
        public int PreImportSeenCount { get; init; }
        public int SeenStoreCountAfterImport { get; init; }
        public int PreImportUnseenCount { get; init; }
        public int UnseenAfterImport { get; init; }
        public int FilteredAfterImport { get; init; }
        public int FavoriteCountBefore { get; init; }
        public int FavoriteCountAfter { get; init; }
        public int ReloadSeenStoreCount { get; init; }
        public int ReloadUnseenCount { get; init; }
        public int ReloadFilteredCount { get; init; }
        public int ReloadFavoriteCount { get; init; }
        public bool PreservedPersistedSeen { get; init; }
        public bool ImportedPersistedSeen { get; init; }
        public bool ImportedByNamePersistedSeen { get; init; }
        public bool ZeroStillUnseen { get; init; }
        public bool ReloadImportedSeen { get; init; }
        public bool ReloadImportedByNameSeen { get; init; }
        public bool ReloadZeroStillUnseen { get; init; }
    }

    private sealed class SharedSeenSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? SharedSeenPath { get; init; }
        public string? LegacySeenPath { get; init; }
        public string? ResolvedSeenPath { get; init; }
        public string? SharedSeedName { get; init; }
        public string? LegacySeedName { get; init; }
        public string? TargetName { get; init; }
        public int InitialSeenStoreCount { get; init; }
        public int SeenStoreCountAfterSelection { get; init; }
        public int SharedMapCountAfterSelection { get; init; }
        public int LegacyMapCountAfterSelection { get; init; }
        public bool SharedHasSharedSeed { get; init; }
        public bool SharedHasLegacySeed { get; init; }
        public bool SharedHasTarget { get; init; }
        public bool LegacyPreserved { get; init; }
        public bool ReloadSharedSeedSeen { get; init; }
        public bool ReloadLegacySeedSeen { get; init; }
        public bool ReloadTargetSeen { get; init; }
        public int ReloadSeenStoreCount { get; init; }
        public MalformedSeenSmokeCaseResult? MalformedLegacy { get; init; }
        public MalformedSeenSmokeCaseResult? MalformedShared { get; init; }
    }

    private sealed class SharedRecentSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? SharedRecentPath { get; init; }
        public string? ResolvedRecentPath { get; init; }
        public string? ImportedCurrentFolder { get; init; }
        public string? WriteFolder { get; init; }
        public string? PreservedFolder { get; init; }
        public string? MalformedFolder { get; init; }
        public string? WriteCurrentFolder { get; init; }
        public List<string> LastFolderAfterWrite { get; init; } = [];
        public int RecentFolderSetCountAfterWrite { get; init; }
        public bool ImportedSharedLastFolder { get; init; }
        public bool WroteLastFolder { get; init; }
        public bool WriteFolderInRecent { get; init; }
        public bool AdditivePreserved { get; init; }
        public bool StatePreserved { get; init; }
        public string? ReloadedCurrentFolder { get; init; }
        public bool ReloadedLocalStateWins { get; init; }
        public bool MalformedPreserved { get; init; }
        public bool LocalStateStillSavedAfterMalformed { get; init; }
        public bool FavoritesUnchangedAfterWrite { get; init; }
        public bool SeenUnchangedAfterWrite { get; init; }
        public bool FavoritesUnchangedAfterMalformed { get; init; }
        public bool SeenUnchangedAfterMalformed { get; init; }
    }

    private sealed class FolderSetSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? SharedRecentPath { get; init; }
        public string? ResolvedRecentPath { get; init; }
        public string? SecondFolder { get; init; }
        public string? MissingFolder { get; init; }
        public string? CurrentFolder { get; init; }
        public List<string> ImportedLandingFolderSet { get; init; } = [];
        public List<string> LandingAfterPaste { get; init; } = [];
        public List<string> CurrentFolderSet { get; init; } = [];
        public List<string> StateLastFolderSet { get; init; } = [];
        public List<string> SharedLastFolderSet { get; init; } = [];
        public int PastedAdded { get; init; }
        public int RecentCountOnStartup { get; init; }
        public int FilteredAfterLoad { get; init; }
        public int ExpectedMinImages { get; init; }
        public int RecentFolderSetCountAfterWrite { get; init; }
        public bool ImportedSharedLastFolderSet { get; init; }
        public bool PastedSecondFolder { get; init; }
        public bool LoadedCurrentFolderSet { get; init; }
        public bool StateSavedFolderSet { get; init; }
        public bool WroteSharedLastFolderSet { get; init; }
        public bool WriteFolderSetInRecent { get; init; }
        public bool AdditivePreserved { get; init; }
        public bool FavoritesUnchanged { get; init; }
        public bool SeenPreserved { get; init; }
    }

    private sealed record GridZoomSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string? StatePath,
        int FilteredCount,
        double InitialWidth,
        double AfterButtonIn,
        double AfterButtonOut,
        double AfterShortcutIn,
        double AfterShortcutReset,
        double AfterWheelIn,
        bool ButtonIn,
        bool ButtonOut,
        bool ShortcutIn,
        bool ShortcutReset,
        bool WheelIn,
        bool WheelOutAndTileSync);

    private sealed record DisplayStyleSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string? ProjectRoot,
        string? StatePath,
        int Filtered,
        DisplayStyleMetrics? Standard,
        DisplayStyleMetrics? Compact,
        DisplayStyleMetrics? Poster,
        DisplayStyleMetrics? PosterZoomed,
        bool CompactChanged,
        bool PosterChanged,
        bool Zoomed,
        bool PersistedPoster);

    private sealed class SortSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? StatePath { get; init; }
        public List<string> ExpectedNewest { get; init; } = [];
        public List<string> ExpectedOldest { get; init; } = [];
        public List<string> ExpectedName { get; init; } = [];
        public List<string> NewestOrder { get; init; } = [];
        public List<string> OldestOrder { get; init; } = [];
        public List<string> NameOrder { get; init; } = [];
        public List<string> RestoredOrder { get; init; } = [];
        public bool SelectedAlpha { get; init; }
        public bool SelectionKeptAfterOldest { get; init; }
        public bool SelectionKeptAfterName { get; init; }
        public bool FilterPreserved { get; init; }
        public bool ModalOpened { get; init; }
        public bool ModalMovedNext { get; init; }
        public string? ModalNextName { get; init; }
        public string? PersistedSort { get; init; }
        public string? RestoredSort { get; init; }
    }

    private sealed class EnhancedFilterSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? JobsPath { get; init; }
        public string? ResolvedJobsPath { get; init; }
        public string? ValidSourceName { get; init; }
        public string? StaleSourceName { get; init; }
        public string? FailedSourceName { get; init; }
        public string? ValidOutputPath { get; init; }
        public string? MissingOutputPath { get; init; }
        public int AllCount { get; init; }
        public int JobsRead { get; init; }
        public int CandidateCount { get; init; }
        public int EnhancedStoreCount { get; init; }
        public bool SelectedValid { get; init; }
        public bool SelectedValidEnhanced { get; init; }
        public string? SelectedOutputPath { get; init; }
        public int EnhancedFilteredCount { get; init; }
        public bool ValidVisible { get; init; }
        public bool StaleVisible { get; init; }
        public bool FailedVisible { get; init; }
        public int AfterClearCount { get; init; }
        public int ReloadFilteredCount { get; init; }
        public bool ReloadValidVisible { get; init; }
        public bool EnhancementStateUnchanged { get; init; }
        public bool ReadOk { get; init; }
        public string? ReadError { get; init; }
    }

    private sealed record StartupSmokeResult(
        bool Ok,
        string Message,
        string Mode,
        long BeforeWindowMs,
        long WindowConstructMs,
        long ShowMs,
        long ReadyMs,
        DateTime CompletedAtUtc);

    private sealed class SharedRecentSmokeSnapshot
    {
        public bool Ok { get; init; }
        public List<string> LastFolderSet { get; init; } = [];
        public List<List<string>> RecentFolderSets { get; init; } = [];

        public bool ContainsFolderSet(string folder)
        {
            string expected = FormatFolderSetForSmoke([folder]);
            return RecentFolderSets.Any(set => string.Equals(FormatFolderSetForSmoke(set), expected, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string FormatFolderSetForSmoke(IEnumerable<string> paths)
        => string.Join("\n", NormalizeFolderSetForSmoke(paths));

    private sealed record MalformedSeenSmokeCaseResult(
        bool Ok,
        string CaseName,
        bool MalformedShared,
        string ResolvedSeenPath,
        bool SelectedTarget,
        bool TargetStillUnseen,
        bool TargetNotPersisted,
        bool ValidSeedLoaded,
        bool SharedPreserved,
        bool LegacyPreserved,
        int SeenStoreCount);

    private sealed record GridRealizationSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string Query,
        int Total,
        int InitialRealized,
        int InitialDeferred,
        bool BatchAdvanced,
        int AfterBatchRealized,
        int AfterBatchDeferred);

    private sealed record ScrollRealizationSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string Query,
        int AdvanceCount,
        int Total,
        int InitialRealized,
        int InitialDeferred,
        int InitialWindowStart,
        int InitialWindowEnd,
        int AdvancesMoved,
        int MaxObservedRealized,
        bool Bounded,
        int FinalRealized,
        int FinalDeferred,
        int FinalWindowStart,
        int FinalWindowEnd,
        int MaxAllowedRealized,
        bool MovedBack);
}
