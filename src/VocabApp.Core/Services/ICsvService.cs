using VocabApp.Core.Csv;

namespace VocabApp.Core.Services;

public interface ICsvService
{
    Task<ImportResult> ImportAsync(
        Stream input,
        ConflictMode conflictMode,
        CancellationToken cancellationToken = default);

    Task ExportAsync(
        Stream output,
        ExportOptions options,
        CancellationToken cancellationToken = default);
}
