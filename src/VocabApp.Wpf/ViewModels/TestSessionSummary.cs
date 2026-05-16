using VocabApp.Core.Models;

namespace VocabApp.Wpf.ViewModels;

public class TestSessionSummary
{
    public TestSessionSummary(
        TestSessionOptions options,
        IReadOnlyList<TestAnsweredQuestion> answered)
    {
        Options = options;
        Answered = answered;
    }

    public TestSessionOptions Options { get; }

    public IReadOnlyList<TestAnsweredQuestion> Answered { get; }

    public int Total => Answered.Count;

    public int Correct => Answered.Count(a => a.IsCorrect);

    public int Wrong => Total - Correct;

    public IReadOnlyList<TestAnsweredQuestion> WrongAnswers =>
        Answered.Where(a => !a.IsCorrect).ToList();
}

public record TestAnsweredQuestion(Word Word, string UserInput, bool IsCorrect);
