using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

public partial class TagsPage : ContentPage
{
    public TagsPage(TagsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is TagsViewModel vm && vm.AppearingCommand.CanExecute(null))
            vm.AppearingCommand.Execute(null);
    }
}
