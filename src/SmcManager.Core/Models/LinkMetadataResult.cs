namespace SmcManager.Core.Models;

/// <summary>
/// Метаданные ссылки: превью и варианты качества (один запрос yt-dlp).
/// </summary>
public class LinkMetadataResult
{
    public LinkPreviewInfo? Preview { get; init; }

    public IReadOnlyList<DownloadQualityOption> Qualities { get; init; } =
        [DownloadQualityOption.BestQuality()];
}
