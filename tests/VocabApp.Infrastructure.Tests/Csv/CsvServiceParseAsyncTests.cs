using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VocabApp.Core.Models;
using VocabApp.Infrastructure.Csv;
using VocabApp.Infrastructure.Persistence;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Csv;

/// <summary>
/// CsvService.ParseAsync の挙動を ImportAsync とは独立に検証する。
/// LLM 生成プレビューから呼ばれる経路なので、DB を一切触らないこと・
/// Word.Tags が Tag(Name=...) で埋まることが特に重要。
/// </summary>
public class CsvServiceParseAsyncTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<VocabDbContext> _factory;

    public CsvServiceParseAsyncTests()
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

    private CsvService CreateService() => new(_factory, NullLogger<CsvService>.Instance);
    private static Stream FromString(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task ParseAsync_ReturnsRows_WithoutTouchingDb()
    {
        var csv = """
            word,meaning,part_of_speech,tags,notes
            abandon,放棄する,verb,toeic;verb,『見捨てる』のニュアンス
            boost,押し上げる,verb,business,後押しする意味
            """;

        var parsed = await CreateService().ParseAsync(FromString(csv));

        parsed.Rows.Should().HaveCount(2);
        parsed.Errors.Should().BeEmpty();

        var first = parsed.Rows[0].Word;
        first.Text.Should().Be("abandon");
        first.Meaning.Should().Be("放棄する");
        first.PartOfSpeech.Should().Be(PartOfSpeech.Verb);
        first.Notes.Should().Be("『見捨てる』のニュアンス");
        first.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "toeic", "verb" });

        await using var db = _factory.CreateDbContext();
        (await db.Words.CountAsync()).Should().Be(0);
        (await db.Tags.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ParseAsync_RequiredColumnMissing_ReportsError()
    {
        var csv = """
            word
            abandon
            """;
        var parsed = await CreateService().ParseAsync(FromString(csv));

        parsed.Rows.Should().BeEmpty();
        parsed.Errors.Should().ContainSingle(e => e.Message.Contains("meaning"));
    }

    [Fact]
    public async Task ParseAsync_BlankWordOrMeaning_BecomesPerRowError()
    {
        var csv = """
            word,meaning
            abandon,放棄する
            ,blank-word
            boost,
            """;
        var parsed = await CreateService().ParseAsync(FromString(csv));

        parsed.Rows.Should().HaveCount(1);
        parsed.Errors.Should().HaveCount(2);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<VocabDbContext>
    {
        private readonly DbContextOptions<VocabDbContext> _options;
        public TestDbContextFactory(DbContextOptions<VocabDbContext> options) => _options = options;
        public VocabDbContext CreateDbContext() => new(_options);
    }
}
