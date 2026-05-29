using SmcManager.Core.Enums;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Один слайд медиа на экране деталей (фото или видео).
/// </summary>
public class MediaSlideViewModel
{
    public string LocalPath { get; init; } = string.Empty;

    public string? ThumbnailPath { get; init; }

    public MediaType MediaType { get; init; }

    public bool IsImage => MediaType == MediaType.Image;

    public bool IsVideo => MediaType == MediaType.Video;

    public string? DisplayImagePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath))
                return ThumbnailPath;

            if (IsImage && File.Exists(LocalPath))
                return LocalPath;

            return null;
        }
    }
}
