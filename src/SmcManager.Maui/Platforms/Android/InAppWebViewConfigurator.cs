using Android.Webkit;

namespace SmcManager.Maui.Platforms.Android;

/// <summary>
/// Держит http(s)-навигацию внутри WebView (логин Instagram/YouTube/VK).
/// </summary>
internal static class InAppWebViewConfigurator
{
    public static void Configure(Microsoft.Maui.Handlers.IWebViewHandler handler)
    {
        if (handler.PlatformView is not global::Android.Webkit.WebView webView)
            return;
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.DomStorageEnabled = true;

        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
            CookieManager.Instance?.SetAcceptThirdPartyCookies(webView, true);

        webView.SetWebViewClient(new InAppWebViewClient());
    }

    private sealed class InAppWebViewClient : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
        {
            if (view is null || request?.Url is null)
                return false;

            var url = request.Url.ToString() ?? string.Empty;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;

            return base.ShouldOverrideUrlLoading(view, request);
        }
    }
}
