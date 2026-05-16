namespace VocabApp.Core.Models;

public class TestQuestion
{
    public TestQuestion(Word word, IReadOnlyList<string>? choices = null, int correctChoiceIndex = -1)
    {
        Word = word;
        Choices = choices;
        CorrectChoiceIndex = correctChoiceIndex;
    }

    public Word Word { get; }

    /// <summary>4 択モード用の選択肢 (正解 1 + ダミー 3)。他モードでは null。</summary>
    public IReadOnlyList<string>? Choices { get; }

    /// <summary>4 択モード用の正解インデックス。他モードでは -1。</summary>
    public int CorrectChoiceIndex { get; }
}
