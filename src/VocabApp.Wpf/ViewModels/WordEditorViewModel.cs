using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VocabApp.Core.Models;
using VocabApp.Core.Utilities;

namespace VocabApp.Wpf.ViewModels;

public partial class WordEditorViewModel : ObservableObject
{
    public WordEditorViewModel()
    {
        PartsOfSpeech.Add(null);
        foreach (var pos in Enum.GetValues<PartOfSpeech>())
        {
            PartsOfSpeech.Add(pos);
        }
    }

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string title = "単語を追加";

    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private string meaning = string.Empty;

    [ObservableProperty]
    private PartOfSpeech? selectedPartOfSpeech;

    [ObservableProperty]
    private string? example;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string tagsInput = string.Empty;

    public ObservableCollection<PartOfSpeech?> PartsOfSpeech { get; } = new();

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Text) &&
        !string.IsNullOrWhiteSpace(Meaning);

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(IsValid));
    partial void OnMeaningChanged(string value) => OnPropertyChanged(nameof(IsValid));

    public void Load(Word? word)
    {
        if (word is null)
        {
            Id = 0;
            Title = "単語を追加";
            Text = string.Empty;
            Meaning = string.Empty;
            SelectedPartOfSpeech = null;
            Example = null;
            Notes = null;
            TagsInput = string.Empty;
        }
        else
        {
            Id = word.Id;
            Title = $"単語を編集: {word.Text}";
            Text = word.Text;
            Meaning = word.Meaning;
            SelectedPartOfSpeech = word.PartOfSpeech;
            Example = word.Example;
            Notes = word.Notes;
            TagsInput = TagParser.Format(word.Tags.Select(t => t.Name));
        }
    }

    public Word ToWord()
    {
        var word = new Word
        {
            Id = Id,
            Text = Text.Trim(),
            Meaning = Meaning.Trim(),
            PartOfSpeech = SelectedPartOfSpeech,
            Example = string.IsNullOrWhiteSpace(Example) ? null : Example!.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes!.Trim(),
        };

        foreach (var name in TagParser.Parse(TagsInput))
        {
            word.Tags.Add(new Tag { Name = name });
        }
        return word;
    }
}
