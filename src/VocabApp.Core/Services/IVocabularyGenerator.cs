using VocabApp.Core.Models;

namespace VocabApp.Core.Services;

public record VocabularyGenerationRequest(string Theme, int Count, string? Level = null);

public interface IVocabularyGenerator
{
    /// <summary>
    /// テーマ・件数等を指定して新規単語を生成する。実装はバックエンドの API キー等を
    /// 内部で参照する (未設定なら例外)。
    /// </summary>
    Task<IReadOnlyList<Word>> GenerateAsync(
        VocabularyGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// API キーが有効か疎通確認する。設定画面で「テスト接続」用に使う。
    /// 引数で渡したキーを使うので、まだ保存していない入力欄の値もテストできる。
    /// </summary>
    Task PingAsync(string apiKey, CancellationToken cancellationToken = default);
}
