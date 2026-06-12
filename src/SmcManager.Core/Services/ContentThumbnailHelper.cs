using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Services;

/// <summary>
/// Путь к превью поста (thumb.jpg или первое изображение карусели).
/// </summary>
public static class ContentThumbnailHelper
{
    public const string ThumbnailFileName = "thumb.jpg";

    public static string GetThumbnailFilePath(string contentDirectory) =>
        Path.Combine(contentDirectory, ThumbnailFileName);

    public static string? ResolveThumbnailPath(ContentItem item, string downloadsRoot)
    {
        if (!string.IsNullOrWhiteSpace(item.StorageRelativePath))
        {
            var thumb = Path.Combine(downloadsRoot, item.StorageRelativePath, ThumbnailFileName);
            if (File.Exists(thumb))
                return thumb;
        }

        foreach (var media in item.MediaFiles.OrderBy(m => m.OrderIndex))
        {
            if (media.MediaType == MediaType.Image
                && File.Exists(media.LocalPath)
                && !IsThumbnailFile(media.LocalPath))
            {
                return media.LocalPath;
            }
        }

        return null;
    }

    public static bool IsThumbnailFile(string path) =>
        Path.GetFileName(path).Equals(ThumbnailFileName, StringComparison.OrdinalIgnoreCase);

    public static bool HasVideoMedia(ContentItem item) =>
        item.MediaFiles.Any(m => m.MediaType == MediaType.Video);

    public static bool HasAvailableMedia(ContentItem item) =>
        item.MediaFiles.Any(m =>
            !string.IsNullOrWhiteSpace(m.LocalPath)
            && !IsThumbnailFile(m.LocalPath)
            && File.Exists(m.LocalPath));
}
