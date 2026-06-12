using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

public partial class TagsPage : ContentPage
{
    private bool _skipNextAppearReload;

    public TagsPage(TagsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Shell.Current?.Navigation.ModalStack.Count > 0)
            _skipNextAppearReload = true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_skipNextAppearReload)
        {
            _skipNextAppearReload = false;
            return;
        }

        if (BindingContext is TagsViewModel vm && vm.AppearingCommand.CanExecute(null))
            vm.AppearingCommand.Execute(null);
    }
}
