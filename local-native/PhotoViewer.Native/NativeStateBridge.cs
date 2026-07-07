using System.Text.Json;

namespace PhotoViewer.Native;

internal sealed record NativeStateSummary(
    string ProjectRoot,
    int FavoriteCount,
    int AlbumCount,
    int AlbumImageCount,
    bool SettingsFound
);

internal sealed record NativeAlbumRecord(
    string Id,
    string Name,
    int ImageCount,
    IReadOnlyList<string> ImagePaths
);

internal sealed record NativeBrowserStateRecord(
    string Key,
    string Value
);

internal static class NativeStateBridge
{
    public const string DefaultBrowserStateExportFileName = "browser-localstorage-export.json";

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
        var albums = LoadAlbums(projectRoot);
        var albumImageCount = albums.Sum(static item => item.ImagePaths.Count);
        var settingsFound = File.Exists(Path.Combine(projectRoot, ".cache", "settings.json"));
        return new NativeStateSummary(projectRoot, favorites.Count, albums.Count, albumImageCount, settingsFound);
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

    public static List<NativeAlbumRecord> LoadAlbums(string projectRoot)
    {
        var path = Path.Combine(projectRoot, ".cache", "albums.json");
        var records = new List<NativeAlbumRecord>();
        if (!File.Exists(path))
        {
            return records;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("albums", out var albums) ||
                albums.ValueKind != JsonValueKind.Array)
            {
                return records;
            }

            var index = 0;
            foreach (var album in albums.EnumerateArray())
            {
                index++;
                if (album.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = ReadString(album, "id") ?? ReadString(album, "slug") ?? $"album-{index}";
                var name = ReadString(album, "name") ?? ReadString(album, "title") ?? id;
                var imagePaths = ReadAlbumImagePaths(album).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var imageCount =
                    imagePaths.Count > 0
                        ? imagePaths.Count
                        : ReadArrayLength(album, "images") ??
                          ReadArrayLength(album, "imageIds") ??
                          ReadArrayLength(album, "paths") ??
                          ReadArrayLength(album, "items") ??
                          0;
                records.Add(new NativeAlbumRecord(id, name, imageCount, imagePaths));
            }
        }
        catch
        {
            return [];
        }

        return records;
    }

    public static IReadOnlyList<NativeBrowserStateRecord> LoadBrowserStateExport(
        string projectRoot,
        string? explicitExportPath)
    {
        var path = ResolveBrowserStateExportPath(projectRoot, explicitExportPath);
        if (path is null || !File.Exists(path))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var records = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ReadBrowserStateObject(doc.RootElement, records);
            return records
                .Where(static item => item.Key.StartsWith("pvu_", StringComparison.Ordinal))
                .OrderBy(static item => item.Key, StringComparer.Ordinal)
                .Select(static item => new NativeBrowserStateRecord(item.Key, item.Value))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string? ResolveBrowserStateExportPath(string projectRoot, string? explicitExportPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitExportPath))
        {
            return Path.GetFullPath(explicitExportPath);
        }

        var defaultPath = Path.Combine(projectRoot, ".cache", "native", DefaultBrowserStateExportFileName);
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    public static string? LoadSettingsJson(string projectRoot)
    {
        var path = Path.Combine(projectRoot, ".cache", "settings.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadArrayLength(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : null;
    }

    private static IEnumerable<string> ReadAlbumImagePaths(JsonElement album)
    {
        foreach (var propertyName in new[] { "images", "imageIds", "paths", "items" })
        {
            if (!album.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in value.EnumerateArray())
            {
                var path = ReadAlbumImagePath(item);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    yield return path;
                }
            }
        }
    }

    private static string? ReadAlbumImagePath(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return NormalizePathLikeValue(element.GetString());
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var field in new[] { "absolutePath", "path", "filePath", "id", "imageId", "src" })
        {
            var value = ReadString(element, field);
            var normalized = NormalizePathLikeValue(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? NormalizePathLikeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch
        {
            return value;
        }
    }

    private static void ReadBrowserStateObject(JsonElement element, Dictionary<string, string> records)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var key = ReadString(item, "key") ?? ReadString(item, "name");
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (item.TryGetProperty("value", out var value))
                {
                    records[key] = JsonElementToStorageString(value);
                }
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var wrapper in new[] { "localStorage", "storage", "pvu" })
        {
            if (element.TryGetProperty(wrapper, out var nested) &&
                (nested.ValueKind == JsonValueKind.Object || nested.ValueKind == JsonValueKind.Array))
            {
                ReadBrowserStateObject(nested, records);
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.StartsWith("pvu_", StringComparison.Ordinal))
            {
                continue;
            }

            records[property.Name] = JsonElementToStorageString(property.Value);
        }
    }

    private static string JsonElementToStorageString(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
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
