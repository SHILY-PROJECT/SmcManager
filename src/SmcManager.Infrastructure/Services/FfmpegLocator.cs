namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Поиск ffmpeg, установленного вместе с yt-dlp.
/// </summary>
internal static class FfmpegLocator
{
    public static string? GetExecutablePath()
    {
        var binary = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        try
        {
            var fromUtils = YoutubeDLSharp.Utils.GetFullPath(binary);
            if (!string.IsNullOrWhiteSpace(fromUtils) && File.Exists(fromUtils))
                return fromUtils;
        }
        catch
        {
            // ignore
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmcManager",
            "yt-dlp",
            binary);

        return File.Exists(local) ? local : null;
    }
}
