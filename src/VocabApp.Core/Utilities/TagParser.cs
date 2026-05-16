namespace VocabApp.Core.Utilities;

public static class TagParser
{
    private static readonly char[] Separators = { ';', ',' };

    public static IReadOnlyList<string> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        return input
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string Format(IEnumerable<string> tagNames)
    {
        return string.Join("; ", tagNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
    }
}
