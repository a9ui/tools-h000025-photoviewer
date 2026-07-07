namespace PhotoViewer.Native;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--headless-scan")
        {
            var incremental = args.Any(static item => string.Equals(item, "--incremental", StringComparison.OrdinalIgnoreCase));
            return await NativeHeadlessRunner.RunScanAsync(args[1], CancellationToken.None, incremental);
        }

        if (args.Length >= 1 && args[0] == "--headless-import")
        {
            return NativeHeadlessRunner.RunImport();
        }

        if (args.Length >= 2 && args[0] == "--headless-search")
        {
            var query = args.Length >= 3 ? args[2] : "";
            var favoritesOnly = args.Any(static item => string.Equals(item, "--favorites-only", StringComparison.OrdinalIgnoreCase));
            return NativeHeadlessRunner.RunSearch(args[1], query, favoritesOnly);
        }

        if (args.Length >= 3 && args[0] == "--headless-favorite")
        {
            return int.TryParse(args[2], out var level)
                ? NativeHeadlessRunner.RunFavorite(args[1], level)
                : 2;
        }

        if (args.Length >= 2 && args[0] == "--headless-perf")
        {
            var iterations = 40;
            var searchQuery = "";
            for (var i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--iterations", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    _ = int.TryParse(args[++i], out iterations);
                }
                else if (string.Equals(args[i], "--search", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    searchQuery = args[++i];
                }
            }

            return NativeHeadlessRunner.RunPerformance(args[1], Math.Max(10, iterations), searchQuery);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
        return 0;
    }
}
