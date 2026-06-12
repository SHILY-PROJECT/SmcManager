using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Views;

namespace SmcManager.Maui.Services;

/// <summary>
/// Текущий экран настроек в стеке Shell (для кнопки «назад» в шапке).
/// </summary>
public static class AppNavigationState
{
    public static bool IsSettingsVisible { get; private set; }

    public static void Update(Shell shell)
    {
        var visible = ResolveSettingsVisible(shell);
        if (visible == IsSettingsVisible)
            return;

        IsSettingsVisible = visible;
        shell.Dispatcher.Dispatch(() =>
            WeakReferenceMessenger.Default.Send(new AppHeaderModeChangedMessage(visible)));
    }

    private static bool ResolveSettingsVisible(Shell shell)
    {
        if (IsSettingsPage(shell.CurrentPage))
            return true;

        var nav = shell.Navigation;
        for (var i = nav.NavigationStack.Count - 1; i >= 0; i--)
        {
            if (IsSettingsPage(nav.NavigationStack[i]))
                return true;
        }

        for (var i = nav.ModalStack.Count - 1; i >= 0; i--)
        {
            if (IsSettingsPage(nav.ModalStack[i]))
                return true;
        }

        var location = shell.CurrentState?.Location.OriginalString ?? string.Empty;
        return location.Contains(nameof(SettingsPage), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSettingsPage(Page? page) =>
        page is SettingsPage;
}
