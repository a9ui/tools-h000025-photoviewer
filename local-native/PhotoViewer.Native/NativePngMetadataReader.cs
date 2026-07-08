using System.Buffers.Binary;
using System.Text;

namespace PhotoViewer.Native;

internal sealed record NativePngMetadata(
    string Raw,
    string Prompt,
    string NegativePrompt,
    IReadOnlyDictionary<string, string> Settings)
{
    public string SettingsSummary => NativePngMetadataReader.FormatSettingsSummary(Settings);
}

internal static class NativePngMetadataReader
{
    private const int MaxTextChunkBytes = 1_048_576;
    private const long MaxBytesBeforeImageData = 4L * 1024L * 1024L;
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static NativePngMetadata? Read(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> signature = stackalloc byte[8];
            if (!ReadExactly(stream, signature) || !signature.SequenceEqual(PngSignature))
            {
                return null;
            }

            var scannedBytes = 8L;
            Span<byte> header = stackalloc byte[8];
            while (scannedBytes < MaxBytesBeforeImageData && ReadExactly(stream, header))
            {
                scannedBytes += 8;
                var chunkLength = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
                var chunkType = Encoding.ASCII.GetString(header[4..8]);
                if (chunkLength > MaxTextChunkBytes)
                {
                    return null;
                }

                if (chunkType == "IDAT")
                {
                    return null;
                }

                var data = new byte[chunkLength];
                if (!ReadExactly(stream, data))
                {
                    return null;
                }

                scannedBytes += chunkLength;
                if (stream.CanSeek)
                {
                    stream.Seek(4, SeekOrigin.Current);
                }
                else
                {
                    Span<byte> crc = stackalloc byte[4];
                    if (!ReadExactly(stream, crc))
                    {
                        return null;
                    }
                }

                scannedBytes += 4;
                if (chunkType != "tEXt")
                {
                    continue;
                }

                var nullIndex = Array.IndexOf(data, (byte)0);
                if (nullIndex < 0)
                {
                    continue;
                }

                var keyword = Encoding.ASCII.GetString(data, 0, nullIndex);
                if (!string.Equals(keyword, "parameters", StringComparison.Ordinal))
                {
                    continue;
                }

                var rawText = Encoding.UTF8.GetString(data, nullIndex + 1, data.Length - nullIndex - 1);
                return ParseStableDiffusionParameters(rawText);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static string FormatSettingsSummary(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.Count == 0)
        {
            return "";
        }

        var preferred = new[] { "Steps", "Sampler", "CFG scale", "Seed", "Size", "Model" };
        var parts = new List<string>();
        foreach (var key in preferred)
        {
            if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}: {value.Trim()}");
            }
        }

        foreach (var (key, value) in settings)
        {
            if (parts.Count >= 6)
            {
                break;
            }

            if (preferred.Contains(key, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            parts.Add($"{key}: {value.Trim()}");
        }

        return string.Join(", ", parts);
    }

    private static NativePngMetadata ParseStableDiffusionParameters(string raw)
    {
        var prompt = "";
        var negativePrompt = "";
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var negativeIndex = raw.IndexOf("Negative prompt:", StringComparison.Ordinal);
        var stepsIndex = raw.IndexOf("\nSteps:", StringComparison.Ordinal);

        if (negativeIndex >= 0 && stepsIndex >= 0)
        {
            prompt = raw[..negativeIndex].Trim();
            negativePrompt = raw[(negativeIndex + "Negative prompt:".Length)..stepsIndex].Trim();
            ParseSettingsLine(raw[(stepsIndex + 1)..], settings);
        }
        else if (negativeIndex >= 0)
        {
            prompt = raw[..negativeIndex].Trim();
            negativePrompt = raw[(negativeIndex + "Negative prompt:".Length)..].Trim();
        }
        else if (stepsIndex >= 0)
        {
            prompt = raw[..stepsIndex].Trim();
            ParseSettingsLine(raw[(stepsIndex + 1)..], settings);
        }
        else
        {
            prompt = raw.Trim();
        }

        return new NativePngMetadata(raw, prompt, negativePrompt, settings);
    }

    private static void ParseSettingsLine(string line, IDictionary<string, string> output)
    {
        var current = line.Trim();
        while (current.Length > 0)
        {
            var colonIndex = current.IndexOf(':');
            if (colonIndex < 0)
            {
                break;
            }

            var key = current[..colonIndex].Trim();
            current = current[(colonIndex + 1)..].TrimStart();

            string value;
            if (current.StartsWith('"'))
            {
                var endQuote = FindClosingQuote(current);
                if (endQuote >= 0)
                {
                    value = current[1..endQuote];
                    current = current[(endQuote + 1)..].TrimStart();
                    if (current.StartsWith(','))
                    {
                        current = current[1..].TrimStart();
                    }
                }
                else
                {
                    value = current[1..];
                    current = "";
                }
            }
            else
            {
                var separator = FindSettingSeparator(current);
                if (separator >= 0)
                {
                    value = current[..separator].Trim();
                    current = current[(separator + 1)..].TrimStart();
                }
                else
                {
                    value = current.Trim();
                    current = "";
                }
            }

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                output[key] = value;
            }
        }
    }

    private static int FindClosingQuote(string value)
    {
        for (var index = 1; index < value.Length; index++)
        {
            if (value[index] == '"' && value[index - 1] != '\\')
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindSettingSeparator(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != ',')
            {
                continue;
            }

            var cursor = index + 1;
            while (cursor < value.Length && char.IsWhiteSpace(value[cursor]))
            {
                cursor++;
            }

            var keyStart = cursor;
            while (cursor < value.Length && (char.IsLetter(value[cursor]) || value[cursor] == '_' || value[cursor] == ' '))
            {
                cursor++;
            }

            if (cursor > keyStart && cursor < value.Length && value[cursor] == ':')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ReadExactly(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read <= 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
