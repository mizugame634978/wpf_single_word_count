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
        WordsView.Filter = MatchesSearch;
        WordsView.SortDescriptions.Add(new SortDescription(nameof(Word.Text), ListSortDirection.Ascending));
    }

    public ObservableCollection<Word> Words { get; } = new();

    public ICollectionView WordsView { get; }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditWordCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteWordCommand))]
    private Word? selectedWord;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    partial void OnSearchTextChanged(string value) => WordsView.Refresh();

    private bool MatchesSearch(object obj)
    {
        if (obj is not Word w)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }
        var q = SearchText.Trim();
        return w.Text.Contains(q, StringComparison.OrdinalIgnoreCase)
            || w.Meaning.Contains(q, StringComparison.OrdinalIgnoreCase)
            || w.Tags.Any(t => t.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
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
            StatusMessage = $"{Words.Count} 件";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load words");
            await _dialogService.ShowErrorAsync($"単語一覧の読み込みに失敗しました。\n{ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddWordAsync()
    {
        var input = await _dialogService.ShowWordEditorAsync(null);
        if (input is null)
        {
            return;
        }
        try
        {
            var saved = await _vocabService.AddAsync(input);
            // 一覧を最新化 (タグの ID 等が確定するため再取得)
            await LoadAsync();
            SelectedWord = Words.FirstOrDefault(w => w.Id == saved.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add word");
            await _dialogService.ShowErrorAsync($"単語の追加に失敗しました。\n{ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task EditWordAsync()
    {
        if (SelectedWord is null)
        {
            return;
        }
        var input = await _dialogService.ShowWordEditorAsync(SelectedWord);
        if (input is null)
        {
            return;
        }
        try
        {
            await _vocabService.UpdateAsync(input);
            await LoadAsync();
            SelectedWord = Words.FirstOrDefault(w => w.Id == input.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update word");
            await _dialogService.ShowErrorAsync($"単語の更新に失敗しました。\n{ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteWordAsync()
    {
        if (SelectedWord is null)
        {
            return;
        }
        var target = SelectedWord;
        var ok = await _dialogService.ConfirmAsync(
            $"「{target.Text}」を削除しますか?", "削除の確認");
        if (!ok)
        {
            return;
        }
        try
        {
            await _vocabService.DeleteAsync(target.Id);
            Words.Remove(target);
            StatusMessage = $"{Words.Count} 件";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete word");
            await _dialogService.ShowErrorAsync($"単語の削除に失敗しました。\n{ex.Message}");
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    private bool CanEditOrDelete() => SelectedWord is not null;

    public static string FormatTags(Word w) => TagParser.Format(w.Tags.Select(t => t.Name));
}
