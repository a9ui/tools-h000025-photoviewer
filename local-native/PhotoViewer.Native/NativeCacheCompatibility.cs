using System.Text;

namespace PhotoViewer.Native;

internal sealed record NativeCacheCompatibilityReport(
    int ImagesChecked,
    int ThumbnailCompatible,
    int ThumbnailMissing,
    int ThumbnailIncompatible,
    int DisplayCompatible,
    int DisplayMissing,
    int DisplayIncompatible
);

internal static class NativeCacheCompatibility
{
    private const int DisplayMaxSize = 2200;
    private const int WebpHeaderBytes = 16;
    private static readonly byte[] RiffHeader = "RIFF"u8.ToArray();
    private static readonly byte[] WebpHeader = "WEBP"u8.ToArray();

    public static NativeCacheCompatibilityReport Check(
        string projectRoot,
        IReadOnlyList<NativeImageRecord> images)
    {
        var thumbsRoot = Path.Combine(projectRoot, ".cache", "thumbs");
        var displayRoot = Path.Combine(projectRoot, ".cache", "display");
        var thumbCompatible = 0;
        var thumbMissing = 0;
        var thumbIncompatible = 0;
        var displayCompatible = 0;
        var displayMissing = 0;
        var displayIncompatible = 0;

        foreach (var image in images)
        {
            var version = GetBrowserCacheVersion(image.AbsolutePath);
            var thumbPath = Path.Combine(thumbsRoot, $"{Base64Url($"{image.AbsolutePath}|{version}")}.webp");
            var displayPath = Path.Combine(displayRoot, $"{Base64Url($"{image.AbsolutePath}|{version}|display:{DisplayMaxSize}")}.webp");

            AddCacheState(thumbPath, ref thumbCompatible, ref thumbMissing, ref thumbIncompatible);
            AddCacheState(displayPath, ref displayCompatible, ref displayMissing, ref displayIncompatible);
        }

        return new NativeCacheCompatibilityReport(
            ImagesChecked: images.Count,
            ThumbnailCompatible: thumbCompatible,
            ThumbnailMissing: thumbMissing,
            ThumbnailIncompatible: thumbIncompatible,
            DisplayCompatible: displayCompatible,
            DisplayMissing: displayMissing,
            DisplayIncompatible: displayIncompatible);
    }

    public static string GetThumbnailCachePath(string projectRoot, string absolutePath)
    {
        var version = GetBrowserCacheVersion(absolutePath);
        return Path.Combine(projectRoot, ".cache", "thumbs", $"{Base64Url($"{absolutePath}|{version}")}.webp");
    }

    public static string GetDisplayCachePath(string projectRoot, string absolutePath)
    {
        var version = GetBrowserCacheVersion(absolutePath);
        return Path.Combine(projectRoot, ".cache", "display", $"{Base64Url($"{absolutePath}|{version}|display:{DisplayMaxSize}")}.webp");
    }

    private static void AddCacheState(string cachePath, ref int compatible, ref int missing, ref int incompatible)
    {
        if (!File.Exists(cachePath))
        {
            missing++;
            return;
        }

        if (HasUsableWebpHeader(cachePath))
        {
            compatible++;
        }
        else
        {
            incompatible++;
        }
    }

    private static string GetBrowserCacheVersion(string absolutePath)
    {
        var mtimeMs = new DateTimeOffset(File.GetLastWriteTimeUtc(absolutePath)).ToUnixTimeMilliseconds();
        return mtimeMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool HasUsableWebpHeader(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (info.Length <= WebpHeaderBytes)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[WebpHeaderBytes];
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Read(header) != WebpHeaderBytes)
            {
                return false;
            }

            var declaredSize = BitConverter.ToUInt32(header[4..8]);
            var chunkType = Encoding.ASCII.GetString(header[12..16]);
            return header[..4].SequenceEqual(RiffHeader) &&
                   header[8..12].SequenceEqual(WebpHeader) &&
                   declaredSize + 8 == info.Length &&
                   (chunkType == "VP8 " || chunkType == "VP8L" || chunkType == "VP8X");
        }
        catch
        {
            return false;
        }
    }
}
