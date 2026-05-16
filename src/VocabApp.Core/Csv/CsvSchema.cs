namespace VocabApp.Core.Csv;

/// <summary>
/// CSV v1 のヘッダ列名定義。
/// </summary>
public static class CsvSchema
{
    public const string Word = "word";
    public const string Meaning = "meaning";
    public const string PartOfSpeech = "part_of_speech";
    public const string Example = "example";
    public const string Tags = "tags";
    public const string Notes = "notes";
    public const string TimesAsked = "times_asked";
    public const string TimesCorrect = "times_correct";
    public const string LastAskedAt = "last_asked_at";
    public const string Mastery = "mastery";

    public static readonly IReadOnlyList<string> RequiredColumns = new[] { Word, Meaning };

    public static readonly IReadOnlyList<string> AllColumns = new[]
    {
        Word, Meaning, PartOfSpeech, Example, Tags, Notes,
        TimesAsked, TimesCorrect, LastAskedAt, Mastery,
    };

    /// <summary>
    /// セル内のタグ区切り (CSV 仕様)。
    /// </summary>
    public const char InCellTagSeparator = ';';
}
