using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;

namespace PhotoViewer.Native;

internal static class NativeFixtureBuilder
{
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
        var nativeRoot = Path.Combine(cacheRoot, "native");

        Directory.CreateDirectory(fixtureRoot);
        Directory.CreateDirectory(nativeRoot);
        Directory.CreateDirectory(Path.Combine(cacheRoot, "thumbs"));
        Directory.CreateDirectory(Path.Combine(cacheRoot, "display"));

        var images = WriteFixtureImages(fixtureRoot);
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
                    ["pvu_view"] = "grid",
                    ["pvu_fav_only"] = "false",
                    ["pvu_recent_dirs"] = new[] { fixtureRoot },
                    ["pvu_recent_albums"] = new[] { "native-fixture-picks" },
                    ["pvu_enhance_settings"] = new { enabled = false },
                },
            },
            created,
            skipped);

        WriteFixtureCache(projectRoot, images);

        Console.WriteLine(
            $"native-fixture complete folder=\"{fixtureRoot}\" images={images.Count} createdState={FormatList(created)} skippedExistingState={FormatList(skipped)} thumbCompatible=1 thumbMissing=1 thumbIncompatible=1 displayCompatible=1 displayMissing=2 displayIncompatible=0");
        return 0;
    }

    private static List<string> WriteFixtureImages(string fixtureRoot)
    {
        var specs = new[]
        {
            ("m2-fixture-1.png", Color.DarkSlateBlue),
            ("m2-fixture-2.png", Color.SeaGreen),
            ("m2-fixture-3.png", Color.IndianRed),
        };
        var paths = new List<string>(specs.Length);

        foreach (var (name, color) in specs)
        {
            var path = Path.GetFullPath(Path.Combine(fixtureRoot, name));
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

    private static void WriteFixtureCache(string projectRoot, IReadOnlyList<string> imagePaths)
    {
        WriteMinimalWebp(NativeCacheCompatibility.GetThumbnailCachePath(projectRoot, imagePaths[0]));
        WriteMinimalWebp(NativeCacheCompatibility.GetDisplayCachePath(projectRoot, imagePaths[0]));
        WriteInvalidWebp(NativeCacheCompatibility.GetThumbnailCachePath(projectRoot, imagePaths[2]));
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

    private static void WriteInvalidWebp(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not a usable webp fixture", Encoding.ASCII);
        TrySetStableTimes(path);
    }

    private static void TrySetStableTimes(string path)
    {
        try
        {
            File.SetCreationTimeUtc(path, FixtureTimestampUtc);
            File.SetLastWriteTimeUtc(path, FixtureTimestampUtc);
            File.SetLastAccessTimeUtc(path, FixtureTimestampUtc);
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
