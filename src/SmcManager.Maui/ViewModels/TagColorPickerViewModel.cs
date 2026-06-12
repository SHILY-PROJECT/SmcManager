using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.ViewModels;

/// <summary>
/// Модальный выбор цвета тега: палитра и HSL-ползунки.
/// </summary>
public partial class TagColorPickerViewModel : ObservableObject
{
    private bool _suppressSliders;

    public IReadOnlyList<string> Palette { get; } = TagColorHelper.Palette;

    [ObservableProperty]
    private string _selectedColor = TagColorHelper.DefaultHex;

    [ObservableProperty]
    private string _selectedHexDisplay = TagColorHelper.DefaultHex;

    [ObservableProperty]
    private Color _previewColor = Color.FromArgb(TagColorHelper.DefaultHex);

    [ObservableProperty]
    private double _hue;

    [ObservableProperty]
    private double _saturation = 80;

    [ObservableProperty]
    private double _lightness = 50;

    public void Initialize(string initialColor)
    {
        ApplyColor(string.IsNullOrWhiteSpace(initialColor) ? TagColorHelper.DefaultHex : initialColor);
        SyncSlidersFromColor();
    }

    [RelayCommand]
    private void SelectPaletteColor(string hex)
    {
        ApplyColor(hex);
        SyncSlidersFromColor();
    }

    [RelayCommand]
    private void ConfirmAsync()
    {
        var ctx = TagColorPickerNavigationContext.Current;
        if (ctx is null) return;

        var hex = TagColorHelper.NormalizeHex(SelectedColor);
        ctx.IsFinished = true;
        ctx.OnColorSelected?.Invoke(hex);
        ctx.Completion.TrySetResult(hex);
    }

    [RelayCommand]
    private void CancelAsync()
    {
        var ctx = TagColorPickerNavigationContext.Current;
        if (ctx is null) return;

        ctx.IsFinished = true;
        ctx.Completion.TrySetResult(null);
    }

    partial void OnHueChanged(double value) => UpdateColorFromSliders();

    partial void OnSaturationChanged(double value) => UpdateColorFromSliders();

    partial void OnLightnessChanged(double value) => UpdateColorFromSliders();

    private void UpdateColorFromSliders()
    {
        if (_suppressSliders) return;

        var color = TagColorHelper.FromHsl(Hue, Saturation / 100.0, Lightness / 100.0);
        ApplyColor(TagColorHelper.ToHex(color));
    }

    private void ApplyColor(string hex)
    {
        if (!TagColorHelper.TryParseHex(hex, out var color))
            color = Color.FromArgb(TagColorHelper.DefaultHex);

        SelectedColor = TagColorHelper.ToHex(color);
        SelectedHexDisplay = SelectedColor;
        PreviewColor = color;
    }

    private void SyncSlidersFromColor()
    {
        if (!TagColorHelper.TryParseHex(SelectedColor, out var color))
            return;

        var (h, s, l) = TagColorHelper.ToHsl(color);
        _suppressSliders = true;
        Hue = h;
        Saturation = s;
        Lightness = l;
        _suppressSliders = false;
    }
}
