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
    IRecipient<TagsChangedMessage>,
    IRecipient<TagSortChangedMessage>,
    IRecipient<ContentDeletedMessage>,
    IRecipient<ShareUrlReceivedMessage>
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
    private bool _suppressUrlNormalization;
    private bool _holdDownloadFormClear;
    private string? _lastShareUrlApplied;
    private long _lastShareAppliedTick;
    private bool _messagingActive;

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

        WeakReferenceMessenger.Default.Register<TagsChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<ContentDeletedMessage>(this);
        WeakReferenceMessenger.Default.Register<ShareUrlReceivedMessage>(this);
    }

    public void ActivateMessaging()
    {
        if (_messagingActive)
            return;

        _messagingActive = true;
        WeakReferenceMessenger.Default.Register<TagSortChangedMessage>(this);
    }

    public void DeactivateMessaging()
    {
        if (!_messagingActive)
            return;

        _messagingActive = false;
        WeakReferenceMessenger.Default.Unregister<TagSortChangedMessage>(this);
    }

    public async Task ApplyIncomingShareUrlAsync(string url, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (force)
            ResetShareUrlApplyGuard();

        if (!TryBeginShareUrlApply(url))
            return;

        ContentNavigationHelper.BeginShareSession();
        await MainThread.InvokeOnMainThreadAsync(() => ApplySharedUrl(url));
        await _settings.SetPendingShareUrlAsync(null);
    }

    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);

    public bool HasPendingLinkPreviews => PendingLinkPreviews.Count > 0;

    public bool IsLoadingLinkMetadata => PendingLinkPreviews.Any(p => p.IsLoadingMetadata);

    public bool ShowLinkMetadataReady => HasPendingLinkPreviews;

    public bool ShowNewTagPanel => IsNewTagPanelVisible && !IsLoadingLinkMetadata;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _recentCountLabel;

    public string RecentDownloadsHeader => $"Последние скачивания";

    public string NewTagPanelToggleText => IsNewTagPanelVisible ? "Скрыть" : "+ Новый тег";

    public IReadOnlyList<string> EmojiSuggestions { get; } = TagEmojiLibrary.Suggested;

    public ObservableCollection<ContentItemDisplayModel> RecentDownloads { get; } = [];

    public ObservableCollection<LinkPreviewItemViewModel> PendingLinkPreviews { get; } = [];

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

    public ObservableCollection<PendingDownloadViewModel> ActiveDownloads { get; } = [];

    public bool HasActiveDownloads => ActiveDownloads.Count > 0;

    public IReadOnlyList<string> ColorPresets { get; } = TagColorPresets.Colors;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        _ = _linkMetadata.WarmupAsync();

        var hadPendingShare = !string.IsNullOrWhiteSpace(await _settings.GetPendingShareUrlAsync());
        await ApplyPendingShareUrlAsync();

        if (_holdDownloadFormClear && !hadPendingShare && string.IsNullOrWhiteSpace(Url))
            await ClearDownloadInputAsync();

        _ = LoadTagsAsync();
        _ = RefreshRecentAsync();
    }

    partial void OnUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasUrl));

        if (!_suppressUrlNormalization && !string.IsNullOrWhiteSpace(value))
            _holdDownloadFormClear = false;

        if (!_suppressUrlNormalization)
            ScheduleUrlRefresh();
    }

    partial void OnIsNewTagPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(NewTagPanelToggleText));
        OnPropertyChanged(nameof(ShowNewTagPanel));
    }

    [RelayCommand]
    private async Task DownloadPreviewAsync(LinkPreviewItemViewModel? preview)
    {
        if (preview is null)
            return;

        if (!UrlPlatformDetector.TryDetect(preview.Url, out _, out _))
        {
            await _toast.ShowWarningAsync("Ссылка не распознана. Поддерживаются Instagram, YouTube и ВКонтакте.");
            return;
        }

        var normalizedUrl = ContentUrlNormalizer.Normalize(preview.Url);

        if (!await DuplicateDownloadHelper.ConfirmReplaceIfExistsAsync(_repository, normalizedUrl))
            return;

        var (useAccount, accountId) = ResolveDownloadAccountSelection(preview);
        var appSettings = await _settings.GetAppSettingsAsync().ConfigureAwait(false);

        var job = new PendingDownloadViewModel(normalizedUrl, preview.PreviewTitle, preview.PreviewAuthor);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ActiveDownloads.Insert(0, job);
            OnPropertyChanged(nameof(HasActiveDownloads));
            RemovePreviewItem(preview);

            if (accountId is int savedAccountId
                && UrlPlatformDetector.TryDetect(normalizedUrl, out var detectedPlatform, out _))
            {
                _ = PersistAuthenticatedAccountChoiceAsync(detectedPlatform, savedAccountId);
            }

            _holdDownloadFormClear = true;
            ClearUrlFieldOnly();
            StatusMessage = "Скачивание запущено. Можно добавить ещё ссылки.";
        });

        _ = RunDownloadJobAsync(job, normalizedUrl, useAccount, accountId, appSettings, preview.SelectedQuality);
    }

    [RelayCommand]
    private async Task ClearUrlAsync()
    {
        _holdDownloadFormClear = false;
        await ClearDownloadInputAsync();
    }

    [RelayCommand]
    private void DismissPreview(LinkPreviewItemViewModel? preview)
    {
        if (preview is null)
            return;

        RemovePreviewItem(preview);
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

    private void ApplySharedUrl(string url) => SetUrl(CleanUrlForDisplay(url));

    private bool TryBeginShareUrlApply(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var now = Environment.TickCount64;
        if (string.Equals(_lastShareUrlApplied, url, StringComparison.OrdinalIgnoreCase)
            && now - _lastShareAppliedTick < 2000)
            return false;

        _lastShareUrlApplied = url;
        _lastShareAppliedTick = now;
        return true;
    }

    private void ResetShareUrlApplyGuard()
    {
        _lastShareUrlApplied = null;
        _lastShareAppliedTick = 0;
    }

    public void Receive(TagsChangedMessage message)
    {
        _ = LoadTagsAsync();
        _ = RefreshRecentAsync();
    }

    public void Receive(TagSortChangedMessage message)
    {
        _ = LoadTagsAsync();
        _ = RefreshRecentAsync();
    }

    public void Receive(ContentDeletedMessage message) =>
        _ = MainThread.InvokeOnMainThreadAsync(RefreshRecentAsync);

    public void Receive(ShareUrlReceivedMessage message) =>
        _ = ApplyIncomingShareUrlAsync(message.Url, force: true);

    [RelayCommand]
    private async Task DeleteRecentItemAsync(ContentItemDisplayModel item)
    {
        if (!await ContentDeletionHelper.ConfirmAndDeleteAsync(_repository, item))
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RecentDownloads.Remove(item);
            OnPropertyChanged(nameof(HasNoRecentDownloads));
            OnPropertyChanged(nameof(RecentDownloadsHeader));
        });
        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
    }

    [RelayCommand]
    private Task OpenRecentContentAsync(ContentItemDisplayModel? item) =>
        item is null
            ? Task.CompletedTask
            : ContentNavigationHelper.OpenDetailAsync(item.Id);

    private void ScheduleUrlRefresh()
    {
        _urlRefreshCts?.Cancel();
        _urlRefreshCts?.Dispose();
        _urlRefreshCts = new CancellationTokenSource();
        var ct = _urlRefreshCts.Token;
        _ = RefreshAfterUrlChangeAsync(ct);
    }

    private async Task RefreshAfterUrlChangeAsync(CancellationToken ct)
    {
        var urlSnapshot = Url;
        if (string.IsNullOrWhiteSpace(urlSnapshot)
            || !UrlPlatformDetector.TryDetect(urlSnapshot, out _, out _))
        {
            return;
        }

        try
        {
            await Task.Delay(200, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested)
                return;

            var current = Url;
            if (string.IsNullOrWhiteSpace(current)
                || !UrlPlatformDetector.TryDetect(current, out _, out _))
            {
                return;
            }

            if (!string.Equals(
                    CleanUrlForDisplay(current),
                    CleanUrlForDisplay(urlSnapshot),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var normalized = CleanUrlForDisplay(current);
            await MainThread.InvokeOnMainThreadAsync(ClearUrlFieldOnly);
            await EnqueuePreviewFromUrlAsync(normalized).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("RefreshAfterUrlChangeAsync cancelled");
        }
    }

    private async Task EnqueuePreviewFromUrlAsync(string normalizedUrl)
    {
        LinkPreviewItemViewModel? item = null;
        var accepted = await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!TryInsertPreviewItem(normalizedUrl, out var created))
                return false;

            item = created;
            return true;
        }).ConfigureAwait(false);

        if (!accepted || item is null)
            return;

        _ = LoadMetadataForPreviewItemAsync(item, item.MetadataCts!.Token);
    }

    private bool TryInsertPreviewItem(string normalizedUrl, out LinkPreviewItemViewModel? item)
    {
        normalizedUrl = ContentUrlNormalizer.Normalize(normalizedUrl);
        item = null;

        if (IsUrlAlreadyInQueue(normalizedUrl))
        {
            ClearUrlFieldOnly();
            _ = _toast.ShowAsync("Эта ссылка уже добавлена в очередь.");
            return false;
        }

        item = CreatePreviewItem(normalizedUrl);
        PendingLinkPreviews.Insert(0, item);
        NotifyPendingPreviewsChanged();
        return true;
    }

    private static bool IsUrlAlreadyInQueue(string normalizedUrl, IEnumerable<LinkPreviewItemViewModel> previews, IEnumerable<PendingDownloadViewModel> activeDownloads)
    {
        return previews.Any(p => string.Equals(
                   ContentUrlNormalizer.Normalize(p.Url),
                   normalizedUrl,
                   StringComparison.OrdinalIgnoreCase))
               || activeDownloads.Any(j => j.IsActive && string.Equals(
                   ContentUrlNormalizer.Normalize(j.Url),
                   normalizedUrl,
                   StringComparison.OrdinalIgnoreCase));
    }

    private bool IsUrlAlreadyInQueue(string normalizedUrl) =>
        IsUrlAlreadyInQueue(normalizedUrl, PendingLinkPreviews, ActiveDownloads);

    private LinkPreviewItemViewModel CreatePreviewItem(string normalizedUrl)
    {
        var item = new LinkPreviewItemViewModel(ContentUrlNormalizer.Normalize(normalizedUrl))
        {
            AccountSelectionChanged = preview =>
            {
                if (preview.SelectedAccountOption is { IsNoAccount: false, AccountId: int accountId, Platform: var platform })
                    _ = PersistAuthenticatedAccountChoiceAsync(platform.Value, accountId);

                _ = RefreshMetadataForPreviewAsync(preview);
            }
        };
        item.MetadataCts = new CancellationTokenSource();
        return item;
    }

    private async Task RefreshMetadataForPreviewAsync(LinkPreviewItemViewModel item)
    {
        item.MetadataCts?.Cancel();
        item.MetadataCts?.Dispose();
        item.MetadataCts = new CancellationTokenSource();
        var ct = item.MetadataCts.Token;

        await MainThread.InvokeOnMainThreadAsync(() => item.IsLoadingMetadata = true);
        NotifyPendingPreviewsChanged();

        try
        {
            await Task.Delay(350, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested || !PendingLinkPreviews.Contains(item))
                return;

            await LoadMetadataForPreviewItemAsync(item, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // выбран другой аккаунт
        }
    }

    private async Task LoadMetadataForPreviewItemAsync(LinkPreviewItemViewModel item, CancellationToken ct)
    {
        try
        {
            await RefreshAccountPickerForItemAsync(item, preserveUserSelection: false).ConfigureAwait(false);
            if (ct.IsCancellationRequested || !PendingLinkPreviews.Contains(item))
                return;

            var (useAccount, accountId) = ResolveDownloadAccountSelection(item);
            var metadata = await _linkMetadata.GetMetadataAsync(
                item.Url, accountId, useAccount, ct).ConfigureAwait(false);

            if (ct.IsCancellationRequested || !PendingLinkPreviews.Contains(item))
                return;

            await MainThread.InvokeOnMainThreadAsync(() => ApplyMetadataToItem(item, metadata));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadMetadataForPreviewItemAsync failed for url={Url}", item.Url);
            if (ct.IsCancellationRequested || !PendingLinkPreviews.Contains(item))
                return;

            var platform = UrlPlatformDetector.TryDetect(item.Url, out var p, out _)
                ? p
                : SocialPlatform.YouTube;

            await MainThread.InvokeOnMainThreadAsync(() =>
                ApplyMetadataToItem(item, new LinkMetadataResult
                {
                    Qualities = [DownloadQualityOption.BestQuality(platform)]
                }));
        }
        finally
        {
            if (!ct.IsCancellationRequested && PendingLinkPreviews.Contains(item))
            {
                await MainThread.InvokeOnMainThreadAsync(() => item.IsLoadingMetadata = false);
                NotifyPendingPreviewsChanged();

                if (!string.IsNullOrWhiteSpace(item.PreviewThumbnail))
                    _ = LoadPreviewImageForItemAsync(item);
            }
        }
    }

    private void ApplyMetadataToItem(LinkPreviewItemViewModel item, LinkMetadataResult metadata)
    {
        SocialPlatform platform = default;
        ContentKind kind = default;
        var hasValidUrl = UrlPlatformDetector.TryDetect(item.Url, out platform, out kind);

        if (metadata.Preview is { } preview)
        {
            item.Platform = preview.Platform;
            item.PreviewTitle = preview.Title;
            item.PreviewAuthor = string.IsNullOrWhiteSpace(preview.Author)
                ? SocialAccountAuth.GetPlatformTitle(preview.Platform)
                : $"@{preview.Author.TrimStart('@')}";
            item.PreviewThumbnail = preview.ThumbnailUrl;

            _logger.LogInformation(
                "ApplyMetadataToItem: url={Url}, title={Title}, author={Author}, thumb={Thumb}",
                item.Url,
                item.PreviewTitle,
                item.PreviewAuthor,
                item.PreviewThumbnail);
        }
        else if (hasValidUrl)
        {
            item.Platform = platform;
            item.PreviewTitle = kind switch
            {
                ContentKind.Reel => "Reels",
                ContentKind.Story => "Stories",
                _ => SocialAccountAuth.GetPlatformTitle(platform)
            };
            item.PreviewAuthor = SocialAccountAuth.GetPlatformTitle(platform);
            item.PreviewThumbnail = null;
            item.PreviewImageFile = null;

            _logger.LogWarning(
                "ApplyMetadataToItem: fallback preview shell for {Platform}/{Kind}, url={Url}",
                platform,
                kind,
                item.Url);
        }

        item.QualityOptions.Clear();
        foreach (var q in metadata.Qualities)
            item.QualityOptions.Add(q);

        item.SelectedQuality = metadata.Qualities.FirstOrDefault(q => q.IsDefault)
                               ?? metadata.Qualities.FirstOrDefault();
        item.ShowQualityPicker = item.QualityOptions.Count > 0;

        item.UpdateAuthIndicators(
            item.Platform ?? platform,
            item.SelectedAccountOption,
            item.LastAppSettings,
            item.LastPlatformAccounts,
            item.LastDefaultAccount);
    }

    private async Task LoadPreviewImageForItemAsync(LinkPreviewItemViewModel item)
    {
        item.ImageCts?.Cancel();
        item.ImageCts?.Dispose();
        item.ImageCts = new CancellationTokenSource();
        var ct = item.ImageCts.Token;
        var url = item.PreviewThumbnail;

        await MainThread.InvokeOnMainThreadAsync(() => item.PreviewImageFile = null);

        if (string.IsNullOrWhiteSpace(url))
            return;

        var fetchOptions = await BuildPreviewImageFetchOptionsAsync(item, ct).ConfigureAwait(false);
        var path = await _remoteImageCache.GetCachedFilePathAsync(url, fetchOptions, ct).ConfigureAwait(false);
        if (ct.IsCancellationRequested || !PendingLinkPreviews.Contains(item))
            return;

        await MainThread.InvokeOnMainThreadAsync(() => item.PreviewImageFile = path);
    }

    private async Task<RemoteImageRequestOptions?> BuildPreviewImageFetchOptionsAsync(
        LinkPreviewItemViewModel item,
        CancellationToken cancellationToken)
    {
        if (item.Platform != SocialPlatform.Instagram)
            return null;

        var cookieHeader = await ResolvePreviewInstagramCookieHeaderAsync(item, cancellationToken)
            .ConfigureAwait(false);
        return RemoteImageRequestOptions.ForInstagram(cookieHeader);
    }

    private async Task<string?> ResolvePreviewInstagramCookieHeaderAsync(
        LinkPreviewItemViewModel item,
        CancellationToken cancellationToken)
    {
        var (useAccount, accountId) = ResolveDownloadAccountSelection(item);
        SocialAccount? account = null;

        if (useAccount && accountId is int id)
            account = await _accountService.GetAccountByIdAsync(id, cancellationToken).ConfigureAwait(false);

        account ??= item.LastDefaultAccount
                    ?? item.LastPlatformAccounts.FirstOrDefault(a => a.IsDefault)
                    ?? item.LastPlatformAccounts.FirstOrDefault(a => SocialAccountAuth.HasAuth(a));

        if (account is null || !SocialAccountAuth.HasAuth(account))
            return null;

        return SocialAccountAuth.BuildCookieHeader(account);
    }

    private void RemovePreviewItem(LinkPreviewItemViewModel item)
    {
        item.MetadataCts?.Cancel();
        item.MetadataCts?.Dispose();
        item.MetadataCts = null;

        item.ImageCts?.Cancel();
        item.ImageCts?.Dispose();
        item.ImageCts = null;

        PendingLinkPreviews.Remove(item);
        NotifyPendingPreviewsChanged();
    }

    private void NotifyPendingPreviewsChanged()
    {
        OnPropertyChanged(nameof(HasPendingLinkPreviews));
        OnPropertyChanged(nameof(IsLoadingLinkMetadata));
        OnPropertyChanged(nameof(ShowLinkMetadataReady));
        OnPropertyChanged(nameof(ShowNewTagPanel));
    }

    private void CancelPendingUrlWork()
    {
        _urlRefreshCts?.Cancel();
        _urlRefreshCts?.Dispose();
        _urlRefreshCts = null;
    }

    private Task ClearDownloadInputAsync() =>
        MainThread.InvokeOnMainThreadAsync(ClearDownloadInputCore);

    private void ClearDownloadInputCore()
    {
        CancelPendingUrlWork();

        _suppressUrlNormalization = true;
        Url = string.Empty;
        _suppressUrlNormalization = false;

        OnPropertyChanged(nameof(HasUrl));

        if (Shell.Current?.CurrentPage is Page page)
            page.Unfocus();
    }

    private void ClearUrlFieldOnly()
    {
        _suppressUrlNormalization = true;
        Url = string.Empty;
        _suppressUrlNormalization = false;
        OnPropertyChanged(nameof(HasUrl));
    }

    private async Task RefreshAccountPickerForItemAsync(
        LinkPreviewItemViewModel item,
        bool preserveUserSelection = false)
    {
        if (!UrlPlatformDetector.TryDetect(item.Url, out var platform, out _))
            return;

        var preserveNoAccount = preserveUserSelection && item.SelectedAccountOption?.IsNoAccount == true;
        int? preserveAccountId = preserveUserSelection
                                 && item.SelectedAccountOption is { IsNoAccount: false, AccountId: var pid }
            ? pid
            : null;

        var accounts = await _accountService.GetAccountsForPlatformAsync(platform).ConfigureAwait(false);
        var appSettings = await _settings.GetAppSettingsAsync().ConfigureAwait(false);
        var defaultAccount = await _accountService.GetDefaultAccountAsync(platform).ConfigureAwait(false);

        item.LastPlatformAccounts = accounts;
        item.LastDefaultAccount = defaultAccount;
        item.LastAppSettings = appSettings;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            item.AccountOptions.Clear();
            item.AccountOptions.Add(AccountPickerOption.WithoutAccount(platform));

            foreach (var account in accounts)
                item.AccountOptions.Add(AccountPickerOption.FromAccount(account, account.IsDefault));

            var withoutAccount = item.AccountOptions.FirstOrDefault(o => o.IsNoAccount);
            AccountPickerOption? selected;

            if (preserveNoAccount)
                selected = withoutAccount ?? item.AccountOptions.FirstOrDefault();
            else if (preserveAccountId is int accountId)
            {
                selected = item.AccountOptions.FirstOrDefault(o => o.AccountId == accountId)
                           ?? withoutAccount
                           ?? item.AccountOptions.FirstOrDefault();
            }
            else if (TryGetSavedAccountId(appSettings, platform, out var savedAccountId)
                     && item.AccountOptions.FirstOrDefault(o => o.AccountId == savedAccountId) is { } savedOption)
            {
                selected = savedOption;
            }
            else
            {
                selected = ResolveInitialAccountOption(accounts, defaultAccount, withoutAccount);
            }

            item.SetSelectedAccountOption(selected);

            item.AccountPickerHint = item.SelectedAccountOption?.IsNoAccount == true
                ? $"Публичный контент {SocialAccountAuth.GetPlatformTitle(platform)} без cookies. Для закрытого — выберите аккаунт или добавьте в настройках."
                : $"Скачивание с cookies аккаунта ({SocialAccountAuth.GetPlatformTitle(platform)}).";

            item.ShowAccountPicker = ShouldShowAccountPicker(appSettings, platform, accounts, defaultAccount);
            item.Platform ??= platform;
            item.UpdateAuthIndicators(
                platform,
                item.SelectedAccountOption,
                appSettings,
                accounts,
                defaultAccount);
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

    private (bool UseAccount, int? AccountId) ResolveDownloadAccountSelection(LinkPreviewItemViewModel item)
    {
        if (item.SelectedAccountOption is { IsNoAccount: false, AccountId: int selectedId })
            return (true, selectedId);

        if (item.SelectedAccountOption?.IsNoAccount == true)
            return (false, null);

        var account = item.LastDefaultAccount
                      ?? item.LastPlatformAccounts.FirstOrDefault(a => a.IsDefault)
                      ?? item.LastPlatformAccounts.FirstOrDefault(a => SocialAccountAuth.HasAuth(a))
                      ?? item.LastPlatformAccounts.FirstOrDefault();

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
        var items = await _repository.GetRecentContentAsync(appSettings.RecentDownloadsCount);

        var displayItems = new List<ContentItemDisplayModel>();
        foreach (var item in items)
        {
            if (!ContentThumbnailHelper.HasAvailableMedia(item))
            {
                await _repository.DeleteContentAsync(item.Id);
                continue;
            }

            var orderedTags = await _tagList.SortTagsAsync(item.Tags);
            displayItems.Add(ContentItemDisplayModel.FromEntity(
                item,
                _storagePaths.DownloadsPath,
                orderedTags));
        }

        var recentCountLabel = appSettings.RecentDownloadsCount;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RecentCountLabel = recentCountLabel;
            RecentDownloads.Clear();
            foreach (var display in displayItems)
                RecentDownloads.Add(display);

            OnPropertyChanged(nameof(HasNoRecentDownloads));
            OnPropertyChanged(nameof(RecentDownloadsHeader));
        });
    }

    private async Task ApplyPendingShareUrlAsync()
    {
        var pending = await _settings.GetPendingShareUrlAsync();
        if (string.IsNullOrWhiteSpace(pending))
            return;

        ResetShareUrlApplyGuard();
        if (!TryBeginShareUrlApply(pending))
            return;

        await _settings.SetPendingShareUrlAsync(null);
        ContentNavigationHelper.BeginShareSession();
        SetUrl(CleanUrlForDisplay(pending));
    }

    private void SetUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        _holdDownloadFormClear = false;
        var cleaned = CleanUrlForDisplay(value);

        if (UrlPlatformDetector.TryDetect(cleaned, out _, out _))
        {
            _ = EnqueuePreviewFromUrlAsync(cleaned);
            return;
        }

        _suppressUrlNormalization = true;
        Url = cleaned;
        _suppressUrlNormalization = false;
        OnPropertyChanged(nameof(HasUrl));
        ScheduleUrlRefresh();
    }

    private static string CleanUrlForDisplay(string url)
    {
        var trimmed = ContentUrlNormalizer.PrepareForDetection(url);
        return UrlPlatformDetector.TryDetect(trimmed, out _, out _)
            ? ContentUrlNormalizer.Normalize(trimmed)
            : trimmed;
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
        AppUserSettings appSettings,
        DownloadQualityOption? quality)
    {
        var tagIds = _selectedTagIds.ToList();
        var request = BuildDownloadRequest(
            normalizedUrl,
            appSettings,
            useAccount,
            accountId,
            tagIds,
            quality);

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
                    ClearUrlFieldOnly();
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
