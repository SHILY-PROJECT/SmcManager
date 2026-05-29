using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using SmcManager.Core.Enums;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Один слайд медиа на экране деталей (фото или видео).
/// </summary>
public partial class MediaSlideViewModel : ObservableObject
{
    public string LocalPath { get; init; } = string.Empty;

    public string? ThumbnailPath { get; init; }

    public MediaType MediaType { get; init; }

    public bool IsImage => MediaType == MediaType.Image;

    public bool IsVideo => MediaType == MediaType.Video;

    [ObservableProperty]
    private bool _isActive;

    public string? DisplayImagePath
    {
        get
        {
            if (IsVideo)
            {
                if (!string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath))
                    return ThumbnailPath;

                return null;
            }

            return File.Exists(LocalPath) ? LocalPath : null;
        }
    }

    public string? PosterPath =>
        !string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath)
            ? ThumbnailPath
            : null;

    public MediaSource? ActiveVideoSource =>
        IsActive && IsVideo && File.Exists(LocalPath)
            ? MediaSource.FromFile(Path.GetFullPath(LocalPath))
            : null;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(ActiveVideoSource));
}
