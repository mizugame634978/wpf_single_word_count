using FluentAssertions;
using VocabApp.Core.Models;
using VocabApp.Core.Utilities;
using Xunit;

namespace VocabApp.Core.Tests.Utilities;

public class PartOfSpeechConverterTests
{
    [Theory]
    [InlineData("noun", PartOfSpeech.Noun)]
    [InlineData("NOUN", PartOfSpeech.Noun)]
    [InlineData("n", PartOfSpeech.Noun)]
    [InlineData("verb", PartOfSpeech.Verb)]
    [InlineData("v", PartOfSpeech.Verb)]
    [InlineData("adj", PartOfSpeech.Adjective)]
    [InlineData("adjective", PartOfSpeech.Adjective)]
    [InlineData("adv", PartOfSpeech.Adverb)]
    [InlineData("phrase", PartOfSpeech.Phrase)]
    public void Parse_RecognisesShortAndLongForms_CaseInsensitive(string input, PartOfSpeech expected)
    {
        PartOfSpeechConverter.Parse(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("xxx")]
    public void Parse_ReturnsNullForBlankOrUnknown(string? input)
    {
        PartOfSpeechConverter.Parse(input).Should().BeNull();
    }

    [Theory]
    [InlineData(PartOfSpeech.Noun, "noun")]
    [InlineData(PartOfSpeech.Verb, "verb")]
    [InlineData(PartOfSpeech.Adjective, "adj")]
    [InlineData(PartOfSpeech.Adverb, "adv")]
    public void Format_UsesShortLowercaseForm(PartOfSpeech value, string expected)
    {
        PartOfSpeechConverter.Format(value).Should().Be(expected);
    }

    [Fact]
    public void Format_ReturnsEmptyForNull()
    {
        PartOfSpeechConverter.Format(null).Should().Be(string.Empty);
    }
}
