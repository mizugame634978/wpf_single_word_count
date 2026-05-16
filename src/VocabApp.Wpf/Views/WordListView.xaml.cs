using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VocabApp.Wpf.Views;

public partial class WordListView : UserControl
{
    public WordListView()
    {
        InitializeComponent();

        // Ctrl+F → 検索ボックスにフォーカス。InputBindings はバインディングの
        // タイミング都合で動かないことがあるため、UserControl レベルで
        // PreviewKeyDown を見る。
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }
}
