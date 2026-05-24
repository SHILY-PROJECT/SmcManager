using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Вариант качества для скачивания (отображается в UI, передаётся в yt-dlp / YoutubeExplode).
/// </summary>
public class DownloadQualityOption
{
    /// <summary>Идентификатор для сохранения в запросе (format_id или «best»).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Подпись в списке выбора.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Строка формата для yt-dlp (-f) или Itag для YouTube.</summary>
    public string FormatSelector { get; init; } = string.Empty;

    /// <summary>Высота кадра для сортировки (0 — аудио).</summary>
    public int Height { get; init; }

    /// <summary>Выбирается по умолчанию (максимальное качество).</summary>
    public bool IsDefault { get; init; }

    public static DownloadQualityOption BestQuality(
        SocialPlatform platform = SocialPlatform.YouTube,
        string? label = null) => new()
    {
        Id = QualityIds.Best,
        Label = label ?? (platform is SocialPlatform.Instagram or SocialPlatform.Vkontakte
            ? "Максимальное (фото и видео)"
            : "Максимальное качество"),
        FormatSelector = platform is SocialPlatform.Instagram or SocialPlatform.Vkontakte
            ? "best[ext=jpg]/best[ext=jpeg]/best[ext=webp]/best[ext=png]/best"
            : "bestvideo+bestaudio/best",
        Height = int.MaxValue,
        IsDefault = true
    };
}

/// <summary>Стандартные идентификаторы качества.</summary>
public static class QualityIds
{
    public const string Best = "best";
}
