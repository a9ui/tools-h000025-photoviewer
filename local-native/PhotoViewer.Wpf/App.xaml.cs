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
}
