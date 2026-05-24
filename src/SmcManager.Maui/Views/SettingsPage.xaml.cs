using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Страница настроек приложения.
/// </summary>
public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (Shell.Current is Shell shell)
            AppNavigationState.Update(shell);

        if (BindingContext is SettingsViewModel vm && vm.AppearingCommand.CanExecute(null))
            vm.AppearingCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        if (Shell.Current is Shell shell)
            AppNavigationState.Update(shell);

        base.OnDisappearing();
    }
}
