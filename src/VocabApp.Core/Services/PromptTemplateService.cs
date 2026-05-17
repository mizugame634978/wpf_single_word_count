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
        sb.AppendLine("あなたは英語教材作成のアシスタントです。日本人英語学習者向けに、");
        sb.AppendLine("以下のテーマと条件で英単語を生成してください。出力は指定された CSV のみとし、");
        sb.AppendLine("前後に説明文・コードフェンス・空行を入れないでください。");
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
        sb.AppendLine();
        sb.AppendLine("列ごとの規則:");
        sb.AppendLine("- word: 英単語 1 語または短いフレーズ");
        sb.AppendLine("- meaning: **日本語固有の単語による和訳**を入れる。");
        sb.AppendLine("    重要: 英単語の音をそのままカタカナで書く音訳は不可。");
        sb.AppendLine("      ✗ dictionary → ディクショナリ");
        sb.AppendLine("      ✗ policy     → ポリシー");
        sb.AppendLine("      ✗ resource   → リソース");
        sb.AppendLine("      ✓ dictionary → 辞書; 辞典");
        sb.AppendLine("      ✓ policy     → 方針; 政策");
        sb.AppendLine("      ✓ resource   → 資源; 資材");
        sb.AppendLine("    日本語として完全に定着しているカタカナ語 (コンピュータ, インターネット,");
        sb.AppendLine("    アルゴリズム 等) はそのまま使ってよい。判断に迷ったらまず漢字・ひらがなで");
        sb.AppendLine("    表す訳を選ぶ。");
        sb.AppendLine("    複数訳がある場合は ; (セミコロン) で区切って 2〜3 個列挙する。");
        sb.AppendLine("- part_of_speech: noun / verb / adj / adv / phrase などの短縮形");
        sb.AppendLine("- example: 英文 1 文。単語の典型的な使い方が分かるもの");
        sb.AppendLine("- tags: セル内で ; 区切り (例: \"toeic;business\")");
        sb.AppendLine("- notes: **日本語で 1 文の短い解説を必ず入れる。**");
        sb.AppendLine("    和訳だけでは伝わらない意味・ニュアンス・使い分け・典型的な文脈を");
        sb.AppendLine("    補足する。学習者がその単語を初めて見る前提で書く。");
        sb.AppendLine();
        sb.AppendLine("CSV 出力の注意:");
        sb.AppendLine("- カンマ , や引用符 \" を含むセルは \" でクォートし、内部の \" は \"\" にエスケープ (RFC 4180)");
        sb.AppendLine("- 1 行 = 1 単語、改行をセル内に入れない");
        sb.AppendLine();
        sb.AppendLine("良い行の例 (この形式に倣う):");
        sb.AppendLine("dictionary,辞書; 辞典,noun,Please pass me the English-Japanese dictionary.,toeic;basic,意味や使い方を調べるための本やアプリ。");
        return sb.ToString();
    }
}
