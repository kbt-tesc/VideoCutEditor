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
            LastExportMode = ExportMode.AudioNormalize,
            LastCodecFamily = CodecFamily.H265,
            LastEncoderKind = EncoderKind.Nvenc,
            LastBitrateMode = BitrateMode.TargetSize,
            LastVideoBitrateKbps = 4500,
            LastTargetSizeMegabytes = 80.5,
            LastQualityValue = 21,
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

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
