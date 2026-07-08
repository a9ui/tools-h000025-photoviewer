using System.Text.Json;

namespace PhotoViewer.Native;

internal static class NativeEnhancementState
{
    public static string JobsFilePath(string projectRoot)
    {
        return Path.Combine(projectRoot, ".cache", "enhance", "jobs.json");
    }

    public static HashSet<string> LoadSucceededSourceIds(string projectRoot)
    {
        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = JobsFilePath(projectRoot);
        if (!File.Exists(path))
        {
            return sourceIds;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            {
                return sourceIds;
            }

            foreach (var job in jobs.EnumerateArray())
            {
                if (!IsSucceededOutputJob(job))
                {
                    continue;
                }

                AddPath(sourceIds, ReadString(job, "sourceId"));
                AddPath(sourceIds, ReadString(job, "sourcePath"));
            }
        }
        catch (JsonException)
        {
            return sourceIds;
        }
        catch (IOException)
        {
            return sourceIds;
        }
        catch (UnauthorizedAccessException)
        {
            return sourceIds;
        }

        return sourceIds;
    }

    private static bool IsSucceededOutputJob(JsonElement job)
    {
        return string.Equals(ReadString(job, "status"), "succeeded", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(ReadString(job, "outputPath"));
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static void AddPath(HashSet<string> sourceIds, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        sourceIds.Add(value);
        try
        {
            sourceIds.Add(Path.GetFullPath(value));
        }
        catch
        {
            // Non-path source ids are ignored by the native absolute-path matcher.
        }
    }
}
