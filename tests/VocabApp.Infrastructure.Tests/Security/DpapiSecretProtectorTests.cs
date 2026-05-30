using FluentAssertions;
using VocabApp.Infrastructure.Security;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Security;

public class DpapiSecretProtectorTests
{
    private readonly DpapiSecretProtector _protector = new();

    [Fact]
    public void RoundTrip_RecoversPlaintext_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            // DPAPI は Windows のみ。CI 上の Linux runner ではスキップ扱い。
            return;
        }

        var encrypted = _protector.Protect("my-api-key");
        encrypted.Should().NotBeNull();
        encrypted.Should().NotBe("my-api-key");

        _protector.Unprotect(encrypted).Should().Be("my-api-key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Protect_NullOrEmpty_ReturnsNull(string? input)
    {
        _protector.Protect(input).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Unprotect_NullOrEmpty_ReturnsNull(string? input)
    {
        _protector.Unprotect(input).Should().BeNull();
    }

    [Fact]
    public void Unprotect_ReturnsNull_OnInvalidCiphertext()
    {
        // 形式不正の Base64 / 別ユーザ暗号 / 壊れたデータでも例外を投げず null を返す。
        _protector.Unprotect("not-base64-at-all").Should().BeNull();
        _protector.Unprotect("dGVzdA==").Should().BeNull(); // valid base64 だが DPAPI 復号失敗
    }
}
