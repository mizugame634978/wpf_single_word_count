using VocabApp.Core.Models;

namespace VocabApp.Core.Utilities;

/// <summary>
/// テスト回答の正誤判定。
/// 仕様 (docs/design.md §3.4):
/// - 大文字小文字を区別しない
/// - 前後空白を無視
/// - 複数訳は ';' のどれかに一致すれば正解 (緩判定)
/// </summary>
public static class AnswerJudge
{
    public static bool Judge(TestMode mode, Word word, string? userInput)
    {
        var input = (userInput ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            return false;
        }

        return mode switch
        {
            TestMode.EnglishToJapanese => MatchAny(word.Meaning, input),
            TestMode.JapaneseToEnglish => string.Equals(word.Text.Trim(), input, StringComparison.OrdinalIgnoreCase),
            TestMode.MultipleChoiceEnglishToJapanese => MatchAny(word.Meaning, input),
            TestMode.Flashcard => true, // フラッシュカードは自己申告なので判定なし
            _ => false,
        };
    }

    private static bool MatchAny(string meaningCell, string input)
    {
        if (string.IsNullOrWhiteSpace(meaningCell))
        {
            return false;
        }
        return meaningCell
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => string.Equals(candidate, input, StringComparison.OrdinalIgnoreCase));
    }
}
