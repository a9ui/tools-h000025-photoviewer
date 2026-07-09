using SkiaSharp;

namespace PhotoViewer.Native;

internal static class NativeWebpDecodeSmoke
{
    public static int Run()
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var smokeRoot = Path.Combine(projectRoot, ".cache", "native-webp-decode-smoke");
        Directory.CreateDirectory(smokeRoot);
        var webpPath = Path.Combine(smokeRoot, $"webp-decode-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Environment.ProcessId}.webp");
        WriteValidWebp(webpPath);

        var decoded = NativeImageDecoder.CanDecode(webpPath, out var decodeMessage);
        var systemDrawingRejected = SystemDrawingRejectsWebp(webpPath);
        Console.WriteLine(
            $"native-webp-decode-smoke complete decoded={BoolText(decoded)} dimensions=\"{Escape(decodeMessage)}\" systemDrawingRejected={BoolText(systemDrawingRejected)} path=\"{Escape(webpPath)}\" browserRuntime=false localHttpServer=false nodeRuntime=false");
        return decoded && systemDrawingRejected ? 0 : 2;
    }

    public static void WriteValidWebp(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(8, 8, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(74, 68, 148));
            using var paint = new SKPaint { Color = new SKColor(168, 88, 220), IsAntialias = false };
            canvas.DrawRect(new SKRect(2, 2, 6, 6), paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, quality: 90)
            ?? throw new InvalidOperationException("Could not encode fixture WebP.");
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        data.SaveTo(stream);
    }

    private static bool SystemDrawingRejectsWebp(string path)
    {
        try
        {
            using var image = Image.FromFile(path);
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static string BoolText(bool value)
    {
        return value.ToString().ToLowerInvariant();
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
