using FluentAssertions;
using VocabApp.Core.Models;
using Xunit;

namespace VocabApp.Core.Tests.Models;

public class WordTests
{
    [Fact]
    public void Word_Defaults_AreSafe()
    {
        var word = new Word();

        word.Text.Should().Be(string.Empty);
        word.Meaning.Should().Be(string.Empty);
        word.Tags.Should().NotBeNull().And.BeEmpty();
        word.TimesAsked.Should().Be(0);
        word.TimesCorrect.Should().Be(0);
        word.Mastery.Should().Be(0);
    }
}
