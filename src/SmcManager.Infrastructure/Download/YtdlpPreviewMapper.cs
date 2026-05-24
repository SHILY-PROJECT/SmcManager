using SmcManager.Core.Enums;
using SmcManager.Core.Models;
using YoutubeDLSharp.Metadata;

namespace SmcManager.Infrastructure.Download;

internal static class YtdlpPreviewMapper
{
    public static LinkPreviewInfo? FromVideoData(
        VideoData? video,
        SocialPlatform platform,
        string normalizedUrl)
    {
        video = ResolvePrimary(video);
        if (video is null) return null;

        var title = FirstNonEmpty(video.Description, video.Title) ?? "Контент";
        if (title.Length > 200)
            title = title[..200] + "…";

        var author = platform == SocialPlatform.Instagram
            ? InstagramAuthorResolver.PickFromVideo(video)
              ?? FirstNonEmpty(video.Uploader, video.Channel)
            : FirstNonEmpty(video.Uploader, video.Channel, video.UploaderID);
        if (!string.IsNullOrWhiteSpace(author))
            author = author.TrimStart('@');

        var thumb = FirstNonEmpty(video.Thumbnail, PickBestThumbnail(video.Thumbnails));

        return new LinkPreviewInfo
        {
            NormalizedUrl = normalizedUrl,
            Platform = platform,
            Title = title,
            Author = author,
            ThumbnailUrl = thumb,
            Description = video.Description
        };
    }

    private static VideoData? ResolvePrimary(VideoData? video)
    {
        if (video is null) return null;
        if (video.Entries is not { Length: > 0 })
            return video;

        return video.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Thumbnail) || !string.IsNullOrEmpty(e.Url))
               ?? video.Entries[0];
    }

    private static string? PickBestThumbnail(ThumbnailData[]? thumbnails)
    {
        if (thumbnails is not { Length: > 0 }) return null;
        return thumbnails
            .Where(t => !string.IsNullOrWhiteSpace(t.Url))
            .OrderByDescending(t => t.Width ?? 0)
            .Select(t => t.Url)
            .FirstOrDefault();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }
}
