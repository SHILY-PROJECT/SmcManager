using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Страница всего скачанного контента.
/// </summary>
public partial class LibraryPage : ContentPage
{
    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LibraryViewModel vm && vm.AppearingCommand.CanExecute(null))
            vm.AppearingCommand.Execute(null);
    }
}
