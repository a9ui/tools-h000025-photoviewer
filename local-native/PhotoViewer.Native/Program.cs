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

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
        return 0;
    }
}
