using FluentAssertions;
using VocabApp.Core.Utilities;
using Xunit;

namespace VocabApp.Core.Tests.Utilities;

public class TagParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_ReturnsEmpty_ForBlankInput(string? input)
    {
        TagParser.Parse(input).Should().BeEmpty();
    }

    [Fact]
    public void Parse_SplitsBySemicolonAndComma_AndTrims()
    {
        TagParser.Parse(" toeic ; verb , business ")
            .Should().Equal("toeic", "verb", "business");
    }

    [Fact]
    public void Parse_DedupesCaseInsensitively_KeepingFirstOccurrence()
    {
        TagParser.Parse("TOEIC; toeic; Toeic; verb")
            .Should().Equal("TOEIC", "verb");
    }

    [Fact]
    public void Format_SortsCaseInsensitively_AndUsesSemicolonSpaceSeparator()
    {
        TagParser.Format(new[] { "verb", "business", "TOEIC" })
            .Should().Be("business; TOEIC; verb");
    }

    [Fact]
    public void Format_ReturnsEmptyString_ForEmptyCollection()
    {
        TagParser.Format(Array.Empty<string>()).Should().Be(string.Empty);
    }
}
