using SmcManager.Core.Enums;

namespace SmcManager.Maui.Services;

/// <summary>
/// Извлечение cookies из WebView после входа.
/// </summary>
public interface IWebCookieExtractor
{
    Task<string?> ExtractCookiesAsync(
        WebView webView,
        SocialPlatform platform,
        CancellationToken cancellationToken = default);
}
