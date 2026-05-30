using SmcManager.Maui.Views;

namespace SmcManager.Maui.Services;

/// <summary>
/// Надёжная навигация к экрану просмотра контента (важно для Android + CollectionView).
/// </summary>
internal static class ContentNavigationHelper
{
    public static async Task OpenDetailAsync(int contentId)
    {
        if (contentId <= 0 || Shell.Current is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Task.Yield();

            await Shell.Current.GoToAsync(
                nameof(ContentDetailPage),
                new Dictionary<string, object> { ["contentId"] = contentId.ToString() });
        });
    }
}
