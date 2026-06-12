using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Services;

/// <summary>
/// Формирует путь хранения: соцсеть / аккаунт / идентификатор_дата-время.
/// </summary>
public static class ContentPathBuilder
{
    private static readonly HashSet<char> InvalidChars = Path.GetInvalidFileNameChars()
        .Concat(Path.GetInvalidPathChars())
        .ToHashSet();

    /// <summary>
    /// Относительный путь каталога поста (без корня downloads).
    /// </summary>
    public static string BuildRelativePath(ContentItem item, bool preferPostedDate = true)
    {
        var platform = GetPlatformFolderName(item.Platform);
        var account = SanitizeSegment(item.AuthorUsername, "unknown");
        var postFolder = BuildPostFolderName(item, preferPostedDate);
        return Path.Combine(platform, account, postFolder);
    }

    /// <summary>
    /// Имя папки поста: {shortcode}_{yyyy-MM-dd_HH-mm-ss}.
    /// </summary>
    public static string BuildPostFolderName(ContentItem item, bool preferPostedDate = true)
    {
        var postId = SanitizeSegment(item.ShortCode ?? $"item{item.Id}", "post");
        var moment = ResolveFolderDateTime(item, preferPostedDate);
        var timestamp = moment.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss");
        return $"{postId}_{timestamp}";
    }

    public static DateTime ResolveFolderDateTime(ContentItem item, bool preferPostedDate)
    {
        if (preferPostedDate && item.PostedAt.HasValue)
            return item.PostedAt.Value;

        return item.DownloadedAt;
    }

    public static string GetPlatformFolderName(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram => "instagram",
        SocialPlatform.YouTube => "youtube",
        SocialPlatform.Vkontakte => "vk",
        _ => platform.ToString().ToLowerInvariant()
    };

    public static string SanitizeSegment(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim().TrimStart('@');
        var chars = trimmed
            .Select(c => InvalidChars.Contains(c) ? '_' : c)
            .ToArray();
        var result = new string(chars).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }
}
