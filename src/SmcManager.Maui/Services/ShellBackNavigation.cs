using SmcManager.Maui.Views;

namespace SmcManager.Maui.Services;

/// <summary>
/// Возврат по стеку Shell вместо закрытия приложения на Android.
/// </summary>
public static class ShellBackNavigation
{
    private static readonly HashSet<Type> FlyoutRootPageTypes =
    [
        typeof(DownloadPage),
        typeof(LibraryPage),
        typeof(GroupsPage),
        typeof(TagsPage),
    ];

    public static bool TryGoBack()
    {
        var shell = Shell.Current;
        if (shell is null)
            return false;

        var navigation = shell.Navigation;

        if (navigation.ModalStack.Count > 0)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await navigation.PopModalAsync(animated: true);
                }
                catch
                {
                    // already closed
                }
            });
            return true;
        }

        if (shell.CurrentPage is null)
            return false;

        if (IsFlyoutRootPage(shell.CurrentPage))
        {
            if (!ShellNavigationHistory.TryPopToPreviousRoute(out var previousRoute))
                return false;

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    ShellNavigationHistory.PrepareBackNavigation();
                    await shell.GoToAsync($"//{previousRoute}");
                }
                catch
                {
                    // navigation failed
                }
            });
            return true;
        }

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await shell.GoToAsync("..");
            }
            catch
            {
                // nothing to pop
            }
        });
        return true;
    }

    public static Task GoBackAsync()
    {
        var shell = Shell.Current;
        if (shell is null)
            return Task.CompletedTask;

        if (shell.Navigation.ModalStack.Count > 0)
        {
            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await shell.Navigation.PopModalAsync(animated: true);
                }
                catch
                {
                    // already closed
                }
            });
        }

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await shell.GoToAsync("..");
            }
            catch
            {
                // nothing to pop
            }
        });
    }

    private static bool IsFlyoutRootPage(Page page) =>
        FlyoutRootPageTypes.Contains(page.GetType());
}
