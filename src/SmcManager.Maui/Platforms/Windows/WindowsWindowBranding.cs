using Microsoft.Maui.Graphics;
using SmcManager.Core.Enums;
using SmcManager.Maui.Services;
using WinUIColor = Windows.UI.Color;

namespace SmcManager.Maui.Platforms.Windows;

/// <summary>
/// Заголовок и цвета title bar Windows под текущую тему.
/// </summary>
public static class WindowsWindowBranding
{
    public static void Apply(Window window, ThemePalette palette, AppColorTheme theme)
    {
        window.Title = AppBranding.FullTitle;

        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
            return;

        nativeWindow.Title = AppBranding.FullTitle;

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        if (appWindow is null)
            return;

        appWindow.Title = AppBranding.FullTitle;

        if (!Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            return;

        var titleBar = appWindow.TitleBar;
        var background = ToWinUiColor(palette.BackgroundPrimary);
        var caption = ResolveCaptionColor(palette, theme);
        var hover = ToWinUiColor(palette.BackgroundSecondary);
        var pressed = ToWinUiColor(palette.BackgroundElevated);

        titleBar.BackgroundColor = background;
        titleBar.InactiveBackgroundColor = background;

        titleBar.ForegroundColor = caption;
        titleBar.InactiveForegroundColor = caption;

        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonPressedBackgroundColor = pressed;

        titleBar.ButtonForegroundColor = caption;
        titleBar.ButtonInactiveForegroundColor = caption;
    }

    /// <summary>
    /// Тёмная тема: белый или розовый текст на чёрной панели. Светлая: тёмный текст.
    /// </summary>
    private static WinUIColor ResolveCaptionColor(ThemePalette palette, AppColorTheme theme) =>
        theme == AppColorTheme.Dark
            ? ToWinUiColor(palette.AccentSecondary)
            : ToWinUiColor(palette.TextPrimary);

    private static WinUIColor ToWinUiColor(Color color)
    {
        static byte Channel(float value) =>
            value switch
            {
                > 1f => (byte)Math.Clamp(value, 0, 255),
                _ => (byte)Math.Clamp(value * 255f, 0, 255)
            };

        return WinUIColor.FromArgb(
            Channel(color.Alpha),
            Channel(color.Red),
            Channel(color.Green),
            Channel(color.Blue));
    }
}
