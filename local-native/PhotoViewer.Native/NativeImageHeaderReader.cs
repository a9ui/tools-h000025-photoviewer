namespace PhotoViewer.Native;

internal readonly record struct NativeImageDimensions(int Width, int Height, bool Found);

internal static class NativeImageHeaderReader
{
    private const int MaxHeaderBytes = 65536;

    public static NativeImageDimensions ReadDimensions(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[Math.Min(MaxHeaderBytes, stream.Length > 0 ? (int)Math.Min(stream.Length, MaxHeaderBytes) : MaxHeaderBytes)];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read < 16)
            {
                return default;
            }

            return Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => ReadPngDimensions(buffer, read),
                ".jpg" or ".jpeg" => ReadJpegDimensions(buffer, read),
                ".gif" => ReadGifDimensions(buffer, read),
                _ => default,
            };
        }
        catch
        {
            return default;
        }
    }

    private static NativeImageDimensions ReadPngDimensions(byte[] buffer, int length)
    {
        if (length < 24 ||
            buffer[0] != 0x89 ||
            buffer[1] != (byte)'P' ||
            buffer[2] != (byte)'N' ||
            buffer[3] != (byte)'G')
        {
            return default;
        }

        var width = (buffer[16] << 24) | (buffer[17] << 16) | (buffer[18] << 8) | buffer[19];
        var height = (buffer[20] << 24) | (buffer[21] << 16) | (buffer[22] << 8) | buffer[23];
        return width > 0 && height > 0 ? new NativeImageDimensions(width, height, true) : default;
    }

    private static NativeImageDimensions ReadGifDimensions(byte[] buffer, int length)
    {
        if (length < 10 ||
            buffer[0] != (byte)'G' ||
            buffer[1] != (byte)'I' ||
            buffer[2] != (byte)'F')
        {
            return default;
        }

        var width = buffer[6] | (buffer[7] << 8);
        var height = buffer[8] | (buffer[9] << 8);
        return width > 0 && height > 0 ? new NativeImageDimensions(width, height, true) : default;
    }

    private static NativeImageDimensions ReadJpegDimensions(byte[] buffer, int length)
    {
        if (length < 4 || buffer[0] != 0xFF || buffer[1] != 0xD8)
        {
            return default;
        }

        var index = 2;
        while (index + 9 < length)
        {
            if (buffer[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = buffer[index + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                var height = (buffer[index + 5] << 8) | buffer[index + 6];
                var width = (buffer[index + 7] << 8) | buffer[index + 8];
                return width > 0 && height > 0 ? new NativeImageDimensions(width, height, true) : default;
            }

            if (index + 3 >= length)
            {
                break;
            }

            var segmentLength = (buffer[index + 2] << 8) | buffer[index + 3];
            if (segmentLength < 2)
            {
                break;
            }

            index += segmentLength + 2;
        }

        return default;
    }
}
