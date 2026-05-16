using FluentAssertions;
using VocabApp.Core.Models;
using VocabApp.Core.Utilities;
using Xunit;

namespace VocabApp.Core.Tests.Utilities;

public class AnswerJudgeTests
{
    private static Word Make(string text, string meaning) => new() { Text = text, Meaning = meaning };

    [Fact]
    public void EnJp_AcceptsExactMatch_CaseInsensitive()
    {
        AnswerJudge.Judge(TestMode.EnglishToJapanese, Make("abandon", "放棄する"), "放棄する")
            .Should().BeTrue();
    }

    [Fact]
    public void EnJp_AcceptsAnyOfSemicolonSeparated()
    {
        var word = Make("abandon", "放棄する; 見捨てる; 諦める");
        AnswerJudge.Judge(TestMode.EnglishToJapanese, word, "見捨てる").Should().BeTrue();
        AnswerJudge.Judge(TestMode.EnglishToJapanese, word, "諦める").Should().BeTrue();
    }

    [Fact]
    public void EnJp_TrimsWhitespace()
    {
        AnswerJudge.Judge(TestMode.EnglishToJapanese, Make("abandon", "放棄する"), "  放棄する  ")
            .Should().BeTrue();
    }

    [Fact]
    public void EnJp_RejectsMismatch()
    {
        AnswerJudge.Judge(TestMode.EnglishToJapanese, Make("abandon", "放棄する"), "捨てる")
            .Should().BeFalse();
    }

    [Fact]
    public void EnJp_RejectsEmptyInput()
    {
        AnswerJudge.Judge(TestMode.EnglishToJapanese, Make("abandon", "放棄する"), string.Empty)
            .Should().BeFalse();
        AnswerJudge.Judge(TestMode.EnglishToJapanese, Make("abandon", "放棄する"), "   ")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("abandon", true)]
    [InlineData("ABANDON", true)]
    [InlineData("  Abandon  ", true)]
    [InlineData("boost", false)]
    public void JpEn_CaseAndWhitespaceInsensitive(string input, bool expected)
    {
        AnswerJudge.Judge(TestMode.JapaneseToEnglish, Make("abandon", "放棄する"), input)
            .Should().Be(expected);
    }

    [Fact]
    public void Mc_BehavesLikeEnJp()
    {
        AnswerJudge.Judge(TestMode.MultipleChoiceEnglishToJapanese,
                Make("abandon", "放棄する"), "放棄する")
            .Should().BeTrue();
    }

    [Fact]
    public void Flashcard_AlwaysTrue_BecauseSelfReported()
    {
        AnswerJudge.Judge(TestMode.Flashcard, Make("abandon", "放棄する"), "anything")
            .Should().BeTrue();
    }
}
