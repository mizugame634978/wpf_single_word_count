namespace VocabApp.Core.Csv;

public class ExportOptions
{
    /// <summary>true の場合、学習統計列 (times_asked / times_correct / last_asked_at / mastery)
    /// を出力に含める。false の場合は出さない (シンプルな単語+和訳 CSV になる)。</summary>
    public bool IncludeLearningStats { get; set; } = true;
}
