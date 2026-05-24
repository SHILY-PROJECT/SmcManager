using System.Text.Json;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;

namespace SmcManager.Maui.Services;

/// <summary>
/// Настройки приложения через MAUI Preferences.
/// </summary>
public class MauiSettingsService : ISettingsService
{
    private const string ProxyKey = "proxy_settings";
    private const string PendingUrlKey = "pending_share_url";
    private const string ColorThemeKey = "color_theme";
    private const string AppSettingsKey = "app_user_settings";
    private const string SettingsSectionsKey = "settings_sections_state";

    public Task<ProxySettings> GetProxySettingsAsync()
    {
        var json = Preferences.Default.Get(ProxyKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return Task.FromResult(new ProxySettings());

        return Task.FromResult(JsonSerializer.Deserialize<ProxySettings>(json) ?? new ProxySettings());
    }

    public Task SaveProxySettingsAsync(ProxySettings settings)
    {
        Preferences.Default.Set(ProxyKey, JsonSerializer.Serialize(settings));
        return Task.CompletedTask;
    }

    public Task<string?> GetPendingShareUrlAsync() =>
        Task.FromResult(Preferences.Default.Get(PendingUrlKey, null as string));

    public Task SetPendingShareUrlAsync(string? url)
    {
        if (url is null)
            Preferences.Default.Remove(PendingUrlKey);
        else
            Preferences.Default.Set(PendingUrlKey, url);
        return Task.CompletedTask;
    }

    public Task<AppColorTheme> GetColorThemeAsync()
    {
        var value = Preferences.Default.Get(ColorThemeKey, (int)AppColorTheme.Light);
        return Task.FromResult(Enum.IsDefined(typeof(AppColorTheme), value)
            ? (AppColorTheme)value
            : AppColorTheme.Light);
    }

    public Task SaveColorThemeAsync(AppColorTheme theme)
    {
        Preferences.Default.Set(ColorThemeKey, (int)theme);
        return Task.CompletedTask;
    }

    public Task<AppUserSettings> GetAppSettingsAsync()
    {
        var json = Preferences.Default.Get(AppSettingsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return Task.FromResult(new AppUserSettings());

        var settings = JsonSerializer.Deserialize<AppUserSettings>(json) ?? new AppUserSettings();
        settings.Normalize();
        return Task.FromResult(settings);
    }

    public Task SaveAppSettingsAsync(AppUserSettings settings)
    {
        settings.Normalize();
        Preferences.Default.Set(AppSettingsKey, JsonSerializer.Serialize(settings));
        return Task.CompletedTask;
    }

    public Task<SettingsSectionsState> GetSettingsSectionsStateAsync()
    {
        var json = Preferences.Default.Get(SettingsSectionsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return Task.FromResult(new SettingsSectionsState());

        return Task.FromResult(
            JsonSerializer.Deserialize<SettingsSectionsState>(json) ?? new SettingsSectionsState());
    }

    public Task SaveSettingsSectionsStateAsync(SettingsSectionsState state)
    {
        Preferences.Default.Set(SettingsSectionsKey, JsonSerializer.Serialize(state));
        return Task.CompletedTask;
    }

    /// <summary>Читает настройки из Preferences без DI (для путей при старте).</summary>
    public static AppUserSettings ReadAppSettingsSnapshot()
    {
        var json = Preferences.Default.Get(AppSettingsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new AppUserSettings();

        var settings = JsonSerializer.Deserialize<AppUserSettings>(json) ?? new AppUserSettings();
        settings.Normalize();
        return settings;
    }
}
