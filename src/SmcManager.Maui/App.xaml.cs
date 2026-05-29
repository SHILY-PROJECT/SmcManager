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

        _ = Task.Run(async () =>
        {
            try
            {
                var settings = _services.GetRequiredService<ISettingsService>();
                var pending = await settings.GetPendingShareUrlAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(pending))
                    return;

                var share = _services.GetRequiredService<ShareLinkService>();
                await share.EnsureDownloadTabAsync().ConfigureAwait(false);
            }
            catch
            {
                // cold-start navigation is best-effort
            }
        });

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
