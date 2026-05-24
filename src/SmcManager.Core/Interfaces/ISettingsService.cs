using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Хранение настроек прокси и прочих параметров приложения.
/// </summary>
public interface ISettingsService
{
    Task<ProxySettings> GetProxySettingsAsync();

    Task SaveProxySettingsAsync(ProxySettings settings);

    Task<string?> GetPendingShareUrlAsync();

    Task SetPendingShareUrlAsync(string? url);

    Task<AppColorTheme> GetColorThemeAsync();

    Task SaveColorThemeAsync(AppColorTheme theme);

    Task<AppUserSettings> GetAppSettingsAsync();

    Task SaveAppSettingsAsync(AppUserSettings settings);

    Task<SettingsSectionsState> GetSettingsSectionsStateAsync();

    Task SaveSettingsSectionsStateAsync(SettingsSectionsState state);
}
