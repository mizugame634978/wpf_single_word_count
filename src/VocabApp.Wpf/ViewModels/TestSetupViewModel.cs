using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VocabApp.Core.Models;

namespace VocabApp.Wpf.ViewModels;

public partial class TestSetupViewModel : ObservableObject
{
    public TestSetupViewModel()
    {
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

        SelectedMode = Modes[0];
        SelectedRange = Ranges[0];
        SelectedCount = Counts[0];
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
