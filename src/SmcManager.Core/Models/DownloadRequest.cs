using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Запрос на скачивание по URL (из поля ввода или Share intent).
/// </summary>
public class DownloadRequest
{
    public string Url { get; set; } = string.Empty;

    /// <summary>Тип контента (пост / рилс / сторис), если известен из URL.</summary>
    public ContentKind? ContentKind { get; set; }

    public IReadOnlyList<int> TagIds { get; set; } = [];

    public int? SocialAccountId { get; set; }

    /// <summary>false — скачивание без cookies (не подставлять аккаунт по умолчанию).</summary>
    public bool UseSocialAccount { get; set; } = true;

    /// <summary>true — в имени папки дата публикации; false — дата скачивания.</summary>
    public bool UsePostedDateForFolder { get; set; } = true;

    /// <summary>Качество: «best» или format_id / itag. null — максимальное.</summary>
    public string? QualityFormatId { get; set; }

    /// <summary>Строка формата yt-dlp (-f) из выбранного качества в UI.</summary>
    public string? QualityFormatSelector { get; set; }
}
