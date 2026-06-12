using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Infrastructure.Services;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Общая логика скачивания через yt-dlp и сохранения в БД.
/// </summary>
internal static class YtdlpDownloadHelper
{
    public static async Task<DownloadResult> DownloadAsync(
        YtdlpHostService ytdlp,
        DownloadRequest request,
        SocialPlatform platform,
        ContentKind kind,
        string? shortCode,
        IContentRepository repository,
        IMediaStorageService storage,
        CancellationToken cancellationToken)
    {
        var ytdlpResult = await ytdlp.DownloadAsync(request, platform, cancellationToken);
        if (!ytdlpResult.Success || ytdlpResult.Payload is null)
        {
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = ytdlpResult.ErrorMessage ?? "Ошибка скачивания через yt-dlp."
            };
        }

        var tempDir = ytdlpResult.Payload.FilePaths.FirstOrDefault() is string first
            ? Path.GetDirectoryName(first)
            : null;

        try
        {
            var content = YtdlpContentMapper.ToContentItem(
                ytdlpResult.Payload.Video,
                request,
                platform,
                kind,
                shortCode,
                ytdlpResult.Payload.AuthorUsername);

            content.StorageRelativePath = ContentPathBuilder.BuildRelativePath(
                content, request.UsePostedDateForFolder);
            content = await repository.SaveContentAsync(content, cancellationToken);

            var order = 0;
            string? firstVideoPath = null;

            foreach (var filePath in ytdlpResult.Payload.FilePaths)
            {
                var mediaType = YtdlpContentMapper.GuessMediaType(filePath);
                if (mediaType == MediaType.Video && firstVideoPath is null)
                    firstVideoPath = filePath;

                var file = await storage.SaveFromLocalFileAsync(
                    content,
                    filePath,
                    order++,
                    request.UsePostedDateForFolder,
                    cancellationToken);
                content.MediaFiles.Add(file);
            }

            await storage.TrySaveVideoThumbnailAsync(
                content,
                YtdlpThumbnailUrlPicker.Pick(ytdlpResult.Payload.Video),
                firstVideoPath ?? content.MediaFiles.FirstOrDefault(m => m.MediaType == MediaType.Video)?.LocalPath,
                request.UsePostedDateForFolder,
                cancellationToken);

            content = await repository.SaveContentAsync(content, cancellationToken);
            await DownloadTagHelper.ApplyTagsAsync(repository, content, request, cancellationToken);
            return new DownloadResult { Success = true, Content = content };
        }
        finally
        {
            YtdlpHostService.TryDeleteDirectory(tempDir);
        }
    }
}
