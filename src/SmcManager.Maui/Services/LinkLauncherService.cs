using SmcManager.Core.Interfaces;
using SmcManager.Core.Services;

namespace SmcManager.Maui.Services;

/// <summary>
/// Открывает исходную ссылку в системном браузере.
/// </summary>
public class LinkLauncherService : ILinkLauncherService
{
    public Task OpenSourceAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.CompletedTask;

        var prepared = ContentUrlNormalizer.PrepareForDetection(url.Trim());
        var normalized = ContentUrlNormalizer.Normalize(prepared);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            }
            catch
            {
                try
                {
                    await Launcher.Default.OpenAsync(uri);
                }
                catch
                {
                    // ignore: no browser / activity unavailable
                }
            }
        });
    }
}
