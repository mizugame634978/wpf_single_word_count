using Microsoft.EntityFrameworkCore;
using VocabApp.Core.Models;
using VocabApp.Core.Services;

namespace VocabApp.Infrastructure.Persistence;

public class VocabularyService : IVocabularyService
{
    private readonly VocabDbContext _db;

    public VocabularyService(VocabDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Word>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Words
            .Include(w => w.Tags)
            .AsNoTracking()
            .OrderBy(w => w.Text)
            .ToListAsync(cancellationToken);
    }

    public async Task<Word?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Words
            .Include(w => w.Tags)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Word> AddAsync(Word word, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        word.CreatedAt = now;
        word.UpdatedAt = now;
        _db.Words.Add(word);
        await _db.SaveChangesAsync(cancellationToken);
        return word;
    }

    public async Task UpdateAsync(Word word, CancellationToken cancellationToken = default)
    {
        word.UpdatedAt = DateTime.UtcNow;
        _db.Words.Update(word);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Words.FindAsync(new object?[] { id }, cancellationToken);
        if (entity is null)
        {
            return;
        }
        _db.Words.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
