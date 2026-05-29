namespace SmcManager.Maui.Services;

/// <summary>
/// Отображаемое имя приложения.
/// </summary>
public static class AppBranding
{
    public const string FullTitle = "SmcManager - от меня щас что требуется?";

    public const string ProductName = "SocialMediaContentManager";

    public const string Tagline = "- от меня ща чё требуется?!";

    public const string Subtitle = "Скачивание и архив";

    public const string AppLogoImage = "app_logo.png";

    public static void ApplyWindowTitles()
    {
        var app = Application.Current;
        if (app is null)
            return;

        foreach (var window in app.Windows)
            window.Title = FullTitle;
    }
}
