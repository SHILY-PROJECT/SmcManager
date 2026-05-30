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
    private bool _isResumed;

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
        _isResumed = true;
        ProcessDeferredShareText();
    }

    protected override void OnPause()
    {
        _isResumed = false;
        base.OnPause();
    }

    private void HandleShareIntent(Intent? intent)
    {
        var sharedText = AndroidShareIntentReader.ReadText(intent);
        if (string.IsNullOrWhiteSpace(sharedText))
            return;

        _deferredShareText = sharedText;

        if (_isResumed)
            ProcessDeferredShareText();
    }

    private void ProcessDeferredShareText()
    {
        if (string.IsNullOrWhiteSpace(_deferredShareText))
            return;

        var text = _deferredShareText;
        _deferredShareText = null;

        if (!ShareLinkService.TryExtractNormalizedUrl(text, out var normalized))
            return;

        var shareService = ResolveShareService();
        if (shareService is null)
        {
            Preferences.Default.Set(MauiSettingsService.PendingShareUrlPreferenceKey, normalized);
            ClearShareIntent();
            return;
        }

        _ = shareService.HandleIncomingUrlAsync(text);
        ClearShareIntent();
    }

    private void ClearShareIntent()
    {
        if (Intent?.Action != Intent.ActionSend)
            return;

        Intent = new Intent(Intent.ActionMain);
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
