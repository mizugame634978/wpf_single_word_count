using FluentAssertions;
using VocabApp.Core.Utilities;
using Xunit;

namespace VocabApp.Core.Tests.Utilities;

public class ExceptionFormatterTests
{
    [Fact]
    public void Format_SingleException_RendersTypeAndMessage()
    {
        var ex = new InvalidOperationException("outer");

        var s = ExceptionFormatter.Format(ex);

        s.Should().Be("InvalidOperationException: outer");
    }

    [Fact]
    public void Format_WithInnerException_RendersChainIndented()
    {
        var inner = new ArgumentException("inner");
        var outer = new InvalidOperationException("outer", inner);

        var s = ExceptionFormatter.Format(outer);

        s.Should().Contain("InvalidOperationException: outer");
        s.Should().Contain("ArgumentException: inner");
        s.Should().Contain("↳");
    }

    [Fact]
    public void Format_RendersAllLevelsOfNesting()
    {
        var l3 = new Exception("l3");
        var l2 = new Exception("l2", l3);
        var l1 = new Exception("l1", l2);

        var s = ExceptionFormatter.Format(l1);

        s.Should().Contain("l1");
        s.Should().Contain("l2");
        s.Should().Contain("l3");
    }
}
