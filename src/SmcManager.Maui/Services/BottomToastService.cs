using System.Collections.Concurrent;
using Microsoft.Maui.Controls.Shapes;

namespace SmcManager.Maui.Services;

/// <summary>
/// Краткое предупреждение внизу активного окна (включая модальные).
/// </summary>
public sealed class BottomToastService
{
    private const int DefaultDurationMs = 3000;

    private readonly ConcurrentDictionary<Page, ToastHost> _hosts = new();
    private CancellationTokenSource? _hideCts;

    public Task ShowWarningAsync(string message, int durationMs = DefaultDurationMs) =>
        ShowAsync(message, durationMs);

    public async Task ShowAsync(string message, int durationMs = DefaultDurationMs)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = GetTopPage();
            if (page is null)
                return;

            var host = _hosts.GetOrAdd(page, CreateHost);
            host.EnsureAttached(page);

            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;

            host.Label.Text = message;
            host.Border.IsVisible = true;
            host.Border.Opacity = 0;
            await host.Border.FadeToAsync(1, 180);

            try
            {
                await Task.Delay(durationMs, token);
                if (!token.IsCancellationRequested)
                {
                    await host.Border.FadeToAsync(0, 180);
                    host.Border.IsVisible = false;
                }
            }
            catch (TaskCanceledException)
            {
                // Новое сообщение заменило текущее.
            }
        });
    }

    private static Page? GetTopPage()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        var root = window?.Page;
        if (root is null)
            return null;

        if (root is Shell shell)
        {
            if (shell.Navigation.ModalStack.Count > 0)
                return shell.Navigation.ModalStack[^1];

            return shell.CurrentPage ?? shell;
        }

        if (root.Navigation.ModalStack.Count > 0)
            return root.Navigation.ModalStack[^1];

        return root;
    }

    private ToastHost CreateHost(Page page)
    {
        var host = new ToastHost();
        page.Disappearing += (_, _) => _hosts.TryRemove(page, out _);
        return host;
    }

    private sealed class ToastHost
    {
        public Border Border { get; }
        public Label Label { get; }
        private bool _isAttached;

        public ToastHost()
        {
            Label = new Label
            {
                FontSize = 14,
                LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = ResolveColor("Danger", Colors.IndianRed)
            };

            Border = new Border
            {
                IsVisible = false,
                Opacity = 0,
                Padding = new Thickness(14, 10),
                Margin = new Thickness(16, 0, 16, 20),
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = ResolveColor("BackgroundElevated", Colors.White),
                Stroke = ResolveColor("Divider", Colors.LightGray),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = Label,
                Shadow = new Shadow
                {
                    Brush = Brush.Black,
                    Opacity = 0.2f,
                    Radius = 8,
                    Offset = new Point(0, 2)
                }
            };
        }

        public void EnsureAttached(Page page)
        {
            if (_isAttached || page is not ContentPage contentPage)
                return;

            _isAttached = true;
            var original = contentPage.Content;
            if (original is null)
                return;

            var grid = new Grid();
            grid.Children.Add(original);
            grid.Children.Add(Border);
            contentPage.Content = grid;
        }

        private static Color ResolveColor(string key, Color fallback) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
                ? color
                : fallback;
    }
}
