using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Services;
using SmcManager.Core.Interfaces;
using SmcManager.Maui.Messages;
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
            && Environment.TickCount64 - _lastIncomingShareAt < 1500)
            return;

        _lastIncomingShareUrl = normalized;
        _lastIncomingShareAt = Environment.TickCount64;
        _inFlightShareUrl = null;

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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessPendingAsync failed: {ex}");
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

            await WaitForDownloadPageAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
                WeakReferenceMessenger.Default.Send(new ShareUrlReceivedMessage(pending)));

            for (var attempt = 0; attempt < 80; attempt++)
            {
                var pendingAfterMessage = await _settings.GetPendingShareUrlAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(pendingAfterMessage))
                    return;

                if (TryGetDownloadViewModel(out var vm))
                {
                    _inFlightShareUrl = pendingAfterMessage;
                    await vm.ApplyIncomingShareUrlAsync(pendingAfterMessage, force: true).ConfigureAwait(false);
                    _ = ClearInFlightShareLaterAsync(pendingAfterMessage);
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

            if (!ShouldNavigateToDownloadRoot(shell))
                return;

            ShellNavigationHistory.RecordFlyoutNavigation("download");
            await shell.GoToAsync("//download", animate: false);
        });

        await WaitForDownloadPageAsync().ConfigureAwait(false);
    }

    private static bool ShouldNavigateToDownloadRoot(Shell shell)
    {
        if (shell.CurrentPage is not DownloadPage)
            return true;

        var location = shell.CurrentState?.Location?.OriginalString ?? string.Empty;
        return location.Contains(nameof(ContentDetailPage), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitForDownloadPageAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (Shell.Current?.CurrentPage is DownloadPage)
                return;

            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private static bool TryGetDownloadViewModel(out DownloadViewModel viewModel)
    {
        viewModel = null!;

        if (Shell.Current?.CurrentPage is DownloadPage downloadPage
            && downloadPage.BindingContext is DownloadViewModel pageViewModel)
        {
            viewModel = pageViewModel;
            return true;
        }

        if (Shell.Current?.CurrentPage?.BindingContext is DownloadViewModel currentViewModel)
        {
            viewModel = currentViewModel;
            return true;
        }

        return false;
    }
}
