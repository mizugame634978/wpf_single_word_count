using System.Text;

namespace VocabApp.Core.Services;

public class PromptTemplateService : IPromptTemplateService
{
    public string BuildVocabularyPrompt(VocabularyPromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Theme))
        {
            throw new ArgumentException("Theme must not be empty.", nameof(request));
        }
        if (request.Count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Count must be positive.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("あなたは英語教材作成のアシスタントです。");
        sb.AppendLine("以下のテーマと条件に従って英単語を生成し、指定された CSV 形式のみを");
        sb.AppendLine("出力してください。前後に説明文・コードフェンス・空行を入れないでください。");
        sb.AppendLine();
        sb.AppendLine($"テーマ: {request.Theme.Trim()}");
        sb.AppendLine($"件数: {request.Count}");
        if (!string.IsNullOrWhiteSpace(request.Level))
        {
            sb.AppendLine($"レベル: {request.Level.Trim()}");
        }
        sb.AppendLine();
        sb.AppendLine("CSV 形式 (1 行目はヘッダ固定):");
        sb.AppendLine("word,meaning,part_of_speech,example,tags,notes");
        sb.AppendLine("- word は英単語 1 語または短いフレーズ");
        sb.AppendLine("- meaning は日本語。複数訳は ; で区切る");
        sb.AppendLine("- part_of_speech は noun / verb / adj / adv / phrase などの短縮形");
        sb.AppendLine("- example は英文 1 文 (省略可。省略時は空欄)");
        sb.AppendLine("- tags はセル内で ; 区切り (例: \"toeic;business\")");
        sb.AppendLine("- notes は学習者向けメモ (省略可)");
        sb.AppendLine("- カンマや引用符を含む値は \" でクォートする (RFC 4180)");
        return sb.ToString();
    }
}
