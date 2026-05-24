using Microsoft.Extensions.DependencyInjection;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;

namespace SmcManager.Maui.Services;

/// <summary>
/// Открывает страницу входа через WebView.
/// </summary>
public class MauiSocialAuthService : ISocialAuthService
{
    private readonly IServiceProvider _services;

    public MauiSocialAuthService(IServiceProvider services) => _services = services;

    public async Task<SocialAuthResult?> LoginAsync(
        SocialPlatform platform,
        CancellationToken cancellationToken = default)
    {
        var shell = Shell.Current;
        if (shell is null) return null;

        var navigation = shell.Navigation;
        var tcs = new TaskCompletionSource<SocialAuthResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        SocialLoginNavigationContext.Current = new SocialLoginNavigationContext
        {
            Platform = platform,
            Completion = tcs
        };

        var page = _services.GetRequiredService<Views.SocialLoginPage>();
        await navigation.PushModalAsync(page, animated: true);

        SocialAuthResult? result;
        try
        {
            await using var reg = cancellationToken.Register(() => tcs.TrySetResult(null));
            result = await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            if (navigation.ModalStack.Count > 0)
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        navigation.PopModalAsync(animated: true));
                }
                catch
                {
                    // already closed
                }
            }

            SocialLoginNavigationContext.Current = null;
        }

        return result;
    }
}
