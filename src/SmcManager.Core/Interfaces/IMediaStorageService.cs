using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Сохранение медиафайлов на диск приложения.
/// </summary>
public interface IMediaStorageService
{
    Task<MediaFile> SaveFromUrlAsync(
        ContentItem content,
        string remoteUrl,
        MediaType mediaType,
        int orderIndex,
        HttpClient httpClient,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default);

    Task<MediaFile> SaveFromLocalFileAsync(
        ContentItem content,
        string sourceFilePath,
        int orderIndex,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default);

    string GetContentDirectory(ContentItem content, bool usePostedDateForFolder = true);

    /// <summary>Сохраняет thumb.jpg для превью в списках (если есть видео).</summary>
    Task TrySaveVideoThumbnailAsync(
        ContentItem content,
        string? remoteThumbnailUrl,
        string? localVideoPath,
        bool usePostedDateForFolder = true,
        CancellationToken cancellationToken = default);
}
