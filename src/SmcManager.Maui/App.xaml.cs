using SmcManager.Core.Interfaces;
using SmcManager.Maui.Services;

namespace SmcManager.Maui;

/// <summary>
/// Инициализация БД при старте приложения.
/// </summary>
public partial class App : Application
{
    private readonly IContentRepository _repository;
    private readonly IServiceProvider _services;

    public App(IContentRepository repository, IServiceProvider services)
    {
        _repository = repository;
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var themeService = _services.GetRequiredService<ThemeService>();
        themeService.InitializeAsync().GetAwaiter().GetResult();

        _repository.InitializeAsync().GetAwaiter().GetResult();

        // AppShell создаём после InitializeComponent(), иначе StaticResource из Colors.xaml ещё недоступны
        var shell = _services.GetRequiredService<AppShell>();
        themeService.Apply(themeService.CurrentTheme);

        var window = new Window(shell);
        AppBranding.ApplyWindowTitles();

#if WINDOWS
        Platforms.Windows.WindowsWindowBranding.Apply(
            window,
            themeService.CurrentPalette,
            themeService.CurrentTheme);
        Platforms.Windows.WindowSizePersistence.Attach(window);
#endif

        return window;
    }
}
