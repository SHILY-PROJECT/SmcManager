using System.Globalization;

namespace SmcManager.Maui.Converters;

/// <summary>
/// Возвращает true, если строка не пустая.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
