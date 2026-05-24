using CommunityToolkit.Maui.Views;
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

    public ImageSource? ImageSource
    {
        get
        {
            var path = IsVideo && !string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath)
                ? ThumbnailPath!
                : LocalPath;

            return File.Exists(path) ? ImageSource.FromFile(path) : null;
        }
    }

    public MediaSource? VideoSource =>
        IsVideo && File.Exists(LocalPath)
            ? MediaSource.FromFile(Path.GetFullPath(LocalPath))
            : null;
}
