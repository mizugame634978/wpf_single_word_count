using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VocabApp.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly WordListViewModel _wordList;
    private readonly TestHostViewModel _testHost;
    private readonly ImportExportViewModel _importExport;
    private readonly SettingsViewModel _settings;

    public MainWindowViewModel(
        WordListViewModel wordList,
        TestHostViewModel testHost,
        ImportExportViewModel importExport,
        SettingsViewModel settings)
    {
        _wordList = wordList;
        _testHost = testHost;
        _importExport = importExport;
        _settings = settings;
        CurrentContent = _wordList;
    }

    [ObservableProperty]
    private string title = "VocabApp";

    [ObservableProperty]
    private object? currentContent;

    public bool IsWordListSelected => CurrentContent == _wordList;
    public bool IsTestSelected => CurrentContent == _testHost;
    public bool IsImportExportSelected => CurrentContent == _importExport;
    public bool IsSettingsSelected => CurrentContent == _settings;

    partial void OnCurrentContentChanged(object? value)
    {
        OnPropertyChanged(nameof(IsWordListSelected));
        OnPropertyChanged(nameof(IsTestSelected));
        OnPropertyChanged(nameof(IsImportExportSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
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

    [RelayCommand]
    private void ShowSettings() => CurrentContent = _settings;

    public async Task InitializeAsync()
    {
        await _wordList.LoadCommand.ExecuteAsync(null);
    }
}
