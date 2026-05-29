using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Модальная страница входа через WebView.
/// </summary>
public partial class SocialLoginPage : ContentPage
{
    private readonly SocialLoginViewModel _viewModel;

    public SocialLoginPage(SocialLoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.CloseRequested = CloseModalAsync;
    }

    private async Task CloseModalAsync()
    {
        if (Navigation.ModalStack.Count == 0)
            return;

        await MainThread.InvokeOnMainThreadAsync(() => Navigation.PopModalAsync(animated: true));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var ctx = SocialLoginNavigationContext.Current;
        if (ctx is null)
        {
            _ = CloseModalAsync();
            return;
        }

        _viewModel.Initialize(ctx.Platform);
        _viewModel.AttachWebView(Browser);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        var ctx = SocialLoginNavigationContext.Current;
        if (ctx is null || ctx.IsFinished) return;

        ctx.IsFinished = true;
        ctx.Completion.TrySetResult(null);
        SocialLoginNavigationContext.Current = null;
    }
}
