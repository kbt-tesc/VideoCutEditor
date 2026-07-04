using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FfmpegToolPathServiceTests
{
    [Fact]
    public void Resolve_uses_configured_existing_paths_before_path_lookup()
    {
        string configuredDirectory = CreateTempDirectory();
        string pathDirectory = CreateTempDirectory();
        string configuredFfmpeg = CreateFile(configuredDirectory, "ffmpeg.exe");
        string configuredFfprobe = CreateFile(configuredDirectory, "ffprobe.exe");
        CreateFile(pathDirectory, "ffmpeg.exe");
        CreateFile(pathDirectory, "ffprobe.exe");
        var service = new FfmpegToolPathService(pathDirectory);

        FfmpegToolPaths paths = service.Resolve(new AppSettings
        {
            FfmpegPath = configuredFfmpeg,
            FfprobePath = configuredFfprobe,
        });

        Assert.Equal(configuredFfmpeg, paths.FfmpegPath);
        Assert.Equal(configuredFfprobe, paths.FfprobePath);
    }

    [Fact]
    public void Resolve_falls_back_to_path_when_settings_are_empty()
    {
        string directory = CreateTempDirectory();
        string ffmpeg = CreateFile(directory, "ffmpeg.exe");
        string ffprobe = CreateFile(directory, "ffprobe.exe");
        var service = new FfmpegToolPathService(directory);

        FfmpegToolPaths paths = service.Resolve(new AppSettings());

        Assert.Equal(ffmpeg, paths.FfmpegPath);
        Assert.Equal(ffprobe, paths.FfprobePath);
    }

    [Fact]
    public void Resolve_falls_back_to_path_when_configured_file_is_missing()
    {
        string directory = CreateTempDirectory();
        string ffmpeg = CreateFile(directory, "ffmpeg.exe");
        string ffprobe = CreateFile(directory, "ffprobe.exe");
        var service = new FfmpegToolPathService(directory);

        FfmpegToolPaths paths = service.Resolve(new AppSettings
        {
            FfmpegPath = @"C:\Missing\ffmpeg.exe",
            FfprobePath = @"C:\Missing\ffprobe.exe",
        });

        Assert.Equal(ffmpeg, paths.FfmpegPath);
        Assert.Equal(ffprobe, paths.FfprobePath);
    }

    [Fact]
    public void FindOnPath_returns_first_matching_directory()
    {
        string firstDirectory = CreateTempDirectory();
        string secondDirectory = CreateTempDirectory();
        string expected = CreateFile(firstDirectory, "ffmpeg.exe");
        CreateFile(secondDirectory, "ffmpeg.exe");
        string pathValue = string.Join(Path.PathSeparator, firstDirectory, secondDirectory);
        var service = new FfmpegToolPathService(pathValue);

        string? actual = service.FindOnPath("ffmpeg");

        Assert.Equal(expected, actual);
    }

    private static string CreateFile(string directory, string fileName)
    {
        string filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, string.Empty);
        return filePath;
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
