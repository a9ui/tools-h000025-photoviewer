namespace PhotoViewer.Native;

internal static class NativeFolderSet
{
    private static readonly char[] Separators = ['\r', '\n', ';', '|'];

    public static List<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatForDisplay(IEnumerable<string> roots)
    {
        return string.Join("; ", NormalizeDistinct(roots));
    }

    public static string FormatForSetting(IEnumerable<string> roots)
    {
        return string.Join('\n', NormalizeDistinct(roots));
    }

    public static string? FindRootForPath(string path, IEnumerable<string> roots)
    {
        var normalizedPath = Normalize(path);
        return NormalizeDistinct(roots)
            .Where(root => IsPathUnderRoot(normalizedPath, root))
            .OrderByDescending(static root => root.Length)
            .FirstOrDefault();
    }

    public static bool IsPathUnderRoot(string path, string root)
    {
        var normalizedPath = Normalize(path);
        var normalizedRoot = Normalize(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatFolderLabel(string folder, IReadOnlyList<string> roots)
    {
        foreach (var root in NormalizeDistinct(roots).OrderByDescending(static item => item.Length))
        {
            if (!IsPathUnderRoot(folder, root))
            {
                continue;
            }

            try
            {
                var relative = Path.GetRelativePath(root, folder);
                var rootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(rootName))
                {
                    rootName = root;
                }

                return relative == "." ? rootName : Path.Combine(rootName, relative);
            }
            catch
            {
                return folder;
            }
        }

        return folder;
    }

    public static List<string> NormalizeDistinct(IEnumerable<string> roots)
    {
        return roots
            .Select(Normalize)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }
}
