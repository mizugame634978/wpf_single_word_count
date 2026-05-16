using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VocabApp.Core.Csv;
using VocabApp.Core.Models;
using VocabApp.Infrastructure.Csv;
using VocabApp.Infrastructure.Persistence;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Csv;

public class CsvServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<VocabDbContext> _factory;

    public CsvServiceTests()
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

    private static Stream FromString(string content)
        => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task ImportAsync_AddsRows_WithMinimumColumns()
    {
        var csv = """
            word,meaning
            abandon,放棄する
            boost,高める
            """;

        var result = await CreateService().ImportAsync(FromString(csv), ConflictMode.Skip);

        result.Added.Should().Be(2);
        result.Updated.Should().Be(0);
        result.Skipped.Should().Be(0);
        result.Errors.Should().BeEmpty();

        await using var db = _factory.CreateDbContext();
        (await db.Words.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ImportAsync_HandlesHeaderOrder_AndOptionalColumns()
    {
        var csv = """
            meaning,word,tags,part_of_speech
            放棄する,abandon,toeic;verb,verb
            """;

        var result = await CreateService().ImportAsync(FromString(csv), ConflictMode.Skip);
        result.Added.Should().Be(1);
        result.Errors.Should().BeEmpty();

        await using var db = _factory.CreateDbContext();
        var w = await db.Words.Include(x => x.Tags).SingleAsync();
        w.Text.Should().Be("abandon");
        w.Meaning.Should().Be("放棄する");
        w.PartOfSpeech.Should().Be(PartOfSpeech.Verb);
        w.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "toeic", "verb" });
    }

    [Fact]
    public async Task ImportAsync_ReportsError_WhenRequiredColumnMissing()
    {
        var csv = """
            word
            abandon
            """;
        var result = await CreateService().ImportAsync(FromString(csv), ConflictMode.Skip);

        result.Errors.Should().ContainSingle(e => e.Message.Contains("meaning"));
        result.Added.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_SkipMode_KeepsExistingUntouched()
    {
        var first = """
            word,meaning,times_asked
            abandon,放棄する,5
            """;
        await CreateService().ImportAsync(FromString(first), ConflictMode.Skip);

        var second = """
            word,meaning,times_asked
            abandon,捨てる,99
            """;
        var result = await CreateService().ImportAsync(FromString(second), ConflictMode.Skip);

        result.Skipped.Should().Be(1);
        result.Added.Should().Be(0);

        await using var db = _factory.CreateDbContext();
        var w = await db.Words.SingleAsync();
        w.Meaning.Should().Be("放棄する");
        w.TimesAsked.Should().Be(5);
    }

    [Fact]
    public async Task ImportAsync_OverwriteMode_UpdatesScalars_AndStatsIfColumnPresent()
    {
        var first = """
            word,meaning,times_asked
            abandon,放棄する,5
            """;
        await CreateService().ImportAsync(FromString(first), ConflictMode.Skip);

        var second = """
            word,meaning,times_asked
            abandon,捨てる,99
            """;
        var result = await CreateService().ImportAsync(FromString(second), ConflictMode.Overwrite);
        result.Updated.Should().Be(1);

        await using var db = _factory.CreateDbContext();
        var w = await db.Words.SingleAsync();
        w.Meaning.Should().Be("捨てる");
        w.TimesAsked.Should().Be(99);
    }

    [Fact]
    public async Task ImportAsync_OverwriteMode_PreservesStats_WhenStatsColumnMissing()
    {
        var first = """
            word,meaning,times_asked,mastery
            abandon,放棄する,5,3
            """;
        await CreateService().ImportAsync(FromString(first), ConflictMode.Skip);

        var second = """
            word,meaning
            abandon,捨てる
            """;
        var result = await CreateService().ImportAsync(FromString(second), ConflictMode.Overwrite);
        result.Updated.Should().Be(1);

        await using var db = _factory.CreateDbContext();
        var w = await db.Words.SingleAsync();
        w.Meaning.Should().Be("捨てる");
        w.TimesAsked.Should().Be(5);
        w.Mastery.Should().Be(3);
    }

    [Fact]
    public async Task ImportAsync_AddAsNewMode_CreatesDuplicate()
    {
        var first = """
            word,meaning
            abandon,放棄する
            """;
        await CreateService().ImportAsync(FromString(first), ConflictMode.Skip);

        var second = """
            word,meaning
            abandon,捨てる
            """;
        var result = await CreateService().ImportAsync(FromString(second), ConflictMode.AddAsNew);
        result.Added.Should().Be(1);

        await using var db = _factory.CreateDbContext();
        var words = await db.Words.OrderBy(w => w.Meaning).ToListAsync();
        words.Should().HaveCount(2);
        words.Select(w => w.Meaning).Should().BeEquivalentTo(new[] { "放棄する", "捨てる" });
    }

    [Fact]
    public async Task ImportAsync_RowWithBlankWordOrMeaning_BecomesError()
    {
        var csv = """
            word,meaning
            ,放棄する
            boost,
            normal,通常
            """;
        var result = await CreateService().ImportAsync(FromString(csv), ConflictMode.Skip);

        result.Added.Should().Be(1);
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExportAsync_WritesHeader_AndAllWords_Sorted()
    {
        await using (var db = _factory.CreateDbContext())
        {
            db.Words.AddRange(
                new Word { Text = "zoom", Meaning = "拡大する" },
                new Word { Text = "abandon", Meaning = "放棄する",
                    PartOfSpeech = PartOfSpeech.Verb,
                    Tags = new List<Tag> { new() { Name = "toeic" } },
                    TimesAsked = 3, TimesCorrect = 2 });
            await db.SaveChangesAsync();
        }

        var stream = new MemoryStream();
        await CreateService().ExportAsync(stream, new ExportOptions { IncludeLearningStats = true });
        stream.Position = 0;
        var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().Be("word,meaning,part_of_speech,example,tags,notes,times_asked,times_correct,last_asked_at,mastery");
        lines[1].Should().StartWith("abandon,放棄する,verb,,toeic,,3,2,");
        lines[2].Should().StartWith("zoom,拡大する,,,,,0,0,");
    }

    [Fact]
    public async Task ExportAsync_OmitsStatsColumns_WhenOptIn_IsFalse()
    {
        await using (var db = _factory.CreateDbContext())
        {
            db.Words.Add(new Word { Text = "abandon", Meaning = "放棄する" });
            await db.SaveChangesAsync();
        }

        var stream = new MemoryStream();
        await CreateService().ExportAsync(stream, new ExportOptions { IncludeLearningStats = false });
        stream.Position = 0;
        var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        text.Should().Contain("word,meaning,part_of_speech,example,tags,notes");
        text.Should().NotContain("times_asked");
    }

    [Fact]
    public async Task RoundTrip_ExportThenImport_PreservesContent()
    {
        await using (var db = _factory.CreateDbContext())
        {
            db.Words.Add(new Word
            {
                Text = "abandon",
                Meaning = "放棄する; 見捨てる",
                PartOfSpeech = PartOfSpeech.Verb,
                Example = "He, regrettably, abandoned the plan.",
                Notes = "TOEIC 頻出",
                Tags = new List<Tag> { new() { Name = "toeic" }, new() { Name = "verb" } },
                TimesAsked = 4, TimesCorrect = 3, Mastery = 2,
                LastAskedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync();
        }

        // Export
        var exportStream = new MemoryStream();
        await CreateService().ExportAsync(exportStream, new ExportOptions { IncludeLearningStats = true });

        // Wipe DB and import
        await using (var db = _factory.CreateDbContext())
        {
            db.Words.RemoveRange(db.Words);
            db.Tags.RemoveRange(db.Tags);
            await db.SaveChangesAsync();
        }

        exportStream.Position = 0;
        var result = await CreateService().ImportAsync(exportStream, ConflictMode.Skip);
        result.Added.Should().Be(1);
        result.Errors.Should().BeEmpty();

        await using var verify = _factory.CreateDbContext();
        var w = await verify.Words.Include(x => x.Tags).SingleAsync();
        w.Text.Should().Be("abandon");
        w.Meaning.Should().Be("放棄する; 見捨てる");
        w.PartOfSpeech.Should().Be(PartOfSpeech.Verb);
        w.Example.Should().Be("He, regrettably, abandoned the plan.");
        w.Notes.Should().Be("TOEIC 頻出");
        w.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "toeic", "verb" });
        w.TimesAsked.Should().Be(4);
        w.TimesCorrect.Should().Be(3);
        w.Mastery.Should().Be(2);
        w.LastAskedAt.Should().Be(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    private sealed class TestDbContextFactory : IDbContextFactory<VocabDbContext>
    {
        private readonly DbContextOptions<VocabDbContext> _options;
        public TestDbContextFactory(DbContextOptions<VocabDbContext> options) => _options = options;
        public VocabDbContext CreateDbContext() => new(_options);
    }
}
