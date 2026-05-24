using Microsoft.Extensions.Logging;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Infrastructure.Download;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Метаданные по ссылке: yt-dlp на десктопе, прямой Instagram API на телефоне.
/// </summary>
public sealed class LinkMetadataService : ILinkMetadataService
{
    private readonly YtdlpHostService _ytdlp;
    private readonly ISocialAccountService _accountService;
    private readonly ILogger<LinkMetadataService> _logger;

    public LinkMetadataService(
        YtdlpHostService ytdlp,
        ISocialAccountService accountService,
        ILogger<LinkMetadataService> logger)
    {
        _ytdlp = ytdlp;
        _accountService = accountService;
        _logger = logger;
    }

    public Task WarmupAsync(CancellationToken cancellationToken = default) =>
        YtdlpRuntimeSupport.IsAvailable
            ? _ytdlp.WarmupAsync(cancellationToken)
            : Task.CompletedTask;

    public async Task<LinkMetadataResult> GetMetadataAsync(
        string url,
        int? socialAccountId,
        bool useSocialAccount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "GetMetadataAsync: url={Url}, accountId={AccountId}, useAccount={UseAccount}, ytdlpAvailable={YtdlpAvailable}",
            url,
            socialAccountId,
            useSocialAccount,
            YtdlpRuntimeSupport.IsAvailable);

        if (!UrlPlatformDetector.TryDetect(url, out var platform, out var kind))
        {
            _logger.LogWarning("GetMetadataAsync: URL not recognized: {Url}", url);
            return new LinkMetadataResult
            {
                Qualities = [DownloadQualityOption.BestQuality(SocialPlatform.YouTube)]
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(50));

        var normalized = ContentUrlNormalizer.Normalize(url);
        if (!string.Equals(normalized, url.Trim(), StringComparison.Ordinal))
            _logger.LogDebug("GetMetadataAsync: normalized {Original} -> {Normalized}", url, normalized);

        try
        {
            if (!YtdlpRuntimeSupport.IsAvailable && platform == SocialPlatform.Instagram)
            {
                var account = await _accountService.ResolveForDownloadAsync(
                    platform,
                    socialAccountId,
                    useSocialAccount,
                    timeoutCts.Token).ConfigureAwait(false);

                var result = await InstagramMobileLinkMetadataFetcher.FetchAsync(
                    normalized, platform, kind, account, timeoutCts.Token).ConfigureAwait(false);

                _logger.LogInformation(
                    "GetMetadataAsync (mobile Instagram): HasPreview={HasPreview}, Title={Title}, Thumb={Thumb}",
                    result.Preview is not null,
                    result.Preview?.Title,
                    result.Preview?.ThumbnailUrl);

                return result;
            }

            var ytdlpResult = await _ytdlp.GetLinkMetadataAsync(
                normalized, platform, socialAccountId, useSocialAccount, timeoutCts.Token).ConfigureAwait(false);

            _logger.LogInformation(
                "GetMetadataAsync: done. HasPreview={HasPreview}, Title={Title}, Thumb={Thumb}, Qualities={QualityCount}",
                ytdlpResult.Preview is not null,
                ytdlpResult.Preview?.Title,
                ytdlpResult.Preview?.ThumbnailUrl,
                ytdlpResult.Qualities.Count);

            return ytdlpResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMetadataAsync failed for {Url}", normalized);
            throw;
        }
    }
}
