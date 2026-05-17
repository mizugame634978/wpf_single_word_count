using System.Text;

namespace VocabApp.Core.Utilities;

public static class ExceptionFormatter
{
    /// <summary>
    /// 例外を「型名: メッセージ」を InnerException までインデント表示で連ねる形式で文字列化する。
    /// </summary>
    public static string Format(Exception ex)
    {
        var sb = new StringBuilder();
        var current = ex;
        var indent = string.Empty;
        while (current is not null)
        {
            sb.Append(indent);
            sb.Append(current.GetType().Name);
            sb.Append(": ");
            sb.AppendLine(current.Message);
            indent += "  ↳ ";
            current = current.InnerException;
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 表示用メッセージ + 最深部までのスタックトレースを返す (デバッグ用)。
    /// </summary>
    public static string FormatWithStack(Exception ex)
    {
        var summary = Format(ex);
        var deepest = ex;
        while (deepest.InnerException is not null)
        {
            deepest = deepest.InnerException;
        }
        return $"{summary}\n\nスタックトレース:\n{deepest.StackTrace}";
    }
}
