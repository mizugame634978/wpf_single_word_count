using System.Collections;
using System.Globalization;
using System.Windows.Data;
using VocabApp.Core.Models;
using VocabApp.Core.Utilities;

namespace VocabApp.Wpf.Converters;

public class TagsToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable<Tag> tags)
        {
            return TagParser.Format(tags.Select(t => t.Name));
        }
        if (value is IEnumerable enumerable)
        {
            return TagParser.Format(enumerable.Cast<Tag>().Select(t => t.Name));
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
