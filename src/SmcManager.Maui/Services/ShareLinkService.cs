using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Services;
using SmcManager.Core.Interfaces;
using SmcManager.Maui.Messages;

namespace SmcManager.Maui.Services;

/// <summary>
/// Обработка ссылок из Share intent и буфера обмена.
/// </summary>
public class ShareLinkService
{
    private readonly ISettingsService _settings;

    public ShareLinkService(ISettingsService settings) => _settings = settings;

    public static bool TryNormalizeIncomingUrl(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var prepared = ContentUrlNormalizer.PrepareForDetection(raw);
        if (!UrlPlatformDetector.TryDetect(prepared, out _, out _))
            return false;

        normalized = ContentUrlNormalizer.Normalize(prepared);
        return !string.IsNullOrWhiteSpace(normalized);
    }

    public async Task HandleIncomingUrlAsync(string? url)
    {
        if (!TryNormalizeIncomingUrl(url, out var normalized))
            return;

        await _settings.SetPendingShareUrlAsync(normalized).ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null
                && Shell.Current.CurrentState?.Location?.OriginalString?.Contains("download", StringComparison.OrdinalIgnoreCase) != true)
            {
                await Shell.Current.GoToAsync("//download");
            }

            WeakReferenceMessenger.Default.Send(new ShareUrlReceivedMessage(normalized));
        });
    }
}
