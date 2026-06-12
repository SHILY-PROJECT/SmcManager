using System.Windows.Input;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.Views.Controls;

/// <summary>
/// Чип тега с цветом из <see cref="ColorHex"/> или стилем темы по умолчанию.
/// </summary>
public partial class TagChipView : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TagChipView), string.Empty);

    public static readonly BindableProperty ColorHexProperty = BindableProperty.Create(
        nameof(ColorHex), typeof(string), typeof(TagChipView), null,
        propertyChanged: OnAppearancePropertyChanged);

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(TagChipView), false,
        propertyChanged: OnAppearancePropertyChanged);

    public static readonly BindableProperty SelectCommandProperty = BindableProperty.Create(
        nameof(SelectCommand), typeof(ICommand), typeof(TagChipView));

    public static readonly BindableProperty SelectParameterProperty = BindableProperty.Create(
        nameof(SelectParameter), typeof(object), typeof(TagChipView));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? ColorHex
    {
        get => (string?)GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => (ICommand?)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public object? SelectParameter
    {
        get => GetValue(SelectParameterProperty);
        set => SetValue(SelectParameterProperty, value);
    }

    public TagChipView()
    {
        InitializeComponent();
        UpdateAppearance();
    }

    private static void OnAppearancePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TagChipView view)
            view.UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        var appearance = TagChipAppearanceFactory.For(ColorHex, IsSelected);
        ChipBorder.BackgroundColor = appearance.Fill;
        ChipBorder.Stroke = appearance.Stroke;
        ChipLabel.TextColor = appearance.Text;
        ChipLabel.FontAttributes = IsSelected ? FontAttributes.Bold : FontAttributes.None;
    }
}
