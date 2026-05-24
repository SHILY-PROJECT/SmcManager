using SmcManager.Core.Enums;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using YoutubeDLSharp.Metadata;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Маппинг VideoData yt-dlp в доменные модели.
/// </summary>
internal static class YtdlpContentMapper
{
    public static ContentItem ToContentItem(
        VideoData? video,
        DownloadRequest request,
        SocialPlatform platform,
        ContentKind kind,
        string? shortCode,
        string? authorUsernameOverride = null,
        string? captionOverride = null)
    {
        video = ResolvePrimaryVideo(video);

        var username = InstagramAuthorResolver.PickFromVideo(video)
                       ?? video?.Uploader
                       ?? video?.Channel
                       ?? video?.Creator;

        if (string.IsNullOrWhiteSpace(username)
            && platform != SocialPlatform.Instagram)
        {
            username = video?.UploaderID ?? video?.ChannelID;
        }

        username ??= "unknown";

        username = username.Trim().TrimStart('@');
        if (platform == SocialPlatform.Instagram && username.All(char.IsDigit))
            username = "unknown";

        if (!string.IsNullOrWhiteSpace(authorUsernameOverride)
            && InstagramAuthorResolver.IsLikelyUsername(authorUsernameOverride))
        {
            username = authorUsernameOverride.Trim().TrimStart('@');
        }

        if (username.Length > 64)
            username = username[..64];

        DateTime? postedAt = video?.UploadDate?.ToUniversalTime()
                            ?? video?.Timestamp?.ToUniversalTime()
                            ?? video?.ReleaseDate?.ToUniversalTime();

        return new ContentItem
        {
            Platform = platform,
            Kind = kind,
            SourceUrl = request.Url.Trim(),
            ShortCode = shortCode ?? video?.ID ?? video?.DisplayID,
            AuthorUsername = username,
            AuthorDisplayName = video?.Uploader ?? video?.Title,
            Caption = captionOverride ?? video?.Description ?? video?.Title,
            PostedAt = postedAt,
            TagId = request.TagId,
            DownloadedAt = DateTime.UtcNow
        };
    }

    private static VideoData? ResolvePrimaryVideo(VideoData? video)
    {
        if (video is null) return null;
        if (video.Entries is not { Length: > 0 }) return video;
        return video.Entries[0];
    }

    public static MediaType GuessMediaType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".mp4" or ".webm" or ".mkv" or ".mov" or ".m4v"
            ? MediaType.Video
            : MediaType.Image;
    }
}
