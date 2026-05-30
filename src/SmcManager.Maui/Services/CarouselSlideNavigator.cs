namespace SmcManager.Maui.Services;

/// <summary>
/// Программная навигация по CarouselView (на Windows уменьшение Position часто не срабатывает).
/// </summary>
internal static class CarouselSlideNavigator
{
    public static void NavigateTo(CarouselView carousel, int index, int itemCount)
    {
        if (itemCount <= 0)
            return;

        index = Math.Clamp(index, 0, itemCount - 1);

#if ANDROID
        if (carousel.Position != index)
            carousel.Position = index;
#else
        carousel.ScrollTo(index, -1, ScrollToPosition.Center, animate: false);
#endif
    }
}
