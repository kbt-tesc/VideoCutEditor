using System.Text.Json;
using System.Text.Json.Serialization;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string settingsDirectory;

    public JsonSettingsService(string? settingsDirectory = null)
    {
        this.settingsDirectory = settingsDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoCutEditor");
        SettingsFilePath = Path.Combine(this.settingsDirectory, "settings.json");
    }

    public string SettingsFilePath { get; }

    public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettings();
        }

        await using FileStream stream = File.OpenRead(SettingsFilePath);
        AppSettings? settings = await JsonSerializer.DeserializeAsync<AppSettings>(
            stream,
            SerializerOptions,
            cancellationToken);

        return settings ?? new AppSettings();
    }

    public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(settingsDirectory);

        await using FileStream stream = File.Create(SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
