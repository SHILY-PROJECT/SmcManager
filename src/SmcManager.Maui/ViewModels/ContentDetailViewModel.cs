using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Просмотр скачанного поста: медиа, описание, открытие в проводнике.
/// </summary>
[QueryProperty(nameof(ContentId), "contentId")]
public partial class ContentDetailViewModel : ObservableObject, IRecipient<TagsChangedMessage>, IRecipient<TagSortChangedMessage>
{
    private readonly IContentRepository _repository;
    private int _loadedContentId;
    private readonly IFileExplorerService _fileExplorer;
    private readonly ILinkLauncherService _linkLauncher;
    private readonly IAppStoragePaths _storagePaths;
    private readonly ISettingsService _settings;
    private readonly TagListService _tagList;
    private readonly TagCreationService _tagCreation;
    private readonly TagColorPickerService _colorPicker;
    private readonly BottomToastService _toast;
    private readonly IMediaShareService _mediaShare;

    private ContentItem? _content;
    private DateTime _enteredAtUtc;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _mediaPrepareGate = new(1, 1);
    private int _loadVersion;
    private CancellationTokenSource? _mediaPrepareCts;
    private bool _suppressTagsChangedReload;
#if ANDROID
    private bool _videoPlaybackRequested;
#endif

    public IReadOnlyList<string> EmojiSuggestions { get; } = TagEmojiLibrary.Suggested;

    public ContentDetailViewModel(
        IContentRepository repository,
        IFileExplorerService fileExplorer,
        ILinkLauncherService linkLauncher,
        IAppStoragePaths storagePaths,
        ISettingsService settings,
        TagListService tagList,
        TagCreationService tagCreation,
        TagColorPickerService colorPicker,
        BottomToastService toast,
        IMediaShareService mediaShare)
    {
        _repository = repository;
        _fileExplorer = fileExplorer;
        _linkLauncher = linkLauncher;
        _storagePaths = storagePaths;
        _settings = settings;
        _tagList = tagList;
        _tagCreation = tagCreation;
        _colorPicker = colorPicker;
        _toast = toast;
        _mediaShare = mediaShare;
        WeakReferenceMessenger.Default.Register<TagsChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<TagSortChangedMessage>(this);
    }

    public void Receive(TagsChangedMessage message)
    {
        if (_suppressTagsChangedReload)
        {
            _suppressTagsChangedReload = false;
            return;
        }

        _ = LoadTagEditorAsync();
    }

    public void Receive(TagSortChangedMessage message) => _ = LoadTagEditorAsync();

    [ObservableProperty]
    private string _contentId = string.Empty;

    partial void OnContentIdChanged(string value)
    {
        if (!int.TryParse(value, out var id) || id <= 0)
            return;

        _enteredAtUtc = DateTime.UtcNow;
        if (_loadedContentId == id && IsContentLoaded)
            return;

        _loadedContentId = 0;
        _ = LoadAsync();
    }

    public ObservableCollection<MediaSlideViewModel> MediaSlides { get; } = [];

    public ObservableCollection<TagChipViewModel> TagChips { get; } = [];

    [ObservableProperty]
    private bool _isNewTagPanelVisible;

    [ObservableProperty]
    private string _newTagName = string.Empty;

    [ObservableProperty]
    private string _selectedTagColor = TagColorPresets.Default;

    [ObservableProperty]
    private string? _newTagStatusMessage;

    [ObservableProperty]
    private string _authorTitle = string.Empty;

    [ObservableProperty]
    private string? _caption;

    [ObservableProperty]
    private bool _hasCaption;

    [ObservableProperty]
    private bool _isContentLoaded;

    [ObservableProperty]
    private string? _userComment;

    [ObservableProperty]
    private bool _isDescriptionExpanded;

    [ObservableProperty]
    private bool _isEditingCaption;

    [ObservableProperty]
    private string _captionDraft = string.Empty;

    [ObservableProperty]
    private bool _isEditingComment;

    [ObservableProperty]
    private string _commentDraft = string.Empty;

    [ObservableProperty]
    private int _currentSlideIndex;

    [ObservableProperty]
    private string _slideIndicator = string.Empty;

    [ObservableProperty]
    private bool _hasMultipleSlides;

    [ObservableProperty]
    private bool _hasMediaSlides;

    [ObservableProperty]
    private bool _canOpenSource;

    [ObservableProperty]
    private bool _isMediaExpanded;

    [ObservableProperty]
    private bool _showCurrentVideoPlayer;

    [ObservableProperty]
    private MediaSource? _currentVideoSource;

    public double MediaCarouselHeight => IsMediaExpanded ? ExpandedMediaHeight : DefaultMediaHeight;

    public double CollapsedMediaHeight => DefaultMediaHeight;

    private const double DefaultMediaHeight = 320;

    private const double CarouselNavButtonSize = 48;

    private const double CarouselPagerHeight = 42;

    public double MediaContentHeight =>
        HasMultipleSlides
            ? Math.Max(CarouselNavButtonSize, MediaCarouselHeight - CarouselPagerHeight)
            : MediaCarouselHeight;

    private static double ExpandedMediaHeight
    {
        get
        {
            var display = DeviceDisplay.MainDisplayInfo;
            var heightDp = display.Height / display.Density;
            return Math.Clamp(heightDp * 0.72, DefaultMediaHeight + 80, 720);
        }
    }

    public int DescriptionMaxLines => IsDescriptionExpanded ? int.MaxValue : 4;

    public string DescriptionToggleText => IsDescriptionExpanded ? "Свернуть описание" : "Показать описание";

    public bool ShowCaptionReadOnly => HasCaption && !IsEditingCaption;

    public bool ShowCaptionPlaceholder => IsContentLoaded && !HasCaption && !IsEditingCaption;

    public bool ShowEditCaptionButton => IsContentLoaded && !IsEditingCaption;

    public bool CanToggleDescription => HasCaption && !IsEditingCaption && (Caption?.Length ?? 0) > 120;

    public bool HasUserComment => !string.IsNullOrWhiteSpace(UserComment);

    public bool ShowCommentReadOnly => HasUserComment && !IsEditingComment;

    public bool ShowCommentPlaceholder => IsContentLoaded && !HasUserComment && !IsEditingComment;

    public bool ShowEditCommentButton => IsContentLoaded && !IsEditingComment;

    partial void OnIsContentLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCaptionPlaceholder));
        OnPropertyChanged(nameof(ShowEditCaptionButton));
        OnPropertyChanged(nameof(ShowCommentPlaceholder));
        OnPropertyChanged(nameof(ShowEditCommentButton));
    }

    partial void OnIsEditingCaptionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCaptionReadOnly));
        OnPropertyChanged(nameof(ShowCaptionPlaceholder));
        OnPropertyChanged(nameof(ShowEditCaptionButton));
        OnPropertyChanged(nameof(CanToggleDescription));
    }

    partial void OnIsEditingCommentChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCommentReadOnly));
        OnPropertyChanged(nameof(ShowCommentPlaceholder));
        OnPropertyChanged(nameof(ShowEditCommentButton));
    }

    partial void OnUserCommentChanged(string? value)
    {
        OnPropertyChanged(nameof(HasUserComment));
        OnPropertyChanged(nameof(ShowCommentReadOnly));
        OnPropertyChanged(nameof(ShowCommentPlaceholder));
    }

    partial void OnIsDescriptionExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(DescriptionMaxLines));
        OnPropertyChanged(nameof(DescriptionToggleText));
    }

    partial void OnCaptionChanged(string? value)
    {
        HasCaption = !string.IsNullOrWhiteSpace(value);
        OnPropertyChanged(nameof(CanToggleDescription));
        OnPropertyChanged(nameof(ShowCaptionPlaceholder));
        OnPropertyChanged(nameof(ShowCaptionReadOnly));
    }

    public bool ShowCarouselPrevious => HasMultipleSlides && CurrentSlideIndex > 0;

    public bool ShowCarouselNext => HasMultipleSlides && CurrentSlideIndex < MediaSlides.Count - 1;

    public bool ShowVideoPlayPrompt
    {
        get
        {
#if ANDROID
            var slide = GetCurrentSlide();
            return slide is { IsVideo: true }
                   && !_videoPlaybackRequested
                   && File.Exists(slide.LocalPath);
#else
            return false;
#endif
        }
    }

    /// <summary>Запрос смены слайда с экрана (обрабатывается ContentDetailPage).</summary>
    public event Action<int>? SlideNavigationRequested;

    /// <summary>Видео подготовлено — страница может вызвать Play().</summary>
    public event Action? VideoPrepareCompleted;

    public bool IsVideoPlaybackRequested
    {
        get
        {
#if ANDROID
            return _videoPlaybackRequested;
#else
            return false;
#endif
        }
    }

    partial void OnCurrentSlideIndexChanged(int value)
    {
        UpdateSlideIndicator();
        OnPropertyChanged(nameof(ShowCarouselPrevious));
        OnPropertyChanged(nameof(ShowCarouselNext));
        GoToPreviousSlideCommand.NotifyCanExecuteChanged();
        GoToNextSlideCommand.NotifyCanExecuteChanged();
        NotifyVideoPlayPromptChanged();
    }

    partial void OnShowCurrentVideoPlayerChanged(bool value) => NotifyVideoPlayPromptChanged();

    partial void OnHasMultipleSlidesChanged(bool value)
    {
        OnPropertyChanged(nameof(MediaContentHeight));
        OnPropertyChanged(nameof(ShowCarouselPrevious));
        OnPropertyChanged(nameof(ShowCarouselNext));
        GoToPreviousSlideCommand.NotifyCanExecuteChanged();
        GoToNextSlideCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoToPreviousSlide() => ShowCarouselPrevious;

    private bool CanGoToNextSlide() => ShowCarouselNext;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousSlide))]
    private void GoToPreviousSlide() => SlideNavigationRequested?.Invoke(CurrentSlideIndex - 1);

    [RelayCommand(CanExecute = nameof(CanGoToNextSlide))]
    private void GoToNextSlide() => SlideNavigationRequested?.Invoke(CurrentSlideIndex + 1);

    /// <summary>Синхронизация индекса после свайпа или программной смены позиции карусели.</summary>
    public void SetSlideIndexFromCarousel(int position)
    {
        if (position < 0 || position >= MediaSlides.Count)
            return;

        if (CurrentSlideIndex == position)
            return;

        CurrentSlideIndex = position;
#if ANDROID
        _videoPlaybackRequested = false;
        StopCurrentVideo();
        NotifyVideoPlayPromptChanged();
#else
        _ = PrepareCurrentSlideMediaAsync();
#endif
    }

    [RelayCommand]
    private async Task PlayCurrentVideoAsync()
    {
#if ANDROID
        _videoPlaybackRequested = true;
        NotifyVideoPlayPromptChanged();
        await PrepareCurrentSlideMediaAsync(forPlaybackRequest: true);
#else
        await PrepareCurrentSlideMediaAsync();
#endif
    }

    private void NotifyVideoPlayPromptChanged() => OnPropertyChanged(nameof(ShowVideoPlayPrompt));

    partial void OnIsMediaExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(MediaCarouselHeight));
        OnPropertyChanged(nameof(MediaContentHeight));
        _ = PersistMediaExpandedAsync(value);
    }

    private async Task PersistMediaExpandedAsync(bool isExpanded)
    {
        var appSettings = await _settings.GetAppSettingsAsync();
        if (appSettings.IsContentMediaExpanded == isExpanded)
            return;

        appSettings.IsContentMediaExpanded = isExpanded;
        await _settings.SaveAppSettingsAsync(appSettings);
    }

    [RelayCommand]
    private void ToggleMediaExpand() => IsMediaExpanded = !IsMediaExpanded;

    [RelayCommand]
    private async Task AppearingAsync() => await LoadAsync();

    /// <summary>Загрузка данных для страницы (вызывается из code-behind после появления).</summary>
    public Task LoadForDisplayAsync()
    {
        _loadedContentId = 0;
        return LoadAsync();
    }

    [RelayCommand]
    private void ToggleDescription()
    {
        IsDescriptionExpanded = !IsDescriptionExpanded;
        OnPropertyChanged(nameof(DescriptionMaxLines));
        OnPropertyChanged(nameof(DescriptionToggleText));
    }

    [RelayCommand]
    private void EditCaption()
    {
        if (_content is null)
            return;

        CaptionDraft = _content.Caption ?? string.Empty;
        IsEditingCaption = true;
    }

    [RelayCommand]
    private async Task SaveCaptionAsync()
    {
        if (_content is null)
            return;

        var normalized = string.IsNullOrWhiteSpace(CaptionDraft) ? null : CaptionDraft.Trim();
        if (!string.Equals(_content.Caption, normalized, StringComparison.Ordinal))
        {
            _content.Caption = normalized;
            await _repository.SaveContentAsync(_content);
            Caption = normalized;
        }

        IsEditingCaption = false;
    }

    [RelayCommand]
    private void CancelCaptionEdit() => IsEditingCaption = false;

    [RelayCommand]
    private void EditComment()
    {
        if (_content is null)
            return;

        CommentDraft = _content.UserComment ?? string.Empty;
        IsEditingComment = true;
    }

    [RelayCommand]
    private async Task SaveCommentAsync()
    {
        if (_content is null)
            return;

        var normalized = string.IsNullOrWhiteSpace(CommentDraft) ? null : CommentDraft.Trim();
        if (!string.Equals(_content.UserComment, normalized, StringComparison.Ordinal))
        {
            _content.UserComment = normalized;
            await _repository.SaveContentAsync(_content);
            UserComment = normalized;
        }

        IsEditingComment = false;
    }

    [RelayCommand]
    private void CancelCommentEdit() => IsEditingComment = false;

    [RelayCommand]
    private void ToggleNewTagPanel() => IsNewTagPanelVisible = !IsNewTagPanelVisible;

    [RelayCommand]
    private void AppendEmoji(string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            return;

        var trimmed = NewTagName.Trim();
        if (trimmed.StartsWith(emoji, StringComparison.Ordinal))
            return;

        var combined = string.IsNullOrWhiteSpace(trimmed) ? $"{emoji} " : $"{emoji} {trimmed}";
        NewTagName = combined.Length <= 32 ? combined : NewTagName;
    }

    [RelayCommand]
    private async Task PickNewTagColorAsync()
    {
        var color = await _colorPicker.PickColorAsync(
            SelectedTagColor,
            hex => SelectedTagColor = TagColorHelper.NormalizeHex(hex));
        if (color is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
            SelectedTagColor = TagColorHelper.NormalizeHex(color));
    }

    [RelayCommand]
    private async Task AddContentTagAsync()
    {
        if (_content is null)
            return;

        var (success, tag, error) = await _tagCreation.TryCreateAsync(NewTagName, SelectedTagColor);
        if (!success)
        {
            await _toast.ShowWarningAsync(error ?? "Введите название тега.");
            return;
        }

        NewTagName = string.Empty;
        SelectedTagColor = TagColorHelper.DefaultHex;
        IsNewTagPanelVisible = false;

        var tagIds = _content.Tags.Select(t => t.Id).ToHashSet();
        tagIds.Add(tag!.Id);
        await _repository.AssignTagsAsync(_content.Id, tagIds.ToList());

        var updated = await _repository.GetContentByIdAsync(_content.Id);
        if (updated is not null)
            _content = updated;

        await LoadTagEditorAsync();
        _suppressTagsChangedReload = true;
        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        NewTagStatusMessage = $"Тег «{tag!.Name}» добавлен и назначен.";
    }

    [RelayCommand]
    private async Task ToggleContentTagAsync(TagChipViewModel chip)
    {
        if (_content is null)
            return;

        var tagIds = _content.Tags.Select(t => t.Id).ToHashSet();
        if (tagIds.Contains(chip.Tag.Id))
            tagIds.Remove(chip.Tag.Id);
        else
            tagIds.Add(chip.Tag.Id);

        await _repository.AssignTagsAsync(_content.Id, tagIds.ToList());

        var updated = await _repository.GetContentByIdAsync(_content.Id);
        if (updated is not null)
            _content = updated;

        SyncTagChipSelection();
        _suppressTagsChangedReload = true;
        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
    }

    [RelayCommand]
    private async Task OpenInExplorerAsync()
    {
        var slide = GetCurrentSlide();
        if (slide is not null && File.Exists(slide.LocalPath))
        {
            await _fileExplorer.OpenFileInExplorerAsync(slide.LocalPath);
            return;
        }

        if (_content is not null)
        {
            var folder = Path.GetDirectoryName(_content.MediaFiles.FirstOrDefault()?.LocalPath ?? string.Empty);
            if (!string.IsNullOrEmpty(folder))
                await _fileExplorer.OpenFolderInExplorerAsync(folder);
        }
    }

    [RelayCommand]
    private async Task OpenSourceAsync()
    {
        if (_content is null || string.IsNullOrWhiteSpace(_content.SourceUrl))
            return;

        if (_enteredAtUtc != default && (DateTime.UtcNow - _enteredAtUtc) < TimeSpan.FromMilliseconds(650))
            return;

        try
        {
            await _linkLauncher.OpenSourceAsync(_content.SourceUrl);
        }
        catch
        {
            // Browser/Launcher failures must not crash the app.
        }
    }

    [RelayCommand]
    private async Task DeleteContentAsync()
    {
        if (_content is null) return;

        var display = ContentItemDisplayModel.FromEntity(_content, _storagePaths.DownloadsPath);
        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(_repository, display))
            return;

        await MainThread.InvokeOnMainThreadAsync(ClearLoadedContentState);
        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
        await ShellBackNavigation.GoBackAsync();
    }

    [RelayCommand]
    private async Task ShareMediaAsync(MediaSlideViewModel? slide)
    {
        slide ??= GetCurrentSlide();
        if (slide is null)
            return;

        var path = Path.GetFullPath(slide.LocalPath);
        if (!File.Exists(path))
            return;

        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = Path.GetFileName(path),
                File = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            await _toast.ShowWarningAsync($"Не удалось отправить файл: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ShareAllContentAsync()
    {
        if (_content is null)
            return;

        var text = BuildShareText(_content.Caption, _content.UserComment);
        var files = CollectShareableFiles();
        if (files.Count == 0 && string.IsNullOrWhiteSpace(text))
        {
            await _toast.ShowWarningAsync("Нечего отправить.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(AuthorTitle) ? "Контент" : AuthorTitle;

        try
        {
            var paths = files.Select(static file => file.FullPath).ToList();
            await _mediaShare.ShareAsync(title, text, paths);
        }
        catch (Exception ex)
        {
            await _toast.ShowWarningAsync($"Не удалось отправить: {ex.Message}");
        }
    }

    private List<ShareFile> CollectShareableFiles()
    {
        var files = new List<ShareFile>();
        foreach (var slide in MediaSlides)
        {
            var path = Path.GetFullPath(slide.LocalPath);
            if (File.Exists(path))
                files.Add(new ShareFile(path));
        }

        return files;
    }

    private static string? BuildShareText(string? caption, string? userComment)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(caption))
            parts.Add(caption.Trim());
        if (!string.IsNullOrWhiteSpace(userComment))
            parts.Add(userComment.Trim());

        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (_content is null) return;

        if (!string.IsNullOrWhiteSpace(_content.StorageRelativePath))
        {
            var root = Path.Combine(_storagePaths.DownloadsPath, _content.StorageRelativePath);
            if (Directory.Exists(root))
            {
                await _fileExplorer.OpenFolderInExplorerAsync(root);
                return;
            }
        }

        if (_content.MediaFiles.FirstOrDefault()?.LocalPath is not { } path) return;
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            await _fileExplorer.OpenFolderInExplorerAsync(folder);
    }

    private MediaSlideViewModel? GetCurrentSlide() =>
        CurrentSlideIndex >= 0 && CurrentSlideIndex < MediaSlides.Count
            ? MediaSlides[CurrentSlideIndex]
            : MediaSlides.FirstOrDefault();

    private async Task LoadAsync()
    {
        if (!int.TryParse(ContentId, out var id) || id <= 0)
            return;

        await _loadGate.WaitAsync().ConfigureAwait(false);
        var version = ++_loadVersion;

        try
        {
            if (_loadedContentId == id && IsContentLoaded)
                return;

            var item = await _repository.GetContentByIdAsync(id).ConfigureAwait(false);
            if (version != _loadVersion)
                return;

            if (item is null)
            {
                await HandleMissingContentAsync("Контент не найден или был удалён.");
                return;
            }

            if (!ContentThumbnailHelper.HasAvailableMedia(item))
            {
                await _repository.DeleteContentAsync(id);
                WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
                await HandleMissingContentAsync("Медиафайлы не найдены. Запись удалена из списка.");
                return;
            }

            var appSettings = await _settings.GetAppSettingsAsync().ConfigureAwait(false);
            if (version != _loadVersion)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (version != _loadVersion)
                    return;

                StopCurrentVideo();
                ApplyLoadedContent(item, id, appSettings);
            });

            if (version != _loadVersion)
                return;

            _ = LoadTagEditorAsync();
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void ApplyLoadedContent(ContentItem item, int id, AppUserSettings appSettings)
    {
        _content = item;
        _loadedContentId = id;
        CanOpenSource = !string.IsNullOrWhiteSpace(item.SourceUrl);
        AuthorTitle = item.Platform == SocialPlatform.YouTube
            ? item.AuthorDisplayName ?? item.AuthorUsername
            : $"@{item.AuthorUsername}";
        Caption = item.Caption;
        HasCaption = !string.IsNullOrWhiteSpace(item.Caption);
        UserComment = item.UserComment;
        IsEditingCaption = false;
        IsEditingComment = false;
        IsContentLoaded = true;
        OnPropertyChanged(nameof(ShowCaptionPlaceholder));

        var downloadsRoot = _storagePaths.DownloadsPath;
        var thumbnailPath = ContentThumbnailHelper.ResolveThumbnailPath(item, downloadsRoot);

        MediaSlides.Clear();
        foreach (var media in item.MediaFiles.OrderBy(m => m.OrderIndex))
        {
            if (!File.Exists(media.LocalPath))
                continue;

            if (ContentThumbnailHelper.IsThumbnailFile(media.LocalPath))
                continue;

            MediaSlides.Add(new MediaSlideViewModel
            {
                LocalPath = media.LocalPath,
                MediaType = media.MediaType,
                ThumbnailPath = media.MediaType == MediaType.Video ? thumbnailPath : null
            });
        }

        HasMultipleSlides = MediaSlides.Count > 1;
        HasMediaSlides = MediaSlides.Count > 0;
        IsMediaExpanded = HasMediaSlides && appSettings.IsContentMediaExpanded;

#if ANDROID
        _videoPlaybackRequested = false;
#endif
        CurrentSlideIndex = 0;
        UpdateSlideIndicator();
        ShowCurrentVideoPlayer = false;
        CurrentVideoSource = null;
        OnPropertyChanged(nameof(ShowCarouselPrevious));
        OnPropertyChanged(nameof(ShowCarouselNext));
        OnPropertyChanged(nameof(CanToggleDescription));
        GoToPreviousSlideCommand.NotifyCanExecuteChanged();
        GoToNextSlideCommand.NotifyCanExecuteChanged();
        NotifyVideoPlayPromptChanged();
    }

    private async Task HandleMissingContentAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(ClearLoadedContentState);
        await _toast.ShowWarningAsync(message);
        await ShellBackNavigation.GoBackAsync();
    }

    private void ClearLoadedContentState()
    {
        StopCurrentVideo();
        _content = null;
        _loadedContentId = 0;
        IsContentLoaded = false;
        HasMediaSlides = false;
        HasMultipleSlides = false;
        CanOpenSource = false;
        AuthorTitle = string.Empty;
        Caption = null;
        HasCaption = false;
        UserComment = null;
        IsEditingCaption = false;
        IsEditingComment = false;
        MediaSlides.Clear();
        TagChips.Clear();
    }

    public void RefreshCurrentSlideMedia()
    {
        try
        {
            var slide = GetCurrentSlide();
#if ANDROID
            if (slide is { IsVideo: true } && !_videoPlaybackRequested)
            {
                ShowCurrentVideoPlayer = false;
                CurrentVideoSource = null;
                NotifyVideoPlayPromptChanged();
                return;
            }
#endif
            if (slide is { IsVideo: true } && File.Exists(slide.LocalPath))
            {
                ShowCurrentVideoPlayer = true;
                var fullPath = Path.GetFullPath(slide.LocalPath);
                CurrentVideoSource = MediaSource.FromFile(fullPath);
                NotifyVideoPlayPromptChanged();
                VideoPrepareCompleted?.Invoke();
                return;
            }

            ShowCurrentVideoPlayer = false;
            CurrentVideoSource = null;
            NotifyVideoPlayPromptChanged();
        }
        catch
        {
            ShowCurrentVideoPlayer = false;
            CurrentVideoSource = null;
            NotifyVideoPlayPromptChanged();
        }
    }

    public void StopCurrentVideo()
    {
        ShowCurrentVideoPlayer = false;
        CurrentVideoSource = null;
#if ANDROID
        _videoPlaybackRequested = false;
#endif
        NotifyVideoPlayPromptChanged();
    }

    public async Task PrepareCurrentSlideMediaAsync(
        bool forPlaybackRequest = false,
        bool forceSurfaceRefresh = false)
    {
        _mediaPrepareCts?.Cancel();
        var cts = new CancellationTokenSource();
        _mediaPrepareCts = cts;

        await _mediaPrepareGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (cts.IsCancellationRequested)
                return;

            var skipInitialReset = forPlaybackRequest
                                   && !forceSurfaceRefresh
                                   && GetCurrentSlide()?.IsVideo == true;
            if (!skipInitialReset)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowCurrentVideoPlayer = false;
                    CurrentVideoSource = null;
                });
            }

            if (MediaSlides.Count == 0)
                return;

#if ANDROID
            if (!forPlaybackRequest)
                await Task.Delay(450, cts.Token).ConfigureAwait(false);
#endif

            var slide = GetCurrentSlide();
            if (slide is null)
                return;

            if (slide.IsVideo)
            {
#if ANDROID
                if (!_videoPlaybackRequested)
                    return;
#endif
                await MediaFileReadiness.WaitForFilesAsync([slide.LocalPath], cts.Token)
                    .ConfigureAwait(false);
            }

            if (cts.IsCancellationRequested)
                return;

            await MainThread.InvokeOnMainThreadAsync(RefreshCurrentSlideMedia);
        }
        catch (OperationCanceledException)
        {
            // ignore stale prepare requests
        }
        finally
        {
            _mediaPrepareGate.Release();
        }
    }

    private void UpdateSlideIndicator() =>
        SlideIndicator = HasMultipleSlides ? $"{CurrentSlideIndex + 1} / {MediaSlides.Count}" : string.Empty;

    private async Task LoadTagEditorAsync()
    {
        if (_content is null)
            return;

        var allTags = await _tagList.GetSortedTagsAsync().ConfigureAwait(false);
        var assignedIds = _content.Tags.Select(t => t.Id).ToHashSet();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            TagChips.Clear();
            foreach (var tag in allTags)
            {
                TagChips.Add(new TagChipViewModel(tag)
                {
                    IsSelected = assignedIds.Contains(tag.Id)
                });
            }
        });
    }

    private void SyncTagChipSelection()
    {
        if (_content is null)
            return;

        var assignedIds = _content.Tags.Select(t => t.Id).ToHashSet();
        foreach (var chip in TagChips)
            chip.IsSelected = assignedIds.Contains(chip.Tag.Id);
    }
}
