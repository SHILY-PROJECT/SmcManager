using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Скачанный контент: метаданные аккаунта-источника, описание и связанные медиа.
/// </summary>
public class ContentItem
{
    public int Id { get; set; }

    public SocialPlatform Platform { get; set; }

    public ContentKind Kind { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string? ShortCode { get; set; }

    public string AuthorUsername { get; set; } = string.Empty;

    public string? AuthorDisplayName { get; set; }

    public string? AuthorProfileImageUrl { get; set; }

    public string? Caption { get; set; }

    /// <summary>Локальная заметка пользователя к посту.</summary>
    public string? UserComment { get; set; }

    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата публикации в соцсети (если удалось определить).</summary>
    public DateTime? PostedAt { get; set; }

    /// <summary>Относительный путь папки: platform/account/postId_datetime.</summary>
    public string? StorageRelativePath { get; set; }

    public List<ContentTag> Tags { get; set; } = [];

    public List<MediaFile> MediaFiles { get; set; } = [];
}
