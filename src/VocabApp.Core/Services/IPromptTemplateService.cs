namespace VocabApp.Core.Services;

public record VocabularyPromptRequest(string Theme, int Count, string? Level = null);

public interface IPromptTemplateService
{
    /// <summary>
    /// 外部 LLM (Gemini CLI, ChatGPT 等) に渡して CSV 形式で単語を生成させる
    /// ためのプロンプト本文を組み立てる。
    /// </summary>
    string BuildVocabularyPrompt(VocabularyPromptRequest request);
}
