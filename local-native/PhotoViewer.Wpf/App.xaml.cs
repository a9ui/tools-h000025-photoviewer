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
        base.OnStartup(e);

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

        int stateSmokeIdx = Array.IndexOf(e.Args, "--state-smoke");
        if (stateSmokeIdx >= 0 && stateSmokeIdx + 1 < e.Args.Length)
        {
            CaptureStateSmoke(e.Args[stateSmokeIdx + 1], e.Args);
            return;
        }

        new MainWindow().Show();
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
