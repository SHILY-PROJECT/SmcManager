using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Модальная страница выбора цвета тега.
/// </summary>
public partial class TagColorPickerPage : ContentPage
{
    private readonly TagColorPickerViewModel _viewModel;

    public TagColorPickerPage(TagColorPickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var ctx = TagColorPickerNavigationContext.Current;
        if (ctx is null)
        {
            _ = DismissAsync();
            return;
        }

        _viewModel.Initialize(ctx.InitialColor);
    }

    private static async Task DismissAsync()
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
        if (navigation?.ModalStack.Count > 0)
            await navigation.PopModalAsync(animated: true);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        var ctx = TagColorPickerNavigationContext.Current;
        if (ctx is null || ctx.IsFinished) return;

        ctx.IsFinished = true;
        ctx.Completion.TrySetResult(null);
        TagColorPickerNavigationContext.Current = null;
    }
}
