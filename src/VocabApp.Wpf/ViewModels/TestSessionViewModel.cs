using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VocabApp.Core.Models;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;
using VocabApp.Wpf.Services;

namespace VocabApp.Wpf.ViewModels;

public partial class TestSessionViewModel : ObservableObject
{
    private readonly ITestSessionService _testService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<TestSessionViewModel> _logger;

    private TestSessionOptions _options = new();
    private TestSession? _session;
    private IReadOnlyList<TestQuestion> _questions = Array.Empty<TestQuestion>();
    private readonly List<TestAnsweredQuestion> _answered = new();
    private int _currentIndex;

    public TestSessionViewModel(
        ITestSessionService testService,
        IDialogService dialogService,
        ILogger<TestSessionViewModel> logger)
    {
        _testService = testService;
        _dialogService = dialogService;
        _logger = logger;
    }

    /// <summary>セッション完了時 (全問回答 or 早期終了で 1 問以上回答済み) のコールバック。</summary>
    public Action<TestSessionSummary>? Completed { get; set; }

    /// <summary>セッション中断 (1 問も回答せず終了) のコールバック。</summary>
    public Action? Cancelled { get; set; }

    [ObservableProperty] private TestMode mode;
    [ObservableProperty] private int totalQuestions;
    [ObservableProperty] private int currentQuestionNumber;
    [ObservableProperty] private string promptText = string.Empty;
    [ObservableProperty] private string userInput = string.Empty;
    [ObservableProperty] private bool isAnswerRevealed;
    [ObservableProperty] private string? answerRevealedText;
    [ObservableProperty] private string statusMessage = string.Empty;

    public ObservableCollection<string> Choices { get; } = new();

    public bool IsTypingMode =>
        Mode == TestMode.EnglishToJapanese || Mode == TestMode.JapaneseToEnglish;

    public bool IsMultipleChoiceMode => Mode == TestMode.MultipleChoiceEnglishToJapanese;

    public bool IsFlashcardMode => Mode == TestMode.Flashcard;

    partial void OnModeChanged(TestMode value)
    {
        OnPropertyChanged(nameof(IsTypingMode));
        OnPropertyChanged(nameof(IsMultipleChoiceMode));
        OnPropertyChanged(nameof(IsFlashcardMode));
    }

    public async Task StartAsync(TestSessionOptions options)
    {
        _options = options;
        try
        {
            var result = await _testService.StartSessionAsync(options);
            _session = result.Session;
            _questions = result.Questions;
            _answered.Clear();
            _currentIndex = 0;
            Mode = options.Mode;
            TotalQuestions = _questions.Count;
            LoadCurrent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start test session");
            await _dialogService.ShowErrorAsync(
                $"テストの開始に失敗しました。\n\n{ex.GetType().Name}: {ex.Message}");
            Cancelled?.Invoke();
        }
    }

    private void LoadCurrent()
    {
        var q = _questions[_currentIndex];
        CurrentQuestionNumber = _currentIndex + 1;
        UserInput = string.Empty;
        IsAnswerRevealed = false;
        AnswerRevealedText = null;
        Choices.Clear();

        PromptText = Mode switch
        {
            TestMode.EnglishToJapanese => q.Word.Text,
            TestMode.MultipleChoiceEnglishToJapanese => q.Word.Text,
            TestMode.Flashcard => q.Word.Text,
            TestMode.JapaneseToEnglish => q.Word.Meaning,
            _ => q.Word.Text,
        };

        if (IsMultipleChoiceMode && q.Choices is { Count: > 0 })
        {
            foreach (var c in q.Choices)
            {
                Choices.Add(c);
            }
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (!IsTypingMode) return;
        var q = _questions[_currentIndex];
        var correct = AnswerJudge.Judge(Mode, q.Word, UserInput);
        await RecordAndAdvance(q.Word, UserInput, correct);
    }

    [RelayCommand]
    private async Task SelectChoiceAsync(string choice)
    {
        if (!IsMultipleChoiceMode) return;
        var q = _questions[_currentIndex];
        var correct = string.Equals(choice, q.Word.Meaning, StringComparison.Ordinal);
        await RecordAndAdvance(q.Word, choice, correct);
    }

    [RelayCommand]
    private void RevealAnswer()
    {
        if (!IsFlashcardMode) return;
        var q = _questions[_currentIndex];
        AnswerRevealedText = q.Word.Meaning;
        IsAnswerRevealed = true;
    }

    [RelayCommand]
    private Task MarkRememberedAsync() => FlashcardMarkAsync(true);

    [RelayCommand]
    private Task MarkForgottenAsync() => FlashcardMarkAsync(false);

    private async Task FlashcardMarkAsync(bool remembered)
    {
        if (!IsFlashcardMode) return;
        var q = _questions[_currentIndex];
        await RecordAndAdvance(q.Word, remembered ? "覚えていた" : "覚えていなかった", remembered);
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        var q = _questions[_currentIndex];
        await RecordAndAdvance(q.Word, "(スキップ)", false);
    }

    [RelayCommand]
    private async Task ExitAsync()
    {
        if (_session is null)
        {
            Cancelled?.Invoke();
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            $"テストを中断しますか? ここまでの回答 ({_answered.Count} 問) は結果に反映されます。",
            "テスト中断");
        if (!confirmed) return;

        await _testService.EndSessionAsync(_session.Id);

        if (_answered.Count == 0)
        {
            Cancelled?.Invoke();
        }
        else
        {
            Completed?.Invoke(new TestSessionSummary(_options, _answered.ToList()));
        }
    }

    private async Task RecordAndAdvance(Word word, string userInput, bool isCorrect)
    {
        if (_session is null) return;

        try
        {
            await _testService.RecordAnswerAsync(_session.Id, word.Id, userInput, isCorrect);
            _answered.Add(new TestAnsweredQuestion(word, userInput, isCorrect));
            StatusMessage = isCorrect ? "○ 正解" : $"× 不正解 (正答: {word.Meaning})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record answer");
            await _dialogService.ShowErrorAsync(
                $"回答の記録に失敗しました。\n\n{ex.GetType().Name}: {ex.Message}");
            return;
        }

        _currentIndex++;
        if (_currentIndex >= _questions.Count)
        {
            await _testService.EndSessionAsync(_session.Id);
            Completed?.Invoke(new TestSessionSummary(_options, _answered.ToList()));
        }
        else
        {
            LoadCurrent();
        }
    }
}
