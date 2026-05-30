using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VocabApp.Core.Services;
using VocabApp.Infrastructure.Csv;
using VocabApp.Infrastructure.Llm;
using VocabApp.Infrastructure.Persistence;
using VocabApp.Infrastructure.Security;
using VocabApp.Infrastructure.Settings;

namespace VocabApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVocabInfrastructure(
        this IServiceCollection services,
        string sqliteConnectionString,
        string settingsFilePath)
    {
        services.AddDbContextFactory<VocabDbContext>(options =>
            options.UseSqlite(sqliteConnectionString));

        services.AddSingleton<IVocabularyService, VocabularyService>();
        services.AddSingleton<ICsvService, CsvService>();
        services.AddSingleton<IPromptTemplateService, PromptTemplateService>();
        services.AddSingleton<ITestSessionService, TestSessionService>();
        services.AddSingleton<ISettingsService>(_ => new JsonSettingsService(settingsFilePath));
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();

        // Gemini 用 HttpClient (タイムアウトは寛容に。生成は数秒〜数十秒かかる)
        // 名前付きクライアントとして登録し、Singleton な Generator から
        // IHttpClientFactory.CreateClient で都度取り出す (captive dependency 回避)
        services.AddHttpClient(GeminiVocabularyGenerator.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddSingleton<GeminiVocabularyGenerator>();
        services.AddSingleton<IVocabularyGenerator>(sp =>
            sp.GetRequiredService<GeminiVocabularyGenerator>());

        return services;
    }
}
