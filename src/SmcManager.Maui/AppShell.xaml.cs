using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Maui.Messages;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;

namespace SmcManager.Maui;

/// <summary>
/// Боковое меню (flyout) и основные разделы приложения.
/// </summary>
public partial class AppShell : Shell, IRecipient<ThemeChangedMessage>
{
    private readonly ThemeService _themeService;

    public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; } = [];

    public AppShell(ThemeService themeService)
    {
        _themeService = themeService;
        InitializeComponent();
        InitializeFlyoutMenu();
        ShellNavigationHistory.ResetToRoute("download");
        WeakReferenceMessenger.Default.Register(this);
        _themeService.ApplyFlyoutIcons(this);
        ApplyFlyoutMenuIcons(_themeService.CurrentPalette);
        Routing.RegisterRoute(nameof(Views.DownloadPage), typeof(Views.DownloadPage));
        Routing.RegisterRoute(nameof(Views.LibraryPage), typeof(Views.LibraryPage));
        Routing.RegisterRoute(nameof(Views.GroupsPage), typeof(Views.GroupsPage));
        Routing.RegisterRoute(nameof(Views.TagsPage), typeof(Views.TagsPage));
        Routing.RegisterRoute(nameof(Views.SettingsPage), typeof(Views.SettingsPage));
        Routing.RegisterRoute(nameof(Views.ContentDetailPage), typeof(Views.ContentDetailPage));
        Routing.RegisterRoute(nameof(Views.TagColorPickerPage), typeof(Views.TagColorPickerPage));
        Navigated += OnShellNavigated;
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e) =>
        AppNavigationState.Update(this);

    protected override bool OnBackButtonPressed()
    {
        if (ShellBackNavigation.TryGoBack())
            return true;

        return base.OnBackButtonPressed();
    }

    public void Receive(ThemeChangedMessage message)
    {
        ThemeService.ApplyFlyoutIcons(this, message.Palette);
        ApplyFlyoutMenuIcons(message.Palette);
    }

    private void InitializeFlyoutMenu()
    {
        FlyoutMenuItems.Add(new FlyoutMenuItem("Скачать", "download"));
        FlyoutMenuItems.Add(new FlyoutMenuItem("Контент", "library"));
        FlyoutMenuItems.Add(new FlyoutMenuItem("Группы", "groups"));
        FlyoutMenuItems.Add(new FlyoutMenuItem("Теги", "tags"));
    }

    private void ApplyFlyoutMenuIcons(ThemePalette palette)
    {
        foreach (var item in FlyoutMenuItems)
        {
            var icon = ThemeService.ResolveFlyoutIcon(item.Route, palette);
            item.Icon = ThemedIconHelper.FromFile(icon);
        }
    }

    private async void OnFlyoutMenuSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not FlyoutMenuItem item)
            return;

        FlyoutMenuView.SelectedItem = null;
        FlyoutIsPresented = false;
        ShellNavigationHistory.RecordFlyoutNavigation(item.Route);
        await GoToAsync($"//{item.Route}");
    }
}
