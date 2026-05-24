using SmcManager.Core.Enums;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Превью Instagram на Android/iOS без yt-dlp.
/// </summary>
internal static class InstagramMobileLinkMetadataFetcher
{
    public static async Task<LinkMetadataResult> FetchAsync(
        string normalizedUrl,
        SocialPlatform platform,
        ContentKind kind,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        var author = await InstagramAuthorResolver.ResolveAsync(
            normalizedUrl, video: null, account, cancellationToken).ConfigureAwait(false);

        var caption = await InstagramCaptionResolver.ResolveAsync(
            normalizedUrl, account, cancellationToken).ConfigureAwait(false);

        var thumbnailUrl = await InstagramMediaApiFetcher.TryGetPreviewThumbnailUrlAsync(
            normalizedUrl, account, cancellationToken).ConfigureAwait(false);

        var title = Truncate(caption, 160)
                    ?? (kind is ContentKind.Reel ? "Reels" : "Instagram");

        LinkPreviewInfo? preview = null;
        if (!string.IsNullOrWhiteSpace(title)
            || !string.IsNullOrWhiteSpace(author)
            || !string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            preview = new LinkPreviewInfo
            {
                NormalizedUrl = normalizedUrl,
                Platform = platform,
                Title = title,
                Author = author,
                ThumbnailUrl = thumbnailUrl,
                Description = caption
            };
        }

        return new LinkMetadataResult
        {
            Preview = preview,
            Qualities = [DownloadQualityOption.BestQuality(platform)]
        };
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }
}
