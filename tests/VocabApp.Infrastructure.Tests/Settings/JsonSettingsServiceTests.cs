using FluentAssertions;
using VocabApp.Core.Models;
using VocabApp.Infrastructure.Settings;
using Xunit;

namespace VocabApp.Infrastructure.Tests.Settings;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public JsonSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VocabAppTests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileMissing()
    {
        var svc = new JsonSettingsService(_settingsPath);
        await svc.LoadAsync();

        svc.Current.DefaultTestMode.Should().Be(TestMode.EnglishToJapanese);
        svc.Current.DefaultTestRange.Should().Be(TestRange.All);
        svc.Current.DefaultTestCount.Should().Be(10);
    }

    [Fact]
    public async Task UpdateAsync_PersistsAndRaisesEvent()
    {
        var svc = new JsonSettingsService(_settingsPath);
        await svc.LoadAsync();

        var changed = 0;
        svc.SettingsChanged += (_, _) => changed++;

        await svc.UpdateAsync(s =>
        {
            s.DefaultTestMode = TestMode.MultipleChoiceEnglishToJapanese;
            s.DefaultTestCount = 50;
        });

        changed.Should().Be(1);

        // 別インスタンスで読み込んでも値が残っている
        var svc2 = new JsonSettingsService(_settingsPath);
        await svc2.LoadAsync();
        svc2.Current.DefaultTestMode.Should().Be(TestMode.MultipleChoiceEnglishToJapanese);
        svc2.Current.DefaultTestCount.Should().Be(50);
    }

    [Fact]
    public async Task LoadAsync_FallsBackToDefaults_OnCorruptFile()
    {
        await File.WriteAllTextAsync(_settingsPath, "this is not json");

        var svc = new JsonSettingsService(_settingsPath);
        await svc.LoadAsync();

        svc.Current.DefaultTestMode.Should().Be(TestMode.EnglishToJapanese);
    }
}
