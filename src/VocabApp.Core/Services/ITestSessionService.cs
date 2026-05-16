using VocabApp.Core.Models;

namespace VocabApp.Core.Services;

public interface ITestSessionService
{
    /// <summary>
    /// 出題条件に従ってセッションを開始し、出題リストを返す。
    /// </summary>
    Task<TestSessionStartResult> StartSessionAsync(
        TestSessionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 1 問分の回答を記録し、対象 Word の習熟度 / 出題回数 / 正答数を更新する。
    /// </summary>
    Task RecordAnswerAsync(
        int sessionId,
        int wordId,
        string userInput,
        bool isCorrect,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// セッションを終了する (TestSession.EndedAt を設定)。
    /// </summary>
    Task EndSessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default);
}
