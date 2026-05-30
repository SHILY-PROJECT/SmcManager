using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Hosting;
using SmcManager.Core.Enums;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views;

/// <summary>
/// Страница скачивания по ссылке.
/// </summary>
public partial class DownloadPage : ContentPage, IRecipient<ThemeChangedMessage>
{
    public DownloadPage(DownloadViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        WeakReferenceMessenger.Default.Register(this);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyThemedIcons();
        if (BindingContext is DownloadViewModel vm)
        {
            vm.ActivateMessaging();
            if (vm.AppearingCommand.CanExecute(null))
                vm.AppearingCommand.Execute(null);
        }

        _ = IPlatformApplication.Current?.Services?.GetService<ShareLinkService>()?.ProcessPendingAsync();
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is DownloadViewModel vm)
            vm.DeactivateMessaging();

        WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
        base.OnDisappearing();
    }

    public void Receive(ThemeChangedMessage message) => ApplyThemedIcons(message.Palette);

    private async void OnRecentDownloadSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView list)
            return;

        if (e.CurrentSelection.FirstOrDefault() is not ContentItemDisplayModel item)
            return;

        list.SelectedItem = null;

        Unfocus();
        await ContentNavigationHelper.OpenDetailAsync(item.Id);
    }

    private void ApplyThemedIcons(ThemePalette? palette = null)
    {
        palette ??= ThemePalette.For(
            Application.Current?.UserAppTheme == AppTheme.Dark
                ? AppColorTheme.Dark
                : AppColorTheme.Light);

        ThemedIconHelper.SetImageSource(DismissPreviewButton, palette.DeleteIcon);
    }
}
