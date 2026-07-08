using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;

namespace PhotoViewer.Native;

internal static class NativeFixtureBuilder
{
    private const int LargeScrollFixtureCount = 240;
    private static readonly DateTime FixtureTimestampUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static int Prepare()
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var cacheRoot = Path.Combine(projectRoot, ".cache");
        var fixtureRoot = Path.Combine(cacheRoot, "native-fixture");
        var fixtureExtraRoot = Path.Combine(cacheRoot, "native-fixture-extra");
        var largeScrollRoot = Path.Combine(cacheRoot, "native-fixture-large");
        var nativeRoot = Path.Combine(cacheRoot, "native");

        Directory.CreateDirectory(fixtureRoot);
        Directory.CreateDirectory(fixtureExtraRoot);
        Directory.CreateDirectory(largeScrollRoot);
        Directory.CreateDirectory(nativeRoot);
        Directory.CreateDirectory(Path.Combine(cacheRoot, "thumbs"));
        Directory.CreateDirectory(Path.Combine(cacheRoot, "display"));
        Directory.CreateDirectory(Path.Combine(cacheRoot, "enhance"));

        var images = WriteFixtureImages(fixtureRoot);
        var extraImages = WriteFolderSetFixtureImages(fixtureExtraRoot);
        var largeScrollImages = WriteLargeScrollFixtureImages(largeScrollRoot, LargeScrollFixtureCount);
        var created = new List<string>();
        var skipped = new List<string>();

        WriteJsonIfMissing(
            Path.Combine(cacheRoot, "favorites.json"),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [images[0]] = 5,
            },
            created,
            skipped);

        WriteJsonIfMissing(
            Path.Combine(cacheRoot, "albums.json"),
            new
            {
                albums = new object[]
                {
                    new
                    {
                        id = "native-fixture-picks",
                        name = "Native Fixture Picks",
                        images = new[] { images[0], images[1] },
                    },
                    new
                    {
                        id = "native-fixture-review",
                        name = "Native Fixture Review",
                        items = new object[]
                        {
                            new { absolutePath = images[2] },
                            new { absolutePath = extraImages[0] },
                        },
                    },
                },
            },
            created,
            skipped);

        WriteJsonIfMissing(
            Path.Combine(cacheRoot, "settings.json"),
            new
            {
                viewMode = "grid",
                favoritesOnly = false,
                nativeFixture = true,
            },
            created,
            skipped);

        WriteJsonIfMissing(
            Path.Combine(nativeRoot, NativeStateBridge.DefaultBrowserStateExportFileName),
            new
            {
                localStorage = new Dictionary<string, object>
                {
                    ["pvu_view"] = new
                    {
                        viewMode = "grid",
                        thumbSize = 200,
                        sortBy = "newest",
                        folderSortBy = "name-asc",
                        rightPanelOpen = true,
                        rightPanelWidth = 320,
                        dateFrom = "",
                        dateTo = "",
                    },
                    ["pvu_fav_only"] = "false",
                    ["pvu_enhanced_only"] = "false",
                    ["pvu_recent_dirs"] = new[] { fixtureRoot, fixtureExtraRoot },
                    ["pvu_pinned_tabs"] = new[] { "native-fixture-preview-a", "native-fixture-preview-b" },
                    ["pvu_recent_albums"] = new[] { "native-fixture-picks" },
                    ["pvu_enhance_settings"] = new { enabled = false },
                },
            },
            created,
            skipped);

        WriteFixtureCache(projectRoot, images);
        WriteEnhancementFixture(cacheRoot, images[0], created, skipped);

        Console.WriteLine(
            $"native-fixture complete folder=\"{fixtureRoot}\" images={images.Count} extraFolder=\"{fixtureExtraRoot}\" extraImages={extraImages.Count} largeScrollFolder=\"{largeScrollRoot}\" largeScrollImages={largeScrollImages.Count} createdState={FormatList(created)} skippedExistingState={FormatList(skipped)} thumbCompatible=1 thumbMissing=2 thumbIncompatible=1 displayCompatible=1 displayMissing=3 displayIncompatible=0");
        return 0;
    }

    private static List<string> WriteFixtureImages(string fixtureRoot)
    {
        var specs = new[]
        {
            ("m2-fixture-1.png", Color.DarkSlateBlue),
            ("m2-fixture-2.png", Color.SeaGreen),
            ("m2-fixture-3.png", Color.IndianRed),
            (Path.Combine("nested", "m10-folder-fixture-4.png"), Color.Goldenrod),
        };
        var paths = new List<string>(specs.Length);

        foreach (var (name, color) in specs)
        {
            var path = Path.GetFullPath(Path.Combine(fixtureRoot, name));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var bitmap = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(color);
                bitmap.Save(path, ImageFormat.Png);
            }

            TrySetStableTimes(path);
            paths.Add(path);
        }

        return paths;
    }

    private static List<string> WriteFolderSetFixtureImages(string fixtureRoot)
    {
        var specs = new[]
        {
            ("m11-folder-set-fixture-1.png", Color.MidnightBlue),
            (Path.Combine("nested-extra", "m11-folder-set-fixture-2.png"), Color.Teal),
        };
        var paths = new List<string>(specs.Length);

        foreach (var (name, color) in specs)
        {
            var path = Path.GetFullPath(Path.Combine(fixtureRoot, name));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var bitmap = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(color);
                bitmap.Save(path, ImageFormat.Png);
            }

            TrySetStableTimes(path);
            paths.Add(path);
        }

        return paths;
    }

    private static List<string> WriteLargeScrollFixtureImages(string fixtureRoot, int count)
    {
        var paths = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var bucket = $"m13-scroll-bucket-{index / 60:00}";
            var name = Path.Combine(bucket, $"m13-scroll-{index:000}.png");
            var path = Path.GetFullPath(Path.Combine(fixtureRoot, name));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var bitmap = new Bitmap(24, 24))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var color = Color.FromArgb(
                    255,
                    32 + (index * 37 % 160),
                    32 + (index * 53 % 160),
                    32 + (index * 71 % 160));
                graphics.Clear(color);
                bitmap.Save(path, ImageFormat.Png);
            }

            TrySetStableTimes(path, FixtureTimestampUtc.AddDays(index / 60).AddMinutes(index));
            paths.Add(path);
        }

        return paths;
    }

    private static void WriteFixtureCache(string projectRoot, IReadOnlyList<string> imagePaths)
    {
        WriteMinimalWebp(NativeCacheCompatibility.GetThumbnailCachePath(projectRoot, imagePaths[0]));
        WriteMinimalWebp(NativeCacheCompatibility.GetDisplayCachePath(projectRoot, imagePaths[0]));
        WriteInvalidWebp(NativeCacheCompatibility.GetThumbnailCachePath(projectRoot, imagePaths[2]));
    }

    private static void WriteEnhancementFixture(
        string cacheRoot,
        string sourcePath,
        ICollection<string> created,
        ICollection<string> skipped)
    {
        var enhanceRoot = Path.Combine(cacheRoot, "enhance");
        var jobsPath = Path.Combine(enhanceRoot, "jobs.json");
        if (File.Exists(jobsPath))
        {
            skipped.Add(Path.GetRelativePath(NativeStateBridge.ResolveProjectRoot(), jobsPath));
            return;
        }

        var outputPath = Path.Combine(enhanceRoot, "outputs", "native-fixture", "m2-fixture-1__enhanced.webp");
        WriteMinimalWebpIfMissing(outputPath, created, skipped);

        var sourceInfo = new FileInfo(sourcePath);
        var sourceMtimeMs = new DateTimeOffset(sourceInfo.LastWriteTimeUtc).ToUnixTimeMilliseconds();
        WriteJsonIfMissing(
            jobsPath,
            new
            {
                version = 1,
                jobs = new object[]
                {
                    new
                    {
                        id = "native-fixture-enhanced-1",
                        sourceId = sourcePath,
                        sourcePath,
                        sourceSignature = new
                        {
                            size = sourceInfo.Length,
                            mtimeMs = sourceMtimeMs,
                        },
                        presetId = "anime-sharp-x2",
                        presetHash = "native-fixture",
                        preset = new
                        {
                            id = "anime-sharp-x2",
                            label = "Native fixture enhanced output",
                            modelFamily = "anime",
                            modelName = "Native fixture",
                            scale = 2,
                            outputFormat = "webp",
                            denoise = 0,
                            sharpen = 0,
                            detail = 0,
                            smoothness = 0,
                            colorBrightness = 0,
                            colorContrast = 0,
                            colorSaturation = 0,
                        },
                        adapterId = "sharp-test",
                        status = "succeeded",
                        progress = 100,
                        outputPath,
                        createdAt = FixtureTimestampUtc.ToString("O"),
                        updatedAt = FixtureTimestampUtc.ToString("O"),
                        finishedAt = FixtureTimestampUtc.ToString("O"),
                    },
                },
            },
            created,
            skipped);
    }

    private static void WriteJsonIfMissing(
        string path,
        object value,
        ICollection<string> created,
        ICollection<string> skipped)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var relativeName = Path.GetRelativePath(NativeStateBridge.ResolveProjectRoot(), path);
        if (File.Exists(path))
        {
            skipped.Add(relativeName);
            return;
        }

        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine, Utf8NoBom);
        TrySetStableTimes(path);
        created.Add(relativeName);
    }

    private static void WriteMinimalWebp(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new byte[24];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes((uint)(bytes.Length - 8)).CopyTo(bytes, 4);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);
        Encoding.ASCII.GetBytes("VP8X").CopyTo(bytes, 12);
        File.WriteAllBytes(path, bytes);
        TrySetStableTimes(path);
    }

    private static void WriteMinimalWebpIfMissing(
        string path,
        ICollection<string> created,
        ICollection<string> skipped)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var relativeName = Path.GetRelativePath(NativeStateBridge.ResolveProjectRoot(), path);
        if (File.Exists(path))
        {
            skipped.Add(relativeName);
            return;
        }

        WriteMinimalWebp(path);
        created.Add(relativeName);
    }

    private static void WriteInvalidWebp(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not a usable webp fixture", Encoding.ASCII);
        TrySetStableTimes(path);
    }

    private static void TrySetStableTimes(string path)
    {
        TrySetStableTimes(path, FixtureTimestampUtc);
    }

    private static void TrySetStableTimes(string path, DateTime timestampUtc)
    {
        try
        {
            File.SetCreationTimeUtc(path, timestampUtc);
            File.SetLastWriteTimeUtc(path, timestampUtc);
            File.SetLastAccessTimeUtc(path, timestampUtc);
        }
        catch
        {
            // File timestamp normalization is best-effort on local fixtures.
        }
    }

    private static string FormatList(IReadOnlyCollection<string> items)
    {
        return items.Count == 0 ? "none" : string.Join(",", items);
    }
}
