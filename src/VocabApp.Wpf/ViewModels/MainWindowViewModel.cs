using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VocabApp.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly WordListViewModel _wordList;

    public MainWindowViewModel(WordListViewModel wordList)
    {
        _wordList = wordList;
        CurrentContent = _wordList;
    }

    [ObservableProperty]
    private string title = "VocabApp";

    [ObservableProperty]
    private object? currentContent;

    [RelayCommand]
    private void ShowWordList() => CurrentContent = _wordList;

    public async Task InitializeAsync()
    {
        await _wordList.LoadCommand.ExecuteAsync(null);
    }
}
