using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Экран просмотра скачанного контента.
/// </summary>
public partial class ContentDetailPage : ContentPage, IRecipient<ThemeChangedMessage>
{
    private bool _carouselHandlersAttached;
    private bool _isUpdatingCarousel;
    private bool _isInitializingCarousel;
    private ContentDetailViewModel? _viewModel;

    public ContentDetailPage(ContentDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.SlideNavigationRequested += OnSlideNavigationRequested;
        WeakReferenceMessenger.Default.Register(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is not ContentDetailViewModel vm)
            return;

        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = vm;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        AttachCarouselHandlers();
        ApplyThemedIcons();
        _ = LoadPageContentAsync(vm);
    }

    private async Task LoadPageContentAsync(ContentDetailViewModel vm)
    {
        await vm.LoadForDisplayAsync();
        await InitializeCarouselWhenReadyAsync(vm);
    }

    private async Task InitializeCarouselWhenReadyAsync(ContentDetailViewModel vm)
    {
        if (vm.MediaSlides.Count == 0 || _isInitializingCarousel)
            return;

        _isInitializingCarousel = true;
        try
        {
            await InitializeCarouselAsync(vm);
        }
        catch
        {
            // carousel init is best-effort; avoid crash on freshly downloaded media
        }
        finally
        {
            _isInitializingCarousel = false;
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is ContentDetailViewModel vm)
        {
            vm.SlideNavigationRequested -= OnSlideNavigationRequested;
            vm.StopCurrentVideo();
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        _isInitializingCarousel = false;
        WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
        DetachCarouselHandlers();
        base.OnDisappearing();
    }

    private async Task InitializeCarouselAsync(ContentDetailViewModel vm)
    {
        _isUpdatingCarousel = true;
        try
        {
            if (MediaCarousel.Handler is null)
                await Task.Delay(100);

            vm.CurrentSlideIndex = 0;

            if (MediaCarousel.Handler is not null && vm.MediaSlides.Count > 0)
                CarouselSlideNavigator.NavigateTo(MediaCarousel, 0, vm.MediaSlides.Count);

#if !ANDROID
            await vm.PrepareCurrentSlideMediaAsync();
#endif
        }
        finally
        {
            _isUpdatingCarousel = false;
        }
    }

    private void OnSlideNavigationRequested(int position) => ApplyCarouselPosition(position);

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContentDetailViewModel.IsMediaExpanded))
        {
            ApplyThemedIcons();
            if (_viewModel is not null)
                Dispatcher.Dispatch(() => ApplyCarouselPosition(_viewModel.CurrentSlideIndex));
            return;
        }

        if (e.PropertyName == nameof(ContentDetailViewModel.MediaCarouselHeight) && _viewModel is not null)
            Dispatcher.Dispatch(() => ApplyCarouselPosition(_viewModel.CurrentSlideIndex));
    }

    public void Receive(ThemeChangedMessage message) => ApplyThemedIcons(message.Palette);

    private void ApplyThemedIcons(ThemePalette? palette = null)
    {
        palette ??= ThemePalette.For(
            Application.Current?.UserAppTheme == AppTheme.Dark
                ? Core.Enums.AppColorTheme.Dark
                : Core.Enums.AppColorTheme.Light);

        var isExpanded = BindingContext is ContentDetailViewModel vm && vm.IsMediaExpanded;
        ThemedIconHelper.ApplyCarouselIcons(
            CarouselPrevButton,
            CarouselNextButton,
            MediaExpandButton,
            isExpanded,
            palette);
        ThemedIconHelper.SetSource(EditCaptionButton, palette.EditCaptionIcon);
        ThemedIconHelper.SetSource(EditCommentButton, palette.EditCaptionIcon);
        ThemedIconHelper.SetImageSource(DeleteContentButton, palette.DeleteIcon);
        ThemedIconHelper.SetImageSource(OpenInExplorerButton, palette.ExplorerIcon);
        ThemedIconHelper.SetImageSource(OpenFolderButton, palette.FolderIcon);
        ThemedIconHelper.SetImageSource(OpenSourceButton, palette.OpenSourceIcon);
        ThemedIconHelper.SetSource(ShareMediaButton, palette.ShareIcon);
    }

    private void AttachCarouselHandlers()
    {
        if (_carouselHandlersAttached)
            return;

        MediaCarousel.PositionChanged += OnMediaCarouselPositionChanged;
        _carouselHandlersAttached = true;
    }

    private void DetachCarouselHandlers()
    {
        if (!_carouselHandlersAttached)
            return;

        MediaCarousel.PositionChanged -= OnMediaCarouselPositionChanged;
        _carouselHandlersAttached = false;
    }

    private void OnMediaCarouselPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (_isUpdatingCarousel)
            return;

        if (BindingContext is ContentDetailViewModel vm)
            vm.SetSlideIndexFromCarousel(e.CurrentPosition);
    }

    private void ApplyCarouselPosition(int position)
    {
        if (BindingContext is not ContentDetailViewModel vm || vm.MediaSlides.Count == 0)
            return;

        position = Math.Clamp(position, 0, vm.MediaSlides.Count - 1);

        _isUpdatingCarousel = true;
        try
        {
            if (vm.CurrentSlideIndex != position)
                vm.CurrentSlideIndex = position;

            if (MediaCarousel.Handler is not null)
                CarouselSlideNavigator.NavigateTo(MediaCarousel, position, vm.MediaSlides.Count);

#if !ANDROID
            _ = vm.PrepareCurrentSlideMediaAsync();
#endif
        }
        finally
        {
            _isUpdatingCarousel = false;
        }
    }
}
