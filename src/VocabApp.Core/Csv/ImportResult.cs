namespace VocabApp.Core.Csv;

public class ImportResult
{
    public int Added { get; set; }

    public int Updated { get; set; }

    public int Skipped { get; set; }

    public List<ImportRowError> Errors { get; } = new();

    public int TotalProcessed => Added + Updated + Skipped + Errors.Count;
}

public record ImportRowError(int LineNumber, string Message);
