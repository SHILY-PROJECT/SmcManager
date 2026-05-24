using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Создание HttpClient с сессией выбранного или дефолтного аккаунта.
/// </summary>
internal static class DownloaderAuth
{
    public static async Task<(SocialAccount? Account, HttpClient Client)> CreateClientAsync(
        IAppHttpClientFactory httpFactory,
        ISocialAccountService accountService,
        SocialPlatform platform,
        int? accountId,
        CancellationToken cancellationToken,
        bool useDefaultWhenUnspecified = true,
        Action<HttpClient>? configureClient = null)
    {
        var account = await accountService.ResolveForDownloadAsync(
            platform, accountId, useDefaultWhenUnspecified, cancellationToken);
        var client = httpFactory.CreateClient(account);
        configureClient?.Invoke(client);
        return (account, client);
    }
}
