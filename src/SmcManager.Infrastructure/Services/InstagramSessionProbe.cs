using SmcManager.Core.Enums;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Проверка Instagram-сессии и получение username через API (в т.ч. Android WebView).
/// </summary>
public static class InstagramSessionProbe
{
    public static async Task<string?> TryGetUsernameAsync(
        string normalizedCookies,
        string? webPageUrl = null,
        CancellationToken cancellationToken = default)
    {
        var fromCookies = SocialAccountAuth.ResolveUsername(
            SocialPlatform.Instagram,
            normalizedCookies,
            webPageUrl);

        if (!string.IsNullOrWhiteSpace(fromCookies))
            return fromCookies;

        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };

            SocialAccountAuth.ApplyInstagramApiHeaders(client.DefaultRequestHeaders, normalizedCookies);

            var apiUrls = new[]
            {
                "https://www.instagram.com/api/v1/accounts/current_user/?edit=true",
                "https://www.instagram.com/api/v1/web/accounts/current_user/"
            };

            foreach (var apiUrl in apiUrls)
            {
                using var response = await client.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (body.Contains("\"status\":\"fail\"", StringComparison.OrdinalIgnoreCase)
                    || body.Contains("login_required", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var username = SocialAccountAuth.ResolveUsername(
                    SocialPlatform.Instagram,
                    normalizedCookies,
                    webPageUrl,
                    SocialAccountAuth.TryParseUsernameFromInstagramApiBody(body));

                if (!string.IsNullOrWhiteSpace(username))
                    return username;
            }
        }
        catch
        {
            // ignore — fallback already tried
        }

        return null;
    }
}
