namespace VocabApp.Core.Services;

/// <summary>
/// シークレット (API キー等) を安全に保存・復元するためのインタフェース。
/// 実装は WPF 配下では DPAPI ベース (<c>System.Security.Cryptography.ProtectedData</c>)、
/// テスト用には平文を返す実装を差し替えて使う。
/// </summary>
public interface ISecretProtector
{
    /// <summary>平文 → 暗号化された Base64 文字列。空入力は null を返す。</summary>
    string? Protect(string? plaintext);

    /// <summary>暗号化された Base64 文字列 → 平文。空入力や復号失敗時は null を返す。</summary>
    string? Unprotect(string? ciphertext);
}
