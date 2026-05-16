using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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
}
