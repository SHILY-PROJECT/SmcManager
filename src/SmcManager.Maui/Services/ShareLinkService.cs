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

    /// <summary>Переход на вкладку «Скачать» (с ожиданием Shell при холодном старте).</summary>
    public async Task EnsureDownloadTabAsync()
    {
        for (var attempt = 0; attempt < 40 && Shell.Current is null; attempt++)
            await Task.Delay(50).ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
                return;

            if (Shell.Current.FlyoutIsPresented)
                Shell.Current.FlyoutIsPresented = false;

            await Shell.Current.GoToAsync("//download");
        });
    }

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

        await EnsureDownloadTabAsync().ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() =>
            WeakReferenceMessenger.Default.Send(new ShareUrlReceivedMessage(normalized)));
    }
}
