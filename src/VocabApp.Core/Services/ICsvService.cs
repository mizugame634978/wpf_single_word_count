using VocabApp.Core.Csv;

namespace VocabApp.Core.Services;

public interface ICsvService
{
    /// <summary>
    /// CSV をパースして <see cref="ParsedCsv"/> を返す (DB アクセスなし)。
    /// 必須列の検証と行ごとのエラー収集まで行う。LLM 生成プレビュー等で再利用するために
    /// インポート本体と分離している。
    /// </summary>
    Task<ParsedCsv> ParseAsync(
        Stream input,
        CancellationToken cancellationToken = default);

    Task<ImportResult> ImportAsync(
        Stream input,
        ConflictMode conflictMode,
        CancellationToken cancellationToken = default);

    Task ExportAsync(
        Stream output,
        ExportOptions options,
        CancellationToken cancellationToken = default);
}
