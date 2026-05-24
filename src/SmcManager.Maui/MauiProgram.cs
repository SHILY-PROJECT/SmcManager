using CommunityToolkit.Maui;
using Serilog;
using SmcManager.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmcManager.Infrastructure;
using SmcManager.Infrastructure.Services;
using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;
using SmcManager.Maui.Views;

namespace SmcManager.Maui;

/// <summary>
/// Точка входа MAUI: DI, шрифты, регистрация ViewModels и страниц.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping(
                    "DisableAutoLink",
                    (handler, _) =>
                    {
                        if (handler.PlatformView is Android.Widget.TextView textView)
                        {
                            textView.LinksClickable = false;
                            textView.AutoLinkMask = 0;
                        }
                    });
#endif
            });

        var appSettings = MauiSettingsService.ReadAppSettingsSnapshot();
        var storagePaths = new AppStoragePaths(appSettings.StorageLocation);

        builder.Services.AddSingleton<IAppStoragePaths>(storagePaths);
        builder.Services.AddSingleton<ISettingsService, MauiSettingsService>();
        builder.Services.AddSmcInfrastructure();
#if ANDROID
        builder.Services.AddSingleton<IMediaStorageService>(sp =>
        {
            var paths = sp.GetRequiredService<IAppStoragePaths>();
            return new Platforms.Android.AndroidMirroringMediaStorageService(
                paths.DownloadsPath,
                sp.GetRequiredService<VideoThumbnailService>());
        });
#endif
        builder.Services.AddSingleton<ShareLinkService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<IFileExplorerService, FileExplorerService>();
        builder.Services.AddSingleton<ILinkLauncherService, LinkLauncherService>();
        builder.Services.AddSingleton<TagCreationService>();
        builder.Services.AddSingleton<TagColorPickerService>();
        builder.Services.AddSingleton<BottomToastService>();
        builder.Services.AddSingleton<RemoteImageCache>();
        builder.Services.AddSingleton<IWebCookieExtractor, WebCookieExtractor>();
        builder.Services.AddSingleton<ISocialAuthService, MauiSocialAuthService>();

        builder.Services.AddTransient<DownloadViewModel>();
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<GroupsViewModel>();
        builder.Services.AddTransient<TagsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<ContentDetailViewModel>();
        builder.Services.AddTransient<SocialLoginViewModel>();
        builder.Services.AddTransient<TagColorPickerViewModel>();

        builder.Services.AddTransient<DownloadPage>();
        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<GroupsPage>();
        builder.Services.AddTransient<TagsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ContentDetailPage>();
        builder.Services.AddTransient<SocialLoginPage>();
        builder.Services.AddTransient<TagColorPickerPage>();

        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        var logsDirectory = Path.Combine(FileSystem.AppDataDirectory, "logs");
        SerilogConfigurator.Configure(builder, logsDirectory);
#else
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

#if ANDROID
        Platforms.Android.AndroidLegacyStorageMigration.TryImportFromPublicPictures();
#endif

#if DEBUG
        Log.Information(
            "App started. DataRoot={DataRoot}, Downloads={Downloads}",
            storagePaths.DataRoot,
            storagePaths.DownloadsPath);
#endif

        return app;
    }
}
