namespace VocabApp.Core.Models;

public class TestSessionOptions
{
    public TestMode Mode { get; set; } = TestMode.EnglishToJapanese;

    public TestRange Range { get; set; } = TestRange.All;

    /// <summary><see cref="Range"/> が <see cref="TestRange.Tag"/> のときの絞り込みタグ名。</summary>
    public string? TagFilter { get; set; }

    /// <summary>出題数。1 以上。実在の母集団が下回る場合は実数に丸める。</summary>
    public int Count { get; set; } = 10;

    /// <summary>
    /// 出題対象の単語 ID を明示する場合に使う (Range / TagFilter は無視)。
    /// 「間違えた単語だけ再テスト」などのリトライ用途。
    /// </summary>
    public IReadOnlyList<int>? OverrideWordIds { get; set; }
}
