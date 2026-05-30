using System.Security.Cryptography;
using System.Text;
using VocabApp.Core.Services;

namespace VocabApp.Infrastructure.Security;

/// <summary>
/// Windows DPAPI (CurrentUser スコープ) でシークレットを暗号化する実装。
/// 暗号化されたバイト列は Base64 文字列にして JSON 等へ格納できる形にする。
/// 同一ユーザの同一マシンでのみ復号できる。
/// </summary>
public class DpapiSecretProtector : ISecretProtector
{
    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return null;
        }
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return null;
        }
        try
        {
            var bytes = Convert.FromBase64String(ciphertext);
            var plaintextBytes = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception)
        {
            // 別ユーザの暗号文・破損・形式不正など。null を返して呼び出し側に「キー未設定」扱いさせる。
            return null;
        }
    }
}
