using VocabApp.Core.Models;

namespace VocabApp.Core.Services;

public interface IVocabularyService
{
    Task<IReadOnlyList<Word>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Word?> FindAsync(int id, CancellationToken cancellationToken = default);

    Task<Word> AddAsync(Word word, CancellationToken cancellationToken = default);

    Task UpdateAsync(Word word, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
