using SmcManager.Core.Enums;
using SmcManager.Core.Models;
using YoutubeDLSharp.Metadata;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Построение списка качеств из метаданных yt-dlp.
/// </summary>
internal static class YtdlpQualityBuilder
{
    public const string BestFormatSelector = "bestvideo+bestaudio/best";

    /// <summary>Фото, карусели и смешанные посты Instagram/VK (не только video).</summary>
    public const string PhotoFriendlyFormatSelector =
        "best[ext=jpg]/best[ext=jpeg]/best[ext=webp]/best[ext=png]/best";

    /// <summary>Рилсы и видео-посты Instagram.</summary>
    public const string VideoFormatSelector =
        "bestvideo+bestaudio/best[ext=mp4]/best";

    public static IReadOnlyList<DownloadQualityOption> FromFormats(
        FormatData[]? formats,
        SocialPlatform platform = SocialPlatform.YouTube)
    {
        var options = new List<DownloadQualityOption> { DownloadQualityOption.BestQuality(platform) };

        if (formats is null || formats.Length == 0)
            return options;

        var seenHeights = new HashSet<int>();

        foreach (var format in formats
                     .Where(f => f.Height is > 0 && !IsAudioOnly(f))
                     .OrderByDescending(f => f.Height)
                     .ThenByDescending(f => f.FileSize ?? 0))
        {
            var height = (int)format.Height!;
            if (!seenHeights.Add(height))
                continue;

            var ext = format.Extension ?? "mp4";
            var note = string.IsNullOrWhiteSpace(format.FormatNote) ? ext : format.FormatNote;
            var sizeMb = format.FileSize is > 0
                ? $" · ~{format.FileSize.Value / (1024 * 1024)} МБ"
                : string.Empty;

            options.Add(new DownloadQualityOption
            {
                Id = format.FormatId ?? height.ToString(),
                Label = $"{height}p · {ext}{sizeMb}" + (string.IsNullOrEmpty(note) ? "" : $" ({note})"),
                FormatSelector = format.FormatId ?? BestFormatSelector,
                Height = height
            });
        }

        var audioOnly = formats
            .Where(IsAudioOnly)
            .OrderByDescending(f => f.AudioBitrate ?? 0)
            .FirstOrDefault();

        if (audioOnly is not null)
        {
            options.Add(new DownloadQualityOption
            {
                Id = audioOnly.FormatId ?? "audio",
                Label = $"Только аудио · {audioOnly.Extension ?? "m4a"}",
                FormatSelector = audioOnly.FormatId ?? "bestaudio",
                Height = 0
            });
        }

        return options;
    }

    public static string ResolveFormatSelector(
        string? qualityFormatId,
        SocialPlatform platform,
        ContentKind? contentKind = null)
    {
        if (!string.IsNullOrWhiteSpace(qualityFormatId) && qualityFormatId != QualityIds.Best)
            return qualityFormatId;

        if (platform == SocialPlatform.Instagram && contentKind is ContentKind.Reel)
            return VideoFormatSelector;

        return platform is SocialPlatform.Instagram or SocialPlatform.Vkontakte
            ? PhotoFriendlyFormatSelector
            : BestFormatSelector;
    }

    public static bool IsVideoContent(SocialPlatform platform, ContentKind? contentKind) =>
        platform == SocialPlatform.Instagram && contentKind is ContentKind.Reel;

    public static bool ShouldRetryInstagramMedia(string format, string? errorLog) =>
        !string.IsNullOrWhiteSpace(errorLog)
        && (errorLog.Contains("no video formats found", StringComparison.OrdinalIgnoreCase)
            || errorLog.Contains("downloading 0 items", StringComparison.OrdinalIgnoreCase))
        && !string.Equals(format, PhotoFriendlyFormatSelector, StringComparison.OrdinalIgnoreCase);

    public static bool ShouldRetryWithPlainBest(string format, string? errorLog) =>
        !string.Equals(format, "best", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(errorLog)
        && errorLog.Contains("no video formats found", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldRetryWithAllFormats(string format, string? errorLog) =>
        !string.Equals(format, "all", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(errorLog)
        && (errorLog.Contains("no video formats found", StringComparison.OrdinalIgnoreCase)
            || errorLog.Contains("downloading 0 items", StringComparison.OrdinalIgnoreCase));

    private static bool IsAudioOnly(FormatData format) =>
        string.Equals(format.VideoCodec, "none", StringComparison.OrdinalIgnoreCase)
        || (format.Height is null or 0 && format.AudioCodec is not null
            && !string.Equals(format.AudioCodec, "none", StringComparison.OrdinalIgnoreCase));
}
