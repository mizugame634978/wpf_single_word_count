using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VocabApp.Core.Services;
using VocabApp.Infrastructure.Csv;
using VocabApp.Infrastructure.Persistence;

namespace VocabApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVocabInfrastructure(
        this IServiceCollection services,
        string sqliteConnectionString)
    {
        services.AddDbContextFactory<VocabDbContext>(options =>
            options.UseSqlite(sqliteConnectionString));

        services.AddSingleton<IVocabularyService, VocabularyService>();
        services.AddSingleton<ICsvService, CsvService>();
        services.AddSingleton<IPromptTemplateService, PromptTemplateService>();
        services.AddSingleton<ITestSessionService, TestSessionService>();

        return services;
    }
}
