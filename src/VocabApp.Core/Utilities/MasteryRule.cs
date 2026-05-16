namespace VocabApp.Core.Utilities;

/// <summary>
/// 習熟度更新の規則 (docs/design.md §3.5)。
/// - 正解で +1 (上限 5)
/// - 不正解で -1 (下限 0)
/// </summary>
public static class MasteryRule
{
    public const int Min = 0;
    public const int Max = 5;

    public static int NextMastery(int current, bool isCorrect)
    {
        var delta = isCorrect ? +1 : -1;
        return Math.Clamp(current + delta, Min, Max);
    }
}
