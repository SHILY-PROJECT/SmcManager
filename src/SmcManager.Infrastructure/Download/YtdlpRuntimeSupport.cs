namespace SmcManager.Infrastructure.Download;

/// <summary>
/// yt-dlp (YoutubeDLSharp) можно запускать только на Windows, Linux и macOS.
/// </summary>
internal static class YtdlpRuntimeSupport
{
    public static bool IsAvailable =>
        !OperatingSystem.IsAndroid()
        && !OperatingSystem.IsIOS()
        && !OperatingSystem.IsTvOS()
        && !OperatingSystem.IsWatchOS();

    public const string MobileUnsupportedMessage =
        "Скачивание YouTube и VK на телефоне пока недоступно. "
        + "Используйте версию для Windows или скачивайте Instagram.";
}
