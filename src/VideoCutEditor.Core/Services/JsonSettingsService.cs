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

        AppSettings? settings;
        try
        {
            await using FileStream stream = File.OpenRead(SettingsFilePath);
            settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                SerializerOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            PreserveInvalidSettings(exception);
            return new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            WriteLoadErrorDiagnostic(exception);
            return new AppSettings();
        }

        if (settings is null)
        {
            PreserveInvalidSettings(new JsonException("The settings file contained no JSON value."));
            return new AppSettings();
        }

        AppSettings normalizedSettings = settings.Fade is null
            ? settings with { Fade = new FadeSettings() }
            : settings;

        return normalizedSettings.LastExportMode == ExportMode.AudioNormalize
            ? normalizedSettings with { LastExportMode = ExportMode.FastCopy, NormalizeAudio = true }
            : normalizedSettings;
    }

    public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(settingsDirectory);
        string temporaryPath = Path.Combine(
            settingsDirectory,
            $"{Path.GetFileName(SettingsFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, SettingsFilePath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private void PreserveInvalidSettings(Exception exception)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        string backupPath = Path.Combine(
            settingsDirectory,
            $"settings.corrupt.{timestamp}.{Guid.NewGuid():N}.json");

        try
        {
            File.Move(SettingsFilePath, backupPath);
        }
        catch (Exception archiveException) when (archiveException is IOException or UnauthorizedAccessException)
        {
            WriteLoadErrorDiagnostic(new AggregateException(exception, archiveException));
        }
    }

    private void WriteLoadErrorDiagnostic(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(settingsDirectory);
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            string diagnosticPath = Path.Combine(
                settingsDirectory,
                $"settings.load-error.{timestamp}.{Guid.NewGuid():N}.txt");
            File.WriteAllText(
                diagnosticPath,
                $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}{exception}");
        }
        catch (Exception diagnosticException) when (diagnosticException is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
