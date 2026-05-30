using SmcManager.Maui.Views;

namespace SmcManager.Maui.Services;

/// <summary>
/// Надёжная навигация к экрану просмотра контента (важно для Android + CollectionView).
/// </summary>
internal static class ContentNavigationHelper
{
    private static int _isNavigating;

    public static async Task OpenDetailAsync(int contentId)
    {
        if (contentId <= 0 || Shell.Current is null)
            return;

        if (Interlocked.CompareExchange(ref _isNavigating, 1, 0) != 0)
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Yield();

                try
                {
                    await Shell.Current.GoToAsync(
                        nameof(ContentDetailPage),
                        new Dictionary<string, object> { ["contentId"] = contentId.ToString() });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Content navigation failed: {ex}");
                }
            });
        }
        finally
        {
            Interlocked.Exchange(ref _isNavigating, 0);
        }
    }
}
