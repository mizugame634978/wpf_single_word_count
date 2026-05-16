using VocabApp.Core.Models;

namespace VocabApp.Core.Utilities;

/// <summary>
/// CSV 値と <see cref="PartOfSpeech"/> 列挙の相互変換。
/// CSV では短く小文字の値 (noun/verb/adj/adv/...) を使う。
/// </summary>
public static class PartOfSpeechConverter
{
    private static readonly Dictionary<string, PartOfSpeech> FromCsv = new(StringComparer.OrdinalIgnoreCase)
    {
        ["noun"] = PartOfSpeech.Noun,
        ["n"] = PartOfSpeech.Noun,
        ["verb"] = PartOfSpeech.Verb,
        ["v"] = PartOfSpeech.Verb,
        ["adj"] = PartOfSpeech.Adjective,
        ["adjective"] = PartOfSpeech.Adjective,
        ["adv"] = PartOfSpeech.Adverb,
        ["adverb"] = PartOfSpeech.Adverb,
        ["pron"] = PartOfSpeech.Pronoun,
        ["pronoun"] = PartOfSpeech.Pronoun,
        ["prep"] = PartOfSpeech.Preposition,
        ["preposition"] = PartOfSpeech.Preposition,
        ["conj"] = PartOfSpeech.Conjunction,
        ["conjunction"] = PartOfSpeech.Conjunction,
        ["interj"] = PartOfSpeech.Interjection,
        ["interjection"] = PartOfSpeech.Interjection,
        ["phrase"] = PartOfSpeech.Phrase,
        ["other"] = PartOfSpeech.Other,
    };

    private static readonly Dictionary<PartOfSpeech, string> ToCsv = new()
    {
        [PartOfSpeech.Noun] = "noun",
        [PartOfSpeech.Verb] = "verb",
        [PartOfSpeech.Adjective] = "adj",
        [PartOfSpeech.Adverb] = "adv",
        [PartOfSpeech.Pronoun] = "pron",
        [PartOfSpeech.Preposition] = "prep",
        [PartOfSpeech.Conjunction] = "conj",
        [PartOfSpeech.Interjection] = "interj",
        [PartOfSpeech.Phrase] = "phrase",
        [PartOfSpeech.Other] = "other",
    };

    public static PartOfSpeech? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return FromCsv.TryGetValue(value.Trim(), out var pos) ? pos : null;
    }

    public static string Format(PartOfSpeech? value)
        => value is null ? string.Empty : ToCsv[value.Value];
}
