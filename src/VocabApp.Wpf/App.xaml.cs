using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using VocabApp.Infrastructure;
using VocabApp.Infrastructure.Persistence;
using VocabApp.Wpf.Services;
using VocabApp.Wpf.ViewModels;
using VocabApp.Wpf.Views;

namespace VocabApp.Wpf;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
            ?? throw new InvalidOperationException("Host has not been initialised yet.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VocabApp");
        Directory.CreateDirectory(appDataDir);

        var dbPath = Path.Combine(appDataDir, "vocab.db");
        var logPath = Path.Combine(appDataDir, "logs", "vocab-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddVocabInfrastructure($"Data Source={dbPath}");

                services.AddSingleton<IDialogService, DialogService>();

                services.AddTransient<WordEditorViewModel>();
                services.AddSingleton<WordListViewModel>();
                services.AddSingleton<ImportExportViewModel>();
                services.AddSingleton<TestSetupViewModel>();
                services.AddTransient<TestSessionViewModel>();
                services.AddTransient<TestResultViewModel>();
                services.AddSingleton<TestHostViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await InitializeDatabaseAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _host.Services.GetRequiredService<MainWindowViewModel>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private async Task InitializeDatabaseAsync()
    {
        if (_host is null)
        {
            return;
        }
        var factory = _host.Services.GetRequiredService<IDbContextFactory<VocabDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        // Phase 0/1: schema is created directly from the model. Switch to
        // Database.MigrateAsync() once migrations are introduced (see README).
        await db.Database.EnsureCreatedAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Database initialised at {Path}",
            db.Database.GetDbConnection().DataSource);
    }
}
