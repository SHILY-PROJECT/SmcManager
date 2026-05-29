using System.Windows.Input;
using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;
using SmcManager.Maui.Views;

namespace SmcManager.Maui.Views.Controls;

/// <summary>
/// Карточка скачанного контента для списков.
/// </summary>
public partial class ContentCardView : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(ContentItemDisplayModel), typeof(ContentCardView),
        propertyChanged: OnItemChanged);

    public static readonly BindableProperty DeleteCommandProperty = BindableProperty.Create(
        nameof(DeleteCommand), typeof(ICommand), typeof(ContentCardView));

    public ContentItemDisplayModel? Item
    {
        get => (ContentItemDisplayModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public ImageSource? ThumbnailSource { get; private set; }
    public ImageSource? PlatformIcon { get; private set; }
    public string AuthorUsername { get; private set; } = string.Empty;
    public string? CaptionPreview { get; private set; }

    public string? CommentPreview { get; private set; }

    public bool HasCommentPreview { get; private set; }
    public string KindLabel { get; private set; } = string.Empty;
    public int MediaCount { get; private set; }

    public ContentCardView() => InitializeComponent();

    private static void OnItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ContentCardView view || newValue is not ContentItemDisplayModel item)
            return;

        view.ThumbnailSource = RemoteImageCache.FromLocalPath(item.ThumbnailPath);
        view.PlatformIcon = item.PlatformIcon;
        view.AuthorUsername = $"@{item.AuthorUsername}";
        view.CaptionPreview = item.CaptionPreview ?? "Без описания";
        view.CommentPreview = string.IsNullOrWhiteSpace(item.CommentPreview)
            ? null
            : $"Комментарий: {item.CommentPreview}";
        view.HasCommentPreview = !string.IsNullOrWhiteSpace(view.CommentPreview);
        view.KindLabel = item.KindLabel;
        view.MediaCount = item.MediaCount;
        view.OnPropertyChanged(nameof(ThumbnailSource));
        view.OnPropertyChanged(nameof(PlatformIcon));
        view.OnPropertyChanged(nameof(AuthorUsername));
        view.OnPropertyChanged(nameof(CaptionPreview));
        view.OnPropertyChanged(nameof(CommentPreview));
        view.OnPropertyChanged(nameof(HasCommentPreview));
        view.OnPropertyChanged(nameof(KindLabel));
        view.OnPropertyChanged(nameof(MediaCount));
    }

    private bool _isNavigating;

    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (Item is null || _isNavigating)
            return;

        _isNavigating = true;
        try
        {
            await Shell.Current.GoToAsync(
                nameof(ContentDetailPage),
                new Dictionary<string, object> { ["contentId"] = Item.Id.ToString() });
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
