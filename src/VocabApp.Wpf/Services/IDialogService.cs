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

    Task ShowInfoAsync(string message, string title = "情報");

    /// <summary>
    /// 任意のテキストをコピー可能な形で表示する。長い本文を見せて
    /// 手動コピーしてもらいたい場合 (LLM 用プロンプト等) に使う。
    /// </summary>
    Task ShowTextDialogAsync(string title, string body, string? hint = null);

    /// <summary>開くファイルダイアログ。キャンセルなら null。</summary>
    Task<string?> ShowOpenFileAsync(string title, string filter);

    /// <summary>保存ファイルダイアログ。キャンセルなら null。</summary>
    Task<string?> ShowSaveFileAsync(string title, string filter, string? defaultFileName = null);
}
