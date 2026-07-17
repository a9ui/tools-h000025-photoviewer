using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoViewer.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (IsAutomationInvocation(e.Args))
        {
            try
            {
                ConfigureAutomationStorage(e.Args);
            }
            catch (ArgumentException ex)
            {
                Trace.TraceError(ex.Message);
                Environment.ExitCode = 1;
                Shutdown(1);
                return;
            }
        }

        var startupWatch = Stopwatch.StartNew();
        base.OnStartup(e);

        int startupSmokeIdx = Array.IndexOf(e.Args, "--startup-smoke");
        if (startupSmokeIdx >= 0 && startupSmokeIdx + 1 < e.Args.Length)
        {
            CaptureStartupSmoke(e.Args[startupSmokeIdx + 1], e.Args, startupWatch);
            return;
        }

        int automationIsolationSmokeIdx = Array.IndexOf(e.Args, "--automation-isolation-smoke");
        if (automationIsolationSmokeIdx >= 0 && automationIsolationSmokeIdx + 1 < e.Args.Length)
        {
            CaptureAutomationIsolationSmoke(e.Args[automationIsolationSmokeIdx + 1]);
            return;
        }

        int diagnosticsSmokeIdx = Array.IndexOf(e.Args, "--diagnostics-smoke");
        if (diagnosticsSmokeIdx >= 0 && diagnosticsSmokeIdx + 1 < e.Args.Length)
        {
            CaptureDiagnosticsSmoke(e.Args[diagnosticsSmokeIdx + 1]);
            return;
        }

        int settingsUnseenDotsSmokeIdx = Array.IndexOf(e.Args, "--settings-unseen-dots-smoke");
        if (settingsUnseenDotsSmokeIdx >= 0 && settingsUnseenDotsSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSettingsUnseenDotsSmoke(e.Args[settingsUnseenDotsSmokeIdx + 1]);
            return;
        }

        int scanCancelSmokeIdx = Array.IndexOf(e.Args, "--scan-cancel-smoke");
        if (scanCancelSmokeIdx >= 0 && scanCancelSmokeIdx + 1 < e.Args.Length)
        {
            CaptureScanCancellationSmoke(e.Args[scanCancelSmokeIdx + 1]);
            return;
        }

        int scanBoundarySmokeIdx = Array.IndexOf(e.Args, "--scan-boundary-smoke");
        if (scanBoundarySmokeIdx >= 0 && scanBoundarySmokeIdx + 1 < e.Args.Length)
        {
            CaptureScanBoundarySmoke(e.Args[scanBoundarySmokeIdx + 1], e.Args);
            return;
        }

        int shotIdx = Array.IndexOf(e.Args, "--shot");
        if (shotIdx >= 0 && shotIdx + 1 < e.Args.Length)
        {
            CaptureShot(e.Args[shotIdx + 1]);
            return;
        }

        int crossRuntimeSharedStateIdx = Array.IndexOf(e.Args, "--cross-runtime-shared-state-smoke");
        if (crossRuntimeSharedStateIdx >= 0 && crossRuntimeSharedStateIdx + 1 < e.Args.Length)
        {
            CaptureCrossRuntimeSharedStateSmoke(e.Args[crossRuntimeSharedStateIdx + 1], e.Args);
            return;
        }

        int crossRuntimeRecentIdx = Array.IndexOf(e.Args, "--cross-runtime-recent-smoke");
        if (crossRuntimeRecentIdx >= 0 && crossRuntimeRecentIdx + 1 < e.Args.Length)
        {
            CaptureCrossRuntimeRecentSmoke(e.Args[crossRuntimeRecentIdx + 1], e.Args);
            return;
        }

        int recentWriteOwnershipIdx = Array.IndexOf(e.Args, "--recent-write-ownership-smoke");
        if (recentWriteOwnershipIdx >= 0 && recentWriteOwnershipIdx + 1 < e.Args.Length)
        {
            CaptureRecentWriteOwnershipSmoke(e.Args[recentWriteOwnershipIdx + 1]);
            return;
        }

        int modalNavSmokeIdx = Array.IndexOf(e.Args, "--modal-nav-smoke");
        if (modalNavSmokeIdx >= 0 && modalNavSmokeIdx + 1 < e.Args.Length)
        {
            CaptureModalNavigationSmoke(e.Args[modalNavSmokeIdx + 1], e.Args);
            return;
        }

        int modalTransformSmokeIdx = Array.IndexOf(e.Args, "--modal-transform-smoke");
        if (modalTransformSmokeIdx >= 0 && modalTransformSmokeIdx + 1 < e.Args.Length)
        {
            CaptureModalTransformSmoke(e.Args[modalTransformSmokeIdx + 1], e.Args);
            return;
        }

        int modalPanSmokeIdx = Array.IndexOf(e.Args, "--modal-pan-smoke");
        if (modalPanSmokeIdx >= 0 && modalPanSmokeIdx + 1 < e.Args.Length)
        {
            CaptureModalPanSmoke(e.Args[modalPanSmokeIdx + 1], e.Args);
            return;
        }

        int modalInteractionSmokeIdx = Array.IndexOf(e.Args, "--modal-interaction-smoke");
        if (modalInteractionSmokeIdx >= 0 && modalInteractionSmokeIdx + 1 < e.Args.Length)
        {
            CaptureModalInteractionSmoke(e.Args[modalInteractionSmokeIdx + 1]);
            return;
        }

        int selectionSmokeIdx = Array.IndexOf(e.Args, "--selection-smoke");
        if (selectionSmokeIdx >= 0 && selectionSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSelectionSmoke(e.Args[selectionSmokeIdx + 1], e.Args);
            return;
        }

        int previewTabsSmokeIdx = Array.IndexOf(e.Args, "--preview-tabs-smoke");
        if (previewTabsSmokeIdx >= 0 && previewTabsSmokeIdx + 1 < e.Args.Length)
        {
            CapturePreviewTabsSmoke(e.Args[previewTabsSmokeIdx + 1], e.Args);
            return;
        }

        int previewTabReorderSmokeIdx = Array.IndexOf(e.Args, "--preview-tab-reorder-smoke");
        if (previewTabReorderSmokeIdx >= 0 && previewTabReorderSmokeIdx + 1 < e.Args.Length)
        {
            CapturePreviewTabReorderSmoke(e.Args[previewTabReorderSmokeIdx + 1]);
            return;
        }

        int previewTabHoverSmokeIdx = Array.IndexOf(e.Args, "--preview-tab-hover-smoke");
        if (previewTabHoverSmokeIdx >= 0 && previewTabHoverSmokeIdx + 1 < e.Args.Length)
        {
            CapturePreviewTabHoverSmoke(e.Args[previewTabHoverSmokeIdx + 1]);
            return;
        }

        int previewDecodeSmokeIdx = Array.IndexOf(e.Args, "--preview-decode-smoke");
        if (previewDecodeSmokeIdx >= 0 && previewDecodeSmokeIdx + 1 < e.Args.Length)
        {
            CapturePreviewDecodeSmoke(e.Args[previewDecodeSmokeIdx + 1], e.Args);
            return;
        }

        int pngMetadataSmokeIdx = Array.IndexOf(e.Args, "--png-metadata-smoke");
        if (pngMetadataSmokeIdx >= 0 && pngMetadataSmokeIdx + 1 < e.Args.Length)
        {
            CapturePngMetadataSmoke(e.Args[pngMetadataSmokeIdx + 1]);
            return;
        }

        int promptTagSearchSmokeIdx = Array.IndexOf(e.Args, "--prompt-tag-search-smoke");
        if (promptTagSearchSmokeIdx >= 0 && promptTagSearchSmokeIdx + 1 < e.Args.Length)
        {
            CapturePromptTagSearchSmoke(e.Args[promptTagSearchSmokeIdx + 1]);
            return;
        }

        int fileDragOutSmokeIdx = Array.IndexOf(e.Args, "--file-drag-out-smoke");
        if (fileDragOutSmokeIdx >= 0 && fileDragOutSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFileDragOutSmoke(e.Args[fileDragOutSmokeIdx + 1]);
            return;
        }

        int explorerRevealSmokeIdx = Array.IndexOf(e.Args, "--explorer-reveal-smoke");
        if (explorerRevealSmokeIdx >= 0 && explorerRevealSmokeIdx + 1 < e.Args.Length)
        {
            CaptureExplorerRevealSmoke(e.Args[explorerRevealSmokeIdx + 1]);
            return;
        }

        int folderDragInSmokeIdx = Array.IndexOf(e.Args, "--folder-drag-in-smoke");
        if (folderDragInSmokeIdx >= 0 && folderDragInSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFolderDragInSmoke(e.Args[folderDragInSmokeIdx + 1]);
            return;
        }

        int shortcutTypingSmokeIdx = Array.IndexOf(e.Args, "--shortcut-typing-smoke");
        if (shortcutTypingSmokeIdx >= 0 && shortcutTypingSmokeIdx + 1 < e.Args.Length)
        {
            CaptureShortcutTypingSmoke(e.Args[shortcutTypingSmokeIdx + 1], e.Args);
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

        int bulkFavoriteSmokeIdx = Array.IndexOf(e.Args, "--bulk-favorite-smoke");
        if (bulkFavoriteSmokeIdx >= 0 && bulkFavoriteSmokeIdx + 1 < e.Args.Length)
        {
            CaptureBulkFavoriteSmoke(e.Args[bulkFavoriteSmokeIdx + 1]);
            return;
        }

        int bulkRecycleSmokeIdx = Array.IndexOf(e.Args, "--bulk-recycle-smoke");
        if (bulkRecycleSmokeIdx >= 0 && bulkRecycleSmokeIdx + 1 < e.Args.Length)
        {
            CaptureBulkRecycleSmoke(e.Args[bulkRecycleSmokeIdx + 1]);
            return;
        }

        int deleteCorrectnessSmokeIdx = Array.IndexOf(e.Args, "--delete-correctness-smoke");
        if (deleteCorrectnessSmokeIdx >= 0 && deleteCorrectnessSmokeIdx + 1 < e.Args.Length)
        {
            CaptureDeleteCorrectnessSmoke(e.Args[deleteCorrectnessSmokeIdx + 1]);
            return;
        }

        int favoriteFilterSmokeIdx = Array.IndexOf(e.Args, "--favorite-filter-smoke");
        if (favoriteFilterSmokeIdx >= 0 && favoriteFilterSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFavoriteFilterSmoke(e.Args[favoriteFilterSmokeIdx + 1]);
            return;
        }

        int p0aSmokeIdx = Array.IndexOf(e.Args, "--p0a-smoke");
        if (p0aSmokeIdx >= 0 && p0aSmokeIdx + 1 < e.Args.Length)
        {
            CaptureP0ASmoke(e.Args[p0aSmokeIdx + 1]);
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

        int folderBucketSmokeIdx = Array.IndexOf(e.Args, "--folder-bucket-smoke");
        if (folderBucketSmokeIdx >= 0 && folderBucketSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFolderBucketSmoke(e.Args[folderBucketSmokeIdx + 1]);
            return;
        }

        int gridZoomSmokeIdx = Array.IndexOf(e.Args, "--grid-zoom-smoke");
        if (gridZoomSmokeIdx >= 0 && gridZoomSmokeIdx + 1 < e.Args.Length)
        {
            CaptureGridZoomSmoke(e.Args[gridZoomSmokeIdx + 1], e.Args);
            return;
        }

        int p0bSmokeIdx = Array.IndexOf(e.Args, "--p0b-smoke");
        if (p0bSmokeIdx >= 0 && p0bSmokeIdx + 1 < e.Args.Length)
        {
            CaptureP0BSmoke(e.Args[p0bSmokeIdx + 1]);
            return;
        }

        int p0cSmokeIdx = Array.IndexOf(e.Args, "--p0c-smoke");
        if (p0cSmokeIdx >= 0 && p0cSmokeIdx + 1 < e.Args.Length)
        {
            CaptureP0CSmoke(e.Args[p0cSmokeIdx + 1]);
            return;
        }

        int p0dSmokeIdx = Array.IndexOf(e.Args, "--p0d-smoke");
        if (p0dSmokeIdx >= 0 && p0dSmokeIdx + 1 < e.Args.Length)
        {
            CaptureP0DSmoke(e.Args[p0dSmokeIdx + 1]);
            return;
        }

        int displayStyleSmokeIdx = Array.IndexOf(e.Args, "--display-style-smoke");
        if (displayStyleSmokeIdx >= 0 && displayStyleSmokeIdx + 1 < e.Args.Length)
        {
            CaptureDisplayStyleSmoke(e.Args[displayStyleSmokeIdx + 1], e.Args);
            return;
        }

        int aspectSmokeIdx = Array.IndexOf(e.Args, "--aspect-smoke");
        if (aspectSmokeIdx >= 0 && aspectSmokeIdx + 1 < e.Args.Length)
        {
            CaptureAspectSmoke(e.Args[aspectSmokeIdx + 1], e.Args);
            return;
        }

        int sortSmokeIdx = Array.IndexOf(e.Args, "--sort-smoke");
        if (sortSmokeIdx >= 0 && sortSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSortSmoke(e.Args[sortSmokeIdx + 1], e.Args);
            return;
        }

        int dateFilterSmokeIdx = Array.IndexOf(e.Args, "--date-filter-smoke");
        if (dateFilterSmokeIdx >= 0 && dateFilterSmokeIdx + 1 < e.Args.Length)
        {
            CaptureDateFilterSmoke(e.Args[dateFilterSmokeIdx + 1]);
            return;
        }

        int p1aSmokeIdx = Array.IndexOf(e.Args, "--p1a-smoke");
        if (p1aSmokeIdx >= 0 && p1aSmokeIdx + 1 < e.Args.Length)
        {
            CaptureP1ASmoke(e.Args[p1aSmokeIdx + 1]);
            return;
        }

        int searchStallSmokeIdx = Array.IndexOf(e.Args, "--search-stall-smoke");
        if (searchStallSmokeIdx >= 0 && searchStallSmokeIdx + 1 < e.Args.Length)
        {
            CaptureSearchStallSmoke(e.Args[searchStallSmokeIdx + 1]);
            return;
        }

        int rapidUiStateSmokeIdx = Array.IndexOf(e.Args, "--rapid-ui-state-smoke");
        if (rapidUiStateSmokeIdx >= 0 && rapidUiStateSmokeIdx + 1 < e.Args.Length)
        {
            CaptureRapidUiStateSmoke(e.Args[rapidUiStateSmokeIdx + 1]);
            return;
        }

        int shutdownStateSmokeIdx = Array.IndexOf(e.Args, "--shutdown-state-smoke");
        if (shutdownStateSmokeIdx >= 0 && shutdownStateSmokeIdx + 1 < e.Args.Length)
        {
            CaptureShutdownStateSmoke(e.Args[shutdownStateSmokeIdx + 1]);
            return;
        }

        int catalogStressSmokeIdx = Array.IndexOf(e.Args, "--catalog-stress-smoke");
        if (catalogStressSmokeIdx >= 0 && catalogStressSmokeIdx + 1 < e.Args.Length)
        {
            CaptureCatalogStressSmoke(e.Args[catalogStressSmokeIdx + 1], e.Args);
            return;
        }

        int p1bSmokeIdx = Array.IndexOf(e.Args, "--p1b-smoke");
        if (p1bSmokeIdx >= 0 && p1bSmokeIdx + 1 < e.Args.Length)
        {
            CaptureP1BSmoke(e.Args[p1bSmokeIdx + 1]);
            return;
        }

        int formatSmokeIdx = Array.IndexOf(e.Args, "--format-smoke");
        if (formatSmokeIdx >= 0 && formatSmokeIdx + 1 < e.Args.Length)
        {
            CaptureFormatSmoke(e.Args[formatSmokeIdx + 1], e.Args);
            return;
        }

        int rightPanelSmokeIdx = Array.IndexOf(e.Args, "--right-panel-smoke");
        if (rightPanelSmokeIdx >= 0 && rightPanelSmokeIdx + 1 < e.Args.Length)
        {
            CaptureRightPanelSmoke(e.Args[rightPanelSmokeIdx + 1]);
            return;
        }

        int enhancedFilterSmokeIdx = Array.IndexOf(e.Args, "--enhanced-filter-smoke");
        if (enhancedFilterSmokeIdx >= 0 && enhancedFilterSmokeIdx + 1 < e.Args.Length)
        {
            CaptureEnhancedFilterSmoke(e.Args[enhancedFilterSmokeIdx + 1], e.Args);
            return;
        }

        int modalEnhancedSmokeIdx = Array.IndexOf(e.Args, "--modal-enhanced-smoke");
        if (modalEnhancedSmokeIdx >= 0 && modalEnhancedSmokeIdx + 1 < e.Args.Length)
        {
            CaptureModalEnhancedSmoke(e.Args[modalEnhancedSmokeIdx + 1], e.Args);
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

    private static bool IsAutomationInvocation(IReadOnlyList<string> args)
        => args.Any(static arg => arg.StartsWith("--", StringComparison.Ordinal)
            && (string.Equals(arg, "--shot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--startup-smoke", StringComparison.OrdinalIgnoreCase)
                 || arg.EndsWith("-smoke", StringComparison.OrdinalIgnoreCase)));

    private void CaptureScanBoundarySmoke(string resultPath, IReadOnlyList<string> args)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string? root = ArgValue(args.ToArray(), "--folder");
        object result;
        bool ok = false;
        try
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                throw new ArgumentException("scan boundary smoke requires an existing --folder");
            ScanBoundarySmokeSnapshot snapshot = PhotoViewer.Wpf.MainWindow.EnumerateImageFilesForSmoke(root);
            ok = true;
            result = new
            {
                ok = true,
                root = Path.GetFullPath(root),
                images = snapshot.Images,
                accessFailures = snapshot.AccessFailures,
                boundarySkips = snapshot.BoundarySkips,
                elapsedMs = snapshot.ElapsedMs,
            };
        }
        catch (Exception ex)
        {
            result = new { ok = false, message = ex.Message };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
        File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = ok ? 0 : 1;
        Shutdown(Environment.ExitCode);
    }

    private static void ConfigureAutomationStorage(IReadOnlyList<string> args)
    {
        ValidateAutomationPathArguments(args);

        string root = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-automation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", Path.Combine(root, "state.json"));
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", Path.Combine(root, "favorites.json"));
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", Path.Combine(root, "seen.json"));
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", Path.Combine(root, "recent-folders.json"));
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", Path.Combine(root, "enhance", "jobs.json"));
    }

    private static void ValidateAutomationPathArguments(IReadOnlyList<string> args)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string[] commandLine = args.ToArray();
        string[] protectedOptions = [
            "--state-path",
            "--favorites-path",
            "--seen-path",
            "--recent-path",
            "--enhancement-jobs-path",
            "--key-root",
        ];

        foreach (string option in protectedOptions)
        {
            string? candidate = ArgValue(commandLine, option);
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Automation {option} path is invalid: {ex.Message}");
            }

            if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Automation {option} must be under the temp directory. Refusing to touch '{fullPath}'.");
        }
    }

    private void CaptureAutomationIsolationSmoke(string resultPath)
    {
        bool positionalFolderRemainsInteractive = !IsAutomationInvocation([
            "PhotoViewer.Wpf.exe",
            @"C:\albums\portrait-smoke",
        ]);
        bool knownSmokeIsIsolated = IsAutomationInvocation(["PhotoViewer.Wpf.exe", "--p1a-smoke", "result.json"]);
        bool screenshotIsIsolated = IsAutomationInvocation(["PhotoViewer.Wpf.exe", "--shot", "shot.png"]);
        bool ok = positionalFolderRemainsInteractive && knownSmokeIsIsolated && screenshotIsIsolated;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(new
        {
            ok,
            positionalFolderRemainsInteractive,
            knownSmokeIsIsolated,
            screenshotIsIsolated,
        }));
        Shutdown(ok ? 0 : 1);
    }

    private void CaptureDiagnosticsSmoke(string resultPath)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(() =>
        {
            win.OpenAppSettingsForSmoke();
            win.Dispatcher.BeginInvoke(() =>
            {
            win.FocusDiagnosticsForSmoke();
            DiagnosticsSmokeSnapshot success = win.CopyDiagnosticsForSmoke(injectClipboardFailure: false);
            DiagnosticsSmokeSnapshot failure = win.CopyDiagnosticsForSmoke(injectClipboardFailure: true);
            bool doneReachable = win.FocusAppSettingsDoneForSmoke();
            string payload = success.CopyText;
            bool privateDataAbsent = !payload.Contains(win.StatePathForSmoke, StringComparison.OrdinalIgnoreCase)
                && !payload.Contains(win.FavoritesPathForSmoke, StringComparison.OrdinalIgnoreCase)
                && !payload.Contains(win.SeenPathForSmoke, StringComparison.OrdinalIgnoreCase)
                && !payload.Contains("\\", StringComparison.Ordinal)
                && !payload.Contains("prompt", StringComparison.OrdinalIgnoreCase);
            bool ok = success.Copied && !failure.Copied && failure.Status.Contains("could not be copied", StringComparison.OrdinalIgnoreCase) && doneReachable
                && success.SurfaceContract && success.SettingsFocused && privateDataAbsent
                && payload.Contains("PhotoViewer.Wpf", StringComparison.Ordinal) && payload.Contains("Build:", StringComparison.Ordinal)
                && payload.Contains("Process:", StringComparison.Ordinal) && payload.Contains("Catalog:", StringComparison.Ordinal);
            WriteCrossRuntimeSharedStateResult(resultPath, new { ok, success, failure, privateDataAbsent, doneReachable, payload });
            win.Close();
            Shutdown(ok ? 0 : 1);
            }, DispatcherPriority.Input);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureSettingsUnseenDotsSmoke(string resultPath)
    {
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-settings-unseen-dots-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "source fixture");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent-folders.json");
        string jobsPath = Path.Combine(smokeRoot, "enhance", "jobs.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(jobsPath)!);
        DateTime fixtureTime = new(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 3; index++)
        {
            string fixturePath = Path.Combine(folder, $"unseen-setting-{index}.png");
            WriteSmokePng(fixturePath, 96, 72, Color.FromRgb((byte)(70 + index * 35), 105, 165));
            // The viewer marks the first selected image Seen. Keep that selection deterministic
            // so this smoke can distinguish selection semantics from the display-only toggle.
            File.SetLastWriteTimeUtc(fixturePath, fixtureTime.AddMinutes(-index));
        }
        string seededSeenPath = Path.Combine(folder, "unseen-setting-0.png");
        File.WriteAllText(favoritesPath, "{}");
        File.WriteAllText(seenPath, JsonSerializer.Serialize(new Dictionary<string, bool> { [seededSeenPath] = true }));
        File.WriteAllText(recentPath, "{\"version\":1,\"lastFolderSet\":[],\"recentFolderSets\":[],\"updatedAtUtc\":\"\"}");
        File.WriteAllText(jobsPath, "{\"version\":1,\"jobs\":[]}");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);

        string sourceBefore = FolderFingerprint(folder);
        string favoritesBefore = FileFingerprint(favoritesPath);
        string seenBefore = FileFingerprint(seenPath);
        string jobsBefore = FileFingerprint(jobsPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var windows = new List<MainWindow>();
        var first = HiddenWindow();
        windows.Add(first);
        first.Show();
        first.Dispatcher.InvokeAsync(async () =>
        {
            object result;
            bool ok = false;
            try
            {
                bool defaultOff = !first.ShowUnseenDotsForSmoke
                    && !first.SidebarUnseenDotsCheckedForSmoke
                    && !first.AppSettingsUnseenDotsCheckedForSmoke;
                await first.LoadFolderAsync(folder);
                string recentAfterFolderOpen = FileFingerprint(recentPath);
                int unseenCount = first.UnseenCountForSmoke;
                bool sidebarFocused = first.FocusSidebarUnseenDotsForSmoke() && first.IsSidebarUnseenDotsFocusedForSmoke;

                first.OpenAppSettingsForSmoke();
                await first.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool settingsFocused = first.FocusAppSettingsUnseenDotsForSmoke() && first.IsAppSettingsUnseenDotsFocusedForSmoke;
                bool accessible = first.UnseenDotsSurfaceContractForSmoke
                    && first.SettingsFocusTrapConfiguredForSmoke
                    && first.IsSettingsDialogFocusedForSmoke;
                bool defaultSyncedInSettings = !first.SidebarUnseenDotsCheckedForSmoke && !first.AppSettingsUnseenDotsCheckedForSmoke;

                first.SetAppSettingsUnseenDotsForSmoke(true);
                bool settingsToSidebar = first.ShowUnseenDotsForSmoke
                    && first.AppSettingsUnseenDotsCheckedForSmoke
                    && first.SidebarUnseenDotsCheckedForSmoke
                    && first.VisibleUnseenDotCountForSmoke == unseenCount
                    && unseenCount == 2;
                first.CloseTopmostOverlayForSmoke();
                first.SetSidebarUnseenDotsForSmoke(false);
                bool sidebarToSettings = !first.ShowUnseenDotsForSmoke
                    && !first.SidebarUnseenDotsCheckedForSmoke
                    && !first.AppSettingsUnseenDotsCheckedForSmoke
                    && first.VisibleUnseenDotCountForSmoke == 0;
                first.OpenAppSettingsForSmoke();
                await first.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool settingsReopenedSynced = !first.AppSettingsUnseenDotsCheckedForSmoke;
                first.SetAppSettingsUnseenDotsForSmoke(true);
                ViewerState? persistedOn = ReadPersistedState(statePath);
                bool persistedEnabled = persistedOn?.ShowUnseenDots == true;
                first.Close();

                var reload = HiddenWindow();
                windows.Add(reload);
                reload.Show();
                reload.OpenAppSettingsForSmoke();
                await reload.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool reloadSynced = reload.ShowUnseenDotsForSmoke
                    && reload.SidebarUnseenDotsCheckedForSmoke
                    && reload.AppSettingsUnseenDotsCheckedForSmoke;
                bool reloadSettingsFocused = reload.FocusAppSettingsUnseenDotsForSmoke() && reload.IsAppSettingsUnseenDotsFocusedForSmoke;
                reload.Close();

                File.WriteAllText(statePath, "{\"Version\":1,\"futureDisplay\":{\"mode\":\"preserve\"}}");
                var migration = HiddenWindow();
                windows.Add(migration);
                migration.Show();
                migration.OpenAppSettingsForSmoke();
                await migration.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool migrationDefaultOff = !migration.ShowUnseenDotsForSmoke
                    && !migration.SidebarUnseenDotsCheckedForSmoke
                    && !migration.AppSettingsUnseenDotsCheckedForSmoke;
                migration.Close();
                bool migrationUnknownPreserved = File.ReadAllText(statePath).Contains("futureDisplay", StringComparison.Ordinal);

                bool seenByteIdentical = string.Equals(seenBefore, FileFingerprint(seenPath), StringComparison.Ordinal);
                bool cacheIsolation = string.Equals(favoritesBefore, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal)
                    && string.Equals(recentAfterFolderOpen, FileFingerprint(recentPath), StringComparison.Ordinal);
                bool sourceUntouched = string.Equals(sourceBefore, FolderFingerprint(folder), StringComparison.Ordinal);
                string rootPrefix = Path.GetFullPath(smokeRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                bool isolated = new[] { statePath, favoritesPath, seenPath, recentPath, jobsPath }
                    .All(path => Path.GetFullPath(path).StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase));
                bool residueFree = NoPersistenceResidue(smokeRoot);
                ok = defaultOff && defaultSyncedInSettings && sidebarFocused && settingsFocused && accessible
                    && settingsToSidebar && sidebarToSettings && settingsReopenedSynced && persistedEnabled
                    && reloadSynced && reloadSettingsFocused && migrationDefaultOff && migrationUnknownPreserved
                    && seenByteIdentical && cacheIsolation && sourceUntouched && isolated && residueFree;
                result = new
                {
                    ok,
                    message = ok
                        ? "sidebar and App Settings unseen-dot controls share one persisted display setting without mutating Seen or unrelated cache"
                        : "unseen-dot Settings parity smoke did not meet its synchronization and isolation contract",
                    defaultOff,
                    defaultSyncedInSettings,
                    sidebarFocused,
                    settingsFocused,
                    accessible,
                    settingsToSidebar,
                    sidebarToSettings,
                    settingsReopenedSynced,
                    persistedEnabled,
                    reloadSynced,
                    reloadSettingsFocused,
                    migrationDefaultOff,
                    migrationUnknownPreserved,
                    unseenCount,
                    seenByteIdentical,
                    cacheIsolation,
                    sourceUntouched,
                    isolated,
                    residueFree,
                };
            }
            catch (Exception ex)
            {
                result = new { ok = false, message = ex.Message, smokeRoot };
            }
            finally
            {
                foreach (MainWindow window in windows)
                {
                    try { if (window.IsLoaded) window.Close(); } catch { }
                }
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteCrossRuntimeSharedStateResult(resultPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureScanCancellationSmoke(string resultPath)
    {
        static async Task<bool> WaitForPhaseAsync(MainWindow window, string phase, int timeoutMilliseconds = 4000)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (string.Equals(window.LoadPhaseForSmoke, phase, StringComparison.Ordinal))
                    return true;
                await Task.Delay(10);
            }
            return false;
        }

        static bool JsonContainsString(string path, string expected)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                return Contains(document.RootElement);
            }
            catch
            {
                return false;
            }

            bool Contains(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                    return string.Equals(element.GetString(), expected, StringComparison.OrdinalIgnoreCase);
                if (element.ValueKind == JsonValueKind.Object)
                    return element.EnumerateObject().Any(property => string.Equals(property.Name, expected, StringComparison.OrdinalIgnoreCase) || Contains(property.Value));
                if (element.ValueKind == JsonValueKind.Array)
                    return element.EnumerateArray().Any(Contains);
                return false;
            }
        }

        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-scan-cancel-" + Guid.NewGuid().ToString("N"));
        string sourceRoot = Path.Combine(smokeRoot, "sources");
        string enumerationA = Path.Combine(sourceRoot, "01 enumeration alpha");
        string enumerationB = Path.Combine(sourceRoot, "02 enumeration beta");
        string baselineFolder = Path.Combine(sourceRoot, "03 published baseline");
        string metadataFolder = Path.Combine(sourceRoot, "04 metadata canceled");
        string newerFolder = Path.Combine(sourceRoot, "05 immediate rescan wins");
        string statePath = Path.Combine(smokeRoot, "cache", "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "cache", "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "cache", "seen.json");
        string recentPath = Path.Combine(smokeRoot, "cache", "recent-folders.json");
        string jobsPath = Path.Combine(smokeRoot, "cache", "enhance", "jobs.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        foreach (string folder in new[] { enumerationA, enumerationB, baselineFolder, metadataFolder, newerFolder })
            Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(jobsPath)!);
        DateTime fixtureTime = new(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc);
        void WriteFolder(string folder, string prefix, int count)
        {
            for (int index = 0; index < count; index++)
            {
                string path = Path.Combine(folder, $"{prefix}-{index:00}.png");
                WriteSmokePng(path, 128, 96, Color.FromRgb((byte)(60 + index * 7 % 170), (byte)(90 + index * 11 % 150), 165));
                File.SetLastWriteTimeUtc(path, fixtureTime.AddMinutes(-index));
            }
        }
        WriteFolder(enumerationA, "enum-a", 4);
        WriteFolder(enumerationB, "enum-b", 3);
        WriteFolder(baselineFolder, "baseline", 2);
        WriteFolder(metadataFolder, "metadata", 8);
        WriteFolder(newerFolder, "newer", 2);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, "{\"Version\":2,\"futureScanMarker\":\"preserve\"}");
        File.WriteAllText(favoritesPath, "{}");
        File.WriteAllText(seenPath, "{}");
        File.WriteAllText(recentPath, "{\"version\":1,\"lastFolderSet\":[],\"recentFolderSets\":[],\"updatedAtUtc\":\"\",\"futureRecentMarker\":\"preserve\"}");
        File.WriteAllText(jobsPath, "{\"version\":1,\"jobs\":[]}");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);

        var sourceFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in new[] { enumerationA, enumerationB, baselineFolder, metadataFolder, newerFolder })
            sourceFingerprints[folder] = FolderFingerprint(folder);
        string favoritesBefore = FileFingerprint(favoritesPath);
        string jobsBefore = FileFingerprint(jobsPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var windows = new List<MainWindow>();
        var window = HiddenWindow();
        windows.Add(window);
        window.Show();
        window.Dispatcher.InvokeAsync(async () =>
        {
            object result;
            bool ok = false;
            try
            {
                int initialCatalog = window.CatalogCountForSmoke;
                string stateBeforeEnumeration = FileFingerprint(statePath);
                string seenBeforeEnumeration = FileFingerprint(seenPath);
                string recentBeforeEnumeration = FileFingerprint(recentPath);
                bool initiallyInactive = !window.CancelScanVisibleForSmoke && !window.CancelScanEnabledForSmoke
                    && window.ScanCancellationSurfaceContractForSmoke;

                window.ConfigureScanPhaseDelaysForSmoke(enumerationMilliseconds: 650, metadataMilliseconds: 0);
                Task enumerationTask = window.LoadFolderSetAsync([enumerationA, enumerationB]);
                bool enumerationPhase = await WaitForPhaseAsync(window, "enumeration")
                    && window.CancelScanVisibleForSmoke && window.CancelScanEnabledForSmoke;
                var enumerationCancelWatch = Stopwatch.StartNew();
                bool enumerationCancelAccepted = window.CancelActiveScanForSmoke();
                enumerationCancelWatch.Stop();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool enumerationCancelUi = window.LandingVisibleForSmoke
                    && !window.CancelScanVisibleForSmoke && !window.CancelScanEnabledForSmoke
                    && window.OpenFolderSetFocusedForSmoke
                    && window.ScanProgressForSmoke == 0
                    && window.ScanLabelForSmoke.Contains("canceled", StringComparison.OrdinalIgnoreCase)
                    && window.ScanMessageForSmoke.Contains("kept", StringComparison.OrdinalIgnoreCase);
                string canceledUiMessage = window.ScanMessageForSmoke;
                bool doubleCancelNoOp = !window.CancelActiveScanForSmoke()
                    && string.Equals(canceledUiMessage, window.ScanMessageForSmoke, StringComparison.Ordinal);
                bool enumerationDraftPreserved = window.LandingFolderSetForSmoke.SequenceEqual([enumerationA, enumerationB], StringComparer.OrdinalIgnoreCase);
                bool enumerationImmediateIsolation = string.Equals(stateBeforeEnumeration, FileFingerprint(statePath), StringComparison.Ordinal)
                    && string.Equals(seenBeforeEnumeration, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBeforeEnumeration, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && window.CurrentFolderSetForSmoke.Count == 0
                    && window.CatalogCountForSmoke == initialCatalog;
                await enumerationTask;
                bool enumerationLateIgnored = string.Equals(window.LoadPhaseForSmoke, "canceled", StringComparison.Ordinal)
                    && window.LandingVisibleForSmoke
                    && window.CurrentFolderSetForSmoke.Count == 0
                    && window.CatalogCountForSmoke == initialCatalog
                    && string.Equals(canceledUiMessage, window.ScanMessageForSmoke, StringComparison.Ordinal)
                    && string.Equals(stateBeforeEnumeration, FileFingerprint(statePath), StringComparison.Ordinal)
                    && string.Equals(seenBeforeEnumeration, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBeforeEnumeration, FileFingerprint(recentPath), StringComparison.Ordinal);

                window.ConfigureScanPhaseDelaysForSmoke(0, 0);
                await window.LoadFolderAsync(baselineFolder);
                List<string> baselineNames = window.AllFileNamesForSmoke;
                bool baselinePublished = !window.LandingVisibleForSmoke
                    && window.CurrentFolderSetForSmoke.SequenceEqual([baselineFolder], StringComparer.OrdinalIgnoreCase)
                    && baselineNames.Count == 2 && baselineNames.All(name => name.StartsWith("baseline-", StringComparison.Ordinal));
                string stateBeforeMetadataCancel = FileFingerprint(statePath);
                string seenBeforeMetadataCancel = FileFingerprint(seenPath);
                string recentBeforeMetadataCancel = FileFingerprint(recentPath);

                window.ConfigureScanPhaseDelaysForSmoke(enumerationMilliseconds: 0, metadataMilliseconds: 900);
                Task metadataTask = window.LoadFolderAsync(metadataFolder);
                bool metadataPhase = await WaitForPhaseAsync(window, "metadata")
                    && window.CancelScanVisibleForSmoke && window.CancelScanEnabledForSmoke;
                var metadataCancelWatch = Stopwatch.StartNew();
                bool metadataCancelAccepted = window.CancelActiveScanForSmoke();
                metadataCancelWatch.Stop();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool metadataCancelUi = window.LandingVisibleForSmoke
                    && window.OpenFolderSetFocusedForSmoke
                    && !window.CancelScanVisibleForSmoke && !window.CancelScanEnabledForSmoke
                    && window.ScanProgressForSmoke == 0
                    && window.LandingFolderSetForSmoke.SequenceEqual([metadataFolder], StringComparer.OrdinalIgnoreCase);
                bool metadataCancelIsolation = string.Equals(stateBeforeMetadataCancel, FileFingerprint(statePath), StringComparison.Ordinal)
                    && string.Equals(seenBeforeMetadataCancel, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBeforeMetadataCancel, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && window.CurrentFolderSetForSmoke.SequenceEqual([baselineFolder], StringComparer.OrdinalIgnoreCase)
                    && window.AllFileNamesForSmoke.SequenceEqual(baselineNames, StringComparer.OrdinalIgnoreCase);

                window.ConfigureScanPhaseDelaysForSmoke(0, 0);
                Task newerTask = window.LoadFolderAsync(newerFolder);
                await newerTask;
                bool newerCompletedBeforeLateCanceledTask = !metadataTask.IsCompleted;
                await metadataTask;
                List<string> finalNames = window.AllFileNamesForSmoke;
                ViewerState? finalState = ReadPersistedState(statePath);
                bool newerRunWon = !window.LandingVisibleForSmoke
                    && string.Equals(window.LoadPhaseForSmoke, "idle", StringComparison.Ordinal)
                    && !window.CancelScanVisibleForSmoke && !window.CancelScanEnabledForSmoke
                    && window.CurrentFolderSetForSmoke.SequenceEqual([newerFolder], StringComparer.OrdinalIgnoreCase)
                    && window.LandingFolderSetForSmoke.SequenceEqual([newerFolder], StringComparer.OrdinalIgnoreCase)
                    && finalNames.Count == 2 && finalNames.All(name => name.StartsWith("newer-", StringComparison.Ordinal))
                    && finalState?.LastFolderSet?.SequenceEqual([newerFolder], StringComparer.OrdinalIgnoreCase) == true
                    && string.Equals(window.LastLoadMetrics?.Folder, newerFolder, StringComparison.OrdinalIgnoreCase);
                bool canceledFoldersNeverPublished = !JsonContainsString(statePath, enumerationA)
                    && !JsonContainsString(statePath, enumerationB)
                    && !JsonContainsString(statePath, metadataFolder)
                    && !JsonContainsString(recentPath, enumerationA)
                    && !JsonContainsString(recentPath, enumerationB)
                    && !JsonContainsString(recentPath, metadataFolder)
                    && !JsonContainsString(seenPath, enumerationA)
                    && !JsonContainsString(seenPath, enumerationB)
                    && !JsonContainsString(seenPath, metadataFolder);
                bool successfulRunsPersisted = JsonContainsString(recentPath, baselineFolder)
                    && JsonContainsString(recentPath, newerFolder)
                    && JsonContainsString(seenPath, Path.Combine(baselineFolder, "baseline-00.png"))
                    && JsonContainsString(seenPath, Path.Combine(newerFolder, "newer-00.png"));
                bool unknownFieldsPreserved = JsonContainsString(statePath, "preserve")
                    && JsonContainsString(recentPath, "preserve");
                bool sourceUntouched = sourceFingerprints.All(pair => string.Equals(pair.Value, FolderFingerprint(pair.Key), StringComparison.Ordinal));
                bool unrelatedCacheUntouched = string.Equals(favoritesBefore, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal);
                string rootPrefix = Path.GetFullPath(smokeRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                bool isolated = new[] { statePath, favoritesPath, seenPath, recentPath, jobsPath }
                    .All(path => Path.GetFullPath(path).StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase));
                bool residueFree = NoPersistenceResidue(smokeRoot);
                bool responsive = enumerationCancelWatch.ElapsedMilliseconds < 100 && metadataCancelWatch.ElapsedMilliseconds < 100;
                bool loadCtsBalanced = window.LoadCtsCreatedCountForSmoke == 4
                    && window.LoadCtsRetiredCountForSmoke == window.LoadCtsCreatedCountForSmoke;
                ok = initiallyInactive && enumerationPhase && enumerationCancelAccepted && enumerationCancelUi
                    && doubleCancelNoOp && enumerationDraftPreserved && enumerationImmediateIsolation && enumerationLateIgnored
                    && baselinePublished && metadataPhase && metadataCancelAccepted && metadataCancelUi && metadataCancelIsolation
                    && newerCompletedBeforeLateCanceledTask && newerRunWon && canceledFoldersNeverPublished
                    && successfulRunsPersisted && unknownFieldsPreserved && sourceUntouched && unrelatedCacheUntouched
                    && isolated && residueFree && responsive && loadCtsBalanced;
                result = new
                {
                    ok,
                    message = ok
                        ? "enumeration and metadata cancellation kept the draft and stores intact; an immediate newer rescan won over delayed canceled work"
                        : "scan cancellation smoke did not meet its focus, publication, generation, or isolation contract",
                    initiallyInactive,
                    enumerationPhase,
                    enumerationCancelAccepted,
                    enumerationCancelMs = enumerationCancelWatch.ElapsedMilliseconds,
                    enumerationCancelUi,
                    doubleCancelNoOp,
                    enumerationDraftPreserved,
                    enumerationImmediateIsolation,
                    enumerationLateIgnored,
                    baselinePublished,
                    metadataPhase,
                    metadataCancelAccepted,
                    metadataCancelMs = metadataCancelWatch.ElapsedMilliseconds,
                    metadataCancelUi,
                    metadataCancelIsolation,
                    newerCompletedBeforeLateCanceledTask,
                    newerRunWon,
                    canceledFoldersNeverPublished,
                    successfulRunsPersisted,
                    unknownFieldsPreserved,
                    sourceUntouched,
                    unrelatedCacheUntouched,
                    isolated,
                    residueFree,
                    loadCtsBalanced,
                };
            }
            catch (Exception ex)
            {
                result = new { ok = false, message = ex.Message, smokeRoot };
            }
            finally
            {
                foreach (MainWindow candidate in windows)
                {
                    try { if (candidate.IsLoaded) candidate.Close(); } catch { }
                }
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteCrossRuntimeSharedStateResult(resultPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
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

            if (args.Contains("--wait-preview-metadata") && !string.IsNullOrWhiteSpace(win.SelectedFileNameForSmoke))
                await win.WaitForPreviewPngMetadataForSmokeAsync(win.SelectedFileNameForSmoke!);

            win.ShowScreen(screen);
            if (args.Contains("--show-app-settings"))
                win.OpenAppSettingsForSmoke();
            if (args.Contains("--show-folder-drop-affordance"))
                win.SetFolderDropAffordanceForSmoke(screen.Equals("landing", StringComparison.OrdinalIgnoreCase), visible: true);
            if (screen.Equals("modal", StringComparison.OrdinalIgnoreCase) && args.Contains("--show-modal-metadata"))
                win.ToggleModalMetadataSidebarForSmoke();
            if (screen.Equals("modal", StringComparison.OrdinalIgnoreCase) && args.Contains("--wait-modal-full-decode")
                && !await win.WaitForModalFullDecodeForSmokeAsync())
                throw new InvalidOperationException("modal full-resolution decode did not complete before the screenshot");
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

    private void CaptureCrossRuntimeSharedStateSmoke(string resultPath, string[] args)
    {
        string? favoritesPath = ArgValue(args, "--favorites-path");
        string? seenPath = ArgValue(args, "--seen-path");
        string? keyRoot = ArgValue(args, "--key-root");
        int iterations = ArgInt(args, "--iterations", 20);
        if (string.IsNullOrWhiteSpace(favoritesPath)
            || string.IsNullOrWhiteSpace(seenPath)
            || string.IsNullOrWhiteSpace(keyRoot)
            || iterations < 1)
        {
            WriteCrossRuntimeSharedStateResult(resultPath, new
            {
                ok = false,
                message = "missing required --favorites-path, --seen-path, --key-root, or valid --iterations",
                iterations,
            });
            Shutdown(1);
            return;
        }

        favoritesPath = Path.GetFullPath(favoritesPath);
        seenPath = Path.GetFullPath(seenPath);
        keyRoot = Path.GetFullPath(keyRoot);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.InvokeAsync(async () =>
        {
            bool ok = true;
            string? error = null;
            try
            {
                Directory.CreateDirectory(keyRoot);
                for (int index = 0; index < iterations; index++)
                {
                    string favoriteKey = Path.Combine(keyRoot, $"wpf-favorite-{index:D2}.png");
                    string seenKey = Path.Combine(keyRoot, $"wpf-seen-{index:D2}.png");
                    int level = (index % 5) + 1;
                    bool wrote = await Task.Run(() => PhotoViewer.Wpf.MainWindow.TryMergeSharedStateForSmoke(
                        favoritesPath,
                        favoriteKey,
                        level,
                        seenPath,
                        seenKey));
                    if (!wrote)
                        throw new InvalidOperationException($"WPF shared-state merge failed at iteration {index}.");
                }
            }
            catch (Exception ex)
            {
                ok = false;
                error = ex.Message;
            }

            WriteCrossRuntimeSharedStateResult(resultPath, new
            {
                ok,
                message = ok ? "WPF shared favorites and seen writer completed" : error,
                iterations,
                favoriteWrites = ok ? iterations : 0,
                seenWrites = ok ? iterations : 0,
                favoritesPath,
                seenPath,
                keyRoot,
            });
            Shutdown(ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private static void WriteCrossRuntimeSharedStateResult(string resultPath, object result)
    {
        string fullPath = Path.GetFullPath(resultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void CaptureCrossRuntimeRecentSmoke(string resultPath, string[] args)
    {
        string? recentPath = ArgValue(args, "--recent-path");
        string? keyRoot = ArgValue(args, "--key-root");
        int iterations = ArgInt(args, "--iterations", 20);
        if (string.IsNullOrWhiteSpace(recentPath) || string.IsNullOrWhiteSpace(keyRoot) || iterations < 1)
        {
            WriteCrossRuntimeSharedStateResult(resultPath, new { ok = false, message = "missing --recent-path, --key-root, or valid --iterations", iterations });
            Shutdown(1);
            return;
        }

        recentPath = Path.GetFullPath(recentPath);
        keyRoot = Path.GetFullPath(keyRoot);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.InvokeAsync(async () =>
        {
            bool ok = true;
            string? error = null;
            try
            {
                Directory.CreateDirectory(keyRoot);
                string marker = Path.Combine(keyRoot, "wpf-latest");
                for (int index = 0; index < iterations; index++)
                {
                    bool wrote = await Task.Run(() => PhotoViewer.Wpf.MainWindow.TryMergeSharedRecentForSmoke(recentPath, marker));
                    if (!wrote)
                        throw new InvalidOperationException($"WPF shared recent merge failed at iteration {index}.");
                }
            }
            catch (Exception ex)
            {
                ok = false;
                error = ex.Message;
            }

            WriteCrossRuntimeSharedStateResult(resultPath, new
            {
                ok,
                message = ok ? "WPF shared recent writer completed" : error,
                iterations,
                recentWrites = ok ? iterations : 0,
                recentPath,
                keyRoot,
            });
            Shutdown(ok ? 0 : 1);
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

                first.ShowModalForShot();
                bool modalOpened = first.ModalVisibleForSmoke;
                bool modalIncreased = first.AdjustModalFavoriteForSmoke(1);
                int modalLevelAfterIncrease = first.ModalFavoriteLevelForSmoke;
                bool modalDecreased = first.AdjustModalFavoriteForSmoke(-1);
                int modalLevelAfterDecrease = first.ModalFavoriteLevelForSmoke;
                first.CloseModalForSmoke();

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
                    && modalOpened
                    && modalIncreased
                    && modalLevelAfterIncrease == 1
                    && modalDecreased
                    && modalLevelAfterDecrease == 0
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
                    ok ? "modal favorite +1/-1 display, level adjustment, clear, filter, and reload persistence passed" : "favorite level adjustment did not match expected 0..5 behavior",
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

    private void CaptureFavoriteFilterSmoke(string resultPath)
    {
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-favorite-filter-smoke-" + Guid.NewGuid().ToString("N"));
        FavoriteFilterSmokeFixture fixture = PrepareFavoriteFilterSmokeFolder(smokeRoot);
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            FavoriteFilterSmokeResult result;
            try
            {
                await first.LoadFolderAsync(fixture.Folder);
                first.SetSortByForSmoke("name");
                int allCount = first.FilteredCountForSmoke;
                List<string> allOrder = first.FilteredFileNamesForSmoke(10);

                bool assignedLevel1 = first.SelectFileNameForSmoke(fixture.Level1Name)
                    && first.SetSelectedFavoriteLevelForSmoke(1);
                bool assignedLevel3 = first.SelectFileNameForSmoke(fixture.Level3Name)
                    && first.SetSelectedFavoriteLevelForSmoke(3);
                bool assignedLevel5 = first.SelectFileNameForSmoke(fixture.Level5Name)
                    && first.SetSelectedFavoriteLevelForSmoke(5);
                int storeCountAfterAssign = first.FavoriteStoreCountForSmoke;

                first.ClearFavoriteFiltersForSmoke();
                bool selectedLevel1 = first.SelectFileNameForSmoke(fixture.Level1Name);
                first.SetFavoriteOnlyFilterForSmoke(true);
                first.SetFavoriteFilterLevelsForSmoke(1);
                int favoritesLv1Count = first.FilteredCountForSmoke;
                List<string> favoritesLv1Order = first.FilteredFileNamesForSmoke(10);

                first.SetFavoriteFilterLevelsForSmoke(3);
                int favoritesLv3Count = first.FilteredCountForSmoke;
                List<string> favoritesLv3Order = first.FilteredFileNamesForSmoke(10);
                string? selectedAfterLv3 = first.SelectedFileNameForSmoke;

                first.SetFavoriteFilterLevelsForSmoke(5);
                int favoritesLv5Count = first.FilteredCountForSmoke;
                List<string> favoritesLv5Order = first.FilteredFileNamesForSmoke(10);

                first.SetUnfavoriteOnlyFilterForSmoke(true);
                int unratedCount = first.FilteredCountForSmoke;
                List<string> unratedOrder = first.FilteredFileNamesForSmoke(10);
                bool unratedIsExclusive = !first.ShowFavoritesOnlyForSmoke && first.ShowUnfavoriteOnlyForSmoke;

                first.ClearFavoriteFiltersForSmoke();
                int clearCount = first.FilteredCountForSmoke;
                List<string> clearOrder = first.FilteredFileNamesForSmoke(10);

                first.SetFavoriteOnlyFilterForSmoke(true);
                first.SetFavoriteFilterLevelsForSmoke(3);
                bool selectedLevel3 = first.SelectFileNameForSmoke(fixture.Level3Name);
                ViewerState? persisted = ReadPersistedState(statePath);
                first.Close();

                int persistedLevel1 = ReadFavoriteLevel(favoritesPath, Path.Combine(fixture.Folder, fixture.Level1Name));
                int persistedLevel3 = ReadFavoriteLevel(favoritesPath, Path.Combine(fixture.Folder, fixture.Level3Name));
                int persistedLevel5 = ReadFavoriteLevel(favoritesPath, Path.Combine(fixture.Folder, fixture.Level5Name));
                int persistedUnrated = ReadFavoriteLevel(favoritesPath, Path.Combine(fixture.Folder, fixture.UnratedName));

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(fixture.Folder);
                int restoredCount = second.FilteredCountForSmoke;
                List<string> restoredOrder = second.FilteredFileNamesForSmoke(10);
                bool restoredFavoriteOnly = second.ShowFavoritesOnlyForSmoke;
                bool restoredUnfavoriteOnly = second.ShowUnfavoriteOnlyForSmoke;
                int restoredFavoriteLevel = second.FavoriteFilterLevelsForSmoke.FirstOrDefault();
                string? restoredSelected = second.SelectedFileNameForSmoke;
                second.SetUnfavoriteOnlyFilterForSmoke(true);
                int reloadUnratedCount = second.FilteredCountForSmoke;
                List<string> reloadUnratedOrder = second.FilteredFileNamesForSmoke(10);
                second.Close();

                bool ok = allCount == 4
                    && allOrder.SequenceEqual(fixture.AllExpected, StringComparer.OrdinalIgnoreCase)
                    && assignedLevel1
                    && assignedLevel3
                    && assignedLevel5
                    && storeCountAfterAssign == 3
                    && selectedLevel1
                    && favoritesLv1Count == 1
                    && favoritesLv1Order.SequenceEqual([fixture.Level1Name], StringComparer.OrdinalIgnoreCase)
                    && favoritesLv3Count == 1
                    && favoritesLv3Order.SequenceEqual([fixture.Level3Name], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterLv3, fixture.Level3Name, StringComparison.OrdinalIgnoreCase)
                    && favoritesLv5Count == 1
                    && favoritesLv5Order.SequenceEqual(fixture.FavoritesLv5Expected, StringComparer.OrdinalIgnoreCase)
                    && unratedIsExclusive
                    && unratedCount == 1
                    && unratedOrder.SequenceEqual(fixture.UnratedExpected, StringComparer.OrdinalIgnoreCase)
                    && clearCount == 4
                    && clearOrder.SequenceEqual(fixture.AllExpected, StringComparer.OrdinalIgnoreCase)
                    && selectedLevel3
                    && persisted?.ShowFavoritesOnly == true
                    && persisted.ShowUnfavoriteOnly == false
                    && persisted.FavoriteFilterLevels is not null && persisted.FavoriteFilterLevels.SequenceEqual([3])
                    && persistedLevel1 == 1
                    && persistedLevel3 == 3
                    && persistedLevel5 == 5
                    && persistedUnrated == 0
                    && restoredFavoriteOnly
                    && !restoredUnfavoriteOnly
                    && restoredFavoriteLevel == 3
                    && restoredCount == 1
                    && restoredOrder.SequenceEqual([fixture.Level3Name], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(restoredSelected, fixture.Level3Name, StringComparison.OrdinalIgnoreCase)
                    && reloadUnratedCount == 1
                    && reloadUnratedOrder.SequenceEqual(fixture.UnratedExpected, StringComparer.OrdinalIgnoreCase);

                result = new FavoriteFilterSmokeResult
                {
                    Ok = ok,
                    Message = ok ? "exact favorite filters, unrated exclusivity, and level-set reload persistence passed" : "favorite filter behavior did not match the exact-level contract",
                    Folder = fixture.Folder,
                    FavoritesPath = favoritesPath,
                    StatePath = statePath,
                    SeenPath = seenPath,
                    AllCount = allCount,
                    AllOrder = allOrder,
                    StoreCountAfterAssign = storeCountAfterAssign,
                    FavoritesLv1Count = favoritesLv1Count,
                    FavoritesLv1Order = favoritesLv1Order,
                    FavoritesLv3Count = favoritesLv3Count,
                    FavoritesLv3Order = favoritesLv3Order,
                    SelectedAfterLv3 = selectedAfterLv3,
                    FavoritesLv5Count = favoritesLv5Count,
                    FavoritesLv5Order = favoritesLv5Order,
                    UnratedCount = unratedCount,
                    UnratedOrder = unratedOrder,
                    UnratedIsExclusive = unratedIsExclusive,
                    ClearCount = clearCount,
                    ClearOrder = clearOrder,
                    PersistedFavoriteOnly = persisted?.ShowFavoritesOnly,
                    PersistedUnfavoriteOnly = persisted?.ShowUnfavoriteOnly,
                    PersistedFavoriteFilterLevel = persisted?.FavoriteFilterLevel,
                    PersistedLevel1 = persistedLevel1,
                    PersistedLevel3 = persistedLevel3,
                    PersistedLevel5 = persistedLevel5,
                    PersistedUnrated = persistedUnrated,
                    RestoredFavoriteOnly = restoredFavoriteOnly,
                    RestoredUnfavoriteOnly = restoredUnfavoriteOnly,
                    RestoredFavoriteLevel = restoredFavoriteLevel,
                    RestoredCount = restoredCount,
                    RestoredOrder = restoredOrder,
                    RestoredSelected = restoredSelected,
                    ReloadUnratedCount = reloadUnratedCount,
                    ReloadUnratedOrder = reloadUnratedOrder,
                };
            }
            catch (Exception ex)
            {
                first.Close();
                result = new FavoriteFilterSmokeResult
                {
                    Ok = false,
                    Message = ex.Message,
                    Folder = fixture.Folder,
                    FavoritesPath = favoritesPath,
                    StatePath = statePath,
                    SeenPath = seenPath,
                };
            }
            finally
            {
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
            }

            WriteFavoriteFilterSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureP0ASmoke(string resultPath)
    {
        string root = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-p0a-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "fixture");
        Directory.CreateDirectory(folder);
        for (int level = 0; level <= 5; level++)
            WriteSmokePng(Path.Combine(folder, $"level-{level}.png"), 80, 60, Color.FromRgb((byte)(40 + level * 25), 90, 160));
        string favorites = Path.Combine(root, "favorites.json");
        string seen = Path.Combine(root, "seen.json");
        string recent = Path.Combine(root, "recent.json");
        string jobs = Path.Combine(root, "jobs.json");
        string state = Path.Combine(root, "state.json");
        string? oldFavorites = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? oldSeen = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? oldState = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? oldRecent = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? oldJobs = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favorites);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seen);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", state);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recent);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var first = HiddenWindow();
        first.Show();
        first.Dispatcher.InvokeAsync(async () =>
        {
            object result;
            bool ok = false;
            try
            {
                await first.LoadFolderAsync(folder);
                first.SetSortByForSmoke("name");
                int dotsDefault = first.VisibleUnseenDotCountForSmoke;
                int unseen = first.UnseenCountForSmoke;
                first.SetShowUnseenDotsForSmoke(true);
                int dotsOn = first.VisibleUnseenDotCountForSmoke;
                first.SetShowUnseenDotsForSmoke(false);
                int dotsOff = first.VisibleUnseenDotCountForSmoke;
                for (int level = 1; level <= 5; level++)
                {
                    first.SelectFileNameForSmoke($"level-{level}.png");
                    first.SetSelectedFavoriteLevelForSmoke(level);
                }
                first.SetFavoriteOnlyFilterForSmoke(true);
                int all = first.FilteredCountForSmoke;
                first.SetFavoriteFilterLevelsForSmoke(1);
                int lv1 = first.FilteredCountForSmoke;
                first.SetFavoriteFilterLevelsForSmoke(1, 4);
                int lv14 = first.FilteredCountForSmoke;
                first.SetFavoriteFilterLevelsForSmoke();
                int allAfterClear = first.FilteredCountForSmoke;
                first.SetUnfavoriteOnlyFilterForSmoke(true);
                int unrated = first.FilteredCountForSmoke;
                bool foldersDefault = first.FoldersSectionExpandedForSmoke;
                first.ToggleFoldersSectionForSmoke();
                bool foldersCollapsed = !first.FoldersSectionExpandedForSmoke;
                first.ToggleFoldersSectionForSmoke();
                first.SetFavoriteOnlyFilterForSmoke(true);
                first.SetFavoriteFilterLevelsForSmoke(1, 4);
                first.Close();
                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(folder);
                bool reloaded = second.ShowFavoritesOnlyForSmoke && second.FavoriteFilterLevelsForSmoke.SequenceEqual([1, 4]);
                second.Close();
                File.WriteAllText(state, "{\"FavoriteFilterLevel\":2}");
                var migration = HiddenWindow();
                migration.Show();
                bool scalarMigration = migration.FavoriteFilterLevelsForSmoke.SequenceEqual([2]);
                migration.Close();
                ok = all == 5 && lv1 == 1 && lv14 == 2 && allAfterClear == 5 && unrated == 1
                    && foldersDefault && foldersCollapsed && dotsDefault == 0 && dotsOn == unseen && dotsOff == 0 && reloaded && scalarMigration;
                result = new { ok, message = ok ? "P0A sidebar contract passed" : "P0A sidebar contract failed", all, lv1, lv14, allAfterClear, unrated, foldersDefault, foldersCollapsed, dotsDefault, dotsOn, dotsOff, unseen, reloaded, scalarMigration, folder, favorites, seen, state };
            }
            catch (Exception ex)
            {
                first.Close();
                result = new { ok = false, message = ex.Message, folder, favorites, seen, state };
            }
            finally
            {
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", oldFavorites);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", oldSeen);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", oldState);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", oldRecent);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", oldJobs);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureP0DSmoke(string resultPath)
    {
        const int fixtureCount = 5000;
        string root = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-p0d-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "fixture");
        string recycle = Path.Combine(root, "fake-recycle");
        string state = Path.Combine(root, "state.json");
        string favorites = Path.Combine(root, "favorites.json");
        string seen = Path.Combine(root, "seen.json");
        string recent = Path.Combine(root, "recent.json");
        string jobs = Path.Combine(root, "jobs.json");
        Directory.CreateDirectory(folder);
        for (int index = 0; index < fixtureCount; index++)
            WriteSmokePng(Path.Combine(folder, $"item-{index:0000}.png"), 4, 3, Color.FromRgb((byte)(30 + index % 180), 100, 160));
        File.WriteAllText(jobs, "{\"version\":1,\"jobs\":[]}");
        File.WriteAllText(state, "{\"favoriteFilterLevel\":2,\"futureFlag\":true}");
        string jobsHashBefore = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(jobs)));
        string? oldState = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? oldFavorites = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? oldSeen = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? oldJobs = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");
        string? oldRecent = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", state);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favorites);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seen);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobs);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recent);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            object result;
            bool ok = false;
            try
            {
                var watch = Stopwatch.StartNew();
                await win.LoadFolderAsync(folder);
                int catalog = win.CatalogCountForSmoke;
                int grid = win.GridRealizedCountForSmoke;
                ListVirtualizationProbe listProbe = await win.ProbeListVirtualizationForSmokeAsync();
                string externalFavorite = Path.Combine(root, "external-favorite.png");
                string externalSeen = Path.Combine(root, "external-seen.png");
                File.WriteAllText(favorites, JsonSerializer.Serialize(new Dictionary<string, int> { [externalFavorite] = 4 }));
                File.WriteAllText(seen, JsonSerializer.Serialize(new Dictionary<string, bool> { [externalSeen] = true }));
                bool favoriteLevels = win.SelectFileNameForSmoke("item-0100.png") && win.SetSelectedFavoriteLevelForSmoke(1)
                    && win.SelectFileNameForSmoke("item-0200.png") && win.SetSelectedFavoriteLevelForSmoke(5);
                bool seenMerged = win.MarkSelectedSeenForSmoke() && File.ReadAllText(seen).Contains(Path.GetFileName(externalSeen), StringComparison.OrdinalIgnoreCase);
                bool favoriteMerged = File.ReadAllText(favorites).Contains(Path.GetFileName(externalFavorite), StringComparison.OrdinalIgnoreCase);
                win.SetShowUnseenDotsForSmoke(false);
                bool dotsDisplayOnly = win.VisibleUnseenDotCountForSmoke == 0 && win.UnseenCountForSmoke > 0;
                win.ToggleFoldersSectionForSmoke();
                bool foldersCollapsed = !win.FoldersSectionExpandedForSmoke;
                win.SetGridModeForSmoke();
                await win.ScrollGridToMiddleForSmokeAsync();
                string? anchor = win.CaptureGridViewportAnchorForSmoke();
                bool zoom300 = win.SetGridZoomForSmoke(300); await win.WaitForGridZoomAnchorForSmokeAsync(); double drift300 = win.LastGridZoomAnchorDriftForSmoke;
                bool zoom80 = win.SetGridZoomForSmoke(80); await win.WaitForGridZoomAnchorForSmokeAsync(); double drift80 = win.LastGridZoomAnchorDriftForSmoke;
                bool zoom200 = win.ZoomResetForSmoke(); await win.WaitForGridZoomAnchorForSmokeAsync(); double drift200 = win.LastGridZoomAnchorDriftForSmoke;
                bool anchorStable = !string.IsNullOrWhiteSpace(anchor) && zoom300 && zoom80 && zoom200 && drift300 <= 8 && drift80 <= 8 && drift200 <= 8;
                win.SetRecycleBinDeleteBackendForSmoke(path => { Directory.CreateDirectory(recycle); File.Move(path, Path.Combine(recycle, Path.GetFileName(path))); return RecycleBinDeleteResult.Success; });
                win.SetConfirmBeforeDeleteForSmoke(false);
                var order = win.FilteredFileNamesForSmoke(fixtureCount);
                int middleIndex = order.Count / 2;
                string middle = order[middleIndex];
                string expectedNext = order[middleIndex + 1];
                win.SelectFileNameForSmoke(middle);
                bool deleted = win.RequestDeleteSelectedForSmoke() && string.Equals(win.SelectedFileNameForSmoke, expectedNext, StringComparison.OrdinalIgnoreCase);
                win.FlushStateForSmoke();
                bool stateUnknownPreserved = File.ReadAllText(state).Contains("futureFlag", StringComparison.OrdinalIgnoreCase);
                long workingSet = Process.GetCurrentProcess().WorkingSet64;
                LoadMetrics? metrics = win.LastLoadMetrics;
                string jobsHashAfter = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(jobs)));
                bool enhancementPassive = jobsHashBefore == jobsHashAfter && win.EnhancementJobsReadForSmoke == 0 && win.EnhancedCandidateCountForSmoke == 0;
                win.Close();
                var reload = HiddenWindow(); reload.Show();
                await reload.LoadFolderAsync(folder);
                bool stateReloaded = reload.ConfirmBeforeDeleteForSmoke == false && reload.FavoriteStoreCountForSmoke >= 2 && File.Exists(state);
                reload.Close();
                File.WriteAllText(favorites, JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    [Path.Combine(folder, "item-0400.png")] = true,
                    [Path.Combine(folder, "item-0401.png")] = 9,
                }));
                File.WriteAllText(state, JsonSerializer.Serialize(new { Version = 1 }));
                var compatibility = HiddenWindow(); compatibility.Show();
                await compatibility.LoadFolderAsync(folder);
                bool legacyBooleanSelected = compatibility.SelectFileNameForSmoke("item-0400.png")
                    && string.Equals(compatibility.SelectedFileNameForSmoke, "item-0400.png", StringComparison.OrdinalIgnoreCase)
                    && compatibility.SelectedFavoriteLevelForSmoke == 1;
                bool clampedNumberSelected = compatibility.SelectFileNameForSmoke("item-0401.png")
                    && string.Equals(compatibility.SelectedFileNameForSmoke, "item-0401.png", StringComparison.OrdinalIgnoreCase)
                    && compatibility.SelectedFavoriteLevelForSmoke == 5;
                bool favoriteLegacyCompatibility = legacyBooleanSelected && clampedNumberSelected;
                compatibility.Close();
                var safety = HiddenWindow(); safety.Show();
                await safety.LoadFolderAsync(folder);
                File.WriteAllText(state, JsonSerializer.Serialize(new { Version = 1, futureState = new { keep = true } }));
                safety.FlushStateForSmoke();
                bool stateExternalUnknownPreserved = File.ReadAllText(state).Contains("futureState", StringComparison.Ordinal);
                File.WriteAllText(recent, JsonSerializer.Serialize(new { version = 1, lastFolderSet = Array.Empty<string>(), recentFolderSets = Array.Empty<string[]>(), updatedAtUtc = DateTimeOffset.UtcNow.ToString("O"), futureRecent = new { keep = true } }));
                safety.FlushStateForSmoke();
                bool recentUnknownPreserved = File.ReadAllText(recent).Contains("futureRecent", StringComparison.Ordinal);
                File.WriteAllText(recent, JsonSerializer.Serialize(new { version = 2, futureRecentVersion = true }));
                string futureRecentBefore = File.ReadAllText(recent);
                safety.FlushStateForSmoke();
                bool recentFutureVersionProtected = File.ReadAllText(recent) == futureRecentBefore;
                File.WriteAllText(favorites, "{\"broken\":{}}");
                string favoriteMalformedBefore = File.ReadAllText(favorites);
                safety.SelectFileNameForSmoke("item-0300.png");
                bool favoriteMalformedProtected = !safety.AdjustSelectedFavoriteForSmoke(1) && File.ReadAllText(favorites) == favoriteMalformedBefore;
                File.WriteAllText(seen, "{\"broken\":[]}");
                string seenMalformedBefore = File.ReadAllText(seen);
                safety.SelectFileNameForSmoke("item-0301.png");
                bool seenMalformedProtected = !safety.MarkSelectedSeenForSmoke() && File.ReadAllText(seen) == seenMalformedBefore;
                PersistenceLockProbe lockProbe = await PhotoViewer.Wpf.MainWindow.ProbePersistenceLockForSmokeAsync(Path.Combine(root, "lock-favorites.json"), Path.Combine(root, "writer-a.png"), Path.Combine(root, "writer-b.png"));
                safety.Close();
                File.WriteAllText(state, JsonSerializer.Serialize(new { Version = 1 }));
                var malformedStateWindow = HiddenWindow(); malformedStateWindow.Show();
                await malformedStateWindow.LoadFolderAsync(folder);
                File.WriteAllText(state, "{\"Version\":1,\"CardWidth\":{}}");
                string malformedStateBefore = File.ReadAllText(state);
                malformedStateWindow.FlushStateForSmoke();
                bool stateMalformedProtected = File.ReadAllText(state) == malformedStateBefore;
                malformedStateWindow.Close();
                File.WriteAllText(state, JsonSerializer.Serialize(new { Version = 1 }));
                var futureStateWindow = HiddenWindow(); futureStateWindow.Show();
                await futureStateWindow.LoadFolderAsync(folder);
                File.WriteAllText(state, JsonSerializer.Serialize(new { Version = 3, futureVersion = true }));
                string futureStateBefore = File.ReadAllText(state);
                futureStateWindow.FlushStateForSmoke();
                bool futureStateProtected = File.ReadAllText(state) == futureStateBefore;
                futureStateWindow.Close();
                watch.Stop();
                ok = catalog == fixtureCount && grid <= win.GridMaxRealizationCountForSmoke && listProbe.ListMode && listProbe.Recycling && listProbe.Bounded && favoriteLevels
                    && favoriteMerged && favoriteLegacyCompatibility && seenMerged && stateUnknownPreserved && stateExternalUnknownPreserved && recentUnknownPreserved && recentFutureVersionProtected && favoriteMalformedProtected && seenMalformedProtected && stateMalformedProtected && futureStateProtected && lockProbe.ConcurrentMerged && lockProbe.StaleRecovered && lockProbe.MalformedLockProtected && dotsDisplayOnly && foldersCollapsed && anchorStable && deleted && stateReloaded && enhancementPassive;
                result = new { ok, message = ok ? "P0D integrated 5000-image gate passed" : "P0D gate failed", folder, fixtureCount, catalog, grid, maxGrid = win.GridMaxRealizationCountForSmoke, listProbe, favoriteLevels, favoriteMerged, favoriteLegacyCompatibility, seenMerged, stateUnknownPreserved, stateExternalUnknownPreserved, recentUnknownPreserved, recentFutureVersionProtected, favoriteMalformedProtected, seenMalformedProtected, stateMalformedProtected, futureStateProtected, lockProbe, dotsDisplayOnly, foldersCollapsed, anchor, drift300, drift80, drift200, deleted, stateReloaded, enhancementPassive, jobs, jobsHashBefore, jobsHashAfter, workingSet, elapsedMs = watch.ElapsedMilliseconds, metrics };
            }
            catch (Exception ex) { result = new { ok = false, message = ex.ToString(), folder, fixtureCount }; }
            finally
            {
                if (win.IsVisible) win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", oldState);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", oldFavorites);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", oldSeen);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", oldJobs);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", oldRecent);
                try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureP0CSmoke(string resultPath)
    {
        string root = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-p0c-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "fixture");
        string recycle = Path.Combine(root, "fake-recycle-bin");
        string outside = Path.Combine(root, "outside");
        string sibling = folder + "-sibling";
        string state = Path.Combine(root, "state.json");
        string favorites = Path.Combine(root, "favorites.json");
        string seen = Path.Combine(root, "seen.json");
        string recent = Path.Combine(root, "recent.json");
        string jobs = Path.Combine(root, "jobs.json");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(sibling);
        foreach (string name in new[] { "alpha.png", "bravo.png", "charlie.png" })
            WriteSmokePng(Path.Combine(folder, name), 32, 24, Color.FromRgb(90, 130, 190));
        string outsidePng = Path.Combine(outside, "outside.png");
        string siblingPng = Path.Combine(sibling, "sibling.png");
        string unsupported = Path.Combine(folder, "unsupported.txt");
        WriteSmokePng(outsidePng, 32, 24, Color.FromRgb(190, 90, 130));
        WriteSmokePng(siblingPng, 32, 24, Color.FromRgb(130, 90, 190));
        File.WriteAllText(unsupported, "not an image");
        string? previousState = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavorites = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeen = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecent = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobs = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", state);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favorites);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seen);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recent);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            object result;
            bool ok = false;
            try
            {
                // The injected backend moves only fixture files; it deliberately contains no hard-delete fallback.
                win.SetRecycleBinDeleteBackendForSmoke(path =>
                {
                    Directory.CreateDirectory(recycle);
                    File.Move(path, Path.Combine(recycle, Path.GetFileName(path)));
                    return RecycleBinDeleteResult.Success;
                });
                await win.LoadFolderAsync(folder);
                var original = win.FilteredFileNamesForSmoke();
                string middle = original[1];
                string next = original[2];
                bool selectedMiddle = win.SelectFileNameForSmoke(middle);
                bool modalOpenedMiddle = win.OpenModalForSmoke();
                bool prompted = win.RequestDeleteSelectedForSmoke() && win.DeleteConfirmationVisibleForSmoke;
                string screenshot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resultPath))!, Path.GetFileNameWithoutExtension(resultPath) + "-modal-confirm.png");
                win.UpdateLayout();
                var visual = (FrameworkElement)win.Content;
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var screenshotEncoder = new PngBitmapEncoder();
                screenshotEncoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var screenshotStream = File.Create(screenshot)) screenshotEncoder.Save(screenshotStream);
                bool confirmationAboveModal = modalOpenedMiddle && win.ModalVisibleForSmoke && win.DeleteConfirmationZIndexForSmoke > win.ModalZIndexForSmoke && File.Exists(screenshot);
                int favoriteBeforeOverlay = win.SelectedFavoriteLevelForSmoke;
                bool confirmationShortcutsGuarded = !win.InvokePreviewKeyForSmoke(Key.F) && !win.InvokePreviewKeyForSmoke(Key.U) && !win.InvokePreviewKeyForSmoke(Key.Delete)
                    && win.SelectedFavoriteLevelForSmoke == favoriteBeforeOverlay && win.DeleteConfirmationVisibleForSmoke;
                bool dialogKeyboardPassThrough = !win.InvokePreviewKeyForSmoke(Key.Tab) && !win.InvokePreviewKeyForSmoke(Key.Space) && !win.InvokePreviewKeyForSmoke(Key.Enter)
                    && win.DeleteConfirmationVisibleForSmoke;
                bool escapeClosedConfirmation = win.InvokePreviewKeyForSmoke(Key.Escape) && !win.DeleteConfirmationVisibleForSmoke && win.ModalVisibleForSmoke;
                bool cancelledUnchanged = win.CatalogCountForSmoke == 3 && File.Exists(Path.Combine(folder, middle));

                win.OpenAppSettingsForSmoke();
                bool settingsShortcutsGuarded = win.AppSettingsVisibleForSmoke && !win.InvokePreviewKeyForSmoke(Key.F) && win.InvokePreviewKeyForSmoke(Key.Escape) && !win.AppSettingsVisibleForSmoke;

                bool promptedAgain = win.RequestDeleteSelectedForSmoke();
                win.ConfirmDeleteForSmoke(doNotAskAgain: true);
                bool middleToNext = !File.Exists(Path.Combine(folder, middle))
                    && string.Equals(win.SelectedFileNameForSmoke, next, StringComparison.OrdinalIgnoreCase)
                    && !win.ConfirmBeforeDeleteForSmoke && win.ModalVisibleForSmoke;

                var afterMiddle = win.FilteredFileNamesForSmoke();
                string last = afterMiddle[^1];
                string previous = afterMiddle[^2];
                win.SelectFileNameForSmoke(last);
                bool modalOpenedLast = win.OpenModalForSmoke();
                bool deletedLast = win.RequestDeleteSelectedForSmoke();
                bool lastToPrevious = modalOpenedLast && deletedLast && win.ModalVisibleForSmoke && string.Equals(win.SelectedFileNameForSmoke, previous, StringComparison.OrdinalIgnoreCase);
                string only = win.FilteredFileNamesForSmoke().Single();
                win.SelectFileNameForSmoke(only);
                bool deletedOnly = win.RequestDeleteSelectedForSmoke();
                bool onlyToEmpty = deletedOnly && win.CatalogCountForSmoke == 0 && win.SelectedPathForSmoke is null && !win.ModalVisibleForSmoke;

                // Filtered subset uses the same command and leaves a zero-item filtered view.
                WriteSmokePng(Path.Combine(folder, "subset-a.png"), 32, 24, Color.FromRgb(80, 150, 130));
                WriteSmokePng(Path.Combine(folder, "subset-b.png"), 32, 24, Color.FromRgb(130, 150, 80));
                await win.LoadFolderAsync(folder);
                win.SetSearchQuery("subset-b", persist: false);
                bool subsetSelected = win.SelectFileNameForSmoke("subset-b.png");
                bool filteredSubset = subsetSelected && win.FilteredCountForSmoke == 1 && win.RequestDeleteSelectedForSmoke() && win.FilteredCountForSmoke == 0;
                win.SetSearchQuery("", persist: false);

                string remaining = win.FilteredFileNamesForSmoke().First();
                bool outsideBlocked = !win.ValidateDeletePathForSmoke(outsidePng);
                bool siblingPrefixBlocked = !win.ValidateDeletePathForSmoke(siblingPng);
                bool indexBlocked = !win.ValidateDeletePathForSmoke(Path.Combine(folder, remaining), includeInCatalog: false);
                bool unsupportedBlocked = !win.ValidateDeletePathForSmoke(unsupported);
                win.SetCanonicalPathResolverForSmoke(_ => throw new IOException("synthetic realpath failure"));
                bool realpathBlocked = !win.ValidateDeletePathForSmoke(Path.Combine(folder, remaining));
                win.SetCanonicalPathResolverForSmoke(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(folder) : Path.GetFullPath(outsidePng));
                bool parentLinkEscapeBlocked = !win.ValidateDeletePathForSmoke(Path.Combine(folder, remaining));
                win.ResetCanonicalPathResolverForSmoke();

                WriteSmokePng(Path.Combine(folder, "failure.png"), 32, 24, Color.FromRgb(170, 90, 100));
                await win.LoadFolderAsync(folder);
                string other = win.FilteredFileNamesForSmoke().First(name => !string.Equals(name, "failure.png", StringComparison.OrdinalIgnoreCase));
                win.SelectFileNameForSmoke(other);
                bool favoriteOther = win.SetSelectedFavoriteLevelForSmoke(3);
                bool seenOther = win.MarkSelectedSeenForSmoke();
                int favoriteCountBefore = win.FavoriteStoreCountForSmoke;
                int seenCountBefore = win.SeenStoreCountForSmoke;
                string failure = win.FilteredFileNamesForSmoke().First(name => !string.Equals(name, other, StringComparison.OrdinalIgnoreCase));
                win.SelectFileNameForSmoke(failure);
                win.SetRecycleBinDeleteBackendForSmoke(_ => RecycleBinDeleteResult.Failed("synthetic recycle failure"));
                int catalogBeforeFailure = win.CatalogCountForSmoke;
                bool recycleFailure = !win.RequestDeleteSelectedForSmoke()
                    && File.Exists(Path.Combine(folder, failure))
                    && win.CatalogCountForSmoke == catalogBeforeFailure
                    && win.FavoriteStoreCountForSmoke == favoriteCountBefore
                    && win.SeenStoreCountForSmoke == seenCountBefore
                    && win.DeleteStatusForSmoke.Contains("Retry", StringComparison.OrdinalIgnoreCase)
                    && win.DeleteStatusVisibleForSmoke && win.DeleteStatusRetryVisibleForSmoke && win.DeleteStatusZIndexForSmoke > win.ModalZIndexForSmoke;
                bool persistedDoNotAskAgain = File.Exists(state) && File.ReadAllText(state).Contains("\"ConfirmBeforeDelete\": false", StringComparison.OrdinalIgnoreCase);
                bool fakeRecycleOnly = Directory.Exists(recycle) && Directory.EnumerateFiles(recycle).Any() && !Directory.EnumerateFiles(folder).Any(path => Path.GetFileName(path) == middle);

                ok = selectedMiddle && prompted && confirmationAboveModal && confirmationShortcutsGuarded && dialogKeyboardPassThrough && escapeClosedConfirmation && cancelledUnchanged && settingsShortcutsGuarded && promptedAgain && middleToNext && lastToPrevious && onlyToEmpty
                    && filteredSubset && outsideBlocked && siblingPrefixBlocked && indexBlocked && unsupportedBlocked && realpathBlocked && parentLinkEscapeBlocked && favoriteOther && seenOther
                    && recycleFailure && persistedDoNotAskAgain && fakeRecycleOnly;
                result = new { ok, message = ok ? "P0C guarded Recycle Bin workflow passed (fake injected backend; no real Recycle Bin integration run)" : "P0C smoke failed", folder, recycle, screenshot, selectedMiddle, prompted, confirmationAboveModal, confirmationShortcutsGuarded, dialogKeyboardPassThrough, escapeClosedConfirmation, cancelledUnchanged, settingsShortcutsGuarded, promptedAgain, middleToNext, lastToPrevious, onlyToEmpty, filteredSubset, outsideBlocked, siblingPrefixBlocked, indexBlocked, unsupportedBlocked, realpathBlocked, parentLinkEscapeBlocked, favoriteOther, seenOther, recycleFailure, persistedDoNotAskAgain, fakeRecycleOnly, deleteStatus = win.DeleteStatusForSmoke };
            }
            catch (Exception ex)
            {
                result = new { ok = false, message = ex.Message, folder, recycle };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousState);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavorites);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeen);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecent);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobs);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureP0BSmoke(string resultPath)
    {
        const int fixtureCount = 1201;
        string root = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-p0b-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "fixture");
        Directory.CreateDirectory(folder);
        for (int index = 0; index < fixtureCount; index++)
            WriteSmokePng(Path.Combine(folder, $"item-{index:0000}.png"), 16, 12, Color.FromRgb((byte)(index % 200 + 30), 80, 140));
        string state = Path.Combine(root, "state.json");
        string favorites = Path.Combine(root, "favorites.json");
        string seen = Path.Combine(root, "seen.json");
        string recent = Path.Combine(root, "recent.json");
        string jobs = Path.Combine(root, "jobs.json");
        string? previousState = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavorites = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeen = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecent = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobs = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", state);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favorites);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seen);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recent);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            object result;
            bool ok = false;
            try
            {
                await win.LoadFolderAsync(folder);
                int total = win.FilteredCountForSmoke;
                int realized = win.GridRealizedCountForSmoke;
                int maxRealized = win.GridMaxRealizationCountForSmoke;
                win.SetSearchQuery("item-1200", persist: false);
                bool selected = win.SelectFileNameForSmoke("item-1200.png");
                bool modal = win.OpenModalForSmoke();
                win.CloseModalForSmoke();
                win.SetSearchQuery("", persist: false);
                for (int index = 0; index < 4; index++)
                    win.RealizeNextGridBatchForSmoke();
                bool scrolledToMiddle = await win.ScrollGridToMiddleForSmokeAsync();
                string? anchor = win.CaptureGridViewportAnchorForSmoke();
                double sidebarBefore = win.SidebarWidthForSmoke;
                double rightBefore = win.RightPanelWidthForSmoke;
                double listThumbBefore = win.ListThumbnailSizeForSmoke;
                bool zoom300 = win.SetGridZoomForSmoke(300);
                await win.WaitForGridZoomAnchorForSmokeAsync();
                bool anchorAt300 = win.GridContainsFileForSmoke(anchor);
                double drift300 = win.LastGridZoomAnchorDriftForSmoke;
                bool identity300 = string.Equals(Path.GetFileName(win.LastGridZoomAnchorPathForSmoke), anchor, StringComparison.OrdinalIgnoreCase);
                bool zoom80 = win.SetGridZoomForSmoke(80);
                await win.WaitForGridZoomAnchorForSmokeAsync();
                bool anchorAt80 = win.GridContainsFileForSmoke(anchor);
                double drift80 = win.LastGridZoomAnchorDriftForSmoke;
                bool identity80 = string.Equals(Path.GetFileName(win.LastGridZoomAnchorPathForSmoke), anchor, StringComparison.OrdinalIgnoreCase);
                bool reset = win.ZoomResetForSmoke() && Math.Abs(win.CardWidthForSmoke - 200) < 0.01;
                await win.WaitForGridZoomAnchorForSmokeAsync();
                double drift200 = win.LastGridZoomAnchorDriftForSmoke;
                bool identity200 = string.Equals(Path.GetFileName(win.LastGridZoomAnchorPathForSmoke), anchor, StringComparison.OrdinalIgnoreCase);
                bool panelsStable = Math.Abs(sidebarBefore - win.SidebarWidthForSmoke) < 0.01 && Math.Abs(rightBefore - win.RightPanelWidthForSmoke) < 0.01;
                bool listMode = win.SetListModeForSmoke();
                double listThumbAfter = win.ListThumbnailSizeForSmoke;
                bool listVirtualized = win.ListUsesRecyclingVirtualizationForSmoke;
                LoadMetrics? metrics = win.LastLoadMetrics;
                ok = total == fixtureCount && realized <= maxRealized && selected && modal && zoom300 && zoom80 && reset
                    && scrolledToMiddle && anchorAt300 && anchorAt80 && identity300 && identity80 && identity200 && drift300 <= 8 && drift80 <= 8 && drift200 <= 8 && panelsStable && listMode && listVirtualized && Math.Abs(listThumbBefore - listThumbAfter) < 0.01;
                result = new { ok, message = ok ? "P0B catalog, viewport zoom anchor, and list virtualization passed" : "P0B smoke failed", folder, fixtureCount, total, realized, maxRealized, selected, modal, scrolledToMiddle, anchor, zoom300, anchorAt300, identity300, drift300, zoom80, anchorAt80, identity80, drift80, reset, identity200, drift200, panelsStable, sidebarBefore, rightBefore, listMode, listVirtualized, listThumbBefore, listThumbAfter, scanMs = metrics?.ScanMs, materializeMs = metrics?.MaterializeMs, thumbnailMs = metrics?.ThumbnailMs, totalMs = metrics?.TotalMs, gridDeferred = metrics?.GridDeferredItems };
            }
            catch (Exception ex)
            {
                result = new { ok = false, message = ex.Message, fixtureCount, folder };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousState);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavorites);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeen);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecent);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobs);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(ok ? 0 : 1);
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

    private void CaptureFolderBucketSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        FolderBucketSmokeFixture fixture = PrepareFolderBucketSmokeFixture(smokeRoot);
        string seededStatePath = Path.Combine(smokeRoot, ".cache", "state.json");
        File.WriteAllText(seededStatePath, "{\"Version\":1,\"futureFolderBucketFlag\":{\"preserve\":true}}");

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            FolderBucketSmokeResult result;
            try
            {
                await first.LoadFolderSetAsync([fixture.FolderA, fixture.FolderB]);
                string statePath = first.StatePathForSmoke;
                int allCount = first.FilteredCountForSmoke;
                int bucketCount = first.FolderBucketCountForSmoke;
                List<string> bucketKeys = first.FolderBucketKeysForSmoke;
                bool defaultExpanded = first.FoldersSectionExpandedForSmoke;
                bool folderAccessibility = first.FolderBucketSelectionAccessibleForSmoke && first.FocusFolderBucketListForSmoke();
                int favoritesBeforeBucketSelection = first.FavoriteStoreCountForSmoke;
                int seenBeforeBucketSelection = first.SeenStoreCountForSmoke;
                bool selectedRange = first.SelectFolderBucketRangeForSmoke(0, 1);
                bool toggledFirstBucket = first.ToggleFolderBucketSelectionForSmoke(0);
                List<string> selectedAfterToggle = first.SelectedFolderBucketKeysForSmoke;
                string? primaryAfterToggle = first.PrimarySelectedFolderBucketKeyForSmoke;
                bool hideSelected = first.HideSelectedFolderBucketsForSmoke();
                int afterHideSelected = first.FilteredCountForSmoke;
                bool showSelected = first.ShowSelectedFolderBucketsForSmoke();
                int afterShowSelected = first.FilteredCountForSmoke;
                bool selectedActionsOk = selectedRange && toggledFirstBucket
                    && selectedAfterToggle.SequenceEqual([fixture.FolderB], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(primaryAfterToggle, fixture.FolderB, StringComparison.OrdinalIgnoreCase)
                    && hideSelected && afterHideSelected == fixture.FolderACount
                    && showSelected && afterShowSelected == fixture.TotalCount
                    && first.FavoriteStoreCountForSmoke == favoritesBeforeBucketSelection
                    && first.SeenStoreCountForSmoke == seenBeforeBucketSelection;

                bool selectedFolderA = first.SelectFileNameForSmoke(fixture.FolderASelectedName);
                bool hideFolderA = first.SetFolderBucketHiddenForSmoke(fixture.FolderA, true);
                int afterHideFolderA = first.FilteredCountForSmoke;
                List<string> afterHideFolderAOrder = first.FilteredFileNamesForSmoke(10);
                string? selectedAfterHideFolderA = first.SelectedFileNameForSmoke;

                bool showFolderA = first.SetFolderBucketHiddenForSmoke(fixture.FolderA, false);
                int afterShowFolderA = first.FilteredCountForSmoke;

                bool selectedFolderAAgain = first.SelectFileNameForSmoke(fixture.FolderASelectedName);
                bool hideFolderB = first.SetFolderBucketHiddenForSmoke(fixture.FolderB, true);
                int afterHideFolderB = first.FilteredCountForSmoke;
                string? selectedAfterHideFolderB = first.SelectedFileNameForSmoke;

                first.InvertFolderBucketsForSmoke();
                int afterInvert = first.FilteredCountForSmoke;
                List<string> afterInvertOrder = first.FilteredFileNamesForSmoke(10);

                first.ShowAllFolderBucketsForSmoke();
                int afterShowAll = first.FilteredCountForSmoke;

                first.HideAllFolderBucketsForSmoke();
                int afterHideAll = first.FilteredCountForSmoke;

                first.ShowAllFolderBucketsForSmoke();
                bool selectedFolderBBeforePersistence = first.SelectFolderBucketRangeForSmoke(1, 1);
                List<string> selectedBeforePersistence = first.SelectedFolderBucketKeysForSmoke;
                bool persistedHideFolderB = first.SetFolderBucketHiddenForSmoke(fixture.FolderB, true);
                first.ToggleFoldersSectionForSmoke();
                ViewerState? persisted = ReadPersistedState(statePath);
                string favoritesPath = first.FavoritesPathForSmoke;
                string seenPath = first.SeenPathForSmoke;
                string favoritesBefore = File.Exists(favoritesPath) ? File.ReadAllText(favoritesPath) : "";
                int seenBefore = first.SeenStoreCountForSmoke;
                first.Close();

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderSetAsync([fixture.FolderA, fixture.FolderB]);
                int restoredCount = second.FilteredCountForSmoke;
                int restoredHiddenCount = second.HiddenFolderBucketCountForSmoke;
                List<string> restoredHiddenKeys = second.HiddenFolderBucketKeysForSmoke;
                List<string> restoredOrder = second.FilteredFileNamesForSmoke(10);
                int favoriteCountAfterReload = second.FavoriteStoreCountForSmoke;
                int seenAfterReload = second.SeenStoreCountForSmoke;
                bool collapsedAfterReload = !second.FoldersSectionExpandedForSmoke;
                List<string> selectedBucketsAfterReload = second.SelectedFolderBucketKeysForSmoke;
                string? primaryBucketAfterReload = second.PrimarySelectedFolderBucketKeyForSmoke;
                bool sortSelectionPreserved = second.SetSortByForSmoke("name")
                    && second.SelectedFolderBucketKeysForSmoke.SequenceEqual([fixture.FolderB], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(second.PrimarySelectedFolderBucketKeyForSmoke, fixture.FolderB, StringComparison.OrdinalIgnoreCase);
                bool rootsPreserved = second.CurrentFolderSetForSmoke.SequenceEqual([fixture.FolderA, fixture.FolderB], StringComparer.OrdinalIgnoreCase);
                second.Close();

                string invalidStatePath = Path.Combine(smokeRoot, ".cache", "invalid-legacy-state.json");
                File.WriteAllText(invalidStatePath, "{\"Version\":99,\"FoldersSectionExpanded\":false}");
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", invalidStatePath);
                var invalidLegacy = HiddenWindow();
                invalidLegacy.Show();
                bool invalidLegacyFallback = invalidLegacy.FoldersSectionExpandedForSmoke;
                invalidLegacy.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);

                bool bucketKeysOk = bucketCount == 2
                    && bucketKeys.Contains(fixture.FolderA, StringComparer.OrdinalIgnoreCase)
                    && bucketKeys.Contains(fixture.FolderB, StringComparer.OrdinalIgnoreCase);
                bool hideFolderAOk = selectedFolderA
                    && hideFolderA
                    && afterHideFolderA == fixture.FolderBCount
                    && afterHideFolderAOrder.All(name => name.EndsWith("-b.png", StringComparison.OrdinalIgnoreCase))
                    && !string.Equals(selectedAfterHideFolderA, fixture.FolderASelectedName, StringComparison.OrdinalIgnoreCase);
                bool showFolderAOk = showFolderA && afterShowFolderA == fixture.TotalCount;
                bool hideFolderBOk = selectedFolderAAgain
                    && hideFolderB
                    && afterHideFolderB == fixture.FolderACount
                    && string.Equals(selectedAfterHideFolderB, fixture.FolderASelectedName, StringComparison.OrdinalIgnoreCase);
                bool invertOk = afterInvert == fixture.FolderBCount
                    && afterInvertOrder.All(name => name.EndsWith("-b.png", StringComparison.OrdinalIgnoreCase));
                bool bulkOk = afterShowAll == fixture.TotalCount && afterHideAll == 0;
                bool persistenceOk = persistedHideFolderB
                    && persisted?.HiddenFolderBuckets is not null
                    && persisted.HiddenFolderBuckets.Count == 1
                    && persisted.HiddenFolderBuckets.Contains(fixture.FolderB, StringComparer.OrdinalIgnoreCase)
                    && restoredCount == fixture.FolderACount
                    && restoredHiddenCount == 1
                    && restoredHiddenKeys.Contains(fixture.FolderB, StringComparer.OrdinalIgnoreCase)
                    && restoredOrder.All(name => name.EndsWith("-a.png", StringComparison.OrdinalIgnoreCase));
                bool stateIsolated = favoriteCountAfterReload == 0
                    && string.IsNullOrWhiteSpace(favoritesBefore)
                    && Path.GetFullPath(favoritesPath).StartsWith(Path.GetFullPath(smokeRoot), StringComparison.OrdinalIgnoreCase)
                    && Path.GetFullPath(seenPath).StartsWith(Path.GetFullPath(smokeRoot), StringComparison.OrdinalIgnoreCase)
                    && seenAfterReload >= seenBefore;

                bool ok = allCount == fixture.TotalCount
                    && bucketKeysOk
                    && hideFolderAOk
                    && showFolderAOk
                    && hideFolderBOk
                    && invertOk
                    && bulkOk
                    && defaultExpanded
                    && folderAccessibility
                    && selectedActionsOk
                    && persistenceOk
                    && selectedFolderBBeforePersistence
                    && persisted?.Version == 2
                    && persisted?.FoldersSectionExpanded == false
                    && persisted?.SelectedFolderBucketKeys?.SequenceEqual([fixture.FolderB], StringComparer.OrdinalIgnoreCase) == true
                    && string.Equals(persisted?.PrimarySelectedFolderBucketKey, fixture.FolderB, StringComparison.OrdinalIgnoreCase)
                    && File.ReadAllText(statePath).Contains("futureFolderBucketFlag", StringComparison.OrdinalIgnoreCase)
                    && collapsedAfterReload
                    && selectedBucketsAfterReload.SequenceEqual([fixture.FolderB], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(primaryBucketAfterReload, fixture.FolderB, StringComparison.OrdinalIgnoreCase)
                    && sortSelectionPreserved
                    && rootsPreserved
                    && invalidLegacyFallback
                    && stateIsolated;

                result = new FolderBucketSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "folder bucket sidebar expansion, multi-select actions, migration, persistence, and state isolation checks passed"
                        : "folder bucket smoke did not meet show/hide/selection/persistence expectations",
                    ProjectRoot = smokeRoot,
                    FolderA = fixture.FolderA,
                    FolderB = fixture.FolderB,
                    StatePath = statePath,
                    AllCount = allCount,
                    BucketCount = bucketCount,
                    BucketKeys = bucketKeys,
                    DefaultExpanded = defaultExpanded,
                    FolderAccessibility = folderAccessibility,
                    SelectedActionsOk = selectedActionsOk,
                    SelectedFolderA = selectedFolderA,
                    AfterHideFolderA = afterHideFolderA,
                    AfterHideFolderAOrder = afterHideFolderAOrder,
                    SelectedAfterHideFolderA = selectedAfterHideFolderA,
                    AfterShowFolderA = afterShowFolderA,
                    SelectedFolderAAgain = selectedFolderAAgain,
                    AfterHideFolderB = afterHideFolderB,
                    SelectedAfterHideFolderB = selectedAfterHideFolderB,
                    AfterInvert = afterInvert,
                    AfterInvertOrder = afterInvertOrder,
                    AfterShowAll = afterShowAll,
                    AfterHideAll = afterHideAll,
                    PersistedHiddenBuckets = persisted?.HiddenFolderBuckets ?? [],
                    SelectedBeforePersistence = selectedBeforePersistence,
                    PersistedSelectedBuckets = persisted?.SelectedFolderBucketKeys ?? [],
                    CollapsedAfterReload = collapsedAfterReload,
                    SelectedBucketsAfterReload = selectedBucketsAfterReload,
                    SortSelectionPreserved = sortSelectionPreserved,
                    InvalidLegacyFallback = invalidLegacyFallback,
                    RestoredCount = restoredCount,
                    RestoredHiddenCount = restoredHiddenCount,
                    RestoredHiddenKeys = restoredHiddenKeys,
                    RestoredOrder = restoredOrder,
                    FavoriteCountAfterReload = favoriteCountAfterReload,
                    SeenBefore = seenBefore,
                    SeenAfterReload = seenAfterReload,
                    FavoritesPath = favoritesPath,
                    SeenPath = seenPath,
                    StateIsolated = stateIsolated,
                };
            }
            catch (Exception ex)
            {
                first.Close();
                result = new FolderBucketSmokeResult { Ok = false, Message = ex.Message, ProjectRoot = smokeRoot, FolderA = fixture.FolderA, FolderB = fixture.FolderB };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteFolderBucketSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
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
                    && Math.Abs(afterShortcutReset - 200) < 0.01
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
                bool posterExpanded = poster.CardWidth > standard.CardWidth
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
                    && posterExpanded
                    && zoomComposes
                    && persistedPoster;

                result = new DisplayStyleSmokeResult(
                    ok,
                    ok ? "display styles changed grid density/size, composed with zoom, and restored from WPF state" : "display style smoke did not meet density/size/persistence expectations",
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

    private void CaptureAspectSmoke(string resultPath, string[] args)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        string aspectFolder = PrepareAspectSmokeFolder(smokeRoot);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            AspectSmokeResult result;
            try
            {
                await first.LoadFolderAsync(aspectFolder);
                int filtered = first.FilteredCountForSmoke;
                List<string> initialOrder = first.FilteredFileNamesForSmoke(3);
                bool selectedBravo = first.SelectFileNameForSmoke("bravo-square.png");
                string? selectedBefore = first.SelectedFileNameForSmoke;

                DisplayStyleMetrics original = first.DisplayStyleMetricsForSmoke();
                bool squareChanged = first.SetAspectModeForSmoke("square");
                DisplayStyleMetrics square = first.DisplayStyleMetricsForSmoke();
                List<string> squareOrder = first.FilteredFileNamesForSmoke(3);
                string? selectedAfterSquare = first.SelectedFileNameForSmoke;

                bool portraitChanged = first.SetAspectModeForSmoke("portrait");
                DisplayStyleMetrics portrait = first.DisplayStyleMetricsForSmoke();
                List<string> portraitOrder = first.FilteredFileNamesForSmoke(3);
                string? selectedAfterPortrait = first.SelectedFileNameForSmoke;

                bool zoomed = first.ZoomInForSmoke();
                DisplayStyleMetrics portraitZoomed = first.DisplayStyleMetricsForSmoke();
                bool originalChanged = first.SetAspectModeForSmoke("original");
                DisplayStyleMetrics restoredOriginalRuntime = first.DisplayStyleMetricsForSmoke();

                string statePath = first.StatePathForSmoke;
                bool persistedTargetSet = first.SetAspectModeForSmoke("portrait");
                first.Close();

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(aspectFolder);
                DisplayStyleMetrics restored = second.DisplayStyleMetricsForSmoke();
                List<string> restoredOrder = second.FilteredFileNamesForSmoke(3);
                second.Close();

                ViewerState? persisted = ReadPersistedState(statePath);
                bool squareShape = Math.Abs(square.CardWidth - square.CardHeight) < 0.5
                    && Math.Abs(square.ListThumbnailWidth - square.ListThumbnailHeight) < 0.5;
                bool portraitShape = portrait.CardHeight > square.CardHeight
                    && portrait.ListThumbnailHeight > square.ListThumbnailHeight
                    && Math.Abs((portrait.CardHeight / portrait.CardWidth) - 1.5) < 0.03;
                bool originalShape = original.CardHeight < square.CardHeight
                    && original.ListThumbnailHeight < square.ListThumbnailHeight
                    && string.Equals(original.AspectMode, "original", StringComparison.OrdinalIgnoreCase);
                bool orderStable = SameNameOrder(squareOrder, initialOrder)
                    && SameNameOrder(portraitOrder, initialOrder)
                    && SameNameOrder(restoredOrder, initialOrder);
                bool selectionStable = selectedBravo
                    && string.Equals(selectedBefore, "bravo-square.png", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterSquare, selectedBefore, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterPortrait, selectedBefore, StringComparison.OrdinalIgnoreCase);
                bool zoomComposes = zoomed
                    && portraitZoomed.CardWidth > portrait.CardWidth
                    && portraitZoomed.CardHeight > portrait.CardHeight
                    && Math.Abs((portraitZoomed.CardHeight / portraitZoomed.CardWidth) - (portrait.CardHeight / portrait.CardWidth)) < 0.03;
                bool persistence = persistedTargetSet
                    && string.Equals(persisted?.AspectMode, "portrait", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(restored.AspectMode, "portrait", StringComparison.OrdinalIgnoreCase)
                    && restored.CardHeight > restored.CardWidth;
                bool runtimeRestore = originalChanged
                    && string.Equals(restoredOriginalRuntime.AspectMode, "original", StringComparison.OrdinalIgnoreCase)
                    && restoredOriginalRuntime.CardHeight < square.CardHeight;

                bool ok = filtered == 3
                    && initialOrder.SequenceEqual(new[] { "alpha-landscape.png", "bravo-square.png", "charlie-portrait.png" })
                    && squareChanged
                    && portraitChanged
                    && squareShape
                    && portraitShape
                    && originalShape
                    && orderStable
                    && selectionStable
                    && zoomComposes
                    && runtimeRestore
                    && persistence;

                result = new AspectSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "aspect controls changed deterministic grid/list dimensions, preserved order/selection, composed with zoom, and restored from WPF state"
                        : "aspect smoke did not meet dimension/order/selection/persistence expectations",
                    Folder = aspectFolder,
                    ProjectRoot = smokeRoot,
                    StatePath = statePath,
                    FilteredCount = filtered,
                    InitialOrder = initialOrder,
                    SquareOrder = squareOrder,
                    PortraitOrder = portraitOrder,
                    RestoredOrder = restoredOrder,
                    Original = original,
                    Square = square,
                    Portrait = portrait,
                    PortraitZoomed = portraitZoomed,
                    RestoredOriginalRuntime = restoredOriginalRuntime,
                    Restored = restored,
                    SelectedBefore = selectedBefore,
                    SelectedAfterSquare = selectedAfterSquare,
                    SelectedAfterPortrait = selectedAfterPortrait,
                    PersistedAspect = persisted?.AspectMode,
                    SquareChanged = squareChanged,
                    PortraitChanged = portraitChanged,
                    OriginalChanged = originalChanged,
                    Zoomed = zoomed,
                    PersistedTargetSet = persistedTargetSet,
                    SquareShape = squareShape,
                    PortraitShape = portraitShape,
                    OriginalShape = originalShape,
                    OrderStable = orderStable,
                    SelectionStable = selectionStable,
                    ZoomComposes = zoomComposes,
                    Persistence = persistence,
                };
            }
            catch (Exception ex)
            {
                first.Close();
                result = new AspectSmokeResult { Ok = false, Message = ex.Message, Folder = aspectFolder, ProjectRoot = smokeRoot };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteAspectSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

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

                bool changedCreatedNewest = first.SetSortByForSmoke("created-newest");
                List<string> createdNewestOrder = first.FilteredFileNamesForSmoke(3);
                bool changedCreatedOldest = first.SetSortByForSmoke("created-oldest");
                List<string> createdOldestOrder = first.FilteredFileNamesForSmoke(3);
                bool changedRandom = first.SetSortByForSmoke("random");
                string randomSeedBefore = first.RandomSortSeedForSmoke;
                List<string> randomOrderBefore = first.FilteredFileNamesForSmoke(3);
                bool reshuffled = first.ReshuffleRandomSortForSmoke();
                string randomSeedAfter = first.RandomSortSeedForSmoke;
                List<string> randomOrderAfter = first.FilteredFileNamesForSmoke(3);
                ViewerState? persisted = ReadPersistedState(statePath);
                first.Close();

                var second = HiddenWindow();
                second.Show();
                await second.LoadFolderAsync(sortFolder);
                string restoredSort = second.SortByForSmoke;
                List<string> restoredOrder = second.FilteredFileNamesForSmoke(3);
                string restoredRandomSeed = second.RandomSortSeedForSmoke;
                second.Close();

                List<string> expectedNewest = [bravoName, alphaName, charlieName];
                List<string> expectedOldest = [charlieName, alphaName, bravoName];
                List<string> expectedName = [alphaName, bravoName, charlieName];
                List<string> expectedCreatedNewest = [bravoName, alphaName, charlieName];
                List<string> expectedCreatedOldest = [charlieName, alphaName, bravoName];
                bool newestOk = SameNameOrder(newestOrder, expectedNewest);
                bool oldestOk = SameNameOrder(oldestOrder, expectedOldest);
                bool nameOk = SameNameOrder(nameOrder, expectedName);
                bool createdNewestOk = SameNameOrder(createdNewestOrder, expectedCreatedNewest);
                bool createdOldestOk = SameNameOrder(createdOldestOrder, expectedCreatedOldest);
                bool randomPersistence = changedRandom
                    && reshuffled
                    && !string.Equals(randomSeedBefore, randomSeedAfter, StringComparison.Ordinal)
                    && persisted?.SortBy == "random"
                    && string.Equals(persisted.RandomSortSeed, randomSeedAfter, StringComparison.Ordinal)
                    && string.Equals(restoredSort, "random", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(restoredRandomSeed, randomSeedAfter, StringComparison.Ordinal)
                    && SameNameOrder(restoredOrder, randomOrderAfter);
                bool ok = newestOk
                    && changedOldest
                    && oldestOk
                    && selectionKeptAfterOldest
                    && changedName
                    && nameOk
                    && selectionKeptAfterName
                    && filterPreserved
                    && modalUsesSortedNameOrder
                    && changedCreatedNewest
                    && createdNewestOk
                    && changedCreatedOldest
                    && createdOldestOk
                    && randomPersistence;

                result = new SortSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "modified, created, name, random/reshuffle sort, filter, selection, modal navigation, and persistence checks passed"
                        : "sort smoke did not meet order/filter/selection/modal/persistence expectations",
                    Folder = sortFolder,
                    ProjectRoot = smokeRoot,
                    StatePath = statePath,
                    ExpectedNewest = expectedNewest,
                    ExpectedOldest = expectedOldest,
                    ExpectedName = expectedName,
                    ExpectedCreatedNewest = expectedCreatedNewest,
                    ExpectedCreatedOldest = expectedCreatedOldest,
                    NewestOrder = newestOrder,
                    OldestOrder = oldestOrder,
                    NameOrder = nameOrder,
                    CreatedNewestOrder = createdNewestOrder,
                    CreatedOldestOrder = createdOldestOrder,
                    RandomOrderBefore = randomOrderBefore,
                    RandomOrderAfter = randomOrderAfter,
                    RestoredOrder = restoredOrder,
                    SelectedAlpha = selectedAlpha,
                    SelectionKeptAfterOldest = selectionKeptAfterOldest,
                    SelectionKeptAfterName = selectionKeptAfterName,
                    FilterPreserved = filterPreserved,
                    ModalOpened = modalOpened,
                    ModalMovedNext = modalMovedNext,
                    ModalNextName = modalNextName,
                    RandomSeedBefore = randomSeedBefore,
                    RandomSeedAfter = randomSeedAfter,
                    PersistedSort = persisted?.SortBy,
                    RestoredSort = restoredSort,
                    RestoredRandomSeed = restoredRandomSeed,
                    RandomPersistence = randomPersistence,
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

    private void CaptureDateFilterSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        DateFilterSmokeFixture fixture = PrepareDateFilterSmokeFolder(smokeRoot);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var first = HiddenWindow();
        first.Show();

        first.Dispatcher.InvokeAsync(async () =>
        {
            DateFilterSmokeResult result;
            try
            {
                await first.LoadFolderAsync(fixture.Folder);
                string statePath = first.StatePathForSmoke;
                int allCount = first.FilteredCountForSmoke;
                List<string> allOrder = first.FilteredFileNamesForSmoke(10);
                bool selectedManualExcluded = first.SelectFileNameForSmoke(fixture.TodayName);
                bool manualRangeChanged = first.SetManualDateRangeForSmoke(fixture.ManualFromText, fixture.ManualToText);
                List<string> manualRangeOrder = first.FilteredFileNamesForSmoke(10);
                string? selectedAfterManualRange = first.SelectedFileNameForSmoke;

                bool manualFromOnlyChanged = first.SetManualDateRangeForSmoke(fixture.ManualFromText, null);
                List<string> manualFromOnlyOrder = first.FilteredFileNamesForSmoke(10);
                string? selectedAfterManualFromOnly = first.SelectedFileNameForSmoke;

                bool manualToOnlyChanged = first.SetManualDateRangeForSmoke(null, fixture.ManualToText);
                List<string> manualToOnlyOrder = first.FilteredFileNamesForSmoke(10);
                string? selectedAfterManualToOnly = first.SelectedFileNameForSmoke;

                bool manualClearChanged = first.ClearManualDateRangeForSmoke();
                List<string> manualClearOrder = first.FilteredFileNamesForSmoke(10);
                string clearPreset = first.DatePresetForSmoke;

                bool manualPersistChanged = first.SetManualDateRangeForSmoke(fixture.ManualFromText, fixture.ManualToText);
                bool selectedManualPersisted = first.SelectFileNameForSmoke(fixture.SevenDayName);
                ViewerState? persistedManual = ReadPersistedState(statePath);
                first.Close();

                WriteLegacyDateFilterState(statePath, "30d", fixture.ManualFromText, fixture.ManualToText);
                var legacyWithRange = HiddenWindow();
                legacyWithRange.Show();
                await legacyWithRange.LoadFolderAsync(fixture.Folder);
                string legacyRangePreset = legacyWithRange.DatePresetForSmoke;
                string? legacyRangeFrom = legacyWithRange.DateFromForSmoke;
                string? legacyRangeTo = legacyWithRange.DateToForSmoke;
                string legacyRangeSummary = legacyWithRange.DateFilterSummaryForSmoke;
                List<string> legacyRangeOrder = legacyWithRange.FilteredFileNamesForSmoke(10);
                ViewerState? migratedLegacyWithRange = ReadPersistedState(statePath);
                bool legacyUnknownFieldPreserved = PersistedStateHasStringProperty(statePath, "futureDateMarker", "preserve-me");
                legacyWithRange.Close();

                WriteLegacyDateFilterState(statePath, "7d", fixture.ManualFromText, null);
                var legacyWithPartialRange = HiddenWindow();
                legacyWithPartialRange.Show();
                await legacyWithPartialRange.LoadFolderAsync(fixture.Folder);
                string legacyPartialPreset = legacyWithPartialRange.DatePresetForSmoke;
                string? legacyPartialFrom = legacyWithPartialRange.DateFromForSmoke;
                string? legacyPartialTo = legacyWithPartialRange.DateToForSmoke;
                List<string> legacyPartialOrder = legacyWithPartialRange.FilteredFileNamesForSmoke(10);
                ViewerState? migratedLegacyWithPartialRange = ReadPersistedState(statePath);
                legacyWithPartialRange.Close();

                WriteLegacyDateFilterState(statePath, "today", null, null);
                var legacyWithoutRange = HiddenWindow();
                legacyWithoutRange.Show();
                await legacyWithoutRange.LoadFolderAsync(fixture.Folder);
                string legacyFallbackPreset = legacyWithoutRange.DatePresetForSmoke;
                string? legacyFallbackFrom = legacyWithoutRange.DateFromForSmoke;
                string? legacyFallbackTo = legacyWithoutRange.DateToForSmoke;
                List<string> legacyFallbackOrder = legacyWithoutRange.FilteredFileNamesForSmoke(10);
                ViewerState? migratedLegacyWithoutRange = ReadPersistedState(statePath);
                legacyWithoutRange.Close();

                string? persistedManualDatePreset = persistedManual?.DatePreset;
                string? persistedManualDateFrom = persistedManual?.DateFrom;
                string? persistedManualDateTo = persistedManual?.DateTo;

                bool allOk = allCount == fixture.AllExpected.Count && SameNameOrder(allOrder, fixture.AllExpected);
                bool manualRangeOk = selectedManualExcluded
                    && manualRangeChanged
                    && string.Equals(first.DatePresetForSmoke, "manual", StringComparison.OrdinalIgnoreCase)
                    && SameNameOrder(manualRangeOrder, fixture.ManualRangeExpected)
                    && string.Equals(selectedAfterManualRange, fixture.ManualRangeExpected.FirstOrDefault(), StringComparison.OrdinalIgnoreCase);
                bool manualFromOnlyOk = manualFromOnlyChanged
                    && SameNameOrder(manualFromOnlyOrder, fixture.ManualFromOnlyExpected)
                    && string.Equals(selectedAfterManualFromOnly, selectedAfterManualRange, StringComparison.OrdinalIgnoreCase);
                bool manualToOnlyOk = manualToOnlyChanged
                    && SameNameOrder(manualToOnlyOrder, fixture.ManualToOnlyExpected)
                    && string.Equals(selectedAfterManualToOnly, selectedAfterManualRange, StringComparison.OrdinalIgnoreCase);
                bool manualClearOk = manualClearChanged
                    && string.Equals(clearPreset, "none", StringComparison.OrdinalIgnoreCase)
                    && SameNameOrder(manualClearOrder, fixture.AllExpected);
                bool manualPersistenceOk = manualPersistChanged
                    && selectedManualPersisted
                    && string.Equals(persistedManualDatePreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(persistedManualDateFrom, fixture.ManualFromText, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(persistedManualDateTo, fixture.ManualToText, StringComparison.OrdinalIgnoreCase);
                bool legacyRangeMigrationOk = string.Equals(legacyRangePreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(legacyRangeFrom, fixture.ManualFromText, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(legacyRangeTo, fixture.ManualToText, StringComparison.OrdinalIgnoreCase)
                    && legacyRangeSummary.StartsWith("Manual:", StringComparison.Ordinal)
                    && SameNameOrder(legacyRangeOrder, fixture.ManualRangeExpected)
                    && string.Equals(migratedLegacyWithRange?.DatePreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(migratedLegacyWithRange?.DateFrom, fixture.ManualFromText, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(migratedLegacyWithRange?.DateTo, fixture.ManualToText, StringComparison.OrdinalIgnoreCase)
                    && legacyUnknownFieldPreserved;
                bool legacyPartialMigrationOk = string.Equals(legacyPartialPreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(legacyPartialFrom, fixture.ManualFromText, StringComparison.OrdinalIgnoreCase)
                    && legacyPartialTo is null
                    && SameNameOrder(legacyPartialOrder, fixture.ManualFromOnlyExpected)
                    && string.Equals(migratedLegacyWithPartialRange?.DatePreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(migratedLegacyWithPartialRange?.DateFrom, fixture.ManualFromText, StringComparison.OrdinalIgnoreCase)
                    && migratedLegacyWithPartialRange?.DateTo is null;
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                bool legacyFallbackMigrationOk = string.Equals(legacyFallbackPreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(legacyFallbackFrom, today, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(legacyFallbackTo, today, StringComparison.OrdinalIgnoreCase)
                    && SameNameOrder(legacyFallbackOrder, fixture.TodayExpected)
                    && string.Equals(migratedLegacyWithoutRange?.DatePreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(migratedLegacyWithoutRange?.DateFrom, today, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(migratedLegacyWithoutRange?.DateTo, today, StringComparison.OrdinalIgnoreCase);

                bool ok = allOk
                    && manualRangeOk
                    && manualFromOnlyOk
                    && manualToOnlyOk
                    && manualClearOk
                    && manualPersistenceOk
                    && legacyRangeMigrationOk
                    && legacyPartialMigrationOk
                    && legacyFallbackMigrationOk;

                result = new DateFilterSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "manual Created/Birth date filtering, fixed legacy migration, clear action, selection fallback/preservation, and persistence checks passed"
                        : "date filter smoke did not meet manual/migration/selection/persistence expectations",
                    Folder = fixture.Folder,
                    ProjectRoot = smokeRoot,
                    StatePath = statePath,
                    TodayName = fixture.TodayName,
                    SevenDayName = fixture.SevenDayName,
                    ThirtyDayName = fixture.ThirtyDayName,
                    ThisYearName = fixture.ThisYearName,
                    PreviousYearName = fixture.PreviousYearName,
                    AllExpected = fixture.AllExpected,
                    ManualFrom = fixture.ManualFromText,
                    ManualTo = fixture.ManualToText,
                    ManualRangeExpected = fixture.ManualRangeExpected,
                    ManualFromOnlyExpected = fixture.ManualFromOnlyExpected,
                    ManualToOnlyExpected = fixture.ManualToOnlyExpected,
                    AllOrder = allOrder,
                    ClearOrder = manualClearOrder,
                    ManualRangeOrder = manualRangeOrder,
                    ManualFromOnlyOrder = manualFromOnlyOrder,
                    ManualToOnlyOrder = manualToOnlyOrder,
                    ManualClearOrder = manualClearOrder,
                    SelectedAfterManualRange = selectedAfterManualRange,
                    SelectedAfterManualFromOnly = selectedAfterManualFromOnly,
                    SelectedAfterManualToOnly = selectedAfterManualToOnly,
                    PersistedManualDatePreset = persistedManualDatePreset,
                    PersistedManualDateFrom = persistedManualDateFrom,
                    PersistedManualDateTo = persistedManualDateTo,
                    LegacyRangePreset = legacyRangePreset,
                    LegacyRangeFrom = legacyRangeFrom,
                    LegacyRangeTo = legacyRangeTo,
                    LegacyRangeSummary = legacyRangeSummary,
                    LegacyRangeOrder = legacyRangeOrder,
                    LegacyPartialPreset = legacyPartialPreset,
                    LegacyPartialFrom = legacyPartialFrom,
                    LegacyPartialTo = legacyPartialTo,
                    LegacyPartialOrder = legacyPartialOrder,
                    LegacyFallbackPreset = legacyFallbackPreset,
                    LegacyFallbackFrom = legacyFallbackFrom,
                    LegacyFallbackTo = legacyFallbackTo,
                    LegacyFallbackOrder = legacyFallbackOrder,
                    LegacyUnknownFieldPreserved = legacyUnknownFieldPreserved,
                };
            }
            catch (Exception ex)
            {
                first.Close();
                result = new DateFilterSmokeResult { Ok = false, Message = ex.Message, Folder = fixture.Folder, ProjectRoot = smokeRoot };
            }
            finally
            {
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteDateFilterSmokeResult(resultFullPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureP1ASmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        string folderA = Path.Combine(smokeRoot, "folder-a");
        string folderB = Path.Combine(smokeRoot, "folder-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        string filenameCatPath = Path.Combine(folderA, "filename-cat.png");
        string promptPath = Path.Combine(folderA, "prompt-only.png");
        string reversedPath = Path.Combine(folderA, "reversed-words.png");
        string addedPath = Path.Combine(folderB, "added-folder.png");
        WriteSmokePng(filenameCatPath, 64, 48, Color.FromRgb(52, 152, 219));
        WritePngTextFixture(
            promptPath,
            "parameters",
            "night cat portrait\nNegative prompt: forbidden wolf\nSteps: 20, Sampler: Euler",
            Color.FromRgb(46, 204, 113));
        WritePngTextFixture(
            reversedPath,
            "parameters",
            "cat beneath a bright night sky\nNegative prompt: artifact\nSteps: 24, Sampler: DPM++ 2M",
            Color.FromRgb(155, 89, 182));
        WriteSmokePng(addedPath, 64, 48, Color.FromRgb(241, 196, 15));

        File.SetCreationTime(filenameCatPath, new DateTime(2026, 1, 10, 10, 0, 0));
        File.SetLastWriteTime(filenameCatPath, new DateTime(2026, 7, 15, 10, 0, 0));
        File.SetCreationTime(promptPath, new DateTime(2026, 7, 15, 12, 0, 0));
        File.SetLastWriteTime(promptPath, new DateTime(2026, 1, 10, 12, 0, 0));
        File.SetCreationTime(reversedPath, new DateTime(2026, 7, 17, 12, 0, 0));
        File.SetLastWriteTime(reversedPath, new DateTime(2026, 7, 17, 12, 0, 0));

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            P1ASmokeResult result;
            try
            {
                await win.LoadFolderAsync(folderA);
                string? indexedPrompt = win.PromptForFileNameForSmoke("prompt-only.png");

                win.SetSearchQuery("filename-cat", persist: false);
                List<string> filenameMatches = win.FilteredFileNamesForSmoke(10);
                win.SetSearchQuery("night cat", persist: false);
                List<string> phraseMatches = win.FilteredFileNamesForSmoke(10);
                win.SetSearchQuery("cat, night", persist: false);
                List<string> andMatches = win.FilteredFileNamesForSmoke(10);
                win.SetSearchQuery("cat,, night,", persist: false);
                List<string> emptyTokenMatches = win.FilteredFileNamesForSmoke(10);
                win.SetSearchQuery("forbidden wolf", persist: false);
                List<string> negativeMatches = win.FilteredFileNamesForSmoke(10);
                win.SetSearchQuery(Path.GetFileName(smokeRoot), persist: false);
                List<string> pathMatches = win.FilteredFileNamesForSmoke(10);

                win.SetSearchQuery("", persist: false);
                bool dateChanged = win.SetManualDateRangeForSmoke("2026-07-14", "2026-07-16");
                List<string> createdDateMatches = win.FilteredFileNamesForSmoke(10);
                string datePreset = win.DatePresetForSmoke;
                string dateSummary = win.DateFilterSummaryForSmoke;
                bool dateCleared = win.ClearManualDateRangeForSmoke();
                string clearedDatePreset = win.DatePresetForSmoke;

                bool folderAdded = await win.AddFoldersToCurrentSetForSmokeAsync([folderB]);
                List<string> currentFolderSet = win.CurrentFolderSetForSmoke;
                int catalogAfterAdd = win.CatalogCountForSmoke;
                win.ReturnToFolderSetEditorForSmoke();
                List<string> landingFolderSet = win.LandingFolderSetForSmoke;

                bool searchOk = string.Equals(indexedPrompt, "night cat portrait", StringComparison.Ordinal)
                    && SameNameOrder(filenameMatches, ["filename-cat.png"])
                    && SameNameOrder(phraseMatches, ["prompt-only.png"])
                    && SameNameOrder(andMatches, ["reversed-words.png", "prompt-only.png"])
                    && SameNameOrder(emptyTokenMatches, andMatches)
                    && negativeMatches.Count == 0
                    && pathMatches.Count == 0;
                bool dateOk = dateChanged
                    && SameNameOrder(createdDateMatches, ["prompt-only.png"])
                    && string.Equals(datePreset, "manual", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(dateSummary, "Manual: 2026-07-14 – 2026-07-16", StringComparison.Ordinal)
                    && dateCleared
                    && string.Equals(clearedDatePreset, "none", StringComparison.OrdinalIgnoreCase);
                bool folderOk = folderAdded
                    && currentFolderSet.Count == 2
                    && currentFolderSet.Contains(folderA, StringComparer.OrdinalIgnoreCase)
                    && currentFolderSet.Contains(folderB, StringComparer.OrdinalIgnoreCase)
                    && catalogAfterAdd == 4
                    && win.LandingVisibleForSmoke
                    && SameNameOrder(landingFolderSet, currentFolderSet);
                bool ok = searchOk && dateOk && folderOk;

                result = new P1ASmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "comma-AND filename/prompt search, Created/Birth manual date filtering, and Add/Change folder semantics passed"
                        : "P1A search/date/folder expectations did not match",
                    SearchOk = searchOk,
                    DateOk = dateOk,
                    FolderOk = folderOk,
                    IndexedPrompt = indexedPrompt,
                    FilenameMatches = filenameMatches,
                    PhraseMatches = phraseMatches,
                    AndMatches = andMatches,
                    EmptyTokenMatches = emptyTokenMatches,
                    NegativeMatches = negativeMatches,
                    PathMatches = pathMatches,
                    CreatedDateMatches = createdDateMatches,
                    CurrentFolderSet = currentFolderSet,
                    LandingFolderSet = landingFolderSet,
                    CatalogAfterAdd = catalogAfterAdd,
                    LandingVisible = win.LandingVisibleForSmoke,
                };
            }
            catch (Exception ex)
            {
                result = new P1ASmokeResult { Message = ex.Message };
            }
            finally
            {
                win.Close();
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
            File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureCatalogStressSmoke(string resultPath, string[] args)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        int count = ArgInt(args, "--count", 20_000);
        if (count < 2)
        {
            WriteCatalogStressSmokeResult(resultFullPath, new CatalogStressSmokeResult
            {
                RequestedCount = count,
                Message = "--count must be at least 2",
            });
            Shutdown(1);
            return;
        }

        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-catalog-stress-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string jobsPath = Path.Combine(smokeRoot, "jobs.json");
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");
        var fixtureWatch = Stopwatch.StartNew();
        Directory.CreateDirectory(folder);
        string firstPath = Path.Combine(folder, "stress-000000.png");
        WriteSmokePng(firstPath, 4, 3, Color.FromRgb(70, 130, 210));
        for (int index = 1; index < count; index++)
            File.Copy(firstPath, Path.Combine(folder, $"stress-{index:D6}.png"));
        fixtureWatch.Stop();

        Environment.CurrentDirectory = smokeRoot;
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.SuppressStatePersistence();

        win.Dispatcher.InvokeAsync(async () =>
        {
            CatalogStressSmokeResult result;
            var process = Process.GetCurrentProcess();
            process.Refresh();
            long workingSetBefore = process.WorkingSet64;
            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);
            int generation0Before = GC.CollectionCount(0);
            int generation1Before = GC.CollectionCount(1);
            int generation2Before = GC.CollectionCount(2);
            var loadWatch = Stopwatch.StartNew();
            var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(15) };
            int heartbeatCount = 0;
            heartbeat.Tick += (_, _) => heartbeatCount++;
            try
            {
                await win.LoadFolderAsync(folder);
                loadWatch.Stop();
                await win.Dispatcher.InvokeAsync(win.UpdateLayout, DispatcherPriority.Render);
                int sourceCountBefore = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly).Count();
                int catalogCount = win.CatalogCountForSmoke;
                int filteredCount = win.FilteredCountForSmoke;
                int gridRealized = win.GridRealizedCountForSmoke;
                int gridMaximum = win.GridMaxRealizationCountForSmoke;
                int gridDeferred = win.GridDeferredCountForSmoke;
                bool listMode = win.SetListModeForSmoke();
                await win.Dispatcher.InvokeAsync(win.UpdateLayout, DispatcherPriority.Render);
                int listRealized = win.ListRealizedContainerCountForSmoke;
                bool listBounded = win.ListUsesRecyclingVirtualizationForSmoke && listRealized <= Math.Min(count, 512);

                string tailName = $"stress-{count - 1:D6}.png";
                bool selectedTail = win.SelectFileNameForSmoke(tailName);
                bool modalTail = selectedTail && win.OpenModalForSmoke() && win.ModalVisibleForSmoke;
                win.CloseModalForSmoke();

                heartbeat.Start();
                var inputWatch = Stopwatch.StartNew();
                var searchWatch = Stopwatch.StartNew();
                string[] rapidQueries = ["s", "st", "str", "stress", "stress-0", "stress-01", "stress-019", "stress-0199", $"stress-{count - 1:D6}"];
                var completions = new List<Task<MainWindow.SearchFilterCompletion>>();
                foreach (string query in rapidQueries)
                    completions.Add(win.SetSearchInputForSmokeAsync(query));
                inputWatch.Stop();
                Task<MainWindow.SearchFilterCompletion> finalTask = completions[^1];
                Task completed = await Task.WhenAny(finalTask, Task.Delay(TimeSpan.FromSeconds(12)));
                MainWindow.SearchFilterCompletion finalCompletion = completed == finalTask
                    ? await finalTask
                    : new MainWindow.SearchFilterCompletion(false, false, "timed out waiting for the final stress search");
                MainWindow.SearchFilterCompletion[] earlierCompletions = await Task.WhenAll(completions.Take(completions.Count - 1));
                await Task.Delay(120);
                heartbeat.Stop();
                searchWatch.Stop();

                List<string> finalMatches = win.FilteredFileNamesForSmoke(4);
                bool finalSearchExact = finalCompletion.Applied
                    && string.Equals(win.SearchQueryForSmoke, $"stress-{count - 1:D6}", StringComparison.Ordinal)
                    && finalMatches.SequenceEqual([tailName], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(win.SelectedFileNameForSmoke, tailName, StringComparison.OrdinalIgnoreCase);
                bool staleCancelled = earlierCompletions.All(static completion => completion.Discarded);
                bool heartbeatAdvanced = heartbeatCount >= 4;
                int sourceCountAfter = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly).Count();
                process.Refresh();
                LoadMetrics? loadMetrics = win.LastLoadMetrics;
                result = new CatalogStressSmokeResult
                {
                    RequestedCount = count,
                    FixtureCount = sourceCountBefore,
                    CatalogCount = catalogCount,
                    FilteredCount = filteredCount,
                    SilentTruncateCount = Math.Max(0, count - catalogCount),
                    GridRealized = gridRealized,
                    GridMaximum = gridMaximum,
                    GridDeferred = gridDeferred,
                    ListRealized = listRealized,
                    ListBounded = listBounded,
                    TailName = tailName,
                    SelectedTail = selectedTail,
                    ModalTail = modalTail,
                    FinalSearchExact = finalSearchExact,
                    StaleCancelled = staleCancelled,
                    HeartbeatCount = heartbeatCount,
                    WorkingSetBeforeBytes = workingSetBefore,
                    WorkingSetAfterBytes = process.WorkingSet64,
                    ManagedBytesBefore = managedBefore,
                    ManagedBytesAfter = GC.GetTotalMemory(forceFullCollection: false),
                    Generation0Collections = GC.CollectionCount(0) - generation0Before,
                    Generation1Collections = GC.CollectionCount(1) - generation1Before,
                    Generation2Collections = GC.CollectionCount(2) - generation2Before,
                    FixtureElapsedMs = fixtureWatch.ElapsedMilliseconds,
                    LoadElapsedMs = loadWatch.ElapsedMilliseconds,
                    SearchInputElapsedMs = inputWatch.ElapsedMilliseconds,
                    SearchElapsedMs = searchWatch.ElapsedMilliseconds,
                    LoadMetricsTotalElapsedMs = loadMetrics?.TotalMs,
                    ScanElapsedMs = loadMetrics?.ScanMs,
                    MetadataElapsedMs = loadMetrics?.MetadataMs,
                    SourceCountAfter = sourceCountAfter,
                    EnhancementJobsRead = win.EnhancementJobsReadForSmoke,
                    EnhancementCandidates = win.EnhancedCandidateCountForSmoke,
                    Ok = sourceCountBefore == count
                        && catalogCount == count
                        && filteredCount == count
                        && gridRealized <= gridMaximum
                        && gridDeferred == count - gridRealized
                        && listMode && listBounded
                        && selectedTail && modalTail
                        && finalSearchExact && staleCancelled && heartbeatAdvanced
                        && sourceCountAfter == count
                        && win.EnhancementJobsReadForSmoke == 0
                        && win.EnhancedCandidateCountForSmoke == 0,
                };
                result.Message = result.Ok
                    ? $"{count:N0}-image catalog stayed exact and structurally bounded while tail search, modal, stale cancellation, and dispatcher responsiveness passed"
                    : "catalog stress structural gate did not meet the expected result";
            }
            catch (Exception ex)
            {
                heartbeat.Stop();
                loadWatch.Stop();
                result = new CatalogStressSmokeResult
                {
                    RequestedCount = count,
                    Message = ex.Message,
                    FixtureElapsedMs = fixtureWatch.ElapsedMilliseconds,
                    LoadElapsedMs = loadWatch.ElapsedMilliseconds,
                };
            }
            finally
            {
                win.Close();
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteCatalogStressSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureSearchStallSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-search-stall-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");

        Directory.CreateDirectory(folder);
        for (int index = 0; index < 4_999; index++)
            WriteSmokePng(Path.Combine(folder, $"rapid-noise-{index:0000}.png"), 4, 3, Color.FromRgb((byte)(30 + index % 180), 90, 160));
        const string finalFileName = "rapid-final-target.png";
        WriteSmokePng(Path.Combine(folder, finalFileName), 4, 3, Color.FromRgb(230, 90, 120));

        Environment.CurrentDirectory = smokeRoot;
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.SuppressStatePersistence();

        win.Dispatcher.InvokeAsync(async () =>
        {
            SearchStallSmokeResult result;
            var heartbeat = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(15),
            };
            int heartbeatCount = 0;
            heartbeat.Tick += (_, _) => heartbeatCount++;
            try
            {
                await win.LoadFolderAsync(folder);
                bool initialSelected = win.SelectFileNameForSmoke("rapid-noise-0000.png");
                heartbeat.Start();
                var inputWatch = Stopwatch.StartNew();
                string[] rapidQueries = ["r", "ra", "rap", "rapid", "rapid-", "rapid-n", "rapid-no", "rapid-noi", "rapid-nois", "rapid-noise", "rapid-final", "rapid-final-target"];
                Task<MainWindow.SearchFilterCompletion>? finalCompletion = null;
                foreach (string query in rapidQueries)
                    finalCompletion = win.SetSearchInputForSmokeAsync(query);
                inputWatch.Stop();

                Task timeout = Task.Delay(TimeSpan.FromSeconds(8));
                Task completed = await Task.WhenAny(finalCompletion!, timeout);
                MainWindow.SearchFilterCompletion completion = completed == finalCompletion
                    ? await finalCompletion!
                    : new MainWindow.SearchFilterCompletion(false, false, "timed out waiting for the final search filter");
                await Task.Delay(120);
                heartbeat.Stop();

                List<string> finalMatches = win.FilteredFileNamesForSmoke(10);
                bool finalQueryApplied = completion.Applied
                    && string.Equals(win.SearchQueryForSmoke, "rapid-final-target", StringComparison.Ordinal)
                    && SameNameOrder(finalMatches, [finalFileName]);
                bool selectionAndPreviewStable = string.Equals(win.SelectedFileNameForSmoke, finalFileName, StringComparison.Ordinal)
                    && win.SelectedCountForSmoke == 1;
                bool heartbeatAdvanced = heartbeatCount >= 4;
                bool ok = initialSelected && heartbeatAdvanced && finalQueryApplied && selectionAndPreviewStable;
                result = new SearchStallSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "5,000-image rapid search debounced on the UI thread and applied only the final query"
                        : "rapid search did not preserve UI responsiveness or the final selection",
                    CatalogCount = win.CatalogCountForSmoke,
                    InputMs = inputWatch.ElapsedMilliseconds,
                    HeartbeatCount = heartbeatCount,
                    FinalCompletionApplied = completion.Applied,
                    FinalCompletionDiscarded = completion.Discarded,
                    CompletionError = completion.Error,
                    FinalQuery = win.SearchQueryForSmoke,
                    FinalMatches = finalMatches,
                    SelectedFileName = win.SelectedFileNameForSmoke,
                    SelectedCount = win.SelectedCountForSmoke,
                    LastAppliedGeneration = win.LastAppliedSearchFilterGenerationForSmoke,
                };
            }
            catch (Exception ex)
            {
                heartbeat.Stop();
                result = new SearchStallSmokeResult { Message = ex.Message };
            }
            finally
            {
                win.Close();
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
            File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureRecentWriteOwnershipSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-recent-ownership-" + Guid.NewGuid().ToString("N"));
        string folderA = Path.Combine(smokeRoot, "folder-a");
        string folderB = Path.Combine(smokeRoot, "folder-b");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string jobsPath = Path.Combine(smokeRoot, "jobs.json");
        string lockPath = recentPath + ".lock";
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        try
        {
            Directory.CreateDirectory(folderA);
            Directory.CreateDirectory(folderB);
            WriteSmokePng(Path.Combine(folderA, "a.png"), 32, 24, Color.FromRgb(80, 130, 210));
            WriteSmokePng(Path.Combine(folderB, "b.png"), 32, 24, Color.FromRgb(200, 110, 90));
            File.WriteAllText(jobsPath, "{\"jobs\":[]}");
            File.WriteAllText(recentPath, JsonSerializer.Serialize(new
            {
                version = 1,
                lastFolderSet = new[] { Path.Combine(smokeRoot, "seed") },
                recentFolderSets = new[] { new[] { Path.Combine(smokeRoot, "seed") } },
                updatedAtUtc = "2026-07-18T00:00:00.0000000Z",
                futureOwner = new { token = "seed-owner", nested = new[] { 3, 1, 4 } },
            }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        }
        catch (Exception ex)
        {
            WriteRecentWriteOwnershipSmokeResult(resultFullPath, new RecentWriteOwnershipSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.InvokeAsync(async () =>
        {
            var windows = new List<MainWindow>();
            RecentWriteOwnershipSmokeResult result;
            try
            {
                var initial = HiddenWindow();
                windows.Add(initial);
                initial.Show();
                await initial.LoadFolderAsync(folderA);
                bool firstExplicitCommit = initial.SharedRecentCommitAttemptCountForSmoke == 1
                    && initial.SharedRecentCommitSuccessCountForSmoke == 1;
                string afterFirstCommit = FileFingerprint(recentPath);
                await initial.SetSearchInputForSmokeAsync("a.png");
                initial.SetRightPanelWidthForSmoke(612);
                initial.SetShowUnseenDotsForSmoke(true);
                initial.FlushStateForSmoke();
                await initial.RefreshActiveFolderForSmokeAsync();
                initial.FlushStateForSmoke();
                bool sameWindowGeneralSavesUntouched = string.Equals(afterFirstCommit, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && initial.SharedRecentCommitAttemptCountForSmoke == 1
                    && initial.SharedRecentCommitSuccessCountForSmoke == 1;
                initial.Close();
                bool firstCloseUntouched = string.Equals(afterFirstCommit, FileFingerprint(recentPath), StringComparison.Ordinal);

                string externalFolder = Path.Combine(smokeRoot, "external-owner");
                var externalHistory = Enumerable.Range(0, 12)
                    .Select(index => new[] { index == 0 ? externalFolder : Path.Combine(smokeRoot, $"external-history-{index:00}") })
                    .ToArray();
                File.WriteAllText(recentPath, JsonSerializer.Serialize(new
                {
                    version = 1,
                    lastFolderSet = new[] { externalFolder },
                    recentFolderSets = externalHistory,
                    updatedAtUtc = "2026-07-18T01:02:03.0000000Z",
                    futureOwner = new { token = "external-newest", nested = new[] { 2, 7, 1, 8 } },
                }, new JsonSerializerOptions { WriteIndented = true }));
                string externalBytes = FileFingerprint(recentPath);
                string externalUnknown;
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(recentPath)))
                    externalUnknown = document.RootElement.GetProperty("futureOwner").GetRawText();

                var passive = HiddenWindow();
                windows.Add(passive);
                passive.Show();
                passive.SetRightPanelWidthForSmoke(477);
                passive.FlushStateForSmoke();
                passive.Close();
                bool externalOwnerPreservedByGeneralState = string.Equals(externalBytes, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && passive.SharedRecentCommitAttemptCountForSmoke == 0
                    && passive.SharedRecentCommitSuccessCountForSmoke == 0;

                var explicitB = HiddenWindow();
                windows.Add(explicitB);
                explicitB.Show();
                await explicitB.LoadFolderAsync(folderB);
                bool secondExplicitCommit = explicitB.SharedRecentCommitAttemptCountForSmoke == 1
                    && explicitB.SharedRecentCommitSuccessCountForSmoke == 1;
                bool latestMerged;
                bool unknownPreserved;
                int mergedHistoryCount;
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(recentPath)))
                {
                    JsonElement root = document.RootElement;
                    var last = root.GetProperty("lastFolderSet").EnumerateArray().Select(static item => item.GetString()).ToList();
                    var history = root.GetProperty("recentFolderSets").EnumerateArray().ToList();
                    mergedHistoryCount = history.Count;
                    string? first = history[0].EnumerateArray().First().GetString();
                    string? second = history[1].EnumerateArray().First().GetString();
                    latestMerged = last.Count == 1
                        && string.Equals(last[0], Path.GetFullPath(folderB), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(first, Path.GetFullPath(folderB), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(second, Path.GetFullPath(externalFolder), StringComparison.OrdinalIgnoreCase)
                        && history.Count == 12;
                    unknownPreserved = string.Equals(root.GetProperty("futureOwner").GetRawText(), externalUnknown, StringComparison.Ordinal);
                }
                string afterSecondCommit = FileFingerprint(recentPath);
                explicitB.FlushStateForSmoke();
                explicitB.SetRightPanelWidthForSmoke(688);
                explicitB.Close();
                bool secondCloseUntouched = string.Equals(afterSecondCommit, FileFingerprint(recentPath), StringComparison.Ordinal);

                var retry = HiddenWindow();
                windows.Add(retry);
                retry.Show();
                File.WriteAllText(lockPath, "{\"owner\":\"external-test\"}");
                string beforeRefusal = FileFingerprint(recentPath);
                await retry.LoadFolderAsync(folderA);
                bool failedNotMarkedSuccessful = retry.SharedRecentCommitAttemptCountForSmoke == 1
                    && retry.SharedRecentCommitSuccessCountForSmoke == 0
                    && string.Equals(beforeRefusal, FileFingerprint(recentPath), StringComparison.Ordinal);
                File.Delete(lockPath);
                await retry.LoadFolderAsync(folderA);
                int attemptsAfterRetry = retry.SharedRecentCommitAttemptCountForSmoke;
                int successesAfterRetry = retry.SharedRecentCommitSuccessCountForSmoke;
                string afterRetry = FileFingerprint(recentPath);
                await retry.LoadFolderAsync(folderA);
                bool retriedThenDeduplicated = attemptsAfterRetry == 2
                    && successesAfterRetry == 1
                    && retry.SharedRecentCommitAttemptCountForSmoke == 2
                    && retry.SharedRecentCommitSuccessCountForSmoke == 1
                    && string.Equals(afterRetry, FileFingerprint(recentPath), StringComparison.Ordinal);
                retry.Close();
                bool retryCloseUntouched = string.Equals(afterRetry, FileFingerprint(recentPath), StringComparison.Ordinal);

                bool sourceUntouched = Directory.EnumerateFiles(folderA, "*.png").Count() == 1
                    && Directory.EnumerateFiles(folderB, "*.png").Count() == 1;
                bool residueFree = NoPersistenceResidue(smokeRoot);
                bool ok = firstExplicitCommit && sameWindowGeneralSavesUntouched && firstCloseUntouched
                    && externalOwnerPreservedByGeneralState && secondExplicitCommit && latestMerged && unknownPreserved
                    && secondCloseUntouched && failedNotMarkedSuccessful && retriedThenDeduplicated
                    && retryCloseUntouched && sourceUntouched && residueFree;
                result = new RecentWriteOwnershipSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "shared recent writes are owned only by successful explicit folder-set commits; latest external data, unknown fields, retry, cap, and close isolation passed"
                        : "recent write ownership smoke did not meet the explicit-commit contract",
                    SmokeRoot = smokeRoot,
                    FirstExplicitCommit = firstExplicitCommit,
                    GeneralStateAndRefreshByteIdentical = sameWindowGeneralSavesUntouched && firstCloseUntouched,
                    ExternalOwnerByteIdentical = externalOwnerPreservedByGeneralState,
                    SecondExplicitCommit = secondExplicitCommit,
                    LatestExternalHistoryMerged = latestMerged,
                    UnknownFieldsPreserved = unknownPreserved,
                    MergedHistoryCount = mergedHistoryCount,
                    ExplicitCommitThenCloseByteIdentical = secondCloseUntouched,
                    FailedWriteNotMarkedSuccessful = failedNotMarkedSuccessful,
                    RetrySucceededAndSameSetDeduplicated = retriedThenDeduplicated,
                    RetryThenCloseByteIdentical = retryCloseUntouched,
                    SourceUntouched = sourceUntouched,
                    ResidueFree = residueFree,
                };
            }
            catch (Exception ex)
            {
                result = new RecentWriteOwnershipSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot };
            }
            finally
            {
                foreach (MainWindow window in windows)
                {
                    try { if (window.IsLoaded) window.Close(); } catch { }
                }
                try { if (File.Exists(lockPath)) File.Delete(lockPath); } catch { }
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteRecentWriteOwnershipSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureRapidUiStateSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-rapid-ui-state-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "rapid fixture");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string jobsPath = Path.Combine(smokeRoot, "jobs.json");
        const int fixtureCount = 321;
        const string finalName = "rapid-final-target.png";
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        try
        {
            Directory.CreateDirectory(folder);
            for (int index = 0; index < fixtureCount - 1; index++)
            {
                int width = index < 24 ? 640 : 12;
                int height = index < 24 ? 480 : 9;
                WriteSmokePng(
                    Path.Combine(folder, $"rapid-image-{index:000}.png"),
                    width,
                    height,
                    Color.FromRgb((byte)(40 + index % 180), (byte)(70 + index % 140), (byte)(90 + index % 120)));
            }
            WriteSmokePng(Path.Combine(folder, finalName), 800, 600, Color.FromRgb(230, 95, 145));
            File.WriteAllText(jobsPath, "{\"jobs\":[]}");
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        }
        catch (Exception ex)
        {
            WriteRapidUiStateSmokeResult(resultFullPath, new RapidUiStateSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            RapidUiStateSmokeResult result;
            var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(15) };
            int heartbeatCount = 0;
            heartbeat.Tick += (_, _) => heartbeatCount++;
            var watch = Stopwatch.StartNew();
            try
            {
                await win.LoadFolderAsync(folder);
                string sourceBefore = FolderFingerprint(folder);
                string jobsBefore = FileFingerprint(jobsPath);
                heartbeat.Start();

                string[] churnNames = Enumerable.Range(0, 24).Select(index => $"rapid-image-{index:000}.png").ToArray();
                bool selectionChurnAccepted = churnNames.All(win.SelectFileNameForSmoke);
                bool finalSelected = win.SelectFileNameForSmoke(finalName);
                PreviewDecodeSmokeSnapshot preview = await win.WaitForCurrentPreviewDecodeForSmokeAsync(finalName);

                bool listMode = win.SetListModeForSmoke() && win.ListModeVisibleForSmoke;
                bool compact = win.SetDisplayStyleForSmoke("compact");
                bool portrait = win.SetAspectModeForSmoke("portrait");
                bool gridMode = win.SetGridModeForSmoke() && win.GridModeVisibleForSmoke;
                bool standard = win.SetDisplayStyleForSmoke("standard");
                bool original = win.SetAspectModeForSmoke("original");
                bool poster = win.SetDisplayStyleForSmoke("poster");
                bool square = win.SetAspectModeForSmoke("square");

                win.FlushStateForSmoke();
                string stateBeforeResizePreview = FileFingerprint(statePath);
                double[] rapidWidths = [260, 740, 315, 880, 420, 612];
                bool widthsAccepted = rapidWidths.All(win.PreviewRightPanelWidthForSmoke);
                string stateDuringResizePreview = FileFingerprint(statePath);
                win.CommitRightPanelWidthForSmoke();
                string stateAfterResizeCommit = FileFingerprint(statePath);
                bool resizeCommittedOnce = string.Equals(stateBeforeResizePreview, stateDuringResizePreview, StringComparison.Ordinal)
                    && !string.Equals(stateDuringResizePreview, stateAfterResizeCommit, StringComparison.Ordinal)
                    && Nearly(win.RightPanelStoredWidthForSmoke, 612);

                string[] favoriteNames = ["rapid-image-000.png", "rapid-image-001.png", "rapid-image-002.png", "rapid-image-003.png", "rapid-image-004.png"];
                bool favoriteLevelsAssigned = true;
                for (int index = 0; index < favoriteNames.Length; index++)
                {
                    favoriteLevelsAssigned &= win.SelectFileNameForSmoke(favoriteNames[index]);
                    favoriteLevelsAssigned &= win.SetSelectedFavoriteLevelForSmoke(index + 1);
                }
                favoriteLevelsAssigned &= win.SelectFileNameForSmoke(finalName) && win.SetSelectedFavoriteLevelForSmoke(3);

                win.SetFavoriteFilterLevelsForSmoke(1);
                win.SetFavoriteFilterLevelsForSmoke(2, 4);
                win.SetFavoriteFilterLevelsForSmoke(5);
                win.SetFavoriteFilterLevelsForSmoke();
                int allCount = win.FilteredCountForSmoke;
                int unseenCount = win.UnseenCountForSmoke;
                win.SetUnseenOnlyFilterForSmoke(true);
                int unseenFilteredCount = win.FilteredCountForSmoke;
                win.SetUnseenOnlyFilterForSmoke(false);
                int afterUnseenClearCount = win.FilteredCountForSmoke;
                win.SetShowUnseenDotsForSmoke(false);
                bool dotsHidden = win.VisibleUnseenDotCountForSmoke == 0;
                win.SetShowUnseenDotsForSmoke(true);
                bool dotsShown = win.VisibleUnseenDotCountForSmoke == unseenCount && unseenCount > 0;
                win.SetFavoriteFilterLevelsForSmoke(3);

                win.SetFavoriteFilterLevelsForSmoke();
                bool openedFirst = win.SelectFileNameForSmoke("rapid-image-000.png") && win.OpenSelectedPreviewTabForSmoke();
                bool openedSecond = win.SelectFileNameForSmoke("rapid-image-010.png") && win.OpenSelectedPreviewTabForSmoke();
                bool openedThird = win.SelectFileNameForSmoke("rapid-image-020.png") && win.OpenSelectedPreviewTabForSmoke();
                bool reordered = win.DragMovePreviewTabForSmoke("rapid-image-020.png", 0);
                bool pinned = win.TogglePreviewTabPinForSmoke("rapid-image-000.png");
                bool closed = win.ClosePreviewTabForSmoke("rapid-image-010.png");
                win.SetFavoriteFilterLevelsForSmoke(3);

                string[] rapidQueries = ["r", "ra", "rapid", "rapid-image", "rapid-final", "rapid-final-target"];
                var searchTasks = new List<Task<MainWindow.SearchFilterCompletion>>();
                foreach (string query in rapidQueries)
                    searchTasks.Add(win.SetSearchInputForSmokeAsync(query));
                Task timeout = Task.Delay(TimeSpan.FromSeconds(8));
                Task finished = await Task.WhenAny(searchTasks[^1], timeout);
                MainWindow.SearchFilterCompletion finalSearch = finished == searchTasks[^1]
                    ? await searchTasks[^1]
                    : new MainWindow.SearchFilterCompletion(false, false, "timed out waiting for rapid UI final search");
                MainWindow.SearchFilterCompletion[] searchResults = await Task.WhenAll(searchTasks);
                int discardedSearches = searchResults.Take(searchResults.Length - 1).Count(static completion => completion.Discarded);
                bool finalSearchOnly = finalSearch.Applied
                    && string.Equals(win.SearchQueryForSmoke, "rapid-final-target", StringComparison.Ordinal)
                    && SameNameOrder(win.FilteredFileNamesForSmoke(10), [finalName]);
                bool openedFinal = win.SelectFileNameForSmoke(finalName) && win.OpenSelectedPreviewTabForSmoke();
                win.FlushStateForSmoke();
                await Task.Delay(200);
                heartbeat.Stop();

                List<string> finalTabs = win.PreviewTabNamesForSmoke;
                string? finalActiveTab = win.ActivePreviewTabNameForSmoke;
                ViewerState? persisted = ReadPersistedState(statePath);
                bool finalStatePersisted = persisted is not null
                    && string.Equals(persisted.SearchQuery, "rapid-final-target", StringComparison.Ordinal)
                    && string.Equals(persisted.SelectedPath, Path.Combine(folder, finalName), StringComparison.OrdinalIgnoreCase)
                    && Nearly(persisted.RightPanelWidth, 612)
                    && string.Equals(persisted.DisplayStyle, "poster", StringComparison.Ordinal)
                    && string.Equals(persisted.AspectMode, "square", StringComparison.Ordinal)
                    && persisted.FavoriteFilterLevels?.SequenceEqual([3]) == true
                    && persisted.ShowUnseenDots
                    && persisted.PreviewTabPaths?.Select(Path.GetFileName).SequenceEqual(finalTabs, StringComparer.OrdinalIgnoreCase) == true
                    && string.Equals(Path.GetFileName(persisted.ActivePreviewTabPath), finalActiveTab, StringComparison.OrdinalIgnoreCase)
                    && persisted.PinnedPreviewPaths?.Select(Path.GetFileName).SequenceEqual(["rapid-image-000.png"], StringComparer.OrdinalIgnoreCase) == true;

                win.Close();
                var reload = HiddenWindow();
                reload.Show();
                await reload.LoadFolderAsync(folder);
                await reload.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                bool restored = string.Equals(reload.SearchQueryForSmoke, "rapid-final-target", StringComparison.Ordinal)
                    && string.Equals(reload.SelectedFileNameForSmoke, finalName, StringComparison.OrdinalIgnoreCase)
                    && Nearly(reload.RightPanelStoredWidthForSmoke, 612)
                    && string.Equals(reload.DisplayStyleForSmoke, "poster", StringComparison.Ordinal)
                    && string.Equals(reload.AspectModeForSmoke, "square", StringComparison.Ordinal)
                    && reload.FavoriteFilterLevelsForSmoke.SequenceEqual([3])
                    && reload.ShowUnseenDotsForSmoke
                    && !reload.UnseenOnlyForSmoke
                    && reload.PreviewTabNamesForSmoke.SequenceEqual(finalTabs, StringComparer.OrdinalIgnoreCase)
                    && string.Equals(reload.ActivePreviewTabNameForSmoke, finalActiveTab, StringComparison.OrdinalIgnoreCase)
                    && reload.IsPreviewTabPinnedForSmoke("rapid-image-000.png");
                bool passive = win.EnhancementJobsReadForSmoke == 0 && win.EnhancedCandidateCountForSmoke == 0
                    && reload.EnhancementJobsReadForSmoke == 0 && reload.EnhancedCandidateCountForSmoke == 0
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal);
                bool sourceUntouched = string.Equals(sourceBefore, FolderFingerprint(folder), StringComparison.Ordinal);
                bool isolated = new[] { statePath, favoritesPath, seenPath, recentPath, jobsPath }
                    .All(path => Path.GetFullPath(path).StartsWith(Path.GetFullPath(smokeRoot), StringComparison.OrdinalIgnoreCase));
                reload.Close();

                bool tabChurn = openedFirst && openedSecond && openedThird && reordered && pinned && closed && openedFinal
                    && finalTabs.SequenceEqual(["rapid-image-020.png", "rapid-image-000.png", finalName], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(finalActiveTab, finalName, StringComparison.OrdinalIgnoreCase);
                bool modeAndLayoutChurn = listMode && compact && portrait && gridMode && standard && original && poster && square;
                bool filterChurn = favoriteLevelsAssigned
                    && allCount == fixtureCount
                    && unseenFilteredCount == unseenCount
                    && afterUnseenClearCount == fixtureCount
                    && dotsHidden && dotsShown;
                bool heartbeatAdvanced = heartbeatCount >= 5;
                bool ok = win.CatalogCountForSmoke == fixtureCount
                    && selectionChurnAccepted && finalSelected && preview.StableLatestSelection
                    && modeAndLayoutChurn && widthsAccepted && resizeCommittedOnce
                    && filterChurn && tabChurn
                    && discardedSearches == rapidQueries.Length - 1 && finalSearchOnly
                    && finalStatePersisted && restored && heartbeatAdvanced
                    && passive && sourceUntouched && isolated;
                watch.Stop();
                result = new RapidUiStateSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "rapid UI/state churn applied only final search, selection, layout, panel, filter, and tab state while keeping heartbeat and enhancement isolation"
                        : "rapid UI/state churn did not restore the final input state",
                    SmokeRoot = smokeRoot,
                    FixtureCount = fixtureCount,
                    SelectionChurnAccepted = selectionChurnAccepted,
                    Preview = preview,
                    ModeAndLayoutChurn = modeAndLayoutChurn,
                    ResizePreviewDidNotPersist = string.Equals(stateBeforeResizePreview, stateDuringResizePreview, StringComparison.Ordinal),
                    ResizeCommitted = resizeCommittedOnce,
                    AllFavoriteSemanticsCount = allCount,
                    FavoriteLevelsAssigned = favoriteLevelsAssigned,
                    UnseenCount = unseenCount,
                    UnseenFilteredCount = unseenFilteredCount,
                    DotsHidden = dotsHidden,
                    DotsShown = dotsShown,
                    TabChurn = tabChurn,
                    FinalTabs = finalTabs,
                    FinalActiveTab = finalActiveTab,
                    DiscardedSearches = discardedSearches,
                    FinalSearchApplied = finalSearchOnly,
                    FinalStatePersisted = finalStatePersisted,
                    Restored = restored,
                    ReloadQuery = reload.SearchQueryForSmoke,
                    ReloadSelected = reload.SelectedFileNameForSmoke,
                    ReloadRightPanelWidth = reload.RightPanelStoredWidthForSmoke,
                    ReloadDisplayStyle = reload.DisplayStyleForSmoke,
                    ReloadAspectMode = reload.AspectModeForSmoke,
                    ReloadFavoriteLevels = reload.FavoriteFilterLevelsForSmoke,
                    ReloadShowUnseenDots = reload.ShowUnseenDotsForSmoke,
                    ReloadUnseenOnly = reload.UnseenOnlyForSmoke,
                    ReloadTabs = reload.PreviewTabNamesForSmoke,
                    ReloadActiveTab = reload.ActivePreviewTabNameForSmoke,
                    ReloadPinned = reload.IsPreviewTabPinnedForSmoke("rapid-image-000.png"),
                    HeartbeatCount = heartbeatCount,
                    EnhancementPassive = passive,
                    SourceUntouched = sourceUntouched,
                    Isolated = isolated,
                    ElapsedMs = watch.ElapsedMilliseconds,
                };
            }
            catch (Exception ex)
            {
                heartbeat.Stop();
                result = new RapidUiStateSmokeResult { Message = ex.ToString(), SmokeRoot = smokeRoot, HeartbeatCount = heartbeatCount };
            }
            finally
            {
                if (win.IsVisible) win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteRapidUiStateSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureShutdownStateSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-shutdown-state-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string jobsPath = Path.Combine(smokeRoot, "jobs.json");
        const string firstName = "shutdown-first.png";
        const string secondName = "shutdown-second.png";
        const string finalName = "shutdown-final-target.png";
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        try
        {
            Directory.CreateDirectory(folder);
            WriteSmokePng(Path.Combine(folder, firstName), 1600, 1200, Color.FromRgb(65, 125, 220));
            WriteSmokePng(Path.Combine(folder, secondName), 1400, 1000, Color.FromRgb(95, 190, 125));
            WriteSmokePng(Path.Combine(folder, finalName), 1800, 1350, Color.FromRgb(225, 100, 150));
            File.WriteAllText(jobsPath, "{\"jobs\":[]}");
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        }
        catch (Exception ex)
        {
            WriteShutdownStateSmokeResult(resultFullPath, new ShutdownStateSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            ShutdownStateSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                bool layoutReady = win.SetDisplayStyleForSmoke("poster")
                    && win.SetAspectModeForSmoke("portrait")
                    && win.SetRightPanelWidthForSmoke(644);
                win.SetFavoriteFilterLevelsForSmoke(2, 5);
                win.SetShowUnseenDotsForSmoke(true);
                win.SetConfirmBeforeDeleteForSmoke(false);

                bool openedFirst = win.SelectFileNameForSmoke(firstName) && win.OpenSelectedPreviewTabForSmoke();
                bool openedSecond = win.SelectFileNameForSmoke(secondName) && win.OpenSelectedPreviewTabForSmoke();
                bool openedFinal = win.SelectFileNameForSmoke(finalName) && win.OpenSelectedPreviewTabForSmoke();
                bool reordered = win.DragMovePreviewTabForSmoke(secondName, 0);
                bool pinned = win.TogglePreviewTabPinForSmoke(firstName);
                win.FlushStateForSmoke();

                win.SetPreviewTabHoverDecodeDelayForSmoke(firstName, 700);
                bool hoverStarted = win.ShowPreviewTabHoverWithDecodeForSmoke(firstName);
                Task<MainWindow.PreviewTabHoverDecodeCompletion> hoverCompletion = win.WaitForPreviewTabHoverDecodeForSmokeAsync();
                bool finalSelected = win.SelectFileNameForSmoke(finalName);
                Task<PreviewDecodeSmokeSnapshot> previewCompletion = win.WaitForCurrentPreviewDecodeForSmokeAsync(finalName);
                string sourceBefore = FolderFingerprint(folder);
                string favoritesBefore = FileFingerprint(favoritesPath);
                string seenBefore = FileFingerprint(seenPath);
                string recentBefore = FileFingerprint(recentPath);
                string jobsBefore = FileFingerprint(jobsPath);
                Task<MainWindow.SearchFilterCompletion> staleOne = win.SetSearchInputForSmokeAsync("sh");
                Task<MainWindow.SearchFilterCompletion> staleTwo = win.SetSearchInputForSmokeAsync("shutdown-final");
                Task<MainWindow.SearchFilterCompletion> finalSearch = win.SetSearchInputForSmokeAsync("shutdown-final-target");

                var closeWatch = Stopwatch.StartNew();
                win.Close();
                closeWatch.Stop();
                int flushCount = win.ShutdownPersistenceFlushCountForSmoke;
                MainWindow.SearchFilterCompletion[] searchCompletions = await Task.WhenAll(staleOne, staleTwo, finalSearch);
                MainWindow.PreviewTabHoverDecodeCompletion hoverAfterClose = await hoverCompletion;
                Task previewTimeout = Task.Delay(TimeSpan.FromSeconds(3));
                Task previewFinished = await Task.WhenAny(previewCompletion, previewTimeout);
                bool previewSettled = previewFinished == previewCompletion;
                string stateImmediatelyAfterClose = FileFingerprint(statePath);
                await Task.Delay(800);
                bool oldAsyncDidNotMutateState = string.Equals(stateImmediatelyAfterClose, FileFingerprint(statePath), StringComparison.Ordinal);
                bool closeStoreIsolation = string.Equals(sourceBefore, FolderFingerprint(folder), StringComparison.Ordinal)
                    && string.Equals(favoritesBefore, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(seenBefore, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBefore, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal);
                bool closeResidueFree = NoPersistenceResidue(smokeRoot);
                ViewerState? persisted = ReadPersistedState(statePath);
                List<string> expectedTabs = [secondName, firstName, finalName];
                bool finalPersisted = persisted is not null
                    && string.Equals(persisted.SearchQuery, "shutdown-final-target", StringComparison.Ordinal)
                    && string.Equals(Path.GetFileName(persisted.SelectedPath), finalName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(persisted.DisplayStyle, "poster", StringComparison.Ordinal)
                    && string.Equals(persisted.AspectMode, "portrait", StringComparison.Ordinal)
                    && Nearly(persisted.RightPanelWidth, 644)
                    && persisted.FavoriteFilterLevels?.SequenceEqual([2, 5]) == true
                    && persisted.ShowUnseenDots
                    && !persisted.ConfirmBeforeDelete
                    && persisted.PreviewTabPaths?.Select(Path.GetFileName).SequenceEqual(expectedTabs, StringComparer.OrdinalIgnoreCase) == true
                    && string.Equals(Path.GetFileName(persisted.ActivePreviewTabPath), finalName, StringComparison.OrdinalIgnoreCase)
                    && persisted.PinnedPreviewPaths?.Select(Path.GetFileName).SequenceEqual([firstName], StringComparer.OrdinalIgnoreCase) == true;

                var reload = HiddenWindow();
                reload.Show();
                await reload.LoadFolderAsync(folder);
                bool restored = string.Equals(reload.SearchQueryForSmoke, "shutdown-final-target", StringComparison.Ordinal)
                    && string.Equals(reload.SelectedFileNameForSmoke, finalName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(reload.DisplayStyleForSmoke, "poster", StringComparison.Ordinal)
                    && string.Equals(reload.AspectModeForSmoke, "portrait", StringComparison.Ordinal)
                    && Nearly(reload.RightPanelStoredWidthForSmoke, 644)
                    && reload.FavoriteFilterLevelsForSmoke.SequenceEqual([2, 5])
                    && reload.ShowUnseenDotsForSmoke
                    && !reload.ConfirmBeforeDeleteForSmoke
                    && reload.PreviewTabNamesForSmoke.SequenceEqual(expectedTabs, StringComparer.OrdinalIgnoreCase)
                    && string.Equals(reload.ActivePreviewTabNameForSmoke, finalName, StringComparison.OrdinalIgnoreCase)
                    && reload.IsPreviewTabPinnedForSmoke(firstName);
                string sourceBeforeReloadClose = FolderFingerprint(folder);
                string favoritesBeforeReloadClose = FileFingerprint(favoritesPath);
                string seenBeforeReloadClose = FileFingerprint(seenPath);
                string recentBeforeReloadClose = FileFingerprint(recentPath);
                string jobsBeforeReloadClose = FileFingerprint(jobsPath);
                reload.Close();
                bool reloadCloseIsolation = string.Equals(sourceBeforeReloadClose, FolderFingerprint(folder), StringComparison.Ordinal)
                    && string.Equals(favoritesBeforeReloadClose, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(seenBeforeReloadClose, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBeforeReloadClose, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && string.Equals(jobsBeforeReloadClose, FileFingerprint(jobsPath), StringComparison.Ordinal);
                bool reloadFlushOnce = reload.ShutdownPersistenceFlushCountForSmoke == 1;

                async Task<ShutdownRefusalSnapshot> RunRefusalAsync(string name, string content, bool contend)
                {
                    string scenarioRoot = Path.Combine(smokeRoot, name);
                    Directory.CreateDirectory(scenarioRoot);
                    string scenarioState = Path.Combine(scenarioRoot, "state.json");
                    File.WriteAllText(scenarioState, content);
                    Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", scenarioState);
                    Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", Path.Combine(scenarioRoot, "recent.json"));
                    var scenario = HiddenWindow();
                    scenario.Show();
                    string before = FileFingerprint(scenarioState);
                    string lockPath = scenarioState + ".lock";
                    if (contend)
                        File.WriteAllText(lockPath, "{\"owner\":\"smoke\"}");
                    Task<MainWindow.SearchFilterCompletion> pending = scenario.SetSearchInputForSmokeAsync("must-not-overwrite");
                    var scenarioCloseWatch = Stopwatch.StartNew();
                    scenario.Close();
                    scenarioCloseWatch.Stop();
                    MainWindow.SearchFilterCompletion completion = await pending;
                    bool lockRemainedOwned = !contend || File.Exists(lockPath);
                    if (contend)
                        File.Delete(lockPath);
                    return new ShutdownRefusalSnapshot(
                        Unchanged: string.Equals(before, FileFingerprint(scenarioState), StringComparison.Ordinal),
                        Closed: !scenario.IsVisible,
                        FlushCount: scenario.ShutdownPersistenceFlushCountForSmoke,
                        PendingDiscarded: completion.Discarded,
                        CloseMs: scenarioCloseWatch.ElapsedMilliseconds,
                        LockRemainedOwned: lockRemainedOwned,
                        ResidueFree: NoPersistenceResidue(scenarioRoot));
                }

                ShutdownRefusalSnapshot malformed = await RunRefusalAsync("malformed", "{broken", contend: false);
                ShutdownRefusalSnapshot protectedFuture = await RunRefusalAsync(
                    "protected",
                    "{\"Version\":999,\"SearchQuery\":\"preserve-future\",\"future\":true}",
                    contend: false);
                ShutdownRefusalSnapshot contended = await RunRefusalAsync(
                    "contended",
                    JsonSerializer.Serialize(new ViewerState { Version = 2, SearchQuery = "preserve-contended" }),
                    contend: true);

                bool refusalsSafe = new[] { malformed, protectedFuture, contended }.All(snapshot =>
                    snapshot.Unchanged && snapshot.Closed && snapshot.FlushCount == 1
                    && snapshot.PendingDiscarded && snapshot.CloseMs < 1_000
                    && snapshot.LockRemainedOwned && snapshot.ResidueFree);
                bool searchCancelled = searchCompletions.All(static completion => completion.Discarded);
                bool asyncCancelled = searchCancelled && hoverAfterClose.Discarded && previewSettled;
                bool enhancementPassive = win.EnhancementJobsReadForSmoke == 0 && win.EnhancedCandidateCountForSmoke == 0
                    && reload.EnhancementJobsReadForSmoke == 0 && reload.EnhancedCandidateCountForSmoke == 0;
                bool setupReady = layoutReady && openedFirst && openedSecond && openedFinal && reordered && pinned && hoverStarted && finalSelected;
                bool ok = setupReady && closeWatch.ElapsedMilliseconds < 300 && flushCount == 1
                    && asyncCancelled && oldAsyncDidNotMutateState
                    && closeStoreIsolation && reloadCloseIsolation
                    && closeResidueFree && NoPersistenceResidue(smokeRoot)
                    && finalPersisted && restored && reloadFlushOnce
                    && refusalsSafe && enhancementPassive;
                result = new ShutdownStateSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "shutdown flushed only the final viewer state once, cancelled stale work, and preserved protected/contended stores"
                        : "shutdown state lifecycle did not meet final-state or refusal safety expectations",
                    SmokeRoot = smokeRoot,
                    SetupReady = setupReady,
                    CloseMs = closeWatch.ElapsedMilliseconds,
                    FlushCount = flushCount,
                    SearchDiscarded = searchCancelled,
                    HoverDiscarded = hoverAfterClose.Discarded,
                    PreviewSettled = previewSettled,
                    OldAsyncDidNotMutateState = oldAsyncDidNotMutateState,
                    FinalPersisted = finalPersisted,
                    Restored = restored,
                    CloseStoreIsolation = closeStoreIsolation,
                    ReloadCloseIsolation = reloadCloseIsolation,
                    ResidueFree = closeResidueFree && NoPersistenceResidue(smokeRoot),
                    EnhancementPassive = enhancementPassive,
                    Malformed = malformed,
                    ProtectedFuture = protectedFuture,
                    Contended = contended,
                };
            }
            catch (Exception ex)
            {
                result = new ShutdownStateSmokeResult { Message = ex.ToString(), SmokeRoot = smokeRoot };
            }
            finally
            {
                if (win.IsVisible) win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteShutdownStateSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureBulkFavoriteSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-bulk-favorite-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");

        Directory.CreateDirectory(folder);
        string alphaPath = Path.Combine(folder, "alpha.png");
        string bravoPath = Path.Combine(folder, "bravo.png");
        string charliePath = Path.Combine(folder, "charlie.png");
        string deltaPath = Path.Combine(folder, "delta.png");
        WriteSmokePng(alphaPath, 32, 24, Color.FromRgb(55, 135, 210));
        WriteSmokePng(bravoPath, 32, 24, Color.FromRgb(100, 185, 115));
        WriteSmokePng(charliePath, 32, 24, Color.FromRgb(195, 125, 75));
        WriteSmokePng(deltaPath, 32, 24, Color.FromRgb(145, 95, 195));
        DateTime commonTimestamp = DateTime.UtcNow.AddMinutes(-5);
        foreach (string path in new[] { alphaPath, bravoPath, charliePath, deltaPath })
        {
            File.SetCreationTimeUtc(path, commonTimestamp);
            File.SetLastWriteTimeUtc(path, commonTimestamp);
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            BulkFavoriteSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSortByForSmoke("name");

                bool selectedAlpha = win.SelectFileNameForSmoke("alpha.png");
                bool singlePanelHidden = !win.BulkFavoritePanelVisibleForSmoke;
                bool alphaSet = win.SetSelectedFavoriteLevelForSmoke(1);
                bool selectedBravo = win.SelectFileNameForSmoke("bravo.png");
                bool bravoSet = win.SetSelectedFavoriteLevelForSmoke(3);
                bool selectedRange = win.SelectRangeForSmoke(0, 2);
                List<string> initialNames = win.SelectedFileNamesForSmoke;
                List<int> initialLevels = win.SelectedFavoriteLevelsForSmoke;
                string mixedSummary = win.BulkSelectionSummaryForSmoke;
                bool mixedPanel = win.BulkFavoritePanelVisibleForSmoke
                    && mixedSummary.Contains("3 images selected", StringComparison.OrdinalIgnoreCase)
                    && mixedSummary.Contains("mixed", StringComparison.OrdinalIgnoreCase);

                var externalMap = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(favoritesPath))
                    ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                externalMap[NormalizeFavoritePath(deltaPath)] = 2;
                File.WriteAllText(favoritesPath, JsonSerializer.Serialize(externalMap, new JsonSerializerOptions { WriteIndented = true }));

                int beforeExactSet = win.FavoriteSaveAttemptCountForSmoke;
                bool exactSet = win.SetSelectedFavoriteLevelForSmoke(4);
                int exactSetAttempts = win.FavoriteSaveAttemptCountForSmoke - beforeExactSet;
                List<int> exactSetLevels = win.SelectedFavoriteLevelsForSmoke;
                bool exactSetPersisted = new[] { alphaPath, bravoPath, charliePath }.All(path => ReadFavoriteLevel(favoritesPath, path) == 4);
                bool externalPreservedAfterSet = ReadFavoriteLevel(favoritesPath, deltaPath) == 2;

                int beforeAdjust = win.FavoriteSaveAttemptCountForSmoke;
                bool adjusted = win.AdjustSelectedFavoriteForSmoke(1);
                int adjustAttempts = win.FavoriteSaveAttemptCountForSmoke - beforeAdjust;
                List<int> adjustedLevels = win.SelectedFavoriteLevelsForSmoke;
                bool adjustedPersisted = new[] { alphaPath, bravoPath, charliePath }.All(path => ReadFavoriteLevel(favoritesPath, path) == 5);
                bool externalPreservedAfterAdjust = ReadFavoriteLevel(favoritesPath, deltaPath) == 2;

                int beforeClear = win.FavoriteSaveAttemptCountForSmoke;
                bool cleared = win.SetSelectedFavoriteLevelForSmoke(0);
                int clearAttempts = win.FavoriteSaveAttemptCountForSmoke - beforeClear;
                List<int> clearedLevels = win.SelectedFavoriteLevelsForSmoke;
                bool clearedFromStore = new[] { alphaPath, bravoPath, charliePath }.All(path => !FavoriteFileContainsPath(favoritesPath, path));
                bool externalPreservedAfterClear = ReadFavoriteLevel(favoritesPath, deltaPath) == 2;

                int beforeRestore = win.FavoriteSaveAttemptCountForSmoke;
                bool restored = win.SetSelectedFavoriteLevelForSmoke(2);
                int restoreAttempts = win.FavoriteSaveAttemptCountForSmoke - beforeRestore;
                List<int> restoredLevels = win.SelectedFavoriteLevelsForSmoke;

                var driftedMap = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(favoritesPath))
                    ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                driftedMap[NormalizeFavoritePath(alphaPath)] = 1;
                driftedMap[NormalizeFavoritePath(bravoPath)] = 1;
                driftedMap[NormalizeFavoritePath(charliePath)] = 1;
                File.WriteAllText(favoritesPath, JsonSerializer.Serialize(driftedMap, new JsonSerializerOptions { WriteIndented = true }));
                int beforeReassert = win.FavoriteSaveAttemptCountForSmoke;
                bool reasserted = win.SetSelectedFavoriteLevelForSmoke(2);
                int reassertAttempts = win.FavoriteSaveAttemptCountForSmoke - beforeReassert;
                bool reassertedPersisted = new[] { alphaPath, bravoPath, charliePath }.All(path => ReadFavoriteLevel(favoritesPath, path) == 2);
                bool externalPreservedAfterReassert = ReadFavoriteLevel(favoritesPath, deltaPath) == 2;

                const string malformedFavorites = "{\"broken\":{}}";
                File.WriteAllText(favoritesPath, malformedFavorites);
                int beforeMalformed = win.FavoriteSaveAttemptCountForSmoke;
                bool malformedRefused = !win.SetSelectedFavoriteLevelForSmoke(3);
                int malformedAttempts = win.FavoriteSaveAttemptCountForSmoke - beforeMalformed;
                List<int> afterRefusalLevels = win.SelectedFavoriteLevelsForSmoke;
                bool malformedPreserved = string.Equals(File.ReadAllText(favoritesPath), malformedFavorites, StringComparison.Ordinal);
                bool refusalStatus = win.DeleteStatusForSmoke.Contains("Favorites", StringComparison.OrdinalIgnoreCase)
                    && win.DeleteStatusForSmoke.Contains("invalid", StringComparison.OrdinalIgnoreCase);

                bool ok = selectedAlpha && singlePanelHidden && alphaSet
                    && selectedBravo && bravoSet && selectedRange
                    && initialNames.SequenceEqual(new[] { "alpha.png", "bravo.png", "charlie.png" }, StringComparer.OrdinalIgnoreCase)
                    && initialLevels.SequenceEqual(new[] { 1, 3, 0 })
                    && mixedPanel
                    && exactSet && exactSetAttempts == 1 && exactSetLevels.All(level => level == 4)
                    && exactSetPersisted && externalPreservedAfterSet
                    && adjusted && adjustAttempts == 1 && adjustedLevels.All(level => level == 5)
                    && adjustedPersisted && externalPreservedAfterAdjust
                    && cleared && clearAttempts == 1 && clearedLevels.All(level => level == 0)
                    && clearedFromStore && externalPreservedAfterClear
                    && restored && restoreAttempts == 1 && restoredLevels.All(level => level == 2)
                    && reasserted && reassertAttempts == 1 && reassertedPersisted && externalPreservedAfterReassert
                    && malformedRefused && malformedAttempts == 1
                    && afterRefusalLevels.All(level => level == 2)
                    && malformedPreserved && refusalStatus;

                result = new BulkFavoriteSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "bulk favorite exact set, adjust, single atomic save, external merge, clear, and malformed rollback passed"
                        : "bulk favorite transaction or selection-surface expectations did not match",
                    SelectedNames = initialNames,
                    InitialLevels = initialLevels,
                    SinglePanelHidden = singlePanelHidden,
                    MixedPanel = mixedPanel,
                    MixedSummary = mixedSummary,
                    ExactSetAttempts = exactSetAttempts,
                    ExactSetLevels = exactSetLevels,
                    ExactSetPersisted = exactSetPersisted,
                    ExternalPreservedAfterSet = externalPreservedAfterSet,
                    AdjustAttempts = adjustAttempts,
                    AdjustedLevels = adjustedLevels,
                    AdjustedPersisted = adjustedPersisted,
                    ExternalPreservedAfterAdjust = externalPreservedAfterAdjust,
                    ClearAttempts = clearAttempts,
                    ClearedLevels = clearedLevels,
                    ClearedFromStore = clearedFromStore,
                    ExternalPreservedAfterClear = externalPreservedAfterClear,
                    RestoreAttempts = restoreAttempts,
                    RestoredLevels = restoredLevels,
                    ReassertAttempts = reassertAttempts,
                    ReassertedPersisted = reassertedPersisted,
                    ExternalPreservedAfterReassert = externalPreservedAfterReassert,
                    MalformedAttempts = malformedAttempts,
                    AfterRefusalLevels = afterRefusalLevels,
                    MalformedPreserved = malformedPreserved,
                    RefusalStatus = refusalStatus,
                };
            }
            catch (Exception ex)
            {
                result = new BulkFavoriteSmokeResult { Message = ex.ToString() };
            }
            finally
            {
                if (win.IsVisible)
                    win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                try
                {
                    string tempRoot = Path.GetFullPath(Path.GetTempPath());
                    string resolvedSmokeRoot = Path.GetFullPath(smokeRoot);
                    if (resolvedSmokeRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedSmokeRoot))
                        Directory.Delete(resolvedSmokeRoot, recursive: true);
                }
                catch { }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
            File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureBulkRecycleSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-bulk-recycle-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string recycle = Path.Combine(smokeRoot, "fake-recycle-bin");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");

        Directory.CreateDirectory(folder);
        string alphaPath = Path.Combine(folder, "alpha.png");
        string bravoPath = Path.Combine(folder, "bravo.png");
        string charliePath = Path.Combine(folder, "charlie.png");
        string deltaPath = Path.Combine(folder, "delta.png");
        foreach ((string path, Color color) in new[]
        {
            (alphaPath, Color.FromRgb(60, 140, 210)),
            (bravoPath, Color.FromRgb(100, 185, 115)),
            (charliePath, Color.FromRgb(195, 125, 75)),
            (deltaPath, Color.FromRgb(145, 95, 195)),
        })
        {
            WriteSmokePng(path, 32, 24, color);
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            BulkRecycleSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSortByForSmoke("name");
                bool favoriteBravo = win.SelectFileNameForSmoke("bravo.png") && win.SetSelectedFavoriteLevelForSmoke(1);
                bool favoriteCharlie = win.SelectFileNameForSmoke("charlie.png") && win.SetSelectedFavoriteLevelForSmoke(1);
                bool favoriteDelta = win.SelectFileNameForSmoke("delta.png") && win.SetSelectedFavoriteLevelForSmoke(1);
                bool tabBravo = win.SelectFileNameForSmoke("bravo.png") && win.OpenSelectedPreviewTabForSmoke();
                bool tabCharlie = win.SelectFileNameForSmoke("charlie.png") && win.OpenSelectedPreviewTabForSmoke();
                win.SetFavoriteOnlyFilterForSmoke(true);
                bool bulkSelected = win.SelectRangeForSmoke(0, 1);
                List<string> beforeCancelSelection = win.SelectedFileNamesForSmoke;
                int backendCalls = 0;
                bool hardDeleteAttempted = false;
                bool failCharlie = true;
                win.SetRecycleBinDeleteBackendForSmoke(path =>
                {
                    backendCalls++;
                    if (failCharlie && string.Equals(Path.GetFileName(path), "charlie.png", StringComparison.OrdinalIgnoreCase))
                        return RecycleBinDeleteResult.Failed("synthetic recycle failure");
                    Directory.CreateDirectory(recycle);
                    File.Move(path, Path.Combine(recycle, Path.GetFileName(path)));
                    return RecycleBinDeleteResult.Success;
                });

                bool promptedCancel = win.RequestBulkDeleteSelectedForSmoke() && win.DeleteConfirmationVisibleForSmoke;
                bool accessibleBulkAction = win.BulkDeleteButtonAccessibleForSmoke && win.DeleteFocusTrapConfiguredForSmoke;
                win.CancelDeleteForSmoke();
                bool cancelMutatedNothing = backendCalls == 0
                    && File.Exists(bravoPath) && File.Exists(charliePath)
                    && win.CatalogCountForSmoke == 4
                    && beforeCancelSelection.SequenceEqual(win.SelectedFileNamesForSmoke, StringComparer.OrdinalIgnoreCase);

                bool promptedConfirm = win.RequestBulkDeleteSelectedForSmoke() && win.DeleteConfirmationVisibleForSmoke;
                win.ConfirmDeleteForSmoke(doNotAskAgain: true);
                ViewerState? persistedAfterPartial = ReadPersistedState(statePath);
                bool partialStateCorrect = !File.Exists(bravoPath) && File.Exists(charliePath)
                    && File.Exists(Path.Combine(recycle, "bravo.png"))
                    && FavoriteFileContainsPath(favoritesPath, bravoPath) && FavoriteFileContainsPath(favoritesPath, charliePath)
                    && ReadSeenFlag(seenPath, bravoPath) && ReadSeenFlag(seenPath, charliePath)
                    && !win.PreviewTabNamesForSmoke.Contains("bravo.png", StringComparer.OrdinalIgnoreCase)
                    && win.PreviewTabNamesForSmoke.Contains("charlie.png", StringComparer.OrdinalIgnoreCase)
                    && persistedAfterPartial?.PreviewTabPaths?.Any(path => string.Equals(path, charliePath, StringComparison.OrdinalIgnoreCase)) == true
                    && persistedAfterPartial?.PreviewTabPaths?.All(path => !string.Equals(path, bravoPath, StringComparison.OrdinalIgnoreCase)) == true
                    && persistedAfterPartial?.ConfirmBeforeDelete == false;
                bool filteredNeighborSelected = win.FilteredFileNamesForSmoke().SequenceEqual(["charlie.png", "delta.png"], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(win.SelectedFileNameForSmoke, "delta.png", StringComparison.OrdinalIgnoreCase)
                    && win.SelectedFileNamesForSmoke.SequenceEqual(["charlie.png", "delta.png"], StringComparer.OrdinalIgnoreCase);
                bool partialStatus = win.DeleteStatusVisibleForSmoke
                    && win.DeleteStatusForSmoke.Contains("1 failed", StringComparison.OrdinalIgnoreCase)
                    && win.DeleteStatusForSmoke.Contains("remain selected", StringComparison.OrdinalIgnoreCase)
                    && !win.ConfirmBeforeDeleteForSmoke;

                bool modalOpened = win.OpenModalForSmoke();
                failCharlie = false;
                bool finalExecuted = win.RequestBulkDeleteSelectedForSmoke();
                bool emptyAfterFinal = finalExecuted
                    && win.FilteredCountForSmoke == 0
                    && win.SelectedCountForSmoke == 0
                    && !win.ModalVisibleForSmoke
                    && win.RightPreviewEmptyForSmoke
                    && win.PreviewTabCountForSmoke == 0;
                bool recycleOnly = !hardDeleteAttempted
                    && backendCalls == 4
                    && Directory.Exists(recycle)
                    && Directory.EnumerateFiles(recycle).Select(Path.GetFileName).OrderBy(static name => name).SequenceEqual(["bravo.png", "charlie.png", "delta.png"], StringComparer.OrdinalIgnoreCase)
                    && File.Exists(alphaPath) && !File.Exists(bravoPath) && !File.Exists(charliePath) && !File.Exists(deltaPath);

                bool ok = favoriteBravo && favoriteCharlie && favoriteDelta && tabBravo && tabCharlie && bulkSelected
                    && promptedCancel && accessibleBulkAction && cancelMutatedNothing
                    && promptedConfirm && partialStateCorrect && filteredNeighborSelected && partialStatus
                    && modalOpened && emptyAfterFinal && recycleOnly;
                result = new BulkRecycleSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "bulk Recycle Bin cancel, partial failure, filtered neighbor, persisted state cleanup, and final empty state passed"
                        : "bulk Recycle Bin workflow did not satisfy the guarded selection contract",
                    CancelMutatedNothing = cancelMutatedNothing,
                    PartialStateCorrect = partialStateCorrect,
                    FilteredNeighborSelected = filteredNeighborSelected,
                    PartialStatus = partialStatus,
                    EmptyAfterFinal = emptyAfterFinal,
                    RecycleOnly = recycleOnly,
                    BackendCalls = backendCalls,
                    Status = win.DeleteStatusForSmoke,
                };
            }
            catch (Exception ex)
            {
                result = new BulkRecycleSmokeResult { Message = ex.ToString() };
            }
            finally
            {
                if (win.IsVisible)
                    win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
            File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureDeleteCorrectnessSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-delete-correctness-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string protectedRoot = Path.Combine(smokeRoot, "protected-app-root");
        string recycle = Path.Combine(smokeRoot, "fake-recycle-bin");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string jobsPath = Path.Combine(smokeRoot, "enhancement-jobs.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(protectedRoot);
        string[] names = ["alpha.png", "bravo.png", "charlie.png", "delta.png", "echo.png", "escape.png"];
        for (int index = 0; index < names.Length; index++)
            WriteSmokePng(Path.Combine(folder, names[index]), 32, 24, Color.FromRgb((byte)(70 + index * 20), 125, 180));
        string protectedTarget = Path.Combine(protectedRoot, "owned.png");
        WriteSmokePng(protectedTarget, 32, 24, Color.FromRgb(210, 80, 95));
        Directory.CreateDirectory(Path.GetDirectoryName(jobsPath)!);
        File.WriteAllText(jobsPath, JsonSerializer.Serialize(new
        {
            version = 1,
            jobs = new[]
            {
                new { id = "retained-source-history", sourcePath = Path.Combine(folder, "alpha.png"), status = "succeeded", progress = 100 },
            },
        }, new JsonSerializerOptions { WriteIndented = true }));

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        MainWindow? reload = null;
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            object result;
            bool ok = false;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSortByForSmoke("name");
                foreach ((string name, int index) in names.Select((name, index) => (name, index)))
                {
                    if (!win.SelectFileNameForSmoke(name) || !win.SetSelectedFavoriteLevelForSmoke((index % 5) + 1))
                        throw new InvalidOperationException($"could not seed Favorite/Seen history for {name}");
                    win.MarkSelectedSeenForSmoke();
                }

                int backendCalls = 0;
                win.SetRecycleBinDeleteBackendForSmoke(path =>
                {
                    backendCalls++;
                    if (string.Equals(Path.GetFileName(path), "delta.png", StringComparison.OrdinalIgnoreCase))
                        return RecycleBinDeleteResult.Failed("synthetic partial failure");
                    Directory.CreateDirectory(recycle);
                    File.Move(path, Path.Combine(recycle, Path.GetFileName(path)));
                    return RecycleBinDeleteResult.Success;
                });

                string alphaPath = Path.Combine(folder, "alpha.png");
                string bravoPath = Path.Combine(folder, "bravo.png");
                string charliePath = Path.Combine(folder, "charlie.png");
                string deltaPath = Path.Combine(folder, "delta.png");
                string echoPath = Path.Combine(folder, "echo.png");
                string escapePath = Path.Combine(folder, "escape.png");

                win.SetConfirmBeforeDeleteForSmoke(false);
                win.SelectFileNameForSmoke("alpha.png");
                win.SetProtectedDeleteRootsForSmoke(folder);
                bool projectAppRootBlocked = !win.RequestDeleteSelectedForSmoke()
                    && backendCalls == 0 && File.Exists(alphaPath) && win.CatalogCountForSmoke == names.Length;

                win.SetProtectedDeleteRootsForSmoke(protectedRoot);
                win.SetCanonicalPathResolverForSmoke(path =>
                    string.Equals(Path.GetFullPath(path), Path.GetFullPath(escapePath), StringComparison.OrdinalIgnoreCase)
                        ? Path.GetFullPath(protectedTarget)
                        : Path.GetFullPath(path));
                win.SelectFileNameForSmoke("escape.png");
                bool canonicalProtectedEscapeBlocked = !win.RequestDeleteSelectedForSmoke()
                    && backendCalls == 0 && File.Exists(escapePath) && File.Exists(protectedTarget);
                win.ResetCanonicalPathResolverForSmoke();
                win.ResetProtectedDeleteRootsForSmoke();

                win.SelectFileNameForSmoke("alpha.png");
                win.SetConfirmBeforeDeleteForSmoke(true);
                bool cancelPrompted = win.RequestDeleteSelectedForSmoke() && win.DeleteConfirmationVisibleForSmoke;
                win.CancelDeleteForSmoke();
                bool cancelNonMutation = cancelPrompted && backendCalls == 0 && File.Exists(alphaPath)
                    && win.CatalogCountForSmoke == names.Length && string.Equals(win.SelectedFileNameForSmoke, "alpha.png", StringComparison.OrdinalIgnoreCase);
                win.SetConfirmBeforeDeleteForSmoke(false);

                string favoritesBefore = FileFingerprint(favoritesPath);
                string seenBefore = FileFingerprint(seenPath);
                string recentBefore = FileFingerprint(recentPath);
                string jobsBefore = FileFingerprint(jobsPath);

                bool singleTabOpened = win.OpenSelectedPreviewTabForSmoke();
                bool singlePinned = win.TogglePreviewTabPinForSmoke("alpha.png") && win.IsPreviewTabPinnedForSmoke("alpha.png");
                bool singleModalOpened = win.OpenModalForSmoke();
                bool singleDeleted = win.RequestDeleteSelectedForSmoke();
                ViewerState? stateAfterSingle = ReadPersistedState(statePath);
                bool singleUiReconciled = singleTabOpened && singlePinned && singleModalOpened && singleDeleted
                    && !File.Exists(alphaPath) && File.Exists(Path.Combine(recycle, "alpha.png"))
                    && string.Equals(win.SelectedFileNameForSmoke, "bravo.png", StringComparison.OrdinalIgnoreCase)
                    && win.ModalVisibleForSmoke && string.Equals(win.ModalDisplayPathForSmoke, bravoPath, StringComparison.OrdinalIgnoreCase)
                    && !win.PreviewTabNamesForSmoke.Contains("alpha.png", StringComparer.OrdinalIgnoreCase)
                    && !win.IsPreviewTabPinnedForSmoke("alpha.png") && win.PinnedPreviewCountForSmoke == 0
                    && win.ClosedPreviewTabCountForSmoke == 0
                    && stateAfterSingle?.PreviewTabPaths?.All(path => !string.Equals(path, alphaPath, StringComparison.OrdinalIgnoreCase)) != false
                    && stateAfterSingle?.PinnedPreviewPaths?.All(path => !string.Equals(path, alphaPath, StringComparison.OrdinalIgnoreCase)) != false
                    && !string.Equals(stateAfterSingle?.ActivePreviewTabPath, alphaPath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(stateAfterSingle?.SelectedPath, alphaPath, StringComparison.OrdinalIgnoreCase);

                win.CloseModalForSmoke();
                bool charliePrepared = win.SelectFileNameForSmoke("charlie.png")
                    && win.OpenSelectedPreviewTabForSmoke()
                    && win.TogglePreviewTabPinForSmoke("charlie.png")
                    && win.ClosePreviewTabForSmoke("charlie.png")
                    && win.ClosedPreviewTabCountForSmoke == 1
                    && win.PinnedPreviewCountForSmoke == 1;
                bool deltaTabOpened = win.SelectFileNameForSmoke("delta.png") && win.OpenSelectedPreviewTabForSmoke();
                List<string> bulkOrder = win.FilteredFileNamesForSmoke();
                int charlieIndex = bulkOrder.FindIndex(name => string.Equals(name, "charlie.png", StringComparison.OrdinalIgnoreCase));
                int deltaIndex = bulkOrder.FindIndex(name => string.Equals(name, "delta.png", StringComparison.OrdinalIgnoreCase));
                bool bulkSelected = charlieIndex >= 0 && deltaIndex == charlieIndex + 1 && win.SelectRangeForSmoke(charlieIndex, deltaIndex);
                bool bulkModalOpened = win.OpenModalForSmoke();
                bool bulkExecuted = win.RequestBulkDeleteSelectedForSmoke();
                ViewerState? stateAfterBulk = ReadPersistedState(statePath);
                bool partialFailureCorrect = charliePrepared && deltaTabOpened && bulkSelected && bulkModalOpened && bulkExecuted
                    && !File.Exists(charliePath) && File.Exists(Path.Combine(recycle, "charlie.png"))
                    && File.Exists(deltaPath) && File.Exists(echoPath)
                    && win.DeleteStatusForSmoke.Contains("1 failed", StringComparison.OrdinalIgnoreCase)
                    && win.SelectedFileNamesForSmoke.Contains("delta.png", StringComparer.OrdinalIgnoreCase)
                    && !win.PreviewTabNamesForSmoke.Contains("charlie.png", StringComparer.OrdinalIgnoreCase)
                    && win.PreviewTabNamesForSmoke.Contains("delta.png", StringComparer.OrdinalIgnoreCase)
                    && win.ClosedPreviewTabCountForSmoke == 0 && win.PinnedPreviewCountForSmoke == 0
                    && stateAfterBulk?.PreviewTabPaths?.All(path => !string.Equals(path, charliePath, StringComparison.OrdinalIgnoreCase)) != false
                    && stateAfterBulk?.PinnedPreviewPaths?.All(path => !string.Equals(path, charliePath, StringComparison.OrdinalIgnoreCase)) != false
                    && !string.Equals(stateAfterBulk?.ActivePreviewTabPath, charliePath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(stateAfterBulk?.SelectedPath, charliePath, StringComparison.OrdinalIgnoreCase);

                bool retainedHistory = FavoriteFileContainsPath(favoritesPath, alphaPath)
                    && FavoriteFileContainsPath(favoritesPath, charliePath)
                    && ReadSeenFlag(seenPath, alphaPath) && ReadSeenFlag(seenPath, charliePath)
                    && string.Equals(favoritesBefore, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(seenBefore, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBefore, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal);

                win.Close();
                reload = HiddenWindow();
                reload.Show();
                await reload.RefreshActiveFolderForSmokeAsync();
                ViewerState? stateAfterReload = ReadPersistedState(statePath);
                bool deadUiAbsentAfterReload = reload.CatalogCountForSmoke == 4
                    && !reload.AllFileNamesForSmoke.Contains("alpha.png", StringComparer.OrdinalIgnoreCase)
                    && !reload.AllFileNamesForSmoke.Contains("charlie.png", StringComparer.OrdinalIgnoreCase)
                    && !reload.PreviewTabNamesForSmoke.Contains("alpha.png", StringComparer.OrdinalIgnoreCase)
                    && !reload.PreviewTabNamesForSmoke.Contains("charlie.png", StringComparer.OrdinalIgnoreCase)
                    && reload.ClosedPreviewTabCountForSmoke == 0 && reload.PinnedPreviewCountForSmoke == 0
                    && stateAfterReload?.PreviewTabPaths?.All(path => !string.Equals(path, alphaPath, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(path, charliePath, StringComparison.OrdinalIgnoreCase)) != false
                    && stateAfterReload?.PinnedPreviewPaths?.All(path => !string.Equals(path, alphaPath, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(path, charliePath, StringComparison.OrdinalIgnoreCase)) != false
                    && !string.Equals(stateAfterReload?.ActivePreviewTabPath, alphaPath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(stateAfterReload?.ActivePreviewTabPath, charliePath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(stateAfterReload?.SelectedPath, alphaPath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(stateAfterReload?.SelectedPath, charliePath, StringComparison.OrdinalIgnoreCase);

                bool storesStillByteIdentical = string.Equals(favoritesBefore, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(seenBefore, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBefore, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal);
                bool recycleOnly = backendCalls == 3
                    && Directory.Exists(recycle)
                    && Directory.EnumerateFiles(recycle).Select(Path.GetFileName).OrderBy(static name => name)
                        .SequenceEqual(["alpha.png", "charlie.png"], StringComparer.OrdinalIgnoreCase)
                    && File.Exists(bravoPath) && File.Exists(deltaPath) && File.Exists(echoPath) && File.Exists(escapePath)
                    && File.Exists(protectedTarget);

                ok = projectAppRootBlocked && canonicalProtectedEscapeBlocked && cancelNonMutation
                    && singleUiReconciled && partialFailureCorrect && retainedHistory
                    && deadUiAbsentAfterReload && storesStillByteIdentical && recycleOnly;
                result = new
                {
                    ok,
                    message = ok
                        ? "protected-root guards and shared single/bulk Recycle reconciliation passed with retained history"
                        : "WPF Delete correctness contract failed",
                    projectAppRootBlocked,
                    canonicalProtectedEscapeBlocked,
                    cancelNonMutation,
                    singleUiReconciled,
                    partialFailureCorrect,
                    retainedHistory,
                    deadUiAbsentAfterReload,
                    storesStillByteIdentical,
                    recycleOnly,
                    backendCalls,
                };
            }
            catch (Exception ex)
            {
                result = new { ok = false, message = ex.ToString() };
            }
            finally
            {
                if (reload?.IsVisible == true)
                    reload.Close();
                if (win.IsVisible)
                    win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
            File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureP1BSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-p1b-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");

        Directory.CreateDirectory(folder);
        WriteSmokePng(Path.Combine(folder, "good-one.png"), 32, 24, Color.FromRgb(60, 140, 210));
        WriteSmokePng(Path.Combine(folder, "good-two.png"), 32, 24, Color.FromRgb(100, 190, 120));
        File.WriteAllText(Path.Combine(folder, "broken.png"), "not an image");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            P1BSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                bool decoderStatus = win.DeleteStatusVisibleForSmoke
                    && win.DeleteStatusForSmoke.Contains("could not be decoded", StringComparison.OrdinalIgnoreCase);
                win.ReportScanAccessFailureForSmoke();
                bool scanStatus = win.DeleteStatusForSmoke.Contains("access was denied", StringComparison.OrdinalIgnoreCase);

                bool selected = win.SelectFileNameForSmoke("good-one.png");
                bool searchHintVisible = win.SearchWatermarkVisibleForSmoke && win.SearchAutomationHelpTextForSmoke;
                win.SetSearchQuery("good", persist: false);
                bool searchHintHiddenWhenTyped = !win.SearchWatermarkVisibleForSmoke;
                win.SetSearchQuery("", persist: false);
                bool searchHintRestored = win.SearchWatermarkVisibleForSmoke;
                bool datePickerNamed = win.DatePickerAutomationNamesForSmoke;
                bool searchFocused = win.FocusSearchInputForSmoke();
                bool searchShortcutSuppressed = win.IsGlobalShortcutInputFocusedForSmoke && !win.InvokePreviewKeyForSmoke(Key.F);
                bool dateFocused = win.FocusDateFromInputForSmoke();
                bool dateShortcutSuppressed = win.IsGlobalShortcutInputFocusedForSmoke && !win.InvokePreviewKeyForSmoke(Key.F);
                bool settingsButtonFocused = win.FocusAppSettingsButtonForSmoke();
                bool buttonShortcutSuppressed = win.IsGlobalShortcutInputFocusedForSmoke && !win.InvokePreviewKeyForSmoke(Key.F);
                bool comboBoxShortcutSuppressed = win.IsGlobalShortcutSuppressedForSmoke(new System.Windows.Controls.ComboBox());
                bool cardsFocused = win.FocusCardsListForSmoke();
                bool globalShortcutHandled = win.InvokePreviewKeyForSmoke(Key.F) && win.SelectedFavoriteLevelForSmoke == 1;
                win.InvokePreviewKeyForSmoke(Key.U);

                win.OpenAppSettingsForSmoke();
                await win.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool settingsFocus = win.AppSettingsVisibleForSmoke && win.IsSettingsDialogFocusedForSmoke && win.SettingsFocusTrapConfiguredForSmoke;
                bool settingsEscCloses = win.InvokePreviewKeyForSmoke(Key.Escape);
                await win.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                bool settingsFocusRestored = settingsEscCloses && !win.AppSettingsVisibleForSmoke && (win.IsAppSettingsButtonFocusedForSmoke || win.IsCardsListFocusedForSmoke);

                win.FocusCardsListForSmoke();
                win.SetConfirmBeforeDeleteForSmoke(true);
                bool deleteRequested = win.RequestDeleteSelectedForSmoke();
                await win.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool deleteFocus = deleteRequested && win.DeleteConfirmationVisibleForSmoke && win.IsDeleteDialogFocusedForSmoke && win.DeleteFocusTrapConfiguredForSmoke;
                win.CancelDeleteForSmoke();
                await win.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                bool deleteFocusRestored = win.IsCardsListFocusedForSmoke;

                File.WriteAllText(statePath + ".lock", "{\"pid\":1}");
                win.FlushStateForSmoke();
                bool lockBusyStatus = win.DeleteStatusForSmoke.Contains("busy", StringComparison.OrdinalIgnoreCase) && win.DeleteStatusRetryVisibleForSmoke;
                File.Delete(statePath + ".lock");
                win.RetryStatusForSmoke();
                bool lockRetryCleared = !win.DeleteStatusRetryVisibleForSmoke;

                File.WriteAllText(favoritesPath, "{\"broken\":{}}");
                bool favoriteRefused = !win.SetSelectedFavoriteLevelForSmoke(2)
                    && win.DeleteStatusForSmoke.Contains("Favorites", StringComparison.OrdinalIgnoreCase)
                    && win.DeleteStatusForSmoke.Contains("invalid", StringComparison.OrdinalIgnoreCase);

                File.WriteAllText(seenPath, "{\"broken\":[]}");
                bool seenRefused = win.SelectFileNameForSmoke("good-two.png")
                    && win.DeleteStatusForSmoke.Contains("Seen state", StringComparison.OrdinalIgnoreCase)
                    && win.DeleteStatusForSmoke.Contains("invalid", StringComparison.OrdinalIgnoreCase);

                File.WriteAllText(statePath, "{\"Version\":1,\"CardWidth\":{}}");
                win.FlushStateForSmoke();
                bool stateRefused = win.DeleteStatusForSmoke.Contains("Viewer settings", StringComparison.OrdinalIgnoreCase)
                    && win.DeleteStatusForSmoke.Contains("invalid", StringComparison.OrdinalIgnoreCase);
                bool logoAccessible = win.LogoHasAutomationNameForSmoke && win.DialogsHaveAutomationNamesForSmoke;
                bool logoActivated = win.ActivateLogoForSmoke();
                win.Close();

                File.WriteAllText(statePath, "{\"Version\":1}");
                File.WriteAllText(recentPath, "{\"version\":2}");
                var recentWindow = HiddenWindow();
                recentWindow.Show();
                await recentWindow.LoadFolderAsync(folder);
                recentWindow.FlushStateForSmoke();
                bool recentRefused = recentWindow.DeleteStatusForSmoke.Contains("Recent folder history", StringComparison.OrdinalIgnoreCase)
                    && recentWindow.DeleteStatusForSmoke.Contains("invalid", StringComparison.OrdinalIgnoreCase);
                recentWindow.Close();

                bool ok = decoderStatus && scanStatus && selected
                    && searchHintVisible && searchHintHiddenWhenTyped && searchHintRestored && datePickerNamed
                    && searchFocused && searchShortcutSuppressed
                    && dateFocused && dateShortcutSuppressed
                    && settingsButtonFocused && buttonShortcutSuppressed && comboBoxShortcutSuppressed
                    && cardsFocused && globalShortcutHandled
                    && settingsFocus && settingsFocusRestored
                    && deleteFocus && deleteFocusRestored
                    && lockBusyStatus && lockRetryCleared
                    && favoriteRefused && seenRefused && stateRefused && recentRefused
                    && logoAccessible && logoActivated;
                result = new P1BSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "non-blocking recoverable status, persistence refusal, scan/decode warning, accessibility naming, dialog focus, and shortcut guards passed"
                        : "P1B error-surface or accessibility expectations did not match",
                    DecoderStatus = decoderStatus,
                    ScanStatus = scanStatus,
                    Selected = selected,
                    SearchHintVisible = searchHintVisible,
                    SearchHintHiddenWhenTyped = searchHintHiddenWhenTyped,
                    SearchHintRestored = searchHintRestored,
                    DatePickerNamed = datePickerNamed,
                    SearchFocused = searchFocused,
                    DateFocused = dateFocused,
                    SettingsButtonFocused = settingsButtonFocused,
                    CardsFocused = cardsFocused,
                    LockBusyStatus = lockBusyStatus,
                    LockRetryCleared = lockRetryCleared,
                    FavoritesRefused = favoriteRefused,
                    SeenRefused = seenRefused,
                    StateRefused = stateRefused,
                    RecentRefused = recentRefused,
                    SearchShortcutSuppressed = searchShortcutSuppressed,
                    DateShortcutSuppressed = dateShortcutSuppressed,
                    ButtonShortcutSuppressed = buttonShortcutSuppressed,
                    ComboBoxShortcutSuppressed = comboBoxShortcutSuppressed,
                    GlobalShortcutHandled = globalShortcutHandled,
                    SettingsFocus = settingsFocus,
                    SettingsFocusRestored = settingsFocusRestored,
                    DeleteFocus = deleteFocus,
                    DeleteFocusRestored = deleteFocusRestored,
                    LogoAccessible = logoAccessible,
                    LogoActivated = logoActivated,
                };
            }
            catch (Exception ex)
            {
                result = new P1BSmokeResult { Message = ex.ToString() };
            }
            finally
            {
                if (win.IsVisible)
                    win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
            File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureRightPanelSmoke(string resultPath)
    {
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
            RightPanelSmokeResult result;
            try
            {
                await first.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                bool defaultOpen = first.RightPanelOpenForSmoke;
                double defaultWidth = first.RightPanelWidthForSmoke;
                bool resized = first.SetRightPanelWidthForSmoke(520);
                await first.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                double resizedWidth = first.RightPanelWidthForSmoke;
                first.FlushStateForSmoke();
                first.Close();

                var second = HiddenWindow();
                second.Show();
                await second.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                bool restoredOpen = second.RightPanelOpenForSmoke;
                double restoredWidth = second.RightPanelWidthForSmoke;
                second.ToggleRightPanelForSmoke();
                bool closed = !second.RightPanelOpenForSmoke;
                double storedWhileClosed = second.RightPanelStoredWidthForSmoke;
                second.FlushStateForSmoke();
                second.Close();

                var third = HiddenWindow();
                third.Show();
                await third.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                bool restoredClosed = !third.RightPanelOpenForSmoke;
                double restoredStoredWidth = third.RightPanelStoredWidthForSmoke;
                third.ToggleRightPanelForSmoke();
                await third.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                bool reopened = third.RightPanelOpenForSmoke;
                double reopenedWidth = third.RightPanelWidthForSmoke;
                bool minClamped = third.SetRightPanelWidthForSmoke(100);
                await third.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                double minWidth = third.RightPanelWidthForSmoke;
                bool maxClamped = third.SetRightPanelWidthForSmoke(1200);
                await third.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                double maxWidth = third.RightPanelWidthForSmoke;
                third.Close();

                bool ok = defaultOpen
                    && Nearly(defaultWidth, 340)
                    && resized
                    && Nearly(resizedWidth, 520)
                    && restoredOpen
                    && Nearly(restoredWidth, 520)
                    && closed
                    && Nearly(storedWhileClosed, 520)
                    && restoredClosed
                    && Nearly(restoredStoredWidth, 520)
                    && reopened
                    && Nearly(reopenedWidth, 520)
                    && minClamped
                    && Nearly(minWidth, 240)
                    && maxClamped
                    && Nearly(maxWidth, 900);
                result = new RightPanelSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "right preview resize, clamp, hide/show, and reload persistence passed"
                        : "right preview panel state did not match resize/persistence expectations",
                    DefaultOpen = defaultOpen,
                    DefaultWidth = defaultWidth,
                    ResizedWidth = resizedWidth,
                    RestoredOpen = restoredOpen,
                    RestoredWidth = restoredWidth,
                    Closed = closed,
                    StoredWhileClosed = storedWhileClosed,
                    RestoredClosed = restoredClosed,
                    RestoredStoredWidth = restoredStoredWidth,
                    Reopened = reopened,
                    ReopenedWidth = reopenedWidth,
                    MinWidth = minWidth,
                    MaxWidth = maxWidth,
                };
            }
            catch (Exception ex)
            {
                result = new RightPanelSmokeResult { Message = ex.Message };
            }
            finally
            {
                if (first.IsVisible) first.Close();
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resultFullPath)!);
            File.WriteAllText(resultFullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private static bool Nearly(double left, double right)
        => Math.Abs(left - right) < 1;

    private void CaptureFormatSmoke(string resultPath, string[] args)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string fixtureFolder = Path.GetFullPath(ArgValue(args, "--fixture-folder") ?? "");
        string resultDir = Path.GetDirectoryName(resultFullPath) ?? Path.GetTempPath();
        string smokeRoot = Path.Combine(resultDir, Path.GetFileNameWithoutExtension(resultFullPath) + "-state-" + Guid.NewGuid().ToString("N"));
        string previousCurrentDirectory = Environment.CurrentDirectory;
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (!Directory.Exists(fixtureFolder))
        {
            WriteFormatSmokeResult(resultFullPath, new FormatSmokeResult { Message = "format fixture folder is unavailable" });
            Shutdown(1);
            return;
        }

        PrepareSharedSeenSmokeEnvironment(smokeRoot);
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            FormatSmokeResult result;
            try
            {
                string[] expected =
                [
                    "format-avif.avif",
                    "format-bmp.bmp",
                    "format-gif.gif",
                    "format-jpeg.jpg",
                    "format-png.png",
                    "format-tiff.tiff",
                    "format-webp.webp",
                ];
                await win.LoadFolderAsync(fixtureFolder);
                var previews = new List<PreviewDecodeSmokeSnapshot>();
                foreach (string fileName in expected)
                    previews.Add(await win.SelectPreviewForSmokeAsync(fileName));

                List<string> catalogNames = win.AllFileNamesForSmoke;
                List<string> extensions = PhotoViewer.Wpf.MainWindow.SupportedImageExtensionsForSmoke;
                bool expectedCatalog = expected.All(name => catalogNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    && !catalogNames.Contains("unsupported.txt", StringComparer.OrdinalIgnoreCase)
                    && catalogNames.Count == expected.Length;
                bool decoded = previews.Count == expected.Length
                    && previews.All(static preview => preview.Selected && preview.DeferredDecodeApplied && preview.PreviewSourcePresent && preview.StableLatestSelection);
                bool extensionContract = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".avif" }
                    .All(extension => extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
                bool nativeExtensions = new[] { ".bmp", ".tif", ".tiff" }
                    .All(extension => extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
                bool ok = expectedCatalog && decoded && extensionContract && nativeExtensions;
                result = new FormatSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "PNG/JPEG/WebP/GIF/AVIF plus native BMP/TIFF catalog and preview decode passed"
                        : "one or more advertised image formats did not catalog or decode",
                    CatalogNames = catalogNames,
                    Extensions = extensions,
                    Previews = previews,
                    BrowserContractFormats = extensionContract,
                    NativeExtensionFormats = nativeExtensions,
                };
            }
            catch (Exception ex)
            {
                result = new FormatSmokeResult { Message = ex.Message };
            }
            finally
            {
                win.Close();
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteFormatSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
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

    private void CaptureModalEnhancedSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteModalEnhancedSmokeResult(resultPath, new ModalEnhancedSmokeResult { Ok = false, Message = "missing required --folder", Folder = folder });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string[] fixtureNames = GetSmokeImageFileNames(fullFolder);
        if (fixtureNames.Length < 3)
        {
            WriteModalEnhancedSmokeResult(resultPath, new ModalEnhancedSmokeResult { Ok = false, Message = "modal enhanced smoke requires at least 3 fixture images", Folder = fullFolder });
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
            ModalEnhancedSmokeResult result;
            try
            {
                await win.LoadFolderAsync(fullFolder);
                bool selectedValid = win.SelectFileNameForSmoke(Path.GetFileName(validSource));
                win.ShowModalForShot();
                bool validToggleAvailable = win.ModalEnhancedToggleAvailableForSmoke;
                string? originalDisplayPath = win.ModalDisplayPathForSmoke;
                bool toggledEnhanced = win.ToggleModalEnhancedForSmoke();
                bool showingEnhanced = win.ModalShowingEnhancedForSmoke;
                string? enhancedDisplayPath = win.ModalDisplayPathForSmoke;
                bool toggledOriginal = win.ToggleModalEnhancedForSmoke();
                bool showingOriginal = !win.ModalShowingEnhancedForSmoke;
                string? restoredOriginalPath = win.ModalDisplayPathForSmoke;

                bool selectedStale = win.SelectFileNameForSmoke(Path.GetFileName(staleSource));
                win.ShowModalForShot();
                bool staleToggleAvailable = win.ModalEnhancedToggleAvailableForSmoke;
                bool staleToggleAttempt = win.ToggleModalEnhancedForSmoke();
                string? staleDisplayPath = win.ModalDisplayPathForSmoke;

                bool selectedAgain = win.SelectFileNameForSmoke(Path.GetFileName(validSource));
                win.ShowModalForShot();
                bool toggledBeforeNavigation = win.ToggleModalEnhancedForSmoke();
                string? validPathBeforeNavigation = win.ModalDisplayPathForSmoke;
                bool movedNext = win.NavigateModalForSmoke(1);
                if (!movedNext)
                    movedNext = win.NavigateModalForSmoke(-1);
                string? nextPath = win.SelectedPathForSmoke;
                bool navigationResetToOriginal = !win.ModalShowingEnhancedForSmoke
                    && string.Equals(win.ModalDisplayPathForSmoke, nextPath, StringComparison.OrdinalIgnoreCase);
                win.Close();

                string afterJobsJson = File.ReadAllText(jobsPath);
                bool enhancementStateUnchanged = string.Equals(beforeJobsJson, afterJobsJson, StringComparison.Ordinal);
                bool ok = win.EnhancementReadOkForSmoke
                    && selectedValid
                    && validToggleAvailable
                    && string.Equals(originalDisplayPath, validSource, StringComparison.OrdinalIgnoreCase)
                    && toggledEnhanced
                    && showingEnhanced
                    && string.Equals(enhancedDisplayPath, validOutput, StringComparison.OrdinalIgnoreCase)
                    && toggledOriginal
                    && showingOriginal
                    && string.Equals(restoredOriginalPath, validSource, StringComparison.OrdinalIgnoreCase)
                    && selectedStale
                    && !staleToggleAvailable
                    && !staleToggleAttempt
                    && string.Equals(staleDisplayPath, staleSource, StringComparison.OrdinalIgnoreCase)
                    && selectedAgain
                    && toggledBeforeNavigation
                    && string.Equals(validPathBeforeNavigation, validOutput, StringComparison.OrdinalIgnoreCase)
                    && movedNext
                    && !string.Equals(nextPath, validSource, StringComparison.OrdinalIgnoreCase)
                    && navigationResetToOriginal
                    && enhancementStateUnchanged;

                result = new ModalEnhancedSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "read-only modal original/enhanced toggle, stale fallback, navigation reset, and jobs-state isolation passed"
                        : "modal enhanced smoke did not meet expected read-only behavior",
                    Folder = fullFolder,
                    ProjectRoot = smokeRoot,
                    JobsPath = jobsPath,
                    ValidSourcePath = validSource,
                    ValidOutputPath = validOutput,
                    StaleSourcePath = staleSource,
                    OriginalDisplayPath = originalDisplayPath,
                    EnhancedDisplayPath = enhancedDisplayPath,
                    RestoredOriginalPath = restoredOriginalPath,
                    StaleDisplayPath = staleDisplayPath,
                    NextPath = nextPath,
                    ValidToggleAvailable = validToggleAvailable,
                    ToggledEnhanced = toggledEnhanced,
                    ToggledOriginal = toggledOriginal,
                    StaleToggleAvailable = staleToggleAvailable,
                    StaleToggleAttempt = staleToggleAttempt,
                    ToggledBeforeNavigation = toggledBeforeNavigation,
                    MovedNext = movedNext,
                    NavigationResetToOriginal = navigationResetToOriginal,
                    EnhancementStateUnchanged = enhancementStateUnchanged,
                    ReadOk = win.EnhancementReadOkForSmoke,
                    ReadError = win.EnhancementReadErrorForSmoke,
                };
            }
            catch (Exception ex)
            {
                result = new ModalEnhancedSmokeResult { Ok = false, Message = ex.Message, Folder = fullFolder, ProjectRoot = smokeRoot, JobsPath = jobsPath };
            }
            finally
            {
                win.Close();
                Environment.CurrentDirectory = previousCurrentDirectory;
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
            }

            WriteModalEnhancedSmokeResult(resultFullPath, result);
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

                int filteredCount = win.FilteredCountForSmoke;
                bool selectedLast = filteredCount > 1 && win.SelectIndexForSmoke(filteredCount - 1);
                string? lastName = win.SelectedFileNameForSmoke;
                bool wrappedLastToFirst = selectedLast && win.NavigateModalForSmoke(1);
                string? wrappedFirstName = win.SelectedFileNameForSmoke;
                bool wrappedFirstToLast = wrappedLastToFirst && win.NavigateModalForSmoke(-1);
                string? wrappedLastName = win.SelectedFileNameForSmoke;

                bool ok = selected
                    && win.ModalVisibleForSmoke
                    && movedNext
                    && movedPrevious
                    && !string.IsNullOrWhiteSpace(startPath)
                    && !string.Equals(startPath, nextPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(startPath, previousPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(previousPath, persisted?.SelectedPath, StringComparison.OrdinalIgnoreCase)
                    && selectedLast
                    && wrappedLastToFirst
                    && wrappedFirstToLast
                    && !string.Equals(lastName, wrappedFirstName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(lastName, wrappedLastName, StringComparison.OrdinalIgnoreCase);

                result = new ModalNavigationSmokeResult(
                    ok,
                    ok ? "modal navigation moved next/previous, wrapped both ends, and persisted selected path" : "modal navigation did not keep selection/state or wrap behavior in sync",
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
                    persisted?.SelectedPath,
                    filteredCount,
                    lastName,
                    wrappedLastToFirst,
                    wrappedFirstName,
                    wrappedFirstToLast,
                    wrappedLastName);
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

    private void CaptureModalInteractionSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-modal-interaction-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string jobsPath = Path.Combine(smokeRoot, "jobs.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");
        Directory.CreateDirectory(folder);
        const string firstName = "a-modal.png";
        const string secondName = "b-modal.png";
        const string thirdName = "c-modal.png";
        WriteSmokePng(Path.Combine(folder, firstName), 48, 36, Color.FromRgb(80, 130, 210));
        WriteSmokePng(Path.Combine(folder, secondName), 48, 36, Color.FromRgb(230, 100, 130));
        WriteSmokePng(Path.Combine(folder, thirdName), 48, 36, Color.FromRgb(100, 200, 130));
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            ModalInteractionSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSortByForSmoke("name");
                bool selected = win.SelectFileNameForSmoke(secondName);
                bool opened = win.OpenModalForSmoke();
                bool accessibility = win.ModalEdgeZonesAccessibleForSmoke;

                win.ScheduleModalChromeToggleForSmoke();
                await Task.Delay(230);
                bool chromeHidden = !win.ModalChromeVisibleForSmoke
                    && win.ModalInteractionFeedbackVisibleForSmoke
                    && win.ModalInteractionFeedbackForSmoke.Contains("hidden", StringComparison.OrdinalIgnoreCase);
                win.ScheduleModalChromeToggleForSmoke();
                await Task.Delay(230);
                bool chromeShown = win.ModalChromeVisibleForSmoke
                    && win.ModalInteractionFeedbackForSmoke.Contains("shown", StringComparison.OrdinalIgnoreCase);

                bool controlDidNotToggle = win.ToggleModalMetadataForSmoke();
                bool doubleClickMetadata = win.ToggleModalMetadataFromImageDoubleClickForSmoke();

                string? beforeEdge = win.SelectedFileNameForSmoke;
                bool edgeNext = win.ModalEdgeNavigateForSmoke(1);
                string? afterEdge = win.SelectedFileNameForSmoke;
                bool edgeFeedback = win.ModalInteractionFeedbackVisibleForSmoke
                    && win.ModalInteractionFeedbackForSmoke.Contains("Next", StringComparison.OrdinalIgnoreCase);

                string? beforeSwipe = win.SelectedFileNameForSmoke;
                bool swipeNext = win.ModalSwipeForSmoke(-200);
                string? afterSwipe = win.SelectedFileNameForSmoke;
                bool smallSwipeIgnored = !win.ModalSwipeForSmoke(20)
                    && string.Equals(win.SelectedFileNameForSmoke, afterSwipe, StringComparison.OrdinalIgnoreCase);

                bool zoomed = win.ModalZoomShortcutForSmoke("plus");
                bool zoomFeedback = win.ModalInteractionFeedbackVisibleForSmoke
                    && win.ModalInteractionFeedbackForSmoke.Contains("Zoom", StringComparison.OrdinalIgnoreCase);
                string? beforeBlockedSwipe = win.SelectedFileNameForSmoke;
                bool zoomedSwipeBlocked = !win.ModalSwipeForSmoke(-200)
                    && string.Equals(win.SelectedFileNameForSmoke, beforeBlockedSwipe, StringComparison.OrdinalIgnoreCase);
                bool reset = win.ResetModalTransformForSmoke();
                bool closed = win.CloseTopmostOverlayForSmoke();

                bool ok = selected && opened && accessibility
                    && chromeHidden && chromeShown && controlDidNotToggle && doubleClickMetadata
                    && edgeNext && !string.Equals(beforeEdge, afterEdge, StringComparison.OrdinalIgnoreCase) && edgeFeedback
                    && swipeNext && !string.Equals(beforeSwipe, afterSwipe, StringComparison.OrdinalIgnoreCase) && smallSwipeIgnored
                    && zoomed && zoomFeedback && zoomedSwipeBlocked && reset && closed && !win.ModalVisibleForSmoke;
                result = new ModalInteractionSmokeResult
                {
                    Ok = ok,
                    Message = ok ? "modal chrome, edge navigation, swipe threshold, feedback, metadata double-click, and zoom/pan isolation passed" : "modal interaction parity did not meet the expected contract",
                    Accessibility = accessibility,
                    ChromeHidden = chromeHidden,
                    ChromeShown = chromeShown,
                    ControlDidNotToggle = controlDidNotToggle,
                    DoubleClickMetadata = doubleClickMetadata,
                    EdgeNext = edgeNext && !string.Equals(beforeEdge, afterEdge, StringComparison.OrdinalIgnoreCase),
                    SwipeNext = swipeNext && !string.Equals(beforeSwipe, afterSwipe, StringComparison.OrdinalIgnoreCase),
                    SmallSwipeIgnored = smallSwipeIgnored,
                    ZoomedSwipeBlocked = zoomedSwipeBlocked,
                    Feedback = edgeFeedback && zoomFeedback,
                    EscapeClosed = closed && !win.ModalVisibleForSmoke,
                };
            }
            catch (Exception ex)
            {
                win.Close();
                result = new ModalInteractionSmokeResult { Message = ex.Message };
            }
            finally
            {
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WriteModalInteractionSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureModalTransformSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        int selectIndex = ArgInt(args, "--select-index", 0);
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteModalTransformSmokeResult(resultPath, new ModalTransformSmokeResult(false, "missing required --folder", folder, selectIndex));
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            ModalTransformSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                bool selected = win.SelectIndexForSmoke(selectIndex);
                win.ShowModalForShot();
                var initial = win.ModalTransformForSmoke();
                bool flipped = win.ToggleModalFlipForSmoke();
                var afterFlip = win.ModalTransformForSmoke();
                bool shortcutZoomed = win.ModalZoomShortcutForSmoke("plus");
                var afterShortcut = win.ModalTransformForSmoke();
                bool wheelZoomed = win.ModalZoomWheelForSmoke(120);
                var afterWheel = win.ModalTransformForSmoke();
                bool reset = win.ModalZoomShortcutForSmoke("0");
                var afterReset = win.ModalTransformForSmoke();
                string? startPath = win.SelectedPathForSmoke;
                bool movedNext = win.NavigateModalForSmoke(1);
                string? nextPath = win.SelectedPathForSmoke;
                var afterNavigation = win.ModalTransformForSmoke();
                bool modalVisibleBeforeClose = win.ModalVisibleForSmoke;
                bool closed = win.CloseTopmostOverlayForSmoke();
                var afterClose = win.ModalTransformForSmoke();

                bool ok = selected
                    && modalVisibleBeforeClose
                    && Math.Abs(initial.Zoom - 1) < 0.0001
                    && initial.ZoomLabel != "100%"
                    && !initial.Flipped
                    && flipped
                    && afterFlip.Flipped
                    && afterFlip.ScaleX < 0
                    && shortcutZoomed
                    && afterShortcut.Zoom > 1
                    && wheelZoomed
                    && afterWheel.Zoom > afterShortcut.Zoom
                    && reset
                    && Math.Abs(afterReset.Zoom - 1) < 0.0001
                    && !afterReset.Flipped
                    && afterReset.ZoomLabel == initial.ZoomLabel
                    && movedNext
                    && !string.Equals(startPath, nextPath, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(afterNavigation.Zoom - 1) < 0.0001
                    && afterNavigation.ZoomLabel == initial.ZoomLabel
                    && !afterNavigation.Flipped
                    && closed
                    && !win.ModalVisibleForSmoke
                    && Math.Abs(afterClose.Zoom - 1) < 0.0001
                    && !afterClose.Flipped
                    && Math.Abs(afterClose.PanX) < 0.0001
                    && Math.Abs(afterClose.PanY) < 0.0001;

                result = new ModalTransformSmokeResult(
                    ok,
                    ok ? "modal flip, keyboard/wheel zoom, reset, and navigation reset passed" : "modal transform smoke did not meet expected behavior",
                    folder,
                    selectIndex,
                    selected,
                    modalVisibleBeforeClose,
                    initial,
                    flipped,
                    afterFlip,
                    shortcutZoomed,
                    afterShortcut,
                    wheelZoomed,
                    afterWheel,
                    reset,
                    afterReset,
                    movedNext,
                    startPath,
                    nextPath,
                    afterNavigation,
                    closed,
                    afterClose);
            }
            catch (Exception ex)
            {
                result = new ModalTransformSmokeResult(false, ex.Message, folder, selectIndex);
            }

            win.Close();
            WriteModalTransformSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureModalPanSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        int selectIndex = ArgInt(args, "--select-index", 0);
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteModalPanSmokeResult(resultPath, new ModalPanSmokeResult(false, "missing required --folder", folder, selectIndex));
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            ModalPanSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                bool selected = win.SelectIndexForSmoke(selectIndex);
                win.ShowModalForShot();
                bool firstZoom = win.ModalZoomShortcutForSmoke("plus");
                bool secondZoom = win.ModalZoomShortcutForSmoke("plus");
                var zoomed = win.ModalTransformForSmoke();
                bool pannedPositive = win.SetModalPanForSmoke(double.MaxValue, double.MinValue);
                var afterPositivePan = win.ModalTransformForSmoke();
                bool pannedNegative = win.SetModalPanForSmoke(double.MinValue, double.MaxValue);
                var afterNegativePan = win.ModalTransformForSmoke();
                bool reset = win.ResetModalTransformForSmoke();
                var afterReset = win.ModalTransformForSmoke();
                string? startPath = win.SelectedPathForSmoke;
                bool movedNext = win.NavigateModalForSmoke(1);
                string? nextPath = win.SelectedPathForSmoke;
                var afterNavigation = win.ModalTransformForSmoke();

                const double epsilon = 0.0001;
                bool ok = selected
                    && win.ModalVisibleForSmoke
                    && firstZoom
                    && secondZoom
                    && zoomed.Zoom > 1
                    && zoomed.MaxPanX > 0
                    && zoomed.MaxPanY > 0
                    && pannedPositive
                    && afterPositivePan.PanX > 0
                    && afterPositivePan.PanY < 0
                    && Math.Abs(afterPositivePan.PanX) <= afterPositivePan.MaxPanX + epsilon
                    && Math.Abs(afterPositivePan.PanY) <= afterPositivePan.MaxPanY + epsilon
                    && pannedNegative
                    && afterNegativePan.PanX < 0
                    && afterNegativePan.PanY > 0
                    && Math.Abs(afterNegativePan.PanX) <= afterNegativePan.MaxPanX + epsilon
                    && Math.Abs(afterNegativePan.PanY) <= afterNegativePan.MaxPanY + epsilon
                    && reset
                    && Math.Abs(afterReset.Zoom - 1) < epsilon
                    && Math.Abs(afterReset.PanX) < epsilon
                    && Math.Abs(afterReset.PanY) < epsilon
                    && movedNext
                    && !string.Equals(startPath, nextPath, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(afterNavigation.PanX) < epsilon
                    && Math.Abs(afterNavigation.PanY) < epsilon;

                result = new ModalPanSmokeResult(
                    ok,
                    ok ? "modal pan clamps after zoom and resets on reset/navigation" : "modal pan smoke did not meet expected behavior",
                    folder,
                    selectIndex,
                    selected,
                    win.ModalVisibleForSmoke,
                    zoomed,
                    pannedPositive,
                    afterPositivePan,
                    pannedNegative,
                    afterNegativePan,
                    reset,
                    afterReset,
                    movedNext,
                    startPath,
                    nextPath,
                    afterNavigation);
            }
            catch (Exception ex)
            {
                result = new ModalPanSmokeResult(false, ex.Message, folder, selectIndex);
            }

            win.Close();
            WriteModalPanSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureSelectionSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        string? statePath = ArgValue(args, "--state-path");
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(statePath))
        {
            WriteSelectionSmokeResult(resultPath, new SelectionSmokeResult(false, "missing required --folder or --state-path", folder, statePath));
            Shutdown(1);
            return;
        }

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            SelectionSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                List<string> names = win.FilteredFileNamesForSmoke(20);
                if (names.Count < 6)
                    throw new InvalidOperationException("selection smoke requires at least 6 fixture images");

                bool rangeSelected = win.SelectRangeForSmoke(1, 3);
                int rangeCount = win.SelectedCountForSmoke;
                List<string> rangeNames = win.SelectedFileNamesForSmoke;
                string? primaryAfterRange = win.SelectedFileNameForSmoke;
                int cardsAfterRange = win.CardsSelectedCountForSmoke;
                int rowsAfterRange = win.RowsSelectedCountForSmoke;
                bool headerAfterRange = win.HeaderStatsForSmoke.StartsWith("3 selected", StringComparison.Ordinal);

                bool toggledRemoved = win.ToggleSelectionForSmoke(2);
                int afterRemoveCount = win.SelectedCountForSmoke;
                List<string> afterRemoveNames = win.SelectedFileNamesForSmoke;
                string? primaryAfterRemove = win.SelectedFileNameForSmoke;

                bool toggledAdded = win.ToggleSelectionForSmoke(5);
                int afterAddCount = win.SelectedCountForSmoke;
                List<string> afterAddNames = win.SelectedFileNamesForSmoke;
                string? primaryAfterAdd = win.SelectedFileNameForSmoke;
                bool listMode = win.SetListModeForSmoke();
                int rowsAfterMode = win.RowsSelectedCountForSmoke;
                int cardsAfterMode = win.CardsSelectedCountForSmoke;
                bool gridMode = win.SetGridModeForSmoke();
                var persisted = ReadPersistedState(statePath);

                bool ok = win.MultiSelectionEnabledForSmoke
                    && rangeSelected
                    && rangeCount == 3
                    && rangeNames.SequenceEqual(names.Skip(1).Take(3), StringComparer.OrdinalIgnoreCase)
                    && string.Equals(primaryAfterRange, names[3], StringComparison.OrdinalIgnoreCase)
                    && cardsAfterRange == 3
                    && rowsAfterRange == 3
                    && headerAfterRange
                    && toggledRemoved
                    && afterRemoveCount == 2
                    && afterRemoveNames.SequenceEqual([names[1], names[3]], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(primaryAfterRemove, names[3], StringComparison.OrdinalIgnoreCase)
                    && toggledAdded
                    && afterAddCount == 3
                    && afterAddNames.SequenceEqual([names[1], names[3], names[5]], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(primaryAfterAdd, names[5], StringComparison.OrdinalIgnoreCase)
                    && listMode
                    && gridMode
                    && rowsAfterMode == 3
                    && cardsAfterMode == 3
                    && string.Equals(persisted?.SelectedPath, win.SelectedPathForSmoke, StringComparison.OrdinalIgnoreCase);

                result = new SelectionSmokeResult(
                    ok,
                    ok ? "Ctrl/Shift-style range and toggle selection, view synchronization, header count, and primary persistence passed" : "selection smoke did not meet expected multi-selection behavior",
                    folder,
                    statePath,
                    names,
                    rangeSelected,
                    rangeCount,
                    rangeNames,
                    primaryAfterRange,
                    cardsAfterRange,
                    rowsAfterRange,
                    toggledRemoved,
                    afterRemoveCount,
                    afterRemoveNames,
                    primaryAfterRemove,
                    toggledAdded,
                    afterAddCount,
                    afterAddNames,
                    primaryAfterAdd,
                    listMode,
                    gridMode,
                    cardsAfterMode,
                    rowsAfterMode,
                    persisted?.SelectedPath);
            }
            catch (Exception ex)
            {
                result = new SelectionSmokeResult(false, ex.Message, folder, statePath);
            }

            win.Close();
            WriteSelectionSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CapturePreviewTabReorderSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-preview-tab-reorder-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        Directory.CreateDirectory(folder);
        const string firstName = "a-first.png";
        const string secondName = "b-pinned.png";
        const string thirdName = "c-active.png";
        WriteSmokePng(Path.Combine(folder, firstName), 48, 36, Color.FromRgb(80, 130, 210));
        WriteSmokePng(Path.Combine(folder, secondName), 48, 36, Color.FromRgb(230, 100, 130));
        WriteSmokePng(Path.Combine(folder, thirdName), 48, 36, Color.FromRgb(100, 200, 130));
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            PreviewTabReorderSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSortByForSmoke("name");
                bool openedFirst = win.SelectFileNameForSmoke(firstName) && win.OpenSelectedPreviewTabForSmoke();
                bool openedSecond = win.SelectFileNameForSmoke(secondName) && win.OpenSelectedPreviewTabForSmoke();
                bool openedThird = win.SelectFileNameForSmoke(thirdName) && win.OpenSelectedPreviewTabForSmoke();
                bool pinnedSecond = win.TogglePreviewTabPinForSmoke(secondName) && win.IsPreviewTabPinnedForSmoke(secondName);
                await win.Dispatcher.InvokeAsync(win.UpdateLayout, DispatcherPriority.Render);
                bool accessible = win.PreviewTabAccessibilityForSmoke;

                bool focusedSecond = win.FocusPreviewTabForSmoke(secondName);
                bool keyboardRight = win.ReorderFocusedPreviewTabForSmoke(1);
                await win.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                bool rightOrder = win.PreviewTabNamesForSmoke.SequenceEqual([firstName, thirdName, secondName], StringComparer.OrdinalIgnoreCase);
                bool rightFocus = win.FocusedPreviewTabForSmoke(secondName);

                bool keyboardLeft = win.ReorderFocusedPreviewTabForSmoke(-1);
                await win.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                bool leftOrder = win.PreviewTabNamesForSmoke.SequenceEqual([firstName, secondName, thirdName], StringComparer.OrdinalIgnoreCase);
                bool leftFocus = win.FocusedPreviewTabForSmoke(secondName);

                bool dragReordered = win.DragMovePreviewTabForSmoke(firstName, 2);
                await win.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                List<string> orderBeforeReload = win.PreviewTabNamesForSmoke;
                string? activeBeforeReload = win.ActivePreviewTabNameForSmoke;
                ViewerState? persisted = ReadPersistedState(statePath);
                string? firstPath = win.PathForFileNameForSmoke(firstName);
                string? secondPath = win.PathForFileNameForSmoke(secondName);
                string? thirdPath = win.PathForFileNameForSmoke(thirdName);
                bool persistedOrderActivePin = secondPath is not null && thirdPath is not null && firstPath is not null
                    && persisted?.PreviewTabPaths?.SequenceEqual([secondPath, thirdPath, firstPath], StringComparer.OrdinalIgnoreCase) == true
                    && string.Equals(persisted.ActivePreviewTabPath, thirdPath, StringComparison.OrdinalIgnoreCase)
                    && persisted.PinnedPreviewPaths?.Contains(secondPath, StringComparer.OrdinalIgnoreCase) == true;
                win.Close();

                var reloaded = HiddenWindow();
                reloaded.Show();
                await reloaded.LoadFolderAsync(folder);
                await reloaded.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                bool reloadPreserved = reloaded.PreviewTabNamesForSmoke.SequenceEqual([secondName, thirdName, firstName], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(reloaded.ActivePreviewTabNameForSmoke, thirdName, StringComparison.OrdinalIgnoreCase)
                    && reloaded.IsPreviewTabPinnedForSmoke(secondName);
                bool invalidRecovery = reloaded.InvalidPreviewTabMovePreservesStateForSmoke();
                bool middleClosedFirst = reloaded.MiddleClosePreviewTabForSmoke(firstName)
                    && !reloaded.PreviewTabNamesForSmoke.Contains(firstName, StringComparer.OrdinalIgnoreCase);
                bool middleClosedSecond = reloaded.MiddleClosePreviewTabForSmoke(secondName)
                    && !reloaded.PreviewTabNamesForSmoke.Contains(secondName, StringComparer.OrdinalIgnoreCase);
                bool middleClosedLast = reloaded.MiddleClosePreviewTabForSmoke(thirdName) && reloaded.PreviewTabCountForSmoke == 0;
                await reloaded.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                bool restoreFocus = reloaded.RestorePreviewTabFocusForSmoke;
                bool restored = reloaded.RestoreLastClosedPreviewTabForSmoke();
                await reloaded.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                bool restoredFocus = reloaded.FocusedPreviewTabForSmoke(thirdName);
                reloaded.Close();

                bool activeAndPinStable = string.Equals(activeBeforeReload, thirdName, StringComparison.OrdinalIgnoreCase)
                    && orderBeforeReload.SequenceEqual([secondName, thirdName, firstName], StringComparer.OrdinalIgnoreCase);
                bool ok = openedFirst && openedSecond && openedThird && pinnedSecond && accessible
                    && focusedSecond && keyboardRight && rightOrder && rightFocus
                    && keyboardLeft && leftOrder && leftFocus && dragReordered
                    && activeAndPinStable && persistedOrderActivePin && reloadPreserved && invalidRecovery
                    && middleClosedFirst && middleClosedSecond && middleClosedLast && restoreFocus && restored && restoredFocus;
                result = new PreviewTabReorderSmokeResult
                {
                    Ok = ok,
                    Message = ok ? "preview tab drag/keyboard reorder, middle close, reload persistence, focus, accessibility, and failure recovery passed" : "preview tab reorder parity did not meet the expected contract",
                    Accessibility = accessible,
                    KeyboardRight = keyboardRight && rightOrder && rightFocus,
                    KeyboardLeft = keyboardLeft && leftOrder && leftFocus,
                    DragReordered = dragReordered,
                    PersistedOrderActivePin = persistedOrderActivePin,
                    ReloadPreserved = reloadPreserved,
                    InvalidRecovery = invalidRecovery,
                    MiddleClose = middleClosedFirst && middleClosedSecond && middleClosedLast,
                    RestoreFocus = restoreFocus && restored && restoredFocus,
                    OrderBeforeReload = orderBeforeReload,
                    ActiveBeforeReload = activeBeforeReload,
                };
            }
            catch (Exception ex)
            {
                win.Close();
                result = new PreviewTabReorderSmokeResult { Message = ex.Message };
            }
            finally
            {
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WritePreviewTabReorderSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CapturePreviewTabsSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WritePreviewTabsSmokeResult(resultPath, new PreviewTabsSmokeResult { Ok = false, Message = "missing required --folder", Folder = folder });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string[] fixtureNames = GetSmokeImageFileNames(fullFolder);
        if (fixtureNames.Length < 3)
        {
            WritePreviewTabsSmokeResult(resultPath, new PreviewTabsSmokeResult { Ok = false, Message = "preview tabs smoke requires at least 3 fixture images", Folder = fullFolder });
            Shutdown(1);
            return;
        }

        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-preview-tabs-smoke-" + Guid.NewGuid().ToString("N"));
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            PreviewTabsSmokeResult result;
            try
            {
                await win.LoadFolderAsync(fullFolder);
                win.SetSortByForSmoke("name");
                string firstName = fixtureNames[0];
                string secondName = fixtureNames[1];

                bool selectedFirst = win.SelectFileNameForSmoke(firstName);
                bool openedFirst = win.OpenSelectedPreviewTabForSmoke();
                int countAfterFirst = win.PreviewTabCountForSmoke;
                string? activeAfterFirst = win.ActivePreviewTabNameForSmoke;
                List<string> tabsAfterFirst = win.PreviewTabNamesForSmoke;

                bool selectedSecond = win.SelectFileNameForSmoke(secondName);
                bool openedSecond = win.OpenSelectedPreviewTabForSmoke();
                int countAfterSecond = win.PreviewTabCountForSmoke;
                string? activeAfterSecond = win.ActivePreviewTabNameForSmoke;
                List<string> tabsAfterSecond = win.PreviewTabNamesForSmoke;
                bool pinnedFirst = win.TogglePreviewTabPinForSmoke(firstName);
                bool firstPinnedAfterToggle = win.IsPreviewTabPinnedForSmoke(firstName);
                int pinnedCountAfterToggle = win.PinnedPreviewCountForSmoke;
                ViewerState? persistedPinState = ReadPersistedState(statePath);
                string? firstPath = win.PathForFileNameForSmoke(firstName);
                bool firstPinPersisted = firstPath is not null
                    && persistedPinState?.PinnedPreviewPaths?.Contains(firstPath, StringComparer.OrdinalIgnoreCase) == true;

                string? selectedBeforeHover = win.SelectedFileNameForSmoke;
                string? activeBeforeHover = win.ActivePreviewTabNameForSmoke;
                bool hoverFirstShown = win.ShowPreviewTabHoverForSmoke(firstName);
                bool hoverVisibleAfterShow = win.PreviewTabHoverVisibleForSmoke;
                string? hoverNameAfterShow = win.HoverPreviewTabNameForSmoke;
                string? hoverPathAfterShow = win.HoverPreviewTabPathForSmoke;
                string? selectedAfterHoverShow = win.SelectedFileNameForSmoke;
                string? activeAfterHoverShow = win.ActivePreviewTabNameForSmoke;
                bool hoverFirstHidden = win.HidePreviewTabHoverForSmoke(firstName);
                bool hoverVisibleAfterHide = win.PreviewTabHoverVisibleForSmoke;
                string? hoverNameAfterHide = win.HoverPreviewTabNameForSmoke;

                bool activatedFirst = win.ActivatePreviewTabForSmoke(firstName);
                string? selectedAfterActivateFirst = win.SelectedFileNameForSmoke;
                string? activeAfterActivateFirst = win.ActivePreviewTabNameForSmoke;

                bool closedFirst = win.ClosePreviewTabForSmoke(firstName);
                int countAfterCloseFirst = win.PreviewTabCountForSmoke;
                int closedStackAfterCloseFirst = win.ClosedPreviewTabCountForSmoke;
                string? activeAfterCloseFirst = win.ActivePreviewTabNameForSmoke;
                string? selectedAfterCloseFirst = win.SelectedFileNameForSmoke;
                bool pinSurvivedClose = win.PinnedPreviewCountForSmoke == 1;

                bool restoredFirst = win.RestoreLastClosedPreviewTabForSmoke();
                int countAfterRestore = win.PreviewTabCountForSmoke;
                string? activeAfterRestore = win.ActivePreviewTabNameForSmoke;
                string? selectedAfterRestore = win.SelectedFileNameForSmoke;
                List<string> tabsAfterRestore = win.PreviewTabNamesForSmoke;
                bool firstPinnedAfterRestore = win.IsPreviewTabPinnedForSmoke(firstName);

                win.CloseAllPreviewTabsForSmoke();
                int countAfterCloseAll = win.PreviewTabCountForSmoke;
                int closedStackAfterCloseAll = win.ClosedPreviewTabCountForSmoke;
                bool pinSurvivedCloseAll = win.PinnedPreviewCountForSmoke == 1;

                bool restoredAfterCloseAll = win.RestoreLastClosedPreviewTabForSmoke();
                int countAfterRestoreAll = win.PreviewTabCountForSmoke;
                string? activeAfterRestoreAll = win.ActivePreviewTabNameForSmoke;
                string? selectedAfterRestoreAll = win.SelectedFileNameForSmoke;
                win.Close();

                var reloaded = HiddenWindow();
                reloaded.Show();
                reloaded.SuppressStatePersistence();
                await reloaded.LoadFolderAsync(fullFolder);
                int tabCountAfterReload = reloaded.PreviewTabCountForSmoke;
                List<string> tabsAfterReload = reloaded.PreviewTabNamesForSmoke;
                string? activeAfterReload = reloaded.ActivePreviewTabNameForSmoke;
                string? selectedAfterReload = reloaded.SelectedFileNameForSmoke;
                bool modalClosedAfterReload = !reloaded.ModalVisibleForSmoke;
                bool selectedFirstAfterReload = reloaded.SelectFileNameForSmoke(firstName);
                bool openedFirstAfterReload = reloaded.OpenSelectedPreviewTabForSmoke();
                bool firstPinnedAfterReload = reloaded.IsPreviewTabPinnedForSmoke(firstName);
                int pinnedCountAfterReload = reloaded.PinnedPreviewCountForSmoke;
                reloaded.Close();

                bool ok = selectedFirst
                    && openedFirst
                    && countAfterFirst == 1
                    && string.Equals(activeAfterFirst, firstName, StringComparison.OrdinalIgnoreCase)
                    && tabsAfterFirst.SequenceEqual([firstName], StringComparer.OrdinalIgnoreCase)
                    && selectedSecond
                    && openedSecond
                    && countAfterSecond == 2
                    && string.Equals(activeAfterSecond, secondName, StringComparison.OrdinalIgnoreCase)
                    && tabsAfterSecond.SequenceEqual([firstName, secondName], StringComparer.OrdinalIgnoreCase)
                    && pinnedFirst
                    && firstPinnedAfterToggle
                    && pinnedCountAfterToggle == 1
                    && firstPinPersisted
                    && hoverFirstShown
                    && hoverVisibleAfterShow
                    && string.Equals(hoverNameAfterShow, firstName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(hoverPathAfterShow)
                    && string.Equals(activeAfterHoverShow, activeBeforeHover, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterHoverShow, selectedBeforeHover, StringComparison.OrdinalIgnoreCase)
                    && hoverFirstHidden
                    && !hoverVisibleAfterHide
                    && string.IsNullOrWhiteSpace(hoverNameAfterHide)
                    && activatedFirst
                    && string.Equals(activeAfterActivateFirst, firstName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterActivateFirst, firstName, StringComparison.OrdinalIgnoreCase)
                    && closedFirst
                    && countAfterCloseFirst == 1
                    && closedStackAfterCloseFirst == 1
                    && string.Equals(activeAfterCloseFirst, secondName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterCloseFirst, secondName, StringComparison.OrdinalIgnoreCase)
                    && pinSurvivedClose
                    && restoredFirst
                    && countAfterRestore == 2
                    && string.Equals(activeAfterRestore, firstName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterRestore, firstName, StringComparison.OrdinalIgnoreCase)
                    && tabsAfterRestore.SequenceEqual([secondName, firstName], StringComparer.OrdinalIgnoreCase)
                    && firstPinnedAfterRestore
                    && countAfterCloseAll == 0
                    && closedStackAfterCloseAll >= 2
                    && pinSurvivedCloseAll
                    && restoredAfterCloseAll
                    && countAfterRestoreAll == 1
                    && !string.IsNullOrWhiteSpace(activeAfterRestoreAll)
                    && string.Equals(selectedAfterRestoreAll, activeAfterRestoreAll, StringComparison.OrdinalIgnoreCase)
                    && tabCountAfterReload == 1
                    && tabsAfterReload.SequenceEqual([firstName], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(activeAfterReload, firstName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterReload, firstName, StringComparison.OrdinalIgnoreCase)
                    && modalClosedAfterReload
                    && selectedFirstAfterReload
                    && openedFirstAfterReload
                    && firstPinnedAfterReload
                    && pinnedCountAfterReload == 1;

                result = new PreviewTabsSmokeResult
                {
                    Ok = ok,
                    Message = ok ? "preview tabs open, pin, hover, activate, close, restore, close-all, automatic reload, and selection sync passed" : "preview tab behavior did not match expected browser parity subset",
                    Folder = fullFolder,
                    StatePath = statePath,
                    SeenPath = seenPath,
                    FavoritesPath = favoritesPath,
                    FirstName = firstName,
                    SecondName = secondName,
                    CountAfterFirst = countAfterFirst,
                    ActiveAfterFirst = activeAfterFirst,
                    TabsAfterFirst = tabsAfterFirst,
                    CountAfterSecond = countAfterSecond,
                    ActiveAfterSecond = activeAfterSecond,
                    TabsAfterSecond = tabsAfterSecond,
                    PinnedFirst = pinnedFirst,
                    FirstPinnedAfterToggle = firstPinnedAfterToggle,
                    PinnedCountAfterToggle = pinnedCountAfterToggle,
                    FirstPinPersisted = firstPinPersisted,
                    HoverFirstShown = hoverFirstShown,
                    HoverVisibleAfterShow = hoverVisibleAfterShow,
                    HoverNameAfterShow = hoverNameAfterShow,
                    HoverPathAfterShow = hoverPathAfterShow,
                    ActiveAfterHoverShow = activeAfterHoverShow,
                    SelectedAfterHoverShow = selectedAfterHoverShow,
                    HoverFirstHidden = hoverFirstHidden,
                    HoverVisibleAfterHide = hoverVisibleAfterHide,
                    HoverNameAfterHide = hoverNameAfterHide,
                    ActiveAfterActivateFirst = activeAfterActivateFirst,
                    SelectedAfterActivateFirst = selectedAfterActivateFirst,
                    CountAfterCloseFirst = countAfterCloseFirst,
                    ClosedStackAfterCloseFirst = closedStackAfterCloseFirst,
                    ActiveAfterCloseFirst = activeAfterCloseFirst,
                    SelectedAfterCloseFirst = selectedAfterCloseFirst,
                    PinSurvivedClose = pinSurvivedClose,
                    CountAfterRestore = countAfterRestore,
                    ActiveAfterRestore = activeAfterRestore,
                    SelectedAfterRestore = selectedAfterRestore,
                    TabsAfterRestore = tabsAfterRestore,
                    FirstPinnedAfterRestore = firstPinnedAfterRestore,
                    CountAfterCloseAll = countAfterCloseAll,
                    ClosedStackAfterCloseAll = closedStackAfterCloseAll,
                    PinSurvivedCloseAll = pinSurvivedCloseAll,
                    CountAfterRestoreAll = countAfterRestoreAll,
                    ActiveAfterRestoreAll = activeAfterRestoreAll,
                    SelectedAfterRestoreAll = selectedAfterRestoreAll,
                    TabCountAfterReload = tabCountAfterReload,
                    TabsAfterReload = tabsAfterReload,
                    ActiveAfterReload = activeAfterReload,
                    SelectedAfterReload = selectedAfterReload,
                    ModalClosedAfterReload = modalClosedAfterReload,
                    SelectedFirstAfterReload = selectedFirstAfterReload,
                    OpenedFirstAfterReload = openedFirstAfterReload,
                    FirstPinnedAfterReload = firstPinnedAfterReload,
                    PinnedCountAfterReload = pinnedCountAfterReload,
                };
            }
            catch (Exception ex)
            {
                win.Close();
                result = new PreviewTabsSmokeResult { Ok = false, Message = ex.Message, Folder = fullFolder, StatePath = statePath, SeenPath = seenPath, FavoritesPath = favoritesPath };
            }
            finally
            {
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WritePreviewTabsSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CapturePreviewTabHoverSmoke(string resultPath)
    {
        string resultFullPath = Path.GetFullPath(resultPath);
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-preview-tab-hover-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");

        Directory.CreateDirectory(folder);
        const string slowName = "a-slow-source.png";
        const string newestName = "b-newest-target.png";
        const string corruptName = "c-corrupt-preview.png";
        WriteSmokePng(Path.Combine(folder, slowName), 1920, 1080, Color.FromRgb(80, 130, 210));
        WriteSmokePng(Path.Combine(folder, newestName), 80, 60, Color.FromRgb(230, 100, 130));
        File.WriteAllBytes(Path.Combine(folder, corruptName), [0x89, 0x50, 0x4e, 0x47, 0x00, 0x01, 0x02, 0x03]);

        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.SuppressStatePersistence();

        win.Dispatcher.InvokeAsync(async () =>
        {
            PreviewTabHoverSmokeResult result;
            var heartbeat = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(15),
            };
            int heartbeatCount = 0;
            heartbeat.Tick += (_, _) => heartbeatCount++;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSortByForSmoke("name");
                bool selectedSlow = win.SelectFileNameForSmoke(slowName);
                bool openedSlow = win.OpenSelectedPreviewTabForSmoke();
                bool selectedNewest = win.SelectFileNameForSmoke(newestName);
                bool openedNewest = win.OpenSelectedPreviewTabForSmoke();
                bool selectedCorrupt = win.SelectFileNameForSmoke(corruptName);
                bool openedCorrupt = win.OpenSelectedPreviewTabForSmoke();
                string? selectedBeforeHover = win.SelectedFileNameForSmoke;
                string? activeBeforeHover = win.ActivePreviewTabNameForSmoke;

                win.SetPreviewTabHoverDecodeDelayForSmoke(slowName, 450);
                heartbeat.Start();
                bool slowShown = win.ShowPreviewTabHoverWithDecodeForSmoke(slowName);
                Task<MainWindow.PreviewTabHoverDecodeCompletion> slowCompletion = win.WaitForPreviewTabHoverDecodeForSmokeAsync();
                bool newestShown = win.ShowPreviewTabHoverWithDecodeForSmoke(newestName);
                Task<MainWindow.PreviewTabHoverDecodeCompletion> newestCompletionTask = win.WaitForPreviewTabHoverDecodeForSmokeAsync();
                Task completed = await Task.WhenAny(newestCompletionTask, Task.Delay(TimeSpan.FromSeconds(6)));
                MainWindow.PreviewTabHoverDecodeCompletion newestCompletion = completed == newestCompletionTask
                    ? await newestCompletionTask
                    : new MainWindow.PreviewTabHoverDecodeCompletion(false, false, false, "timed out waiting for newest hover decode");
                MainWindow.PreviewTabHoverDecodeCompletion slowCompletionResult = await slowCompletion;
                await Task.Delay(500);
                heartbeat.Stop();

                bool newestStayedVisible = string.Equals(win.HoverPreviewTabNameForSmoke, newestName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileName(win.HoverPreviewTabBitmapPathForSmoke), newestName, StringComparison.OrdinalIgnoreCase);
                string? newestHoverName = win.HoverPreviewTabNameForSmoke;
                string? newestBitmapName = Path.GetFileName(win.HoverPreviewTabBitmapPathForSmoke);
                bool selectionAndActiveStable = string.Equals(win.SelectedFileNameForSmoke, selectedBeforeHover, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(win.ActivePreviewTabNameForSmoke, activeBeforeHover, StringComparison.OrdinalIgnoreCase);

                bool corruptShown = win.ShowPreviewTabHoverWithDecodeForSmoke(corruptName);
                Task<MainWindow.PreviewTabHoverDecodeCompletion> corruptCompletionTask = win.WaitForPreviewTabHoverDecodeForSmokeAsync();
                completed = await Task.WhenAny(corruptCompletionTask, Task.Delay(TimeSpan.FromSeconds(6)));
                MainWindow.PreviewTabHoverDecodeCompletion corruptCompletion = completed == corruptCompletionTask
                    ? await corruptCompletionTask
                    : new MainWindow.PreviewTabHoverDecodeCompletion(false, false, false, "timed out waiting for corrupt hover decode");
                bool corruptInlineRecovery = corruptCompletion.Failed
                    && win.DeleteStatusVisibleForSmoke
                    && win.DeleteStatusForSmoke.Contains("Preview tab image could not be decoded", StringComparison.OrdinalIgnoreCase)
                    && win.PreviewTabHoverVisibleForSmoke
                    && string.Equals(win.HoverPreviewTabNameForSmoke, corruptName, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(win.HoverPreviewTabBitmapPathForSmoke)
                    && !win.DeleteConfirmationVisibleForSmoke
                    && !win.ModalVisibleForSmoke;

                bool heartbeatAdvanced = heartbeatCount >= 4;
                bool ok = selectedSlow && openedSlow && selectedNewest && openedNewest && selectedCorrupt && openedCorrupt
                    && slowShown && newestShown && corruptShown
                    && heartbeatAdvanced
                    && newestCompletion.Applied
                    && slowCompletionResult.Discarded
                    && newestStayedVisible
                    && selectionAndActiveStable
                    && corruptInlineRecovery
                    && win.PreviewTabHoverDecodeStartCountForSmoke >= 3
                    && win.PreviewTabHoverDecodeFailureCountForSmoke >= 1;
                result = new PreviewTabHoverSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "preview-tab hover decode stayed non-blocking, newest-only, and recoverable for corrupt input"
                        : "preview-tab hover decode did not meet responsiveness or recovery expectations",
                    HeartbeatCount = heartbeatCount,
                    SlowCompletionDiscarded = slowCompletionResult.Discarded,
                    NewestCompletionApplied = newestCompletion.Applied,
                    NewestHoverName = newestHoverName,
                    NewestBitmapName = newestBitmapName,
                    SelectionAndActiveStable = selectionAndActiveStable,
                    CorruptCompletionFailed = corruptCompletion.Failed,
                    CorruptInlineRecovery = corruptInlineRecovery,
                    DecodeStartCount = win.PreviewTabHoverDecodeStartCountForSmoke,
                    DecodeFailureCount = win.PreviewTabHoverDecodeFailureCountForSmoke,
                    Status = win.DeleteStatusForSmoke,
                };
            }
            catch (Exception ex)
            {
                heartbeat.Stop();
                result = new PreviewTabHoverSmokeResult { Message = ex.Message };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WritePreviewTabHoverSmokeResult(resultFullPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
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

    private void CaptureShortcutTypingSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WriteShortcutTypingSmokeResult(resultPath, new ShortcutTypingSmokeResult
            {
                Message = "missing required --folder",
            });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string[] fixtureNames = GetSmokeImageFileNames(fullFolder);
        if (fixtureNames.Length == 0)
        {
            WriteShortcutTypingSmokeResult(resultPath, new ShortcutTypingSmokeResult
            {
                Message = "shortcut typing smoke requires at least one fixture image",
                Folder = fullFolder,
            });
            Shutdown(1);
            return;
        }

        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-shortcut-typing-smoke-" + Guid.NewGuid().ToString("N"));
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();

        win.Dispatcher.InvokeAsync(async () =>
        {
            ShortcutTypingSmokeResult result;
            try
            {
                await win.LoadFolderAsync(fullFolder);
                string selectedName = fixtureNames[0];
                bool selected = win.SelectFileNameForSmoke(selectedName);
                int favoriteBefore = win.SelectedFavoriteLevelForSmoke;

                bool searchFocused = win.FocusSearchInputForSmoke();
                bool editableFocus = win.IsEditableTextInputFocusedForSmoke;
                bool fHandledWhileTyping = win.InvokePreviewKeyForSmoke(Key.F);
                bool uHandledWhileTyping = win.InvokePreviewKeyForSmoke(Key.U);
                int favoriteAfterTyping = win.SelectedFavoriteLevelForSmoke;

                win.SetSearchQuery("fox", persist: false);
                bool queryRecorded = string.Equals(win.SearchQueryForSmoke, "fox", StringComparison.Ordinal);
                win.SetSearchQuery("", persist: false);
                bool reselected = win.SelectFileNameForSmoke(selectedName);
                bool cardsFocused = win.FocusCardsListForSmoke();
                bool nonEditableFocus = !win.IsEditableTextInputFocusedForSmoke;
                bool fHandledOutsideTyping = win.InvokePreviewKeyForSmoke(Key.F);
                int favoriteAfterF = win.SelectedFavoriteLevelForSmoke;
                bool uHandledOutsideTyping = win.InvokePreviewKeyForSmoke(Key.U);
                int favoriteAfterU = win.SelectedFavoriteLevelForSmoke;

                bool ok = selected
                    && searchFocused
                    && editableFocus
                    && !fHandledWhileTyping
                    && !uHandledWhileTyping
                    && favoriteAfterTyping == favoriteBefore
                    && queryRecorded
                    && reselected
                    && cardsFocused
                    && nonEditableFocus
                    && fHandledOutsideTyping
                    && favoriteAfterF == 1
                    && uHandledOutsideTyping
                    && favoriteAfterU == 0;

                result = new ShortcutTypingSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "editable text input kept F/U as text keys while global favorites shortcuts still worked outside the input"
                        : "shortcut typing guard did not preserve the expected text-input and global-shortcut behavior",
                    Folder = fullFolder,
                    StatePath = statePath,
                    FavoritesPath = favoritesPath,
                    SelectedName = selectedName,
                    SearchFocused = searchFocused,
                    EditableFocus = editableFocus,
                    FHandledWhileTyping = fHandledWhileTyping,
                    UHandledWhileTyping = uHandledWhileTyping,
                    FavoriteBefore = favoriteBefore,
                    FavoriteAfterTyping = favoriteAfterTyping,
                    QueryRecorded = queryRecorded,
                    CardsFocused = cardsFocused,
                    NonEditableFocus = nonEditableFocus,
                    FHandledOutsideTyping = fHandledOutsideTyping,
                    UHandledOutsideTyping = uHandledOutsideTyping,
                    FavoriteAfterF = favoriteAfterF,
                    FavoriteAfterU = favoriteAfterU,
                };
            }
            catch (Exception ex)
            {
                result = new ShortcutTypingSmokeResult
                {
                    Message = ex.Message,
                    Folder = fullFolder,
                    StatePath = statePath,
                    FavoritesPath = favoritesPath,
                };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WriteShortcutTypingSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CapturePngMetadataSmoke(string resultPath)
    {
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-png-metadata-smoke-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string validName = "metadata.png";
        string missingName = "missing.png";
        string ignoredName = "ignored-text.png";
        string validPath = Path.Combine(folder, validName);
        string missingPath = Path.Combine(folder, missingName);
        string ignoredPath = Path.Combine(folder, ignoredName);
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");

        try
        {
            Directory.CreateDirectory(folder);
            WriteSmokePng(missingPath, 128, 96, Color.FromRgb(52, 152, 219));
            WritePngTextFixture(
                validPath,
                "parameters",
                "masterpiece, studio portrait\nNegative prompt: lowres, text\nSteps: 28, Sampler: Euler a, CFG scale: 6.5, Seed: 1234",
                Color.FromRgb(46, 204, 113));
            WritePngTextFixture(ignoredPath, "not-parameters", "this text must not become image metadata", Color.FromRgb(231, 76, 60));
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        }
        catch (Exception ex)
        {
            WritePngMetadataSmokeResult(resultPath, new PngMetadataSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            PngMetadataSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                PngMetadataSmokeSnapshot valid = await win.SelectPngMetadataForSmokeAsync(validName);
                MetadataCopySmokeSnapshot validCopy = win.CopyCurrentPreviewMetadataForSmoke();
                MetadataCopySmokeSnapshot validPromptCopy = win.CopyCurrentPreviewPromptForSmoke(negative: false);
                MetadataCopySmokeSnapshot validNegativeCopy = win.CopyCurrentPreviewPromptForSmoke(negative: true);
                ModalMetadataSmokeSnapshot initialModal = win.OpenModalMetadataForSmoke();
                ModalMetadataSmokeSnapshot validModal = win.ToggleModalMetadataSidebarForSmoke();
                ModalMetadataSmokeSnapshot negativeTabModal = win.SelectModalMetadataTabForSmoke("negative");
                ModalMetadataSmokeSnapshot settingsTabModal = win.SelectModalMetadataTabForSmoke("settings");
                ModalMetadataSmokeSnapshot promptTabModal = win.SelectModalMetadataTabForSmoke("prompt");
                ModalMetadataSmokeSnapshot hiddenModal = win.ToggleModalMetadataSidebarForSmoke();
                ModalMetadataSmokeSnapshot doubleClickModal = win.DoubleClickModalImageForSmoke();
                ModalMetadataSmokeSnapshot doubleClickHiddenModal = win.DoubleClickModalImageForSmoke();
                PngMetadataSmokeSnapshot missing = await win.SelectPngMetadataForSmokeAsync(missingName);
                MetadataCopySmokeSnapshot missingCopy = win.CopyCurrentPreviewMetadataForSmoke();
                MetadataCopySmokeSnapshot missingPromptCopy = win.CopyCurrentPreviewPromptForSmoke(negative: false);
                MetadataCopySmokeSnapshot missingNegativeCopy = win.CopyCurrentPreviewPromptForSmoke(negative: true);
                bool ignoredTextSkipped = !PhotoViewer.Wpf.MainWindow.HasPngParametersForSmoke(ignoredPath);

                bool firstSelectionStarted = win.SelectFileNameForSmoke(validName);
                bool latestSelectionStarted = win.SelectFileNameForSmoke(missingName);
                PngMetadataSmokeSnapshot latest = await win.WaitForPreviewPngMetadataForSmokeAsync(missingName);
                MetadataCopySmokeSnapshot latestCopy = win.CopyCurrentPreviewMetadataForSmoke();
                bool latestSelectionStable = latest.Selected
                    && !latest.MetadataApplied
                    && string.Equals(latest.SelectedPath, missingPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(latest.Prompt, missingPath, StringComparison.OrdinalIgnoreCase)
                    && !latest.SamplerVisible;

                bool ok = valid.MetadataApplied
                    && string.Equals(valid.Prompt, "masterpiece, studio portrait", StringComparison.Ordinal)
                    && string.Equals(valid.NegativePrompt, "lowres, text", StringComparison.Ordinal)
                    && string.Equals(valid.Sampler, "Euler a", StringComparison.Ordinal)
                    && valid.SamplerVisible
                    && validCopy.Copied
                    && validCopy.CopyEnabled
                    && string.Equals(validCopy.SelectedPath, validCopy.MetadataPath, StringComparison.OrdinalIgnoreCase)
                    && validCopy.CopyText.Contains("Prompt: masterpiece, studio portrait", StringComparison.Ordinal)
                    && validCopy.CopyText.Contains("Negative prompt: lowres, text", StringComparison.Ordinal)
                    && validCopy.CopyText.Contains("Steps: 28", StringComparison.Ordinal)
                    && validCopy.CopyText.Contains("Raw parameters:", StringComparison.Ordinal)
                    && validPromptCopy.Copied && validPromptCopy.CopyEnabled
                    && string.Equals(validPromptCopy.CopyText, "masterpiece, studio portrait", StringComparison.Ordinal)
                    && validNegativeCopy.Copied && validNegativeCopy.CopyEnabled
                    && string.Equals(validNegativeCopy.CopyText, "lowres, text", StringComparison.Ordinal)
                    && initialModal.ModalVisible
                    && !initialModal.SidebarVisible
                    && initialModal.MetadataCurrent
                    && validModal.ModalVisible
                    && validModal.SidebarVisible
                    && validModal.MetadataCurrent
                    && validModal.Status.Contains("Sampler: Euler a", StringComparison.Ordinal)
                    && string.Equals(validModal.Prompt, "masterpiece, studio portrait", StringComparison.Ordinal)
                    && string.Equals(validModal.NegativePrompt, "lowres, text", StringComparison.Ordinal)
                    && validModal.CopyMetadataEnabled
                    && validModal.CopyPromptEnabled
                    && validModal.CopyNegativeEnabled
                    && string.Equals(validModal.ActiveTab, "prompt", StringComparison.Ordinal)
                    && validModal.PromptPanelVisible
                    && !validModal.NegativePanelVisible
                    && !validModal.SettingsPanelVisible
                    && string.Equals(negativeTabModal.ActiveTab, "negative", StringComparison.Ordinal)
                    && !negativeTabModal.PromptPanelVisible
                    && negativeTabModal.NegativePanelVisible
                    && !negativeTabModal.SettingsPanelVisible
                    && string.Equals(negativeTabModal.NegativePrompt, "lowres, text", StringComparison.Ordinal)
                    && negativeTabModal.CopyMetadataEnabled
                    && negativeTabModal.CopyPromptEnabled
                    && negativeTabModal.CopyNegativeEnabled
                    && string.Equals(settingsTabModal.ActiveTab, "settings", StringComparison.Ordinal)
                    && !settingsTabModal.PromptPanelVisible
                    && !settingsTabModal.NegativePanelVisible
                    && settingsTabModal.SettingsPanelVisible
                    && settingsTabModal.Settings.Contains("Sampler: Euler a", StringComparison.Ordinal)
                    && settingsTabModal.CopyMetadataEnabled
                    && settingsTabModal.CopyPromptEnabled
                    && settingsTabModal.CopyNegativeEnabled
                    && string.Equals(promptTabModal.ActiveTab, "prompt", StringComparison.Ordinal)
                    && promptTabModal.PromptPanelVisible
                    && !promptTabModal.NegativePanelVisible
                    && !promptTabModal.SettingsPanelVisible
                    && string.Equals(promptTabModal.Prompt, "masterpiece, studio portrait", StringComparison.Ordinal)
                    && promptTabModal.CopyMetadataEnabled
                    && promptTabModal.CopyPromptEnabled
                    && promptTabModal.CopyNegativeEnabled
                    && hiddenModal.ModalVisible
                    && !hiddenModal.SidebarVisible
                    && hiddenModal.MetadataCurrent
                    && hiddenModal.CopyMetadataEnabled
                    && hiddenModal.CopyPromptEnabled
                    && hiddenModal.CopyNegativeEnabled
                    && doubleClickModal.ModalVisible
                    && doubleClickModal.SidebarVisible
                    && doubleClickModal.MetadataCurrent
                    && doubleClickModal.CopyMetadataEnabled
                    && doubleClickModal.CopyPromptEnabled
                    && doubleClickModal.CopyNegativeEnabled
                    && doubleClickHiddenModal.ModalVisible
                    && !doubleClickHiddenModal.SidebarVisible
                    && doubleClickHiddenModal.MetadataCurrent
                    && doubleClickHiddenModal.CopyMetadataEnabled
                    && doubleClickHiddenModal.CopyPromptEnabled
                    && doubleClickHiddenModal.CopyNegativeEnabled
                    && missing.Selected
                    && !missing.MetadataApplied
                    && !missingCopy.Copied
                    && !missingCopy.CopyEnabled
                    && !missingPromptCopy.Copied && !missingPromptCopy.CopyEnabled
                    && !missingNegativeCopy.Copied && !missingNegativeCopy.CopyEnabled
                    && string.Equals(missing.Prompt, missingPath, StringComparison.OrdinalIgnoreCase)
                    && !missing.SamplerVisible
                    && ignoredTextSkipped
                    && firstSelectionStarted
                    && latestSelectionStarted
                    && latestSelectionStable
                    && !latestCopy.Copied
                    && !latestCopy.CopyEnabled;
                result = new PngMetadataSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "lazy PNG parameters metadata survives Prompt/Negative/Settings tab selection plus button and image-double-click sidebar show/hide cycles; missing and unrelated text chunks stay on the safe path fallback"
                        : "PNG parameters metadata smoke did not meet expected lazy selection behavior",
                    SmokeRoot = smokeRoot,
                    Folder = folder,
                    ValidPath = validPath,
                    MissingPath = missingPath,
                    IgnoredPath = ignoredPath,
                    Valid = valid,
                    ValidCopy = validCopy,
                    InitialModal = initialModal,
                    ValidModal = validModal,
                    NegativeTabModal = negativeTabModal,
                    SettingsTabModal = settingsTabModal,
                    PromptTabModal = promptTabModal,
                    HiddenModal = hiddenModal,
                    DoubleClickModal = doubleClickModal,
                    DoubleClickHiddenModal = doubleClickHiddenModal,
                    Missing = missing,
                    MissingCopy = missingCopy,
                    IgnoredTextSkipped = ignoredTextSkipped,
                    FirstSelectionStarted = firstSelectionStarted,
                    LatestSelectionStarted = latestSelectionStarted,
                    Latest = latest,
                    LatestCopy = latestCopy,
                    LatestSelectionStable = latestSelectionStable,
                };
            }
            catch (Exception ex)
            {
                result = new PngMetadataSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot, Folder = folder, ValidPath = validPath, MissingPath = missingPath, IgnoredPath = ignoredPath };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WritePngMetadataSmokeResult(resultPath, result);
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CapturePromptTagSearchSmoke(string resultPath)
    {
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-prompt-tag-search-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string taggedName = "tagged.png";
        string otherName = "other.png";
        string taggedPath = Path.Combine(folder, taggedName);
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");

        try
        {
            Directory.CreateDirectory(folder);
            WritePngTextFixture(
                taggedPath,
                "parameters",
                "studio portrait, soft light, Studio Portrait, ,   \nNegative prompt: lowres\nSteps: 12, Sampler: Euler",
                Color.FromRgb(52, 152, 219));
            WriteSmokePng(Path.Combine(folder, otherName), 64, 48, Color.FromRgb(46, 204, 113));
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        }
        catch (Exception ex)
        {
            WritePromptTagSearchSmokeResult(resultPath, new PromptTagSearchSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            PromptTagSearchSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                win.SetSearchQuery("studio portrait", persist: false);
                PngMetadataSmokeSnapshot metadata = await win.SelectPngMetadataForSmokeAsync(taggedName);
                bool opened = win.OpenModalForSmoke();
                bool sidebarVisible = win.ToggleModalMetadataSidebarForSmoke().SidebarVisible;
                List<string> initialTags = win.ModalPromptTagsForSmoke;
                bool initialAccessibilityReady = win.ModalPromptTagsAccessibilityReadyForSmoke;
                long sourceLengthBefore = new FileInfo(taggedPath).Length;
                DateTime sourceWriteBefore = File.GetLastWriteTimeUtc(taggedPath);
                int enhancementJobsBefore = win.EnhancementJobsReadForSmoke;
                int enhancementCandidatesBefore = win.EnhancedCandidateCountForSmoke;

                PromptTagSearchSmokeSnapshot appended = win.SearchModalPromptTagForSmoke("soft light");
                bool reopened = win.OpenModalForSmoke();
                PngMetadataSmokeSnapshot refreshedMetadata = await win.WaitForPreviewPngMetadataForSmokeAsync(taggedName);
                PromptTagSearchSmokeSnapshot deduped = win.SearchModalPromptTagForSmoke("soft light");
                ViewerState? persistedState = ReadPersistedState(statePath);
                var reloaded = HiddenWindow();
                reloaded.Show();
                await reloaded.LoadFolderAsync(folder);
                string reloadedQuery = reloaded.SearchQueryForSmoke;
                List<string> reloadedNames = reloaded.FilteredFileNamesForSmoke();
                reloaded.Close();
                win.SetSearchQuery("", persist: false);
                PngMetadataSmokeSnapshot missingMetadata = await win.SelectPngMetadataForSmokeAsync(otherName);
                bool missingModalOpened = win.OpenModalForSmoke();
                bool missingSidebarVisible = win.ToggleModalMetadataSidebarForSmoke().SidebarVisible;
                bool promptFallbackVisible = win.ModalPromptTagsForSmoke.Count == 0 && win.ModalPromptTagFallbackVisibleForSmoke;
                win.CloseModalForSmoke();
                bool sourceUntouched = sourceLengthBefore == new FileInfo(taggedPath).Length
                    && sourceWriteBefore == File.GetLastWriteTimeUtc(taggedPath);
                bool searchPersisted = string.Equals(persistedState?.SearchQuery, "studio portrait, soft light", StringComparison.Ordinal)
                    && string.Equals(reloadedQuery, "studio portrait, soft light", StringComparison.Ordinal)
                    && reloadedNames.SequenceEqual([taggedName], StringComparer.OrdinalIgnoreCase);

                bool ok = metadata.MetadataApplied
                    && opened && sidebarVisible && reopened
                    && appended.Applied && deduped.Applied
                    && refreshedMetadata.MetadataApplied
                    && initialTags.SequenceEqual(["studio portrait", "soft light"], StringComparer.OrdinalIgnoreCase)
                    && string.Equals(appended.SearchQuery, "studio portrait, soft light", StringComparison.Ordinal)
                    && string.Equals(deduped.SearchQuery, appended.SearchQuery, StringComparison.Ordinal)
                    && !appended.ModalVisible && !deduped.ModalVisible
                    && appended.SearchFocused && deduped.SearchFocused
                    && initialAccessibilityReady && appended.AccessibilityReady && deduped.AccessibilityReady
                    && appended.FilteredNames.SequenceEqual([taggedName], StringComparer.OrdinalIgnoreCase)
                    && deduped.FilteredNames.SequenceEqual([taggedName], StringComparer.OrdinalIgnoreCase)
                    && sourceUntouched && searchPersisted
                    && !missingMetadata.MetadataApplied && missingModalOpened && missingSidebarVisible && promptFallbackVisible
                    && enhancementJobsBefore == win.EnhancementJobsReadForSmoke
                    && enhancementCandidatesBefore == win.EnhancedCandidateCountForSmoke;
                result = new PromptTagSearchSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "modal prompt tags append a deduped comma query, close the modal, apply search, focus search, and persist only the search query without source, metadata, or enhancement mutation"
                        : "prompt tag search smoke did not meet the modal/search isolation contract",
                    SmokeRoot = smokeRoot,
                    TaggedPath = taggedPath,
                    MetadataApplied = metadata.MetadataApplied,
                    MetadataRefreshed = refreshedMetadata.MetadataApplied,
                    OpenedModal = opened,
                    SidebarVisible = sidebarVisible,
                    InitialTags = initialTags,
                    InitialAccessibilityReady = initialAccessibilityReady,
                    Appended = appended,
                    Deduped = deduped,
                    SourceUntouched = sourceUntouched,
                    SearchPersisted = searchPersisted,
                    ReloadedQuery = reloadedQuery,
                    ReloadedNames = reloadedNames,
                    PromptFallbackVisible = promptFallbackVisible,
                    EnhancementJobsBefore = enhancementJobsBefore,
                    EnhancementJobsAfter = win.EnhancementJobsReadForSmoke,
                    EnhancementCandidatesBefore = enhancementCandidatesBefore,
                    EnhancementCandidatesAfter = win.EnhancedCandidateCountForSmoke,
                };
            }
            catch (Exception ex)
            {
                result = new PromptTagSearchSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot, TaggedPath = taggedPath };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WritePromptTagSearchSmokeResult(resultPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureFileDragOutSmoke(string resultPath)
    {
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-file-drag-out-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "images");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string jobsPath = Path.Combine(smokeRoot, "jobs.json");
        string[] names = ["alpha.png", "bravo.png", "charlie.png"];
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        try
        {
            Directory.CreateDirectory(folder);
            for (int index = 0; index < names.Length; index++)
            {
                string path = Path.Combine(folder, names[index]);
                WriteSmokePng(path, 64, 48, Color.FromRgb((byte)(52 + index * 30), (byte)(152 - index * 20), (byte)(219 - index * 30)));
                File.SetLastWriteTime(path, DateTime.Today.AddMinutes(-index));
            }
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        }
        catch (Exception ex)
        {
            WriteFileDragOutSmokeResult(resultPath, new FileDragOutSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            FileDragOutSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                List<string> displayOrder = win.FilteredFileNamesForSmoke(10);
                bool selectedRange = win.SelectRangeForSmoke(0, 1);
                FileDragOutSmokeSnapshot selectedPayload = win.BuildFileDropPayloadForSmoke(displayOrder[0], originWasSelected: true);
                bool selectedSingle = win.SelectIndexForSmoke(0);
                string stateBefore = FileFingerprint(statePath);
                string favoritesBefore = FileFingerprint(favoritesPath);
                string seenBefore = FileFingerprint(seenPath);
                string jobsBefore = FileFingerprint(jobsPath);
                FileDragOutSmokeSnapshot originOnlyPayload = win.BuildFileDropPayloadForSmoke(displayOrder[2], originWasSelected: false);
                FileDragOutSmokeSnapshot invalidPayload = win.BuildFileDropPayloadForSmoke("missing.png", originWasSelected: false);
                List<string> expectedSelectedPaths = displayOrder.Take(2)
                    .Select(name => win.PathForFileNameForSmoke(name)!)
                    .Select(Path.GetFullPath)
                    .ToList();
                string expectedOriginOnlyPath = Path.GetFullPath(win.PathForFileNameForSmoke(displayOrder[2])!);
                int sourceCountAfter = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly).Count();
                bool mutableStateUntouched = string.Equals(stateBefore, FileFingerprint(statePath), StringComparison.Ordinal)
                    && string.Equals(favoritesBefore, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(seenBefore, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal);
                bool ok = selectedRange && selectedSingle
                    && selectedPayload.Built && selectedPayload.FileDropFormatPresent && selectedPayload.ExceedsThreshold && selectedPayload.SurfaceContractReady
                    && selectedPayload.Paths.SequenceEqual(expectedSelectedPaths, StringComparer.OrdinalIgnoreCase)
                    && originOnlyPayload.Built && originOnlyPayload.Paths.SequenceEqual([expectedOriginOnlyPath], StringComparer.OrdinalIgnoreCase)
                    && !invalidPayload.Built && !string.IsNullOrWhiteSpace(invalidPayload.Reason)
                    && win.FileDragThresholdRejectsExactDistanceForSmoke
                    && sourceCountAfter == names.Length
                    && mutableStateUntouched
                    && win.EnhancementJobsReadForSmoke == 0 && win.EnhancedCandidateCountForSmoke == 0;
                result = new FileDragOutSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "FileDrop payload validation, selected display order, threshold contract, source surfaces, and no-side-effect checks passed without starting an OS drag"
                        : "file drag-out smoke did not meet the payload or source-surface contract",
                    SmokeRoot = smokeRoot,
                    DisplayOrder = displayOrder,
                    SelectedPayload = selectedPayload,
                    OriginOnlyPayload = originOnlyPayload,
                    InvalidPayload = invalidPayload,
                    ExactThresholdRejected = win.FileDragThresholdRejectsExactDistanceForSmoke,
                    SourceCountAfter = sourceCountAfter,
                    MutableStateUntouched = mutableStateUntouched,
                    EnhancementJobsRead = win.EnhancementJobsReadForSmoke,
                    EnhancementCandidates = win.EnhancedCandidateCountForSmoke,
                };
            }
            catch (Exception ex)
            {
                result = new FileDragOutSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteFileDragOutSmokeResult(resultPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureExplorerRevealSmoke(string resultPath)
    {
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-explorer-reveal-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(smokeRoot, "Unicode 雪 images with spaces");
        string outsideFolder = Path.Combine(smokeRoot, "outside active root");
        string validName = "O'Brien 「quote safe」 image.png";
        string validPath = Path.Combine(folder, validName);
        string otherPath = Path.Combine(folder, "other.png");
        string missingPath = Path.Combine(folder, "missing source.png");
        string unsupportedPath = Path.Combine(folder, "unsupported source.txt");
        string outsidePath = Path.Combine(outsideFolder, "outside.png");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent-folders.json");
        string jobsPath = Path.Combine(smokeRoot, "enhancement-jobs.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        try
        {
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(outsideFolder);
            WriteSmokePng(validPath, 96, 64, Color.FromRgb(64, 132, 220));
            WriteSmokePng(otherPath, 64, 96, Color.FromRgb(166, 94, 210));
            WriteSmokePng(outsidePath, 48, 48, Color.FromRgb(210, 120, 70));
            File.WriteAllText(unsupportedPath, "not an image");
            File.WriteAllText(jobsPath, "{\"jobs\":[]}");
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        }
        catch (Exception ex)
        {
            WriteExplorerRevealSmokeResult(resultPath, new ExplorerRevealSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            ExplorerRevealSmokeResult result;
            try
            {
                await win.LoadFolderAsync(folder);
                bool selected = win.SelectFileNameForSmoke(validName);
                bool modalOpened = win.OpenModalForSmoke();
                win.FlushStateForSmoke();

                var sourceBefore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [validPath] = FileFingerprint(validPath),
                    [otherPath] = FileFingerprint(otherPath),
                    [unsupportedPath] = FileFingerprint(unsupportedPath),
                    [outsidePath] = FileFingerprint(outsidePath),
                };
                string stateBefore = FileFingerprint(statePath);
                string favoritesBefore = FileFingerprint(favoritesPath);
                string seenBefore = FileFingerprint(seenPath);
                string recentBefore = FileFingerprint(recentPath);
                string jobsBefore = FileFingerprint(jobsPath);

                ExplorerRevealSmokeSnapshot rightPreview = win.ActivateExplorerRevealForSmoke("right-preview", "success");
                ExplorerRevealSmokeSnapshot modal = win.ActivateExplorerRevealForSmoke("modal", "success");
                ExplorerRevealSmokeSnapshot launcherFailure = win.ActivateExplorerRevealForSmoke("right-preview", "failure");
                ExplorerRevealSmokeSnapshot launcherException = win.ActivateExplorerRevealForSmoke("modal", "throw");
                ExplorerRevealValidationSnapshot outside = win.ValidateExplorerRevealPathForSmoke(outsidePath, includeInCatalog: true);
                ExplorerRevealValidationSnapshot catalogAbsent = win.ValidateExplorerRevealPathForSmoke(otherPath, includeInCatalog: false);
                ExplorerRevealValidationSnapshot missing = win.ValidateExplorerRevealPathForSmoke(missingPath, includeInCatalog: true);
                ExplorerRevealValidationSnapshot unsupported = win.ValidateExplorerRevealPathForSmoke(unsupportedPath, includeInCatalog: true);

                string canonical = Path.GetFullPath(validPath);
                string expectedArgument = $"/select,{canonical}";
                bool rightValid = rightPreview.Launched
                    && string.Equals(rightPreview.FileName, "explorer.exe", StringComparison.OrdinalIgnoreCase)
                    && rightPreview.Arguments.SequenceEqual([expectedArgument], StringComparer.Ordinal)
                    && string.IsNullOrEmpty(rightPreview.ArgumentsText)
                    && rightPreview.UseShellExecute
                    && rightPreview.AutomationReady && rightPreview.Focused
                    && string.Equals(rightPreview.Surface, "right-preview", StringComparison.Ordinal);
                bool modalValid = modal.Launched
                    && string.Equals(modal.FileName, "explorer.exe", StringComparison.OrdinalIgnoreCase)
                    && modal.Arguments.SequenceEqual([expectedArgument], StringComparer.Ordinal)
                    && string.IsNullOrEmpty(modal.ArgumentsText)
                    && modal.UseShellExecute
                    && modal.AutomationReady && modal.Focused
                    && string.Equals(modal.Surface, "modal", StringComparison.Ordinal);
                bool genericErrors = !launcherFailure.Launched && !launcherException.Launched
                    && string.Equals(launcherFailure.Status, "Show in folder could not start Explorer. Try again.", StringComparison.Ordinal)
                    && string.Equals(launcherException.Status, launcherFailure.Status, StringComparison.Ordinal)
                    && !launcherFailure.Status.Contains(smokeRoot, StringComparison.OrdinalIgnoreCase)
                    && !launcherException.Status.Contains(validName, StringComparison.OrdinalIgnoreCase);
                bool rejections = !outside.Accepted && outside.Reason.Contains("outside", StringComparison.OrdinalIgnoreCase)
                    && !catalogAbsent.Accepted && catalogAbsent.Reason.Contains("catalog", StringComparison.OrdinalIgnoreCase)
                    && !missing.Accepted && missing.Reason.Contains("no longer exists", StringComparison.OrdinalIgnoreCase)
                    && !unsupported.Accepted && unsupported.Reason.Contains("unsupported", StringComparison.OrdinalIgnoreCase);
                bool sourceUntouched = sourceBefore.All(pair => string.Equals(pair.Value, FileFingerprint(pair.Key), StringComparison.Ordinal));
                bool mutableStateUntouched = string.Equals(stateBefore, FileFingerprint(statePath), StringComparison.Ordinal)
                    && string.Equals(favoritesBefore, FileFingerprint(favoritesPath), StringComparison.Ordinal)
                    && string.Equals(seenBefore, FileFingerprint(seenPath), StringComparison.Ordinal)
                    && string.Equals(recentBefore, FileFingerprint(recentPath), StringComparison.Ordinal)
                    && string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal);
                bool passive = win.EnhancementJobsReadForSmoke == 0 && win.EnhancedCandidateCountForSmoke == 0;
                bool quoteSafeFixture = validName.Contains('\'')
                    && validPath.Contains(' ')
                    && validPath.Any(static value => value > 127);
                bool ok = selected && modalOpened && rightValid && modalValid && genericErrors && rejections
                    && sourceUntouched && mutableStateUntouched && passive && quoteSafeFixture;

                result = new ExplorerRevealSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "guarded Explorer reveal passed both action surfaces, ArgumentList quoting, rejection, accessibility, and no-side-effect checks without starting Explorer"
                        : "Explorer reveal smoke did not meet the guarded action contract",
                    SmokeRoot = smokeRoot,
                    ValidPath = canonical,
                    ExpectedArgument = expectedArgument,
                    Selected = selected,
                    ModalOpened = modalOpened,
                    QuoteSafeFixture = quoteSafeFixture,
                    RightPreview = rightPreview,
                    Modal = modal,
                    LauncherFailure = launcherFailure,
                    LauncherException = launcherException,
                    OutsideActiveRoot = outside,
                    CatalogAbsent = catalogAbsent,
                    Missing = missing,
                    Unsupported = unsupported,
                    GenericErrors = genericErrors,
                    SourceUntouched = sourceUntouched,
                    MutableStateUntouched = mutableStateUntouched,
                    Passive = passive,
                };
            }
            catch (Exception ex)
            {
                result = new ExplorerRevealSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot, ValidPath = validPath };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteExplorerRevealSmokeResult(resultPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CaptureFolderDragInSmoke(string resultPath)
    {
        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-folder-drag-in-" + Guid.NewGuid().ToString("N"));
        string firstFolder = Path.Combine(smokeRoot, "first-folder");
        string secondFolder = Path.Combine(smokeRoot, "second-folder");
        string rejectedFile = Path.Combine(smokeRoot, "not-a-folder.txt");
        string missingFolder = Path.Combine(smokeRoot, "missing-folder");
        string statePath = Path.Combine(smokeRoot, "state.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string recentPath = Path.Combine(smokeRoot, "recent-folders.json");
        string jobsPath = Path.Combine(smokeRoot, "jobs.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousRecentPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
        string? previousJobsPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");

        try
        {
            Directory.CreateDirectory(firstFolder);
            Directory.CreateDirectory(secondFolder);
            WriteSmokePng(Path.Combine(firstFolder, "first.png"), 48, 32, Color.FromRgb(80, 120, 210));
            WriteSmokePng(Path.Combine(secondFolder, "second.png"), 48, 32, Color.FromRgb(120, 190, 100));
            File.WriteAllText(rejectedFile, "not a folder");
            File.WriteAllText(jobsPath, "{\"jobs\":[]}");
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", recentPath);
            Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", jobsPath);
        }
        catch (Exception ex)
        {
            WriteFolderDragInSmokeResult(resultPath, new FolderDragInSmokeResult { Message = ex.Message, SmokeRoot = smokeRoot });
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            FolderDragInSmokeResult result;
            try
            {
                win.SetLandingFolderSetForSmoke([firstFolder]);
                FolderDropSmokeSnapshot landingDrop = await win.DropFoldersForSmokeAsync([secondFolder, secondFolder.ToUpperInvariant(), rejectedFile, missingFolder], landing: true);
                List<string> landingFolders = win.LandingFolderSetForSmoke;
                FolderDropSmokeSnapshot rejectedDrop = await win.DropFoldersForSmokeAsync([rejectedFile, missingFolder], landing: true);

                await win.LoadFolderAsync(firstFolder);
                var sourceBefore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [Path.Combine(firstFolder, "first.png")] = FileFingerprint(Path.Combine(firstFolder, "first.png")),
                    [Path.Combine(secondFolder, "second.png")] = FileFingerprint(Path.Combine(secondFolder, "second.png")),
                };
                string jobsBefore = FileFingerprint(jobsPath);
                FolderDropSmokeSnapshot viewerDrop = await win.DropFoldersForSmokeAsync([secondFolder, secondFolder.ToUpperInvariant()], landing: false);
                bool sourceUntouched = sourceBefore.All(pair => string.Equals(pair.Value, FileFingerprint(pair.Key), StringComparison.Ordinal))
                    && Directory.EnumerateFiles(firstFolder, "*.png").Count() == 1
                    && Directory.EnumerateFiles(secondFolder, "*.png").Count() == 1;
                bool isolated = new[] { win.StatePathForSmoke, win.FavoritesPathForSmoke, win.SeenPathForSmoke, win.SharedRecentPathForSmoke, win.EnhancementJobsPathForSmoke }
                    .All(path => path.StartsWith(smokeRoot, StringComparison.OrdinalIgnoreCase));
                bool landingOk = landingDrop.Accepted && landingDrop.AddedCount == 1 && landingDrop.RejectedCount == 2
                    && landingFolders.Count == 2 && landingFolders.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2;
                bool rejectedOk = !rejectedDrop.Accepted && rejectedDrop.RejectedCount == 2 && rejectedDrop.Status.Contains("rejected", StringComparison.OrdinalIgnoreCase);
                bool viewerOk = viewerDrop.Accepted && viewerDrop.AddedCount == 1
                    && win.CurrentFolderSetForSmoke.Count == 2 && win.CatalogCountForSmoke == 2;
                bool passive = string.Equals(jobsBefore, FileFingerprint(jobsPath), StringComparison.Ordinal)
                    && win.EnhancementJobsReadForSmoke == 0 && win.EnhancedCandidateCountForSmoke == 0;
                bool ok = landingOk && rejectedOk && viewerOk && sourceUntouched && isolated && passive && win.FolderDropSurfaceContractForSmoke;
                result = new FolderDragInSmokeResult
                {
                    Ok = ok,
                    Message = ok
                        ? "Landing and viewer folder drop canonicalized existing folders, deduplicated case-insensitively, rescanned by reference, and left sources/jobs isolated"
                        : "folder drag-in smoke did not meet the folder-set, rejection, or isolation contract",
                    SmokeRoot = smokeRoot,
                    LandingDrop = landingDrop,
                    RejectedDrop = rejectedDrop,
                    ViewerDrop = viewerDrop,
                    LandingFolders = landingFolders,
                    CurrentFolders = win.CurrentFolderSetForSmoke,
                    CatalogCount = win.CatalogCountForSmoke,
                    SourceUntouched = sourceUntouched,
                    Isolated = isolated,
                    Passive = passive,
                    SurfaceContract = win.FolderDropSurfaceContractForSmoke,
                };
            }
            catch (Exception ex)
            {
                result = new FolderDragInSmokeResult { Message = ex.ToString(), SmokeRoot = smokeRoot };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH", previousRecentPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH", previousJobsPath);
            }

            WriteFolderDragInSmokeResult(resultPath, result);
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); } catch { }
            Shutdown(result.Ok ? 0 : 1);
        }, DispatcherPriority.ContextIdle);
    }

    private void CapturePreviewDecodeSmoke(string resultPath, string[] args)
    {
        string? folder = ArgValue(args, "--folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            WritePreviewDecodeSmokeResult(resultPath, new PreviewDecodeSmokeResult { Message = "missing required --folder" });
            Shutdown(1);
            return;
        }

        string fullFolder = Path.GetFullPath(folder);
        string[] fixtureNames = GetSmokeImageFileNames(fullFolder);
        if (fixtureNames.Length < 2)
        {
            WritePreviewDecodeSmokeResult(resultPath, new PreviewDecodeSmokeResult
            {
                Message = "preview decode smoke requires at least two fixture images",
                Folder = fullFolder,
            });
            Shutdown(1);
            return;
        }

        string targetName = ArgValue(args, "--select-name") ?? fixtureNames[^1];
        string? rapidSelectionName = fixtureNames.FirstOrDefault(name => !string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(rapidSelectionName))
        {
            WritePreviewDecodeSmokeResult(resultPath, new PreviewDecodeSmokeResult
            {
                Message = "preview decode smoke could not choose a distinct rapid selection fixture",
                Folder = fullFolder,
                TargetName = targetName,
            });
            Shutdown(1);
            return;
        }

        string smokeRoot = Path.Combine(Path.GetTempPath(), "photoviewer-wpf-preview-decode-smoke-" + Guid.NewGuid().ToString("N"));
        string statePath = Path.Combine(smokeRoot, "state.json");
        string seenPath = Path.Combine(smokeRoot, "seen.json");
        string favoritesPath = Path.Combine(smokeRoot, "favorites.json");
        string? previousStatePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
        string? previousSeenPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");
        string? previousFavoritesPath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", statePath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", seenPath);
        Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", favoritesPath);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var win = HiddenWindow();
        win.Show();
        win.Dispatcher.InvokeAsync(async () =>
        {
            PreviewDecodeSmokeResult result;
            try
            {
                await win.LoadFolderAsync(fullFolder);
                bool rapidSelected = win.SelectFileNameForSmoke(rapidSelectionName);
                PreviewDecodeSmokeSnapshot snapshot = await win.SelectPreviewForSmokeAsync(targetName);
                bool targetSelected = string.Equals(win.SelectedFileNameForSmoke, targetName, StringComparison.OrdinalIgnoreCase);
                bool ok = rapidSelected
                    && snapshot.Selected
                    && snapshot.DeferredDecodeApplied
                    && snapshot.PreviewSourcePresent
                    && snapshot.StableLatestSelection
                    && targetSelected;

                result = new PreviewDecodeSmokeResult
                {
                    Ok = ok,
                    Message = ok ? snapshot.Message : "preview decode smoke did not keep the decoded image synchronized with the latest selection",
                    Folder = fullFolder,
                    StatePath = statePath,
                    TargetName = targetName,
                    RapidSelectionName = rapidSelectionName,
                    RapidSelectionApplied = rapidSelected,
                    TargetSelected = targetSelected,
                    ExpectedPath = snapshot.ExpectedPath,
                    SelectionImmediateMs = snapshot.SelectionImmediateMs,
                    PreviewImmediateMs = snapshot.PreviewImmediateMs,
                    DeferredDecodeMs = snapshot.DeferredDecodeMs,
                    DeferredDecodeApplied = snapshot.DeferredDecodeApplied,
                    PreviewSourcePresent = snapshot.PreviewSourcePresent,
                    StableLatestSelection = snapshot.StableLatestSelection,
                };
            }
            catch (Exception ex)
            {
                result = new PreviewDecodeSmokeResult
                {
                    Message = ex.Message,
                    Folder = fullFolder,
                    StatePath = statePath,
                    TargetName = targetName,
                    RapidSelectionName = rapidSelectionName,
                };
            }
            finally
            {
                win.Close();
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH", previousStatePath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH", previousSeenPath);
                Environment.SetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH", previousFavoritesPath);
            }

            WritePreviewDecodeSmokeResult(resultPath, result);
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

    private static string FileFingerprint(string path)
    {
        if (!File.Exists(path))
            return "missing";

        byte[] bytes = File.ReadAllBytes(path);
        return $"{bytes.LongLength}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))}";
    }

    private static string FolderFingerprint(string path)
    {
        if (!Directory.Exists(path))
            return "missing";

        string manifest = string.Join("\n", Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .OrderBy(static candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => $"{Path.GetRelativePath(path, candidate)}:{FileFingerprint(candidate)}"));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(manifest)));
    }

    private static bool NoPersistenceResidue(string root)
        => Directory.Exists(root)
            && !Directory.EnumerateFiles(root, "*.lock", SearchOption.AllDirectories).Any()
            && !Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any();

    private static void WriteLegacyDateFilterState(string path, string preset, string? from, string? to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var state = new Dictionary<string, object?>
        {
            ["Version"] = 2,
            ["DatePreset"] = preset,
            ["DateFrom"] = from,
            ["DateTo"] = to,
            ["futureDateMarker"] = "preserve-me",
        };
        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool PersistedStateHasStringProperty(string path, string propertyName, string expectedValue)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), expectedValue, StringComparison.Ordinal);
        }
        catch
        {
            return false;
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

    private static bool FavoriteFileContainsPath(string favoritesPath, string selectedPath)
    {
        try
        {
            if (!File.Exists(favoritesPath))
                return false;

            string normalizedSelected = NormalizeFavoritePath(selectedPath);
            using var document = JsonDocument.Parse(File.ReadAllText(favoritesPath));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject().Any(property => string.Equals(
                    NormalizeFavoritePath(property.Name),
                    normalizedSelected,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
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

    private static FolderBucketSmokeFixture PrepareFolderBucketSmokeFixture(string smokeRoot)
    {
        string folderA = Path.Combine(smokeRoot, "bucket-a");
        string folderB = Path.Combine(smokeRoot, "bucket-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        var inputs = new[]
        {
            new { Folder = folderA, Name = "alpha-a.png", ModifiedUtc = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(52, 152, 219) },
            new { Folder = folderA, Name = "bravo-a.png", ModifiedUtc = new DateTime(2026, 7, 10, 11, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(46, 204, 113) },
            new { Folder = folderB, Name = "charlie-b.png", ModifiedUtc = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(231, 76, 60) },
            new { Folder = folderB, Name = "delta-b.png", ModifiedUtc = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(155, 89, 182) },
        };

        foreach (var input in inputs)
        {
            string path = Path.Combine(input.Folder, input.Name);
            WriteSmokePng(path, 128, 96, input.Color);
            File.SetLastWriteTimeUtc(path, input.ModifiedUtc);
        }

        return new FolderBucketSmokeFixture
        {
            FolderA = folderA,
            FolderB = folderB,
            FolderASelectedName = "bravo-a.png",
            FolderACount = 2,
            FolderBCount = 2,
            TotalCount = 4,
        };
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
                CreatedUtc = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc),
            },
            new
            {
                SourceName = fixtureNames[1],
                TargetName = "bravo-sort" + Path.GetExtension(fixtureNames[1]).ToLowerInvariant(),
                ModifiedUtc = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc),
            },
            new
            {
                SourceName = fixtureNames[2],
                TargetName = "charlie-sort" + Path.GetExtension(fixtureNames[2]).ToLowerInvariant(),
                ModifiedUtc = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc),
            },
        };

        foreach (var input in inputs)
        {
            string source = Path.Combine(sourceFolder, input.SourceName);
            string destination = Path.Combine(target, input.TargetName);
            File.Copy(source, destination, overwrite: true);
            File.SetLastWriteTimeUtc(destination, input.ModifiedUtc);
            File.SetCreationTimeUtc(destination, input.CreatedUtc);
        }

        return target;
    }

    private static DateFilterSmokeFixture PrepareDateFilterSmokeFolder(string smokeRoot)
    {
        string target = Path.Combine(smokeRoot, "date-filter-folder");
        Directory.CreateDirectory(target);

        DateTime today = DateTime.Today;
        DateTime thisYearOnlyDate = today.AddDays(-45);
        if (thisYearOnlyDate.Year != today.Year)
            thisYearOnlyDate = new DateTime(today.Year, 1, 1, 9, 0, 0);

        var inputs = new List<DateFilterSmokeImage>
        {
            new("alpha-today.png", today.AddHours(12), Color.FromRgb(52, 152, 219)),
            new("bravo-7d.png", today.AddDays(-6).AddHours(11), Color.FromRgb(46, 204, 113)),
            new("charlie-30d.png", today.AddDays(-20).AddHours(10), Color.FromRgb(231, 76, 60)),
            new("delta-this-year.png", thisYearOnlyDate, Color.FromRgb(155, 89, 182)),
            new("echo-previous-year.png", new DateTime(today.Year - 1, 12, 31, 9, 0, 0), Color.FromRgb(241, 196, 15)),
        };

        foreach (var input in inputs)
        {
            string path = Path.Combine(target, input.Name);
            WriteSmokePng(path, 128, 96, input.Color);
            File.SetLastWriteTime(path, input.ModifiedLocal);
            File.SetCreationTime(path, input.ModifiedLocal);
        }

        DateTime sevenFrom = today.AddDays(-6);
        DateTime thirtyFrom = today.AddDays(-29);
        DateTime yearFrom = new(today.Year, 1, 1);
        DateTime manualFrom = today.AddDays(-20);
        DateTime manualTo = today.AddDays(-6);

        return new DateFilterSmokeFixture
        {
            Folder = target,
            TodayName = inputs[0].Name,
            SevenDayName = inputs[1].Name,
            ThirtyDayName = inputs[2].Name,
            ThisYearName = inputs[3].Name,
            PreviousYearName = inputs[4].Name,
            AllExpected = ExpectedDateFilterNames(inputs, null, null),
            TodayExpected = ExpectedDateFilterNames(inputs, today, today),
            SevenDayExpected = ExpectedDateFilterNames(inputs, sevenFrom, today),
            ThirtyDayExpected = ExpectedDateFilterNames(inputs, thirtyFrom, today),
            ThisYearExpected = ExpectedDateFilterNames(inputs, yearFrom, today),
            ManualFrom = manualFrom,
            ManualTo = manualTo,
            ManualRangeExpected = ExpectedDateFilterNames(inputs, manualFrom, manualTo),
            ManualFromOnlyExpected = ExpectedDateFilterNames(inputs, manualFrom, null),
            ManualToOnlyExpected = ExpectedDateFilterNames(inputs, null, manualTo),
        };
    }

    private static FavoriteFilterSmokeFixture PrepareFavoriteFilterSmokeFolder(string smokeRoot)
    {
        string target = Path.Combine(smokeRoot, "favorite-filter-folder");
        Directory.CreateDirectory(target);

        var inputs = new[]
        {
            new { Name = "alpha-unrated.png", ModifiedUtc = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(149, 165, 166) },
            new { Name = "bravo-level1.png", ModifiedUtc = new DateTime(2026, 7, 10, 11, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(52, 152, 219) },
            new { Name = "charlie-level3.png", ModifiedUtc = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(46, 204, 113) },
            new { Name = "delta-level5.png", ModifiedUtc = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), Color = Color.FromRgb(241, 196, 15) },
        };

        foreach (var input in inputs)
        {
            string path = Path.Combine(target, input.Name);
            WriteSmokePng(path, 128, 96, input.Color);
            File.SetLastWriteTimeUtc(path, input.ModifiedUtc);
        }

        return new FavoriteFilterSmokeFixture
        {
            Folder = target,
            UnratedName = "alpha-unrated.png",
            Level1Name = "bravo-level1.png",
            Level3Name = "charlie-level3.png",
            Level5Name = "delta-level5.png",
            AllExpected = ["alpha-unrated.png", "bravo-level1.png", "charlie-level3.png", "delta-level5.png"],
            FavoritesLv1Expected = ["bravo-level1.png", "charlie-level3.png", "delta-level5.png"],
            FavoritesLv3Expected = ["charlie-level3.png", "delta-level5.png"],
            FavoritesLv5Expected = ["delta-level5.png"],
            UnratedExpected = ["alpha-unrated.png"],
        };
    }

    private static List<string> ExpectedDateFilterNames(IEnumerable<DateFilterSmokeImage> inputs, DateTime? from, DateTime? to)
    {
        return inputs
            .Where(input => !from.HasValue || input.ModifiedLocal.Date >= from.Value.Date)
            .Where(input => !to.HasValue || input.ModifiedLocal.Date <= to.Value.Date)
            .OrderByDescending(input => input.ModifiedLocal)
            .ThenBy(input => input.Name, StringComparer.OrdinalIgnoreCase)
            .Select(input => input.Name)
            .ToList();
    }

    private static string PrepareAspectSmokeFolder(string smokeRoot)
    {
        string target = Path.Combine(smokeRoot, "aspect-folder");
        Directory.CreateDirectory(target);

        var inputs = new[]
        {
            new
            {
                Name = "alpha-landscape.png",
                Width = 150,
                Height = 100,
                Color = Color.FromRgb(52, 152, 219),
                ModifiedUtc = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc),
            },
            new
            {
                Name = "bravo-square.png",
                Width = 120,
                Height = 120,
                Color = Color.FromRgb(46, 204, 113),
                ModifiedUtc = new DateTime(2026, 7, 10, 11, 0, 0, DateTimeKind.Utc),
            },
            new
            {
                Name = "charlie-portrait.png",
                Width = 100,
                Height = 150,
                Color = Color.FromRgb(231, 76, 60),
                ModifiedUtc = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc),
            },
        };

        foreach (var input in inputs)
        {
            string path = Path.Combine(target, input.Name);
            WriteSmokePng(path, input.Width, input.Height, input.Color);
            File.SetLastWriteTimeUtc(path, input.ModifiedUtc);
        }

        return target;
    }

    private static void WriteSmokePng(string path, int width, int height, Color color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = color.B;
            pixels[offset + 1] = color.G;
            pixels[offset + 2] = color.R;
            pixels[offset + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void WritePngTextFixture(string path, string keyword, string text, Color color)
    {
        WriteSmokePng(path, 128, 96, color);
        byte[] png = File.ReadAllBytes(path);
        int insertOffset = FindPngIdatOffset(png);
        byte[] keywordBytes = System.Text.Encoding.Latin1.GetBytes(keyword);
        byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        byte[] data = new byte[keywordBytes.Length + 1 + textBytes.Length];
        Buffer.BlockCopy(keywordBytes, 0, data, 0, keywordBytes.Length);
        Buffer.BlockCopy(textBytes, 0, data, keywordBytes.Length + 1, textBytes.Length);
        byte[] chunk = BuildPngTextChunk(data);
        byte[] combined = new byte[png.Length + chunk.Length];
        Buffer.BlockCopy(png, 0, combined, 0, insertOffset);
        Buffer.BlockCopy(chunk, 0, combined, insertOffset, chunk.Length);
        Buffer.BlockCopy(png, insertOffset, combined, insertOffset + chunk.Length, png.Length - insertOffset);
        File.WriteAllBytes(path, combined);
    }

    private static int FindPngIdatOffset(byte[] png)
    {
        const int signatureLength = 8;
        int offset = signatureLength;
        while (offset + 12 <= png.Length)
        {
            int length = ReadPngBigEndianInt32(png, offset);
            if (length < 0 || offset + length + 12 > png.Length)
                break;
            bool idat = png[offset + 4] == (byte)'I'
                && png[offset + 5] == (byte)'D'
                && png[offset + 6] == (byte)'A'
                && png[offset + 7] == (byte)'T';
            if (idat)
                return offset;
            offset += length + 12;
        }

        throw new InvalidOperationException("smoke PNG did not contain an IDAT chunk");
    }

    private static byte[] BuildPngTextChunk(byte[] data)
    {
        byte[] chunk = new byte[data.Length + 12];
        WritePngBigEndianInt32(chunk, 0, data.Length);
        chunk[4] = (byte)'t';
        chunk[5] = (byte)'E';
        chunk[6] = (byte)'X';
        chunk[7] = (byte)'t';
        Buffer.BlockCopy(data, 0, chunk, 8, data.Length);
        uint crc = ComputePngCrc(chunk.AsSpan(4, data.Length + 4));
        WritePngBigEndianInt32(chunk, data.Length + 8, unchecked((int)crc));
        return chunk;
    }

    private static int ReadPngBigEndianInt32(byte[] value, int offset)
        => (value[offset] << 24) | (value[offset + 1] << 16) | (value[offset + 2] << 8) | value[offset + 3];

    private static void WritePngBigEndianInt32(byte[] value, int offset, int number)
    {
        value[offset] = unchecked((byte)(number >> 24));
        value[offset + 1] = unchecked((byte)(number >> 16));
        value[offset + 2] = unchecked((byte)(number >> 8));
        value[offset + 3] = unchecked((byte)number);
    }

    private static uint ComputePngCrc(ReadOnlySpan<byte> data)
    {
        uint crc = 0xffffffff;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
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

    private static void WriteCatalogStressSmokeResult(string path, CatalogStressSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteShortcutTypingSmokeResult(string path, ShortcutTypingSmokeResult result)
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

    private static void WriteModalTransformSmokeResult(string path, ModalTransformSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteModalInteractionSmokeResult(string path, ModalInteractionSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteModalPanSmokeResult(string path, ModalPanSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteSelectionSmokeResult(string path, SelectionSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WritePreviewTabsSmokeResult(string path, PreviewTabsSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WritePreviewTabReorderSmokeResult(string path, PreviewTabReorderSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WritePreviewTabHoverSmokeResult(string path, PreviewTabHoverSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WritePreviewDecodeSmokeResult(string path, PreviewDecodeSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WritePngMetadataSmokeResult(string path, PngMetadataSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WritePromptTagSearchSmokeResult(string path, PromptTagSearchSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteFileDragOutSmokeResult(string path, FileDragOutSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteExplorerRevealSmokeResult(string path, ExplorerRevealSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteRapidUiStateSmokeResult(string path, RapidUiStateSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteShutdownStateSmokeResult(string path, ShutdownStateSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteRecentWriteOwnershipSmokeResult(string path, RecentWriteOwnershipSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteFolderDragInSmokeResult(string path, FolderDragInSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteFormatSmokeResult(string path, FormatSmokeResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
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

    private static void WriteFavoriteFilterSmokeResult(string path, FavoriteFilterSmokeResult result)
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

    private static void WriteFolderBucketSmokeResult(string path, FolderBucketSmokeResult result)
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

    private static void WriteAspectSmokeResult(string path, AspectSmokeResult result)
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

    private static void WriteDateFilterSmokeResult(string path, DateFilterSmokeResult result)
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

    private static void WriteModalEnhancedSmokeResult(string path, ModalEnhancedSmokeResult result)
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

    private sealed class ShortcutTypingSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? StatePath { get; init; }
        public string? FavoritesPath { get; init; }
        public string? SelectedName { get; init; }
        public bool SearchFocused { get; init; }
        public bool EditableFocus { get; init; }
        public bool FHandledWhileTyping { get; init; }
        public bool UHandledWhileTyping { get; init; }
        public int FavoriteBefore { get; init; }
        public int FavoriteAfterTyping { get; init; }
        public bool QueryRecorded { get; init; }
        public bool CardsFocused { get; init; }
        public bool NonEditableFocus { get; init; }
        public bool FHandledOutsideTyping { get; init; }
        public bool UHandledOutsideTyping { get; init; }
        public int FavoriteAfterF { get; init; }
        public int FavoriteAfterU { get; init; }
    }

    private sealed class PreviewDecodeSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? StatePath { get; init; }
        public string? TargetName { get; init; }
        public string? RapidSelectionName { get; init; }
        public bool RapidSelectionApplied { get; init; }
        public bool TargetSelected { get; init; }
        public string? ExpectedPath { get; init; }
        public long SelectionImmediateMs { get; init; }
        public long PreviewImmediateMs { get; init; }
        public long DeferredDecodeMs { get; init; }
        public bool DeferredDecodeApplied { get; init; }
        public bool PreviewSourcePresent { get; init; }
        public bool StableLatestSelection { get; init; }
    }

    private sealed class PngMetadataSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public string? Folder { get; init; }
        public string? ValidPath { get; init; }
        public string? MissingPath { get; init; }
        public string? IgnoredPath { get; init; }
        public PngMetadataSmokeSnapshot? Valid { get; init; }
        public MetadataCopySmokeSnapshot? ValidCopy { get; init; }
        public ModalMetadataSmokeSnapshot? InitialModal { get; init; }
        public ModalMetadataSmokeSnapshot? ValidModal { get; init; }
        public ModalMetadataSmokeSnapshot? NegativeTabModal { get; init; }
        public ModalMetadataSmokeSnapshot? SettingsTabModal { get; init; }
        public ModalMetadataSmokeSnapshot? PromptTabModal { get; init; }
        public ModalMetadataSmokeSnapshot? HiddenModal { get; init; }
        public ModalMetadataSmokeSnapshot? DoubleClickModal { get; init; }
        public ModalMetadataSmokeSnapshot? DoubleClickHiddenModal { get; init; }
        public PngMetadataSmokeSnapshot? Missing { get; init; }
        public MetadataCopySmokeSnapshot? MissingCopy { get; init; }
        public bool IgnoredTextSkipped { get; init; }
        public bool FirstSelectionStarted { get; init; }
        public bool LatestSelectionStarted { get; init; }
        public PngMetadataSmokeSnapshot? Latest { get; init; }
        public MetadataCopySmokeSnapshot? LatestCopy { get; init; }
        public bool LatestSelectionStable { get; init; }
    }

    private sealed class PromptTagSearchSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public string? TaggedPath { get; init; }
        public bool MetadataApplied { get; init; }
        public bool MetadataRefreshed { get; init; }
        public bool OpenedModal { get; init; }
        public bool SidebarVisible { get; init; }
        public List<string> InitialTags { get; init; } = [];
        public bool InitialAccessibilityReady { get; init; }
        public PromptTagSearchSmokeSnapshot? Appended { get; init; }
        public PromptTagSearchSmokeSnapshot? Deduped { get; init; }
        public bool SourceUntouched { get; init; }
        public bool SearchPersisted { get; init; }
        public string? ReloadedQuery { get; init; }
        public List<string> ReloadedNames { get; init; } = [];
        public bool PromptFallbackVisible { get; init; }
        public int EnhancementJobsBefore { get; init; }
        public int EnhancementJobsAfter { get; init; }
        public int EnhancementCandidatesBefore { get; init; }
        public int EnhancementCandidatesAfter { get; init; }
    }

    private sealed class FileDragOutSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public List<string> DisplayOrder { get; init; } = [];
        public FileDragOutSmokeSnapshot? SelectedPayload { get; init; }
        public FileDragOutSmokeSnapshot? OriginOnlyPayload { get; init; }
        public FileDragOutSmokeSnapshot? InvalidPayload { get; init; }
        public bool ExactThresholdRejected { get; init; }
        public int SourceCountAfter { get; init; }
        public bool MutableStateUntouched { get; init; }
        public int EnhancementJobsRead { get; init; }
        public int EnhancementCandidates { get; init; }
    }

    private sealed class ExplorerRevealSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public string? ValidPath { get; init; }
        public string? ExpectedArgument { get; init; }
        public bool Selected { get; init; }
        public bool ModalOpened { get; init; }
        public bool QuoteSafeFixture { get; init; }
        public ExplorerRevealSmokeSnapshot? RightPreview { get; init; }
        public ExplorerRevealSmokeSnapshot? Modal { get; init; }
        public ExplorerRevealSmokeSnapshot? LauncherFailure { get; init; }
        public ExplorerRevealSmokeSnapshot? LauncherException { get; init; }
        public ExplorerRevealValidationSnapshot? OutsideActiveRoot { get; init; }
        public ExplorerRevealValidationSnapshot? CatalogAbsent { get; init; }
        public ExplorerRevealValidationSnapshot? Missing { get; init; }
        public ExplorerRevealValidationSnapshot? Unsupported { get; init; }
        public bool GenericErrors { get; init; }
        public bool SourceUntouched { get; init; }
        public bool MutableStateUntouched { get; init; }
        public bool Passive { get; init; }
    }

    private sealed class FolderDragInSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public FolderDropSmokeSnapshot? LandingDrop { get; init; }
        public FolderDropSmokeSnapshot? RejectedDrop { get; init; }
        public FolderDropSmokeSnapshot? ViewerDrop { get; init; }
        public List<string> LandingFolders { get; init; } = [];
        public List<string> CurrentFolders { get; init; } = [];
        public int CatalogCount { get; init; }
        public bool SourceUntouched { get; init; }
        public bool Isolated { get; init; }
        public bool Passive { get; init; }
        public bool SurfaceContract { get; init; }
    }

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
        string? PersistedPath,
        int FilteredCount = 0,
        string? LastName = null,
        bool WrappedLastToFirst = false,
        string? WrappedFirstName = null,
        bool WrappedFirstToLast = false,
        string? WrappedLastName = null);

    private sealed record ModalTransformSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        int SelectIndex,
        bool Selected = false,
        bool ModalVisible = false,
        ModalTransformSnapshot Initial = default,
        bool Flipped = false,
        ModalTransformSnapshot AfterFlip = default,
        bool ShortcutZoomed = false,
        ModalTransformSnapshot AfterShortcut = default,
        bool WheelZoomed = false,
        ModalTransformSnapshot AfterWheel = default,
        bool Reset = false,
        ModalTransformSnapshot AfterReset = default,
        bool MovedNext = false,
        string? StartPath = null,
        string? NextPath = null,
        ModalTransformSnapshot AfterNavigation = default,
        bool Closed = false,
        ModalTransformSnapshot AfterClose = default);

    private sealed class ModalInteractionSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public bool Accessibility { get; init; }
        public bool ChromeHidden { get; init; }
        public bool ChromeShown { get; init; }
        public bool ControlDidNotToggle { get; init; }
        public bool DoubleClickMetadata { get; init; }
        public bool EdgeNext { get; init; }
        public bool SwipeNext { get; init; }
        public bool SmallSwipeIgnored { get; init; }
        public bool ZoomedSwipeBlocked { get; init; }
        public bool Feedback { get; init; }
        public bool EscapeClosed { get; init; }
    }

    private sealed record ModalPanSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        int SelectIndex,
        bool Selected = false,
        bool ModalVisible = false,
        ModalTransformSnapshot Zoomed = default,
        bool PannedPositive = false,
        ModalTransformSnapshot AfterPositivePan = default,
        bool PannedNegative = false,
        ModalTransformSnapshot AfterNegativePan = default,
        bool Reset = false,
        ModalTransformSnapshot AfterReset = default,
        bool MovedNext = false,
        string? StartPath = null,
        string? NextPath = null,
        ModalTransformSnapshot AfterNavigation = default);

    private sealed record SelectionSmokeResult(
        bool Ok,
        string Message,
        string? Folder,
        string? StatePath,
        List<string>? FixtureNames = null,
        bool RangeSelected = false,
        int RangeCount = 0,
        List<string>? RangeNames = null,
        string? PrimaryAfterRange = null,
        int CardsAfterRange = 0,
        int RowsAfterRange = 0,
        bool ToggledRemoved = false,
        int AfterRemoveCount = 0,
        List<string>? AfterRemoveNames = null,
        string? PrimaryAfterRemove = null,
        bool ToggledAdded = false,
        int AfterAddCount = 0,
        List<string>? AfterAddNames = null,
        string? PrimaryAfterAdd = null,
        bool ListMode = false,
        bool GridMode = false,
        int CardsAfterMode = 0,
        int RowsAfterMode = 0,
        string? PersistedSelectedPath = null);

    private sealed class PreviewTabsSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? StatePath { get; init; }
        public string? SeenPath { get; init; }
        public string? FavoritesPath { get; init; }
        public string? FirstName { get; init; }
        public string? SecondName { get; init; }
        public int CountAfterFirst { get; init; }
        public string? ActiveAfterFirst { get; init; }
        public List<string> TabsAfterFirst { get; init; } = [];
        public int CountAfterSecond { get; init; }
        public string? ActiveAfterSecond { get; init; }
        public List<string> TabsAfterSecond { get; init; } = [];
        public bool PinnedFirst { get; init; }
        public bool FirstPinnedAfterToggle { get; init; }
        public int PinnedCountAfterToggle { get; init; }
        public bool FirstPinPersisted { get; init; }
        public bool HoverFirstShown { get; init; }
        public bool HoverVisibleAfterShow { get; init; }
        public string? HoverNameAfterShow { get; init; }
        public string? HoverPathAfterShow { get; init; }
        public string? ActiveAfterHoverShow { get; init; }
        public string? SelectedAfterHoverShow { get; init; }
        public bool HoverFirstHidden { get; init; }
        public bool HoverVisibleAfterHide { get; init; }
        public string? HoverNameAfterHide { get; init; }
        public string? ActiveAfterActivateFirst { get; init; }
        public string? SelectedAfterActivateFirst { get; init; }
        public int CountAfterCloseFirst { get; init; }
        public int ClosedStackAfterCloseFirst { get; init; }
        public string? ActiveAfterCloseFirst { get; init; }
        public string? SelectedAfterCloseFirst { get; init; }
        public bool PinSurvivedClose { get; init; }
        public int CountAfterRestore { get; init; }
        public string? ActiveAfterRestore { get; init; }
        public string? SelectedAfterRestore { get; init; }
        public List<string> TabsAfterRestore { get; init; } = [];
        public bool FirstPinnedAfterRestore { get; init; }
        public int CountAfterCloseAll { get; init; }
        public int ClosedStackAfterCloseAll { get; init; }
        public bool PinSurvivedCloseAll { get; init; }
        public int CountAfterRestoreAll { get; init; }
        public string? ActiveAfterRestoreAll { get; init; }
        public string? SelectedAfterRestoreAll { get; init; }
        public int TabCountAfterReload { get; init; }
        public List<string> TabsAfterReload { get; init; } = [];
        public string? ActiveAfterReload { get; init; }
        public string? SelectedAfterReload { get; init; }
        public bool ModalClosedAfterReload { get; init; }
        public bool SelectedFirstAfterReload { get; init; }
        public bool OpenedFirstAfterReload { get; init; }
        public bool FirstPinnedAfterReload { get; init; }
        public int PinnedCountAfterReload { get; init; }
    }

    private sealed class PreviewTabReorderSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public bool Accessibility { get; init; }
        public bool KeyboardRight { get; init; }
        public bool KeyboardLeft { get; init; }
        public bool DragReordered { get; init; }
        public bool PersistedOrderActivePin { get; init; }
        public bool ReloadPreserved { get; init; }
        public bool InvalidRecovery { get; init; }
        public bool MiddleClose { get; init; }
        public bool RestoreFocus { get; init; }
        public List<string> OrderBeforeReload { get; init; } = [];
        public string? ActiveBeforeReload { get; init; }
    }

    private sealed class PreviewTabHoverSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public int HeartbeatCount { get; init; }
        public bool SlowCompletionDiscarded { get; init; }
        public bool NewestCompletionApplied { get; init; }
        public string? NewestHoverName { get; init; }
        public string? NewestBitmapName { get; init; }
        public bool SelectionAndActiveStable { get; init; }
        public bool CorruptCompletionFailed { get; init; }
        public bool CorruptInlineRecovery { get; init; }
        public int DecodeStartCount { get; init; }
        public int DecodeFailureCount { get; init; }
        public string Status { get; init; } = "";
    }

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

    private sealed class FavoriteFilterSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? FavoritesPath { get; init; }
        public string? StatePath { get; init; }
        public string? SeenPath { get; init; }
        public int AllCount { get; init; }
        public List<string> AllOrder { get; init; } = [];
        public int StoreCountAfterAssign { get; init; }
        public int FavoritesLv1Count { get; init; }
        public List<string> FavoritesLv1Order { get; init; } = [];
        public int FavoritesLv3Count { get; init; }
        public List<string> FavoritesLv3Order { get; init; } = [];
        public string? SelectedAfterLv3 { get; init; }
        public int FavoritesLv5Count { get; init; }
        public List<string> FavoritesLv5Order { get; init; } = [];
        public int UnratedCount { get; init; }
        public List<string> UnratedOrder { get; init; } = [];
        public bool UnratedIsExclusive { get; init; }
        public int ClearCount { get; init; }
        public List<string> ClearOrder { get; init; } = [];
        public bool? PersistedFavoriteOnly { get; init; }
        public bool? PersistedUnfavoriteOnly { get; init; }
        public int? PersistedFavoriteFilterLevel { get; init; }
        public int PersistedLevel1 { get; init; }
        public int PersistedLevel3 { get; init; }
        public int PersistedLevel5 { get; init; }
        public int PersistedUnrated { get; init; }
        public bool RestoredFavoriteOnly { get; init; }
        public bool RestoredUnfavoriteOnly { get; init; }
        public int RestoredFavoriteLevel { get; init; }
        public int RestoredCount { get; init; }
        public List<string> RestoredOrder { get; init; } = [];
        public string? RestoredSelected { get; init; }
        public int ReloadUnratedCount { get; init; }
        public List<string> ReloadUnratedOrder { get; init; } = [];
    }

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

    private sealed class FolderBucketSmokeFixture
    {
        public string FolderA { get; init; } = "";
        public string FolderB { get; init; } = "";
        public string FolderASelectedName { get; init; } = "";
        public int FolderACount { get; init; }
        public int FolderBCount { get; init; }
        public int TotalCount { get; init; }
    }

    private sealed class FolderBucketSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? ProjectRoot { get; init; }
        public string? FolderA { get; init; }
        public string? FolderB { get; init; }
        public string? StatePath { get; init; }
        public string? FavoritesPath { get; init; }
        public string? SeenPath { get; init; }
        public int AllCount { get; init; }
        public int BucketCount { get; init; }
        public List<string> BucketKeys { get; init; } = [];
        public bool DefaultExpanded { get; init; }
        public bool FolderAccessibility { get; init; }
        public bool SelectedActionsOk { get; init; }
        public bool SelectedFolderA { get; init; }
        public int AfterHideFolderA { get; init; }
        public List<string> AfterHideFolderAOrder { get; init; } = [];
        public string? SelectedAfterHideFolderA { get; init; }
        public int AfterShowFolderA { get; init; }
        public bool SelectedFolderAAgain { get; init; }
        public int AfterHideFolderB { get; init; }
        public string? SelectedAfterHideFolderB { get; init; }
        public int AfterInvert { get; init; }
        public List<string> AfterInvertOrder { get; init; } = [];
        public int AfterShowAll { get; init; }
        public int AfterHideAll { get; init; }
        public List<string> PersistedHiddenBuckets { get; init; } = [];
        public List<string> SelectedBeforePersistence { get; init; } = [];
        public List<string> PersistedSelectedBuckets { get; init; } = [];
        public bool CollapsedAfterReload { get; init; }
        public List<string> SelectedBucketsAfterReload { get; init; } = [];
        public bool SortSelectionPreserved { get; init; }
        public bool InvalidLegacyFallback { get; init; }
        public int RestoredCount { get; init; }
        public int RestoredHiddenCount { get; init; }
        public List<string> RestoredHiddenKeys { get; init; } = [];
        public List<string> RestoredOrder { get; init; } = [];
        public int FavoriteCountAfterReload { get; init; }
        public int SeenBefore { get; init; }
        public int SeenAfterReload { get; init; }
        public bool StateIsolated { get; init; }
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

    private sealed class AspectSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? StatePath { get; init; }
        public int FilteredCount { get; init; }
        public List<string> InitialOrder { get; init; } = [];
        public List<string> SquareOrder { get; init; } = [];
        public List<string> PortraitOrder { get; init; } = [];
        public List<string> RestoredOrder { get; init; } = [];
        public DisplayStyleMetrics? Original { get; init; }
        public DisplayStyleMetrics? Square { get; init; }
        public DisplayStyleMetrics? Portrait { get; init; }
        public DisplayStyleMetrics? PortraitZoomed { get; init; }
        public DisplayStyleMetrics? RestoredOriginalRuntime { get; init; }
        public DisplayStyleMetrics? Restored { get; init; }
        public string? SelectedBefore { get; init; }
        public string? SelectedAfterSquare { get; init; }
        public string? SelectedAfterPortrait { get; init; }
        public string? PersistedAspect { get; init; }
        public bool SquareChanged { get; init; }
        public bool PortraitChanged { get; init; }
        public bool OriginalChanged { get; init; }
        public bool Zoomed { get; init; }
        public bool PersistedTargetSet { get; init; }
        public bool SquareShape { get; init; }
        public bool PortraitShape { get; init; }
        public bool OriginalShape { get; init; }
        public bool OrderStable { get; init; }
        public bool SelectionStable { get; init; }
        public bool ZoomComposes { get; init; }
        public bool Persistence { get; init; }
    }

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
        public List<string> ExpectedCreatedNewest { get; init; } = [];
        public List<string> ExpectedCreatedOldest { get; init; } = [];
        public List<string> NewestOrder { get; init; } = [];
        public List<string> OldestOrder { get; init; } = [];
        public List<string> NameOrder { get; init; } = [];
        public List<string> CreatedNewestOrder { get; init; } = [];
        public List<string> CreatedOldestOrder { get; init; } = [];
        public List<string> RandomOrderBefore { get; init; } = [];
        public List<string> RandomOrderAfter { get; init; } = [];
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
        public string? RandomSeedBefore { get; init; }
        public string? RandomSeedAfter { get; init; }
        public string? RestoredRandomSeed { get; init; }
        public bool RandomPersistence { get; init; }
    }

    private sealed record DateFilterSmokeImage(string Name, DateTime ModifiedLocal, Color Color);

    private sealed class DateFilterSmokeFixture
    {
        public string Folder { get; init; } = "";
        public string TodayName { get; init; } = "";
        public string SevenDayName { get; init; } = "";
        public string ThirtyDayName { get; init; } = "";
        public string ThisYearName { get; init; } = "";
        public string PreviousYearName { get; init; } = "";
        public List<string> AllExpected { get; init; } = [];
        public List<string> TodayExpected { get; init; } = [];
        public List<string> SevenDayExpected { get; init; } = [];
        public List<string> ThirtyDayExpected { get; init; } = [];
        public List<string> ThisYearExpected { get; init; } = [];
        public DateTime ManualFrom { get; init; }
        public DateTime ManualTo { get; init; }
        public string ManualFromText => ManualFrom.ToString("yyyy-MM-dd");
        public string ManualToText => ManualTo.ToString("yyyy-MM-dd");
        public List<string> ManualRangeExpected { get; init; } = [];
        public List<string> ManualFromOnlyExpected { get; init; } = [];
        public List<string> ManualToOnlyExpected { get; init; } = [];
    }

    private sealed class FavoriteFilterSmokeFixture
    {
        public string Folder { get; init; } = "";
        public string UnratedName { get; init; } = "";
        public string Level1Name { get; init; } = "";
        public string Level3Name { get; init; } = "";
        public string Level5Name { get; init; } = "";
        public List<string> AllExpected { get; init; } = [];
        public List<string> FavoritesLv1Expected { get; init; } = [];
        public List<string> FavoritesLv3Expected { get; init; } = [];
        public List<string> FavoritesLv5Expected { get; init; } = [];
        public List<string> UnratedExpected { get; init; } = [];
    }

    private sealed class DateFilterSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? StatePath { get; init; }
        public string? TodayName { get; init; }
        public string? SevenDayName { get; init; }
        public string? ThirtyDayName { get; init; }
        public string? ThisYearName { get; init; }
        public string? PreviousYearName { get; init; }
        public List<string> AllExpected { get; init; } = [];
        public List<string> TodayExpected { get; init; } = [];
        public List<string> SevenDayExpected { get; init; } = [];
        public List<string> ThirtyDayExpected { get; init; } = [];
        public List<string> ThisYearExpected { get; init; } = [];
        public string? ManualFrom { get; init; }
        public string? ManualTo { get; init; }
        public List<string> ManualRangeExpected { get; init; } = [];
        public List<string> ManualFromOnlyExpected { get; init; } = [];
        public List<string> ManualToOnlyExpected { get; init; } = [];
        public List<string> AllOrder { get; init; } = [];
        public List<string> TodayOrder { get; init; } = [];
        public List<string> SevenDayOrder { get; init; } = [];
        public List<string> ThirtyDayOrder { get; init; } = [];
        public List<string> ThisYearOrder { get; init; } = [];
        public List<string> ClearOrder { get; init; } = [];
        public List<string> ManualRangeOrder { get; init; } = [];
        public List<string> ManualFromOnlyOrder { get; init; } = [];
        public List<string> ManualToOnlyOrder { get; init; } = [];
        public List<string> ManualClearOrder { get; init; } = [];
        public bool SelectedThirtyTarget { get; init; }
        public string? SelectedAfterToday { get; init; }
        public string? SelectedAfterThirty { get; init; }
        public string? SelectedAfterSeven { get; init; }
        public string? SelectedAfterManualRange { get; init; }
        public string? SelectedAfterManualFromOnly { get; init; }
        public string? SelectedAfterManualToOnly { get; init; }
        public string? PersistedPresetDatePreset { get; init; }
        public string? PersistedPresetDateFrom { get; init; }
        public string? PersistedPresetDateTo { get; init; }
        public string? PersistedManualDatePreset { get; init; }
        public string? PersistedManualDateFrom { get; init; }
        public string? PersistedManualDateTo { get; init; }
        public string? RestoredDatePreset { get; init; }
        public string? RestoredDateFrom { get; init; }
        public string? RestoredDateTo { get; init; }
        public List<string> RestoredOrder { get; init; } = [];
        public string? RestoredSelected { get; init; }
        public string? LegacyRangePreset { get; init; }
        public string? LegacyRangeFrom { get; init; }
        public string? LegacyRangeTo { get; init; }
        public string? LegacyRangeSummary { get; init; }
        public List<string> LegacyRangeOrder { get; init; } = [];
        public string? LegacyPartialPreset { get; init; }
        public string? LegacyPartialFrom { get; init; }
        public string? LegacyPartialTo { get; init; }
        public List<string> LegacyPartialOrder { get; init; } = [];
        public string? LegacyFallbackPreset { get; init; }
        public string? LegacyFallbackFrom { get; init; }
        public string? LegacyFallbackTo { get; init; }
        public List<string> LegacyFallbackOrder { get; init; } = [];
        public bool LegacyUnknownFieldPreserved { get; init; }
    }

    private sealed class P1ASmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public bool SearchOk { get; init; }
        public bool DateOk { get; init; }
        public bool FolderOk { get; init; }
        public string? IndexedPrompt { get; init; }
        public List<string> FilenameMatches { get; init; } = [];
        public List<string> PhraseMatches { get; init; } = [];
        public List<string> AndMatches { get; init; } = [];
        public List<string> EmptyTokenMatches { get; init; } = [];
        public List<string> NegativeMatches { get; init; } = [];
        public List<string> PathMatches { get; init; } = [];
        public List<string> CreatedDateMatches { get; init; } = [];
        public List<string> CurrentFolderSet { get; init; } = [];
        public List<string> LandingFolderSet { get; init; } = [];
        public int CatalogAfterAdd { get; init; }
        public bool LandingVisible { get; init; }
    }

    private sealed class CatalogStressSmokeResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public int RequestedCount { get; set; }
        public int FixtureCount { get; set; }
        public int CatalogCount { get; set; }
        public int FilteredCount { get; set; }
        public int SilentTruncateCount { get; set; }
        public int GridRealized { get; set; }
        public int GridMaximum { get; set; }
        public int GridDeferred { get; set; }
        public int ListRealized { get; set; }
        public bool ListBounded { get; set; }
        public string TailName { get; set; } = "";
        public bool SelectedTail { get; set; }
        public bool ModalTail { get; set; }
        public bool FinalSearchExact { get; set; }
        public bool StaleCancelled { get; set; }
        public int HeartbeatCount { get; set; }
        public long WorkingSetBeforeBytes { get; set; }
        public long WorkingSetAfterBytes { get; set; }
        public long ManagedBytesBefore { get; set; }
        public long ManagedBytesAfter { get; set; }
        public int Generation0Collections { get; set; }
        public int Generation1Collections { get; set; }
        public int Generation2Collections { get; set; }
        public long FixtureElapsedMs { get; set; }
        public long LoadElapsedMs { get; set; }
        public long SearchInputElapsedMs { get; set; }
        public long SearchElapsedMs { get; set; }
        public double? LoadMetricsTotalElapsedMs { get; set; }
        public double? ScanElapsedMs { get; set; }
        public double? MetadataElapsedMs { get; set; }
        public int SourceCountAfter { get; set; }
        public int EnhancementJobsRead { get; set; }
        public int EnhancementCandidates { get; set; }
    }

    private sealed class SearchStallSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public int CatalogCount { get; init; }
        public long InputMs { get; init; }
        public int HeartbeatCount { get; init; }
        public bool FinalCompletionApplied { get; init; }
        public bool FinalCompletionDiscarded { get; init; }
        public string? CompletionError { get; init; }
        public string FinalQuery { get; init; } = "";
        public List<string> FinalMatches { get; init; } = [];
        public string? SelectedFileName { get; init; }
        public int SelectedCount { get; init; }
        public long LastAppliedGeneration { get; init; }
    }

    private sealed class RapidUiStateSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public int FixtureCount { get; init; }
        public bool SelectionChurnAccepted { get; init; }
        public PreviewDecodeSmokeSnapshot? Preview { get; init; }
        public bool ModeAndLayoutChurn { get; init; }
        public bool ResizePreviewDidNotPersist { get; init; }
        public bool ResizeCommitted { get; init; }
        public int AllFavoriteSemanticsCount { get; init; }
        public bool FavoriteLevelsAssigned { get; init; }
        public int UnseenCount { get; init; }
        public int UnseenFilteredCount { get; init; }
        public bool DotsHidden { get; init; }
        public bool DotsShown { get; init; }
        public bool TabChurn { get; init; }
        public List<string> FinalTabs { get; init; } = [];
        public string? FinalActiveTab { get; init; }
        public int DiscardedSearches { get; init; }
        public bool FinalSearchApplied { get; init; }
        public bool FinalStatePersisted { get; init; }
        public bool Restored { get; init; }
        public string? ReloadQuery { get; init; }
        public string? ReloadSelected { get; init; }
        public double ReloadRightPanelWidth { get; init; }
        public string? ReloadDisplayStyle { get; init; }
        public string? ReloadAspectMode { get; init; }
        public List<int> ReloadFavoriteLevels { get; init; } = [];
        public bool ReloadShowUnseenDots { get; init; }
        public bool ReloadUnseenOnly { get; init; }
        public List<string> ReloadTabs { get; init; } = [];
        public string? ReloadActiveTab { get; init; }
        public bool ReloadPinned { get; init; }
        public int HeartbeatCount { get; init; }
        public bool EnhancementPassive { get; init; }
        public bool SourceUntouched { get; init; }
        public bool Isolated { get; init; }
        public long ElapsedMs { get; init; }
    }

    private sealed class ShutdownStateSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public bool SetupReady { get; init; }
        public long CloseMs { get; init; }
        public int FlushCount { get; init; }
        public bool SearchDiscarded { get; init; }
        public bool HoverDiscarded { get; init; }
        public bool PreviewSettled { get; init; }
        public bool OldAsyncDidNotMutateState { get; init; }
        public bool FinalPersisted { get; init; }
        public bool Restored { get; init; }
        public bool CloseStoreIsolation { get; init; }
        public bool ReloadCloseIsolation { get; init; }
        public bool ResidueFree { get; init; }
        public bool EnhancementPassive { get; init; }
        public ShutdownRefusalSnapshot Malformed { get; init; }
        public ShutdownRefusalSnapshot ProtectedFuture { get; init; }
        public ShutdownRefusalSnapshot Contended { get; init; }
    }

    private sealed class RecentWriteOwnershipSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? SmokeRoot { get; init; }
        public bool FirstExplicitCommit { get; init; }
        public bool GeneralStateAndRefreshByteIdentical { get; init; }
        public bool ExternalOwnerByteIdentical { get; init; }
        public bool SecondExplicitCommit { get; init; }
        public bool LatestExternalHistoryMerged { get; init; }
        public bool UnknownFieldsPreserved { get; init; }
        public int MergedHistoryCount { get; init; }
        public bool ExplicitCommitThenCloseByteIdentical { get; init; }
        public bool FailedWriteNotMarkedSuccessful { get; init; }
        public bool RetrySucceededAndSameSetDeduplicated { get; init; }
        public bool RetryThenCloseByteIdentical { get; init; }
        public bool SourceUntouched { get; init; }
        public bool ResidueFree { get; init; }
    }

    private readonly record struct ShutdownRefusalSnapshot(
        bool Unchanged,
        bool Closed,
        int FlushCount,
        bool PendingDiscarded,
        long CloseMs,
        bool LockRemainedOwned,
        bool ResidueFree);

    private sealed class BulkFavoriteSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public List<string> SelectedNames { get; init; } = [];
        public List<int> InitialLevels { get; init; } = [];
        public bool SinglePanelHidden { get; init; }
        public bool MixedPanel { get; init; }
        public string MixedSummary { get; init; } = "";
        public int ExactSetAttempts { get; init; }
        public List<int> ExactSetLevels { get; init; } = [];
        public bool ExactSetPersisted { get; init; }
        public bool ExternalPreservedAfterSet { get; init; }
        public int AdjustAttempts { get; init; }
        public List<int> AdjustedLevels { get; init; } = [];
        public bool AdjustedPersisted { get; init; }
        public bool ExternalPreservedAfterAdjust { get; init; }
        public int ClearAttempts { get; init; }
        public List<int> ClearedLevels { get; init; } = [];
        public bool ClearedFromStore { get; init; }
        public bool ExternalPreservedAfterClear { get; init; }
        public int RestoreAttempts { get; init; }
        public List<int> RestoredLevels { get; init; } = [];
        public int ReassertAttempts { get; init; }
        public bool ReassertedPersisted { get; init; }
        public bool ExternalPreservedAfterReassert { get; init; }
        public int MalformedAttempts { get; init; }
        public List<int> AfterRefusalLevels { get; init; } = [];
        public bool MalformedPreserved { get; init; }
        public bool RefusalStatus { get; init; }
    }

    private sealed class BulkRecycleSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public bool CancelMutatedNothing { get; init; }
        public bool PartialStateCorrect { get; init; }
        public bool FilteredNeighborSelected { get; init; }
        public bool PartialStatus { get; init; }
        public bool EmptyAfterFinal { get; init; }
        public bool RecycleOnly { get; init; }
        public int BackendCalls { get; init; }
        public string Status { get; init; } = "";
    }

    private sealed class P1BSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public bool DecoderStatus { get; init; }
        public bool ScanStatus { get; init; }
        public bool Selected { get; init; }
        public bool SearchHintVisible { get; init; }
        public bool SearchHintHiddenWhenTyped { get; init; }
        public bool SearchHintRestored { get; init; }
        public bool DatePickerNamed { get; init; }
        public bool SearchFocused { get; init; }
        public bool DateFocused { get; init; }
        public bool SettingsButtonFocused { get; init; }
        public bool CardsFocused { get; init; }
        public bool LockBusyStatus { get; init; }
        public bool LockRetryCleared { get; init; }
        public bool FavoritesRefused { get; init; }
        public bool SeenRefused { get; init; }
        public bool StateRefused { get; init; }
        public bool RecentRefused { get; init; }
        public bool SearchShortcutSuppressed { get; init; }
        public bool DateShortcutSuppressed { get; init; }
        public bool ButtonShortcutSuppressed { get; init; }
        public bool ComboBoxShortcutSuppressed { get; init; }
        public bool GlobalShortcutHandled { get; init; }
        public bool SettingsFocus { get; init; }
        public bool SettingsFocusRestored { get; init; }
        public bool DeleteFocus { get; init; }
        public bool DeleteFocusRestored { get; init; }
        public bool LogoAccessible { get; init; }
        public bool LogoActivated { get; init; }
    }

    private sealed class RightPanelSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public bool DefaultOpen { get; init; }
        public double DefaultWidth { get; init; }
        public double ResizedWidth { get; init; }
        public bool RestoredOpen { get; init; }
        public double RestoredWidth { get; init; }
        public bool Closed { get; init; }
        public double StoredWhileClosed { get; init; }
        public bool RestoredClosed { get; init; }
        public double RestoredStoredWidth { get; init; }
        public bool Reopened { get; init; }
        public double ReopenedWidth { get; init; }
        public double MinWidth { get; init; }
        public double MaxWidth { get; init; }
    }

    private sealed class FormatSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public bool BrowserContractFormats { get; init; }
        public bool NativeExtensionFormats { get; init; }
        public List<string> CatalogNames { get; init; } = [];
        public List<string> Extensions { get; init; } = [];
        public List<PreviewDecodeSmokeSnapshot> Previews { get; init; } = [];
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

    private sealed class ModalEnhancedSmokeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string? Folder { get; init; }
        public string? ProjectRoot { get; init; }
        public string? JobsPath { get; init; }
        public string? ValidSourcePath { get; init; }
        public string? ValidOutputPath { get; init; }
        public string? StaleSourcePath { get; init; }
        public string? OriginalDisplayPath { get; init; }
        public string? EnhancedDisplayPath { get; init; }
        public string? RestoredOriginalPath { get; init; }
        public string? StaleDisplayPath { get; init; }
        public string? NextPath { get; init; }
        public bool ValidToggleAvailable { get; init; }
        public bool ToggledEnhanced { get; init; }
        public bool ToggledOriginal { get; init; }
        public bool StaleToggleAvailable { get; init; }
        public bool StaleToggleAttempt { get; init; }
        public bool ToggledBeforeNavigation { get; init; }
        public bool MovedNext { get; init; }
        public bool NavigationResetToOriginal { get; init; }
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
