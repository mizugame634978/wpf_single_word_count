using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VocabApp.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly WordListViewModel _wordList;
    private readonly TestHostViewModel _testHost;
    private readonly ImportExportViewModel _importExport;

    public MainWindowViewModel(
        WordListViewModel wordList,
        TestHostViewModel testHost,
        ImportExportViewModel importExport)
    {
        _wordList = wordList;
        _testHost = testHost;
        _importExport = importExport;
        CurrentContent = _wordList;
    }

    [ObservableProperty]
    private string title = "VocabApp";

    [ObservableProperty]
    private object? currentContent;

    public bool IsWordListSelected => CurrentContent == _wordList;
    public bool IsTestSelected => CurrentContent == _testHost;
    public bool IsImportExportSelected => CurrentContent == _importExport;

    partial void OnCurrentContentChanged(object? value)
    {
        OnPropertyChanged(nameof(IsWordListSelected));
        OnPropertyChanged(nameof(IsTestSelected));
        OnPropertyChanged(nameof(IsImportExportSelected));
    }

    [RelayCommand]
    private void ShowWordList() => CurrentContent = _wordList;

    [RelayCommand]
    private void ShowTest()
    {
        _testHost.ResetToSetup();
        CurrentContent = _testHost;
    }

    [RelayCommand]
    private void ShowImportExport() => CurrentContent = _importExport;

    public async Task InitializeAsync()
    {
        await _wordList.LoadCommand.ExecuteAsync(null);
    }
}
