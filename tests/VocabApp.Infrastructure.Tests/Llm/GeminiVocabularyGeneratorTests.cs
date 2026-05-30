using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VocabApp.Core.Models;
using VocabApp.Core.Services;
using VocabApp.Infrastructure.Csv;
using VocabApp.Infrastructure.Llm;
using VocabApp.Infrastructure.Persistence;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Llm;

public class GeminiVocabularyGeneratorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<VocabDbContext> _factory;

    public GeminiVocabularyGeneratorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<VocabDbContext>()
            .UseSqlite(_connection)
            .Options;
        _factory = new TestDbContextFactory(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private GeminiVocabularyGenerator CreateGenerator(
        IHttpClientFactory factory,
        ISettingsService settings)
    {
        return new GeminiVocabularyGenerator(
            factory,
            settings,
            new PlaintextProtector(),
            new PromptTemplateService(),
            new CsvService(_factory, NullLogger<CsvService>.Instance),
            NullLogger<GeminiVocabularyGenerator>.Instance);
    }

    [Fact]
    public async Task GenerateAsync_ParsesCsvResponse_AsWords()
    {
        var settings = new FakeSettings
        {
            Current = { GeminiApiKeyEncrypted = "fake-key" },
        };

        // Gemini が返してきた CSV を含む応答 JSON をモック。
        var csvText =
            "word,meaning,part_of_speech,example,tags,notes\n" +
            "abandon,放棄する,verb,He abandoned the plan.,toeic,日本語の解説\n" +
            "boost,押し上げる,verb,Boost morale.,business,意義を強化する\n";
        var responseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = csvText } } } }
            }
        });

        var factory = new FakeHttpClientFactory(req =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.ToString().Should().Contain("gemini-2.5-flash");
            req.Headers.GetValues("x-goog-api-key").Should().Contain("fake-key");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        });

        var gen = CreateGenerator(factory, settings);
        var words = await gen.GenerateAsync(new VocabularyGenerationRequest("toeic verbs", 2));

        words.Should().HaveCount(2);
        words[0].Text.Should().Be("abandon");
        words[0].Meaning.Should().Be("放棄する");
        words[0].PartOfSpeech.Should().Be(PartOfSpeech.Verb);
        words[0].Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "toeic" });
        words[1].Text.Should().Be("boost");
    }

    [Fact]
    public async Task GenerateAsync_StripsCodeFences_BeforeParsing()
    {
        var settings = new FakeSettings { Current = { GeminiApiKeyEncrypted = "k" } };
        var csvText = "```csv\nword,meaning\nabandon,放棄する\n```";
        var responseJson = JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text = csvText } } } } }
        });
        var factory = new FakeHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var gen = CreateGenerator(factory, settings);
        var words = await gen.GenerateAsync(new VocabularyGenerationRequest("x", 1));

        words.Should().ContainSingle(w => w.Text == "abandon" && w.Meaning == "放棄する");
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenApiKeyMissing()
    {
        var settings = new FakeSettings();   // GeminiApiKeyEncrypted == null
        var factory = new FakeHttpClientFactory(_ => throw new InvalidOperationException("must not be called"));

        var gen = CreateGenerator(factory, settings);
        var act = async () => await gen.GenerateAsync(new VocabularyGenerationRequest("x", 1));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*API キー*");
    }

    [Fact]
    public async Task GenerateAsync_PropagatesHttpErrors()
    {
        var settings = new FakeSettings { Current = { GeminiApiKeyEncrypted = "k" } };
        var factory = new FakeHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"message\":\"invalid api key\"}}"),
        });

        var gen = CreateGenerator(factory, settings);
        var act = async () => await gen.GenerateAsync(new VocabularyGenerationRequest("x", 1));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PingAsync_SendsMinimalRequest_WithGivenKey()
    {
        var settings = new FakeSettings();
        HttpRequestMessage? captured = null;
        var factory = new FakeHttpClientFactory(req =>
        {
            captured = req;
            var responseJson = JsonSerializer.Serialize(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = "ok" } } } } }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        });

        var gen = CreateGenerator(factory, settings);
        await gen.PingAsync("direct-key");

        captured.Should().NotBeNull();
        captured!.Headers.GetValues("x-goog-api-key").Should().Contain("direct-key");
    }

    private sealed class TestDbContextFactory : IDbContextFactory<VocabDbContext>
    {
        private readonly DbContextOptions<VocabDbContext> _options;
        public TestDbContextFactory(DbContextOptions<VocabDbContext> options) => _options = options;
        public VocabDbContext CreateDbContext() => new(_options);
    }

    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Current { get; set; } = new();
        public event EventHandler? SettingsChanged;
        public Task LoadAsync(CancellationToken c = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken c = default) => Task.CompletedTask;
        public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken c = default)
        {
            mutate(Current);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class PlaintextProtector : ISecretProtector
    {
        public string? Protect(string? plaintext) => plaintext;
        public string? Unprotect(string? ciphertext) => ciphertext;
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        public HttpClient CreateClient(string name) =>
            new(new FakeHandler(_handler)) { Timeout = TimeSpan.FromSeconds(10) };

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _h;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> h) => _h = h;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_h(request));
        }
    }
}
