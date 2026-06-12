using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Превью контента по ссылке (до скачивания).
/// </summary>
public class LinkPreviewInfo
{
    public required string NormalizedUrl { get; init; }

    public SocialPlatform Platform { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Author { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? Description { get; init; }
}
