using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class JsonSettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_returns_defaults_when_file_does_not_exist()
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);

        AppSettings settings = await service.LoadAsync();

        Assert.Null(settings.FfmpegPath);
        Assert.Null(settings.FfprobePath);
        Assert.Equal(ExportMode.FastCopy, settings.LastExportMode);
    }

    [Fact]
    public async Task SaveAsync_persists_configured_tool_paths_and_output_directory()
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);
        var expected = new AppSettings
        {
            FfmpegPath = @"C:\Tools\ffmpeg.exe",
            FfprobePath = @"C:\Tools\ffprobe.exe",
            OutputDirectory = @"D:\Exports",
            LastExportMode = ExportMode.Reencode,
            LastCodecFamily = CodecFamily.H265,
            LastEncoderKind = EncoderKind.Nvenc,
            LastBitrateMode = BitrateMode.TargetSize,
            LastVideoBitrateKbps = 4500,
            LastTargetSizeMegabytes = 80.5,
            LastQualityValue = 21,
            NormalizeAudio = true,
            ConvertHdrToSdr = true,
            AdditionalFfmpegArguments = "-preset slow -movflags +faststart",
            Fade = new FadeSettings
            {
                VideoFadeIn = true,
                VideoFadeOut = true,
                AudioFadeIn = true,
                AudioFadeOut = true,
                DurationSeconds = 1.25,
            },
        };

        await service.SaveAsync(expected);
        AppSettings actual = await service.LoadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LoadAsync_treats_null_fade_settings_as_defaults()
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(service.SettingsFilePath, """{"fade":null}""");

        AppSettings settings = await service.LoadAsync();

        Assert.Equal(new FadeSettings(), settings.Fade);
    }

    [Fact]
    public async Task LoadAsync_migrates_legacy_audio_normalize_mode_to_fast_copy_setting()
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(service.SettingsFilePath, """{"lastExportMode":"AudioNormalize"}""");

        AppSettings settings = await service.LoadAsync();

        Assert.Equal(ExportMode.FastCopy, settings.LastExportMode);
        Assert.True(settings.NormalizeAudio);
    }

    [Theory]
    [InlineData("{ not-json")]
    [InlineData("")]
    public async Task LoadAsync_recovers_defaults_and_preserves_invalid_settings_for_diagnosis(string contents)
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);
        await File.WriteAllTextAsync(service.SettingsFilePath, contents);

        AppSettings settings = await service.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
        Assert.False(File.Exists(service.SettingsFilePath));
        string backupPath = Assert.Single(Directory.GetFiles(directory, "settings.corrupt.*.json"));
        Assert.Equal(contents, await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task LoadAsync_returns_defaults_without_leaking_when_settings_file_is_locked()
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);
        await File.WriteAllTextAsync(service.SettingsFilePath, "{}");
        await using FileStream lockedFile = new(
            service.SettingsFilePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        AppSettings settings = await service.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
        Assert.True(File.Exists(service.SettingsFilePath));
        Assert.Single(Directory.GetFiles(directory, "settings.load-error.*.txt"));
    }

    [Fact]
    public async Task SaveAsync_replaces_existing_settings_and_leaves_no_temporary_files()
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);
        await service.SaveAsync(new AppSettings { FfmpegPath = "old.exe" });

        var replacement = new AppSettings { FfmpegPath = "new.exe", OutputDirectory = @"D:\Exports" };
        await service.SaveAsync(replacement);

        Assert.Equal(replacement, await service.LoadAsync());
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task SaveAsync_cleans_temporary_file_when_serialization_is_canceled()
    {
        string directory = CreateTempDirectory();
        var service = new JsonSettingsService(directory);
        var original = new AppSettings { FfmpegPath = "original.exe" };
        await service.SaveAsync(original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.SaveAsync(new AppSettings { FfmpegPath = "replacement.exe" }, cancellation.Token));

        Assert.Equal(original, await service.LoadAsync());
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
