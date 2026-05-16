using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocabApp.Core.Models;
using VocabApp.Infrastructure.Persistence;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Persistence;

public class VocabDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<VocabDbContext> _options;

    public VocabDbContextTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<VocabDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new VocabDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task EnsureCreated_BuildsSchema()
    {
        using var db = new VocabDbContext(_options);
        (await db.Words.CountAsync()).Should().Be(0);
        (await db.Tags.CountAsync()).Should().Be(0);
        (await db.TestSessions.CountAsync()).Should().Be(0);
        (await db.TestAnswers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Word_CanRoundTripWithTags()
    {
        await using (var db = new VocabDbContext(_options))
        {
            var word = new Word
            {
                Text = "abandon",
                Meaning = "放棄する",
                PartOfSpeech = PartOfSpeech.Verb,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Tags = new List<Tag> { new() { Name = "toeic" } },
            };
            db.Words.Add(word);
            await db.SaveChangesAsync();
        }

        await using (var db = new VocabDbContext(_options))
        {
            var loaded = await db.Words.Include(w => w.Tags).SingleAsync();
            loaded.Text.Should().Be("abandon");
            loaded.PartOfSpeech.Should().Be(PartOfSpeech.Verb);
            loaded.Tags.Should().ContainSingle(t => t.Name == "toeic");
        }
    }
}
