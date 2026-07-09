using System.Text.Json;

namespace PhotoViewer.Native;

internal sealed record NativeSharedFavoriteWriteResult(bool Succeeded, string Warning);

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

    public static Dictionary<string, int> LoadFavorites(
        string projectRoot,
        ICollection<NativeImportWarning>? warnings = null)
    {
        var path = FavoritesPath(projectRoot);
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
                AddWarning(
                    warnings,
                    "favorites",
                    path,
                    "unexpected-shape",
                    "favorites.json was readable JSON but not an object.",
                    "Favorites were skipped; fix or regenerate .cache/favorites.json, then run Import again.");
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
        catch (JsonException ex)
        {
            AddWarning(
                warnings,
                "favorites",
                path,
                "malformed-json",
                ex.Message,
                "Favorites were skipped; fix or regenerate .cache/favorites.json, then run Import again.");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (IsRecoverableReadException(ex))
        {
            AddWarning(
                warnings,
                "favorites",
                path,
                "unreadable",
                ex.Message,
                "Favorites were skipped; check file permissions or replace .cache/favorites.json, then run Import again.");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        return favorites;
    }

    public static NativeSharedFavoriteWriteResult WriteFavoriteLevel(string projectRoot, string absolutePath, int level)
    {
        var path = FavoritesPath(projectRoot);
        var normalizedPath = Path.GetFullPath(absolutePath);
        var clamped = Math.Clamp(level, 0, 5);
        var favorites = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new NativeSharedFavoriteWriteResult(false, "shared favorites skipped: favorites.json is not an object");
                }

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Number ||
                        !property.Value.TryGetInt32(out var existingLevel) ||
                        existingLevel <= 0)
                    {
                        continue;
                    }

                    favorites[property.Name] = Math.Clamp(existingLevel, 1, 5);
                }
            }
            catch (JsonException ex)
            {
                return new NativeSharedFavoriteWriteResult(false, $"shared favorites skipped: malformed favorites.json ({ex.Message})");
            }
            catch (Exception ex) when (IsRecoverableReadException(ex))
            {
                return new NativeSharedFavoriteWriteResult(false, $"shared favorites skipped: favorites.json was not readable ({ex.Message})");
            }
        }

        foreach (var key in favorites.Keys.ToArray())
        {
            try
            {
                if (string.Equals(Path.GetFullPath(key), normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    favorites.Remove(key);
                }
            }
            catch
            {
                if (string.Equals(key, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    favorites.Remove(key);
                }
            }
        }

        if (clamped > 0)
        {
            favorites[normalizedPath] = clamped;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var orderedFavorites = favorites
                .Where(static item => item.Value > 0)
                .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static item => item.Key, static item => Math.Clamp(item.Value, 1, 5), StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(orderedFavorites, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = $"{path}.{Environment.ProcessId}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tmp";
            File.WriteAllText(tempPath, json + Environment.NewLine);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (IsRecoverableWriteException(ex))
        {
            return new NativeSharedFavoriteWriteResult(false, $"shared favorites write failed: {ex.Message}");
        }

        return new NativeSharedFavoriteWriteResult(true, "");
    }

    public static string FavoritesPath(string projectRoot)
    {
        return Path.Combine(projectRoot, ".cache", "favorites.json");
    }

    public static List<NativeAlbumRecord> LoadAlbums(
        string projectRoot,
        ICollection<NativeImportWarning>? warnings = null)
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
                AddWarning(
                    warnings,
                    "albums",
                    path,
                    "unexpected-shape",
                    "albums.json was readable JSON but did not contain an albums array.",
                    "Albums were skipped; export or repair .cache/albums.json, then run Import again.");
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
        catch (JsonException ex)
        {
            AddWarning(
                warnings,
                "albums",
                path,
                "malformed-json",
                ex.Message,
                "Albums were skipped; fix or regenerate .cache/albums.json, then run Import again.");
            return [];
        }
        catch (Exception ex) when (IsRecoverableReadException(ex))
        {
            AddWarning(
                warnings,
                "albums",
                path,
                "unreadable",
                ex.Message,
                "Albums were skipped; check file permissions or replace .cache/albums.json, then run Import again.");
            return [];
        }

        return records;
    }

    public static IReadOnlyList<NativeBrowserStateRecord> LoadBrowserStateExport(
        string projectRoot,
        string? explicitExportPath,
        ICollection<NativeImportWarning>? warnings = null)
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
        catch (JsonException ex)
        {
            AddWarning(
                warnings,
                "browser-state-export",
                path,
                "malformed-json",
                ex.Message,
                "Browser localStorage export was skipped; rerun the browser export to a valid JSON file, then run Import again.");
            return [];
        }
        catch (Exception ex) when (IsRecoverableReadException(ex))
        {
            AddWarning(
                warnings,
                "browser-state-export",
                path,
                "unreadable",
                ex.Message,
                "Browser localStorage export was skipped; check the path or permissions, then run Import again.");
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

    public static string? LoadSettingsJson(
        string projectRoot,
        ICollection<NativeImportWarning>? warnings = null)
    {
        var path = Path.Combine(projectRoot, ".cache", "settings.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                AddWarning(
                    warnings,
                    "settings",
                    path,
                    "unexpected-shape",
                    "settings.json was readable JSON but not an object.",
                    "Settings fell back to native defaults; fix or regenerate .cache/settings.json, then run Import again.");
                return null;
            }

            return json;
        }
        catch (JsonException ex)
        {
            AddWarning(
                warnings,
                "settings",
                path,
                "malformed-json",
                ex.Message,
                "Settings fell back to native defaults; fix or regenerate .cache/settings.json, then run Import again.");
            return null;
        }
        catch (Exception ex) when (IsRecoverableReadException(ex))
        {
            AddWarning(
                warnings,
                "settings",
                path,
                "unreadable",
                ex.Message,
                "Settings fell back to native defaults; check file permissions or replace .cache/settings.json, then run Import again.");
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

    private static bool IsRecoverableReadException(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException;
    }

    private static bool IsRecoverableWriteException(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException;
    }

    private static void AddWarning(
        ICollection<NativeImportWarning>? warnings,
        string source,
        string path,
        string code,
        string message,
        string recoveryAction)
    {
        warnings?.Add(new NativeImportWarning(source, Path.GetFullPath(path), code, message, recoveryAction));
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
