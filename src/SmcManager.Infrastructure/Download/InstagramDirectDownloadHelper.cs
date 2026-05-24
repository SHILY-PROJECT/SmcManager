using Microsoft.Extensions.Logging;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Infrastructure.Services;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Скачивание Instagram без yt-dlp (Android / iOS).
/// </summary>
internal static class InstagramDirectDownloadHelper
{
    public static async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        ContentKind kind,
        string? shortCode,
        ISocialAccountService accountService,
        IContentRepository repository,
        IMediaStorageService storage,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var downloadUrl = ContentUrlNormalizer.Normalize(request.Url);
        logger.LogInformation(
            "InstagramDirectDownload: url={Url}, shortCode={ShortCode}, kind={Kind}",
            downloadUrl,
            shortCode,
            kind);

        var account = await accountService.ResolveForDownloadAsync(
            SocialPlatform.Instagram,
            request.SocialAccountId,
            request.UseSocialAccount,
            cancellationToken).ConfigureAwait(false);

        logger.LogDebug(
            "InstagramDirectDownload: accountId={AccountId}, hasCookies={HasCookies}",
            account?.Id,
            !string.IsNullOrWhiteSpace(account?.Cookies));

        var outputDir = Path.Combine(Path.GetTempPath(), "smc-instagram", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        try
        {
            var files = await InstagramDirectMediaDownloader.TryDownloadAsync(
                downloadUrl,
                account,
                outputDir,
                metadata: null,
                request.ContentKind,
                cancellationToken).ConfigureAwait(false);

            logger.LogInformation("InstagramDirectDownload: downloaded {FileCount} file(s)", files.Count);

            if (files.Count == 0)
            {
                logger.LogWarning("InstagramDirectDownload: no media files");
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = account is null
                        ? "Не удалось скачать пост Instagram. Добавьте аккаунт с cookies (sessionid) в настройках "
                          + "или откройте пост в публичном доступе."
                        : "Не удалось скачать медиа Instagram. Проверьте cookies аккаунта и доступность поста."
                };
            }

            var authorUsername = await InstagramAuthorResolver.ResolveAsync(
                downloadUrl, null, account, cancellationToken).ConfigureAwait(false);

            var caption = await InstagramCaptionResolver.ResolveAsync(
                downloadUrl, account, cancellationToken).ConfigureAwait(false);

            var content = YtdlpContentMapper.ToContentItem(
                null,
                request,
                SocialPlatform.Instagram,
                kind,
                shortCode,
                authorUsername,
                caption);

            content.StorageRelativePath = ContentPathBuilder.BuildRelativePath(
                content, request.UsePostedDateForFolder);
            content = await repository.SaveContentAsync(content, cancellationToken).ConfigureAwait(false);

            var order = 0;
            string? firstVideoPath = null;

            foreach (var filePath in files)
            {
                var mediaType = YtdlpContentMapper.GuessMediaType(filePath);
                if (mediaType == MediaType.Video && firstVideoPath is null)
                    firstVideoPath = filePath;

                var file = await storage.SaveFromLocalFileAsync(
                    content,
                    filePath,
                    order++,
                    request.UsePostedDateForFolder,
                    cancellationToken).ConfigureAwait(false);
                content.MediaFiles.Add(file);
            }

            var previewThumbUrl = await InstagramMediaApiFetcher.TryGetPreviewThumbnailUrlAsync(
                downloadUrl, account, cancellationToken).ConfigureAwait(false);

            logger.LogDebug(
                "InstagramDirectDownload: saving thumbnail, firstVideo={Video}, remoteThumb={Thumb}",
                firstVideoPath,
                previewThumbUrl);

            await storage.TrySaveVideoThumbnailAsync(
                content,
                remoteThumbnailUrl: previewThumbUrl,
                firstVideoPath ?? content.MediaFiles.FirstOrDefault(m => m.MediaType == MediaType.Video)?.LocalPath,
                request.UsePostedDateForFolder,
                cancellationToken).ConfigureAwait(false);

            content = await repository.SaveContentAsync(content, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "InstagramDirectDownload: success contentId={ContentId}, media={MediaCount}",
                content.Id,
                content.MediaFiles.Count);
            return new DownloadResult { Success = true, Content = content };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InstagramDirectDownload failed for {Url}", downloadUrl);
            throw;
        }
        finally
        {
            YtdlpHostService.TryDeleteDirectory(outputDir);
        }
    }
}
