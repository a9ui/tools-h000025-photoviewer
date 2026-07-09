using SkiaSharp;

namespace PhotoViewer.Native;

internal static class NativeImageDecoder
{
    public static Image LoadImageCopy(string filePath)
    {
        return string.Equals(Path.GetExtension(filePath), ".webp", StringComparison.OrdinalIgnoreCase)
            ? LoadWebpImageCopy(filePath)
            : LoadSystemDrawingImageCopy(filePath);
    }

    public static bool CanDecode(string filePath, out string message)
    {
        try
        {
            using var image = LoadImageCopy(filePath);
            message = $"{image.Width}x{image.Height}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            message = ex.Message;
            return false;
        }
    }

    private static Image LoadSystemDrawingImageCopy(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        using var source = Image.FromStream(memory);
        return new Bitmap(source);
    }

    private static Image LoadWebpImageCopy(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var data = SKData.Create(stream);
        using var image = SKImage.FromEncodedData(data)
            ?? throw new InvalidDataException("WebP image could not be decoded.");
        using var encoded = image.Encode(SKEncodedImageFormat.Png, quality: 100)
            ?? throw new InvalidDataException("Decoded WebP image could not be converted for WinForms display.");
        using var memory = new MemoryStream(encoded.ToArray());
        using var source = Image.FromStream(memory);
        return new Bitmap(source);
    }
}
