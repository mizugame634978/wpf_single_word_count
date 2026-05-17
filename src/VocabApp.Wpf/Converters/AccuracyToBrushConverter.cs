using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VocabApp.Wpf.Converters;

/// <summary>正答率 (double 0..100) を 3 段階の色に変換: <60=赤, <80=オレンジ, ≥80=緑。</summary>
public class AccuracyToBrushConverter : IValueConverter
{
    public static readonly AccuracyToBrushConverter Instance = new();

    private static readonly Brush LowBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush MidBrush = new SolidColorBrush(Color.FromRgb(0xED, 0x6C, 0x02));
    private static readonly Brush HighBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));

    static AccuracyToBrushConverter()
    {
        LowBrush.Freeze();
        MidBrush.Freeze();
        HighBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double pct) return HighBrush;
        return pct switch
        {
            >= 80 => HighBrush,
            >= 60 => MidBrush,
            _ => LowBrush,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
