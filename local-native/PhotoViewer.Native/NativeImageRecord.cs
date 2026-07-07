namespace PhotoViewer.Native;

internal sealed record NativeImageRecord(
    string Id,
    string AbsolutePath,
    string Filename,
    string Folder,
    long SizeBytes,
    DateTime CreatedAtUtc,
    DateTime ModifiedAtUtc,
    int FavoriteLevel
);
