using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VocabApp.Wpf.Converters;

public class NonEmptyStringToVisibilityConverter : IValueConverter
{
    public static readonly NonEmptyStringToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
