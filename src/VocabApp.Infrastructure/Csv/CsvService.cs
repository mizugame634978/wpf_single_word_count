using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VocabApp.Core.Csv;
using VocabApp.Core.Models;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;
using VocabApp.Infrastructure.Persistence;

namespace VocabApp.Infrastructure.Csv;

public class CsvService : ICsvService
{
    private readonly IDbContextFactory<VocabDbContext> _factory;
    private readonly ILogger<CsvService> _logger;

    public CsvService(IDbContextFactory<VocabDbContext> factory, ILogger<CsvService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<ParsedCsv> ParseAsync(Stream input, CancellationToken cancellationToken = default)
    {
        var parsed = new ParsedCsv();
        var rows = ParseRows(input, parsed.Errors);
        foreach (var row in rows)
        {
            // Tag は名前だけ。DB 解決は呼び出し側に任せる (LLM 生成プレビュー用)。
            row.Word.Tags = row.TagNames.Select(n => new Tag { Name = n }).ToList();
            parsed.Rows.Add(new ParsedCsvRow(row.LineNumber, row.Word, row.PresentColumns));
        }
        return Task.FromResult(parsed);
    }

    public async Task<ImportResult> ImportAsync(
        Stream input,
        ConflictMode conflictMode,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportResult();
        var parsedRows = ParseRows(input, result.Errors);
        if (parsedRows.Count == 0)
        {
            return result;
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        // word + part_of_speech をキーに既存単語を一括ロード
        var keys = parsedRows
            .Select(r => (r.Word.Text, r.Word.PartOfSpeech))
            .Distinct()
            .ToList();

        var texts = keys.Select(k => k.Text).Distinct().ToList();
        var candidates = await db.Words
            .Include(w => w.Tags)
            .Where(w => texts.Contains(w.Text))
            .ToListAsync(cancellationToken);

        var byKey = candidates
            .GroupBy(w => (w.Text, w.PartOfSpeech))
            .ToDictionary(g => g.Key, g => g.First());

        // 取り込み中に解決した Tag インスタンスをキャッシュ (大小無視)
        var tagCache = await db.Tags.ToDictionaryAsync(
            t => t.Name, t => t, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in parsedRows)
        {
            try
            {
                var key = (row.Word.Text, row.Word.PartOfSpeech);
                byKey.TryGetValue(key, out var existing);

                if (existing is null || conflictMode == ConflictMode.AddAsNew)
                {
                    var newWord = BuildNewWord(row, tagCache);
                    db.Words.Add(newWord);
                    result.Added++;
                }
                else if (conflictMode == ConflictMode.Skip)
                {
                    result.Skipped++;
                }
                else
                {
                    ApplyOverwrite(existing, row, tagCache);
                    result.Updated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Row {Line} failed to import", row.LineNumber);
                result.Errors.Add(new ImportRowError(row.LineNumber, ex.Message));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task ExportAsync(
        Stream output,
        ExportOptions options,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var words = await db.Words
            .Include(w => w.Tags)
            .AsNoTracking()
            .OrderBy(w => w.Text)
            .ToListAsync(cancellationToken);

        var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                NewLine = Environment.NewLine,
            };
            using var csv = new CsvWriter(writer, config);

            csv.WriteField(CsvSchema.Word);
            csv.WriteField(CsvSchema.Meaning);
            csv.WriteField(CsvSchema.PartOfSpeech);
            csv.WriteField(CsvSchema.Example);
            csv.WriteField(CsvSchema.Tags);
            csv.WriteField(CsvSchema.Notes);
            if (options.IncludeLearningStats)
            {
                csv.WriteField(CsvSchema.TimesAsked);
                csv.WriteField(CsvSchema.TimesCorrect);
                csv.WriteField(CsvSchema.LastAskedAt);
                csv.WriteField(CsvSchema.Mastery);
            }
            await csv.NextRecordAsync();

            foreach (var w in words)
            {
                csv.WriteField(w.Text);
                csv.WriteField(w.Meaning);
                csv.WriteField(PartOfSpeechConverter.Format(w.PartOfSpeech));
                csv.WriteField(w.Example ?? string.Empty);
                csv.WriteField(string.Join(CsvSchema.InCellTagSeparator,
                    w.Tags.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)));
                csv.WriteField(w.Notes ?? string.Empty);
                if (options.IncludeLearningStats)
                {
                    csv.WriteField(w.TimesAsked);
                    csv.WriteField(w.TimesCorrect);
                    csv.WriteField(w.LastAskedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
                    csv.WriteField(w.Mastery);
                }
                await csv.NextRecordAsync();
            }
        }
    }

    private static Word BuildNewWord(CsvParsedRow row, Dictionary<string, Tag> tagCache)
    {
        var now = DateTime.UtcNow;
        var word = new Word
        {
            Text = row.Word.Text,
            Meaning = row.Word.Meaning,
            PartOfSpeech = row.Word.PartOfSpeech,
            Example = row.Word.Example,
            Notes = row.Word.Notes,
            TimesAsked = row.PresentColumns.Contains(CsvSchema.TimesAsked) ? row.Word.TimesAsked : 0,
            TimesCorrect = row.PresentColumns.Contains(CsvSchema.TimesCorrect) ? row.Word.TimesCorrect : 0,
            LastAskedAt = row.PresentColumns.Contains(CsvSchema.LastAskedAt) ? row.Word.LastAskedAt : null,
            Mastery = row.PresentColumns.Contains(CsvSchema.Mastery) ? row.Word.Mastery : 0,
            CreatedAt = now,
            UpdatedAt = now,
            Tags = ResolveTags(row.TagNames, tagCache),
        };
        return word;
    }

    private static void ApplyOverwrite(Word existing, CsvParsedRow row, Dictionary<string, Tag> tagCache)
    {
        existing.Meaning = row.Word.Meaning;
        if (row.PresentColumns.Contains(CsvSchema.PartOfSpeech))
        {
            existing.PartOfSpeech = row.Word.PartOfSpeech;
        }
        if (row.PresentColumns.Contains(CsvSchema.Example))
        {
            existing.Example = row.Word.Example;
        }
        if (row.PresentColumns.Contains(CsvSchema.Notes))
        {
            existing.Notes = row.Word.Notes;
        }
        if (row.PresentColumns.Contains(CsvSchema.Tags))
        {
            existing.Tags.Clear();
            foreach (var tag in ResolveTags(row.TagNames, tagCache))
            {
                existing.Tags.Add(tag);
            }
        }
        if (row.PresentColumns.Contains(CsvSchema.TimesAsked))
        {
            existing.TimesAsked = row.Word.TimesAsked;
        }
        if (row.PresentColumns.Contains(CsvSchema.TimesCorrect))
        {
            existing.TimesCorrect = row.Word.TimesCorrect;
        }
        if (row.PresentColumns.Contains(CsvSchema.LastAskedAt))
        {
            existing.LastAskedAt = row.Word.LastAskedAt;
        }
        if (row.PresentColumns.Contains(CsvSchema.Mastery))
        {
            existing.Mastery = row.Word.Mastery;
        }
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private static List<Tag> ResolveTags(IReadOnlyList<string> names, Dictionary<string, Tag> tagCache)
    {
        var result = new List<Tag>(names.Count);
        foreach (var name in names)
        {
            if (tagCache.TryGetValue(name, out var tag))
            {
                result.Add(tag);
            }
            else
            {
                var fresh = new Tag { Name = name };
                tagCache[name] = fresh;
                result.Add(fresh);
            }
        }
        return result;
    }

    private static List<CsvParsedRow> ParseRows(Stream input, List<ImportRowError> errors)
    {
        var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
        };
        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
        {
            errors.Add(new ImportRowError(0, "CSV が空です。"));
            return new List<CsvParsedRow>();
        }
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var presentColumns = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);

        foreach (var required in CsvSchema.RequiredColumns)
        {
            if (!presentColumns.Contains(required))
            {
                errors.Add(new ImportRowError(1,
                    $"必須列 '{required}' が見つかりません。"));
                return new List<CsvParsedRow>();
            }
        }

        var rows = new List<CsvParsedRow>();
        while (csv.Read())
        {
            var lineNumber = csv.Parser.Row;
            try
            {
                var text = csv.GetField(CsvSchema.Word)?.Trim() ?? string.Empty;
                var meaning = csv.GetField(CsvSchema.Meaning)?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    errors.Add(new ImportRowError(lineNumber, "word が空です。"));
                    continue;
                }
                if (string.IsNullOrEmpty(meaning))
                {
                    errors.Add(new ImportRowError(lineNumber, "meaning が空です。"));
                    continue;
                }

                var word = new Word
                {
                    Text = text,
                    Meaning = meaning,
                    PartOfSpeech = presentColumns.Contains(CsvSchema.PartOfSpeech)
                        ? PartOfSpeechConverter.Parse(csv.GetField(CsvSchema.PartOfSpeech))
                        : null,
                    Example = GetOptionalString(csv, presentColumns, CsvSchema.Example),
                    Notes = GetOptionalString(csv, presentColumns, CsvSchema.Notes),
                    TimesAsked = GetOptionalInt(csv, presentColumns, CsvSchema.TimesAsked, 0),
                    TimesCorrect = GetOptionalInt(csv, presentColumns, CsvSchema.TimesCorrect, 0),
                    Mastery = GetOptionalInt(csv, presentColumns, CsvSchema.Mastery, 0),
                    LastAskedAt = GetOptionalDate(csv, presentColumns, CsvSchema.LastAskedAt),
                };

                var tagNames = presentColumns.Contains(CsvSchema.Tags)
                    ? ParseTagCell(csv.GetField(CsvSchema.Tags))
                    : Array.Empty<string>();

                rows.Add(new CsvParsedRow(lineNumber, word, presentColumns, tagNames));
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(lineNumber, ex.Message));
            }
        }
        return rows;
    }

    private static string? GetOptionalString(CsvReader csv, HashSet<string> columns, string column)
    {
        if (!columns.Contains(column))
        {
            return null;
        }
        var value = csv.GetField(column);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int GetOptionalInt(CsvReader csv, HashSet<string> columns, string column, int defaultValue)
    {
        if (!columns.Contains(column))
        {
            return defaultValue;
        }
        var raw = csv.GetField(column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"{column} の値 '{raw}' を整数に変換できません。");
        }
        return parsed;
    }

    private static DateTime? GetOptionalDate(CsvReader csv, HashSet<string> columns, string column)
    {
        if (!columns.Contains(column))
        {
            return null;
        }
        var raw = csv.GetField(column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        if (!DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new FormatException($"{column} の値 '{raw}' を日時に変換できません (ISO 8601 を期待)。");
        }
        return parsed;
    }

    private static IReadOnlyList<string> ParseTagCell(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
        {
            return Array.Empty<string>();
        }
        return cell
            .Split(CsvSchema.InCellTagSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record CsvParsedRow(
        int LineNumber,
        Word Word,
        HashSet<string> PresentColumns,
        IReadOnlyList<string> TagNames);
}
