using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Сохраняет медиа в каталог: downloads/{platform}/{account}/{postId_datetime}/.
/// </summary>
public class MediaStorageService : IMediaStorageService
{
    private readonly string _rootPath;
    private readonly VideoThumbnailService _thumbnails;

    public MediaStorageService(string rootPath, VideoThumbnailService thumbnails)
    {
        _rootPath = rootPath;
        _thumbnails = thumbnails;
    }

    public string GetContentDirectory(ContentItem content, bool usePostedDateForFolder = true)
    {
        if (!string.IsNullOrWhiteSpace(content.StorageRelativePath))
            return Path.Combine(_rootPath, content.StorageRelativePath);

        var relative = ContentPathBuilder.BuildRelativePath(content, usePostedDateForFolder);
        return Path.Combine(_rootPath, relative);
    }

    public async Task<MediaFile> SaveFromUrlAsync(
        ContentItem content,
        string remoteUrl,
        MediaType mediaType,
        int orderIndex,
        HttpClient httpClient,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content.StorageRelativePath))
            content.StorageRelativePath = ContentPathBuilder.BuildRelativePath(content, usePostedDateForFolder);

        var dir = GetContentDirectory(content, usePostedDateForFolder);
        Directory.CreateDirectory(dir);

        var extension = mediaType == MediaType.Video ? ".mp4" : ".jpg";
        var fileName = $"{orderIndex:D2}{extension}";
        var localPath = Path.Combine(dir, fileName);

        await using var stream = await httpClient.GetStreamAsync(remoteUrl, cancellationToken);
        await using var file = File.Create(localPath);
        await stream.CopyToAsync(file, cancellationToken);

        return new MediaFile
        {
            ContentItemId = content.Id,
            MediaType = mediaType,
            LocalPath = localPath,
            RemoteUrl = remoteUrl,
            OrderIndex = orderIndex
        };
    }

    public async Task<MediaFile> SaveFromLocalFileAsync(
        ContentItem content,
        string sourceFilePath,
        int orderIndex,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content.StorageRelativePath))
            content.StorageRelativePath = ContentPathBuilder.BuildRelativePath(content, usePostedDateForFolder);

        var dir = GetContentDirectory(content, usePostedDateForFolder);
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrEmpty(ext))
            ext = ".bin";

        var fileName = $"{orderIndex:D2}{ext}";
        var localPath = Path.Combine(dir, fileName);

        await using (var source = File.OpenRead(sourceFilePath))
        await using (var dest = File.Create(localPath))
            await source.CopyToAsync(dest, cancellationToken);

        var mediaType = ext.ToLowerInvariant() is ".mp4" or ".webm" or ".mkv" or ".mov" or ".m4v"
            ? MediaType.Video
            : MediaType.Image;

        return new MediaFile
        {
            ContentItemId = content.Id,
            MediaType = mediaType,
            LocalPath = localPath,
            RemoteUrl = sourceFilePath,
            OrderIndex = orderIndex
        };
    }

    public async Task TrySaveVideoThumbnailAsync(
        ContentItem content,
        string? remoteThumbnailUrl,
        string? localVideoPath,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default)
    {
        var hasVideo = content.MediaFiles.Any(m => m.MediaType == MediaType.Video)
                       || IsVideoPath(localVideoPath);

        if (!hasVideo)
            return;

        var dir = GetContentDirectory(content, usePostedDateForFolder);
        await _thumbnails.TrySaveThumbnailAsync(dir, remoteThumbnailUrl, localVideoPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsVideoPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var ext = Path.GetExtension(path);
        return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase);
    }
}
