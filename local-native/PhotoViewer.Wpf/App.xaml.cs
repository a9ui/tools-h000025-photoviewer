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

    private static void WriteStateSmokeResult(string path, StateSmokeResult result)
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
}
