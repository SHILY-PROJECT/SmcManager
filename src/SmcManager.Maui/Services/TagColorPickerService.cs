using Microsoft.Extensions.DependencyInjection;

namespace SmcManager.Maui.Services;

/// <summary>
/// Открывает модальную страницу выбора цвета тега.
/// </summary>
public sealed class TagColorPickerService(IServiceProvider services)
{
    public async Task<string?> PickColorAsync(string initialColor, CancellationToken cancellationToken = default)
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
        if (navigation is null)
            return null;

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        TagColorPickerNavigationContext.Current = new TagColorPickerNavigationContext
        {
            InitialColor = initialColor,
            Completion = tcs
        };

        var page = services.GetRequiredService<Views.TagColorPickerPage>();
        await MainThread.InvokeOnMainThreadAsync(() =>
            navigation.PushModalAsync(page, animated: true));

        try
        {
            await using var reg = cancellationToken.Register(() => tcs.TrySetResult(null));
            return await tcs.Task.ConfigureAwait(false);
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

            TagColorPickerNavigationContext.Current = null;
        }
    }
}
