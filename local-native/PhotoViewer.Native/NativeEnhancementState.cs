using System.Text.Json;

namespace PhotoViewer.Native;

internal sealed record NativeEnhancedOutput(string SourcePath, string OutputPath);

internal static class NativeEnhancementState
{
    public static string JobsFilePath(string projectRoot)
    {
        return Path.Combine(projectRoot, ".cache", "enhance", "jobs.json");
    }

    public static HashSet<string> LoadSucceededSourceIds(string projectRoot)
    {
        return LoadSucceededOutputs(projectRoot).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, NativeEnhancedOutput> LoadSucceededOutputs(string projectRoot, string? jobsPath = null)
    {
        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputs = new Dictionary<string, NativeEnhancedOutput>(StringComparer.OrdinalIgnoreCase);
        var path = string.IsNullOrWhiteSpace(jobsPath) ? JobsFilePath(projectRoot) : jobsPath;
        if (!File.Exists(path))
        {
            return outputs;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            {
                return outputs;
            }

            foreach (var job in jobs.EnumerateArray())
            {
                if (!IsSucceededOutputJob(job))
                {
                    continue;
                }

                var outputPath = ResolveJobPath(projectRoot, ReadString(job, "outputPath"));
                if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
                {
                    continue;
                }

                var sourcePath = ResolveJobPath(projectRoot, ReadString(job, "sourcePath"));
                if (!string.IsNullOrWhiteSpace(sourcePath) && !SourceSignatureMatches(sourcePath, job))
                {
                    continue;
                }

                AddOutput(outputs, ReadString(job, "sourceId"), sourcePath, outputPath);
                AddOutput(outputs, sourcePath, sourcePath, outputPath);
            }
        }
        catch (JsonException)
        {
            return outputs;
        }
        catch (IOException)
        {
            return outputs;
        }
        catch (UnauthorizedAccessException)
        {
            return outputs;
        }

        return outputs;
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

    private static string ResolveJobPath(string projectRoot, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(projectRoot, value));
        }
        catch
        {
            return "";
        }
    }

    private static void AddOutput(
        IDictionary<string, NativeEnhancedOutput> outputs,
        string sourceId,
        string sourcePath,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        outputs[sourceId] = new NativeEnhancedOutput(sourcePath, outputPath);
        try
        {
            outputs[Path.GetFullPath(sourceId)] = new NativeEnhancedOutput(sourcePath, outputPath);
        }
        catch
        {
            // Non-path source ids are still useful if the native record uses the same id.
        }
    }

    private static bool SourceSignatureMatches(string sourcePath, JsonElement job)
    {
        if (!job.TryGetProperty("sourceSignature", out var signature) || signature.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        if (!File.Exists(sourcePath))
        {
            return false;
        }

        var info = new FileInfo(sourcePath);
        if (signature.TryGetProperty("size", out var size) &&
            size.TryGetInt64(out var expectedSize) &&
            expectedSize != info.Length)
        {
            return false;
        }

        if (signature.TryGetProperty("mtimeMs", out var mtime) &&
            mtime.TryGetInt64(out var expectedMtimeMs))
        {
            var actualMtimeMs = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds();
            if (expectedMtimeMs != actualMtimeMs)
            {
                return false;
            }
        }

        return true;
    }
}
