namespace VocabApp.Core.Models;

public class TestAnswer
{
    public int Id { get; set; }

    public int TestSessionId { get; set; }

    public TestSession? TestSession { get; set; }

    public int WordId { get; set; }

    public Word? Word { get; set; }

    public string UserInput { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public DateTime AnsweredAt { get; set; }
}
