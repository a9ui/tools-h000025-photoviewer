using System.Text.Json;

namespace PhotoViewer.Native;

internal sealed record NativeEnhancedOutput(string SourcePath, string OutputPath);

internal enum NativeEnhancementJobStatus
{
    Absent,
    PendingOrRunning,
    SucceededDisplayable,
    SucceededInvalidOutput,
    Failed,
    Unknown,
}

internal sealed record NativeEnhancementStatus(
    NativeEnhancementJobStatus Status,
    string SourcePath,
    string OutputPath,
    string RawStatus);

internal sealed record NativeEnhancementSummary(
    int Total,
    int PendingOrRunning,
    int SucceededDisplayable,
    int SucceededInvalidOutput,
    int Failed,
    int Unknown);

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

    public static NativeEnhancementStatus GetStatusForSource(string projectRoot, string sourcePath, string? jobsPath = null)
    {
        var status = new NativeEnhancementStatus(
            NativeEnhancementJobStatus.Absent,
            sourcePath,
            "",
            "");
        var normalizedSource = ResolveJobPath(projectRoot, sourcePath);
        foreach (var job in LoadJobs(projectRoot, jobsPath))
        {
            if (!JobMatchesSource(job, normalizedSource))
            {
                continue;
            }

            status = ToStatus(job);
        }

        return status;
    }

    public static NativeEnhancementSummary LoadSummary(string projectRoot, string? jobsPath = null)
    {
        var total = 0;
        var pendingOrRunning = 0;
        var succeededDisplayable = 0;
        var succeededInvalidOutput = 0;
        var failed = 0;
        var unknown = 0;

        foreach (var job in LoadJobs(projectRoot, jobsPath))
        {
            total++;
            switch (ToStatus(job).Status)
            {
                case NativeEnhancementJobStatus.PendingOrRunning:
                    pendingOrRunning++;
                    break;
                case NativeEnhancementJobStatus.SucceededDisplayable:
                    succeededDisplayable++;
                    break;
                case NativeEnhancementJobStatus.SucceededInvalidOutput:
                    succeededInvalidOutput++;
                    break;
                case NativeEnhancementJobStatus.Failed:
                    failed++;
                    break;
                case NativeEnhancementJobStatus.Unknown:
                    unknown++;
                    break;
            }
        }

        return new NativeEnhancementSummary(
            total,
            pendingOrRunning,
            succeededDisplayable,
            succeededInvalidOutput,
            failed,
            unknown);
    }

    private static IEnumerable<NativeEnhancementJob> LoadJobs(string projectRoot, string? jobsPath)
    {
        var loadedJobs = new List<NativeEnhancementJob>();
        var path = string.IsNullOrWhiteSpace(jobsPath) ? JobsFilePath(projectRoot) : jobsPath;
        if (!File.Exists(path))
        {
            return loadedJobs;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            {
                return loadedJobs;
            }

            foreach (var job in jobs.EnumerateArray())
            {
                var sourcePath = ResolveJobPath(projectRoot, ReadString(job, "sourcePath"));
                var outputPath = ResolveJobPath(projectRoot, ReadString(job, "outputPath"));
                loadedJobs.Add(new NativeEnhancementJob(
                    ReadString(job, "sourceId"),
                    sourcePath,
                    outputPath,
                    ReadString(job, "status"),
                    SourceSignatureMatches(sourcePath, job)));
            }
        }
        catch (JsonException)
        {
            return loadedJobs;
        }
        catch (IOException)
        {
            return loadedJobs;
        }
        catch (UnauthorizedAccessException)
        {
            return loadedJobs;
        }

        return loadedJobs;
    }

    private static NativeEnhancementStatus ToStatus(NativeEnhancementJob job)
    {
        if (string.Equals(job.RawStatus, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            var displayable = job.SourceSignatureMatches &&
                !string.IsNullOrWhiteSpace(job.OutputPath) &&
                File.Exists(job.OutputPath) &&
                NativeImageDecoder.CanDecode(job.OutputPath, out _);
            return new NativeEnhancementStatus(
                displayable ? NativeEnhancementJobStatus.SucceededDisplayable : NativeEnhancementJobStatus.SucceededInvalidOutput,
                job.SourcePath,
                job.OutputPath,
                job.RawStatus);
        }

        if (string.Equals(job.RawStatus, "failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.RawStatus, "error", StringComparison.OrdinalIgnoreCase))
        {
            return new NativeEnhancementStatus(NativeEnhancementJobStatus.Failed, job.SourcePath, job.OutputPath, job.RawStatus);
        }

        if (string.Equals(job.RawStatus, "pending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.RawStatus, "queued", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.RawStatus, "running", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.RawStatus, "processing", StringComparison.OrdinalIgnoreCase))
        {
            return new NativeEnhancementStatus(NativeEnhancementJobStatus.PendingOrRunning, job.SourcePath, job.OutputPath, job.RawStatus);
        }

        return new NativeEnhancementStatus(NativeEnhancementJobStatus.Unknown, job.SourcePath, job.OutputPath, job.RawStatus);
    }

    private static bool JobMatchesSource(NativeEnhancementJob job, string sourcePath)
    {
        return string.Equals(job.SourceId, sourcePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase);
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

    private sealed record NativeEnhancementJob(
        string SourceId,
        string SourcePath,
        string OutputPath,
        string RawStatus,
        bool SourceSignatureMatches);
}
