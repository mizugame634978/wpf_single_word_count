using FluentAssertions;
using VocabApp.Core.Utilities;
using Xunit;

namespace VocabApp.Core.Tests.Utilities;

public class MasteryRuleTests
{
    [Theory]
    [InlineData(0, true, 1)]
    [InlineData(2, true, 3)]
    [InlineData(5, true, 5)]   // 上限張り付き
    [InlineData(3, false, 2)]
    [InlineData(0, false, 0)]  // 下限張り付き
    public void NextMastery_AppliesDeltaWithClamp(int current, bool correct, int expected)
    {
        MasteryRule.NextMastery(current, correct).Should().Be(expected);
    }
}
