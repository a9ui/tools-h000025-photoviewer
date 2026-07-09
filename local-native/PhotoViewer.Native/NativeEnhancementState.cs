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
    private static readonly object SnapshotLock = new();
    private static NativeEnhancementSnapshot? _snapshot;

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
        return new Dictionary<string, NativeEnhancedOutput>(
            LoadSnapshot(projectRoot, jobsPath).SucceededOutputs,
            StringComparer.OrdinalIgnoreCase);
    }

    public static NativeEnhancementStatus GetStatusForSource(string projectRoot, string sourcePath, string? jobsPath = null)
    {
        var normalizedSource = ResolveJobPath(projectRoot, sourcePath);
        var snapshot = LoadSnapshot(projectRoot, jobsPath);
        return snapshot.StatusBySource.TryGetValue(normalizedSource, out var status)
            ? status
            : new NativeEnhancementStatus(
                NativeEnhancementJobStatus.Absent,
                sourcePath,
                "",
                "");
    }

    public static NativeEnhancementSummary LoadSummary(string projectRoot, string? jobsPath = null)
    {
        return LoadSnapshot(projectRoot, jobsPath).Summary;
    }

    private static NativeEnhancementSnapshot LoadSnapshot(string projectRoot, string? jobsPath)
    {
        var path = string.IsNullOrWhiteSpace(jobsPath) ? JobsFilePath(projectRoot) : jobsPath;
        var fingerprint = NativeEnhancementSnapshotFingerprint.From(path);
        lock (SnapshotLock)
        {
            if (_snapshot is not null &&
                string.Equals(_snapshot.ProjectRoot, projectRoot, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_snapshot.JobsPath, path, StringComparison.OrdinalIgnoreCase) &&
                _snapshot.Fingerprint == fingerprint)
            {
                return _snapshot;
            }

            _snapshot = BuildSnapshot(projectRoot, path, fingerprint);
            return _snapshot;
        }
    }

    private static NativeEnhancementSnapshot BuildSnapshot(
        string projectRoot,
        string path,
        NativeEnhancementSnapshotFingerprint fingerprint)
    {
        var jobs = LoadJobs(projectRoot, path).ToList();
        var outputs = new Dictionary<string, NativeEnhancedOutput>(StringComparer.OrdinalIgnoreCase);
        var statuses = new Dictionary<string, NativeEnhancementStatus>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        var pendingOrRunning = 0;
        var succeededDisplayable = 0;
        var succeededInvalidOutput = 0;
        var failed = 0;
        var unknown = 0;

        foreach (var job in jobs)
        {
            total++;
            var status = ToStatus(job);
            AddStatus(statuses, job.SourceId, status);
            AddStatus(statuses, job.SourcePath, status);
            if (job.SourceSignatureMatches &&
                status.Status is NativeEnhancementJobStatus.SucceededDisplayable or NativeEnhancementJobStatus.SucceededInvalidOutput &&
                !string.IsNullOrWhiteSpace(job.OutputPath) &&
                File.Exists(job.OutputPath))
            {
                AddOutput(outputs, job.SourceId, job.SourcePath, job.OutputPath);
                AddOutput(outputs, job.SourcePath, job.SourcePath, job.OutputPath);
            }

            switch (status.Status)
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

        return new NativeEnhancementSnapshot(
            projectRoot,
            path,
            fingerprint,
            outputs,
            statuses,
            new NativeEnhancementSummary(
                total,
                pendingOrRunning,
                succeededDisplayable,
                succeededInvalidOutput,
                failed,
                unknown));
    }

    private static IEnumerable<NativeEnhancementJob> LoadJobs(string projectRoot, string? jobsPath)
    {
        var loadedJobs = new List<NativeEnhancementJob>();
        if (string.IsNullOrWhiteSpace(jobsPath) || !File.Exists(jobsPath))
        {
            return loadedJobs;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(jobsPath));
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

    private static void AddStatus(
        IDictionary<string, NativeEnhancementStatus> statuses,
        string sourceId,
        NativeEnhancementStatus status)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        statuses[sourceId] = status;
        try
        {
            statuses[Path.GetFullPath(sourceId)] = status;
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

    private sealed record NativeEnhancementSnapshot(
        string ProjectRoot,
        string JobsPath,
        NativeEnhancementSnapshotFingerprint Fingerprint,
        Dictionary<string, NativeEnhancedOutput> SucceededOutputs,
        Dictionary<string, NativeEnhancementStatus> StatusBySource,
        NativeEnhancementSummary Summary);

    private readonly record struct NativeEnhancementSnapshotFingerprint(
        bool Exists,
        long Length,
        long LastWriteUtcTicks)
    {
        public static NativeEnhancementSnapshotFingerprint From(string path)
        {
            if (!File.Exists(path))
            {
                return new NativeEnhancementSnapshotFingerprint(false, 0, 0);
            }

            try
            {
                var info = new FileInfo(path);
                return new NativeEnhancementSnapshotFingerprint(true, info.Length, info.LastWriteTimeUtc.Ticks);
            }
            catch (IOException)
            {
                return new NativeEnhancementSnapshotFingerprint(false, 0, 0);
            }
            catch (UnauthorizedAccessException)
            {
                return new NativeEnhancementSnapshotFingerprint(false, 0, 0);
            }
        }
    }
}
