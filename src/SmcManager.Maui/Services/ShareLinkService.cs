using System.Text.RegularExpressions;
using SmcManager.Core.Services;
using SmcManager.Core.Interfaces;
using SmcManager.Maui.ViewModels;
using SmcManager.Maui.Views;

namespace SmcManager.Maui.Services;

/// <summary>
/// Обработка ссылок из Share intent и буфера обмена.
/// </summary>
public class ShareLinkService
{
    private static readonly Regex HrefRegex = new(
        @"href\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly SemaphoreSlim ProcessGate = new(1, 1);
    private static readonly SemaphoreSlim DeliverGate = new(1, 1);
    private static string? _inFlightShareUrl;
    private static string? _lastIncomingShareUrl;
    private static long _lastIncomingShareAt;

    private readonly ISettingsService _settings;

    public ShareLinkService(ISettingsService settings) => _settings = settings;

    /// <summary>Сохраняет Share URL и доставляет его на «Главную», когда Shell готов.</summary>
    public async Task OnShareUrlReceivedAsync(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        if (string.Equals(_lastIncomingShareUrl, normalized, StringComparison.OrdinalIgnoreCase)
            && Environment.TickCount64 - _lastIncomingShareAt < 15_000)
            return;

        _lastIncomingShareUrl = normalized;
        _lastIncomingShareAt = Environment.TickCount64;

        ContentNavigationHelper.BeginShareSession();
        await _settings.SetPendingShareUrlAsync(normalized).ConfigureAwait(false);
        await ProcessPendingAsync().ConfigureAwait(false);
    }

    /// <summary>Доставляет отложенную Share-ссылку (если есть).</summary>
    public async Task ProcessPendingAsync()
    {
        await ProcessGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var pending = await _settings.GetPendingShareUrlAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(pending))
                return;

            await ShellReadiness.WaitAsync().ConfigureAwait(false);
            await NavigateToDownloadTabAsync().ConfigureAwait(false);
            await DeliverPendingShareAsync().ConfigureAwait(false);
        }
        finally
        {
            ProcessGate.Release();
        }
    }

    /// <summary>Отправляет отложенную ссылку на экран «Скачать» (если есть).</summary>
    public async Task DeliverPendingShareAsync()
    {
        await ShellReadiness.WaitAsync().ConfigureAwait(false);
        await DeliverGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var pending = await _settings.GetPendingShareUrlAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(pending))
                return;

            if (string.Equals(_inFlightShareUrl, pending, StringComparison.OrdinalIgnoreCase))
                return;

            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (Shell.Current?.CurrentPage?.BindingContext is DownloadViewModel vm)
                {
                    _inFlightShareUrl = pending;
                    await vm.ApplyIncomingShareUrlAsync(pending).ConfigureAwait(false);
                    _ = ClearInFlightShareLaterAsync(pending);
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            await _settings.SetPendingShareUrlAsync(pending).ConfigureAwait(false);
        }
        finally
        {
            DeliverGate.Release();
        }
    }

    private static async Task ClearInFlightShareLaterAsync(string url)
    {
        await Task.Delay(2500).ConfigureAwait(false);
        if (string.Equals(_inFlightShareUrl, url, StringComparison.OrdinalIgnoreCase))
            _inFlightShareUrl = null;
    }

    public static bool TryExtractNormalizedUrl(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        foreach (var candidate in EnumerateUrlCandidates(raw))
        {
            if (TryNormalizeIncomingUrl(candidate, out normalized))
                return true;
        }

        return false;
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

    private static IEnumerable<string> EnumerateUrlCandidates(string raw)
    {
        yield return raw;

        var extracted = ContentUrlNormalizer.ExtractHttpUrl(raw);
        if (!string.Equals(extracted, raw, StringComparison.Ordinal))
            yield return extracted;

        foreach (Match match in HrefRegex.Matches(raw))
        {
            var href = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(href))
                yield return href;
        }
    }

    private async Task NavigateToDownloadTabAsync()
    {
        await ShellReadiness.WaitAsync().ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not Shell shell)
                return;

            shell.FlyoutIsPresented = false;

            while (shell.Navigation.ModalStack.Count > 0)
                await shell.Navigation.PopModalAsync(animated: false);

            if (shell.CurrentPage is DownloadPage)
                return;

            var location = shell.CurrentState?.Location?.OriginalString ?? string.Empty;
            if (location.Contains("download", StringComparison.OrdinalIgnoreCase)
                && !location.Contains(nameof(ContentDetailPage), StringComparison.OrdinalIgnoreCase))
                return;

            if (!location.Contains("download", StringComparison.OrdinalIgnoreCase))
                ShellNavigationHistory.RecordFlyoutNavigation("download");

            await shell.GoToAsync("//download", animate: false);
        });
    }
}
