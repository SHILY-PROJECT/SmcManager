using SmcManager.Core.Enums;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Модель отображения скачанного контента в списках.
/// </summary>
public class ContentItemDisplayModel
{
    public int Id { get; init; }

    public string AuthorUsername { get; init; } = string.Empty;

    public string? CaptionPreview { get; init; }

    public string? CommentPreview { get; init; }

    public bool HasCommentPreview => !string.IsNullOrWhiteSpace(CommentPreview);

    public string KindLabel { get; init; } = string.Empty;

    public string PlatformLabel { get; init; } = string.Empty;

    public SocialPlatform Platform { get; init; }

    public ImageSource PlatformIcon { get; init; } = null!;

    public string? TagName { get; init; }

    public string? TagColor { get; init; }

    public string? ThumbnailPath { get; init; }

    public int MediaCount { get; init; }

    public DateTime DownloadedAt { get; init; }

    public static ContentItemDisplayModel FromEntity(ContentItem item, string downloadsRoot) => new()
    {
        Id = item.Id,
        AuthorUsername = item.AuthorUsername,
        CaptionPreview = Truncate(item.Caption, 120),
        CommentPreview = Truncate(item.UserComment, 80),
        KindLabel = item.Kind.ToString(),
        PlatformLabel = item.Platform.ToString(),
        Platform = item.Platform,
        PlatformIcon = SocialPlatformIcons.GetIcon(item.Platform),
        TagName = item.Tag?.Name,
        TagColor = item.Tag?.ColorHex,
        ThumbnailPath = ContentThumbnailHelper.ResolveThumbnailPath(item, downloadsRoot),
        MediaCount = item.MediaFiles.Count,
        DownloadedAt = item.DownloadedAt.ToLocalTime()
    };

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max] + "…";
}
