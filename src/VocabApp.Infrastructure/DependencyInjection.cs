using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VocabApp.Core.Services;
using VocabApp.Infrastructure.Persistence;

namespace VocabApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVocabInfrastructure(
        this IServiceCollection services,
        string sqliteConnectionString)
    {
        services.AddDbContext<VocabDbContext>(options =>
            options.UseSqlite(sqliteConnectionString));

        services.AddScoped<IVocabularyService, VocabularyService>();

        return services;
    }
}
