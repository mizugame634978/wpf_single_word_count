using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocabApp.Core.Models;
using VocabApp.Infrastructure.Persistence;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Persistence;

public class TestSessionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<VocabDbContext> _factory;

    public TestSessionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<VocabDbContext>()
            .UseSqlite(_connection)
            .Options;
        _factory = new TestDbContextFactory(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private TestSessionService CreateService() => new(_factory);

    private async Task SeedAsync(params (string text, string meaning, int mastery, int asked, int correct, string[] tags)[] words)
    {
        await using var db = _factory.CreateDbContext();
        foreach (var (text, meaning, mastery, asked, correct, tags) in words)
        {
            db.Words.Add(new Word
            {
                Text = text,
                Meaning = meaning,
                Mastery = mastery,
                TimesAsked = asked,
                TimesCorrect = correct,
                Tags = tags.Select(t => new Tag { Name = t }).ToList(),
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task StartSessionAsync_All_PicksUpToCount()
    {
        await SeedAsync(
            ("a", "あ", 0, 0, 0, Array.Empty<string>()),
            ("b", "い", 0, 0, 0, Array.Empty<string>()),
            ("c", "う", 0, 0, 0, Array.Empty<string>()));
        var result = await CreateService().StartSessionAsync(new TestSessionOptions
        {
            Mode = TestMode.EnglishToJapanese, Range = TestRange.All, Count = 2,
        });
        result.Questions.Should().HaveCount(2);
        result.Session.Id.Should().BeGreaterThan(0);
        result.Session.Mode.Should().Be(TestMode.EnglishToJapanese);
    }

    [Fact]
    public async Task StartSessionAsync_All_AcceptsFewerCandidatesThanRequested()
    {
        await SeedAsync(("a", "あ", 0, 0, 0, Array.Empty<string>()));
        var result = await CreateService().StartSessionAsync(new TestSessionOptions { Count = 10 });
        result.Questions.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartSessionAsync_Throws_WhenNoCandidates()
    {
        var act = async () => await CreateService().StartSessionAsync(new TestSessionOptions { Count = 5 });
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*見つかりません*");
    }

    [Fact]
    public async Task StartSessionAsync_Weak_FiltersByMasteryOrAccuracy()
    {
        await SeedAsync(
            ("low_mastery", "low", 1, 10, 10, Array.Empty<string>()),         // mastery <= 1
            ("low_accuracy", "lo", 4, 10, 5, Array.Empty<string>()),          // accuracy 50%
            ("strong", "strong", 5, 10, 10, Array.Empty<string>()));          // 除外される
        var result = await CreateService().StartSessionAsync(new TestSessionOptions
        {
            Range = TestRange.Weak, Count = 10,
        });
        result.Questions.Select(q => q.Word.Text)
            .Should().BeEquivalentTo(new[] { "low_mastery", "low_accuracy" });
    }

    [Fact]
    public async Task StartSessionAsync_Unasked_PicksOnlyTimesAskedZero()
    {
        await SeedAsync(
            ("fresh", "f", 0, 0, 0, Array.Empty<string>()),
            ("used", "u", 0, 3, 1, Array.Empty<string>()));
        var result = await CreateService().StartSessionAsync(new TestSessionOptions
        {
            Range = TestRange.Unasked, Count = 10,
        });
        result.Questions.Should().ContainSingle(q => q.Word.Text == "fresh");
    }

    [Fact]
    public async Task StartSessionAsync_Tag_FiltersByTagName()
    {
        await SeedAsync(
            ("a", "あ", 0, 0, 0, new[] { "toeic" }),
            ("b", "い", 0, 0, 0, new[] { "daily" }));
        var result = await CreateService().StartSessionAsync(new TestSessionOptions
        {
            Range = TestRange.Tag, TagFilter = "toeic", Count = 10,
        });
        result.Questions.Should().ContainSingle(q => q.Word.Text == "a");
    }

    [Fact]
    public async Task StartSessionAsync_OverrideWordIds_IgnoresRange()
    {
        await SeedAsync(
            ("fresh", "f", 5, 0, 0, Array.Empty<string>()),
            ("used", "u", 5, 3, 3, Array.Empty<string>()));
        int freshId;
        await using (var db = _factory.CreateDbContext())
        {
            freshId = (await db.Words.SingleAsync(w => w.Text == "fresh")).Id;
        }
        var result = await CreateService().StartSessionAsync(new TestSessionOptions
        {
            Range = TestRange.Weak,        // 普通なら mastery 5 で除外されるはず
            Count = 10,
            OverrideWordIds = new[] { freshId },
        });
        result.Questions.Should().ContainSingle(q => q.Word.Text == "fresh");
    }

    [Fact]
    public async Task StartSessionAsync_MultipleChoice_BuildsFourChoicesIncludingCorrect()
    {
        await SeedAsync(
            ("a", "あ", 0, 0, 0, Array.Empty<string>()),
            ("b", "い", 0, 0, 0, Array.Empty<string>()),
            ("c", "う", 0, 0, 0, Array.Empty<string>()),
            ("d", "え", 0, 0, 0, Array.Empty<string>()),
            ("e", "お", 0, 0, 0, Array.Empty<string>()));
        var result = await CreateService().StartSessionAsync(new TestSessionOptions
        {
            Mode = TestMode.MultipleChoiceEnglishToJapanese, Count = 1,
        });
        var q = result.Questions[0];
        q.Choices.Should().NotBeNull();
        q.Choices!.Should().HaveCount(4);
        q.Choices!.Should().Contain(q.Word.Meaning);
        q.CorrectChoiceIndex.Should().Be(q.Choices!.ToList().IndexOf(q.Word.Meaning));
    }

    [Fact]
    public async Task RecordAnswerAsync_UpdatesStatsAndMastery()
    {
        await SeedAsync(("a", "あ", 2, 0, 0, Array.Empty<string>()));
        var service = CreateService();
        var start = await service.StartSessionAsync(new TestSessionOptions { Count = 1 });
        var wordId = start.Questions[0].Word.Id;

        await service.RecordAnswerAsync(start.Session.Id, wordId, "あ", isCorrect: true);

        await using var db = _factory.CreateDbContext();
        var word = await db.Words.SingleAsync();
        word.TimesAsked.Should().Be(1);
        word.TimesCorrect.Should().Be(1);
        word.Mastery.Should().Be(3);
        word.LastAskedAt.Should().NotBeNull();

        var answer = await db.TestAnswers.SingleAsync();
        answer.WordId.Should().Be(wordId);
        answer.IsCorrect.Should().BeTrue();
        answer.UserInput.Should().Be("あ");
    }

    [Fact]
    public async Task RecordAnswerAsync_WrongAnswer_DecrementsMasteryWithFloor()
    {
        await SeedAsync(("a", "あ", 0, 0, 0, Array.Empty<string>()));
        var service = CreateService();
        var start = await service.StartSessionAsync(new TestSessionOptions { Count = 1 });
        var wordId = start.Questions[0].Word.Id;

        await service.RecordAnswerAsync(start.Session.Id, wordId, "wrong", isCorrect: false);

        await using var db = _factory.CreateDbContext();
        var word = await db.Words.SingleAsync();
        word.TimesAsked.Should().Be(1);
        word.TimesCorrect.Should().Be(0);
        word.Mastery.Should().Be(0);   // 下限張り付き
    }

    [Fact]
    public async Task EndSessionAsync_SetsEndedAt()
    {
        await SeedAsync(("a", "あ", 0, 0, 0, Array.Empty<string>()));
        var service = CreateService();
        var start = await service.StartSessionAsync(new TestSessionOptions { Count = 1 });

        await service.EndSessionAsync(start.Session.Id);

        await using var db = _factory.CreateDbContext();
        var s = await db.TestSessions.SingleAsync();
        s.EndedAt.Should().NotBeNull();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<VocabDbContext>
    {
        private readonly DbContextOptions<VocabDbContext> _options;
        public TestDbContextFactory(DbContextOptions<VocabDbContext> options) => _options = options;
        public VocabDbContext CreateDbContext() => new(_options);
    }
}
