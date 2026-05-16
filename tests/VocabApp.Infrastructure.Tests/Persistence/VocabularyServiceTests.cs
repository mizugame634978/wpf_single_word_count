using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocabApp.Core.Models;
using VocabApp.Infrastructure.Persistence;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Persistence;

public class VocabularyServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<VocabDbContext> _factory;

    public VocabularyServiceTests()
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

    [Fact]
    public async Task AddAsync_StoresWordWithTags_AndSetsTimestamps()
    {
        var service = new VocabularyService(_factory);

        var saved = await service.AddAsync(new Word
        {
            Text = "abandon",
            Meaning = "放棄する",
            PartOfSpeech = PartOfSpeech.Verb,
            Tags = new List<Tag> { new() { Name = "toeic" }, new() { Name = "verb" } },
        });

        saved.Id.Should().BeGreaterThan(0);
        saved.CreatedAt.Should().NotBe(default);
        saved.UpdatedAt.Should().NotBe(default);

        await using var db = _factory.CreateDbContext();
        var stored = await db.Words.Include(w => w.Tags).SingleAsync();
        stored.Text.Should().Be("abandon");
        stored.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "toeic", "verb" });
    }

    [Fact]
    public async Task AddAsync_ReusesExistingTagsByName_CaseInsensitive()
    {
        var service = new VocabularyService(_factory);

        await service.AddAsync(new Word
        {
            Text = "abandon",
            Meaning = "放棄する",
            Tags = new List<Tag> { new() { Name = "TOEIC" } },
        });

        await service.AddAsync(new Word
        {
            Text = "boost",
            Meaning = "高める",
            Tags = new List<Tag> { new() { Name = "toeic" }, new() { Name = "verb" } },
        });

        await using var db = _factory.CreateDbContext();
        var tags = await db.Tags.OrderBy(t => t.Name).ToListAsync();
        tags.Should().HaveCount(2);
        tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "TOEIC", "verb" });

        var boost = await db.Words.Include(w => w.Tags).SingleAsync(w => w.Text == "boost");
        boost.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "TOEIC", "verb" });
    }

    [Fact]
    public async Task UpdateAsync_UpdatesScalarFields_AndReconcilesTags()
    {
        var service = new VocabularyService(_factory);
        var saved = await service.AddAsync(new Word
        {
            Text = "abandon",
            Meaning = "放棄する",
            Tags = new List<Tag> { new() { Name = "toeic" }, new() { Name = "verb" } },
        });

        var edited = new Word
        {
            Id = saved.Id,
            Text = "abandon",
            Meaning = "見捨てる",
            PartOfSpeech = PartOfSpeech.Verb,
            Example = "He abandoned the plan.",
            Tags = new List<Tag> { new() { Name = "verb" }, new() { Name = "business" } },
        };
        await service.UpdateAsync(edited);

        await using var db = _factory.CreateDbContext();
        var stored = await db.Words.Include(w => w.Tags).SingleAsync();
        stored.Meaning.Should().Be("見捨てる");
        stored.Example.Should().Be("He abandoned the plan.");
        stored.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "verb", "business" });

        // 元の "toeic" タグはほかの単語が参照していないが、Tag 行自体は残る (将来の掃除は別タスク)
        var tagNames = await db.Tags.Select(t => t.Name).ToListAsync();
        tagNames.Should().Contain("toeic");
    }

    [Fact]
    public async Task UpdateAsync_DoesNotResetLearningStats()
    {
        var service = new VocabularyService(_factory);
        var saved = await service.AddAsync(new Word { Text = "abandon", Meaning = "放棄する" });

        await using (var db = _factory.CreateDbContext())
        {
            var entity = await db.Words.SingleAsync();
            entity.TimesAsked = 5;
            entity.TimesCorrect = 3;
            entity.Mastery = 2;
            entity.LastAskedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        await service.UpdateAsync(new Word
        {
            Id = saved.Id,
            Text = "abandon",
            Meaning = "見捨てる",
        });

        await using var verify = _factory.CreateDbContext();
        var stored = await verify.Words.SingleAsync();
        stored.Meaning.Should().Be("見捨てる");
        stored.TimesAsked.Should().Be(5);
        stored.TimesCorrect.Should().Be(3);
        stored.Mastery.Should().Be(2);
        stored.LastAskedAt.Should().Be(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task DeleteAsync_RemovesWord()
    {
        var service = new VocabularyService(_factory);
        var saved = await service.AddAsync(new Word { Text = "abandon", Meaning = "放棄する" });

        await service.DeleteAsync(saved.Id);

        await using var db = _factory.CreateDbContext();
        (await db.Words.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_IncludesTags_AndOrdersByText()
    {
        var service = new VocabularyService(_factory);
        await service.AddAsync(new Word { Text = "zoom", Meaning = "拡大する" });
        await service.AddAsync(new Word
        {
            Text = "abandon",
            Meaning = "放棄する",
            Tags = new List<Tag> { new() { Name = "verb" } },
        });

        var all = await service.GetAllAsync();
        all.Select(w => w.Text).Should().Equal("abandon", "zoom");
        all.First().Tags.Should().ContainSingle(t => t.Name == "verb");
    }

    private sealed class TestDbContextFactory : IDbContextFactory<VocabDbContext>
    {
        private readonly DbContextOptions<VocabDbContext> _options;
        public TestDbContextFactory(DbContextOptions<VocabDbContext> options) => _options = options;
        public VocabDbContext CreateDbContext() => new(_options);
    }
}
