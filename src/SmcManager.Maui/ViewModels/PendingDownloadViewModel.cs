using CommunityToolkit.Mvvm.ComponentModel;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Активное или завершённое скачивание на вкладке «Скачать».
/// </summary>
public partial class PendingDownloadViewModel : ObservableObject
{
    public PendingDownloadViewModel(string url, string? previewTitle, string? previewAuthor)
    {
        Url = url;
        var author = string.IsNullOrWhiteSpace(previewAuthor) ? null : previewAuthor.Trim();
        var title = string.IsNullOrWhiteSpace(previewTitle) ? null : previewTitle.Trim();
        DisplayTitle = title ?? author ?? ShortUrl(url);
        DisplaySubtitle = title is not null && author is not null ? author : ShortUrl(url);
    }

    public string Url { get; }

    public string DisplayTitle { get; }

    public string DisplaySubtitle { get; }

    [ObservableProperty]
    private string _status = "В очереди…";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isFailed;

    public bool IsFinished => IsCompleted || IsFailed;

    private static string ShortUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host + uri.AbsolutePath;
        return url.Length > 48 ? url[..48] + "…" : url;
    }
}
