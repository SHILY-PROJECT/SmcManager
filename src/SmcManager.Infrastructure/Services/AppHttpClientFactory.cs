using System.Net;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// HttpClient с прокси и cookies подключённого аккаунта.
/// </summary>
public class AppHttpClientFactory : IAppHttpClientFactory
{
    private readonly ISettingsService _settings;

    public AppHttpClientFactory(ISettingsService settings) => _settings = settings;

    public HttpClient CreateClient(SocialAccount? account = null)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var proxySettings = _settings.GetProxySettingsAsync().GetAwaiter().GetResult();
        if (proxySettings.IsEnabled && !string.IsNullOrWhiteSpace(proxySettings.Host))
        {
            var proxy = new WebProxy(proxySettings.Host, proxySettings.Port);
            if (!string.IsNullOrEmpty(proxySettings.Username))
                proxy.Credentials = new NetworkCredential(proxySettings.Username, proxySettings.Password);
            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(3) };
        var isInstagram = account?.Platform == Core.Enums.SocialPlatform.Instagram;
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            isInstagram
                ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                : "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

        if (account is not null)
        {
            var cookieHeader = SocialAccountAuth.BuildCookieHeader(account);
            if (!string.IsNullOrEmpty(cookieHeader))
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        return client;
    }
}
