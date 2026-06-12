using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Maui.Messages;

namespace SmcManager.Maui.Services;

/// <summary>
/// Применяет светлую/тёмную тему и сохраняет выбор пользователя.
/// </summary>
public class ThemeService
{
    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings) => _settings = settings;

    public AppColorTheme CurrentTheme { get; private set; } = AppColorTheme.Light;

    public ThemePalette CurrentPalette => ThemePalette.For(CurrentTheme);

    public async Task InitializeAsync()
    {
        var saved = await _settings.GetColorThemeAsync();
        Apply(saved);
    }

    public async Task SetThemeAsync(AppColorTheme theme)
    {
        Apply(theme);
        await _settings.SaveColorThemeAsync(theme);
    }

    public void Apply(AppColorTheme theme)
    {
        CurrentTheme = theme;
        var app = Application.Current;
        if (app is null) return;

        app.UserAppTheme = theme == AppColorTheme.Dark ? AppTheme.Dark : AppTheme.Light;

        var palette = ThemePalette.For(theme);
        ApplyPalette(app.Resources, palette);

        if (app.Windows.Count > 0 && app.Windows[0].Page is AppShell shell)
        {
            shell.BackgroundColor = palette.BackgroundPrimary;
            shell.FlyoutBackgroundColor = palette.BackgroundSecondary;
            Shell.SetForegroundColor(shell, palette.TextPrimary);
            Shell.SetTitleColor(shell, palette.TextPrimary);
            shell.FlyoutIsPresented = false;
            ApplyFlyoutIcons(shell, palette);
        }

        AppBranding.ApplyWindowTitles();
#if WINDOWS
        foreach (var window in app.Windows)
            Platforms.Windows.WindowsWindowBranding.Apply(window, palette, theme);
#endif

        WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(palette));
    }

    private static void ApplyPalette(ResourceDictionary resources, ThemePalette palette)
    {
        resources["BackgroundPrimary"] = palette.BackgroundPrimary;
        resources["BackgroundSecondary"] = palette.BackgroundSecondary;
        resources["BackgroundElevated"] = palette.BackgroundElevated;
        resources["TextPrimary"] = palette.TextPrimary;
        resources["TextSecondary"] = palette.TextSecondary;
        resources["TextMuted"] = palette.TextMuted;
        resources["Divider"] = palette.Divider;
        resources["AccentPrimary"] = palette.AccentPrimary;
        resources["AccentSecondary"] = palette.AccentSecondary;
        resources["DownloadAccent"] = palette.AccentPrimary;
        resources["AccentGradientEnd"] = palette.AccentSecondary;
        resources["Primary"] = palette.AccentPrimary;
        resources["Secondary"] = palette.AccentSecondary;
        resources["Danger"] = palette.Danger;
        resources["TagChipBackground"] = palette.TagChipBackground;
        resources["TagChipFill"] = palette.TagChipFill;
        resources["TagChipStroke"] = palette.TagChipStroke;
        resources["TagChipText"] = palette.TagChipText;
        resources["TagChipSelectedFill"] = palette.TagChipSelectedFill;
        resources["TagChipSelectedText"] = palette.TagChipSelectedText;
        resources["MenuIcon"] = palette.MenuIcon;
        resources["CarouselPrevIcon"] = palette.CarouselPrevIcon;
        resources["CarouselNextIcon"] = palette.CarouselNextIcon;
        resources["MediaExpandIcon"] = palette.MediaExpandIcon;
        resources["MediaCollapseIcon"] = palette.MediaCollapseIcon;
        resources["EditIcon"] = palette.EditIcon;
        resources["DeleteIcon"] = palette.DeleteIcon;
        resources["PasteIcon"] = palette.PasteIcon;
        resources["DownloadButtonIcon"] = palette.DownloadButtonIcon;
        resources["AddTagIcon"] = palette.AddTagIcon;
        resources["SaveTagIcon"] = palette.SaveTagIcon;
        resources["ExplorerIcon"] = palette.ExplorerIcon;
        resources["FolderIcon"] = palette.FolderIcon;
        resources["OpenSourceIcon"] = palette.OpenSourceIcon;
        resources["ShareIcon"] = palette.ShareIcon;

        resources["OffBlack"] = palette.BackgroundPrimary;
        resources["White"] = Color.FromArgb("#FFFFFF");
        resources["Black"] = Color.FromArgb("#000000");
    }

    public void ApplyFlyoutIcons(Shell shell) =>
        ApplyFlyoutIcons(shell, CurrentPalette);

    public static void ApplyFlyoutIcons(Shell shell, ThemePalette palette)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var item in shell.Items)
            {
                if (item is not FlyoutItem flyoutItem)
                    continue;

                var icon = ResolveFlyoutIcon(flyoutItem.Route, palette);
                var source = ThemedIconHelper.FromFile(icon);
                flyoutItem.Icon = null;
                flyoutItem.Icon = source;
                flyoutItem.FlyoutIcon = source;

                foreach (var section in flyoutItem.Items)
                {
                    foreach (var element in section.Items)
                    {
                        if (element is ShellContent shellContent)
                            shellContent.Icon = source;
                    }
                }
            }
        });
    }

    public static string ResolveFlyoutIcon(string? route, ThemePalette palette) =>
        route switch
        {
            "download" => palette.TabDownloadIcon,
            "library" => palette.TabLibraryIcon,
            "groups" => palette.TabGroupsIcon,
            "tags" => palette.TabTagsIcon,
            "settings" => palette.TabSettingsIcon,
            _ => palette.TabDownloadIcon
        };
}
