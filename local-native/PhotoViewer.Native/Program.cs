namespace PhotoViewer.Native;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--headless-scan")
        {
            return await NativeHeadlessRunner.RunScanAsync(args[1], CancellationToken.None);
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

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
        return 0;
    }
}
