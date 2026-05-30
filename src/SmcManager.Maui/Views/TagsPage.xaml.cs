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
        // Не перезагружаем список при закрытии модальной палитры цвета — иначе сбрасывается режим редактирования.
        if (Shell.Current?.Navigation.ModalStack.Count > 0)
            return;

        if (BindingContext is TagsViewModel vm && vm.AppearingCommand.CanExecute(null))
            vm.AppearingCommand.Execute(null);
    }
}
