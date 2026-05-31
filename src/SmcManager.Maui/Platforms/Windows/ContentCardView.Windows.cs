#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace SmcManager.Maui.Views.Controls;

public partial class ContentCardView
{
    partial void InitPlatformInteractions()
    {
        CardBorder.HandlerChanged += OnCardHandlerChanged;
        OnCardHandlerChanged(this, EventArgs.Empty);
    }

    private void OnCardHandlerChanged(object? sender, EventArgs e)
    {
        if (CardBorder.Handler?.PlatformView is not UIElement uiElement)
            return;

        uiElement.RightTapped -= OnRightTapped;
        uiElement.RightTapped += OnRightTapped;
    }

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e) =>
        _ = ShowContextActionsAsync();
}
#endif
