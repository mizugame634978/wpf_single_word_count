using System.Windows;

namespace VocabApp.Wpf.Views;

public partial class WordEditorWindow : Window
{
    public WordEditorWindow()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
