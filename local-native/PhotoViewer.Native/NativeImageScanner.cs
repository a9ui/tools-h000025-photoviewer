namespace PhotoViewer.Native;

internal static class NativeImageScanner
{
    internal static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".avif",
        ".gif",
    };

    public static Task<List<NativeImageRecord>> ScanAsync(
        string root,
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

            var records = new List<NativeImageRecord>(capacity: 8192);
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
                favorites.TryGetValue(absolutePath, out var favoriteLevel);
                var dimensions = NativeImageHeaderReader.ReadDimensions(absolutePath);

                records.Add(new NativeImageRecord(
                    Id: absolutePath,
                    AbsolutePath: absolutePath,
                    Filename: info.Name,
                    Folder: info.DirectoryName ?? string.Empty,
                    SizeBytes: info.Length,
                    CreatedAtUtc: info.CreationTimeUtc,
                    ModifiedAtUtc: info.LastWriteTimeUtc,
                    FavoriteLevel: favoriteLevel,
                    Width: dimensions.Found ? dimensions.Width : null,
                    Height: dimensions.Found ? dimensions.Height : null
                ));

                count++;
                var now = Environment.TickCount64;
                if (now - lastProgress >= 120)
                {
                    lastProgress = now;
                    progress?.Report(new NativeScanProgress(count, info.DirectoryName ?? resolvedRoot));
                }
            }

            records.Sort(static (a, b) =>
            {
                var modified = b.ModifiedAtUtc.CompareTo(a.ModifiedAtUtc);
                return modified != 0
                    ? modified
                    : string.Compare(a.AbsolutePath, b.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            });

            progress?.Report(new NativeScanProgress(records.Count, resolvedRoot));
            return records;
        }, cancellationToken);
    }
}
