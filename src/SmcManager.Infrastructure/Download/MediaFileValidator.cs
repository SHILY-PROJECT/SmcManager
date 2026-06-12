namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Проверка, что скачанный файл — валидное изображение или видео, а не HTML/обрывок.
/// </summary>
internal static class MediaFileValidator
{
    private const int MinBytes = 2048;

    public static bool IsValidFile(string path, bool requireVideo)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < MinBytes)
                return false;

            var ext = Path.GetExtension(path);
            if (requireVideo || ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                              || ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
                              || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase))
                return IsMp4Container(path);

            return IsImageFile(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMp4Container(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (read < 8)
            return false;

        return header[4] == (byte)'f'
               && header[5] == (byte)'t'
               && header[6] == (byte)'y'
               && header[7] == (byte)'p';
    }

    private static bool IsImageFile(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (read < 3)
            return false;

        if (header[0] == 0xFF && header[1] == 0xD8)
            return true;

        if (read >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return true;

        if (read >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;

        return false;
    }
}
