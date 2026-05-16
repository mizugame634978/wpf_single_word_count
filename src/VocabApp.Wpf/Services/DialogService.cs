using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using VocabApp.Core.Models;
using VocabApp.Wpf.ViewModels;
using VocabApp.Wpf.Views;

namespace VocabApp.Wpf.Services;

public class DialogService : IDialogService
{
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
        MessageBox.Show(
            Application.Current?.MainWindow!,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string message, string title = "情報")
    {
        MessageBox.Show(
            Application.Current?.MainWindow!,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
        Clipboard.SetDataObject(text, copy: true);
    }
}
