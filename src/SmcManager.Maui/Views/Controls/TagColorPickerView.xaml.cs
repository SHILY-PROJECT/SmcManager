using Microsoft.Extensions.DependencyInjection;
using SmcManager.Maui.Services;

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

    private static void OnSelectedColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TagColorPickerView view && newValue is string hex)
            view.ApplyColor(hex);
    }

    private async void OnOpenPicker(object? sender, EventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var picker = services?.GetService<TagColorPickerService>();
        if (picker is null) return;

        var result = await picker.PickColorAsync(SelectedColor);
        if (result is not null)
            SelectedColor = result;
    }

    private void ApplyColor(string hex)
    {
        if (!TagColorHelper.TryParseHex(hex, out var color))
            color = Color.FromArgb(TagColorHelper.DefaultHex);

        SelectedSwatchBorder.BackgroundColor = color;
    }
}
