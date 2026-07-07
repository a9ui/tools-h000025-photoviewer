using System.Text.Json;

namespace PhotoViewer.Native;

internal sealed record NativeStateSummary(
    string ProjectRoot,
    int FavoriteCount,
    int AlbumCount,
    bool SettingsFound
);

internal static class NativeStateBridge
{
    public static string ResolveProjectRoot()
    {
        foreach (var candidate in EnumerateRootCandidates())
        {
            if (File.Exists(Path.Combine(candidate, "PROJECT.md")) &&
                Directory.Exists(Path.Combine(candidate, "src")))
            {
                return candidate;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    public static NativeStateSummary ReadSummary(string projectRoot)
    {
        var favorites = LoadFavorites(projectRoot);
        var albumCount = CountAlbums(projectRoot);
        var settingsFound = File.Exists(Path.Combine(projectRoot, ".cache", "settings.json"));
        return new NativeStateSummary(projectRoot, favorites.Count, albumCount, settingsFound);
    }

    public static Dictionary<string, int> LoadFavorites(string projectRoot)
    {
        var path = Path.Combine(projectRoot, ".cache", "favorites.json");
        var favorites = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return favorites;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return favorites;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Number ||
                    !property.Value.TryGetInt32(out var level) ||
                    level <= 0)
                {
                    continue;
                }

                try
                {
                    favorites[Path.GetFullPath(property.Name)] = Math.Clamp(level, 1, 5);
                }
                catch
                {
                    favorites[property.Name] = Math.Clamp(level, 1, 5);
                }
            }
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        return favorites;
    }

    private static int CountAlbums(string projectRoot)
    {
        var path = Path.Combine(projectRoot, ".cache", "albums.json");
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.TryGetProperty("albums", out var albums) &&
                albums.ValueKind == JsonValueKind.Array
                    ? albums.GetArrayLength()
                    : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static IEnumerable<string> EnumerateRootCandidates()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }
}
