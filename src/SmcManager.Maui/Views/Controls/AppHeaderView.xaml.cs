using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Enums;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Services;
using SmcManager.Maui.Views;

namespace SmcManager.Maui.Views.Controls;

/// <summary>
/// Верхняя панель: кнопка меню и заголовок страницы.
/// </summary>
public partial class AppHeaderView : ContentView,
    IRecipient<ThemeChangedMessage>,
    IRecipient<AppHeaderModeChangedMessage>
{
    private bool _isNavigating;
    private bool _isBackMode;
    private ThemePalette? _lastPalette;

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(AppHeaderView), string.Empty);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(AppHeaderView), string.Empty);

    public static readonly BindableProperty ShowTitleProperty = BindableProperty.Create(
        nameof(ShowTitle), typeof(bool), typeof(AppHeaderView), true);

    public static readonly BindableProperty ShowSubtitleProperty = BindableProperty.Create(
        nameof(ShowSubtitle), typeof(bool), typeof(AppHeaderView), false);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool ShowTitle
    {
        get => (bool)GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    public bool ShowSubtitle
    {
        get => (bool)GetValue(ShowSubtitleProperty);
        set => SetValue(ShowSubtitleProperty, value);
    }

    public AppHeaderView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshHeaderActionMode();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is null)
        {
            WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<AppHeaderModeChangedMessage>(this);
            return;
        }

        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<AppHeaderModeChangedMessage>(this);
        RefreshHeaderActionMode();
        ApplyIcons(_lastPalette ?? ThemePalette.For(GetCurrentTheme()));
    }

    public void Receive(ThemeChangedMessage message)
    {
        if (Parent is null || Handler is null || SettingsButton.Handler is null)
            return;

        ApplyIcons(message.Palette);
    }

    public void Receive(AppHeaderModeChangedMessage message)
    {
        if (!IsHeaderOnCurrentPage())
            return;

        Dispatcher.Dispatch(RefreshHeaderActionMode);
    }

    private void RefreshHeaderActionMode()
    {
        if (!CanUpdateChrome())
            return;

        var backMode = AppNavigationState.IsSettingsVisible;
        if (_isBackMode == backMode)
            return;

        _isBackMode = backMode;
        SemanticProperties.SetDescription(
            SettingsButton,
            _isBackMode ? "Назад" : "Настройки");

        if (_lastPalette is not null)
            ApplyIcons(_lastPalette);
    }

    private bool CanUpdateChrome() =>
        Parent is not null &&
        Window is not null &&
        Handler is not null &&
        SettingsButton.Handler is not null &&
        IsHeaderOnCurrentPage();

    private bool IsHeaderOnCurrentPage()
    {
        var hostPage = FindHostPage();
        if (hostPage is null || Shell.Current?.CurrentPage is not Page currentPage)
            return false;

        return ReferenceEquals(hostPage, currentPage);
    }

    private Page? FindHostPage()
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is Page page)
                return page;
        }

        return null;
    }

    private void ApplyIcons(ThemePalette palette)
    {
        _lastPalette = palette;
        ThemedIconHelper.SetSource(
            SettingsButton,
            _isBackMode ? palette.HeaderBackIcon : palette.TabSettingsIcon);
    }

    private static AppColorTheme GetCurrentTheme()
    {
        var app = Application.Current;
        if (app is null) return AppColorTheme.Light;
        return app.UserAppTheme == AppTheme.Dark ? AppColorTheme.Dark : AppColorTheme.Light;
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is Shell shell)
            shell.FlyoutIsPresented = !shell.FlyoutIsPresented;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (_isNavigating || Shell.Current is null)
            return;

        if (AppNavigationState.IsSettingsVisible || _isBackMode)
        {
            ShellBackNavigation.TryGoBack();
            return;
        }

        _isNavigating = true;
        try
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
