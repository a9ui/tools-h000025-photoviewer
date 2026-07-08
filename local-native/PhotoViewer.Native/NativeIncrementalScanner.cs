namespace PhotoViewer.Native;

internal sealed record NativeIncrementalScanResult(
    IReadOnlyList<NativeImageRecord> AddedOrUpdated,
    IReadOnlyList<string> RemovedPaths,
    int UnchangedCount,
    int ScannedCount);

internal static class NativeIncrementalScanner
{
    private static readonly HashSet<string> SupportedExtensions = NativeImageScanner.SupportedExtensions;

    public static Task<NativeIncrementalScanResult> ScanAsync(
        string root,
        IReadOnlyDictionary<string, NativeImageRecord> existingByPath,
        IReadOnlyDictionary<string, int> favorites,
        IProgress<NativeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var resolvedRoot = Path.GetFullPath(root);
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.System | FileAttributes.Temporary,
            };

            var addedOrUpdated = new List<NativeImageRecord>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unchanged = 0;
            var count = 0;
            var lastProgress = Environment.TickCount64;

            foreach (var filePath in Directory.EnumerateFiles(resolvedRoot, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
                {
                    continue;
                }

                FileInfo info;
                try
                {
                    info = new FileInfo(filePath);
                }
                catch
                {
                    continue;
                }

                var absolutePath = info.FullName;
                seenPaths.Add(absolutePath);
                count++;

                var dimensions = NativeImageHeaderReader.ReadDimensions(absolutePath);
                var hasExisting = existingByPath.TryGetValue(absolutePath, out var existing);
                var existingImage = hasExisting ? existing : null;
                var fileUnchanged = existingImage is not null &&
                    existingImage.SizeBytes == info.Length &&
                    existingImage.ModifiedAtUtc == info.LastWriteTimeUtc &&
                    (existingImage.Width.HasValue || !dimensions.Found) &&
                    (existingImage.Height.HasValue || !dimensions.Found);
                if (fileUnchanged && existingImage is not null && existingImage.MetadataChecked)
                {
                    unchanged++;
                }
                else
                {
                    favorites.TryGetValue(absolutePath, out var favoriteLevel);
                    var metadata = NativePngMetadataReader.Read(absolutePath);
                    addedOrUpdated.Add(new NativeImageRecord(
                        Id: absolutePath,
                        AbsolutePath: absolutePath,
                        Filename: info.Name,
                        Folder: info.DirectoryName ?? string.Empty,
                        SizeBytes: info.Length,
                        CreatedAtUtc: info.CreationTimeUtc,
                        ModifiedAtUtc: info.LastWriteTimeUtc,
                        FavoriteLevel: favoriteLevel,
                        Width: dimensions.Found ? dimensions.Width : existingImage?.Width,
                        Height: dimensions.Found ? dimensions.Height : existingImage?.Height,
                        Prompt: metadata?.Prompt ?? existingImage?.Prompt ?? "",
                        NegativePrompt: metadata?.NegativePrompt ?? existingImage?.NegativePrompt ?? "",
                        MetadataSettingsSummary: metadata?.SettingsSummary ?? existingImage?.MetadataSettingsSummary ?? "",
                        MetadataRaw: metadata?.Raw ?? existingImage?.MetadataRaw ?? "",
                        MetadataChecked: true
                    ));
                }

                var now = Environment.TickCount64;
                if (now - lastProgress >= 120)
                {
                    lastProgress = now;
                    progress?.Report(new NativeScanProgress(count, info.DirectoryName ?? resolvedRoot));
                }
            }

            var removed = existingByPath.Keys
                .Where(path => !seenPaths.Contains(path))
                .ToList();

            progress?.Report(new NativeScanProgress(count, resolvedRoot));
            return new NativeIncrementalScanResult(addedOrUpdated, removed, unchanged, count);
        }, cancellationToken);
    }
}
