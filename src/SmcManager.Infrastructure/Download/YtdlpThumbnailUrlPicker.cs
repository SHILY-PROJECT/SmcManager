using YoutubeDLSharp.Metadata;

namespace SmcManager.Infrastructure.Download;

internal static class YtdlpThumbnailUrlPicker
{
    public static string? Pick(VideoData? video)
    {
        video = ResolvePrimary(video);
        if (video is null)
            return null;

        if (!string.IsNullOrWhiteSpace(video.Thumbnail))
            return video.Thumbnail.Trim();

        if (video.Thumbnails is not { Length: > 0 })
            return null;

        return video.Thumbnails
            .Where(t => !string.IsNullOrWhiteSpace(t.Url))
            .OrderByDescending(t => t.Width ?? 0)
            .Select(t => t.Url!.Trim())
            .FirstOrDefault();
    }

    private static VideoData? ResolvePrimary(VideoData? video)
    {
        if (video is null) return null;
        if (video.Entries is not { Length: > 0 }) return video;
        return video.Entries[0];
    }
}
