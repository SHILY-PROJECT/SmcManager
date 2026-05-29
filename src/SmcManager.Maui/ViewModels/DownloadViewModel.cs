using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Вкладка «Скачать»: ввод ссылки, выбор тега, аккаунта и список последних скачиваний.
/// </summary>
public partial class DownloadViewModel : ObservableObject,
    IRecipient<ShareUrlReceivedMessage>,
    IRecipient<TagsChangedMessage>,
    IRecipient<TagSortChangedMessage>,
    IRecipient<ContentDeletedMessage>
{
    private readonly IDownloadOrchestrator _orchestrator;
    private readonly ILinkMetadataService _linkMetadata;
    private readonly IContentRepository _repository;
    private readonly ISocialAccountService _accountService;
    private readonly ISettingsService _settings;
    private readonly IAppStoragePaths _storagePaths;
    private readonly TagCreationService _tagCreation;
    private readonly TagListService _tagList;
    private readonly BottomToastService _toast;
    private readonly RemoteImageCache _remoteImageCache;
    private readonly ILogger<DownloadViewModel> _logger;

    private CancellationTokenSource? _urlRefreshCts;
    private CancellationTokenSource? _metadataRefreshCts;
    private CancellationTokenSource? _previewImageCts;
    private bool _suppressAccountSelectionChange;
    private bool _suppressUrlNormalization;
    private bool _holdDownloadFormClear;
    private string? _pendingDownloadUrl;
    private IReadOnlyList<SocialAccount> _lastPlatformAccounts = [];
    private SocialAccount? _lastDefaultAccount;
    private AppUserSettings? _lastAppSettings;
    private SocialPlatform? _previewPlatform;

    public DownloadViewModel(
        IDownloadOrchestrator orchestrator,
        ILinkMetadataService linkMetadata,
        IContentRepository repository,
        ISocialAccountService accountService,
        ISettingsService settings,
        IAppStoragePaths storagePaths,
        TagCreationService tagCreation,
        TagListService tagList,
        BottomToastService toast,
        RemoteImageCache remoteImageCache,
        ILogger<DownloadViewModel> logger)
    {
        _orchestrator = orchestrator;
        _linkMetadata = linkMetadata;
        _repository = repository;
        _accountService = accountService;
        _settings = settings;
        _storagePaths = storagePaths;
        _tagCreation = tagCreation;
        _tagList = tagList;
        _toast = toast;
        _remoteImageCache = remoteImageCache;
        _logger = logger;
        WeakReferenceMessenger.Default.Register<ShareUrlReceivedMessage>(this);
        WeakReferenceMessenger.Default.Register<TagsChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<TagSortChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<ContentDeletedMessage>(this);
    }

    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);

    public bool ShowPreviewDownloadButton =>
        ShowLinkPreview && !IsLoadingLinkMetadata;

    public bool ShowFallbackDownloadButton =>
        !ShowLinkPreview
        && !IsLoadingLinkMetadata
        && !string.IsNullOrWhiteSpace(ResolveActiveDownloadUrl());

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _recentCountLabel;

    [ObservableProperty]
    private bool _showAccountPicker;

    [ObservableProperty]
    private string _accountPickerHint = string.Empty;

    public string RecentDownloadsHeader => $"Последние скачивания";

    public string NewTagPanelToggleText => IsNewTagPanelVisible ? "Скрыть" : "+ Новый тег";

    public IReadOnlyList<string> EmojiSuggestions { get; } = TagEmojiLibrary.Suggested;

    public ObservableCollection<ContentItemDisplayModel> RecentDownloads { get; } = [];

    public ObservableCollection<AccountPickerOption> AccountOptions { get; } = [];

    public bool HasNoRecentDownloads => RecentDownloads.Count == 0;

    private readonly HashSet<int> _selectedTagIds = [];

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
    private AccountPickerOption? _selectedAccountOption;

    [ObservableProperty]
    private bool _showQualityPicker;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingLinkMetadata))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowFallbackDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowLinkMetadataReady))]
    private bool _isLoadingQualities;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingLinkMetadata))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowFallbackDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowLinkMetadataReady))]
    private bool _isLoadingPreview;

    public bool IsLoadingLinkMetadata => IsLoadingPreview || IsLoadingQualities;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreviewDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowFallbackDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowLinkMetadataReady))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewAccountIndicators))]
    private bool _showLinkPreview;

    public bool ShowLinkMetadataReady => ShowLinkPreview && !IsLoadingLinkMetadata;

    [ObservableProperty]
    private string? _previewTitle;

    [ObservableProperty]
    private string? _previewAuthor;

    [ObservableProperty]
    private string? _previewThumbnail;

    [ObservableProperty]
    private string? _previewImageFile;

    [ObservableProperty]
    private string? _previewPlatformIconFile;

    [ObservableProperty]
    private string? _previewAuthStatusIconFile;

    [ObservableProperty]
    private bool _previewUsesAuthenticatedAccount;

    [ObservableProperty]
    private DownloadQualityOption? _selectedQuality;

    public bool ShowPreviewAccountIndicators => ShowLinkPreview;

    public string PreviewAuthStatusDescription =>
        PreviewUsesAuthenticatedAccount
            ? "Скачивание с авторизованным аккаунтом"
            : "Скачивание без аккаунта";

    public ObservableCollection<DownloadQualityOption> QualityOptions { get; } = [];

    public ObservableCollection<PendingDownloadViewModel> ActiveDownloads { get; } = [];

    public bool HasActiveDownloads => ActiveDownloads.Count > 0;

    public IReadOnlyList<string> ColorPresets { get; } = TagColorPresets.Colors;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        _ = _linkMetadata.WarmupAsync();
        await LoadTagsAsync();
        await RefreshRecentAsync();
        await ApplyPendingShareUrlAsync();

        if (_holdDownloadFormClear)
            await ClearDownloadInputAsync();
        else if (!string.IsNullOrWhiteSpace(Url))
            ScheduleUrlRefresh();
    }

    partial void OnUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasUrl));
        OnPropertyChanged(nameof(ShowFallbackDownloadButton));

        if (!_suppressUrlNormalization && !string.IsNullOrWhiteSpace(value))
            _holdDownloadFormClear = false;

        if (!_suppressUrlNormalization)
            ScheduleUrlRefresh();
    }

    partial void OnSelectedAccountOptionChanged(AccountPickerOption? value)
    {
        if (_suppressAccountSelectionChange || value is null) return;

        AccountPickerHint = value.IsNoAccount
            ? "Будет попытка скачать без входа. Для закрытого контента выберите аккаунт или добавьте его в настройках."
            : "Скачивание с cookies выбранного аккаунта.";

        if (value is { IsNoAccount: false, AccountId: int accountId, Platform: var platform })
            _ = PersistAuthenticatedAccountChoiceAsync(platform.Value, accountId);

        UpdatePreviewAccountIndicators();
        ScheduleMetadataRefresh();
    }

    partial void OnShowLinkPreviewChanged(bool value)
    {
        UpdatePreviewAccountIndicators();
        OnPropertyChanged(nameof(ShowPreviewAccountIndicators));
        OnPropertyChanged(nameof(ShowPreviewDownloadButton));
        OnPropertyChanged(nameof(ShowFallbackDownloadButton));
    }

    partial void OnPreviewUsesAuthenticatedAccountChanged(bool value) =>
        OnPropertyChanged(nameof(PreviewAuthStatusDescription));

    partial void OnIsNewTagPanelVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(NewTagPanelToggleText));

    [RelayCommand]
    private async Task DownloadAsync()
    {
        var activeUrl = ResolveActiveDownloadUrl();
        if (string.IsNullOrWhiteSpace(activeUrl))
        {
            await _toast.ShowWarningAsync("Вставьте ссылку на пост, рилс или видео.");
            return;
        }

        if (!UrlPlatformDetector.TryDetect(activeUrl, out _, out _))
        {
            await _toast.ShowWarningAsync("Ссылка не распознана. Поддерживаются Instagram, YouTube и ВКонтакте.");
            return;
        }

        var normalizedUrl = ContentUrlNormalizer.Normalize(activeUrl);

        if (!await DuplicateDownloadHelper.ConfirmReplaceIfExistsAsync(_repository, normalizedUrl))
            return;

        var (useAccount, accountId) = ResolveDownloadAccountSelection();
        var appSettings = await _settings.GetAppSettingsAsync().ConfigureAwait(false);

        var job = new PendingDownloadViewModel(normalizedUrl, PreviewTitle, PreviewAuthor);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ActiveDownloads.Insert(0, job);
            OnPropertyChanged(nameof(HasActiveDownloads));

            if (accountId is int savedAccountId
                && UrlPlatformDetector.TryDetect(normalizedUrl, out var detectedPlatform, out _))
            {
                _ = PersistAuthenticatedAccountChoiceAsync(detectedPlatform, savedAccountId);
            }

            _holdDownloadFormClear = true;
            ClearDownloadInputCore();
            StatusMessage = "Скачивание запущено. Можно добавить ещё ссылки.";
        });

        _ = RunDownloadJobAsync(job, normalizedUrl, useAccount, accountId, appSettings);
    }

    [RelayCommand]
    private async Task ClearUrlAsync()
    {
        _holdDownloadFormClear = false;
        await ClearDownloadInputAsync();
    }

    [RelayCommand]
    private async Task DismissPreviewAsync()
    {
        _pendingDownloadUrl = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ClearLinkMetadataUi();
            OnPropertyChanged(nameof(ShowFallbackDownloadButton));
        });
    }

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
    private void SelectTagColor(string color) => SelectedTagColor = color;

    [RelayCommand]
    private void ToggleTagChip(TagChipViewModel chip)
    {
        if (_selectedTagIds.Contains(chip.Tag.Id))
            _selectedTagIds.Remove(chip.Tag.Id);
        else
            _selectedTagIds.Add(chip.Tag.Id);

        SyncTagChipSelection();
    }

    [RelayCommand]
    private void ClearTag()
    {
        _selectedTagIds.Clear();
        SyncTagChipSelection();
    }

    [RelayCommand]
    private async Task AddTagAsync()
    {
        var (success, tag, error) = await _tagCreation.TryCreateAsync(NewTagName, SelectedTagColor);
        if (!success)
        {
            await _toast.ShowWarningAsync(error ?? "Введите название тега.");
            return;
        }

        NewTagName = string.Empty;
        IsNewTagPanelVisible = false;
        await LoadTagsAsync();
        if (tag is not null)
            _selectedTagIds.Add(tag.Id);
        SyncTagChipSelection();
        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        NewTagStatusMessage = $"Тег «{tag!.Name}» добавлен.";
    }

    [RelayCommand]
    private async Task PasteFromClipboardAsync()
    {
        var text = await Clipboard.Default.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
            return;

        SetUrl(CleanUrlForDisplay(text));
    }

    public void Receive(ShareUrlReceivedMessage message)
    {
        if (MainThread.IsMainThread)
            SetUrl(CleanUrlForDisplay(message.Url));
        else
            MainThread.BeginInvokeOnMainThread(() => SetUrl(CleanUrlForDisplay(message.Url)));
    }

    public void Receive(TagsChangedMessage message) => _ = LoadTagsAsync();

    public void Receive(TagSortChangedMessage message)
    {
        _ = LoadTagsAsync();
        _ = RefreshRecentAsync();
    }

    public void Receive(ContentDeletedMessage message) => _ = RefreshRecentAsync();

    [RelayCommand]
    private async Task DeleteRecentItemAsync(ContentItemDisplayModel item)
    {
        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(_repository, item))
            return;

        RecentDownloads.Remove(item);
        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
        OnPropertyChanged(nameof(HasNoRecentDownloads));
        OnPropertyChanged(nameof(RecentDownloadsHeader));
    }

    private void ScheduleUrlRefresh()
    {
        _metadataRefreshCts?.Cancel();
        _metadataRefreshCts?.Dispose();
        _metadataRefreshCts = null;

        _urlRefreshCts?.Cancel();
        _urlRefreshCts?.Dispose();
        _urlRefreshCts = new CancellationTokenSource();
        var ct = _urlRefreshCts.Token;
        _ = RefreshAfterUrlChangeAsync(ct);
    }

    private void ScheduleMetadataRefresh()
    {
        _metadataRefreshCts?.Cancel();
        _metadataRefreshCts?.Dispose();
        _metadataRefreshCts = new CancellationTokenSource();
        var ct = _metadataRefreshCts.Token;
        _ = RefreshMetadataOnlyAsync(ct);
    }

    private async Task RefreshMetadataOnlyAsync(CancellationToken ct)
    {
        var owner = _metadataRefreshCts;
        var activeUrl = ResolveActiveDownloadUrl();
        if (string.IsNullOrWhiteSpace(activeUrl)
            || !UrlPlatformDetector.TryDetect(activeUrl, out _, out _))
        {
            return;
        }

        try
        {
            await Task.Delay(350, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoadingPreview = true;
                IsLoadingQualities = true;
            });

            var (useAccount, accountId) = ResolveDownloadAccountSelection();

            var url = await EnsureCleanUrlInFieldAsync(activeUrl).ConfigureAwait(false);
            var metadata = await _linkMetadata.GetMetadataAsync(
                url, accountId, useAccount, ct).ConfigureAwait(false);

            if (ct.IsCancellationRequested) return;

            await MainThread.InvokeOnMainThreadAsync(() => ApplyMetadata(metadata));
        }
        catch (OperationCanceledException)
        {
            // другой выбор аккаунта
        }
        catch
        {
            if (ct.IsCancellationRequested) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var platform = UrlPlatformDetector.TryDetect(Url, out var p, out _)
                    ? p
                    : SocialPlatform.YouTube;
                ApplyMetadata(new LinkMetadataResult
                {
                    Qualities = [DownloadQualityOption.BestQuality(platform)]
                });
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ReferenceEquals(_metadataRefreshCts, owner) || !ct.IsCancellationRequested)
                    FinishLinkMetadataLoading();
            });
        }
    }

    private async Task RefreshAfterUrlChangeAsync(CancellationToken ct)
    {
        var owner = _urlRefreshCts;

        if (string.IsNullOrWhiteSpace(Url))
        {
            if (!string.IsNullOrWhiteSpace(_pendingDownloadUrl))
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ClearLinkMetadataUi();
                ResetAccountPickerUi();
            });
            return;
        }

        _pendingDownloadUrl = null;

        if (!UrlPlatformDetector.TryDetect(Url, out _, out _))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ClearLinkMetadataUi();
                ResetAccountPickerUi();
            });
            return;
        }

        try
        {
            await Task.Delay(650, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            await MainThread.InvokeOnMainThreadAsync(ClearLinkMetadataUi);

            var url = await EnsureCleanUrlInFieldAsync(Url).ConfigureAwait(false);
            _logger.LogDebug("RefreshAfterUrlChangeAsync: fetching metadata for {Url}", url);
            await RefreshAccountPickerAsync(preserveUserSelection: false).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoadingPreview = true;
                IsLoadingQualities = true;
            });

            var (useAccount, accountId) = ResolveDownloadAccountSelection();

            var metadata = await _linkMetadata.GetMetadataAsync(
                url, accountId, useAccount, ct).ConfigureAwait(false);

            if (ct.IsCancellationRequested) return;

            await MainThread.InvokeOnMainThreadAsync(() => ApplyMetadata(metadata));
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("RefreshAfterUrlChangeAsync cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshAfterUrlChangeAsync failed for url={Url}", Url);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var platform = UrlPlatformDetector.TryDetect(Url, out var p, out _)
                    ? p
                    : SocialPlatform.YouTube;
                ApplyMetadata(new LinkMetadataResult
                {
                    Qualities = [DownloadQualityOption.BestQuality(platform)]
                });
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ReferenceEquals(_urlRefreshCts, owner) || !ct.IsCancellationRequested)
                    FinishLinkMetadataLoading();
            });
        }
    }

    private void ApplyMetadata(LinkMetadataResult metadata)
    {
        if (metadata.Preview is { } preview)
        {
            _previewPlatform = preview.Platform;
            PreviewTitle = preview.Title;
            PreviewAuthor = string.IsNullOrWhiteSpace(preview.Author)
                ? SocialAccountAuth.GetPlatformTitle(preview.Platform)
                : $"@{preview.Author.TrimStart('@')}";
            PreviewThumbnail = preview.ThumbnailUrl;
            ShowLinkPreview = !string.IsNullOrWhiteSpace(PreviewTitle)
                              || !string.IsNullOrWhiteSpace(PreviewThumbnail);

            if (ShowLinkPreview)
            {
                var activeUrl = ResolveActiveDownloadUrl();
                if (!string.IsNullOrWhiteSpace(activeUrl))
                    _pendingDownloadUrl = ContentUrlNormalizer.Normalize(activeUrl);

                ClearUrlFieldOnly();
                OnPropertyChanged(nameof(ShowFallbackDownloadButton));
            }

            _logger.LogInformation(
                "ApplyMetadata: preview shown. Title={Title}, Author={Author}, Thumb={Thumb}, ShowLinkPreview={Show}",
                PreviewTitle,
                PreviewAuthor,
                PreviewThumbnail,
                ShowLinkPreview);
        }
        else
        {
            ShowLinkPreview = false;
            _logger.LogWarning(
                "ApplyMetadata: no preview from metadata. Qualities={QualityCount}",
                metadata.Qualities.Count);
        }

        QualityOptions.Clear();
        foreach (var q in metadata.Qualities)
            QualityOptions.Add(q);

        SelectedQuality = metadata.Qualities.FirstOrDefault(q => q.IsDefault)
                          ?? metadata.Qualities.FirstOrDefault();
        ShowQualityPicker = QualityOptions.Count > 0;

        _logger.LogDebug(
            "ApplyMetadata: ShowQualityPicker={ShowQuality}, selected={Quality}",
            ShowQualityPicker,
            SelectedQuality?.Label);

        UpdatePreviewAccountIndicators();
    }

    private void UpdatePreviewAccountIndicators()
    {
        if (!ShowLinkPreview)
        {
            PreviewPlatformIconFile = null;
            PreviewAuthStatusIconFile = null;
            PreviewUsesAuthenticatedAccount = false;
            OnPropertyChanged(nameof(ShowPreviewAccountIndicators));
            return;
        }

        var platform = ResolvePreviewPlatform();
        PreviewPlatformIconFile = SocialPlatformIcons.GetIconFileName(platform);
        PreviewUsesAuthenticatedAccount = ResolvePreviewUsesAuthenticatedAccount(
            SelectedAccountOption,
            _lastAppSettings,
            _lastPlatformAccounts,
            _lastDefaultAccount);
        PreviewAuthStatusIconFile = SocialPlatformIcons.GetAuthStatusIconFileName(PreviewUsesAuthenticatedAccount);

        OnPropertyChanged(nameof(ShowPreviewAccountIndicators));
    }

    private SocialPlatform ResolvePreviewPlatform()
    {
        if (_previewPlatform is { } platform)
            return platform;

        var activeUrl = ResolveActiveDownloadUrl();
        return !string.IsNullOrWhiteSpace(activeUrl)
               && UrlPlatformDetector.TryDetect(activeUrl, out var detected, out _)
            ? detected
            : SocialPlatform.YouTube;
    }

    private static bool ResolvePreviewUsesAuthenticatedAccount(
        AccountPickerOption? selected,
        AppUserSettings? appSettings,
        IReadOnlyList<SocialAccount> accounts,
        SocialAccount? defaultAccount)
    {
        if (selected?.IsNoAccount == true)
            return false;

        if (selected is { IsNoAccount: false, AccountId: not null })
            return true;

        if (appSettings?.PreferDownloadWithoutAccount == true && accounts.Count == 0)
            return false;

        var account = defaultAccount
                      ?? accounts.FirstOrDefault(a => a.IsDefault)
                      ?? accounts.FirstOrDefault(a => SocialAccountAuth.HasAuth(a))
                      ?? accounts.FirstOrDefault();

        return account is not null && SocialAccountAuth.HasAuth(account);
    }

    private async Task LoadPreviewImageAsync(string? url)
    {
        _previewImageCts?.Cancel();
        _previewImageCts?.Dispose();
        _previewImageCts = new CancellationTokenSource();
        var ct = _previewImageCts.Token;

        await MainThread.InvokeOnMainThreadAsync(() => PreviewImageFile = null);

        if (string.IsNullOrWhiteSpace(url))
            return;

        var path = await _remoteImageCache.GetCachedFilePathAsync(url, ct).ConfigureAwait(false);
        if (ct.IsCancellationRequested)
            return;

        await MainThread.InvokeOnMainThreadAsync(() => PreviewImageFile = path);
    }

    private void FinishLinkMetadataLoading()
    {
        IsLoadingPreview = false;
        IsLoadingQualities = false;

        if (ShowLinkPreview && !string.IsNullOrWhiteSpace(PreviewThumbnail))
            _ = LoadPreviewImageAsync(PreviewThumbnail);
    }

    private void CancelPendingUrlWork()
    {
        _urlRefreshCts?.Cancel();
        _urlRefreshCts?.Dispose();
        _urlRefreshCts = null;

        _metadataRefreshCts?.Cancel();
        _metadataRefreshCts?.Dispose();
        _metadataRefreshCts = null;

        _previewImageCts?.Cancel();
        _previewImageCts?.Dispose();
        _previewImageCts = null;

        IsLoadingPreview = false;
        IsLoadingQualities = false;
    }

    private Task ClearDownloadInputAsync() =>
        MainThread.InvokeOnMainThreadAsync(ClearDownloadInputCore);

    private void ClearDownloadInputCore()
    {
        CancelPendingUrlWork();
        _pendingDownloadUrl = null;

        _suppressUrlNormalization = true;
        Url = string.Empty;
        _suppressUrlNormalization = false;

        ClearLinkMetadataUi();
        ResetAccountPickerUi();
        OnPropertyChanged(nameof(HasUrl));
        OnPropertyChanged(nameof(ShowFallbackDownloadButton));
    }

    private void ClearUrlFieldOnly()
    {
        _suppressUrlNormalization = true;
        Url = string.Empty;
        _suppressUrlNormalization = false;
        OnPropertyChanged(nameof(HasUrl));
    }

    private string? ResolveActiveDownloadUrl()
    {
        if (!string.IsNullOrWhiteSpace(Url))
            return CleanUrlForDisplay(Url);

        return _pendingDownloadUrl;
    }

    private void ClearLinkMetadataUi()
    {
        _pendingDownloadUrl = null;
        _previewPlatform = null;
        ShowLinkPreview = false;
        OnPropertyChanged(nameof(ShowFallbackDownloadButton));
        PreviewTitle = null;
        PreviewAuthor = null;
        PreviewThumbnail = null;
        PreviewImageFile = null;
        PreviewPlatformIconFile = null;
        PreviewAuthStatusIconFile = null;
        PreviewUsesAuthenticatedAccount = false;
        OnPropertyChanged(nameof(ShowPreviewAccountIndicators));

        ShowQualityPicker = false;
        SelectedQuality = null;
        QualityOptions.Clear();
    }

    private void ResetAccountPickerUi()
    {
        ShowAccountPicker = false;
        _suppressAccountSelectionChange = true;
        SelectedAccountOption = null;
        _suppressAccountSelectionChange = false;
        AccountOptions.Clear();
        _lastPlatformAccounts = [];
        _lastDefaultAccount = null;
        _lastAppSettings = null;
    }

    private async Task RefreshAccountPickerAsync(bool preserveUserSelection = false)
    {
        var activeUrl = ResolveActiveDownloadUrl();
        if (string.IsNullOrWhiteSpace(activeUrl)
            || !UrlPlatformDetector.TryDetect(activeUrl, out var platform, out _))
        {
            await MainThread.InvokeOnMainThreadAsync(ResetAccountPickerUi);
            return;
        }

        var preserveNoAccount = preserveUserSelection && SelectedAccountOption?.IsNoAccount == true;
        int? preserveAccountId = preserveUserSelection && SelectedAccountOption is { IsNoAccount: false, AccountId: var pid }
            ? pid
            : null;

        var accounts = await _accountService.GetAccountsForPlatformAsync(platform).ConfigureAwait(false);
        var appSettings = await _settings.GetAppSettingsAsync().ConfigureAwait(false);
        var defaultAccount = await _accountService.GetDefaultAccountAsync(platform).ConfigureAwait(false);

        _lastPlatformAccounts = accounts;
        _lastDefaultAccount = defaultAccount;
        _lastAppSettings = appSettings;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            AccountOptions.Clear();
            AccountOptions.Add(AccountPickerOption.WithoutAccount(platform));

            foreach (var account in accounts)
                AccountOptions.Add(AccountPickerOption.FromAccount(account, account.IsDefault));

            var withoutAccount = AccountOptions.FirstOrDefault(o => o.IsNoAccount);
            _suppressAccountSelectionChange = true;

            if (preserveNoAccount)
                SelectedAccountOption = withoutAccount ?? AccountOptions.FirstOrDefault();
            else if (preserveAccountId is int accountId)
            {
                SelectedAccountOption = AccountOptions.FirstOrDefault(o => o.AccountId == accountId)
                                        ?? withoutAccount
                                        ?? AccountOptions.FirstOrDefault();
            }
            else if (TryGetSavedAccountId(appSettings, platform, out var savedAccountId)
                     && AccountOptions.FirstOrDefault(o => o.AccountId == savedAccountId) is { } savedOption)
            {
                SelectedAccountOption = savedOption;
            }
            else
            {
                SelectedAccountOption = ResolveInitialAccountOption(
                    accounts,
                    defaultAccount,
                    withoutAccount);
            }

            _suppressAccountSelectionChange = false;

            AccountPickerHint = SelectedAccountOption?.IsNoAccount == true
                ? $"Публичный контент {SocialAccountAuth.GetPlatformTitle(platform)} без cookies. Для закрытого — выберите аккаунт или добавьте в настройках."
                : $"Скачивание с cookies аккаунта ({SocialAccountAuth.GetPlatformTitle(platform)}).";

            ShowAccountPicker = ShouldShowAccountPicker(appSettings, platform, accounts, defaultAccount);
            UpdatePreviewAccountIndicators();
        });
    }

    private static AccountPickerOption? ResolveInitialAccountOption(
        IReadOnlyList<SocialAccount> accounts,
        SocialAccount? defaultAccount,
        AccountPickerOption? withoutAccount)
    {
        if (accounts.Count == 0)
            return withoutAccount;

        var preferred = defaultAccount
                        ?? accounts.FirstOrDefault(a => a.IsDefault)
                        ?? accounts.FirstOrDefault(a => SocialAccountAuth.HasAuth(a))
                        ?? accounts[0];

        return AccountPickerOption.FromAccount(preferred, preferred.IsDefault);
    }

    private (bool UseAccount, int? AccountId) ResolveDownloadAccountSelection()
    {
        if (SelectedAccountOption is { IsNoAccount: false, AccountId: int selectedId })
            return (true, selectedId);

        if (SelectedAccountOption?.IsNoAccount == true)
            return (false, null);

        var account = _lastDefaultAccount
                      ?? _lastPlatformAccounts.FirstOrDefault(a => a.IsDefault)
                      ?? _lastPlatformAccounts.FirstOrDefault(a => SocialAccountAuth.HasAuth(a))
                      ?? _lastPlatformAccounts.FirstOrDefault();

        if (account is not null && SocialAccountAuth.HasAuth(account))
            return (true, account.Id);

        return (false, null);
    }

    private static bool ShouldShowAccountPicker(
        AppUserSettings appSettings,
        SocialPlatform platform,
        IReadOnlyList<SocialAccount> accounts,
        SocialAccount? defaultAccount)
    {
        if (appSettings.PreferDownloadWithoutAccount)
            return false;

        if (accounts.Count == 0)
            return false;

        if (TryGetSavedAccountId(appSettings, platform, out var savedAccountId)
            && accounts.Any(a => a.Id == savedAccountId))
            return false;

        if (defaultAccount is not null)
            return false;

        return true;
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _tagList.GetSortedTagsAsync();
        var selectedIds = _selectedTagIds.ToHashSet();
        Tags.Clear();
        TagChips.Clear();
        foreach (var tag in tags)
        {
            Tags.Add(tag);
            TagChips.Add(new TagChipViewModel(tag));
        }

        _selectedTagIds.Clear();
        foreach (var id in selectedIds.Where(id => Tags.Any(t => t.Id == id)))
            _selectedTagIds.Add(id);

        SyncTagChipSelection();
    }

    private void SyncTagChipSelection()
    {
        foreach (var chip in TagChips)
            chip.IsSelected = _selectedTagIds.Contains(chip.Tag.Id);

        OnPropertyChanged(nameof(IsNoTagSelected));
    }

    private async Task RefreshRecentAsync()
    {
        var appSettings = await _settings.GetAppSettingsAsync();
        RecentCountLabel = appSettings.RecentDownloadsCount;

        var items = await _repository.GetRecentContentAsync(appSettings.RecentDownloadsCount);
        RecentDownloads.Clear();
        foreach (var item in items)
        {
            var orderedTags = await _tagList.SortTagsAsync(item.Tags);
            RecentDownloads.Add(ContentItemDisplayModel.FromEntity(
                item,
                _storagePaths.DownloadsPath,
                orderedTags));
        }

        OnPropertyChanged(nameof(HasNoRecentDownloads));
        OnPropertyChanged(nameof(RecentDownloadsHeader));
    }

    private async Task ApplyPendingShareUrlAsync()
    {
        var pending = await _settings.GetPendingShareUrlAsync();
        if (!string.IsNullOrWhiteSpace(pending))
        {
            SetUrl(CleanUrlForDisplay(pending));
            await _settings.SetPendingShareUrlAsync(null);
        }
    }

    private void SetUrl(string value)
    {
        _holdDownloadFormClear = false;
        _pendingDownloadUrl = null;
        _suppressUrlNormalization = true;
        Url = value;
        _suppressUrlNormalization = false;
        ScheduleUrlRefresh();
    }

    private static string CleanUrlForDisplay(string url)
    {
        var trimmed = ContentUrlNormalizer.PrepareForDetection(url);
        return UrlPlatformDetector.TryDetect(trimmed, out _, out _)
            ? ContentUrlNormalizer.Normalize(trimmed)
            : trimmed;
    }

    private async Task<string> EnsureCleanUrlInFieldAsync(string currentUrl)
    {
        var cleaned = CleanUrlForDisplay(currentUrl);

        if (!string.IsNullOrWhiteSpace(Url)
            && !string.Equals(cleaned, Url.Trim(), StringComparison.Ordinal))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _suppressUrlNormalization = true;
                Url = cleaned;
                _suppressUrlNormalization = false;
            });
        }
        else if (string.IsNullOrWhiteSpace(Url)
                 && !string.Equals(cleaned, _pendingDownloadUrl, StringComparison.Ordinal))
        {
            await MainThread.InvokeOnMainThreadAsync(() => _pendingDownloadUrl = cleaned);
        }

        return cleaned;
    }

    public ObservableCollection<ContentTag> Tags { get; } = [];

    public bool IsNoTagSelected => _selectedTagIds.Count == 0;

    private static DownloadRequest BuildDownloadRequest(
        string normalizedUrl,
        AppUserSettings appSettings,
        bool useAccount,
        int? accountId,
        IReadOnlyList<int> tagIds,
        DownloadQualityOption? quality)
    {
        UrlPlatformDetector.TryDetect(normalizedUrl, out _, out var kind);

        return new DownloadRequest
        {
            Url = normalizedUrl,
            ContentKind = kind,
            TagIds = tagIds,
            SocialAccountId = accountId,
            UseSocialAccount = accountId.HasValue || useAccount,
            UsePostedDateForFolder = appSettings.UsePostedDateForFolder,
            QualityFormatId = quality?.Id ?? QualityIds.Best,
            QualityFormatSelector = quality?.FormatSelector
        };
    }

    private async Task RunDownloadJobAsync(
        PendingDownloadViewModel job,
        string normalizedUrl,
        bool useAccount,
        int? accountId,
        AppUserSettings appSettings)
    {
        var tagIds = _selectedTagIds.ToList();
        var request = BuildDownloadRequest(
            normalizedUrl,
            appSettings,
            useAccount,
            accountId,
            tagIds,
            SelectedQuality);

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                job.Status = "Подготовка…";
                job.Progress = 0.05;
            });

            _logger.LogInformation("RunDownloadJobAsync: started for {Url}", normalizedUrl);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                job.Status = "Скачивание…";
                job.Progress = 0.35;
            });

            var result = await _orchestrator.DownloadAndSaveAsync(request).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (result.Success && result.Content is not null)
                {
                    _logger.LogInformation(
                        "RunDownloadJobAsync: success contentId={Id}, media={Count}",
                        result.Content.Id,
                        result.Content.MediaFiles.Count);
                    var mediaCount = result.Content.MediaFiles.Count;
                    job.Status = mediaCount > 1
                        ? $"Готово ({mediaCount} файлов)"
                        : "Готово";
                    job.Progress = 1;
                    job.IsCompleted = true;
                    _holdDownloadFormClear = true;
                    ClearDownloadInputCore();
                    await RefreshRecentAsync();
                    ActiveDownloads.Remove(job);
                }
                else
                {
                    _logger.LogWarning("RunDownloadJobAsync: failed {Error}", result.ErrorMessage);
                    job.Status = result.ErrorMessage ?? "Ошибка скачивания";
                    job.IsFailed = true;
                    _holdDownloadFormClear = false;
                }

                job.IsActive = false;
                OnPropertyChanged(nameof(HasActiveDownloads));
                if (!job.IsCompleted)
                    _ = RemoveFinishedJobLaterAsync(job);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunDownloadJobAsync exception for {Url}", normalizedUrl);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                job.Status = ex.Message;
                job.IsFailed = true;
                job.IsActive = false;
                OnPropertyChanged(nameof(HasActiveDownloads));
                _ = RemoveFinishedJobLaterAsync(job);
            });
        }
    }

    private async Task RemoveFinishedJobLaterAsync(PendingDownloadViewModel job)
    {
        await Task.Delay(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!job.IsActive && ActiveDownloads.Contains(job))
                ActiveDownloads.Remove(job);
            OnPropertyChanged(nameof(HasActiveDownloads));
        });
    }

    private async Task PersistAuthenticatedAccountChoiceAsync(SocialPlatform platform, int accountId)
    {
        var settings = await _settings.GetAppSettingsAsync().ConfigureAwait(false);
        settings.LastDownloadAccountIdByPlatform[platform.ToString()] = accountId;
        await _settings.SaveAppSettingsAsync(settings).ConfigureAwait(false);
    }

    private static bool TryGetSavedAccountId(
        AppUserSettings settings,
        SocialPlatform platform,
        out int accountId) =>
        settings.LastDownloadAccountIdByPlatform.TryGetValue(platform.ToString(), out accountId);

    [RelayCommand]
    private void DismissDownloadJob(PendingDownloadViewModel job)
    {
        if (job.IsActive) return;
        ActiveDownloads.Remove(job);
        OnPropertyChanged(nameof(HasActiveDownloads));
    }
}
