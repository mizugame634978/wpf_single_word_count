using Microsoft.EntityFrameworkCore;
using VocabApp.Core.Models;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;

namespace VocabApp.Infrastructure.Persistence;

public class TestSessionService : ITestSessionService
{
    private readonly IDbContextFactory<VocabDbContext> _factory;

    public TestSessionService(IDbContextFactory<VocabDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<TestSessionStartResult> StartSessionAsync(
        TestSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Count <= 0)
        {
            throw new ArgumentException("Count must be positive.", nameof(options));
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var candidates = await BuildCandidateQuery(db, options)
            .Include(w => w.Tags)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("出題対象の単語が見つかりませんでした。");
        }

        var picked = candidates
            .OrderBy(_ => Random.Shared.Next())
            .Take(options.Count)
            .ToList();

        var session = new TestSession
        {
            Mode = options.Mode,
            StartedAt = DateTime.UtcNow,
        };
        db.TestSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var questions = options.Mode == TestMode.MultipleChoiceEnglishToJapanese
            ? await BuildMultipleChoiceQuestionsAsync(db, picked, cancellationToken)
            : picked.Select(w => new TestQuestion(w)).ToList();

        return new TestSessionStartResult(session, questions);
    }

    public async Task RecordAnswerAsync(
        int sessionId,
        int wordId,
        string userInput,
        bool isCorrect,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var word = await db.Words.FirstOrDefaultAsync(w => w.Id == wordId, cancellationToken)
            ?? throw new InvalidOperationException($"Word id={wordId} not found.");

        var answeredAt = DateTime.UtcNow;
        word.TimesAsked += 1;
        if (isCorrect)
        {
            word.TimesCorrect += 1;
        }
        word.LastAskedAt = answeredAt;
        word.Mastery = MasteryRule.NextMastery(word.Mastery, isCorrect);
        word.UpdatedAt = answeredAt;

        db.TestAnswers.Add(new TestAnswer
        {
            TestSessionId = sessionId,
            WordId = wordId,
            UserInput = userInput,
            IsCorrect = isCorrect,
            AnsweredAt = answeredAt,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EndSessionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var session = await db.TestSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return;
        }
        session.EndedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Word> BuildCandidateQuery(VocabDbContext db, TestSessionOptions options)
    {
        if (options.OverrideWordIds is { Count: > 0 } ids)
        {
            var idSet = ids.ToList();
            return db.Words.Where(w => idSet.Contains(w.Id));
        }

        IQueryable<Word> query = db.Words;

        switch (options.Range)
        {
            case TestRange.Tag:
                var tag = (options.TagFilter ?? string.Empty).Trim();
                if (tag.Length == 0)
                {
                    throw new ArgumentException("TagFilter is required when Range == Tag.", nameof(options));
                }
                query = query.Where(w => w.Tags.Any(t => t.Name == tag));
                break;
            case TestRange.Weak:
                // mastery <= 1 もしくは TimesAsked>0 かつ正答率<70%
                query = query.Where(w => w.Mastery <= 1
                    || (w.TimesAsked > 0 && (double)w.TimesCorrect / w.TimesAsked < 0.7));
                break;
            case TestRange.Unasked:
                query = query.Where(w => w.TimesAsked == 0);
                break;
            case TestRange.All:
            default:
                break;
        }

        return query;
    }

    private static async Task<IReadOnlyList<TestQuestion>> BuildMultipleChoiceQuestionsAsync(
        VocabDbContext db,
        IReadOnlyList<Word> picked,
        CancellationToken cancellationToken)
    {
        var pickedIds = picked.Select(p => p.Id).ToHashSet();

        // ディストラクターの母集団: 出題対象以外の全単語。多すぎなければ全件
        // ロードしてもよい (個人用なので数千語想定)。
        var pool = await db.Words
            .Where(w => !pickedIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Meaning, w.PartOfSpeech, TagIds = w.Tags.Select(t => t.Id).ToList() })
            .ToListAsync(cancellationToken);

        var questions = new List<TestQuestion>(picked.Count);
        foreach (var word in picked)
        {
            var wordTagIds = word.Tags.Select(t => t.Id).ToHashSet();
            var preferred = pool
                .Where(p => wordTagIds.Overlaps(p.TagIds) || p.PartOfSpeech == word.PartOfSpeech)
                .Select(p => p.Meaning)
                .Distinct()
                .Where(m => !string.Equals(m, word.Meaning, StringComparison.Ordinal))
                .OrderBy(_ => Random.Shared.Next())
                .Take(3)
                .ToList();

            // 不足分は全プールからランダムで補う
            if (preferred.Count < 3)
            {
                var fallback = pool
                    .Select(p => p.Meaning)
                    .Distinct()
                    .Where(m => !string.Equals(m, word.Meaning, StringComparison.Ordinal)
                                && !preferred.Contains(m))
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(3 - preferred.Count)
                    .ToList();
                preferred.AddRange(fallback);
            }

            // それでも 4 択にならない場合 (語数 < 4) は埋め草を入れる
            while (preferred.Count < 3)
            {
                preferred.Add("(該当なし)");
            }

            var choices = preferred.Append(word.Meaning).OrderBy(_ => Random.Shared.Next()).ToList();
            var correctIndex = choices.IndexOf(word.Meaning);
            questions.Add(new TestQuestion(word, choices, correctIndex));
        }
        return questions;
    }
}
