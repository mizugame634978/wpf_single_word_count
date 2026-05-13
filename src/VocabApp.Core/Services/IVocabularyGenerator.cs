using VocabApp.Core.Models;

namespace VocabApp.Core.Services;

public record VocabularyGenerationRequest(string Theme, int Count, string? Level = null);

public interface IVocabularyGenerator
{
    Task<IReadOnlyList<Word>> GenerateAsync(
        VocabularyGenerationRequest request,
        CancellationToken cancellationToken = default);
}
