using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VocabApp.Core.Csv;
using VocabApp.Core.Models;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;
using VocabApp.Wpf.Services;

namespace VocabApp.Wpf.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    private const string CsvFilter = "CSV ファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*";

    private readonly ICsvService _csvService;
    private readonly IPromptTemplateService _promptService;
    private readonly IVocabularyGenerator _generator;
    private readonly IVocabularyService _vocabService;
    private readonly IDialogService _dialogService;
    private readonly WordListViewModel _wordListViewModel;
    private readonly ILogger<ImportExportViewModel> _logger;

    public ImportExportViewModel(
        ICsvService csvService,
        IPromptTemplateService promptService,
        IVocabularyGenerator generator,
        IVocabularyService vocabService,
        IDialogService dialogService,
        WordListViewModel wordListViewModel,
        ILogger<ImportExportViewModel> logger)
    {
        _csvService = csvService;
        _promptService = promptService;
        _generator = generator;
        _vocabService = vocabService;
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

    /// <summary>AI で生成した結果のプレビュー (チェックを付けて取り込む)。</summary>
    public ObservableCollection<GeneratedWordRow> GeneratedRows { get; } = new();

    [ObservableProperty]
    private string generationStatus = string.Empty;

    public bool HasGenerationResult => GeneratedRows.Count > 0;

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
        if (!ValidatePromptInputs()) return;

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

    [RelayCommand]
    private async Task GenerateWithLlmAsync()
    {
        if (!ValidatePromptInputs()) return;

        IsBusy = true;
        GenerationStatus = $"Gemini で生成中 (テーマ: {PromptTheme}, 件数: {PromptCount})…";
        try
        {
            var result = await _generator.GenerateAsync(new VocabularyGenerationRequest(
                Theme: PromptTheme,
                Count: PromptCount,
                Level: string.IsNullOrWhiteSpace(PromptLevel) ? null : PromptLevel));

            // 既存単語と重複している行は自動でチェックオフ。
            var existing = await _vocabService.GetAllAsync();
            var existingKeys = existing
                .Select(w => (w.Text.Trim().ToLowerInvariant(), w.PartOfSpeech))
                .ToHashSet();

            GeneratedRows.Clear();
            foreach (var w in result)
            {
                var isDup = existingKeys.Contains((w.Text.Trim().ToLowerInvariant(), w.PartOfSpeech));
                GeneratedRows.Add(new GeneratedWordRow(w, isSelected: !isDup, isDuplicate: isDup));
            }
            OnPropertyChanged(nameof(HasGenerationResult));

            var dupCount = GeneratedRows.Count(r => r.IsDuplicate);
            GenerationStatus = $"{GeneratedRows.Count} 件生成しました (うち重複 {dupCount} 件はチェックを外しています)。確認の上『選択した N 件を取り込む』を押してください。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM generation failed");
            GenerationStatus = "生成に失敗しました。";
            await _dialogService.ShowErrorAsync(
                $"AI 生成に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportGeneratedAsync()
    {
        var selected = GeneratedRows.Where(r => r.IsSelected).Select(r => r.Word).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync(
                "取り込む単語が選択されていません。", "選択なし");
            return;
        }

        IsBusy = true;
        GenerationStatus = $"取り込み中 ({selected.Count} 件)…";
        var added = 0;
        var errors = 0;
        try
        {
            foreach (var word in selected)
            {
                try
                {
                    await _vocabService.AddAsync(word);
                    added++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add generated word: {Text}", word.Text);
                    errors++;
                }
            }

            GenerationStatus = $"取り込み完了: {added} 件追加 / {errors} 件エラー";
            await _wordListViewModel.LoadCommand.ExecuteAsync(null);
            GeneratedRows.Clear();
            OnPropertyChanged(nameof(HasGenerationResult));
            await _dialogService.ShowInfoAsync(GenerationStatus, "取り込み完了");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import generated words");
            GenerationStatus = "取り込みに失敗しました。";
            await _dialogService.ShowErrorAsync(
                $"取り込み中にエラーが発生しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearGeneration()
    {
        GeneratedRows.Clear();
        GenerationStatus = string.Empty;
        OnPropertyChanged(nameof(HasGenerationResult));
    }

    private bool ValidatePromptInputs()
    {
        if (string.IsNullOrWhiteSpace(PromptTheme))
        {
            _ = _dialogService.ShowErrorAsync("テーマを入力してください。", "入力エラー");
            return false;
        }
        if (PromptCount <= 0)
        {
            _ = _dialogService.ShowErrorAsync("件数は 1 以上を指定してください。", "入力エラー");
            return false;
        }
        return true;
    }

    public record ConflictModeChoice(ConflictMode Value, string Display);
}

/// <summary>AI 生成結果プレビュー 1 行ぶん。</summary>
public partial class GeneratedWordRow : ObservableObject
{
    public GeneratedWordRow(Word word, bool isSelected, bool isDuplicate)
    {
        Word = word;
        this.isSelected = isSelected;
        IsDuplicate = isDuplicate;
    }

    public Word Word { get; }

    public bool IsDuplicate { get; }

    public string TagsDisplay =>
        string.Join("; ", Word.Tags.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

    [ObservableProperty]
    private bool isSelected;
}
