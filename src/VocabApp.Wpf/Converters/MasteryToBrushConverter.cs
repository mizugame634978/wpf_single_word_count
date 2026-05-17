using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VocabApp.Wpf.Converters;

/// <summary>習熟度 (int 0..5) を 3 段階の色に変換: 苦手=赤, 中位=オレンジ, 習得=緑。</summary>
public class MasteryToBrushConverter : IValueConverter
{
    public static readonly MasteryToBrushConverter Instance = new();

    private static readonly Brush WeakBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));     // 赤
    private static readonly Brush MidBrush = new SolidColorBrush(Color.FromRgb(0xED, 0x6C, 0x02));      // オレンジ
    private static readonly Brush StrongBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));   // 緑
    private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));  // 中性グレー

    static MasteryToBrushConverter()
    {
        WeakBrush.Freeze();
        MidBrush.Freeze();
        StrongBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int m) return DefaultBrush;
        return m switch
        {
            <= 1 => WeakBrush,
            >= 4 => StrongBrush,
            _ => MidBrush,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
