using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Infrastructure.Services;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Вход через встроенный браузер.
/// </summary>
public partial class SocialLoginViewModel : ObservableObject
{
    private readonly IWebCookieExtractor _cookieExtractor;
    private readonly ISocialAccountValidationService _accountValidation;
    private readonly BottomToastService _toast;
    private WebView? _webView;

    public SocialLoginViewModel(
        IWebCookieExtractor cookieExtractor,
        ISocialAccountValidationService accountValidation,
        BottomToastService toast)
    {
        _cookieExtractor = cookieExtractor;
        _accountValidation = accountValidation;
        _toast = toast;
    }

    public SocialPlatform Platform { get; private set; }

    public Uri LoginUrl { get; private set; } = new("about:blank");

    public string PlatformTitle { get; private set; } = string.Empty;

    public string Instructions { get; private set; } = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public Func<Task>? CloseRequested { get; set; }

    public void Initialize(SocialPlatform platform)
    {
        Platform = platform;
        StatusMessage = null;
        LoginUrl = SocialLoginConfig.GetLoginUrl(platform);
        PlatformTitle = $"Вход: {SocialAccountAuth.GetPlatformTitle(platform)}";
        Instructions = platform switch
        {
            SocialPlatform.Instagram =>
                "Войдите в Instagram. Когда откроется лента или профиль, нажмите «Готово».",
            SocialPlatform.YouTube =>
                "Войдите в Google/YouTube. Когда откроется главная YouTube, нажмите «Готово».",
            SocialPlatform.Vkontakte =>
                "Войдите во ВКонтакте. После входа нажмите «Готово».",
            _ => "Войдите в аккаунт и нажмите «Готово»."
        };
        OnPropertyChanged(nameof(LoginUrl));
        OnPropertyChanged(nameof(PlatformTitle));
        OnPropertyChanged(nameof(Instructions));
    }

    public void AttachWebView(WebView webView) => _webView = webView;

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (_webView is null)
        {
            await _toast.ShowWarningAsync("Браузер не готов. Подождите загрузки страницы.");
            return;
        }

        IsBusy = true;
        StatusMessage = "Получение cookies…";

        try
        {
            var raw = await _cookieExtractor.ExtractCookiesAsync(_webView, Platform);
            if (string.IsNullOrWhiteSpace(raw))
            {
                StatusMessage = null;
                await _toast.ShowWarningAsync(
                    "Cookies не найдены. Откройте instagram.com, войдите в аккаунт и нажмите «Готово» снова.");
                return;
            }

            var normalized = SocialAccountAuth.NormalizeAuthInput(Platform, raw);
            if (!SocialAccountAuth.ValidateAuth(Platform, normalized, out var formatError))
            {
                StatusMessage = null;
                await _toast.ShowWarningAsync(formatError
                                            ?? "В cookies нет sessionid. Дождитесь входа в Instagram и нажмите «Готово» ещё раз.");
                return;
            }

            string? pageUrl = null;
            await MainThread.InvokeOnMainThreadAsync(() =>
                pageUrl = _webView?.Source?.ToString());

            StatusMessage = "Проверка авторизации…";
            SocialAccountValidationResult validation;
            try
            {
                validation = await _accountValidation
                    .ValidateAsync(Platform, normalized, pageUrl)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusMessage = null;
                await _toast.ShowWarningAsync($"Проверка не удалась: {ex.Message}");
                return;
            }

            if (!validation.IsValid)
            {
                StatusMessage = null;
                await _toast.ShowWarningAsync(validation.Message);
                return;
            }

            var username = SocialAccountAuth.ResolveUsername(
                Platform,
                normalized,
                pageUrl,
                validation.Username);

            if (Platform == SocialPlatform.Instagram && string.IsNullOrWhiteSpace(username))
                username = await InstagramSessionProbe.TryGetUsernameAsync(normalized, pageUrl).ConfigureAwait(false);

            StatusMessage = "Сохранение…";
            await FinishAsync(new SocialAuthResult
            {
                Platform = Platform,
                Cookies = normalized,
                Username = username,
                AuthMethod = SocialAuthMethod.WebLogin,
                IsSessionValidated = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            await _toast.ShowWarningAsync($"Ошибка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync() => FinishAsync(null);

    private async Task FinishAsync(SocialAuthResult? result)
    {
        var ctx = SocialLoginNavigationContext.Current;
        if (ctx is null || ctx.IsFinished) return;

        ctx.IsFinished = true;
        ctx.Completion.TrySetResult(result);
        SocialLoginNavigationContext.Current = null;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (CloseRequested is not null)
                await CloseRequested.Invoke();
        });
    }
}
