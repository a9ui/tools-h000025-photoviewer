namespace PhotoViewer.Native;

internal sealed record NativeImportReport(
    string DatabasePath,
    int FavoriteCount,
    int AlbumCount,
    bool SettingsFound,
    int ImageCount,
    DateTime ImportedAtUtc
);
