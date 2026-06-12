using System.Text;
using SmcManager.Core.Enums;
using SmcManager.Core.Services;

namespace SmcManager.Maui.Services;

/// <summary>
/// Чтение cookies из WebView (Windows WebView2 / Android CookieManager).
/// </summary>
public partial class WebCookieExtractor : IWebCookieExtractor
{
    public Task<string?> ExtractCookiesAsync(
        WebView webView,
        SocialPlatform platform,
        CancellationToken cancellationToken = default)
    {
        if (MainThread.IsMainThread)
            return ExtractPlatformCookiesAsync(webView, platform, cancellationToken);

        return MainThread.InvokeOnMainThreadAsync(() =>
            ExtractPlatformCookiesAsync(webView, platform, cancellationToken));
    }

    private static string MergeCookieParts(string raw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;

            var name = part[..eq].Trim();
            if (!seen.Add(name)) continue;

            if (builder.Length > 0) builder.Append("; ");
            builder.Append(name).Append('=').Append(part[(eq + 1)..].Trim());
        }

        return builder.Length == 0 ? string.Empty : builder.ToString();
    }

    private static async Task<string?> ExtractPlatformCookiesAsync(
        WebView webView,
        SocialPlatform platform,
        CancellationToken cancellationToken)
    {
        if (webView.Handler is null)
            await WaitForHandlerAsync(webView, cancellationToken).ConfigureAwait(false);

#if WINDOWS
        return await ExtractWindowsAsync(webView, platform, cancellationToken).ConfigureAwait(false);
#elif ANDROID
        return await ExtractAndroidAsync(webView, platform, cancellationToken).ConfigureAwait(false);
#else
        return null;
#endif
    }

    private static async Task WaitForHandlerAsync(WebView webView, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 40; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (webView.Handler is not null) return;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

#if WINDOWS
    private static async Task<string?> ExtractWindowsAsync(
        WebView webView,
        SocialPlatform platform,
        CancellationToken cancellationToken)
    {
        var webView2 = ResolveWebView2(webView);
        if (webView2 is null) return null;

        await webView2.EnsureCoreWebView2Async();
        cancellationToken.ThrowIfCancellationRequested();

        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in SocialLoginConfig.GetCookieUrls(platform))
            urls.Add(url);

        if (webView2.Source is { } source && !string.IsNullOrWhiteSpace(source.AbsoluteUri))
            urls.Add(source.AbsoluteUri);

        var parts = new List<string>();
        foreach (var url in urls)
        {
            var cookies = await webView2.CoreWebView2.CookieManager.GetCookiesAsync(url);
            foreach (var cookie in cookies)
                parts.Add($"{cookie.Name}={cookie.Value}");
        }

        if (parts.Count == 0)
        {
            var docCookies = await TryGetDocumentCookiesAsync(webView2);
            if (!string.IsNullOrWhiteSpace(docCookies))
                parts.Add(docCookies);
        }

        var merged = parts.Count == 0 ? null : string.Join("; ", parts);
        return string.IsNullOrWhiteSpace(merged) ? null : MergeCookieParts(merged);
    }

    private static Microsoft.UI.Xaml.Controls.WebView2? ResolveWebView2(WebView webView)
    {
        var platformView = webView.Handler?.PlatformView;
        if (platformView is Microsoft.UI.Xaml.Controls.WebView2 direct)
            return direct;

        var type = platformView?.GetType();
        if (type is null) return null;

        var platformViewProp = type.GetProperty("PlatformView");
        if (platformViewProp?.GetValue(platformView) is Microsoft.UI.Xaml.Controls.WebView2 nested)
            return nested;

        var coreProp = type.GetProperty("CoreWebView2");
        if (coreProp?.GetValue(platformView) is not null && platformView is Microsoft.UI.Xaml.Controls.WebView2 wv)
            return wv;

        return null;
    }

    private static async Task<string?> TryGetDocumentCookiesAsync(
        Microsoft.UI.Xaml.Controls.WebView2 webView2)
    {
        try
        {
            var json = await webView2.CoreWebView2.ExecuteScriptAsync("document.cookie");
            if (string.IsNullOrWhiteSpace(json) || json == "null" || json == "\"\"")
                return null;

            return json.Trim('"').Replace("\\u0026", "&", StringComparison.Ordinal);
        }
        catch
        {
            return null;
        }
    }
#endif

#if ANDROID
    private static async Task<string?> ExtractAndroidAsync(
        WebView webView,
        SocialPlatform platform,
        CancellationToken cancellationToken)
    {
        var manager = Android.Webkit.CookieManager.Instance;
        if (manager is null) return null;

        manager.SetAcceptCookie(true);
        manager.Flush();
        var parts = new List<string>();

        foreach (var url in SocialLoginConfig.GetCookieUrls(platform))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cookie = manager.GetCookie(url);
            if (!string.IsNullOrWhiteSpace(cookie))
                parts.Add(cookie);
        }

        string? pageUrl = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
            pageUrl = webView.Source?.ToString()).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(pageUrl))
        {
            var pageCookie = manager.GetCookie(pageUrl);
            if (!string.IsNullOrWhiteSpace(pageCookie))
                parts.Add(pageCookie);
        }

        string? documentCookies = null;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                documentCookies = await webView.EvaluateJavaScriptAsync("document.cookie");
            }
            catch
            {
                documentCookies = null;
            }
        }).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(documentCookies))
        {
            var unquoted = documentCookies.Trim().Trim('"').Replace("\\u0026", "&", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(unquoted))
                parts.Add(unquoted);
        }

        var merged = parts.Count == 0 ? null : string.Join("; ", parts);
        return string.IsNullOrWhiteSpace(merged) ? null : MergeCookieParts(merged);
    }
#endif
}
