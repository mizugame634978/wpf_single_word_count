using CommunityToolkit.Mvvm.ComponentModel;

namespace VocabApp.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "VocabApp";

    [ObservableProperty]
    private string statusMessage = "Phase 0: 起動と DB 初期化のみ実装済み";
}
