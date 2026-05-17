using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VocabApp.Core.Models;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;
using VocabApp.Wpf.Services;

namespace VocabApp.Wpf.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        ISettingsService settings,
        IDialogService dialogService,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _dialogService = dialogService;
        _logger = logger;

        Modes = new[]
        {
            new ModeChoice(TestMode.EnglishToJapanese, "英 → 和 (入力)"),
            new ModeChoice(TestMode.JapaneseToEnglish, "和 → 英 (入力)"),
            new ModeChoice(TestMode.MultipleChoiceEnglishToJapanese, "英 → 和 (4 択)"),
            new ModeChoice(TestMode.Flashcard, "フラッシュカード"),
        };
        Ranges = new[]
        {
            new RangeChoice(TestRange.All, "全単語"),
            new RangeChoice(TestRange.Weak, "苦手"),
            new RangeChoice(TestRange.Unasked, "未出題のみ"),
            new RangeChoice(TestRange.Tag, "タグ指定"),
        };
        Counts = new[] { 10, 20, 50, 100 };

        SelectedMode = Modes.First(m => m.Value == _settings.Current.DefaultTestMode);
        SelectedRange = Ranges.First(r => r.Value == _settings.Current.DefaultTestRange);
        SelectedCount = _settings.Current.DefaultTestCount;

        DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VocabApp", "vocab.db");
        LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VocabApp", "logs");
    }

    public IReadOnlyList<ModeChoice> Modes { get; }
    public IReadOnlyList<RangeChoice> Ranges { get; }
    public IReadOnlyList<int> Counts { get; }

    [ObservableProperty] private ModeChoice selectedMode;
    [ObservableProperty] private RangeChoice selectedRange;
    [ObservableProperty] private int selectedCount;
    [ObservableProperty] private string statusMessage = string.Empty;

    public string DbPath { get; }
    public string LogDir { get; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await _settings.UpdateAsync(s =>
            {
                s.DefaultTestMode = SelectedMode.Value;
                s.DefaultTestRange = SelectedRange.Value;
                s.DefaultTestCount = SelectedCount;
            });
            StatusMessage = "設定を保存しました";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            await _dialogService.ShowErrorAsync(
                $"設定の保存に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    [RelayCommand]
    private async Task OpenLogFolderAsync()
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(
                $"ログフォルダを開けませんでした。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    [RelayCommand]
    private async Task ResetDatabaseAsync()
    {
        var ok = await _dialogService.ConfirmAsync(
            $"DB ファイルを削除します。すべての単語と学習履歴が失われます。\n\n対象:\n{DbPath}\n\n本当に続行しますか? (削除後はアプリを再起動してください)",
            "DB の初期化");
        if (!ok) return;

        try
        {
            if (File.Exists(DbPath))
            {
                File.Delete(DbPath);
            }
            StatusMessage = "DB を削除しました。アプリを再起動してください。";
            await _dialogService.ShowInfoAsync(
                "DB ファイルを削除しました。\n反映するにはアプリを再起動してください。",
                "DB 初期化完了");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset DB");
            await _dialogService.ShowErrorAsync(
                $"DB の削除に失敗しました。アプリが DB ファイルを掴んでいるため、終了後に手動で削除してください。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    public record ModeChoice(TestMode Value, string Display);
    public record RangeChoice(TestRange Value, string Display);
}
