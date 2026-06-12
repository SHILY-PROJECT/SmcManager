using System.Windows.Input;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls.Shapes;
using SmcManager.Core.Interfaces;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views.Controls;

/// <summary>
/// Карточка скачанного контента для списков.
/// Android: долгое нажатие — поделиться / удалить.
/// Windows: контекстное меню по ПКМ — поделиться / удалить.
/// </summary>
public partial class ContentCardView : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(ContentItemDisplayModel), typeof(ContentCardView),
        propertyChanged: OnItemChanged);

    public static readonly BindableProperty DeleteCommandProperty = BindableProperty.Create(
        nameof(DeleteCommand), typeof(ICommand), typeof(ContentCardView));

    public static readonly BindableProperty OpenCommandProperty = BindableProperty.Create(
        nameof(OpenCommand), typeof(ICommand), typeof(ContentCardView));

    public static readonly BindableProperty IsNavigationEnabledProperty = BindableProperty.Create(
        nameof(IsNavigationEnabled), typeof(bool), typeof(ContentCardView), true);

    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(
        nameof(IsSwipeEnabled), typeof(bool), typeof(ContentCardView), true);

    private TouchBehavior? _androidLongPressBehavior;

    public bool IsNavigationEnabled
    {
        get => (bool)GetValue(IsNavigationEnabledProperty);
        set => SetValue(IsNavigationEnabledProperty, value);
    }

    public bool IsSwipeEnabled
    {
        get => (bool)GetValue(IsSwipeEnabledProperty);
        set => SetValue(IsSwipeEnabledProperty, value);
    }

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

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public ImageSource? ThumbnailSource { get; private set; }
    public string PlatformIconFile { get; private set; } = string.Empty;
    public string AuthorUsername { get; private set; } = string.Empty;
    public string? CaptionPreview { get; private set; }

    public string? CommentPreview { get; private set; }

    public bool HasCommentPreview { get; private set; }
    public string KindLabel { get; private set; } = string.Empty;
    public int MediaCount { get; private set; }
    public IReadOnlyList<ContentTagDisplayModel> Tags { get; private set; } = [];
    public bool HasTags { get; private set; }

    public ContentCardView()
    {
        InitializeComponent();
        InitPlatformInteractions();
    }

    partial void InitPlatformInteractions();

    private void SetupAndroidLongPress()
    {
        if (_androidLongPressBehavior is not null)
            CardBorder.Behaviors.Remove(_androidLongPressBehavior);

        _androidLongPressBehavior = new TouchBehavior
        {
            LongPressCommand = new Command(async () => await ShowContextActionsAsync().ConfigureAwait(false))
        };
        CardBorder.Behaviors.Add(_androidLongPressBehavior);
    }

    private static void OnItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ContentCardView view || newValue is not ContentItemDisplayModel item)
            return;

        view.ThumbnailSource = RemoteImageCache.FromLocalPath(item.ThumbnailPath);
        view.PlatformIconFile = item.PlatformIconFile;
        view.AuthorUsername = $"@{item.AuthorUsername}";
        view.CaptionPreview = item.CaptionPreview ?? "Без описания";
        view.CommentPreview = string.IsNullOrWhiteSpace(item.CommentPreview)
            ? null
            : $"Комментарий: {item.CommentPreview}";
        view.HasCommentPreview = !string.IsNullOrWhiteSpace(view.CommentPreview);
        view.KindLabel = item.KindLabel;
        view.MediaCount = item.MediaCount;
        view.Tags = item.Tags;
        view.HasTags = item.HasTags;
        view.OnPropertyChanged(nameof(ThumbnailSource));
        view.OnPropertyChanged(nameof(PlatformIconFile));
        view.OnPropertyChanged(nameof(AuthorUsername));
        view.OnPropertyChanged(nameof(CaptionPreview));
        view.OnPropertyChanged(nameof(CommentPreview));
        view.OnPropertyChanged(nameof(HasCommentPreview));
        view.OnPropertyChanged(nameof(KindLabel));
        view.OnPropertyChanged(nameof(MediaCount));
        view.OnPropertyChanged(nameof(Tags));
        view.OnPropertyChanged(nameof(HasTags));
    }

    private bool _isNavigating;

    private async void OnCardTapped(object? sender, EventArgs e)
    {
        if (!IsNavigationEnabled || Item is null || _isNavigating)
            return;

        _isNavigating = true;
        try
        {
            if (OpenCommand?.CanExecute(Item) == true)
            {
                OpenCommand.Execute(Item);
                return;
            }

            if (Shell.Current?.CurrentPage is Page page)
                page.Unfocus();

            await ContentNavigationHelper.OpenDetailAsync(Item.Id);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async Task ShowContextActionsAsync()
    {
        if (Item is null)
            return;

        var page = GetHostPage();
        if (page is null)
            return;

        var menu = new ContentCardContextMenuView();
        menu.AttachToPage(page);

        var options = new PopupOptions
        {
            CanBeDismissedByTappingOutsideOfPopup = true,
            PageOverlayColor = Color.FromArgb("#66000000"),
            Shape = new RoundRectangle
            {
                CornerRadius = 0,
                StrokeThickness = 0,
                Fill = new SolidColorBrush(Colors.Transparent),
                Stroke = new SolidColorBrush(Colors.Transparent)
            },
            Shadow = null
        };

        var popupResult = await page
            .ShowPopupAsync<ContentCardContextAction>(menu, options)
            .ConfigureAwait(true);

        if (popupResult.WasDismissedByTappingOutsideOfPopup)
            return;

        if (popupResult.Result == ContentCardContextAction.Share)
            await ShareItemAsync().ConfigureAwait(false);
        else if (popupResult.Result == ContentCardContextAction.Delete)
            await DeleteItemAsync().ConfigureAwait(false);
    }

    private async Task ShareItemAsync()
    {
        if (Item is null)
            return;

        var repository = ResolveService<IContentRepository>();
        var mediaShare = ResolveService<IMediaShareService>();
        var toast = ResolveService<BottomToastService>();
        if (repository is null || mediaShare is null || toast is null)
            return;

        await ContentShareHelper.ShareContentAsync(repository, mediaShare, toast, Item.Id)
            .ConfigureAwait(false);
    }

    private async Task DeleteItemAsync()
    {
        if (Item is null)
            return;

        if (DeleteCommand?.CanExecute(Item) == true)
        {
            DeleteCommand.Execute(Item);
            return;
        }

        var repository = ResolveService<IContentRepository>();
        if (repository is null)
            return;

        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(repository, Item).ConfigureAwait(false))
            return;

        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
    }

    private Page? GetHostPage() =>
        Window?.Page
        ?? Shell.Current?.CurrentPage
        ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    private static T? ResolveService<T>() where T : class =>
        IPlatformApplication.Current?.Services?.GetService<T>();
}
