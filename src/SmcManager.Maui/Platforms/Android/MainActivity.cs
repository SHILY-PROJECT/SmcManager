using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using AndroidX.Core.View;
using Microsoft.Maui.Storage;
using SmcManager.Maui.Services;

namespace SmcManager.Maui;

/// <summary>
/// Точка входа Android: обработка Share intent с ссылкой.
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "text/plain")]
public class MainActivity : MauiAppCompatActivity
{
    private string? _deferredShareText;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Window is not null)
        {
            // Edge-to-edge + SafeAreaEdges на страницах; единая конфигурация окна снижает «плавающие» сбои отступов.
            WindowCompat.SetDecorFitsSystemWindows(Window, false);
        }

        OnBackPressedDispatcher.AddCallback(this, new BackNavigationCallback(this));
        HandleShareIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is null)
            return;

        Intent = intent;
        HandleShareIntent(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        ProcessDeferredShareText();
    }

    private void HandleShareIntent(Intent? intent)
    {
        var sharedText = AndroidShareIntentReader.ReadText(intent);
        if (string.IsNullOrWhiteSpace(sharedText))
            return;

        if (!TryHandleShareText(sharedText))
            _deferredShareText = sharedText;
    }

    private void ProcessDeferredShareText()
    {
        if (string.IsNullOrWhiteSpace(_deferredShareText))
            return;

        var text = _deferredShareText;
        _deferredShareText = null;

        if (!TryHandleShareText(text))
            _deferredShareText = text;
    }

    private bool TryHandleShareText(string sharedText)
    {
        var shareService = ResolveShareService();
        if (shareService is null)
        {
            if (!ShareLinkService.TryNormalizeIncomingUrl(sharedText, out var normalized))
                return false;

            Preferences.Default.Set(MauiSettingsService.PendingShareUrlPreferenceKey, normalized);
            return true;
        }

        _ = shareService.HandleIncomingUrlAsync(sharedText);
        return true;
    }

    private static ShareLinkService? ResolveShareService() =>
        IPlatformApplication.Current?.Services?.GetService<ShareLinkService>();

    private sealed class BackNavigationCallback : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public BackNavigationCallback(MainActivity activity) : base(true) =>
            _activity = activity;

        public override void HandleOnBackPressed()
        {
            if (ShellBackNavigation.TryGoBack())
                return;

            Enabled = false;
            _activity.OnBackPressedDispatcher.OnBackPressed();
            Enabled = true;
        }
    }
}
