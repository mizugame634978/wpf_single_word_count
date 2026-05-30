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
    private readonly ISecretProtector _protector;
    private readonly IVocabularyGenerator _generator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        ISettingsService settings,
        ISecretProtector protector,
        IVocabularyGenerator generator,
        IDialogService dialogService,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _protector = protector;
        _generator = generator;
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

        // 平文を直接 VM が持たない方針: 入力欄は GeminiApiKeyInput に書く。
        // 既存キーが登録されている場合はマスク文字列を表示し、ユーザが入れ直したいときだけ
        // 空にして再入力する運用にする。
        HasStoredGeminiKey = !string.IsNullOrEmpty(_settings.Current.GeminiApiKeyEncrypted);

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
    [ObservableProperty] private string geminiApiKeyInput = string.Empty;
    [ObservableProperty] private bool hasStoredGeminiKey;
    [ObservableProperty] private bool isPinging;
    [ObservableProperty] private string geminiStatusMessage = string.Empty;

    public string StoredKeyStatusText => HasStoredGeminiKey ? "登録済み (DPAPI で暗号化)" : "未登録";

    partial void OnHasStoredGeminiKeyChanged(bool value)
        => OnPropertyChanged(nameof(StoredKeyStatusText));

    public string DbPath { get; }
    public string LogDir { get; }
    public string ApiKeyHelpUrl => "https://aistudio.google.com/apikey";

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
    private async Task SaveGeminiKeyAsync()
    {
        var input = (GeminiApiKeyInput ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            await _dialogService.ShowErrorAsync("API キーが空です。入力欄に貼り付けてから保存してください。", "入力エラー");
            return;
        }
        try
        {
            var encrypted = _protector.Protect(input);
            await _settings.UpdateAsync(s => s.GeminiApiKeyEncrypted = encrypted);
            HasStoredGeminiKey = !string.IsNullOrEmpty(encrypted);
            GeminiApiKeyInput = string.Empty; // メモリに残さない
            GeminiStatusMessage = "API キーを暗号化して保存しました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Gemini API key");
            await _dialogService.ShowErrorAsync(
                $"API キーの保存に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    [RelayCommand]
    private async Task ClearGeminiKeyAsync()
    {
        var ok = await _dialogService.ConfirmAsync(
            "保存されている Gemini API キーを削除しますか?",
            "API キーの削除");
        if (!ok) return;

        try
        {
            await _settings.UpdateAsync(s => s.GeminiApiKeyEncrypted = null);
            HasStoredGeminiKey = false;
            GeminiApiKeyInput = string.Empty;
            GeminiStatusMessage = "API キーを削除しました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Gemini API key");
            await _dialogService.ShowErrorAsync(
                $"API キーの削除に失敗しました。\n\n{ExceptionFormatter.Format(ex)}");
        }
    }

    [RelayCommand]
    private async Task TestGeminiConnectionAsync()
    {
        // 入力欄に値があればそれを優先、なければ保存済みキーを復号して使う
        string? apiKey = null;
        if (!string.IsNullOrWhiteSpace(GeminiApiKeyInput))
        {
            apiKey = GeminiApiKeyInput.Trim();
        }
        else if (HasStoredGeminiKey)
        {
            apiKey = _protector.Unprotect(_settings.Current.GeminiApiKeyEncrypted);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await _dialogService.ShowErrorAsync(
                "API キーが見つかりません。入力欄に貼り付けるか、保存済みのキーがあるか確認してください。",
                "テスト接続");
            return;
        }

        IsPinging = true;
        GeminiStatusMessage = "Gemini に接続中…";
        try
        {
            await _generator.PingAsync(apiKey);
            GeminiStatusMessage = "✓ 接続成功。API キーは有効です。";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini ping failed");
            GeminiStatusMessage = "✗ 接続失敗。詳細はダイアログを確認してください。";
            await _dialogService.ShowErrorAsync(
                $"Gemini への接続に失敗しました。\n\n{ExceptionFormatter.Format(ex)}",
                "テスト接続");
        }
        finally
        {
            IsPinging = false;
        }
    }

    [RelayCommand]
    private async Task OpenApiKeyHelpAsync()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = ApiKeyHelpUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(
                $"ブラウザを開けませんでした。\n手動で {ApiKeyHelpUrl} を開いてください。\n\n{ExceptionFormatter.Format(ex)}");
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
