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
            string? browserStateExportPath = null;
            for (var i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--browser-state-export", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    browserStateExportPath = args[++i];
                }
            }

            return NativeHeadlessRunner.RunImport(browserStateExportPath);
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

        if (args.Length >= 2 && args[0] == "--headless-cache-compat")
        {
            return NativeHeadlessRunner.RunCacheCompatibility(args[1]);
        }

        if (args.Length >= 2 && args[0] == "--headless-ui-smoke")
        {
            var searchQuery = "fixture";
            for (var i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--search", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    searchQuery = args[++i];
                }
            }

            ApplicationConfiguration.Initialize();
            return await MainForm.RunUiSmokeAsync(args[1], searchQuery);
        }

        if (args.Length >= 3 && args[0] == "--headless-folder-set-smoke")
        {
            var searchQuery = "fixture";
            var roots = new List<string>();
            for (var i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--search", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    searchQuery = args[++i];
                    continue;
                }

                roots.Add(args[i]);
            }

            ApplicationConfiguration.Initialize();
            return await MainForm.RunFolderSetSmokeAsync(roots, searchQuery);
        }

        if (args.Length >= 2 && args[0] == "--headless-large-scroll-smoke")
        {
            ApplicationConfiguration.Initialize();
            return await MainForm.RunLargeScrollSmokeAsync(args[1]);
        }

        if (args.Length >= 1 && args[0] == "--prepare-fixture")
        {
            return NativeFixtureBuilder.Prepare();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
        return 0;
    }
}
