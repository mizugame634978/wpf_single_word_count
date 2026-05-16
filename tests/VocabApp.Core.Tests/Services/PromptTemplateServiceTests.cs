using FluentAssertions;
using VocabApp.Core.Services;
using Xunit;

namespace VocabApp.Core.Tests.Services;

public class PromptTemplateServiceTests
{
    private readonly PromptTemplateService _service = new();

    [Fact]
    public void BuildVocabularyPrompt_IncludesThemeCountAndCsvHeader()
    {
        var prompt = _service.BuildVocabularyPrompt(
            new VocabularyPromptRequest("ビジネス英語の動詞", 20));

        prompt.Should().Contain("テーマ: ビジネス英語の動詞");
        prompt.Should().Contain("件数: 20");
        prompt.Should().Contain("word,meaning,part_of_speech,example,tags,notes");
        prompt.Should().NotContain("レベル:");
    }

    [Fact]
    public void BuildVocabularyPrompt_IncludesLevel_WhenSpecified()
    {
        var prompt = _service.BuildVocabularyPrompt(
            new VocabularyPromptRequest("TOEIC 名詞", 10, "TOEIC 700"));

        prompt.Should().Contain("レベル: TOEIC 700");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildVocabularyPrompt_RejectsEmptyTheme(string? theme)
    {
        var act = () => _service.BuildVocabularyPrompt(new VocabularyPromptRequest(theme!, 10));
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildVocabularyPrompt_RejectsNonPositiveCount(int count)
    {
        var act = () => _service.BuildVocabularyPrompt(new VocabularyPromptRequest("x", count));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
