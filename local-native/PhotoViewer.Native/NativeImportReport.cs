namespace PhotoViewer.Native;

internal sealed record NativeImportReport(
    string DatabasePath,
    int FavoriteCount,
    int AlbumCount,
    int AlbumImageCount,
    bool SettingsFound,
    int BrowserStateKeyCount,
    int ImageCount,
    DateTime ImportedAtUtc
);
