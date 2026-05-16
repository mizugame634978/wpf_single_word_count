namespace VocabApp.Core.Models;

public class Word
{
    public int Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public string Meaning { get; set; } = string.Empty;

    public PartOfSpeech? PartOfSpeech { get; set; }

    public string? Example { get; set; }

    public string? Notes { get; set; }

    public int TimesAsked { get; set; }

    public int TimesCorrect { get; set; }

    public DateTime? LastAskedAt { get; set; }

    public int Mastery { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Tag> Tags { get; set; } = new();
}
