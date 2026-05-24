using System.Globalization;

namespace SmcManager.Maui.Converters;

/// <summary>
/// Преобразует #RRGGBB в Color для привязок XAML.
/// </summary>
public class HexToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Colors.Transparent;

        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
