using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VocabApp.Core.Models;
using VocabApp.Core.Services;

namespace VocabApp.Infrastructure.Llm;

/// <summary>
/// Gemini REST API (Google AI Studio) を直接叩いて単語を生成する実装。
/// プロンプト本体は <see cref="IPromptTemplateService"/> を再利用するので、
/// 外部 LLM 取り込みルート (Phase 2) と完全に同じ仕様で動く。
/// 応答は CSV として返るのでそのまま <see cref="ICsvService.ParseAsync"/> でパース。
/// </summary>
public class GeminiVocabularyGenerator : IVocabularyGenerator
{
    public const string DefaultModel = "gemini-2.5-flash";

    public const string HttpClientName = "GeminiVocabularyGenerator";

    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ISettingsService _settings;
    private readonly ISecretProtector _protector;
    private readonly IPromptTemplateService _promptService;
    private readonly ICsvService _csvService;
    private readonly ILogger<GeminiVocabularyGenerator> _logger;

    public GeminiVocabularyGenerator(
        IHttpClientFactory httpFactory,
        ISettingsService settings,
        ISecretProtector protector,
        IPromptTemplateService promptService,
        ICsvService csvService,
        ILogger<GeminiVocabularyGenerator> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings;
        _protector = protector;
        _promptService = promptService;
        _csvService = csvService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Word>> GenerateAsync(
        VocabularyGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _protector.Unprotect(_settings.Current.GeminiApiKeyEncrypted);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API キーが設定されていません。設定画面から登録してください。");
        }

        var prompt = _promptService.BuildVocabularyPrompt(
            new VocabularyPromptRequest(request.Theme, request.Count, request.Level));

        var responseText = await CallApiAsync(apiKey, prompt, cancellationToken);
        var csvBody = StripCodeFences(responseText);

        _logger.LogInformation("Gemini returned {Chars} chars of CSV", csvBody.Length);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvBody));
        var parsed = await _csvService.ParseAsync(stream, cancellationToken);

        if (parsed.Rows.Count == 0)
        {
            var firstError = parsed.Errors.FirstOrDefault();
            throw new InvalidOperationException(
                firstError is null
                    ? "Gemini の応答から CSV を取得できませんでした。"
                    : $"Gemini の応答が想定した CSV になっていません。\n行 {firstError.LineNumber}: {firstError.Message}");
        }

        return parsed.Rows.Select(r => r.Word).ToList();
    }

    /// <summary>
    /// 1 トークンだけ生成させて API キー疎通を確認する。
    /// </summary>
    public async Task PingAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API キーが空です。", nameof(apiKey));
        }
        await CallApiAsync(apiKey, "ping", cancellationToken, maxOutputTokens: 1);
    }

    private async Task<string> CallApiAsync(
        string apiKey,
        string prompt,
        CancellationToken cancellationToken,
        int? maxOutputTokens = null)
    {
        using var http = _httpFactory.CreateClient(HttpClientName);
        var url = string.Format(Endpoint, DefaultModel);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Add("x-goog-api-key", apiKey);

        var body = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Parts = new[] { new GeminiPart { Text = prompt } },
                },
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0.7,
                MaxOutputTokens = maxOutputTokens,
            },
        };

        requestMessage.Content = JsonContent.Create(body, options: JsonOpts);

        using var response = await http.SendAsync(requestMessage, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API HTTP {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new HttpRequestException(
                $"Gemini API HTTP {(int)response.StatusCode} {response.StatusCode}: {Truncate(responseBody, 500)}");
        }

        var parsed = JsonSerializer.Deserialize<GeminiResponse>(responseBody, JsonOpts)
            ?? throw new InvalidOperationException("Gemini API の応答を JSON としてデコードできませんでした。");

        var candidate = parsed.Candidates?.FirstOrDefault()
            ?? throw new InvalidOperationException("Gemini API が候補を返しませんでした。応答: " + Truncate(responseBody, 500));

        var text = candidate.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini API が空のテキストを返しました。");
        }
        return text;
    }

    private static string StripCodeFences(string text)
    {
        // ``` または ```csv の囲みを除去する。
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```"))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;
        var afterOpening = trimmed[(firstNewline + 1)..];

        var endIndex = afterOpening.LastIndexOf("```", StringComparison.Ordinal);
        if (endIndex < 0) return afterOpening.Trim();
        return afterOpening[..endIndex].Trim();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class GeminiRequest
    {
        public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
    }

    private sealed class GeminiPart
    {
        public string? Text { get; set; }
    }

    private sealed class GeminiGenerationConfig
    {
        public double? Temperature { get; set; }
        public int? MaxOutputTokens { get; set; }
    }

    private sealed class GeminiResponse
    {
        public GeminiCandidate[]? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
        public string? FinishReason { get; set; }
    }
}
