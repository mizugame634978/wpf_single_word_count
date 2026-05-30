using VocabApp.Core.Models;

namespace VocabApp.Core.Csv;

/// <summary>
/// CSV を 1 度だけパースした結果。
/// インポートと『LLM 生成プレビュー』の双方から使う。
/// </summary>
public class ParsedCsv
{
    public List<ParsedCsvRow> Rows { get; } = new();

    public List<ImportRowError> Errors { get; } = new();
}

/// <summary>
/// 1 行分のパース結果。
/// </summary>
/// <param name="LineNumber">CSV 上の行番号 (1-based; ヘッダ込み)</param>
/// <param name="Word">マップ済みの <see cref="Word"/> (Tags は名前のみ流し込んだ Tag インスタンス)</param>
/// <param name="PresentColumns">この行が属するヘッダに存在した列名集合 (大小無視)</param>
public record ParsedCsvRow(
    int LineNumber,
    Word Word,
    HashSet<string> PresentColumns);
