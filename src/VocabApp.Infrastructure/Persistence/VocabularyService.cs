using Microsoft.EntityFrameworkCore;
using VocabApp.Core.Models;
using VocabApp.Core.Services;

namespace VocabApp.Infrastructure.Persistence;

public class VocabularyService : IVocabularyService
{
    private readonly IDbContextFactory<VocabDbContext> _factory;

    public VocabularyService(IDbContextFactory<VocabDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Word>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Words
            .Include(w => w.Tags)
            .AsNoTracking()
            .OrderBy(w => w.Text)
            .ToListAsync(cancellationToken);
    }

    public async Task<Word?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Words
            .Include(w => w.Tags)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Word> AddAsync(Word word, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;
        word.Id = 0;
        word.CreatedAt = now;
        word.UpdatedAt = now;
        word.Tags = await ResolveTagsAsync(db, word.Tags.Select(t => t.Name), cancellationToken);

        db.Words.Add(word);
        await db.SaveChangesAsync(cancellationToken);
        return word;
    }

    public async Task UpdateAsync(Word word, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Words
            .Include(w => w.Tags)
            .FirstOrDefaultAsync(w => w.Id == word.Id, cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException($"Word with id={word.Id} was not found.");
        }

        existing.Text = word.Text;
        existing.Meaning = word.Meaning;
        existing.PartOfSpeech = word.PartOfSpeech;
        existing.Example = word.Example;
        existing.Notes = word.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        // TimesAsked / TimesCorrect / LastAskedAt / Mastery は学習中の更新であり
        // 編集ダイアログ経由では触らない。
        existing.Tags = await ResolveTagsAsync(db, word.Tags.Select(t => t.Name), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Words.FindAsync(new object?[] { id }, cancellationToken);
        if (entity is null)
        {
            return;
        }
        db.Words.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<Tag>> ResolveTagsAsync(
        VocabDbContext db,
        IEnumerable<string> tagNames,
        CancellationToken cancellationToken)
    {
        var distinct = tagNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
        {
            return new List<Tag>();
        }

        var existing = await db.Tags
            .Where(t => distinct.Contains(t.Name))
            .ToListAsync(cancellationToken);

        var existingByName = existing.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<Tag>(distinct.Count);
        foreach (var name in distinct)
        {
            if (existingByName.TryGetValue(name, out var tag))
            {
                result.Add(tag);
            }
            else
            {
                result.Add(new Tag { Name = name });
            }
        }
        return result;
    }
}
