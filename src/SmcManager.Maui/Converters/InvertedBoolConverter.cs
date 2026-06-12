using System.Globalization;

namespace SmcManager.Maui.Converters;

/// <summary>
/// Инвертирует bool (для IsEnabled при IsBusy).
/// </summary>
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}
