using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using VocabApp.Core.Models;
using VocabApp.Wpf.ViewModels;
using VocabApp.Wpf.Views;

namespace VocabApp.Wpf.Services;

public class DialogService : IDialogService
{
    private const string LogHint = "詳細はログを確認してください: %AppData%\\VocabApp\\logs";

    private readonly IServiceProvider _services;

    public DialogService(IServiceProvider services)
    {
        _services = services;
    }

    public Task<Word?> ShowWordEditorAsync(Word? word)
    {
        var vm = _services.GetRequiredService<WordEditorViewModel>();
        vm.Load(word);

        var window = new WordEditorWindow
        {
            DataContext = vm,
            Owner = Application.Current?.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var ok = window.ShowDialog() == true;
        return Task.FromResult(ok ? vm.ToWord() : null);
    }

    public Task<bool> ConfirmAsync(string message, string title)
    {
        var result = MessageBox.Show(
            Application.Current?.MainWindow!,
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);
        return Task.FromResult(result == MessageBoxResult.OK);
    }

    public Task ShowErrorAsync(string message, string title = "エラー")
    {
        ShowCopyableDialog(title, title, message, LogHint, isError: true);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string message, string title = "情報")
    {
        ShowCopyableDialog(title, title, message, hint: null, isError: false);
        return Task.CompletedTask;
    }

    public Task<string?> ShowOpenFileAsync(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false,
        };
        var ok = dialog.ShowDialog(Application.Current?.MainWindow) == true;
        return Task.FromResult<string?>(ok ? dialog.FileName : null);
    }

    public Task<string?> ShowSaveFileAsync(string title, string filter, string? defaultFileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            OverwritePrompt = true,
            AddExtension = true,
            FileName = defaultFileName ?? string.Empty,
        };
        var ok = dialog.ShowDialog(Application.Current?.MainWindow) == true;
        return Task.FromResult<string?>(ok ? dialog.FileName : null);
    }

    public void SetClipboardText(string text)
    {
        // クリップボードはほかのアプリ (クリップボードマネージャ等) にロック
        // されていると ExternalException を投げる。短い間隔でリトライする。
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (ExternalException) when (attempt < 5)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static void ShowCopyableDialog(string title, string headerText, string message, string? hint, bool isError)
    {
        var dialog = new ErrorDialog(title, headerText, message, hint, isError)
        {
            Owner = Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }
}
