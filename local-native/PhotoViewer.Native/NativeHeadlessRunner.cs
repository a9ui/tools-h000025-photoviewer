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

    public static int RunPvuStateMigrationSmoke()
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var runId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Environment.ProcessId}";
        var smokeRoot = Path.Combine(projectRoot, ".cache", "native-pvu-state-smoke", runId);
        var smokeNative = Path.Combine(smokeRoot, ".cache", "native");
        var browserRecentA = Path.Combine(smokeRoot, "browser-recent-a");
        var browserRecentB = Path.Combine(smokeRoot, "browser-recent-b");
        var browserRecentFallback = Path.Combine(smokeRoot, "browser-recent-fallback");
        var browserHiddenFolder = Path.Combine(browserRecentB, "hidden-nested");
        var browserSeenImage = Path.Combine(browserRecentA, "seen-browser.png");
        var nativeRecentA = Path.Combine(smokeRoot, "native-recent-a");
        var nativeRecentB = Path.Combine(smokeRoot, "native-recent-b");
        var nativeHiddenFolder = Path.Combine(nativeRecentA, "native-hidden");
        var nativeSeenImage = Path.Combine(nativeRecentA, "native-seen.png");
        Directory.CreateDirectory(Path.Combine(smokeRoot, "src"));
        Directory.CreateDirectory(smokeNative);
        Directory.CreateDirectory(browserRecentA);
        Directory.CreateDirectory(browserRecentB);
        Directory.CreateDirectory(browserRecentFallback);
        Directory.CreateDirectory(browserHiddenFolder);
        Directory.CreateDirectory(nativeRecentA);
        Directory.CreateDirectory(nativeRecentB);
        Directory.CreateDirectory(nativeHiddenFolder);
        File.WriteAllText(Path.Combine(smokeRoot, "PROJECT.md"), "# Native pvu state smoke" + Environment.NewLine, Encoding.UTF8);

        var exportPath = Path.Combine(smokeNative, "browser-localstorage-export.json");
        var browserLastDirSet = NativeFolderSet.FormatForSetting(new[] { browserRecentA, browserRecentB });
        var payload = new
        {
            localStorage = new Dictionary<string, object>
            {
                ["pvu_view"] = new
                {
                    viewMode = "grid",
                    thumbSize = 220,
                    sortBy = "created-newest",
                    rightPanelOpen = true,
                    rightPanelWidth = 360,
                    dateFrom = "2026-07-01",
                    dateTo = "2026-07-08",
                    hiddenFolders = new[] { "1:hidden-nested" },
                    folderSortBy = "count-desc",
                },
                ["pvu_enhanced_only"] = "1",
                ["pvu_fav_only"] = "1",
                ["pvu_unfav_only"] = "0",
                ["pvu_last_dir_set"] = browserLastDirSet,
                ["pvu_recent_dirs"] = new[] { browserLastDirSet, browserRecentFallback },
                ["pvu_seen_images"] = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    [browserSeenImage] = true,
                },
                ["pvu_perf_enabled"] = "1",
                ["pvu_legacy_imported"] = "1",
                ["pvu_server_legacy_imported"] = "1",
            },
        };
        File.WriteAllText(
            exportPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var store = new NativeImageStore(smokeRoot);
        var firstImport = store.ImportProjectState(exportPath);
        var pvuViewModeMigrated = string.Equals(store.GetSetting("view_mode", ""), "grid", StringComparison.OrdinalIgnoreCase);
        var pvuEnhancedOnlyMigrated = string.Equals(store.GetSetting("enhanced_only_filter", ""), "1", StringComparison.OrdinalIgnoreCase);
        var pvuFavoriteFilterMigrated = string.Equals(store.GetSetting("favorite_filter", ""), "favorites", StringComparison.OrdinalIgnoreCase);
        var pvuDateRangeMigrated = string.Equals(store.GetSetting("date_filter", ""), "custom", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("date_from", ""), "2026-07-01", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("date_to", ""), "2026-07-08", StringComparison.OrdinalIgnoreCase);
        var pvuThumbnailSizeMigrated = string.Equals(store.GetSetting("thumbnail_size", ""), "192", StringComparison.OrdinalIgnoreCase);
        var pvuThumbnailSizeClamped = pvuThumbnailSizeMigrated;
        var pvuSortModeMigrated = string.Equals(store.GetSetting("sort_mode", ""), "Created", StringComparison.OrdinalIgnoreCase);
        var expectedBrowserRecentFolderSet = NativeFolderSet.FormatForSetting(new[] { browserRecentA, browserRecentB });
        var expectedBrowserRecentRoot = NativeFolderSet.NormalizeDistinct(new[] { browserRecentA })[0];
        var pvuRecentFoldersMigrated = string.Equals(store.GetSetting("recent_folder_set", ""), expectedBrowserRecentFolderSet, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("recent_folder", ""), expectedBrowserRecentRoot, StringComparison.OrdinalIgnoreCase);
        var pvuRightPreviewMigrated = string.Equals(store.GetSetting("preview_visible", ""), "1", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("preview_splitter_distance", ""), "920", StringComparison.OrdinalIgnoreCase);
        var pvuHiddenFoldersMigrated = string.Equals(store.GetSetting("hidden_folder_buckets", ""), browserHiddenFolder, StringComparison.OrdinalIgnoreCase);
        var pvuSeenImagesMigrated = firstImport.SeenImageCount == 1 &&
            store.CountSeenImages() == 1 &&
            string.Equals(store.GetSetting("browser_seen_image_count", ""), "1", StringComparison.OrdinalIgnoreCase);
        var pvuFolderSortModeMigrated = string.Equals(store.GetSetting("folder_sort_mode", ""), "CountDesc", StringComparison.OrdinalIgnoreCase);
        var migrations = store.GetSetting("pvu_state_migrations", "");
        var migrationRecorded = string.Equals(store.GetSetting("pvu_state_migration_count", ""), "11", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_seen_images", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_view.viewMode", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_enhanced_only", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_fav_only/pvu_unfav_only", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_view.dateFrom/dateTo", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_view.rightPanel", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_view.thumbSize", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_view.sortBy", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_view.hiddenFolders", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_view.folderSortBy", StringComparison.OrdinalIgnoreCase) &&
            migrations.Contains("pvu_recent_dirs/pvu_last_dir_set", StringComparison.OrdinalIgnoreCase);
        var browserPvuViewMirror = store.GetSetting("browser_pvu_view", "");
        var browserMirrorStored = browserPvuViewMirror.Contains("viewMode", StringComparison.OrdinalIgnoreCase) &&
            browserPvuViewMirror.Contains("thumbSize", StringComparison.OrdinalIgnoreCase) &&
            browserPvuViewMirror.Contains("sortBy", StringComparison.OrdinalIgnoreCase) &&
            browserPvuViewMirror.Contains("rightPanelOpen", StringComparison.OrdinalIgnoreCase) &&
            browserPvuViewMirror.Contains("rightPanelWidth", StringComparison.OrdinalIgnoreCase) &&
            browserPvuViewMirror.Contains("hiddenFolders", StringComparison.OrdinalIgnoreCase) &&
            browserPvuViewMirror.Contains("folderSortBy", StringComparison.OrdinalIgnoreCase);
        var enhancedMirrorStored = string.Equals(store.GetSetting("browser_pvu_enhanced_only", ""), "1", StringComparison.OrdinalIgnoreCase);
        var favoriteMirrorStored = string.Equals(store.GetSetting("browser_pvu_fav_only", ""), "1", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("browser_pvu_unfav_only", ""), "0", StringComparison.OrdinalIgnoreCase);
        var recentMirrorStored = store.GetSetting("browser_pvu_last_dir_set", "").Contains("browser-recent-a", StringComparison.OrdinalIgnoreCase) &&
            store.GetSetting("browser_pvu_recent_dirs", "").Contains("browser-recent-b", StringComparison.OrdinalIgnoreCase);
        var seenMirrorStored = store.GetSetting("browser_pvu_seen_images", "").Contains("seen-browser", StringComparison.OrdinalIgnoreCase);
        var perfMirrorStored = string.Equals(store.GetSetting("browser_pvu_perf_enabled", ""), "1", StringComparison.OrdinalIgnoreCase);
        var markerMirrorStored =
            string.Equals(store.GetSetting("browser_pvu_legacy_imported", ""), "1", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("browser_pvu_server_legacy_imported", ""), "1", StringComparison.OrdinalIgnoreCase);
        var pvuPerfFlagDeferred =
            perfMirrorStored &&
            !migrations.Contains("pvu_perf_enabled", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(store.GetSetting("perf_enabled", ""));
        var pvuLegacyMarkersRejected =
            markerMirrorStored &&
            !migrations.Contains("pvu_legacy_imported", StringComparison.OrdinalIgnoreCase) &&
            !migrations.Contains("pvu_server_legacy_imported", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(store.GetSetting("legacy_imported", "")) &&
            string.IsNullOrWhiteSpace(store.GetSetting("server_legacy_imported", ""));

        store.SaveSetting("view_mode", "details");
        store.SaveSetting("enhanced_only_filter", "0");
        store.SaveSetting("favorite_filter", "unrated");
        store.SaveSetting("date_filter", "custom");
        store.SaveSetting("date_from", "2026-06-01");
        store.SaveSetting("date_to", "2026-06-02");
        store.SaveSetting("thumbnail_size", "128");
        store.SaveSetting("sort_mode", "Name");
        store.SaveRecentFolderSet(new[] { nativeRecentA, nativeRecentB });
        store.SaveSetting("preview_visible", "0");
        store.SaveSetting("preview_splitter_distance", "840");
        store.SaveSetting("hidden_folder_buckets", nativeHiddenFolder);
        store.MarkImageSeen(nativeSeenImage);
        store.SaveSetting("folder_sort_mode", "NameDesc");
        var secondImport = store.ImportProjectState(exportPath);
        var nativeViewModePreserved = string.Equals(store.GetSetting("view_mode", ""), "details", StringComparison.OrdinalIgnoreCase);
        var nativeEnhancedOnlyPreserved = string.Equals(store.GetSetting("enhanced_only_filter", ""), "0", StringComparison.OrdinalIgnoreCase);
        var nativeFavoriteFilterPreserved = string.Equals(store.GetSetting("favorite_filter", ""), "unrated", StringComparison.OrdinalIgnoreCase);
        var nativeDateRangePreserved = string.Equals(store.GetSetting("date_filter", ""), "custom", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("date_from", ""), "2026-06-01", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("date_to", ""), "2026-06-02", StringComparison.OrdinalIgnoreCase);
        var nativeThumbnailSizePreserved = string.Equals(store.GetSetting("thumbnail_size", ""), "128", StringComparison.OrdinalIgnoreCase);
        var nativeSortModePreserved = string.Equals(store.GetSetting("sort_mode", ""), "Name", StringComparison.OrdinalIgnoreCase);
        var expectedNativeRecentFolderSet = NativeFolderSet.FormatForSetting(new[] { nativeRecentA, nativeRecentB });
        var expectedNativeRecentRoot = NativeFolderSet.NormalizeDistinct(new[] { nativeRecentA })[0];
        var nativeRecentFolderSetPreserved = string.Equals(store.GetSetting("recent_folder_set", ""), expectedNativeRecentFolderSet, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("recent_folder", ""), expectedNativeRecentRoot, StringComparison.OrdinalIgnoreCase);
        var nativeRightPreviewPreserved = string.Equals(store.GetSetting("preview_visible", ""), "0", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("preview_splitter_distance", ""), "840", StringComparison.OrdinalIgnoreCase);
        var nativeHiddenFoldersPreserved = string.Equals(store.GetSetting("hidden_folder_buckets", ""), nativeHiddenFolder, StringComparison.OrdinalIgnoreCase);
        var nativeSeenImagesPreserved = store.CountSeenImages() == 2;
        var nativeFolderSortModePreserved = string.Equals(store.GetSetting("folder_sort_mode", ""), "NameDesc", StringComparison.OrdinalIgnoreCase);

        var malformedExportPath = Path.Combine(smokeNative, "malformed-enhanced-only-export.json");
        var malformedPayload = new
        {
            localStorage = new Dictionary<string, object>
            {
                ["pvu_view"] = new { viewMode = "grid", thumbSize = "wide", sortBy = "oldest", dateFrom = "not-a-date", dateTo = "2026-07-08", rightPanelOpen = "maybe", rightPanelWidth = "wide", hiddenFolders = new { invalid = true }, folderSortBy = "count-sideways" },
                ["pvu_enhanced_only"] = "maybe",
                ["pvu_fav_only"] = "maybe",
                ["pvu_unfav_only"] = "0",
                ["pvu_recent_dirs"] = new { invalid = true },
                ["pvu_seen_images"] = "{ malformed",
                ["pvu_perf_enabled"] = "1",
            },
        };
        File.WriteAllText(
            malformedExportPath,
            JsonSerializer.Serialize(malformedPayload, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var malformedImport = store.ImportProjectState(malformedExportPath);
        var malformedEnhancedOnlyWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_enhanced_only", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-boolean-value", StringComparison.OrdinalIgnoreCase));
        var malformedFavoriteFilterWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_fav_only", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-boolean-value", StringComparison.OrdinalIgnoreCase));
        var malformedDateRangeWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_view", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-date-range-value", StringComparison.OrdinalIgnoreCase));
        var malformedThumbnailSizeWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_view", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-thumbnail-size-value", StringComparison.OrdinalIgnoreCase));
        var unsupportedSortModeWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_view", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "unsupported-sort-mode-value", StringComparison.OrdinalIgnoreCase));
        var malformedRecentDirsWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_recent_dirs", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-recent-dirs-value", StringComparison.OrdinalIgnoreCase));
        var malformedRightPreviewWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_view", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-right-preview-value", StringComparison.OrdinalIgnoreCase));
        var malformedHiddenFoldersWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_view", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-hidden-folders-value", StringComparison.OrdinalIgnoreCase));
        var malformedSeenImagesWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_seen_images", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "malformed-json-value", StringComparison.OrdinalIgnoreCase));
        var unsupportedFolderSortWarning = malformedImport.Warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export:pvu_view", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(warning.Code, "unsupported-folder-sort-value", StringComparison.OrdinalIgnoreCase));
        var nativeEnhancedOnlyStillPreserved = string.Equals(store.GetSetting("enhanced_only_filter", ""), "0", StringComparison.OrdinalIgnoreCase);
        var nativeFavoriteFilterStillPreserved = string.Equals(store.GetSetting("favorite_filter", ""), "unrated", StringComparison.OrdinalIgnoreCase);
        var nativeDateRangeStillPreserved = string.Equals(store.GetSetting("date_filter", ""), "custom", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("date_from", ""), "2026-06-01", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("date_to", ""), "2026-06-02", StringComparison.OrdinalIgnoreCase);
        var nativeThumbnailSizeStillPreserved = string.Equals(store.GetSetting("thumbnail_size", ""), "128", StringComparison.OrdinalIgnoreCase);
        var nativeSortModeStillPreserved = string.Equals(store.GetSetting("sort_mode", ""), "Name", StringComparison.OrdinalIgnoreCase);
        var nativeRecentFolderSetStillPreserved = string.Equals(store.GetSetting("recent_folder_set", ""), expectedNativeRecentFolderSet, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("recent_folder", ""), expectedNativeRecentRoot, StringComparison.OrdinalIgnoreCase);
        var nativeRightPreviewStillPreserved = string.Equals(store.GetSetting("preview_visible", ""), "0", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(store.GetSetting("preview_splitter_distance", ""), "840", StringComparison.OrdinalIgnoreCase);
        var nativeHiddenFoldersStillPreserved = string.Equals(store.GetSetting("hidden_folder_buckets", ""), nativeHiddenFolder, StringComparison.OrdinalIgnoreCase);
        var nativeSeenImagesStillPreserved = store.CountSeenImages() == 2;
        var nativeFolderSortModeStillPreserved = string.Equals(store.GetSetting("folder_sort_mode", ""), "NameDesc", StringComparison.OrdinalIgnoreCase);
        var passed = pvuViewModeMigrated &&
            pvuEnhancedOnlyMigrated &&
            pvuFavoriteFilterMigrated &&
            pvuDateRangeMigrated &&
            pvuThumbnailSizeMigrated &&
            pvuThumbnailSizeClamped &&
            pvuSortModeMigrated &&
            pvuRecentFoldersMigrated &&
            pvuRightPreviewMigrated &&
            pvuHiddenFoldersMigrated &&
            pvuSeenImagesMigrated &&
            pvuFolderSortModeMigrated &&
            migrationRecorded &&
            browserMirrorStored &&
            enhancedMirrorStored &&
            favoriteMirrorStored &&
            recentMirrorStored &&
            seenMirrorStored &&
            perfMirrorStored &&
            markerMirrorStored &&
            pvuPerfFlagDeferred &&
            pvuLegacyMarkersRejected &&
            nativeViewModePreserved &&
            nativeEnhancedOnlyPreserved &&
            nativeFavoriteFilterPreserved &&
            nativeDateRangePreserved &&
            nativeThumbnailSizePreserved &&
            nativeSortModePreserved &&
            nativeRecentFolderSetPreserved &&
            nativeRightPreviewPreserved &&
            nativeHiddenFoldersPreserved &&
            nativeSeenImagesPreserved &&
            nativeFolderSortModePreserved &&
            malformedEnhancedOnlyWarning &&
            malformedFavoriteFilterWarning &&
            malformedDateRangeWarning &&
            malformedThumbnailSizeWarning &&
            unsupportedSortModeWarning &&
            malformedRecentDirsWarning &&
            malformedRightPreviewWarning &&
            malformedHiddenFoldersWarning &&
            malformedSeenImagesWarning &&
            unsupportedFolderSortWarning &&
            nativeEnhancedOnlyStillPreserved &&
            nativeFavoriteFilterStillPreserved &&
            nativeDateRangeStillPreserved &&
            nativeThumbnailSizeStillPreserved &&
            nativeSortModeStillPreserved &&
            nativeRecentFolderSetStillPreserved &&
            nativeRightPreviewStillPreserved &&
            nativeHiddenFoldersStillPreserved &&
            nativeSeenImagesStillPreserved &&
            nativeFolderSortModeStillPreserved &&
            firstImport.WarningCount == 0 &&
            secondImport.WarningCount == 0 &&
            malformedImport.WarningCount == 10;

        Console.WriteLine(
            $"native-pvu-state-smoke complete pvuViewModeMigrated={BoolText(pvuViewModeMigrated)} pvuEnhancedOnlyMigrated={BoolText(pvuEnhancedOnlyMigrated)} pvuFavoriteFilterMigrated={BoolText(pvuFavoriteFilterMigrated)} pvuDateRangeMigrated={BoolText(pvuDateRangeMigrated)} pvuThumbnailSizeMigrated={BoolText(pvuThumbnailSizeMigrated)} pvuThumbnailSizeClamped={BoolText(pvuThumbnailSizeClamped)} pvuSortModeMigrated={BoolText(pvuSortModeMigrated)} pvuRecentFoldersMigrated={BoolText(pvuRecentFoldersMigrated)} pvuRightPreviewMigrated={BoolText(pvuRightPreviewMigrated)} pvuHiddenFoldersMigrated={BoolText(pvuHiddenFoldersMigrated)} pvuSeenImagesMigrated={BoolText(pvuSeenImagesMigrated)} pvuFolderSortModeMigrated={BoolText(pvuFolderSortModeMigrated)} pvuPerfFlagDeferred={BoolText(pvuPerfFlagDeferred)} pvuLegacyMarkersRejected={BoolText(pvuLegacyMarkersRejected)} migrationRecorded={BoolText(migrationRecorded)} browserMirrorStored={BoolText(browserMirrorStored)} enhancedMirrorStored={BoolText(enhancedMirrorStored)} favoriteMirrorStored={BoolText(favoriteMirrorStored)} recentMirrorStored={BoolText(recentMirrorStored)} seenMirrorStored={BoolText(seenMirrorStored)} perfMirrorStored={BoolText(perfMirrorStored)} markerMirrorStored={BoolText(markerMirrorStored)} nativeViewModePreserved={BoolText(nativeViewModePreserved)} nativeEnhancedOnlyPreserved={BoolText(nativeEnhancedOnlyPreserved)} nativeFavoriteFilterPreserved={BoolText(nativeFavoriteFilterPreserved)} nativeDateRangePreserved={BoolText(nativeDateRangePreserved)} nativeThumbnailSizePreserved={BoolText(nativeThumbnailSizePreserved)} nativeSortModePreserved={BoolText(nativeSortModePreserved)} nativeRecentFolderSetPreserved={BoolText(nativeRecentFolderSetPreserved)} nativeRightPreviewPreserved={BoolText(nativeRightPreviewPreserved)} nativeHiddenFoldersPreserved={BoolText(nativeHiddenFoldersPreserved)} nativeSeenImagesPreserved={BoolText(nativeSeenImagesPreserved)} nativeFolderSortModePreserved={BoolText(nativeFolderSortModePreserved)} malformedEnhancedOnlyWarning={BoolText(malformedEnhancedOnlyWarning)} malformedFavoriteFilterWarning={BoolText(malformedFavoriteFilterWarning)} malformedDateRangeWarning={BoolText(malformedDateRangeWarning)} malformedThumbnailSizeWarning={BoolText(malformedThumbnailSizeWarning)} unsupportedSortModeWarning={BoolText(unsupportedSortModeWarning)} malformedRecentDirsWarning={BoolText(malformedRecentDirsWarning)} malformedRightPreviewWarning={BoolText(malformedRightPreviewWarning)} malformedHiddenFoldersWarning={BoolText(malformedHiddenFoldersWarning)} malformedSeenImagesWarning={BoolText(malformedSeenImagesWarning)} unsupportedFolderSortWarning={BoolText(unsupportedFolderSortWarning)} nativeEnhancedOnlyStillPreserved={BoolText(nativeEnhancedOnlyStillPreserved)} nativeFavoriteFilterStillPreserved={BoolText(nativeFavoriteFilterStillPreserved)} nativeDateRangeStillPreserved={BoolText(nativeDateRangeStillPreserved)} nativeThumbnailSizeStillPreserved={BoolText(nativeThumbnailSizeStillPreserved)} nativeSortModeStillPreserved={BoolText(nativeSortModeStillPreserved)} nativeRecentFolderSetStillPreserved={BoolText(nativeRecentFolderSetStillPreserved)} nativeRightPreviewStillPreserved={BoolText(nativeRightPreviewStillPreserved)} nativeHiddenFoldersStillPreserved={BoolText(nativeHiddenFoldersStillPreserved)} nativeSeenImagesStillPreserved={BoolText(nativeSeenImagesStillPreserved)} nativeFolderSortModeStillPreserved={BoolText(nativeFolderSortModeStillPreserved)} browserStateKeys={store.CountBrowserStateKeys()} firstWarnings={firstImport.WarningCount} secondWarnings={secondImport.WarningCount} malformedWarnings={malformedImport.WarningCount} smokeRoot=\"{smokeRoot}\" browserRuntime=false localHttpServer=false nodeRuntime=false");
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
