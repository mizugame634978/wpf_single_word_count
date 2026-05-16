using VocabApp.Core.Models;

namespace VocabApp.Wpf.Services;

public interface IDialogService
{
    /// <summary>
    /// 単語の編集ダイアログを表示する。
    /// </summary>
    /// <param name="word">編集対象。null の場合は新規追加モード。</param>
    /// <returns>OK で閉じた場合は編集後の Word。Cancel の場合は null。</returns>
    Task<Word?> ShowWordEditorAsync(Word? word);

    Task<bool> ConfirmAsync(string message, string title);

    Task ShowErrorAsync(string message, string title = "エラー");
}
