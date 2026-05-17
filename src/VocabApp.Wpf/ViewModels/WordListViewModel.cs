using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VocabApp.Core.Models;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;
using VocabApp.Wpf.Services;

namespace VocabApp.Wpf.ViewModels;

public partial class WordListViewModel : ObservableObject
{
    private readonly IVocabularyService _vocabService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<WordListViewModel> _logger;

    public WordListViewModel(
        IVocabularyService vocabService,
        IDialogService dialogService,
        ILogger<WordListViewModel> logger)
    {
        _vocabService = vocabService;
        _dialogService = dialogService;
        _logger = logger;

        WordsView = CollectionViewSource.GetDefaultView(Words);
        WordsView.Filter = MatchesFilters;
        WordsView.SortDescriptions.Add(new SortDescription(nameof(Word.Text), ListSortDirection.Ascending));

        MasteryFilters = new[]
        {
            new MasteryFilter("（すべて）", null),
            new MasteryFilter("苦手 (0-1)", w => w.Mastery <= 1),
            new MasteryFilter("中位 (2-3)", w => w.Mastery is >= 2 and <= 3),
            new MasteryFilter("習得済 (4-5)", w => w.Mastery >= 4),
            new MasteryFilter("未出題", w => w.TimesAsked == 0),
        };
        SelectedMasteryFilter = MasteryFilters[0];

        PosFilters = new List<PosFilter> { new("（すべて）", null) };
        foreach (var pos in Enum.GetValues<PartOfSpeech>())
        {
            PosFilters.Add(new PosFilter(pos.ToString(), pos));
        }
        SelectedPosFilter = PosFilters[0];

        SelectedTag = AllTagsSentinel;
    }

    public ObservableCollection<Word> Words { get; } = new();
    public ICollectionView WordsView { get; }

    public ObservableCollection<string> AvailableTags { get; } = new();
    public IReadOnlyList<MasteryFilter> MasteryFilters { get; }
    public List<PosFilter> PosFilters { get; }

    public const string AllTagsSentinel = "（すべて）";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string? selectedTag;

    [ObservableProperty]
    private MasteryFilter selectedMasteryFilter;

    [ObservableProperty]
    private PosFilter selectedPosFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditWordCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteWordCommand))]
    private Word? selectedWord;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    partial void OnSearchTextChanged(string value) => WordsView.Refresh();
    partial void OnSelectedTagChanged(string? value) => WordsView.Refresh();
    partial void OnSelectedMasteryFilterChanged(MasteryFilter value) => WordsView.Refresh();
    partial void OnSelectedPosFilterChanged(PosFilter value) => WordsView.Refresh();

    private bool MatchesFilters(object obj)
    {
        if (obj is not Word w)
        {
            return false;
        }

        // テキスト検索
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            var matched = w.Text.Contains(q, StringComparison.OrdinalIgnoreCase)
                || w.Meaning.Contains(q, StringComparison.OrdinalIgnoreCase)
                || w.Tags.Any(t => t.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
            if (!matched) return false;
        }

        // タグ
        if (SelectedTag is not null && SelectedTag != AllTagsSentinel)
        {
            if (!w.Tags.Any(t => string.Equals(t.Name, SelectedTag, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // 習熟度
        if (SelectedMasteryFilter.Predicate is { } pred && !pred(w))
        {
            return false;
        }

        // 品詞
        if (SelectedPosFilter.Value is { } pos && w.PartOfSpeech != pos)
        {
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            var words = await _vocabService.GetAllAsync();
            Words.Clear();
            foreach (var w in words)
            {
                Words.Add(w);
            }

            RefreshAvailableTags();
            StatusMessage = $"{Words.Count} 件";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load words");
            await _dialogService.ShowErrorAsync(
                $"単語一覧の読み込みに失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshAvailableTags()
    {
        var current = SelectedTag;
        AvailableTags.Clear();
        AvailableTags.Add(AllTagsSentinel);
        foreach (var name in Words
                     .SelectMany(w => w.Tags.Select(t => t.Name))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            AvailableTags.Add(name);
        }
        SelectedTag = current is not null && AvailableTags.Contains(current)
            ? current
            : AllTagsSentinel;
    }

    [RelayCommand]
    private async Task AddWordAsync()
    {
        var input = await _dialogService.ShowWordEditorAsync(null);
        if (input is null) return;
        try
        {
            var saved = await _vocabService.AddAsync(input);
            await LoadAsync();
            SelectedWord = Words.FirstOrDefault(w => w.Id == saved.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add word");
            await _dialogService.ShowErrorAsync(
                $"単語の追加に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task EditWordAsync()
    {
        if (SelectedWord is null) return;
        var input = await _dialogService.ShowWordEditorAsync(SelectedWord);
        if (input is null) return;
        try
        {
            await _vocabService.UpdateAsync(input);
            await LoadAsync();
            SelectedWord = Words.FirstOrDefault(w => w.Id == input.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update word");
            await _dialogService.ShowErrorAsync(
                $"単語の更新に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteWordAsync()
    {
        if (SelectedWord is null) return;
        var target = SelectedWord;
        var ok = await _dialogService.ConfirmAsync(
            $"「{target.Text}」を削除しますか?", "削除の確認");
        if (!ok) return;
        try
        {
            await _vocabService.DeleteAsync(target.Id);
            Words.Remove(target);
            RefreshAvailableTags();
            StatusMessage = $"{Words.Count} 件";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete word");
            await _dialogService.ShowErrorAsync(
                $"単語の削除に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = string.Empty;
        SelectedTag = AllTagsSentinel;
        SelectedMasteryFilter = MasteryFilters[0];
        SelectedPosFilter = PosFilters[0];
    }

    private bool CanEditOrDelete() => SelectedWord is not null;

    public static string FormatTags(Word w) => TagParser.Format(w.Tags.Select(t => t.Name));

    public record MasteryFilter(string Display, Func<Word, bool>? Predicate);
    public record PosFilter(string Display, PartOfSpeech? Value);
}
