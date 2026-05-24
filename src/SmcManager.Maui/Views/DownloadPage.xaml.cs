using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Страница скачивания по ссылке.
/// </summary>
public partial class DownloadPage : ContentPage
{
    public DownloadPage(DownloadViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DownloadViewModel vm && vm.AppearingCommand.CanExecute(null))
            vm.AppearingCommand.Execute(null);
    }
}
