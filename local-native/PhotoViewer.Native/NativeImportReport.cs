namespace PhotoViewer.Native;

internal sealed record NativeImportReport(
    string DatabasePath,
    int FavoriteCount,
    int AlbumCount,
    int AlbumImageCount,
    bool SettingsFound,
    int BrowserStateKeyCount,
    int SeenImageCount,
    int ImageCount,
    DateTime ImportedAtUtc,
    IReadOnlyList<NativeImportWarning> Warnings
)
{
    public int WarningCount => Warnings.Count;

    public string RecoverySummary => Warnings.Count == 0
        ? ""
        : string.Join(" | ", Warnings.Select(static warning => $"{warning.Source}: {warning.RecoveryAction}"));
}
