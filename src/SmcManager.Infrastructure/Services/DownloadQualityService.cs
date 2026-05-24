using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Список качеств через yt-dlp для поддерживаемых платформ.
/// </summary>
public sealed class DownloadQualityService : IDownloadQualityService
{
    private readonly YtdlpHostService _ytdlp;

    public DownloadQualityService(YtdlpHostService ytdlp) => _ytdlp = ytdlp;

    public async Task<IReadOnlyList<DownloadQualityOption>> GetQualitiesAsync(
        string url,
        int? socialAccountId,
        bool useSocialAccount,
        CancellationToken cancellationToken = default)
    {
        if (!UrlPlatformDetector.TryDetect(url, out var platform, out _))
            return [DownloadQualityOption.BestQuality(SocialPlatform.YouTube)];

        var normalized = ContentUrlNormalizer.Normalize(url);
        return await _ytdlp.GetQualitiesAsync(
            normalized, platform, socialAccountId, useSocialAccount, cancellationToken);
    }
}
