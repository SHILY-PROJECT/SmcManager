using Microsoft.Extensions.DependencyInjection;

namespace SmcManager.Maui.Services;

/// <summary>
/// Открывает модальную страницу выбора цвета тега.
/// </summary>
public sealed class TagColorPickerService(IServiceProvider services)
{
    public async Task<string?> PickColorAsync(
        string initialColor,
        Action<string>? onColorSelected = null,
        CancellationToken cancellationToken = default)
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
        if (navigation is null)
            return null;

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        TagColorPickerNavigationContext.Current = new TagColorPickerNavigationContext
        {
            InitialColor = initialColor,
            Completion = tcs,
            OnColorSelected = onColorSelected
        };

        var page = services.GetRequiredService<Views.TagColorPickerPage>();
        await MainThread.InvokeOnMainThreadAsync(() =>
            navigation.PushModalAsync(page, animated: true));

        try
        {
            await using var reg = cancellationToken.Register(() => tcs.TrySetResult(null));
            var result = await tcs.Task;
            return result is null
                ? null
                : await MainThread.InvokeOnMainThreadAsync(() => result);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (navigation.ModalStack.Count > 0)
                {
                    try
                    {
                        await navigation.PopModalAsync(animated: true);
                    }
                    catch
                    {
                        // already closed
                    }
                }

                TagColorPickerNavigationContext.Current = null;
            });
        }
    }
}
