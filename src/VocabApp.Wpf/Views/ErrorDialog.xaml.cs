using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace VocabApp.Wpf.Views;

public partial class ErrorDialog : Window
{
    public ErrorDialog(string title, string headerText, string message, string? hint, bool isError)
    {
        InitializeComponent();
        Title = title;
        HeaderTextBlock.Text = headerText;
        HeaderTextBlock.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20))
            : new SolidColorBrush(Color.FromRgb(0x20, 0x40, 0x80));
        MessageTextBox.Text = message;
        if (string.IsNullOrWhiteSpace(hint))
        {
            HintTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            HintTextBlock.Text = hint;
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Clipboard.SetText(MessageTextBox.Text);
                CopyButton.Content = "コピーしました";
                return;
            }
            catch (ExternalException) when (attempt < 5)
            {
                Thread.Sleep(50);
            }
            catch
            {
                CopyButton.Content = "コピー失敗";
                return;
            }
        }
    }
}
