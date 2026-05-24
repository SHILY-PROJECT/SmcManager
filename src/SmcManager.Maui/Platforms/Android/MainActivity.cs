using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
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
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OnBackPressedDispatcher.AddCallback(this, new BackNavigationCallback(this));
        HandleShareIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleShareIntent(intent);
    }

    private void HandleShareIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend) return;
        if (intent.Type != "text/plain") return;

        var sharedText = intent.GetStringExtra(Intent.ExtraText);
        if (string.IsNullOrWhiteSpace(sharedText)) return;

        var services = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
        var shareService = services?.GetService<ShareLinkService>();
        if (shareService is not null)
            _ = shareService.HandleIncomingUrlAsync(sharedText);
    }

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
