using System.Text.Json;
using System.Text.Json.Serialization;
using VocabApp.Core.Services;

namespace VocabApp.Infrastructure.Settings;

public class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public JsonSettingsService(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            Current = new AppSettings();
            return;
        }
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            Current = loaded ?? new AppSettings();
        }
        catch
        {
            // 設定ファイルが壊れていても起動できるようデフォルトに戻す。
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, cancellationToken);
    }

    public async Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        mutate(Current);
        await SaveAsync(cancellationToken);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
