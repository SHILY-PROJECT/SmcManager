using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SmcManager.Maui.Models;
using SmcManager.Maui.Services;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Views.Controls;

/// <summary>
/// Кнопка выбранного цвета; открывает модальную палитру.
/// </summary>
public partial class TagColorPickerView : ContentView
{
    public static readonly BindableProperty SelectedColorProperty =
        BindableProperty.Create(
            nameof(SelectedColor),
            typeof(string),
            typeof(TagColorPickerView),
            TagColorHelper.DefaultHex,
            BindingMode.TwoWay,
            propertyChanged: OnSelectedColorChanged);

    public static readonly BindableProperty PickColorCommandProperty =
        BindableProperty.Create(
            nameof(PickColorCommand),
            typeof(ICommand),
            typeof(TagColorPickerView));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(
            nameof(CommandParameter),
            typeof(object),
            typeof(TagColorPickerView));

    public TagColorPickerView()
    {
        InitializeComponent();
        ApplyColor(SelectedColor);
    }

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ICommand? PickColorCommand
    {
        get => (ICommand?)GetValue(PickColorCommandProperty);
        set => SetValue(PickColorCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private static void OnSelectedColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TagColorPickerView view && newValue is string hex)
            view.ApplyColor(hex);
    }

    private async void OnOpenPicker(object? sender, EventArgs e)
    {
        var parameter = CommandParameter ?? BindingContext;

        if (PickColorCommand is not null && PickColorCommand.CanExecute(parameter))
        {
            await ExecutePickColorCommandAsync(PickColorCommand, parameter);
            ApplyColor(SelectedColor);
            return;
        }

        var services = Application.Current?.Handler?.MauiContext?.Services;
        var picker = services?.GetService<TagColorPickerService>();
        if (picker is null)
            return;

        var result = await picker.PickColorAsync(SelectedColor);
        if (result is not null)
            SelectedColor = TagColorHelper.NormalizeHex(result);
    }

    private static async Task ExecutePickColorCommandAsync(ICommand command, object? parameter)
    {
        switch (command)
        {
            case IAsyncRelayCommand<TagRowViewModel> rowCommand when parameter is TagRowViewModel row:
                await rowCommand.ExecuteAsync(row);
                break;
            case IAsyncRelayCommand asyncCommand:
                await asyncCommand.ExecuteAsync(parameter);
                break;
            default:
                command.Execute(parameter);
                break;
        }
    }

    private void ApplyColor(string hex)
    {
        if (!TagColorHelper.TryParseHex(hex, out var color))
            color = Color.FromArgb(TagColorHelper.DefaultHex);

        SelectedSwatchBorder.BackgroundColor = color;
    }
}
