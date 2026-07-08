namespace PhotoViewer.Native;

internal sealed record NativeImageRecord(
    string Id,
    string AbsolutePath,
    string Filename,
    string Folder,
    long SizeBytes,
    DateTime CreatedAtUtc,
    DateTime ModifiedAtUtc,
    int FavoriteLevel,
    int? Width = null,
    int? Height = null,
    string Prompt = "",
    string NegativePrompt = "",
    string MetadataSettingsSummary = "",
    string MetadataRaw = "",
    bool MetadataChecked = false,
    bool IsSeen = false
);
