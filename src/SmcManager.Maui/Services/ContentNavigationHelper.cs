using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using SmcManager.Maui.ViewModels;
using SmcManager.Maui.Views;

namespace SmcManager.Maui.Services;

/// <summary>
/// Надёжная навигация к экрану просмотра контента (важно для Android + Share intent).
/// </summary>
internal static class ContentNavigationHelper
{
    private static int _shareSessionActive;

    public static void BeginShareSession() => Interlocked.Exchange(ref _shareSessionActive, 1);

    public static Task OpenDetailAsync(int contentId)
    {
        if (contentId <= 0)
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(() => NavigateToDetailAsync(contentId));
    }

    private static async Task NavigateToDetailAsync(int contentId)
    {
        if (Shell.Current is not Shell shell)
            return;

        shell.CurrentPage?.Unfocus();
        await Task.Yield();

        var shareSession = Interlocked.CompareExchange(ref _shareSessionActive, 0, 0) == 1;

        if (shareSession && await TryPresentDetailPageAsync(shell, contentId, modal: true))
        {
            EndShareSession();
            return;
        }

        var parameters = new ShellNavigationQueryParameters
        {
            ["contentId"] = contentId.ToString()
        };

        var routes = new[]
        {
            nameof(ContentDetailPage),
            $"//download/{nameof(ContentDetailPage)}"
        };

        foreach (var route in routes)
        {
            try
            {
                await shell.GoToAsync(route, parameters);
                EndShareSession();
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Content GoToAsync failed (route={route}, id={contentId}): {ex.Message}");
            }
        }

        if (await TryPresentDetailPageAsync(shell, contentId, modal: true)
            || await TryPresentDetailPageAsync(shell, contentId, modal: false))
        {
            EndShareSession();
            return;
        }

        if (shareSession)
            Interlocked.Exchange(ref _shareSessionActive, 1);
    }

    private static async Task<bool> TryPresentDetailPageAsync(Shell shell, int contentId, bool modal)
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null)
                return false;

            var page = services.GetRequiredService<ContentDetailPage>();
            if (page.BindingContext is ContentDetailViewModel vm)
                vm.ContentId = contentId.ToString();

            if (modal)
                await shell.Navigation.PushModalAsync(page, animated: true);
            else
                await shell.Navigation.PushAsync(page, animated: true);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Content {(modal ? "PushModalAsync" : "PushAsync")} failed (id={contentId}): {ex}");
            return false;
        }
    }

    private static void EndShareSession() => Interlocked.Exchange(ref _shareSessionActive, 0);
}
