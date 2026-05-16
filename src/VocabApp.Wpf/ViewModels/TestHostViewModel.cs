using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using VocabApp.Core.Models;

namespace VocabApp.Wpf.ViewModels;

public partial class TestHostViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly TestSetupViewModel _setup;
    private readonly WordListViewModel _wordListViewModel;

    public TestHostViewModel(
        IServiceProvider services,
        TestSetupViewModel setup,
        WordListViewModel wordListViewModel)
    {
        _services = services;
        _setup = setup;
        _wordListViewModel = wordListViewModel;

        _setup.StartRequested = StartSessionAsync;
        CurrentContent = _setup;
    }

    [ObservableProperty]
    private object currentContent;

    /// <summary>テストナビが押されたときの初期化 (常に Setup に戻す)。</summary>
    public void ResetToSetup()
    {
        CurrentContent = _setup;
    }

    private async Task StartSessionAsync(TestSessionOptions options)
    {
        var sessionVm = _services.GetRequiredService<TestSessionViewModel>();
        sessionVm.Completed = OnSessionCompleted;
        sessionVm.Cancelled = OnSessionCancelled;
        CurrentContent = sessionVm;
        await sessionVm.StartAsync(options);
    }

    private void OnSessionCompleted(TestSessionSummary summary)
    {
        // 単語一覧の習熟度等が更新されているので、戻ったときに反映されるよう
        // バックグラウンドで再読み込みを発火しておく。
        _ = _wordListViewModel.LoadCommand.ExecuteAsync(null);

        var resultVm = _services.GetRequiredService<TestResultViewModel>();
        resultVm.BackToSetupRequested = ResetToSetup;
        resultVm.RetryRequested = StartSessionAsync;
        resultVm.Load(summary);
        CurrentContent = resultVm;
    }

    private void OnSessionCancelled()
    {
        ResetToSetup();
    }
}
