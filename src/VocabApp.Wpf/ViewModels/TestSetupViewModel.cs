using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VocabApp.Core.Models;
using VocabApp.Core.Services;

namespace VocabApp.Wpf.ViewModels;

public partial class TestSetupViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    public TestSetupViewModel(ISettingsService settings)
    {
        _settings = settings;

        Modes = new[]
        {
            new ModeChoice(TestMode.EnglishToJapanese, "英 → 和 (入力)"),
            new ModeChoice(TestMode.JapaneseToEnglish, "和 → 英 (入力)"),
            new ModeChoice(TestMode.MultipleChoiceEnglishToJapanese, "英 → 和 (4 択)"),
            new ModeChoice(TestMode.Flashcard, "フラッシュカード (自己申告)"),
        };
        Ranges = new[]
        {
            new RangeChoice(TestRange.All, "全単語"),
            new RangeChoice(TestRange.Weak, "苦手 (mastery ≤ 1 か 正答率 < 70%)"),
            new RangeChoice(TestRange.Unasked, "未出題のみ"),
            new RangeChoice(TestRange.Tag, "タグ指定"),
        };
        Counts = new[] { 10, 20, 50, 100 };

        // 設定の既定値を使って初期化
        var s = _settings.Current;
        SelectedMode = Modes.FirstOrDefault(m => m.Value == s.DefaultTestMode) ?? Modes[0];
        SelectedRange = Ranges.FirstOrDefault(r => r.Value == s.DefaultTestRange) ?? Ranges[0];
        SelectedCount = Counts.Contains(s.DefaultTestCount) ? s.DefaultTestCount : Counts[0];

        // 他画面で設定が更新されたら反映する。
        _settings.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        var s = _settings.Current;
        SelectedMode = Modes.FirstOrDefault(m => m.Value == s.DefaultTestMode) ?? SelectedMode;
        SelectedRange = Ranges.FirstOrDefault(r => r.Value == s.DefaultTestRange) ?? SelectedRange;
        if (Counts.Contains(s.DefaultTestCount))
        {
            SelectedCount = s.DefaultTestCount;
        }
    }

    public IReadOnlyList<ModeChoice> Modes { get; }
    public IReadOnlyList<RangeChoice> Ranges { get; }
    public IReadOnlyList<int> Counts { get; }

    [ObservableProperty]
    private ModeChoice selectedMode;

    [ObservableProperty]
    private RangeChoice selectedRange;

    [ObservableProperty]
    private int selectedCount;

    [ObservableProperty]
    private string tagFilter = string.Empty;

    public bool IsTagFilterVisible => SelectedRange.Value == TestRange.Tag;

    partial void OnSelectedRangeChanged(RangeChoice value)
    {
        OnPropertyChanged(nameof(IsTagFilterVisible));
    }

    /// <summary>「開始」ボタンが押されたときに親に通知するコールバック。</summary>
    public Func<TestSessionOptions, Task>? StartRequested { get; set; }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (StartRequested is null)
        {
            return;
        }

        var options = new TestSessionOptions
        {
            Mode = SelectedMode.Value,
            Range = SelectedRange.Value,
            TagFilter = SelectedRange.Value == TestRange.Tag
                ? (string.IsNullOrWhiteSpace(TagFilter) ? null : TagFilter.Trim())
                : null,
            Count = SelectedCount,
        };

        await StartRequested(options);
    }

    public record ModeChoice(TestMode Value, string Display);
    public record RangeChoice(TestRange Value, string Display);
}
