using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Infrastructure.Data;
using SmcManager.Infrastructure.Download;
using SmcManager.Infrastructure.Services;

namespace SmcManager.Infrastructure;

/// <summary>
/// Регистрация сервисов Infrastructure в DI-контейнере.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSmcInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((sp, options) =>
            {
                var paths = sp.GetRequiredService<IAppStoragePaths>();
                options.UseSqlite($"Data Source={paths.DatabasePath}");
            },
            contextLifetime: ServiceLifetime.Singleton,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddSingleton<IContentRepository>(sp =>
        {
            var paths = sp.GetRequiredService<IAppStoragePaths>();
            return new ContentRepository(sp.GetRequiredService<AppDbContext>(), paths.DownloadsPath);
        });
        services.AddSingleton<VideoThumbnailService>();
        services.AddSingleton<IMediaStorageService>(sp =>
        {
            var paths = sp.GetRequiredService<IAppStoragePaths>();
            return new MediaStorageService(paths.DownloadsPath, sp.GetRequiredService<VideoThumbnailService>());
        });
        services.AddSingleton<IAppHttpClientFactory, AppHttpClientFactory>();
        services.AddSingleton<ISocialAccountService, SocialAccountService>();
        services.AddSingleton<YtdlpHostService>();
        services.AddSingleton<IDownloadQualityService, DownloadQualityService>();
        services.AddSingleton<ILinkMetadataService, LinkMetadataService>();
        services.AddSingleton<ISocialAccountValidationService, SocialAccountValidationService>();
        services.AddSingleton<IDownloadOrchestrator, DownloadOrchestrator>();

        services.AddSingleton<IContentDownloader, InstagramDownloader>();
        services.AddSingleton<IContentDownloader, YouTubeDownloader>();
        services.AddSingleton<IContentDownloader, VkDownloader>();

        return services;
    }
}
