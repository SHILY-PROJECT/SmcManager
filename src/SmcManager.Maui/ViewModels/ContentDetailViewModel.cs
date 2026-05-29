using System.Collections.ObjectModel;
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
public partial class ContentDetailViewModel : ObservableObject, IQueryAttributable, IRecipient<TagsChangedMessage>
{
    private readonly IContentRepository _repository;
    private int _loadedContentId;
    private readonly IFileExplorerService _fileExplorer;
    private readonly ILinkLauncherService _linkLauncher;
    private readonly IAppStoragePaths _storagePaths;
    private readonly ISettingsService _settings;

    private ContentItem? _content;
    private DateTime _enteredAtUtc;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private int _loadVersion;

    public ContentDetailViewModel(
        IContentRepository repository,
        IFileExplorerService fileExplorer,
        ILinkLauncherService linkLauncher,
        IAppStoragePaths storagePaths,
        ISettingsService settings)
    {
        _repository = repository;
        _fileExplorer = fileExplorer;
        _linkLauncher = linkLauncher;
        _storagePaths = storagePaths;
        _settings = settings;
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(TagsChangedMessage message) => _ = LoadTagEditorAsync();

    public string ContentId { get; set; } = string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("contentId", out var value))
            ContentId = value?.ToString() ?? string.Empty;

        _enteredAtUtc = DateTime.UtcNow;
    }

    public ObservableCollection<MediaSlideViewModel> MediaSlides { get; } = [];

    public ObservableCollection<TagChipViewModel> TagChips { get; } = [];

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

    public double MediaCarouselHeight => IsMediaExpanded ? ExpandedMediaHeight : DefaultMediaHeight;

    private const double DefaultMediaHeight = 320;

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

    /// <summary>Запрос смены слайда с экрана (обрабатывается ContentDetailPage).</summary>
    public event Action<int>? SlideNavigationRequested;

    partial void OnCurrentSlideIndexChanged(int value)
    {
        UpdateSlideIndicator();
        UpdateActiveSlide(value);
        OnPropertyChanged(nameof(ShowCarouselPrevious));
        OnPropertyChanged(nameof(ShowCarouselNext));
        GoToPreviousSlideCommand.NotifyCanExecuteChanged();
        GoToNextSlideCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasMultipleSlidesChanged(bool value)
    {
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
    }

    partial void OnIsMediaExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(MediaCarouselHeight));
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
    public Task LoadForDisplayAsync() => LoadAsync();

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
        if (_content is null || string.IsNullOrWhiteSpace(_content.SourceUrl)) return;

        // Android: предотвращаем "прокликивание" — первый тап по карточке может
        // попасть в кнопку на новом экране в момент навигации.
        if (_enteredAtUtc != default && (DateTime.UtcNow - _enteredAtUtc) < TimeSpan.FromMilliseconds(650))
            return;

        await _linkLauncher.OpenSourceAsync(_content.SourceUrl);
    }

    [RelayCommand]
    private async Task DeleteContentAsync()
    {
        if (_content is null) return;

        var display = ContentItemDisplayModel.FromEntity(_content, _storagePaths.DownloadsPath);
        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(_repository, display))
            return;

        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
        await Shell.Current.GoToAsync("..");
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

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = Path.GetFileName(path),
            File = new ShareFile(path)
        });
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
            if (item is null || version != _loadVersion)
                return;

            var appSettings = await _settings.GetAppSettingsAsync().ConfigureAwait(false);
            if (version != _loadVersion)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (version != _loadVersion)
                    return;

                ApplyLoadedContent(item, id, appSettings);
            });

            if (version != _loadVersion)
                return;

            await LoadTagEditorAsync().ConfigureAwait(false);
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

        CurrentSlideIndex = 0;
        UpdateSlideIndicator();
        foreach (var slide in MediaSlides)
            slide.IsActive = false;
        OnPropertyChanged(nameof(ShowCarouselPrevious));
        OnPropertyChanged(nameof(ShowCarouselNext));
        OnPropertyChanged(nameof(CanToggleDescription));
        GoToPreviousSlideCommand.NotifyCanExecuteChanged();
        GoToNextSlideCommand.NotifyCanExecuteChanged();
    }

    public void UpdateActiveSlide(int index)
    {
        if (MediaSlides.Count == 0)
            return;

        index = Math.Clamp(index, 0, MediaSlides.Count - 1);
        for (var i = 0; i < MediaSlides.Count; i++)
            MediaSlides[i].IsActive = i == index;
    }

    public async Task ActivateCurrentSlideAsync()
    {
        if (MediaSlides.Count == 0)
            return;

#if ANDROID
        await Task.Delay(150).ConfigureAwait(false);
#endif

        var index = CurrentSlideIndex;
        await MainThread.InvokeOnMainThreadAsync(() => UpdateActiveSlide(index));
    }

    private void UpdateSlideIndicator() =>
        SlideIndicator = HasMultipleSlides ? $"{CurrentSlideIndex + 1} / {MediaSlides.Count}" : string.Empty;

    private async Task LoadTagEditorAsync()
    {
        if (_content is null)
            return;

        var allTags = await _repository.GetTagsAsync().ConfigureAwait(false);
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
