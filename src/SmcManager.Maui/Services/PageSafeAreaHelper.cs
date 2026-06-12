#if ANDROID
using Android.Views;
using AndroidX.Core.View;
#endif

namespace SmcManager.Maui.Services;

/// <summary>
/// Повторно применяет safe area на Android после навигации Shell (кэшированные вкладки иногда теряют отступ под статус-бар).
/// </summary>
public static class PageSafeAreaHelper
{
#if ANDROID
    private static readonly Microsoft.Maui.SafeAreaEdges ContainerInsets =
        new(Microsoft.Maui.SafeAreaRegions.Container);
#endif

    public static void EnsureApplied(Page? page)
    {
        if (page is ContentPage contentPage)
            EnsureApplied(contentPage);
    }

    public static void EnsureApplied(ContentPage? page)
    {
        if (page is null)
            return;

#if ANDROID
        if (page.SafeAreaEdges == Microsoft.Maui.SafeAreaEdges.None)
            page.SafeAreaEdges = ContainerInsets;

        ReapplyInsets(page);
#endif
    }

#if ANDROID
    private static void ReapplyInsets(ContentPage page)
    {
        void Apply()
        {
            var target = page.SafeAreaEdges == Microsoft.Maui.SafeAreaEdges.None
                ? ContainerInsets
                : page.SafeAreaEdges;

            page.SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.None;
            page.SafeAreaEdges = target;
            RequestNativeInsets(page);
        }

        Apply();
        page.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(16), Apply);
        page.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), Apply);
    }

    private static void RequestNativeInsets(Page page)
    {
        if (page.Handler?.PlatformView is not global::Android.Views.View nativeView)
            return;

        ViewCompat.RequestApplyInsets(nativeView);
        nativeView.RequestLayout();
    }
#endif
}
