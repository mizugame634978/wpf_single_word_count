using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VocabApp.Core.Models;

namespace VocabApp.Wpf.ViewModels;

public partial class TestResultViewModel : ObservableObject
{
    private TestSessionSummary? _summary;

    [ObservableProperty] private int total;
    [ObservableProperty] private int correct;
    [ObservableProperty] private int wrong;
    [ObservableProperty] private double accuracyPercent;
    [ObservableProperty] private bool hasWrongAnswers;

    public ObservableCollection<TestAnsweredQuestion> WrongAnswers { get; } = new();

    public Action? BackToSetupRequested { get; set; }
    public Func<TestSessionOptions, Task>? RetryRequested { get; set; }

    public void Load(TestSessionSummary summary)
    {
        _summary = summary;
        Total = summary.Total;
        Correct = summary.Correct;
        Wrong = summary.Wrong;
        AccuracyPercent = summary.Total == 0 ? 0 : Math.Round(100.0 * summary.Correct / summary.Total, 1);
        WrongAnswers.Clear();
        foreach (var a in summary.WrongAnswers)
        {
            WrongAnswers.Add(a);
        }
        HasWrongAnswers = WrongAnswers.Count > 0;
    }

    [RelayCommand]
    private void BackToSetup() => BackToSetupRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanRetryWrong))]
    private async Task RetryWrongAsync()
    {
        if (_summary is null || RetryRequested is null) return;
        var ids = _summary.WrongAnswers.Select(a => a.Word.Id).ToList();
        var retryOptions = new TestSessionOptions
        {
            Mode = _summary.Options.Mode,
            Count = ids.Count,
            OverrideWordIds = ids,
        };
        await RetryRequested(retryOptions);
    }

    private bool CanRetryWrong() => HasWrongAnswers;

    partial void OnHasWrongAnswersChanged(bool value)
    {
        RetryWrongCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RetrySameAsync()
    {
        if (_summary is null || RetryRequested is null) return;
        await RetryRequested(_summary.Options);
    }
}
