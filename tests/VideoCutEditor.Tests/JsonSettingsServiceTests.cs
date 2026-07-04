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
        };

        await service.SaveAsync(expected);
        AppSettings actual = await service.LoadAsync();

        Assert.Equal(expected, actual);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
