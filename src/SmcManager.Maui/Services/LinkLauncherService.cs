using SmcManager.Core.Interfaces;

namespace SmcManager.Maui.Services;

/// <summary>
/// Открывает исходную ссылку в системном браузере.
/// </summary>
public class LinkLauncherService : ILinkLauncherService
{
    public async Task OpenSourceAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return;

        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.External);
    }
}
