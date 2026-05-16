namespace VocabApp.Core.Models;

public class TestSessionStartResult
{
    public TestSessionStartResult(TestSession session, IReadOnlyList<TestQuestion> questions)
    {
        Session = session;
        Questions = questions;
    }

    public TestSession Session { get; }

    public IReadOnlyList<TestQuestion> Questions { get; }
}
