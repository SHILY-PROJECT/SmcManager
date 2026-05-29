using SmcManager.Core.Enums;

namespace SmcManager.Core.Services;

/// <summary>
/// Определяет платформу и тип контента по URL.
/// </summary>
public static class UrlPlatformDetector
{
    public static bool TryDetect(string url, out SocialPlatform platform, out ContentKind kind)
    {
        platform = SocialPlatform.Instagram;
        kind = ContentKind.Post;

        if (string.IsNullOrWhiteSpace(url)) return false;

        url = ContentUrlNormalizer.ExtractHttpUrl(url);
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("instagram.com") || host.Contains("instagr.am"))
        {
            platform = SocialPlatform.Instagram;
            var path = uri.AbsolutePath.ToLowerInvariant();
            if (path.Contains("/reel/") || path.Contains("/reels/"))
                kind = ContentKind.Reel;
            else if (path.Contains("/stories/"))
                kind = ContentKind.Story;
            else
                kind = ContentKind.Post;
            return true;
        }

        if (host.Contains("youtube.com") || host.Contains("youtu.be"))
        {
            platform = SocialPlatform.YouTube;
            kind = ContentKind.Post;
            return true;
        }

        if (host.Contains("vk.com") || host.Contains("vkontakte"))
        {
            platform = SocialPlatform.Vkontakte;
            kind = ContentKind.Post;
            return true;
        }

        return false;
    }
}
