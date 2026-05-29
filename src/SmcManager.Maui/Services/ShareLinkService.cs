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

    public async Task HandleIncomingUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var trimmed = ContentUrlNormalizer.ExtractHttpUrl(url).Trim();
        if (!trimmed.Contains("instagram", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("youtu", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("vk.com", StringComparison.OrdinalIgnoreCase))
            return;

        await _settings.SetPendingShareUrlAsync(trimmed);
        WeakReferenceMessenger.Default.Send(new ShareUrlReceivedMessage(trimmed));
    }
}
