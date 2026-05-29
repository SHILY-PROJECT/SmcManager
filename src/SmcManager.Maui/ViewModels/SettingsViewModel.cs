using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Infrastructure.Services;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;
using SmcManager.Maui.Messages;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Настройки: аккаунты соцсетей, прокси, тема.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISocialAccountService _accountService;
    private readonly ISocialAuthService _authService;
    private readonly ISocialAccountValidationService _accountValidation;
    private readonly ISettingsService _settings;
    private readonly IAppStoragePaths _storagePaths;
    private readonly ThemeService _themeService;
    private readonly BottomToastService _toast;

    public SettingsViewModel(
        ISocialAccountService accountService,
        ISocialAuthService authService,
        ISocialAccountValidationService accountValidation,
        ISettingsService settings,
        IAppStoragePaths storagePaths,
        ThemeService themeService,
        BottomToastService toast)
    {
        _accountService = accountService;
        _authService = authService;
        _accountValidation = accountValidation;
        _settings = settings;
        _storagePaths = storagePaths;
        _themeService = themeService;
        _toast = toast;
    }

    public ObservableCollection<SocialAccountRowViewModel> AccountRows { get; } = [];

    private readonly Dictionary<SocialAccountRowViewModel, PropertyChangedEventHandler> _accountRowHandlers = new();

    [ObservableProperty]
    private bool _proxyEnabled;

    [ObservableProperty]
    private string _proxyHost = string.Empty;

    [ObservableProperty]
    private string? _proxyPortText = "8080";

    [ObservableProperty]
    private string? _proxyUsername;

    [ObservableProperty]
    private string? _proxyPassword;

    [ObservableProperty]
    private SocialPlatform _newAccountPlatform = SocialPlatform.Instagram;

    [ObservableProperty]
    private PlatformOption? _selectedPlatformOption;

    [ObservableProperty]
    private bool _isAddAccountPanelVisible;

    [ObservableProperty]
    private bool _preferDownloadWithoutAccount = true;

    [ObservableProperty]
    private string _newAccountDisplayName = string.Empty;

    [ObservableProperty]
    private string _newAccountUsername = string.Empty;

    [ObservableProperty]
    private string? _newAccountAuth;

    [ObservableProperty]
    private bool _newAccountIsDefault = true;

    [ObservableProperty]
    private string _accountAuthHint = SocialAccountAuth.GetAuthHint(SocialPlatform.Instagram);

    [ObservableProperty]
    private string _accountAuthPlaceholder = SocialAccountAuth.GetAuthPlaceholder(SocialPlatform.Instagram);

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private bool _isValidatingAccount;

    private bool _suppressThemeChange;
    private bool _suppressSectionPersistence;
    private bool _suppressTagSortChange;

    [ObservableProperty]
    private bool _isAppearanceSectionExpanded = true;

    [ObservableProperty]
    private bool _isStorageSectionExpanded = true;

    [ObservableProperty]
    private bool _isDownloadSectionExpanded = true;

    [ObservableProperty]
    private bool _isTagsSectionExpanded = true;

    [ObservableProperty]
    private bool _isAccountsSectionExpanded = true;

    [ObservableProperty]
    private bool _isProxySectionExpanded = true;

    public IReadOnlyList<PlatformOption> PlatformOptions { get; } =
    [
        new() { Platform = SocialPlatform.Instagram, Title = "Instagram" },
        new() { Platform = SocialPlatform.YouTube, Title = "YouTube" },
        new() { Platform = SocialPlatform.Vkontakte, Title = "ВКонтакте" }
    ];

    public string AddAccountPanelToggleText =>
        IsAddAccountPanelVisible ? "Скрыть форму" : "+ Добавить аккаунт";

    public IReadOnlyList<TagSortOption> TagSortOptions { get; } =
    [
        new() { Mode = TagSortMode.Default, Title = "По умолчанию" },
        new() { Mode = TagSortMode.UsageCount, Title = "По количеству использования" },
        new() { Mode = TagSortMode.DateAdded, Title = "По дате добавления" },
        new() { Mode = TagSortMode.Name, Title = "По алфавиту" }
    ];

    [ObservableProperty]
    private TagSortOption? _selectedTagSortOption;

    public IReadOnlyList<int> RecentCountOptions { get; } = AppUserSettings.AllowedRecentCounts;

    [ObservableProperty]
    private int _selectedRecentCount = 10;

    [ObservableProperty]
    private bool _usePostedDateForFolder = true;

    public IReadOnlyList<StorageLocationOption> StorageLocationOptions { get; } = CreateStorageLocationOptions();

    [ObservableProperty]
    private StorageLocationOption? _selectedStorageOption;

    [ObservableProperty]
    private string _currentDataRootDisplay = string.Empty;

    [ObservableProperty]
    private string _portableDataPathPreview = string.Empty;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        _suppressThemeChange = true;
        IsDarkTheme = _themeService.CurrentTheme == AppColorTheme.Dark;
        _suppressThemeChange = false;

        var appSettings = await _settings.GetAppSettingsAsync();
        SelectedRecentCount = appSettings.RecentDownloadsCount;
        UsePostedDateForFolder = appSettings.UsePostedDateForFolder;
        PreferDownloadWithoutAccount = appSettings.PreferDownloadWithoutAccount;

        _suppressTagSortChange = true;
        SelectedTagSortOption = TagSortOptions.FirstOrDefault(o => o.Mode == appSettings.TagSortMode)
                                ?? TagSortOptions[0];
        _suppressTagSortChange = false;

        SelectedStorageOption = StorageLocationOptions.FirstOrDefault(o => o.Location == appSettings.StorageLocation)
                                ?? StorageLocationOptions[0];
        CurrentDataRootDisplay = _storagePaths.LocationDescription;
        PortableDataPathPreview = AppStoragePathResolver.ResolveDataRoot(AppStorageLocation.NextToExecutable);

        SelectedPlatformOption = PlatformOptions.FirstOrDefault(p => p.Platform == NewAccountPlatform)
                                 ?? PlatformOptions[0];

        await LoadSectionsStateAsync();
        await LoadAccountsAsync();
        await EnsureDownloadUsesActiveAccountsAsync();
        await LoadProxyAsync();
        UpdateAccountHints();
        _ = _accountValidation.WarmupAsync();
    }

    private async Task LoadSectionsStateAsync()
    {
        _suppressSectionPersistence = true;
        var state = await _settings.GetSettingsSectionsStateAsync();
        IsAppearanceSectionExpanded = state.IsExpanded(SettingsSectionIds.Appearance);
        IsStorageSectionExpanded = state.IsExpanded(SettingsSectionIds.Storage);
        IsDownloadSectionExpanded = state.IsExpanded(SettingsSectionIds.Download);
        IsTagsSectionExpanded = state.IsExpanded(SettingsSectionIds.Tags);
        IsAccountsSectionExpanded = state.IsExpanded(SettingsSectionIds.Accounts);
        IsProxySectionExpanded = state.IsExpanded(SettingsSectionIds.Proxy);
        _suppressSectionPersistence = false;
    }

    private async Task PersistSectionExpandedAsync(string sectionId, bool isExpanded)
    {
        if (_suppressSectionPersistence) return;

        var state = await _settings.GetSettingsSectionsStateAsync();
        state.SetExpanded(sectionId, isExpanded);
        await _settings.SaveSettingsSectionsStateAsync(state);
    }

    partial void OnIsAppearanceSectionExpandedChanged(bool value) =>
        _ = PersistSectionExpandedAsync(SettingsSectionIds.Appearance, value);

    partial void OnIsStorageSectionExpandedChanged(bool value) =>
        _ = PersistSectionExpandedAsync(SettingsSectionIds.Storage, value);

    partial void OnIsDownloadSectionExpandedChanged(bool value) =>
        _ = PersistSectionExpandedAsync(SettingsSectionIds.Download, value);

    partial void OnIsTagsSectionExpandedChanged(bool value) =>
        _ = PersistSectionExpandedAsync(SettingsSectionIds.Tags, value);

    partial void OnIsAccountsSectionExpandedChanged(bool value) =>
        _ = PersistSectionExpandedAsync(SettingsSectionIds.Accounts, value);

    partial void OnIsProxySectionExpandedChanged(bool value) =>
        _ = PersistSectionExpandedAsync(SettingsSectionIds.Proxy, value);

    partial void OnSelectedTagSortOptionChanged(TagSortOption? value)
    {
        if (_suppressTagSortChange || value is null)
            return;

        _ = SaveTagSortAsync(value.Mode);
    }

    private async Task SaveTagSortAsync(TagSortMode mode)
    {
        var current = await _settings.GetAppSettingsAsync();
        if (current.TagSortMode == mode)
            return;

        current.TagSortMode = mode;
        await _settings.SaveAppSettingsAsync(current);
        WeakReferenceMessenger.Default.Send(new TagSortChangedMessage());
        StatusMessage = "Сортировка тегов сохранена.";
    }

    [RelayCommand]
    private async Task SaveAppSettingsAsync()
    {
        var current = await _settings.GetAppSettingsAsync();
        current.RecentDownloadsCount = SelectedRecentCount;
        current.UsePostedDateForFolder = UsePostedDateForFolder;
        current.PreferDownloadWithoutAccount = PreferDownloadWithoutAccount;
        await _settings.SaveAppSettingsAsync(current);
        StatusMessage = UsePostedDateForFolder
            ? $"Сохранено: {SelectedRecentCount} постов, дата публикации в пути папки."
            : $"Сохранено: {SelectedRecentCount} постов, дата скачивания в пути папки.";
    }

    [RelayCommand]
    private async Task SaveStorageSettingsAsync()
    {
        var current = await _settings.GetAppSettingsAsync();
        current.StorageLocation = SelectedStorageOption?.Location ?? AppStorageLocation.DefaultLocal;
        await _settings.SaveAppSettingsAsync(current);
        StatusMessage = "Папка данных сохранена. Перезапустите приложение, чтобы база и загрузки использовали новый путь.";
    }

    private static IReadOnlyList<StorageLocationOption> CreateStorageLocationOptions()
    {
#if WINDOWS
        var defaultDescription =
            $"%LocalAppData%\\{AppStoragePathResolver.WindowsLocalParentFolder}\\{AppStoragePathResolver.WindowsLocalAppFolder}";
        var portableDescription = "Папка Data рядом с SmcManager.Maui.exe";
        return
        [
            new StorageLocationOption
            {
                Location = AppStorageLocation.DefaultLocal,
                Title = "Стандартная папка",
                Description = defaultDescription
            },
            new StorageLocationOption
            {
                Location = AppStorageLocation.NextToExecutable,
                Title = "Рядом с приложением",
                Description = portableDescription
            }
        ];
#elif ANDROID
        const string defaultDescription =
            "Внутренняя папка приложения; медиа дополнительно копируются в Pictures/SmcManager";
        return
        [
            new StorageLocationOption
            {
                Location = AppStorageLocation.DefaultLocal,
                Title = "Стандартная папка",
                Description = defaultDescription
            }
        ];
#else
        const string defaultDescription = "Системная папка данных приложения";
        const string portableDescription = "Подпапка Data рядом с файлами приложения";
        return
        [
            new StorageLocationOption
            {
                Location = AppStorageLocation.DefaultLocal,
                Title = "Стандартная папка",
                Description = defaultDescription
            },
            new StorageLocationOption
            {
                Location = AppStorageLocation.NextToExecutable,
                Title = "Рядом с приложением",
                Description = portableDescription
            }
        ];
#endif
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        var theme = IsDarkTheme ? AppColorTheme.Dark : AppColorTheme.Light;
        await _themeService.SetThemeAsync(theme);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (_suppressThemeChange) return;
        if (ToggleThemeCommand.CanExecute(null))
            ToggleThemeCommand.Execute(null);
    }

    partial void OnNewAccountPlatformChanged(SocialPlatform value) => UpdateAccountHints();

    partial void OnSelectedPlatformOptionChanged(PlatformOption? value)
    {
        if (value is null) return;
        if (NewAccountPlatform != value.Platform)
            NewAccountPlatform = value.Platform;
    }

    partial void OnIsAddAccountPanelVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(AddAccountPanelToggleText));

    [RelayCommand]
    private void ToggleAddAccountPanel() =>
        IsAddAccountPanelVisible = !IsAddAccountPanelVisible;

    [RelayCommand]
    private async Task SaveProxyAsync()
    {
        if (!int.TryParse(ProxyPortText, out var port))
            port = 8080;

        var host = ProxyHost.Trim();
        var useProxy = ProxyEnabled && !string.IsNullOrWhiteSpace(host);

        await _settings.SaveProxySettingsAsync(new ProxySettings
        {
            IsEnabled = useProxy,
            Host = host,
            Port = port,
            Username = string.IsNullOrWhiteSpace(ProxyUsername) ? null : ProxyUsername.Trim(),
            Password = string.IsNullOrWhiteSpace(ProxyPassword) ? null : ProxyPassword
        });

        StatusMessage = useProxy ? "Прокси сохранён." : "Прокси отключён (необязательно).";
    }

    [RelayCommand]
    private async Task PasteAuthFromClipboardAsync()
    {
        var text = await Clipboard.Default.GetTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
            NewAccountAuth = text.Trim();
    }

    [RelayCommand]
    private async Task LoginWithBrowserAsync()
    {
        IsAddAccountPanelVisible = true;
        StatusMessage = "Открытие окна входа…";

        try
        {
            var result = await _authService.LoginAsync(NewAccountPlatform);
            if (result is null)
            {
                StatusMessage = "Вход отменён.";
                return;
            }

            if (string.IsNullOrWhiteSpace(result.Cookies))
            {
                StatusMessage = "Cookies не получены. Попробуйте снова или вставьте cookies вручную.";
                await ShowAlertAsync("Вход не завершён", StatusMessage);
                return;
            }

            var displayName = NewAccountDisplayName.Trim();
            var username = (result.Username ?? NewAccountUsername).Trim().TrimStart('@');

            if (!result.IsSessionValidated)
            {
                var validation = await ValidateCookiesAsync(result.Cookies);
                if (validation is null) return;

                username = SocialAccountAuth.ResolveUsername(
                    NewAccountPlatform,
                    result.Cookies,
                    apiUsername: validation.Username)
                    ?? username;
            }
            else
            {
                username = SocialAccountAuth.ResolveUsername(
                    NewAccountPlatform,
                    result.Cookies,
                    apiUsername: result.Username)
                    ?? username;
            }

            if (NewAccountPlatform == SocialPlatform.Instagram
                && string.IsNullOrWhiteSpace(username)
                && !string.IsNullOrWhiteSpace(result.Cookies))
            {
                username = await InstagramSessionProbe.TryGetUsernameAsync(result.Cookies)
                           ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(username)
                && !string.IsNullOrWhiteSpace(result.Cookies))
            {
                username = SocialAccountAuth.TryParseUsernameFromCookies(NewAccountPlatform, result.Cookies)
                           ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(username))
                displayName = $"Аккаунт {SocialAccountAuth.GetPlatformTitle(NewAccountPlatform)}";
            else if (SocialAccountAuth.IsGenericDisplayName(displayName, NewAccountPlatform)
                     && !string.IsNullOrWhiteSpace(username))
            {
                displayName = string.Empty;
            }

            StatusMessage = "Сохранение аккаунта…";

            var account = await _accountService.SaveAccountAsync(new SocialAccount
            {
                Platform = NewAccountPlatform,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
                Username = username,
                Cookies = result.Cookies,
                AuthMethod = SocialAuthMethod.WebLogin,
                IsDefault = NewAccountIsDefault,
                IsActive = true,
                ConnectedAt = DateTime.UtcNow
            });

            await LoadAccountsAsync();

            if (!string.IsNullOrWhiteSpace(result.Username))
                NewAccountUsername = result.Username;

            NewAccountAuth = null;
            IsAddAccountPanelVisible = false;

            var title = account.DisplayName ?? account.Username;
            StatusMessage = $"Вход выполнен: «{title}».";
            await ShowAlertAsync("Готово", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            await ShowAlertAsync("Ошибка входа", ex.Message);
        }
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        var displayName = NewAccountDisplayName.Trim();
        var username = NewAccountUsername.Trim().TrimStart('@');

        if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(username))
        {
            await _toast.ShowWarningAsync("Укажите название или @username, чтобы отличать аккаунты в списке.");
            return;
        }

        string? cookies = string.IsNullOrWhiteSpace(NewAccountAuth) ? null : NewAccountAuth.Trim();

        if (cookies is not null)
        {
            var validation = await ValidateCookiesAsync(cookies);
            if (validation is null) return;

            if (!string.IsNullOrWhiteSpace(validation.Username))
                username = validation.Username;
        }

        var account = await _accountService.SaveAccountAsync(new SocialAccount
        {
            Platform = NewAccountPlatform,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
            Username = username,
            Cookies = cookies,
            AuthMethod = SocialAuthMethod.ManualCookies,
            IsDefault = NewAccountIsDefault,
            IsActive = true,
            ConnectedAt = DateTime.UtcNow
        });

        await LoadAccountsAsync();

        NewAccountDisplayName = string.Empty;
        NewAccountUsername = string.Empty;
        NewAccountAuth = null;
        IsAddAccountPanelVisible = false;

        StatusMessage = cookies is null
            ? $"Аккаунт «{account.DisplayName ?? account.Username}» добавлен (без cookies — только публичное)."
            : $"Аккаунт «{account.DisplayName ?? account.Username}» добавлен. Авторизация подтверждена.";
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(SocialAccountRowViewModel row)
    {
        var page = Shell.Current ?? Application.Current?.MainPage;
        if (page is null) return;

        var confirm = await page.DisplayAlertAsync(
            "Удалить аккаунт?",
            $"«{row.Title}» будет удалён из списка.",
            "Удалить",
            "Отмена");
        if (!confirm) return;

        await _accountService.DeleteAccountAsync(row.Account.Id);
        await LoadAccountsAsync();
        StatusMessage = "Аккаунт удалён.";
    }

    [RelayCommand]
    private async Task SetDefaultAccountAsync(SocialAccountRowViewModel row)
    {
        await _accountService.SetDefaultAccountAsync(row.Account.Platform, row.Account.Id);
        await LoadAccountsAsync();
        StatusMessage = $"Аккаунт «{row.Title}» — по умолчанию для {SocialAccountAuth.GetPlatformTitle(row.Account.Platform)}.";
    }

    private async Task LoadAccountsAsync()
    {
        var list = await _accountService.GetAccountsAsync();
        var changed = false;

        foreach (var account in list)
        {
            if (!SocialAccountAuth.HasAuth(account))
                continue;

            var username = account.Username;
            if (string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(account.Cookies))
            {
                username = SocialAccountAuth.TryParseUsernameFromCookies(account.Platform, account.Cookies);
            }

            if (string.IsNullOrWhiteSpace(username)
                && account.Platform == SocialPlatform.Instagram
                && !string.IsNullOrWhiteSpace(account.Cookies))
            {
                username = await InstagramSessionProbe.TryGetUsernameAsync(account.Cookies);
            }

            if (string.IsNullOrWhiteSpace(username))
                continue;

            if (string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase)
                && !SocialAccountAuth.IsGenericDisplayName(account.DisplayName, account.Platform))
            {
                continue;
            }

            account.Username = username;
            if (SocialAccountAuth.IsGenericDisplayName(account.DisplayName, account.Platform))
                account.DisplayName = null;

            await _accountService.SaveAccountAsync(account);
            changed = true;
        }

        if (changed)
            list = await _accountService.GetAccountsAsync();

        DetachAccountRowHandlers();
        AccountRows.Clear();

        foreach (var account in list)
        {
            var row = new SocialAccountRowViewModel(account);
            PropertyChangedEventHandler handler = async (_, e) =>
            {
                if (e.PropertyName == nameof(SocialAccountRowViewModel.IsActive))
                    await PersistAccountActiveAsync(row);
            };
            row.PropertyChanged += handler;
            _accountRowHandlers[row] = handler;
            AccountRows.Add(row);
        }
    }

    private void DetachAccountRowHandlers()
    {
        foreach (var (row, handler) in _accountRowHandlers)
            row.PropertyChanged -= handler;
        _accountRowHandlers.Clear();
    }

    private async Task PersistAccountActiveAsync(SocialAccountRowViewModel row)
    {
        if (row.Account.IsActive == row.IsActive)
            return;

        row.Account.IsActive = row.IsActive;
        await _accountService.SaveAccountAsync(row.Account);

        if (row.IsActive)
        {
            var settings = await _settings.GetAppSettingsAsync();
            if (settings.PreferDownloadWithoutAccount)
            {
                settings.PreferDownloadWithoutAccount = false;
                await _settings.SaveAppSettingsAsync(settings);
                PreferDownloadWithoutAccount = false;
            }
        }

        StatusMessage = row.IsActive
            ? $"Аккаунт «{row.Title}» используется для скачивания."
            : $"Аккаунт «{row.Title}» отключён для скачивания.";
    }

    private async Task EnsureDownloadUsesActiveAccountsAsync()
    {
        var accounts = await _accountService.GetAccountsAsync();
        if (!accounts.Any(a => a.IsActive && SocialAccountAuth.HasAuth(a)))
            return;

        if (!PreferDownloadWithoutAccount)
            return;

        var settings = await _settings.GetAppSettingsAsync();
        settings.PreferDownloadWithoutAccount = false;
        await _settings.SaveAppSettingsAsync(settings);
        PreferDownloadWithoutAccount = false;
    }

    private async Task LoadProxyAsync()
    {
        var proxy = await _settings.GetProxySettingsAsync();
        ProxyEnabled = proxy.IsEnabled;
        ProxyHost = proxy.Host;
        ProxyPortText = proxy.Port.ToString();
        ProxyUsername = proxy.Username;
        ProxyPassword = proxy.Password;
    }

    private void UpdateAccountHints()
    {
        AccountAuthHint = SocialAccountAuth.GetAuthHint(NewAccountPlatform);
        AccountAuthPlaceholder = SocialAccountAuth.GetAuthPlaceholder(NewAccountPlatform);
    }

    private static async Task ShowAlertAsync(string title, string message)
    {
        var page = Shell.Current ?? Application.Current?.MainPage;
        if (page is null) return;
        await page.DisplayAlertAsync(title, message, "OK");
    }

    private async Task<SocialAccountValidationResult?> ValidateCookiesAsync(string cookies)
    {
        IsValidatingAccount = true;
        StatusMessage = "Проверка авторизации…";

        try
        {
            var result = await _accountValidation.ValidateAsync(NewAccountPlatform, cookies);
            if (!result.IsValid)
            {
                await _toast.ShowWarningAsync(result.Message);
                return null;
            }

            return result;
        }
        finally
        {
            IsValidatingAccount = false;
        }
    }
}
