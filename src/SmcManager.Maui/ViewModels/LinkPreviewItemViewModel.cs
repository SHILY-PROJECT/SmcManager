using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SmcManager.Core.Enums;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Один элемент очереди предпросмотра на вкладке «Скачать».
/// </summary>
public partial class LinkPreviewItemViewModel : ObservableObject
{
    public LinkPreviewItemViewModel(string url)
    {
        Url = url;
    }

    public string Url { get; }

    internal CancellationTokenSource? MetadataCts { get; set; }

    internal CancellationTokenSource? ImageCts { get; set; }

    [ObservableProperty]
    private bool _isLoadingMetadata = true;

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
    private bool _showQualityPicker;

    [ObservableProperty]
    private DownloadQualityOption? _selectedQuality;

    [ObservableProperty]
    private bool _showAccountPicker;

    [ObservableProperty]
    private string _accountPickerHint = string.Empty;

    [ObservableProperty]
    private AccountPickerOption? _selectedAccountOption;

    public SocialPlatform? Platform { get; set; }

    public ObservableCollection<DownloadQualityOption> QualityOptions { get; } = [];

    public ObservableCollection<AccountPickerOption> AccountOptions { get; } = [];

    internal IReadOnlyList<SocialAccount> LastPlatformAccounts { get; set; } = [];

    internal SocialAccount? LastDefaultAccount { get; set; }

    internal AppUserSettings? LastAppSettings { get; set; }

    internal Action<LinkPreviewItemViewModel>? AccountSelectionChanged { get; set; }

    private bool _suppressAccountSelectionChange;

    public bool ShowMetadataReady => !IsLoadingMetadata;

    public bool ShowPreviewAccountIndicators => !IsLoadingMetadata;

    public bool ShowPreviewDownloadButton => !IsLoadingMetadata;

    public string PreviewAuthStatusDescription =>
        PreviewUsesAuthenticatedAccount
            ? "Скачивание с авторизованным аккаунтом"
            : "Скачивание без аккаунта";

    partial void OnIsLoadingMetadataChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowMetadataReady));
        OnPropertyChanged(nameof(ShowPreviewDownloadButton));
        OnPropertyChanged(nameof(ShowPreviewAccountIndicators));
    }

    partial void OnSelectedAccountOptionChanged(AccountPickerOption? value)
    {
        if (_suppressAccountSelectionChange || value is null)
            return;

        AccountPickerHint = value.IsNoAccount
            ? "Будет попытка скачать без входа. Для закрытого контента выберите аккаунт или добавьте его в настройках."
            : "Скачивание с cookies выбранного аккаунта.";

        UpdateAuthIndicators(
            Platform ?? SocialPlatform.Instagram,
            value,
            LastAppSettings,
            LastPlatformAccounts,
            LastDefaultAccount);

        AccountSelectionChanged?.Invoke(this);
    }

    internal void SetSelectedAccountOption(AccountPickerOption? option)
    {
        _suppressAccountSelectionChange = true;
        SelectedAccountOption = option;
        _suppressAccountSelectionChange = false;
    }

    partial void OnPreviewUsesAuthenticatedAccountChanged(bool value) =>
        OnPropertyChanged(nameof(PreviewAuthStatusDescription));

    public void UpdateAuthIndicators(
        SocialPlatform platform,
        AccountPickerOption? selected,
        AppUserSettings? appSettings,
        IReadOnlyList<SocialAccount> accounts,
        SocialAccount? defaultAccount)
    {
        PreviewPlatformIconFile = SocialPlatformIcons.GetIconFileName(platform);
        PreviewUsesAuthenticatedAccount = ResolveUsesAuthenticatedAccount(
            selected, appSettings, accounts, defaultAccount);
        PreviewAuthStatusIconFile = SocialPlatformIcons.GetAuthStatusIconFileName(PreviewUsesAuthenticatedAccount);
    }

    private static bool ResolveUsesAuthenticatedAccount(
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
}
