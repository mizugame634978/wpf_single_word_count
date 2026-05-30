using VocabApp.Core.Models;

namespace VocabApp.Core.Services;

public class AppSettings
{
    public TestMode DefaultTestMode { get; set; } = TestMode.EnglishToJapanese;
    public TestRange DefaultTestRange { get; set; } = TestRange.All;
    public int DefaultTestCount { get; set; } = 10;

    /// <summary>
    /// Gemini API キーを DPAPI で暗号化して Base64 化したもの。
    /// 平文を直接入れてはいけない。読み書きは <see cref="ISecretProtector"/> 経由で行う。
    /// </summary>
    public string? GeminiApiKeyEncrypted { get; set; }
}

public interface ISettingsService
{
    AppSettings Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>設定が更新されて保存された後に発火する。</summary>
    event EventHandler? SettingsChanged;

    /// <summary>変更を適用する。コールバック内で <see cref="AppSettings"/> を変更し、
    /// その後ファイルへ保存 & イベント発火する。</summary>
    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default);
}
