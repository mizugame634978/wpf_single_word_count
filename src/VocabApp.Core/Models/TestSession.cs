namespace VocabApp.Core.Models;

public class TestSession
{
    public int Id { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public TestMode Mode { get; set; }

    public List<TestAnswer> Answers { get; set; } = new();
}
