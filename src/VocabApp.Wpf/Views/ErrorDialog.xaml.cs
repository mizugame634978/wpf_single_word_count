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

        // ヒント末尾に Ctrl+C 案内を常に付けて、ユーザを手動コピーへ誘導する。
        var copyHint = "本文は選択済みです。Ctrl+C でコピーできます。";
        HintTextBlock.Text = string.IsNullOrWhiteSpace(hint)
            ? copyHint
            : $"{hint}\n\n{copyHint}";

        // 開いた瞬間に本文を全選択 + フォーカスして、ユーザが Ctrl+C のみで
        // 取得できるようにする。アプリ側からのクリップボード書き込みは
        // 環境によっては失敗するため、ボタン経由のコピーは提供しない
        // (docs/coding-rules.md 参照)。
        Loaded += (_, _) =>
        {
            MessageTextBox.Focus();
            MessageTextBox.SelectAll();
        };
    }
}
