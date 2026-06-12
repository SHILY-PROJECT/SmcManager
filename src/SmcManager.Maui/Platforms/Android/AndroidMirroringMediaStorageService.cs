using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Infrastructure.Services;

namespace SmcManager.Maui.Platforms.Android;

/// <summary>
/// Сохраняет медиа в AppData и дублирует файлы в Pictures/SmcManager.
/// </summary>
public sealed class AndroidMirroringMediaStorageService : IMediaStorageService
{
    private readonly MediaStorageService _inner;

    public AndroidMirroringMediaStorageService(string rootPath, VideoThumbnailService thumbnails) =>
        _inner = new MediaStorageService(rootPath, thumbnails);

    public string GetContentDirectory(ContentItem content, bool usePostedDateForFolder = true) =>
        _inner.GetContentDirectory(content, usePostedDateForFolder);

    public async Task<MediaFile> SaveFromUrlAsync(
        ContentItem content,
        string remoteUrl,
        MediaType mediaType,
        int orderIndex,
        HttpClient httpClient,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default)
    {
        var file = await _inner.SaveFromUrlAsync(
            content, remoteUrl, mediaType, orderIndex, httpClient, usePostedDateForFolder, cancellationToken);
        AndroidPublicMediaExporter.TryMirror(file.LocalPath);
        return file;
    }

    public async Task<MediaFile> SaveFromLocalFileAsync(
        ContentItem content,
        string sourceFilePath,
        int orderIndex,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default)
    {
        var file = await _inner.SaveFromLocalFileAsync(
            content, sourceFilePath, orderIndex, usePostedDateForFolder, cancellationToken);
        AndroidPublicMediaExporter.TryMirror(file.LocalPath);
        return file;
    }

    public async Task TrySaveVideoThumbnailAsync(
        ContentItem content,
        string? remoteThumbnailUrl,
        string? localVideoPath,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default)
    {
        await _inner.TrySaveVideoThumbnailAsync(
            content, remoteThumbnailUrl, localVideoPath, usePostedDateForFolder, cancellationToken);

        var dir = _inner.GetContentDirectory(content, usePostedDateForFolder);
        var thumb = Path.Combine(dir, "thumb.jpg");
        AndroidPublicMediaExporter.TryMirror(thumb);
    }
}
