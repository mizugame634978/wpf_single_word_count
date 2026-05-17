using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VocabApp.Core.Csv;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;
using VocabApp.Wpf.Services;

namespace VocabApp.Wpf.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    private const string CsvFilter = "CSV ファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*";

    private readonly ICsvService _csvService;
    private readonly IPromptTemplateService _promptService;
    private readonly IDialogService _dialogService;
    private readonly WordListViewModel _wordListViewModel;
    private readonly ILogger<ImportExportViewModel> _logger;

    public ImportExportViewModel(
        ICsvService csvService,
        IPromptTemplateService promptService,
        IDialogService dialogService,
        WordListViewModel wordListViewModel,
        ILogger<ImportExportViewModel> logger)
    {
        _csvService = csvService;
        _promptService = promptService;
        _dialogService = dialogService;
        _wordListViewModel = wordListViewModel;
        _logger = logger;

        ConflictModes = new List<ConflictModeChoice>
        {
            new(ConflictMode.Skip, "スキップ (既存を残す)"),
            new(ConflictMode.Overwrite, "上書き (既存を更新。学習統計は CSV に含まれる場合のみ更新)"),
            new(ConflictMode.AddAsNew, "別レコードとして追加"),
        };
        SelectedConflictMode = ConflictModes[0];
    }

    public List<ConflictModeChoice> ConflictModes { get; }

    [ObservableProperty]
    private ConflictModeChoice selectedConflictMode;

    [ObservableProperty]
    private bool includeLearningStats = true;

    [ObservableProperty]
    private string promptTheme = string.Empty;

    [ObservableProperty]
    private int promptCount = 20;

    [ObservableProperty]
    private string promptLevel = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = await _dialogService.ShowOpenFileAsync(
            "インポートする CSV を選択", CsvFilter);
        if (path is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "インポート中...";
        try
        {
            await using var stream = File.OpenRead(path);
            var result = await _csvService.ImportAsync(stream, SelectedConflictMode.Value);

            var summary =
                $"追加: {result.Added} / 更新: {result.Updated} / スキップ: {result.Skipped} / エラー: {result.Errors.Count}";
            StatusMessage = summary;

            if (result.Errors.Count > 0)
            {
                var head = string.Join("\n",
                    result.Errors.Take(10).Select(e => $"  行 {e.LineNumber}: {e.Message}"));
                var more = result.Errors.Count > 10 ? $"\n  …他 {result.Errors.Count - 10} 件" : string.Empty;
                await _dialogService.ShowInfoAsync(
                    $"{summary}\n\nエラー:\n{head}{more}",
                    "インポート完了 (一部エラー)");
            }
            else
            {
                await _dialogService.ShowInfoAsync(summary, "インポート完了");
            }

            await _wordListViewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV import failed");
            StatusMessage = "インポートに失敗しました";
            await _dialogService.ShowErrorAsync(
                $"インポート中にエラーが発生しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var path = await _dialogService.ShowSaveFileAsync(
            "エクスポート先の CSV を指定", CsvFilter,
            $"vocab-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        if (path is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "エクスポート中...";
        try
        {
            await using (var stream = File.Create(path))
            {
                await _csvService.ExportAsync(stream, new ExportOptions
                {
                    IncludeLearningStats = IncludeLearningStats,
                });
            }
            StatusMessage = $"エクスポート完了: {path}";
            await _dialogService.ShowInfoAsync($"エクスポートしました:\n{path}", "エクスポート完了");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV export failed");
            StatusMessage = "エクスポートに失敗しました";
            await _dialogService.ShowErrorAsync(
                $"エクスポート中にエラーが発生しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShowPromptAsync()
    {
        if (string.IsNullOrWhiteSpace(PromptTheme))
        {
            await _dialogService.ShowErrorAsync("テーマを入力してください。", "入力エラー");
            return;
        }
        if (PromptCount <= 0)
        {
            await _dialogService.ShowErrorAsync("件数は 1 以上を指定してください。", "入力エラー");
            return;
        }

        string prompt;
        try
        {
            prompt = _promptService.BuildVocabularyPrompt(new VocabularyPromptRequest(
                Theme: PromptTheme,
                Count: PromptCount,
                Level: string.IsNullOrWhiteSpace(PromptLevel) ? null : PromptLevel));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build prompt");
            await _dialogService.ShowErrorAsync(
                $"プロンプト生成に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
            return;
        }

        StatusMessage = "プロンプトを表示しました";
        await _dialogService.ShowTextDialogAsync(
            "LLM 用プロンプト",
            prompt,
            "本文は選択済みです。Ctrl+C でコピー後、Gemini CLI / ChatGPT などに貼り付け、得られた CSV を上の「インポート」から取り込んでください。");
    }

    public record ConflictModeChoice(ConflictMode Value, string Display);
}
