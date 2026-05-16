namespace VocabApp.Core.Models;

public enum TestRange
{
    /// <summary>全単語</summary>
    All,

    /// <summary>指定タグの単語のみ</summary>
    Tag,

    /// <summary>苦手 (mastery &lt;= 1 もしくは直近の正答率 &lt; 70%)</summary>
    Weak,

    /// <summary>未出題 (TimesAsked == 0)</summary>
    Unasked,
}
