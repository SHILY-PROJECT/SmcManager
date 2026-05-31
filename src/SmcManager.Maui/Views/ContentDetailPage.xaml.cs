using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
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
    private TimeSpan? _pendingSeekPosition;
    private bool _pendingAutoPlay;

    public ContentDetailPage(ContentDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.SlideNavigationRequested += OnSlideNavigationRequested;
        viewModel.VideoPrepareCompleted += OnVideoPrepareCompleted;
        VideoPlayer.MediaOpened += OnVideoMediaOpened;
        WeakReferenceMessenger.Default.Register(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is not ContentDetailViewModel vm)
            return;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.VideoPrepareCompleted -= OnVideoPrepareCompleted;
        }

        _viewModel = vm;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.VideoPrepareCompleted += OnVideoPrepareCompleted;

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
            vm.VideoPrepareCompleted -= OnVideoPrepareCompleted;
            vm.StopCurrentVideo();
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.VideoPrepareCompleted -= OnVideoPrepareCompleted;
            _viewModel = null;
        }

        _isInitializingCarousel = false;
        _pendingAutoPlay = false;
        _pendingSeekPosition = null;
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
            CaptureVideoResumeState();
            ApplyThemedIcons();

            if (_viewModel is not null)
            {
                Dispatcher.Dispatch(async () =>
                {
                    ApplyCarouselPosition(_viewModel.CurrentSlideIndex);
#if ANDROID
                    if (_viewModel.ShowCurrentVideoPlayer || _viewModel.IsVideoPlaybackRequested)
                    {
                        await Task.Delay(150);
                        await _viewModel.PrepareCurrentSlideMediaAsync(
                            forPlaybackRequest: true,
                            forceSurfaceRefresh: true);
                    }
#endif
                });
            }

            return;
        }

        if (e.PropertyName == nameof(ContentDetailViewModel.MediaCarouselHeight) && _viewModel is not null)
            Dispatcher.Dispatch(() => ApplyCarouselPosition(_viewModel.CurrentSlideIndex));
    }

    private void OnVideoPrepareCompleted() => TryStartVideoPlayback();

    private void OnVideoMediaOpened(object? sender, EventArgs e)
    {
        if (_pendingSeekPosition is { } seekPosition && seekPosition > TimeSpan.Zero)
        {
            VideoPlayer.SeekTo(seekPosition);
            _pendingSeekPosition = null;
        }

        if (_pendingAutoPlay || _viewModel?.IsVideoPlaybackRequested == true)
        {
            _pendingAutoPlay = false;
            TryStartVideoPlayback();
        }
    }

    private void CaptureVideoResumeState()
    {
        if (!VideoPlayer.IsVisible || VideoPlayer.Source is null)
            return;

        try
        {
            _pendingSeekPosition = VideoPlayer.Position;
            _pendingAutoPlay = VideoPlayer.CurrentState
                is MediaElementState.Playing
                or MediaElementState.Buffering;
        }
        catch
        {
            _pendingSeekPosition = null;
            _pendingAutoPlay = false;
        }
    }

    private void TryStartVideoPlayback()
    {
        if (!VideoPlayer.IsVisible || VideoPlayer.Source is null)
            return;

        if (_viewModel?.IsVideoPlaybackRequested != true)
            return;

        if (VideoPlayer.CurrentState is MediaElementState.Playing or MediaElementState.Buffering)
            return;

        try
        {
            VideoPlayer.Play();
        }
        catch
        {
            // MediaElement may not be ready yet; MediaOpened will retry.
        }
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
        ThemedIconHelper.SetImageSource(ShareCurrentMediaButton, palette.ShareIcon);
        ThemedIconHelper.SetImageSource(ShareAllContentButton, palette.ShareIcon);
        ThemedIconHelper.SetImageSource(OpenSourceButton, palette.OpenSourceIcon);
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
