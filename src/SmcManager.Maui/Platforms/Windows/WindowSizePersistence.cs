using SmcManager.Core.Enums;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.Platforms.Windows;

/// <summary>
/// Сохраняет и восстанавливает размер окна приложения на Windows.
/// </summary>
public static class WindowSizePersistence
{
    private const string WidthKey = "windows_window_width";
    private const string HeightKey = "windows_window_height";
    private const double MinWidth = 520;
    private const double MinHeight = 640;
    private const double DefaultWidth = 1100;
    private const double DefaultHeight = 800;

    public static void Attach(Window window)
    {
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler is null)
                return;

            var themeService = Application.Current?.Handler?.MauiContext?.Services
                .GetService<ThemeService>();
            var palette = themeService?.CurrentPalette ?? ThemePalette.Light;
            var theme = themeService?.CurrentTheme ?? AppColorTheme.Light;
            WindowsWindowBranding.Apply(window, palette, theme);
        };

        Restore(window);

        window.SizeChanged += (_, _) => Save(window);
        window.Destroying += (_, _) => Save(window);
    }

    private static void Restore(Window window)
    {
        var width = Preferences.Default.Get(WidthKey, 0.0);
        var height = Preferences.Default.Get(HeightKey, 0.0);

        window.Width = width >= MinWidth ? width : DefaultWidth;
        window.Height = height >= MinHeight ? height : DefaultHeight;
    }

    private static void Save(Window window)
    {
        if (window.Width < MinWidth || window.Height < MinHeight)
            return;

        Preferences.Default.Set(WidthKey, window.Width);
        Preferences.Default.Set(HeightKey, window.Height);
    }
}
