using SmcManager.Maui.Services;

namespace SmcManager.Maui.Behaviors;

/// <summary>
/// На Android повторно применяет safe area при появлении страницы.
/// </summary>
public sealed class SafeAreaRefreshBehavior : Behavior<ContentPage>
{
    protected override void OnAttachedTo(ContentPage page)
    {
        base.OnAttachedTo(page);
        page.Appearing += OnPageAppearing;
    }

    protected override void OnDetachingFrom(ContentPage page)
    {
        page.Appearing -= OnPageAppearing;
        base.OnDetachingFrom(page);
    }

    private void OnPageAppearing(object? sender, EventArgs e)
    {
        if (sender is ContentPage page)
            PageSafeAreaHelper.EnsureApplied(page);
    }
}
