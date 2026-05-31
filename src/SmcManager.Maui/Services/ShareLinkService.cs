using System.Text.RegularExpressions;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Services;
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

        ContentNavigationHelper.EndShareSession();
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

            if (!IsDownloadRootVisible(Shell.Current))
                await NavigateToDownloadTabAsync().ConfigureAwait(false);

            if (!IsDownloadRootVisible(Shell.Current))
            {
                await _settings.SetPendingShareUrlAsync(pending).ConfigureAwait(false);
                return;
            }

            for (var attempt = 0; attempt < 80; attempt++)
            {
                var pendingAfterMessage = await _settings.GetPendingShareUrlAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(pendingAfterMessage))
                    return;

                if (TryGetVisibleDownloadViewModel(out var vm))
                {
                    _inFlightShareUrl = pendingAfterMessage;
                    await vm.ApplyIncomingShareUrlAsync(pendingAfterMessage, force: true).ConfigureAwait(false);
                    ContentNavigationHelper.EndShareSession();
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

            await ResetNavigationForShareAsync(shell).ConfigureAwait(true);
            ShellNavigationHistory.ResetToRoute("download");

            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    await shell.GoToAsync("//download", animate: false).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Share GoToAsync //download failed: {ex.Message}");
                }

                if (IsDownloadRootVisible(shell))
                    return;

                await PopShellRouteAsync(shell).ConfigureAwait(true);
                await Task.Delay(80).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        await WaitForDownloadPageAsync().ConfigureAwait(false);
    }

    private static async Task ResetNavigationForShareAsync(Shell shell)
    {
        shell.FlyoutIsPresented = false;

        while (shell.Navigation.ModalStack.Count > 0)
            await shell.Navigation.PopModalAsync(animated: false).ConfigureAwait(true);

        for (var i = 0; i < 12; i++)
        {
            if (IsFlyoutRootPage(shell.CurrentPage))
                break;

            await PopShellRouteAsync(shell).ConfigureAwait(true);
        }

        while (shell.Navigation.NavigationStack.Count > 1)
            await shell.Navigation.PopAsync(animated: false).ConfigureAwait(true);
    }

    private static async Task PopShellRouteAsync(Shell shell)
    {
        try
        {
            await shell.GoToAsync("..", animate: false).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share GoToAsync .. failed: {ex.Message}");
        }
    }

    private static bool IsFlyoutRootPage(Page? page) =>
        page is DownloadPage or LibraryPage or GroupsPage or TagsPage;

    private static bool IsDownloadRootVisible(Shell? shell)
    {
        if (shell?.CurrentPage is not DownloadPage)
            return false;

        var location = shell.CurrentState?.Location?.OriginalString ?? string.Empty;
        if (!location.Contains("download", StringComparison.OrdinalIgnoreCase))
            return false;

        return !location.Contains(nameof(ContentDetailPage), StringComparison.OrdinalIgnoreCase)
               && !location.Contains(nameof(SettingsPage), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitForDownloadPageAsync()
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            if (IsDownloadRootVisible(Shell.Current))
                return;

            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private static bool TryGetVisibleDownloadViewModel(out DownloadViewModel viewModel)
    {
        viewModel = null!;

        if (Shell.Current?.CurrentPage is DownloadPage downloadPage
            && downloadPage.BindingContext is DownloadViewModel pageViewModel
            && IsDownloadRootVisible(Shell.Current))
        {
            viewModel = pageViewModel;
            return true;
        }

        return false;
    }
}
