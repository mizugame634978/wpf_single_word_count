using System.Windows.Controls;
using VocabApp.Wpf.ViewModels;

namespace VocabApp.Wpf.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PasswordBox.Password はセキュリティ上の理由から DependencyProperty では
    /// なく、データバインディングできないため、PasswordChanged を VM へ流す。
    /// VM 側は <see cref="SettingsViewModel.GeminiApiKeyInput"/> で受ける。
    /// </summary>
    private void OnGeminiKeyPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not PasswordBox box) return;
        if (DataContext is SettingsViewModel vm)
        {
            vm.GeminiApiKeyInput = box.Password;
        }
    }
}
