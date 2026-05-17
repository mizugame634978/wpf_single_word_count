using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using VocabApp.Core.Services;
using VocabApp.Core.Utilities;
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
        var settingsPath = Path.Combine(appDataDir, "settings.json");
        var logPath = Path.Combine(appDataDir, "logs", "vocab-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();

        // 未処理例外を全部 ErrorDialog に流す。
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddVocabInfrastructure(
                    $"Data Source={dbPath}",
                    settingsPath);

                services.AddSingleton<IDialogService, DialogService>();

                services.AddTransient<WordEditorViewModel>();
                services.AddSingleton<WordListViewModel>();
                services.AddSingleton<ImportExportViewModel>();
                services.AddSingleton<TestSetupViewModel>();
                services.AddTransient<TestSessionViewModel>();
                services.AddTransient<TestResultViewModel>();
                services.AddSingleton<TestHostViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await LoadSettingsAsync();
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

    private async Task LoadSettingsAsync()
    {
        if (_host is null) return;
        var settings = _host.Services.GetRequiredService<ISettingsService>();
        await settings.LoadAsync();
    }

    private async Task InitializeDatabaseAsync()
    {
        if (_host is null)
        {
            return;
        }
        var factory = _host.Services.GetRequiredService<IDbContextFactory<VocabDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        // Phase 0..4: schema is created directly from the model.
        await db.Database.EnsureCreatedAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Database initialised at {Path}",
            db.Database.GetDbConnection().DataSource);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unhandled UI exception");
        ShowUnhandled(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Logger.Fatal(ex, "Unhandled non-UI exception");
            ShowUnhandled(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unobserved task exception");
        ShowUnhandled(e.Exception);
        e.SetObserved();
    }

    private void ShowUnhandled(Exception ex)
    {
        try
        {
            // UI スレッド以外から飛んでくる可能性があるので Dispatcher 経由で表示
            Dispatcher.Invoke(() =>
            {
                var dialog = _host?.Services.GetService<IDialogService>();
                if (dialog is null)
                {
                    MessageBox.Show(ex.ToString(), "予期しないエラー",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _ = dialog.ShowErrorAsync(
                    ExceptionFormatter.FormatWithStack(ex),
                    "予期しないエラー");
            });
        }
        catch
        {
            // 二次例外は握りつぶす。最低限ログには出ている。
        }
    }
}
